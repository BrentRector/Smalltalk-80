namespace Core.Memory;

/// <summary>
/// Object-Oriented Pointer (OOP) representation following the Blue Book specification.
/// Uses tagged pointers for SmallIntegers (odd values) and object references (even values).
/// </summary>
public readonly record struct OOP(uint Value)
{
    /// <summary>
    /// The nil object reference.
    /// </summary>
    public static readonly OOP Nil = new(0);
    
    /// <summary>
    /// The true object reference.
    /// </summary>
    public static readonly OOP True = new(2);
    
    /// <summary>
    /// The false object reference.
    /// </summary>
    public static readonly OOP False = new(4);

    /// <summary>
    /// Checks if this OOP represents a SmallInteger.
    /// SmallIntegers are tagged with the least significant bit set to 1.
    /// </summary>
    public bool IsSmallInteger => (Value & 1) == 1;

    /// <summary>
    /// Checks if this OOP represents an object pointer.
    /// Object pointers have the least significant bit set to 0.
    /// </summary>
    public bool IsPointer => (Value & 1) == 0;

    /// <summary>
    /// Checks if this OOP is nil.
    /// </summary>
    public bool IsNil => Value == 0;

    /// <summary>
    /// Gets the SmallInteger value (for compatibility).
    /// </summary>
    public int SmallIntegerValue => ToSmallInteger();

    /// <summary>
    /// Creates an OOP from a SmallInteger value.
    /// </summary>
    /// <param name="value">The integer value to encode.</param>
    /// <returns>An OOP representing the SmallInteger.</returns>
    public static OOP FromSmallInteger(int value)
    {
        // Shift left by 1 and set LSB to 1
        return new OOP((uint)((value << 1) | 1));
    }

    /// <summary>
    /// Extracts the SmallInteger value from this OOP.
    /// </summary>
    /// <returns>The integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this OOP is not a SmallInteger.</exception>
    public int ToSmallInteger()
    {
        if (!IsSmallInteger)
        {
            throw new InvalidOperationException("OOP does not represent a SmallInteger");
        }

        // Arithmetic shift right by 1 to preserve sign
        return (int)Value >> 1;
    }

    /// <summary>
    /// Creates an OOP from an object table index.
    /// </summary>
    /// <param name="index">The object table index.</param>
    /// <returns>An OOP representing the object reference.</returns>
    public static OOP FromPointer(uint index)
    {
        // Shift left by 1, LSB remains 0
        return new OOP(index << 1);
    }

    /// <summary>
    /// Extracts the object table index from this OOP.
    /// </summary>
    /// <returns>The object table index.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this OOP is not a pointer.</exception>
    public uint ToPointer()
    {
        if (!IsPointer)
        {
            throw new InvalidOperationException("OOP does not represent a pointer");
        }

        // Shift right by 1 to get the actual index
        return Value >> 1;
    }

    /// <summary>
    /// Checks if the SmallInteger value fits within the tagged representation.
    /// </summary>
    /// <param name="value">The integer value to check.</param>
    /// <returns>True if the value can be represented as a SmallInteger.</returns>
    public static bool CanBeSmallInteger(long value)
    {
        // Check if value fits in 31 bits (since we use 1 bit for tagging)
        const long MinSmallInteger = -(1L << 30);
        const long MaxSmallInteger = (1L << 30) - 1;
        return value >= MinSmallInteger && value <= MaxSmallInteger;
    }

    /// <summary>
    /// Implicit conversion from uint to OOP.
    /// </summary>
    public static implicit operator OOP(uint value) => new(value);

    /// <summary>
    /// Implicit conversion from OOP to uint.
    /// </summary>
    public static implicit operator uint(OOP oop) => oop.Value;

    /// <summary>
    /// Returns a string representation of this OOP.
    /// </summary>
    public override string ToString() => IsSmallInteger 
        ? $"SmallInt({ToSmallInteger()})" 
        : IsNil 
            ? "nil" 
            : $"OOP(0x{Value:X8})";
}