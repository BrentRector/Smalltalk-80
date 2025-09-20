using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Duplicates the top stack element.
/// </summary>
public sealed class DuplicateTopOperation : IBytecodeOperation
{
    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var top = ctx.Stack.Peek();
        ctx.Stack.Push(top);
    }
}
