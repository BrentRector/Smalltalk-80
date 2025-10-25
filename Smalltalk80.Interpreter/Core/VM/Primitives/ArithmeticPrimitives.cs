using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Arithmetic primitive operations for SmallInteger (Primitives 1-17).
/// These are the fundamental arithmetic operations optimized for tagged integers.
/// SmallInteger range: -2^30 to 2^30-1 (30-bit signed integer with 2 tag bits).
/// </summary>
public static class ArithmeticPrimitives
{
    private const int SmallIntegerMin = -(1 << 30);
    private const int SmallIntegerMax = (1 << 30) - 1;

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

            // Check if result is within SmallInteger range (30-bit signed)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow SmallInteger range - let Smalltalk fall back to LargeInteger method
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow - also fail primitive
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
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

            // Check if result is within SmallInteger range (30-bit signed)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow/underflow SmallInteger range - let Smalltalk fall back to LargeInteger method
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow - also fail primitive
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

            // Check if result is within SmallInteger range (30-bit signed)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow SmallInteger range - let Smalltalk fall back to LargeInteger method
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow - also fail primitive
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Primitive 10: SmallInteger / (exact division only - fails if remainder exists)
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

        // Validate divisor is not zero
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: SmallIntegerMin / -1 would give 2^30 (outside SmallInteger range)
        if (a == SmallIntegerMin && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Primitive / requires EXACT division - fail if remainder exists
        if (a % b != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int result = a / b;

        // Check if result is within SmallInteger range
        if (result < SmallIntegerMin || result > SmallIntegerMax)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return SmalltalkOOP.FromSmallInteger(result);
    }

    // Primitive 11: SmallInteger \\ (modulo - remainder follows divisor sign)
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

        // Validate divisor is not zero
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // C# % gives remainder with sign of dividend
        int rem = a % b;

        // Smalltalk \\ (mod) has remainder sign follow divisor
        // Blue Book: a \\ b has same sign as b
        // Adjust when signs differ and remainder is non-zero
        if (rem != 0 && ((a < 0) != (b < 0)))
            rem += b;

        return SmalltalkOOP.FromSmallInteger(rem);
    }

    // Primitive 12: SmallInteger // (quo - truncation toward zero)
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

        // Validate divisor is not zero
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: Int32.MinValue / -1
        if (a == int.MinValue && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int result = a / b;
        return SmalltalkOOP.FromSmallInteger(result);
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
        if (shift > 30 || shift < -30)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // For left shifts, check if result will overflow 30-bit range
        if (shift > 0)
        {
            // Check if value is too large to shift left without overflow
            // Maximum shiftable value: 2^(30-shift) - 1 for positive
            // Minimum shiftable value: -(2^(30-shift)) for negative
            long result = (long)a << shift;
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            return SmalltalkOOP.FromSmallInteger((int)result);
        }
        else if (shift < 0)
        {
            // Right shift - always safe, never overflows
            int result = a >> (-shift);
            return SmalltalkOOP.FromSmallInteger(result);
        }
        else
        {
            // Zero shift - return value unchanged
            return SmalltalkOOP.FromSmallInteger(a);
        }
    }

    // Primitive 17: SmallInteger div: (floor division toward -infinity)
    // Different from quo which truncates toward zero
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

        // Validate divisor is not zero
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: Int32.MinValue / -1
        if (a == int.MinValue && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Floor division: always round toward -infinity
        int quot = a / b;  // C# truncates toward zero
        int rem = a % b;

        // Adjust quotient if signs differ and remainder is non-zero
        // Examples: -7 div: 3 = -3 (not -2), 7 div: -3 = -3 (not -2)
        if (rem != 0 && ((a < 0) != (b < 0)))
            quot--;

        return SmalltalkOOP.FromSmallInteger(quot);
    }
}
