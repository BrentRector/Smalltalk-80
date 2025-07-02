using Smalltalk80;

namespace Smalltalk80.ByteCodes.Send;

/// <summary>
///     Sends one of the special selectors that are implicitly known to the interpreter.
/// </summary>
public sealed class SpecialSelectorSendOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpecialSelectorSendOperation"/> class.
    /// </summary>
    /// <param name="index">The selector table index.</param>
    public SpecialSelectorSendOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.Dispatch((Bytecode)opcode);
    }
}
