using Smalltalk80;

namespace Smalltalk80.ByteCodes.Jump;

/// <summary>
///     Pops a value and performs a short jump if it is <c>false</c>.
/// </summary>
public sealed class PopJumpFalseShortOperation : IBytecodeOperation
{
    private readonly byte offset;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PopJumpFalseShortOperation"/> class.
    /// </summary>
    /// <param name="offset">The bytecode offset to jump by when the value is <c>false</c>.</param>
    public PopJumpFalseShortOperation(byte offset) => this.offset = offset;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var value = ctx.Stack.Pop();
        if (value is bool b && !b)
        {
            ctx.PC += offset;
        }
    }
}
