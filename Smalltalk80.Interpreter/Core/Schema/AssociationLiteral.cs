namespace Smalltalk80.Interpreter.Core.Schema;

/// <summary>
/// Association literal structure for method literals.
/// Matches coldstart-v1.0.2.schema.json literalValue association structure.
/// Supports both global variables (poolDictionaryName = null) and pool variables.
/// </summary>
public class AssociationLiteral
{
    public required string KeyType { get; init; }  // Must be "symbol"
    public required int KeyIndex { get; init; }    // Index into symbols[]
    public ObjectRef? ValueRef { get; init; }      // Can be null (will be patched)
    public string? PoolDictionaryName { get; init; } // Pool dictionary name for pool variables, null for globals
}
