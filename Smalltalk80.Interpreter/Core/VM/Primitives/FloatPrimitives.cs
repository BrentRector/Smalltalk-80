using System;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Float primitives (40-54) using new IPrimitive interface.
/// Float is stored as IEEE 754 double precision (8 bytes / 4 words).
///
/// OPTIMIZATION (2025-12-28): Zero-allocation IEEE 754 conversion.
/// - GetFloatAsDouble: Single heap call reads 4 words and returns double
/// - SetFloatFromDouble: Single heap call writes double to 4 words
/// - Uses cached FloatClassOOP instead of string-based class lookup
/// - Uses BitConverter.Int64BitsToDouble for zero-allocation bit reinterpretation
/// </summary>
public static class FloatArithmetic
{
    /// <summary>
    /// Helper: Converts a Float object to C# double.
    /// OPTIMIZED: Single heap call reads all 4 words and converts to IEEE 754 double.
    /// </summary>
    private static double ToDouble(ISmalltalkHeap heap, SmalltalkOOP floatOop)
    {
        return heap.GetFloatAsDouble(floatOop);
    }

    /// <summary>
    /// Helper: Creates a Float object from C# double.
    /// OPTIMIZED: Uses cached FloatClassOOP and single heap call to write IEEE 754 value.
    /// </summary>
    private static SmalltalkOOP FromDouble(PrimitiveExecutionContext ctx, double value)
    {
        // Allocate Float with 4 indexed words using cached class OOP
        var floatOop = ctx.HeapManager.AllocateInstanceOf(ctx.Metadata!.FloatClassOOP, 4);

        // Write IEEE 754 double directly to Float's 4 words
        ctx.HeapManager.SetFloatFromDouble(floatOop, value);

        return floatOop;
    }

    /// <summary>
    /// Helper: Checks if an object is a Float.
    /// OPTIMIZED: Uses cached FloatClassOOP for O(1) class comparison.
    /// </summary>
    private static bool IsFloat(PrimitiveExecutionContext ctx, SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger) return false;
        return ctx.HeapManager.GetClassOOP(oop) == ctx.Metadata!.FloatClassOOP;
    }

    /// <summary>
    /// Helper: Gets numeric value (SmallInteger or Float) as double.
    /// OPTIMIZED: Uses cached FloatClassOOP for type check.
    /// </summary>
    private static bool TryGetDouble(PrimitiveExecutionContext ctx, SmalltalkOOP oop, out double value)
    {
        if (oop.IsSmallInteger)
        {
            value = oop.AsSmallInteger();
            return true;
        }
        if (IsFloat(ctx, oop))
        {
            value = ToDouble(ctx.HeapManager, oop);
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>Primitive 40: SmallInteger asFloat</summary>
    public class AsFloat : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            double value = context.Receiver.AsSmallInteger();
            return PrimitiveResult.Ok(FromDouble(context, value));
        }
    }

    /// <summary>Primitive 41: Float + (addition)</summary>
    public class Add : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(FromDouble(context, a + b));
        }
    }

    /// <summary>Primitive 42: Float - (subtraction)</summary>
    public class Subtract : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(FromDouble(context, a - b));
        }
    }

    /// <summary>Primitive 43: Float &lt; (less than)</summary>
    public class LessThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a < b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 44: Float &gt; (greater than)</summary>
    public class GreaterThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a > b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 45: Float &lt;= (less or equal)</summary>
    public class LessOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a <= b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 46: Float &gt;= (greater or equal)</summary>
    public class GreaterOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a >= b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 47: Float = (equal)</summary>
    public class Equal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a == b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 48: Float ~= (not equal)</summary>
    public class NotEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.FromBool(a != b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 49: Float * (multiplication)</summary>
    public class Multiply : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(FromDouble(context, a * b));
        }
    }

    /// <summary>Primitive 50: Float / (division)</summary>
    public class Divide : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double a)) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Arguments[0], out double b)) return PrimitiveResult.Fail(context.Metadata!);
            if (b == 0) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(FromDouble(context, a / b));
        }
    }

    /// <summary>Primitive 51: Float truncated - returns SmallInteger</summary>
    public class Truncated : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double value)) return PrimitiveResult.Fail(context.Metadata!);

            double truncated = Math.Truncate(value);
            const int SmallIntegerMin = -(1 << 30);
            const int SmallIntegerMax = (1 << 30) - 1;

            if (truncated < SmallIntegerMin || truncated > SmallIntegerMax)
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)truncated));
        }
    }

    /// <summary>Primitive 52: Float fractionPart</summary>
    public class FractionPart : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double value)) return PrimitiveResult.Fail(context.Metadata!);

            double fraction = value - Math.Truncate(value);
            return PrimitiveResult.Ok(FromDouble(context, fraction));
        }
    }

    /// <summary>Primitive 53: Float exponent - returns SmallInteger</summary>
    public class Exponent : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double value)) return PrimitiveResult.Fail(context.Metadata!);

            if (value == 0) return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0));

            int exponent = (int)Math.Floor(Math.Log2(Math.Abs(value))) + 1;
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(exponent));
        }
    }

    /// <summary>Primitive 54: Float timesTwoPower: - multiply by 2^n</summary>
    public class TimesTwoPower : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!TryGetDouble(context, context.Receiver, out double value)) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int power = context.Arguments[0].AsSmallInteger();
            double result = value * Math.Pow(2, power);
            return PrimitiveResult.Ok(FromDouble(context, result));
        }
    }
}
