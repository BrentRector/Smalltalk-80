using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Provides primitive operations for Dictionary objects, especially for the Smalltalk global dictionary.
/// </summary>
public static class DictionaryPrimitives
{
    /// <summary>
    /// Primitive for Dictionary>>at: - retrieves value for a key.
    /// Used by Smalltalk at: #GlobalName
    /// </summary>
    public static bool DictionaryAt(Context ctx)
    {
        if (ctx.Stack.Pop() is not Symbol key)
            return false;
        if (ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
            
        // Check if this is the Smalltalk global dictionary
        if (receiver == SystemDictionary.GetSmalltalk())
        {
            var dict = SystemDictionary.GetSmalltalkDictionary();
            if (dict.TryGetValue(key, out var value))
            {
                ctx.Stack.Push(value);
                return true;
            }
            
            // Key not found - let Smalltalk handle it
            return false;
        }
        
        // For other dictionaries, use the regular instance variable storage
        // This is a simplified implementation - full Dictionary support would need more work
        return false;
    }
    
    /// <summary>
    /// Primitive for Dictionary>>at:put: - stores value for a key.
    /// Used by Smalltalk at: #GlobalName put: anObject
    /// </summary>
    public static bool DictionaryAtPut(Context ctx)
    {
        var value = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not Symbol key)
            return false;
        if (ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
            
        // Check if this is the Smalltalk global dictionary
        if (receiver == SystemDictionary.GetSmalltalk())
        {
            var dict = SystemDictionary.GetSmalltalkDictionary();
            dict[key] = value!;
            ctx.Stack.Push(value); // at:put: returns the value
            
            Console.WriteLine($"[DICTIONARY] Stored {key.Value} in Smalltalk global dictionary");
            return true;
        }
        
        // For other dictionaries, use the regular instance variable storage
        // This is a simplified implementation
        return false;
    }
    
    /// <summary>
    /// Primitive for Dictionary>>at:ifAbsent: - retrieves value or evaluates block.
    /// </summary>
    public static bool DictionaryAtIfAbsent(Context ctx)
    {
        var absentBlock = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not Symbol key)
            return false;
        if (ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
            
        // Check if this is the Smalltalk global dictionary
        if (receiver == SystemDictionary.GetSmalltalk())
        {
            var dict = SystemDictionary.GetSmalltalkDictionary();
            if (dict.TryGetValue(key, out var value))
            {
                ctx.Stack.Push(value);
                return true;
            }
            
            // Key not found - need to evaluate the absent block
            // This would require sending value to the block
            // For now, let the Smalltalk code handle it
            return false;
        }
        
        return false;
    }
    
    /// <summary>
    /// Primitive for Dictionary>>includesKey: - checks if key exists.
    /// </summary>
    public static bool DictionaryIncludesKey(Context ctx)
    {
        if (ctx.Stack.Pop() is not Symbol key)
            return false;
        if (ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
            
        // Check if this is the Smalltalk global dictionary
        if (receiver == SystemDictionary.GetSmalltalk())
        {
            var dict = SystemDictionary.GetSmalltalkDictionary();
            var exists = dict.ContainsKey(key);
            
            // Push true or false
            if (SystemDictionary.TryGetClass(exists ? "True" : "False", out var boolClass))
            {
                ctx.Stack.Push(boolClass);
            }
            else
            {
                ctx.Stack.Push(exists);
            }
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Primitive for Dictionary>>removeKey: - removes a key-value pair.
    /// </summary>
    public static bool DictionaryRemoveKey(Context ctx)
    {
        if (ctx.Stack.Pop() is not Symbol key)
            return false;
        if (ctx.Stack.Pop() is not SmalltalkObject receiver)
            return false;
            
        // Check if this is the Smalltalk global dictionary
        if (receiver == SystemDictionary.GetSmalltalk())
        {
            var dict = SystemDictionary.GetSmalltalkDictionary();
            if (dict.TryGetValue(key, out var value))
            {
                dict.Remove(key);
                ctx.Stack.Push(value); // removeKey: returns the removed value
                return true;
            }
            
            // Key not found - let Smalltalk handle the error
            return false;
        }
        
        return false;
    }
}