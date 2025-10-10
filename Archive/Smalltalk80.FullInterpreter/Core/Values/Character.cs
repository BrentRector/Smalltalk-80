namespace Smalltalk80.Interpreter.Core.Values;

using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk Character (tagged immediate value).
/// </summary>
public sealed class Character : SObject, IEquatable<Character>, IComparable<Character>
{
    private readonly char value;
    
    /// <summary>
    /// Initializes a new Character with the specified value.
    /// </summary>
    public Character(char value)
    {
        if (value > 255)
        {
            throw new ArgumentException("Character must be in ASCII range (0-255)");
        }
        this.value = value;
    }
    
    /// <summary>
    /// Gets the character value.
    /// </summary>
    public char Value => value;
    
    /// <summary>
    /// Gets the ASCII code of this character.
    /// </summary>
    public int AsciiValue => value;
    
    /// <summary>
    /// Gets the class of Character.
    /// </summary>
    
    /// <summary>
    /// Gets whether this is an immediate value.
    /// </summary>
    public override bool IsImmediate => true;
    
    /// <summary>
    /// Creates a Character from a char value.
    /// </summary>
    public static Character From(char value) => new(value);
    
    /// <summary>
    /// Creates a Character from an ASCII code.
    /// </summary>
    public static Character FromAscii(int asciiCode)
    {
        if (asciiCode < 0 || asciiCode > 255)
        {
            throw new ArgumentException("ASCII code must be 0-255");
        }
        return new Character((char)asciiCode);
    }
    
    /// <summary>
    /// Checks if this character is a digit.
    /// </summary>
    public bool IsDigit => char.IsDigit(value);
    
    /// <summary>
    /// Checks if this character is a letter.
    /// </summary>
    public bool IsLetter => char.IsLetter(value);
    
    /// <summary>
    /// Checks if this character is whitespace.
    /// </summary>
    public bool IsWhitespace => char.IsWhiteSpace(value);
    
    /// <summary>
    /// Converts this character to uppercase.
    /// </summary>
    public Character ToUpper() => new(char.ToUpper(value));
    
    /// <summary>
    /// Converts this character to lowercase.
    /// </summary>
    public Character ToLower() => new(char.ToLower(value));
    
    /// <summary>
    /// Compares this Character to another.
    /// </summary>
    public int CompareTo(Character? other)
    {
        if (other == null) return 1;
        return value.CompareTo(other.value);
    }
    
    /// <summary>
    /// Converts this Character to a display string.
    /// </summary>
    public override string ToDisplayString() => $"${value}";
    
    /// <summary>
    /// Gets the hash code for this Character.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether this Character equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is Character other && Equals(other);
    
    /// <summary>
    /// Determines whether this Character equals another Character.
    /// </summary>
    public bool Equals(Character? other) => other != null && value == other.value;
    
    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Character? left, Character? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.value == right.value;
    }
    
    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Character? left, Character? right) => !(left == right);
    
    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(Character left, Character right) => left.value < right.value;
    
    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(Character left, Character right) => left.value > right.value;
    
    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(Character left, Character right) => left.value <= right.value;
    
    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(Character left, Character right) => left.value >= right.value;
}