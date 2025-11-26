using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                int result = checked(a + b);
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail(context.Metadata!);
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(context.Metadata!); }
        }
    }

    /// <summary>Primitive 2: SmallInteger - (subtraction)</summary>
    public class Subtract : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                int result = checked(a - b);
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail(context.Metadata!);
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(context.Metadata!); }
        }
    }

    /// <summary>Primitive 3: SmallInteger &lt; (less than)</summary>
    public class LessThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a < b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 4: SmallInteger &gt; (greater than)</summary>
    public class GreaterThan : IPrimitive
    {
        private static int _logCount = 0;
        private const int MAX_LOGS = 50;

        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            bool result = a > b;

            // Debug: trace comparisons involving 38 (fileList size)
            if (b == 38 && _logCount < MAX_LOGS)
            {
                _logCount++;
                Console.WriteLine($"[DEBUG Primitive 4] {a} > {b} = {result}");
            }

            return PrimitiveResult.FromBool(result, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 5: SmallInteger &lt;= (less or equal)</summary>
    public class LessOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a <= b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 6: SmallInteger &gt;= (greater or equal)</summary>
    public class GreaterOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a >= b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 7: SmallInteger = (equal)</summary>
    public class Equal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a == b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 8: SmallInteger ~= (not equal)</summary>
    public class NotEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();
            return PrimitiveResult.FromBool(a != b, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 9: SmallInteger * (multiplication)</summary>
    public class Multiply : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            try
            {
                long result = (long)a * b;
                if (result < SmallIntegerMin || result > SmallIntegerMax)
                    return PrimitiveResult.Fail(context.Metadata!);
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)result));
            }
            catch (OverflowException) { return PrimitiveResult.Fail(context.Metadata!); }
        }
    }

    /// <summary>Primitive 10: SmallInteger / (division - exact)</summary>
    public class Divide : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail(context.Metadata!);
            if (a % b != 0) return PrimitiveResult.Fail(context.Metadata!); // Must be exact division

            int result = a / b;
            if (result < SmallIntegerMin || result > SmallIntegerMax)
                return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 11: SmallInteger \\ (modulo - Smalltalk floored division)</summary>
    public class Mod : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[Primitive 11] INVOKED - receiver={context.Receiver}, args={context.Arguments.Length}");

            if (context.Arguments.Length != 1)
            {
                Console.WriteLine("[Primitive 11] FAILED - wrong argument count");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
            {
                Console.WriteLine($"[Primitive 11] FAILED - not SmallIntegers");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0)
            {
                Console.WriteLine("[Primitive 11] FAILED - division by zero");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // Smalltalk \\ uses floored division (result has same sign as divisor)
            int result = a - b * (int)Math.Floor((double)a / b);
            Console.WriteLine($"[Primitive 11] SUCCESS: {a} \\\\ {b} = {result}");

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
    }

    /// <summary>Primitive 12: SmallInteger // (integer divide - floored)</summary>
    public class Div : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int b = context.Arguments[0].AsSmallInteger();

            if (b == 0) return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (!context.Receiver.IsSmallInteger || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int a = context.Receiver.AsSmallInteger();
            int shift = context.Arguments[0].AsSmallInteger();

            int result;
            if (shift >= 0)
            {
                // Left shift - check for overflow
                if (shift >= 31) return PrimitiveResult.Fail(context.Metadata!);
                long shiftedValue = (long)a << shift;
                if (shiftedValue < SmallIntegerMin || shiftedValue > SmallIntegerMax)
                    return PrimitiveResult.Fail(context.Metadata!);
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

/// <summary>
/// Miscellaneous arithmetic-related primitives.
/// </summary>
public static class MiscArithmetic
{
    /// <summary>
    /// Primitive 76: SmallInteger asObject - Convert SmallInteger to object pointer.
    /// Blue Book: Page 641, Chapter 29.
    /// </summary>
    /// <remarks>
    /// Converts a SmallInteger value to an object pointer by doubling.
    /// SmallIntegers are tagged with low bit set (odd): 2n+1
    /// Objects have low bit clear (even): 2n
    /// So doubling the SmallInteger value gives the object pointer.
    /// </remarks>
    public class AsObject : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Blue Book: primitive takes no arguments
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Must be a SmallInteger to convert to object pointer
            if (!context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Extract integer value from tagged OOP
            int value = context.Receiver.AsSmallInteger();

            // Object pointer is twice the SmallInteger value
            int objectPointer = value * 2;
            var oop = new SmalltalkOOP(objectPointer);

            // Verify the resulting OOP references a valid object in the heap
            if (oop.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Check if OOP is in valid range
            if (objectPointer < 0) return PrimitiveResult.Fail(context.Metadata!);

            // The heap manager will handle validation when the object is accessed
            return PrimitiveResult.Ok(oop);
        }
    }
}
