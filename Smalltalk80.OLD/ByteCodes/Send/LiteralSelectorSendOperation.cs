using Smalltalk80;

namespace Smalltalk80.ByteCodes.Send;

/// <summary>
///     Sends a selector taken from the method's literal frame with a fixed number of arguments.
/// </summary>
public sealed class LiteralSelectorSendOperation : IBytecodeOperation
{
    private readonly byte index;
    private readonly int argCount;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LiteralSelectorSendOperation"/> class.
    /// </summary>
    /// <param name="index">The selector literal index.</param>
    /// <param name="argCount">The number of message arguments.</param>
    public LiteralSelectorSendOperation(byte index, int argCount)
    {
        this.index = index;
        this.argCount = argCount;
    }

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.Dispatch((Bytecode)opcode);
    }
}
