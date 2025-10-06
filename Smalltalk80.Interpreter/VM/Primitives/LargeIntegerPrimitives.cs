using System.Numerics;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// LargeInteger primitives for arbitrary precision arithmetic operations (primitives 21-37).
/// Based on Blue Book specification. LargeInteger is stored as byte array in little-endian format.
/// </summary>
public static class LargeIntegerPrimitives
{
    /// <summary>
    /// Converts a LargeInteger object to a BigInteger.
    /// </summary>
    private static BigInteger ToBigInteger(IPrimitiveContext ctx, int largeIntId)
    {
        // Get the byte array from the LargeInteger object
        int size = GetIndexedSize(ctx, largeIntId);
        byte[] bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = (byte)ctx.Heap.GetIndexedField(largeIntId, i);
        }

        return new BigInteger(bytes);
    }

    /// <summary>
    /// Creates a LargeInteger object from a BigInteger.
    /// </summary>
    private static int FromBigInteger(IPrimitiveContext ctx, BigInteger value)
    {
        byte[] bytes = value.ToByteArray();

        // TODO: Need class lookup infrastructure
        // For now, use placeholder (will fail until infrastructure is added)
        int classId = GetLargeIntegerClass(ctx, value >= BigInteger.Zero);

        int largeIntId = ctx.Heap.AllocateRawObject(
            classId: classId,
            format: SmalltalkHeap.ObjectFormat.ByteIndexed,
            fixedSize: 0,
            indexedSize: bytes.Length
        );

        for (int i = 0; i < bytes.Length; i++)
        {
            ctx.Heap.SetIndexedField(largeIntId, i, bytes[i]);
        }

        return largeIntId;
    }

    /// <summary>
    /// Checks if an object is a LargeInteger.
    /// </summary>
    private static bool IsLargeInteger(IPrimitiveContext ctx, int objectId)
    {
        if (ctx.IsSmallInteger(objectId))
            return false;

        int classId = ctx.Heap.GetClass(objectId);
        // TODO: Check if classId is LargePositiveInteger or LargeNegativeInteger
        // For now, assume any non-SmallInteger numeric type might be LargeInteger
        return true; // Temporary - needs proper class checking
    }

    /// <summary>
    /// Gets indexed size of an object.
    /// TODO: This should be in SmalltalkHeap API
    /// </summary>
    private static int GetIndexedSize(IPrimitiveContext ctx, int objectId)
    {
        // TODO: Need SmalltalkHeap.GetIndexedSize() method
        // For now, we'll have to probe until we hit an exception
        throw new NotImplementedException("GetIndexedSize not yet implemented in SmalltalkHeap");
    }

    /// <summary>
    /// Gets the LargeInteger class ID.
    /// TODO: This should use class lookup by name.
    /// </summary>
    private static int GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
    {
        // TODO: Need class lookup infrastructure
        throw new NotImplementedException("LargeInteger class lookup not yet implemented");
    }

    // Primitive 21: LargeInteger + (addition)
    public static PrimitiveResult Add(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left + right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 22: LargeInteger - (subtraction)
    public static PrimitiveResult Subtract(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left - right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 23: LargeInteger < (less than)
    public static PrimitiveResult LessThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isLess = left < right;

        // TODO: Should return true/false objects from special objects array
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isLess ? 1 : 0));
    }

    // Primitive 24: LargeInteger > (greater than)
    public static PrimitiveResult GreaterThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isGreater = left > right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isGreater ? 1 : 0));
    }

    // Primitive 25: LargeInteger <= (less than or equal)
    public static PrimitiveResult LessOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isLessOrEqual = left <= right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isLessOrEqual ? 1 : 0));
    }

    // Primitive 26: LargeInteger >= (greater than or equal)
    public static PrimitiveResult GreaterOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isGreaterOrEqual = left >= right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isGreaterOrEqual ? 1 : 0));
    }

    // Primitive 27: LargeInteger = (equality)
    public static PrimitiveResult Equal(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isEqual = left == right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isEqual ? 1 : 0));
    }

    // Primitive 28: LargeInteger ~= (not equal)
    public static PrimitiveResult NotEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        bool isNotEqual = left != right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isNotEqual ? 1 : 0));
    }

    // Primitive 29: LargeInteger * (multiplication)
    public static PrimitiveResult Multiply(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left * right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 30: LargeInteger / (division - fails if not exact)
    public static PrimitiveResult Divide(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.Failed();

        BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);

        if (remainder != BigInteger.Zero)
            return PrimitiveResult.Failed(); // Let Smalltalk create Fraction

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, quotient));
    }

    // Primitive 31: LargeInteger \\ (modulo)
    public static PrimitiveResult Mod(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.Failed();

        BigInteger remainder = dividend % divisor;
        if (remainder != BigInteger.Zero && (dividend < BigInteger.Zero) != (divisor < BigInteger.Zero))
            remainder += divisor;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, remainder));
    }

    // Primitive 32: LargeInteger // (integer division)
    public static PrimitiveResult Div(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.Failed();

        BigInteger quotient = dividend / divisor;
        if ((dividend < BigInteger.Zero) != (divisor < BigInteger.Zero) && dividend % divisor != BigInteger.Zero)
            quotient--;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, quotient));
    }

    // Primitive 33: LargeInteger quo: (truncated division)
    public static PrimitiveResult Quo(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.Failed();

        BigInteger quotient = dividend / divisor;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, quotient));
    }

    // Primitive 34: LargeInteger bitAnd:
    public static PrimitiveResult BitAnd(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left & right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 35: LargeInteger bitOr:
    public static PrimitiveResult BitOr(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left | right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 36: LargeInteger bitXor:
    public static PrimitiveResult BitXor(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
            return PrimitiveResult.Failed();

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left ^ right;

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }

    // Primitive 37: LargeInteger bitShift:
    public static PrimitiveResult BitShift(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsLargeInteger(ctx, receiver) || !ctx.IsSmallInteger(args[0]))
            return PrimitiveResult.Failed();

        BigInteger value = ToBigInteger(ctx, receiver);
        int shiftCount = ctx.DecodeSmallInteger(args[0]);

        BigInteger result = shiftCount switch
        {
            0 => value,
            > 0 => value << shiftCount,
            < 0 => value >> -shiftCount
        };

        return PrimitiveResult.Succeeded(FromBigInteger(ctx, result));
    }
}
