namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Class and metaclass descriptor.
/// Matches coldstart-v1.0.0.schema.json classes items schema.
/// </summary>
public class ClassDescriptor
{
    public required string LocalId { get; init; }
    public required int NameIndex { get; init; }
    public required bool IsMetaclass { get; init; }
    public string? MetaclassOfLocalId { get; init; }
    public required int Format { get; init; }  // 16-bit format word
    public required int[] InstVarNames { get; init; }
    public required int[] ClassVarNames { get; init; }
    public required string[] PoolLocalIds { get; init; }
    public int? SuperNameIndex { get; init; }
    public required string Category { get; init; }
    public int? CommentStringIndex { get; init; }
    public string? ClassSideInitMethodLocalId { get; init; }
    public string? Notes { get; init; }
}
