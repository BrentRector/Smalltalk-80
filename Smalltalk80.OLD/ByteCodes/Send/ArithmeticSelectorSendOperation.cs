using Smalltalk80;

namespace Smalltalk80.ByteCodes.Send;

/// <summary>
///     Sends one of the arithmetic selectors defined in the interpreter's special table.
/// </summary>
public sealed class ArithmeticSelectorSendOperation : IBytecodeOperation
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ArithmeticSelectorSendOperation"/> class.
    /// </summary>
    /// <param name="index">The selector table index.</param>
    public ArithmeticSelectorSendOperation(byte index)
    {
    }

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.Dispatch((Bytecode)opcode);
    }
}
