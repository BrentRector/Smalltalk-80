using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes the current method receiver onto the evaluation stack.
/// </summary>
public sealed class PushSelfOperation : IBytecodeOperation
{
    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.Stack.Push(interpreter.CurrentContext.Receiver);
    }
}
