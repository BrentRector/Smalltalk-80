namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Blue Book method header (16-bit word).
/// Matches coldstart-v1.0.2.schema.json method header structure.
/// </summary>
public class MethodHeader
{
    public required int Word { get; init; }  // 16-bit header word
    public int? LiteralCount { get; init; }
    public int? TempCount { get; init; }
    public int? ArgCount { get; init; }
    public int? PrimitiveIndex { get; init; }
    public bool? LargeContext { get; init; }
}
