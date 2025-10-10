using System;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// String and ByteArray primitives (primarily primitives 63-68 for strings, 105 for string comparison).
/// These primitives handle character-based operations and string comparisons.
/// Based on Blue Book specification for String and ByteArray operations.
/// </summary>
public static class StringPrimitives
{
    /// <summary>
    /// Primitive 105: String comparison.
    /// Compares two strings lexicographically.
    /// </summary>
    /// <param name="receiver">The first string.</param>
    /// <param name="arguments">Single argument containing the second string.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>-1, 0, or 1 as SmallInteger for less than, equal, or greater than.</returns>
    public static PrimitiveResult PrimitiveStringCompare(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver) || !context.IsHeapObject(arguments[0]))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        var receiverOop = receiver.AsObjectPointer();
        var argumentOop = arguments[0].AsObjectPointer();

        var receiverSize = context.GetIndexedSize(receiverOop);
        var argumentSize = context.GetIndexedSize(argumentOop);

        // Both must be byte-indexable (String or ByteArray)
        if (receiverSize == 0 || argumentSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Compare byte by byte
        int minLength = Math.Min(receiverSize, argumentSize);

        for (int i = 0; i < minLength; i++)
        {
            var receiverByte = context.GetIndexedPointer(receiverOop, i);
            var argumentByte = context.GetIndexedPointer(argumentOop, i);

            if (!receiverByte.IsSmallInteger || !argumentByte.IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

            int receiverValue = receiverByte.AsSmallInteger();
            int argumentValue = argumentByte.AsSmallInteger();

            if (receiverValue < argumentValue)
                return SObjectRef.FromSmallInteger(-1);
            if (receiverValue > argumentValue)
                return SObjectRef.FromSmallInteger(1);
        }

        // All compared bytes are equal, check lengths
        if (receiverSize < argumentSize)
            return SObjectRef.FromSmallInteger(-1);
        if (receiverSize > argumentSize)
            return SObjectRef.FromSmallInteger(1);

        return SObjectRef.FromSmallInteger(0);
    }

    /// <summary>
    /// Primitive 103: String replaceFrom:to:with:startingAt:.
    /// Efficiently copies a range of elements from one collection to another.
    /// </summary>
    /// <param name="receiver">The destination collection.</param>
    /// <param name="arguments">Four arguments: start index, stop index, source collection, source start index.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver after replacement.</returns>
    public static PrimitiveResult PrimitiveStringReplace(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 4)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // All indices must be SmallIntegers
        if (!arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger || !arguments[3].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int startIndex = arguments[0].AsSmallInteger() - 1; // Convert to 0-based
        int stopIndex = arguments[1].AsSmallInteger() - 1;
        SObjectRef sourceOOP = arguments[2];
        int sourceStart = arguments[3].AsSmallInteger() - 1;

        // Validate indices
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        if (!context.IsHeapObject(receiver) || !context.IsHeapObject(sourceOOP))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        var destOop = receiver.AsObjectPointer();
        var sourceOop = sourceOOP.AsObjectPointer();

        var destSize = context.GetIndexedSize(destOop);
        var sourceSize = context.GetIndexedSize(sourceOop);

        // Both must be indexable
        if (destSize == 0 || sourceSize == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        int copyCount = stopIndex - startIndex + 1;

        // Check bounds
        if (stopIndex >= destSize || sourceStart + copyCount > sourceSize)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

        // Copy elements
        for (int i = 0; i < copyCount; i++)
        {
            var value = context.GetIndexedPointer(sourceOop, sourceStart + i);
            context.SetIndexedPointer(destOop, startIndex + i, value);
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 104: String hash.
    /// Computes a hash value for a string.
    /// </summary>
    /// <param name="receiver">The string to hash.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Hash value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveStringHash(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();
        var size = context.GetIndexedSize(oop);

        // Must be byte-indexable
        if (size == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // Compute hash based on string contents
        int hash = 0;
        for (int i = 0; i < Math.Min(size, 16); i++) // Sample first 16 chars
        {
            var byteRef = context.GetIndexedPointer(oop, i);
            if (!byteRef.IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

            hash = (hash * 31) + byteRef.AsSmallInteger();
        }

        // Mix in the length
        hash ^= size;

        // Ensure positive and within SmallInteger range
        hash &= 0x3FFFFFFF;

        return SObjectRef.FromSmallInteger(hash);
    }

    /// <summary>
    /// Primitive 106: String asSymbol.
    /// Converts a string to a unique symbol.
    /// </summary>
    /// <param name="receiver">The string to convert.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The interned symbol.</returns>
    public static PrimitiveResult PrimitiveAsSymbol(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();
        var size = context.GetIndexedSize(oop);

        // Must be byte-indexable
        if (size == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // Extract string from bytes
        char[] chars = new char[size];
        for (int i = 0; i < size; i++)
        {
            var byteRef = context.GetIndexedPointer(oop, i);
            if (!byteRef.IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

            int byteValue = byteRef.AsSmallInteger();
            if (byteValue < 0 || byteValue > 255)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

            chars[i] = (char)byteValue;
        }

        string stringValue = new(chars);

        // Use the context's symbol interning
        return context.InternSymbol(stringValue);
    }

    /// <summary>
    /// Helper method to create a String object from a C# string.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="value">The string value.</param>
    /// <returns>OOP of the created String object.</returns>
    public static SObjectRef CreateString(IPrimitiveContext context, string value)
    {
        var stringClass = context.GetClass("String");
        if (stringClass.IsNil)
            return SObjectRef.Nil;

        // Allocate a string with the appropriate size
        var stringRef = context.NewInstance(stringClass, value.Length);

        if (!stringRef.IsNil && context.IsHeapObject(stringRef))
        {
            var oop = stringRef.AsObjectPointer();
            for (int i = 0; i < value.Length; i++)
            {
                context.SetIndexedPointer(oop, i, SObjectRef.FromSmallInteger(value[i]));
            }
        }

        return stringRef;
    }

    /// <summary>
    /// Helper method to extract a C# string from a String object.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="stringOop">The String object OOP.</param>
    /// <returns>The extracted string, or null if invalid.</returns>
    public static string? ExtractString(IPrimitiveContext context, SObjectRef stringOop)
    {
        if (!context.IsHeapObject(stringOop))
            return null;

        var oop = stringOop.AsObjectPointer();
        var size = context.GetIndexedSize(oop);

        if (size == 0)
            return string.Empty;

        char[] chars = new char[size];
        for (int i = 0; i < size; i++)
        {
            var byteRef = context.GetIndexedPointer(oop, i);
            if (!byteRef.IsSmallInteger)
                return null;

            int byteValue = byteRef.AsSmallInteger();
            if (byteValue < 0 || byteValue > 255)
                return null;

            chars[i] = (char)byteValue;
        }

        return new string(chars);
    }

    /// <summary>
    /// Registers all string primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        // Note: Primitives 63-64 are registered in ArrayPrimitives as they're shared
        table.Register(103, "primitiveStringReplace", PrimitiveStringReplace);
        table.Register(104, "primitiveStringHash", PrimitiveStringHash);
        table.Register(105, "primitiveStringCompare", PrimitiveStringCompare);
        table.Register(106, "primitiveAsSymbol", PrimitiveAsSymbol);
    }
}