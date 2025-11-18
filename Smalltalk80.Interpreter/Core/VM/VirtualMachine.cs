using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.VM.Interpreter;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Attributes;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// The Smalltalk-80 Virtual Machine - orchestrates bytecode execution and implements IPrimitiveContext.
///
/// Architecture (after Phase 2 decomposition):
/// - VirtualMachine.cs: Interpreter loop, NLR state machine, IPrimitiveContext, component coordination
/// - VirtualMachine.MessageSending.cs: SendMessage, ConsumeCallOutcome, error diagnostics
/// - VirtualMachine.VariableAccess.cs: Unified temp frame variable access, method literal helpers
///
/// Extracted components:
/// - IStackManager → StackManager.cs: Stack operations (Push, Pop, Peek)
/// - IContextManager → ContextManager.cs: Context lifecycle and activation
/// - IMessageDispatcher → MessageDispatcher.cs: Method lookup in class hierarchy
/// - IBytecodeInterpreter → BytecodeInterpreter.cs: Bytecode dispatch and execution
/// - IVMDebugger → VMDebugger.cs: Debugging and diagnostic output
///
/// Uses ISmalltalkHeapManager for object storage and LayoutResolver for dynamic field access.
/// Consistently uses SmalltalkOOP for ALL object references to avoid OOP↔int conversions.
///
/// RUNTIME ONLY: This class must ONLY access the Smalltalk heap via ISmalltalkHeapManager.
/// NO access to cold start proxy data (_symbols[], ColdStartBundle, unfrozen registries).
/// </summary>
[RuntimeOnly(Reason = "Executes Smalltalk bytecode at runtime, must use heap-only access")]
public partial class VirtualMachine : IPrimitiveContext, IBytecodeContext, IVMDebugHelpers, ICacheManager, IPrimitiveMessageSender
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly RuntimeHeapAccessor _runtimeHeapAccess;
    private readonly LayoutResolver _layoutResolver;
    private readonly VMMetadata _metadata;
    private readonly IPlatformBridge? _platformBridge;
    private readonly IStackManager _stackManager;
    private readonly IContextManager _contextManager;
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly IBytecodeInterpreter _bytecodeInterpreter;
    private readonly IVMDebugger _debugger;
    private readonly IPrimitiveRegistry _primitiveRegistry;

    // Verbose debug mode for current init expression (controlled by Program.cs)
    private bool _verboseDebugMode = false;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    // Instruction execution counter for debugging infinite loops
    private long _instructionCount = 0;
    private const long MAX_INSTRUCTIONS = 500000;  // Halt at 500K instructions (infinite loop threshold)

    /// <summary>Gets the total number of instructions executed by this VM instance.</summary>
    public long InstructionCount => _instructionCount;

    /// <summary>Resets the instruction counter to zero. Used before executing init expressions.</summary>
    public void ResetInstructionCount() => _instructionCount = 0;

    // Trampoline state - used to avoid recursive ExecuteBytecodeLoop calls
    // (CallOutcome, CallOutcomeKind, InterpreterStatus moved to Interpreter/ExecutionState.cs)
    private SmalltalkOOP _returnValue;  // Return value from execution
    private SmalltalkOOP _initialContextOOP;  // Starting context for this execution
    private bool _contextSwitchPending;  // Signal that a context switch occurred (for trampoline)
    private bool _contextSwitchedByPrimitive;  // Primitive created new context (e.g., block invocation)

    // Non-local return state (replaces exception-based unwinding)
    private bool _nonLocalReturnPending;  // NLR in progress
    private SmalltalkOOP _nlrValue;  // Value to return
    private SmalltalkOOP _nlrTargetContext;  // Context to unwind to

    // Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _arithmeticSelectorOOPs;

    // Cached selector OOPs for special send bytecodes (0xC0-0xCF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _specialSelectorOOPs;

    // Blue Book architectural constants are defined in VMConstants.cs
    // All context field indices are now static constants shared across the VM

    // Verbose debugging support
    public bool VerboseDebug { get; set; } = false;
    private readonly Dictionary<SmalltalkOOP, string> _methodSources = new();

    // Selector trace mechanism - shows nested method calls and returns
    private bool _selectorTraceEnabled = false;
    
    // Public property to enable bytecode tracing from outside
    public bool EnableBytecodeTracing
    {
        get => _selectorTraceEnabled;
        set
        {
            _selectorTraceEnabled = value;
            _bytecodeInstructionCounter = 0; // Reset counters when enabling
            _pushTraceCounter = 0;
        }
    }
    
    private int _bytecodeInstructionCounter = 0;
    private int _pushTraceCounter = 0;
    private int _selectorTraceDepth = 0;
    private readonly Stack<(string className, string selector, int depth)> _selectorTraceStack = new();

    public VirtualMachine(ISmalltalkHeapManager heapManager, LayoutResolver layoutResolver, VMMetadata metadata, IPlatformBridge? platformBridge = null)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _runtimeHeapAccess = new RuntimeHeapAccessor(heapManager);
        RuntimeSpecialObjects.Instance.Initialize(_runtimeHeapAccess);
        _metadata = metadata;
        _platformBridge = platformBridge;

        _contextManager = new ContextManager(
            heapManager,
            metadata,
            _layoutResolver,
            this // IVMHelpers
        );

        _stackManager = new StackManager(
            heapManager,
            metadata,
            this, // IVMHelpers
            () => _contextManager.ActiveContext,
            () => _selectorTraceEnabled,
            () => _pushTraceCounter,
            () => _pushTraceCounter++
        );

        _messageDispatcher = new MessageDispatcher(
            heapManager,
            this // IVMHelpers
        );

        _bytecodeInterpreter = new BytecodeInterpreter(
            heapManager,
            this, // IBytecodeContext
            _runtimeHeapAccess,
            metadata
        );

        _debugger = new VMDebugger(
            heapManager,
            _contextManager,
            this // IVMDebugHelpers
        );

        // Initialize primitive registry with new IPrimitive implementations
        // All 110 primitives migrated to IPrimitive pattern (2025-11-18)
        _primitiveRegistry = Primitives.PrimitiveRegistry.CreateWithNewPrimitives();
    }

    /// <summary>
    /// Initializes the selector OOP caches by looking up selectors in the Smalltalk heap.
    /// Must be called after cold start completes and all symbols are interned.
    /// </summary>
    public void InitializeSelectorCaches()
    {
        // Initialize arithmetic selector OOPs (bytecodes 0xB0-0xBF)
        string[] arithmeticSelectors = new[]
        {
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
        };

        _arithmeticSelectorOOPs = new SmalltalkOOP[16];
        for (int i = 0; i < arithmeticSelectors.Length; i++)
        {
            _arithmeticSelectorOOPs[i] = LookupSymbolInHeap(arithmeticSelectors[i]);
        }

        // Initialize special selector OOPs (bytecodes 0xC0-0xCF)
        string[] specialSelectors = new[]
        {
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        };

        _specialSelectorOOPs = new SmalltalkOOP[16];
        for (int i = 0; i < specialSelectors.Length; i++)
        {
            _specialSelectorOOPs[i] = LookupSymbolInHeap(specialSelectors[i]);
        }

        // Pass selector caches to bytecode interpreter
        _bytecodeInterpreter.InitializeSelectorCaches(_arithmeticSelectorOOPs, _specialSelectorOOPs);
    }

    /// <summary>
    /// Looks up a symbol in the Smalltalk heap by searching USTable.
    /// CRITICAL: Uses heap-only access, no C# proxy data.
    /// </summary>
    private SmalltalkOOP LookupSymbolInHeap(string symbolName)
    {
        // Get USTable OOP
        // NOTE: During VM initialization (cold start), GlobalRegistry is valid and contains USTable
        // This is acceptable because VM initialization happens during cold start, not runtime
        if (!GlobalRegistry.TryGet("USTable", out var ustableOOP))
        {
            throw new InvalidOperationException("USTable not found - cannot initialize selector caches. " +
                "This method should only be called during cold start initialization.");
        }

        // USTable is an Array in the Smalltalk heap containing Symbol OOPs
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Convert search string to bytes (Latin-1 encoding)
        byte[] searchBytes = System.Text.Encoding.Latin1.GetBytes(symbolName);

        // Search through USTable (Array-of-Arrays structure for collision chaining)
        for (int i = 0; i < ustableSize; i++)
        {
            var bucketOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil buckets (empty slots)
            if (bucketOOP == nilOOP)
                continue;

            // Bucket is an Array of Symbols - iterate through it
            int bucketSize = _heapManager.GetIndexedSize(bucketOOP);
            for (int k = 0; k < bucketSize; k++)
            {
                var symbolOOP = _heapManager.GetIndexedOOP(bucketOOP, k);

                // Skip nil slots (shouldn't happen, but defensive)
                if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                    continue;

                // Compare Symbol bytes in heap
                int symbolLength = _heapManager.GetIndexedSize(symbolOOP);
                if (symbolLength == searchBytes.Length)
                {
                    bool match = true;
                    for (int j = 0; j < searchBytes.Length; j++)
                    {
                        byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                        if (symbolByte != searchBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return symbolOOP;
                }
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in USTable during selector cache initialization");
    }

    /// <summary>
    /// Enables or disables verbose debug output for the current init expression.
    /// Used by Program.cs to reduce noise from successful init expressions.
    /// </summary>
    public void SetVerboseDebugMode(bool enabled)
    {
        _verboseDebugMode = enabled;
        _heapManager.VerboseDebugMode = enabled;
        _bytecodeInterpreter.VerboseDebug = enabled;
    }

    /// <summary>
    /// Enables selector trace for debugging - shows all method calls and returns with nesting.
    /// </summary>
    public void EnableSelectorTrace()
    {
        _selectorTraceEnabled = true;
        _selectorTraceDepth = 0;
        _selectorTraceStack.Clear();
    }

    /// <summary>
    /// Disables selector trace.
    /// </summary>
    public void DisableSelectorTrace()
    {
        _selectorTraceEnabled = false;
        _selectorTraceDepth = 0;
        _selectorTraceStack.Clear();
    }

    /// <summary>
    /// Prints a trace message with proper indentation based on call depth.
    /// </summary>
    private void TraceMessage(string message)
    {
        if (_selectorTraceEnabled)
        {
            string indent = new string(' ', _selectorTraceDepth * 2);
            // Console.WriteLine($"{indent}{message}");
        }
    }

    /// <summary>
    /// Executes a method with the given receiver and arguments.
    /// Returns the result of the execution.
    /// </summary>
    public SmalltalkOOP Execute(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments)
    {
        // Parse method header to check for quick forms
        var header = ParseMethodHeader(methodOOP);

        // Quick-return-self optimization (flags=5)
        if (header.IsQuickReturnSelf)
        {
            return receiverOOP;
        }

        // Quick-return-field optimization (flags=6)
        if (header.IsQuickReturnField)
        {
            var fieldOOP = _heapManager.GetInstanceVariable(receiverOOP, header.QuickReturnFieldIndex);
            return fieldOOP;
        }

        // Create a MethodContext for this execution
        _contextManager.SetActiveContext(_contextManager.CreateMethodContext(methodOOP, receiverOOP, arguments));

        // Execute bytecodes using trampoline pattern to prevent stack overflow
        return InterpreterLoop();
    }

    public SmalltalkOOP ExecuteExistingContext(SmalltalkOOP contextOOP)
    {
        // Reset context depth counter for fresh execution
        // Each init expression should start with depth=0
        _contextManager.ResetContextDepth();

        _contextManager.SetActiveContext(contextOOP);
        var result = InterpreterLoop();
        // Console.WriteLine($"[ExecuteExistingContext] Finished with result {result}");
        return result;
    }

    /// <summary>
    /// Top-level interpreter loop using trampoline pattern.
    /// Prevents .NET stack overflow by avoiding recursive ExecuteBytecodeLoop calls.
    /// This implements the Blue Book's single-loop interpreter design.
    /// </summary>
    private SmalltalkOOP InterpreterLoop()
    {
        // Save initial context - we return when we get back to this context
        _initialContextOOP = _contextManager.ActiveContext;

        while (true)
        {
            // Execute bytecodes in current context
            InterpreterStatus status = ExecuteBytecodeStep();

            switch (status)
            {
                case InterpreterStatus.Continue:
                    // Context switched (message send or return) - keep executing
                    continue;

                case InterpreterStatus.Return:
                    // Execution complete - return the value
                    return _returnValue;

                case InterpreterStatus.Yield:
                    // Temporary pause (not currently used)
                    throw new NotImplementedException("Yield not implemented");

                default:
                    throw new InvalidOperationException($"Unknown interpreter status: {status}");
            }
        }
    }

    /// <summary>
    /// Executes bytecodes in the current context until a context switch or return.
    /// Returns InterpreterStatus indicating whether to continue or return.
    /// </summary>
    private InterpreterStatus ExecuteBytecodeStep()
    {
        // if (_verboseDebugMode)
        //     Console.WriteLine($"\n[ExecuteBytecodeStep] START in context {_contextManager.ActiveContext}");

        // If NLR is pending, continue unwinding instead of executing bytecodes
        if (_nonLocalReturnPending)
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[ExecuteBytecodeStep] NLR pending, continuing unwinding");
            return HandleReturn(_nlrValue);
        }

        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Check if this is a BlockContext - they retrieve method from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(_contextManager.ActiveContext);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        SmalltalkOOP methodOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: bytecode stored inline in parent method, receiver from home MethodContext
            // Blue Book design: BlockContext.startpc points to bytecode offset in parent method
            // With nested block support: traverse home chain to find the ultimate MethodContext
            var methodContextOOP = _contextManager.FindMethodContext(_contextManager.ActiveContext);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            // MethodContext: method and receiver in instance variables
            methodOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, receiverIndex);
        }

        // Diagnostic: show stack pointer
        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, spIndex);
        int sp = spOOP.AsSmallInteger();

        // if (_verboseDebugMode)
        // {
        //     Console.WriteLine($"  methodOOP={methodOOP}, receiverOOP={receiverOOP}");
        //     Console.WriteLine($"  receiverClass={GetClassName(GetClass(receiverOOP))}, sp={sp}");
        // }

        // Validate that methodOOP is a CompiledMethod (PointerIndexed), not a bytecode array (ByteIndexed)
        var methodFormat = _heapManager.GetObjectFormat(methodOOP);
        if (methodFormat != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            var contextClass = GetClassName(GetClass(_contextManager.ActiveContext));
            throw new InvalidOperationException(
                $"ExecuteBytecodeStep: method field in context contains non-PointerIndexed object!\n" +
                $"  Context: {_contextManager.ActiveContext}, contextClass={contextClass}\n" +
                $"  Method field (index {methodIndex}): {methodOOP}\n" +
                $"  Method format: {methodFormat} (expected PointerIndexed)\n" +
                $"  Method class: {className}\n" +
                $"  Method localId: {localId}\n" +
                $"  This suggests the bytecode array was stored in the context instead of the CompiledMethod.");
        }

        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

            // Update IP
            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Increment and check instruction counter (one count per bytecode executed)
            _instructionCount++;
            
            // Report progress every 10K instructions during potential infinite loops (only when tracing is enabled)
            if (_selectorTraceEnabled && _instructionCount % 10000 == 0)
            {
                var methodLocalId = GetLocalId(methodOOP) ?? "unknown";
                var ctxReceiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, VMConstants.CONTEXT_RECEIVER_INDEX);
                var className = GetClassName(GetClass(ctxReceiverOOP));
                Console.WriteLine($"[{_instructionCount / 1000}K instructions] In: {className}>>{methodLocalId}, IP={ip-1}");
            }
            
            // Special trace around the critical range
            if (_selectorTraceEnabled && _instructionCount >= 2020 && _instructionCount <= 2045)
            {
                var methodLocalId = GetLocalId(methodOOP) ?? "unknown";
                Console.WriteLine($"[BYTECODE #{_instructionCount}] IP={ip-1}, opcode=0x{bytecode:X2}, method={methodLocalId}");
            }
            
            if (_instructionCount >= MAX_INSTRUCTIONS)
            {
                throw new InvalidOperationException($"Infinite loop detected: {_instructionCount} instructions exceeded threshold of {MAX_INSTRUCTIONS}");
            }

            // Execute the bytecode using the bytecode interpreter
            bool continueExecution = _bytecodeInterpreter.ExecuteBytecode(bytecode, methodOOP);

            if (!continueExecution)
            {
                // Check if this was a context switch or a method return
                if (_contextSwitchPending)
                {
                    // Context switch - continue execution in new context
                    _contextSwitchPending = false;
                    return InterpreterStatus.Continue;
                }

                // Method returned - handle the return
                // For non-local returns, the value was already popped and stored in _nlrValue
                SmalltalkOOP returnValue;
                if (_nonLocalReturnPending)
                {
                    // Non-local return: value already popped by RETURN_*_POP bytecode
                    returnValue = _nlrValue;
                    // if (_verboseDebugMode)
                    //     Console.WriteLine($"[ExecuteBytecodeStep] Non-local return pending, using _nlrValue: {returnValue}");
                }
                else
                {
                    // Normal return: pop the return value from stack
                    returnValue = Pop();
                    // if (_verboseDebugMode)
                    //     Console.WriteLine($"[ExecuteBytecodeStep] Method returned, popped value: {returnValue}");
                }
                return HandleReturn(returnValue);
            }

            // Re-read IP in case bytecode modified it (jumps)
            ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self
        return HandleReturn(receiverOOP);
    }

    /// <summary>
    /// Handles a method/block return. Switches back to sender context.
    /// Handles non-local returns by unwinding contexts until reaching target.
    /// Returns InterpreterStatus indicating whether to continue or complete.
    /// </summary>
    private InterpreterStatus HandleReturn(SmalltalkOOP returnValue)
    {
        // Declare common variables once for all code paths
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        SmalltalkOOP senderOOP;

        // Handle non-local returns (NLR)
        if (_nonLocalReturnPending)
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding from {_contextManager.ActiveContext} to target {_nlrTargetContext}, value={_nlrValue}");

            // Check if current context is the NLR target
            if (_contextManager.ActiveContext == _nlrTargetContext)
            {
                // if (_verboseDebugMode)
                //     Console.WriteLine($"[HandleReturn NLR] TARGET REACHED at {_contextManager.ActiveContext}");

                // Target reached - clear NLR state and push value
                _nonLocalReturnPending = false;
                SmalltalkOOP nlrValue = _nlrValue;
                _nlrValue = default;
                _nlrTargetContext = default;

                // Get sender to continue execution
                senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);

                // Add context to size-specific pool for reuse
                _contextManager.AddContextToPool(_contextManager.ActiveContext);

                // Decrement context depth
                _contextManager.DecrementContextDepth();

                // Check if target was the initial context
                if (senderOOP == _initialContextOOP || senderOOP == SpecialObjectsArray.Instance.Nil)
                {
                    _returnValue = nlrValue;
                    _contextManager.SetActiveContext(_initialContextOOP);
                    // if (_verboseDebugMode)
                    //     Console.WriteLine($"[HandleReturn NLR] Returning to initial context");
                    return InterpreterStatus.Return;
                }

                // Switch to sender and push NLR value
                _contextManager.SetActiveContext(senderOOP);
                Push(nlrValue);
                // if (_verboseDebugMode)
                //     Console.WriteLine($"[HandleReturn NLR] Pushed NLR value onto sender {senderOOP}");
                return InterpreterStatus.Continue;
            }

            // Not the target - continue unwinding (don't push value)
            senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding through {_contextManager.ActiveContext}, next sender={senderOOP}");

            // Add context to size-specific pool for reuse
            _contextManager.AddContextToPool(_contextManager.ActiveContext);

            // Decrement context depth
            _contextManager.DecrementContextDepth();

            // Check if we ran out of contexts (error case)
            if (senderOOP == SpecialObjectsArray.Instance.Nil)
            {
                throw new InvalidOperationException(
                    $"Non-local return target context {_nlrTargetContext} not found in sender chain. " +
                    $"Target may have already returned.");
            }

            // Continue unwinding
            _contextManager.SetActiveContext(senderOOP);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Switched to sender {_contextManager.ActiveContext}, continuing unwinding");
            return InterpreterStatus.Continue;
        }

        // Normal return (not NLR)
        senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);

        // if (_verboseDebugMode)
        // {
        //     Console.WriteLine($"[HandleReturn] Normal return from {_contextManager.ActiveContext} to {senderOOP}");
        //     Console.WriteLine($"  Return value: {returnValue} (SmallInt={returnValue.IsSmallInteger})");
        // }

        // DIAGNOSTIC: Track return values from hash/stringhash methods (disabled for quiet mode)
        // int methodIndex2 = VMConstants.CONTEXT_METHOD_INDEX;
        // var returningMethodOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, methodIndex2);
        // string? methodLocalId = GetLocalId(returningMethodOOP);
        // if (methodLocalId != null && (methodLocalId.Contains(">>hash") || methodLocalId.Contains(">>stringhash")))
        // {
        //     var className = returnValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(returnValue));
        //     Console.WriteLine($"\n[RETURN from {methodLocalId}] value={returnValue}, class={className}, isSmallInt={returnValue.IsSmallInteger}");
        // }


        // Selector trace: log method return
        if (_selectorTraceEnabled && _selectorTraceStack.Count > 0)
        {
            var (className, selector, depth) = _selectorTraceStack.Pop();
            _selectorTraceDepth = depth;  // Restore depth before decrement
            string returnValueStr = returnValue.IsSmallInteger
                ? returnValue.AsSmallInteger().ToString()
                : $"OOP({returnValue.RawValue})";
            TraceMessage($"← {className}>>{selector} => {returnValueStr}");
        }

        // Add context to size-specific pool for reuse
        _contextManager.AddContextToPool(_contextManager.ActiveContext);

        // Decrement context depth
        _contextManager.DecrementContextDepth();

        // Check if we're returning to the initial context
        if (senderOOP == _initialContextOOP || senderOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Execution complete - return to caller
            _returnValue = returnValue;
            _contextManager.SetActiveContext(_initialContextOOP);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn] Execution complete, returning to initial context");
            return InterpreterStatus.Return;
        }

        // Switch to sender context and push return value
        _contextManager.SetActiveContext(senderOOP);
        Push(returnValue);
        // if (_verboseDebugMode)
        //     Console.WriteLine($"  Pushed return value onto sender's stack");

        // Continue execution in sender context
        return InterpreterStatus.Continue;
    }

    /// <summary>
    /// Main bytecode execution loop (DEPRECATED - use InterpreterLoop instead).
    /// This method is kept for compatibility but will be phased out.
    /// </summary>
    private SmalltalkOOP ExecuteBytecodeLoop()
    {
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Check if this is a BlockContext - they retrieve method from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(_contextManager.ActiveContext);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        SmalltalkOOP methodOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: bytecode stored inline in parent method, receiver from home MethodContext
            // Blue Book design: BlockContext.startpc points to bytecode offset in parent method
            // With nested block support: traverse home chain to find the ultimate MethodContext
            var methodContextOOP = _contextManager.FindMethodContext(_contextManager.ActiveContext);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            // MethodContext: method and receiver in instance variables
            methodOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, receiverIndex);
        }

        // Diagnostic: Show method name when recursion depth > 10
        // if (_activeContextCount > 10 && _activeContextCount <= 15)  // Limit output
        // {
        //     var selectorOOP = GetMethodLiteral(methodOOP, 0);
        //     var selectorName = GetSymbolName(selectorOOP);
        //     var receiverClass = GetClass(receiverOOP);
        //     var className = GetClassName(receiverClass);
        //
        //     // Debug: show what literal[0] actually is
        //     string literalInfo = "";
        //     if (selectorOOP == SpecialObjectsArray.Instance.Nil)
        //     {
        //         literalInfo = " [literal[0]=nil]";
        //     }
        //     else if (selectorOOP.IsSmallInteger)
        //     {
        //         literalInfo = $" [literal[0]=SmallInteger({selectorOOP.AsSmallInteger()})]";
        //     }
        //     else
        //     {
        //         var litClass = GetClassName(GetClass(selectorOOP));
        //         literalInfo = $" [literal[0]={litClass}, name='{selectorName}']";
        //     }
        //
        //     Console.WriteLine($"[DEPTH={_activeContextCount}] {className}>>{selectorName}{literalInfo}");
        // }

        // Debug: Check what methodOOP we got
        if (methodOOP.IsSmallInteger)
        {
            var activeClassName = _heapManager.GetClassName(_heapManager.GetClassOOP(_contextManager.ActiveContext));
            var methodContextOOP = isBlockContext ? _contextManager.FindMethodContext(_contextManager.ActiveContext) : _contextManager.ActiveContext;
            var methodContextClassName = _heapManager.GetClassName(_heapManager.GetClassOOP(methodContextOOP));
            throw new InvalidOperationException(
                $"ExecuteBytecodeLoop: methodOOP is SmallInteger!\n" +
                $"  ActiveContext: {_contextManager.ActiveContext}, class={activeClassName}\n" +
                $"  isBlockContext: {isBlockContext}\n" +
                $"  methodContext: {methodContextOOP}, class={methodContextClassName}\n" +
                $"  methodIndex: {methodIndex}\n" +
                $"  methodOOP: {methodOOP} (SmallInteger={methodOOP.AsSmallInteger()})");
        }

        // Validate that methodOOP is a CompiledMethod (PointerIndexed), not a bytecode array (ByteIndexed)
        var methodFormat = _heapManager.GetObjectFormat(methodOOP);
        if (methodFormat != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            var contextClass = GetClassName(GetClass(_contextManager.ActiveContext));
            throw new InvalidOperationException(
                $"ExecuteBytecodeLoop: method field in context contains non-PointerIndexed object!\n" +
                $"  Context: {_contextManager.ActiveContext}, contextClass={contextClass}\n" +
                $"  Method field (index {methodIndex}): {methodOOP}\n" +
                $"  Method format: {methodFormat} (expected PointerIndexed)\n" +
                $"  Method class: {className}\n" +
                $"  Method localId: {localId}\n" +
                $"  This suggests the bytecode array was stored in the context instead of the CompiledMethod.");
        }
        // TODO: Implement GetMethodBytecodes in heap manager or add helper method
        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];
            
            // DEBUG: Trace first 100 bytecodes when tracing is enabled
            if (_selectorTraceEnabled && _bytecodeInstructionCounter < 100)
            {
                _bytecodeInstructionCounter++;
                
                // Peek ahead for extended opcodes
                string opcodeDesc = $"0x{bytecode:X2}";
                if (bytecode == 0x80 && ip < bytecodes.Length)
                {
                    byte subOp = bytecodes[ip];
                    string[] extNames = new[] {
                        "PUSH_IVAR", "STORE_IVAR", "POP_STORE_IVAR", "PUSH_TEMP", "STORE_TEMP", "POP_STORE_TEMP",
                        "PUSH_LITERAL", "PUSH_LITERAL_VAR", "STORE_LITERAL_VAR", "POP_STORE_LITERAL_VAR",
                        "SEND_0", "SEND_1", "SEND_2", "SEND_N", "SEND_SUPER_0", "SEND_SUPER_1",
                        "SEND_SUPER_2", "SEND_SUPER_N", "POP", "DUP", "JUMP_TRUE", "JUMP_FALSE",
                        "JUMP", "JUMP_LONG_TRUE", "JUMP_LONG_FALSE", "JUMP_LONG"
                    };
                    string extName = subOp < extNames.Length ? extNames[subOp] : $"UNKNOWN";
                    opcodeDesc = $"0x{bytecode:X2} 0x{subOp:X2} ({extName})";
                }
                else
                {
                    opcodeDesc = BytecodeDisassembler.GetOpcodeName(bytecode) ?? opcodeDesc;
                }
                
                Console.WriteLine($"[BYTECODE #{_bytecodeInstructionCounter}] IP={(ip-1)}, {opcodeDesc}");
            }

            // For extended opcodes, peek ahead and show the full instruction
            string opcodeName;
            string opcodeBytes;
            if (bytecode == 0x80 && ip < bytecodes.Length)
            {
                byte subOp = bytecodes[ip];
                string[] extendedNames = new[] {
                    "PUSH_IVAR", "STORE_IVAR", "POP_STORE_IVAR", "PUSH_TEMP", "STORE_TEMP", "POP_STORE_TEMP",
                    "PUSH_LITERAL", "PUSH_LITERAL_VAR", "STORE_LITERAL_VAR", "POP_STORE_LITERAL_VAR",
                    "SEND_0", "SEND_1", "SEND_2", "SEND_N", "SEND_SUPER_0", "SEND_SUPER_1",
                    "SEND_SUPER_2", "SEND_SUPER_N", "POP", "DUP", "JUMP_TRUE", "JUMP_FALSE",
                    "JUMP", "JUMP_LONG_TRUE", "JUMP_LONG_FALSE", "JUMP_LONG"
                };
                opcodeName = subOp < extendedNames.Length ? extendedNames[subOp] : $"EXTENDED_UNKNOWN";
                opcodeBytes = $"0x{bytecode:X2} 0x{subOp:X2}";
            }
            else
            {
                opcodeName = BytecodeDisassembler.GetOpcodeName(bytecode);
                opcodeBytes = $"0x{bytecode:X2}";
            }

            // Update IP
            _heapManager.SetInstanceVariable(_contextManager.ActiveContext, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Execute the bytecode using the bytecode interpreter
            bool continueExecution = _bytecodeInterpreter.ExecuteBytecode(bytecode, methodOOP);

            if (!continueExecution)
            {
                // Check if this is a non-local return (block return bytecodes 0x7D/0x7E)
                // If so, the return value is already in _nlrValue and we must not pop again
                if (_nonLocalReturnPending)
                {
                    // Non-local return: value already popped and stored in _nlrValue
                    return _nlrValue;
                }
                // Normal return: pop the result and return it
                return Pop();
            }

            // Re-read IP in case bytecode modified it (jumps)
            ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self (receiverIndex and receiverOOP already declared at top)
        return receiverOOP;
    }

    // Methods extracted to partial classes:
    // - VirtualMachine.MessageSending.cs: SendSuperMessage, SendMessage, ConsumeCallOutcome, DumpMethodStateOnError, ReuseContextForTailCall, IsAtTailCallPosition
    // - VirtualMachine.VariableAccess.cs: GetMethodBytecodes, GetLiteralCount, GetMethodLiteral, GetTemporaryVariable, SetTemporaryVariable, FindHomeMethodContext
    // Diagnostic methods (DumpSenderChain, DumpStack, etc.) extracted to VMDebugger component

    /// <summary>
    /// Invokes a block CompiledMethod with proper BlockContext wrapping for v1.0.3 semantics.
    /// Creates BlockContext, resolves closure scope, and executes with proper self resolution.
    /// </summary>
    /// <param name="blockMethodOOP">The block CompiledMethod to invoke</param>
    /// <param name="args">Arguments to pass to the block</param>
    /// <returns>The result of block execution</returns>
    private SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args)
    {
        try
        {
            return InvokeBlockInternal(blockMethodOOP, args);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private SmalltalkOOP InvokeBlockInternal(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args)
    {
        // Parse block header for metadata
        var blockHeader = ParseMethodHeader(blockMethodOOP);
        int argCount = args.Length;
        int tempCount = blockHeader.NumTemps;

        // Get receiver from current context (blocks inherit self)
        var currentReceiverOOP = _heapManager.GetInstanceVariable(
            _contextManager.ActiveContext,
            VMConstants.CONTEXT_RECEIVER_INDEX
        );

        // Create MethodContext for block execution
        var blockContextOOP = _contextManager.CreateMethodContext(
            blockMethodOOP,
            currentReceiverOOP,
            args,
            _contextManager.ActiveContext      // sender = current context (return point)
        );

        // Switch to block context
        var previousContext = _contextManager.ActiveContext;
        _contextManager.SetActiveContext(blockContextOOP);

        if (_verboseDebugMode)
        {
            Console.WriteLine($"[InvokeBlockInternal] Context switch: {previousContext} -> {blockContextOOP}");
            Console.WriteLine($"  Block has {argCount} args, {tempCount} temps");
            Console.WriteLine($"  sender = {previousContext}");
        }

        // Signal context switch to primitive caller
        _contextSwitchedByPrimitive = true;

        // Return nil as sentinel (actual result will be pushed by HandleReturn)
        return SpecialObjectsArray.Instance.Nil;
    }


    /// <summary>
    /// Gets the local ID for an object (used for blockOwnerMap lookup).
    /// Uses the reverse mapping built during Phase8.
    /// </summary>
    private string? GetLocalId(SmalltalkOOP objectOOP)
    {
        // NOTE: objectIdToLocalId is VM metadata (not Smalltalk data), so GlobalRegistry access is correct
        var objectIdToLocalId = GlobalRegistry.GetObject<Dictionary<SmalltalkOOP, string>>("objectIdToLocalId");
        if (objectIdToLocalId?.TryGetValue(objectOOP, out string? localId) == true)
        {
            return localId;
        }
        return null;
    }

    /// <summary>
    /// Parses a CompiledMethod header word according to the Smalltalk-80 specification.
    /// MethodHeaderInfo record is defined in IVMHelpers.cs.
    /// </summary>
    private MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP)
    {
        var headerOop = GetMethodLiteral(methodOOP, 0);

        // DEBUG: Check if headerOop is SmallInteger
        if (!headerOop.IsSmallInteger)
        {
            Console.WriteLine($"[ParseMethodHeader] WARNING: Header is not SmallInteger: {headerOop}");
            // Return default values
            return new MethodHeaderInfo(0, false, 0, 0, 0, 0, false, false, 0);
        }

        int header = headerOop.AsSmallInteger();

        // Method header word format per docs/Interpreter-Architecture.md lines 5015-5020:
        // Bits 0-5:   numLiterals (6 bits, 0-63)
        // Bit 6:      needsLargeFrame flag
        // Bits 7-11:  numTemps (5 bits, 0-31)
        // Bits 12-14: flags field (3 bits)
        // Bit 15:     Reserved/unused

        int numLiterals = header & 0x3F;              // Bits 0-5
        bool needsLargeFrame = (header & 0x40) != 0;  // Bit 6
        int numTemps = (header >> 7) & 0x1F;          // Bits 7-11
        int flags = (header >> 12) & 0x7;             // Bits 12-14

        int numArgs;
        int primitive = 0;

        // Interpret flags field
        if (flags < 5)
        {
            // Simple form: flags 0-4 encode numArgs directly
            numArgs = flags;
        }
        else if (flags == 7)
        {
            // Extended form: numArgs and primitive in last literal
            if (numLiterals > 0)
            {
                // Last literal is at indexed slot (numLiterals), since:
                // - Indexed slot 0 = header
                // - Indexed slots 1..N = literals (N = numLiterals)
                // - Indexed slot N+1 = bytecode array (hidden)
                // So last literal (literalN) is at indexed slot numLiterals
                var lastLiteralOop = GetMethodLiteral(methodOOP, numLiterals);

                if (!lastLiteralOop.IsSmallInteger)
                {
                    numArgs = 0;
                    primitive = 0;
                }
                else
                {
                    int lastLiteral = lastLiteralOop.AsSmallInteger();
                    primitive = lastLiteral & 0xFF;           // Low byte
                    numArgs = (lastLiteral >> 8) & 0x1F;     // High bits
                }
            }
            else
            {
                numArgs = 0;
                primitive = 0;
            }
        }
        else
        {
            // Flags 5-6: Quick-return forms (not supported yet)
            numArgs = 0;
        }

        return new MethodHeaderInfo(
            NumLiterals: numLiterals,
            NeedsLargeFrame: needsLargeFrame,
            NumTemps: numTemps,
            Flags: flags,
            NumArgs: numArgs,
            Primitive: primitive,
            IsQuickReturnSelf: flags == 5,
            IsQuickReturnField: flags == 6,
            QuickReturnFieldIndex: flags == 6 ? numTemps : 0  // numTemps repurposed for flags=6
        );
    }

    // Explicit interface implementations for IVMHelpers
    // These delegate to the existing private methods while maintaining encapsulation
    SmalltalkOOP IVMHelpers.GetClass(SmalltalkOOP objectOOP) => GetClass(objectOOP);
    string IVMHelpers.GetClassName(SmalltalkOOP classOOP) => GetClassName(classOOP);
    SmalltalkOOP IVMHelpers.FindMethodContext(SmalltalkOOP contextOOP) => _contextManager.FindMethodContext(contextOOP);
    MethodHeaderInfo IVMHelpers.ParseMethodHeader(SmalltalkOOP methodOOP) => ParseMethodHeader(methodOOP);

    // IBytecodeContext implementation
    string? IBytecodeContext.GetSymbolName(SmalltalkOOP symbolOOP) => GetSymbolName(symbolOOP);
    string? IBytecodeContext.GetLocalId(SmalltalkOOP objectOOP) => GetLocalId(objectOOP);
    SmalltalkOOP IBytecodeContext.GetMethodLiteral(SmalltalkOOP methodOOP, int index) => GetMethodLiteral(methodOOP, index);
    byte[] IBytecodeContext.GetMethodBytecodes(SmalltalkOOP methodOOP) => GetMethodBytecodes(methodOOP);
    SmalltalkOOP IBytecodeContext.GetTemporaryVariable(SmalltalkOOP contextOOP, int index) => GetTemporaryVariable(contextOOP, index);
    void IBytecodeContext.SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP) => SetTemporaryVariable(contextOOP, index, valueOOP);
    void IBytecodeContext.Push(SmalltalkOOP valueOOP) => Push(valueOOP);
    SmalltalkOOP IBytecodeContext.Pop() => Pop();
    SmalltalkOOP IBytecodeContext.Peek() => Peek();
    SmalltalkOOP IBytecodeContext.ResolveAndPatchPoolVariable(SmalltalkOOP poolLiteralOOP, SmalltalkOOP methodOOP, int literalIndex, SmalltalkOOP poolNameSymbol, SmalltalkOOP keySymbol)
        => ResolveAndPatchPoolVariable(poolLiteralOOP, methodOOP, literalIndex, poolNameSymbol, keySymbol);
    SmalltalkOOP IBytecodeContext.LookupInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol)
        => LookupInDictionary(dictionaryOOP, keySymbol);
    SmalltalkOOP IBytecodeContext.ActiveContext => _contextManager.ActiveContext;

    bool IBytecodeContext.NonLocalReturnPending
    {
        get => _nonLocalReturnPending;
        set => _nonLocalReturnPending = value;
    }

    SmalltalkOOP IBytecodeContext.NLRTargetContext
    {
        get => _nlrTargetContext;
        set => _nlrTargetContext = value;
    }

    SmalltalkOOP IBytecodeContext.NLRValue
    {
        get => _nlrValue;
        set => _nlrValue = value;
    }

    bool IBytecodeContext.ContextSwitchPending
    {
        get => _contextSwitchPending;
        set => _contextSwitchPending = value;
    }

    void IBytecodeContext.DumpStack(string label) => _debugger.DumpStack(label);

    bool IBytecodeContext.IsInSenderChain(SmalltalkOOP targetOOP, SmalltalkOOP startOOP)
        => _contextManager.IsInSenderChain(targetOOP, startOOP);

    bool IBytecodeContext.SendMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        var outcome = SendMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    bool IBytecodeContext.SendSuperMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        var outcome = SendSuperMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    void IBytecodeContext.AddAssociationToClassPoolIfNeeded(SmalltalkOOP classOOP, SmalltalkOOP associationOOP)
        => AddAssociationToClassPoolIfNeeded(classOOP, associationOOP);

    // IVMDebugHelpers explicit interface implementations
    SmalltalkOOP IVMDebugHelpers.GetClass(SmalltalkOOP objectOOP) => GetClass(objectOOP);
    string IVMDebugHelpers.GetClassName(SmalltalkOOP classOOP) => GetClassName(classOOP);
    string? IVMDebugHelpers.GetSymbolName(SmalltalkOOP symbolOOP) => GetSymbolName(symbolOOP);
    string? IVMDebugHelpers.GetLocalId(SmalltalkOOP objectOOP) => GetLocalId(objectOOP);
    SmalltalkOOP IVMDebugHelpers.GetMethodLiteral(SmalltalkOOP methodOOP, int index) => GetMethodLiteral(methodOOP, index);
    byte[] IVMDebugHelpers.GetMethodBytecodes(SmalltalkOOP methodOOP) => GetMethodBytecodes(methodOOP);

    /// <summary>
    /// Pushes a value onto the operand stack.
    /// Delegates to StackManager for implementation.
    /// </summary>
    private void Push(SmalltalkOOP valueOOP) => _stackManager.Push(valueOOP);

    /// <summary>
    /// Pops a value from the operand stack.
    /// Delegates to StackManager for implementation.
    /// </summary>
    private SmalltalkOOP Pop() => _stackManager.Pop();

    /// <summary>
    /// Peeks at the top value on the operand stack without removing it.
    /// Delegates to StackManager for implementation.
    /// </summary>
    private SmalltalkOOP Peek() => _stackManager.Peek();


    /// <summary>
    /// Checks if a target context is in the sender chain (for NLR validation).
    /// </summary>
    /// <param name="targetOOP">Context to search for</param>
    /// <returns>True if target is in sender chain</returns>
    private bool IsInSenderChain(SmalltalkOOP targetOOP)
    {
        var currentOOP = _contextManager.ActiveContext;
        int depth = 0;
        const int MAX_DEPTH = 5000;

        while (currentOOP != SpecialObjectsArray.Instance.Nil && depth < MAX_DEPTH)
        {
            if (currentOOP == targetOOP)
                return true;

            currentOOP = _heapManager.GetInstanceVariable(
                currentOOP,
                VMConstants.CONTEXT_SENDER_INDEX
            );
            depth++;
        }

        return false;
    }

    private SmalltalkOOP GetClass(SmalltalkOOP objectOOP)
    {
        // SmallIntegers are immediate values - return their class directly
        if (objectOOP.IsSmallInteger)
            return _metadata.SmallIntegerClassOOP;

        // For heap objects, class is in slot 0
        var classOOP = _heapManager.GetSlot(objectOOP, 0);
        return classOOP;
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        // Check if this is a metaclass by trying to read thisClass field
        // All metaclass instances (meta:X) have thisClass pointing to their instance class (class:X)
        try
        {
            var thisClassOOP = _heapManager.GetInstanceVariable(classOOP, _metadata.MetaclassThisClassFieldIndex);
            if (thisClassOOP != SpecialObjectsArray.Instance.Nil && thisClassOOP != SmalltalkOOP.Null)
            {
                // This is a metaclass - get the name from its thisClass field
                var nameSymbolOOP = _heapManager.GetInstanceVariable(thisClassOOP, _metadata.ClassNameFieldIndex);
                var baseName = GetSymbolName(nameSymbolOOP);
                if (baseName != null)
                    return baseName + " class";
            }
        }
        catch
        {
            // Not a metaclass or field doesn't exist - fall through to regular class handling
        }

        // Regular class - get name directly
        var nameSymbolOOP2 = _heapManager.GetInstanceVariable(classOOP, _metadata.ClassNameFieldIndex);
        return GetSymbolName(nameSymbolOOP2) ?? $"<unnamed class {classOOP}>";
    }

    private string? GetSymbolName(SmalltalkOOP symbolOOP)
    {
        var symbolClass = GetClass(symbolOOP);
        if (symbolClass != _metadata.SymbolClassOOP)
            return null;
        
        int byteCount = _heapManager.GetIndexedSize(symbolOOP);
        if (byteCount == 0)
            return null;
            
        var bytes = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }
        
        return System.Text.Encoding.ASCII.GetString(bytes);
    }

    // IPrimitiveContext implementation - using new heap manager API with SmalltalkOOP types
    ISmalltalkHeapManager IPrimitiveContext.HeapManager => _heapManager;
    void IPrimitiveContext.InvalidateAllRuntimeCaches()
    {
        _runtimeHeapAccess.InvalidateAllCaches();
    }

    // ICacheManager explicit interface implementation
    void ICacheManager.ClearMethodCache()
    {
        ClearMethodCache();
    }

    void ICacheManager.InvalidateAllRuntimeCaches()
    {
        _runtimeHeapAccess.InvalidateAllCaches();
    }

    // IPrimitiveMessageSender explicit interface implementation
    SmalltalkOOP IPrimitiveMessageSender.SendMessage(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] arguments)
    {
        // This is called by perform primitives via the new IPrimitive pattern
        // We need to dispatch the message and handle the result
        var outcome = SendMessage(receiver, selector, arguments);

        // Handle the outcome
        if (outcome.Kind == CallOutcomeKind.Normal)
        {
            return outcome.Value;
        }
        else if (outcome.Kind == CallOutcomeKind.ContextSwitch)
        {
            // Context switch - signal and return nil
            // The InterpreterLoop will handle the context switch
            _contextSwitchPending = true;
            return SpecialObjectsArray.Instance.Nil;
        }
        else
        {
            // Other outcomes (VMException, NonLocalReturn) - return nil
            // In legacy perform primitives, these were also handled
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    SmalltalkOOP IPrimitiveContext.GetBlockContextClassOOP()
    {
        // Return BlockContext class OOP from VMMetadata (populated during cold start)
        // BlockContext is the Blue Book class used for blocks (primitives 80, 81, 82)
        return _metadata.BlockContextClassOOP;
    }

    void IPrimitiveContext.SetActiveContext(SmalltalkOOP newContextOOP)
    {
        // Switch to new context and signal that a primitive switched contexts
        _contextManager.SetActiveContext(newContextOOP);
        _contextSwitchedByPrimitive = true;
    }

    IPlatformBridge? IPrimitiveContext.PlatformBridge => _platformBridge;
    bool IPrimitiveContext.PrimitiveSuccess { get => _primitiveSuccess; set => _primitiveSuccess = value; }

    SmalltalkOOP IPrimitiveContext.GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
    {
        // Use LayoutResolver to get field index from precomputed layouts (not stale!)
        string classLocalId = className.StartsWith("class:") ? className : $"class:{className}";
        int fieldIndex = _layoutResolver.GetFieldIndex(classLocalId, fieldName);
        return _heapManager.GetInstanceVariable(oop, fieldIndex);
    }

    void IPrimitiveContext.SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
    {
        // Use LayoutResolver to get field index from precomputed layouts (not stale!)
        string classLocalId = className.StartsWith("class:") ? className : $"class:{className}";
        int fieldIndex = _layoutResolver.GetFieldIndex(classLocalId, fieldName);
        _heapManager.SetInstanceVariable(oop, fieldIndex, value);
    }

    Memory<byte> IPrimitiveContext.GetByteArray(SmalltalkOOP oop)
    {
        int size = _heapManager.GetIndexedSize(oop);
        if (size == 0)
            return Memory<byte>.Empty;

        byte[] bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(oop, i);
        }
        return bytes;
    }

    SmalltalkOOP IPrimitiveContext.AllocateObject(string className, int indexedSize)
    {
        // Look up class OOP from heap - className might already have "class:" prefix
        string classNameClean = className.StartsWith("class:") ? className.Substring(6) : className;
        var classOOP = _runtimeHeapAccess.GetClass(classNameClean);
        if (classOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException($"Class {classNameClean} not found in heap");
        }

        // Console.WriteLine($"[AllocateObject] className={classNameClean}, classOOP={classOOP}, indexedSize={indexedSize}");

        // Read the format field from the class object to determine the proper ObjectFormat
        // Classes have instance variable "format" which contains encoding per ColdStartJsonGenerator:
        //   Bits 13-12: instanceSpec (0=reserved, 1=Pointer, 2=PointerIndexed, 3=ByteIndexed)
        //   Bits 11-6: fixedSize (number of instance variables for instances - not the class!)
        //   Bit 4: indexable flag
        //   Bit 3: pointer flag

        // Per Blue Book, all classes (Behavior subclasses) have format at instance variable index 2
        var formatOOP = _heapManager.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_FORMAT_INDEX);
        if (!formatOOP.IsSmallInteger)
        {
            throw new InvalidOperationException($"Class {classNameClean} format field is not a SmallInteger: {formatOOP}");
        }

        int formatValue = formatOOP.AsSmallInteger();

        // Decode format field per ColdStartJsonGenerator.CalculateFormatWord()
        // Bits 13-12: Instance Specification (00=no fields, 01=pointers, 10=words, 11=bytes)
        // Bit 3: Pointer flag (1 = indexed portion contains pointers)
        int instanceSpec = (formatValue >> 12) & 0x3;  // Bits 13-12
        int fixedSize = (formatValue >> 6) & 0x3F;      // Bits 11-6
        bool hasPointerIndexedSlots = (formatValue & 0x08) != 0;  // Bit 3

        ObjectFormat format = instanceSpec switch
        {
            0 => ObjectFormat.Pointer,          // Fixed-size pointer object
            1 => ObjectFormat.Pointer,          // 01 = pointer fields (subclass:)
            2 => hasPointerIndexedSlots ? ObjectFormat.PointerIndexed : ObjectFormat.WordIndexed,  // 10 = variableSubclass: (PointerIndexed) or variableWordSubclass: (WordIndexed)
            3 => ObjectFormat.ByteIndexed,      // 11 = byte-indexed (variableByteSubclass:)
            _ => throw new InvalidOperationException($"Invalid instanceSpec {instanceSpec} in format {formatValue:X4}")
        };

        // Allocate the object
        var objectOOP = _heapManager.AllocateObject(format, fixedSize, indexedSize);

        // Set the class reference
        _heapManager.SetSlot(objectOOP, 0, classOOP);

        return objectOOP;
    }

    SmalltalkOOP IPrimitiveContext.SendMessage(SmalltalkOOP receiverOop, SmalltalkOOP selectorOop, SmalltalkOOP argumentsArray)
    {
        // Extract arguments from the array
        int argCount = _heapManager.GetIndexedSize(argumentsArray);
        var args = new SmalltalkOOP[argCount];
        for (int i = 0; i < argCount; i++)
        {
            args[i] = _heapManager.GetIndexedOOP(argumentsArray, i);
        }

        // Call the private SendMessage method and convert Normal outcome to OOP,
        // or throw/propagate a NonLocalReturnException for non-local returns so primitives that expect OOP behave.
        var outcome = SendMessage(receiverOop, selectorOop, args);
        switch (outcome.Kind)
        {
            case CallOutcomeKind.Normal:
                return outcome.Value;
            case CallOutcomeKind.NonLocalReturn:
                // TODO: Phase 3 - Implement NonLocalReturnException
                // For now, treat non-local returns as normal returns
                // throw new NonLocalReturnException(outcome.Value, outcome.TargetContextOop);
                return outcome.Value;
            case CallOutcomeKind.VMException:
                throw outcome.VMException!;
            default:
                throw new InvalidOperationException("Unknown CallOutcome kind");
        }
    }

    SmalltalkOOP IPrimitiveContext.GetActiveContext()
    {
        return _contextManager.ActiveContext;
    }

    int IPrimitiveContext.GetInstructionPointer()
    {
        // Get the instruction pointer from the active context
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        var ipOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, ipIndex);
        return ipOOP.AsSmallInteger();
    }

    SmalltalkOOP IPrimitiveContext.CreateBlockContext(SmalltalkOOP homeContextOop, int startPC, int argCount)
    {
        // Determine the home context (if current context is already a BlockContext, use its home)
        var actualHomeOOP = homeContextOop;

        // Check if homeContextOop is a BlockContext - if so, get its home field
        // For simplicity, we'll use the passed homeContextOop directly for now
        // TODO: Implement proper home context resolution for nested blocks

        // Calculate stack size (args + some execution stack space)
        int stackSize = argCount + 16; // 16 slots for execution stack

        // Get BlockContext class from runtime heap lookup (primitive 80-82 compatibility)
        var blockContextClassOOP = _runtimeHeapAccess.GetClass("BlockContext");

        // Allocate BlockContext with indexed fields for stack
        // BlockContext has 6 fixed fields (sender, pc, stackp, nargs, startpc, home) plus indexed stack slots
        var blockContextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 6, stackSize);
        _heapManager.SetSlot(blockContextOOP, 0, blockContextClassOOP); // Set class reference

        // Get field indices using LayoutResolver
        int senderIndex = VMConstants.BLOCKCONTEXT_CALLER_INDEX;
        int pcIndex = VMConstants.BLOCKCONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int nargsIndex = VMConstants.BLOCKCONTEXT_NARGS_INDEX;
        int startpcIndex = VMConstants.BLOCKCONTEXT_STARTPC_INDEX;
        int homeIndex = VMConstants.BLOCKCONTEXT_HOME_INDEX;

        // Initialize BlockContext fields (all 6 fixed fields)
        _heapManager.SetInstanceVariable(blockContextOOP, senderIndex, SpecialObjectsArray.Instance.Nil); // sender initially nil
        _heapManager.SetInstanceVariable(blockContextOOP, pcIndex, SpecialObjectsArray.Instance.Nil); // pc initially nil
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(0)); // Stack starts empty
        _heapManager.SetInstanceVariable(blockContextOOP, nargsIndex, SmalltalkOOP.FromSmallInteger(argCount));
        _heapManager.SetInstanceVariable(blockContextOOP, startpcIndex, SmalltalkOOP.FromSmallInteger(startPC));
        _heapManager.SetInstanceVariable(blockContextOOP, homeIndex, actualHomeOOP);

        // Initialize indexed fields to nil
        for (int i = 0; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        return blockContextOOP;
    }


    SmalltalkOOP IPrimitiveContext.ActivateBlockContext(SmalltalkOOP blockContextOop, SmalltalkOOP argumentsArray)
    {
        // Get block context fields
        var blockContextOOP = blockContextOop;
        int homeIndex = VMConstants.BLOCKCONTEXT_HOME_INDEX;
        int startpcIndex = VMConstants.BLOCKCONTEXT_STARTPC_INDEX;
        int nargsIndex = VMConstants.BLOCKCONTEXT_NARGS_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;

        var homeOOP = _heapManager.GetInstanceVariable(blockContextOOP, homeIndex);
        var startPcOOP = _heapManager.GetInstanceVariable(blockContextOOP, startpcIndex);
        var nargsOOP = _heapManager.GetInstanceVariable(blockContextOOP, nargsIndex);

        // Validate that startpc and nargs are SmallIntegers
        if (!startPcOOP.IsSmallInteger)
        {
            Console.WriteLine($"[ActivateBlockContext ERROR] startpc is not a SmallInteger: {startPcOOP}");
            Console.WriteLine($"[ActivateBlockContext ERROR] BlockContext OOP: {blockContextOOP}");
            Console.WriteLine($"[ActivateBlockContext ERROR] Dumping BlockContext fields:");
            for (int i = 0; i < 6; i++)
            {
                var fieldOOP = _heapManager.GetInstanceVariable(blockContextOOP, i);
                Console.WriteLine($"  Slot {i}: {fieldOOP} (IsSmallInt={fieldOOP.IsSmallInteger})");
            }
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!nargsOOP.IsSmallInteger)
        {
            Console.WriteLine($"[ActivateBlockContext ERROR] nargs is not a SmallInteger: {nargsOOP}");
            Console.WriteLine($"[ActivateBlockContext ERROR] BlockContext OOP: {blockContextOOP}");
            Console.WriteLine($"[ActivateBlockContext ERROR] Dumping BlockContext fields:");
            for (int i = 0; i < 6; i++)
            {
                var fieldOOP = _heapManager.GetInstanceVariable(blockContextOOP, i);
                Console.WriteLine($"  Slot {i}: {fieldOOP} (IsSmallInt={fieldOOP.IsSmallInteger})");
            }
            return SpecialObjectsArray.Instance.Nil;
        }

        int startPC = startPcOOP.AsSmallInteger();
        int expectedArgCount = nargsOOP.AsSmallInteger();

        // Verify argument count matches
        var argumentsOOP = argumentsArray;
        int providedArgCount = _heapManager.GetIndexedSize(argumentsOOP);
        if (providedArgCount != expectedArgCount)
        {
            // Argument count mismatch - this will cause primitive to fail
            return SpecialObjectsArray.Instance.Nil;
        }

        // Copy arguments into the BlockContext's OWN indexed slots (Blue Book architecture)
        // Each BlockContext stores its own arguments locally at indexed[0..nargs-1]
        // When a nested block accesses outer block arguments, PUSH_TEMP will traverse the home chain
        for (int i = 0; i < providedArgCount; i++)
        {
            var argOOP = _heapManager.GetIndexedOOP(argumentsOOP, i);
            _heapManager.SetIndexedOOP(blockContextOOP, i, argOOP);  // Store in THIS BlockContext, not home!
        }

        // Set up the block context for execution
        _heapManager.SetInstanceVariable(blockContextOOP, senderIndex, _contextManager.ActiveContext); // Current context becomes sender
        _heapManager.SetInstanceVariable(blockContextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(startPC)); // Set PC to start of block
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(expectedArgCount)); // Stack pointer after args

        // Save current context and switch to block context
        var previousContext = _contextManager.ActiveContext;
        _contextManager.SetActiveContext(blockContextOOP);

        // Execute the block
        var result = ExecuteBytecodeLoop();

        // Restore previous context
        _contextManager.SetActiveContext(previousContext);

        return result;
    }

    /// <summary>
    /// Adds an Association to a class's classPool Dictionary if it doesn't already exist.
    /// Implements lazy class variable initialization: on first store, add the Association to classPool.
    ///
    /// If the classPool dictionary is full, this method returns without throwing an exception,
    /// allowing the Smalltalk code to handle dictionary growth through its normal
    /// fullCheck → grow → become: mechanism.
    /// </summary>
    private void AddAssociationToClassPoolIfNeeded(SmalltalkOOP classOOP, SmalltalkOOP associationOOP)
    {
        // SmallIntegers don't have class pools - skip
        if (classOOP.IsSmallInteger)
            return;

        // For metaclasses, class variables are stored in the non-meta class's classPool
        // e.g., Float class (metaclass) shares class variables with Float (regular class)
        var actualClassOOP = classOOP;

        // Check if this is a metaclass by checking if it's an instance of Metaclass
        var classOfClassOOP = _heapManager.GetClassOOP(classOOP);
        
        var metaclassOOP = _runtimeHeapAccess.GetClass("Metaclass");
        if (metaclassOOP != SpecialObjectsArray.Instance.Nil)
        {
            if (classOfClassOOP == metaclassOOP)
            {
                // This is a metaclass - get the regular class it's a metaclass of
                // Metaclass has instance variable 'thisClass' at fixed index 6
                actualClassOOP = _heapManager.GetInstanceVariable(classOOP, VMConstants.METACLASS_THISCLASS_INDEX);
            }
        }
        
        // Now try to find classPool instance variable (only Class objects have it)
        // Class instanceVariableNames: 'name classPool sharedPools' (plus inherited from ClassDescription/Behavior)
        // classPool is at index 7 (0-5 from Behavior/ClassDescription, 6=name, 7=classPool)
        int classPoolIndex = _layoutResolver.GetFieldIndex("class:Class", "classPool");
        
        // Verify actualClassOOP has enough instance variables for classPool
        var format = _heapManager.GetObjectFormat(actualClassOOP);
        if (format != ObjectFormat.Pointer && format != ObjectFormat.PointerIndexed)
        {
            // Not an object with instance variables - skip
            return;
        }
        
        // Try to get classPool - catch any out of range errors
        SmalltalkOOP classPoolOOP;
        try
        {
            classPoolOOP = _heapManager.GetInstanceVariable(actualClassOOP, classPoolIndex);
        }
        catch (Exception)
        {
            // classPool index out of range - not a Class object (likely global variable access)
            return;
        }
        
        // Check if classPool is nil (shouldn't happen - Phase4 should have created it)
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        if (classPoolOOP == nilOOP)
        {
            return;
        }
        
        // Get the Association's key (variable name Symbol)
        var keyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
        
        // Use atomic insert-if-absent to add Association to classPool
        // This handles both first-time insertion and idempotent re-insertion
        var winnerAssocOOP = DictionaryAtomicInsertIfAbsent(classPoolOOP, keyOOP, associationOOP);

        // Check if the dictionary was full (reuse nilOOP from above)
        if (winnerAssocOOP == nilOOP)
        {
            // Dictionary is full - let Smalltalk handle the growth
            // The Smalltalk at:put: method will be executed, which will:
            // 1. Call atNewIndex:put: to add the new association
            // 2. Call fullCheck to detect that the dictionary is full
            // 3. Call grow to create a larger dictionary
            // 4. Use become: to swap the old and new dictionaries
            // We simply return here and let the normal bytecode execution continue
            return;
        }

        // Validate canonicalization: winner should be same as our Association
        // With Phase8 canonicalization, all methods share the same Association OOP
        if (winnerAssocOOP != associationOOP)
        {
            // CANONICALIZATION VIOLATION DETECTED
            // This means either:
            // 1. Phase8 canonicalization failed (bug in loader)
            // 2. Someone created a non-canonical Association at runtime (illegal)
            //
            // We keep the existing winner (first-writer-wins) but this indicates a serious bug
        }
    }
    
    /// <summary>
    /// Sets an Association's value field with proper write-barrier discipline.
    /// 
    /// loaderMode controls write-barrier usage:
    /// - true: Bypass write-barrier (loader-only, single-threaded, before GC enabled)
    /// - false: Use write-barrier (runtime, GC may be active)
    /// 
    /// CRITICAL: Runtime code MUST use loaderMode=false to ensure GC correctness.
    /// </summary>
    private void SetAssociationValue(SmalltalkOOP associationOOP, SmalltalkOOP valueOOP, bool loaderMode)
    {
        if (loaderMode)
        {
            // Loader-only: single-threaded, no GC active, can skip barrier
            _heapManager.SetInstanceVariableNoBarrier(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
        }
        else
        {
            // Runtime: GC may be active, must use write barrier
            _heapManager.SetInstanceVariableWithBarrier(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
        }
    }
    
    /// <summary>
    /// Allocates an empty Dictionary for lazy classPool initialization.
    /// Dictionary has instance variables: tally (SmallInteger 0), plus indexed array for associations.
    /// Initial size chosen to accommodate typical class variable counts with reasonable load factor.
    /// </summary>
    private SmalltalkOOP AllocateEmptyClassPoolDictionary()
    {
        const int initialSize = 8;

        var dictClassOOP = _runtimeHeapAccess.GetClass("Dictionary");
        if (dictClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Dictionary class not found - needed for classPool lazy initialization");
        }

        // Dictionary inherits from Set, which has 1 instance variable: tally
        int instVarCount = _layoutResolver.GetInstanceVariableCount("class:Dictionary");

        var dictOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed,
            instVarCount,
            initialSize
        );

        _heapManager.SetSlot(dictOOP, 0, dictClassOOP);

        // Set tally to 0 (Dictionary inherits tally from Set at index 0)
        _heapManager.SetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX, SmalltalkOOP.FromSmallInteger(0));
        
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < initialSize; i++)
        {
            _heapManager.SetIndexedOOP(dictOOP, i, nilOOP);
        }
        
        return dictOOP;
    }
    
    /// <summary>
    /// Atomically inserts Association into Dictionary if key doesn't exist.
    /// Returns the winning Association OOP (either newly inserted or existing).
    ///
    /// Provides insert-if-absent semantics with proper synchronization:
    /// - If key exists, returns existing Association OOP (no modification)
    /// - If key doesn't exist and space available, inserts Association and returns it
    /// - If dictionary is full, returns nil to signal that Smalltalk should handle growth
    /// - Thread-safe for Smalltalk-80 single-threaded VM (synchronization ready for future)
    ///
    /// Return values:
    /// - Existing Association OOP if key already exists (winner)
    /// - Newly inserted Association OOP if insertion successful (winner)
    /// - nil if dictionary is full (caller should let Smalltalk handle growth via fullCheck → grow → become:)
    /// </summary>
    private SmalltalkOOP DictionaryAtomicInsertIfAbsent(SmalltalkOOP dictionaryOOP, SmalltalkOOP keyOOP, SmalltalkOOP associationOOP)
    {
        int size = _heapManager.GetIndexedSize(dictionaryOOP);

        // NOTE: Smalltalk-80 VM is single-threaded, so we don't need actual atomics.
        // However, this API provides the correct semantics for future concurrent VMs.

        // PHASE 1: Check if key already exists
        for (int i = 0; i < size; i++)
        {
            var existingAssocOOP = _heapManager.GetIndexedOOP(dictionaryOOP, i);
            if (existingAssocOOP == SpecialObjectsArray.Instance.Nil)
                continue;

            var existingKeyOOP = _heapManager.GetInstanceVariable(existingAssocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (existingKeyOOP == keyOOP)
            {
                // Key exists - return existing Association (winner)
                return existingAssocOOP;
            }
        }

        // PHASE 2: Key doesn't exist - insert new Association
        for (int i = 0; i < size; i++)
        {
            var slotOOP = _heapManager.GetIndexedOOP(dictionaryOOP, i);
            if (slotOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Found empty slot - insert here
                _heapManager.SetIndexedOOP(dictionaryOOP, i, associationOOP);

                // Increment tally (Dictionary inherits tally from Set at index 0)
                var tallyOOP = _heapManager.GetInstanceVariable(dictionaryOOP, VMConstants.DICTIONARY_TALLY_INDEX);
                int tally = tallyOOP.AsSmallInteger();
                _heapManager.SetInstanceVariable(dictionaryOOP, VMConstants.DICTIONARY_TALLY_INDEX, SmalltalkOOP.FromSmallInteger(tally + 1));

                // Return the newly inserted Association (winner)
                return associationOOP;
            }
        }

        // No space - Dictionary is full
        // Return nil to signal that the dictionary is full.
        // The calling Smalltalk code will handle this by:
        // 1. Falling back to the Smalltalk at:put: method
        // 2. Which calls atNewIndex:put: that increments tally
        // 3. Which calls fullCheck that detects fullness
        // 4. Which calls grow to create a larger dictionary
        // 5. Which uses become: to swap the old and new dictionaries
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Checks if an OOP references a block literal (CompiledMethod with localId starting with "block:").
    /// </summary>
    private bool IsBlockLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return false;

        // Check if this OOP has a localId starting with "block:"
        string? localId = GetLocalId(oop);
        return localId != null && localId.StartsWith("block:");
    }

    IEnumerable<SmalltalkOOP> IPrimitiveContext.GetAllObjects()
    {
        // TODO: Implement proper enumeration of all objects in the object table
        // For now, return empty enumerable
        return Enumerable.Empty<SmalltalkOOP>();
    }

    void IPrimitiveContext.BecomeOneWay(SmalltalkOOP fromOop, SmalltalkOOP toOop)
    {
        // TODO: Implement BecomeOneWay in heap manager
        // This needs to replace all references to fromOop with toOop
    }

    void IPrimitiveContext.BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2)
    {
        // Implement two-way become: swap the identities of oop1 and oop2
        // This requires:
        // 1. Swapping the object table entries
        // 2. Updating ALL references in the heap to point to the swapped objects
        
        _heapManager.BecomeTwoWay(oop1, oop2);
    }


    int IPrimitiveContext.GetFieldIndex(string className, string fieldName)
    {
        // Use LayoutResolver to get field index from precomputed layouts (not stale!)
        string classLocalId = className.StartsWith("class:") ? className : $"class:{className}";
        return _layoutResolver.GetFieldIndex(classLocalId, fieldName);
    }

    SmalltalkOOP IPrimitiveContext.GetInstanceVariable(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetInstanceVariable(objectOOP, index);
    }

    int IPrimitiveContext.GetIndexedSize(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetIndexedSize(objectOOP);
    }

    SmalltalkOOP IPrimitiveContext.GetIndexedOOP(SmalltalkOOP objectOOP, int index)
    {
        return _heapManager.GetIndexedOOP(objectOOP, index);
    }

    ObjectFormat IPrimitiveContext.GetObjectFormat(SmalltalkOOP objectOOP)
    {
        return _heapManager.GetObjectFormat(objectOOP);
    }

    SmalltalkOOP IPrimitiveContext.InternSymbol(string symbolName)
    {
        // CRITICAL: Must access USTable from Smalltalk heap, NOT from C# proxy data
        // The _symbols array and SymbolRegistry are coldstart-time snapshots that become
        // stale as soon as the Smalltalk interpreter begins running and creates new symbols.

        // Get USTable (Universal Symbol Table) from GlobalRegistry
        // Get USTable from heap (via the Smalltalk global dictionary)
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;
        var ustableOOP = _runtimeHeapAccess.LookupGlobalByName(smalltalkDictOOP, "USTable");
        if (ustableOOP == SpecialObjectsArray.Instance.Nil)
        {
            // USTable not yet created (shouldn't happen after cold start)
            return SpecialObjectsArray.Instance.Nil;
        }

        // USTable is an Array of Symbol OOPs (with nils for empty slots)
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Convert input string to bytes for comparison (Latin-1 encoding)
        byte[] searchBytes = System.Text.Encoding.Latin1.GetBytes(symbolName);

        // Iterate through all slots in USTable
        for (int i = 0; i < ustableSize; i++)
        {
            var symbolOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil slots (empty entries in hash table)
            if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                continue;

            // Get the Symbol's string length from its indexed fields in the Smalltalk heap
            int symbolLength = _heapManager.GetIndexedSize(symbolOOP);

            // Compare byte-by-byte
            if (symbolLength == searchBytes.Length)
            {
                bool match = true;
                for (int j = 0; j < searchBytes.Length; j++)
                {
                    byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                    if (symbolByte != searchBytes[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Found matching symbol in the Smalltalk heap
                    return symbolOOP;
                }
            }
        }

        // Symbol not found - return Nil to signal primitive failure
        // The Smalltalk fallback code will create a new Symbol if needed
        return SpecialObjectsArray.Instance.Nil;
    }

    SmalltalkOOP IPrimitiveContext.GetGlobalObjectByName(string globalName)
    {
        // HEAP-ONLY IMPLEMENTATION: Access Smalltalk global dictionary from heap,
        // NOT from GlobalRegistry (which is a cold start snapshot that becomes stale)

        // Get Smalltalk SystemDictionary from SpecialObjectsArray
        var smalltalkOOP = SpecialObjectsArray.Instance.Smalltalk;
        if (smalltalkOOP == SpecialObjectsArray.Instance.Nil)
        {
            // SystemDictionary not yet created (shouldn't happen after cold start)
            return SpecialObjectsArray.Instance.Nil;
        }

        // Determine what we're looking for based on the prefix
        string lookupKey;
        if (globalName.StartsWith("class:"))
        {
            // Strip "class:" prefix - the key in Smalltalk is just the class name
            lookupKey = globalName.Substring(6);
        }
        else if (globalName.StartsWith("global:"))
        {
            // Strip "global:" prefix
            lookupKey = globalName.Substring(7);
        }
        else
        {
            // No prefix - use as-is
            lookupKey = globalName;
        }

        // First, intern the key as a Symbol (required for dictionary lookup)
        var keySymbolOOP = ((IPrimitiveContext)this).InternSymbol(lookupKey);
        if (keySymbolOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Symbol not found - global doesn't exist
            return SpecialObjectsArray.Instance.Nil;
        }

        // SystemDictionary is a Dictionary (PointerIndexed subclass of Set)
        // Dictionary structure:
        //   Instance variable 0: tally (SmallInteger count of entries, inherited from Set)
        //   Indexed slots 0..N: Association objects (PointerIndexed)
        //
        // Dictionary does NOT have an "array" instance variable - it stores Associations
        // directly in indexed slots!

        // Search through SystemDictionary's indexed slots
        int dictionarySize = _heapManager.GetIndexedSize(smalltalkOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        for (int i = 0; i < dictionarySize; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(smalltalkOOP, i);

            // Skip nil slots (empty hash table slots)
            if (associationOOP == nilOOP || associationOOP.IsSmallInteger)
                continue;

            // Association has two instance variables: key (Symbol) and value (Object)
            var assocKeyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);

            // Check if key matches
            if (assocKeyOOP == keySymbolOOP)
            {
                // Found it! Return the value
                var valueOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
                return valueOOP;
            }
        }

        // Global not found
        return SpecialObjectsArray.Instance.Nil;
    }

    string? IPrimitiveContext.GetClassNameForObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
        {
            return "class:SmallInteger";
        }

        // Get the class OOP from slot 0 of the object
        var classOOP = _heapManager.GetSlot(objectOOP, 0);
        if (classOOP == SpecialObjectsArray.Instance.Nil || classOOP == SmalltalkOOP.Null)
        {
            return null;
        }

        // Use the private GetClassName method to get the class name
        return GetClassName(classOOP);
    }

    /// <summary>
    /// Clears the method lookup cache.
    /// Currently a no-op since this implementation doesn't have a method cache.
    /// The selector caches are static and don't need clearing.
    /// </summary>
    public void ClearMethodCache()
    {
        // No-op: This implementation doesn't have a dynamic method cache
        // The selector caches (_arithmeticSelectorCache, _specialSelectorCache) are
        // static lookup tables initialized once at startup and never modified.
    }

    /// <summary>
    /// Resolves a pool variable on first execution and patches the literal for fast subsequent access.
    /// Pool variable format: (#PoolName, #Key) -> (PoolDictOOP, #Key)
    /// </summary>
    private SmalltalkOOP ResolveAndPatchPoolVariable(SmalltalkOOP poolLiteralOOP, SmalltalkOOP methodOOP, int literalIndex, SmalltalkOOP poolNameSymbol, SmalltalkOOP keySymbol)
    {
        // 1. Resolve pool dictionary name to actual dictionary object
        var smalltalkDict = SpecialObjectsArray.Instance.Smalltalk;
        var poolDictOOP = LookupInDictionary(smalltalkDict, poolNameSymbol);
        
        if (poolDictOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Pool dictionary not found - fall back to Undeclared
            // This handles cases where the pool dictionary is created by an initialize method
            var poolNameString = GetSymbolString(poolNameSymbol);
            Console.WriteLine($"[POOL VAR FALLBACK] Pool dictionary '{poolNameString}' not found, checking Undeclared");

            // Look up in Undeclared dictionary
            var systemDictOOP = SpecialObjectsArray.Instance.Smalltalk;
            var undeclaredOOP = _runtimeHeapAccess.LookupGlobalByName(systemDictOOP, "Undeclared");
            if (undeclaredOOP != SpecialObjectsArray.Instance.Nil)
            {
                var undeclaredValueOOP = LookupInDictionary(undeclaredOOP, keySymbol);
                if (undeclaredValueOOP != SpecialObjectsArray.Instance.Nil)
                {
                    Console.WriteLine($"[POOL VAR FALLBACK] Found '{GetSymbolString(keySymbol)}' in Undeclared");
                    // Return the Association from Undeclared (don't patch, as pool dict may be created later)
                    return undeclaredValueOOP;
                }
            }
            
            throw new InvalidOperationException($"Pool dictionary not found: {poolNameString}, and variable '{GetSymbolString(keySymbol)}' not in Undeclared");
        }
        
        // 2. Look up key in pool dictionary
        var resolvedValueOOP = LookupInDictionary(poolDictOOP, keySymbol);
        
        if (resolvedValueOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Key not found in pool dictionary - this is an error
            var poolNameString = GetSymbolString(poolNameSymbol);
            var keyString = GetSymbolString(keySymbol);
            throw new InvalidOperationException($"Pool variable '{keyString}' not found in pool dictionary '{poolNameString}'");
        }
        
        // 3. Patch literal: Replace poolNameSymbol with poolDictOOP
        // This makes subsequent executions faster (no name lookup)
        _heapManager.SetIndexedOOP(poolLiteralOOP, 0, poolDictOOP);
        
        return resolvedValueOOP;
    }

    /// <summary>
    /// Looks up a key (Symbol) in a Dictionary and returns the value.
    /// Returns nil if key not found.
    /// </summary>
    private SmalltalkOOP LookupInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol)
    {
        // Dictionary is a variableSubclass of Set with:
        //   Instance variable [0]: tally (inherited from Set)
        //   Indexed slots [0..N]: Association array
        // The indexed portion IS the dictionary array, not a separate instance variable
        int arraySize = _heapManager.GetIndexedSize(dictionaryOOP);
        
        // Linear search through dictionary indexed slots
        for (int i = 0; i < arraySize; i++)
        {
            var elementOOP = _heapManager.GetIndexedOOP(dictionaryOOP, i);
            
            if (elementOOP == SpecialObjectsArray.Instance.Nil)
            {
                continue; // Empty slot
            }
            
            // Element should be an Association
            var assocKeyOOP = _heapManager.GetInstanceVariable(elementOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            
            if (assocKeyOOP == keySymbol)
            {
                // Found it - return the value
                var assocValueOOP = _heapManager.GetInstanceVariable(elementOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
                return assocValueOOP;
            }
        }
        
        // Not found
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Gets the string value of a Symbol OOP.
    /// </summary>
    private string GetSymbolString(SmalltalkOOP symbolOOP)
    {
        int size = _heapManager.GetIndexedSize(symbolOOP);
        var chars = new char[size];
        
        for (int i = 0; i < size; i++)
        {
            byte b = _heapManager.GetIndexedByte(symbolOOP, i);
            chars[i] = (char)b;
        }
        
        return new string(chars);
    }

}