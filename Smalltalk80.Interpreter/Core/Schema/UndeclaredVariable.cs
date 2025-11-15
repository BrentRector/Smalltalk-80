using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents an undeclared variable found during compilation (v1.0.7 compatibility).
/// The compiler tracks all references to variables that couldn't be resolved.
/// These will be added to the Undeclared dictionary during cold start.
/// Note: This class is maintained for backward compatibility with v1.0.7 bundles.
/// </summary>
public sealed record UndeclaredVariable
{
    /// <summary>
    /// Name of the undeclared variable (e.g., "Display", "Processor", "Transcript").
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// List of method identifiers that reference this variable.
    /// Format: "ClassName>>selector" or "class:ClassName>>selector"
    /// Used for diagnostics and debugging.
    /// </summary>
    [JsonPropertyName("references")]
    public required string[] References { get; init; }
}