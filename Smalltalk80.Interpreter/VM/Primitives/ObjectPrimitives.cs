using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Object manipulation primitives (Primitives 60-79, 110-127).
/// Handles array access, object creation, identity, and class operations.
/// </summary>
public static class ObjectPrimitives
{
    // Primitive 60: Array at:
    public static PrimitiveResult At(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed(); // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField(receiver, index - 1);
            return PrimitiveResult.Succeeded(value);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 61: Array at:put:
    public static PrimitiveResult AtPut(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 2) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed();

        int value = args[1];

        try
        {
            // Convert to 0-based index
            ctx.Heap.SetIndexedField(receiver, index - 1, value);
            return PrimitiveResult.Succeeded(value);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 62: Object size
    public static PrimitiveResult Size(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();

        // For now, this requires heap support - return failed
        // TODO: Add GetIndexedSize to heap interface
        return PrimitiveResult.Failed();
    }

    // Primitive 70: Object new
    public static PrimitiveResult New(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();

        // Receiver is the class
        // Need to allocate new instance with appropriate size
        // TODO: Implement when we have class metadata access
        return PrimitiveResult.Failed();
    }

    // Primitive 71: Object new:
    public static PrimitiveResult NewWithSize(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int size = ctx.DecodeSmallInteger(args[0]);
        if (size < 0) return PrimitiveResult.Failed();

        // Allocate indexed object of given size
        // TODO: Implement when we have proper class allocation
        return PrimitiveResult.Failed();
    }

    // Primitive 110: Object ==
    public static PrimitiveResult IdentityEquals(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();

        bool identical = receiver == args[0];
        return PrimitiveResult.Succeeded(identical ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 111: Object class
    public static PrimitiveResult Class(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();

        try
        {
            int classId = ctx.Heap.GetClass(receiver);
            return PrimitiveResult.Succeeded(classId);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 70-77: Instance variable access
    // These are optimized for getting/setting instance variables

    public static PrimitiveResult InstVarAt(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed();

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetField(receiver, index - 1);
            return PrimitiveResult.Succeeded(value);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    public static PrimitiveResult InstVarAtPut(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 2) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed();

        int value = args[1];

        try
        {
            // Convert to 0-based index
            ctx.Heap.SetField(receiver, index - 1, value);
            return PrimitiveResult.Succeeded(value);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 63: String at: (character access for byte-indexed objects)
    public static PrimitiveResult StringAt(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed(); // Smalltalk arrays are 1-indexed

        try
        {
            // Convert to 0-based index
            int value = ctx.Heap.GetIndexedField(receiver, index - 1);
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(value & 0xFF)); // Ensure byte range
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 64: String at:put: (character store for byte-indexed objects)
    public static PrimitiveResult StringAtPut(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 2) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(args[0]) || !ctx.IsSmallInteger(args[1])) return PrimitiveResult.Failed();

        int index = ctx.DecodeSmallInteger(args[0]);
        if (index < 1) return PrimitiveResult.Failed();

        int charValue = ctx.DecodeSmallInteger(args[1]);
        if (charValue < 0 || charValue > 255) return PrimitiveResult.Failed();

        try
        {
            // Convert to 0-based index
            ctx.Heap.SetIndexedField(receiver, index - 1, charValue);
            return PrimitiveResult.Succeeded(args[1]);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 65: Stream next (stub - requires Stream support)
    public static PrimitiveResult Next(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();
        // TODO: Implement when Stream support is added
        return PrimitiveResult.Failed();
    }

    // Primitive 66: Stream nextPut: (stub - requires Stream support)
    public static PrimitiveResult NextPut(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        // TODO: Implement when Stream support is added
        return PrimitiveResult.Failed();
    }

    // Primitive 67: Stream atEnd (stub - requires Stream support)
    public static PrimitiveResult AtEnd(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();
        // TODO: Implement when Stream support is added
        return PrimitiveResult.Failed();
    }

    // Primitive 68: Object become: (object swapping - stub)
    public static PrimitiveResult Become(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();

        // Cannot become with SmallIntegers
        if (ctx.IsSmallInteger(receiver) || ctx.IsSmallInteger(args[0]))
            return PrimitiveResult.Failed();

        // TODO: Implement object swapping in heap
        // This requires deep memory manipulation
        return PrimitiveResult.Failed();
    }

    // Primitive 75: Object hash
    public static PrimitiveResult Hash(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0) return PrimitiveResult.Failed();

        // Use object ID as hash for heap objects
        // For SmallIntegers, use the value itself
        if (ctx.IsSmallInteger(receiver))
        {
            return PrimitiveResult.Succeeded(receiver);
        }
        else
        {
            // Object ID is already a good hash
            int hash = receiver >> 1; // Remove pointer tag
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(hash & 0x1FFFFFFF));
        }
    }
}
