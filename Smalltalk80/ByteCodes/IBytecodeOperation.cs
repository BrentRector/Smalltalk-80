using System;

namespace Smalltalk80.ByteCodes;

/// <summary>
///     Represents a runnable Smalltalk-80 bytecode instruction.
/// </summary>
public interface IBytecodeOperation
{
    /// <summary>
    ///     Executes the operation using the provided <see cref="Interpreter"/>.
    /// </summary>
    /// <param name="interpreter">The interpreter executing the bytecode.</param>
    /// <param name="opcode">The raw opcode value.</param>
    void Execute(Interpreter interpreter, byte opcode);
}
