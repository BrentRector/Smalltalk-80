using System;
using Smalltalk80;

namespace Smalltalk80.ByteCodes.Stack;

/// <summary>
///     Performs an extended push which can access up to sixty-four receiver
///     variables, temporary locations, literal constants or literal variables.
/// </summary>
public sealed class ExtendedPushOperation : IBytecodeOperation
{
    /// <inheritdoc />
    public void Execute(Interpreter interpreter, byte opcode)
    {
        var ctx = interpreter.CurrentContext;
        var descriptor = ctx.Method.Bytecodes[ctx.PC++];
        var type = descriptor >> 6;
        var index = descriptor & 0x3F;

        switch (type)
        {
            case 0:
                ctx.Stack.Push(ctx.Receiver.Fields[index]);
                break;
            case 1:
                ctx.Stack.Push(ctx.Temporaries[index]);
                break;
            case 2:
                ctx.Stack.Push(ctx.Method.Literals[index]);
                break;
            case 3:
                var assoc = (global::Association)ctx.Method.Literals[index]!;
                ctx.Stack.Push(assoc.Value);
                break;
            default:
                throw new InvalidOperationException($"Invalid extended push type {type}");
        }
    }
}
