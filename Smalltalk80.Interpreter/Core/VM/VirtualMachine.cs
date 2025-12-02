using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.VM.Interpreter;
using Smalltalk80.Interpreter.Core.VM.Caching;
using Smalltalk80.Interpreter.Core.VM.Diagnostics;
using Smalltalk80.Interpreter.Core.VM.Scheduling;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.Attributes;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

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
/// Uses ISmalltalkHeap for object storage and LayoutResolver for dynamic field access.
/// Consistently uses SmalltalkOOP for ALL object references to avoid OOP↔int conversions.
///
/// RUNTIME ONLY: This class must ONLY access the Smalltalk heap via ISmalltalkHeap.
/// NO access to cold start proxy data (_symbols[], ColdStartBundle, unfrozen registries).
/// </summary>
[RuntimeOnly(Reason = "Executes Smalltalk bytecode at runtime, must use heap-only access")]
public partial class VirtualMachine : IPrimitiveContext, IBytecodeContext, ICacheManager, IPrimitiveMessageSender
{
    private readonly ISmalltalkHeap _heapManager;

    private readonly RuntimeHeapAccessor _runtimeHeapAccess;
    private readonly LayoutResolver _layoutResolver;
    private readonly VMMetadata _metadata;
    private readonly IPlatformBridge? _platformBridge;
    private readonly IDiagnosticHub? _diagnostics;
    private readonly IStackManager _stackManager;
    private readonly IContextManager _contextManager;
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly IBytecodeInterpreter _bytecodeInterpreter;
    private readonly IVMDebugger _debugger;
    private readonly IPrimitiveRegistry _primitiveRegistry;
    private readonly SelectorCache _selectorCache;
    private readonly NonLocalReturnHandler _nlrHandler;
    private readonly ExecutionTracer _tracer;
    private readonly ITimerManager _timerManager;

    // Verbose debug mode for current init expression (controlled by Program.cs)
    private bool _verboseDebugMode = false;

    // Primitive success flag (Blue Book specification)
    private bool _primitiveSuccess;

    /// <summary>Gets the total number of instructions executed by this VM instance.</summary>
    public long InstructionCount => _tracer.InstructionCount;

    /// <summary>Resets the instruction counter to zero. Used before executing init expressions.</summary>
    public void ResetInstructionCount() => _tracer.ResetInstructionCount();

    // Trampoline state - used to avoid recursive ExecuteBytecodeLoop calls
    // (CallOutcome, CallOutcomeKind, InterpreterStatus moved to Interpreter/ExecutionState.cs)
    private SmalltalkOOP _returnValue;  // Return value from execution
    private SmalltalkOOP _initialContextOOP;  // Starting context for this execution
    private SmalltalkOOP _initialProcessOOP;  // Starting process for this execution (to detect forked process termination)
    private bool _contextSwitchPending;  // Signal that a context switch occurred (for trampoline)
    private bool _contextSwitchedByPrimitive;  // Primitive created new context (e.g., block invocation)

    // Non-local return state is now managed by _nlrHandler

    // Cached selector OOPs for fast-path conditional dispatch
    private SmalltalkOOP _ifTrueSelectorOOP;
    private SmalltalkOOP _ifFalseSelectorOOP;
    private SmalltalkOOP _ifTrueIfFalseSelectorOOP;
    private SmalltalkOOP _ifFalseIfTrueSelectorOOP;
    private SmalltalkOOP _whileTrueSelectorOOP;
    private SmalltalkOOP _whileFalseSelectorOOP;
    private bool _conditionalSelectorsInitialized;

    // Blue Book architectural constants are defined in VMConstants.cs
    // All context field indices are now static constants shared across the VM

    // Verbose debugging support
    public bool VerboseDebug { get; set; } = false;
    private readonly Dictionary<SmalltalkOOP, string> _methodSources = new();

    /// <summary>
    /// Cancellation token that signals when the VM should shut down.
    /// Checked periodically in the interpreter loop.
    /// </summary>
    public CancellationToken ShutdownToken { get; set; } = CancellationToken.None;

    // Public property to enable bytecode tracing from outside
    public bool EnableBytecodeTracing
    {
        get => _tracer.SelectorTraceEnabled;
        set => _tracer.SelectorTraceEnabled = value;
    }

    /// <summary>Gets the message dispatcher for runtime message sending.</summary>
    public IMessageDispatcher MessageDispatcher => _messageDispatcher;

    /// <summary>Gets the context manager for runtime context management.</summary>
    public IContextManager ContextManager => _contextManager;

    public VirtualMachine(ISmalltalkHeap heapManager,  LayoutResolver layoutResolver, VMMetadata metadata, IPlatformBridge? platformBridge = null, IDiagnosticHub? diagnostics = null)
    {
        _heapManager = heapManager;

        _layoutResolver = layoutResolver;
        _runtimeHeapAccess = new RuntimeHeapAccessor(heapManager, metadata);
        RuntimeSpecialObjects.Instance.Initialize(_runtimeHeapAccess);
        _metadata = metadata;
        _platformBridge = platformBridge;
        _diagnostics = diagnostics;

        // Initialize tracer first as other components may need it
        _tracer = new ExecutionTracer();

        // Initialize timer manager for Delay mechanism (primitive 100)
        _timerManager = new TimerManager();

        _contextManager = new ContextManager(
            heapManager,
            metadata,
            this, // IObjectMetadataAccessor
            diagnostics // For GC event notifications
        );

        // Initialize context manager to register for GC events
        _contextManager.Initialize();

        _stackManager = new StackManager(
            heapManager,
            metadata,
            this, // IObjectMetadataAccessor
            () => _contextManager.ActiveContext,
            () => _tracer.SelectorTraceEnabled,
            () => _tracer.PushTraceCounter,
            () => _tracer.IncrementPushTraceCounter()
        );

        _messageDispatcher = new MessageDispatcher(
            heapManager,
            this, // IObjectMetadataAccessor
            metadata
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
            this, // IBytecodeContext
            metadata
        );

        // Initialize primitive registry with new IPrimitive implementations
        // All 110 primitives migrated to IPrimitive pattern (2025-11-18)
        _primitiveRegistry = Primitives.PrimitiveRegistry.CreateWithNewPrimitives();

        // Initialize selector cache (populated later via InitializeSelectorCaches)
        _selectorCache = new SelectorCache(heapManager, metadata);

        // Initialize NLR handler
        _nlrHandler = new NonLocalReturnHandler(metadata.KnownObjects.Nil);
    }

    /// <summary>
    /// Initializes the selector OOP caches by looking up selectors in the Smalltalk heap.
    /// Must be called after cold start completes and all symbols are interned.
    /// </summary>
    public void InitializeSelectorCaches()
    {
        // Delegate to SelectorCache component
        _selectorCache.Initialize();

        // Pass selector caches to bytecode interpreter
        _bytecodeInterpreter.InitializeSelectorCaches(
            _selectorCache.ArithmeticSelectors!,
            _selectorCache.SpecialSelectors!);

        // Initialize conditional selector OOPs for fast-path dispatch
        _ifTrueSelectorOOP = _selectorCache.LookupSelector("ifTrue:");
        _ifFalseSelectorOOP = _selectorCache.LookupSelector("ifFalse:");
        _ifTrueIfFalseSelectorOOP = _selectorCache.LookupSelector("ifTrue:ifFalse:");
        _ifFalseIfTrueSelectorOOP = _selectorCache.LookupSelector("ifFalse:ifTrue:");
        _whileTrueSelectorOOP = _selectorCache.LookupSelector("whileTrue:");
        _whileFalseSelectorOOP = _selectorCache.LookupSelector("whileFalse:");
        _conditionalSelectorsInitialized = true;
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
    public void EnableSelectorTrace() => _tracer.StartSelectorTrace();

    /// <summary>
    /// Disables selector trace.
    /// </summary>
    public void DisableSelectorTrace() => _tracer.StopSelectorTrace();

    /// <summary>
    /// Gets or sets whether infinite loop detection is enabled.
    /// Disable this for interactive event loops that intentionally run forever.
    /// </summary>
    public bool InfiniteLoopDetectionEnabled
    {
        get => _tracer.InfiniteLoopDetectionEnabled;
        set => _tracer.InfiniteLoopDetectionEnabled = value;
    }

    /// <summary>
    /// Prints a trace message with proper indentation based on call depth.
    /// </summary>
    private void TraceMessage(string message)
    {
        if (_tracer.SelectorTraceEnabled)
        {
            string indent = new string(' ', _tracer.SelectorTraceDepth * 2);
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
        _contextManager.SetActiveContext(contextOOP);
        var result = InterpreterLoop();
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

        // Save initial process - forked processes should switch, not exit to C#
        _initialProcessOOP = GetActiveProcess();

        // Counter for periodic shutdown and idle checks (every N bytecodes)
        int periodicCheckCounter = 0;
        const int PeriodicCheckInterval = 1000;

        // Idle detection: pause briefly when running background process to reduce CPU
        const int BackgroundProcessPriority = 1;  // SystemRockBottomPriority
        const int MaxIdleWaitMs = 10;  // Maximum time to wait for events when idle

        while (true)
        {
            // Periodic checks (not every bytecode for performance)
            if (++periodicCheckCounter >= PeriodicCheckInterval)
            {
                periodicCheckCounter = 0;

                // Shutdown check
                if (ShutdownToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("VM shutdown requested", ShutdownToken);
                }

                // Check for expired timer and signal semaphore (for Delay mechanism)
                CheckAndSignalExpiredTimer();

                // Idle detection: if running background process, wait briefly for events/timers
                // This reduces CPU usage from 100% to near-zero when the system is idle
                if (_platformBridge != null)
                {
                    var activeProcess = GetActiveProcess();
                    if (activeProcess != _metadata.KnownObjects.Nil)
                    {
                        var priorityOOP = _heapManager.GetInstanceVariable(activeProcess, VMConstants.PROCESS_PRIORITY_INDEX);
                        if (priorityOOP.IsSmallInteger && priorityOOP.AsSmallInteger() == BackgroundProcessPriority)
                        {
                            // Calculate wait time: MIN(timer expiration, max idle wait)
                            int waitMs = MaxIdleWaitMs;
                            int timerMs = _timerManager.GetMillisecondsUntilNextTimer();
                            if (timerMs >= 0 && timerMs < waitMs)
                            {
                                waitMs = Math.Max(1, timerMs); // Wait at least 1ms
                            }

                            // Running background process - wait for events or timer
                            // This allows the OS to schedule other threads and reduces CPU
                            _platformBridge.WaitForEvents(waitMs);

                            // Check timer again after waking up
                            CheckAndSignalExpiredTimer();
                        }
                    }
                }
            }

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
        if (_nlrHandler.IsPending)
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[ExecuteBytecodeStep] NLR pending, continuing unwinding");
            return HandleReturn(_nlrHandler.Value);
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
            if (!_tracer.IncrementInstructionCount())
            {
                _tracer.ThrowIfInfiniteLoop();
            }

            // Report progress every 10K instructions during potential infinite loops (only when tracing is enabled)
            if (_tracer.ShouldTraceInstructionPeriodic())
            {
                var methodLocalId = GetLocalId(methodOOP) ?? "unknown";
                var ctxReceiverOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, VMConstants.CONTEXT_RECEIVER_INDEX);
                var className = GetClassName(GetClass(ctxReceiverOOP));
                _tracer.TraceInstructionPeriodic(className, methodLocalId, ip - 1);
            }

            // Special trace around the critical range
            if (_tracer.ShouldTraceBytecodeRange())
            {
                var methodLocalId = GetLocalId(methodOOP) ?? "unknown";
                _tracer.TraceBytecodeRange(ip - 1, bytecode, methodLocalId);
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
                // For non-local returns, the value was already popped and stored in NLR handler
                SmalltalkOOP returnValue;
                if (_nlrHandler.IsPending)
                {
                    // Non-local return: value already popped by RETURN_*_POP bytecode
                    returnValue = _nlrHandler.Value;
                    // if (_verboseDebugMode)
                    //     Console.WriteLine($"[ExecuteBytecodeStep] Non-local return pending, using _nlrHandler.Value: {returnValue}");
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
        SmalltalkOOP returningContext;

        // Handle non-local returns (NLR)
        if (_nlrHandler.IsPending)
        {
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding from {_contextManager.ActiveContext} to target {_nlrHandler.TargetContext}, value={_nlrHandler.Value}");

            // Check if current context is the NLR target
            if (_nlrHandler.IsTargetReached(_contextManager.ActiveContext))
            {
                // Target reached - clear NLR state and get the value
                SmalltalkOOP nlrValue = _nlrHandler.Complete();

                // Get sender to continue execution
                senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);

                // Save context to pool AFTER switching (must not be active)
                returningContext = _contextManager.ActiveContext;

                // Check if target was the initial context
                if (senderOOP == _initialContextOOP || senderOOP == _metadata.KnownObjects.Nil)
                {
                    _returnValue = nlrValue;
                    _contextManager.SetActiveContext(_initialContextOOP);
                    _contextManager.AddContextToPool(returningContext);
                    return InterpreterStatus.Return;
                }

                // Switch to sender and push NLR value
                _contextManager.SetActiveContext(senderOOP);
                _contextManager.AddContextToPool(returningContext);
                Push(nlrValue);
                return InterpreterStatus.Continue;
            }

            // Not the target - continue unwinding (don't push value)
            senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Unwinding through {_contextManager.ActiveContext}, next sender={senderOOP}");

            // Save context to pool AFTER switching (must not be active)
            returningContext = _contextManager.ActiveContext;

            // Check if we ran out of contexts (error case)
            if (senderOOP == _metadata.KnownObjects.Nil)
            {
                throw new InvalidOperationException(
                    $"Non-local return target context {_nlrHandler.TargetContext} not found in sender chain. " +
                    $"Target may have already returned.");
            }

            // Continue unwinding
            _contextManager.SetActiveContext(senderOOP);
            _contextManager.AddContextToPool(returningContext);
            // if (_verboseDebugMode)
            //     Console.WriteLine($"[HandleReturn NLR] Switched to sender {_contextManager.ActiveContext}, continuing unwinding");
            return InterpreterStatus.Continue;
        }

        // Normal return (not NLR)
        senderOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, senderIndex);

        // Emit MethodExitEvent for profiling/diagnostics
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.MethodExit) == true)
        {
            // Get method info from the returning context
            var returningMethodOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, VMConstants.CONTEXT_METHOD_INDEX);
            string? methodLocalId = GetLocalId(returningMethodOOP);

            // Parse "ClassName>>selector" format
            string className = "unknown";
            string selector = "unknown";
            if (methodLocalId != null)
            {
                int separatorIndex = methodLocalId.IndexOf(">>");
                if (separatorIndex > 0)
                {
                    className = methodLocalId[..separatorIndex];
                    selector = methodLocalId[(separatorIndex + 2)..];
                }
                else
                {
                    selector = methodLocalId;
                }
            }

            _diagnostics.Emit(new MethodExitEvent
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                InstructionCount = _tracer.InstructionCount,
                Selector = selector,
                ReceiverClass = className,
                ReturnValue = returnValue,
                CallDepth = _tracer.SelectorTraceDepth
            });
        }

        // Selector trace: log method return
        if (_tracer.SelectorTraceEnabled && _tracer.HasSelectorTraceEntries)
        {
            var (className, selector, depth) = _tracer.PopSelectorTrace();
            _tracer.SetSelectorTraceDepth(depth);  // Restore depth before decrement
            string returnValueStr = returnValue.IsSmallInteger
                ? returnValue.AsSmallInteger().ToString()
                : $"OOP({returnValue.RawValue})";
            TraceMessage($"← {className}>>{selector} => {returnValueStr}");
        }

        // Save context to pool AFTER switching (must not be active)
        returningContext = _contextManager.ActiveContext;

        // Handle nil sender - the top-level context is returning
        // This could be initial process completion OR forked process termination
        if (senderOOP == _metadata.KnownObjects.Nil)
        {
            // Check if we're in the initial process or a forked process
            SmalltalkOOP currentProcess = GetActiveProcess();

            if (currentProcess == _initialProcessOOP)
            {
                // Initial process completed - return to caller
                _returnValue = returnValue;
                _contextManager.SetActiveContext(_metadata.KnownObjects.Nil);
                _contextManager.AddContextToPool(returningContext);
                return InterpreterStatus.Return;
            }

            // Forked process terminated - switch to next runnable process
            // Debug: Log which process/context is terminating
            var termPriorityOOP = _heapManager.GetInstanceVariable(currentProcess, VMConstants.PROCESS_PRIORITY_INDEX);
            int termPriority = termPriorityOOP.IsSmallInteger ? termPriorityOOP.AsSmallInteger() : -1;
            if (termPriority == 1)
            {
                Console.WriteLine($"[PROCESS TERMINATING] Priority 1 (background) process={currentProcess}");
                Console.WriteLine($"  Returning context: {returningContext}");
                string returnValueStr;
                if (returnValue.IsSmallInteger)
                    returnValueStr = $"SmallInteger({returnValue.AsSmallInteger()})";
                else
                {
                    var rvClassName = _heapManager.GetClassName(returnValue);
                    returnValueStr = $"{returnValue} (class: {rvClassName})";
                }
                Console.WriteLine($"  Return value: {returnValueStr}");
                // Show context details
                var ctxClassOOP = _heapManager.GetClassOOP(returningContext);
                var ctxClassName = _heapManager.GetClassName(returningContext);
                Console.WriteLine($"  Context class: {ctxClassName}");
                if (ctxClassName == "BlockContext")
                {
                    var homeOOP = _heapManager.GetInstanceVariable(returningContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                    var startPCOOP = _heapManager.GetInstanceVariable(returningContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
                    Console.WriteLine($"  Block home: {homeOOP}, startPC: {startPCOOP}");
                    // Also show the home context's method selector
                    // Note: Literal 0 (selector) is at indexed slot 1 (slot 0 is header)
                    var homeMethodOOP = _heapManager.GetInstanceVariable(homeOOP, VMConstants.CONTEXT_METHOD_INDEX);
                    var literalCount = GetLiteralCount(homeMethodOOP);
                    if (literalCount > 0)
                    {
                        var selectorOOP = GetMethodLiteral(homeMethodOOP, 1); // slot 1 = literal 0 = selector
                        var selectorName = GetSymbolName(selectorOOP);
                        Console.WriteLine($"  Home method selector: {selectorName}");
                    }
                    // Dump bytecodes to see what's executing
                    int startPC = startPCOOP.IsSmallInteger ? startPCOOP.AsSmallInteger() : -1;
                    var bytecodes = GetMethodBytecodes(homeMethodOOP);
                    Console.WriteLine($"  Bytecodes: length={bytecodes.Length}, startPC={startPC}");
                    if (bytecodes.Length > 0)
                    {
                        Console.Write($"  All bytecodes: ");
                        for (int i = 0; i < Math.Min(30, bytecodes.Length); i++)
                        {
                            if (i == startPC) Console.Write("[");
                            Console.Write($"{bytecodes[i]} ");
                            if (i == startPC) Console.Write("] ");
                        }
                        Console.WriteLine(bytecodes.Length > 30 ? "..." : "");
                    }
                }
            }
            _contextManager.AddContextToPool(returningContext);

            if (SwitchToNextRunnableProcess(currentProcess))
            {
                // Successfully switched - continue execution in new process
                return InterpreterStatus.Continue;
            }

            // No runnable process found - this is the error condition we need to debug
            var processPriorityOOP = _heapManager.GetInstanceVariable(currentProcess, VMConstants.PROCESS_PRIORITY_INDEX);
            int processPriority = processPriorityOOP.IsSmallInteger ? processPriorityOOP.AsSmallInteger() : -1;
            Console.WriteLine($"[HandleReturn] ERROR: No runnable processes found!");
            Console.WriteLine($"  Terminating process: {currentProcess} (priority={processPriority})");
            _returnValue = returnValue;
            return InterpreterStatus.Return;
        }

        // Switch to sender context and push return value
        _contextManager.SetActiveContext(senderOOP);
        _contextManager.AddContextToPool(returningContext);
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
        //     if (selectorOOP == _metadata.KnownObjects.Nil)
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
            if (_tracer.SelectorTraceEnabled && _tracer.BytecodeInstructionCounter < 100)
            {
                _tracer.IncrementBytecodeCounter();

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

                Console.WriteLine($"[BYTECODE #{_tracer.BytecodeInstructionCounter}] IP={(ip-1)}, {opcodeDesc}");
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
                // If so, the return value is already in NLR handler and we must not pop again
                if (_nlrHandler.IsPending)
                {
                    // Non-local return: value already popped and stored in NLR handler
                    return _nlrHandler.Value;
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
        return _metadata.KnownObjects.Nil;
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
    /// MethodHeaderInfo record is defined in IObjectMetadataAccessor.cs.
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

    // Explicit interface implementations for IObjectMetadataAccessor
    // These delegate to the existing private methods while maintaining encapsulation
    SmalltalkOOP IObjectMetadataAccessor.GetClass(SmalltalkOOP objectOOP) => GetClass(objectOOP);
    string IObjectMetadataAccessor.GetClassName(SmalltalkOOP classOOP) => GetClassName(classOOP);
    SmalltalkOOP IObjectMetadataAccessor.FindMethodContext(SmalltalkOOP contextOOP) => _contextManager.FindMethodContext(contextOOP);
    MethodHeaderInfo IObjectMetadataAccessor.ParseMethodHeader(SmalltalkOOP methodOOP) => ParseMethodHeader(methodOOP);

    // IStackOperations implementation
    void IStackOperations.Push(SmalltalkOOP valueOOP) => Push(valueOOP);
    SmalltalkOOP IStackOperations.Pop() => Pop();
    SmalltalkOOP IStackOperations.Peek() => Peek();

    // INonLocalReturnState implementation
    bool INonLocalReturnState.NonLocalReturnPending
    {
        get => _nlrHandler.IsPending;
        set => _nlrHandler.SetPending(value);
    }

    SmalltalkOOP INonLocalReturnState.NLRTargetContext
    {
        get => _nlrHandler.TargetContext;
        set => _nlrHandler.SetTargetContext(value);
    }

    SmalltalkOOP INonLocalReturnState.NLRValue
    {
        get => _nlrHandler.Value;
        set => _nlrHandler.SetValue(value);
    }

    // IMethodAccess implementation
    SmalltalkOOP IMethodAccess.GetMethodLiteral(SmalltalkOOP methodOOP, int index) => GetMethodLiteral(methodOOP, index);
    byte[] IMethodAccess.GetMethodBytecodes(SmalltalkOOP methodOOP) => GetMethodBytecodes(methodOOP);

    // IMessageSender implementation
    bool IMessageSender.SendMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        // Fast path for conditional dispatch on True/False
        if (_conditionalSelectorsInitialized)
        {
            var trueOOP = _metadata.KnownObjects.True;
            var falseOOP = _metadata.KnownObjects.False;

            // ifTrue: - 1 arg
            if (selectorOOP == _ifTrueSelectorOOP && args.Length == 1)
            {
                if (receiverOOP == trueOOP)
                {
                    // True>>ifTrue: aBlock → aBlock value
                    return ActivateBlockForConditional(args[0]);
                }
                else if (receiverOOP == falseOOP)
                {
                    // False>>ifTrue: aBlock → nil
                    _stackManager.Push(_metadata.KnownObjects.Nil);
                    return true;
                }
            }
            // ifFalse: - 1 arg
            else if (selectorOOP == _ifFalseSelectorOOP && args.Length == 1)
            {
                if (receiverOOP == trueOOP)
                {
                    // True>>ifFalse: aBlock → nil
                    _stackManager.Push(_metadata.KnownObjects.Nil);
                    return true;
                }
                else if (receiverOOP == falseOOP)
                {
                    // False>>ifFalse: aBlock → aBlock value
                    return ActivateBlockForConditional(args[0]);
                }
            }
            // ifTrue:ifFalse: - 2 args
            else if (selectorOOP == _ifTrueIfFalseSelectorOOP && args.Length == 2)
            {
                if (receiverOOP == trueOOP)
                {
                    // True>>ifTrue:ifFalse: → trueBlock value
                    return ActivateBlockForConditional(args[0]);
                }
                else if (receiverOOP == falseOOP)
                {
                    // False>>ifTrue:ifFalse: → falseBlock value
                    return ActivateBlockForConditional(args[1]);
                }
            }
            // ifFalse:ifTrue: - 2 args
            else if (selectorOOP == _ifFalseIfTrueSelectorOOP && args.Length == 2)
            {
                if (receiverOOP == trueOOP)
                {
                    // True>>ifFalse:ifTrue: → trueBlock value
                    return ActivateBlockForConditional(args[1]);
                }
                else if (receiverOOP == falseOOP)
                {
                    // False>>ifFalse:ifTrue: → falseBlock value
                    return ActivateBlockForConditional(args[0]);
                }
            }
        }

        var outcome = SendMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    /// <summary>
    /// Activates a block for conditional dispatch (ifTrue:/ifFalse: fast path).
    /// Returns false if block was activated (context switch), true otherwise.
    /// </summary>
    private bool ActivateBlockForConditional(SmalltalkOOP blockOOP)
    {
        // Check if it's actually a BlockContext
        var blockClassOOP = _heapManager.GetClassOOP(blockOOP);
        if (blockClassOOP == _metadata.BlockContextClassOOP)
        {
            // Use the existing ActivateBlock implementation
            if (((IBytecodeContext)this).ActivateBlock(blockOOP, []))
            {
                return false; // Context switched
            }
        }
        // Not a BlockContext or activation failed - push nil and continue
        // (This handles edge cases like nil blocks)
        _stackManager.Push(_metadata.KnownObjects.Nil);
        return true;
    }

    bool IMessageSender.SendSuperMessageAndContinue(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
    {
        var outcome = SendSuperMessage(receiverOOP, selectorOOP, args);
        return ConsumeCallOutcome(outcome);
    }

    // IBytecodeContext implementation
    string? IBytecodeContext.GetSymbolName(SmalltalkOOP symbolOOP) => GetSymbolName(symbolOOP);
    string? IBytecodeContext.GetLocalId(SmalltalkOOP objectOOP) => GetLocalId(objectOOP);
    SmalltalkOOP IBytecodeContext.GetTemporaryVariable(SmalltalkOOP contextOOP, int index) => GetTemporaryVariable(contextOOP, index);
    void IBytecodeContext.SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP) => SetTemporaryVariable(contextOOP, index, valueOOP);
    SmalltalkOOP IBytecodeContext.ResolveAndPatchPoolVariable(SmalltalkOOP poolLiteralOOP, SmalltalkOOP methodOOP, int literalIndex, SmalltalkOOP poolNameSymbol, SmalltalkOOP keySymbol)
        => ResolveAndPatchPoolVariable(poolLiteralOOP, methodOOP, literalIndex, poolNameSymbol, keySymbol);
    SmalltalkOOP IBytecodeContext.LookupInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol)
        => LookupInDictionary(dictionaryOOP, keySymbol);
    SmalltalkOOP IBytecodeContext.ActiveContext => _contextManager.ActiveContext;

    bool IBytecodeContext.ContextSwitchPending
    {
        get => _contextSwitchPending;
        set => _contextSwitchPending = value;
    }

    void IBytecodeContext.DumpStack(string label) => _debugger.DumpStack(label);

    bool IBytecodeContext.IsInSenderChain(SmalltalkOOP targetOOP, SmalltalkOOP startOOP)
        => _contextManager.IsInSenderChain(targetOOP, startOOP);

    void IBytecodeContext.AddAssociationToClassPoolIfNeeded(SmalltalkOOP classOOP, SmalltalkOOP associationOOP)
        => AddAssociationToClassPoolIfNeeded(classOOP, associationOOP);

    bool IBytecodeContext.ActivateBlock(SmalltalkOOP blockContextOOP, SmalltalkOOP[] args)
    {
        // Get block's expected argument count
        var blockArgCountOOP = _heapManager.GetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
        if (!blockArgCountOOP.IsSmallInteger) return false;

        int blockArgCount = blockArgCountOOP.AsSmallInteger();

        // Verify argument count matches
        if (args.Length != blockArgCount) return false;

        // Get initialIP from blockContext
        var initialIP = _heapManager.GetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);

        // Set PC to initialIP
        _heapManager.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_PC_INDEX, initialIP);

        // BLUE BOOK: Push arguments onto BlockContext's stack (in order)
        // The block's first bytecodes (POP_STORE_TEMP) will pop them into correct temp slots
        for (int i = 0; i < args.Length; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, args[i]);
        }

        // Set stack pointer to argument count (points to next free slot after arguments)
        _heapManager.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_STACKP_INDEX,
            SmalltalkOOP.FromSmallInteger(args.Length));

        // Set caller to current active context
        _heapManager.SetInstanceVariable(blockContextOOP, VMConstants.BLOCKCONTEXT_CALLER_INDEX,
            _contextManager.ActiveContext);

        // Switch to the BlockContext
        _contextManager.SetActiveContext(blockContextOOP);
        _contextSwitchPending = true;

        return true;
    }

    void IBytecodeContext.MarkContextCreatedBlock(SmalltalkOOP homeContextOOP)
        => _contextManager.MarkContextCreatedBlock(homeContextOOP);

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

        while (currentOOP != _metadata.KnownObjects.Nil && depth < MAX_DEPTH)
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
        var classOOP = _heapManager.GetClassOOP(objectOOP);
        return classOOP;
    }

    private string GetClassName(SmalltalkOOP classOOP)
    {
        // Check if this is a metaclass by trying to read thisClass field
        // All metaclass instances (meta:X) have thisClass pointing to their instance class (class:X)
        try
        {
            var thisClassOOP = _heapManager.GetInstanceVariable(classOOP, _metadata.MetaclassThisClassFieldIndex);
            if (thisClassOOP != _metadata.KnownObjects.Nil && thisClassOOP != _heapManager.GetNil())
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
    ISmalltalkHeap IPrimitiveContext.HeapManager => _heapManager;
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
            return _metadata.KnownObjects.Nil;
        }
        else
        {
            // Other outcomes (VMException, NonLocalReturn) - return nil
            // In legacy perform primitives, these were also handled
            return _metadata.KnownObjects.Nil;
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
        if (classOOP == _metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException($"Class {classNameClean} not found in heap");
        }

        // Console.WriteLine($"[AllocateObject] className={classNameClean}, classOOP={classOOP}, indexedSize={indexedSize}");

        // Read the format field from the class object to determine the proper ObjectFormat
        // Per Blue Book Chapter 27, all classes (Behavior subclasses) have format at instance variable index 2
        var formatOOP = _heapManager.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_FORMAT_INDEX);
        if (!formatOOP.IsSmallInteger)
        {
            throw new InvalidOperationException($"Class {classNameClean} format field is not a SmallInteger: {formatOOP}");
        }

        // Decode format using the FormatWord semantic wrapper
        var formatWord = new FormatWord(formatOOP.AsSmallInteger());
        int fixedSize = formatWord.FixedFields;

        // Convert to ObjectFormat enum
        ObjectFormat format;
        if (formatWord.IsPointers)
        {
            format = formatWord.IsIndexable ? ObjectFormat.PointerIndexed : ObjectFormat.Pointer;
        }
        else if (formatWord.IsIndexable)
        {
            format = formatWord.IsWords ? ObjectFormat.WordIndexed : ObjectFormat.ByteIndexed;
        }
        else
        {
            // Non-pointer, non-indexable (shouldn't occur in practice)
            format = ObjectFormat.Pointer;
        }

        // Allocate the object
        var objectOOP = _heapManager.AllocateObject(format, fixedSize, indexedSize);

        // Set the class reference
        _heapManager.SetClassOOP(objectOOP, classOOP);

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
        _heapManager.SetClassOOP(blockContextOOP, blockContextClassOOP); // Set class reference

        // Get field indices using LayoutResolver
        int senderIndex = VMConstants.BLOCKCONTEXT_CALLER_INDEX;
        int pcIndex = VMConstants.BLOCKCONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int nargsIndex = VMConstants.BLOCKCONTEXT_NARGS_INDEX;
        int startpcIndex = VMConstants.BLOCKCONTEXT_STARTPC_INDEX;
        int homeIndex = VMConstants.BLOCKCONTEXT_HOME_INDEX;

        // Initialize BlockContext fields (all 6 fixed fields)
        _heapManager.SetInstanceVariable(blockContextOOP, senderIndex, _metadata.KnownObjects.Nil); // sender initially nil
        _heapManager.SetInstanceVariable(blockContextOOP, pcIndex, _metadata.KnownObjects.Nil); // pc initially nil
        _heapManager.SetInstanceVariable(blockContextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(0)); // Stack starts empty
        _heapManager.SetInstanceVariable(blockContextOOP, nargsIndex, SmalltalkOOP.FromSmallInteger(argCount));
        _heapManager.SetInstanceVariable(blockContextOOP, startpcIndex, SmalltalkOOP.FromSmallInteger(startPC));
        _heapManager.SetInstanceVariable(blockContextOOP, homeIndex, actualHomeOOP);

        // Initialize indexed fields to nil
        for (int i = 0; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(blockContextOOP, i, _metadata.KnownObjects.Nil);
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
            return _metadata.KnownObjects.Nil;
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
            return _metadata.KnownObjects.Nil;
        }

        int startPC = startPcOOP.AsSmallInteger();
        int expectedArgCount = nargsOOP.AsSmallInteger();

        // Verify argument count matches
        var argumentsOOP = argumentsArray;
        int providedArgCount = _heapManager.GetIndexedSize(argumentsOOP);
        if (providedArgCount != expectedArgCount)
        {
            // Argument count mismatch - this will cause primitive to fail
            return _metadata.KnownObjects.Nil;
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
        if (metaclassOOP != _metadata.KnownObjects.Nil)
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
        var nilOOP = _metadata.KnownObjects.Nil;
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
        if (dictClassOOP == _metadata.KnownObjects.Nil)
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

        _heapManager.SetClassOOP(dictOOP, dictClassOOP);

        // Set tally to 0 (Dictionary inherits tally from Set at index 0)
        _heapManager.SetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX, SmalltalkOOP.FromSmallInteger(0));
        
        var nilOOP = _metadata.KnownObjects.Nil;
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
            if (existingAssocOOP == _metadata.KnownObjects.Nil)
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
            if (slotOOP == _metadata.KnownObjects.Nil)
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
        return _metadata.KnownObjects.Nil;
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
        // Swaps the ObjectTable entries - all existing OOP references are implicitly updated
        // since OOPs are just indices into the ObjectTable

        _heapManager.Become.BecomeTwoWay(oop1, oop2);
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

        // Get USTable from Symbol's classPool (it's a class variable, not a global)
        var ustableOOP = _runtimeHeapAccess.LookupClassVariable("Symbol", "USTable");
        if (ustableOOP == _metadata.KnownObjects.Nil)
        {
            // USTable not yet created (shouldn't happen after cold start)
            return _metadata.KnownObjects.Nil;
        }

        // USTable is an Array of Symbol OOPs (with nils for empty slots)
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = _metadata.KnownObjects.Nil;

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
        return _metadata.KnownObjects.Nil;
    }

    SmalltalkOOP IPrimitiveContext.GetGlobalObjectByName(string globalName)
    {
        // HEAP-ONLY IMPLEMENTATION: Access Smalltalk global dictionary from heap,
        // NOT from GlobalRegistry (which is a cold start snapshot that becomes stale)

        // Get Smalltalk SystemDictionary from KnownObjects
        var smalltalkOOP = _metadata.KnownObjects.Smalltalk;
        if (smalltalkOOP == _metadata.KnownObjects.Nil)
        {
            // SystemDictionary not yet created (shouldn't happen after cold start)
            return _metadata.KnownObjects.Nil;
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
        if (keySymbolOOP == _metadata.KnownObjects.Nil)
        {
            // Symbol not found - global doesn't exist
            return _metadata.KnownObjects.Nil;
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
        var nilOOP = _metadata.KnownObjects.Nil;

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
        return _metadata.KnownObjects.Nil;
    }

    string? IPrimitiveContext.GetClassNameForObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
        {
            return "class:SmallInteger";
        }

        // Get the class OOP from slot 0 of the object
        var classOOP = _heapManager.GetClassOOP(objectOOP);
        if (classOOP == _metadata.KnownObjects.Nil || classOOP == _heapManager.GetNil())
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
        var smalltalkDict = _metadata.KnownObjects.Smalltalk;
        var poolDictOOP = LookupInDictionary(smalltalkDict, poolNameSymbol);
        
        if (poolDictOOP == _metadata.KnownObjects.Nil)
        {
            // Pool dictionary not found - fall back to Undeclared
            // This handles cases where the pool dictionary is created by an initialize method
            var poolNameString = GetSymbolString(poolNameSymbol);
            Console.WriteLine($"[POOL VAR FALLBACK] Pool dictionary '{poolNameString}' not found, checking Undeclared");

            // Look up in Undeclared dictionary
            var systemDictOOP = _metadata.KnownObjects.Smalltalk;
            var undeclaredOOP = _runtimeHeapAccess.LookupGlobalByName(systemDictOOP, "Undeclared");
            if (undeclaredOOP != _metadata.KnownObjects.Nil)
            {
                var undeclaredValueOOP = LookupInDictionary(undeclaredOOP, keySymbol);
                if (undeclaredValueOOP != _metadata.KnownObjects.Nil)
                {
                    Console.WriteLine($"[POOL VAR FALLBACK] Found '{GetSymbolString(keySymbol)}' in Undeclared");
                    // Return the Association from Undeclared (don't patch, as pool dict may be created later)
                    return undeclaredValueOOP;
                }
            }
            
            throw new InvalidOperationException($"Pool dictionary not found: {poolNameString}, and variable '{GetSymbolString(keySymbol)}' not in Undeclared");
        }
        
        // 2. Look up key in pool dictionary - get the Association, not just the value
        // This allows us to distinguish "not found" from "value is nil"
        var resolvedAssocOOP = LookupAssociationInDictionary(poolDictOOP, keySymbol);

        if (resolvedAssocOOP == _metadata.KnownObjects.Nil)
        {
            // Key not found in pool dictionary
            var poolNameString = GetSymbolString(poolNameSymbol);
            var keyString = GetSymbolString(keySymbol);
            throw new InvalidOperationException($"Pool variable '{keyString}' not found in pool dictionary '{poolNameString}'");
        }

        // 3. Patch literal: Replace poolNameSymbol with poolDictOOP
        // This makes subsequent executions faster (no name lookup)
        _heapManager.SetIndexedOOP(poolLiteralOOP, 0, poolDictOOP);

        // Return the Association's value
        var resolvedValueOOP = _heapManager.GetInstanceVariable(resolvedAssocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
        return resolvedValueOOP;
    }

    /// <summary>
    /// Looks up a key (Symbol) in a Dictionary and returns the Association OOP.
    /// Returns nil if key not found.
    /// Uses hash-based lookup matching Smalltalk's Dictionary>>at: semantics.
    /// </summary>
    private SmalltalkOOP LookupAssociationInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol)
    {
        // Dictionary is a variableSubclass of Set with:
        //   Instance variable [0]: tally (inherited from Set)
        //   Indexed slots [0..N]: Association array
        int arraySize = _heapManager.GetIndexedSize(dictionaryOOP);
        if (arraySize == 0) return _metadata.KnownObjects.Nil;

        // Find starting slot (matching Smalltalk's Dictionary>>findKeyOrNil:)
        int startIndex = IdentityHashUtility.ComputeDictionaryHashIndex(keySymbol, arraySize);
        int index = startIndex;

        // Linear probe until we find the key or hit nil
        do
        {
            var elementOOP = _heapManager.GetIndexedOOP(dictionaryOOP, index);

            if (elementOOP == _metadata.KnownObjects.Nil)
            {
                // Empty slot - key not in dictionary
                return _metadata.KnownObjects.Nil;
            }

            // Element should be an Association
            var assocKeyOOP = _heapManager.GetInstanceVariable(elementOOP, VMConstants.ASSOCIATION_KEY_INDEX);

            if (assocKeyOOP == keySymbol)
            {
                // Found it - return the Association OOP
                return elementOOP;
            }

            // Linear probe to next slot
            index = (index + 1) % arraySize;
        } while (index != startIndex);

        // Wrapped around completely - not found
        return _metadata.KnownObjects.Nil;
    }

    /// <summary>
    /// Looks up a key (Symbol) in a Dictionary and returns the value.
    /// Returns nil if key not found.
    /// Uses hash-based lookup matching Smalltalk's Dictionary>>at: semantics.
    /// </summary>
    private SmalltalkOOP LookupInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbol)
    {
        var assocOOP = LookupAssociationInDictionary(dictionaryOOP, keySymbol);
        if (assocOOP == _metadata.KnownObjects.Nil)
        {
            return _metadata.KnownObjects.Nil;
        }
        return _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
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

    #region Process Scheduling Support

    /// <summary>
    /// Checks if a timer has expired and signals the associated semaphore.
    /// This is the core mechanism for the Delay class.
    /// Called periodically from the interpreter loop.
    /// </summary>
    private void CheckAndSignalExpiredTimer()
    {
        var semaphoreOOP = _timerManager.GetExpiredTimerSemaphore();
        if (semaphoreOOP == default || semaphoreOOP == _metadata.KnownObjects.Nil)
            return;

        // Signal the semaphore by incrementing excessSignals or waking a waiter
        // Semaphore layout: firstLink (0), lastLink (1), excessSignals (2)
        var firstLinkOOP = _heapManager.GetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);

        if (firstLinkOOP == _metadata.KnownObjects.Nil)
        {
            // No processes waiting - increment excessSignals
            var excessOOP = _heapManager.GetInstanceVariable(semaphoreOOP, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX);
            if (excessOOP.IsSmallInteger)
            {
                int excess = excessOOP.AsSmallInteger();
                _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.SEMAPHORE_EXCESSSIGNALS_INDEX,
                    SmalltalkOOP.FromSmallInteger(excess + 1));
            }
        }
        else
        {
            // Process is waiting - wake it up by moving to ready queue
            // Remove first process from semaphore's wait list
            var processOOP = firstLinkOOP;
            var nextLinkOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
            _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, nextLinkOOP);
            if (nextLinkOOP == _metadata.KnownObjects.Nil)
            {
                _heapManager.SetInstanceVariable(semaphoreOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, _metadata.KnownObjects.Nil);
            }

            // Clear process's myList and nextLink
            _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
            _heapManager.SetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);

            // Add process to its priority queue (make it runnable)
            var priorityOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX);
            if (priorityOOP.IsSmallInteger)
            {
                int priority = priorityOOP.AsSmallInteger();
                AddProcessToReadyQueue(processOOP, priority);
            }
        }
    }

    /// <summary>
    /// Adds a process to the appropriate priority ready queue.
    /// </summary>
    private void AddProcessToReadyQueue(SmalltalkOOP processOOP, int priority)
    {
        var schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return;

        var processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
            return;

        // Get the LinkedList for this priority (index = priority - 1)
        int index = priority - 1;
        if (index < 0 || index >= _heapManager.GetIndexedSize(processListsOOP))
            return;

        var listOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
        if (listOOP == _metadata.KnownObjects.Nil)
            return;

        // Add process to end of list (LinkedList>>addLast:)
        var lastLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);
        if (lastLinkOOP == _metadata.KnownObjects.Nil)
        {
            // List is empty
            _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, processOOP);
        }
        else
        {
            // Append to end
            _heapManager.SetInstanceVariable(lastLinkOOP, VMConstants.LINK_NEXTLINK_INDEX, processOOP);
        }
        _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, processOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, listOOP);
    }

    /// <summary>
    /// Gets the currently active process OOP from ProcessorScheduler.
    /// </summary>
    private SmalltalkOOP GetActiveProcess()
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        return _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);
    }

    /// <summary>
    /// Switches execution to the next runnable process when the current process terminates.
    /// Used by HandleReturn when a forked process completes its block.
    /// Returns true if successfully switched, false if no runnable processes found.
    /// </summary>
    private bool SwitchToNextRunnableProcess(SmalltalkOOP terminatingProcess)
    {
        SmalltalkOOP schedulerOOP = _metadata.ProcessorScheduler;
        if (schedulerOOP == _metadata.KnownObjects.Nil)
            return false;

        // Find highest priority runnable process
        SmalltalkOOP nextProcess = FindHighestPriorityRunnableProcess(schedulerOOP);
        if (nextProcess == _metadata.KnownObjects.Nil)
        {
            // This is the error condition - dump queues for debugging
            Console.WriteLine($"[SwitchToNextRunnableProcess] No runnable process found!");
            DumpProcessQueues(schedulerOOP);
            return false;
        }

        // Remove the process from its priority queue (it's about to become active)
        SmalltalkOOP myListOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_MYLIST_INDEX);
        if (myListOOP != _metadata.KnownObjects.Nil)
        {
            // Get process priority for logging (only log priority 1 - background process)
            var priorityOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_PRIORITY_INDEX);
            int priority = priorityOOP.IsSmallInteger ? priorityOOP.AsSmallInteger() : -1;
            if (priority == 1)
                Console.WriteLine($"[PQ-] REMOVE process={nextProcess} from priority={priority} (activating after termination)");

            RemoveProcessFromList(nextProcess, myListOOP);
            _heapManager.SetInstanceVariable(nextProcess, VMConstants.PROCESS_MYLIST_INDEX, _metadata.KnownObjects.Nil);
        }

        // Get the suspended context from the next process
        SmalltalkOOP nextContext = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
        if (nextContext == _metadata.KnownObjects.Nil)
            return false;

        // Debug: Log when activating priority 1 process
        var activatingPriorityOOP = _heapManager.GetInstanceVariable(nextProcess, VMConstants.PROCESS_PRIORITY_INDEX);
        int activatingPriority = activatingPriorityOOP.IsSmallInteger ? activatingPriorityOOP.AsSmallInteger() : -1;
        if (activatingPriority == 1)
        {
            Console.WriteLine($"[ACTIVATING PROCESS] Priority 1 (background) process={nextProcess}");
            Console.WriteLine($"  Suspended context: {nextContext}");
            var ctxClassName = _heapManager.GetClassName(nextContext);
            Console.WriteLine($"  Context class: {ctxClassName}");
            var pcOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX);
            var startPCOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            var callerOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX);
            Console.WriteLine($"  PC: {pcOOP}, startPC: {startPCOOP}, caller: {callerOOP}");
            if (ctxClassName == "BlockContext")
            {
                var homeOOP = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                Console.WriteLine($"  Home context: {homeOOP}");
                var homeClassName = _heapManager.GetClassName(homeOOP);
                Console.WriteLine($"  Home class: {homeClassName}");
                if (homeClassName == "MethodContext")
                {
                    var methodOOP = _heapManager.GetInstanceVariable(homeOOP, VMConstants.CONTEXT_METHOD_INDEX);
                    Console.WriteLine($"  Method: {methodOOP}");
                }
            }
        }

        // Check if this is an unactivated BlockContext (PC is nil)
        // This happens when resuming a newly created process via [block] newProcess
        SmalltalkOOP nextContextPC = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX);
        if (nextContextPC == _metadata.KnownObjects.Nil)
        {
            // Activate the block context - set PC from startPC
            SmalltalkOOP startPC = _heapManager.GetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STARTPC_INDEX);
            if (startPC == _metadata.KnownObjects.Nil || !startPC.IsSmallInteger)
                return false;

            if (activatingPriority == 1)
                Console.WriteLine($"  Activating block: setting PC={startPC.AsSmallInteger()}, stackp=0, caller=nil");

            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_PC_INDEX, startPC);
            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));
            _heapManager.SetInstanceVariable(nextContext, VMConstants.BLOCKCONTEXT_CALLER_INDEX, _metadata.KnownObjects.Nil);
        }

        // Update Processor's activeProcess
        _heapManager.SetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX, nextProcess);

        // Switch to the new context
        _contextManager.SetActiveContext(nextContext);

        return true;
    }

    /// <summary>
    /// Finds the highest priority process that is runnable (on a quiescentProcessList).
    /// Searches from priority 8 down to priority 1.
    /// </summary>
    private SmalltalkOOP FindHighestPriorityRunnableProcess(SmalltalkOOP schedulerOOP)
    {
        SmalltalkOOP processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        // Search from highest priority (8) down to lowest (1)
        // Array indices are 0-7 for priorities 1-8
        for (int priority = 8; priority >= 1; priority--)
        {
            int index = priority - 1;
            SmalltalkOOP priorityListOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
            if (priorityListOOP == _metadata.KnownObjects.Nil)
                continue;

            // Check if list has any processes (firstLink != nil)
            SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
            if (firstLinkOOP != _metadata.KnownObjects.Nil)
            {
                return firstLinkOOP;
            }
        }

        return _metadata.KnownObjects.Nil;
    }

    /// <summary>
    /// Diagnostic method to dump the contents of all process queues.
    /// </summary>
    private void DumpProcessQueues(SmalltalkOOP schedulerOOP)
    {
        SmalltalkOOP processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        if (processListsOOP == _metadata.KnownObjects.Nil)
        {
            Console.WriteLine("  quiescentProcessLists is nil!");
            return;
        }

        int arraySize = _heapManager.GetIndexedSize(processListsOOP);
        Console.WriteLine($"  quiescentProcessLists array size: {arraySize}");

        for (int priority = 1; priority <= 8; priority++)
        {
            int index = priority - 1;
            if (index >= arraySize)
            {
                Console.WriteLine($"  Priority {priority}: index out of bounds");
                continue;
            }

            SmalltalkOOP priorityListOOP = _heapManager.GetIndexedOOP(processListsOOP, index);
            if (priorityListOOP == _metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"  Priority {priority}: LinkedList is nil");
                continue;
            }

            SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
            SmalltalkOOP lastLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);

            if (firstLinkOOP == _metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"  Priority {priority}: empty (firstLink=nil)");
            }
            else
            {
                // Count processes in the list
                int count = 0;
                SmalltalkOOP current = firstLinkOOP;
                while (current != _metadata.KnownObjects.Nil && count < 100)
                {
                    count++;
                    SmalltalkOOP suspCtx = _heapManager.GetInstanceVariable(current, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
                    Console.WriteLine($"  Priority {priority}: Process {current} (suspendedContext={suspCtx})");
                    current = _heapManager.GetInstanceVariable(current, VMConstants.LINK_NEXTLINK_INDEX);
                }
            }
        }
    }

    /// <summary>
    /// Removes a process from a linked list (LinkedList structure).
    /// </summary>
    private void RemoveProcessFromList(SmalltalkOOP processOOP, SmalltalkOOP listOOP)
    {
        SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX);
        SmalltalkOOP lastLinkOOP = _heapManager.GetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX);

        if (processOOP == firstLinkOOP)
        {
            // Process is first in list
            SmalltalkOOP nextLinkOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
            _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_FIRSTLINK_INDEX, nextLinkOOP);

            if (processOOP == lastLinkOOP)
            {
                // Was also the last (only process in list)
                _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, _metadata.KnownObjects.Nil);
            }
        }
        else
        {
            // Process is not first - find predecessor
            SmalltalkOOP prevLinkOOP = firstLinkOOP;
            while (prevLinkOOP != _metadata.KnownObjects.Nil)
            {
                SmalltalkOOP nextLinkOOP = _heapManager.GetInstanceVariable(prevLinkOOP, VMConstants.LINK_NEXTLINK_INDEX);
                if (nextLinkOOP == processOOP)
                {
                    // Found predecessor - relink
                    SmalltalkOOP afterRemoved = _heapManager.GetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX);
                    _heapManager.SetInstanceVariable(prevLinkOOP, VMConstants.LINK_NEXTLINK_INDEX, afterRemoved);

                    if (processOOP == lastLinkOOP)
                    {
                        _heapManager.SetInstanceVariable(listOOP, VMConstants.LINKEDLIST_LASTLINK_INDEX, prevLinkOOP);
                    }
                    break;
                }
                prevLinkOOP = nextLinkOOP;
            }
        }

        // Clear the removed process's next link
        _heapManager.SetInstanceVariable(processOOP, VMConstants.LINK_NEXTLINK_INDEX, _metadata.KnownObjects.Nil);
    }

    #endregion
}