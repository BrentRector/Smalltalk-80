using System.Numerics;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// LargeInteger primitives for arbitrary precision arithmetic operations (primitives 21-37).
/// Based on Blue Book specification, these handle arithmetic operations on large integers.
/// LargeInteger in Smalltalk-80 is stored as a byte array in little-endian format.
/// </summary>
public static class LargeIntegerPrimitives
{
    /// <summary>
    /// Converts a LargeInteger object to a System.Numerics.BigInteger.
    /// </summary>
    /// <param name="largeIntRef">The LargeInteger object reference.</param>
    /// <returns>The corresponding BigInteger value.</returns>
    private static BigInteger ToBigInteger(SObjectRef largeIntRef)
    {
        nint oop = largeIntRef.AsObjectPointer();
        byte[] bytes = ObjectMemory.GetByteIndexedData(oop);

        // Smalltalk-80 stores LargeIntegers in little-endian format
        // BigInteger constructor expects little-endian with sign extension
        return new BigInteger(bytes);
    }

    /// <summary>
    /// Creates a LargeInteger object from a System.Numerics.BigInteger.
    /// </summary>
    /// <param name="value">The BigInteger value.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The LargeInteger object reference.</returns>
    private static SObjectRef FromBigInteger(BigInteger value, IPrimitiveContext context)
    {
        // Get the byte representation in little-endian format
        byte[] bytes = value.ToByteArray();

        // Get LargePositiveInteger or LargeNegativeInteger class
        string className = value >= BigInteger.Zero ? "LargePositiveInteger" : "LargeNegativeInteger";
        SObjectRef classRef = context.GetClass(className);

        // Allocate the LargeInteger object with the byte array size
        nint largeIntOop = context.AllocateObject(classRef, 0, bytes.Length);

        // Set the byte data
        ObjectMemory.SetByteIndexedData(largeIntOop, bytes);

        return SObjectRef.FromObjectPointer(largeIntOop);
    }

    /// <summary>
    /// Checks if the receiver and argument are both LargeIntegers.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="argument">The argument object.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if both are LargeIntegers, false otherwise.</returns>
    private static bool AreBothLargeIntegers(SObjectRef receiver, SObjectRef argument, IPrimitiveContext context)
    {
        if (!context.IsHeapObject(receiver) || !context.IsHeapObject(argument))
            return false;

        SObjectRef receiverClass = context.GetObjectClass(receiver.AsObjectPointer());
        SObjectRef argumentClass = context.GetObjectClass(argument.AsObjectPointer());

        SObjectRef largePositiveIntClass = context.GetClass("LargePositiveInteger");
        SObjectRef largeNegativeIntClass = context.GetClass("LargeNegativeInteger");

        bool receiverIsLargeInt = receiverClass.Equals(largePositiveIntClass) ||
                                   receiverClass.Equals(largeNegativeIntClass);
        bool argumentIsLargeInt = argumentClass.Equals(largePositiveIntClass) ||
                                   argumentClass.Equals(largeNegativeIntClass);

        return receiverIsLargeInt && argumentIsLargeInt;
    }

    /// <summary>
    /// Primitive 21: LargeInteger + (addition).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to add.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Sum as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveAdd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left + right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 22: LargeInteger - (subtraction).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to subtract.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Difference as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveSubtract(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left - right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 23: LargeInteger &lt; (less than).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isLess = left < right;

        return context.GetSpecialObject(isLess ? 2 : 1);
    }

    /// <summary>
    /// Primitive 24: LargeInteger &gt; (greater than).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt; argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThan(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isGreater = left > right;

        return context.GetSpecialObject(isGreater ? 2 : 1);
    }

    /// <summary>
    /// Primitive 25: LargeInteger &lt;= (less than or equal).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &lt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveLessThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isLessOrEqual = left <= right;

        return context.GetSpecialObject(isLessOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 26: LargeInteger &gt;= (greater than or equal).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver &gt;= argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveGreaterThanOrEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isGreaterOrEqual = left >= right;

        return context.GetSpecialObject(isGreaterOrEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 27: LargeInteger = (equality).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isEqual = left == right;

        return context.GetSpecialObject(isEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 28: LargeInteger ~= (not equal).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to compare.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if receiver not equals argument, false otherwise.</returns>
    public static PrimitiveResult PrimitiveNotEqual(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        bool isNotEqual = left != right;

        return context.GetSpecialObject(isNotEqual ? 2 : 1);
    }

    /// <summary>
    /// Primitive 29: LargeInteger * (multiplication).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to multiply.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Product as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveMultiply(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left * right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 30: LargeInteger / (division).
    /// In Smalltalk, / returns a Fraction if not evenly divisible.
    /// This primitive fails for non-exact division, allowing Smalltalk to handle it.
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Quotient as LargeInteger if exact, or failure otherwise.</returns>
    public static PrimitiveResult PrimitiveDivide(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger dividend = ToBigInteger(receiver);
        BigInteger divisor = ToBigInteger(arguments[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Check if division is exact
        BigInteger remainder;
        BigInteger quotient = BigInteger.DivRem(dividend, divisor, out remainder);

        if (remainder != BigInteger.Zero)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument); // Let Smalltalk create Fraction

        return FromBigInteger(quotient, context);
    }

    /// <summary>
    /// Primitive 31: LargeInteger \\ (modulo).
    /// Returns the remainder of division with same sign as divisor.
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Remainder as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveModulo(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger dividend = ToBigInteger(receiver);
        BigInteger divisor = ToBigInteger(arguments[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Smalltalk modulo has same sign as divisor
        BigInteger remainder = dividend % divisor;
        if (remainder != BigInteger.Zero && (dividend < BigInteger.Zero) != (divisor < BigInteger.Zero))
            remainder += divisor;

        return FromBigInteger(remainder, context);
    }

    /// <summary>
    /// Primitive 32: LargeInteger // (integer division).
    /// Returns the floor of the division (rounds towards negative infinity).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Floor of quotient as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveIntegerDivide(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger dividend = ToBigInteger(receiver);
        BigInteger divisor = ToBigInteger(arguments[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Floor division: rounds towards negative infinity
        BigInteger quotient = dividend / divisor;
        if ((dividend < BigInteger.Zero) != (divisor < BigInteger.Zero) && dividend % divisor != BigInteger.Zero)
            quotient--;

        return FromBigInteger(quotient, context);
    }

    /// <summary>
    /// Primitive 33: LargeInteger quo: (truncated division).
    /// Returns the truncated quotient (rounds towards zero).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger divisor.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Truncated quotient as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveQuo(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger dividend = ToBigInteger(receiver);
        BigInteger divisor = ToBigInteger(arguments[0]);

        if (divisor == BigInteger.Zero)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.DivideByZero);

        // Truncated division: rounds towards zero
        BigInteger quotient = dividend / divisor;

        return FromBigInteger(quotient, context);
    }

    /// <summary>
    /// Primitive 34: LargeInteger bitAnd: (bitwise AND).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to AND with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise AND result as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveBitAnd(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left & right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 35: LargeInteger bitOr: (bitwise OR).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to OR with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise OR result as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveBitOr(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left | right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 36: LargeInteger bitXor: (bitwise XOR).
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing the LargeInteger to XOR with.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Bitwise XOR result as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveBitXor(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!AreBothLargeIntegers(receiver, arguments[0], context))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger left = ToBigInteger(receiver);
        BigInteger right = ToBigInteger(arguments[0]);
        BigInteger result = left ^ right;

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Primitive 37: LargeInteger bitShift: (bit shift).
    /// Positive argument shifts left, negative shifts right.
    /// </summary>
    /// <param name="receiver">The LargeInteger receiver.</param>
    /// <param name="arguments">Single argument containing shift count (positive=left, negative=right).</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Shifted value as LargeInteger.</returns>
    public static PrimitiveResult PrimitiveBitShift(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver) || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Check if receiver is a LargeInteger
        SObjectRef receiverClass = context.GetObjectClass(receiver.AsObjectPointer());
        SObjectRef largePositiveIntClass = context.GetClass("LargePositiveInteger");
        SObjectRef largeNegativeIntClass = context.GetClass("LargeNegativeInteger");

        if (!receiverClass.Equals(largePositiveIntClass) && !receiverClass.Equals(largeNegativeIntClass))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        BigInteger value = ToBigInteger(receiver);
        int shiftCount = arguments[0].AsSmallInteger();

        BigInteger result = shiftCount switch
        {
            0 => value,
            > 0 => value << shiftCount,
            < 0 => value >> -shiftCount
        };

        return FromBigInteger(result, context);
    }

    /// <summary>
    /// Registers all LargeInteger primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(21, "LargeInteger+", PrimitiveAdd);
        table.Register(22, "LargeInteger-", PrimitiveSubtract);
        table.Register(23, "LargeInteger<", PrimitiveLessThan);
        table.Register(24, "LargeInteger>", PrimitiveGreaterThan);
        table.Register(25, "LargeInteger<=", PrimitiveLessThanOrEqual);
        table.Register(26, "LargeInteger>=", PrimitiveGreaterThanOrEqual);
        table.Register(27, "LargeInteger=", PrimitiveEqual);
        table.Register(28, "LargeInteger~=", PrimitiveNotEqual);
        table.Register(29, "LargeInteger*", PrimitiveMultiply);
        table.Register(30, "LargeInteger/", PrimitiveDivide);
        table.Register(31, "LargeInteger\\\\", PrimitiveModulo);
        table.Register(32, "LargeInteger//", PrimitiveIntegerDivide);
        table.Register(33, "LargeInteger quo:", PrimitiveQuo);
        table.Register(34, "LargeInteger bitAnd:", PrimitiveBitAnd);
        table.Register(35, "LargeInteger bitOr:", PrimitiveBitOr);
        table.Register(36, "LargeInteger bitXor:", PrimitiveBitXor);
        table.Register(37, "LargeInteger bitShift:", PrimitiveBitShift);
    }
}