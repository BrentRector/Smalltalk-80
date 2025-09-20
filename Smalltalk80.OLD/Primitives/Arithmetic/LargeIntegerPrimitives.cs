using System;
using System.Numerics;

namespace Smalltalk80;

/// <summary>
/// Provides primitive implementations for operations on large integers.
/// </summary>
public static class LargeIntegerPrimitives
{
    /// <summary>Performs addition on two <see cref="BigInteger"/> values.</summary>
    public static bool Add(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai + bi);
        return true;
    }

    /// <summary>Subtracts the second <see cref="BigInteger"/> from the first.</summary>
    public static bool Subtract(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai - bi);
        return true;
    }

    /// <summary>Multiplies two <see cref="BigInteger"/> values.</summary>
    public static bool Multiply(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai * bi);
        return true;
    }

    /// <summary>
    /// Divides the first value by the second when the division is exact.
    /// </summary>
    public static bool Divide(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi || bi.IsZero)
            return false;
        if (ai % bi != BigInteger.Zero) return false;
        ctx.Stack.Push(ai / bi);
        return true;
    }

    /// <summary>Rounds the quotient toward negative infinity.</summary>
    public static bool Div(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi || bi.IsZero)
            return false;
        var result = (BigInteger)Math.Floor((double)ai / (double)bi);
        ctx.Stack.Push(result);
        return true;
    }

    /// <summary>Truncated integer division rounding toward zero.</summary>
    public static bool Quo(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi || bi.IsZero)
            return false;
        ctx.Stack.Push(ai / bi);
        return true;
    }

    /// <summary>Computes the remainder of a division.</summary>
    public static bool Mod(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi || bi.IsZero)
            return false;
        ctx.Stack.Push(ai % bi);
        return true;
    }

    /// <summary>Bitwise AND of two values.</summary>
    public static bool BitAnd(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai & bi);
        return true;
    }

    /// <summary>Bitwise OR of two values.</summary>
    public static bool BitOr(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai | bi);
        return true;
    }

    /// <summary>Bitwise exclusive-OR of two values.</summary>
    public static bool BitXor(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai ^ bi);
        return true;
    }

    /// <summary>Shifts the first value by the specified number of bits.</summary>
    public static bool BitShift(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not int shift) return false;
        ctx.Stack.Push(shift >= 0 ? ai << shift : ai >> -shift);
        return true;
    }

    /// <summary>Returns <c>true</c> if the two values are equal.</summary>
    public static bool Equals(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai == bi);
        return true;
    }

    /// <summary>Returns <c>true</c> if the values are not equal.</summary>
    public static bool NotEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai != bi);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is less than the second.</summary>
    public static bool LessThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai < bi);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is greater than the second.</summary>
    public static bool GreaterThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai > bi);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is less than or equal to the second.</summary>
    public static bool LessOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai <= bi);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is greater than or equal to the second.</summary>
    public static bool GreaterOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not BigInteger ai || b is not BigInteger bi) return false;
        ctx.Stack.Push(ai >= bi);
        return true;
    }
}
