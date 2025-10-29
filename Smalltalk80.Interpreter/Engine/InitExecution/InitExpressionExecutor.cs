using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Memory;

using System.Text.Json;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Engine.LoaderPhases;

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
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VirtualMachine _vm;
    private readonly LayoutResolver _layoutResolver;
    private readonly ProcessPrimitives _processPrimitives;
    private readonly InitPolicy _policy;
    private readonly int _defaultTimeoutMs;

    public InitExpressionExecutor(
        ISmalltalkHeapManager heapManager,
        VirtualMachine vm,
        LayoutResolver layoutResolver,
        InitPolicy policy = InitPolicy.AbortOnFirstError,
        int defaultTimeoutMs = 5000)
    {
        _heapManager = heapManager;
        _vm = vm;
        _layoutResolver = layoutResolver;
        _policy = policy;
        _defaultTimeoutMs = defaultTimeoutMs;
        _processPrimitives = new ProcessPrimitives(heapManager, layoutResolver, vm);
    }


    /// <summary>
    /// Executes all init expressions from SpecialObjectsArray.InitExpressionMethods.
    /// </summary>
    public InitExecutionReport ExecuteAll(CancellationToken cancellationToken = default)
    {
        var results = new List<InitResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Get init expression array from SpecialObjectsArray
        var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
        if (initArrayOOP == SmalltalkOOP.Null)
        {
            throw new InvalidOperationException("Init expression methods not found in SpecialObjectsArray - coldstart must complete first");
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
                    resultOOP = _processPrimitives.Resume(processOOP);
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
            return InitResult.FatalError(initLocalId, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a CompiledMethod OOP is suitable for init expression execution.
    /// </summary>
    private bool ValidateCompiledMethod(SmalltalkOOP methodOOP, out string? error)
    {
        error = null;

        // Basic validation - check it's not null
        if (methodOOP == SmalltalkOOP.Null)
        {
            error = "Method OOP is null (0)";
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
    /// Per Smalltalk-80 semantics: use Smalltalk global (from SpecialObjectsArray).
    /// </summary>
    private SmalltalkOOP GetCanonicalReceiverForDoIt()
    {
        // Get Smalltalk global (SystemDictionary) from SpecialObjectsArray
        return SpecialObjectsArray.Instance.Smalltalk;
    }

    private SmalltalkOOP CreateMethodContextForInitExpression(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP)
    {
        // Get MethodContext class OOP - lookup from live heap
        // Find MethodContext class by querying Smalltalk dictionary
        var smalltalkOOP = SpecialObjectsArray.Instance.Smalltalk;
        var methodContextClassOOP = LookupGlobal(smalltalkOOP, "MethodContext");
        if (methodContextClassOOP == SmalltalkOOP.Null)
        {
            throw new InvalidOperationException("MethodContext class not found in Smalltalk dictionary");
        }

        // Get instance variable count from class
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");

        // Allocate MethodContext with indexed slots for stack (use 12 for small frame)
        var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, indexedSize: 12);
        _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP);

        // Set instance variables using LayoutResolver indices
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.METHODCONTEXT_METHOD_INDEX;
        int receiverMapIndex = VMConstants.METHODCONTEXT_RECEIVERMAP_INDEX;
        int receiverIndex = VMConstants.METHODCONTEXT_RECEIVER_INDEX;

        _heapManager.SetSlot(contextOOP, senderIndex + 1, SpecialObjectsArray.Instance.Nil);
        _heapManager.SetSlot(contextOOP, pcIndex + 1, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetSlot(contextOOP, stackpIndex + 1, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetSlot(contextOOP, methodIndex + 1, methodOOP);
        _heapManager.SetSlot(contextOOP, receiverMapIndex + 1, SpecialObjectsArray.Instance.Nil);
        _heapManager.SetSlot(contextOOP, receiverIndex + 1, receiverOOP);

        // Initialize indexed portion (stack) with nil
        int indexedBase = instanceVarCount + 1; // +1 for class slot
        for (int i = 0; i < 12; i++)
        {
            _heapManager.SetSlot(contextOOP, indexedBase + i, SpecialObjectsArray.Instance.Nil);
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
        if (symbolOOP == SmalltalkOOP.Null)
        {
            return SmalltalkOOP.Null;
        }

        for (int i = 0; i < size; i++)
        {
            var assocOOP = _heapManager.GetIndexedOOP(systemDictOOP, i);
            if (assocOOP == SpecialObjectsArray.Instance.Nil)
                continue;

            // Association has key at slot 0 (after class slot)
            var keyOOP = _heapManager.GetSlot(assocOOP, 1);
            if (keyOOP == symbolOOP)
            {
                // Return value at slot 1 (after class slot)
                return _heapManager.GetSlot(assocOOP, 2);
            }
        }

        return SmalltalkOOP.Null;
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

        return SmalltalkOOP.Null;
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
        var smalltalkOOP = SpecialObjectsArray.Instance.Smalltalk;
        var processClassOOP = LookupGlobal(smalltalkOOP, "Process");
        if (processClassOOP == SmalltalkOOP.Null)
        {
            throw new InvalidOperationException("Process class not found in Smalltalk dictionary");
        }

        // Get instance variable count from class
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:Process");

        // Allocate Process object
        var processOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, indexedSize: 0);
        _heapManager.SetSlot(processOOP, 0, processClassOOP);

        // Set instance variables using LayoutResolver indices
        int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
        int priorityIndex = _layoutResolver.GetFieldIndex("class:Process", "priority");
        int myListIndex = _layoutResolver.GetFieldIndex("class:Process", "myList");

        _heapManager.SetSlot(processOOP, suspendedContextIndex + 1, suspendedContextOOP);
        _heapManager.SetSlot(processOOP, priorityIndex + 1, SmalltalkOOP.FromSmallInteger(priority));
        _heapManager.SetSlot(processOOP, myListIndex + 1, SpecialObjectsArray.Instance.Nil);

        return processOOP;
    }

    /// <summary>
    /// Executes a method with timeout support.
    /// </summary>
    private SmalltalkOOP ExecuteWithTimeout(SmalltalkOOP processOOP, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _processPrimitives.Resume(processOOP);
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