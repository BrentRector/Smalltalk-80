using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Context provided to primitive methods for accessing VM services.
/// </summary>
public interface IPrimitiveContext
{
    /// <summary>
    /// Gets a special object by SOA index.
    /// </summary>
    SObjectRef GetSpecialObject(int index);

    /// <summary>
    /// Gets the class reference for a given class name.
    /// </summary>
    SObjectRef GetClass(string className);

    /// <summary>
    /// Creates a new instance of a class.
    /// </summary>
    SObjectRef NewInstance(SObjectRef classRef, int indexedSize);

    /// <summary>
    /// Performs a message send (for primitives that need to invoke methods).
    /// </summary>
    SObjectRef Send(SObjectRef receiver, string selector, SObjectRef[] arguments);

    /// <summary>
    /// Interns a symbol string and returns its reference.
    /// </summary>
    SObjectRef InternSymbol(string value);

    /// <summary>
    /// Returns true if the argument is a SmallInteger.
    /// </summary>
    bool IsSmallInteger(SObjectRef objRef);

    /// <summary>
    /// Returns true if the argument is a heap object.
    /// </summary>
    bool IsHeapObject(SObjectRef objRef);

    /// <summary>
    /// Gets the indexed size of an object (0 for non-indexed objects).
    /// </summary>
    int GetIndexedSize(nint oop);

    /// <summary>
    /// Gets an indexed pointer field from an object.
    /// </summary>
    SObjectRef GetIndexedPointer(nint oop, int index);

    /// <summary>
    /// Sets an indexed pointer field in an object.
    /// </summary>
    void SetIndexedPointer(nint oop, int index, SObjectRef value);

    /// <summary>
    /// Gets an instance variable from an object.
    /// </summary>
    SObjectRef GetInstanceVariable(nint oop, int index);

    /// <summary>
    /// Sets an instance variable in an object.
    /// </summary>
    void SetInstanceVariable(nint oop, int index, SObjectRef value);

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    SObjectRef GetObjectClass(nint oop);

    /// <summary>
    /// Allocates a new object with the given class and size.
    /// </summary>
    nint AllocateObject(SObjectRef classRef, int instanceVarCount, int indexedSize);

    /// <summary>
    /// Gets indexed byte data from an object (for byte arrays/bitmaps).
    /// Returns null if the object doesn't contain byte data.
    /// </summary>
    Memory<byte>? GetIndexedBytes(nint oop);

    /// <summary>
    /// Looks up a global variable by name.
    /// </summary>
    SObjectRef? LookupGlobal(string name);
}
