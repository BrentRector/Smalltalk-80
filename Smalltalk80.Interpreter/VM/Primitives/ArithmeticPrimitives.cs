using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Arithmetic primitive operations for SmallInteger (Primitives 1-17).
/// These are the fundamental arithmetic operations optimized for tagged integers.
/// </summary>
public static class ArithmeticPrimitives
{
    // Primitive 1: SmallInteger +
    public static PrimitiveResult Add(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        try
        {
            int result = checked(a + b);
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch (OverflowException)
        {
            return PrimitiveResult.Failed(); // Fall back to LargeInteger method
        }
    }

    // Primitive 2: SmallInteger -
    public static PrimitiveResult Subtract(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        try
        {
            int result = checked(a - b);
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch (OverflowException)
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 3: SmallInteger <
    public static PrimitiveResult LessThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a < b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 4: SmallInteger >
    public static PrimitiveResult GreaterThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a > b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 5: SmallInteger <=
    public static PrimitiveResult LessOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a <= b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 6: SmallInteger >=
    public static PrimitiveResult GreaterOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a >= b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 7: SmallInteger =
    public static PrimitiveResult Equal(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a == b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 8: SmallInteger ~=
    public static PrimitiveResult NotEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        return PrimitiveResult.Succeeded(a != b ? GlobalRegistry.TrueObjectId : GlobalRegistry.FalseObjectId);
    }

    // Primitive 9: SmallInteger *
    public static PrimitiveResult Multiply(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        try
        {
            int result = checked(a * b);
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch (OverflowException)
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 10: SmallInteger /
    public static PrimitiveResult Divide(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        if (b == 0) return PrimitiveResult.Failed();

        try
        {
            int result = a / b;
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 11: SmallInteger \\
    public static PrimitiveResult Mod(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        if (b == 0) return PrimitiveResult.Failed();

        try
        {
            int result = a % b;
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 12: SmallInteger //
    public static PrimitiveResult Quo(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        if (b == 0) return PrimitiveResult.Failed();

        try
        {
            int result = a / b;
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 13: SmallInteger bitAnd:
    public static PrimitiveResult BitAnd(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        int result = a & b;
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
    }

    // Primitive 14: SmallInteger bitOr:
    public static PrimitiveResult BitOr(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        int result = a | b;
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
    }

    // Primitive 15: SmallInteger bitXor:
    public static PrimitiveResult BitXor(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        int result = a ^ b;
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
    }

    // Primitive 16: SmallInteger bitShift:
    public static PrimitiveResult BitShift(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int shift = ctx.DecodeSmallInteger(args[0]);

        try
        {
            int result = shift >= 0 ? a << shift : a >> (-shift);
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }

    // Primitive 17: SmallInteger div:
    public static PrimitiveResult Div(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1) return PrimitiveResult.Failed();
        if (!ctx.IsSmallInteger(receiver) || !ctx.IsSmallInteger(args[0])) return PrimitiveResult.Failed();

        int a = ctx.DecodeSmallInteger(receiver);
        int b = ctx.DecodeSmallInteger(args[0]);

        if (b == 0) return PrimitiveResult.Failed();

        try
        {
            int result = a / b;
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(result));
        }
        catch
        {
            return PrimitiveResult.Failed();
        }
    }
}
