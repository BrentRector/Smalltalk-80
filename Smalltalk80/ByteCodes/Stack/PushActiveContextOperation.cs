using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes a reference to the currently active context onto the stack.
/// </summary>
public sealed class PushActiveContextOperation : IBytecodeOperation
{
    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.Stack.Push(interpreter.CurrentContext);
    }
}
