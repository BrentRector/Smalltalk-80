using System.Diagnostics;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.InitExecution;

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
    private readonly SmalltalkHeap _heap;
    private readonly VirtualMachine _vm;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly InitPolicy _policy;
    private readonly int _defaultTimeoutMs;

    public InitExpressionExecutor(
        ISmalltalkHeap heap,
        VirtualMachine vm,
        Dictionary<string, int> localIdToObjectId,
        InitPolicy policy = InitPolicy.AbortOnFirstError,
        int defaultTimeoutMs = 5000)
    {
        _heap = (SmalltalkHeap)heap; // Cast to concrete type for indexed field access
        _vm = vm;
        _localIdToObjectId = localIdToObjectId;
        _policy = policy;
        _defaultTimeoutMs = defaultTimeoutMs;
    }

    /// <summary>
    /// Executes all init expressions from the provided array object.
    /// </summary>
    public InitExecutionReport ExecuteAll(int initArrayOop, CancellationToken cancellationToken = default)
    {
        var results = new List<InitResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Get init expression array size
        int count = _heap.GetIndexedSize(initArrayOop);

        // TEMPORARY: Only execute Float>>initialize (init:123) for testing
        string targetInitLocalId = "init:123"; // Float initialize
        Console.WriteLine($"DEBUG: Searching for {targetInitLocalId} out of {count} total init expressions");

        // Find the target init expression
        int targetMethodOop = 0;
        int targetIndex = -1;

        if (_localIdToObjectId.TryGetValue(targetInitLocalId, out targetMethodOop))
        {
            // Find its index in the init array
            for (int i = 0; i < count; i++)
            {
                int methodOop = _heap.GetIndexedField(initArrayOop, i);
                if (methodOop == targetMethodOop)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        if (targetIndex < 0)
        {
            Console.WriteLine($"ERROR: Could not find {targetInitLocalId} in init expressions array");
            totalStopwatch.Stop();
            return new InitExecutionReport
            {
                Total = count,
                Succeeded = 0,
                Failed = 0,
                Results = results,
                TotalElapsedMilliseconds = totalStopwatch.ElapsedMilliseconds
            };
        }

        Console.WriteLine($"DEBUG: Found {targetInitLocalId} at index {targetIndex}, OOP={targetMethodOop}");
        Console.WriteLine($"DEBUG: Executing init expression #{targetIndex}: {targetInitLocalId}");

        // Execute the target init expression
        var result = ExecuteInitExpression(targetInitLocalId, targetMethodOop);
        results.Add(result);

        Console.WriteLine($"DEBUG: Init expression #{targetIndex} result: {result.Status}");

        totalStopwatch.Stop();

        return new InitExecutionReport
        {
            Total = count, // Report actual total, not just what we executed
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
    public InitResult ExecuteInitExpression(string initLocalId, int compiledMethodOop, int? timeoutMs = null)
    {
        var effectiveTimeout = timeoutMs ?? _defaultTimeoutMs;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Lookup and sanity-check
            if (!ValidateCompiledMethod(compiledMethodOop, out string? validationError))
            {
                return InitResult.ValidationFailed(initLocalId, validationError ?? "Unknown validation error");
            }

            // Step 2: Prepare canonical receiver
            int receiverOop = GetCanonicalReceiverForDoIt();

            // Step 3: Allocate MethodContext
            // TODO: This needs to be implemented - for now use VM.Execute which internally creates context
            // int methodContextOop = AllocateMethodContextForDoIt(compiledMethodOop, receiverOop);

            // Step 4: Root for GC
            // TODO: Temporarily add methodContextOop to Special Objects Array or GC root set
            // _heap.AddTemporaryGCRoot(methodContextOop);

            // Step 5: Execute with timeout
            int resultOop;
            try
            {
                if (effectiveTimeout > 0)
                {
                    using var cts = new CancellationTokenSource(effectiveTimeout);
                    resultOop = ExecuteWithTimeout(compiledMethodOop, receiverOop, cts.Token);
                }
                else
                {
                    resultOop = _vm.Execute(compiledMethodOop, receiverOop, Array.Empty<int>());
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
            return InitResult.Success(initLocalId, resultOop, stopwatch.ElapsedMilliseconds);
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
    private bool ValidateCompiledMethod(int methodOop, out string? error)
    {
        error = null;

        // Basic validation - just check it's not zero
        if (methodOop == 0)
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
    /// Per Smalltalk-80 semantics: use Smalltalk global if available, otherwise nil.
    /// </summary>
    private int GetCanonicalReceiverForDoIt()
    {
        // Try to get Smalltalk global (SystemDictionary)
        if (GlobalRegistry.TryGet("Smalltalk", out int smalltalkOop))
        {
            return smalltalkOop;
        }

        if (GlobalRegistry.TryGet("SystemDictionary", out int sysDictOop))
        {
            return sysDictOop;
        }

        // Fall back to nil
        return GlobalRegistry.NilObjectId;
    }

    /// <summary>
    /// Executes a method with timeout support.
    /// </summary>
    private int ExecuteWithTimeout(int methodOop, int receiverOop, CancellationToken cancellationToken)
    {
        // TODO: Implement proper timeout support in VM
        // For now, just check cancellation before execution
        cancellationToken.ThrowIfCancellationRequested();
        return _vm.Execute(methodOop, receiverOop, Array.Empty<int>());
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
