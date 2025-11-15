using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents a pool dictionary consumer in the dependency graph.
/// A consumer is a class that references pool variables, either by
/// declaring pool dictionaries or having methods that access pool variables.
/// </summary>
public sealed record Consumer
{
    /// <summary>
    /// Local identifier of the class that consumes pool variables.
    /// References a class in the ColdStartBundle.Classes array.
    /// </summary>
    [JsonPropertyName("classLocalId")]
    public required string ClassLocalId { get; init; }

    /// <summary>
    /// Local identifier of the pool dictionary being consumed.
    /// References an object in the ColdStartBundle.Objects array.
    /// </summary>
    [JsonPropertyName("poolLocalId")]
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// List of symbol indices for the pool variables being consumed.
    /// Each index references an entry in the ColdStartBundle.Symbols array.
    /// Represents the keys being referenced from the pool dictionary.
    /// </summary>
    [JsonPropertyName("symbols")]
    public required List<int> Symbols { get; init; }

    /// <summary>
    /// Optional local identifier of the specific method that consumes the pool variables.
    /// May be null if consumption spans multiple methods or is not specifically tracked.
    /// </summary>
    [JsonPropertyName("methodLocalId")]
    public string? MethodLocalId { get; init; }

    /// <summary>
    /// Optional notes about this consumer.
    /// May include details about how the pool variables are used or special requirements.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}