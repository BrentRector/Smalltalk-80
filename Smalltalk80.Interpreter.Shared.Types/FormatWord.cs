namespace Smalltalk80.Interpreter.Shared.Types;

/// <summary>
/// Semantic wrapper for Smalltalk-80 class format word encoding.
///
/// Format word bit layout (differs from Blue Book Chapter 27, matches Smalltalk-80.st source):
///   Bits 0-7:   fixedFields (number of fixed instance variables, 0-255)
///   Bits 8-11:  reserved
///   Bit 12:     isIndexable (1=has indexed fields, 0=fixed size only)
///   Bit 13:     isWords     (1=word-addressed, 0=byte-addressed)
///   Bit 14:     isPointers  (1=contains pointers, 0=contains values)
///
/// Type flag combinations:
///   0x4000 = Fixed pointer object (subclass:)
///   0x5000 = Pointer-indexed (variableSubclass:)
///   0x3000 = Word-indexed (variableWordSubclass:)
///   0x1000 = Byte-indexed (variableByteSubclass:)
///
/// Smalltalk source expectations:
///   instSize:    ^format bitAnd: 255
///   isBits:      ^format noMask: 16384   (bit 14 = 0)
///   isBytes:     ^format noMask: 8192    (bit 13 = 0)
///   isVariable:  ^(format bitAnd: 4096) ~= 0
/// </summary>
public readonly struct FormatWord
{
    private readonly int _value;

    // Bit masks
    private const int FixedFieldsMask = 0xFF;      // Bits 0-7
    private const int IsIndexableBit = 0x1000;     // Bit 12
    private const int IsWordsBit = 0x2000;         // Bit 13
    private const int IsPointersBit = 0x4000;      // Bit 14

    // Common format type flags
    public const int FixedPointerFlags = 0x4000;           // subclass:
    public const int PointerIndexedFlags = 0x5000;         // variableSubclass:
    public const int WordIndexedFlags = 0x3000;            // variableWordSubclass:
    public const int ByteIndexedFlags = 0x1000;            // variableByteSubclass:

    /// <summary>
    /// Creates a FormatWord from a raw integer value.
    /// </summary>
    public FormatWord(int value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the raw integer value for storage in Smalltalk objects.
    /// </summary>
    public int Value => _value;

    /// <summary>
    /// Gets the number of fixed instance variables (bits 0-7).
    /// Equivalent to Smalltalk: format bitAnd: 255
    /// </summary>
    public int FixedFields => _value & FixedFieldsMask;

    /// <summary>
    /// Gets whether the object has indexed fields (bit 12).
    /// Equivalent to Smalltalk: (format bitAnd: 4096) ~= 0
    /// </summary>
    public bool IsIndexable => (_value & IsIndexableBit) != 0;

    /// <summary>
    /// Gets whether indexed fields are word-addressed (bit 13).
    /// Only meaningful when IsIndexable is true.
    /// </summary>
    public bool IsWords => (_value & IsWordsBit) != 0;

    /// <summary>
    /// Gets whether the object contains pointers (bit 14).
    /// Equivalent to Smalltalk: format noMask: 16384 (returns false when bit is set)
    /// </summary>
    public bool IsPointers => (_value & IsPointersBit) != 0;

    /// <summary>
    /// Gets whether the object contains bits (non-pointer data).
    /// Equivalent to Smalltalk: format noMask: 16384
    /// </summary>
    public bool IsBits => !IsPointers;

    /// <summary>
    /// Gets whether indexed fields are byte-addressed.
    /// Equivalent to Smalltalk: format noMask: 8192
    /// Only meaningful when IsIndexable is true and IsBits is true.
    /// </summary>
    public bool IsBytes => !IsWords;

    /// <summary>
    /// Creates a FormatWord for a fixed pointer object (subclass:).
    /// </summary>
    public static FormatWord CreateFixedPointer(int fixedFieldCount)
    {
        ValidateFixedFieldCount(fixedFieldCount);
        return new FormatWord(FixedPointerFlags | fixedFieldCount);
    }

    /// <summary>
    /// Creates a FormatWord for a pointer-indexed object (variableSubclass:).
    /// </summary>
    public static FormatWord CreatePointerIndexed(int fixedFieldCount)
    {
        ValidateFixedFieldCount(fixedFieldCount);
        return new FormatWord(PointerIndexedFlags | fixedFieldCount);
    }

    /// <summary>
    /// Creates a FormatWord for a word-indexed object (variableWordSubclass:).
    /// </summary>
    public static FormatWord CreateWordIndexed(int fixedFieldCount)
    {
        ValidateFixedFieldCount(fixedFieldCount);
        return new FormatWord(WordIndexedFlags | fixedFieldCount);
    }

    /// <summary>
    /// Creates a FormatWord for a byte-indexed object (variableByteSubclass:).
    /// </summary>
    public static FormatWord CreateByteIndexed(int fixedFieldCount)
    {
        ValidateFixedFieldCount(fixedFieldCount);
        return new FormatWord(ByteIndexedFlags | fixedFieldCount);
    }

    /// <summary>
    /// Creates a FormatWord from a subclass kind string.
    /// </summary>
    public static FormatWord FromSubclassKind(string subclassKind, int fixedFieldCount)
    {
        return subclassKind switch
        {
            "subclass:" => CreateFixedPointer(fixedFieldCount),
            "variableSubclass:" => CreatePointerIndexed(fixedFieldCount),
            "variableWordSubclass:" => CreateWordIndexed(fixedFieldCount),
            "variableByteSubclass:" => CreateByteIndexed(fixedFieldCount),
            _ => CreateFixedPointer(fixedFieldCount) // Default fallback
        };
    }

    private static void ValidateFixedFieldCount(int count)
    {
        if (count < 0 || count > 255)
            throw new ArgumentOutOfRangeException(nameof(count),
                $"Fixed field count must be 0-255, got {count}");
    }

    public override string ToString()
    {
        return $"FormatWord(0x{_value:X4}: fields={FixedFields}, " +
               $"indexable={IsIndexable}, words={IsWords}, pointers={IsPointers})";
    }

    // Implicit conversions for convenience
    public static implicit operator int(FormatWord format) => format._value;
    public static implicit operator FormatWord(int value) => new FormatWord(value);
}
