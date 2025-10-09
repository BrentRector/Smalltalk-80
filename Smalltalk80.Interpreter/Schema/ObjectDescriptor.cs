namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Concrete object descriptor.
/// Matches coldstart-v1.0.2.schema.json objects items schema.
/// </summary>
public class ObjectDescriptor
{
    public required string LocalId { get; init; }
    public required int ClassNameIndex { get; init; }
    public required string FormatHint { get; init; }  // "pointers", "words", "bytes", "compiledMethod", "custom"
    public required FieldValue[] Fields { get; init; }
    public required FieldValue[] IndexedFields { get; init; }
    public string? Notes { get; init; }
}
