using System.Numerics;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// LargeInteger arithmetic primitives (21-37) using new IPrimitive interface.
/// Based on Blue Book specification. LargeInteger is stored as byte array in little-endian format.
/// </summary>
public static class LargeIntegerArithmetic
{
    private const int SmallIntegerMin = -(1 << 30);
    private const int SmallIntegerMax = (1 << 30) - 1;

    /// <summary>
    /// Converts a LargeInteger object to a BigInteger.
    /// </summary>
    private static BigInteger ToBigInteger(PrimitiveExecutionContext context, SmalltalkOOP largeIntOop)
    {
        int size = context.HeapManager.GetIndexedSize(largeIntOop);
        byte[] bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = context.HeapManager.GetIndexedByte(largeIntOop, i);
        }

        return new BigInteger(bytes);
    }

    /// <summary>
    /// Creates a LargeInteger object from a BigInteger.
    /// Returns SmallInteger if value fits in 30-bit range.
    /// </summary>
    private static SmalltalkOOP FromBigInteger(PrimitiveExecutionContext context, BigInteger value)
    {
        // Optimization: If value fits in SmallInteger range, return SmallInteger
        if (value >= SmallIntegerMin && value <= SmallIntegerMax)
        {
            return SmalltalkOOP.FromSmallInteger((int)value);
        }

        byte[] bytes = value.ToByteArray();

        // Trim leading sign-extension bytes for canonical representation
        int len = bytes.Length;
        byte signByte = (value.Sign < 0) ? (byte)0xFF : (byte)0x00;
        while (len > 1 && bytes[len - 1] == signByte)
            len--;

        // Determine proper class based on sign
        SmalltalkOOP classOOP = GetLargeIntegerClass(context, value.Sign >= 0);

        // Allocate byte-indexed object
        SmalltalkOOP largeIntOop = context.HeapManager.AllocateObject(
            format: ObjectFormat.ByteIndexed,
            fixedSlots: 0,
            indexedSize: len
        );

        // Store trimmed bytes
        for (int i = 0; i < len; i++)
        {
            context.HeapManager.SetIndexedByte(largeIntOop, i, bytes[i]);
        }

        // Set class reference
        context.HeapManager.SetClassOOP(largeIntOop, classOOP);

        return largeIntOop;
    }

    /// <summary>
    /// Checks if an object is a LargeInteger.
    /// </summary>
    private static bool IsLargeInteger(PrimitiveExecutionContext context, SmalltalkOOP objectOop)
    {
        if (objectOop.IsSmallInteger)
            return false;

        SmalltalkOOP classOOP = context.HeapManager.GetClassOOP(objectOop);

        // Check against both LargeInteger classes
        var posClass = context.RuntimeHeapAccess?.GetClass("LargePositiveInteger");
        if (posClass != null && posClass != context.HeapManager.GetNil() && classOOP == posClass)
            return true;

        var negClass = context.RuntimeHeapAccess?.GetClass("LargeNegativeInteger");
        if (negClass != null && negClass != context.HeapManager.GetNil() && classOOP == negClass)
            return true;

        return false;
    }

    /// <summary>
    /// Gets the LargeInteger class OOP based on sign.
    /// </summary>
    private static SmalltalkOOP GetLargeIntegerClass(PrimitiveExecutionContext context, bool isPositive)
    {
        string className = isPositive
            ? "LargePositiveInteger"
            : "LargeNegativeInteger";

        var classOOP = context.RuntimeHeapAccess?.GetClass(className);

        if (classOOP != null && classOOP != context.HeapManager.GetNil())
            return classOOP.Value;

        throw new InvalidOperationException(
            $"{className} not found. LargeInteger classes must be installed during cold start.");
    }

    /// <summary>Primitive 21: LargeInteger + (addition)</summary>
    public class Add : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left + right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 22: LargeInteger - (subtraction)</summary>
    public class Subtract : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left - right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 23: LargeInteger &lt; (less than)</summary>
    public class LessThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left < right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 24: LargeInteger &gt; (greater than)</summary>
    public class GreaterThan : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left > right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 25: LargeInteger &lt;= (less or equal)</summary>
    public class LessOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left <= right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 26: LargeInteger &gt;= (greater or equal)</summary>
    public class GreaterOrEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left >= right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 27: LargeInteger = (equal)</summary>
    public class Equal : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left == right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 28: LargeInteger ~= (not equal)</summary>
    public class NotEqual : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            return PrimitiveResult.FromBool(left != right, context.Metadata!.KnownObjects.True, context.Metadata!.KnownObjects.False);
        }
    }

    /// <summary>Primitive 29: LargeInteger * (multiplication)</summary>
    public class Multiply : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left * right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 30: LargeInteger / (exact division)</summary>
    public class Divide : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger dividend = ToBigInteger(context, context.Receiver);
            BigInteger divisor = ToBigInteger(context, context.Arguments[0]);

            if (divisor == BigInteger.Zero) return PrimitiveResult.Fail();

            BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);
            if (remainder != BigInteger.Zero) return PrimitiveResult.Fail(); // Must be exact

            return PrimitiveResult.Ok(FromBigInteger(context, quotient));
        }
    }

    /// <summary>Primitive 31: LargeInteger \\ (modulo - Smalltalk floored division)</summary>
    public class Mod : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger dividend = ToBigInteger(context, context.Receiver);
            BigInteger divisor = ToBigInteger(context, context.Arguments[0]);

            if (divisor == BigInteger.Zero) return PrimitiveResult.Fail();

            // Smalltalk \\ gives remainder with sign of divisor
            BigInteger remainder = dividend % divisor;
            if (remainder != BigInteger.Zero && (dividend < BigInteger.Zero) != (divisor < BigInteger.Zero))
                remainder += divisor;

            return PrimitiveResult.Ok(FromBigInteger(context, remainder));
        }
    }

    /// <summary>Primitive 32: LargeInteger // (floor division)</summary>
    public class Div : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger dividend = ToBigInteger(context, context.Receiver);
            BigInteger divisor = ToBigInteger(context, context.Arguments[0]);

            if (divisor == BigInteger.Zero) return PrimitiveResult.Fail();

            // Floor division - rounds toward negative infinity
            BigInteger quotient = dividend / divisor;
            if ((dividend < BigInteger.Zero) != (divisor < BigInteger.Zero) && dividend % divisor != BigInteger.Zero)
                quotient--;

            return PrimitiveResult.Ok(FromBigInteger(context, quotient));
        }
    }

    /// <summary>Primitive 33: LargeInteger quo: (truncated division toward zero)</summary>
    public class Quo : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger dividend = ToBigInteger(context, context.Receiver);
            BigInteger divisor = ToBigInteger(context, context.Arguments[0]);

            if (divisor == BigInteger.Zero) return PrimitiveResult.Fail();

            // C# / already truncates toward zero
            BigInteger quotient = dividend / divisor;

            return PrimitiveResult.Ok(FromBigInteger(context, quotient));
        }
    }

    /// <summary>Primitive 34: LargeInteger bitAnd:</summary>
    public class BitAnd : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left & right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 35: LargeInteger bitOr:</summary>
    public class BitOr : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left | right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 36: LargeInteger bitXor:</summary>
    public class BitXor : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !IsLargeInteger(context, context.Arguments[0]))
                return PrimitiveResult.Fail();

            BigInteger left = ToBigInteger(context, context.Receiver);
            BigInteger right = ToBigInteger(context, context.Arguments[0]);
            BigInteger result = left ^ right;

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }

    /// <summary>Primitive 37: LargeInteger bitShift:</summary>
    public class BitShift : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (!IsLargeInteger(context, context.Receiver) || !context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            BigInteger value = ToBigInteger(context, context.Receiver);
            int shiftCount = context.Arguments[0].AsSmallInteger();

            BigInteger result = shiftCount switch
            {
                0 => value,
                > 0 => value << shiftCount,
                < 0 => value >> -shiftCount
            };

            return PrimitiveResult.Ok(FromBigInteger(context, result));
        }
    }
}
