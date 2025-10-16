using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// String and ByteArray primitives (103-106).
/// Handles character-based operations and string comparisons.
/// Based on Blue Book specification for String and ByteArray operations.
/// </summary>
public static class StringPrimitives
{
    /// <summary>
    /// Primitive 103: replaceFrom:to:with:startingAt:
    /// Efficiently copies a range of elements from one collection to another.
    /// </summary>
    public static SmalltalkOOP StringReplace(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 4)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger || !args[3].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int startIndex = args[0].AsSmallInteger() - 1; // Convert to 0-based
        int stopIndex = args[1].AsSmallInteger() - 1;
        SmalltalkOOP sourceOop = args[2];
        int sourceStart = args[3].AsSmallInteger() - 1;

        // Validate indices
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger || sourceOop.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int copyCount = stopIndex - startIndex + 1;

        // Validate bounds
        int receiverSize = ctx.HeapManager.GetIndexedSize(receiver);
        int sourceSize = ctx.HeapManager.GetIndexedSize(sourceOop);

        if (stopIndex >= receiverSize || sourceStart + copyCount > sourceSize)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Copy elements
        try
        {
            for (int i = 0; i < copyCount; i++)
            {
                // String/ByteArray use ByteIndexed format
                byte value = ctx.HeapManager.GetIndexedByte(sourceOop, sourceStart + i);
                ctx.HeapManager.SetIndexedByte(receiver, startIndex + i, value);
            }
            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 104: String hash
    /// Computes a hash value for a string.
    /// </summary>
    public static SmalltalkOOP StringHash(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
            int size = ctx.HeapManager.GetIndexedSize(receiver);
            int hash = 0;

            for (int i = 0; i < size; i++)
            {
                byte byteValue = ctx.HeapManager.GetIndexedByte(receiver, i);
                hash = (hash * 31) + byteValue;
            }

            // Ensure positive and within SmallInteger range
            hash &= 0x3FFFFFFF;

            return SmalltalkOOP.FromSmallInteger(hash);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 105: String compare
    /// Compares two strings lexicographically.
    /// Returns -1, 0, or 1 for less than, equal, or greater than.
    /// </summary>
    public static SmalltalkOOP StringCompare(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int receiverSize = ctx.HeapManager.GetIndexedSize(receiver);
            int argSize = ctx.HeapManager.GetIndexedSize(args[0]);
            int minSize = Math.Min(receiverSize, argSize);

            // Compare byte by byte up to the shorter length
            for (int i = 0; i < minSize; i++)
            {
                byte receiverByte = ctx.HeapManager.GetIndexedByte(receiver, i);
                byte argByte = ctx.HeapManager.GetIndexedByte(args[0], i);

                if (receiverByte < argByte)
                    return SmalltalkOOP.FromSmallInteger(-1);

                if (receiverByte > argByte)
                    return SmalltalkOOP.FromSmallInteger(1);
            }

            // If all compared bytes are equal, compare lengths
            if (receiverSize < argSize)
                return SmalltalkOOP.FromSmallInteger(-1);

            if (receiverSize > argSize)
                return SmalltalkOOP.FromSmallInteger(1);

            // Completely equal
            return SmalltalkOOP.FromSmallInteger(0);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 106: String asSymbol
    /// Converts a string to a unique symbol.
    /// </summary>
    public static SmalltalkOOP AsSymbol(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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

        // TODO: Need symbol interning infrastructure
        // For now, fail - will implement when symbol table is added
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }
}
