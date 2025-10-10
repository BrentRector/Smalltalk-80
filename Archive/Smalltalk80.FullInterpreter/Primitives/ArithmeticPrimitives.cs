using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Arithmetic primitives for SmallInteger operations (primitives 1-17).
/// Based on Blue Book specification, these handle basic arithmetic and bitwise operations.
/// Failures due to overflow allow Smalltalk to promote to LargeInteger.
/// </summary>
public static class ArithmeticPrimitives
{
    /// <summary>
    /// SmallInteger range limits (30-bit signed integer due to tag bit).
    /// </summary>
    private const int SmallIntegerMinValue = -(1 << 30); // -1,073,741,824
    private const int SmallIntegerMaxValue = (1 << 30) - 1; // 1,073,741,823

    /// <summary>
    /// Primitive 1: SmallInteger + (addition).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to add.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Sum as SmallInteger, or failure if overflow or type mismatch.</returns>
    public static PrimitiveResult PrimitiveAdd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Use long to detect overflow
        long result = (long)receiver.AsSmallInteger() + arguments[0].AsSmallInteger();

        // Check for SmallInteger range overflow (30-bit signed range)
        if (result < SmallIntegerMinValue || result > SmallIntegerMaxValue)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        return SObjectRef.FromSmallInteger((int)result);
    }

    /// <summary>
    /// Primitive 2: SmallInteger - (subtraction).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to subtract.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Difference as SmallInteger, or failure if overflow or type mismatch.</returns>
    public static PrimitiveResult PrimitiveSubtract(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Use long to detect overflow/underflow
        long result = (long)receiver.AsSmallInteger() - arguments[0].AsSmallInteger();

        // Check for SmallInteger range overflow/underflow (30-bit signed range)
        if (result < SmallIntegerMinValue || result > SmallIntegerMaxValue)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        return SObjectRef.FromSmallInteger((int)result);
    }

    /// <summary>
    /// Primitive 3: SmallInteger &lt; (less than).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isLess = receiver.AsSmallInteger() < arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isLess ? 2 : 1);
    }

    /// <summary>
    /// Primitive 4: SmallInteger &gt; (greater than).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isGreater = receiver.AsSmallInteger() > arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isGreater ? 2 : 1);
    }

    /// <summary>
    /// Primitive 5: SmallInteger &lt;= (less than or equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isLessOrEqual = receiver.AsSmallInteger() <= arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isLessOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 6: SmallInteger &gt;= (greater than or equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isGreaterOrEqual = receiver.AsSmallInteger() >= arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isGreaterOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 7: SmallInteger = (equality).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isEqual = receiver.AsSmallInteger() == arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 8: SmallInteger ~= (not equal).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver not equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveNotEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        bool isNotEqual = receiver.AsSmallInteger() != arguments[0].AsSmallInteger();
        return context.GetSpecialObject(isNotEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 9: SmallInteger * (multiplication).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to multiply.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Product as SmallInteger, or failure if overflow or type mismatch.</returns>
    public static PrimitiveResult PrimitiveMultiply(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Use long to detect overflow
        long result = (long)receiver.AsSmallInteger() * arguments[0].AsSmallInteger();

        // Check for SmallInteger range overflow (30-bit signed range)
        if (result < SmallIntegerMinValue || result > SmallIntegerMaxValue)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        return SObjectRef.FromSmallInteger((int)result);
    }

    /// <summary>
    /// Primitive 10: SmallInteger / (division).
    /// In Smalltalk, / returns a Fraction if not evenly divisible.
    /// This primitive fails for non-exact division, allowing Smalltalk to handle it.
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Quotient as SmallInteger if exact, or failure otherwise.</returns>
    public static PrimitiveResult PrimitiveDivide(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int dividend = receiver.AsSmallInteger();
        int divisor = arguments[0].AsSmallInteger();

        if (divisor == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Check if division is exact
        if (dividend % divisor != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument); // Let Smalltalk create Fraction

        return SObjectRef.FromSmallInteger(dividend / divisor);
    }

    /// <summary>
    /// Primitive 11: SmallInteger \\ (modulo).
    /// Returns the remainder of division with same sign as divisor.
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Remainder as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveModulo(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int dividend = receiver.AsSmallInteger();
        int divisor = arguments[0].AsSmallInteger();

        if (divisor == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Smalltalk modulo has same sign as divisor (Blue Book spec)
        // Formula: a \\ b = a - (a // b * b)
        int quotient = dividend / divisor;
        if ((dividend < 0) != (divisor < 0) && dividend % divisor != 0)
            quotient--;

        int remainder = dividend - (quotient * divisor);
        return SObjectRef.FromSmallInteger(remainder);
    }

    /// <summary>
    /// Primitive 12: SmallInteger // (integer division).
    /// Returns the floor of the division (rounds towards negative infinity).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Floor of quotient as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveIntegerDivide(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int dividend = receiver.AsSmallInteger();
        int divisor = arguments[0].AsSmallInteger();

        if (divisor == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Floor division: rounds towards negative infinity
        int quotient = dividend / divisor;
        if ((dividend < 0) != (divisor < 0) && dividend % divisor != 0)
            quotient--;

        return SObjectRef.FromSmallInteger(quotient);
    }

    /// <summary>
    /// Primitive 13: SmallInteger quo: (truncated division).
    /// Returns the truncated quotient (rounds towards zero).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Truncated quotient as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveQuo(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int dividend = receiver.AsSmallInteger();
        int divisor = arguments[0].AsSmallInteger();

        if (divisor == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Truncated division: rounds towards zero
        return SObjectRef.FromSmallInteger(dividend / divisor);
    }

    /// <summary>
    /// Primitive 14: SmallInteger bitAnd: (bitwise AND).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to AND with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise AND result as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveBitAnd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int result = receiver.AsSmallInteger() & arguments[0].AsSmallInteger();
        return SObjectRef.FromSmallInteger(result);
    }

    /// <summary>
    /// Primitive 15: SmallInteger bitOr: (bitwise OR).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to OR with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise OR result as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveBitOr(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int result = receiver.AsSmallInteger() | arguments[0].AsSmallInteger();
        return SObjectRef.FromSmallInteger(result);
    }

    /// <summary>
    /// Primitive 16: SmallInteger bitXor: (bitwise XOR).
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing the SmallInteger to XOR with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise XOR result as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveBitXor(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int result = receiver.AsSmallInteger() ^ arguments[0].AsSmallInteger();
        return SObjectRef.FromSmallInteger(result);
    }

    /// <summary>
    /// Primitive 17: SmallInteger bitShift: (bit shift).
    /// Positive argument shifts left, negative shifts right.
    /// </summary>
    /// <param name="receiver">The SmallInteger receiver.</param>
    /// <param name="arguments">Single argument containing shift count (positive=left, negative=right).</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Shifted value as SmallInteger, or failure if overflow.</returns>
    public static PrimitiveResult PrimitiveBitShift(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        int value = receiver.AsSmallInteger();
        int shiftCount = arguments[0].AsSmallInteger();

        // Handle shift bounds
        if (shiftCount is > 31 or < -31)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        int result = shiftCount switch
        {
            0 => value,
            > 0 => LeftShiftWithOverflowCheck(value, shiftCount),
            < 0 => value >> -shiftCount // Right shift (arithmetic)
        };

        // Check if left shift caused overflow
        if (shiftCount > 0 && result == int.MinValue)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        return SObjectRef.FromSmallInteger(result);

        // Local function for left shift with overflow detection
        int LeftShiftWithOverflowCheck(int value, int shift)
        {
            // Check if left shift would overflow SmallInteger range
            long result = (long)value << shift;
            if (result < SmallIntegerMinValue || result > SmallIntegerMaxValue)
                return int.MinValue; // Signal overflow
            return (int)result;
        }
    }

    /// <summary>
    /// Registers all arithmetic primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(1, "primitiveAdd", PrimitiveAdd);
        table.Register(2, "primitiveSubtract", PrimitiveSubtract);
        table.Register(3, "primitiveLessThan", PrimitiveLessThan);
        table.Register(4, "primitiveGreaterThan", PrimitiveGreaterThan);
        table.Register(5, "primitiveLessThanOrEqual", PrimitiveLessThanOrEqual);
        table.Register(6, "primitiveGreaterThanOrEqual", PrimitiveGreaterThanOrEqual);
        table.Register(7, "primitiveEqual", PrimitiveEqual);
        table.Register(8, "primitiveNotEqual", PrimitiveNotEqual);
        table.Register(9, "primitiveMultiply", PrimitiveMultiply);
        table.Register(10, "primitiveDivide", PrimitiveDivide);
        table.Register(11, "primitiveModulo", PrimitiveModulo);
        table.Register(12, "primitiveIntegerDivide", PrimitiveIntegerDivide);
        table.Register(13, "primitiveQuo", PrimitiveQuo);
        table.Register(14, "primitiveBitAnd", PrimitiveBitAnd);
        table.Register(15, "primitiveBitOr", PrimitiveBitOr);
        table.Register(16, "primitiveBitXor", PrimitiveBitXor);
        table.Register(17, "primitiveBitShift", PrimitiveBitShift);
    }
}