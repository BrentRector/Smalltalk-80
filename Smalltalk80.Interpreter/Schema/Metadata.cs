namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Cold start bundle metadata.
/// Matches coldstart-v1.0.0.schema.json metadata structure.
/// </summary>
public class Metadata
{
    public required string Version { get; init; }
    public required string Generator { get; init; }
    public required string GeneratedAt { get; init; }
    public string? SourceFile { get; init; }
    public required string SourceChecksum { get; init; }
    public int? SymbolCount { get; init; }
    public int? StringCount { get; init; }
    public int? ClassCount { get; init; }
    public int? MethodCount { get; init; }
    public int? ObjectCount { get; init; }
    public int? InitExpressionCount { get; init; }
    public string? Notes { get; init; }
}
