using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Attributes;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// The Smalltalk-80 Virtual Machine - handles bytecode execution, message sending, and method dispatch.
/// Uses ISmalltalkHeapManager for object storage and LayoutResolver for dynamic field access.
/// Consistently uses SmalltalkOOP for ALL object references to avoid OOP↔int conversions.
///
/// RUNTIME ONLY: This class must ONLY access the Smalltalk heap via ISmalltalkHeapManager.
/// NO access to cold start proxy data (_symbols[], ColdStartBundle, unfrozen registries).
/// </summary>
[RuntimeOnly(Reason = "Executes Smalltalk bytecode at runtime, must use heap-only access")]
public partial class VirtualMachine : IPrimitiveContext
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly RuntimeHeapAccess _runtimeHeapAccess;
    private readonly LayoutResolver _layoutResolver;
    private readonly VMMetadata _metadata;
    private readonly IPlatformBridge? _platformBridge;
    private readonly DebugOptions _debugOptions;

    // Verbose debug mode for current init expression (controlled by Program.cs)
    private bool _verboseDebugMode = false;

    // Active context (current MethodContext being executed) - now using SmalltalkOOP
    private SmalltalkOOP _activeContextOOP;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    // Context depth tracking to detect infinite recursion
    // (Trampoline architecture prevents CLR stack overflow, so this limit is only for detecting logic errors)
    private int _activeContextCount = 0;
    private const int MAX_CONTEXT_DEPTH = 5000;  // Increased for init expressions with deep dependencies

    // Cursor creation loop diagnostics

    // Call outcome used for NLR propagation and context switching in trampoline interpreter
    private enum CallOutcomeKind { Normal, NonLocalReturn, VMException, ContextSwitch }

    private readonly struct CallOutcome
    {
        public readonly CallOutcomeKind Kind;
        public readonly SmalltalkOOP Value;
        public readonly SmalltalkOOP TargetContextOop;
        public readonly Exception? VMEx;

        private CallOutcome(CallOutcomeKind kind, SmalltalkOOP value, SmalltalkOOP target, Exception? vmEx)
        {
            Kind = kind;
            Value = value;
            TargetContextOop = target;
            VMEx = vmEx;
        }

        public static CallOutcome Normal(SmalltalkOOP value) => new(CallOutcomeKind.Normal, value, SmalltalkOOP.Null, null);
        public static CallOutcome NonLocalReturn(SmalltalkOOP target, SmalltalkOOP value) => new(CallOutcomeKind.NonLocalReturn, value, target, null);
        public static CallOutcome VMException(Exception ex) => new(CallOutcomeKind.VMException, SmalltalkOOP.Null, SmalltalkOOP.Null, ex);
        public static CallOutcome ContextSwitch() => new(CallOutcomeKind.ContextSwitch, SmalltalkOOP.Null, SmalltalkOOP.Null, null);
    }

    // Trampoline interpreter execution status
    private enum InterpreterStatus
    {
        Continue,  // Context switched - keep executing
        Return,    // Execution complete - return value
        Yield      // Temporary pause (for future use)
    }

    // Trampoline state - used to avoid recursive ExecuteBytecodeLoop calls
    private SmalltalkOOP _returnValue;  // Return value from execution
    private SmalltalkOOP _initialContextOOP;  // Starting context for this execution
    private bool _contextSwitchPending;  // Signal that a context switch occurred (for trampoline)
    private bool _contextSwitchedByPrimitive;  // Primitive created new context (e.g., block invocation)

    // Context pooling - reuse recently returned contexts to reduce GC pressure
    // Dictionary keyed by indexed size (stack size) for efficient matching
    private Dictionary<int, Stack<SmalltalkOOP>> _contextPools = new Dictionary<int, Stack<SmalltalkOOP>>();
    private const int MAX_POOLED_CONTEXTS_PER_SIZE = 20;

    // Non-local return state (replaces exception-based unwinding)
    private bool _nonLocalReturnPending;  // NLR in progress
    private SmalltalkOOP _nlrValue;  // Value to return
    private SmalltalkOOP _nlrTargetContext;  // Context to unwind to

    // Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _arithmeticSelectorOOPs;

    // Cached selector OOPs for special send bytecodes (0xC0-0xCF) - indexed by bytecode & 0x0F
    private SmalltalkOOP[]? _specialSelectorOOPs;

    // DEBUG: Context creation counter for diagnostic output
    private static int _createContextCallCount = 0;

    // Blue Book architectural constants are defined in VMConstants.cs
    // All context field indices are now static constants shared across the VM

    // Verbose debugging support
    public bool VerboseDebug { get; set; } = false;
    private readonly Dictionary<SmalltalkOOP, string> _methodSources = new();

    // Selector trace mechanism - shows nested method calls and returns
    private bool _selectorTraceEnabled = false;
    private int _selectorTraceDepth = 0;
    private readonly Stack<(string className, string selector, int depth)> _selectorTraceStack = new();

    public VirtualMachine(ISmalltalkHeapManager heapManager, LayoutResolver layoutResolver, VMMetadata metadata, IPlatformBridge? platformBridge = null, DebugOptions? debugOptions = null)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _runtimeHeapAccess = new RuntimeHeapAccess(heapManager);
        RuntimeSpecialObjects.Instance.Initialize(_runtimeHeapAccess);
        _metadata = metadata;
        _platformBridge = platformBridge;
        _debugOptions = debugOptions ?? DebugOptions.Default;
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
        _activeContextOOP = CreateMethodContext(methodOOP, receiverOOP, arguments);

        // Execute bytecodes using trampoline pattern to prevent stack overflow
        return InterpreterLoop();
    }

    public SmalltalkOOP ExecuteExistingContext(SmalltalkOOP contextOOP)
    {
        // Reset context depth counter for fresh execution
        // Each init expression should start with depth=0
        _activeContextCount = 0;

        // Console.WriteLine($"[ExecuteExistingContext] Starting execution of context {contextOOP}");
        _activeContextOOP = contextOOP;
        var result = InterpreterLoop();
        // Console.WriteLine($"[ExecuteExistingContext] Finished with result {result}");
        return result;
    }

    /// <summary>
    /// Creates a MethodContext object for executing a method.
    /// Implements proper Blue Book MethodContext initialization with all required fields.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod OOP to execute</param>
    /// <param name="receiverOOP">Receiver OOP for this activation</param>
    /// <param name="arguments">Method arguments as OOPs</param>
    /// <param name="callerContextOOP">Optional caller context (sender), default for top-level DoIts</param>
    /// <returns>Allocated and initialized MethodContext OOP</returns>
    private SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
    {
        // DEBUG: Log first 20 context creations
        System.Threading.Interlocked.Increment(ref _createContextCallCount);
        string? methodLocalId = GetLocalId(methodOOP);

        // Step 1: Validate CompiledMethod exists and parse header
        var header = ParseMethodHeader(methodOOP);

        // Validate argument count matches
        if (arguments.Length != header.NumArgs)
        {
            throw new InvalidOperationException(
                $"Argument count mismatch: method expects {header.NumArgs} args, got {arguments.Length}");
        }

        // Step 2: Calculate stack size (args + temps + execution stack)
        // Use large frame (48) or small frame (16) based on needsLargeFrame flag
        int executionStackSize = header.NeedsLargeFrame ? 48 : 16;
        int stackSize = header.NumArgs + header.NumTemps + executionStackSize;

        // Step 3: Get MethodContext class from RuntimeSpecialObjects
        var methodContextClassOOP = RuntimeSpecialObjects.Instance.MethodContextClass;

        // Step 4: Try to reuse a pooled context with matching size, otherwise allocate new
        SmalltalkOOP contextOOP;
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");

        // Check size-specific pool for matching context
        if (_contextPools.TryGetValue(stackSize, out var pool) && pool.Count > 0)
        {
            // Reuse pooled context with exact size match
            // CRITICAL: Never reuse the active context or any context in the sender chain!
            contextOOP = pool.Pop();

            // Check if we accidentally picked the active context
            if (contextOOP == _activeContextOOP || IsInSenderChain(contextOOP, _activeContextOOP))
            {
                // Put it back and allocate a fresh one
                pool.Push(contextOOP);
                contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
                _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP);
            }
            else
            {
                // if (_verboseDebugMode)
                //     Console.WriteLine($"[POOL] Reusing pooled context {contextOOP} with size {stackSize} (pool size now: {pool.Count})");
            }
        }
        else
        {
            // Allocate new context
            contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
            _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP); // Set class reference
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[POOL] Allocated new context {contextOOP} with size {stackSize} (pool empty or doesn't exist)");
        }

        // Increment context depth and check limit
        _activeContextCount++;

        if (_activeContextCount > MAX_CONTEXT_DEPTH)
        {
            // Console.WriteLine($"\n[CONTEXT DEPTH EXCEEDED] Depth: {_activeContextCount}, Limit: {MAX_CONTEXT_DEPTH}");
            // Console.WriteLine($"Current receiver: {receiverOOP}, method: {methodOOP}");
            DumpSenderChain(callerContextOOP, 30);

            throw new InvalidOperationException(
                $"Context depth limit exceeded: {_activeContextCount} contexts (limit: {MAX_CONTEXT_DEPTH}). " +
                $"This typically indicates infinite recursion or primitive failure causing excessive message sends.");
        }

        // Step 5: Get field indices using LayoutResolver
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Step 6: Initialize MethodContext fixed fields

        // sender: caller context or nil for top-level DoIts
        var callerOOP = callerContextOOP == default ? SpecialObjectsArray.Instance.Nil : callerContextOOP;
        _heapManager.SetInstanceVariable(contextOOP, senderIndex, callerOOP);

        // pc: start at bytecode offset 0
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));

        // stackp: points after args+temps (start of execution stack)
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

        // method: the CompiledMethod being executed
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);

        // receiver: the object receiving this message
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);

        // Step 7: Initialize indexed fields (args + temps + stack)

        // Copy arguments into arg slots
        for (int i = 0; i < header.NumArgs; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, arguments[i]);
        }

        // CRITICAL: Initialize temps to nil for ALL contexts (both new and pooled)
        // Previously, pooled contexts retained stale temp values from previous use!
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Initialize execution stack slots to nil (also for all contexts)
        for (int i = header.NumArgs + header.NumTemps; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Note: GC root registration is implicit - the VM's _activeContextOOP field keeps the context alive
        // Note: home field is not applicable for MethodContext (only for BlockContext)
        // Note: literalFrame is accessed via method.literals, not stored in context

        return contextOOP;
    }

    /// <summary>
    /// Top-level interpreter loop using trampoline pattern.
    /// Prevents .NET stack overflow by avoiding recursive ExecuteBytecodeLoop calls.
    /// This implements the Blue Book's single-loop interpreter design.
    /// </summary>
    private SmalltalkOOP InterpreterLoop()
    {
        // Save initial context - we return when we get back to this context
        _initialContextOOP = _activeContextOOP;

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
        //     Console.WriteLine($"\n[ExecuteBytecodeStep] START in context {_activeContextOOP}");

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
        var contextClassOOP = _heapManager.GetClassOOP(_activeContextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        SmalltalkOOP methodOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: bytecode stored inline in parent method, receiver from home MethodContext
            // Blue Book design: BlockContext.startpc points to bytecode offset in parent method
            // With nested block support: traverse home chain to find the ultimate MethodContext
            var methodContextOOP = FindMethodContext(_activeContextOOP);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            // MethodContext: method and receiver in instance variables
            methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);
        }

        // Diagnostic: show stack pointer
        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
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
            var contextClass = GetClassName(GetClass(_activeContextOOP));
            throw new InvalidOperationException(
                $"ExecuteBytecodeStep: method field in context contains non-PointerIndexed object!\n" +
                $"  Context: {_activeContextOOP}, contextClass={contextClass}\n" +
                $"  Method field (index {methodIndex}): {methodOOP}\n" +
                $"  Method format: {methodFormat} (expected PointerIndexed)\n" +
                $"  Method class: {className}\n" +
                $"  Method localId: {localId}\n" +
                $"  This suggests the bytecode array was stored in the context instead of the CompiledMethod.");
        }

        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

            // Update IP
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Execute the bytecode
            bool continueExecution = ExecuteBytecode(bytecode, methodOOP);

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
            ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self
        return HandleReturn(receiverOOP);
    }

    /// <summary>
    /// Adds a context to the size-specific pool for reuse.
    /// CRITICAL: Only pool contexts that are truly dead (no longer referenced anywhere).
    /// </summary>
    private void AddContextToPool(SmalltalkOOP contextOOP)
    {
        // NEVER pool the active context
        if (contextOOP == _activeContextOOP)
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[POOL] SKIPPED pooling {contextOOP} - it's the active context!");
            return;
        }

        // NEVER pool a context that's in the active context's sender chain
        if (IsInSenderChain(contextOOP, _activeContextOOP))
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[POOL] SKIPPED pooling {contextOOP} - it's in the sender chain of {_activeContextOOP}");
            return;
        }

        int size = _heapManager.GetIndexedSize(contextOOP);
        if (!_contextPools.TryGetValue(size, out var poolForSize))
        {
            poolForSize = new Stack<SmalltalkOOP>();
            _contextPools[size] = poolForSize;
        }
        if (poolForSize.Count < MAX_POOLED_CONTEXTS_PER_SIZE)
        {
            poolForSize.Push(contextOOP);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[POOL] Added {contextOOP} to pool (size {size}), pool now has {poolForSize.Count} contexts");
        }
    }

    /// <summary>
    /// Checks if a context is in the sender chain of another context.
    /// </summary>
    private bool IsInSenderChain(SmalltalkOOP contextToFind, SmalltalkOOP startContext)
    {
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        SmalltalkOOP current = startContext;
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Walk up the sender chain looking for contextToFind
        for (int i = 0; i < 100; i++) // Limit iterations to prevent infinite loops
        {
            if (current == nilOOP || current == default)
                return false;

            var senderOOP = _heapManager.GetInstanceVariable(current, senderIndex);
            if (senderOOP == contextToFind)
                return true;

            current = senderOOP;
        }

        return false;
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
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding from {_activeContextOOP} to target {_nlrTargetContext}, value={_nlrValue}");

            // Check if current context is the NLR target
            if (_activeContextOOP == _nlrTargetContext)
            {
                // if (_verboseDebugMode)
                //     Console.WriteLine($"[HandleReturn NLR] TARGET REACHED at {_activeContextOOP}");

                // Target reached - clear NLR state and push value
                _nonLocalReturnPending = false;
                SmalltalkOOP nlrValue = _nlrValue;
                _nlrValue = default;
                _nlrTargetContext = default;

                // Get sender to continue execution
                senderOOP = _heapManager.GetInstanceVariable(_activeContextOOP, senderIndex);

                // Add context to size-specific pool for reuse
                AddContextToPool(_activeContextOOP);

                // Decrement context depth
                _activeContextCount--;

                // Check if target was the initial context
                if (senderOOP == _initialContextOOP || senderOOP == SpecialObjectsArray.Instance.Nil)
                {
                    _returnValue = nlrValue;
                    _activeContextOOP = _initialContextOOP;
                    // if (_verboseDebugMode)
                    //     Console.WriteLine($"[HandleReturn NLR] Returning to initial context");
                    return InterpreterStatus.Return;
                }

                // Switch to sender and push NLR value
                _activeContextOOP = senderOOP;
                Push(nlrValue);
                // if (_verboseDebugMode)
                //     Console.WriteLine($"[HandleReturn NLR] Pushed NLR value onto sender {senderOOP}");
                return InterpreterStatus.Continue;
            }

            // Not the target - continue unwinding (don't push value)
            senderOOP = _heapManager.GetInstanceVariable(_activeContextOOP, senderIndex);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding through {_activeContextOOP}, next sender={senderOOP}");

            // Add context to size-specific pool for reuse
            AddContextToPool(_activeContextOOP);

            // Decrement context depth
            _activeContextCount--;

            // Check if we ran out of contexts (error case)
            if (senderOOP == SpecialObjectsArray.Instance.Nil)
            {
                throw new InvalidOperationException(
                    $"Non-local return target context {_nlrTargetContext} not found in sender chain. " +
                    $"Target may have already returned.");
            }

            // Continue unwinding
            _activeContextOOP = senderOOP;
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Switched to sender {_activeContextOOP}, continuing unwinding");
            return InterpreterStatus.Continue;
        }

        // Normal return (not NLR)
        senderOOP = _heapManager.GetInstanceVariable(_activeContextOOP, senderIndex);

        // if (_verboseDebugMode)
        // {
        //     Console.WriteLine($"[HandleReturn] Normal return from {_activeContextOOP} to {senderOOP}");
        //     Console.WriteLine($"  Return value: {returnValue} (SmallInt={returnValue.IsSmallInteger})");
        // }

        // DIAGNOSTIC: Track return values from hash/stringhash methods (disabled for quiet mode)
        // int methodIndex2 = VMConstants.CONTEXT_METHOD_INDEX;
        // var returningMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex2);
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
        AddContextToPool(_activeContextOOP);

        // Decrement context depth
        _activeContextCount--;

        // Check if we're returning to the initial context
        if (senderOOP == _initialContextOOP || senderOOP == SpecialObjectsArray.Instance.Nil)
        {
            // Execution complete - return to caller
            _returnValue = returnValue;
            _activeContextOOP = _initialContextOOP;
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn] Execution complete, returning to initial context");
            return InterpreterStatus.Return;
        }

        // Switch to sender context and push return value
        _activeContextOOP = senderOOP;
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
        var contextClassOOP = _heapManager.GetClassOOP(_activeContextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        SmalltalkOOP methodOOP;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: bytecode stored inline in parent method, receiver from home MethodContext
            // Blue Book design: BlockContext.startpc points to bytecode offset in parent method
            // With nested block support: traverse home chain to find the ultimate MethodContext
            var methodContextOOP = FindMethodContext(_activeContextOOP);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, VMConstants.CONTEXT_RECEIVER_INDEX);
        }
        else
        {
            // MethodContext: method and receiver in instance variables
            methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
            receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);
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
            var activeClassName = _heapManager.GetClassName(_heapManager.GetClassOOP(_activeContextOOP));
            var methodContextOOP = isBlockContext ? FindMethodContext(_activeContextOOP) : _activeContextOOP;
            var methodContextClassName = _heapManager.GetClassName(_heapManager.GetClassOOP(methodContextOOP));
            throw new InvalidOperationException(
                $"ExecuteBytecodeLoop: methodOOP is SmallInteger!\n" +
                $"  ActiveContext: {_activeContextOOP}, class={activeClassName}\n" +
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
            var contextClass = GetClassName(GetClass(_activeContextOOP));
            throw new InvalidOperationException(
                $"ExecuteBytecodeLoop: method field in context contains non-PointerIndexed object!\n" +
                $"  Context: {_activeContextOOP}, contextClass={contextClass}\n" +
                $"  Method field (index {methodIndex}): {methodOOP}\n" +
                $"  Method format: {methodFormat} (expected PointerIndexed)\n" +
                $"  Method class: {className}\n" +
                $"  Method localId: {localId}\n" +
                $"  This suggests the bytecode array was stored in the context instead of the CompiledMethod.");
        }
        // TODO: Implement GetMethodBytecodes in heap manager or add helper method
        byte[] bytecodes = GetMethodBytecodes(methodOOP);

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();

        while (ip < bytecodes.Length)
        {
            byte bytecode = bytecodes[ip++];

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

            // if (_debugOptions.ShouldDebugBytecode)
            // {
            //     Console.WriteLine($"[BYTECODE EXEC] context={_activeContextOOP}, ip={ip-1}, bytecode={opcodeName} ({opcodeBytes})");
            // }

            // Update IP
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

            // Execute the bytecode
            bool continueExecution = ExecuteBytecode(bytecode, methodOOP);

            if (!continueExecution)
            {
                // if (_debugOptions.ShouldDebugBytecode)
                // {
                //     Console.WriteLine($"[BYTECODE RETURN] context={_activeContextOOP}, returning from bytecode loop");
                // }
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
            ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            ip = ipOOP.AsSmallInteger();
        }

        // If we fall off the end, return self (receiverIndex and receiverOOP already declared at top)
        return receiverOOP;
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP)
    {
        // DEBUG: Track bytecode execution for debugging
        // if (_activeContextCount <= 3)  // Only log first few contexts
        // {
        //     Console.WriteLine($"[BYTECODE] 0x{bytecode:X2} ({bytecode}) in method {GetLocalId(methodOOP)}");
        // }

        // Check if this is a BlockContext - they retrieve receiver from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(_activeContextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;
        SmalltalkOOP receiverOOP;

        if (isBlockContext)
        {
            // BlockContext: receiver from home MethodContext (slot 5 in BlockContext is HOME, not receiver!)
            var methodContextOOP = FindMethodContext(_activeContextOOP);
            receiverOOP = _heapManager.GetInstanceVariable(methodContextOOP, receiverIndex);
        }
        else
        {
            // MethodContext: receiver in instance variable
            receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);
        }

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            int index = BytecodeUtil.PushIvarIndex(bytecode);
            var valueOOP = _heapManager.GetInstanceVariable(receiverOOP, index);
            Push(valueOOP);
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        // Single-Context Architecture: Uses lexicalParent chain traversal for variable access
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            int index = BytecodeUtil.PushTempIndex(bytecode);

            if (VerboseDebug)
            {
                var contextClass = _heapManager.GetClassOOP(_activeContextOOP);
                string contextType = (contextClass == _metadata.BlockContextClassOOP) ? "BlockContext" : "MethodContext";
                Console.WriteLine($"[PUSH_TEMP] index={index}, activeContext={_activeContextOOP} ({contextType})");
            }

            var valueOOP = GetTemporaryVariable(_activeContextOOP, index);

            if (VerboseDebug)
            {
                string valueDesc = valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" :
                                   valueOOP == SpecialObjectsArray.Instance.Nil ? "nil" :
                                   $"{_heapManager.GetClassName(valueOOP)} OOP({valueOOP})";
                Console.WriteLine($"[PUSH_TEMP] Retrieved value: {valueDesc}");
                DumpStack("After PUSH_TEMP");
            }

            Push(valueOOP);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // CRITICAL: Add +1 to skip header at lit[0]
        // NOTE: Blocks are NOT stored as literals anymore (Blue Book inline bytecode approach)
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
            Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            int index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header


            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)
            if (associationOOP == SpecialObjectsArray.Instance.Nil)
            {
                Push(SpecialObjectsArray.Instance.Nil);
                return true;
            }

            // Association has layout: [key, value]
            // DEFENSIVE: Check if this is actually an Association
            var classOOP = _heapManager.GetClassOOP(associationOOP);
            int instVarCount = _heapManager.GetInstanceVariableCount(associationOOP);

            if (instVarCount < 2)
            {
                Console.WriteLine($"[PUSH_LITERAL_VAR] ERROR: Expected Association, got object with {instVarCount} instance variables");
                Console.WriteLine($"  associationOOP: {associationOOP}");
                Console.WriteLine($"  classOOP: {classOOP}");
                Console.WriteLine($"  methodOOP: {methodOOP}");
                Console.WriteLine($"  literal index: {index}");

                // This might be a Symbol or other object that should be pushed directly
                Push(associationOOP);
                return true;
            }

            // Association has a fixed layout per Smalltalk-80 specification:
            // slot 0 = key, slot 1 = value
            var valueOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

            Push(valueOOP);
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            int index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var valueOOP = Pop();
            _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        // Single-Context Architecture: Uses lexicalParent chain traversal for variable access
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            int index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var valueOOP = Pop();
            SetTemporaryVariable(_activeContextOOP, index, valueOOP);
            return true;
        }

        // Special constants and returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
                Push(receiverOOP);
                return true;

            case Bytecode.PUSH_TRUE:
                Push(SpecialObjectsArray.Instance.True);
                return true;

            case Bytecode.PUSH_FALSE:
                Push(SpecialObjectsArray.Instance.False);
                return true;

            case Bytecode.PUSH_NIL:
                Push(SpecialObjectsArray.Instance.Nil);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                Push(SmalltalkOOP.FromSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                Push(SmalltalkOOP.FromSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                Push(SmalltalkOOP.FromSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                Push(SmalltalkOOP.FromSmallInteger(2));
                return true;

            case Bytecode.RETURN_SELF:
                // TODO: Implement using lexicalParent traversal (Single-Context Architecture)
                Push(receiverOOP);
                return false; // Return from method

            case Bytecode.RETURN_TRUE:
                Push(SpecialObjectsArray.Instance.True);
                return false;

            case Bytecode.RETURN_FALSE:
                Push(SpecialObjectsArray.Instance.False);
                return false;

            case Bytecode.RETURN_NIL:
                Push(SpecialObjectsArray.Instance.Nil);
                return false;

            case Bytecode.RETURN_TOP:
                // Value already on stack
                return false;

            case Bytecode.RETURN_SELF_POP:
                // Blue Book: Return self from block's home MethodContext (non-local return)
                {
                    // Check if we're in a BlockContext by reading slot 5 (home field)
                    var homeOOP = _heapManager.GetInstanceVariable(
                        _activeContextOOP,
                        VMConstants.BLOCKCONTEXT_HOME_INDEX  // Slot 5
                    );

                    // If home is not nil, we're in a BlockContext
                    if (homeOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // Non-local return from BlockContext to home MethodContext
                        var targetContextOOP = homeOOP;

                        // Validate home is still in sender chain (not already returned)
                        if (!IsInSenderChain(targetContextOOP, _activeContextOOP))
                        {
                            throw new InvalidOperationException(
                                "Block return failed: home context has already returned");
                        }

                        // Initiate non-local return to home's sender
                        _nonLocalReturnPending = true;
                        _nlrTargetContext = targetContextOOP;
                        _nlrValue = receiverOOP;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal return self
                        Push(receiverOOP);
                        return false;
                    }
                }

            case Bytecode.RETURN_TOP_POP:
                // Blue Book: Return top of stack from block's home MethodContext (non-local return)
                {
                    var returnValue = Pop();

                    // Check if we're in a BlockContext by reading slot 5 (home field)
                    var homeOOP = _heapManager.GetInstanceVariable(
                        _activeContextOOP,
                        VMConstants.BLOCKCONTEXT_HOME_INDEX  // Slot 5, NOT lexicalParent (slot 4)!
                    );

                    if (homeOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        // We're in a BlockContext - perform non-local return
                        var targetContextOOP = homeOOP;

                        // Validate target is in sender chain (safety check)
                        if (!IsInSenderChain(targetContextOOP, _activeContextOOP))
                        {
                            throw new InvalidOperationException(
                                "Non-local return target not in sender chain - block's defining method has already returned");
                        }

                        // Initiate non-local return
                        _nonLocalReturnPending = true;
                        _nlrTargetContext = targetContextOOP;
                        _nlrValue = returnValue;

                        return false; // Exit bytecode loop to unwind
                    }
                    else
                    {
                        // We're in a MethodContext - normal method return
                        Push(returnValue);
                        return false;
                    }
                }

            case Bytecode.RESERVED_RETURN:
                // No operation / reserved (0x7F)
                return true;
        }

        // Extended bytecodes (0x80)
        if (bytecode == 0x80)
        {
            return ExecuteExtendedBytecode(methodOOP);
        }

        // Push active context (0x89) - used for blockCopy: primitive
        if (bytecode == (byte)Bytecode.PUSH_ACTIVE_CONTEXT)
        {
            Push(_activeContextOOP);
            return true;
        }

        // Short jump forward (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            int distance = (bytecode - 0x90) + 1; // Jump 1-8 bytes
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP.AsSmallInteger();
            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Medium jump forward (0xA0)
        if (bytecode == 0xA0)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP2 = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP2.AsSmallInteger();

            byte distanceByte = bytecodes[currentIp++];
            int distance = distanceByte + 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Long jump forward (0xA1-0xA7)
        if (bytecode >= 0xA1 && bytecode <= 0xA7)
        {
            byte[] bytecodes = GetMethodBytecodes(methodOOP);
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP3 = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP3.AsSmallInteger();

            int numBytes = bytecode - 0x9F; // 0xA1 => 2 bytes, 0xA2 => 3 bytes, etc.
            int distance = 0;
            for (int i = 0; i < numBytes; i++)
            {
                distance = (distance << 8) | bytecodes[currentIp++];
            }
            distance += 1; // Distance is encoded as (distance - 1)

            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(currentIp + distance));
            return true;
        }

        // Backward jumps (0xA8-0xAF) - for tail recursion optimization
        if (bytecode >= 0xA8 && bytecode <= 0xAF)
        {
            int offset = bytecode - 0xA8;  // 0xA8 => jump back 0 (nop), 0xA9 => back 1, ..., 0xAF => back 7
            int ipIndex = VMConstants.CONTEXT_PC_INDEX;
            var ipOOP4 = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
            int currentIp = ipOOP4.AsSmallInteger();

            // Jump backward by offset bytes
            // For tail recursion, compiler emits STORE_TEMP for each argument, then JUMP_BACKWARD
            // The offset should point back to the method start (after argument setup)
            int newIp = currentIp - offset;

            if (newIp < 0)
            {
                throw new InvalidOperationException(
                    $"Backward jump offset {offset} would jump before method start " +
                    $"(currentIp={currentIp}, newIp={newIp})");
            }

            _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(newIp));
            return true;
        }

        // Send arithmetic messages (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            return ExecuteSendArithmetic(bytecode);
        }

        // Send special messages (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            return ExecuteSendSpecial(bytecode);
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend0(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send0Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var receiverOOP2 = Pop();
            var outcome0 = SendMessage(receiverOOP2, selectorOOP, Array.Empty<SmalltalkOOP>());
            if (!ConsumeCallOutcome(outcome0)) return false;
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var argOOP = Pop();
            var receiverOOP2 = Pop();

            // Console.WriteLine($"[SEND1 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_activeContextOOP}");
            var outcome1 = SendMessage(receiverOOP2, selectorOOP, new[] { argOOP });
            // Console.WriteLine($"[SEND1 AFTER] Message outcome.Kind={outcome1.Kind}, context={_activeContextOOP}");
            if (!ConsumeCallOutcome(outcome1)) return false;
            // Console.WriteLine($"[SEND1 PUSHED] Pushed result onto stack");
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // CRITICAL: Add +1 to skip header at lit[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            int selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

            // TODO: Handle blocks with nil literals using lexicalParent (Single-Context Architecture)

            var arg2OOP = Pop();
            var arg1OOP = Pop();
            var receiverOOP2 = Pop();

            // Console.WriteLine($"[SEND2 BEFORE] About to send message, receiver={receiverOOP2}, selector={GetSymbolName(selectorOOP)}, context={_activeContextOOP}");
            var outcome2 = SendMessage(receiverOOP2, selectorOOP, new[] { arg1OOP, arg2OOP });
            // Console.WriteLine($"[SEND2 AFTER] Message outcome.Kind={outcome2.Kind}, context={_activeContextOOP}");
            if (!ConsumeCallOutcome(outcome2)) return false;
            // Console.WriteLine($"[SEND2 PUSHED] Pushed result onto stack");
            return true;
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    /// <summary>
    /// Executes extended bytecode operations (0x80 + sub-operation byte).
    /// </summary>
    private bool ExecuteExtendedBytecode(SmalltalkOOP methodOOP)
    {
        byte[] bytecodes = GetMethodBytecodes(methodOOP);
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
        int ip = ipOOP.AsSmallInteger();
        byte subOp = bytecodes[ip++];
        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

        var receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);

        switch (subOp)
        {
            case 0x00: // PUSH_IVAR - Push instance variable at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = _heapManager.GetInstanceVariable(receiverOOP, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x01: // STORE_IVAR - Store to instance variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x02: // POP_STORE_IVAR - Pop and store to instance variable
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    _heapManager.SetInstanceVariable(receiverOOP, index, valueOOP);
                    return true;
                }

            case 0x03: // PUSH_TEMP - Push temporary at any index
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = GetTemporaryVariable(_activeContextOOP, index);
                    Push(valueOOP);
                    return true;
                }

            case 0x04: // STORE_TEMP - Store to temporary without popping
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Peek(); // Don't pop - STORE keeps value on stack
                    SetTemporaryVariable(_activeContextOOP, index, valueOOP);
                    return true;
                }

            case 0x05: // POP_STORE_TEMP - Pop and store to temporary
                // Uses BlockContext home chain traversal for proper variable access
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    SetTemporaryVariable(_activeContextOOP, index, valueOOP);
                    return true;
                }

            case 0x06: // PUSH_LITERAL - Push literal constant at any index
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var literal = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    Push(literal);
                    return true;
                }

            case 0x07: // PUSH_LITERAL_VAR - Push literal variable (association value)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    var keyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                    var valueOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);

                    Push(valueOOP);
                    return true;
                }

            case 0x08: // STORE_LITERAL_VAR - Store to literal variable without popping
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header
                    _heapManager.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
                    Push(valueOOP); // Leave value on stack
                    return true;
                }

            case 0x09: // POP_STORE_LITERAL_VAR - Pop and store to literal variable (class/global variable)
                {
                    byte index = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var valueOOP = Pop();
                    var associationOOP = GetMethodLiteral(methodOOP, index + 1); // +1 to skip header

                    // DIAGNOSTIC: Validate this is actually an Association
                    if (associationOOP.IsSmallInteger)
                    {
                        string? methodLocalId = GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is SmallInteger({associationOOP.AsSmallInteger()}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}");
                    }

                    var literalClass = _heapManager.GetSlot(associationOOP, 0);
                    string? literalClassName = GetClassName(literalClass);

                    // Allow "Association" or "class:Association" as valid class names
                    bool isAssociation = literalClassName == "class:Association" || literalClassName == "Association";

                    if (!isAssociation)
                    {
                        string? methodLocalId = GetLocalId(methodOOP);
                        throw new InvalidOperationException(
                            $"POP_STORE_LITERAL_VAR: literal[{index}] is {literalClassName} (oop={associationOOP}), expected Association. " +
                            $"Method: {methodLocalId}, PC: {ip - 2}. " +
                            $"This likely means the compiler generated wrong bytecode (should be POP_STORE_TEMP for temp variable).");
                    }

                    // DIAGNOSTIC: Log Association key for debugging
                    if (_createContextCallCount <= 20)
                    {
                        // Debug output disabled for quiet mode
                        // string valStr = valueOOP.IsSmallInteger ? $"SmallInteger({valueOOP.AsSmallInteger()})" : $"{valueOOP}";
                        // var keyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                        // string? keyName = GetSymbolName(keyOOP);
                        // string? methodLocalId = GetLocalId(methodOOP);
                        // Console.WriteLine($"[POP_STORE_LITERAL_VAR] Storing to Association key='{keyName}', value={valStr}, method={methodLocalId}");
                    }

                    // Store the value in the Association using write-barrier (runtime mode)
                    SetAssociationValue(associationOOP, valueOOP, loaderMode: false);

                    // Add Association to classPool if it's a class variable (lazy initialization)
                    // For class-side methods, receiver IS the class object which has the classPool
                    AddAssociationToClassPoolIfNeeded(receiverOOP, associationOOP);

                    return true;
                }

            case 0x0A: // SEND_0 - Send with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0B: // SEND_1 - Send with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0C: // SEND_2 - Send with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0D: // SEND_N - Send with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    var outcome = SendMessage(receiver, selectorOOP, args);
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0E: // SEND_SUPER_0 - Send to super with 0 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, Array.Empty<SmalltalkOOP>());
                    return ConsumeCallOutcome(outcome);
                }

            case 0x0F: // SEND_SUPER_1 - Send to super with 1 arg
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg = Pop();
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, new[] { arg });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x10: // SEND_SUPER_2 - Send to super with 2 args
                {
                    byte selectorIndex = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header
                    var arg2 = Pop();
                    var arg1 = Pop();
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, new[] { arg1, arg2 });
                    return ConsumeCallOutcome(outcome);
                }

            case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
                {
                    byte selectorIndex = bytecodes[ip++];
                    byte argCount = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
                    var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

                    var args = new SmalltalkOOP[argCount];
                    for (int i = argCount - 1; i >= 0; i--)
                    {
                        args[i] = Pop();
                    }
                    var receiver = Pop();
                    var outcome = SendSuperMessage(receiver, selectorOOP, args);
                    return ConsumeCallOutcome(outcome);
                }

            case 0x12: // POP - Pop and discard top of stack
                {
                    Pop();
                    return true;
                }

            case 0x13: // DUP - Duplicate top of stack
                {
                    int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
                    var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
                    int sp = spOOP.AsSmallInteger();
                    if (sp <= 0)
                    {
                        throw new InvalidOperationException("Stack underflow on DUP");
                    }
                    var valueOOP = _heapManager.GetIndexedOOP(_activeContextOOP, sp - 1);
                    Push(valueOOP);
                    return true;
                }

            case 0x14: // JUMP_TRUE - Pop and jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x15: // JUMP_FALSE - Pop and jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x16: // JUMP - Unconditional jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    int newIP = ip + offset;
                    // Console.WriteLine($"[JUMP] from IP={ip-2} offset={offset} to newIP={newIP}, method={GetLocalId(methodOOP)}");
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(newIP));
                    return true;
                }

            case 0x17: // JUMP_LONG_TRUE - Pop and long jump forward if true
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.True)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x18: // JUMP_LONG_FALSE - Pop and long jump forward if false
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));

                    int offset = (offsetHigh << 8) | offsetLow;
                    var valueOOP = Pop();

                    if (valueOOP == SpecialObjectsArray.Instance.False)
                    {
                        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    }
                    return true;
                }

            case 0x19: // JUMP_LONG - Unconditional long jump forward
                {
                    byte offsetHigh = bytecodes[ip++];
                    byte offsetLow = bytecodes[ip++];
                    int offset = (offsetHigh << 8) | offsetLow;
                    _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip + offset));
                    return true;
                }

            default:
                throw new NotImplementedException($"Extended bytecode 0x80 0x{subOp:X2} not implemented");
        }
    }

    /// <summary>
    /// Executes arithmetic message send (0xB0-0xBF).
    /// Returns true to continue execution, false if context switched or method returned.
    /// </summary>
    private bool ExecuteSendArithmetic(byte bytecode)
    {
        if (_arithmeticSelectorOOPs == null)
        {
            throw new InvalidOperationException("Arithmetic selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _arithmeticSelectorOOPs[selectorIndex];

        var argOOP = Pop();
        var receiverOOP = Pop();

        // DEBUG: Only log when @ is sent to non-SmallInteger (error case)
        if (bytecode == 187 && (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger))
        {
            string recClass = receiverOOP.IsSmallInteger ? "SmallInteger" : _heapManager.GetClassName(receiverOOP);
            string argClass = argOOP.IsSmallInteger ? "SmallInteger" : _heapManager.GetClassName(argOOP);
            string? recSymbol = recClass == "Symbol" ? GetSymbolName(receiverOOP) : null;
            Console.WriteLine($"[@ ERROR] receiver={receiverOOP} ({recClass}{(recSymbol != null ? $" #{recSymbol}" : "")}), arg={argOOP} ({argClass})");
        }

        // Try primitive arithmetic for SmallIntegers
        if (receiverOOP.IsSmallInteger && argOOP.IsSmallInteger)
        {
            // For primitive arithmetic, we need the selector name for ExecuteIntegerArithmetic
            string selector = selectorIndex switch
            {
                0x00 => "+",
                0x01 => "-",
                0x02 => "<",
                0x03 => ">",
                0x04 => "<=",
                0x05 => ">=",
                0x06 => "=",
                0x07 => "~=",
                0x08 => "*",
                0x09 => "/",
                0x0A => "\\",
                0x0B => "@",
                0x0C => "bitShift:",
                0x0D => "//",
                0x0E => "bitAnd:",
                0x0F => "bitOr:",
                _ => throw new InvalidOperationException($"Unknown arithmetic bytecode: 0x{bytecode:X2}")
            };

            var resultOOP = ExecuteIntegerArithmetic(receiverOOP, selector, argOOP);
            if (resultOOP != SpecialObjectsArray.Instance.Nil ||
                selector == "=" || selector == "~=" ||
                selector == "<" || selector == ">" ||
                selector == "<=" || selector == ">=")
            {
                Push(resultOOP);
                return true;
            }
        }

        // Fall back to message send using cached selector OOP
        var outcome = SendMessage(receiverOOP, selectorOOP, new[] { argOOP });
        return ConsumeCallOutcome(outcome);
    }

    /// <summary>
    /// Executes special message send (0xC0-0xCF).
    /// </summary>
    private bool ExecuteSendSpecial(byte bytecode)
    {
        if (_specialSelectorOOPs == null)
        {
            throw new InvalidOperationException("Special selector cache not initialized. Call InitializeSelectorCaches() after cold start.");
        }

        int selectorIndex = bytecode & 0x0F;
        var selectorOOP = _specialSelectorOOPs[selectorIndex];

        // Determine argument count from selector index
        int argCount = selectorIndex switch
        {
            0 => 1,  // at:
            1 => 2,  // at:put:
            2 => 0,  // size
            3 => 0,  // next
            4 => 1,  // nextPut:
            5 => 0,  // atEnd
            6 => 1,  // ==
            7 => 0,  // class
            8 => 1,  // blockCopy: (Blue Book standard - 1 arg: nargs only)
            9 => 0,  // value
            10 => 1, // value:
            11 => 1, // do:
            12 => 0, // new
            13 => 1, // new:
            14 => 0, // x
            15 => 0, // y
            _ => throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}")
        };

        // Handle blockCopy: BEFORE popping arguments - it's a context primitive, not a message send
        if (selectorIndex == 8) // blockCopy:
        {
            // blockCopy: is unique - it operates on the active context, not a receiver on the stack
            // The argument (nargs) is on top of stack, but we DON'T pop a receiver
            var nargsOOP = Pop();

            if (!nargsOOP.IsSmallInteger)
            {
                throw new InvalidOperationException($"blockCopy: arg must be SmallInteger, got {nargsOOP}");
            }

            // Call primitive 80 (blockCopy:) - receiver is the active context
            var blockContextOOP = ControlPrimitives.BlockCopy(this, _activeContextOOP, new[] { nargsOOP });

            // Push the new BlockContext onto the stack
            Push(blockContextOOP);
            return true;
        }

        // Pop arguments
        var args = new SmalltalkOOP[argCount];

        // DIAGNOSTIC: Log what we're about to pop for at:
        if (selectorIndex == 0) // at:
        {
            int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
            var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
            int sp = spOOP.AsSmallInteger();
            Console.WriteLine($"[ExecuteSendSpecial at:] About to pop, sp={sp}");
            if (sp > 0)
            {
                var topValue = _heapManager.GetIndexedOOP(_activeContextOOP, sp - 1);
                Console.WriteLine($"  Stack top (sp-1): {topValue} (isSmallInt={topValue.IsSmallInteger})");
            }
            if (sp > 1)
            {
                var nextValue = _heapManager.GetIndexedOOP(_activeContextOOP, sp - 2);
                Console.WriteLine($"  Stack (sp-2): {nextValue}");
            }
        }

        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
            if (selectorIndex == 0) Console.WriteLine($"  Popped arg[{i}]: {args[i]} (isSmallInt={args[i].IsSmallInteger})");
        }

        var receiverOOP = Pop();
        if (selectorIndex == 0) Console.WriteLine($"  Popped receiver: {receiverOOP}");

        // Handle special cases
        if (selectorIndex == 6) // ==
        {
            bool isIdentical = receiverOOP == args[0];
            Push(isIdentical ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False);
            return true;
        }

        if (selectorIndex == 7) // class
        {
            try
            {
                var classOOP = GetClass(receiverOOP);
                Push(classOOP);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERROR in 'class' special selector");
                Console.WriteLine($"   receiverOOP: {receiverOOP}");
                Console.WriteLine($"   IsSmallInteger: {receiverOOP.IsSmallInteger}");
                Console.WriteLine($"   Exception: {ex.Message}");
                throw;
            }
        }

        // Fall back to message send using cached selector OOP
        var outcome = SendMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    /// <summary>
    /// Executes integer arithmetic primitively (for SmallIntegers).
    /// Returns nil OOP if operation fails or is not supported.
    /// </summary>
    private SmalltalkOOP ExecuteIntegerArithmetic(SmalltalkOOP receiverOOP, string selector, SmalltalkOOP argOOP)
    {
        if (!receiverOOP.IsSmallInteger || !argOOP.IsSmallInteger)
            return SpecialObjectsArray.Instance.Nil;

        int a = receiverOOP.AsSmallInteger();
        int b = argOOP.AsSmallInteger();

        try
        {
            return selector switch
            {
                "+" => SmalltalkOOP.FromSmallInteger(a + b),
                "-" => SmalltalkOOP.FromSmallInteger(a - b),
                "*" => SmalltalkOOP.FromSmallInteger(a * b),
                "/" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : SpecialObjectsArray.Instance.Nil,
                "\\" => b != 0 ? SmalltalkOOP.FromSmallInteger(a % b) : SpecialObjectsArray.Instance.Nil,
                "//" => b != 0 ? SmalltalkOOP.FromSmallInteger(a / b) : SpecialObjectsArray.Instance.Nil,
                "<" => a < b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                ">" => a > b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "<=" => a <= b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                ">=" => a >= b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "=" => a == b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "~=" => a != b ? SpecialObjectsArray.Instance.True : SpecialObjectsArray.Instance.False,
                "bitAnd:" => SmalltalkOOP.FromSmallInteger(a & b),
                "bitOr:" => SmalltalkOOP.FromSmallInteger(a | b),
                "bitShift:" => b >= 0 ? SmalltalkOOP.FromSmallInteger(a << b) : SmalltalkOOP.FromSmallInteger(a >> (-b)),
                _ => SpecialObjectsArray.Instance.Nil
            };
        }
        catch
        {
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Sends a super message to a receiver.
    /// Looks up the method starting from the superclass of the currently executing method's home class.
    /// Returns a CallOutcome that signals whether a non-local return occurred.
    /// </summary>
    private CallOutcome SendSuperMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // For super sends, we need to start method lookup from the superclass of the class
        // that contains the currently executing method, NOT from the receiver's class.

        // CRITICAL FIX: Read the method from the correct context
        // When executing inside a block, _activeContextOOP is a BlockContext, and we need to walk to home MethodContext.
        // When executing in a method, _activeContextOOP is already a MethodContext.

        // Check if we're in a BlockContext or MethodContext
        var contextClass = GetClassName(_heapManager.GetSlot(_activeContextOOP, 0));
        SmalltalkOOP methodContextOOP;

        if (contextClass == "BlockContext")
        {
            // We're in a block - walk to the home MethodContext
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            methodContextOOP = FindMethodContext(_activeContextOOP);

            // if (_debugOptions.ShouldDebugMessages)
            // {
            //     Console.WriteLine($"[Super Send in Block] Walking from BlockContext to home MethodContext (oop={methodContextOOP})");
            // }
        }
        else if (contextClass == "MethodContext")
        {
            // We're already in a MethodContext
            methodContextOOP = _activeContextOOP;
        }
        else
        {
            throw new InvalidOperationException(
                $"Super send requires MethodContext or BlockContext, but _activeContextOOP is {contextClass}.");
        }

        // Now read the method from the MethodContext (slot 3)
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        var currentMethodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);

        // Get the class that owns this method (the home class)
        // CompiledMethod structure: fixed slot 0 = homeClass, indexed slots = [header] [literals...] [bytecodeArray]
        // The home class is stored in fixed slot 0
        var homeClassOOP = _heapManager.GetInstanceVariable(currentMethodOOP, 0);

        // Get the superclass of the home class
        // Use Blue Book architectural constant: superclass is ALWAYS at index 0 in Behavior
        // This is guaranteed by the compiler which enforces this invariant
        var superclassOOP = _heapManager.GetInstanceVariable(homeClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

        // Get selector name for diagnostics
        string? selectorName = GetSymbolName(selectorOOP);

        // Now look up the method starting from the superclass
        var methodInfo = GetMethod(superclassOOP, selectorName!);

        if (methodInfo == null)
        {
            string? superclassName = GetClassName(superclassOOP);
            throw new InvalidOperationException(
                $"Super method not found: {selectorName ?? "<unknown>"} in superclass {superclassName} (superclassOOP={superclassOOP})");
        }

        // Check if the method has a primitive
        int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodOOP);
        if (primitiveNumber > 0)
        {
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                return CallOutcome.Normal(primitiveResult);
            }
        }

        // TRAMPOLINE MODE: Create context and signal context switch (don't recurse)
        // The InterpreterLoop will continue execution in the new context

        // Create new context with current context as sender
        _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _activeContextOOP);
        // Console.WriteLine($"[SendMessage AFTER CreateMethodContext] _activeContextOOP = {_activeContextOOP}");

        // Signal context switch - InterpreterLoop will continue in new context
        return CallOutcome.ContextSwitch();
    }

    /// <summary>
    /// Sends a message to a receiver.
    /// Looks up the method in the receiver's class and executes it.
    /// Returns a CallOutcome that signals whether a non-local return occurred.
    /// </summary>
    private CallOutcome SendMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // Look up the method in the receiver's class
        var classOOP = GetClass(receiverOOP);

        // Get selector name once for all uses
        string? selectorName = GetSymbolName(selectorOOP);








        // DIAGNOSTIC: Track > and max: message sends (DISABLED - causes issues with SmallInteger diagnostic output)
        // if (selectorName == ">")
        // {
        //     var argInfo = args.Length > 0 ? $"{args[0]} (SmallInt={args[0].IsSmallInteger})" : "none";
        //     Console.WriteLine($"\n[>] receiver {receiverOOP} (SmallInt={receiverOOP.IsSmallInteger}, class=\"{GetClassName(classOOP)}\") | arg={argInfo}");
        // }
        // if (selectorName == "max:")
        // {
        //     var argInfo = args.Length > 0 ? $"{args[0]}" : "none";
        //     Console.WriteLine($"\n[max:] receiver {receiverOOP} | arg={argInfo} | class=\"{GetClassName(classOOP)}\"");
        // }

        // SPECIAL CASE: Block CompiledMethods responding to value/value:/value:value:
        // In v1.0.3, blocks are separate CompiledMethod objects that respond to value messages
        // by creating proper BlockContext and executing with closure semantics
        var compiledMethodClassId = RuntimeSpecialObjects.Instance.CompiledMethodClass;
        if (classOOP == compiledMethodClassId)
        {
            if (selectorName == "value" || selectorName == "value:" || selectorName == "value:value:")
            {
                // Execute the block with proper BlockContext wrapping
                return CallOutcome.Normal(InvokeBlock(receiverOOP, args));
            }
        }

        // Debug: check for classId 0 on "comment:" send
        if (classOOP == SmalltalkOOP.Null)
        {
            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "unknown"} in class 0 (receiver OOP={receiverOOP}, receiverId has classId 0!)");
        }

        var methodInfo = GetMethod(classOOP, selectorName!);

        // DIAGNOSTIC: Show what method was found for hash/stringhash (disabled for quiet mode)
        // if ((selectorName == "hash" || selectorName == "stringhash") && methodInfo != null)
        // {
        //     int primNum = GetPrimitiveNumber(methodInfo.MethodOOP);
        //     string methodLocalId = GetLocalId(methodInfo.MethodOOP) ?? "unknown";
        //     Console.WriteLine($"  Found method: {methodLocalId}, primitiveNumber={primNum}");
        // }

        if (methodInfo == null)
        {
            string? className = GetClassName(classOOP);
            string receiverLocalId = GetLocalId(receiverOOP) ?? "";
            string receiverInfo = string.IsNullOrEmpty(receiverLocalId)
                ? $"OOP({receiverOOP.RawValue})"
                : $"{receiverLocalId} OOP({receiverOOP.RawValue})";

            Console.WriteLine($"\n❌ METHOD NOT FOUND ERROR");
            Console.WriteLine($"   Selector: {selectorName ?? "<unknown>"}");
            Console.WriteLine($"   Receiver: {receiverInfo}");
            Console.WriteLine($"   Receiver class: {className} OOP({classOOP.RawValue})");
            Console.WriteLine($"   Arguments: {args.Length}");
            Console.WriteLine("\nSmalltalk Context Chain:");
            DumpSenderChain(_activeContextOOP, 10);

            throw new InvalidOperationException(
                $"Method not found: {selectorName ?? "<unknown>"} in class {className} OOP({classOOP.RawValue})");
        }

        // Check if the method has a primitive
        int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodOOP);

        if (primitiveNumber > 0)
        {
            // Try to execute the primitive
            var primitiveResult = PrimitiveTable.Execute(primitiveNumber, this, receiverOOP, args);

            if (_primitiveSuccess)
            {
                // Check if primitive created a new context (e.g., block invocation via Primitive 207)
                if (_contextSwitchedByPrimitive)
                {
                    _contextSwitchedByPrimitive = false;
                    // Primitive created new context - signal context switch
                    return CallOutcome.ContextSwitch();
                }

                // Normal primitive success - return result directly
                return CallOutcome.Normal(primitiveResult);
            }

            // Primitive failed - execute method bytecodes as fallback
        }

        // TRAMPOLINE MODE: Create context and signal context switch (don't recurse)
        // The InterpreterLoop will continue execution in the new context

        // TAIL CALL OPTIMIZATION: DISABLED - caused stack underflow bugs
        // TODO: Fix tail call optimization to properly handle stack management
        // if (IsAtTailCallPosition())
        // {
        //     Console.WriteLine($"[TAIL CALL] Reusing context for {selectorName}");
        //     ReuseContextForTailCall(methodInfo.MethodOOP, receiverOOP, args);
        //     return CallOutcome.ContextSwitch();
        // }

        // Create new context with current context as sender
        _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _activeContextOOP);

        // Selector trace: log method entry
        if (_selectorTraceEnabled)
        {
            string className = GetClassName(classOOP) ?? "?";
            TraceMessage($"→ {className}>>{selectorName}");
            _selectorTraceStack.Push((className, selectorName!, _selectorTraceDepth));
            _selectorTraceDepth++;
        }


        // Signal context switch - InterpreterLoop will continue in new context
        return CallOutcome.ContextSwitch();
    }

    /// <summary>
    /// Consume a CallOutcome in the bytecode loop / caller.
    /// Returns true to continue execution (normal), false to indicate that the current method/context has returned (stop loop).
    /// If the outcome is a VMException, throws it. If the outcome is a NonLocalReturn targeted at another context, rethrows as NonLocalReturnException for propagation.
    /// If the outcome is a ContextSwitch, sets _contextSwitchPending and returns false to exit the bytecode loop.
    /// </summary>
    private bool ConsumeCallOutcome(CallOutcome outcome)
    {
        // if (_verboseDebugMode)
        //     Console.WriteLine($"[ConsumeCallOutcome] outcome.Kind={outcome.Kind}");

        if (outcome.Kind == CallOutcomeKind.Normal)
        {
            Push(outcome.Value);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"  Normal: pushed {outcome.Value}");
            return true;
        }

        if (outcome.Kind == CallOutcomeKind.VMException)
        {
            throw outcome.VMEx!;
        }

        if (outcome.Kind == CallOutcomeKind.ContextSwitch)
        {
            // Context switch occurred - signal to exit bytecode loop and continue in new context
            _contextSwitchPending = true;
            return false;
        }

        // Non-local return
        if (outcome.Kind == CallOutcomeKind.NonLocalReturn)
        {
            // Console.WriteLine($"[CONSUME NLR] target={outcome.TargetContextOop}, activeContext={_activeContextOOP}, match={outcome.TargetContextOop == _activeContextOOP}");

            // If NLR targets this context, this context must return immediately with the provided value.
            if (outcome.TargetContextOop == _activeContextOOP)
            {
                // Console.WriteLine($"[NLR TARGET MATCH] Stopping execution, returning value={outcome.Value}");
                // push the return value and signal method-return
                Push(outcome.Value);
                return false;
            }

            // Otherwise propagate as exception for callers to handle / continue unwinding
            // Console.WriteLine($"[NLR PROPAGATE] Rethrowing exception to continue unwinding to {outcome.TargetContextOop}");
            throw new NonLocalReturnException(outcome.Value, outcome.TargetContextOop);
        }

        throw new InvalidOperationException("Unhandled CallOutcome kind");
    }

    /// <summary>
    /// Dumps detailed method state when an error occurs during execution.
    /// Shows bytecode disassembly, source code, literals, and local variables.
    /// </summary>
    private void DumpMethodStateOnError(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] args, SmalltalkOOP classOOP, string selectorName, Exception ex)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine($"ERROR IN: {GetClassName(classOOP)}>>{selectorName}");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"Exception: {ex.Message}\n");

        // Get method header using correct custom format
        var headerInfo = ParseMethodHeader(methodOOP);
        int literalCount = headerInfo.NumLiterals;
        int tempCount = headerInfo.NumTemps;
        int argCount = headerInfo.NumArgs;

        // Get bytecodes
        int literalFrameSize = literalCount + 1; // +1 for header
        var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, literalFrameSize);
        int bytecodeCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
        byte[] bytecodes = new byte[bytecodeCount];
        for (int i = 0; i < bytecodeCount; i++)
        {
            bytecodes[i] = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
        }

        // Get literals
        SmalltalkOOP[] literalOOPs = new SmalltalkOOP[literalCount];
        for (int i = 0; i < literalCount; i++)
        {
            literalOOPs[i] = _heapManager.GetIndexedOOP(methodOOP, i + 1);
        }

        // Display source code if available
        if (_methodSources.TryGetValue(methodOOP, out string? source))
        {
            Console.WriteLine("SOURCE CODE:");
            Console.WriteLine(source);
            Console.WriteLine();
        }

        // Display bytecode disassembly
        Console.WriteLine("BYTECODE DISASSEMBLY:");
        string disassembly = BytecodeDisassembler.Disassemble(bytecodes, literalOOPs,
            (litOOP) => DescribeLiteral(litOOP),
            Array.Empty<string>());
        Console.WriteLine(disassembly);

        // Display literals
        Console.WriteLine("LITERALS:");
        for (int i = 0; i < literalCount; i++)
        {
            var litOOP = _heapManager.GetIndexedOOP(methodOOP, i + 1);
            Console.WriteLine($"  [{i}] = {DescribeLiteral(litOOP)}");
        }
        Console.WriteLine();

        // Display arguments
        Console.WriteLine($"ARGUMENTS ({args.Length}):");
        for (int i = 0; i < args.Length; i++)
        {
            Console.WriteLine($"  arg{i} = {DescribeValue(args[i])}");
        }
        Console.WriteLine();

        // Display receiver
        Console.WriteLine("RECEIVER:");
        Console.WriteLine($"  self = {DescribeValue(receiverOOP)}");
        Console.WriteLine();

        // Display temps (if in active context)
        if (_activeContextOOP != SmalltalkOOP.Null)
        {
            Console.WriteLine($"TEMPORARIES ({tempCount}):");
            for (int i = 0; i < tempCount; i++)
            {
                var tempOOP = _heapManager.GetIndexedOOP(_activeContextOOP, i);
                Console.WriteLine($"  temp{i} = {DescribeValue(tempOOP)}");
            }
            Console.WriteLine();
        }

        // Display stack
        if (_activeContextOOP != SmalltalkOOP.Null)
        {
            int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
            var stackpOOP = _heapManager.GetInstanceVariable(_activeContextOOP, stackpIndex);
            int stackp = stackpOOP.AsSmallInteger();

            Console.WriteLine($"STACK (sp={stackp}):");
            int stackStart = tempCount;
            for (int i = stackStart; i < stackp; i++)
            {
                var stackOOP = _heapManager.GetIndexedOOP(_activeContextOOP, i);
                Console.WriteLine($"  [{i - stackStart}] = {DescribeValue(stackOOP)}");
            }
        }

        Console.WriteLine(new string('=', 80) + "\n");
    }

    /// <summary>
    /// Reuses the current context for a tail call by replacing method, receiver, and arguments.
    /// This prevents context accumulation in recursive patterns like whileTrue:.
    /// </summary>
    private void ReuseContextForTailCall(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments)
    {
        var header = ParseMethodHeader(methodOOP);

        // Validate argument count
        if (arguments.Length != header.NumArgs)
        {
            throw new InvalidOperationException(
                $"Tail call argument count mismatch: method expects {header.NumArgs} args, got {arguments.Length}");
        }

        // Get field indices
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Replace method and receiver
        _heapManager.SetInstanceVariable(_activeContextOOP, methodIndex, methodOOP);
        _heapManager.SetInstanceVariable(_activeContextOOP, receiverIndex, receiverOOP);

        // Reset PC to start of method
        _heapManager.SetInstanceVariable(_activeContextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));

        // Reset stack pointer to after args+temps
        _heapManager.SetInstanceVariable(_activeContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

        // Replace arguments
        for (int i = 0; i < header.NumArgs; i++)
        {
            _heapManager.SetIndexedOOP(_activeContextOOP, i, arguments[i]);
        }

        // Clear temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heapManager.SetIndexedOOP(_activeContextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Note: sender stays the same - we return to the same place
        // Note: we don't increment _activeContextCount since we're reusing a context
    }

    /// <summary>
    /// Checks if the current instruction pointer is at a tail call position.
    /// A tail call occurs when the next bytecode after a message send is a return.
    /// </summary>
    private bool IsAtTailCallPosition()
    {
        try
        {
            // Get current PC
            int pcIndex = VMConstants.CONTEXT_PC_INDEX;
            var pcOOP = _heapManager.GetInstanceVariable(_activeContextOOP, pcIndex);
            if (!pcOOP.IsSmallInteger) return false;
            int currentPC = pcOOP.AsSmallInteger();

            // Get method
            int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
            var methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);

            // Get bytecodes from method (last indexed item is the bytecode array OOP)
            int literalCount = _heapManager.GetIndexedSize(methodOOP);
            if (literalCount == 0) return false;
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, literalCount - 1);

            // Get bytecode array bytes
            int bytecodeLength = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            if (bytecodeLength == 0) return false;

            // Check if next bytecode (after current send completes) is a return
            // The send bytecode we just executed consumed some bytes, so we check currentPC+1
            int nextPC = currentPC + 1;
            if (nextPC >= bytecodeLength) return false;

            byte nextBytecode = _heapManager.GetIndexedByte(bytecodeArrayOOP, nextPC);

            // Check if it's a return bytecode
            return BytecodeUtil.IsReturn(nextBytecode);
        }
        catch
        {
            return false;
        }
    }

    private void DumpSenderChain(SmalltalkOOP contextOOP, int maxDepth)
    {
        Console.WriteLine($"  SENDER CHAIN (showing up to {maxDepth} contexts):");

        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        var currentOOP = contextOOP;
        int depth = 0;

        while (currentOOP != SmalltalkOOP.Null && currentOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            if (currentOOP.IsSmallInteger)
            {
                Console.WriteLine($"    [{depth}] INVALID - SmallInteger {currentOOP.AsSmallInteger()}");
                break;
            }

            try
            {
                var methodOOP = _heapManager.GetInstanceVariable(currentOOP, methodIndex);

                // Get selector from method's literal[0] (stored at indexed slot 1 in our format)
                // indexed slot 0 = header, indexed slot 1 = literal[0] (selector)
                // Blocks don't have selectors, they have other literals at literal[0]
                string selectorName = "<unknown>";
                try
                {
                    var selectorOOP = GetMethodLiteral(methodOOP, 1);
                    var symbolName = GetSymbolName(selectorOOP);

                    // Check if this is a selector (Symbol) or something else (likely a block)
                    if (symbolName != null && selectorOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        selectorName = symbolName;
                    }
                    else if (selectorOOP == SpecialObjectsArray.Instance.Nil)
                    {
                        // No literal[0] - this is a block
                        selectorName = "<block>";
                    }
                    else if (selectorOOP.IsSmallInteger)
                    {
                        // Probably a block with SmallInt literal
                        selectorName = "<block>";
                    }
                    else
                    {
                        // Has literal[0] but it's not a Symbol - likely a block
                        selectorName = "<block>";
                    }
                }
                catch (Exception ex)
                {
                    selectorName = $"<error: {ex.Message}>";
                }

                var classOOP = _heapManager.GetClassOOP(currentOOP);
                var className = GetClassName(classOOP) ?? "?";

                // Get symbolic names for context and method
                string contextName = GetLocalId(currentOOP) ?? "";
                string methodName = GetLocalId(methodOOP) ?? "";

                // Format context info
                string contextInfo = string.IsNullOrEmpty(contextName)
                    ? $"context=OOP({currentOOP.RawValue})"
                    : $"context={contextName} OOP({currentOOP.RawValue})";

                // Format method info
                string methodInfo = string.IsNullOrEmpty(methodName)
                    ? $"method=OOP({methodOOP.RawValue})"
                    : $"method={methodName} OOP({methodOOP.RawValue})";

                Console.WriteLine($"    [{depth}] {className}>>{selectorName} ({contextInfo}, {methodInfo})");

                var senderOOP = _heapManager.GetInstanceVariable(currentOOP, senderIndex);
                currentOOP = senderOOP;
                depth++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [{depth}] ERROR reading context: {ex.Message}");
                break;
            }
        }

        if (depth >= maxDepth)
        {
            Console.WriteLine($"    ... (truncated, chain continues)");
        }
    }

    private void DumpStack(string label)
    {
        Console.WriteLine($"  [{label}] STACK:");
        var stackpOOP = _heapManager.GetInstanceVariable(_activeContextOOP, VMConstants.CONTEXT_STACKP_INDEX);
        int stackp = stackpOOP.AsSmallInteger();

        Console.WriteLine($"    stackp={stackp}");

        // Show top 5 stack entries
        int startIdx = Math.Max(0, stackp - 5);
        for (int i = startIdx; i < stackp; i++)
        {
            var value = _heapManager.GetIndexedOOP(_activeContextOOP, VMConstants.CONTEXT_TEMP_FRAME_START + i);
            string valueDesc = value.IsSmallInteger ? $"SmallInteger({value.AsSmallInteger()})" :
                               value == SpecialObjectsArray.Instance.Nil ? "nil" :
                               $"{_heapManager.GetClassName(value)} OOP({value})";
            Console.WriteLine($"      [{i}] = {valueDesc}");
        }
    }

    private string DescribeLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"Symbol(#{name})";
        }

        if (className == "Association")
        {
            var keyOOP = _heapManager.GetInstanceVariable(oop, VMConstants.ASSOCIATION_KEY_INDEX);
            string? keyName = GetSymbolName(keyOOP);
            return $"Association(#{keyName})";
        }

        if (className == "CompiledMethod")
        {
            return $"CompiledMethod(block)";
        }

        return $"{className}({oop})";
    }

    private string DescribeValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        if (oop == SpecialObjectsArray.Instance.Nil)
        {
            return "nil";
        }

        if (oop == SpecialObjectsArray.Instance.True)
        {
            return "true";
        }

        if (oop == SpecialObjectsArray.Instance.False)
        {
            return "false";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"#{name}";
        }

        return $"{className}({oop})";
    }

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
            _activeContextOOP,
            VMConstants.CONTEXT_RECEIVER_INDEX
        );

        // Create MethodContext for block execution
        var blockContextOOP = CreateMethodContext(
            blockMethodOOP,
            currentReceiverOOP,
            args,
            _activeContextOOP      // sender = current context (return point)
        );

        // Switch to block context
        var previousContext = _activeContextOOP;
        _activeContextOOP = blockContextOOP;

        if (_verboseDebugMode)
        {
            Console.WriteLine($"[InvokeBlockInternal] Context switch: {previousContext} -> {blockContextOOP}");
            Console.WriteLine($"  Block has {argCount} args, {tempCount} temps");
            Console.WriteLine($"  lexicalParent = {previousContext}");
        }

        // Signal context switch to primitive caller
        _contextSwitchedByPrimitive = true;

        // Return nil as sentinel (actual result will be pushed by HandleReturn)
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Creates a MethodContext for executing a block, backed by a BlockContext.
    /// This implements Approach B from the documentation.
    /// </summary>
    // REMOVED: CreateBlockExecutionContext - obsolete in single-context architecture
    private SmalltalkOOP CreateBlockExecutionContext(SmalltalkOOP blockMethodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP blockContextOOP, SmalltalkOOP senderContextOOP)
    {
        throw new NotImplementedException("CreateBlockExecutionContext is obsolete in single-context architecture. Blocks should use CreateMethodContext with lexicalParent field.");
    }










    // TODO: Implement using lexicalParent field (Single-Context Architecture)
    /// <summary>
    /// Finds the ultimate MethodContext by traversing the home chain.
    /// With nested block support, home can point to BlockContext → BlockContext → MethodContext.
    /// This traverses until it finds a MethodContext.
    /// </summary>
    private SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP)
    {
        var currentOOP = contextOOP;
        int depth = 0;
        const int MAX_DEPTH = 100;

        while (depth < MAX_DEPTH)
        {
            var contextClassOOP = _heapManager.GetClassOOP(currentOOP);
            bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

            if (!isBlockContext)
            {
                // Found a MethodContext (or something that's not a BlockContext)
                return currentOOP;
            }

            // It's a BlockContext - traverse to its home
            currentOOP = _heapManager.GetInstanceVariable(currentOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            depth++;
        }

        throw new InvalidOperationException($"FindMethodContext: Max traversal depth exceeded (depth={MAX_DEPTH})");
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
    ///
    /// Header format (16 bits):
    ///   Bits 0-5   (6 bits): numLiterals (0-63)
    ///   Bit  6     (1 bit):  needsLargeFrame flag
    ///   Bits 7-11  (5 bits): numTemps (0-31)
    ///   Bits 12-14 (3 bits): flags field
    ///
    /// Flags field interpretation:
    ///   0-4: Simple method, flags = numArgs (0-4 args, no primitive)
    ///   5:   Quick-return-self (returns receiver without bytecode execution)
    ///   6:   Quick-return-field (returns instance variable at index numTemps)
    ///   7:   Extended form (numArgs and primitive in last literal)
    /// </summary>
    private record MethodHeaderInfo(
        int NumLiterals,
        bool NeedsLargeFrame,
        int NumTemps,
        int Flags,
        int NumArgs,
        int Primitive,
        bool IsQuickReturnSelf,
        bool IsQuickReturnField,
        int QuickReturnFieldIndex
    );

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

    /// <summary>
    /// Extracts the primitive number from a method's header.
    /// Returns 0 if the method has no primitive.
    /// </summary>
    private int GetPrimitiveNumber(SmalltalkOOP methodOOP)
    {
        var headerInfo = ParseMethodHeader(methodOOP);
        return headerInfo.Primitive;
    }

    /// <summary>
    /// Pushes a value onto the operand stack.
    /// </summary>
    private void Push(SmalltalkOOP valueOOP)
    {
        // DIAGNOSTIC: Validate _activeContextOOP is a valid context (MethodContext or BlockContext)
        var activeContextFormat = _heapManager.GetObjectFormat(_activeContextOOP);
        var activeContextClass = GetClassName(GetClass(_activeContextOOP));

        // Both MethodContext (PointerIndexed) and BlockContext (should be PointerIndexed but may be Pointer due to allocation bug) are valid
        bool isValidContext = (activeContextFormat == ObjectFormat.PointerIndexed) ||
                             (activeContextFormat == ObjectFormat.Pointer && (activeContextClass == "MethodContext" || activeContextClass == "BlockContext"));

        if (!isValidContext)
        {
            var valueClass = valueOOP.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(valueOOP));
            Console.WriteLine($"[ERROR Push] _activeContextOOP is not a valid context!");
            Console.WriteLine($"  _activeContextOOP: {_activeContextOOP}");
            Console.WriteLine($"  Expected: MethodContext or BlockContext");
            Console.WriteLine($"  Actual format: {activeContextFormat}");
            Console.WriteLine($"  Actual class: {activeContextClass}");
            Console.WriteLine($"  Trying to push value: {valueOOP}");
            Console.WriteLine($"  Value class: {valueClass}");
            throw new InvalidOperationException($"Push() called with invalid context: {_activeContextOOP}, format={activeContextFormat}, class={activeContextClass}");
        }

        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Check for stack overflow
        int stackSize = _heapManager.GetIndexedSize(_activeContextOOP);
        if (sp >= stackSize)
        {
            throw new InvalidOperationException(
                $"Stack overflow: sp={sp}, allocated size={stackSize}, context={_activeContextOOP}");
        }

        _heapManager.SetIndexedOOP(_activeContextOOP, sp, valueOOP);
        _heapManager.SetInstanceVariable(_activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp + 1));
    }

    /// <summary>
    /// Pops a value from the operand stack.
    /// </summary>
    private SmalltalkOOP Pop()
    {
        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        // Get method to determine where execution stack starts
        // sp is absolute index: [0..numArgs-1][numArgs..numArgs+numTemps-1][numArgs+numTemps..stackSize-1]
        //                        ^--arguments--^------temporaries--------^-------execution stack----------^
        // Execution stack empty when sp == (numArgs + numTemps)
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        // Check if this is a BlockContext - they retrieve method from home MethodContext
        var contextClassOOP = _heapManager.GetClassOOP(_activeContextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        int initialSP;
        SmalltalkOOP methodOOP;

        if (isBlockContext)
        {
            // BlockContext: execution stack starts after block arguments
            // Block's indexed area: [0..nargs-1] = args, [nargs..] = execution stack
            var nargsOOP = _heapManager.GetInstanceVariable(_activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
            initialSP = nargsOOP.AsSmallInteger();

            // Get method from home context (needed for diagnostic messages)
            // For nested blocks, home might be another BlockContext
            // Must traverse the home chain to find the ultimate MethodContext
            var methodContextOOP = FindMethodContext(_activeContextOOP);
            methodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
        }
        else
        {
            // MethodContext: execution stack starts after args + temps
            // Method's indexed area: [0..numArgs-1] = args, [numArgs..numArgs+numTemps-1] = temps, [numArgs+numTemps..] = execution stack
            methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
            var header = ParseMethodHeader(methodOOP);
            initialSP = header.NumArgs + header.NumTemps;
        }

        if (sp <= initialSP)
        {
            // Diagnostic: show what context we're in
            var contextClass = GetClassName(GetClass(_activeContextOOP));
            var methodClass = methodOOP.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(methodOOP));
            Console.WriteLine($"[Pop ERROR] Stack underflow diagnostics:");
            Console.WriteLine($"  activeContext: {_activeContextOOP}, class: {contextClass}");
            Console.WriteLine($"  methodOOP: {methodOOP}, class: {methodClass}");
            Console.WriteLine($"  sp={sp}, initialSP={initialSP}");

            if (isBlockContext)
            {
                var nargsOOP = _heapManager.GetInstanceVariable(_activeContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
                Console.WriteLine($"  BlockContext: nargs={nargsOOP.AsSmallInteger()}");
            }
            else
            {
                var header = ParseMethodHeader(methodOOP);
                Console.WriteLine($"  MethodContext: numArgs={header.NumArgs}, numTemps={header.NumTemps}, numLiterals={header.NumLiterals}");
            }

            throw new InvalidOperationException(
                $"Stack underflow: sp={sp}, initialSP={initialSP} in {contextClass}");
        }

        sp--;
        // if (_verboseDebugMode)
        //     Console.WriteLine($"[Pop] context={_activeContextOOP}, sp before={sp+1}, sp after={sp}");

        _heapManager.SetInstanceVariable(_activeContextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp));
        var value = _heapManager.GetIndexedOOP(_activeContextOOP, sp);

        // if (_verboseDebugMode)
        //     Console.WriteLine($"[Pop] popped value={value}");

        return value;
    }

    /// <summary>
    /// Peeks at the top value on the operand stack without removing it.
    /// </summary>
    private SmalltalkOOP Peek()
    {
        int spIndex = VMConstants.CONTEXT_STACKP_INDEX;
        var spOOP = _heapManager.GetInstanceVariable(_activeContextOOP, spIndex);
        int sp = spOOP.AsSmallInteger();

        if (sp <= 0)
        {
            throw new InvalidOperationException("Stack underflow in Peek()");
        }

        var value = _heapManager.GetIndexedOOP(_activeContextOOP, sp - 1);
        return value;
    }


    /// <summary>
    /// Checks if a target context is in the sender chain (for NLR validation).
    /// </summary>
    /// <param name="targetOOP">Context to search for</param>
    /// <returns>True if target is in sender chain</returns>
    private bool IsInSenderChain(SmalltalkOOP targetOOP)
    {
        var currentOOP = _activeContextOOP;
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

    // Helper methods for heap access during refactoring
    private byte[] GetMethodBytecodes(SmalltalkOOP methodOOP)
    {
        // TODO: Implement GetMethodBytecodes - need to access bytecode array from CompiledMethod
        // CompiledMethod structure: [header, literal1, ..., literalN, bytecodeArrayOOP]
        // The last indexed slot contains OOP to ByteArray with bytecodes
        int indexedSize = _heapManager.GetIndexedSize(methodOOP);
        if (indexedSize > 0)
        {
            // Get the last indexed slot which should be the bytecode array OOP
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, indexedSize - 1);
            int byteCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            byte[] bytecodes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                bytecodes[i] = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
            }
            return bytecodes;
        }
        return Array.Empty<byte>();
    }

    private int GetLiteralCount(SmalltalkOOP methodOOP)
    {
        // Get the total indexed size of the CompiledMethod
        int indexedSize = _heapManager.GetIndexedSize(methodOOP);

        // The indexed portion contains: [header, literal1, literal2, ..., literalN, bytecode array]
        // So literal count = indexedSize - 2 (subtract header at index 0 and bytecode array at end)
        return indexedSize - 2;
    }

    private SmalltalkOOP GetMethodLiteral(SmalltalkOOP methodOOP, int index)
    {
        // Validate that methodOOP is a CompiledMethod (PointerIndexed)
        var format = _heapManager.GetObjectFormat(methodOOP);
        if (format != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            throw new InvalidOperationException(
                $"GetMethodLiteral called on non-PointerIndexed object: {methodOOP}, " +
                $"format={format}, class={className}, localId={localId}. " +
                $"Expected CompiledMethod (PointerIndexed), got {format}. " +
                $"Index={index}");
        }

        // Get the literal from indexed slots
        var literalOOP = _heapManager.GetIndexedOOP(methodOOP, index);

        // Note: Literals can be Symbols (ByteIndexed), Strings (ByteIndexed),
        // SmallIntegers, Arrays (PointerIndexed), CompiledMethods (PointerIndexed), etc.
        // ByteIndexed literals (Symbols/Strings) are normal and expected.

        return literalOOP;
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

    private MethodInfo? GetMethod(SmalltalkOOP classOOP, string selectorName)
    {
        var currentClassOOP = classOOP;
        int depth = 0;
        const int maxDepth = 20; // Prevent infinite loops


        // Walk up the inheritance chain until we find the method or reach nil
        while (currentClassOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            string currentClassName = GetClassName(currentClassOOP);
            // if (_debugOptions.ShouldDebugMethodLookup)
            // {
            //     Console.WriteLine($"  [GetMethod] depth={depth}, {currentClassName}, looking for '{selectorName}'");
            // }

            // Get the method dictionary from the class
            // Use Blue Book architectural constants: methodDict is ALWAYS at index 1 in Behavior
            var methodDictOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_METHODDICT_INDEX);

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                // No method dictionary, try superclass
                // Use Blue Book architectural constant: superclass is ALWAYS at index 0 in Behavior
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

                if (superclassOOP == SpecialObjectsArray.Instance.Nil)
                {
                    // if (_debugOptions.ShouldDebugMethodLookup)
                    // {
                    //     Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root of hierarchy)");
                    // }
                    break; // Reached root of class hierarchy
                }

                // if (_debugOptions.ShouldDebugMethodLookup)
                // {
                //     string superclassName = GetClassName(superclassOOP);
                //     Console.WriteLine($"  '{selectorName}' not found, trying superclass={superclassName}");
                // }
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // MethodDictionary structure (IdentityDictionary):
            // - Instance variable [0] = valuesArray (Array of methods)
            // - Instance variable [1] = tally (SmallInteger)
            // - Indexed fields = keys (selectors)
            // To find a method: search indexed fields for selector, then get method from valuesArray at same index

            var valuesArrayOOP = _heapManager.GetInstanceVariable(methodDictOOP, 0);
            if (valuesArrayOOP == SpecialObjectsArray.Instance.Nil)
            {
                // No values array, try superclass
                // Use Blue Book architectural constant: superclass is ALWAYS at index 0 in Behavior
                var superclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
                string superclassName = GetClassName(superclassOOP);
                Console.WriteLine($"  '{selectorName}' not found, trying superclass={superclassName}");
                currentClassOOP = superclassOOP;
                depth++;
                continue;
            }

            // DIAGNOSTIC: Get format once for both validation blocks
            var methodDictFormat = _heapManager.GetObjectFormat(methodDictOOP);

            // DIAGNOSTIC: Validate methodDictOOP format FIRST (before we try to access its indexed slots)
            if (methodDictFormat != ObjectFormat.PointerIndexed)
            {
                var methodDictClass = GetClassName(GetClass(methodDictOOP));
                Console.WriteLine($"[ERROR] methodDictOOP has wrong format!");
                Console.WriteLine($"  methodDictOOP: {methodDictOOP}");
                Console.WriteLine($"  Expected: PointerIndexed (MethodDictionary)");
                Console.WriteLine($"  Actual: {methodDictFormat}");
                Console.WriteLine($"  Class: {methodDictClass}");
                Console.WriteLine($"  currentClassOOP={currentClassOOP}, className={GetClassName(currentClassOOP)}");
                Console.WriteLine($"  Looking for selector: {selectorName}");

                // Check ALL instance variables in the class
                Console.WriteLine($"  Checking class instance variables:");
                try
                {
                    string? className = GetClassName(currentClassOOP);
                    int instanceVarCount = className != null ? _layoutResolver.GetInstanceVariableCount(className) : 0;
                    Console.WriteLine($"    Instance variable count: {instanceVarCount}");
                    for (int slot = 0; slot < instanceVarCount; slot++)
                    {
                        try
                        {
                            var slotValue = _heapManager.GetInstanceVariable(currentClassOOP, slot);
                            var slotFormat = slotValue.IsSmallInteger ? "SmallInteger" : _heapManager.GetObjectFormat(slotValue).ToString();
                            var slotClass = slotValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(slotValue));
                            Console.WriteLine($"    slot[{slot}]: {slotValue}, format={slotFormat}, class={slotClass}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    slot[{slot}]: ERROR - {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ERROR getting instance variables: {ex.Message}");
                }

                throw new InvalidOperationException($"Class methodDict field contains {methodDictFormat} object (class={methodDictClass}) instead of PointerIndexed MethodDictionary");
            }

            // DIAGNOSTIC: Validate valuesArrayOOP format BEFORE using it
            var valuesArrayFormat = _heapManager.GetObjectFormat(valuesArrayOOP);
            if (valuesArrayFormat != ObjectFormat.PointerIndexed)
            {
                var valuesArrayClass = GetClassName(GetClass(valuesArrayOOP));
                Console.WriteLine($"[ERROR] valuesArrayOOP has wrong format!");
                Console.WriteLine($"  valuesArrayOOP: {valuesArrayOOP}");
                Console.WriteLine($"  Expected: PointerIndexed (Array)");
                Console.WriteLine($"  Actual: {valuesArrayFormat}");
                Console.WriteLine($"  Class: {valuesArrayClass}");
                Console.WriteLine($"  methodDictOOP: {methodDictOOP}, format={methodDictFormat}");
                Console.WriteLine($"  currentClassOOP={currentClassOOP}, className={GetClassName(currentClassOOP)}");
                Console.WriteLine($"  Looking for selector: {selectorName}");

                // Check ALL instance variables in the MethodDictionary
                Console.WriteLine($"  Checking MethodDictionary instance variables:");
                for (int slot = 0; slot < 3; slot++)
                {
                    try
                    {
                        var slotValue = _heapManager.GetInstanceVariable(methodDictOOP, slot);
                        var slotFormat = slotValue.IsSmallInteger ? "SmallInteger" : _heapManager.GetObjectFormat(slotValue).ToString();
                        var slotClass = slotValue.IsSmallInteger ? "SmallInteger" : GetClassName(GetClass(slotValue));
                        Console.WriteLine($"    slot[{slot}]: {slotValue}, format={slotFormat}, class={slotClass}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    slot[{slot}]: ERROR - {ex.Message}");
                    }
                }

                throw new InvalidOperationException($"MethodDictionary slot[0] (valuesArray) contains {valuesArrayFormat} object (class={valuesArrayClass}) instead of PointerIndexed Array");
            }

            int dictSize = _heapManager.GetIndexedSize(methodDictOOP);


            for (int i = 0; i < dictSize; i++)
            {
                var selectorOOP = _heapManager.GetIndexedOOP(methodDictOOP, i);

                // Skip nil entries (empty slots in the hash table)
                if (selectorOOP == SpecialObjectsArray.Instance.Nil)
                    continue;

                // Check if this is the selector we're looking for
                string? currentSelectorName = GetSymbolName(selectorOOP);

                if (currentSelectorName == selectorName)
                {
                    // Found it! Get the method from the valuesArray at the same index
                    var methodOOP = _heapManager.GetIndexedOOP(valuesArrayOOP, i);
                    return new MethodInfo { MethodOOP = methodOOP };
                }
            }

            // Not found in this class, try superclass
            // Use Blue Book architectural constant: superclass is ALWAYS at index 0 in Behavior
            var nextSuperclassOOP = _heapManager.GetInstanceVariable(currentClassOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);

            // if (_debugOptions.ShouldDebugMethodLookup)
            // {
            //     if (nextSuperclassOOP == SpecialObjectsArray.Instance.Nil)
            //     {
            //         Console.WriteLine($"  '{selectorName}' not found, superclass is nil (reached root)");
            //     }
            //     else
            //     {
            //         string nextSuperclassName = GetClassName(nextSuperclassOOP);
            //         Console.WriteLine($"  '{selectorName}' not found, trying superclass={nextSuperclassName}");
            //     }
            // }

            currentClassOOP = nextSuperclassOOP;
            depth++;
        }

        // if (_debugOptions.ShouldDebugMethodLookup)
        // {
        //     Console.WriteLine($"  [GetMethod] FAILED: '{selectorName}' not found after {depth} levels");
        // }
        return null;
    }

    private class MethodInfo
    {
        public SmalltalkOOP MethodOOP { get; set; }
    }

    // IPrimitiveContext implementation - using new heap manager API with SmalltalkOOP types
    ISmalltalkHeapManager IPrimitiveContext.HeapManager => _heapManager;
    void IPrimitiveContext.InvalidateAllRuntimeCaches()
    {
        _runtimeHeapAccess.InvalidateAllCaches();
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
        _activeContextOOP = newContextOOP;
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

        Console.WriteLine($"[AllocateObject] className={classNameClean}, classOOP={classOOP}, indexedSize={indexedSize}");

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
                throw outcome.VMEx!;
            default:
                throw new InvalidOperationException("Unknown CallOutcome kind");
        }
    }

    SmalltalkOOP IPrimitiveContext.GetActiveContext()
    {
        return _activeContextOOP;
    }

    int IPrimitiveContext.GetInstructionPointer()
    {
        // Get the instruction pointer from the active context
        int ipIndex = VMConstants.CONTEXT_PC_INDEX;
        var ipOOP = _heapManager.GetInstanceVariable(_activeContextOOP, ipIndex);
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
        _heapManager.SetInstanceVariable(blockContextOOP, senderIndex, _activeContextOOP); // Current context becomes sender
        _heapManager.SetInstanceVariable(blockContextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(startPC)); // Set PC to start of block
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(expectedArgCount)); // Stack pointer after args

        // Save current context and switch to block context
        var previousContext = _activeContextOOP;
        _activeContextOOP = blockContextOOP;

        // Execute the block
        var result = ExecuteBytecodeLoop();

        // Restore previous context
        _activeContextOOP = previousContext;

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
            Smalltalk80.Interpreter.Core.Objects.ObjectFormat.PointerIndexed,
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
        // TODO: Implement BecomeTwoWay in heap manager
        // This needs to swap all references between oop1 and oop2
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

    private void DumpBytecodes(SmalltalkOOP methodOOP, string prefix)
    {
        var bytecodes = GetMethodBytecodes(methodOOP);
        Console.WriteLine($"{prefix}Bytecode dump ({bytecodes.Length} bytes):");

        int i = 0;
        while (i < bytecodes.Length && i < 50)
        {
            byte b = bytecodes[i];
            int offset = i;
            string desc = b switch
            {
                0x00 => "PUSH_RECEIVER",
                0x01 => "PUSH_TRUE",
                0x02 => "PUSH_FALSE",
                0x03 => "PUSH_NIL",
                0x04 => $"STORE_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x05 => $"POP_STORE_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x06 => $"PUSH_LITERAL {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x10 => $"PUSH_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x11 => $"PUSH_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x20 => $"PUSH_LITERAL {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x5C => "RETURN_STACK_TOP",
                0x5D => "RETURN_NIL",
                0x5E => "RETURN_TRUE",
                0x5F => "RETURN_FALSE",
                0x87 => "POP_STACK_TOP",
                >= 0xB0 and <= 0xBF => $"SEND_ARITH (selector={(b - 0xB0)})",
                >= 0xD0 and <= 0xDF => $"SEND1 (selector={(b - 0xD0)})",
                >= 0xF0 and <= 0xFF => $"SEND2 (selector={(b - 0xF0)})",
                _ => $"0x{b:X2}"
            };

            Console.WriteLine($"{prefix}  [{offset}] {desc}");
            i++;
        }
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
    /// Gets a temporary variable using UNIFIED TEMP FRAME architecture.
    /// ALL variables stored in MethodContext - NO dual routing.
    ///
    /// Block arguments and temps are stored in the MethodContext temp frame
    /// at absolute indices assigned by the compiler. When executing inside a block,
    /// we traverse to the home MethodContext via the 'home' pointer and use the
    /// absolute index directly (no adjustment needed).
    /// </summary>
    /// <param name="contextOOP">Current context (MethodContext or BlockContext)</param>
    /// <param name="index">Absolute index in MethodContext temp frame (0-based)</param>
    /// <returns>Value of the temporary variable</returns>
    private SmalltalkOOP GetTemporaryVariable(SmalltalkOOP contextOOP, int index)
    {
        var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        if (isBlockContext)
        {
            // Traverse to home MethodContext (NO index adjustment!)
            var homeOOP = _heapManager.GetInstanceVariable(
                contextOOP,
                VMConstants.BLOCKCONTEXT_HOME_INDEX
            );

            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.GetTemporaryVariable] BlockContext → home, index={index}");
            }

            // Recursive call with SAME index (absolute indexing)
            return GetTemporaryVariable(homeOOP, index);
        }
        else
        {
            // MethodContext: Direct access using absolute index (base case)
            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.GetTemporaryVariable] MethodContext index={index}");
            }

            return _heapManager.GetIndexedOOP(contextOOP, index);
        }
    }

    /// <summary>
    /// Helper method to format an OOP value for debugging
    /// </summary>
    private string FormatOOPValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return $"SmallInteger({oop.AsSmallInteger()})";
        if (oop == SpecialObjectsArray.Instance.Nil)
            return "nil";
        if (oop == SpecialObjectsArray.Instance.True)
            return "true";
        if (oop == SpecialObjectsArray.Instance.False)
            return "false";

        try
        {
            string className = _heapManager.GetClassName(oop);
            return $"{className} OOP({oop})";
        }
        catch
        {
            return $"OOP({oop})";
        }
    }

    /// <summary>
    /// Sets a temporary variable using UNIFIED TEMP FRAME architecture.
    /// ALL variables stored in MethodContext - NO dual routing.
    ///
    /// Block arguments and temps are stored in the MethodContext temp frame
    /// at absolute indices assigned by the compiler. When executing inside a block,
    /// we traverse to the home MethodContext via the 'home' pointer and use the
    /// absolute index directly (no adjustment needed).
    /// </summary>
    /// <param name="contextOOP">Current context (MethodContext or BlockContext)</param>
    /// <param name="index">Absolute index in MethodContext temp frame (0-based)</param>
    /// <param name="valueOOP">Value to store</param>
    private void SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP)
    {
        var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        if (isBlockContext)
        {
            // Traverse to home MethodContext (NO index adjustment!)
            var homeOOP = _heapManager.GetInstanceVariable(
                contextOOP,
                VMConstants.BLOCKCONTEXT_HOME_INDEX
            );

            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.SetTemporaryVariable] BlockContext → home, index={index}, value={valueOOP}");
            }

            // Recursive call with SAME index (absolute indexing)
            SetTemporaryVariable(homeOOP, index, valueOOP);
        }
        else
        {
            // MethodContext: Direct write using absolute index (base case)
            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.SetTemporaryVariable] MethodContext index={index}, value={valueOOP}");
            }

            _heapManager.SetIndexedOOP(contextOOP, index, valueOOP);
        }
    }

    /// <summary>
    /// Finds the home MethodContext by traversing the home chain.
    /// For MethodContext: returns itself.
    /// For BlockContext: follows home chain until MethodContext is found.
    /// Blue Book Chapter 30: BlueBook-Context-Architecture.md Section 4.3
    /// </summary>
    /// <param name="contextOOP">Starting context (MethodContext or BlockContext)</param>
    /// <returns>The MethodContext at the root of the home chain</returns>
    private SmalltalkOOP FindHomeMethodContext(SmalltalkOOP contextOOP)
    {
        // Traverse home chain until we find a MethodContext
        while (true)
        {
            var contextClassOOP = _heapManager.GetClassOOP(contextOOP);

            // Check if this is a BlockContext
            if (contextClassOOP == _metadata.BlockContextClassOOP)
            {
                // Follow home link
                contextOOP = _heapManager.GetInstanceVariable(
                    contextOOP,
                    VMConstants.BLOCKCONTEXT_HOME_INDEX
                );

                if (contextOOP == SpecialObjectsArray.Instance.Nil)
                {
                    throw new InvalidOperationException("Home chain broken - no MethodContext found");
                }

                continue;  // Keep traversing
            }

            // Must be MethodContext (or some other context) - return it
            return contextOOP;
        }
    }
}