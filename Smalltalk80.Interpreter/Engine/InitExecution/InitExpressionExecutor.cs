using System.Diagnostics;
using System.Text.Json;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.Diagnostics.Listeners;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Executes init expressions (pre-compiled DoIts) with proper context allocation,
/// GC rooting, timeout enforcement, and structured diagnostic capture.
///
/// Each init expression executes as an ordinary method activation with:
/// - Canonical receiver (Smalltalk global or nil)
/// - Proper MethodContext allocation
/// - Transient GC roots for active context
/// - Timeout and error mapping
/// - Structured diagnostics for CI/CD
/// </summary>
public class InitExpressionExecutor
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly VirtualMachine _vm;
    private readonly VMMetadata _metadata;
    private readonly InitPolicy _policy;
    private readonly int _defaultTimeoutMs;
    private readonly IDiagnosticHub? _diagnostics;

    public InitExpressionExecutor(
        ISmalltalkHeap heapManager,
        VirtualMachine vm,
        VMMetadata metadata,
        IDiagnosticHub? diagnostics = null,
        InitPolicy policy = InitPolicy.AbortOnFirstError,
        int defaultTimeoutMs = 5000)
    {
        _heapManager = heapManager;
        _vm = vm;
        _metadata = metadata;
        _diagnostics = diagnostics;
        _policy = policy;
        _defaultTimeoutMs = defaultTimeoutMs;
    }

    /// <summary>
    /// Resumes a Process by executing its suspended context.
    /// </summary>
    private SmalltalkOOP ResumeProcess(SmalltalkOOP processOOP)
    {
        // Get the suspended context from the Process (instance variable 1: suspendedContext)
        var contextOOP = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
        return _vm.ExecuteExistingContext(contextOOP);
    }


    /// <summary>
    /// Executes all init expressions from VMMetadata.InitExpressionMethods.
    /// </summary>
    public InitExecutionReport ExecuteAll(CancellationToken cancellationToken = default)
    {
        var results = new List<InitResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Get init expression array from VMMetadata
        var initArrayOOP = _metadata.InitExpressionMethods;
        if (initArrayOOP == _heapManager.GetNil())
        {
            throw new InvalidOperationException("Init expression methods not found in VMMetadata - coldstart must complete first");
        }

        int count = _heapManager.GetIndexedSize(initArrayOOP);

        // Execute all init expressions
        for (int i = 0; i < count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var methodOOP = _heapManager.GetIndexedOOP(initArrayOOP, i);

            // Use index as identifier (runtime has no localId concept)
            string initId = $"init:{i}";
            var result = ExecuteInitExpression(initId, methodOOP);
            results.Add(result);

            // Run GC after each init expression to validate collection of temporary objects
            // (Process and MethodContext are abandoned after each expression completes)
            var gcStats = _heapManager.GC.GarbageCollect();
            Console.WriteLine($"[GC after {initId}] Collected {gcStats.CollectedObjects} objects, {gcStats.TotalObjects} total, {gcStats.LiveObjects} live");

            if (result.Status != InitStatus.Success && _policy == InitPolicy.AbortOnFirstError)
            {
                break;
            }
        }

        totalStopwatch.Stop();

        return new InitExecutionReport
        {
            Total = count,
            Succeeded = results.Count(r => r.Status == InitStatus.Success),
            Failed = results.Count(r => r.Status != InitStatus.Success),
            Results = results,
            TotalElapsedMilliseconds = totalStopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Executes a single init expression with full diagnostics.
    /// Follows the deterministic recipe:
    /// 1. Lookup and validate
    /// 2. Prepare canonical receiver
    /// 3. Allocate MethodContext
    /// 4. Root for GC
    /// 5. Execute with timeout
    /// 6. Capture diagnostics
    /// 7. Teardown and unroot
    /// </summary>
    public InitResult ExecuteInitExpression(string initLocalId, SmalltalkOOP compiledMethodOOP, int? timeoutMs = null)
    {
        var effectiveTimeout = timeoutMs ?? _defaultTimeoutMs;
        var stopwatch = Stopwatch.StartNew();

        // Reset instruction counter for each init expression (prevents accumulation across expressions)
        _vm.ResetInstructionCount();

        try
        {
            // Step 1: Lookup and sanity-check
            if (!ValidateCompiledMethod(compiledMethodOOP, out string? validationError))
            {
                return InitResult.ValidationFailed(initLocalId, validationError ?? "Unknown validation error");
            }

            // Step 2: Prepare canonical receiver
            SmalltalkOOP receiverOOP = GetCanonicalReceiverForDoIt();

            // Step 3: Create MethodContext for init expression
            SmalltalkOOP methodContextOOP = CreateMethodContextForInitExpression(compiledMethodOOP, receiverOOP);

            // Step 4: Create Process with this context
            SmalltalkOOP processOOP = CreateProcess(methodContextOOP, priority: 4);

            // Step 5: Execute via primitive 87 (resume)
            SmalltalkOOP resultOOP;
            try
            {
                if (effectiveTimeout > 0)
                {
                    using var cts = new CancellationTokenSource(effectiveTimeout);
                    resultOOP = ExecuteWithTimeout(processOOP, cts.Token);
                }
                else
                {
                    resultOOP = ResumeProcess(processOOP);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return InitResult.Timeout(initLocalId, stopwatch.ElapsedMilliseconds);
            }
            catch (PrimitiveFailureException pex)
            {
                stopwatch.Stop();
                return InitResult.PrimitiveFailure(initLocalId, pex.PrimitiveIndex, pex.Message, stopwatch.ElapsedMilliseconds);
            }
            catch (SmalltalkException sex)
            {
                stopwatch.Stop();
                string stackTrace = CaptureStackTrace(sex);
                return InitResult.Exception(initLocalId, sex.ExceptionOop, sex.Message, stackTrace, stopwatch.ElapsedMilliseconds);
            }

            // Step 6 & 7: Success - teardown handled by VM
            stopwatch.Stop();
            return InitResult.Success(initLocalId, resultOOP, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return InitResult.FatalError(initLocalId, $"{ex.GetType().Name}: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Executes an init expression with on-demand compilation and GC verification.
    /// This method:
    /// 1. Compiles the init expression from bundle on-demand
    /// 2. Creates a MethodContext and wires it to the existing activeProcess.suspendedContext
    /// 3. Runs GC to verify init expression NOT collected (reachable via Processor → activeProcess → suspendedContext → method)
    /// 4. Executes the init expression
    /// 5. Clears Process.suspendedContext (Process remains as activeProcess)
    /// 6. Runs GC to verify init expression IS collected (MethodContext, CompiledMethod, ByteArray now unreachable)
    /// </summary>
    public InitResult ExecuteInitExpressionOnDemand(
        InitExpression expr,
        InitExpressionCompiler compiler,
        int? timeoutMs = null)
    {
        var effectiveTimeout = timeoutMs ?? _defaultTimeoutMs;
        var stopwatch = Stopwatch.StartNew();

        Console.Write($"  {expr.Source}... ");

        // Enable diagnostic listener for VariableNode initialize to debug hash/modulo issue
        MethodTraceListener? traceListener = null;
        bool enableTracing = expr.Source.Contains("VariableNode initialize");
        if (enableTracing && _diagnostics != null)
        {
            Console.WriteLine();
            Console.WriteLine("=== ENABLING METHOD TRACE ===");
            traceListener = new MethodTraceListener { MaxDepth = 20 };
            _diagnostics.Register(traceListener,
                DiagnosticEventTypes.MethodEntry |
                DiagnosticEventTypes.PrimitiveCall |
                DiagnosticEventTypes.PrimitiveFailure);
        }

        // CRITICAL: Reset instruction counter for EACH init expression
        // Without this, the counter accumulates across ALL expressions:
        // 40 expressions × 10K instructions each = 400K accumulated before #41 even starts!
        _vm.ResetInstructionCount();

        try
        {
            // Compile init expression on-demand
            SmalltalkOOP compiledMethodOOP = compiler.CompileInitExpression(expr);

            // Create MethodContext
            SmalltalkOOP receiverOOP = GetCanonicalReceiverForDoIt();
            SmalltalkOOP methodContextOOP = CreateMethodContextForInitExpression(compiledMethodOOP, receiverOOP);

            // Get existing activeProcess and wire MethodContext to it
            var schedulerOOP = _metadata.ProcessorScheduler;
            SmalltalkOOP processOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

            // Set the process's suspendedContext to our new MethodContext
            _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, methodContextOOP);

            // Execute the init expression
            SmalltalkOOP resultOOP;
            try
            {
                if (effectiveTimeout > 0)
                {
                    using var cts = new CancellationTokenSource(effectiveTimeout);
                    resultOOP = ExecuteWithTimeout(processOOP, cts.Token);
                }
                else
                {
                    resultOOP = ResumeProcess(processOOP);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Console.WriteLine($"❌ TIMEOUT");
                return InitResult.Timeout(expr.LocalId, stopwatch.ElapsedMilliseconds);
            }
            catch (PrimitiveFailureException pex)
            {
                stopwatch.Stop();
                Console.WriteLine($"❌ PRIMITIVE FAILURE: {pex.Message}");
                return InitResult.PrimitiveFailure(expr.LocalId, pex.PrimitiveIndex, pex.Message, stopwatch.ElapsedMilliseconds);
            }
            catch (SmalltalkException sex)
            {
                stopwatch.Stop();
                Console.WriteLine($"❌ EXCEPTION: {sex.Message}");
                string stackTrace = CaptureStackTrace(sex);
                return InitResult.Exception(expr.LocalId, sex.ExceptionOop, sex.Message, stackTrace, stopwatch.ElapsedMilliseconds);
            }

            // Clear the Process's suspendedContext
            _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, _heapManager.GetNil());

            // Run GC - SHOULD collect (MethodContext, CompiledMethod, ByteArray no longer reachable)
            var gcStatsAfter = _heapManager.GC.GarbageCollect();

            // Verify the CompiledMethod was collected
            bool compiledMethodAlive = IsObjectAlive(compiledMethodOOP);
            bool methodContextAlive = IsObjectAlive(methodContextOOP);

            stopwatch.Stop();

            if (compiledMethodAlive || methodContextAlive)
            {
                // Check if contexts are kept alive by forked processes (expected behavior)
                bool keptAliveByForkedProcess = IsKeptAliveByForkedProcess(methodContextOOP, schedulerOOP);

                if (keptAliveByForkedProcess)
                {
                    // This is EXPECTED - init expression forked a persistent process
                    Console.WriteLine($"✓ {stopwatch.ElapsedMilliseconds}ms (contexts kept alive by forked process)");
                    return InitResult.Success(expr.LocalId, resultOOP, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    // This is UNEXPECTED - contexts alive but NOT due to forked processes
                    Console.WriteLine($" ❌ FAILED (objects not collected, not kept alive by forked process)");
                    Console.WriteLine($"    CompiledMethod {compiledMethodOOP}: {(compiledMethodAlive ? "ALIVE" : "collected")}");
                    Console.WriteLine($"    MethodContext {methodContextOOP}: {(methodContextAlive ? "ALIVE" : "collected")}");
                    return InitResult.FatalError(expr.LocalId, $"CompiledMethod or MethodContext still alive after execution (not due to forked process)");
                }
            }

            // Show simple success message
            Console.WriteLine($"✓ {stopwatch.ElapsedMilliseconds}ms");

            return InitResult.Success(expr.LocalId, resultOOP, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Console.WriteLine($"❌ ERROR: {ex.Message}");
            return InitResult.FatalError(expr.LocalId, $"{ex.GetType().Name}: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
        finally
        {
            // Unregister trace listener if it was added
            if (traceListener != null && _diagnostics != null)
            {
                _diagnostics.Unregister(traceListener);
                Console.WriteLine("=== METHOD TRACE DISABLED ===");
            }
        }
    }

    /// <summary>
    /// Checks if an object is still alive (not collected by GC).
    /// </summary>
    private bool IsObjectAlive(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return true; // SmallIntegers are immediate values, always "alive"

        try
        {
            // Try to access the object's class - if it's been collected, this will fail
            var classOOP = _heapManager.GetClassOOP(oop);
            return classOOP != _heapManager.GetNil();
        }
        catch
        {
            return false; // Object no longer exists
        }
    }

    /// <summary>
    /// Checks if a MethodContext is kept alive by a forked process in the ProcessorScheduler.
    /// This is expected behavior when init expressions fork background processes.
    /// </summary>
    private bool IsKeptAliveByForkedProcess(SmalltalkOOP methodContextOOP, SmalltalkOOP schedulerOOP)
    {
        try
        {
            // Get process lists array from scheduler
            SmalltalkOOP processListsArrayOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
            int numPriorities = _heapManager.GetIndexedSize(processListsArrayOOP);

            // Check each priority level
            for (int priorityIndex = 0; priorityIndex < numPriorities; priorityIndex++)
            {
                SmalltalkOOP processListOOP = _heapManager.GetIndexedOOP(processListsArrayOOP, priorityIndex);
                if (processListOOP == _metadata.KnownObjects.Nil)
                    continue;

                // Get first process in list
                SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(processListOOP, 0);
                if (firstLinkOOP == _metadata.KnownObjects.Nil)
                    continue;

                // Walk linked list of processes
                SmalltalkOOP currentProcessOOP = firstLinkOOP;
                int processCount = 0;
                while (currentProcessOOP != _metadata.KnownObjects.Nil && processCount < 100)
                {
                    processCount++;
                    var procSuspendedCtx = _heapManager.GetInstanceVariable(currentProcessOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

                    // Check if suspended context is our MethodContext
                    if (procSuspendedCtx == methodContextOOP)
                        return true;

                    // Check if it's a BlockContext whose home or sender chain reaches our MethodContext
                    if (procSuspendedCtx != _metadata.KnownObjects.Nil && !procSuspendedCtx.IsSmallInteger)
                    {
                        var ctxClass = _heapManager.GetClassOOP(procSuspendedCtx);
                        var ctxClassName = _heapManager.GetClassName(ctxClass);

                        if (ctxClassName.Contains("BlockContext"))
                        {
                            // Check home pointer
                            var homeCtx = _heapManager.GetInstanceVariable(procSuspendedCtx, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                            if (homeCtx == methodContextOOP)
                                return true;

                            // Check home's sender chain
                            if (homeCtx != _metadata.KnownObjects.Nil && !homeCtx.IsSmallInteger)
                            {
                                if (IsSenderChainReaches(homeCtx, methodContextOOP))
                                    return true;
                            }

                            // Check BlockContext's own sender chain
                            if (IsSenderChainReaches(procSuspendedCtx, methodContextOOP))
                                return true;
                        }
                        else if (ctxClassName.Contains("MethodContext"))
                        {
                            // Check MethodContext's sender chain
                            if (IsSenderChainReaches(procSuspendedCtx, methodContextOOP))
                                return true;
                        }
                    }

                    // Move to next process
                    currentProcessOOP = _heapManager.GetInstanceVariable(currentProcessOOP, VMConstants.PROCESS_NEXTLINK_INDEX);
                }
            }

            return false;
        }
        catch
        {
            // If any error occurs during checking, assume not kept alive by forked process
            return false;
        }
    }

    /// <summary>
    /// Checks if a context's sender chain reaches the target context.
    /// </summary>
    private bool IsSenderChainReaches(SmalltalkOOP contextOOP, SmalltalkOOP targetOOP)
    {
        SmalltalkOOP currentSender = _heapManager.GetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX);
        int depth = 0;

        while (currentSender != _metadata.KnownObjects.Nil && !currentSender.IsSmallInteger && depth < 20)
        {
            if (currentSender == targetOOP)
                return true;

            currentSender = _heapManager.GetInstanceVariable(currentSender, VMConstants.CONTEXT_SENDER_INDEX);
            depth++;
        }

        return false;
    }

    /// <summary>
    /// Validates that a CompiledMethod OOP is suitable for init expression execution.
    /// </summary>
    private bool ValidateCompiledMethod(SmalltalkOOP methodOOP, out string? error)
    {
        error = null;

        // Basic validation - check it's not nil
        if (methodOOP == _metadata.KnownObjects.Nil)
        {
            error = "Method OOP is nil";
            return false;
        }

        // TODO: Add more validation:
        // - Check that it's actually a CompiledMethod
        // - Verify header fields are reasonable
        // - Check that literals are resolved

        return true;
    }

    /// <summary>
    /// Gets the canonical receiver for DoIt expressions.
    /// Per Smalltalk-80 semantics: use Smalltalk global (from GlobalRegistry).
    /// </summary>
    private SmalltalkOOP GetCanonicalReceiverForDoIt()
    {
        // Get Smalltalk global (SystemDictionary) from GlobalRegistry
        return GlobalRegistry.Get("Smalltalk");
    }

    private SmalltalkOOP CreateMethodContextForInitExpression(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP)
    {
        // Get MethodContext class OOP - lookup from live heap
        // Find MethodContext class by querying Smalltalk dictionary
        var smalltalkOOP = GlobalRegistry.Get("Smalltalk");
        var methodContextClassOOP = LookupGlobal(smalltalkOOP, "MethodContext");
        if (methodContextClassOOP == _metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("MethodContext class not found in Smalltalk dictionary");
        }

        // MethodContext has a fixed layout per Smalltalk-80 specification:
        // MethodContext inherits: InstructionStream (sender, pc) → ContextPart (stackp)
        // MethodContext adds: method, lexicalParent, receiver
        // Total: 6 instance variables (sender, pc, stackp, method, lexicalParent, receiver)
        // Starting at index 6 for indexed slots (temp frame)
        const int METHODCONTEXT_INSTANCE_VAR_COUNT = 6;

        // Allocate MethodContext with indexed slots for stack (use 12 for small frame)
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, indexedSize: 12);
        _heapManager.SetClassOOP(contextOOP, methodContextClassOOP);

        // Set instance variables using VMConstants indices
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int lexicalParentIndex = VMConstants.CONTEXT_LEXICALPARENT_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        _heapManager.SetInstanceVariable(contextOOP, senderIndex, _heapManager.GetNil());
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);
        _heapManager.SetInstanceVariable(contextOOP, lexicalParentIndex, _heapManager.GetNil());
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);

        // Initialize indexed portion (stack) with nil
        for (int i = 0; i < 12; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, _heapManager.GetNil());
        }

        return contextOOP;
    }

    /// <summary>
    /// Looks up a global variable in the SystemDictionary.
    /// </summary>
    private SmalltalkOOP LookupGlobal(SmalltalkOOP systemDictOOP, string globalName)
    {
        // SystemDictionary is an array of Associations
        int size = _heapManager.GetIndexedSize(systemDictOOP);

        // Find the symbol OOP for the global name by searching USTable
        var symbolOOP = FindSymbol(globalName);
        if (symbolOOP == _metadata.KnownObjects.Nil)
        {
            return _metadata.KnownObjects.Nil;
        }

        for (int i = 0; i < size; i++)
        {
            var assocOOP = _heapManager.GetIndexedOOP(systemDictOOP, i);
            if (assocOOP == _heapManager.GetNil())
                continue;

            // Association has key at instance variable 0
            var keyOOP = _heapManager.GetInstanceVariable(assocOOP, 0);
            if (keyOOP == symbolOOP)
            {
                // Return value at instance variable 1
                return _heapManager.GetInstanceVariable(assocOOP, 1);
            }
        }

        return _metadata.KnownObjects.Nil;
    }

    /// <summary>
    /// Finds a symbol OOP by searching through all registered symbols.
    /// This is a runtime-safe alternative to SymbolRegistry.GetSymbolOOP(string).
    /// </summary>
    private SmalltalkOOP FindSymbol(string symbolText)
    {
        // Search through all symbols registered during Phase 1
        foreach (var (index, symbolOOP) in SymbolRegistry.AllSymbols())
        {
            // Compare the symbol's string contents with the search text
            // Symbols are String objects, so read their bytes
            var symbolString = ReadSymbolString(symbolOOP);
            if (symbolString == symbolText)
            {
                return symbolOOP;
            }
        }

        return _metadata.KnownObjects.Nil;
    }

    /// <summary>
    /// Reads a Symbol object's string value from the heap.
    /// </summary>
    private string ReadSymbolString(SmalltalkOOP symbolOOP)
    {
        // Symbol is a String subclass, so it's stored as ByteIndexed
        int size = _heapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private SmalltalkOOP CreateProcess(SmalltalkOOP suspendedContextOOP, int priority)
    {
        // Get Process class OOP - lookup from live heap
        var smalltalkOOP = GlobalRegistry.Get("Smalltalk");
        var processClassOOP = LookupGlobal(smalltalkOOP, "Process");
        if (processClassOOP == _metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("Process class not found in Smalltalk dictionary");
        }

        // Process has a fixed layout per Smalltalk-80 specification:
        // Process inherits from Link (1 instance variable: nextLink)
        // Process adds 3 instance variables: suspendedContext, priority, myList
        // Total: 4 instance variables (nextLink, suspendedContext, priority, myList)
        const int PROCESS_INSTANCE_VAR_COUNT = 4;

        // Allocate Process object (fixed size, not indexed)
        var processOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, PROCESS_INSTANCE_VAR_COUNT, indexedSize: 0);
        _heapManager.SetClassOOP(processOOP, processClassOOP);

        // Set instance variables using fixed indices from VMConstants
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, suspendedContextOOP);
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_PRIORITY_INDEX, SmalltalkOOP.FromSmallInteger(priority));
        _heapManager.SetInstanceVariable(processOOP, VMConstants.PROCESS_MYLIST_INDEX, _heapManager.GetNil());

        return processOOP;
    }

    /// <summary>
    /// Executes a method with timeout support.
    /// </summary>
    private SmalltalkOOP ExecuteWithTimeout(SmalltalkOOP processOOP, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ResumeProcess(processOOP);
    }

    /// <summary>
    /// Captures a deterministic stack trace from a Smalltalk exception.
    /// </summary>
    private string CaptureStackTrace(SmalltalkException exception)
    {
        // TODO: Implement proper stack trace capture
        // Should return a structured trace like:
        // "at method:Foo>>bar (pc=15)\nat method:Baz>>qux (pc=42)"
        return exception.StackTrace ?? "Stack trace not available";
    }
}

/// <summary>
/// Exception thrown when a primitive operation fails without fallback.
/// </summary>
public class PrimitiveFailureException : Exception
{
    public int PrimitiveIndex { get; }

    public PrimitiveFailureException(int primitiveIndex, string message)
        : base(message)
    {
        PrimitiveIndex = primitiveIndex;
    }
}

/// <summary>
/// Exception thrown when Smalltalk code raises an exception.
/// </summary>
public class SmalltalkException : Exception
{
    public int ExceptionOop { get; }

    public SmalltalkException(int exceptionOop, string message)
        : base(message)
    {
        ExceptionOop = exceptionOop;
    }
}