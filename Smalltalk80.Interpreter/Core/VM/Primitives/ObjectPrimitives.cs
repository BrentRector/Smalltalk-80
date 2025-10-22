using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Object manipulation primitives (Primitives 60-79, 110-127).
/// Handles array access, object creation, identity, and class operations.
/// </summary>
public static class ObjectPrimitives
{
    // Primitive 60: Array at:
    public static SmalltalkOOP At(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Arrays use PointerIndexed format - get OOP directly
            return ctx.HeapManager.GetIndexedOOP(receiver, index - 1);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 61: Array at:put:
    public static SmalltalkOOP AtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        SmalltalkOOP value = args[1];

        try
        {
            // Convert to 0-based index
            // Arrays use PointerIndexed format - set OOP directly
            ctx.HeapManager.SetIndexedOOP(receiver, index - 1, value);
            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 62: Object size
    public static SmalltalkOOP Size(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        int indexedSize = ctx.HeapManager.GetIndexedSize(receiver);
        return SmalltalkOOP.FromSmallInteger(indexedSize);
    }

    // Primitive 70: Object new
    public static SmalltalkOOP New(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        // Allocate a new instance of the receiver class with no indexed fields
        // Uses heap data only - reads the class's format and instance variable count
        try
        {
            return ctx.HeapManager.AllocateInstanceOf(receiver, 0);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 71: Object new:
    public static SmalltalkOOP NewWithSize(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        // Allocate indexed object of the receiver class with given size
        // Uses heap data only - reads the class's format and instance variable count
        // Used for Array new: n, ByteArray new: n, etc.
        try
        {
            return ctx.HeapManager.AllocateInstanceOf(receiver, size);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 110: Object ==
    public static SmalltalkOOP IdentityEquals(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Class(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Get the class OOP from slot 0
            return ctx.HeapManager.GetClassOOP(receiver);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 70-77: Instance variable access
    // These are optimized for getting/setting instance variables

    public static SmalltalkOOP InstVarAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Get instance variable as OOP
            return ctx.HeapManager.GetInstanceVariable(receiver, index - 1);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    public static SmalltalkOOP InstVarAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        SmalltalkOOP value = args[1];

        try
        {
            // Convert to 0-based index
            // Set instance variable as OOP
            ctx.HeapManager.SetInstanceVariable(receiver, index - 1, value);
            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 63: String at: (character access for byte-indexed objects)
    public static SmalltalkOOP StringAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Strings use ByteIndexed format
            byte byteValue = ctx.HeapManager.GetIndexedByte(receiver, index - 1);
            return SmalltalkOOP.FromSmallInteger(byteValue);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 64: String at:put: (character store for byte-indexed objects)
    public static SmalltalkOOP StringAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            ctx.HeapManager.SetIndexedByte(receiver, index - 1, (byte)charValue);
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
    public static SmalltalkOOP Next(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Get Stream instance variables
            SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, "class:Stream", "position");
            SmalltalkOOP readLimitOOP = ctx.GetFieldValue(receiver, "class:Stream", "readLimit");
            SmalltalkOOP collectionOOP = ctx.GetFieldValue(receiver, "class:Stream", "collection");

            // Decode SmallIntegers
            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

            // Check if at end
            if (position >= readLimit)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Read element at position (1-indexed in Smalltalk)
            // Streams typically work with PointerIndexed collections
            SmalltalkOOP element = ctx.HeapManager.GetIndexedOOP(collectionOOP, position);

            // Increment position
            ctx.SetFieldValue(receiver, "class:Stream", "position", SmalltalkOOP.FromSmallInteger(position + 1));

            return element;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 66: Stream nextPut: - Write element to stream
    public static SmalltalkOOP NextPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            SmalltalkOOP value = args[0];

            // Get Stream instance variables
            SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, "class:Stream", "position");
            SmalltalkOOP writeLimitOOP = ctx.GetFieldValue(receiver, "class:Stream", "writeLimit");
            SmalltalkOOP collectionOOP = ctx.GetFieldValue(receiver, "class:Stream", "collection");

            // Decode SmallIntegers
            int position = positionOOP.AsSmallInteger();
            int writeLimit = writeLimitOOP.AsSmallInteger();

            // Check if at limit
            if (position >= writeLimit)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Write element at position (0-indexed internally)
            // Streams typically work with PointerIndexed collections
            ctx.HeapManager.SetIndexedOOP(collectionOOP, position, value);

            // Increment position
            ctx.SetFieldValue(receiver, "class:Stream", "position", SmalltalkOOP.FromSmallInteger(position + 1));

            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 67: Stream atEnd - Check if stream is at end
    public static SmalltalkOOP AtEnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Get Stream instance variables
            SmalltalkOOP positionOOP = ctx.GetFieldValue(receiver, "class:Stream", "position");
            SmalltalkOOP readLimitOOP = ctx.GetFieldValue(receiver, "class:Stream", "readLimit");

            // Decode SmallIntegers
            int position = positionOOP.AsSmallInteger();
            int readLimit = readLimitOOP.AsSmallInteger();

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
    public static SmalltalkOOP Become(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        try
        {
            // Swap identities bidirectionally
            ctx.BecomeTwoWay(receiver, args[0]);
            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 68: CompiledMethod objectAt: - Access method header/literal
    public static SmalltalkOOP ObjectAt(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            
            // Get the indexed OOP at the specified position (1-based → 0-based)
            // CompiledMethod uses PointerIndexed format for header and literals
            return ctx.HeapManager.GetIndexedOOP(receiver, index - 1);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 69: CompiledMethod objectAt:put: - Modify method header/literal
    public static SmalltalkOOP ObjectAtPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        SmalltalkOOP value = args[1];

        try
        {
            // For CompiledMethod objects:
            // Index 1 = method header
            // Index 2+ = literals  
            // This modifies the PointerIndexed part of the CompiledMethod
            // (header + literals), NOT the bytecode array
            
            // Set the indexed OOP at the specified position (1-based → 0-based)
            // CompiledMethod uses PointerIndexed format for header and literals
            ctx.HeapManager.SetIndexedOOP(receiver, index - 1, value);
            return value;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 75: Object asOop - Get object pointer as integer
    public static SmalltalkOOP AsOop(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
        
        // Return the raw object table index as a SmallInteger
        // This is a special case where we need the raw index value for identity
        // Extract just the index portion of the OOP (bits 1-30, bit 0 is tag)
        int rawIndex = receiver.RawValue >> 1;
        return SmalltalkOOP.FromSmallInteger(rawIndex);
    }

    // Primitive 75 alternative: Object hash (for backward compatibility)
    public static SmalltalkOOP Hash(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            // Extract raw index from the OOP value for hashing
            int rawIndex = receiver.RawValue >> 1;
            return SmalltalkOOP.FromSmallInteger(rawIndex & 0x1FFFFFFF);
        }
    }
}
