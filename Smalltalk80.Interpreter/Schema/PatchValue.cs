namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Patch value descriptor.
/// Matches coldstart-v1.0.2.schema.json patch value structure.
/// </summary>
public class PatchValue
{
    public required string Type { get; init; }  // "objectRef", "symbolRef", "smallInt", "nil"
    public string? LocalId { get; init; }
    public int? SymbolIndex { get; init; }
    public int? IntValue { get; init; }
}
