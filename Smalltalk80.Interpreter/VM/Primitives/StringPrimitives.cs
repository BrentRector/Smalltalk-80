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
    public static PrimitiveResult StringReplace(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 4)
            return PrimitiveResult.Failed();

        if (!ctx.IsSmallInteger(args[0]) || !ctx.IsSmallInteger(args[1]) || !ctx.IsSmallInteger(args[3]))
            return PrimitiveResult.Failed();

        int startIndex = ctx.DecodeSmallInteger(args[0]) - 1; // Convert to 0-based
        int stopIndex = ctx.DecodeSmallInteger(args[1]) - 1;
        int sourceId = args[2];
        int sourceStart = ctx.DecodeSmallInteger(args[3]) - 1;

        // Validate indices
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
            return PrimitiveResult.Failed();

        if (ctx.IsSmallInteger(receiver) || ctx.IsSmallInteger(sourceId))
            return PrimitiveResult.Failed();

        // TODO: Need GetIndexedSize to validate bounds
        int copyCount = stopIndex - startIndex + 1;

        // Copy elements
        try
        {
            for (int i = 0; i < copyCount; i++)
            {
                int value = ctx.Heap.GetIndexedField(sourceId, sourceStart + i);
                ctx.Heap.SetIndexedField(receiver, startIndex + i, value);
            }
            return PrimitiveResult.Succeeded(receiver);
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    /// <summary>
    /// Primitive 104: String hash
    /// Computes a hash value for a string.
    /// </summary>
    public static PrimitiveResult StringHash(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (ctx.IsSmallInteger(receiver))
            return PrimitiveResult.Failed();

        // TODO: Need GetIndexedSize to get string length
        // For now, compute hash from first few bytes
        try
        {
            int hash = 0;
            for (int i = 0; i < 16; i++) // Sample first 16 chars
            {
                try
                {
                    int byteValue = ctx.Heap.GetIndexedField(receiver, i);
                    hash = (hash * 31) + byteValue;
                }
                catch
                {
                    // Reached end of string
                    break;
                }
            }

            // Ensure positive and within SmallInteger range
            hash &= 0x3FFFFFFF;

            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(hash));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    /// <summary>
    /// Primitive 105: String compare
    /// Compares two strings lexicographically.
    /// Returns -1, 0, or 1 for less than, equal, or greater than.
    /// </summary>
    public static PrimitiveResult StringCompare(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (ctx.IsSmallInteger(receiver) || ctx.IsSmallInteger(args[0]))
            return PrimitiveResult.Failed();

        // TODO: Need GetIndexedSize to get lengths
        // For now, compare byte by byte until one ends
        try
        {
            int i = 0;
            while (true)
            {
                int receiverByte, argByte;
                bool receiverHasMore, argHasMore;

                try
                {
                    receiverByte = ctx.Heap.GetIndexedField(receiver, i);
                    receiverHasMore = true;
                }
                catch
                {
                    receiverByte = 0;
                    receiverHasMore = false;
                }

                try
                {
                    argByte = ctx.Heap.GetIndexedField(args[0], i);
                    argHasMore = true;
                }
                catch
                {
                    argByte = 0;
                    argHasMore = false;
                }

                if (!receiverHasMore && !argHasMore)
                    return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(0)); // Equal

                if (!receiverHasMore)
                    return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(-1)); // Receiver shorter

                if (!argHasMore)
                    return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(1)); // Arg shorter

                if (receiverByte < argByte)
                    return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(-1));

                if (receiverByte > argByte)
                    return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(1));

                i++;

                // Safety limit to prevent infinite loop
                if (i > 10000)
                    return PrimitiveResult.Failed();
            }
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    /// <summary>
    /// Primitive 106: String asSymbol
    /// Converts a string to a unique symbol.
    /// </summary>
    public static PrimitiveResult AsSymbol(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (ctx.IsSmallInteger(receiver))
            return PrimitiveResult.Failed();

        // TODO: Need symbol interning infrastructure
        // For now, fail - will implement when symbol table is added
        return PrimitiveResult.Failed();
    }
}
