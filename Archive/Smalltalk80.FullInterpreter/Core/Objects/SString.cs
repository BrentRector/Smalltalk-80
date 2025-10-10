namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a Smalltalk String.
/// </summary>
public sealed class SString : SObject, IEquatable<SString>
{
    private readonly string value;
    
    /// <summary>
    /// Initializes a new SString with the specified value.
    /// </summary>
    public SString(string value)
    {
        this.value = value ?? string.Empty;
    }
    
    /// <summary>
    /// Gets the string value.
    /// </summary>
    public string Value => value;
    
    /// <summary>
    /// Gets the length of the string.
    /// </summary>
    public int Length => value.Length;
    
    // Class property removed - deprecated SystemDictionary.StringClass no longer exists
    
    /// <summary>
    /// Creates an SString from a .NET string.
    /// </summary>
    public static SString From(string value) => new(value);
    
    /// <summary>
    /// Gets a character at the specified index (1-based like Smalltalk).
    /// </summary>
    public Character At(int index)
    {
        if (index < 1 || index > value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "String index out of bounds");
        }
        return Character.From(value[index - 1]);
    }
    
    /// <summary>
    /// Concatenates this string with another.
    /// </summary>
    public SString Concatenate(SString other) => new(value + other.value);
    
    /// <summary>
    /// Gets a substring (1-based indices like Smalltalk).
    /// </summary>
    public SString Substring(int startIndex, int endIndex)
    {
        if (startIndex < 1 || endIndex > value.Length || startIndex > endIndex)
        {
            throw new ArgumentException("Invalid substring indices");
        }
        return new SString(value.Substring(startIndex - 1, endIndex - startIndex + 1));
    }
    
    /// <summary>
    /// Converts this string to uppercase.
    /// </summary>
    public SString ToUpper() => new(value.ToUpper());
    
    /// <summary>
    /// Converts this string to lowercase.
    /// </summary>
    public SString ToLower() => new(value.ToLower());
    
    /// <summary>
    /// Converts this SString to a display string.
    /// </summary>
    public override string ToDisplayString() => $"'{value}'";
    
    /// <summary>
    /// Gets the hash code for this SString.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether this SString equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is SString other && Equals(other);
    
    /// <summary>
    /// Determines whether this SString equals another SString.
    /// </summary>
    public bool Equals(SString? other) => other != null && value == other.value;
    
    /// <summary>
    /// Implicit conversion from string to SString.
    /// </summary>
    public static implicit operator SString(string value) => new(value);
    
    /// <summary>
    /// Implicit conversion from SString to string.
    /// </summary>
    public static implicit operator string(SString sString) => sString.value;
}