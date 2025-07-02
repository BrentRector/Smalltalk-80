using System;

namespace Smalltalk80;

/// <summary>
/// Provides primitive operations for generic Smalltalk objects.
/// </summary>
public static class ObjectPrimitives
{
    /// <summary>
    /// Pushes <c>true</c> if the two popped values reference the same object.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Equivalent(Context ctx)
    {
        var b = ctx.Stack.Pop();
        var a = ctx.Stack.Pop();
        ctx.Stack.Push(ReferenceEquals(a, b));
        return true;
    }

    /// <summary>
    /// Pushes the class of the popped object instance.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Class(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        ctx.Stack.Push(obj.ClassPointer);
        return true;
    }

    /// <summary>
    /// Reads the indexed field of a <see cref="SmalltalkObject"/>.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool At(Context ctx)
    {
        if (ctx.Stack.Pop() is not int index ||
            ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        if (index < 1 || index > obj.Fields.Length)
            return false;
        ctx.Stack.Push(obj.Fields[index - 1]);
        return true;
    }

    /// <summary>
    /// Stores a value into the indexed field of a <see cref="SmalltalkObject"/>.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool AtPut(Context ctx)
    {
        var value = ctx.Stack.Pop();
        if (ctx.Stack.Pop() is not int index ||
            ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        if (index < 1 || index > obj.Fields.Length)
            return false;
        obj.Fields[index - 1] = value;
        ctx.Stack.Push(value);
        return true;
    }

    /// <summary>
    /// Pushes the number of indexable fields in a <see cref="SmalltalkObject"/>.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Size(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject obj)
            return false;
        ctx.Stack.Push(obj.Fields.Length);
        return true;
    }
}
