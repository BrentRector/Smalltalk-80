using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Arithmetic primitive operations for SmallInteger (Primitives 1-17).
/// These are the fundamental arithmetic operations optimized for tagged integers.
/// </summary>
public static class ArithmeticPrimitives
{
    // Primitive 1: SmallInteger +
    public static SmalltalkOOP Add(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            int result = checked(a + b);
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil; // Fall back to LargeInteger method
        }
    }

    // Primitive 2: SmallInteger -
    public static SmalltalkOOP Subtract(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            int result = checked(a - b);
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 3: SmallInteger <
    public static SmalltalkOOP LessThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a < b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 4: SmallInteger >
    public static SmalltalkOOP GreaterThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a > b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 5: SmallInteger <=
    public static SmalltalkOOP LessOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a <= b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 6: SmallInteger >=
    public static SmalltalkOOP GreaterOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a >= b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 7: SmallInteger =
    public static SmalltalkOOP Equal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a == b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 8: SmallInteger ~=
    public static SmalltalkOOP NotEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a != b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 9: SmallInteger *
    public static SmalltalkOOP Multiply(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            int result = checked(a * b);
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 10: SmallInteger /
    public static SmalltalkOOP Divide(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int result = a / b;
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 11: SmallInteger \\
    public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // C# % gives remainder with sign of dividend
            int rem = a % b;

            // Smalltalk \\ (mod) has remainder sign follow divisor
            // Blue Book: a \\ b has same sign as b
            // Adjust when signs differ and remainder is non-zero
            if (rem != 0 && ((a < 0) != (b < 0)))
                rem += b;

            return SmalltalkOOP.FromSmallInteger(rem);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 12: SmallInteger //
    public static SmalltalkOOP Quo(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            int result = a / b;
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 13: SmallInteger bitAnd:
    public static SmalltalkOOP BitAnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a & b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // Primitive 14: SmallInteger bitOr:
    public static SmalltalkOOP BitOr(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a | b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // Primitive 15: SmallInteger bitXor:
    public static SmalltalkOOP BitXor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a ^ b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // Primitive 16: SmallInteger bitShift:
    public static SmalltalkOOP BitShift(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int shift = args[0].AsSmallInteger();

        // SmallInteger is 30-bit; limit shifts to safe range
        // Shifts >= 30 bits will overflow SmallInteger range
        // Shifts >= 32 bits have undefined behavior in C#
        // Fail primitive for large shifts to allow fallback to LargeInteger
        if (shift > 30 || shift < -30)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Safe to shift since we validated bounds above
            int result = shift >= 0 ? (a << shift) : (a >> (-shift));
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 17: SmallInteger div:
    // Blue Book semantics: Floor division (toward -infinity)
    // Different from quo: which truncates toward zero
    public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Floor division: always round toward -infinity
            int quot = a / b;  // C# truncates toward zero
            int rem = a % b;

            // Adjust quotient if signs differ and remainder is non-zero
            // Examples: -7 div: 3 = -3 (not -2), 7 div: -3 = -3 (not -2)
            if (rem != 0 && ((a < 0) != (b < 0)))
                quot--;

            return SmalltalkOOP.FromSmallInteger(quot);
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }
}