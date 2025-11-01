namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Cold start bundle metadata.
/// Matches coldstart-v1.0.7.schema.json metadata structure.
/// v1.0.5: Changed BlockContext metadata from dual-ID to single blockMethodId.
/// v1.0.7: Added UndeclaredCount for compiler-tracked undeclared variables.
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
    public int? BlockContextCount { get; init; }
    public int? UndeclaredCount { get; init; }
    public BlockContextMetadata[]? BlockContexts { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// BlockContext metadata entry (v1.0.5).
/// These are tooling-only templates, NOT runtime objects.
/// Used by validators and debuggers to inspect closure structure.
/// v1.0.5: Uses single blockMethodId instead of localId + method.
/// </summary>
public class BlockContextMetadata
{
    public required string BlockMethodId { get; init; }
    public required int InitialIP { get; init; }
    public required int BlockArgumentCount { get; init; }
    public string? HomeContext { get; init; }
    public string? OuterContext { get; init; }
}
