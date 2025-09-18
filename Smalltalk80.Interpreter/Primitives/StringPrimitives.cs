using System;
using Smalltalk80.Interpreter.ObjectModel;

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
    public static PrimitiveResult PrimitiveStringCompare(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var receiverObj = context.Memory.GetObject(receiver);
        var argumentObj = context.Memory.GetObject(arguments[0]);
        
        if (receiverObj == null || argumentObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        // Both must be byte-indexable (String or ByteArray)
        if (!receiverObj.IsByteIndexable || !argumentObj.IsByteIndexable)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Compare byte by byte
        int minLength = Math.Min(receiverObj.Fields.Length, argumentObj.Fields.Length);
        
        for (int i = 0; i < minLength; i++)
        {
            if (!receiverObj.Fields[i].IsSmallInteger || !argumentObj.Fields[i].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            int receiverByte = receiverObj.Fields[i].SmallIntegerValue;
            int argumentByte = argumentObj.Fields[i].SmallIntegerValue;
            
            if (receiverByte < argumentByte)
                return OOP.FromSmallInteger(-1);
            if (receiverByte > argumentByte)
                return OOP.FromSmallInteger(1);
        }
        
        // All compared bytes are equal, check lengths
        if (receiverObj.Fields.Length < argumentObj.Fields.Length)
            return OOP.FromSmallInteger(-1);
        if (receiverObj.Fields.Length > argumentObj.Fields.Length)
            return OOP.FromSmallInteger(1);
        
        return OOP.FromSmallInteger(0);
    }

    /// <summary>
    /// Primitive 103: String replaceFrom:to:with:startingAt:.
    /// Efficiently copies a range of elements from one collection to another.
    /// </summary>
    /// <param name="receiver">The destination collection.</param>
    /// <param name="arguments">Four arguments: start index, stop index, source collection, source start index.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver after replacement.</returns>
    public static PrimitiveResult PrimitiveStringReplace(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 4)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // All indices must be SmallIntegers
        if (!arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger || !arguments[3].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int startIndex = arguments[0].SmallIntegerValue - 1; // Convert to 0-based
        int stopIndex = arguments[1].SmallIntegerValue - 1;
        OOP sourceOOP = arguments[2];
        int sourceStart = arguments[3].SmallIntegerValue - 1;
        
        // Validate indices
        if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
        
        var destObj = context.Memory.GetObject(receiver);
        var sourceObj = context.Memory.GetObject(sourceOOP);
        
        if (destObj == null || sourceObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        // Both must be indexable
        if (!destObj.IsIndexable || !sourceObj.IsIndexable)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        int copyCount = stopIndex - startIndex + 1;
        
        // Check bounds
        if (stopIndex >= destObj.Fields.Length || sourceStart + copyCount > sourceObj.Fields.Length)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
        
        // For byte-indexable objects, ensure compatibility
        if (destObj.IsByteIndexable != sourceObj.IsByteIndexable)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Copy elements
        for (int i = 0; i < copyCount; i++)
        {
            destObj.Fields[startIndex + i] = sourceObj.Fields[sourceStart + i];
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
    public static PrimitiveResult PrimitiveStringHash(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var obj = context.Memory.GetObject(receiver);
        if (obj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Must be byte-indexable
        if (!obj.IsByteIndexable)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Compute hash based on string contents
        int hash = 0;
        for (int i = 0; i < Math.Min(obj.Fields.Length, 16); i++) // Sample first 16 chars
        {
            if (!obj.Fields[i].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            
            hash = (hash * 31) + obj.Fields[i].SmallIntegerValue;
        }
        
        // Mix in the length
        hash ^= obj.Fields.Length;
        
        // Ensure positive and within SmallInteger range
        hash &= 0x3FFFFFFF;
        
        return OOP.FromSmallInteger(hash);
    }

    /// <summary>
    /// Primitive 106: String asSymbol.
    /// Converts a string to a unique symbol.
    /// </summary>
    /// <param name="receiver">The string to convert.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The interned symbol.</returns>
    public static PrimitiveResult PrimitiveAsSymbol(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var obj = context.Memory.GetObject(receiver);
        if (obj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Must be byte-indexable
        if (!obj.IsByteIndexable)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Extract string from bytes
        char[] chars = new char[obj.Fields.Length];
        for (int i = 0; i < obj.Fields.Length; i++)
        {
            if (!obj.Fields[i].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            
            int byteValue = obj.Fields[i].SmallIntegerValue;
            if (byteValue < 0 || byteValue > 255)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            chars[i] = (char)byteValue;
        }
        
        string stringValue = new(chars);
        
        // Use the VM's symbol interning if it implements ISymbolTable
        if (context is Bootstrap.ISymbolTable symbolTable)
        {
            return symbolTable.Intern(stringValue);
        }
        
        // Fallback: primitive fails and lets Smalltalk handle it
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Helper method to create a String object from a C# string.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="value">The string value.</param>
    /// <returns>OOP of the created String object.</returns>
    public static OOP CreateString(IPrimitiveContext context, string value)
    {
        var stringClass = context.GetClass("String");
        if (stringClass.IsNil)
            return OOP.Nil;
        
        var stringOop = context.Memory.AllocateByteArray(stringClass, value.Length);
        var stringObj = context.Memory.GetObject(stringOop);
        
        if (stringObj != null)
        {
            for (int i = 0; i < value.Length; i++)
            {
                stringObj.Fields[i] = OOP.FromSmallInteger(value[i]);
            }
        }
        
        return stringOop;
    }

    /// <summary>
    /// Helper method to extract a C# string from a String object.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <param name="stringOop">The String object OOP.</param>
    /// <returns>The extracted string, or null if invalid.</returns>
    public static string? ExtractString(IPrimitiveContext context, OOP stringOop)
    {
        var obj = context.Memory.GetObject(stringOop);
        if (obj == null || !obj.IsByteIndexable)
            return null;
        
        char[] chars = new char[obj.Fields.Length];
        for (int i = 0; i < obj.Fields.Length; i++)
        {
            if (!obj.Fields[i].IsSmallInteger)
                return null;
            
            int byteValue = obj.Fields[i].SmallIntegerValue;
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