namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Patch target descriptor.
/// Matches coldstart-v1.0.0.schema.json patch target structure.
/// </summary>
public class PatchTarget
{
    public required string Kind { get; init; }  // "methodLiteral", "objectField", "objectIndexed", "classSlot", "metaclassWiring", "systemDictionary", "custom"
    public string? MethodLocalId { get; init; }
    public int? LiteralIndex { get; init; }
    public string? ObjectLocalId { get; init; }
    public int? FieldIndex { get; init; }
    public int? IndexedSlot { get; init; }
    public string? ClassLocalId { get; init; }
    public string? SlotName { get; init; }
}
