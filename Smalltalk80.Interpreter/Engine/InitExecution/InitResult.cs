namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Result of executing a single init expression (DoIt).
/// Maps interpreter outcomes to deterministic status codes for CI/CD parsing.
/// </summary>
public record InitResult
{
    public string InitLocalId { get; init; } = "";
    public InitStatus Status { get; init; }
    public int? ReturnValueOop { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public InitDiagnostic? Diagnostic { get; init; }

    public static InitResult Success(string initLocalId, int returnValueOop, long elapsedMs)
        => new() { InitLocalId = initLocalId, Status = InitStatus.Success, ReturnValueOop = returnValueOop, ElapsedMilliseconds = elapsedMs };

    public static InitResult ValidationFailed(string initLocalId, string reason)
        => new() { InitLocalId = initLocalId, Status = InitStatus.ValidationFailed, Diagnostic = new InitDiagnostic { Message = reason } };

    public static InitResult Exception(string initLocalId, int exceptionOop, string message, string stackTrace, long elapsedMs)
        => new()
        {
            InitLocalId = initLocalId,
            Status = InitStatus.Exception,
            ElapsedMilliseconds = elapsedMs,
            Diagnostic = new InitDiagnostic
            {
                ExceptionOop = exceptionOop,
                Message = message,
                StackTrace = stackTrace
            }
        };

    public static InitResult PrimitiveFailure(string initLocalId, int primitiveIndex, string reason, long elapsedMs)
        => new()
        {
            InitLocalId = initLocalId,
            Status = InitStatus.PrimitiveFailure,
            ElapsedMilliseconds = elapsedMs,
            Diagnostic = new InitDiagnostic
            {
                PrimitiveIndex = primitiveIndex,
                Message = reason
            }
        };

    public static InitResult Timeout(string initLocalId, long elapsedMs)
        => new() { InitLocalId = initLocalId, Status = InitStatus.Timeout, ElapsedMilliseconds = elapsedMs };

    public static InitResult FatalError(string initLocalId, string message)
        => new() { InitLocalId = initLocalId, Status = InitStatus.FatalError, Diagnostic = new InitDiagnostic { Message = message } };
}

/// <summary>
/// Deterministic status codes for init expression execution.
/// </summary>
public enum InitStatus
{
    Success,            // Normal return
    Exception,          // Smalltalk exception raised
    PrimitiveFailure,   // Primitive failed without fallback
    Timeout,            // Execution exceeded timeout
    ValidationFailed,   // Pre-execution validation failed
    FatalError          // VM-level error (should not happen in production)
}

/// <summary>
/// Detailed diagnostic information for non-success init results.
/// </summary>
public record InitDiagnostic
{
    public string? Message { get; init; }
    public int? ExceptionOop { get; init; }
    public int? PrimitiveIndex { get; init; }
    public string? StackTrace { get; init; }
    public int? FailedAtPc { get; init; }
}

/// <summary>
/// Policy for handling init expression failures during cold start.
/// </summary>
public enum InitPolicy
{
    /// <summary>
    /// Stop processing remaining init expressions on first error.
    /// Surface error immediately and fail cold start.
    /// </summary>
    AbortOnFirstError,

    /// <summary>
    /// Continue processing all init expressions, collecting diagnostics.
    /// Allow cold start to complete even if some inits fail.
    /// </summary>
    ContinueAndCollect
}

/// <summary>
/// Aggregate report of all init expression executions.
/// </summary>
public record InitExecutionReport
{
    public int Total { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public List<InitResult> Results { get; init; } = new();
    public long TotalElapsedMilliseconds { get; init; }

    public List<InitResult> Failures => Results.Where(r => r.Status != InitStatus.Success).ToList();
}
