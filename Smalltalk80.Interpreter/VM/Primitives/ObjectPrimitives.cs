using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Object manipulation primitives (Primitives 60-79, 110-127).
/// Handles array access, object creation, identity, and class operations.
/// </summary>
public static class ObjectPrimitives
{
    // Primitive 60: Array at:
    public static SObjectRef? At(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;
        if (!args[0].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null; // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromObjectPointer(value);
        }
        catch
        {
            return null;
        }
    }

    // Primitive 61: Array at:put:
    public static SObjectRef? AtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2) return null;
        if (!args[0].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null;

        SObjectRef value = args[1];

        try
        {
            // Convert to 0-based index
            int valueAsInt = value.IsSmallInteger ? (int)value.AsSmallInteger() : (int)value.AsObjectPointer();
            ctx.Heap.SetIndexedField((int)receiver.AsObjectPointer(), index - 1, valueAsInt);
            return value;
        }
        catch
        {
            return null;
        }
    }

    // Primitive 62: Object size
    public static SObjectRef? Size(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;

        if (receiver.IsSmallInteger)
            return null; // SmallIntegers don't have a size

        int indexedSize = ctx.Heap.GetIndexedSize((int)receiver.AsObjectPointer());
        return SObjectRef.FromSmallInteger(indexedSize);
    }

    // Primitive 70: Object new
    public static SObjectRef? New(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;

        // Receiver is the class
        // Need to allocate new instance with appropriate size
        // TODO: Implement when we have class metadata access
        return null;
    }

    // Primitive 71: Object new:
    public static SObjectRef? NewWithSize(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;
        if (!args[0].IsSmallInteger) return null;

        int size = args[0].AsSmallInteger();
        if (size < 0) return null;

        // Allocate indexed object of given size
        // TODO: Implement when we have proper class allocation
        return null;
    }

    // Primitive 110: Object ==
    public static SObjectRef? IdentityEquals(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;

        return receiver == args[0]
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 111: Object class
    public static SObjectRef? Class(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;

        try
        {
            int classId = ctx.Heap.GetClass((int)receiver.AsObjectPointer());
            return SObjectRef.FromObjectPointer(classId);
        }
        catch
        {
            return null;
        }
    }

    // Primitive 70-77: Instance variable access
    // These are optimized for getting/setting instance variables

    public static SObjectRef? InstVarAt(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;
        if (!args[0].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null;

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromObjectPointer(value);
        }
        catch
        {
            return null;
        }
    }

    public static SObjectRef? InstVarAtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2) return null;
        if (!args[0].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null;

        SObjectRef value = args[1];

        try
        {
            // Convert to 0-based index
            int valueAsInt = value.IsSmallInteger ? (int)value.AsSmallInteger() : (int)value.AsObjectPointer();
            ctx.Heap.SetField((int)receiver.AsObjectPointer(), index - 1, valueAsInt);
            return value;
        }
        catch
        {
            return null;
        }
    }

    // Primitive 63: String at: (character access for byte-indexed objects)
    public static SObjectRef? StringAt(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;
        if (!args[0].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null; // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromSmallInteger(value & 0xFF); // Ensure byte range
        }
        catch
        {
            return null;
        }
    }

    // Primitive 64: String at:put: (character store for byte-indexed objects)
    public static SObjectRef? StringAtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2) return null;
        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger) return null;

        int index = args[0].AsSmallInteger();
        if (index < 1) return null;

        int charValue = args[1].AsSmallInteger();
        if (charValue < 0 || charValue > 255) return null;

        try
        {
            // Convert to 0-based index
            ctx.Heap.SetIndexedField((int)receiver.AsObjectPointer(), index - 1, charValue);
            return args[1];
        }
        catch
        {
            return null;
        }
    }

    // Primitive 65: Stream next (stub - requires Stream support)
    public static SObjectRef? Next(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;
        // TODO: Implement when Stream support is added
        return null;
    }

    // Primitive 66: Stream nextPut: (stub - requires Stream support)
    public static SObjectRef? NextPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;
        // TODO: Implement when Stream support is added
        return null;
    }

    // Primitive 67: Stream atEnd (stub - requires Stream support)
    public static SObjectRef? AtEnd(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;
        // TODO: Implement when Stream support is added
        return null;
    }

    // Primitive 68: Object become: (object swapping - stub)
    public static SObjectRef? Become(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1) return null;

        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
            return null;

        // TODO: Implement object swapping in heap
        // This requires deep memory manipulation
        return null;
    }

    // Primitive 75: Object hash
    public static SObjectRef? Hash(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0) return null;

        // Use object ID as hash for heap objects
        // For SmallIntegers, use the value itself
        if (receiver.IsSmallInteger)
        {
            return receiver;
        }
        else
        {
            // Object ID is already a good hash
            int oop = (int)receiver.AsObjectPointer();
            int hash = oop >> 1; // Remove pointer tag
            return SObjectRef.FromSmallInteger(hash & 0x1FFFFFFF);
        }
    }
}
