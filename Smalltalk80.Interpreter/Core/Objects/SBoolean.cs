namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Base class for Smalltalk boolean objects.
/// </summary>
public abstract class SBoolean : SObject
{
    /// <summary>
    /// Protected constructor for derived classes.
    /// </summary>
    protected SBoolean() { }
    
    /// <summary>
    /// Gets the boolean value.
    /// </summary>
    public abstract bool Value { get; }
}

/// <summary>
/// Represents the Smalltalk true object (singleton).
/// </summary>
public sealed class STrue : SBoolean
{
    private static readonly STrue instance = new();
    
    /// <summary>
    /// Private constructor for singleton.
    /// </summary>
    private STrue() { }
    
    /// <summary>
    /// Gets the singleton true instance.
    /// </summary>
    public static STrue Instance => instance;
    
    /// <summary>
    /// Gets the boolean value (true).
    /// </summary>
    public override bool Value => true;
    
    /// <summary>
    /// Gets the class of true (True).
    /// </summary>
    public override SClass Class => SystemDictionary.TrueClass;
    
    /// <summary>
    /// Converts true to a display string.
    /// </summary>
    public override string ToDisplayString() => "true";
    
    /// <summary>
    /// Gets the hash code for true.
    /// </summary>
    public override int GetHashCode() => 1;
    
    /// <summary>
    /// Determines whether this object equals another object.
    /// </summary>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
}

/// <summary>
/// Represents the Smalltalk false object (singleton).
/// </summary>
public sealed class SFalse : SBoolean
{
    private static readonly SFalse instance = new();
    
    /// <summary>
    /// Private constructor for singleton.
    /// </summary>
    private SFalse() { }
    
    /// <summary>
    /// Gets the singleton false instance.
    /// </summary>
    public static SFalse Instance => instance;
    
    /// <summary>
    /// Gets the boolean value (false).
    /// </summary>
    public override bool Value => false;
    
    /// <summary>
    /// Gets the class of false (False).
    /// </summary>
    public override SClass Class => SystemDictionary.FalseClass;
    
    /// <summary>
    /// Converts false to a display string.
    /// </summary>
    public override string ToDisplayString() => "false";
    
    /// <summary>
    /// Gets the hash code for false.
    /// </summary>
    public override int GetHashCode() => 0;
    
    /// <summary>
    /// Determines whether this object equals another object.
    /// </summary>
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);
}