using System;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Float primitives for floating-point arithmetic operations (primitives 40-54).
/// Based on Blue Book specification, these handle floating-point operations.
/// Float in Smalltalk-80 is stored as IEEE 754 double precision (8 bytes).
/// </summary>
public static class FloatPrimitives
{
    /// <summary>
    /// Converts a Float object to a C# double.
    /// </summary>
    /// <param name="floatRef">The Float object reference.</param>
    /// <returns>The corresponding double value.</returns>
    private static double ToDouble(SObjectRef floatRef)
    {
        nint oop = floatRef.AsObjectPointer();
        byte[] bytes = ObjectMemory.GetByteIndexedData(oop);

        // Smalltalk-80 stores Floats as 8-byte IEEE 754 double precision
        if (bytes.Length != 8)
            throw new InvalidOperationException("Invalid Float object: expected 8 bytes");

        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Creates a Float object from a C# double.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The Float object reference.</returns>
    private static SObjectRef FromDouble(double value, IPrimitiveContext context)
    {
        // Get Float class
        SObjectRef floatClass = context.GetClass("Float");

        // Allocate the Float object with 8 bytes for IEEE 754 double
        nint floatOop = context.AllocateObject(floatClass, 0, 8);

        // Convert double to byte array and set it
        byte[] bytes = BitConverter.GetBytes(value);
        ObjectMemory.SetByteIndexedData(floatOop, bytes);

        return SObjectRef.FromObjectPointer(floatOop);
    }

    /// <summary>
    /// Checks if the object is a Float.
    /// </summary>
    /// <param name="objRef">The object reference to check.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if the object is a Float, false otherwise.</returns>
    private static bool IsFloat(SObjectRef objRef, IPrimitiveContext context)
    {
        if (!context.IsHeapObject(objRef))
            return false;

        SObjectRef objClass = context.GetObjectClass(objRef.AsObjectPointer());
        SObjectRef floatClass = context.GetClass("Float");

        return objClass.Equals(floatClass);
    }

    /// <summary>
    /// Primitive 40: SmallInteger asFloat (convert SmallInteger to Float).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Float representation of the SmallInteger.</returns>
    public static PrimitiveResult PrimitiveAsFloat(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double value = (double)receiver.AsSmallInteger();
        return FromDouble(value, context);
    }

    /// <summary>
    /// Primitive 41: Float + (addition).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to add.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Sum as Float.</returns>
    public static PrimitiveResult PrimitiveAdd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        double result = left + right;

        return FromDouble(result, context);
    }

    /// <summary>
    /// Primitive 42: Float - (subtraction).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to subtract.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Difference as Float.</returns>
    public static PrimitiveResult PrimitiveSubtract(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        double result = left - right;

        return FromDouble(result, context);
    }

    /// <summary>
    /// Primitive 43: Float &lt; (less than).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        bool isLess = left < right;

        return context.GetSpecialObject(isLess ? 2 : 1);
    }

    /// <summary>
    /// Primitive 44: Float &gt; (greater than).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        bool isGreater = left > right;

        return context.GetSpecialObject(isGreater ? 2 : 1);
    }

    /// <summary>
    /// Primitive 45: Float &lt;= (less than or equal).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        bool isLessOrEqual = left <= right;

        return context.GetSpecialObject(isLessOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 46: Float &gt;= (greater than or equal).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        bool isGreaterOrEqual = left >= right;

        return context.GetSpecialObject(isGreaterOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 47: Float = (equality).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);

        // Note: This uses direct equality comparison, which may have issues with floating-point precision
        // Smalltalk implementations may handle this differently
        bool isEqual = left == right;

        return context.GetSpecialObject(isEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 48: Float ~= (not equal).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver not equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveNotEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        bool isNotEqual = left != right;

        return context.GetSpecialObject(isNotEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 49: Float * (multiplication).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float to multiply.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Product as Float.</returns>
    public static PrimitiveResult PrimitiveMultiply(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);
        double result = left * right;

        return FromDouble(result, context);
    }

    /// <summary>
    /// Primitive 50: Float / (division).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single argument containing the Float divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Quotient as Float.</returns>
    public static PrimitiveResult PrimitiveDivide(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !IsFloat(arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double left = ToDouble(receiver);
        double right = ToDouble(arguments[0]);

        // IEEE 754 handles division by zero as infinity or NaN
        double result = left / right;

        return FromDouble(result, context);
    }

    /// <summary>
    /// Primitive 51: Float truncated (convert to integer, truncate towards zero).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Truncated integer value as SmallInteger or LargeInteger.</returns>
    public static PrimitiveResult PrimitiveTruncated(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double value = ToDouble(receiver);

        // Check for special cases
        if (double.IsNaN(value) || double.IsInfinity(value))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        // Truncate towards zero
        double truncated = Math.Truncate(value);

        // Check if it fits in a SmallInteger (30-bit signed range)
        const int SmallIntegerMinValue = -(1 << 30);
        const int SmallIntegerMaxValue = (1 << 30) - 1;

        if (truncated >= SmallIntegerMinValue && truncated <= SmallIntegerMaxValue)
        {
            return SObjectRef.FromSmallInteger((int)truncated);
        }

        // Otherwise, create a LargeInteger
        var bigIntValue = new System.Numerics.BigInteger(truncated);
        byte[] bytes = bigIntValue.ToByteArray();

        string className = bigIntValue >= System.Numerics.BigInteger.Zero ?
            "LargePositiveInteger" : "LargeNegativeInteger";
        SObjectRef classRef = context.GetClass(className);

        nint largeIntOop = context.AllocateObject(classRef, 0, bytes.Length);
        ObjectMemory.SetByteIndexedData(largeIntOop, bytes);

        return SObjectRef.FromObjectPointer(largeIntOop);
    }

    /// <summary>
    /// Primitive 52: Float fractionPart (get the fractional part).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Fractional part as Float.</returns>
    public static PrimitiveResult PrimitiveFractionPart(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double value = ToDouble(receiver);

        // Get the fractional part (value - truncated value)
        double fractionPart = value - Math.Truncate(value);

        return FromDouble(fractionPart, context);
    }

    /// <summary>
    /// Primitive 53: Float exponent (get the binary exponent).
    /// </summary>
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">No arguments expected.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Exponent as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveExponent(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double value = ToDouble(receiver);

        // Handle special cases
        if (value == 0.0)
            return SObjectRef.FromSmallInteger(0);

        if (double.IsNaN(value) || double.IsInfinity(value))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

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
    /// <param name="receiver">The Float receiver.</param>
    /// <param name="arguments">Single SmallInteger argument containing the power.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Result as Float.</returns>
    public static PrimitiveResult PrimitiveTimesTwoPower(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!IsFloat(receiver, context) || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        double value = ToDouble(receiver);
        int power = arguments[0].AsSmallInteger();

        // Use Math.ScaleB for efficient scaling by power of 2
        // This is equivalent to value * 2^power but more efficient
        double result = Math.ScaleB(value, power);

        return FromDouble(result, context);
    }

    /// <summary>
    /// Registers all Float primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(40, "SmallInteger asFloat", PrimitiveAsFloat);
        table.Register(41, "Float+", PrimitiveAdd);
        table.Register(42, "Float-", PrimitiveSubtract);
        table.Register(43, "Float<", PrimitiveLessThan);
        table.Register(44, "Float>", PrimitiveGreaterThan);
        table.Register(45, "Float<=", PrimitiveLessThanOrEqual);
        table.Register(46, "Float>=", PrimitiveGreaterThanOrEqual);
        table.Register(47, "Float=", PrimitiveEqual);
        table.Register(48, "Float~=", PrimitiveNotEqual);
        table.Register(49, "Float*", PrimitiveMultiply);
        table.Register(50, "Float/", PrimitiveDivide);
        table.Register(51, "Float truncated", PrimitiveTruncated);
        table.Register(52, "Float fractionPart", PrimitiveFractionPart);
        table.Register(53, "Float exponent", PrimitiveExponent);
        table.Register(54, "Float timesTwoPower:", PrimitiveTimesTwoPower);
    }
}