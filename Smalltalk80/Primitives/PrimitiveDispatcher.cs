using System;
using System.Collections.Generic;
using Smalltalk80;

/// <summary>
/// Provides a dispatcher for built-in primitive operations invoked by compiled methods.
/// </summary>
public static class Primitives
{
    /// <summary>Delegate representing a primitive implementation.</summary>
    /// <param name="context">The current execution context.</param>
    /// <returns><c>true</c> if the primitive handled the call.</returns>
    private delegate bool PrimitiveHandler(Context context);

    /// <summary>Table mapping primitive indices to their handlers.</summary>
    private static readonly Dictionary<int, PrimitiveHandler> Handlers = new()
    {
        [1] = IntegerPrimitives.Add,
        [2] = IntegerPrimitives.Subtract,
        [3] = IntegerPrimitives.Multiply,
        [4] = IntegerPrimitives.Divide,
        [5] = IntegerPrimitives.Equals
    };

    static Primitives()
    {
        // Fill remaining required primitive slots with stubs returning false
        for (var i = 6; i <= 100; i++)
        {
            if (!Handlers.ContainsKey(i))
            {
                Handlers[i] = static _ => false;
            }
        }
    }

    /// <summary>
    /// Executes the primitive with the specified index.
    /// </summary>
    /// <param name="index">The primitive index.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns><c>true</c> if the primitive handled the call; otherwise <c>false</c>.</returns>
    public static bool Execute(int index, Context context)
        => Handlers.TryGetValue(index, out var handler) && handler(context);
}
