using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Stores the stack top into one of the receiver's instance variables and
///     removes the value from the stack.
/// </summary>
public sealed class StoreAndPopReceiverVariableOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="StoreAndPopReceiverVariableOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based instance variable index.</param>
    public StoreAndPopReceiverVariableOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        ctx.Receiver.Fields[index] = ctx.Stack.Pop();
    }
}
