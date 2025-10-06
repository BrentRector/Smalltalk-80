namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Initialization expression (DoIt) descriptor.
/// Matches coldstart-v1.0.0.schema.json initExpressions items schema.
/// </summary>
public class InitExpression
{
    public required string LocalId { get; init; }
    public required MethodHeader Header { get; init; }
    public required LiteralValue[] Literals { get; init; }
    public required object Bytecodes { get; init; }  // int[] or string (base64)
    public string? Source { get; init; }
    public string? Notes { get; init; }
}
