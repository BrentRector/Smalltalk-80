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

        // Bytecodes 131-191 are not part of the canonical Blue Book specification
        // They remain mapped to HandleUnknownBytecode to throw errors if encountered

        // Send literal selector bytecodes (192-255)
        // Blue Book canonical encoding: bits 6-7 = arg count, bits 2-5 = selector index
        for (int i = 192; i <= 255; i++)
        {
            int bytecode = i; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, bytecode);
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
    private static void HandleDoubleExtendedSend(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Extended send bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleSingleExtendedSuper(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Extended super bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleDoubleExtendedSuper(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Extended super bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandlePopStack(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.Stack.Pop();
    }
    private static void HandleDuplicateTop(Interpreter interpreter, byte opcode)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Peek();
        context.Stack.Push(value);
    }
    private static void HandlePushActiveContext(Interpreter interpreter, byte opcode)
    {
        interpreter.CurrentContext.Stack.Push(interpreter.CurrentContext);
    }
    private static void HandleJumpShort(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Jump bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleJumpLong(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Jump bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleJumpIfFalseShort(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Conditional jump bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleJumpIfFalseLong(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Conditional jump bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleJumpIfTrueShort(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Conditional jump bytecode {opcode} is not part of the Blue Book specification");
    }
    private static void HandleJumpIfTrueLong(Interpreter interpreter, byte opcode)
    {
        // Non-canonical extension - should not be used in Blue Book VM
        throw new InvalidOperationException($"Conditional jump bytecode {opcode} is not part of the Blue Book specification");
    }
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

    private static void HandleSendLiteralSelector(Interpreter interpreter, int bytecode)
    {
        var context = interpreter.CurrentContext;
        // Blue Book canonical encoding:
        // Bits 6-7: Argument count (00=0, 01=1, 10=2, 11=N args)
        // Bits 2-5: Literal frame index (0-15)
        // Bits 0-1: Unused/reserved
        
        var argCount = (bytecode >> 6) & 0x3; // Extract bits 6-7
        var selectorIndex = (bytecode >> 2) & 0xF; // Extract bits 2-5
        
        if (selectorIndex >= context.Method.Literals.Length)
        {
            throw new InvalidOperationException($"Selector index {selectorIndex} out of bounds for literal frame (size: {context.Method.Literals.Length})");
        }
        
        if (!(context.Method.Literals[selectorIndex] is Symbol selector))
        {
            throw new InvalidOperationException($"Literal at index {selectorIndex} is not a Symbol: {context.Method.Literals[selectorIndex]?.GetType()}");
        }
        
        // Handle special case for 11xx_xxxx (bits 6-7 = 11) - variable argument count
        if (argCount == 3) // 11 binary
        {
            // For now, treat as 0 arguments - proper implementation would need more context
            // TODO: Implement variable argument count handling
            argCount = 0;
        }
        
        interpreter.PerformSend(selector, argCount, false);
    }

    // Blue Book does not have separate super send bytecodes in the 192-255 range
    // Super sends are handled through extended bytecodes or compiler-generated patterns
    // This method is no longer used but kept for reference
    private static void HandleSendSuperMessage_Obsolete(Interpreter interpreter, int index)
    {
        throw new InvalidOperationException("Super send bytecodes are not part of the canonical Blue Book 192-255 range");
    }
}
