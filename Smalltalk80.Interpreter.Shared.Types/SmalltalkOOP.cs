namespace Smalltalk80.Interpreter.Shared.Types;

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

    /// <summary>
    /// Extracts SmallInteger value. Throws InvalidOperationException if this OOP is not a SmallInteger.
    /// IMPORTANT: This is the ONLY way to convert an OOP to an integer. Never use RawValue >> 1 directly.
    /// </summary>
    public int AsSmallInteger()
    {
        if (!IsSmallInteger)
            throw new InvalidOperationException($"Cannot convert non-SmallInteger OOP to integer. OOP value: {_value:X8}, IsObjectPointer: {IsObjectPointer}");
        return _value >> 1;
    }

    /// <summary>
    /// Extracts object table index from an object pointer OOP. Throws if OOP is a SmallInteger.
    /// IMPORTANT: Only use this for object pointers, never for SmallIntegers.
    /// INTERNAL USE ONLY: This method exposes heap manager implementation details.
    /// External code should treat the heap manager as a black box.
    /// </summary>
    public int ToObjectTableIndex()
    {
        if (IsSmallInteger)
            throw new InvalidOperationException($"Cannot extract object table index from SmallInteger OOP. SmallInteger value: {_value >> 1}");
        return _value >> 1;
    }

    /// <summary>Creates a SmallInteger OOP from an integer value.</summary>
    public static SmalltalkOOP FromSmallInteger(int value) => new((value << 1) | 1);

    /// <summary>
    /// Creates an object pointer OOP from an ObjectTable index.
    /// INTERNAL USE ONLY: This method exposes heap manager implementation details.
    /// External code should treat the heap manager as a black box and use heap manager allocation APIs.
    /// </summary>
    public static SmalltalkOOP FromObjectTableIndex(int index) => new(index << 1);

    /// <summary>Returns the raw tagged value.</summary>
    public int RawValue => _value;

    /// <summary>Null/zero OOP constant.</summary>
    public static readonly SmalltalkOOP Null = new(0);

    public override string ToString() => IsSmallInteger
        ? $"SmallInteger({AsSmallInteger()})"
        : $"oop={_value}";

    public override bool Equals(object? obj) => obj is SmalltalkOOP other && _value == other._value;
    public override int GetHashCode() => _value.GetHashCode();

    public static bool operator ==(SmalltalkOOP left, SmalltalkOOP right) => left._value == right._value;
    public static bool operator !=(SmalltalkOOP left, SmalltalkOOP right) => left._value != right._value;

    // Implicit conversion from int to OOP (for raw OOP values, not SmallIntegers)
    public static implicit operator SmalltalkOOP(int value) => new(value);
}
