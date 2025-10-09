namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Discriminated union for object field/indexed values.
/// Matches coldstart-v1.0.2.schema.json#/definitions/fieldValue
/// </summary>
public class FieldValue
{
    public required string Type { get; init; }

    // For "smallInt", "largePositiveInteger", "largeNegativeInteger", "float"
    public object? Value { get; init; }  // int for smallInt, string for large integers, double for float

    // For "symbolRef", "stringRef"
    public int? Index { get; init; }

    // For "objectRef"
    public string? LocalId { get; init; }

    // For "character"
    public int? CodePoint { get; init; }

    // For "bytes"
    public string? Encoding { get; init; }
    public string? Data { get; init; }
}
