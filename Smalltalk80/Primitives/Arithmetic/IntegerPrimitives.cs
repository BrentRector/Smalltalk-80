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
    /// Divides the first integer on the stack by the second. The primitive
    /// succeeds only when the division is exact.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the result is an exact integer.</returns>
    public static bool Divide(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi || bi == 0)
            return false;

        if (ai % bi != 0)
            return false;

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
    /// <summary>
    /// Pushes <c>true</c> if the second value is greater than the first.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool LessThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai < bi);
        return true;
    }

    /// <summary>
    /// Pushes <c>true</c> if the first value is greater than the second.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool GreaterThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai > bi);
        return true;
    }

    /// <summary>
    /// Pushes <c>true</c> if the first value is less than or equal to the second.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool LessOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai <= bi);
        return true;
    }

    /// <summary>
    /// Pushes <c>true</c> if the first value is greater than or equal to the second.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool GreaterOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai >= bi);
        return true;
    }

    /// <summary>
    /// Pushes a boolean indicating whether the two values are not equal.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool NotEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        ctx.Stack.Push(!object.Equals(a, b));
        return true;
    }

    /// <summary>
    /// Pushes the remainder of dividing the first integer by the second.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool Mod(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi || bi == 0) return false;
        ctx.Stack.Push(ai % bi);
        return true;
    }

    /// <summary>
    /// Performs rounding division of two integers, rounding toward negative
    /// infinity.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the operation succeeded.</returns>
    public static bool Div(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi || bi == 0)
            return false;
        ctx.Stack.Push((int)Math.Floor((double)ai / bi));
        return true;
    }

    /// <summary>
    /// Performs truncated division of two integers, rounding toward zero.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the operation succeeded.</returns>
    public static bool Quo(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi || bi == 0)
            return false;
        ctx.Stack.Push(ai / bi);
        return true;
    }

    /// <summary>
    /// Performs a bitwise AND of two integers.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool BitAnd(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai & bi);
        return true;
    }

    /// <summary>
    /// Performs a bitwise OR of two integers.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool BitOr(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai | bi);
        return true;
    }

    /// <summary>
    /// Performs a bitwise exclusive-OR of two integers.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool BitXor(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai ^ bi);
        return true;
    }

    /// <summary>
    /// Shifts the first integer by the amount specified in the second.
    /// Positive values shift left; negative values shift right.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    public static bool BitShift(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(bi >= 0 ? ai << bi : ai >> -bi);
        return true;
    }

    /// <summary>
    /// Constructs a simple point-like object from two numeric values.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <returns><c>true</c> if the point was created.</returns>
    public static bool MakePoint(Context ctx)
    {
        object? y = ctx.Stack.Pop();
        object? x = ctx.Stack.Pop();
        var point = new SmalltalkObject(null, 2);
        point.Fields[0] = x;
        point.Fields[1] = y;
        ctx.Stack.Push(point);
        return true;
    }
}
