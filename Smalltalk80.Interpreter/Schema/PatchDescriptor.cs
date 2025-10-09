namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Patch operation descriptor.
/// Matches coldstart-v1.0.2.schema.json patches items schema.
/// </summary>
public class PatchDescriptor
{
    public required string PatchId { get; init; }
    public required PatchTarget Target { get; init; }
    public required string Operation { get; init; }  // "setValue", "setAssociationValue", "wireMetaclass", "addToSystemDictionary", "custom"
    public PatchValue? Value { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
}
