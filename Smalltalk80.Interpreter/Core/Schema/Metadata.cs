using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Cold start bundle metadata.
/// Matches coldstart-v1.0.8.schema.json metadata structure.
/// v1.0.5: Changed BlockContext metadata from dual-ID to single blockMethodId.
/// v1.0.7: Added UndeclaredCount for compiler-tracked undeclared variables.
/// v1.0.8: Removed BlockContextCount, BlockContexts, and UndeclaredCount fields.
/// </summary>
public sealed record Metadata
{
    /// <summary>
    /// Schema version of the cold start bundle.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }
    /// <summary>
    /// Name and version of the tool that generated this bundle.
    /// </summary>
    [JsonPropertyName("generator")]
    public required string Generator { get; init; }
    /// <summary>
    /// ISO 8601 timestamp when this bundle was generated.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public required string GeneratedAt { get; init; }
    /// <summary>
    /// Optional path to the source file used to generate this bundle.
    /// </summary>
    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; init; }
    /// <summary>
    /// Checksum of the source file for verification.
    /// </summary>
    [JsonPropertyName("sourceChecksum")]
    public required string SourceChecksum { get; init; }
    /// <summary>
    /// Optional count of symbols in the bundle.
    /// </summary>
    [JsonPropertyName("symbolCount")]
    public int? SymbolCount { get; init; }
    /// <summary>
    /// Optional count of strings in the bundle.
    /// </summary>
    [JsonPropertyName("stringCount")]
    public int? StringCount { get; init; }
    /// <summary>
    /// Optional count of classes in the bundle.
    /// </summary>
    [JsonPropertyName("classCount")]
    public int? ClassCount { get; init; }
    /// <summary>
    /// Optional count of methods in the bundle.
    /// </summary>
    [JsonPropertyName("methodCount")]
    public int? MethodCount { get; init; }
    /// <summary>
    /// Optional count of objects in the bundle.
    /// </summary>
    [JsonPropertyName("objectCount")]
    public int? ObjectCount { get; init; }
    /// <summary>
    /// Optional count of initialization expressions in the bundle.
    /// </summary>
    [JsonPropertyName("initExpressionCount")]
    public int? InitExpressionCount { get; init; }
    /// <summary>
    /// Optional additional notes about the bundle.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

