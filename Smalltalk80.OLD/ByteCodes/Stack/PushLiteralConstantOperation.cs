using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes a literal constant from the method's literal frame.
/// </summary>
public sealed class PushLiteralConstantOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PushLiteralConstantOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based literal index.</param>
    public PushLiteralConstantOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        ctx.Stack.Push(ctx.Method.Literals[index]);
    }
}
