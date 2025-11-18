using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

/// <summary>
/// Primitive 1: SmallInteger + - Integer addition with overflow detection.
/// Example of new IPrimitive interface implementation.
///
/// Blue Book: Page 636, Chapter 29
/// </summary>
public class AddPrimitive : IPrimitive
{
    private const int SmallIntegerMin = -(1 << 30);
    private const int SmallIntegerMax = (1 << 30) - 1;

    /// <inheritdoc/>
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        // Validate single argument
        if (context.Arguments.Length != 1)
        {
            return PrimitiveResult.Fail();
        }

        var receiver = context.Receiver;
        var arg = context.Arguments[0];

        // Validate both are SmallIntegers
        if (!receiver.IsSmallInteger || !arg.IsSmallInteger)
        {
            return PrimitiveResult.Fail();
        }

        // Extract integer values
        int a = receiver.AsSmallInteger();
        int b = arg.AsSmallInteger();

        try
        {
            // Use checked arithmetic to detect Int32 overflow
            int result = checked(a + b);

            // Verify result fits in SmallInteger range
            if (result < SmallIntegerMin || result > SmallIntegerMax)
            {
                return PrimitiveResult.Fail();
            }

            // Success - return SmallInteger result
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
        }
        catch (OverflowException)
        {
            return PrimitiveResult.Fail();
        }
    }
}
