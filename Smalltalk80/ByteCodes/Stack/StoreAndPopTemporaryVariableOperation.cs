using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Stores the stack top into a temporary variable and removes the value from
///     the stack.
/// </summary>
public sealed class StoreAndPopTemporaryVariableOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="StoreAndPopTemporaryVariableOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based temporary index.</param>
    public StoreAndPopTemporaryVariableOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        ctx.Temporaries[index] = ctx.Stack.Pop();
    }
}
