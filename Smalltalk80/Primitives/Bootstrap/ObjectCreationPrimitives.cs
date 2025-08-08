using System;

namespace Smalltalk80.Primitives.Bootstrap;

/// <summary>
/// Primitives for creating objects via message sends.
/// These primitives support the Phase 1 bootstrap where all objects are created through class messages.
/// </summary>
public static class ObjectCreationPrimitives
{
    /// <summary>
    /// Primitive 180: Create a SmallInteger with a given value.
    /// Stack: [receiver (SmallInteger class), value] -> [new SmallInteger]
    /// </summary>
    public static bool NewSmallInteger(Context ctx)
    {
        // Pop the value argument
        var valueArg = ctx.Stack.Pop();
        
        // Pop the receiver (SmallInteger class)
        var receiver = ctx.Stack.Pop();
        
        // Extract the integer value
        int value;
        if (valueArg is int intValue)
        {
            value = intValue;
        }
        else if (valueArg is SmalltalkObject obj && obj.Fields.Length > 0 && obj.Fields[0] is int objValue)
        {
            value = objValue;
        }
        else
        {
            // Can't extract value, primitive fails
            ctx.Stack.Push(receiver); // Restore receiver
            ctx.Stack.Push(valueArg); // Restore argument
            return false;
        }
        
        // Create a new SmallInteger object
        // The receiver should be the SmallInteger class
        SmalltalkObject? smallIntegerClass = receiver as SmalltalkObject;
        if (smallIntegerClass == null)
        {
            // Receiver is not a valid class
            ctx.Stack.Push(receiver);
            ctx.Stack.Push(valueArg);
            return false;
        }
        
        // Create the SmallInteger instance
        var smallInt = new SmalltalkObject(smallIntegerClass, 1);
        smallInt.SetField(0, value);
        
        // Push the result
        ctx.Stack.Push(smallInt);
        return true;
    }
    
    /// <summary>
    /// Primitive 181: Create or return an interned Symbol.
    /// Stack: [receiver (Symbol class), string] -> [Symbol instance]
    /// </summary>
    public static bool NewSymbol(Context ctx)
    {
        // Pop the string argument
        var stringArg = ctx.Stack.Pop();
        
        // Pop the receiver (Symbol class)
        var receiver = ctx.Stack.Pop();
        
        // Extract the string value
        string? stringValue = null;
        if (stringArg is string str)
        {
            stringValue = str;
        }
        else if (stringArg is SmalltalkObject obj && obj.Fields.Length > 0)
        {
            if (obj.Fields[0] is string objStr)
            {
                stringValue = objStr;
            }
        }
        
        if (stringValue == null)
        {
            // Can't extract string, primitive fails
            ctx.Stack.Push(receiver);
            ctx.Stack.Push(stringArg);
            return false;
        }
        
        // The receiver should be the Symbol class
        SmalltalkObject? symbolClass = receiver as SmalltalkObject;
        if (symbolClass == null)
        {
            ctx.Stack.Push(receiver);
            ctx.Stack.Push(stringArg);
            return false;
        }
        
        // TODO: Implement symbol interning table
        // For now, just create a new Symbol instance
        var symbol = new SmalltalkObject(symbolClass, 1);
        symbol.SetField(0, stringValue);
        
        // Push the result
        ctx.Stack.Push(symbol);
        return true;
    }
    
    /// <summary>
    /// Primitive 182: Create a new String of given size.
    /// Stack: [receiver (String class), size] -> [String instance]
    /// </summary>
    public static bool NewString(Context ctx)
    {
        // Pop the size argument
        var sizeArg = ctx.Stack.Pop();
        
        // Pop the receiver (String class)
        var receiver = ctx.Stack.Pop();
        
        // Extract the size value
        int size;
        if (sizeArg is int intSize)
        {
            size = intSize;
        }
        else if (sizeArg is SmalltalkObject obj && obj.Fields.Length > 0 && obj.Fields[0] is int objSize)
        {
            size = objSize;
        }
        else
        {
            // Can't extract size, primitive fails
            ctx.Stack.Push(receiver);
            ctx.Stack.Push(sizeArg);
            return false;
        }
        
        // The receiver should be the String class
        SmalltalkObject? stringClass = receiver as SmalltalkObject;
        if (stringClass == null)
        {
            ctx.Stack.Push(receiver);
            ctx.Stack.Push(sizeArg);
            return false;
        }
        
        // Create a new String instance
        // For now, we'll create it with a single field containing an empty string
        // In a full implementation, this would be a character array
        var str = new SmalltalkObject(stringClass, 1);
        str.SetField(0, new string(' ', size));
        
        // Push the result
        ctx.Stack.Push(str);
        return true;
    }
}