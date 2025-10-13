namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Tagged object reference following Smalltalk-80 Blue Book specification.
/// Implements immediate SmallInteger encoding with LSB tagging.
/// LSB = 0: Object pointer (ObjectTable index), LSB = 1: SmallInteger immediate value
/// </summary>
public readonly struct SmalltalkOOP
{
    private readonly int _value;

    /// <summary>Creates a new SmalltalkOOP from a raw value.</summary>
    public SmalltalkOOP(int value) => _value = value;

    /// <summary>True if this OOP represents a SmallInteger (LSB = 1).</summary>
    public bool IsSmallInteger => (_value & 1) == 1;

    /// <summary>True if this OOP represents an object pointer (LSB = 0).</summary>
    public bool IsObjectPointer => (_value & 1) == 0;

    /// <summary>Extracts SmallInteger value (assumes IsSmallInteger = true).</summary>
    public int AsSmallInteger() => _value >> 1;

    /// <summary>Extracts object table index (assumes IsObjectPointer = true).</summary>
    public int AsObjectTableIndex() => _value >> 1;

    /// <summary>Creates a SmallInteger OOP from an integer value.</summary>
    public static SmalltalkOOP FromSmallInteger(int value) => new((value << 1) | 1);

    /// <summary>Creates an object pointer OOP from an ObjectTable index.</summary>
    public static SmalltalkOOP FromObjectTableIndex(int index) => new(index << 1);

    /// <summary>Returns the raw tagged value.</summary>
    public int RawValue => _value;

    /// <summary>Null/zero OOP constant.</summary>
    public static readonly SmalltalkOOP Null = new(0);

    public override string ToString() => IsSmallInteger 
        ? $"SmallInteger({AsSmallInteger()})" 
        : $"ObjectPointer(index={AsObjectTableIndex()})";

    public override bool Equals(object? obj) => obj is SmalltalkOOP other && _value == other._value;
    public override int GetHashCode() => _value.GetHashCode();
    
    public static bool operator ==(SmalltalkOOP left, SmalltalkOOP right) => left._value == right._value;
    public static bool operator !=(SmalltalkOOP left, SmalltalkOOP right) => left._value != right._value;

    // Implicit conversions for convenience
    public static implicit operator int(SmalltalkOOP oop) => oop._value;
    public static implicit operator SmalltalkOOP(int value) => new(value);
}