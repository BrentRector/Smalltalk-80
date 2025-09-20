using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes the value of a temporary variable onto the evaluation stack.
/// </summary>
public sealed class PushTemporaryVariableOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PushTemporaryVariableOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based temporary index.</param>
    public PushTemporaryVariableOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        ctx.Stack.Push(ctx.Temporaries[index]);
    }
}
