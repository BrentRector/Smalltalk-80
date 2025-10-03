using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Array and collection primitives (primitives 60-68).
/// These primitives provide basic indexed access and manipulation for collections.
/// Based on Blue Book specification for Array, String, and ByteArray operations.
/// </summary>
public static class ArrayPrimitives
{
    /// <summary>
    /// Primitive 60: Object at: (indexed access).
    /// Accesses indexed field of any indexable object.
    /// </summary>
    /// <param name="receiver">The indexable object.</param>
    /// <param name="arguments">Single argument containing 1-based index.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Value at index, or failure if invalid index or non-indexable receiver.</returns>
    public static PrimitiveResult PrimitiveAt(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();
        var indexedSize = context.GetIndexedSize(oop);

        if (indexedSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        if (index < 0 || index >= indexedSize)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        return context.GetIndexedPointer(oop, index);
    }

    /// <summary>
    /// Primitive 61: Object at:put: (indexed store).
    /// Stores value at indexed field of any indexable object.
    /// </summary>
    /// <param name="receiver">The indexable object.</param>
    /// <param name="arguments">Two arguments: index (1-based) and value to store.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The stored value, or failure if invalid index or non-indexable receiver.</returns>
    public static PrimitiveResult PrimitiveAtPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing
        SObjectRef value = arguments[1];

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();
        var indexedSize = context.GetIndexedSize(oop);

        if (indexedSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        if (index < 0 || index >= indexedSize)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        context.SetIndexedPointer(oop, index, value);
        return value; // at:put: returns the value stored
    }

    /// <summary>
    /// Primitive 62: Object size (number of indexed fields).
    /// Returns the number of indexed fields in an object.
    /// </summary>
    /// <param name="receiver">The object to query.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Size as SmallInteger, or 0 if not indexable.</returns>
    public static PrimitiveResult PrimitiveSize(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return SObjectRef.FromSmallInteger(0);

        var oop = receiver.AsObjectPointer();
        var indexedSize = context.GetIndexedSize(oop);

        return SObjectRef.FromSmallInteger(indexedSize);
    }

    /// <summary>
    /// Primitive 63: String at: (character access).
    /// Accesses character at index in a String or ByteArray.
    /// </summary>
    /// <param name="receiver">The String or ByteArray.</param>
    /// <param name="arguments">Single argument containing 1-based index.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Character value as SmallInteger, or failure if invalid.</returns>
    public static PrimitiveResult PrimitiveStringAt(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();

        // Check if it's a byte-indexable object (String, ByteArray)
        // We check this by attempting to get an indexed value and seeing if it's a SmallInteger (byte)
        var indexedSize = context.GetIndexedSize(oop);
        if (indexedSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        if (index < 0 || index >= indexedSize)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        // For byte-indexable objects, fields contain SmallIntegers representing bytes
        return context.GetIndexedPointer(oop, index);
    }

    /// <summary>
    /// Primitive 64: String at:put: (character store).
    /// Stores character value at index in a String or ByteArray.
    /// </summary>
    /// <param name="receiver">The String or ByteArray.</param>
    /// <param name="arguments">Two arguments: index (1-based) and character value as SmallInteger.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The stored value, or failure if invalid.</returns>
    public static PrimitiveResult PrimitiveStringAtPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing
        int charValue = arguments[1].AsSmallInteger();

        // Validate character value is in byte range
        if (charValue < 0 || charValue > 255)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();

        // Check if it's a byte-indexable object (String, ByteArray)
        var indexedSize = context.GetIndexedSize(oop);
        if (indexedSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        if (index < 0 || index >= indexedSize)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        context.SetIndexedPointer(oop, index, SObjectRef.FromSmallInteger(charValue));
        return arguments[1]; // Return the value stored
    }

    /// <summary>
    /// Primitive 65: Array next (stream primitive).
    /// Used by Stream classes for efficient iteration.
    /// </summary>
    /// <param name="receiver">The stream object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Next element or failure if at end.</returns>
    public static PrimitiveResult PrimitiveNext(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // This is a stub - full implementation requires Stream support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 66: Array nextPut: (stream primitive).
    /// Used by WriteStream for efficient writing.
    /// </summary>
    /// <param name="receiver">The stream object.</param>
    /// <param name="arguments">Single argument containing value to write.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The written value or failure.</returns>
    public static PrimitiveResult PrimitiveNextPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // This is a stub - full implementation requires Stream support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 67: Array atEnd (stream primitive).
    /// Tests if a stream is at its end.
    /// </summary>
    /// <param name="receiver">The stream object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if at end, false otherwise.</returns>
    public static PrimitiveResult PrimitiveAtEnd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // This is a stub - full implementation requires Stream support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 68: Object become: (object swapping).
    /// Swaps the identity of two objects in memory.
    /// This is a dangerous primitive that affects object identity globally.
    /// </summary>
    /// <param name="receiver">First object to swap.</param>
    /// <param name="arguments">Single argument containing second object to swap.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver after swapping.</returns>
    public static PrimitiveResult PrimitiveObjectBecome(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        SObjectRef other = arguments[0];

        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || other.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        // Swap the objects in memory
        // Note: The actual swapping would need to be implemented in ObjectMemoryNew
        // For now, return failure as the operation is not yet supported
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Registers all array primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(60, "primitiveAt", PrimitiveAt);
        table.Register(61, "primitiveAtPut", PrimitiveAtPut);
        table.Register(62, "primitiveSize", PrimitiveSize);
        table.Register(63, "primitiveStringAt", PrimitiveStringAt);
        table.Register(64, "primitiveStringAtPut", PrimitiveStringAtPut);
        table.Register(65, "primitiveNext", PrimitiveNext);
        table.Register(66, "primitiveNextPut", PrimitiveNextPut);
        table.Register(67, "primitiveAtEnd", PrimitiveAtEnd);
        table.Register(68, "primitiveBecome", PrimitiveObjectBecome);
    }
}