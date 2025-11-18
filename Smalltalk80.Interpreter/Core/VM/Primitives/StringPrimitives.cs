using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// String and ByteArray primitives (105-106).
/// Handles character-based operations and string comparisons.
/// Based on Smalltalk-80 specification for String and ByteArray operations.
/// </summary>
/// <remarks>
/// <para><b>Blue Book Context:</b></para>
/// <para>Primitives 105-109 are marked as implementation-specific in the Blue Book.
/// These primitives provide optimized operations for String and ByteArray manipulation
/// that would be inefficient in pure Smalltalk.</para>
///
/// <para><b>Implementation Architecture:</b></para>
/// <list type="bullet">
/// <item>All operations use heap-only access pattern</item>
/// <item>Handles both String and ByteArray objects uniformly</item>
/// <item>Overlap-safe copying for replaceFrom:to:with:startingAt:</item>
/// <item>Symbol interning through USTable access</item>
/// </list>
///
/// <para><b>Primitives Implemented:</b></para>
/// <list type="bullet">
/// <item>105: replaceFrom:to:with:startingAt: - Bulk element copy operation</item>
/// <item>104 (StringHash): String hash - Hash value computation</item>
/// <item>105 (StringCompare): String compare - Lexicographic comparison</item>
/// <item>106: asSymbol - Convert String to unique Symbol</item>
/// </list>
///
/// <para><b>Conformance:</b> Implementation-specific primitives following Smalltalk semantics</para>
/// </remarks>
public static class StringPrimitives
{
    // <summary>
    // Primitive 105: replaceFrom:to:with:startingAt: - Bulk element copy
    // Blue Book: Implementation-specific, not formally specified
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Copies elements from source collection to receiver, handling overlapping regions correctly.
    // This primitive is critical for efficient collection manipulation.</para>
    //
    // <para><b>Arguments:</b></para>
    // <list type="bullet">
    // <item>args[0]: startIndex - First index to replace (1-based)</item>
    // <item>args[1]: stopIndex - Last index to replace (1-based)</item>
    // <item>args[2]: sourceCollection - Source of elements</item>
    // <item>args[3]: sourceStartIndex - Starting position in source (1-based)</item>
    // </list>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Converts 1-based Smalltalk indices to 0-based C# indices</item>
    // <item>Validates all bounds before copying</item>
    // <item>Handles overlapping regions by choosing appropriate copy direction</item>
    // <item>Works for both ByteIndexed objects (String, ByteArray)</item>
    // </list>
    //
    // <para><b>Conformance:</b> Semantically correct implementation with overlap handling</para>
    // </remarks>
    public static SmalltalkOOP StringReplace(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveReplaceFromToWithStartingAt
        // Bulk copy operation for byte-indexed collections

        // Expect 4 arguments: startIndex, stopIndex, sourceCollection, sourceStartIndex
        if (args.Length != 4)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Validate index arguments are SmallIntegers
        if (!args[0].IsSmallInteger || !args[1].IsSmallInteger || !args[3].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert Smalltalk 1-based indices to C# 0-based indices
        int startIndex = args[0].AsSmallInteger() - 1;
        int stopIndex = args[1].AsSmallInteger() - 1;
        SmalltalkOOP sourceOop = args[2];
        int sourceStart = args[3].AsSmallInteger() - 1;

        // Validate index ranges
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both receiver and source must be byte-indexed objects
        if (receiver.IsSmallInteger || sourceOop.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Calculate number of elements to copy
        int copyCount = stopIndex - startIndex + 1;

        // Validate bounds against actual collection sizes
        int receiverSize = ctx.HeapManager.GetIndexedSize(receiver);
        int sourceSize = ctx.HeapManager.GetIndexedSize(sourceOop);

        if (stopIndex >= receiverSize || sourceStart + copyCount > sourceSize)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Perform copy with overlap detection
        try
        {
            // Check for overlapping regions when copying within same object
            if (receiver == sourceOop && startIndex > sourceStart && startIndex < sourceStart + copyCount)
            {
                // Overlapping region detected - copy backwards to preserve data
                // Example: 'ABCDEF' replaceFrom: 3 to: 5 with: 'ABCDEF' startingAt: 2
                // Must copy 'BCD' to positions 3-5 without corrupting source
                for (int i = copyCount - 1; i >= 0; i--)
                {
                    byte value = ctx.HeapManager.GetIndexedByte(sourceOop, sourceStart + i);
                    ctx.HeapManager.SetIndexedByte(receiver, startIndex + i, value);
                }
            }
            else
            {
                // No overlap or safe direction - copy forward for better cache performance
                for (int i = 0; i < copyCount; i++)
                {
                    byte value = ctx.HeapManager.GetIndexedByte(sourceOop, sourceStart + i);
                    ctx.HeapManager.SetIndexedByte(receiver, startIndex + i, value);
                }
            }

            // Return receiver (modified in place)
            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 104: String hash
    // Computes a hash value for a string.
    // </summary>
    public static SmalltalkOOP StringHash(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveStringHash
        // Computes hash value for string/symbol comparison

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be byte-indexed object (String or Symbol)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Get string length
            int size = ctx.HeapManager.GetIndexedSize(receiver);
            int hash = 0;

            // Compute hash using polynomial rolling hash
            // Uses prime multiplier 31 for good distribution
            for (int i = 0; i < size; i++)
            {
                byte byteValue = ctx.HeapManager.GetIndexedByte(receiver, i);
                hash = (hash * 31) + byteValue;
            }

            // Ensure positive and within SmallInteger range (30 bits)
            hash &= 0x3FFFFFFF;

            // Return hash as SmallInteger
            return SmalltalkOOP.FromSmallInteger(hash);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 105: String compare
    // Compares two strings lexicographically.
    // Returns -1, 0, or 1 for less than, equal, or greater than.
    // </summary>
    public static SmalltalkOOP StringCompare(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveStringCompare
        // Lexicographic comparison for sorting

        // Expect single string argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both receiver and argument must be byte-indexed objects
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Get sizes of both strings
            int receiverSize = ctx.HeapManager.GetIndexedSize(receiver);
            int argSize = ctx.HeapManager.GetIndexedSize(args[0]);
            int minSize = Math.Min(receiverSize, argSize);

            // Compare byte by byte up to shorter length
            // ASCII/Latin-1 byte values determine ordering
            for (int i = 0; i < minSize; i++)
            {
                byte receiverByte = ctx.HeapManager.GetIndexedByte(receiver, i);
                byte argByte = ctx.HeapManager.GetIndexedByte(args[0], i);

                // First difference determines result
                if (receiverByte < argByte)
                    return SmalltalkOOP.FromSmallInteger(-1); // Receiver &lt; argument

                if (receiverByte > argByte)
                    return SmalltalkOOP.FromSmallInteger(1); // Receiver &gt; argument
            }

            // All compared bytes equal - shorter string is less
            if (receiverSize < argSize)
                return SmalltalkOOP.FromSmallInteger(-1);

            if (receiverSize > argSize)
                return SmalltalkOOP.FromSmallInteger(1);

            // Strings are identical
            return SmalltalkOOP.FromSmallInteger(0);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 106: String asSymbol
    // Converts a string to a unique symbol.
    // Returns the existing Symbol if the string is already interned, otherwise fails
    // and lets Smalltalk create a new Symbol.
    // </summary>
    public static SmalltalkOOP AsSymbol(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveAsSymbol
        // Interns string to unique symbol

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be String object
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract string bytes from receiver
        var stringBytes = ctx.GetByteArray(receiver);
        if (stringBytes.Length == 0)
        {
            // Empty string handling
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert bytes to .NET string (Smalltalk uses Latin-1 encoding)
        string stringValue = System.Text.Encoding.Latin1.GetString(stringBytes.Span);

        // Attempt to find existing symbol in USTable (Unique String Table)
        var symbolOOP = ctx.InternSymbol(stringValue);

        // If symbol doesn't exist yet, fail primitive
        // Smalltalk will create new Symbol and add to USTable
        if (symbolOOP == SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return existing interned symbol
        return symbolOOP;
    }
}
