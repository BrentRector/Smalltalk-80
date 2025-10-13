namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// BlockContext object structure (v1.0.3).
/// Explicit field layout for validation and documentation.
/// Fields array for BlockContext objects contains:
/// [method, initialIP, blockArgumentCount, homeContext, outerContext, unused]
/// </summary>
public class BlockContextStructure
{
    public required string Method { get; init; }  // LocalId of the block's CompiledMethod
    public required int InitialIP { get; init; }  // Instruction pointer (typically 0 for blocks)
    public required int BlockArgumentCount { get; init; }  // Number of block arguments (0-15)
    public string? HomeContext { get; init; }  // LocalId of enclosing method/block (may be null)
    public string? OuterContext { get; init; }  // LocalId of outermost method (may be null)
}
