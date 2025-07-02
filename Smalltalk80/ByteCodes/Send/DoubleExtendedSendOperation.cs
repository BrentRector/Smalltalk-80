using Smalltalk80;

namespace Smalltalk80.ByteCodes.Send;

/// <summary>
///     Sends a message using two extension bytes that specify argument count and selector index.
/// </summary>
public sealed class DoubleExtendedSendOperation : IBytecodeOperation
{
    private readonly bool superSend;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DoubleExtendedSendOperation"/> class.
    /// </summary>
    /// <param name="superSend">Whether the send should start lookup in the superclass.</param>
    public DoubleExtendedSendOperation(bool superSend) => this.superSend = superSend;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.Dispatch((Bytecode)opcode);
    }
}
