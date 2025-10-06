namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Base class for bootstrap Smalltalk objects (compatibility with FullInterpreter).
/// This is a stub for the new architecture where objects are managed through ObjectMemory.
/// </summary>
public class SObject
{
    public override string ToString() => GetType().Name;
}
