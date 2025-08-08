using System;

/// <summary>
/// Optimized bytecode dispatcher using a lookup table for better performance.
/// </summary>
public static class BytecodeDispatcher
{
    /// <summary>
    /// Delegate for bytecode operation handlers.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="opcode">The bytecode opcode.</param>
    private delegate void BytecodeHandler(Interpreter interpreter, byte opcode);

    /// <summary>
    /// Lookup table mapping bytecode values to handler methods.
    /// </summary>
    private static readonly BytecodeHandler[] Handlers = new BytecodeHandler[256];

    /// <summary>
    /// Initializes the bytecode handler lookup table.
    /// </summary>
    static BytecodeDispatcher()
    {
        // Initialize all handlers to the unknown bytecode handler
        for (int i = 0; i < 256; i++)
        {
            Handlers[i] = HandleUnknownBytecode;
        }

        // Receiver variable push bytecodes (0-15)
        for (int i = 0; i <= 15; i++)
        {
            int index = i; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandlePushReceiverVariable(interpreter, index);
        }

        // Temporary variable push bytecodes (16-31)
        for (int i = 16; i <= 31; i++)
        {
            int index = i - 16; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandlePushTemporaryVariable(interpreter, index);
        }

        // Literal constant push bytecodes (32-63)
        for (int i = 32; i <= 63; i++)
        {
            int index = i - 32; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandlePushLiteralConstant(interpreter, index);
        }

        // Literal variable push bytecodes (64-95)
        for (int i = 64; i <= 95; i++)
        {
            int index = i - 64; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandlePushLiteralVariable(interpreter, index);
        }

        // Store into receiver variables (no pop) (96-127)
        for (int i = 96; i <= 127; i++)
        {
            int index = i - 96; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreIntoReceiverVariable(interpreter, index);
        }

        // Store into temporary variables (no pop) (128-159)
        for (int i = 128; i <= 159; i++)
        {
            int index = i - 128; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreIntoTemporaryVariable(interpreter, index);
        }

        // Store into literal variables (no pop) (160-175)
        for (int i = 160; i <= 175; i++)
        {
            int index = i - 160; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreIntoLiteralVariable(interpreter, index);
        }


        // Return operations (176-180)
        Handlers[176] = (interpreter, opcode) => HandleReturnReceiver(interpreter);
        Handlers[177] = (interpreter, opcode) => HandleReturnTrue(interpreter);
        Handlers[178] = (interpreter, opcode) => HandleReturnFalse(interpreter);
        Handlers[179] = (interpreter, opcode) => HandleReturnNil(interpreter);
        Handlers[180] = (interpreter, opcode) => HandleReturnTop(interpreter);

        // Extended bytecodes (non-Blue Book extensions)
        Handlers[131] = HandleSingleExtendedSend;
        Handlers[132] = HandleDoubleExtendedSend;
        Handlers[133] = HandleSingleExtendedSuper;
        Handlers[134] = HandleDoubleExtendedSuper;
        Handlers[135] = HandlePopStack;
        Handlers[136] = HandleDuplicateTop;
        Handlers[137] = HandlePushActiveContext;

        // Jump bytecodes (non-Blue Book extensions)
        Handlers[138] = HandleJumpShort;
        Handlers[139] = HandleJumpLong;
        Handlers[140] = HandleJumpIfFalseShort;
        Handlers[141] = HandleJumpIfFalseLong;
        Handlers[142] = HandleJumpIfTrueShort;
        Handlers[143] = HandleJumpIfTrueLong;

        // Send message bytecodes (192-223)
        for (int i = 192; i <= 223; i++)
        {
            int index = i - 192; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleSendMessage(interpreter, index);
        }

        // Send super message bytecodes (224-255)
        for (int i = 224; i <= 255; i++)
        {
            int index = i - 224; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleSendSuperMessage(interpreter, index);
        }
    }

    /// <summary>
    /// Dispatches a bytecode to its appropriate handler.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="opcode">The bytecode to dispatch.</param>
    public static void Dispatch(Interpreter interpreter, byte opcode)
    {
        Handlers[opcode](interpreter, opcode);
    }

    // Handler method implementations would go here
    // For brevity, I'm showing just a few examples:

    private static void HandlePushReceiverVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context.Receiver.Fields[index]);
    }

    private static void HandlePushTemporaryVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context.Temporaries[index]);
    }

    private static void HandleReturnReceiver(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        interpreter.HandleReturn(context.Receiver);
    }
    
    private static void HandleReturnTop(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        var topValue = context.Stack.Pop();
        interpreter.HandleReturn(topValue ?? throw new InvalidOperationException("Cannot return null value"));
    }

    private static void HandleUnknownBytecode(Interpreter interpreter, byte opcode)
    {
        throw new NotImplementedException($"Unknown bytecode: {opcode}");
    }

    // Additional handler methods would be implemented here following the same pattern
    // This provides a foundation that's much faster than the current if/else chain

    private static void HandleExtendedPush(Interpreter interpreter, byte opcode)
    {
        // Implementation for extended push operations
        var context = interpreter.CurrentContext;
        byte descriptor = context.Method.Bytecodes[context.PC++];
        // Handle extended push based on descriptor
    }

    private static void HandleSingleExtendedSend(Interpreter interpreter, byte opcode)
    {
        // Implementation for single extended send
        var context = interpreter.CurrentContext;
        byte descriptor = context.Method.Bytecodes[context.PC++];
        int argCount = descriptor >> 5;
        int selectorIndex = descriptor & 0x1F;
        var selector = (Symbol)context.Method.Literals[selectorIndex];
        // Perform send
    }

    private static void HandleShortUnconditionalJump(Interpreter interpreter, int offset)
    {
        interpreter.CurrentContext.PC += offset;
    }

    private static void HandleShortConditionalJump(Interpreter interpreter, int offset, bool jumpOnTrue)
    {
        var context = interpreter.CurrentContext;
        var condObj = context.Stack.Pop();
        if (condObj is bool b && b == jumpOnTrue)
        {
            context.PC += offset;
        }
    }

    private static void HandleSendArithmetic(Interpreter interpreter, int index)
    {
        // Implementation for arithmetic message sends
        // Uses the special selector table from the interpreter
    }

    private static void HandleSendLiteral(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var selector = (Symbol)context.Method.Literals[index];
        // Determine argument count and perform send
    }

    // Additional placeholder handlers - full implementation would include all bytecodes
    private static void HandleDoubleExtendedSend(Interpreter interpreter, byte opcode) { }
    private static void HandleSingleExtendedSuper(Interpreter interpreter, byte opcode) { }
    private static void HandleDoubleExtendedSuper(Interpreter interpreter, byte opcode) { }
    private static void HandlePopStack(Interpreter interpreter, byte opcode) { }
    private static void HandleDuplicateTop(Interpreter interpreter, byte opcode) { }
    private static void HandlePushActiveContext(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpShort(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpLong(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpIfFalseShort(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpIfFalseLong(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpIfTrueShort(Interpreter interpreter, byte opcode) { }
    private static void HandleJumpIfTrueLong(Interpreter interpreter, byte opcode) { }
    private static void HandlePushLiteralConstant(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context.Method.Literals[index]);
    }

    private static void HandlePushLiteralVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var assoc = (Association)context.Method.Literals[index]!;
        context.Stack.Push(assoc.Value);
    }

    private static void HandleStoreIntoReceiverVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Peek(); // Blue Book store doesn't pop
        context.Receiver.Fields[index] = value;
    }

    private static void HandleStoreIntoTemporaryVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Peek(); // Blue Book store doesn't pop
        context.Temporaries[index] = value;
    }

    private static void HandleStoreIntoLiteralVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Peek(); // Blue Book store doesn't pop
        var assoc = (Association)context.Method.Literals[index]!;
        assoc.Value = value;
    }

    private static void HandleReturnTrue(Interpreter interpreter)
    {
        interpreter.HandleReturn(true);
    }

    private static void HandleReturnFalse(Interpreter interpreter)
    {
        interpreter.HandleReturn(false);
    }

    private static void HandleReturnNil(Interpreter interpreter)
    {
        interpreter.HandleReturn(null!);
    }

    private static void HandleSendMessage(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var selector = (Symbol)context.Method.Literals[index];
        // Determine argument count from selector - this is a simplification
        interpreter.PerformSend(selector, 0, false);
    }

    private static void HandleSendSuperMessage(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var selector = (Symbol)context.Method.Literals[index];
        // Determine argument count from selector - this is a simplification  
        interpreter.PerformSend(selector, 0, true);
    }
}
