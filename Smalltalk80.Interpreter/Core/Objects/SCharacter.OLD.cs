namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a Smalltalk-80 Character object.
///
/// Per Blue Book (Smalltalk-80.st line 7741):
///   Magnitude subclass: #Character
///     instanceVariableNames: 'value'
///     classVariableNames: 'CharacterTable'
///
/// Characters are NOT immediate values in classic Smalltalk-80.
/// They are full objects with one instance variable storing the ASCII value (0-255).
/// All 256 possible Character instances are pre-created and stored in CharacterTable.
/// </summary>
public sealed class SCharacter : SInstance, IEquatable<SCharacter>, IComparable<SCharacter>
{
    // Class variable: CharacterTable stores all 256 unique Character instances
    private static SCharacter[]? characterTable;

    /// <summary>
    /// Private constructor - Characters are created only through InitializeCharacterTable().
    /// </summary>
    private SCharacter(int asciiValue)
        : base(SystemDictionary.CharacterClass, instanceVarCount: 1)
    {
        // Store ASCII value (0-255) as SmallInteger in instance variable[0]
        SetInstanceVariable(0, SObjectRef.FromSmallInteger(asciiValue));
    }

    /// <summary>
    /// Gets the ASCII value of this Character (from instance variable 'value').
    /// </summary>
    public int AsciiValue
    {
        get
        {
            var valueRef = GetInstanceVariable(0);
            if (!valueRef.IsSmallInteger)
                throw new InvalidOperationException("Character value must be SmallInteger");
            return valueRef.AsSmallInteger();
        }
    }

    /// <summary>
    /// Gets the char representation of this Character.
    /// </summary>
    public char CharValue => (char)AsciiValue;

    /// <summary>
    /// Initializes the CharacterTable with all 256 Character instances.
    /// Must be called during cold start after Character class is created.
    /// </summary>
    public static void InitializeCharacterTable()
    {
        if (characterTable != null)
            throw new InvalidOperationException("CharacterTable already initialized");

        characterTable = new SCharacter[256];

        for (int i = 0; i < 256; i++)
        {
            characterTable[i] = new SCharacter(i);
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[SCharacter] CharacterTable initialized with 256 Character instances");
        }
    }

    /// <summary>
    /// Gets the unique Character instance for the given ASCII value.
    /// Per Blue Book: "The instances of this class are created uniquely"
    /// </summary>
    /// <param name="asciiValue">ASCII value (0-255)</param>
    /// <returns>The unique Character instance</returns>
    public static SCharacter Value(int asciiValue)
    {
        if (characterTable == null)
            throw new InvalidOperationException("CharacterTable not initialized. Call InitializeCharacterTable() first.");

        if (asciiValue < 0 || asciiValue > 255)
            throw new ArgumentOutOfRangeException(nameof(asciiValue), "Character value must be 0-255");

        return characterTable[asciiValue];
    }

    /// <summary>
    /// Creates a Character from a char value.
    /// </summary>
    public static SCharacter From(char ch)
    {
        if (ch > 255)
            throw new ArgumentException("Character must be in ASCII range (0-255)");

        return Value((int)ch);
    }

    #region Character Testing (per Blue Book Character protocol)

    /// <summary>
    /// Returns true if this is a digit character ($0-$9).
    /// </summary>
    public bool IsDigit => CharValue >= '0' && CharValue <= '9';

    /// <summary>
    /// Returns true if this is a letter character ($A-$Z or $a-$z).
    /// Per Smalltalk-80.st: uses octal ranges for efficiency
    /// </summary>
    public bool IsLetter
    {
        get
        {
            int val = AsciiValue;
            // Lowercase: 8r141 to 8r172 (97-122 in decimal, 'a'-'z')
            // Uppercase: 8r101 to 8r132 (65-90 in decimal, 'A'-'Z')
            return (val >= 0x61 && val <= 0x7A) || (val >= 0x41 && val <= 0x5A);
        }
    }

    /// <summary>
    /// Returns true if this is a separator character (space, CR, tab, LF, form feed).
    /// Per Smalltalk-80.st line 7796-7805
    /// </summary>
    public bool IsSeparator
    {
        get
        {
            int val = AsciiValue;
            return val == 32 ||  // space
                   val == 13 ||  // CR
                   val == 9 ||   // tab
                   val == 10 ||  // LF
                   val == 12;    // form feed
        }
    }

    /// <summary>
    /// Returns true if this is an uppercase letter.
    /// </summary>
    public bool IsUppercase => CharValue >= 'A' && CharValue <= 'Z';

    /// <summary>
    /// Returns true if this is a lowercase letter.
    /// </summary>
    public bool IsLowercase => CharValue >= 'a' && CharValue <= 'z';

    /// <summary>
    /// Returns true if this is alphanumeric.
    /// </summary>
    public bool IsAlphaNumeric => IsLetter || IsDigit;

    #endregion

    #region Character Conversion

    /// <summary>
    /// Returns the uppercase version of this Character.
    /// Per Smalltalk-80.st line 7850-7856
    /// </summary>
    public SCharacter AsUppercase
    {
        get
        {
            int val = AsciiValue;
            // If lowercase (8r141 to 8r172), subtract 8r40 (32 decimal)
            if (val >= 0x61 && val <= 0x7A)
                return Value(val - 0x20);
            return this;
        }
    }

    /// <summary>
    /// Returns the lowercase version of this Character.
    /// Per Smalltalk-80.st line 7841-7847
    /// </summary>
    public SCharacter AsLowercase
    {
        get
        {
            int val = AsciiValue;
            // If uppercase (8r101 to 8r132), add 8r40 (32 decimal)
            if (val >= 0x41 && val <= 0x5A)
                return Value(val + 0x20);
            return this;
        }
    }

    #endregion

    #region Comparison (IComparable<SCharacter>)

    /// <summary>
    /// Compares this Character to another Character.
    /// </summary>
    public int CompareTo(SCharacter? other)
    {
        if (other == null) return 1;
        return AsciiValue.CompareTo(other.AsciiValue);
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(SCharacter left, SCharacter right)
        => left.AsciiValue < right.AsciiValue;

    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(SCharacter left, SCharacter right)
        => left.AsciiValue > right.AsciiValue;

    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(SCharacter left, SCharacter right)
        => left.AsciiValue <= right.AsciiValue;

    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(SCharacter left, SCharacter right)
        => left.AsciiValue >= right.AsciiValue;

    #endregion

    #region Equality (IEquatable<SCharacter>)

    /// <summary>
    /// Characters are compared by identity (==) per Blue Book.
    /// Since there's only one instance per ASCII value, == works correctly.
    /// Primitive 110 is used in Smalltalk-80.st line 7759.
    /// </summary>
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj);

    /// <summary>
    /// Character equality uses identity comparison.
    /// </summary>
    public bool Equals(SCharacter? other)
        => ReferenceEquals(this, other);

    /// <summary>
    /// Hash based on ASCII value (per Smalltalk-80.st line 7765).
    /// </summary>
    public override int GetHashCode() => AsciiValue;

    /// <summary>
    /// Equality operator - uses reference equality since Characters are unique.
    /// </summary>
    public static bool operator ==(SCharacter? left, SCharacter? right)
        => ReferenceEquals(left, right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SCharacter? left, SCharacter? right)
        => !ReferenceEquals(left, right);

    #endregion

    #region Display

    /// <summary>
    /// Returns the Smalltalk display string for this Character ($A, $B, etc.).
    /// Per Smalltalk-80.st line 7827-7829
    /// </summary>
    public override string ToDisplayString() => $"${CharValue}";

    #endregion

    #region Common Characters (class-side accessors per Blue Book)

    /// <summary>
    /// Returns the space character (ASCII 32).
    /// </summary>
    public static SCharacter Space => Value(32);

    /// <summary>
    /// Returns the carriage return character (ASCII 13).
    /// </summary>
    public static SCharacter CR => Value(13);

    /// <summary>
    /// Returns the tab character (ASCII 9).
    /// </summary>
    public static SCharacter Tab => Value(9);

    /// <summary>
    /// Returns the line feed character (ASCII 10).
    /// </summary>
    public static SCharacter LF => Value(10);

    /// <summary>
    /// Returns the backspace character (ASCII 8).
    /// </summary>
    public static SCharacter Backspace => Value(8);

    /// <summary>
    /// Returns the form feed/new page character (ASCII 12).
    /// </summary>
    public static SCharacter NewPage => Value(12);

    /// <summary>
    /// Returns the escape character (ASCII 160 per Smalltalk-80.st).
    /// </summary>
    public static SCharacter Esc => Value(160);

    #endregion
}