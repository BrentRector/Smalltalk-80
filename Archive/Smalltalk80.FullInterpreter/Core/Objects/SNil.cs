namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents the Smalltalk nil object (singleton).
/// </summary>
public sealed class SNil : SObject
{
    private static readonly SNil instance = new();
    
    /// <summary>
    /// Private constructor for singleton.
    /// </summary>
    private SNil() { }
    
    /// <summary>
    /// Gets the singleton nil instance.
    /// </summary>
    public static SNil Instance => instance;
    
    /// <summary>
    /// Gets whether this object is nil.
    /// </summary>
    public override bool IsNil => true;
    
    /// <summary>
    /// Gets the class of nil (UndefinedObject).
    /// </summary>
    
    /// <summary>
    /// Converts nil to a display string.
    /// </summary>
    public override string ToDisplayString() => "nil";
    
    /// <summary>
    /// Gets the hash code for nil.
    /// </summary>
    public override int GetHashCode() => 0;
    
    /// <summary>
    /// Determines whether this object equals another object.
    /// </summary>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
}