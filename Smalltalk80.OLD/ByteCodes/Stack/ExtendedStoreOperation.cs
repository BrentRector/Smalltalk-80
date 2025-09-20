using System;
using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Stores the stack top into a variable described by an extended descriptor.
/// </summary>
public sealed class ExtendedStoreOperation : IBytecodeOperation
{
    private readonly bool popAfter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExtendedStoreOperation"/> class.
    /// </summary>
    /// <param name="popAfter">Whether to pop the value after storing.</param>
    public ExtendedStoreOperation(bool popAfter) => this.popAfter = popAfter;

    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var descriptor = ctx.Method.Bytecodes[ctx.PC++];
        var type = descriptor >> 6;
        var index = descriptor & 0x3F;
        var value = ctx.Stack.Peek();

        switch (type)
        {
            case 0:
                ctx.Receiver.Fields[index] = value;
                break;
            case 1:
                ctx.Temporaries[index] = value!;
                break;
            case 2:
                throw new InvalidOperationException("Cannot store into a literal constant");
            case 3:
                var assoc = (global::Association)ctx.Method.Literals[index]!;
                assoc.Value = value;
                break;
            default:
                throw new InvalidOperationException($"Invalid extended store type {type}");
        }

        if (popAfter)
        {
            _ = ctx.Stack.Pop();
        }
    }
}
