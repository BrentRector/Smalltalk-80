using Smalltalk80;

namespace Smalltalk80.ByteCodes.Jump;

/// <summary>
///     Pops a value and performs a long jump if it matches the specified boolean
///     condition.
/// </summary>
public sealed class PopJumpLongOperation : IBytecodeOperation
{
    private readonly bool whenTrue;
    private readonly byte highBits;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PopJumpLongOperation"/> class.
    /// </summary>
    /// <param name="whenTrue">Whether to jump when the value is <c>true</c>.</param>
    /// <param name="highBits">The high-order bits encoded in the opcode.</param>
    public PopJumpLongOperation(bool whenTrue, byte highBits)
    {
        this.whenTrue = whenTrue;
        this.highBits = highBits;
    }

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        byte low = ctx.Method.Bytecodes[ctx.PC++];
        int offset = (highBits << 8) | low;
        var value = ctx.Stack.Pop();
        if (value is bool b && b == whenTrue)
        {
            ctx.PC += offset;
        }
    }
}
