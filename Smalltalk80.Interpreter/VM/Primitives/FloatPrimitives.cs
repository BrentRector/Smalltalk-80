using System;

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
    private static double ToDouble(IPrimitiveContext ctx, int floatId)
    {
        // Get the 8 bytes from the Float object (byte-indexed)
        byte[] bytes = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = (byte)ctx.Heap.GetIndexedField(floatId, i);
        }

        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Creates a Float object from a C# double.
    /// </summary>
    private static int FromDouble(IPrimitiveContext ctx, double value)
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

        return floatId;
    }

    /// <summary>
    /// Checks if an object is a Float.
    /// </summary>
    private static bool IsFloat(IPrimitiveContext ctx, int objectId)
    {
        if (ctx.IsSmallInteger(objectId))
            return false;

        int classId = ctx.Heap.GetClass(objectId);
        int floatClass = GetFloatClass(ctx);

        return classId == floatClass;
    }

    /// <summary>
    /// Gets the Float class ID.
    /// TODO: This should use special objects array or class lookup.
    /// </summary>
    private static int GetFloatClass(IPrimitiveContext ctx)
    {
        // TODO: Need to add infrastructure to look up classes by name
        // For now, return a placeholder (will fail until infrastructure is added)
        throw new NotImplementedException("Float class lookup not yet implemented - needs special objects array or class registry");
    }

    /// <summary>
    /// Primitive 40: SmallInteger asFloat (convert SmallInteger to Float).
    /// </summary>
    public static PrimitiveResult AsFloat(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (!ctx.IsSmallInteger(receiver))
            return PrimitiveResult.Failed();

        int value = ctx.DecodeSmallInteger(receiver);
        double floatValue = (double)value;

        return PrimitiveResult.Succeeded(FromDouble(ctx, floatValue));
    }

    /// <summary>
    /// Primitive 41: Float + (addition).
    /// </summary>
    public static PrimitiveResult Add(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left + right;

        return PrimitiveResult.Succeeded(FromDouble(ctx, result));
    }

    /// <summary>
    /// Primitive 42: Float - (subtraction).
    /// </summary>
    public static PrimitiveResult Subtract(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left - right;

        return PrimitiveResult.Succeeded(FromDouble(ctx, result));
    }

    /// <summary>
    /// Primitive 43: Float &lt; (less than).
    /// </summary>
    public static PrimitiveResult LessThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isLess = left < right;

        // TODO: Need to get true/false from special objects array
        // For now, encode as 1 (true) or 0 (false) SmallInteger
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isLess ? 1 : 0));
    }

    /// <summary>
    /// Primitive 44: Float &gt; (greater than).
    /// </summary>
    public static PrimitiveResult GreaterThan(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isGreater = left > right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isGreater ? 1 : 0));
    }

    /// <summary>
    /// Primitive 45: Float &lt;= (less than or equal).
    /// </summary>
    public static PrimitiveResult LessOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isLessOrEqual = left <= right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isLessOrEqual ? 1 : 0));
    }

    /// <summary>
    /// Primitive 46: Float &gt;= (greater than or equal).
    /// </summary>
    public static PrimitiveResult GreaterOrEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isGreaterOrEqual = left >= right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isGreaterOrEqual ? 1 : 0));
    }

    /// <summary>
    /// Primitive 47: Float = (equality).
    /// </summary>
    public static PrimitiveResult Equal(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isEqual = left == right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isEqual ? 1 : 0));
    }

    /// <summary>
    /// Primitive 48: Float ~= (not equal).
    /// </summary>
    public static PrimitiveResult NotEqual(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        bool isNotEqual = left != right;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(isNotEqual ? 1 : 0));
    }

    /// <summary>
    /// Primitive 49: Float * (multiplication).
    /// </summary>
    public static PrimitiveResult Multiply(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);
        double result = left * right;

        return PrimitiveResult.Succeeded(FromDouble(ctx, result));
    }

    /// <summary>
    /// Primitive 50: Float / (division).
    /// </summary>
    public static PrimitiveResult Divide(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !IsFloat(ctx, args[0]))
            return PrimitiveResult.Failed();

        double left = ToDouble(ctx, receiver);
        double right = ToDouble(ctx, args[0]);

        // IEEE 754 handles division by zero as infinity or NaN
        double result = left / right;

        return PrimitiveResult.Succeeded(FromDouble(ctx, result));
    }

    /// <summary>
    /// Primitive 51: Float truncated (convert to integer, truncate towards zero).
    /// </summary>
    public static PrimitiveResult Truncated(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver))
            return PrimitiveResult.Failed();

        double value = ToDouble(ctx, receiver);

        // Check for special cases
        if (double.IsNaN(value) || double.IsInfinity(value))
            return PrimitiveResult.Failed();

        // Truncate towards zero
        double truncated = Math.Truncate(value);

        // Check if it fits in a SmallInteger (30-bit signed range)
        const int SmallIntegerMinValue = -(1 << 30);
        const int SmallIntegerMaxValue = (1 << 30) - 1;

        if (truncated >= SmallIntegerMinValue && truncated <= SmallIntegerMaxValue)
        {
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger((int)truncated));
        }

        // TODO: Create LargeInteger for values outside SmallInteger range
        // For now, fail if too large
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 52: Float fractionPart (get the fractional part).
    /// </summary>
    public static PrimitiveResult FractionPart(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver))
            return PrimitiveResult.Failed();

        double value = ToDouble(ctx, receiver);
        double fractionPart = value - Math.Truncate(value);

        return PrimitiveResult.Succeeded(FromDouble(ctx, fractionPart));
    }

    /// <summary>
    /// Primitive 53: Float exponent (get the binary exponent).
    /// </summary>
    public static PrimitiveResult Exponent(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver))
            return PrimitiveResult.Failed();

        double value = ToDouble(ctx, receiver);

        // Handle special cases
        if (value == 0.0)
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(0));

        if (double.IsNaN(value) || double.IsInfinity(value))
            return PrimitiveResult.Failed();

        // Get the IEEE 754 representation
        long bits = BitConverter.DoubleToInt64Bits(value);

        // Extract the biased exponent (bits 52-62)
        int biasedExponent = (int)((bits >> 52) & 0x7FF);

        // Subtract the bias (1023) to get the actual exponent
        int exponent = biasedExponent - 1023;

        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(exponent));
    }

    /// <summary>
    /// Primitive 54: Float timesTwoPower: (multiply by power of 2).
    /// </summary>
    public static PrimitiveResult TimesTwoPower(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        if (!IsFloat(ctx, receiver) || !ctx.IsSmallInteger(args[0]))
            return PrimitiveResult.Failed();

        double value = ToDouble(ctx, receiver);
        int power = ctx.DecodeSmallInteger(args[0]);

        // Use Math.ScaleB for efficient scaling by power of 2
        double result = Math.ScaleB(value, power);

        return PrimitiveResult.Succeeded(FromDouble(ctx, result));
    }
}
