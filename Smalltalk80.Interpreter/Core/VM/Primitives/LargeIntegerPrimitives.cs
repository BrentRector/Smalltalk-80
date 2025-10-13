using System.Numerics;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// LargeInteger primitives for arbitrary precision arithmetic operations (primitives 21-37).
/// Based on Blue Book specification. LargeInteger is stored as byte array in little-endian format.
/// </summary>
public static class LargeIntegerPrimitives
{
    /// <summary>
    /// Converts a LargeInteger object to a BigInteger.
    /// </summary>
    private static BigInteger ToBigInteger(IPrimitiveContext ctx, SObjectRef largeIntRef)
    {
        // Get the byte array from the LargeInteger object
        int largeIntId = (int)largeIntRef.AsObjectPointer();
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
    private static SObjectRef FromBigInteger(IPrimitiveContext ctx, BigInteger value)
    {
        byte[] bytes = value.ToByteArray();

        // TODO: Need class lookup infrastructure
        // For now, use placeholder (will fail until infrastructure is added)
        int classId = GetLargeIntegerClass(ctx, value >= BigInteger.Zero);

        int largeIntId = ctx.Heap.AllocateRawObject(
            classId: classId,
            format: ObjectFormat.ByteIndexed,
            fixedSize: 0,
            indexedSize: bytes.Length
        );

        for (int i = 0; i < bytes.Length; i++)
        {
            ctx.Heap.SetIndexedField(largeIntId, i, bytes[i]);
        }

        return SObjectRef.FromObjectPointer(largeIntId);
    }

    /// <summary>
    /// Checks if an object is a LargeInteger.
    /// </summary>
    private static bool IsLargeInteger(IPrimitiveContext ctx, SObjectRef objectRef)
    {
        if (objectRef.IsSmallInteger)
            return false;

        int classId = ctx.Heap.GetClass((int)objectRef.AsObjectPointer());
        // TODO: Check if classId is LargePositiveInteger or LargeNegativeInteger
        // For now, assume any non-SmallInteger numeric type might be LargeInteger
        return true; // Temporary - needs proper class checking
    }

    /// <summary>
    /// Gets indexed size of an object.
    /// </summary>
    private static int GetIndexedSize(IPrimitiveContext ctx, int objectId)
    {
        return ctx.Heap.GetIndexedSize(objectId);
    }

    /// <summary>
    /// Gets the LargeInteger class ID.
    /// </summary>
    private static int GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
    {
        // In Smalltalk-80, there are two classes: LargePositiveInteger and LargeNegativeInteger
        return isPositive
            ? ctx.Heap.GetClassByName("LargePositiveInteger")
            : ctx.Heap.GetClassByName("LargeNegativeInteger");
    }

    // Primitive 21: LargeInteger + (addition)
    public static SObjectRef Add(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left + right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 22: LargeInteger - (subtraction)
    public static SObjectRef Subtract(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left - right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 23: LargeInteger < (less than)
    public static SObjectRef LessThan(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left < right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 24: LargeInteger > (greater than)
    public static SObjectRef GreaterThan(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left > right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 25: LargeInteger <= (less than or equal)
    public static SObjectRef LessOrEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left <= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 26: LargeInteger >= (greater than or equal)
    public static SObjectRef GreaterOrEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left >= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 27: LargeInteger = (equality)
    public static SObjectRef Equal(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left == right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 28: LargeInteger ~= (not equal)
    public static SObjectRef NotEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        return left != right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // Primitive 29: LargeInteger * (multiplication)
    public static SObjectRef Multiply(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left * right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 30: LargeInteger / (division - fails if not exact)
    public static SObjectRef Divide(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);

        if (remainder != BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil; // Let Smalltalk create Fraction
        }

        return FromBigInteger(ctx, quotient);
    }

    // Primitive 31: LargeInteger \\ (modulo)
    public static SObjectRef Mod(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger remainder = dividend % divisor;
        if (remainder != BigInteger.Zero && (dividend < BigInteger.Zero) != (divisor < BigInteger.Zero))
            remainder += divisor;

        return FromBigInteger(ctx, remainder);
    }

    // Primitive 32: LargeInteger // (integer division)
    public static SObjectRef Div(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger quotient = dividend / divisor;
        if ((dividend < BigInteger.Zero) != (divisor < BigInteger.Zero) && dividend % divisor != BigInteger.Zero)
            quotient--;

        return FromBigInteger(ctx, quotient);
    }

    // Primitive 33: LargeInteger quo: (truncated division)
    public static SObjectRef Quo(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger quotient = dividend / divisor;

        return FromBigInteger(ctx, quotient);
    }

    // Primitive 34: LargeInteger bitAnd:
    public static SObjectRef BitAnd(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left & right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 35: LargeInteger bitOr:
    public static SObjectRef BitOr(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left | right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 36: LargeInteger bitXor:
    public static SObjectRef BitXor(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);
        BigInteger result = left ^ right;

        return FromBigInteger(ctx, result);
    }

    // Primitive 37: LargeInteger bitShift:
    public static SObjectRef BitShift(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsLargeInteger(ctx, receiver) || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        BigInteger value = ToBigInteger(ctx, receiver);
        int shiftCount = args[0].AsSmallInteger();

        BigInteger result = shiftCount switch
        {
            0 => value,
            > 0 => value << shiftCount,
            < 0 => value >> -shiftCount
        };

        return FromBigInteger(ctx, result);
    }
}
