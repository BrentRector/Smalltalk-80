using System;

namespace Smalltalk80.Interpreter.ObjectModel;
/// <summary>
/// Object-Oriented Pointer (OOP) - fundamental pointer type in Smalltalk-80.
/// Encodes either an object reference (even) or SmallInteger (odd).
/// This is the only way to reference objects or immediate integers.
/// </summary>
/// <param name="value">The raw encoded value.</param>
public readonly record struct OOP(uint value) : IEquatable<OOP>
{
    /// <summary>
    /// The nil object OOP (always 0).
    /// </summary>
    public static readonly OOP Nil = new(0);

    /// <summary>
    /// Creates an OOP for a SmallInteger.
    /// </summary>
    public static OOP FromSmallInteger(int integerValue) =>
        new((uint)((integerValue << 1) | 1));

    /// <summary>
    /// Creates an OOP for an object at the given index.
    /// </summary>
    public static OOP FromObjectIndex(uint objectIndex) =>
        new(objectIndex << 1);

    /// <summary>
    /// Creates an OOP from a raw encoded value.
    /// </summary>
    public static OOP FromRaw(uint rawValue) => new(rawValue);

    /// <summary>
    /// Gets whether this OOP represents a SmallInteger.
    /// </summary>
    public bool IsSmallInteger => (value & 1) == 1;

    /// <summary>
    /// Gets whether this OOP represents an object pointer.
    /// </summary>
    public bool IsObjectPointer => (value & 1) == 0;

    /// <summary>
    /// Gets whether this is the nil object.
    /// </summary>
    public bool IsNil => value == 0;

    /// <summary>
    /// Gets the SmallInteger value if this is a SmallInteger.
    /// </summary>
    public int SmallIntegerValue => !IsSmallInteger
        ? throw new InvalidOperationException("OOP is not a SmallInteger")
        : (int)value >> 1;

    /// <summary>
    /// Gets the object index if this is an object pointer.
    /// </summary>
    public uint ObjectIndex => !IsObjectPointer
        ? throw new InvalidOperationException("OOP is not an object pointer")
        : value >> 1;

    /// <summary>
    /// Gets the raw encoded value.
    /// </summary>
    public uint RawValue => value;

    public bool Equals(OOP other) => value == other.value;

    public override int GetHashCode() => value.GetHashCode();

    public override string ToString() => this switch
    {
        { IsNil: true } => "nil",
        { IsSmallInteger: true } => $"SmallInt({SmallIntegerValue})",
        _ => $"OOP({ObjectIndex})"
    };
}