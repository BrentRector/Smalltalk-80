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
        // 113-119 are part of pushReceiver range but unused in canonical Blue Book

        // pushConstant range (120-127) - Push constants like nil, true, false, -1 to 2
        // These must be in the literal frame in canonical Blue Book
        // 120-127 are reserved but not specifically defined in canonical spec
        
        // 128-135 are pushActiveContext and related (except 135 which is pop stack)
        Handlers[135] = (interpreter, opcode) => HandlePopStack(interpreter);
        
        // 136-143 are return operations
        Handlers[136] = (interpreter, opcode) => HandleReturnReceiver(interpreter);
        Handlers[137] = (interpreter, opcode) => HandleReturnTop(interpreter);
        Handlers[138] = (interpreter, opcode) => HandleReturnTopFromBlock(interpreter);
        // 139-143 are additional return operations

        // pushActiveContext range (128-134)
        // These are context-related push operations
        // Not fully defined in canonical spec

        // Send operations (144-175)
        // 144-151: sendSelector with 0 args (literal index 0-7)
        for (int i = 144; i <= 151; i++)
        {
            int index = i - 144; // Literal index 0-7
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 0);
        }
        
        // 152-159: sendSelector with 1 arg (literal index 0-7)
        for (int i = 152; i <= 159; i++)
        {
            int index = i - 152; // Literal index 0-7
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 1);
        }
        
        // 160-167: sendSelector with 2 args (literal index 0-7)
        for (int i = 160; i <= 167; i++)
        {
            int index = i - 160; // Literal index 0-7
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 2);
        }
        
        // 168-175: sendSelector with 3 args (literal index 0-7)
        for (int i = 168; i <= 175; i++)
        {
            int index = i - 168; // Literal index 0-7
            Handlers[i] = (interpreter, opcode) => HandleSendLiteralSelector(interpreter, index, 3);
        }

        // Send operations (176-223) - Blue Book specification
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
        
        // 224-255: Unused in canonical Blue Book
        // Later implementations use these for extended operations
    }

    /// <summary>
    /// Dispatches a bytecode to its appropriate handler.
    /// </summary>
    /// <param name="interpreter">The interpreter instance.</param>
    /// <param name="opcode">The bytecode to dispatch.</param>
    public static void Dispatch(Interpreter interpreter, byte opcode)
    {
        // Debug logging for Phase 3 unknown methods
        if (interpreter.CurrentContext.Method.SelectorName == null || interpreter.CurrentContext.Method.SelectorName == "unknown")
        {
            Console.WriteLine($"[DEBUG] PC={interpreter.CurrentContext.PC-1}, opcode={opcode}");
        }
        
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
        interpreter.HandleReturn(topValue ?? throw new InvalidOperationException("Cannot return null value"));
    }

    private static void HandlePopStack(Interpreter interpreter)
    {
        var context = interpreter.CurrentContext;
        context.Stack.Pop();
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
        
        Console.WriteLine($"[DEBUG] HandlePushLiteralVariable: index={index}, literal type={literal?.GetType()?.Name ?? "null"}");
        Console.WriteLine($"[DEBUG]   Current method: {context.Method.SelectorName ?? "unknown"}");
        if (literal is SmalltalkObject so)
        {
            Console.WriteLine($"[DEBUG]   Literal is SmalltalkObject OOP={so.Oop}, fields={so.Fields.Length}");
        }
        
        if (literal is Association assoc)
        {
            // C# Association object (from Phase 1/2)
            Console.WriteLine($"[DEBUG]   C# Association key={assoc.Key}, value type={assoc.Value?.GetType()?.Name ?? "null"}");
            context.Stack.Push(assoc.Value);
        }
        else if (literal is SmalltalkObject smalltalkAssoc)
        {
            // SmalltalkObject representing an Association (from Phase 3)
            // Association format: [key, value]
            Console.WriteLine($"[DEBUG]   SmalltalkObject with {smalltalkAssoc.Fields.Length} fields");
            if (smalltalkAssoc.Fields.Length >= 2)
            {
                var value = smalltalkAssoc.Fields[1]; // value field
                Console.WriteLine($"[DEBUG]   Pushing value: {value?.GetType()?.Name ?? "null"}");
                context.Stack.Push(value);
                Console.WriteLine($"[DEBUG]   Stack now has {context.Stack.Count} items");
            }
            else
            {
                Console.WriteLine($"[DEBUG] ERROR: Association has {smalltalkAssoc.Fields.Length} fields, expected 2");
                Console.WriteLine($"[DEBUG]   Association OOP: {smalltalkAssoc.Oop}");
                Console.WriteLine($"[DEBUG]   Association class: {smalltalkAssoc.ClassPointer?.Oop ?? -1}");
                for (int i = 0; i < smalltalkAssoc.Fields.Length; i++)
                {
                    Console.WriteLine($"[DEBUG]   Field[{i}]: {smalltalkAssoc.Fields[i]?.GetType()?.Name ?? "null"}");
                }
                throw new InvalidOperationException($"SmalltalkObject Association at literal {index} has insufficient fields ({smalltalkAssoc.Fields.Length})");
            }
        }
        else
        {
            throw new InvalidOperationException($"Literal variable at index {index} is not an Association, got {literal?.GetType()?.Name ?? "null"}");
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



}
