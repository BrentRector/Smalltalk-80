using Smalltalk80;

namespace Smalltalk80.ByteCodes.Jump;

/// <summary>
///     Performs an unconditional short jump forward by a fixed offset.
/// </summary>
public sealed class ShortJumpOperation : IBytecodeOperation
{
    private readonly byte offset;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ShortJumpOperation"/> class.
    /// </summary>
    /// <param name="offset">The bytecode offset to jump by.</param>
    public ShortJumpOperation(byte offset) => this.offset = offset;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.PC += offset;
    }
}
