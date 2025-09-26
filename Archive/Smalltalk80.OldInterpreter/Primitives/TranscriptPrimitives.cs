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
    /// Registers all Transcript-related primitives with the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register primitives with.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(250, "primitiveWriteChar", PrimitiveWriteChar);
        table.Register(251, "primitiveFlushOutput", PrimitiveFlushOutput);
        table.Register(252, "primitiveClearConsole", PrimitiveClearConsole);
    }
}