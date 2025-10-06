namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Source file manifest entry.
/// Matches coldstart-v1.0.0.schema.json sourceFiles items schema.
/// </summary>
public class SourceFile
{
    public required int Index { get; init; }
    public required string Path { get; init; }
    public string? Checksum { get; init; }
    public string? Notes { get; init; }
}
