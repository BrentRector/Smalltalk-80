using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes a constant value onto the evaluation stack.
/// </summary>
public sealed class PushConstantOperation : IBytecodeOperation
{
    private readonly object? constant;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PushConstantOperation"/>
    ///     class.
    /// </summary>
    /// <param name="constant">The constant value to push.</param>
    public PushConstantOperation(object? constant) => this.constant = constant;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.Stack.Push(constant);
    }
}
