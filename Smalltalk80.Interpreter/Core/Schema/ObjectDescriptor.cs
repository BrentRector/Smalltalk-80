namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Concrete object descriptor.
/// Matches coldstart-v1.0.3.schema.json objects items schema.
/// </summary>
public class ObjectDescriptor
{
    public required string LocalId { get; init; }
    public required int ClassNameIndex { get; init; }
    public required string FormatHint { get; init; }  // "pointers", "words", "bytes", "compiledMethod", "blockContext", "custom"
    public required FieldValue[] Fields { get; init; }
    public required FieldValue[] IndexedFields { get; init; }
    public BlockContextStructure? BlockContextStructure { get; init; }  // v1.0.3: Optional explicit structure for blockContext objects
    public string? Notes { get; init; }
}
