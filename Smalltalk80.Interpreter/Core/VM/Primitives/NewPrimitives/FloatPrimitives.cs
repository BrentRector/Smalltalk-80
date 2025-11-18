using System;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

/// <summary>
/// Float primitives (40-54) using new IPrimitive interface.
/// Float is stored as IEEE 754 double precision (8 bytes / 4 words).
/// </summary>
public static class FloatArithmetic
{
    /// <summary>Helper: Converts a Float object to C# double.</summary>
    private static double ToDouble(ISmalltalkHeapManager heap, SmalltalkOOP floatOop)
    {
        byte[] bytes = new byte[8];
        for (int i = 0; i < 4; i++)
        {
            ushort word = heap.GetIndexedWord(floatOop, i);
            bytes[i * 2] = (byte)(word & 0xFF);
            bytes[i * 2 + 1] = (byte)((word >> 8) & 0xFF);
        }
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>Helper: Creates a Float object from C# double.</summary>
    private static SmalltalkOOP FromDouble(PrimitiveExecutionContext ctx, double value)
    {
        // Allocate Float with 4 indexed words
        var floatOop = ctx.RuntimeHeapAccess!.AllocateObject("class:Float", 4);
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 4; i++)
        {
            ushort word = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            ctx.HeapManager.SetIndexedWord(floatOop, i, word);
        }
        return floatOop;
    }

    /// <summary>Helper: Checks if an object is a Float.</summary>
    private static bool IsFloat(ISmalltalkHeapManager heap, SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger) return false;
        var format = heap.GetObjectFormat(oop);
        if (format != ObjectFormat.WordIndexed) return false;
        return heap.GetIndexedSize(oop) == 4;
    }

    /// <summary>Helper: Gets numeric value (SmallInteger or Float) as double.</summary>
    private static bool TryGetDouble(ISmalltalkHeapManager heap, SmalltalkOOP oop, out double value)
    {
        if (oop.IsSmallInteger)
        {
            value = oop.AsSmallInteger();
            return true;
        }
        if (IsFloat(heap, oop))
        {
            value = ToDouble(heap, oop);
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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            double value = context.Receiver.AsSmallInteger();
            return PrimitiveResult.Ok(FromDouble(context, value));
        }
    }

    /// <summary>Primitive 41: Float + (addition)</summary>
    public class Add : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.Ok(FromDouble(context, a + b));
        }
    }

    /// <summary>Primitive 42: Float - (subtraction)</summary>
    public class Subtract : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.Ok(FromDouble(context, a - b));
        }
    }

    /// <summary>Primitive 43: Float &lt; (less than)</summary>
    public class LessThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a < b);
        }
    }

    /// <summary>Primitive 44: Float &gt; (greater than)</summary>
    public class GreaterThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a > b);
        }
    }

    /// <summary>Primitive 45: Float &lt;= (less or equal)</summary>
    public class LessOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a <= b);
        }
    }

    /// <summary>Primitive 46: Float &gt;= (greater or equal)</summary>
    public class GreaterOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a >= b);
        }
    }

    /// <summary>Primitive 47: Float = (equal)</summary>
    public class Equal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a == b);
        }
    }

    /// <summary>Primitive 48: Float ~= (not equal)</summary>
    public class NotEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.FromBool(a != b);
        }
    }

    /// <summary>Primitive 49: Float * (multiplication)</summary>
    public class Multiply : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            return PrimitiveResult.Ok(FromDouble(context, a * b));
        }
    }

    /// <summary>Primitive 50: Float / (division)</summary>
    public class Divide : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double a)) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Arguments[0], out double b)) return PrimitiveResult.Fail();
            if (b == 0) return PrimitiveResult.Fail();
            return PrimitiveResult.Ok(FromDouble(context, a / b));
        }
    }

    /// <summary>Primitive 51: Float truncated - returns SmallInteger</summary>
    public class Truncated : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double value)) return PrimitiveResult.Fail();

            double truncated = Math.Truncate(value);
            const int SmallIntegerMin = -(1 << 30);
            const int SmallIntegerMax = (1 << 30) - 1;

            if (truncated < SmallIntegerMin || truncated > SmallIntegerMax)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)truncated));
        }
    }

    /// <summary>Primitive 52: Float fractionPart</summary>
    public class FractionPart : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double value)) return PrimitiveResult.Fail();

            double fraction = value - Math.Truncate(value);
            return PrimitiveResult.Ok(FromDouble(context, fraction));
        }
    }

    /// <summary>Primitive 53: Float exponent - returns SmallInteger</summary>
    public class Exponent : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double value)) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!TryGetDouble(context.HeapManager, context.Receiver, out double value)) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int power = context.Arguments[0].AsSmallInteger();
            double result = value * Math.Pow(2, power);
            return PrimitiveResult.Ok(FromDouble(context, result));
        }
    }
}
