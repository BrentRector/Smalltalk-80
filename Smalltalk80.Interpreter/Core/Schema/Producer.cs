using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents a pool dictionary producer in the dependency graph.
/// A producer is a class that populates a pool dictionary, typically
/// by calling at:put: methods in its initialize method.
/// </summary>
public sealed record Producer
{
    /// <summary>
    /// Local identifier of the class that produces pool variables.
    /// References a class in the ColdStartBundle.Classes array.
    /// </summary>
    [JsonPropertyName("classLocalId")]
    public required string ClassLocalId { get; init; }

    /// <summary>
    /// Local identifier of the pool dictionary being populated.
    /// References an object in the ColdStartBundle.Objects array.
    /// </summary>
    [JsonPropertyName("poolLocalId")]
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// List of symbol indices for the pool variables being produced.
    /// Each index references an entry in the ColdStartBundle.Symbols array.
    /// Represents the keys being added to the pool dictionary.
    /// </summary>
    [JsonPropertyName("symbols")]
    public required List<int> Symbols { get; init; }

    /// <summary>
    /// Optional local identifier of the specific method that produces the pool variables.
    /// Typically the class's initialize method. May be null if not specifically tracked.
    /// </summary>
    [JsonPropertyName("methodLocalId")]
    public string? MethodLocalId { get; init; }

    /// <summary>
    /// Optional notes about this producer.
    /// May include implementation details or special handling requirements.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}