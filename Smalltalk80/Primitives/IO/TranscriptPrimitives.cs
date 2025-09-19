using System;

namespace Smalltalk80;

/// <summary>
/// Implements Transcript-related primitives for console output.
/// These primitives connect Smalltalk's TextCollector class to the C# Console.
/// </summary>
public static class TranscriptPrimitives
{
    /// <summary>
    /// Primitive 250: Write a character to the console.
    /// Accepts either a Character object or a SmallInteger character code.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    public static bool WriteChar(Context ctx)
    {
        // Pop the argument (character or character code)
        var arg = ctx.Stack.Pop();
        
        // Pop the receiver
        var receiver = ctx.Stack.Pop();
        
        if (arg == null)
        {
            // Push receiver back and fail
            ctx.Stack.Push(receiver);
            return false;
        }
        
        try
        {
            char charToWrite;
            
            // Handle SmallInteger character code
            if (arg is int charCode)
            {
                if (charCode < 0 || charCode > 127)
                {
                    // Invalid character code
                    ctx.Stack.Push(receiver);
                    return false;
                }
                charToWrite = (char)charCode;
            }
            // Handle Character object
            else if (arg is SmalltalkObject charObj)
            {
                // Character objects have a 'value' field at index 0 with the character code
                if (charObj.Fields == null || charObj.Fields.Length == 0)
                {
                    ctx.Stack.Push(receiver);
                    return false;
                }
                
                // Get the character code from the Character object
                if (charObj.Fields[0] is int code)
                {
                    if (code < 0 || code > 127)
                    {
                        ctx.Stack.Push(receiver);
                        return false;
                    }
                    charToWrite = (char)code;
                }
                else
                {
                    ctx.Stack.Push(receiver);
                    return false;
                }
            }
            else
            {
                // Unsupported argument type
                ctx.Stack.Push(receiver);
                return false;
            }
            
            // Write the character to the console
            Console.Write(charToWrite);
            
            // Push the receiver back (successful primitive returns receiver)
            ctx.Stack.Push(receiver);
            return true;
        }
        catch
        {
            // If console operations fail (e.g., redirected output issues)
            ctx.Stack.Push(receiver);
            return false;
        }
    }
    
    /// <summary>
    /// Primitive 251: Flush console output.
    /// Ensures all buffered output is written to the console.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    public static bool FlushOutput(Context ctx)
    {
        // Pop the receiver
        var receiver = ctx.Stack.Pop();
        
        try
        {
            // Flush the console output
            Console.Out.Flush();
            
            // Push the receiver back (successful primitive returns receiver)
            ctx.Stack.Push(receiver);
            return true;
        }
        catch
        {
            // If flush fails for any reason
            ctx.Stack.Push(receiver);
            return false;
        }
    }
    
    /// <summary>
    /// Primitive 252: Clear the console.
    /// Clears the console screen if possible.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    /// <returns><c>true</c> if successful; <c>false</c> otherwise.</returns>
    public static bool ClearConsole(Context ctx)
    {
        // Pop the receiver
        var receiver = ctx.Stack.Pop();
        
        try
        {
            // Try to clear the console
            // This may fail if output is redirected or in certain environments
            Console.Clear();
            
            // Push the receiver back (successful primitive returns receiver)
            ctx.Stack.Push(receiver);
            return true;
        }
        catch
        {
            // Clear operation failed (common in redirected output scenarios)
            // Still return success but without clearing
            ctx.Stack.Push(receiver);
            return true;
        }
    }
}