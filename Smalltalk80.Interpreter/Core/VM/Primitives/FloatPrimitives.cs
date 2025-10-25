using System;
using System.Numerics;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Float primitives for floating-point arithmetic operations (primitives 40-54).
/// Based on Blue Book specification. Float is stored as IEEE 754 double precision (8 bytes).
/// </summary>
public static class FloatPrimitives
{
    /// <summary>
    /// Converts a Float object to a C# double.
    /// </summary>
    private static double ToDouble(IPrimitiveContext ctx, SmalltalkOOP floatOop)
    {
        // Get the 8 bytes from the Float object (byte-indexed)
        byte[] bytes = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = ctx.HeapManager.GetIndexedByte(floatOop, i);
        }

        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Creates a Float object from a C# double.
    /// </summary>
    private static SmalltalkOOP FromDouble(IPrimitiveContext ctx, double value)
    {
        // Allocate Float object with 8 indexed bytes using dynamic class lookup
        SmalltalkOOP floatOop = ctx.AllocateObject("class:Float", 8);

        // Store the double as 8 bytes
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            ctx.HeapManager.SetIndexedByte(floatOop, i, bytes[i]);
        }
        
        return floatOop;
    }

    /// <summary>
    /// Checks if an object is a Float.
    /// </summary>
    private static bool IsFloat(IPrimitiveContext ctx, SmalltalkOOP objectOop)
    {
        if (objectOop.IsSmallInteger)
            return false;

        // Check if the object's format is ByteIndexed with 8 bytes (Float characteristic)
        var format = ctx.HeapManager.GetObjectFormat(objectOop);
        if (format != ObjectFormat.ByteIndexed)
            return false;
            
        int size = ctx.HeapManager.GetIndexedSize(objectOop);
        return size == 8;
    }


    /// <summary>
    /// Primitive 40: SmallInteger asFloat (convert SmallInteger to Float).
    /// </summary>
    public static SmalltalkOOP AsFloat(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int value = receiver.AsSmallInteger();
        double floatValue = (double)value;

        return FromDouble(ctx, floatValue);
    }

    /// <summary>
    /// Primitive 41: Float + (addition).
    /// </summary>
    public static SmalltalkOOP Add(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left + right;

        return FromDouble(ctx, result);
    }

    /// <summary>
    /// Primitive 42: Float - (subtraction).
    /// </summary>
    public static SmalltalkOOP Subtract(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left - right;

        return FromDouble(ctx, result);
    }

    /// <summary>
    /// Primitive 43: Float &lt; (less than).
    /// </summary>
    public static SmalltalkOOP LessThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left < right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 44: Float &gt; (greater than).
    /// </summary>
    public static SmalltalkOOP GreaterThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left > right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 45: Float &lt;= (less than or equal).
    /// </summary>
    public static SmalltalkOOP LessOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left <= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 46: Float &gt;= (greater than or equal).
    /// </summary>
    public static SmalltalkOOP GreaterOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left >= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 47: Float = (equality).
    /// </summary>
    public static SmalltalkOOP Equal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left == right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 48: Float ~= (not equal).
    /// </summary>
    public static SmalltalkOOP NotEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        return left != right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    /// <summary>
    /// Primitive 49: Float * (multiplication).
    /// </summary>
    public static SmalltalkOOP Multiply(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left * right;

        return FromDouble(ctx, result);
    }

    /// <summary>
    /// Primitive 50: Float / (division).
    /// </summary>
    public static SmalltalkOOP Divide(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        // IEEE 754 handles division by zero as infinity or NaN
        double result = left / right;

        return FromDouble(ctx, result);
    }

    /// <summary>
    /// Primitive 51: Float truncated (convert to integer, truncate towards zero).
    /// </summary>
    public static SmalltalkOOP Truncated(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double value = ToDouble(ctx, receiver);

        // Check for special cases
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Truncate towards zero
        double truncated = Math.Truncate(value);

        // Check if it fits in a SmallInteger (30-bit signed range)
        const int SmallIntegerMinValue = -(1 << 30);
        const int SmallIntegerMaxValue = (1 << 30) - 1;

        if (truncated >= SmallIntegerMinValue && truncated <= SmallIntegerMaxValue)
        {
            return SmalltalkOOP.FromSmallInteger((int)truncated);
        }

        // Value is outside SmallInteger range - create LargeInteger (heap-only)
        // Convert double to BigInteger (which truncates automatically)
        BigInteger bigInt = (BigInteger)truncated;

        // Use LargeIntegerPrimitives to create heap-resident LargeInteger object
        return LargeIntegerPrimitives.FromBigInteger(ctx, bigInt);
    }

    /// <summary>
    /// Primitive 52: Float fractionPart (get the fractional part).
    /// </summary>
    public static SmalltalkOOP FractionPart(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double value = ToDouble(ctx, receiver);
        double fractionPart = value - Math.Truncate(value);

        return FromDouble(ctx, fractionPart);
    }

    /// <summary>
    /// Primitive 53: Float exponent (get the binary exponent).
    /// </summary>
    public static SmalltalkOOP Exponent(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double value = ToDouble(ctx, receiver);

        // Handle special cases
        if (value == 0.0)
            return SmalltalkOOP.FromSmallInteger(0);

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get the IEEE 754 representation
        long bits = BitConverter.DoubleToInt64Bits(value);

        // Extract the biased exponent (bits 52-62)
        int biasedExponent = (int)((bits >> 52) & 0x7FF);

        // Subtract the bias (1023) to get the actual exponent
        int exponent = biasedExponent - 1023;

        return SmalltalkOOP.FromSmallInteger(exponent);
    }

    /// <summary>
    /// Primitive 54: Float timesTwoPower: (multiply by power of 2).
    /// </summary>
    public static SmalltalkOOP TimesTwoPower(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!IsFloat(ctx, receiver) || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        double value = ToDouble(ctx, receiver);
        int power = args[0].AsSmallInteger();

        // Use Math.ScaleB for efficient scaling by power of 2
        double result = Math.ScaleB(value, power);

        return FromDouble(ctx, result);
    }
}