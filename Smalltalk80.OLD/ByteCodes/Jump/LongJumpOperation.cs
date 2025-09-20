using Smalltalk80;

namespace Smalltalk80.ByteCodes.Jump;

/// <summary>
///     Performs an unconditional long jump using a descriptor byte for the low
///     bits of the offset.
/// </summary>
public sealed class LongJumpOperation : IBytecodeOperation
{
    private readonly byte highBits;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LongJumpOperation"/> class.
    /// </summary>
    /// <param name="highBits">The high-order bits encoded in the opcode.</param>
    public LongJumpOperation(byte highBits) => this.highBits = highBits;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        byte low = ctx.Method.Bytecodes[ctx.PC++];
        int offset = ((highBits + 4) << 8) | low;
        ctx.PC += offset;
    }
}
