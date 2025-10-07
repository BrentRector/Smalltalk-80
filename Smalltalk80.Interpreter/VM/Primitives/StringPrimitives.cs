using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.VM.Primitives;

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
    public static SObjectRef StringReplace(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
        SObjectRef sourceRef = args[2];
        int sourceStart = args[3].AsSmallInteger() - 1;

        // Validate indices
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger || sourceRef.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int copyCount = stopIndex - startIndex + 1;

        // Validate bounds
        int receiverId = (int)receiver.AsObjectPointer();
        int sourceId = (int)sourceRef.AsObjectPointer();
        int receiverSize = ctx.Heap.GetIndexedSize(receiverId);
        int sourceSize = ctx.Heap.GetIndexedSize(sourceId);

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
                int value = ctx.Heap.GetIndexedField(sourceId, sourceStart + i);
                ctx.Heap.SetIndexedField(receiverId, startIndex + i, value);
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
    public static SObjectRef StringHash(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
            int receiverId = (int)receiver.AsObjectPointer();
            int size = ctx.Heap.GetIndexedSize(receiverId);
            int hash = 0;

            for (int i = 0; i < size; i++)
            {
                int byteValue = ctx.Heap.GetIndexedField(receiverId, i);
                hash = (hash * 31) + byteValue;
            }

            // Ensure positive and within SmallInteger range
            hash &= 0x3FFFFFFF;

            return SObjectRef.FromSmallInteger(hash);
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
    public static SObjectRef StringCompare(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
            int receiverId = (int)receiver.AsObjectPointer();
            int argId = (int)args[0].AsObjectPointer();
            int receiverSize = ctx.Heap.GetIndexedSize(receiverId);
            int argSize = ctx.Heap.GetIndexedSize(argId);
            int minSize = Math.Min(receiverSize, argSize);

            // Compare byte by byte up to the shorter length
            for (int i = 0; i < minSize; i++)
            {
                int receiverByte = ctx.Heap.GetIndexedField(receiverId, i);
                int argByte = ctx.Heap.GetIndexedField(argId, i);

                if (receiverByte < argByte)
                    return SObjectRef.FromSmallInteger(-1);

                if (receiverByte > argByte)
                    return SObjectRef.FromSmallInteger(1);
            }

            // If all compared bytes are equal, compare lengths
            if (receiverSize < argSize)
                return SObjectRef.FromSmallInteger(-1);

            if (receiverSize > argSize)
                return SObjectRef.FromSmallInteger(1);

            // Completely equal
            return SObjectRef.FromSmallInteger(0);
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
    public static SObjectRef AsSymbol(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
