namespace Smalltalk80.Interpreter.Core.Values;

using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Float value in Smalltalk-80.
/// Floats are variable word objects that store raw numeric data.
/// </summary>
public sealed class SFloat : SObject
{
    private readonly double value;
    private static SClass? floatClass;
    
    /// <summary>
    /// Initializes a new instance of the SFloat class.
    /// </summary>
    /// <param name="value">The double value to wrap.</param>
    internal SFloat(double value)
    {
        this.value = value;
    }
    
    /// <summary>
    /// Gets the wrapped double value.
    /// </summary>
    public double Value => value;
    
    /// <summary>
    /// Gets the Float class.
    /// </summary>
    public override SClass Class => floatClass ?? throw new InvalidOperationException("Float class not initialized");
    
    /// <summary>
    /// Sets the Float class (called during bootstrap).
    /// </summary>
    internal static void SetFloatClass(SClass sClass)
    {
        floatClass = sClass;
    }
    
    /// <summary>
    /// Creates a Float from a double value.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>An SFloat instance.</returns>
    public static SFloat From(double value)
    {
        return new SFloat(value);
    }
    
    /// <summary>
    /// Creates a Float from a SmallInteger.
    /// </summary>
    /// <param name="integer">The SmallInteger to convert.</param>
    /// <returns>An SFloat instance.</returns>
    public static SFloat FromInteger(SmallInteger integer)
    {
        return new SFloat(integer.Value);
    }
    
    /// <summary>
    /// Adds another Float or SmallInteger to this Float.
    /// </summary>
    public SFloat Add(SObject other)
    {
        var otherValue = other switch
        {
            SFloat f => f.value,
            SmallInteger i => (double)i.Value,
            _ => throw new ArgumentException($"Cannot add {other.GetType().Name} to Float")
        };
        return From(value + otherValue);
    }
    
    /// <summary>
    /// Subtracts another Float or SmallInteger from this Float.
    /// </summary>
    public SFloat Subtract(SObject other)
    {
        var otherValue = other switch
        {
            SFloat f => f.value,
            SmallInteger i => (double)i.Value,
            _ => throw new ArgumentException($"Cannot subtract {other.GetType().Name} from Float")
        };
        return From(value - otherValue);
    }
    
    /// <summary>
    /// Multiplies this Float by another Float or SmallInteger.
    /// </summary>
    public SFloat Multiply(SObject other)
    {
        var otherValue = other switch
        {
            SFloat f => f.value,
            SmallInteger i => (double)i.Value,
            _ => throw new ArgumentException($"Cannot multiply Float by {other.GetType().Name}")
        };
        return From(value * otherValue);
    }
    
    /// <summary>
    /// Divides this Float by another Float or SmallInteger.
    /// </summary>
    public SFloat Divide(SObject other)
    {
        var otherValue = other switch
        {
            SFloat f => f.value,
            SmallInteger i => (double)i.Value,
            _ => throw new ArgumentException($"Cannot divide Float by {other.GetType().Name}")
        };
        
        if (otherValue == 0.0)
        {
            throw new DivideByZeroException();
        }
        
        return From(value / otherValue);
    }
    
    /// <summary>
    /// Compares this Float to another Float or SmallInteger.
    /// </summary>
    public int CompareTo(SObject other)
    {
        var otherValue = other switch
        {
            SFloat f => f.value,
            SmallInteger i => (double)i.Value,
            _ => throw new ArgumentException($"Cannot compare Float to {other.GetType().Name}")
        };
        return value.CompareTo(otherValue);
    }
    
    /// <summary>
    /// Truncates this Float to an integer.
    /// </summary>
    public SmallInteger Truncate()
    {
        var truncated = (int)Math.Truncate(value);
        if (truncated >= SmallInteger.MinValue && truncated <= SmallInteger.MaxValue)
        {
            return SmallInteger.From(truncated);
        }
        throw new OverflowException($"Truncated value {truncated} exceeds SmallInteger range");
    }
    
    /// <summary>
    /// Returns the fractional part of this Float.
    /// </summary>
    public SFloat FractionPart()
    {
        return From(value - Math.Truncate(value));
    }
    
    /// <summary>
    /// Returns the exponent of this Float.
    /// </summary>
    public SmallInteger Exponent()
    {
        if (value == 0.0) return SmallInteger.From(0);
        
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 0x7FF) - 1023;
        return SmallInteger.From(exponent);
    }
    
    /// <summary>
    /// Gets the hash code for this Float.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether this Float equals another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj switch
        {
            SFloat f => value == f.value,
            SmallInteger i => value == i.Value,
            _ => false
        };
    }
    
    /// <summary>
    /// Converts this Float to a display string.
    /// </summary>
    public override string ToDisplayString() => value.ToString();
    
    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SFloat? left, SFloat? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.value == right.value;
    }
    
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SFloat? left, SFloat? right) => !(left == right);
    
    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(SFloat left, SFloat right) => left.value < right.value;
    
    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(SFloat left, SFloat right) => left.value > right.value;
    
    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(SFloat left, SFloat right) => left.value <= right.value;
    
    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(SFloat left, SFloat right) => left.value >= right.value;
}