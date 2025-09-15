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
        
        // Push special constants (113-119)
        Handlers[113] = (interpreter, opcode) => HandlePushConstantTrue(interpreter);
        Handlers[114] = (interpreter, opcode) => HandlePushConstantFalse(interpreter);
        Handlers[115] = (interpreter, opcode) => HandlePushConstantNil(interpreter);
        Handlers[116] = (interpreter, opcode) => HandlePushConstantMinusOne(interpreter);
        Handlers[117] = (interpreter, opcode) => HandlePushConstantZero(interpreter);
        Handlers[118] = (interpreter, opcode) => HandlePushConstantOne(interpreter);
        Handlers[119] = (interpreter, opcode) => HandlePushConstantTwo(interpreter);

        // Return operations (120-125)
        Handlers[120] = (interpreter, opcode) => HandleReturnReceiver(interpreter);
        Handlers[121] = (interpreter, opcode) => HandleReturnTrue(interpreter);
        Handlers[122] = (interpreter, opcode) => HandleReturnFalse(interpreter);
        Handlers[123] = (interpreter, opcode) => HandleReturnNil(interpreter);
        Handlers[124] = (interpreter, opcode) => HandleReturnTop(interpreter);
        Handlers[125] = (interpreter, opcode) => HandleReturnTopFromBlock(interpreter);
        
        // Extended return bytecodes (126-127) - unused in Blue Book but used in compiled code
        Handlers[126] = (interpreter, opcode) => HandleExtendedReturn(interpreter);
        Handlers[127] = (interpreter, opcode) => HandleExtendedReturn(interpreter);
        
        // Extended push/store/pop bytecodes (128-134) - Single byte extensions
        Handlers[128] = (interpreter, opcode) => HandleExtendedPush(interpreter);
        Handlers[129] = (interpreter, opcode) => HandleExtendedStore(interpreter);
        Handlers[130] = (interpreter, opcode) => HandleExtendedStoreAndPop(interpreter);
        Handlers[131] = (interpreter, opcode) => HandleExtendedSend(interpreter);
        Handlers[132] = (interpreter, opcode) => HandleExtendedSuperSend(interpreter);
        Handlers[133] = (interpreter, opcode) => HandlePopStack(interpreter);
        Handlers[134] = (interpreter, opcode) => HandleDuplicateTop(interpreter);
        
        // Stack operations (135-137)
        Handlers[135] = (interpreter, opcode) => HandlePushActiveContext(interpreter);
        // 136-137 are unused in Blue Book
        
        // 138-143 are unused in canonical Blue Book but used in compiled code
        for (int i = 138; i <= 143; i++)
        {
            Handlers[i] = (interpreter, opcode) => HandleExtendedBytecode(interpreter, opcode);
        }

        // Jump operations (144-175) - Blue Book specification
        // 144-151: Jump if false (conditional) offset 1-8
        for (int i = 144; i <= 151; i++)
        {
            int offset = i - 143; // Jump offset 1-8
            Handlers[i] = (interpreter, opcode) => HandleJumpIfFalse(interpreter, offset);
        }
        
        // 152-159: Jump forward (unconditional) offset 1-8
        for (int i = 152; i <= 159; i++)
        {
            int offset = i - 151; // Jump offset 1-8
            Handlers[i] = (interpreter, opcode) => HandleJumpForward(interpreter, offset);
        }
        
        // 160-167: Long jump if true with pop, next byte is offset + 4
        for (int i = 160; i <= 167; i++)
        {
            int highBits = (i - 160) * 256; // High bits of offset
            Handlers[i] = (interpreter, opcode) => HandleLongJumpIfTrue(interpreter, highBits);
        }
        
        // 168-171: Long jump if false with pop, next byte is offset + 4
        for (int i = 168; i <= 171; i++)
        {
            int highBits = (i - 168) * 256; // High bits of offset
            Handlers[i] = (interpreter, opcode) => HandleLongJumpIfFalse(interpreter, highBits);
        }
        
        // 172-175: Long unconditional jump, next byte is offset
        for (int i = 172; i <= 175; i++)
        {
            int highBits = (i - 172) * 256; // High bits of offset
            Handlers[i] = (interpreter, opcode) => HandleLongJumpForward(interpreter, highBits);
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
        
        // 224-239: Send arithmetic selector (special selectors 0-15)
        for (int i = 224; i <= 239; i++)
        {
            int index = i - 224; // Special selector index 0-15
            Handlers[i] = (interpreter, opcode) => HandleSendArithmeticSelector(interpreter, index);
        }
        
        // 240-255: Send special selector (special selectors 16-31)
        for (int i = 240; i <= 255; i++)
        {
            int index = i - 240 + 16; // Special selector index 16-31
            Handlers[i] = (interpreter, opcode) => HandleSendSpecialSelector(interpreter, index);
        }
    }

    /// <summary>
    /// Dispatches a bytecode to its appropriate handler.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="opcode">The bytecode to dispatch.</param>
    public static void Dispatch(Interpreter interpreter, byte opcode)
    {
        // Debug logging for Phase 3 unknown methods (disabled for tests)
        // if (interpreter.CurrentContext.Method.SelectorName == null || interpreter.CurrentContext.Method.SelectorName == "unknown")
        // {
        //     Console.WriteLine($"[DEBUG] PC={interpreter.CurrentContext.PC-1}, opcode={opcode}");
        // }
        
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



    private static void HandleSendLiteralSelector(Interpreter interpreter, int index, int argCount)
    {
        var context = interpreter.CurrentContext;
        
        Console.WriteLine($"[DEBUG] HandleSendLiteralSelector: index={index}, argCount={argCount}, stack count={context.Stack.Count}");
        
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
        
        Console.WriteLine($"[DEBUG]   Literal type: {literal.GetType().Name}");
        
        // Handle both C# Symbols (from Phase 1/2) and SmalltalkObject Symbols (from Phase 3)
        if (literal is Symbol symbol)
        {
            // C# Symbol - use directly
            Console.WriteLine($"[DEBUG]   C# Symbol: {symbol}");
            interpreter.PerformSend(symbol, argCount, false);
        }
        else if (literal is SmalltalkObject symbolObj)
        {
            // SmalltalkObject Symbol - extract the string and create a C# Symbol
            if (symbolObj.Fields.Length > 0 && symbolObj.Fields[0] is string str)
            {
                Console.WriteLine($"[DEBUG]   SmalltalkObject Symbol: {str}");
                interpreter.PerformSend(new Symbol(str), argCount, false);
            }
            else
            {
                throw new InvalidOperationException($"Literal at index {index} is a SmalltalkObject but doesn't contain a string");
            }
        }
        else
        {
            throw new InvalidOperationException($"Literal at index {index} is not a Symbol, got {literal.GetType().Name}");
        }
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
        interpreter.HandleReturn(topValue!);
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

    private static void HandlePopStack(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Pop();
    }
    
    private static void HandleDuplicateTop(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        var top = context.Stack.Peek();
        context.Stack.Push(top);
    }
    
    private static void HandlePushActiveContext(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Push(context);
    }
    
    private static void HandlePushConstantTrue(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(true);
    }
    
    private static void HandlePushConstantFalse(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(false);
    }
    
    private static void HandlePushConstantNil(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(null);
    }
    
    private static void HandlePushConstantMinusOne(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(-1);
    }
    
    private static void HandlePushConstantZero(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(0);
    }
    
    private static void HandlePushConstantOne(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(1);
    }
    
    private static void HandlePushConstantTwo(Interpreter interpreter)
    {
        interpreter.CurrentContext.Stack.Push(2);
    }
    
    private static void HandleSendArithmeticSelector(Interpreter interpreter, int index)
    {
        // Arithmetic selectors are special selectors 0-15 and are binary messages
        // They always take 1 argument
        var context = interpreter.CurrentContext;
        
        // Pop arguments from stack
        var arg = context.Stack.Pop();
        var receiver = context.Stack.Pop();
        
        // For now, handle basic integer arithmetic
        if (receiver is int intReceiver && arg is int intArg)
        {
            int result;
            switch (index)
            {
                case 0: // +
                    result = intReceiver + intArg;
                    break;
                case 1: // -
                    result = intReceiver - intArg;
                    break;
                case 2: // <
                    context.Stack.Push(intReceiver < intArg);
                    return;
                case 3: // >
                    context.Stack.Push(intReceiver > intArg);
                    return;
                case 4: // <=
                    context.Stack.Push(intReceiver <= intArg);
                    return;
                case 5: // >=
                    context.Stack.Push(intReceiver >= intArg);
                    return;
                case 6: // =
                    context.Stack.Push(intReceiver == intArg);
                    return;
                case 7: // ~=
                    context.Stack.Push(intReceiver != intArg);
                    return;
                case 8: // *
                    result = intReceiver * intArg;
                    break;
                case 9: // /
                    result = intReceiver / intArg;
                    break;
                case 10: // \\
                    result = intReceiver % intArg;
                    break;
                case 11: // @
                    // Point creation - just push back for now
                    context.Stack.Push(new object[] { intReceiver, intArg });
                    return;
                case 12: // bitShift:
                    result = intArg > 0 ? intReceiver << intArg : intReceiver >> -intArg;
                    break;
                case 13: // //
                    result = intReceiver / intArg;
                    break;
                case 14: // bitAnd:
                    result = intReceiver & intArg;
                    break;
                case 15: // bitOr:
                    result = intReceiver | intArg;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown arithmetic selector index: {index}");
            }
            context.Stack.Push(result);
        }
        else
        {
            // For non-integer types, would need to perform proper message send
            // For now, just throw an error
            throw new NotImplementedException($"Arithmetic on non-integers not yet implemented");
        }
    }
    
    private static void HandleSendSpecialSelector(Interpreter interpreter, int index)
    {
        // Special selectors 16-31 with varying argument counts
        // For now, just perform a regular send
        var argCounts = new int[] 
        {
            1,2,0,0,1,0,1,0,1,0,1,1,0,1,0,0
        };
        var actualIndex = index - 16;
        if (actualIndex >= 0 && actualIndex < argCounts.Length)
        {
            HandleSendLiteralSelector(interpreter, actualIndex, argCounts[actualIndex]);
        }
        else
        {
            throw new InvalidOperationException($"Invalid special selector index: {index}");
        }
    }

    private static void HandleUnknownBytecode(Interpreter interpreter, byte opcode)
    {
        var context = interpreter.CurrentContext;
        Console.WriteLine($"[BYTECODE ERROR] Unknown bytecode: {opcode} (0x{opcode:X2})");
        Console.WriteLine($"[BYTECODE ERROR] Method has {context.Method.Bytecodes.Length} bytes total");
        Console.WriteLine($"[BYTECODE ERROR] Current PC: {context.PC}");
        Console.WriteLine($"[BYTECODE ERROR] Full bytecode sequence:");
        for (int i = 0; i < Math.Min(context.Method.Bytecodes.Length, 20); i++)
        {
            var b = context.Method.Bytecodes[i];
            Console.WriteLine($"[BYTECODE ERROR]   [{i}]: {b} (0x{b:X2})");
        }
        throw new NotImplementedException($"Unknown bytecode: {opcode} (0x{opcode:X2})");
    }

    // Additional handler methods would be implemented here following the same pattern
    // This provides a foundation that's much faster than the current if/else chain




    // Additional placeholder handlers - full implementation would include all bytecodes
    private static void HandlePushLiteralConstant(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var literal = context.Method.Literals[index];
        
        // SmallIntegers are stored as tagged integers: (value << 1) | 1
        // We need to push them as-is for Smalltalk compatibility
        context.Stack.Push(literal);
    }

    private static void HandlePushLiteralVariable(Interpreter interpreter, int index)
    {
        var context = interpreter.CurrentContext;
        var literal = context.Method.Literals[index];
        
        // According to Smalltalk-80 specification, PushLiteralVariable (bytecodes 64-95)
        // MUST have an Association in the literal frame, never anything else.
        // An Association has 2 fields: key (Symbol) and value (the actual object).
        // The bytecode pushes the value field of the Association.
        
        if (literal is Association assoc)
        {
            // C# Association object (from Phase 1/2)
            context.Stack.Push(assoc.Value);
        }
        else if (literal is SmalltalkObject smalltalkAssoc && smalltalkAssoc.Fields.Length == 2)
        {
            // SmalltalkObject Association - must have exactly 2 fields
            // Field 0: key (Symbol)
            // Field 1: value (the object)
            var value = smalltalkAssoc.Fields[1];
            context.Stack.Push(value);
        }
        else
        {
            // This is an error - PushLiteralVariable requires an Association
            throw new InvalidOperationException(
                $"PushLiteralVariable at index {index} requires an Association, " +
                $"but found {literal?.GetType()?.Name ?? "null"}" +
                (literal is SmalltalkObject so ? $" with {so.Fields.Length} fields" : "") +
                $". This indicates incorrect literal creation in the bootstrap.");
        }
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

    // Extended bytecode handlers

    private static void HandleExtendedReturn(Interpreter interpreter)
    {
        // Extended return - used for returning from blocks to methods
        var context = interpreter.CurrentContext;
        var value = context.Stack.Pop();
        interpreter.HandleReturn(value ?? throw new InvalidOperationException("Cannot return null"));
    }

    private static void HandleExtendedPush(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        byte extByte = context.Method.Bytecodes[context.PC++];
        
        // Extended push format: bits 6-7 = type, bits 0-5 = index
        int type = (extByte >> 6) & 0x03;
        int index = extByte & 0x3F;
        
        switch (type)
        {
            case 0: // Receiver variable
                context.Stack.Push(context.Receiver.Fields[index]);
                break;
            case 1: // Temporary variable
                context.Stack.Push(context.Temporaries[index]);
                break;
            case 2: // Literal constant
                context.Stack.Push(context.Method.Literals[index]);
                break;
            case 3: // Literal variable (Association value)
                var assoc = context.Method.Literals[index];
                if (assoc is Association a)
                    context.Stack.Push(a.Value);
                else if (assoc is SmalltalkObject obj && obj.Fields.Length == 2)
                    context.Stack.Push(obj.Fields[1]);
                else
                    throw new InvalidOperationException($"Extended push literal variable requires Association at index {index}");
                break;
        }
    }

    private static void HandleExtendedStore(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        byte extByte = context.Method.Bytecodes[context.PC++];
        
        // Extended store format: bits 6-7 = type, bits 0-5 = index
        int type = (extByte >> 6) & 0x03;
        int index = extByte & 0x3F;
        var value = context.Stack.Peek(); // Store doesn't pop
        
        switch (type)
        {
            case 0: // Receiver variable
                context.Receiver.Fields[index] = value;
                break;
            case 1: // Temporary variable
                context.Temporaries[index] = value;
                break;
            case 2: // Illegal - can't store into literal constant
                throw new InvalidOperationException("Cannot store into literal constant");
            case 3: // Literal variable (Association value)
                var assoc = context.Method.Literals[index];
                if (assoc is Association a)
                    a.Value = value;
                else if (assoc is SmalltalkObject obj && obj.Fields.Length == 2)
                    obj.Fields[1] = value;
                else
                    throw new InvalidOperationException($"Extended store literal variable requires Association at index {index}");
                break;
        }
    }

    private static void HandleExtendedStoreAndPop(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        byte extByte = context.Method.Bytecodes[context.PC++];
        
        // Extended store and pop format: bits 6-7 = type, bits 0-5 = index
        int type = (extByte >> 6) & 0x03;
        int index = extByte & 0x3F;
        var value = context.Stack.Pop(); // Store and pop does pop
        
        switch (type)
        {
            case 0: // Receiver variable
                context.Receiver.Fields[index] = value;
                break;
            case 1: // Temporary variable
                context.Temporaries[index] = value;
                break;
            case 2: // Illegal - can't store into literal constant
                throw new InvalidOperationException("Cannot store into literal constant");
            case 3: // Literal variable (Association value)
                var assoc = context.Method.Literals[index];
                if (assoc is Association a)
                    a.Value = value;
                else if (assoc is SmalltalkObject obj && obj.Fields.Length == 2)
                    obj.Fields[1] = value;
                else
                    throw new InvalidOperationException($"Extended store and pop literal variable requires Association at index {index}");
                break;
        }
    }

    private static void HandleExtendedSend(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        byte extByte = context.Method.Bytecodes[context.PC++];
        
        // Extended send format: bits 5-7 = arg count, bits 0-4 = literal selector index
        int argCount = (extByte >> 5) & 0x07;
        int selectorIndex = extByte & 0x1F;
        
        HandleSendLiteralSelector(interpreter, selectorIndex, argCount);
    }

    private static void HandleExtendedSuperSend(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        byte extByte = context.Method.Bytecodes[context.PC++];
        
        // Extended super send format: bits 5-7 = arg count, bits 0-4 = literal selector index
        int argCount = (extByte >> 5) & 0x07;
        int selectorIndex = extByte & 0x1F;
        
        // For now, treat super send like regular send
        // TODO: Implement proper super send semantics
        HandleSendLiteralSelector(interpreter, selectorIndex, argCount);
    }

    private static void HandleExtendedBytecode(Interpreter interpreter, byte opcode)
    {
        // Bytecodes 138-143 are used in the compiled system but not defined in Blue Book
        // They appear to be additional extended operations
        // For now, throw an error so we can identify what they're supposed to do
        var context = interpreter.CurrentContext;
        Console.WriteLine($"[WARNING] Extended bytecode {opcode} (0x{opcode:X2}) used but not fully implemented");
        
        // Based on usage patterns, these might be additional jump or store operations
        // For now, just skip them
        throw new NotImplementedException($"Extended bytecode {opcode} not yet implemented");
    }

    // Jump operation handlers - Blue Book specification

    private static void HandleJumpIfFalse(Interpreter interpreter, int offset)
    {
        var context = interpreter.CurrentContext;
        var condition = context.Stack.Pop();
        
        // In Smalltalk, false and nil are falsy, everything else is truthy
        bool isFalse = condition == null || 
                       (condition is bool b && !b);
        
        if (isFalse)
        {
            context.PC += offset; // Jump forward by offset
        }
    }

    private static void HandleJumpForward(Interpreter interpreter, int offset)
    {
        var context = interpreter.CurrentContext;
        context.PC += offset; // Unconditional jump forward
    }

    private static void HandleLongJumpIfTrue(Interpreter interpreter, int highBits)
    {
        var context = interpreter.CurrentContext;
        byte lowByte = context.Method.Bytecodes[context.PC++];
        int offset = highBits + lowByte;
        
        var condition = context.Stack.Pop();
        
        // In Smalltalk, everything except false and nil is truthy
        bool isTrue = condition != null && 
                      !(condition is bool b && !b);
        
        if (isTrue)
        {
            context.PC += offset - 1; // -1 because we already incremented PC for the parameter byte
        }
    }

    private static void HandleLongJumpIfFalse(Interpreter interpreter, int highBits)
    {
        var context = interpreter.CurrentContext;
        byte lowByte = context.Method.Bytecodes[context.PC++];
        int offset = highBits + lowByte;
        
        var condition = context.Stack.Pop();
        
        // In Smalltalk, false and nil are falsy
        bool isFalse = condition == null || 
                       (condition is bool b && !b);
        
        if (isFalse)
        {
            context.PC += offset - 1; // -1 because we already incremented PC for the parameter byte
        }
    }

    private static void HandleLongJumpForward(Interpreter interpreter, int highBits)
    {
        var context = interpreter.CurrentContext;
        byte lowByte = context.Method.Bytecodes[context.PC++];
        int offset = highBits + lowByte;
        
        context.PC += offset - 1; // -1 because we already incremented PC for the parameter byte
    }

}
