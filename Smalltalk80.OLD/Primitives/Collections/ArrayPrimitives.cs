using System;
using System.Text;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Primitives.Collections;

/// <summary>
/// Provides primitive operations for arrays, strings, and characters.
/// </summary>
public static class ArrayPrimitives
{
    /// <summary>
    /// Primitive 158: String equality comparison.
    /// </summary>
    public static bool StringEquals(Context context)
    {
        // Pop arguments: receiver and argument
        var arg = context.Stack.Pop();
        var receiver = context.Stack.Pop();
        
        // Handle SmallInteger OOPs
        if (receiver is int recOop)
        {
            receiver = ObjectMemory.FromOop(recOop);
        }
        if (arg is int argOop)
        {
            arg = ObjectMemory.FromOop(argOop);
        }
        
        // Check if both are strings
        if (receiver is SmalltalkObject recObj && arg is SmalltalkObject argObj)
        {
            // Try to extract string values
            if (recObj.TryGetStringValue(out var recStr) && argObj.TryGetStringValue(out var argStr))
            {
                // Push boolean result
                var result = recStr == argStr;
                // Push boolean result as integer for now
                // TODO: Return actual True/False singleton objects
                context.Stack.Push(VMPrimitiveAdapter.MakeSmallInteger(result ? 1 : 0));
                return true;
            }
        }
        
        // Also handle direct string comparisons
        if (receiver is string str1 && arg is string str2)
        {
            var result = str1 == str2;
            // Push boolean result as integer for now
            // TODO: Return actual True/False singleton objects
            context.Stack.Push(VMPrimitiveAdapter.MakeSmallInteger(result ? 1 : 0));
            return true;
        }
        
        // Primitive failed - restore stack
        context.Stack.Push(receiver);
        context.Stack.Push(arg);
        return false;
    }
    
    /// <summary>
    /// Primitive 159: String less than comparison.
    /// </summary>
    public static bool StringLessThan(Context context)
    {
        // Pop arguments: receiver and argument
        var arg = context.Stack.Pop();
        var receiver = context.Stack.Pop();
        
        // Handle SmallInteger OOPs
        if (receiver is int recOop)
        {
            receiver = ObjectMemory.FromOop(recOop);
        }
        if (arg is int argOop)
        {
            arg = ObjectMemory.FromOop(argOop);
        }
        
        // Check if both are strings
        if (receiver is SmalltalkObject recObj && arg is SmalltalkObject argObj)
        {
            // Try to extract string values
            if (recObj.TryGetStringValue(out var recStr) && argObj.TryGetStringValue(out var argStr))
            {
                // Push boolean result
                var result = string.Compare(recStr, argStr, StringComparison.Ordinal) < 0;
                // Push boolean result as integer for now
                // TODO: Return actual True/False singleton objects
                context.Stack.Push(VMPrimitiveAdapter.MakeSmallInteger(result ? 1 : 0));
                return true;
            }
        }
        
        // Also handle direct string comparisons
        if (receiver is string str1 && arg is string str2)
        {
            var result = string.Compare(str1, str2, StringComparison.Ordinal) < 0;
            // Push boolean result as integer for now
            // TODO: Return actual True/False singleton objects
            context.Stack.Push(VMPrimitiveAdapter.MakeSmallInteger(result ? 1 : 0));
            return true;
        }
        
        // Primitive failed - restore stack
        context.Stack.Push(receiver);
        context.Stack.Push(arg);
        return false;
    }
    
    /// <summary>
    /// Primitive 170: Create a Character from an integer value.
    /// Note: This primitive is currently reassigned. Original index might be different.
    /// In Smalltalk-80, Character class>>value: uses this primitive.
    /// </summary>
    public static bool CharacterFromValue(Context context)
    {
        // Pop argument: integer value
        var value = context.Stack.Pop();
        
        // Extract integer value
        int charCode = 0;
        bool gotValue = false;
        
        if (value is int intVal)
        {
            // It's a SmallInteger OOP - decode it
            if (VMPrimitiveAdapter.IsSmallInteger(intVal))
            {
                charCode = VMPrimitiveAdapter.SmallIntegerValue(intVal);
                gotValue = true;
            }
        }
        else if (value is SmalltalkObject obj)
        {
            // Try to get integer value from object
            if (obj.Fields.Length > 0 && obj.Fields[0] is int intValue)
            {
                charCode = intValue;
                gotValue = true;
            }
        }
        
        if (gotValue && charCode >= 0 && charCode <= 65535)
        {
            // Create a Character object
            var characterClassOop = -1; // TODO: Look up Character class
            if (characterClassOop >= 0)
            {
                var characterClass = ObjectMemory.FromOop(characterClassOop) as SmalltalkObject;
                if (characterClass != null)
                {
                    // Create new Character instance with the value
                    var character = new SmalltalkObject(characterClass, 1);
                    character.SetField(0, charCode);
                    ObjectMemory.Register(character);
                    context.Stack.Push(character);
                    return true;
                }
            }
            
            // Fallback: create a simple object to represent character
            var simpleChar = new SmalltalkObject(null, 1);
            simpleChar.SetField(0, (char)charCode);
            context.Stack.Push(simpleChar);
            return true;
        }
        
        // Primitive failed - restore stack
        context.Stack.Push(value);
        return false;
    }
    
    /// <summary>
    /// Primitive 63: String at: index - get character at position.
    /// </summary>
    public static bool StringAt(Context context)
    {
        // Pop arguments: index and receiver
        var index = context.Stack.Pop();
        var receiver = context.Stack.Pop();
        
        // Handle SmallInteger OOP for receiver
        if (receiver is int recOop)
        {
            receiver = ObjectMemory.FromOop(recOop);
        }
        
        // Get index value
        int idx = 0;
        if (index is int indexOop && VMPrimitiveAdapter.IsSmallInteger(indexOop))
        {
            idx = VMPrimitiveAdapter.SmallIntegerValue(indexOop);
        }
        else if (index is SmalltalkObject indexObj && indexObj.Fields.Length > 0 && indexObj.Fields[0] is int intVal)
        {
            idx = intVal;
        }
        else
        {
            // Invalid index - fail primitive
            context.Stack.Push(receiver);
            context.Stack.Push(index);
            return false;
        }
        
        // Get string from receiver
        if (receiver is SmalltalkObject strObj && strObj.TryGetStringValue(out var str))
        {
            // Smalltalk uses 1-based indexing
            if (idx >= 1 && idx <= str.Length)
            {
                var ch = str[idx - 1];
                
                // Create Character object
                var characterClassOop = -1; // TODO: Look up Character class
                if (characterClassOop >= 0)
                {
                    var characterClass = ObjectMemory.FromOop(characterClassOop) as SmalltalkObject;
                    if (characterClass != null)
                    {
                        var character = new SmalltalkObject(characterClass, 1);
                        character.SetField(0, (int)ch);
                        context.Stack.Push(character);
                        return true;
                    }
                }
                
                // Fallback: push character code as SmallInteger
                context.Stack.Push(VMPrimitiveAdapter.MakeSmallInteger((int)ch));
                return true;
            }
        }
        
        // Primitive failed - restore stack
        context.Stack.Push(receiver);
        context.Stack.Push(index);
        return false;
    }
    
    /// <summary>
    /// Primitive 64: String at:put: - set character at position.
    /// </summary>
    public static bool StringAtPut(Context context)
    {
        // Pop arguments: value, index, and receiver
        var value = context.Stack.Pop();
        var index = context.Stack.Pop();
        var receiver = context.Stack.Pop();
        
        // Handle SmallInteger OOP for receiver
        if (receiver is int recOop)
        {
            receiver = ObjectMemory.FromOop(recOop);
        }
        
        // Get index value
        int idx = 0;
        if (index is int indexOop && VMPrimitiveAdapter.IsSmallInteger(indexOop))
        {
            idx = VMPrimitiveAdapter.SmallIntegerValue(indexOop);
        }
        else if (index is SmalltalkObject indexObj && indexObj.Fields.Length > 0 && indexObj.Fields[0] is int intVal)
        {
            idx = intVal;
        }
        else
        {
            // Invalid index - fail primitive
            context.Stack.Push(receiver);
            context.Stack.Push(index);
            context.Stack.Push(value);
            return false;
        }
        
        // Get character value
        char ch = '\0';
        if (value is SmalltalkObject charObj)
        {
            // It's a Character object - get its value
            if (charObj.Fields.Length > 0 && charObj.Fields[0] is int charCode)
            {
                ch = (char)charCode;
            }
            else if (charObj.Fields.Length > 0 && charObj.Fields[0] is char directChar)
            {
                ch = directChar;
            }
        }
        else if (value is int charInt)
        {
            if (VMPrimitiveAdapter.IsSmallInteger(charInt))
            {
                ch = (char)VMPrimitiveAdapter.SmallIntegerValue(charInt);
            }
        }
        else if (value is char directChar)
        {
            ch = directChar;
        }
        
        if (ch == '\0')
        {
            // Invalid character - fail primitive
            context.Stack.Push(receiver);
            context.Stack.Push(index);
            context.Stack.Push(value);
            return false;
        }
        
        // Modify string in receiver
        if (receiver is SmalltalkObject strObj)
        {
            // Strings in Smalltalk are typically immutable, but for primitives we need to support mutation
            // We'll need to handle this specially based on how strings are stored
            
            // For now, we'll fail this primitive as string mutation requires special handling
            // In a full implementation, this would modify the string's byte array representation
            context.Stack.Push(receiver);
            context.Stack.Push(index);
            context.Stack.Push(value);
            return false;
        }
        
        // Primitive failed - restore stack
        context.Stack.Push(receiver);
        context.Stack.Push(index);
        context.Stack.Push(value);
        return false;
    }
    
    /// <summary>
    /// Primitive 65: Stream next - get next element from stream.
    /// </summary>
    public static bool Next(Context context)
    {
        // This primitive is for Stream objects
        // For now, return false to use Smalltalk implementation
        return false;
    }
    
    /// <summary>
    /// Primitive 66: Stream nextPut: - write next element to stream.
    /// </summary>
    public static bool NextPut(Context context)
    {
        // This primitive is for Stream objects
        // For now, return false to use Smalltalk implementation
        return false;
    }
    
    /// <summary>
    /// Primitive 67: Stream atEnd - test if at end of stream.
    /// </summary>
    public static bool AtEnd(Context context)
    {
        // This primitive is for Stream objects
        // For now, return false to use Smalltalk implementation
        return false;
    }
}