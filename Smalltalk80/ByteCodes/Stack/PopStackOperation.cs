using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Removes the top object from the evaluation stack.
/// </summary>
public sealed class PopStackOperation : IBytecodeOperation
{
    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        _ = interpreter.CurrentContext.Stack.Pop();
    }
}
