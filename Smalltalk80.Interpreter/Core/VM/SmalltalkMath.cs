namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides Smalltalk-80 mathematical operations with exact Blue Book semantics.
/// CRITICAL: Smalltalk operators have different semantics than C# operators!
/// </summary>
public static class SmalltalkMath
{
    /// <summary>
    /// Implements Smalltalk-80 modulo (\\) operator with floored division semantics.
    /// CRITICAL: Different from C# % operator which uses truncated division!
    ///
    /// Smalltalk \\ (modulo):
    ///   - Remainder sign follows DIVISOR (floored division)
    ///   - Example: -7 \\ 3 = 2
    ///
    /// C# % (remainder):
    ///   - Remainder sign follows DIVIDEND (truncated division)
    ///   - Example: -7 % 3 = -1
    ///
    /// This matches the implementation in Primitive 11 (SmallInteger \\).
    /// See ArithmeticPrimitives.cs:827-873 for full specification.
    /// </summary>
    /// <param name="dividend">The number to be divided (receiver)</param>
    /// <param name="divisor">The number to divide by (argument)</param>
    /// <returns>Remainder with sign following divisor (Smalltalk semantics)</returns>
    public static int Modulo(int dividend, int divisor)
    {
        // Blue Book: integerResult ← integerReceiver \\ integerArgument
        // C# % gives remainder with sign of dividend (truncated division)
        // Smalltalk \\ gives remainder with sign of divisor (floored division)
        int remainder = dividend % divisor;

        // Adjust when signs differ and remainder is non-zero
        // Blue Book: a \\ b has same sign as b (when non-zero)
        // Examples:
        //   C#: -7 % 3 = -1   →   Smalltalk: -7 \\ 3 = 2
        //   C#: 7 % -3 = 1    →   Smalltalk: 7 \\ -3 = -2
        if (remainder != 0 && ((dividend < 0) != (divisor < 0)))
            remainder += divisor;

        return remainder;
    }
}
