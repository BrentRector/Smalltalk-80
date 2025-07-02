using System;

namespace Smalltalk80;

/// <summary>
/// Provides primitive operations for Strings and Streams.
/// </summary>
public static class ArrayPrimitives
{
    /// <summary>
    /// Retrieves a character from the given char array using a one-based index.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the operation succeeds.</returns>
    public static bool StringAt(Context ctx)
    {
        if (ctx.Stack.Pop() is not int index || ctx.Stack.Pop() is not char[] array)
            return false;
        if (index < 1 || index > array.Length)
            return false;
        ctx.Stack.Push(array[index - 1]);
        return true;
    }

    /// <summary>
    /// Stores a character into the given char array using a one-based index.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the character was stored.</returns>
    public static bool StringAtPut(Context ctx)
    {
        var valueObj = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not int index || ctx.Stack.Pop() is not char[] array)
            return false;
        if (valueObj is not char value)
            return false;
        if (index < 1 || index > array.Length)
            return false;
        array[index - 1] = value;
        ctx.Stack.Push(value);
        return true;
    }

    /// <summary>
    /// Reads the next element from a stream backed by an array.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if a value was read.</returns>
    public static bool Next(Context ctx)
    {
        if (ctx.Stack.Pop() is not StreamObject stream)
            return false;
        var array = stream.Array;
        int index = stream.Index;
        if (index >= stream.ReadLimit || index >= array.Fields.Length)
            return false;
        index++;
        stream.Index = index;
        ctx.Stack.Push(array.Fields[index - 1]);
        return true;
    }

    /// <summary>
    /// Writes the next element to a stream backed by an array.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the value was written.</returns>
    public static bool NextPut(Context ctx)
    {
        var value = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not StreamObject stream)
            return false;
        var array = stream.Array;
        int index = stream.Index;
        if (index >= stream.WriteLimit || index >= array.Fields.Length)
            return false;
        index++;
        array.Fields[index - 1] = value;
        stream.Index = index;
        ctx.Stack.Push(value);
        return true;
    }

    /// <summary>
    /// Determines whether a stream is at or beyond its read limit.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the end has been reached.</returns>
    public static bool AtEnd(Context ctx)
    {
        if (ctx.Stack.Pop() is not StreamObject stream)
            return false;
        var array = stream.Array;
        bool atEnd = stream.Index >= stream.ReadLimit || stream.Index >= array.Fields.Length;
        ctx.Stack.Push(atEnd);
        return true;
    }
}
