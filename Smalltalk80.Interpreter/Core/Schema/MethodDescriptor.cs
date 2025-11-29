using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Compiled method descriptor.
/// Matches coldstart-v1.0.8.schema.json methods items schema.
/// v1.0.8: Added optional PoolReferences field for pool dictionary tracking.
/// </summary>
public sealed record MethodDescriptor
{
    /// <summary>
    /// Unique local identifier for this method within the bundle.
    /// </summary>
    [JsonPropertyName("localId")]
    public required string LocalId { get; init; }
    /// <summary>
    /// Local identifier of the class that owns this method.
    /// </summary>
    [JsonPropertyName("homeClassLocalId")]
    public required string HomeClassLocalId { get; init; }
    /// <summary>
    /// Index into the symbols array for the method selector.
    /// </summary>
    [JsonPropertyName("selectorIndex")]
    public required int SelectorIndex { get; init; }
    /// <summary>
    /// Method header containing metadata about the method.
    /// </summary>
    [JsonPropertyName("header")]
    public required MethodHeader Header { get; init; }
    /// <summary>
    /// Array of literal values used by this method.
    /// </summary>
    [JsonPropertyName("literals")]
    public required LiteralValue[] Literals { get; init; }
    /// <summary>
    /// Bytecode array for this method. Can be int[] or base64 string.
    /// </summary>
    [JsonPropertyName("bytecodes")]
    public required object Bytecodes { get; init; }
    /// <summary>
    /// Optional source code for this method.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }
    /// <summary>
    /// Optional index into the source files array (1 = .sources, 2 = .changes).
    /// </summary>
    [JsonPropertyName("fileIndex")]
    public int? FileIndex { get; init; }
    /// <summary>
    /// Optional byte position in the source file for this method's source code.
    /// Used with FileIndex to retrieve source via CompiledMethod>>getSource.
    /// </summary>
    [JsonPropertyName("sourcePosition")]
    public int? SourcePosition { get; init; }
    /// <summary>
    /// Optional notes about this method.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
    /// <summary>
    /// Optional method category for browser display (e.g., "accessing", "initialize-release").
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }
    /// <summary>
    /// Optional ordered argument names for debugging.
    /// </summary>
    [JsonPropertyName("argNames")]
    public string[]? ArgNames { get; init; }
    /// <summary>
    /// Optional ordered temporary variable names for debugging.
    /// </summary>
    [JsonPropertyName("tempNames")]
    public string[]? TempNames { get; init; }

    /// <summary>
    /// Optional pool dictionary references made by this method (v1.0.8).
    /// Used for dependency analysis and initialization ordering.
    /// </summary>
    [JsonPropertyName("poolReferences")]
    public PoolReference[]? PoolReferences { get; init; }
}
