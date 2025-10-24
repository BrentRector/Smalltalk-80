using System.Numerics;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

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
    private static BigInteger ToBigInteger(IPrimitiveContext ctx, SmalltalkOOP largeIntOop)
    {
        // Get the byte array from the LargeInteger object
        int size = GetIndexedSize(ctx, largeIntOop);
        byte[] bytes = new byte[size];

        for (int i = 0; i < size; i++)
        {
            bytes[i] = ctx.HeapManager.GetIndexedByte(largeIntOop, i);
        }

        return new BigInteger(bytes);
    }

    /// <summary>
    /// Creates a LargeInteger object from a BigInteger.
    /// </summary>
    private static SmalltalkOOP FromBigInteger(IPrimitiveContext ctx, BigInteger value)
    {
        // Optimization: If value fits in SmallInteger range, return SmallInteger instead
        // SmallInteger range: -2^30 to 2^30-1 (30-bit signed integer)
        const int SmallIntegerMin = -(1 << 30);
        const int SmallIntegerMax = (1 << 30) - 1;

        if (value >= SmallIntegerMin && value <= SmallIntegerMax)
        {
            return SmalltalkOOP.FromSmallInteger((int)value);
        }

        byte[] bytes = value.ToByteArray(); // little-endian two's complement

        // Trim leading sign-extension bytes for canonical representation
        // BigInteger.ToByteArray can return bytes like: [0x34, 0x12, 0x00, 0x00]
        // We want to trim trailing 0x00 (positive) or 0xFF (negative) bytes
        // Keep at least one byte even for zero
        int len = bytes.Length;
        byte signByte = (value.Sign < 0) ? (byte)0xFF : (byte)0x00;
        while (len > 1 && bytes[len - 1] == signByte)
            len--;

        // Determine proper class based on sign
        SmalltalkOOP classOOP = GetLargeIntegerClass(ctx, value.Sign >= 0);

        // Allocate byte-indexed object
        // Use low-level allocation since we need to set class explicitly afterward
        SmalltalkOOP largeIntOop = ctx.HeapManager.AllocateObject(
            format: ObjectFormat.ByteIndexed,
            fixedSlots: 0,
            indexedSize: len
        );

        // Store trimmed bytes
        for (int i = 0; i < len; i++)
        {
            ctx.HeapManager.SetIndexedByte(largeIntOop, i, bytes[i]);
        }

        // Set class reference using the new SetClassOOP method
        // This ensures proper class pointer handling and maintains heap invariants
        ctx.HeapManager.SetClassOOP(largeIntOop, classOOP);

        return largeIntOop;
    }

    /// <summary>
    /// Checks if an object is a LargeInteger.
    /// </summary>
    private static bool IsLargeInteger(IPrimitiveContext ctx, SmalltalkOOP objectOop)
    {
        if (objectOop.IsSmallInteger)
            return false;

        SmalltalkOOP classOOP = ctx.HeapManager.GetClassOOP(objectOop);

        // Check against both LargeInteger classes
        if (GlobalRegistry.TryGet("class:LargePositiveInteger", out var posClass) && classOOP == posClass)
            return true;
        if (GlobalRegistry.TryGet("class:LargeNegativeInteger", out var negClass) && classOOP == negClass)
            return true;

        return false;
    }

    /// <summary>
    /// Gets indexed size of an object.
    /// </summary>
    private static int GetIndexedSize(IPrimitiveContext ctx, SmalltalkOOP objectOop)
    {
        return ctx.HeapManager.GetIndexedSize(objectOop);
    }

    /// <summary>
    /// Gets the LargeInteger class OOP based on sign.
    /// </summary>
    private static SmalltalkOOP GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
    {
        string className = isPositive
            ? "class:LargePositiveInteger"
            : "class:LargeNegativeInteger";

        if (GlobalRegistry.TryGet(className, out var classOOP))
            return classOOP;

        throw new InvalidOperationException(
            $"{className} not registered in GlobalRegistry during cold start. " +
            "This indicates a bootstrap phase error - LargeInteger classes must be " +
            "registered during Phase 2 (Class Shells) of the cold start process.");
    }

    // Primitive 21: LargeInteger + (addition)
    public static SmalltalkOOP Add(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Subtract(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP LessThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP GreaterThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP LessOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP GreaterOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Equal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP NotEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Multiply(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Divide(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP Quo(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP BitAnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP BitOr(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP BitXor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
    public static SmalltalkOOP BitShift(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
