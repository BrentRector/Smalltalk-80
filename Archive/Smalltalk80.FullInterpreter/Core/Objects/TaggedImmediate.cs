namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Provides support for tagged immediate values (SmallInteger and Character).
/// In Smalltalk-80, immediate values are encoded directly in the pointer:
/// - Odd values represent SmallIntegers (value = (ptr >> 1))
/// - Even non-zero values with specific tag represent Characters
/// </summary>
public static class TaggedImmediate
{
    private const int SmallIntegerTag = 1;  // Odd bit set
    private const int CharacterTag = 2;     // Even with bit 1 set
    
    /// <summary>
    /// Checks if a value is a tagged SmallInteger.
    /// </summary>
    public static bool IsSmallInteger(nint value) => (value & 1) == SmallIntegerTag;
    
    /// <summary>
    /// Checks if a value is a tagged Character.
    /// </summary>
    public static bool IsCharacter(nint value) => value != 0 && (value & 3) == CharacterTag;
    
    /// <summary>
    /// Checks if a value is any tagged immediate.
    /// </summary>
    public static bool IsImmediate(nint value) => IsSmallInteger(value) || IsCharacter(value);
    
    /// <summary>
    /// Tags an integer as a SmallInteger.
    /// </summary>
    public static nint TagSmallInteger(int value)
    {
        // Check for overflow
        if (value < SmallInteger.MinValue || value > SmallInteger.MaxValue)
        {
            throw new OverflowException($"Value {value} is out of SmallInteger range");
        }
        
        return (nint)((value << 1) | SmallIntegerTag);
    }
    
    /// <summary>
    /// Untags a SmallInteger to get its value.
    /// </summary>
    public static int UntagSmallInteger(nint tagged)
    {
        if (!IsSmallInteger(tagged))
        {
            throw new ArgumentException("Not a tagged SmallInteger");
        }
        
        return (int)(tagged >> 1);
    }
    
    /// <summary>
    /// Tags a character code as a Character.
    /// </summary>
    public static nint TagCharacter(int charCode)
    {
        if (charCode < 0 || charCode > 255)
        {
            throw new ArgumentException("Character code must be 0-255");
        }
        
        return (nint)((charCode << 2) | CharacterTag);
    }
    
    /// <summary>
    /// Untags a Character to get its code.
    /// </summary>
    public static int UntagCharacter(nint tagged)
    {
        if (!IsCharacter(tagged))
        {
            throw new ArgumentException("Not a tagged Character");
        }
        
        return (int)(tagged >> 2);
    }
}