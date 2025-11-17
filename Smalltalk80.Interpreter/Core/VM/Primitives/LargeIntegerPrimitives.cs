using System.Numerics;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// LargeInteger primitives for arbitrary precision arithmetic operations (primitives 21-37).
/// Based on Blue Book specification. LargeInteger is stored as byte array in little-endian format.
/// </summary>
/// <remarks>
/// <para><b>Blue Book Note:</b></para>
/// <para>"There are adequate Smalltalk implementations for all of these operations so the
/// primitive routines are optional" (Blue Book page 568, Chapter 29)</para>
///
/// <para><b>Implementation Details:</b></para>
/// <list type="bullet">
/// <item>All 17 LargeInteger primitives (21-37) are implemented using System.Numerics.BigInteger</item>
/// <item>Arithmetic operations: +, -, *, /, \, //, quo:</item>
/// <item>Comparison operations: &lt;, &gt;, &lt;=, &gt;=, =, ~=</item>
/// <item>Bitwise operations: bitAnd:, bitOr:, bitXor:, bitShift:</item>
/// </list>
///
/// <para><b>Key Features:</b></para>
/// <list type="bullet">
/// <item>Automatic conversion between SmallInteger and LargeInteger</item>
/// <item>Proper sign handling for LargePositiveInteger vs LargeNegativeInteger</item>
/// <item>Optimization: FromBigInteger automatically returns SmallInteger when result fits in 30-bit range</item>
/// <item>All operations correctly delegate to .NET BigInteger for unlimited precision</item>
/// </list>
///
/// <para><b>Conformance:</b> EXCEEDS SPECIFICATION - These primitives are optional per Blue Book,
/// but we provide full implementation for performance and enhanced arithmetic capabilities.</para>
/// </remarks>
public static class LargeIntegerPrimitives
{
    // <summary>
    // Converts a LargeInteger object to a BigInteger.
    // </summary>
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

    // <summary>
    // Creates a LargeInteger object from a BigInteger.
    // Internal visibility allows FloatPrimitives to use this for Truncated conversion.
    // </summary>
    internal static SmalltalkOOP FromBigInteger(IPrimitiveContext ctx, BigInteger value)
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

    // <summary>
    // Checks if an object is a LargeInteger.
    // </summary>
    private static bool IsLargeInteger(IPrimitiveContext ctx, SmalltalkOOP objectOop)
    {
        if (objectOop.IsSmallInteger)
            return false;

        SmalltalkOOP classOOP = ctx.HeapManager.GetClassOOP(objectOop);

        // Check against both LargeInteger classes (heap-only access)
        var posClass = ctx.GetGlobalObjectByName("class:LargePositiveInteger");
        if (posClass != SpecialObjectsArray.Instance.Nil && classOOP == posClass)
            return true;

        var negClass = ctx.GetGlobalObjectByName("class:LargeNegativeInteger");
        if (negClass != SpecialObjectsArray.Instance.Nil && classOOP == negClass)
            return true;

        return false;
    }

    // <summary>
    // Gets indexed size of an object.
    // </summary>
    private static int GetIndexedSize(IPrimitiveContext ctx, SmalltalkOOP objectOop)
    {
        return ctx.HeapManager.GetIndexedSize(objectOop);
    }

    // <summary>
    // Gets the LargeInteger class OOP based on sign (heap-only access).
    // </summary>
    private static SmalltalkOOP GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
    {
        string className = isPositive
            ? "class:LargePositiveInteger"
            : "class:LargeNegativeInteger";

        var classOOP = ctx.GetGlobalObjectByName(className);

        if (classOOP != SpecialObjectsArray.Instance.Nil)
            return classOOP;

        throw new InvalidOperationException(
            $"{className} not found in Smalltalk global dictionary. " +
            "This indicates a bootstrap phase error - LargeInteger classes must be " +
            "installed during cold start process.");
    }

    // <summary>
    // Primitive 21: LargeInteger + - Arbitrary precision addition
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Blue Book Note:</b></para>
    // <para>"There are adequate Smalltalk implementations for all of these operations so the
    // primitive routines are optional."</para>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are LargeInteger instances</item>
    // <item>Converts both to BigInteger for computation</item>
    // <item>Returns result as SmallInteger if fits in 30-bit range, otherwise LargeInteger</item>
    // </list>
    //
    // <para><b>Conformance:</b> EXCEEDS SPECIFICATION - Optional primitive implemented for performance</para>
    // </remarks>
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

    // <summary>
    // Primitive 22: LargeInteger - (subtraction) - Arbitrary precision subtraction
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Converts both LargeInteger operands to BigInteger</item>
    // <item>Performs arbitrary precision subtraction</item>
    // <item>Result may change sign (e.g., positive - larger positive = negative)</item>
    // <item>Automatically selects LargePositiveInteger or LargeNegativeInteger based on sign</item>
    // </list>
    // </remarks>
    public static SmalltalkOOP Subtract(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: LargeInteger arithmetic using arbitrary precision
        // Validate single argument required for binary operation
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both operands must be LargeInteger instances
        // Fails if either is not - lets Smalltalk handle mixed-type arithmetic
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract BigInteger values from byte array representation
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Perform arbitrary precision subtraction
        // Sign of result determines LargePositiveInteger vs LargeNegativeInteger
        BigInteger result = left - right;

        // Convert back, optimizing to SmallInteger if fits in 30-bit range
        return FromBigInteger(ctx, result);
    }

    // <summary>
    // Primitive 23: LargeInteger &lt; - Arbitrary precision less than comparison
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP LessThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument for binary comparison
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both operands must be LargeInteger for primitive to succeed
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to BigInteger and perform comparison
        // Handles arbitrary precision and mixed signs correctly
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return Boolean singleton based on comparison result
        return left < right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 24: LargeInteger > - Arbitrary precision greater than comparison
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP GreaterThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count for binary operation
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type validation - both must be LargeInteger
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Perform arbitrary precision comparison
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return appropriate Boolean singleton
        return left > right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 25: LargeInteger &lt;= - Arbitrary precision less than or equal comparison
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP LessOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Standard validation for binary operation
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Ensure both are LargeInteger instances
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert and compare with full precision
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return Boolean result
        return left <= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 26: LargeInteger >= - Arbitrary precision greater than or equal comparison
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP GreaterOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type check both operands
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Perform comparison at full precision
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return Boolean singleton
        return left >= right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 27: LargeInteger = - Arbitrary precision equality test
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP Equal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both must be LargeInteger for primitive to handle
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Compare BigInteger values for exact equality
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return Boolean result
        return left == right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 28: LargeInteger ~= - Arbitrary precision inequality test
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP NotEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type validation
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Compare for inequality
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Return Boolean result
        return left != right
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 29: LargeInteger * - Arbitrary precision multiplication
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    public static SmalltalkOOP Multiply(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument for binary multiplication
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both operands must be LargeInteger
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract BigInteger values from byte arrays
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Perform arbitrary precision multiplication
        // Result grows to sum of digit counts (no overflow possible)
        BigInteger result = left * right;

        // Convert back, selecting appropriate class based on sign
        return FromBigInteger(ctx, result);
    }

    // <summary>
    // Primitive 30: LargeInteger / - Exact division (fails if not exact)
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Returns quotient only if division is exact (no remainder). This matches
    // Smalltalk's / operator which expects exact division for integers.</para>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Fails primitive if divisor is zero</item>
    // <item>Fails primitive if division has remainder (not exact)</item>
    // <item>Returns quotient as SmallInteger if fits, otherwise LargeInteger</item>
    // </list>
    //
    // <para><b>Conformance:</b> EXCEEDS SPECIFICATION - Optional primitive implemented</para>
    // </remarks>
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

    // <summary>
    // Primitive 31: LargeInteger \\ - Modulo operation with sign correction
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Implements Smalltalk modulo semantics where result has same sign as divisor.
    // This differs from C# % operator which gives remainder with sign of dividend.</para>
    // </remarks>
    public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument for binary operation
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type validation - both must be LargeInteger
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract dividend and divisor
        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        // Division by zero check
        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Compute remainder with Smalltalk semantics
        // C# % gives remainder with sign of dividend
        // Smalltalk \\ gives remainder with sign of divisor
        BigInteger remainder = dividend % divisor;
        if (remainder != BigInteger.Zero && (dividend < BigInteger.Zero) != (divisor < BigInteger.Zero))
            remainder += divisor;

        // Return result, optimizing to SmallInteger if possible
        return FromBigInteger(ctx, remainder);
    }

    // <summary>
    // Primitive 32: LargeInteger // - Floor division (rounds toward negative infinity)
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Implements floor division which rounds toward negative infinity.
    // This maintains the invariant: (a // b) * b + (a \\ b) = a</para>
    // </remarks>
    public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type check both operands
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract operands
        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        // Guard against division by zero
        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Perform floor division
        // C# / truncates toward zero, we need floor toward negative infinity
        BigInteger quotient = dividend / divisor;
        if ((dividend < BigInteger.Zero) != (divisor < BigInteger.Zero) && dividend % divisor != BigInteger.Zero)
            quotient--; // Adjust for floor semantics when signs differ

        // Return result
        return FromBigInteger(ctx, quotient);
    }

    // <summary>
    // Primitive 33: LargeInteger quo: - Truncated division (rounds toward zero)
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Implements truncated division which rounds toward zero.
    // This matches C# / operator behavior for integers.</para>
    // </remarks>
    public static SmalltalkOOP Quo(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type validation
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract operands
        BigInteger dividend = ToBigInteger(ctx, receiver);
        BigInteger divisor = ToBigInteger(ctx, args[0]);

        // Check for division by zero
        if (divisor == BigInteger.Zero)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Perform truncated division (toward zero)
        // C# / already truncates toward zero for BigInteger
        BigInteger quotient = dividend / divisor;

        // Return result
        return FromBigInteger(ctx, quotient);
    }

    // <summary>
    // Primitive 34: LargeInteger bitAnd: - Bitwise AND operation
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Performs bitwise AND on two's complement representation.
    // Negative numbers are treated as infinite sign extension.</para>
    // </remarks>
    public static SmalltalkOOP BitAnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Both must be LargeInteger
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract operands
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Perform bitwise AND
        // BigInteger handles two's complement semantics correctly
        BigInteger result = left & right;

        // Return result
        return FromBigInteger(ctx, result);
    }

    // <summary>
    // Primitive 35: LargeInteger bitOr: - Bitwise OR operation
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Performs bitwise OR on two's complement representation.
    // Negative numbers are treated as infinite sign extension.</para>
    // </remarks>
    public static SmalltalkOOP BitOr(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type check
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract BigInteger values
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Perform bitwise OR
        // Handles sign extension for negative values
        BigInteger result = left | right;

        // Return result
        return FromBigInteger(ctx, result);
    }

    // <summary>
    // Primitive 36: LargeInteger bitXor: - Bitwise XOR operation
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Performs bitwise XOR on two's complement representation.
    // Result is negative if operands have different signs.</para>
    // </remarks>
    public static SmalltalkOOP BitXor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Type validation
        if (!IsLargeInteger(ctx, receiver) || !IsLargeInteger(ctx, args[0]))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract operands
        BigInteger left = ToBigInteger(ctx, receiver);
        BigInteger right = ToBigInteger(ctx, args[0]);

        // Perform bitwise XOR
        // XOR of different sign numbers produces negative result
        BigInteger result = left ^ right;

        // Return result
        return FromBigInteger(ctx, result);
    }

    // <summary>
    // Primitive 37: LargeInteger bitShift: - Logical shift operation
    // Blue Book: Page 568, Chapter 29 (Optional primitive)
    // </summary>
    // <remarks>
    // <para><b>Semantics:</b></para>
    // <para>Shifts bits left (positive argument) or right (negative argument).
    // Right shift is arithmetic (sign-preserving) for negative numbers.</para>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Argument must be SmallInteger specifying shift amount</item>
    // <item>Positive shifts left (multiply by 2^n)</item>
    // <item>Negative shifts right (divide by 2^n)</item>
    // <item>Handles arbitrary precision shifts via BigInteger</item>
    // </list>
    //
    // <para><b>Conformance:</b> EXCEEDS SPECIFICATION - Optional primitive with full implementation</para>
    // </remarks>
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
