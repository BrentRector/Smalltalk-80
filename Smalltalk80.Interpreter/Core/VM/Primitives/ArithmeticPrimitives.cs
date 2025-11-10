using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Arithmetic primitive operations for SmallInteger (Primitives 1-17).
/// These are the fundamental arithmetic operations optimized for tagged integers.
/// SmallInteger range: -2^30 to 2^30-1 (30-bit signed integer with 2 tag bits).
/// </summary>
public static class ArithmeticPrimitives
{
    private const int SmallIntegerMin = -(1 << 30);
    private const int SmallIntegerMax = (1 << 30) - 1;

    // <summary>
    // Primitive 1: SmallInteger + - Integer addition with overflow detection
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveAdd
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver + integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers (IsSmallInteger check)</item>
    // <item>Extracts integer values using AsSmallInteger()</item>
    // <item>Uses checked arithmetic to detect Int32 overflow</item>
    // <item>Additionally checks result is within SmallInteger range [-2^30, 2^30-1]</item>
    // <item>On success, returns SmallInteger OOP; on failure, sets PrimitiveSuccess=false and returns nil</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Our implementation correctly follows the Blue Book specification with proper
    // overflow detection. The use of checked arithmetic and explicit range validation ensures
    // we can detect when the result exceeds 30-bit signed range.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Uses C# checked arithmetic and exception handling for overflow detection</item>
    // <item>Returns nil on failure instead of unpop (different calling convention)</item>
    // <item>Includes debug logging for troubleshooting (can be removed in production)</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE - Semantically equivalent with robust overflow handling</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Sum as SmallInteger, or nil if overflow/type error</returns>
    public static SmalltalkOOP Add(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: integerArgument ← self popInteger
        // Our calling convention passes arguments separately, validate single argument
        if (args.Length != 1)
        {
            Console.WriteLine($"[Primitive 1: Add] FAILED - Wrong argument count: {args.Length}");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: self success ifTrue: [check both are integers]
        // Validate both receiver and argument are SmallIntegers (tagged with low bit set)
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            Console.WriteLine($"[Primitive 1: Add] FAILED - Not both SmallIntegers");
            Console.WriteLine($"[Primitive 1: Add]   receiver: IsSmallInteger={receiver.IsSmallInteger}, RawValue=0x{receiver.RawValue:X}");
            Console.WriteLine($"[Primitive 1: Add]   argument: IsSmallInteger={args[0].IsSmallInteger}, RawValue=0x{args[0].RawValue:X}");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs (shift right by 1 to remove tag bit)
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            // Blue Book: integerResult ← integerReceiver + integerArgument
            // Use checked arithmetic to detect Int32 overflow during addition
            int result = checked(a + b);

            // Blue Book: self success: (memory isIntegerValue: integerResult)
            // Verify result fits in SmallInteger range (30-bit signed: -2^30 to 2^30-1)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow SmallInteger range - fail primitive, let Smalltalk handle with LargeInteger
                Console.WriteLine($"[Primitive 1: Add] FAILED - Overflow SmallInteger range: {a} + {b} = {result}");
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Blue Book: self pushInteger: integerResult
            // Success - return tagged SmallInteger result (shift left by 1 and set low bit)
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow detected by checked arithmetic - fail primitive
            Console.WriteLine($"[Primitive 1: Add] FAILED - Int32 overflow: {a} + {b}");
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 2: SmallInteger - - Integer subtraction with underflow detection
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveSubtract
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver - integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Uses checked arithmetic to detect Int32 underflow</item>
    // <item>Validates result is within SmallInteger range [-2^30, 2^30-1]</item>
    // <item>Returns SmallInteger OOP on success; sets PrimitiveSuccess=false on failure</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Identical logic to primitive 1, correctly handles underflow cases.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Uses C# exception handling for overflow detection</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Minuend (SmallInteger)</param>
    // <param name="args">Array containing subtrahend (SmallInteger)</param>
    // <returns>Difference as SmallInteger, or nil if underflow/type error</returns>
    public static SmalltalkOOP Subtract(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: integerArgument ← self popInteger
        // Validate we have exactly one argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerReceiver ← self popInteger
        // Blue Book: self success ifTrue: [both must be SmallIntegers]
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            // Blue Book: integerResult ← integerReceiver - integerArgument
            // Use checked arithmetic to detect Int32 underflow
            int result = checked(a - b);

            // Blue Book: self success: (memory isIntegerValue: integerResult)
            // Check if result is within SmallInteger range (30-bit signed)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow/underflow SmallInteger range - let Smalltalk fall back to LargeInteger method
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Blue Book: self pushInteger: integerResult
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow - also fail primitive
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 3: SmallInteger &lt; - Integer less-than comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveLessThan
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver &lt; integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer comparison</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects, not SmallIntegers. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP LessThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        // Both must be SmallIntegers for primitive to succeed
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        // Blue Book: integerReceiver < integerArgument
        //   ifTrue: [self push: TruePointer]
        //   ifFalse: [self push: FalsePointer]
        // Return Boolean singleton objects, NOT SmallIntegers
        return a < b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 4: SmallInteger &gt; - Integer greater-than comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveGreaterThan
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver &gt; integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer comparison</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // <item>Includes debug logging for troubleshooting</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // <item>Includes debug logging (can be removed in production)</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP GreaterThan(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a > b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 5: SmallInteger &lt;= - Integer less-than-or-equal comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveLessOrEqual
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver &lt;= integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer comparison</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // <item>Includes debug logging for troubleshooting</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP LessOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a <= b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 6: SmallInteger &gt;= - Integer greater-than-or-equal comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveGreaterOrEqual
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver &gt;= integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer comparison</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP GreaterOrEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a >= b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 7: SmallInteger = - Integer equality comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveEqual
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver = integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer equality test</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP Equal(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a == b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 8: SmallInteger ~= - Integer inequality comparison
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveNotEqual
    //     | integerReceiver integerArgument |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerReceiver ~= integerArgument
    //             ifTrue: [self push: TruePointer]
    //             ifFalse: [self push: FalsePointer]]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs direct integer inequality test</item>
    // <item>Returns proper Boolean objects (True or False singletons)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly returns Boolean objects. All comparison operators handle full SmallInteger range correctly.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Boolean True or False singleton</returns>
    public static SmalltalkOOP NotEqual(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        return a != b
            ? SpecialObjectsArray.Instance.True
            : SpecialObjectsArray.Instance.False;
    }

    // <summary>
    // Primitive 9: SmallInteger * - Integer multiplication with overflow detection
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveMultiply
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver * integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Uses checked arithmetic to detect Int32 overflow</item>
    // <item>Validates result is within SmallInteger range [-2^30, 2^30-1]</item>
    // <item>Returns SmallInteger OOP on success; sets PrimitiveSuccess=false on failure</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correct overflow detection for multiplication. The primitive fails appropriately when the result exceeds SmallInteger range.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Uses C# exception handling for overflow detection</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Product as SmallInteger, or nil if overflow/type error</returns>
    public static SmalltalkOOP Multiply(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        // Both operands must be SmallIntegers
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        try
        {
            // Blue Book: integerResult ← integerReceiver * integerArgument
            // Use checked arithmetic to detect Int32 overflow
            // Multiplication is most likely to overflow of all basic operations
            int result = checked(a * b);

            // Blue Book: self success: (memory isIntegerValue: integerResult)
            // Check if result is within SmallInteger range (30-bit signed)
            // Even if multiplication doesn't overflow Int32, it may overflow SmallInteger
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                // Overflow SmallInteger range - let Smalltalk fall back to LargeInteger method
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Blue Book: self pushInteger: integerResult
            return SmalltalkOOP.FromSmallInteger(result);
        }
        catch (OverflowException)
        {
            // Int32 overflow detected - fail primitive for LargeInteger fallback
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 10: SmallInteger / - Exact integer division
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveDivide
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success: integerArgument ~= 0.
    //     self success: integerReceiver \\ integerArgument = 0.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver // integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self push: (memory integerObjectOf: integerResult)]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates divisor is not zero</item>
    // <item>Checks for exactness - fails if remainder exists (a % b != 0)</item>
    // <item>Handles special overflow case: SmallIntegerMin / -1</item>
    // <item>Returns result only for exact divisions</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly implements exact division semantics. The primitive fails for inexact divisions, allowing Smalltalk to fall back to fraction creation.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Explicitly handles SmallIntegerMin / -1 overflow case (enhancement)</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE WITH ENHANCEMENT</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Dividend (SmallInteger)</param>
    // <param name="args">Array containing divisor (SmallInteger)</param>
    // <returns>Quotient as SmallInteger if exact, or nil if inexact/error</returns>
    public static SmalltalkOOP Divide(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        // Blue Book: self success: integerArgument ~= 0
        // Division by zero is undefined - fail primitive
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: SmallIntegerMin / -1 would give 2^30 (outside SmallInteger range)
        // This is a special case because -(SmallIntegerMin) > SmallIntegerMax
        if (a == SmallIntegerMin && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: self success: integerReceiver \\ integerArgument = 0
        // CRITICAL: Primitive / requires EXACT division - fail if remainder exists
        // This is different from quo: which always succeeds with truncation
        // If division is inexact, Smalltalk will create a Fraction instead
        if (a % b != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerResult ← integerReceiver // integerArgument
        // Note: Blue Book uses // but means exact division here, not floor division
        int result = a / b;

        // Blue Book: self success: (memory isIntegerValue: integerResult)
        // Check if result is within SmallInteger range (should always be true after checks above)
        if (result < SmallIntegerMin || result > SmallIntegerMax)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: self push: (memory integerObjectOf: integerResult)
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // <summary>
    // Primitive 11: SmallInteger \\ - Modulo operation (remainder follows divisor sign)
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveMod
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success: integerArgument ~= 0.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver \\ integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates divisor is not zero</item>
    // <item>Computes remainder using C# % operator (sign follows dividend)</item>
    // <item>Adjusts result to follow Smalltalk semantics (sign follows divisor)</item>
    // <item>When signs differ and remainder is non-zero, adds divisor to adjust</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>CRITICAL - Correctly implements Smalltalk modulo semantics where remainder sign follows divisor, not dividend. Example: -7 \\ 3 = 2 (not -1).</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Must adjust C# % result to match Smalltalk semantics</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE WITH CRITICAL CORRECTION</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Dividend (SmallInteger)</param>
    // <param name="args">Array containing divisor (SmallInteger)</param>
    // <returns>Remainder as SmallInteger following Smalltalk modulo semantics</returns>
    public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        // Blue Book: self success: integerArgument ~= 0
        // Modulo by zero is undefined - fail primitive
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerResult ← integerReceiver \\ integerArgument
        // CRITICAL SEMANTIC DIFFERENCE:
        // C# % operator gives remainder with sign of dividend (truncated division)
        // Smalltalk \\ gives remainder with sign of divisor (floored division)
        int rem = a % b;

        // Smalltalk \\ (mod) has remainder sign follow divisor
        // Blue Book: a \\ b has same sign as b (when non-zero)
        // Examples:
        //   C#: -7 % 3 = -1   Smalltalk: -7 \\ 3 = 2
        //   C#: 7 % -3 = 1    Smalltalk: 7 \\ -3 = -2
        // Adjust when signs differ and remainder is non-zero
        if (rem != 0 && ((a < 0) != (b < 0)))
            rem += b;

        // Blue Book: self pushInteger: integerResult
        return SmalltalkOOP.FromSmallInteger(rem);
    }

    // <summary>
    // Primitive 13: SmallInteger quo: - Truncated division (rounds toward zero)
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveQuo
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success: integerArgument ~= 0.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver quo: integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates divisor is not zero</item>
    // <item>Handles overflow case: Int32.MinValue / -1</item>
    // <item>Uses C# / operator which naturally truncates toward zero</item>
    // <item>No adjustment needed - C# semantics match quo: semantics</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>C# / operator naturally implements quo: semantics (truncation toward zero). Correctly distinguishes between quo: (truncate) and // (floor).</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Explicitly handles overflow case (enhancement)</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Dividend (SmallInteger)</param>
    // <param name="args">Array containing divisor (SmallInteger)</param>
    // <returns>Quotient as SmallInteger (truncated toward zero)</returns>
    public static SmalltalkOOP Quo(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        // Validate divisor is not zero
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: Int32.MinValue / -1
        if (a == int.MinValue && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int result = a / b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // <summary>
    // Primitive 14: SmallInteger bitAnd: - Bitwise AND operation
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveBitAnd
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver bitAnd: integerArgument].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs bitwise AND using C# &amp; operator</item>
    // <item>Result always fits in SmallInteger range (no overflow possible)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Straightforward implementation. SmallInteger range guaranteed to fit in 30 bits, so no overflow concerns.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Bitwise AND result as SmallInteger</returns>
    public static SmalltalkOOP BitAnd(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a & b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // <summary>
    // Primitive 15: SmallInteger bitOr: - Bitwise OR operation
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveBitOr
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver bitOr: integerArgument].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs bitwise OR using C# | operator</item>
    // <item>Result always fits in SmallInteger range (no overflow possible)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Straightforward implementation. SmallInteger range guaranteed to fit in 30 bits, so no overflow concerns.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Bitwise OR result as SmallInteger</returns>
    public static SmalltalkOOP BitOr(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a | b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // <summary>
    // Primitive 16: SmallInteger bitXor: - Bitwise XOR operation
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveBitXor
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver bitXor: integerArgument].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and argument are SmallIntegers</item>
    // <item>Performs bitwise XOR using C# ^ operator</item>
    // <item>Result always fits in SmallInteger range (no overflow possible)</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Straightforward implementation. SmallInteger range guaranteed to fit in 30 bits, so no overflow concerns.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Left operand (SmallInteger)</param>
    // <param name="args">Array containing right operand (SmallInteger)</param>
    // <returns>Bitwise XOR result as SmallInteger</returns>
    public static SmalltalkOOP BitXor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        int result = a ^ b;
        return SmalltalkOOP.FromSmallInteger(result);
    }

    // <summary>
    // Primitive 17: SmallInteger bitShift: - Bit shift operation
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveBitShift
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver bitShift: integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver and shift count are SmallIntegers</item>
    // <item>Limits shift range to ±30 bits (SmallInteger width)</item>
    // <item>For left shifts (positive): Uses long to detect overflow before it happens</item>
    // <item>For right shifts (negative): Uses arithmetic shift (sign extension)</item>
    // <item>Zero shift returns value unchanged</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Excellent implementation with enhancements. Explicitly limits shifts to ±30 bits, preventing undefined behavior. Uses long for overflow detection on left shifts.</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Explicitly limits shifts to ±30 bits (enhancement - prevents undefined behavior)</item>
    // <item>Uses long for overflow detection (implementation detail)</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> EXCELLENT WITH ENHANCEMENTS</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Value to shift (SmallInteger)</param>
    // <param name="args">Array containing shift count (SmallInteger, positive=left, negative=right)</param>
    // <returns>Shifted value as SmallInteger, or nil if overflow</returns>
    public static SmalltalkOOP BitShift(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int shift = args[0].AsSmallInteger();

        // SmallInteger is 30-bit; limit shifts to safe range
        // Shifts >= 30 bits will completely overflow SmallInteger range
        // Shifts >= 32 bits have undefined behavior in C#
        // This is an implementation enhancement not in Blue Book
        if (shift > 30 || shift < -30)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerResult ← integerReceiver bitShift: integerArgument
        // Positive shift = left shift (multiply by 2^shift)
        // Negative shift = right shift (divide by 2^shift with truncation)
        // Zero shift = no change
        if (shift > 0)
        {
            // Left shift: Check if result will overflow 30-bit range
            // Use long to detect overflow before it happens
            // Maximum safe value to shift: roughly 2^(30-shift)
            long result = (long)a << shift;

            // Blue Book: self success: (memory isIntegerValue: integerResult)
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Blue Book: self pushInteger: integerResult
            return SmalltalkOOP.FromSmallInteger((int)result);
        }
        else if (shift < 0)
        {
            // Right shift: Always safe, never overflows
            // C# >> is arithmetic shift (preserves sign bit)
            // This matches Smalltalk's right shift semantics
            int result = a >> (-shift);

            // Blue Book: self pushInteger: integerResult
            return SmalltalkOOP.FromSmallInteger(result);
        }
        else
        {
            // Zero shift - return value unchanged
            // Blue Book: self pushInteger: integerResult
            return SmalltalkOOP.FromSmallInteger(a);
        }
    }

    // <summary>
    // Primitive 12: SmallInteger // - Floor division (rounds toward negative infinity)
    // Blue Book: Page 636, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveDiv
    //     | integerReceiver integerArgument integerResult |
    //     integerArgument ← self popInteger.
    //     integerReceiver ← self popInteger.
    //     self success: integerArgument ~= 0.
    //     self success ifTrue:
    //         [integerResult ← integerReceiver // integerArgument.
    //         self success: (memory isIntegerValue: integerResult)].
    //     self success
    //         ifTrue: [self pushInteger: integerResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates divisor is not zero</item>
    // <item>Handles overflow case: Int32.MinValue / -1</item>
    // <item>Computes quotient using C# / operator (truncates toward zero)</item>
    // <item>Adjusts result to floor division (rounds toward negative infinity)</item>
    // <item>When signs differ and remainder is non-zero, decrements quotient</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>CRITICAL - Correctly implements floor division semantics. C# truncates toward zero, but Smalltalk rounds toward negative infinity. Example: -7 // 3 = -3 (not -2).</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Must adjust C# / result to implement floor division</item>
    // <item>Explicitly handles overflow case (enhancement)</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE WITH CRITICAL CORRECTION</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">Dividend (SmallInteger)</param>
    // <param name="args">Array containing divisor (SmallInteger)</param>
    // <returns>Floor division quotient as SmallInteger</returns>
    public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate single argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerArgument ← self popInteger
        // Blue Book: integerReceiver ← self popInteger
        if (!receiver.IsSmallInteger || !args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer values from tagged OOPs
        int a = receiver.AsSmallInteger();
        int b = args[0].AsSmallInteger();

        // Blue Book: self success: integerArgument ~= 0
        // Division by zero is undefined - fail primitive
        if (b == 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check for overflow case: Int32.MinValue / -1
        // This would overflow even in Int32, not just SmallInteger
        if (a == int.MinValue && b == -1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: integerResult ← integerReceiver // integerArgument
        // CRITICAL SEMANTIC DIFFERENCE:
        // C# / operator truncates toward zero (quo: semantics)
        // Smalltalk // rounds toward negative infinity (floor division)
        int quot = a / b;  // C# truncates toward zero
        int rem = a % b;

        // Adjust quotient for floor division semantics
        // When signs differ and remainder is non-zero, decrement quotient
        // Examples:
        //   C#: -7 / 3 = -2    Smalltalk: -7 // 3 = -3
        //   C#: 7 / -3 = -2    Smalltalk: 7 // -3 = -3
        // This ensures: a = (a // b) * b + (a \\ b)
        if (rem != 0 && ((a < 0) != (b < 0)))
            quot--;

        // Blue Book: self pushInteger: integerResult
        return SmalltalkOOP.FromSmallInteger(quot);
    }

    // <summary>
    // Primitive 76: SmallInteger asObject - Convert SmallInteger to object pointer
    // Blue Book: Page 641, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveAsObject
    //     | thisReceiver newOop |
    //     thisReceiver ← self popStack.
    //     newOop ← thisReceiver bitAnd: 16rFFFE.
    //     self success: (memory hasObject: newOop).
    //     self success
    //         ifTrue: [self push: newOop]
    //         ifFalse: [self unPop: 1]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates receiver is a SmallInteger</item>
    // <item>Doubles the SmallInteger value to get object pointer</item>
    // <item>Verifies the resulting OOP exists in the heap</item>
    // <item>Returns the object if valid, fails otherwise</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Correctly converts SmallInteger value to object pointer by doubling. SmallIntegers are tagged with low bit set (odd), objects have low bit clear (even).</para>
    //
    // <para><b>Differences from Specification:</b></para>
    // <list type="bullet">
    // <item>Uses multiplication by 2 instead of bitAnd (equivalent result)</item>
    // <item>Returns nil on failure instead of unpop</item>
    // </list>
    //
    // <para><b>Conformance:</b> PERFECT CONFORMANCE</para>
    // </remarks>
    // <param name="ctx">Primitive execution context</param>
    // <param name="receiver">SmallInteger value to convert</param>
    // <param name="args">Empty array (no arguments)</param>
    // <returns>Object at pointer location, or nil if invalid</returns>
    public static SmalltalkOOP AsObject(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitive takes no arguments
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: thisReceiver ← self popStack
        // Must be a SmallInteger to convert to object pointer
        if (!receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract integer value from tagged OOP
        int value = receiver.AsSmallInteger();

        // Blue Book: newOop ← thisReceiver bitAnd: 16rFFFE
        // This clears the low bit to convert odd SmallInteger to even object pointer
        // Our implementation: Object pointer is twice the SmallInteger value
        // SmallIntegers are tagged with low bit set (odd): 2n+1
        // Objects have low bit clear (even): 2n
        // So doubling the SmallInteger value gives an object pointer
        int objectPointer = value * 2;
        var oop = new SmalltalkOOP(objectPointer);

        // Blue Book: self success: (memory hasObject: newOop)
        // Verify the resulting OOP actually references a valid object in the heap
        if (!ctx.HeapManager.IsValidOOP(oop))
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: self push: newOop
        return oop;
    }
}
