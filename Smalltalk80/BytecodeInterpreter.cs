using System;

namespace Smalltalk80;

/// <summary>
///     Interprets an array of Smalltalk-80 bytecodes. The implementation here is
///     intentionally simplified but demonstrates how each bytecode could be
///     decoded and executed.
/// </summary>
public sealed class BytecodeInterpreter
{
    /// <summary>Executes the provided bytecode sequence.</summary>
    /// <param name="code">The bytecode array to interpret.</param>
    /// <param name="context">Execution context providing stack and variables.</param>
    /// <returns>The value produced by the executed code.</returns>
    public object? Execute(byte[] code, ExecutionContext context)
    {
        object? result = null;

        while (context.ProgramCounter < code.Length)
        {
            var opcode = (Bytecode)code[context.ProgramCounter++];

            switch (opcode)
            {
                // Push receiver variables
                case >= Bytecode.PushReceiverVariable0 and <= Bytecode.PushReceiverVariable15:
                    Push(context, context.InstanceVariables[(int)opcode & 0x0F]);
                    break;

                // Push temporary variables
                case >= Bytecode.PushTemporary0 and <= Bytecode.PushTemporary15:
                    Push(context, context.Temporaries[(int)opcode & 0x0F]);
                    break;

                // Push literal constants
                case >= Bytecode.PushLiteralConstant0 and <= Bytecode.PushLiteralConstant31:
                    Push(context, context.Literals[(int)opcode - 0x20]);
                    break;

                // Push literal variables
                case >= Bytecode.PushLiteralVariable0 and <= Bytecode.PushLiteralVariable31:
                    Push(context, context.Literals[(int)opcode - 0x40]);
                    break;

                // Store into receiver variables
                case >= Bytecode.PopStoreReceiverVariable0 and <= Bytecode.PopStoreReceiverVariable7:
                    StoreReceiver(context, (int)opcode - 0x60, pop: true);
                    break;

                // Store into temporary variables
                case >= Bytecode.PopStoreTemporary0 and <= Bytecode.PopStoreTemporary7:
                    StoreTemporary(context, (int)opcode - 0x68, pop: true);
                    break;

                // Push constants
                case Bytecode.PushConstReceiver:
                    Push(context, context.Receiver!);
                    break;
                case Bytecode.PushConstTrue:
                    Push(context, true);
                    break;
                case Bytecode.PushConstFalse:
                    Push(context, false);
                    break;
                case Bytecode.PushConstNil:
                    Push(context, null);
                    break;
                case Bytecode.PushConstMinusOne:
                    Push(context, -1);
                    break;
                case Bytecode.PushConstZero:
                    Push(context, 0);
                    break;
                case Bytecode.PushConstOne:
                    Push(context, 1);
                    break;
                case Bytecode.PushConstTwo:
                    Push(context, 2);
                    break;

                // Return constants
                case Bytecode.ReturnReceiver:
                    return context.Receiver;
                case Bytecode.ReturnTrue:
                    return true;
                case Bytecode.ReturnFalse:
                    return false;
                case Bytecode.ReturnNil:
                    return null;
                case Bytecode.ReturnTopFromMessage:
                case Bytecode.ReturnTopFromBlock:
                    return Pop(context);

                case Bytecode.ExtendedPush:
                    HandleExtendedPush(code, context);
                    break;
                case Bytecode.ExtendedStore:
                    HandleExtendedStore(code, context, pop: false);
                    break;
                case Bytecode.ExtendedStoreAndPop:
                    HandleExtendedStore(code, context, pop: true);
                    break;
                case Bytecode.SingleExtendedSend:
                case Bytecode.DoubleExtendedSend:
                case Bytecode.SingleExtendedSuper:
                case Bytecode.DoubleExtendedSuper:
                    // Message sending is highly implementation specific. This
                    // prototype simply throws to indicate the bytecode was seen.
                    throw new NotImplementedException($"Send bytecode '{opcode}' not implemented.");
                case Bytecode.PopStackTop:
                    Pop(context);
                    break;
                case Bytecode.DuplicateStackTop:
                    Push(context, Peek(context));
                    break;
                case Bytecode.PushActiveContext:
                    Push(context, context);
                    break;

                // Short jumps
                case >= Bytecode.ShortJump1 and <= Bytecode.ShortJump8:
                    Jump(context, ((int)opcode - 0x90) + 1);
                    break;
                case >= Bytecode.PopAndJumpOnFalse1 and <= Bytecode.PopAndJumpOnFalse8:
                    if (Equals(Pop(context), false))
                        Jump(context, ((int)opcode - 0x98) + 1);
                    break;

                // Long jumps (simplified two's complement form)
                case >= Bytecode.LongJump0 and <= Bytecode.LongJump7:
                    Jump(context, ReadLongDisplacement(code, context, opcode));
                    break;
                case >= Bytecode.PopAndLongJumpOnTrue0 and <= Bytecode.PopAndLongJumpOnTrue3:
                    if (Equals(Pop(context), true))
                        Jump(context, ReadLongDisplacement(code, context, opcode));
                    else
                        context.ProgramCounter++; // skip displacement byte
                    break;
                case >= Bytecode.PopAndLongJumpOnFalse0 and <= Bytecode.PopAndLongJumpOnFalse3:
                    if (Equals(Pop(context), false))
                        Jump(context, ReadLongDisplacement(code, context, opcode));
                    else
                        context.ProgramCounter++;
                    break;

                // Arithmetic messages (partial implementation)
                case >= Bytecode.SendArithmeticMessage0 and <= Bytecode.SendArithmeticMessage15:
                    PerformArithmetic(opcode, context);
                    break;

                default:
                    throw new NotImplementedException($"Bytecode '{opcode}' is not implemented in this prototype.");
            }
        }

        return result;
    }

    private static void Push(ExecutionContext ctx, object? value)
        => ctx.Stack[ctx.StackPointer++] = value!;

    private static object? Pop(ExecutionContext ctx)
        => ctx.Stack[--ctx.StackPointer];

    private static object? Peek(ExecutionContext ctx)
        => ctx.Stack[ctx.StackPointer - 1];

    private static void Jump(ExecutionContext ctx, int offset)
        => ctx.ProgramCounter += offset;

    private static void StoreReceiver(ExecutionContext ctx, int index, bool pop)
    {
        ctx.InstanceVariables[index] = pop ? Pop(ctx) : Peek(ctx);
    }

    private static void StoreTemporary(ExecutionContext ctx, int index, bool pop)
    {
        ctx.Temporaries[index] = pop ? Pop(ctx) : Peek(ctx);
    }

    private static void HandleExtendedPush(byte[] code, ExecutionContext ctx)
    {
        var descriptor = code[ctx.ProgramCounter++];
        var type = (descriptor >> 6) & 0x03;
        var index = descriptor & 0x3F;
        switch (type)
        {
            case 0:
                Push(ctx, ctx.InstanceVariables[index]);
                break;
            case 1:
                Push(ctx, ctx.Temporaries[index]);
                break;
            case 2:
                Push(ctx, ctx.Literals[index]);
                break;
            case 3:
                Push(ctx, ctx.Literals[index]);
                break;
        }
    }

    private static void HandleExtendedStore(byte[] code, ExecutionContext ctx, bool pop)
    {
        var descriptor = code[ctx.ProgramCounter++];
        var type = (descriptor >> 6) & 0x03;
        var index = descriptor & 0x3F;
        switch (type)
        {
            case 0:
                StoreReceiver(ctx, index, pop);
                break;
            case 1:
                StoreTemporary(ctx, index, pop);
                break;
            case 2:
                throw new InvalidOperationException("Cannot store into literal constant.");
            case 3:
                if (pop)
                    ctx.Literals[index] = Pop(ctx);
                else
                    ctx.Literals[index] = Peek(ctx);
                break;
        }
    }

    private static int ReadLongDisplacement(byte[] code, ExecutionContext ctx, Bytecode opcode)
    {
        int high = ((int)opcode & 0x07) << 8;
        int low = code[ctx.ProgramCounter++];
        int value = high | low;
        // sign extend 11-bit value
        if ((value & 0x400) != 0)
            value -= 0x800;
        return value;
    }

    private static void PerformArithmetic(Bytecode opcode, ExecutionContext ctx)
    {
        int index = (int)opcode - 0xB0;
        var b = Pop(ctx);
        var a = Pop(ctx);
        if (a is int ai && b is int bi)
        {
            object result = index switch
            {
                0 => ai + bi,
                1 => ai - bi,
                8 => ai * bi,
                9 => ai / bi,
                _ => throw new NotImplementedException($"Arithmetic message {index} not implemented.")
            };
            Push(ctx, result);
        }
        else
        {
            throw new InvalidOperationException("Arithmetic operations expect integers in this prototype.");
        }
    }
}
