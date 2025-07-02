using System;

namespace Smalltalk80;

/// <summary>
/// Provides primitive implementations for floating point operations.
/// </summary>
public static class FloatPrimitives
{
    /// <summary>Converts an integer value to a floating point number.</summary>
    public static bool AsFloat(Context ctx)
    {
        object? a = ctx.Stack.Pop();
        if (a is not int ai) return false;
        ctx.Stack.Push((double)ai);
        return true;
    }

    /// <summary>Adds two <see cref="double"/> values.</summary>
    public static bool Add(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad + bd);
        return true;
    }

    /// <summary>Subtracts the second value from the first.</summary>
    public static bool Subtract(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad - bd);
        return true;
    }

    /// <summary>Multiplies two floating point numbers.</summary>
    public static bool Multiply(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad * bd);
        return true;
    }

    /// <summary>Divides the first value by the second.</summary>
    public static bool Divide(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd || bd == 0.0) return false;
        double result = ad / bd;
        if (double.IsInfinity(result) || double.IsNaN(result)) return false;
        ctx.Stack.Push(result);
        return true;
    }

    /// <summary>Returns <c>true</c> if the two values are equal.</summary>
    public static bool Equals(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad.Equals(bd));
        return true;
    }

    /// <summary>Returns <c>true</c> if the values are not equal.</summary>
    public static bool NotEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(!ad.Equals(bd));
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is less than the second.</summary>
    public static bool LessThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad < bd);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is greater than the second.</summary>
    public static bool GreaterThan(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad > bd);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is less than or equal to the second.</summary>
    public static bool LessOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad <= bd);
        return true;
    }

    /// <summary>Returns <c>true</c> if the first value is greater than or equal to the second.</summary>
    public static bool GreaterOrEqual(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || b is not double bd) return false;
        ctx.Stack.Push(ad >= bd);
        return true;
    }

    /// <summary>Returns the integer part of the receiver.</summary>
    public static bool Truncated(Context ctx)
    {
        object? a = ctx.Stack.Pop();
        if (a is not double ad) return false;
        double truncated = Math.Truncate(ad);
        if (truncated < int.MinValue || truncated > int.MaxValue) return false;
        ctx.Stack.Push((int)truncated);
        return true;
    }

    /// <summary>Returns the fractional part of the receiver.</summary>
    public static bool FractionalPart(Context ctx)
    {
        object? a = ctx.Stack.Pop();
        if (a is not double ad) return false;
        ctx.Stack.Push(ad - Math.Truncate(ad));
        return true;
    }

    /// <summary>Returns the exponent of the receiver in base two.</summary>
    public static bool Exponent(Context ctx)
    {
        object? a = ctx.Stack.Pop();
        if (a is not double ad || ad == 0.0) return false;
        ctx.Stack.Push(Math.ILogB(ad));
        return true;
    }

    /// <summary>Multiplies the receiver by two raised to the specified power.</summary>
    public static bool TimesTwoPower(Context ctx)
    {
        object? expObj = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not double ad || expObj is not int exp) return false;
        ctx.Stack.Push(ad * Math.Pow(2, exp));
        return true;
    }
}
