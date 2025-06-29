using System;

namespace Smalltalk80;

/// <summary>
///     Interprets an array of Smalltalk-80 bytecodes. The implementation here is
///     intentionally simplified but demonstrates how each bytecode could be
///     decoded and executed.
/// </summary>
// Blue Book - Chapter 28: The Interpreter
public sealed class BytecodeInterpreter
{
    /// <summary>
    ///     Executes a sequence of bytecodes in the given <paramref name="context" />.
    /// </summary>
    /// <param name="code">The bytecode stream to interpret.</param>
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
                    HandleSingleExtendedSend(code, context);
                    break;
                case Bytecode.DoubleExtendedSend:
                    HandleDoubleExtendedSend(code, context);
                    break;
                case Bytecode.SingleExtendedSuper:
                    HandleSingleExtendedSend(code, context, true);
                    break;
                case Bytecode.DoubleExtendedSuper:
                    HandleDoubleExtendedSend(code, context, true);
                    break;
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

                // Arithmetic messages
                case >= Bytecode.SendArithmeticMessage0 and <= Bytecode.SendArithmeticMessage15:
                    PerformArithmetic(opcode, context);
                    break;

                // Special selectors
                case >= Bytecode.SendSpecialMessage0 and <= Bytecode.SendSpecialMessage15:
                    SendSpecialSelector(context, (int)opcode - 0xC0);
                    break;

                // Literal selector sends
                case >= Bytecode.SendLiteralSelectorNoArgs0 and <= Bytecode.SendLiteralSelectorNoArgs15:
                    SendLiteralSelector(context, (int)opcode - 0xD0, 0);
                    break;
                case >= Bytecode.SendLiteralSelectorOneArg0 and <= Bytecode.SendLiteralSelectorOneArg15:
                    SendLiteralSelector(context, (int)opcode - 0xE0, 1);
                    break;
                case >= Bytecode.SendLiteralSelectorTwoArgs0 and <= Bytecode.SendLiteralSelectorTwoArgs15:
                    SendLiteralSelector(context, (int)opcode - 0xF0, 2);
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
                0  => ai + bi,
                1  => ai - bi,
                2  => ai < bi,
                3  => ai > bi,
                4  => ai <= bi,
                5  => ai >= bi,
                6  => ai == bi,
                7  => ai != bi,
                8  => ai * bi,
                9  => ai / bi,
                10 => ai & bi,
                11 => ai | bi,
                12 => ai ^ bi,
                13 => ai << bi,
                14 => ai >> bi,
                15 => ai % bi,
                _  => throw new InvalidOperationException()
            };
            Push(ctx, result);
        }
        else
        {
            throw new InvalidOperationException("Arithmetic operations expect integers in this prototype.");
        }
    }

    private static void HandleSingleExtendedSend(byte[] code, ExecutionContext ctx, bool toSuper = false)
    {
        var descriptor = code[ctx.ProgramCounter++];
        int argCount = (descriptor >> 5) & 0x07;
        int selectorIndex = descriptor & 0x1F;
        SendLiteralSelector(ctx, selectorIndex, argCount, toSuper);
    }

    private static void HandleDoubleExtendedSend(byte[] code, ExecutionContext ctx, bool toSuper = false)
    {
        int argCount = code[ctx.ProgramCounter++];
        int selectorIndex = code[ctx.ProgramCounter++];
        SendLiteralSelector(ctx, selectorIndex, argCount, toSuper);
    }

    private static void SendLiteralSelector(ExecutionContext ctx, int selectorIndex, int argCount, bool toSuper = false)
    {
        var selectorObj = ctx.Literals[selectorIndex];
        var selector = selectorObj?.ToString() ?? throw new InvalidOperationException("Selector must be a string");
        if (toSuper)
            InvokeSendSuper(ctx, selector, argCount);
        else
            InvokeSend(ctx, selector, argCount);
    }

    private static readonly string[] SpecialSelectors =
    [
        "at:", "at:put:", "size", "next", "nextPut:", "atEnd", "class",
        "blockCopy:", "value", "value:", "do:", "new", "new:", "x", "y", "valueWithArguments:"
    ];

    private static readonly int[] SpecialSelectorArgCounts =
    [
        1, 2, 0, 0, 1, 0, 0,
        1, 0, 1, 1, 0, 1, 0, 0, 1
    ];

    private static void SendSpecialSelector(ExecutionContext ctx, int index)
        => InvokeSend(ctx, SpecialSelectors[index], SpecialSelectorArgCounts[index]);

    private static void InvokeSend(ExecutionContext ctx, string selector, int argCount)
    {
        object?[] args = new object?[argCount];
        for (var i = argCount - 1; i >= 0; i--)
            args[i] = Pop(ctx);

        var receiver = Pop(ctx);
        var result = InvokeSelector(receiver, selector, args);
        Push(ctx, result);
    }

    private static void InvokeSendSuper(ExecutionContext ctx, string selector, int argCount)
    {
        object?[] args = new object?[argCount];
        for (var i = argCount - 1; i >= 0; i--)
            args[i] = Pop(ctx);

        var receiver = Pop(ctx);

        var methodClass = ctx.MethodClass;
        if (methodClass is null)
        {
            Push(ctx, InvokeSelector(receiver, selector, args));
            return;
        }

        var targetType = methodClass.BaseType;
        object? result = null;
        while (targetType is not null)
        {
            var method = targetType.GetMethod(selector,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (method is not null)
            {
                result = method.Invoke(receiver, args);
                break;
            }
            targetType = targetType.BaseType;
        }

        if (targetType is null && result is null)
        {
            result = InvokeSelector(receiver, selector, args);
        }

        Push(ctx, result);
    }

    private static object? InvokeSelector(object? receiver, string selector, object?[] args)
    {
        if (receiver is null)
            throw new NullReferenceException();

        return selector switch
        {
            "class" => receiver.GetType(),
            "new" when receiver is Type t => Activator.CreateInstance(t),
            "new:" when receiver is Type t => Activator.CreateInstance(t, args),
            "size" => receiver switch
            {
                Array a => a.Length,
                System.Collections.ICollection c => c.Count,
                _ => receiver.GetType().GetProperty("Length")?.GetValue(receiver)
                     ?? receiver.GetType().GetProperty("Count")?.GetValue(receiver)
            },
            "at:" => (receiver as System.Collections.IList)?[(int)args[0]!]!,
            "at:put:" =>
            receiver is System.Collections.IList list
                ? list[(int)args[0]!] = args[1]
                : throw new NotImplementedException(),
            "value" when receiver is Delegate d => d.DynamicInvoke(),
            "value:" when receiver is Delegate d => d.DynamicInvoke(args),
            "valueWithArguments:" when receiver is Delegate d => d.DynamicInvoke((object[]?)args[0] ?? Array.Empty<object?>()),
            _ =>
            receiver.GetType().GetMethod(selector)?.Invoke(receiver, args)
                ?? throw new NotImplementedException($"Selector '{selector}' not understood by {receiver.GetType()}")
        };
    }
}
