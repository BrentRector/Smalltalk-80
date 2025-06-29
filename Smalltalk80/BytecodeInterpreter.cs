using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Interprets a sequence of Smalltalk-80 bytecodes.
/// </summary>
public class BytecodeInterpreter
{
    private readonly Dictionary<byte, Action<ExecutionContext>> _handlers;

    public BytecodeInterpreter()
    {
        _handlers = new Dictionary<byte, Action<ExecutionContext>>
        {
            { (byte)Bytecode.PushConstantZero, ctx => PushConstant(ctx, 0) },
            { (byte)Bytecode.PushConstantOne, ctx => PushConstant(ctx, 1) },
            { (byte)Bytecode.Add, Add },
            { (byte)Bytecode.Subtract, Subtract },
            { (byte)Bytecode.ReturnTop, ReturnTop }
        };
    }

    public object? Execute(byte[] code, ExecutionContext context)
    {
        while (context.ProgramCounter < code.Length)
        {
            byte opcode = code[context.ProgramCounter++];

            if (!_handlers.TryGetValue(opcode, out var handler))
            {
                throw new InvalidOperationException($"Unknown bytecode {opcode:X2}");
            }

            handler(context);
        }

        return null;
    }

    private static void PushConstant(ExecutionContext context, object value)
    {
        context.Stack[context.StackPointer++] = value;
    }

    private static void Add(ExecutionContext context)
    {
        var b = context.Stack[--context.StackPointer];
        var a = context.Stack[--context.StackPointer];
        if (a is int ai && b is int bi)
        {
            context.Stack[context.StackPointer++] = ai + bi;
        }
        else
        {
            throw new InvalidOperationException("Add expects integers");
        }
    }

    private static void Subtract(ExecutionContext context)
    {
        var b = context.Stack[--context.StackPointer];
        var a = context.Stack[--context.StackPointer];
        if (a is int ai && b is int bi)
        {
            context.Stack[context.StackPointer++] = ai - bi;
        }
        else
        {
            throw new InvalidOperationException("Subtract expects integers");
        }
    }

    private static void ReturnTop(ExecutionContext context)
    {
        context.ProgramCounter = int.MaxValue; // exit loop
    }
}

