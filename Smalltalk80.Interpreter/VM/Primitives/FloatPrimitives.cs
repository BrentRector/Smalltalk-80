using System;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Float primitives for floating-point arithmetic operations (primitives 40-54).
/// Based on Blue Book specification. Float is stored as IEEE 754 double precision (8 bytes).
/// </summary>
public static class FloatPrimitives
{
    /// <summary>
    /// Converts a Float object to a C# double.
    /// </summary>
    private static double ToDouble(IPrimitiveContext ctx, SObjectRef floatRef)
    {
        // Get the 8 bytes from the Float object (byte-indexed)
        byte[] bytes = new byte[8];
        int floatId = (int)floatRef.AsObjectPointer();
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = (byte)ctx.Heap.GetIndexedField(floatId, i);
        }

        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Creates a Float object from a C# double.
    /// </summary>
    private static SObjectRef FromDouble(IPrimitiveContext ctx, double value)
    {
        // Get Float class ID from well-known globals
        // TODO: Need to add GetClassByName or use special objects array
        // For now, assume Float class is known (will need infrastructure)

        // Allocate Float object with 8 indexed bytes
        // Format: ByteIndexed, no fixed fields, 8 indexed bytes
        int floatClass = GetFloatClass(ctx);
        int floatId = ctx.Heap.AllocateRawObject(
            classId: floatClass,
            format: SmalltalkHeap.ObjectFormat.ByteIndexed,
            fixedSize: 0,
            indexedSize: 8
        );

        // Store the double as 8 bytes
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            ctx.Heap.SetIndexedField(floatId, i, bytes[i]);
        }

        return SObjectRef.FromObjectPointer(floatId);
    }

    /// <summary>
    /// Checks if an object is a Float.
    /// </summary>
    private static bool IsFloat(IPrimitiveContext ctx, SObjectRef objectRef)
    {
        if (objectRef.IsSmallInteger)
            return false;

        int classId = ctx.Heap.GetClass((int)objectRef.AsObjectPointer());
        int floatClass = GetFloatClass(ctx);

        return classId == floatClass;
    }

    /// <summary>
    /// Gets the Float class ID.
    /// </summary>
    private static int GetFloatClass(IPrimitiveContext ctx)
    {
        return ctx.Heap.GetClassByName("Float");
    }

    /// <summary>
    /// Primitive 40: SmallInteger asFloat (convert SmallInteger to Float).
    /// </summary>
    public static SObjectRef AsFloat(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Add(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Subtract(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef LessThan(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef GreaterThan(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef LessOrEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef GreaterOrEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Equal(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef NotEqual(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Multiply(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Divide(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Truncated(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
            return SObjectRef.FromSmallInteger((int)truncated);
        }

        // TODO: Create LargeInteger for values outside SmallInteger range
        // For now, fail if too large
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 52: Float fractionPart (get the fractional part).
    /// </summary>
    public static SObjectRef FractionPart(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef Exponent(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
            return SObjectRef.FromSmallInteger(0);

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

        return SObjectRef.FromSmallInteger(exponent);
    }

    /// <summary>
    /// Primitive 54: Float timesTwoPower: (multiply by power of 2).
    /// </summary>
    public static SObjectRef TimesTwoPower(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
