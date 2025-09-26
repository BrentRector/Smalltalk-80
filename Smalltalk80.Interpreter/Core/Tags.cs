using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Provides efficient tagged immediate value support for SmallInteger and Character types.
/// Uses dual-bit tagging to distinguish between object pointers and immediate values.
/// </summary>
/// <remarks>
/// Bit patterns:
/// - xxxxxxx01 = SmallInteger (odd)
/// - xxxxxxx10 = Character (even, non-zero)
/// - xxxxxxx00 = Object pointer (divisible by 4)
/// </remarks>
public static class Tags
{
    /// <summary>
    /// Tag mask for SmallInteger values (LSB = 01).
    /// </summary>
    private const long SmallIntTag = 0x1;

    /// <summary>
    /// Tag mask for Character values (LSB = 10).
    /// </summary>
    private const long CharacterTag = 0x2;

    /// <summary>
    /// Tag mask for checking immediate type (both tag bits).
    /// </summary>
    private const long TagMask = 0x3;

    /// <summary>
    /// Shift amount for encoding/decoding values (2 bits for tags).
    /// </summary>
    private const int TagShift = 2;

    /// <summary>
    /// Minimum SmallInteger value that can be tagged.
    /// </summary>
    public const long MinSmallInteger = long.MinValue >> TagShift;

    /// <summary>
    /// Maximum SmallInteger value that can be tagged.
    /// </summary>
    public const long MaxSmallInteger = long.MaxValue >> TagShift;

    /// <summary>
    /// Maximum Character value that can be encoded.
    /// </summary>
    public const int MaxCharacter = int.MaxValue >> TagShift;

    #region SmallInteger Operations

    /// <summary>
    /// Creates a tagged SmallInteger from a value.
    /// </summary>
    /// <param name="value">The integer value to tag.</param>
    /// <returns>A tagged SmallInteger immediate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromSmallInteger(long value) 
        => (value << TagShift) | SmallIntTag;

    /// <summary>
    /// Extracts the SmallInteger value from a tagged immediate.
    /// </summary>
    /// <param name="tagged">The tagged SmallInteger.</param>
    /// <returns>The untagged integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToSmallInteger(object tagged) 
        => ((long)tagged) >> TagShift;

    /// <summary>
    /// Checks if a value is a tagged SmallInteger.
    /// </summary>
    /// <param name="v">The value to check.</param>
    /// <returns>True if the value is a SmallInteger; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmallInteger(object v) 
        => v is long lv && (lv & TagMask) == SmallIntTag;

    /// <summary>
    /// Tries to unbox a SmallInteger for hot interpreter paths.
    /// </summary>
    /// <param name="v">The value to unbox.</param>
    /// <param name="value">The unboxed integer value if successful.</param>
    /// <returns>True if the value was a SmallInteger; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnboxSmallInteger(object v, out long value)
    {
        if (v is long lv && (lv & TagMask) == SmallIntTag)
        {
            value = lv >> TagShift;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Tags an integer value as a SmallInteger immediate with range checking.
    /// </summary>
    /// <param name="value">The value to tag.</param>
    /// <returns>A tagged SmallInteger.</returns>
    /// <exception cref="OverflowException">Thrown when the value is out of SmallInteger range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TagInt(long value)
    {
        if (value < MinSmallInteger || value > MaxSmallInteger)
            throw new OverflowException($"Value {value} is out of SmallInteger range [{MinSmallInteger}, {MaxSmallInteger}]");
        
        return FromSmallInteger(value);
    }

    /// <summary>
    /// Tags an integer value without range checking (for performance).
    /// </summary>
    /// <param name="value">The value to tag.</param>
    /// <returns>A tagged SmallInteger.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TagIntUnchecked(long value) 
        => FromSmallInteger(value);

    /// <summary>
    /// Untags a SmallInteger immediate to get its value.
    /// </summary>
    /// <param name="tagged">The tagged SmallInteger.</param>
    /// <returns>The untagged integer value.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a SmallInteger.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UntagInt(object tagged)
    {
        if (!IsSmallInteger(tagged))
            throw new ArgumentException("Object is not a tagged SmallInteger");
        
        return ToSmallInteger(tagged);
    }

    /// <summary>
    /// Untags a SmallInteger immediate without checking (for performance).
    /// </summary>
    /// <param name="tagged">The tagged SmallInteger.</param>
    /// <returns>The untagged integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UntagIntUnchecked(object tagged) 
        => ToSmallInteger(tagged);

    #endregion

    #region Character Operations

    /// <summary>
    /// Creates a tagged Character from a value.
    /// </summary>
    /// <param name="value">The character code to tag.</param>
    /// <returns>A tagged Character immediate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromCharacter(int value) 
        => ((long)value << TagShift) | CharacterTag;

    /// <summary>
    /// Creates a tagged Character from a char.
    /// </summary>
    /// <param name="c">The character to tag.</param>
    /// <returns>A tagged Character immediate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromCharacter(char c) 
        => FromCharacter((int)c);

    /// <summary>
    /// Extracts the Character value from a tagged immediate.
    /// </summary>
    /// <param name="tagged">The tagged Character.</param>
    /// <returns>The untagged character code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToCharacter(object tagged) 
        => (int)(((long)tagged) >> TagShift);

    /// <summary>
    /// Extracts the Character as a char from a tagged immediate.
    /// </summary>
    /// <param name="tagged">The tagged Character.</param>
    /// <returns>The untagged character.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static char ToChar(object tagged) 
        => (char)ToCharacter(tagged);

    /// <summary>
    /// Checks if a value is a tagged Character.
    /// </summary>
    /// <param name="v">The value to check.</param>
    /// <returns>True if the value is a Character; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCharacter(object v) 
        => v is long lv && (lv & TagMask) == CharacterTag;

    /// <summary>
    /// Tries to unbox a Character for hot interpreter paths.
    /// </summary>
    /// <param name="v">The value to unbox.</param>
    /// <param name="value">The unboxed character code if successful.</param>
    /// <returns>True if the value was a Character; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnboxCharacter(object v, out int value)
    {
        if (v is long lv && (lv & TagMask) == CharacterTag)
        {
            value = (int)(lv >> TagShift);
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Tags a character code as a Character immediate with range checking.
    /// </summary>
    /// <param name="value">The character code to tag.</param>
    /// <returns>A tagged Character.</returns>
    /// <exception cref="OverflowException">Thrown when the value is out of Character range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TagChar(int value)
    {
        if (value < 0 || value > MaxCharacter)
            throw new OverflowException($"Value {value} is out of Character range [0, {MaxCharacter}]");
        
        return FromCharacter(value);
    }

    /// <summary>
    /// Tags a char as a Character immediate.
    /// </summary>
    /// <param name="c">The character to tag.</param>
    /// <returns>A tagged Character.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object TagChar(char c) 
        => FromCharacter(c);

    /// <summary>
    /// Untags a Character immediate to get its value.
    /// </summary>
    /// <param name="tagged">The tagged Character.</param>
    /// <returns>The untagged character code.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a Character.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UntagChar(object tagged)
    {
        if (!IsCharacter(tagged))
            throw new ArgumentException("Object is not a tagged Character");
        
        return ToCharacter(tagged);
    }

    /// <summary>
    /// Untags a Character immediate without checking (for performance).
    /// </summary>
    /// <param name="tagged">The tagged Character.</param>
    /// <returns>The untagged character code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UntagCharUnchecked(object tagged) 
        => ToCharacter(tagged);

    #endregion

    #region General Operations

    /// <summary>
    /// Checks if a value is any tagged immediate (SmallInteger or Character).
    /// </summary>
    /// <param name="v">The value to check.</param>
    /// <returns>True if the value is a tagged immediate; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImmediate(object v) 
        => v is long lv && (lv & TagMask) != 0;

    /// <summary>
    /// Checks if a value is an object reference (not immediate).
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is an object reference; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsObject(object value) 
        => value is not long || ((long)value & TagMask) == 0;

    /// <summary>
    /// Gets the class of a value, handling both immediates and objects.
    /// </summary>
    /// <param name="value">The value to get the class of.</param>
    /// <param name="kernel">The kernel containing class references.</param>
    /// <returns>The class of the value.</returns>
    /// <exception cref="ArgumentException">Thrown when the value type is unknown.</exception>
    public static SClass ClassOf(object value, Kernel kernel)
    {
        if (value == null || value == Nil.Instance)
            return kernel.UndefinedObjectClass;
        
        if (value == True.Instance)
            return kernel.TrueClass;
        
        if (value == False.Instance)
            return kernel.FalseClass;
        
        if (IsSmallInteger(value))
            return kernel.SmallIntegerClass;
        
        if (IsCharacter(value))
            return kernel.CharacterClass;
        
        if (value is SObject sobj)
            return sobj.Klass;
        
        throw new ArgumentException($"Unknown value type: {value.GetType()}");
    }

    /// <summary>
    /// Checks if two values are identical (same object or same immediate).
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True if the values are identical; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreIdentical(object left, object right)
    {
        // For immediates, compare the tagged values directly
        if (left is long lv1 && right is long lv2)
            return lv1 == lv2;
        
        // For objects, compare references
        return ReferenceEquals(left, right);
    }

    #endregion

    #region SmallInteger Arithmetic with Overflow Detection

    /// <summary>
    /// Performs SmallInteger addition with overflow checking.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if overflow occurs or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? AddSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        try
        {
            checked
            {
                var result = a + b;
                if (result >= MinSmallInteger && result <= MaxSmallInteger)
                    return TagIntUnchecked(result);
            }
        }
        catch (OverflowException)
        {
            // Fall through
        }
        
        return null; // Overflow - need to create LargeInteger
    }

    /// <summary>
    /// Performs SmallInteger subtraction with overflow checking.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if overflow occurs or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? SubtractSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        try
        {
            checked
            {
                var result = a - b;
                if (result >= MinSmallInteger && result <= MaxSmallInteger)
                    return TagIntUnchecked(result);
            }
        }
        catch (OverflowException)
        {
            // Fall through
        }
        
        return null; // Overflow - need to create LargeInteger
    }

    /// <summary>
    /// Performs SmallInteger multiplication with overflow checking.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if overflow occurs or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? MultiplySmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        try
        {
            checked
            {
                var result = a * b;
                if (result >= MinSmallInteger && result <= MaxSmallInteger)
                    return TagIntUnchecked(result);
            }
        }
        catch (OverflowException)
        {
            // Fall through
        }
        
        return null; // Overflow - need to create LargeInteger
    }

    /// <summary>
    /// Performs SmallInteger division.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if not exact division, division by zero, or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? DivideSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        if (b == 0)
            return null; // Division by zero
        
        if (a % b != 0)
            return null; // Result is not an integer - need Float
        
        var result = a / b;
        if (result >= MinSmallInteger && result <= MaxSmallInteger)
            return TagIntUnchecked(result);
        
        return null; // Overflow - need to create LargeInteger
    }

    /// <summary>
    /// Performs SmallInteger integer division (quotient).
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The quotient if successful; null if division by zero or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? QuoSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        if (b == 0)
            return null; // Division by zero
        
        var result = a / b;
        if (result >= MinSmallInteger && result <= MaxSmallInteger)
            return TagIntUnchecked(result);
        
        return null; // Overflow - need to create LargeInteger
    }

    /// <summary>
    /// Performs SmallInteger modulo.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The modulo result if successful; null if division by zero or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? ModSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        if (b == 0)
            return null; // Division by zero
        
        var result = a % b;
        // Smalltalk modulo always returns non-negative result
        if (result < 0)
            result += Math.Abs(b);
        
        return TagIntUnchecked(result);
    }

    /// <summary>
    /// Performs bitwise AND on SmallIntegers.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? BitAndSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        return TagIntUnchecked(a & b);
    }

    /// <summary>
    /// Performs bitwise OR on SmallIntegers.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? BitOrSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        return TagIntUnchecked(a | b);
    }

    /// <summary>
    /// Performs bitwise XOR on SmallIntegers.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result if successful; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? BitXorSmallIntegers(object left, object right)
    {
        if (!TryUnboxSmallInteger(left, out var a) || !TryUnboxSmallInteger(right, out var b))
            return null;
        
        return TagIntUnchecked(a ^ b);
    }

    /// <summary>
    /// Performs left shift on a SmallInteger.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="shift">The shift amount.</param>
    /// <returns>The result if successful; null if overflow or operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? BitShiftSmallIntegers(object value, object shift)
    {
        if (!TryUnboxSmallInteger(value, out var v) || !TryUnboxSmallInteger(shift, out var s))
            return null;
        
        if (s < 0)
        {
            // Right shift
            return TagIntUnchecked(v >> (int)-s);
        }
        
        // Left shift with overflow check
        if (s >= 63)
            return null; // Would overflow
        
        var result = v << (int)s;
        if (result >= MinSmallInteger && result <= MaxSmallInteger)
            return TagIntUnchecked(result);
        
        return null; // Overflow
    }

    #endregion

    #region SmallInteger Comparison

    /// <summary>
    /// Compares two SmallIntegers.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>-1 if left &lt; right, 0 if equal, 1 if left &gt; right.</returns>
    /// <exception cref="ArgumentException">Thrown when operands are not SmallIntegers.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CompareSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            throw new ArgumentException("Both arguments must be SmallIntegers");
        
        // Can compare tagged values directly since they preserve ordering
        var a = (long)left;
        var b = (long)right;
        return a.CompareTo(b);
    }

    /// <summary>
    /// Checks if left is less than right.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if left &lt; right; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? LessThanSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left < (long)right;
    }

    /// <summary>
    /// Checks if left is less than or equal to right.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if left &lt;= right; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? LessOrEqualSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left <= (long)right;
    }

    /// <summary>
    /// Checks if left is greater than right.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if left &gt; right; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? GreaterThanSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left > (long)right;
    }

    /// <summary>
    /// Checks if left is greater than or equal to right.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if left &gt;= right; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? GreaterOrEqualSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left >= (long)right;
    }

    /// <summary>
    /// Checks if two SmallIntegers are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if equal; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? EqualSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left == (long)right;
    }

    /// <summary>
    /// Checks if two SmallIntegers are not equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if not equal; null if operands are not SmallIntegers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? NotEqualSmallIntegers(object left, object right)
    {
        if (!IsSmallInteger(left) || !IsSmallInteger(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left != (long)right;
    }

    #endregion

    #region Character Comparison

    /// <summary>
    /// Compares two Characters.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>-1 if left &lt; right, 0 if equal, 1 if left &gt; right.</returns>
    /// <exception cref="ArgumentException">Thrown when operands are not Characters.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CompareCharacters(object left, object right)
    {
        if (!IsCharacter(left) || !IsCharacter(right))
            throw new ArgumentException("Both arguments must be Characters");
        
        // Can compare tagged values directly since they preserve ordering
        var a = (long)left;
        var b = (long)right;
        return a.CompareTo(b);
    }

    /// <summary>
    /// Checks if two Characters are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if equal; false if not equal; null if operands are not Characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool? EqualCharacters(object left, object right)
    {
        if (!IsCharacter(left) || !IsCharacter(right))
            return null;
        
        // Can compare tagged values directly
        return (long)left == (long)right;
    }

    #endregion
}