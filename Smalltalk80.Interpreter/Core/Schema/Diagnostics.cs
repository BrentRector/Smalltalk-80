using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Diagnostic information for pool dependency graph validation.
/// Contains messages, errors, and other diagnostic data generated
/// during pool dependency analysis and validation.
/// </summary>
public sealed record Diagnostics
{
    /// <summary>
    /// Optional list of informational messages about the dependency graph.
    /// Includes warnings, informational notices, and validation results.
    /// </summary>
    [JsonPropertyName("messages")]
    public List<string>? Messages { get; init; }

    /// <summary>
    /// Optional list of error messages encountered during dependency analysis.
    /// Includes circular dependencies, missing producers, and validation failures.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<string>? Errors { get; init; }

    /// <summary>
    /// Optional additional notes about the diagnostic results.
    /// May include summary information or recommendations for resolving issues.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}