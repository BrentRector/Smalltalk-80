namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents an undeclared variable found during compilation.
/// The compiler tracks all references to variables that couldn't be resolved.
/// These will be added to the Undeclared dictionary during cold start.
/// </summary>
public class UndeclaredVariable
{
    /// <summary>
    /// Name of the undeclared variable (e.g., "Display", "Processor", "Transcript").
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// List of method identifiers that reference this variable.
    /// Format: "ClassName>>selector" or "class:ClassName>>selector"
    /// Used for diagnostics and debugging.
    /// </summary>
    public required string[] References { get; init; }
}
