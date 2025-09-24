using Core.Memory;

namespace Core.Abstractions.Memory;

/// <summary>
/// Defines the interface for the Smalltalk object memory system.
/// </summary>
public interface IObjectMemory
{
    /// <summary>
    /// Allocates a new object with the specified class and size.
    /// </summary>
    OOP AllocateObject(OOP classOop, int size);

    /// <summary>
    /// Allocates a new byte object with the specified class and byte size.
    /// </summary>
    OOP AllocateByteObject(OOP classOop, int byteSize);

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    OOP FetchClassOf(OOP oop);

    /// <summary>
    /// Fetches a field value from an object.
    /// </summary>
    OOP FetchPointer(OOP oop, int fieldIndex);

    /// <summary>
    /// Stores a field value in an object.
    /// </summary>
    void StorePointer(OOP oop, int fieldIndex, OOP value);

    /// <summary>
    /// Gets the size of an object.
    /// </summary>
    int FetchWordLengthOf(OOP oop);

    /// <summary>
    /// Performs garbage collection.
    /// </summary>
    void CollectGarbage();

    /// <summary>
    /// Checks if an OOP is a SmallInteger.
    /// </summary>
    bool IsSmallInteger(OOP oop);

    /// <summary>
    /// Gets the value of a SmallInteger.
    /// </summary>
    int SmallIntegerValue(OOP oop);

    /// <summary>
    /// Creates a SmallInteger OOP.
    /// </summary>
    OOP SmallIntegerObjectOf(int value);

    /// <summary>
    /// Fetches a byte value from an object.
    /// </summary>
    byte FetchByte(OOP oop, int index);

    /// <summary>
    /// Stores a byte value in an object.
    /// </summary>
    void StoreByte(OOP oop, int index, byte value);
}