using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents a dependency edge in the pool dependency graph.
/// An edge indicates that one class must initialize before another
/// to satisfy pool dictionary dependencies.
/// </summary>
public sealed record Edge
{
    /// <summary>
    /// Local identifier of the source class (dependency).
    /// This class must initialize before the target class.
    /// References a class in the ColdStartBundle.Classes array.
    /// </summary>
    [JsonPropertyName("from")]
    public required string From { get; init; }

    /// <summary>
    /// Local identifier of the target class (dependent).
    /// This class depends on the source class and must initialize after it.
    /// References a class in the ColdStartBundle.Classes array.
    /// </summary>
    [JsonPropertyName("to")]
    public required string To { get; init; }

    /// <summary>
    /// Optional list of symbol indices that represent the specific pool variables
    /// involved in this dependency relationship. Each index references an entry
    /// in the ColdStartBundle.Symbols array. May be null if not tracked at symbol level.
    /// </summary>
    [JsonPropertyName("symbols")]
    public List<int>? Symbols { get; init; }

    /// <summary>
    /// Optional notes about this dependency edge.
    /// May include details about why this dependency exists or special handling requirements.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}