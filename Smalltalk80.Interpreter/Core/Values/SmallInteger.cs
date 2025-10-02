namespace Smalltalk80.Interpreter.Core.Values;

using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk SmallInteger (tagged immediate value).
/// </summary>
public sealed class SmallInteger : SObject, IEquatable<SmallInteger>, IComparable<SmallInteger>
{
    /// <summary>
    /// The minimum value for a SmallInteger (30-bit on 32-bit, 62-bit on 64-bit).
    /// </summary>
    public static readonly int MinValue = IntPtr.Size == 8 ? -(1 << 30) : -(1 << 14);
    
    /// <summary>
    /// The maximum value for a SmallInteger.
    /// </summary>
    public static readonly int MaxValue = IntPtr.Size == 8 ? (1 << 30) - 1 : (1 << 14) - 1;
    
    private readonly int value;
    
    /// <summary>
    /// Initializes a new SmallInteger with the specified value.
    /// </summary>
    public SmallInteger(int value)
    {
        if (value < MinValue || value > MaxValue)
        {
            throw new OverflowException($"Value {value} is out of SmallInteger range");
        }
        this.value = value;
    }
    
    /// <summary>
    /// Gets the integer value.
    /// </summary>
    public int Value => value;
    
    /// <summary>
    /// Gets the class of SmallInteger.
    /// </summary>
    
    /// <summary>
    /// Gets whether this is an immediate value.
    /// </summary>
    public override bool IsImmediate => true;
    
    /// <summary>
    /// Creates a SmallInteger from an integer value.
    /// </summary>
    public static SmallInteger From(int value) => new(value);
    
    /// <summary>
    /// Adds two SmallIntegers.
    /// </summary>
    public SmallInteger Add(SmallInteger other)
    {
        long result = (long)value + other.value;
        if (result < MinValue || result > MaxValue)
        {
            // Would overflow to LargeInteger in real Smalltalk
            throw new OverflowException("SmallInteger overflow");
        }
        return new SmallInteger((int)result);
    }
    
    /// <summary>
    /// Subtracts another SmallInteger from this one.
    /// </summary>
    public SmallInteger Subtract(SmallInteger other)
    {
        long result = (long)value - other.value;
        if (result < MinValue || result > MaxValue)
        {
            throw new OverflowException("SmallInteger overflow");
        }
        return new SmallInteger((int)result);
    }
    
    /// <summary>
    /// Multiplies two SmallIntegers.
    /// </summary>
    public SmallInteger Multiply(SmallInteger other)
    {
        long result = (long)value * other.value;
        if (result < MinValue || result > MaxValue)
        {
            throw new OverflowException("SmallInteger overflow");
        }
        return new SmallInteger((int)result);
    }
    
    /// <summary>
    /// Divides this SmallInteger by another (integer division).
    /// </summary>
    public SmallInteger Divide(SmallInteger other)
    {
        if (other.value == 0)
        {
            throw new DivideByZeroException();
        }
        return new SmallInteger(value / other.value);
    }
    
    /// <summary>
    /// Gets the remainder of dividing this SmallInteger by another.
    /// </summary>
    public SmallInteger Modulo(SmallInteger other)
    {
        if (other.value == 0)
        {
            throw new DivideByZeroException();
        }
        return new SmallInteger(value % other.value);
    }
    
    /// <summary>
    /// Compares this SmallInteger to another.
    /// </summary>
    public int CompareTo(SmallInteger? other)
    {
        if (other == null) return 1;
        return value.CompareTo(other.value);
    }
    
    /// <summary>
    /// Converts this SmallInteger to a display string.
    /// </summary>
    public override string ToDisplayString() => value.ToString();
    
    /// <summary>
    /// Gets the hash code for this SmallInteger.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether this SmallInteger equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is SmallInteger other && Equals(other);
    
    /// <summary>
    /// Determines whether this SmallInteger equals another SmallInteger.
    /// </summary>
    public bool Equals(SmallInteger? other) => other != null && value == other.value;
    
    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SmallInteger? left, SmallInteger? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.value == right.value;
    }
    
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SmallInteger? left, SmallInteger? right) => !(left == right);
    
    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(SmallInteger left, SmallInteger right) => left.value < right.value;
    
    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(SmallInteger left, SmallInteger right) => left.value > right.value;
    
    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(SmallInteger left, SmallInteger right) => left.value <= right.value;
    
    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(SmallInteger left, SmallInteger right) => left.value >= right.value;
}