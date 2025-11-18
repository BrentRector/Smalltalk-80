using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

/// <summary>
/// SmallInteger arithmetic primitives (1-17) using new IPrimitive interface.
/// These are the fundamental arithmetic operations optimized for tagged integers.
/// SmallInteger range: -2^30 to 2^30-1 (30-bit signed integer with 2 tag bits).
/// </summary>
public static class SmallIntegerArithmetic
{
    private const int SmallIntegerMin = -(1 << 30);
    private const int SmallIntegerMax = (1 << 30) - 1;

    /// <summary>Primitive 1: SmallInteger + (addition)</summary>
    public class Add : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                int result = checked(a + b);
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail();
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(); }
        }
    }

    /// <summary>Primitive 2: SmallInteger - (subtraction)</summary>
    public class Subtract : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                int result = checked(a - b);
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail();
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(); }
        }
    }

    /// <summary>Primitive 3: SmallInteger &lt; (less than)</summary>
    public class LessThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a < b);
        }
    }

    /// <summary>Primitive 4: SmallInteger &gt; (greater than)</summary>
    public class GreaterThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a > b);
        }
    }

    /// <summary>Primitive 5: SmallInteger &lt;= (less or equal)</summary>
    public class LessOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a <= b);
        }
    }

    /// <summary>Primitive 6: SmallInteger &gt;= (greater or equal)</summary>
    public class GreaterOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a >= b);
        }
    }

    /// <summary>Primitive 7: SmallInteger = (equal)</summary>
    public class Equal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a == b);
        }
    }

    /// <summary>Primitive 8: SmallInteger ~= (not equal)</summary>
    public class NotEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a != b);
        }
    }

    /// <summary>Primitive 9: SmallInteger * (multiplication)</summary>
    public class Multiply : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                long result = (long)a * b;
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail();
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(); }
        }
    }

    /// <summary>Primitive 10: SmallInteger / (division - exact)</summary>
    public class Divide : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail();
            if (a % b != 0) return PrimitiveResult.Fail(); // Must be exact division

            int result = a / b;
            if (result < SmallIntegerMin || result > SmallIntegerMax)
                return PrimitiveResult.Fail();
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 11: SmallInteger \\ (modulo - Smalltalk floored division)</summary>
    public class Mod : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail();

            // Smalltalk \\ uses floored division (result has same sign as divisor)
            int result = a - b * (int)Math.Floor((double)a / b);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 12: SmallInteger // (integer divide - floored)</summary>
    public class Div : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail();

            // Smalltalk // uses floored division (rounds toward negative infinity)
            int result = (int)Math.Floor((double)a / b);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 13: SmallInteger quo: (quotient - truncated toward zero)</summary>
    public class Quo : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail();

            // C# / already truncates toward zero
            int result = a / b;
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 14: SmallInteger bitAnd:</summary>
    public class BitAnd : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(a & b));
        }
    }

    /// <summary>Primitive 15: SmallInteger bitOr:</summary>
    public class BitOr : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(a | b));
        }
    }

    /// <summary>Primitive 16: SmallInteger bitXor:</summary>
    public class BitXor : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(a ^ b));
        }
    }

    /// <summary>Primitive 17: SmallInteger bitShift:</summary>
    public class BitShift : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            int a = context.Receiver.AsSmallInteger();
            int shift = context.Arguments[0].AsSmallInteger();

            int result;
            if (shift >= 0)
            {
                // Left shift - check for overflow
                if (shift >= 31) return PrimitiveResult.Fail();
                long shiftedValue = (long)a << shift;
                if (shiftedValue < SmallIntegerMin || shiftedValue > SmallIntegerMax)
                    return PrimitiveResult.Fail();
                result = (int)shiftedValue;
            }
            else
            {
                // Right shift (arithmetic)
                int rightShift = -shift;
                if (rightShift >= 31)
                    result = a < 0 ? -1 : 0;
                else
                    result = a >> rightShift;
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }
}
