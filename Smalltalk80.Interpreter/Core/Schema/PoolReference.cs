using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Represents a reference to a pool variable within a method or init expression.
/// Used to track which pool dictionaries and symbols are accessed by specific methods.
/// </summary>
public sealed record PoolReference
{
    /// <summary>
    /// Local identifier of the pool dictionary being referenced.
    /// References an object in the ColdStartBundle.Objects array.
    /// </summary>
    [JsonPropertyName("poolLocalId")]
    public required string PoolLocalId { get; init; }

    /// <summary>
    /// Index of the symbol being referenced in the pool dictionary.
    /// References an entry in the ColdStartBundle.Symbols array.
    /// </summary>
    [JsonPropertyName("symbolIndex")]
    public required int SymbolIndex { get; init; }

    /// <summary>
    /// Usage type of this pool reference.
    /// "consume" indicates the method reads from the pool variable.
    /// "produce" indicates the method writes to the pool variable (via at:put:).
    /// </summary>
    [JsonPropertyName("usage")]
    public required string Usage { get; init; } // "consume" or "produce"
}