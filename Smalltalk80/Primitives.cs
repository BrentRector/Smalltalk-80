using System;

/// <summary>
///     Contains built-in primitive operations invoked by compiled methods.
/// </summary>
public static class Primitives
{
    /// <summary>
    ///     Executes the primitive with the specified index.
    /// </summary>
    /// <param name="index">The primitive index.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns><c>true</c> if the primitive handled the call; otherwise <c>false</c>.</returns>
    public static bool Execute(int index, Context context)
    {
        switch (index) {
            case 1: return PrimitiveAdd(context);
            case 2: return PrimitiveSubtract(context);
            default: return false;
        }
    }

    private static bool PrimitiveAdd(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai + bi);
        return true;
    }

    private static bool PrimitiveSubtract(Context ctx)
    {
        object? b = ctx.Stack.Pop();
        object? a = ctx.Stack.Pop();
        if (a is not int ai || b is not int bi) return false;
        ctx.Stack.Push(ai - bi);
        return true;
    }
}