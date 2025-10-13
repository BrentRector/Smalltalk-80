using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Object manipulation primitives (Primitives 60-79, 110-127).
/// Handles array access, object creation, identity, and class operations.
/// </summary>
public static class ObjectPrimitives
{
    // Primitive 60: Array at:
    public static SObjectRef At(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromObjectPointer(value);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 61: Array at:put:
    public static SObjectRef AtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

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
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 62: Object size
    public static SObjectRef Size(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // SmallIntegers don't have a size

        int indexedSize = ctx.Heap.GetIndexedSize((int)receiver.AsObjectPointer());
        return SObjectRef.FromSmallInteger(indexedSize);
    }

    // Primitive 70: Object new
    public static SObjectRef New(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Allocate a new instance with no indexed fields
        // The class is responsible for initialization via #initialize
        // We allocate with size 0 for fixed-size objects
        try
        {
            int newInstanceOop = ctx.AllocateObject("class:Object", 0);
            return SObjectRef.FromObjectPointer(newInstanceOop);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 71: Object new:
    public static SObjectRef NewWithSize(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int size = args[0].AsSmallInteger();
        if (size < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Allocate indexed object of given size
        // Used for Array new: n, ByteArray new: n, etc.
        try
        {
            int newInstanceOop = ctx.AllocateObject("class:Array", size);
            return SObjectRef.FromObjectPointer(newInstanceOop);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 110: Object ==
    public static SObjectRef IdentityEquals(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return receiver == args[0]
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 111: Object class
    public static SObjectRef Class(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int classId = ctx.Heap.GetClass((int)receiver.AsObjectPointer());
            return SObjectRef.FromObjectPointer(classId);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 70-77: Instance variable access
    // These are optimized for getting/setting instance variables

    public static SObjectRef InstVarAt(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromObjectPointer(value);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    public static SObjectRef InstVarAtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

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
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 63: String at: (character access for byte-indexed objects)
    public static SObjectRef StringAt(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField((int)receiver.AsObjectPointer(), index - 1);
            return SObjectRef.FromSmallInteger(value & 0xFF); // Ensure byte range
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 64: String at:put: (character store for byte-indexed objects)
    public static SObjectRef StringAtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int charValue = args[1].AsSmallInteger();
        if (charValue < 0 || charValue > 255)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Convert to 0-based index
            ctx.Heap.SetIndexedField((int)receiver.AsObjectPointer(), index - 1, charValue);
            return args[1];
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 65: Stream next - Read next element from stream
    // Blue Book: Stream has instance variables: collection, position, readLimit
    public static SObjectRef Next(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int streamOop = (int)receiver.AsObjectPointer();

            // Get Stream instance variables
            int positionEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "position");
            int readLimitEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "readLimit");
            int collectionOop = ctx.GetFieldValue(streamOop, "class:Stream", "collection");

            // Decode SmallIntegers
            int position = positionEncoded >> 1;
            int readLimit = readLimitEncoded >> 1;

            // Check if at end
            if (position >= readLimit)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Read element at position (1-indexed in Smalltalk)
            int element = ctx.Heap.GetIndexedField(collectionOop, position);

            // Increment position
            ctx.SetFieldValue(streamOop, "class:Stream", "position", (position + 1) << 1 | 1);

            return SObjectRef.FromObjectPointer(element);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 66: Stream nextPut: - Write element to stream
    public static SObjectRef NextPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int streamOop = (int)receiver.AsObjectPointer();
            SObjectRef value = args[0];

            // Get Stream instance variables
            int positionEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "position");
            int writeLimitEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "writeLimit");
            int collectionOop = ctx.GetFieldValue(streamOop, "class:Stream", "collection");

            // Decode SmallIntegers
            int position = positionEncoded >> 1;
            int writeLimit = writeLimitEncoded >> 1;

            // Check if at limit
            if (position >= writeLimit)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Write element at position (0-indexed internally)
            int valueAsInt = value.IsSmallInteger ? (int)value.AsSmallInteger() : (int)value.AsObjectPointer();
            ctx.Heap.SetIndexedField(collectionOop, position, valueAsInt);

            // Increment position
            ctx.SetFieldValue(streamOop, "class:Stream", "position", (position + 1) << 1 | 1);

            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 67: Stream atEnd - Check if stream is at end
    public static SObjectRef AtEnd(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int streamOop = (int)receiver.AsObjectPointer();

            // Get Stream instance variables
            int positionEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "position");
            int readLimitEncoded = ctx.GetFieldValue(streamOop, "class:Stream", "readLimit");

            // Decode SmallIntegers
            int position = positionEncoded >> 1;
            int readLimit = readLimitEncoded >> 1;

            // Return true if at or past limit
            return position >= readLimit
                ? SpecialObjectsArray.Instance.True
                : SpecialObjectsArray.Instance.False;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 68: Object become: (two-way identity swap)
    public static SObjectRef Become(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int oop1 = (int)receiver.AsObjectPointer();
        int oop2 = (int)args[0].AsObjectPointer();

        try
        {
            // Swap identities bidirectionally
            ctx.BecomeTwoWay(oop1, oop2);
            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 68: CompiledMethod objectAt: - Access method header/literal
    public static SObjectRef ObjectAt(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        } // Smalltalk arrays are 1-indexed

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // For CompiledMethod objects:
            // Index 1 = method header
            // Index 2+ = literals
            // This accesses the PointerIndexed part of the CompiledMethod
            // (header + literals), NOT the bytecode array
            int receiverOop = (int)receiver.AsObjectPointer();
            
            // Get the indexed OOP at the specified position (1-based → 0-based)
            int value = ctx.Heap.GetIndexedField(receiverOop, index - 1);
            return SObjectRef.FromObjectPointer(value);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 69: CompiledMethod objectAt:put: - Modify method header/literal
    public static SObjectRef ObjectAtPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        if (!args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int index = args[0].AsSmallInteger();
        if (index < 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SObjectRef value = args[1];

        try
        {
            // For CompiledMethod objects:
            // Index 1 = method header
            // Index 2+ = literals  
            // This modifies the PointerIndexed part of the CompiledMethod
            // (header + literals), NOT the bytecode array
            int receiverOop = (int)receiver.AsObjectPointer();
            
            // Set the indexed OOP at the specified position (1-based → 0-based)
            int valueAsInt = value.IsSmallInteger ? (int)value.AsSmallInteger() : (int)value.AsObjectPointer();
            ctx.Heap.SetIndexedField(receiverOop, index - 1, valueAsInt);
            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 75: Object asOop - Get object pointer as integer
    public static SObjectRef AsOop(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return the object pointer as an integer (unique object ID)
        // For SmallIntegers, this fails (they don't have object pointers)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
        
        // Return the object table index as a SmallInteger
        int oop = (int)receiver.AsObjectPointer();
        return SObjectRef.FromSmallInteger(oop);
    }

    // Primitive 75 alternative: Object hash (for backward compatibility)
    public static SObjectRef Hash(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

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
