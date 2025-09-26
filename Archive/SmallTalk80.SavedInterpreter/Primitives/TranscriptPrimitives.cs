using System;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Implements Transcript-related primitives for console output (primitives 250-252).
/// These primitives connect Smalltalk's TextCollector class to the C# Console.
/// </summary>
public static class TranscriptPrimitives
{
    /// <summary>
    /// Primitive 250: Write a character to the console.
    /// Accepts either a Character object or a SmallInteger character code.
    /// </summary>
    /// <param name="receiver">The TextCollector receiver.</param>
    /// <param name="arguments">Single argument: character or character code.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver on success or failure.</returns>
    public static PrimitiveResult PrimitiveWriteChar(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var arg = arguments[0];
        
        try
        {
            char charToWrite;
            
            // Handle SmallInteger character code
            if (arg.IsSmallInteger)
            {
                int charCode = arg.SmallIntegerValue;
                if (charCode < 0 || charCode > 127)
                {
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
                }
                charToWrite = (char)charCode;
            }
            // Handle Character object
            else if (!arg.IsNil)
            {
                var charObj = context.Memory.GetObject(arg);
                if (charObj == null || charObj.Fields.Length == 0)
                {
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
                }
                
                // Character objects have a 'value' field at index 0 with the character code
                var valueField = charObj.Fields[0];
                if (!valueField.IsSmallInteger)
                {
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
                }
                
                int code = valueField.SmallIntegerValue;
                if (code < 0 || code > 127)
                {
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
                }
                charToWrite = (char)code;
            }
            else
            {
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            }
            
            // Write the character to the console
            Console.Write(charToWrite);
            
            // Return the receiver (successful primitive returns receiver)
            return receiver;
        }
        catch
        {
            // If console operations fail (e.g., redirected output issues)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }
    
    /// <summary>
    /// Primitive 251: Flush console output.
    /// Ensures all buffered output is written to the console.
    /// </summary>
    /// <param name="receiver">The TextCollector receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver on success or failure.</returns>
    public static PrimitiveResult PrimitiveFlushOutput(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        try
        {
            // Flush the console output
            Console.Out.Flush();
            
            // Return the receiver (successful primitive returns receiver)
            return receiver;
        }
        catch
        {
            // If flush fails for any reason
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }
    
    /// <summary>
    /// Primitive 252: Clear the console.
    /// Clears the console screen if possible.
    /// </summary>
    /// <param name="receiver">The TextCollector receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver on success or failure.</returns>
    public static PrimitiveResult PrimitiveClearConsole(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        try
        {
            // Try to clear the console
            // This may fail if output is redirected or in certain environments
            Console.Clear();
            
            // Return the receiver (successful primitive returns receiver)
            return receiver;
        }
        catch
        {
            // Clear operation failed (common in redirected output scenarios)
            // Still return success but without clearing
            return receiver;
        }
    }
    
    /// <summary>
    /// Primitive 253: Write a complete string to the console.
    /// Accepts a String object and writes its entire contents to the console.
    /// </summary>
    /// <param name="receiver">The TextCollector receiver.</param>
    /// <param name="arguments">Single argument: String to write.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver on success or failure.</returns>
    public static PrimitiveResult PrimitiveWriteString(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var stringArg = arguments[0];
        
        try
        {
            // Extract string from OOP
            string? stringToWrite = ExtractString(stringArg, context);
            
            if (stringToWrite == null)
            {
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            }
            
            // Write the entire string to the console
            Console.Write(stringToWrite);
            
            // Return the receiver (successful primitive returns receiver)
            return receiver;
        }
        catch
        {
            // If console operations fail (e.g., redirected output issues)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }
    
    /// <summary>
    /// Extracts a .NET string from a Smalltalk String or Symbol OOP.
    /// </summary>
    /// <param name="stringOop">The OOP representing a Smalltalk String or Symbol.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The extracted string, or null if extraction failed.</returns>
    private static string? ExtractString(OOP stringOop, IPrimitiveContext context)
    {
        if (stringOop.IsNil || stringOop.IsSmallInteger)
            return null;
        
        var stringObj = context.Memory.GetObject(stringOop);
        if (stringObj == null)
            return null;
        
        // Check if it's a byte-indexed object (String or Symbol)
        if (stringObj.IsBytes)
        {
            // For byte arrays, the bytes are packed into OOP fields
            // Each OOP field (32 bits) holds 4 bytes
            var byteCount = stringObj.Header.Size;
            var chars = new char[byteCount];
            
            // Extract bytes from the packed OOP fields
            for (int i = 0; i < byteCount; i++)
            {
                int fieldIndex = i / 4;  // Which OOP field contains this byte
                int byteOffset = i % 4;  // Which byte within that field
                
                if (fieldIndex < stringObj.Fields.Length)
                {
                    uint fieldValue = stringObj.Fields[fieldIndex].RawValue;
                    // Extract the specific byte from the 32-bit field
                    byte byteValue = (byte)((fieldValue >> (byteOffset * 8)) & 0xFF);
                    chars[i] = (char)byteValue;
                }
            }
            return new string(chars);
        }
        
        return null;
    }
    
    /// <summary>
    /// Registers all Transcript-related primitives with the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register primitives with.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(250, "primitiveWriteChar", PrimitiveWriteChar);
        table.Register(251, "primitiveFlushOutput", PrimitiveFlushOutput);
        table.Register(252, "primitiveClearConsole", PrimitiveClearConsole);
        table.Register(253, "primitiveWriteString", PrimitiveWriteString);
    }
}