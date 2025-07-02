using Smalltalk80;

namespace Smalltalk80.ByteCodes.Send;

/// <summary>
///     Sends a message using a single descriptor byte that encodes both the
///     selector index and argument count.
/// </summary>
public sealed class SingleExtendedSendOperation : IBytecodeOperation
{
    private readonly bool superSend;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleExtendedSendOperation"/> class.
    /// </summary>
    /// <param name="superSend">Whether the send should start lookup in the superclass.</param>
    public SingleExtendedSendOperation(bool superSend) => this.superSend = superSend;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.Dispatch((Bytecode)opcode);
    }
}
