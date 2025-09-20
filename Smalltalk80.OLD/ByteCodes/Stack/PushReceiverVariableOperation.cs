using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes the value of one of the receiver's instance variables onto the
///     evaluation stack.
/// </summary>
public sealed class PushReceiverVariableOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PushReceiverVariableOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based instance variable index.</param>
    public PushReceiverVariableOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        ctx.Stack.Push(ctx.Receiver.Fields[index]);
    }
}
