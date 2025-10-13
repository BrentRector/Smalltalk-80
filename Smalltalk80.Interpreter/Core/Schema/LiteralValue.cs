namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Discriminated union for method literal frame entries.
/// Matches coldstart-v1.0.3.schema.json#/definitions/literalValue
/// </summary>
public class LiteralValue
{
    public required string Type { get; init; }

    // For "symbol", "string"
    public int? Index { get; init; }

    // For "smallInt", "largePositiveInteger", "largeNegativeInteger", "float"
    public object? Value { get; init; }  // int for smallInt, string for large, double for float

    // For "association"
    public AssociationLiteral? Association { get; init; }

    // For "array"
    public LiteralValue[]? Elements { get; init; }

    // For "objectRef" or "compiledMethod" (blocks reference methods via localId)
    public string? LocalId { get; init; }

    // For "character"
    public int? CodePoint { get; init; }

    // For "bytes"
    public string? Encoding { get; init; }
    public string? Data { get; init; }
}
