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
            // Priority 8 (TimingPriority) prevents preemption when init expressions
            // fork processes at priority 8 (e.g., Delay's timing process).
            // Since 8 > 8 is false, no preemption occurs.
            SmalltalkOOP processOOP = CreateProcess(methodContextOOP, priority: 8);

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
        int? timeoutMs = null,
        bool verbose = true)
    {
        var effectiveTimeout = timeoutMs ?? _defaultTimeoutMs;
        var stopwatch = Stopwatch.StartNew();

        if (verbose)
            Console.Write($"  {expr.Source}... ");

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
                if (verbose)
                    Console.WriteLine($"❌ TIMEOUT");
                return InitResult.Timeout(expr.LocalId, stopwatch.ElapsedMilliseconds);
            }
            catch (PrimitiveFailureException pex)
            {
                stopwatch.Stop();
                if (verbose)
                    Console.WriteLine($"❌ PRIMITIVE FAILURE: {pex.Message}");
                return InitResult.PrimitiveFailure(expr.LocalId, pex.PrimitiveIndex, pex.Message, stopwatch.ElapsedMilliseconds);
            }
            catch (SmalltalkException sex)
            {
                stopwatch.Stop();
                if (verbose)
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
                bool keptAliveByForkedProcess = IsKeptAliveByForkedProcess(methodContextOOP, schedulerOOP, verbose);

                if (keptAliveByForkedProcess)
                {
                    // This is EXPECTED - init expression forked a persistent process
                    if (verbose)
                        Console.WriteLine($"✓ {stopwatch.ElapsedMilliseconds}ms (contexts kept alive by forked process)");
                    return InitResult.Success(expr.LocalId, resultOOP, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    // This is UNEXPECTED - contexts alive but NOT due to forked processes
                    if (verbose)
                    {
                        Console.WriteLine($" ❌ FAILED (objects not collected, not kept alive by forked process)");
                        Console.WriteLine($"    CompiledMethod {compiledMethodOOP}: {(compiledMethodAlive ? "ALIVE" : "collected")}");
                        Console.WriteLine($"    MethodContext {methodContextOOP}: {(methodContextAlive ? "ALIVE" : "collected")}");
                    }
                    return InitResult.FatalError(expr.LocalId, $"CompiledMethod or MethodContext still alive after execution (not due to forked process)");
                }
            }

            // Show simple success message
            if (verbose)
                Console.WriteLine($"✓ {stopwatch.ElapsedMilliseconds}ms");

            return InitResult.Success(expr.LocalId, resultOOP, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (verbose)
                Console.WriteLine($"❌ ERROR: {ex.Message}");
            return InitResult.FatalError(expr.LocalId, $"{ex.GetType().Name}: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
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
    private bool IsKeptAliveByForkedProcess(SmalltalkOOP methodContextOOP, SmalltalkOOP schedulerOOP, bool verbose = false)
    {
        try
        {
            // Check 1: Processes in quiescentProcessLists (runnable processes)
            if (CheckProcessListsForContext(schedulerOOP, methodContextOOP, verbose))
                return true;

            // Check 2: Processes blocked on known system semaphores
            // This handles cases like Delay TimerProcess blocked on TimingSemaphore
            if (CheckBlockedProcessesForContext(methodContextOOP, verbose))
                return true;

            return false;
        }
        catch (Exception ex)
        {
            if (verbose)
                Console.WriteLine($"    [DEBUG] IsKeptAliveByForkedProcess exception: {ex.Message}");
            // If any error occurs during checking, assume not kept alive by forked process
            return false;
        }
    }

    /// <summary>
    /// Checks if any process in the quiescentProcessLists keeps the context alive.
    /// </summary>
    private bool CheckProcessListsForContext(SmalltalkOOP schedulerOOP, SmalltalkOOP methodContextOOP, bool verbose = false)
    {
        // Get process lists array from scheduler
        SmalltalkOOP processListsArrayOOP = _heapManager.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);
        int numPriorities = _heapManager.GetIndexedSize(processListsArrayOOP);

        if (verbose)
            Console.WriteLine($"    [DEBUG] Checking {numPriorities} priority queues");

        // Check each priority level
        for (int priorityIndex = 0; priorityIndex < numPriorities; priorityIndex++)
        {
            SmalltalkOOP processListOOP = _heapManager.GetIndexedOOP(processListsArrayOOP, priorityIndex);
            if (processListOOP == _metadata.KnownObjects.Nil)
                continue;

            if (CheckLinkedListForContext(processListOOP, methodContextOOP, verbose, $"priority {priorityIndex + 1}"))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks processes blocked on known system semaphores or stored in class variables.
    /// </summary>
    private bool CheckBlockedProcessesForContext(SmalltalkOOP methodContextOOP, bool verbose = false)
    {
        var smalltalkOOP = GlobalRegistry.Get("Smalltalk");

        // Check Delay class's semaphore waiting lists
        // Note: Delay's class variables are: AccessProtect, ActiveDelay, SuspendedDelays, TimingSemaphore
        // There is NO TimerProcess variable - the process is created by forkAt: but not stored
        var delayClassOOP = LookupGlobal(smalltalkOOP, "Delay");
        if (verbose)
            Console.WriteLine($"    [DEBUG] Delay class lookup: {(delayClassOOP != _metadata.KnownObjects.Nil ? delayClassOOP.ToString() : "nil")}");

        if (delayClassOOP != _metadata.KnownObjects.Nil)
        {
            // Check TimingSemaphore waiting list - this is where the timer process waits
            var timingSemaphoreOOP = GetClassVariable(delayClassOOP, "TimingSemaphore");
            if (verbose)
                Console.WriteLine($"    [DEBUG] TimingSemaphore class var: {(timingSemaphoreOOP != _metadata.KnownObjects.Nil ? timingSemaphoreOOP.ToString() : "nil")}");

            if (timingSemaphoreOOP != _metadata.KnownObjects.Nil)
            {
                if (CheckLinkedListForContext(timingSemaphoreOOP, methodContextOOP, verbose, "TimingSemaphore"))
                    return true;
            }

            // Also check AccessProtect mutex
            var accessProtectOOP = GetClassVariable(delayClassOOP, "AccessProtect");
            if (verbose)
                Console.WriteLine($"    [DEBUG] AccessProtect class var: {(accessProtectOOP != _metadata.KnownObjects.Nil ? accessProtectOOP.ToString() : "nil")}");

            if (accessProtectOOP != _metadata.KnownObjects.Nil)
            {
                if (CheckLinkedListForContext(accessProtectOOP, methodContextOOP, verbose, "AccessProtect"))
                    return true;
            }
        }

        // Check InputState class's InputSemaphore
        // Note: InputProcess and InputSemaphore are in InputState, not InputSensor!
        var inputStateClassOOP = LookupGlobal(smalltalkOOP, "InputState");
        if (verbose)
            Console.WriteLine($"    [DEBUG] InputState class lookup: {(inputStateClassOOP != _metadata.KnownObjects.Nil ? inputStateClassOOP.ToString() : "nil")}");

        if (inputStateClassOOP != _metadata.KnownObjects.Nil)
        {
            // Check InputProcess directly (stored in class variable)
            var inputProcessOOP = GetClassVariable(inputStateClassOOP, "InputProcess");
            if (verbose)
                Console.WriteLine($"    [DEBUG] InputProcess class var: {(inputProcessOOP != _metadata.KnownObjects.Nil ? inputProcessOOP.ToString() : "nil")}");

            if (inputProcessOOP != _metadata.KnownObjects.Nil)
            {
                if (ProcessKeepsContextAlive(inputProcessOOP, methodContextOOP, verbose))
                    return true;
            }

            // Check InputSemaphore waiting list
            var inputSemaphoreOOP = GetClassVariable(inputStateClassOOP, "InputSemaphore");
            if (inputSemaphoreOOP != _metadata.KnownObjects.Nil)
            {
                if (CheckLinkedListForContext(inputSemaphoreOOP, methodContextOOP, verbose, "InputSemaphore"))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a class variable value from a class's classPool, traversing the superclass chain.
    /// Class variables can be inherited from superclasses.
    /// </summary>
    private SmalltalkOOP GetClassVariable(SmalltalkOOP classOOP, string variableName)
    {
        try
        {
            // Class instance variable layout:
            // 0: superclass, 1: methodDict, 2: format, 3: instanceVariables, 4: organization,
            // 5: subclasses, 6: name, 7: classPool, 8: sharedPools
            const int SUPERCLASS_INDEX = 0;
            const int CLASS_POOL_INDEX = 7;

            // Try symbol-based lookup first
            var symbolOOP = FindSymbol(variableName);

            // Traverse superclass chain looking for the class variable
            var currentClassOOP = classOOP;
            int depth = 0;
            while (currentClassOOP != _metadata.KnownObjects.Nil && !currentClassOOP.IsSmallInteger && depth < 20)
            {
                var classPoolOOP = _heapManager.GetInstanceVariable(currentClassOOP, CLASS_POOL_INDEX);

                if (classPoolOOP != _metadata.KnownObjects.Nil)
                {
                    // classPool is a Dictionary (Set of Associations)
                    int poolSize = _heapManager.GetIndexedSize(classPoolOOP);

                    for (int i = 0; i < poolSize; i++)
                    {
                        var assocOOP = _heapManager.GetIndexedOOP(classPoolOOP, i);
                        if (assocOOP == _heapManager.GetNil())
                            continue;

                        // Association has key at instance variable 0
                        var keyOOP = _heapManager.GetInstanceVariable(assocOOP, 0);

                        // Try exact symbol match first
                        if (symbolOOP != _metadata.KnownObjects.Nil && keyOOP == symbolOOP)
                        {
                            return _heapManager.GetInstanceVariable(assocOOP, 1);
                        }

                        // Fall back to string content comparison
                        if (keyOOP != _metadata.KnownObjects.Nil && !keyOOP.IsSmallInteger)
                        {
                            var keyString = ReadSymbolString(keyOOP);
                            if (keyString == variableName)
                            {
                                return _heapManager.GetInstanceVariable(assocOOP, 1);
                            }
                        }
                    }
                }

                // Move to superclass
                currentClassOOP = _heapManager.GetInstanceVariable(currentClassOOP, SUPERCLASS_INDEX);
                depth++;
            }

            return _metadata.KnownObjects.Nil;
        }
        catch
        {
            return _metadata.KnownObjects.Nil;
        }
    }

    /// <summary>
    /// Checks if any process in a linked list keeps the context alive.
    /// Works for both priority lists and semaphore waiting lists.
    /// </summary>
    private bool CheckLinkedListForContext(SmalltalkOOP listOOP, SmalltalkOOP methodContextOOP, bool verbose = false, string listName = "list")
    {
        // LinkedList/Semaphore has firstLink at index 0
        SmalltalkOOP firstLinkOOP = _heapManager.GetInstanceVariable(listOOP, 0);
        if (firstLinkOOP == _metadata.KnownObjects.Nil)
        {
            if (verbose)
                Console.WriteLine($"    [DEBUG] {listName} firstLink is nil (empty list)");
            return false;
        }

        // Walk linked list of processes
        SmalltalkOOP currentProcessOOP = firstLinkOOP;
        int processCount = 0;
        while (currentProcessOOP != _metadata.KnownObjects.Nil && processCount < 100)
        {
            processCount++;
            if (ProcessKeepsContextAlive(currentProcessOOP, methodContextOOP, verbose))
            {
                if (verbose)
                    Console.WriteLine($"    [DEBUG] {listName} process #{processCount} keeps context alive");
                return true;
            }

            // Move to next process
            currentProcessOOP = _heapManager.GetInstanceVariable(currentProcessOOP, VMConstants.PROCESS_NEXTLINK_INDEX);
        }

        if (verbose && processCount > 0)
            Console.WriteLine($"    [DEBUG] {listName} checked {processCount} processes, none keep context alive");

        return false;
    }

    /// <summary>
    /// Checks if a process's suspended context chain reaches the target context.
    /// </summary>
    private bool ProcessKeepsContextAlive(SmalltalkOOP processOOP, SmalltalkOOP methodContextOOP, bool verbose = false)
    {
        var procSuspendedCtx = _heapManager.GetInstanceVariable(processOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);

        if (verbose)
        {
            var procClass = _heapManager.GetClassOOP(processOOP);
            var procClassName = _heapManager.GetClassName(procClass);
            Console.WriteLine($"    [DEBUG] Checking process {processOOP} (class: {procClassName})");
            Console.WriteLine($"    [DEBUG]   suspendedContext: {procSuspendedCtx}, target: {methodContextOOP}");
        }

        // Use recursive context chain check
        return ContextReachesTarget(procSuspendedCtx, methodContextOOP, verbose, 0);
    }

    /// <summary>
    /// Recursively checks if a context (or its home/sender chain) reaches the target.
    /// Handles nested BlockContexts by traversing both home pointers and sender chains.
    /// </summary>
    private bool ContextReachesTarget(SmalltalkOOP contextOOP, SmalltalkOOP targetOOP, bool verbose, int depth)
    {
        if (depth > 30) return false; // Prevent infinite recursion

        if (contextOOP == _metadata.KnownObjects.Nil || contextOOP.IsSmallInteger)
            return false;

        // Direct match
        if (contextOOP == targetOOP)
        {
            if (verbose)
                Console.WriteLine($"    [DEBUG]   {new string(' ', depth * 2)}Found target at depth {depth}!");
            return true;
        }

        var ctxClass = _heapManager.GetClassOOP(contextOOP);
        var ctxClassName = _heapManager.GetClassName(ctxClass);

        if (verbose && depth == 0)
            Console.WriteLine($"    [DEBUG]   Context class: {ctxClassName}");

        if (ctxClassName.Contains("BlockContext"))
        {
            // For BlockContext: check home pointer recursively
            var homeCtx = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            if (verbose)
                Console.WriteLine($"    [DEBUG]   {new string(' ', depth * 2)}BlockContext home: {homeCtx}");

            // Recursively check if home reaches target
            if (ContextReachesTarget(homeCtx, targetOOP, verbose, depth + 1))
                return true;

            // Also check this BlockContext's sender chain
            var sender = _heapManager.GetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX);
            if (ContextReachesTarget(sender, targetOOP, verbose, depth + 1))
                return true;
        }
        else if (ctxClassName.Contains("MethodContext"))
        {
            // For MethodContext: check sender chain
            var sender = _heapManager.GetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX);
            if (verbose)
                Console.WriteLine($"    [DEBUG]   {new string(' ', depth * 2)}MethodContext sender: {sender}");

            if (ContextReachesTarget(sender, targetOOP, verbose, depth + 1))
                return true;
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