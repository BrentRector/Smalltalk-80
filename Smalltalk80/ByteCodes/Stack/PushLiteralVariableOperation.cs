using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Pushes the value of an <see cref="Association"/> stored in the method's
///     literal frame.
/// </summary>
public sealed class PushLiteralVariableOperation : IBytecodeOperation
{
    private readonly byte index;

    /// <summary>
    ///     Initializes a new instance of the
    ///     <see cref="PushLiteralVariableOperation"/> class.
    /// </summary>
    /// <param name="index">The zero-based literal index.</param>
    public PushLiteralVariableOperation(byte index) => this.index = index;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var assoc = (global::Association)ctx.Method.Literals[index]!;
        ctx.Stack.Push(assoc.Value);
    }
}
