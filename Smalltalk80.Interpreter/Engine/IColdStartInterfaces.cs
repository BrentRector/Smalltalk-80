using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Common interface for cold start phases.
/// Enables testability, composability, and clear phase contracts.
/// Each phase has explicit preconditions and postconditions that can be validated.
/// </summary>
public interface IPhase
{
    /// <summary>
    /// Gets the phase name for diagnostics and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the phase number (1-6).
    /// </summary>
    int PhaseNumber { get; }

    /// <summary>
    /// Executes the phase.
    /// Throws if preconditions are not met or execution fails.
    /// </summary>
    void Run();

    /// <summary>
    /// Validates phase preconditions before execution.
    /// Returns list of failed preconditions, empty if all pass.
    /// </summary>
    /// <returns>List of precondition failure messages.</returns>
    IReadOnlyList<string> ValidatePreconditions();

    /// <summary>
    /// Validates phase postconditions after execution.
    /// Returns list of failed postconditions, empty if all pass.
    /// </summary>
    /// <returns>List of postcondition failure messages.</returns>
    IReadOnlyList<string> ValidatePostconditions();
}

/// <summary>
/// Public API for cold start loader.
/// Builds heap from ColdStartBundle and prepares VM for execution.
/// </summary>
public interface IColdStartLoader
{
    /// <summary>
    /// Executes all cold start phases in order.
    /// Does NOT execute init expressions - that should be done separately.
    /// </summary>
    void Run();

    /// <summary>
    /// Gets the layout resolver (available after Run() completes).
    /// Used by VM for field access during runtime.
    /// </summary>
    LayoutResolver? LayoutResolver { get; }

    /// <summary>
    /// Gets the localId to OOP mapping (available after Run() completes).
    /// Maps compiler-generated localIds to allocated heap OOPs.
    /// Returns null after ReleaseResources() is called.
    /// </summary>
    IReadOnlyDictionary<string, SmalltalkOOP>? LocalIdToObjectId { get; }

    /// <summary>
    /// Gets the cold start execution report with metrics and timing.
    /// </summary>
    ColdstartReport Report { get; }

    /// <summary>
    /// Gets all phases for inspection/testing.
    /// Returns null after ReleaseResources() is called.
    /// </summary>
    IReadOnlyList<IPhase>? Phases { get; }
}

/// <summary>
/// Result of phase validation.
/// </summary>
public readonly struct PhaseValidationResult
{
    /// <summary>
    /// Gets whether all validations passed.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the list of failure messages (empty if success).
    /// </summary>
    public IReadOnlyList<string> Failures { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static PhaseValidationResult Ok() => new(true, Array.Empty<string>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static PhaseValidationResult Fail(IReadOnlyList<string> failures) => new(false, failures);

    private PhaseValidationResult(bool success, IReadOnlyList<string> failures)
    {
        Success = success;
        Failures = failures;
    }
}
