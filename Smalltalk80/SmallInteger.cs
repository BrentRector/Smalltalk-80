// ==============================================================================
// SmallInteger - Wrapper for immediate integer values in Smalltalk
// ==============================================================================

using System;

namespace Smalltalk80;

/// <summary>
/// Represents a SmallInteger immediate value in Smalltalk.
/// In a real Smalltalk-80 VM, SmallIntegers are tagged pointers, not objects.
/// This wrapper allows us to use them during bootstrap.
/// </summary>
public sealed class SmallInteger : SmalltalkObject
{
    /// <summary>
    /// Gets the integer value.
    /// </summary>
    public int Value { get; }
    
    /// <summary>
    /// Initializes a new SmallInteger with the specified value.
    /// </summary>
    public SmallInteger(int value) : base(null, 0)
    {
        Value = value;
    }
    
    /// <summary>
    /// Implicit conversion from int to SmallInteger.
    /// </summary>
    public static implicit operator SmallInteger(int value)
    {
        return new SmallInteger(value);
    }
    
    /// <summary>
    /// Implicit conversion from SmallInteger to int.
    /// </summary>
    public static implicit operator int(SmallInteger smallInt)
    {
        return smallInt.Value;
    }
    
    public override string ToString()
    {
        return Value.ToString();
    }
    
    public override bool Equals(object? obj)
    {
        return obj is SmallInteger other && Value == other.Value;
    }
    
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}