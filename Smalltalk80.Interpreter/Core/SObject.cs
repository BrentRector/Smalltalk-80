using System.Threading;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Base class for all Smalltalk objects in the direct object model.
/// Provides stable identity hashing and instance variable storage.
/// </summary>
public class SObject
{
    /// <summary>
    /// Direct reference to the object's class.
    /// </summary>
    public SClass? Klass { get; set; }

    /// <summary>
    /// Storage for instance variables.
    /// </summary>
    public object?[]? InstVars { get; set; }

    /// <summary>
    /// Cached identity hash value for stable hashing across GC moves.
    /// </summary>
    protected int? _identityHash;

    /// <summary>
    /// Gets the stable identity hash for this object.
    /// The hash is lazily allocated and remains constant for the object's lifetime.
    /// </summary>
    public int IdentityHash
    {
        get
        {
            if (_identityHash.HasValue) return _identityHash.Value;
            
            var h = Interlocked.Increment(ref SObjectHashAllocator.Next);
            if (h == 0) h = Interlocked.Increment(ref SObjectHashAllocator.Next); // Skip 0
            _identityHash = h;
            return h;
        }
    }

    /// <summary>
    /// Initializes a new instance of SObject.
    /// </summary>
    public SObject() { }

    /// <summary>
    /// Initializes a new instance of SObject with a specified class.
    /// </summary>
    /// <param name="klass">The class of this object.</param>
    public SObject(SClass? klass)
    {
        Klass = klass;
    }

    /// <summary>
    /// Initializes a new instance of SObject with a specified class and instance variable count.
    /// </summary>
    /// <param name="klass">The class of this object.</param>
    /// <param name="instVarCount">The number of instance variables to allocate.</param>
    public SObject(SClass? klass, int instVarCount)
    {
        Klass = klass;
        if (instVarCount > 0)
        {
            InstVars = new object?[instVarCount];
        }
    }

    /// <summary>
    /// Gets an instance variable at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <returns>The value of the instance variable, or null if not set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public object? GetInstVar(int index)
    {
        if (InstVars == null || index < 0 || index >= InstVars.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Instance variable index {index} is out of range.");
        
        return InstVars[index];
    }

    /// <summary>
    /// Sets an instance variable at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the instance variable.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public void SetInstVar(int index, object? value)
    {
        if (InstVars == null || index < 0 || index >= InstVars.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Instance variable index {index} is out of range.");
        
        InstVars[index] = value;
    }

    /// <summary>
    /// Gets the number of instance variables in this object.
    /// </summary>
    public int InstVarCount => InstVars?.Length ?? 0;

    /// <summary>
    /// Determines whether this object is a SmallInteger (tagged immediate).
    /// </summary>
    public virtual bool IsImmediate => false;

    /// <summary>
    /// Returns a string representation of this object for debugging.
    /// </summary>
    public override string ToString()
    {
        var className = Klass?.Name ?? "UnknownClass";
        return $"{className}@{IdentityHash:X8}";
    }
}

/// <summary>
/// Global identity hash allocator for stable hash generation.
/// </summary>
internal static class SObjectHashAllocator
{
    /// <summary>
    /// Next available identity hash value.
    /// Starts at 3 to reserve 0, 1, 2 for special objects (nil, true, false).
    /// </summary>
    internal static int Next = 3;
}

/// <summary>
/// Singleton nil object representing undefined/null in Smalltalk.
/// </summary>
public sealed class SNil : SObject
{
    /// <summary>
    /// The singleton instance of nil.
    /// </summary>
    public static readonly SNil Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern.
    /// </summary>
    private SNil()
    {
        _identityHash = 0; // nil always has identity hash 0
    }

    /// <summary>
    /// Returns the string representation of nil.
    /// </summary>
    public override string ToString() => "nil";
}

/// <summary>
/// Singleton true object representing boolean true in Smalltalk.
/// </summary>
public sealed class STrue : SObject
{
    /// <summary>
    /// The singleton instance of true.
    /// </summary>
    public static readonly STrue Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern.
    /// </summary>
    private STrue()
    {
        _identityHash = 1; // true always has identity hash 1
    }

    /// <summary>
    /// Returns the string representation of true.
    /// </summary>
    public override string ToString() => "true";
}

/// <summary>
/// Singleton false object representing boolean false in Smalltalk.
/// </summary>
public sealed class SFalse : SObject
{
    /// <summary>
    /// The singleton instance of false.
    /// </summary>
    public static readonly SFalse Instance = new();

    /// <summary>
    /// Private constructor to enforce singleton pattern.
    /// </summary>
    private SFalse()
    {
        _identityHash = 2; // false always has identity hash 2
    }

    /// <summary>
    /// Returns the string representation of false.
    /// </summary>
    public override string ToString() => "false";
}

