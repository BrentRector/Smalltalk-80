namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Compiled method descriptor.
/// Matches coldstart-v1.0.0.schema.json methods items schema.
/// </summary>
public class MethodDescriptor
{
    public required string LocalId { get; init; }
    public required string HomeClassLocalId { get; init; }
    public required int SelectorIndex { get; init; }
    public required MethodHeader Header { get; init; }
    public required LiteralValue[] Literals { get; init; }
    public required object Bytecodes { get; init; }  // int[] or string (base64)
    public string? Source { get; init; }
    public int? FileIndex { get; init; }
    public string? Notes { get; init; }
    public string[]? ArgNames { get; init; }  // Ordered argument names
    public string[]? TempNames { get; init; }  // Ordered temporary variable names
}
