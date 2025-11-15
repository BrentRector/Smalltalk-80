using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Initialization expression (DoIt) descriptor.
/// Matches coldstart-v1.0.8.schema.json initExpressions items schema.
/// v1.0.8: Added optional PoolReferences field for pool dictionary tracking.
/// </summary>
public sealed record InitExpression
{
    /// <summary>
    /// Unique local identifier for this initialization expression.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }
    /// <summary>
    /// Method header containing metadata about the expression.
    /// </summary>
    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }
    /// <summary>
    /// Array of literal values used by this expression.
    /// </summary>
    [JsonPropertyName("literals")]
    public required LiteralValue[] Literals { get; init; }
    /// <summary>
    /// Bytecode array for this expression. Can be int[] or base64 string.
    /// </summary>
    [JsonPropertyName("bytecodes")]
    public required object Bytecodes { get; init; }
    /// <summary>
    /// Optional source code for this expression.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }
    /// <summary>
    /// Optional notes about this expression.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Optional pool dictionary references made by this expression (v1.0.8).
    /// Used for dependency analysis and initialization ordering.
    /// </summary>
    [JsonPropertyName("poolReferences")]
    public PoolReference[]? PoolReferences { get; init; }
}
