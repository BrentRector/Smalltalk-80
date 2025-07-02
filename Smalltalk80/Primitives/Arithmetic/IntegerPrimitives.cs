using System;

namespace Smalltalk80;

/// <summary>
/// Provides arithmetic primitive implementations for <see cref="SmallInteger"/> operations.
/// </summary>
public static class IntegerPrimitives
{
    /// <summary>
    /// Adds two integers from the stack.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the operation succeeded.</returns>
    public static bool Add(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai + bi);
        return true;
    }

    /// <summary>
    /// Subtracts the second integer on the stack from the first.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Subtract(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai - bi);
        return true;
    }

    /// <summary>
    /// Multiplies two integers from the stack.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Multiply(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai * bi);
        return true;
    }

    /// <summary>
    /// Divides the first integer on the stack by the second.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Divide(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi || bi == 0) return false;
        ctx.Stack.Push(ai / bi);
        return true;
    }

    /// <summary>
    /// Pushes a boolean indicating whether the two values are equal.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Equals(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        ctx.Stack.Push(object.Equals(a, b));
        return true;
    }
}
