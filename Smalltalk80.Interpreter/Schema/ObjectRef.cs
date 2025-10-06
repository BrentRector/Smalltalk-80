namespace Smalltalk80.Interpreter.Schema;

/// <summary>
/// Simple object reference used in association values and patches.
/// </summary>
public class ObjectRef
{
    public required string Type { get; init; }  // Always "objectRef"
    public required string LocalId { get; init; }
}
