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

        // Store and pop receiver variable bytecodes (96-103)
        for (int i = 96; i <= 103; i++)
        {
            int index = i - 96; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreAndPopReceiverVariable(interpreter, index);
        }

        // Store and pop temporary variable bytecodes (104-111)
        for (int i = 104; i <= 111; i++)
        {
            int index = i - 104; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreAndPopTemporaryVariable(interpreter, index);
        }

        // Push special constants (112-119)
        Handlers[112] = (interpreter, opcode) => HandlePushSelf(interpreter);
        Handlers[113] = (interpreter, opcode) => HandlePushTrue(interpreter);
        Handlers[114] = (interpreter, opcode) => HandlePushFalse(interpreter);
        Handlers[115] = (interpreter, opcode) => HandlePushNil(interpreter);
        Handlers[116] = (interpreter, opcode) => HandlePushMinusOne(interpreter);
        Handlers[117] = (interpreter, opcode) => HandlePushZero(interpreter);
        Handlers[118] = (interpreter, opcode) => HandlePushOne(interpreter);
        Handlers[119] = (interpreter, opcode) => HandlePushTwo(interpreter);

        // Return bytecodes (120-125)
        Handlers[120] = (interpreter, opcode) => HandleReturnSelf(interpreter);
        Handlers[121] = (interpreter, opcode) => HandleReturnTrue(interpreter);
        Handlers[122] = (interpreter, opcode) => HandleReturnFalse(interpreter);
        Handlers[123] = (interpreter, opcode) => HandleReturnNil(interpreter);
        Handlers[124] = (interpreter, opcode) => HandleReturnTopFromMethod(interpreter);
        Handlers[125] = (interpreter, opcode) => HandleReturnTopFromBlock(interpreter);

        // Extended bytecodes (128-143)
        Handlers[128] = HandleExtendedPush;
        Handlers[129] = HandleExtendedStore;
        Handlers[130] = HandleExtendedStoreAndPop;
        Handlers[131] = HandleSingleExtendedSend;
        Handlers[132] = HandleDoubleExtendedSend;
        Handlers[133] = HandleSingleExtendedSuper;
        Handlers[134] = HandleDoubleExtendedSuper;
        Handlers[135] = HandlePopStack;
        Handlers[136] = HandleDuplicateTop;
        Handlers[137] = HandlePushActiveContext;

        // Jump bytecodes (144-175)
        for (int i = 144; i <= 151; i++)
        {
            int offset = i - 143; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleShortUnconditionalJump(interpreter, offset);
        }

        for (int i = 152; i <= 159; i++)
        {
            int offset = i - 151; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleShortConditionalJump(interpreter, offset, false);
        }

        for (int i = 160; i <= 167; i++)
        {
            int offset = i - 159; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleShortConditionalJump(interpreter, offset, true);
        }

        Handlers[168] = HandleLongUnconditionalJump;
        Handlers[169] = HandleLongConditionalJump;
        Handlers[170] = HandleLongConditionalJump;
        Handlers[171] = HandleLongConditionalJump;
        Handlers[172] = HandleLongConditionalJump;

        // Send arithmetic bytecodes (176-191)
        for (int i = 176; i <= 191; i++)
        {
            int index = i - 176; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleSendArithmetic(interpreter, index);
        }

        // Send special selector bytecodes (192-207)
        for (int i = 192; i <= 207; i++)
        {
            int index = i - 176; // Capture for closure (offset by 176 for special selectors)
            Handlers[i] = (interpreter, opcode) => HandleSendSpecial(interpreter, index);
        }

        // Send literal selector bytecodes (208-255)
        for (int i = 208; i <= 255; i++)
        {
            int index = i - 208; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleSendLiteral(interpreter, index);
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

    private static void HandlePushSelf(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context.Receiver);
    }

    private static void HandleReturnSelf(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        interpreter.HandleReturn(context.Receiver);
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
    private static void HandleExtendedStore(Interpreter interpreter, byte opcode) { }
    private static void HandleExtendedStoreAndPop(Interpreter interpreter, byte opcode) { }
    private static void HandleDoubleExtendedSend(Interpreter interpreter, byte opcode) { }
    private static void HandleSingleExtendedSuper(Interpreter interpreter, byte opcode) { }
    private static void HandleDoubleExtendedSuper(Interpreter interpreter, byte opcode) { }
    private static void HandlePopStack(Interpreter interpreter, byte opcode) { }
    private static void HandleDuplicateTop(Interpreter interpreter, byte opcode) { }
    private static void HandlePushActiveContext(Interpreter interpreter, byte opcode) { }
    private static void HandleLongUnconditionalJump(Interpreter interpreter, byte opcode) { }
    private static void HandleLongConditionalJump(Interpreter interpreter, byte opcode) { }
    private static void HandleSendSpecial(Interpreter interpreter, int index) { }
    private static void HandlePushLiteralConstant(Interpreter interpreter, int index) { }
    private static void HandlePushLiteralVariable(Interpreter interpreter, int index) { }
    private static void HandleStoreAndPopReceiverVariable(Interpreter interpreter, int index) { }
    private static void HandleStoreAndPopTemporaryVariable(Interpreter interpreter, int index) { }
    private static void HandlePushTrue(Interpreter interpreter) { }
    private static void HandlePushFalse(Interpreter interpreter) { }
    private static void HandlePushNil(Interpreter interpreter) { }
    private static void HandlePushMinusOne(Interpreter interpreter) { }
    private static void HandlePushZero(Interpreter interpreter) { }
    private static void HandlePushOne(Interpreter interpreter) { }
    private static void HandlePushTwo(Interpreter interpreter) { }
    private static void HandleReturnTrue(Interpreter interpreter) { }
    private static void HandleReturnFalse(Interpreter interpreter) { }
    private static void HandleReturnNil(Interpreter interpreter) { }
    private static void HandleReturnTopFromMethod(Interpreter interpreter) { }
    private static void HandleReturnTopFromBlock(Interpreter interpreter) { }
}
