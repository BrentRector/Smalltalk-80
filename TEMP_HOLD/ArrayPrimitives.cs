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

    /// <summary>
    /// Compares two strings for equality.
    /// Used by String>>=aString primitive 158.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the comparison succeeded.</returns>
    public static bool StringEquals(Context ctx)
    {
        var other = ctx.Stack.Pop();
        var receiver = ctx.Stack.Pop();

        // Handle string arrays (char[])
        if (receiver is char[] receiverArray && other is char[] otherArray)
        {
            bool areEqual = receiverArray.Length == otherArray.Length;
            if (areEqual)
            {
                for (int i = 0; i < receiverArray.Length; i++)
                {
                    if (receiverArray[i] != otherArray[i])
                    {
                        areEqual = false;
                        break;
                    }
                }
            }
            ctx.Stack.Push(areEqual);
            return true;
        }

        // Handle string objects
        if (receiver is string receiverStr && other is string otherStr)
        {
            ctx.Stack.Push(receiverStr.Equals(otherStr));
            return true;
        }

        // Fall back to reference equality for other types
        ctx.Stack.Push(ReferenceEquals(receiver, other));
        return true;
    }

    /// <summary>
    /// Compares two strings lexicographically.
    /// Used by String&lt;aString primitive 159.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the comparison succeeded.</returns>
    public static bool StringLessThan(Context ctx)
    {
        var other = ctx.Stack.Pop();
        var receiver = ctx.Stack.Pop();

        // Handle string arrays (char[])
        if (receiver is char[] receiverArray && other is char[] otherArray)
        {
            int minLength = Math.Min(receiverArray.Length, otherArray.Length);
            for (int i = 0; i < minLength; i++)
            {
                int comparison = receiverArray[i].CompareTo(otherArray[i]);
                if (comparison < 0)
                {
                    ctx.Stack.Push(true);
                    return true;
                }
                if (comparison > 0)
                {
                    ctx.Stack.Push(false);
                    return true;
                }
            }
            // All compared characters are equal, shorter string is "less than"
            ctx.Stack.Push(receiverArray.Length < otherArray.Length);
            return true;
        }

        // Handle string objects
        if (receiver is string receiverStr && other is string otherStr)
        {
            ctx.Stack.Push(string.Compare(receiverStr, otherStr, StringComparison.Ordinal) < 0);
            return true;
        }

        // Cannot compare non-strings
        return false;
    }

    /// <summary>
    /// Creates a Character object from an ASCII value.
    /// Used by Character class>>value: primitive 170.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the character was created successfully.</returns>
    public static bool CharacterFromValue(Context ctx)
    {
        if (ctx.Stack.Pop() is not int asciiValue)
            return false;

        // Validate ASCII range (0-255)
        if (asciiValue < 0 || asciiValue > 255)
            return false;

        // Create Character object with the ASCII value
        var characterClass = SystemDictionary.GetClass("Character");
        if (characterClass == null)
            return false;

        var character = new SmalltalkObject(characterClass, 1);
        character.Fields[0] = asciiValue;
        ctx.Stack.Push(character);
        return true;
    }
}
