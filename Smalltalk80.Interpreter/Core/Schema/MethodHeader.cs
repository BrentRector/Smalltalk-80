namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Blue Book method header (16-bit word).
/// Matches coldstart-v1.0.3.schema.json method header structure.
/// v1.0.3: All fields are REQUIRED (not optional).
/// </summary>
public class MethodHeader
{
    public required int Word { get; init; }  // 16-bit header word
    public required int LiteralCount { get; init; }  // v1.0.3: REQUIRED
    public required int TempCount { get; init; }  // v1.0.3: REQUIRED
    public required int ArgCount { get; init; }  // v1.0.3: REQUIRED
    public required int PrimitiveIndex { get; init; }  // v1.0.3: REQUIRED
    public required bool LargeContext { get; init; }  // v1.0.3: REQUIRED
}
