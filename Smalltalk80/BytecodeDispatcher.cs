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

        // Store and pop receiver variables (96-103)
        for (int i = 96; i <= 103; i++)
        {
            int index = i - 96; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreAndPopReceiverVariable(interpreter, index);
        }

        // Store and pop temporary variables (104-111)
        for (int i = 104; i <= 111; i++)
        {
            int index = i - 104; // Capture for closure
            Handlers[i] = (interpreter, opcode) => HandleStoreAndPopTemporaryVariable(interpreter, index);
        }

        // Push receiver (self) - bytecode 112
        Handlers[112] = (interpreter, opcode) => HandlePushReceiver(interpreter);

        // Bytecodes 113-119 are undefined in canonical Blue Book
        // 120-127 are return operations and will be handled below

        // Return operations (120-125)
        Handlers[120] = (interpreter, opcode) => HandleReturnReceiver(interpreter);
        // 121-123 are undefined in canonical Blue Book
        Handlers[124] = (interpreter, opcode) => HandleReturnTop(interpreter);
        Handlers[125] = (interpreter, opcode) => HandleReturnTopFromBlock(interpreter);

        // Extended operations (128-137)
        Handlers[128] = (interpreter, opcode) => HandleExtendedPush(interpreter, opcode);
        Handlers[129] = (interpreter, opcode) => HandleExtendedStore(interpreter, opcode);
        Handlers[130] = (interpreter, opcode) => HandleExtendedStoreAndPop(interpreter, opcode);
        Handlers[131] = (interpreter, opcode) => HandleSingleExtendedSend(interpreter, opcode);
        Handlers[132] = (interpreter, opcode) => HandleDoubleExtendedSend(interpreter, opcode);
        Handlers[133] = (interpreter, opcode) => HandleSingleExtendedSuperSend(interpreter, opcode);
        Handlers[134] = (interpreter, opcode) => HandleDoubleExtendedSuperSend(interpreter, opcode);
        Handlers[135] = (interpreter, opcode) => HandlePopStack(interpreter, opcode);
        Handlers[136] = (interpreter, opcode) => HandleDuplicateTop(interpreter, opcode);
        Handlers[137] = (interpreter, opcode) => HandlePushActiveContext(interpreter, opcode);

        // Jump operations (144-175)
        for (int i = 144; i <= 151; i++)
        {
            int offset = i - 143; // Jump offset 1-8
            Handlers[i] = (interpreter, opcode) => HandleShortUnconditionalJump(interpreter, offset);
        }
        
        for (int i = 152; i <= 159; i++)
        {
            int offset = i - 151; // Jump offset 1-8
            Handlers[i] = (interpreter, opcode) => HandleShortConditionalJump(interpreter, offset, false);
        }
        
        for (int i = 160; i <= 167; i++)
        {
            Handlers[i] = (interpreter, opcode) => HandleLongUnconditionalJump(interpreter, opcode);
        }
        
        for (int i = 168; i <= 171; i++)
        {
            Handlers[i] = (interpreter, opcode) => HandleLongConditionalJumpTrue(interpreter, opcode);
        }
        
        for (int i = 172; i <= 175; i++)
        {
            Handlers[i] = (interpreter, opcode) => HandleLongConditionalJumpFalse(interpreter, opcode);
        }

        // Send operations (176-255) - Blue Book specification
        // 176-191: Send literal selector with 0 args
        for (int i = 176; i <= 191; i++)
        {
            int index = i - 176; // Literal index 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 0);
        }
        
        // 192-207: Send literal selector with 1 arg
        for (int i = 192; i <= 207; i++)
        {
            int index = i - 192; // Literal index 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 1);
        }
        
        // 208-223: Send literal selector with 2 args
        for (int i = 208; i <= 223; i++)
        {
            int index = i - 208; // Literal index 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 2);
        }
        
        // 224-239: Send arithmetic selector (+, -, <, >, etc.)
        for (int i = 224; i <= 239; i++)
        {
            int selectorIndex = i - 224; // Arithmetic selector 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendArithmeticSelector(interpreter, selectorIndex);
        }
        
        // 240-255: Send special selector (at:, at:put:, size, etc.)
        for (int i = 240; i <= 255; i++)
        {
            int selectorIndex = i - 240; // Special selector 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendCommonSelector(interpreter, selectorIndex);
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

    private static void HandlePushReceiver(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context.Receiver);
    }

    private static void HandleStoreAndPopReceiverVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Pop();
        context.Receiver.Fields[index] = value;
    }

    private static void HandleStoreAndPopTemporaryVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Pop();
        context.Temporaries[index] = value;
    }

    private static void HandleReturnTopFromBlock(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        var value = context.Stack.Pop();
        interpreter.HandleReturn(value ?? throw new InvalidOperationException("Cannot return null"));
    }

    private static void HandleExtendedStore(Interpreter interpreter, byte opcode)
    {
        // Extended store implementation would go here
        throw new NotImplementedException("Extended store not yet implemented");
    }

    private static void HandleExtendedStoreAndPop(Interpreter interpreter, byte opcode)
    {
        // Extended store and pop implementation would go here
        throw new NotImplementedException("Extended store and pop not yet implemented");
    }

    private static void HandleSingleExtendedSuperSend(Interpreter interpreter, byte opcode)
    {
        // Single extended super send implementation would go here
        throw new NotImplementedException("Single extended super send not yet implemented");
    }

    private static void HandleDoubleExtendedSuperSend(Interpreter interpreter, byte opcode)
    {
        // Double extended super send implementation would go here
        throw new NotImplementedException("Double extended super send not yet implemented");
    }

    private static void HandleLongUnconditionalJump(Interpreter interpreter, byte opcode)
    {
        var context = interpreter.CurrentContext;
        int offset = ((opcode & 0x07) << 8) + context.Method.Bytecodes[context.PC++];
        context.PC += offset;
    }

    private static void HandleLongConditionalJumpTrue(Interpreter interpreter, byte opcode)
    {
        // Long conditional jump on true implementation would go here
        throw new NotImplementedException("Long conditional jump on true not yet implemented");
    }

    private static void HandleLongConditionalJumpFalse(Interpreter interpreter, byte opcode)
    {
        // Long conditional jump on false implementation would go here
        throw new NotImplementedException("Long conditional jump on false not yet implemented");
    }

    private static void HandleSendArithmeticSelector(Interpreter interpreter, int selectorIndex)
    {
        // Send arithmetic selector implementation would go here
        throw new NotImplementedException("Send arithmetic selector not yet implemented");
    }

    private static void HandleSendCommonSelector(Interpreter interpreter, int selectorIndex)
    {
        // Send common selector implementation would go here
        throw new NotImplementedException("Send common selector not yet implemented");
    }

    private static void HandleSendLiteralSelector(Interpreter interpreter, int index, int argCount)
    {
        var context = interpreter.CurrentContext;
        
        // Get the selector from the literal frame
        if (index >= context.Method.Literals.Length)
        {
            throw new InvalidOperationException($"Selector index {index} out of bounds for literal frame (size: {context.Method.Literals.Length})");
        }
        
        var literal = context.Method.Literals[index];
        
        if (literal == null)
        {
            throw new InvalidOperationException($"Literal at index {index} is null");
        }
        
        // The literal IS the selector - just use it directly
        // The interpreter's PerformSend should handle Smalltalk objects
        interpreter.PerformSendWithObject(literal, argCount, false);
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
        // Return the Smalltalk true singleton object
        if (ObjectMemory.TrueObject != null)
        {
            interpreter.HandleReturn(ObjectMemory.TrueObject);
        }
        else
        {
            // Debug: log the issue
            Console.WriteLine("[BYTECODE] WARNING: True singleton not found in ObjectMemory.TrueObject");
            throw new InvalidOperationException("True singleton not found - ensure Phase 2 bootstrap creates it");
        }
    }

    private static void HandleReturnFalse(Interpreter interpreter)
    {
        // Return the Smalltalk false singleton object
        if (ObjectMemory.FalseObject != null)
        {
            interpreter.HandleReturn(ObjectMemory.FalseObject);
        }
        else
        {
            throw new InvalidOperationException("False singleton not found - ensure Phase 2 bootstrap creates it");
        }
    }

    private static void HandleReturnNil(Interpreter interpreter)
    {
        // Return the Smalltalk nil singleton object
        if (ObjectMemory.NilObject != null)
        {
            interpreter.HandleReturn(ObjectMemory.NilObject);
        }
        else
        {
            throw new InvalidOperationException("Nil singleton not found - ensure Phase 2 bootstrap creates it");
        }
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
