namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Association literal structure for method literals.
/// Matches coldstart-v1.0.0.schema.json literalValue association structure.
/// </summary>
public class AssociationLiteral
{
    public required string KeyType { get; init; }  // Must be "symbol"
    public required int KeyIndex { get; init; }    // Index into symbols[]
    public ObjectRef? ValueRef { get; init; }      // Can be null (will be patched)
}
