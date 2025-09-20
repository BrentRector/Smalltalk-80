using System;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Dispatches and executes Smalltalk-80 bytecodes according to the Blue Book specification.
/// Implements the complete bytecode set as defined in Chapter 28 of the Blue Book (1983).
/// </summary>
public sealed class BytecodeDispatcher
{
    private readonly IExecutionContext executionContext;
    
    /// <summary>
    /// Creates a new bytecode dispatcher.
    /// </summary>
    /// <param name="context">The execution context for VM services.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public BytecodeDispatcher(IExecutionContext context)
    {
        executionContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Executes a single bytecode instruction according to Blue Book specification.
    /// </summary>
    /// <param name="context">The method context in which to execute.</param>
    /// <param name="bytecode">The bytecode instruction to execute.</param>
    /// <returns>True to continue execution, false to stop (e.g., return).</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public bool ExecuteBytecode(MethodContext context, byte bytecode)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        // Trace bytecode execution if enabled
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"[BYTECODE] PC={context.PC - 1}, Bytecode={bytecode:X2} ({bytecode}), Stack depth={context.Stack.Count}");
        }
        
        // Dispatch based on Blue Book bytecode specification
        return bytecode switch
        {
            // 0-15: Push Receiver Variable
            <= 15 => PushReceiverVariable(context, bytecode),
            
            // 16-31: Push Temporary Location
            <= 31 => PushTemporaryVariable(context, bytecode - 16),
            
            // 32-63: Push Literal Constant
            <= 63 => PushLiteralConstant(context, bytecode - 32),
            
            // 64-95: Push Literal Variable (Association value)
            <= 95 => PushLiteralVariable(context, bytecode - 64),
            
            // 96-103: Pop and Store Receiver Variable
            <= 103 => PopAndStoreReceiverVariable(context, bytecode - 96),
            
            // 104-111: Pop and Store Temporary Location
            <= 111 => PopAndStoreTemporaryVariable(context, bytecode - 104),
            
            // 112-119: Push (receiver, true, false, nil, -1, 0, 1, 2)
            112 => PushReceiver(context),
            113 => PushConstant(context, executionContext.GetSpecialObject(SpecialObjects.TrueObject)),
            114 => PushConstant(context, executionContext.GetSpecialObject(SpecialObjects.FalseObject)),
            115 => PushConstant(context, OOP.Nil),
            116 => PushConstant(context, OOP.FromSmallInteger(-1)),
            117 => PushConstant(context, OOP.FromSmallInteger(0)),
            118 => PushConstant(context, OOP.FromSmallInteger(1)),
            119 => PushConstant(context, OOP.FromSmallInteger(2)),
            
            // 120-123: Return (receiver, true, false, nil) From Message
            120 => ReturnReceiver(context),
            121 => ReturnConstant(context, executionContext.GetSpecialObject(SpecialObjects.TrueObject)),
            122 => ReturnConstant(context, executionContext.GetSpecialObject(SpecialObjects.FalseObject)),
            123 => ReturnConstant(context, OOP.Nil),
            
            // 124: Return Stack Top from Message
            124 => ReturnTop(context),
            
            // 125: Return Stack Top from Block
            125 => BlockReturn(context),
            
            // 126-127: unused
            126 or 127 => true,
            
            // 128: Extended Push
            128 => ExtendedPush(context),
            
            // 129: Extended Store
            129 => ExtendedStore(context),
            
            // 130: Extended Pop and Store
            130 => ExtendedPopAndStore(context),
            
            // 131: Send Literal Selector with Arguments
            131 => SendLiteralSelectorWithArguments(context),
            
            // 132: Send Literal Selector (extended) with Arguments
            132 => SendLiteralSelectorExtended(context),
            
            // 133: Send Literal Selector to Superclass with Arguments
            133 => SuperSendLiteralSelectorWithArguments(context),
            
            // 134: Send Literal Selector (extended) to Superclass with Arguments
            134 => SuperSendLiteralSelectorExtended(context),
            
            // 135: Pop Stack Top
            135 => PopStackTop(context),
            
            // 136: Duplicate Stack Top
            136 => DuplicateTop(context),
            
            // 137: Push Active Context
            137 => PushActiveContext(context),
            
            // 138-143: unused
            >= 138 and <= 143 => true,
            
            // 144-151: Jump iii+1 (i.e., 1 through 8)
            >= 144 and <= 151 => ShortJump(context, (bytecode & 0x07) + 1),
            
            // 152-159: Pop and Jump On False iii+1 (i.e., 1 through 8)
            >= 152 and <= 159 => ShortJumpOnFalse(context, (bytecode & 0x07) + 1),
            
            // 160-167: Long Jump (iii+4)*256 + next byte
            >= 160 and <= 167 => LongJump(context, (bytecode & 0x07) + 4),
            
            // 168-171: Pop and Jump On True ii*256 + next byte
            >= 168 and <= 171 => LongJumpOnTrue(context, bytecode & 0x03),
            
            // 172-175: Pop and Jump On False ii*256 + next byte
            >= 172 and <= 175 => LongJumpOnFalse(context, bytecode & 0x03),
            
            // 176-191: Send Arithmetic Message #iiii
            >= 176 and <= 191 => SendArithmeticMessage(context, bytecode & 0x0F),
            
            // 192-207: Send Special Message #iiii
            >= 192 and <= 207 => SendSpecialMessage(context, bytecode & 0x0F),
            
            // 208-223: Send Literal Selector #iiii With No Arguments
            >= 208 and <= 223 => SendLiteralSelectorNoArgs(context, bytecode & 0x0F),
            
            // 224-239: Send Literal Selector #iiii With 1 Argument
            >= 224 and <= 239 => SendLiteralSelectorOneArg(context, bytecode & 0x0F),
            
            // 240-255: Send Literal Selector #iiii With 2 Arguments
            _ => SendLiteralSelectorTwoArgs(context, bytecode & 0x0F)
        };
    }

    #region Stack Operations - Push

    /// <summary>
    /// Pushes the specified instance variable of the receiver onto the stack.
    /// </summary>
    private bool PushReceiverVariable(MethodContext context, int index)
    {
        var value = executionContext.GetFieldByIndex(context.Receiver, index);
        context.Stack.Push(value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushReceiverVariable #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Pushes the specified temporary variable onto the stack.
    /// </summary>
    private bool PushTemporaryVariable(MethodContext context, int index)
    {
        var value = index < context.Temporaries.Length ? context.Temporaries[index] : OOP.Nil;
        context.Stack.Push(value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushTemporaryVariable #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Pushes the receiver onto the stack.
    /// </summary>
    private bool PushReceiver(MethodContext context)
    {
        context.Stack.Push(context.Receiver);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushReceiver = {context.Receiver}");
        }
        
        return true;
    }

    /// <summary>
    /// Pushes a constant value onto the stack.
    /// </summary>
    private bool PushConstant(MethodContext context, OOP constant)
    {
        context.Stack.Push(constant);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushConstant = {constant}");
        }
        
        return true;
    }

    /// <summary>
    /// Pushes a literal constant from the method's literal frame onto the stack.
    /// </summary>
    private bool PushLiteralConstant(MethodContext context, int index)
    {
        var literals = GetLiterals(context.Method);
        var value = index < literals.Length ? literals[index] : OOP.Nil;
        context.Stack.Push(value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushLiteralConstant #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Pushes the value field of a literal Association onto the stack.
    /// </summary>
    private bool PushLiteralVariable(MethodContext context, int index)
    {
        var literals = GetLiterals(context.Method);
        if (index < literals.Length)
        {
            // The literal should be an Association; push its value field
            var association = literals[index];
            
            // Enhanced debug output to diagnose the Association issue
            if (DebugOptions.EnableBytecodeTrace)
            {
                var assocObj = executionContext.Memory.GetObject(association);
                if (assocObj != null && assocObj.Fields.Length >= 2)
                {
                    var keyField = assocObj.Fields[0];
                    var valueField = assocObj.Fields[1];
                    var keyStr = executionContext.GetSymbolString(keyField);
                    Console.WriteLine($"  -> PushLiteralVariable #{index}: Association key='{keyStr}', value={valueField}");
                }
                else
                {
                    Console.WriteLine($"  -> PushLiteralVariable #{index}: Association={association} (invalid or nil)");
                }
            }
            
            var value = executionContext.GetFieldByName(association, "value");
            
            // Debug: Check if value is valid
            if (value.IsNil && DebugOptions.EnableDebugOutput)
            {
                Console.WriteLine($"[DEBUG] WARNING: PushLiteralVariable #{index} has nil value!");
            }
            
            context.Stack.Push(value);
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> Pushed value: {value}");
            }
        }
        else
        {
            context.Stack.Push(OOP.Nil);
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> PushLiteralVariable #{index} out of range, pushing Nil");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Pops the top of stack and stores it in the specified receiver variable.
    /// </summary>
    private bool PopAndStoreReceiverVariable(MethodContext context, int index)
    {
        var value = context.Stack.Pop();
        executionContext.SetFieldByIndex(context.Receiver, index, value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PopAndStoreReceiverVariable #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Pops the top of stack and stores it in the specified temporary variable.
    /// </summary>
    private bool PopAndStoreTemporaryVariable(MethodContext context, int index)
    {
        var value = context.Stack.Pop();
        if (index < context.Temporaries.Length)
        {
            context.Temporaries[index] = value;
        }
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PopAndStoreTemporaryVariable #{index} = {value}");
        }
        
        return true;
    }

    #endregion

    #region Stack Operations - Manipulation

    /// <summary>
    /// Pops and discards the top element of the stack.
    /// </summary>
    private bool PopStackTop(MethodContext context)
    {
        if (context.Stack.Count == 0)
        {
            throw new InvalidOperationException($"PopStackTop (bytecode 135): Stack is empty at PC {context.PC-1}");
        }
        
        context.Stack.Pop();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine("  -> PopStackTop");
        }
        
        return true;
    }

    /// <summary>
    /// Duplicates the top element of the stack.
    /// </summary>
    private bool DuplicateTop(MethodContext context)
    {
        context.Stack.Duplicate();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine("  -> DuplicateTop");
        }
        
        return true;
    }

    #endregion

    #region Primitive Operations

    /// <summary>
    /// Executes a VM primitive operation.
    /// </summary>
    private bool ExecutePrimitive(MethodContext context, int primitiveIndex)
    {
        // Primitive execution would be handled by the VM
        // For now, this is a placeholder
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> DoPrimitive #{primitiveIndex} (not implemented)");
        }
        
        return true;
    }

    #endregion

    #region Context Operations

    /// <summary>
    /// Pushes the current method context object onto the stack.
    /// </summary>
    private bool PushActiveContext(MethodContext context)
    {
        var contextOop = executionContext.CreateContextObject(context);
        context.Stack.Push(contextOop);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushActiveContext = {contextOop}");
        }
        
        return true;
    }

    #endregion

    #region Block Operations

    /// <summary>
    /// Creates and pushes a block closure onto the stack.
    /// </summary>
    private bool PushBlockCopy(MethodContext context, int blockIndex)
    {
        // Block creation - simplified for now
        // Should create a BlockContext with homeContext set to current context
        var blockContext = executionContext.CreateBlockContext(context, blockIndex);
        context.Stack.Push(blockContext);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> PushBlockCopy #{blockIndex} = {blockContext}");
        }
        
        return true;
    }

    #endregion

    #region Return Operations

    /// <summary>
    /// Returns the receiver to the calling context.
    /// </summary>
    private bool ReturnReceiver(MethodContext context)
    {
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> ReturnReceiver = {context.Receiver}");
        }
        
        executionContext.Return(context, context.Receiver);
        return false;
    }

    /// <summary>
    /// Returns the specified constant to the calling context.
    /// </summary>
    private bool ReturnConstant(MethodContext context, OOP constant)
    {
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> ReturnConstant = {constant}");
        }
        
        executionContext.Return(context, constant);
        return false;
    }

    /// <summary>
    /// Returns the top of stack to the calling context.
    /// </summary>
    private bool ReturnTop(MethodContext context)
    {
        var returnValue = context.Stack.Pop();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> ReturnTopOfStack = {returnValue}");
        }
        
        executionContext.Return(context, returnValue);
        return false;
    }

    /// <summary>
    /// Performs a non-local return from a block to its home context.
    /// </summary>
    private bool BlockReturn(MethodContext context)
    {
        var returnValue = context.Stack.Pop();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> BlockReturn = {returnValue}");
        }
        
        executionContext.ReturnFromBlock(context, returnValue);
        return false;
    }

    #endregion

    #region Extended Operations (128-137)

    /// <summary>
    /// Extended Push: Push (Receiver Variable, Temporary Location, Literal Constant, Literal Variable) [jj] #kkkkkk.
    /// </summary>
    private bool ExtendedPush(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var variableType = (nextByte >> 6) & 0x03; // Extract jj (bits 6-7)
        var variableIndex = nextByte & 0x3F; // Extract kkkkkk (bits 0-5)
        
        return variableType switch
        {
            0 => PushReceiverVariable(context, variableIndex),
            1 => PushTemporaryVariable(context, variableIndex),
            2 => PushLiteralConstant(context, variableIndex),
            3 => PushLiteralVariable(context, variableIndex),
            _ => true
        };
    }

    /// <summary>
    /// Extended Store: Store (Receiver Variable, Temporary Location, Illegal, Literal Variable) [jj] #kkkkkk.
    /// </summary>
    private bool ExtendedStore(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var variableType = (nextByte >> 6) & 0x03; // Extract jj (bits 6-7)
        var variableIndex = nextByte & 0x3F; // Extract kkkkkk (bits 0-5)
        
        if (context.Stack.Count == 0)
        {
            throw new InvalidOperationException($"ExtendedStore: Stack is empty at PC {context.PC-2}");
        }
        
        var value = context.Stack.Top; // Store doesn't pop
        
        return variableType switch
        {
            0 => StoreReceiverVariable(context, variableIndex, value),
            1 => StoreTemporaryVariable(context, variableIndex, value),
            2 => true, // Illegal
            3 => StoreLiteralVariable(context, variableIndex, value),
            _ => true
        };
    }

    /// <summary>
    /// Extended Pop and Store: Pop and Store (Receiver Variable, Temporary Location, Illegal, Literal Variable) [jj] #kkkkkk.
    /// </summary>
    private bool ExtendedPopAndStore(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var variableType = (nextByte >> 6) & 0x03; // Extract jj (bits 6-7)
        var variableIndex = nextByte & 0x3F; // Extract kkkkkk (bits 0-5)
        
        return variableType switch
        {
            0 => PopAndStoreReceiverVariable(context, variableIndex),
            1 => PopAndStoreTemporaryVariable(context, variableIndex),
            2 => true, // Illegal
            3 => PopAndStoreLiteralVariable(context, variableIndex),
            _ => true
        };
    }

    /// <summary>
    /// Send Literal Selector #kkkkk With jjj Arguments.
    /// </summary>
    private bool SendLiteralSelectorWithArguments(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var argCount = (nextByte >> 5) & 0x07; // Extract jjj (bits 5-7)
        var selectorIndex = nextByte & 0x1F; // Extract kkkkk (bits 0-4)
        
        return SendLiteralSelector(context, selectorIndex, argCount);
    }

    /// <summary>
    /// Send Literal Selector #kkkkkkkk With jjjjjjjj Arguments (extended).
    /// </summary>
    private bool SendLiteralSelectorExtended(MethodContext context)
    {
        var selectorIndex = GetNextByte(context);
        var argCount = GetNextByte(context);
        
        return SendLiteralSelector(context, selectorIndex, argCount);
    }

    /// <summary>
    /// Send Literal Selector #kkkkk To Superclass With jjj Arguments.
    /// </summary>
    private bool SuperSendLiteralSelectorWithArguments(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var argCount = (nextByte >> 5) & 0x07; // Extract jjj (bits 5-7)
        var selectorIndex = nextByte & 0x1F; // Extract kkkkk (bits 0-4)
        
        return SuperSendLiteralSelector(context, selectorIndex, argCount);
    }

    /// <summary>
    /// Send Literal Selector #kkkkkkkk To Superclass With jjjjjjjj Arguments (extended).
    /// </summary>
    private bool SuperSendLiteralSelectorExtended(MethodContext context)
    {
        var selectorIndex = GetNextByte(context);
        var argCount = GetNextByte(context);
        
        return SuperSendLiteralSelector(context, selectorIndex, argCount);
    }

    #endregion

    #region Jump Operations (144-175)

    /// <summary>
    /// Short Jump: Jump iii+1 (i.e., 1 through 8).
    /// </summary>
    private bool ShortJump(MethodContext context, int jumpDistance)
    {
        context.PC += jumpDistance;
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> ShortJump distance={jumpDistance}, new PC={context.PC}");
        }
        
        return true;
    }

    /// <summary>
    /// Short Jump On False: Pop and Jump On False iii+1 (i.e., 1 through 8).
    /// </summary>
    private bool ShortJumpOnFalse(MethodContext context, int jumpDistance)
    {
        var condition = context.Stack.Pop();
        if (condition == executionContext.GetSpecialObject(SpecialObjects.FalseObject))
        {
            context.PC += jumpDistance;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> ShortJumpOnFalse taken, distance={jumpDistance}, new PC={context.PC}");
            }
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> ShortJumpOnFalse not taken, condition={condition}");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Long Jump: Jump (iii+4)*256 + next byte.
    /// </summary>
    private bool LongJump(MethodContext context, int highBits)
    {
        var lowByte = GetNextByte(context);
        var jumpDistance = (highBits * 256) + lowByte;
        
        context.PC += jumpDistance;
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> LongJump distance={jumpDistance}, new PC={context.PC}");
        }
        
        return true;
    }

    /// <summary>
    /// Long Jump On True: Pop and Jump On True ii*256 + next byte.
    /// </summary>
    private bool LongJumpOnTrue(MethodContext context, int highBits)
    {
        var lowByte = GetNextByte(context);
        var jumpDistance = (highBits * 256) + lowByte;
        
        var condition = context.Stack.Pop();
        if (condition == executionContext.GetSpecialObject(SpecialObjects.TrueObject))
        {
            context.PC += jumpDistance;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongJumpOnTrue taken, distance={jumpDistance}, new PC={context.PC}");
            }
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongJumpOnTrue not taken, condition={condition}");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Long Jump On False: Pop and Jump On False ii*256 + next byte.
    /// </summary>
    private bool LongJumpOnFalse(MethodContext context, int highBits)
    {
        var lowByte = GetNextByte(context);
        var jumpDistance = (highBits * 256) + lowByte;
        
        var condition = context.Stack.Pop();
        if (condition == executionContext.GetSpecialObject(SpecialObjects.FalseObject))
        {
            context.PC += jumpDistance;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongJumpOnFalse taken, distance={jumpDistance}, new PC={context.PC}");
            }
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongJumpOnFalse not taken, condition={condition}");
            }
        }
        
        return true;
    }

    #endregion

    #region Send Operations (176-255)

    /// <summary>
    /// Send Arithmetic Message #iiii (176-191).
    /// Common arithmetic selectors like +, -, *, /, etc.
    /// </summary>
    private bool SendArithmeticMessage(MethodContext context, int selectorIndex)
    {
        // The specific arithmetic selectors are defined in the special selectors array
        // Common mappings: 0=+, 1=-, 2=<, 3=>, 4=<=, 5=>=, 6==, 7=~=, 8=*, 9=//, 10=\\, 11=@, 12=bitShift:, 13=//, 14=bitAnd:, 15=bitOr:
        var selector = GetArithmeticSelector(selectorIndex);
        var argCount = GetArithmeticSelectorArgCount(selectorIndex);
        
        var args = context.Stack.PopN(argCount);
        var receiver = context.Stack.Pop();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            var selectorString = executionContext.GetSymbolString(selector);
            Console.WriteLine($"  -> SendArithmeticMessage #{selectorIndex} ({selectorString}) with {argCount} args");
        }
        
        executionContext.Send(receiver, selector, args);
        return true;
    }

    /// <summary>
    /// Send Special Message #iiii (192-207).
    /// Common special selectors like at:, at:put:, size, etc.
    /// </summary>
    private bool SendSpecialMessage(MethodContext context, int selectorIndex)
    {
        // The specific special selectors are defined in the special selectors array
        // Common mappings: 0=at:, 1=at:put:, 2=size, 3=next, 4=nextPut:, 5=atEnd, 6==, 7=class, 8=blockCopy:, 9=value, 10=value:, 11=do:, 12=new, 13=new:, 14=x, 15=y
        var selector = GetSpecialSelector(selectorIndex);
        var argCount = GetSpecialSelectorArgCount(selectorIndex);
        
        var args = context.Stack.PopN(argCount);
        var receiver = context.Stack.Pop();
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            var selectorString = executionContext.GetSymbolString(selector);
            Console.WriteLine($"  -> SendSpecialMessage #{selectorIndex} ({selectorString}) with {argCount} args");
        }
        
        executionContext.Send(receiver, selector, args);
        return true;
    }

    /// <summary>
    /// Send Literal Selector #iiii With No Arguments (208-223).
    /// </summary>
    private bool SendLiteralSelectorNoArgs(MethodContext context, int selectorIndex)
    {
        return SendLiteralSelector(context, selectorIndex, 0);
    }

    /// <summary>
    /// Send Literal Selector #iiii With 1 Argument (224-239).
    /// </summary>
    private bool SendLiteralSelectorOneArg(MethodContext context, int selectorIndex)
    {
        return SendLiteralSelector(context, selectorIndex, 1);
    }

    /// <summary>
    /// Send Literal Selector #iiii With 2 Arguments (240-255).
    /// </summary>
    private bool SendLiteralSelectorTwoArgs(MethodContext context, int selectorIndex)
    {
        return SendLiteralSelector(context, selectorIndex, 2);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the literal array from a compiled method.
    /// </summary>
    private OOP[] GetLiterals(OOP method)
    {
        var literalsArrayOop = executionContext.GetFieldByName(method, "literals");
        if (!literalsArrayOop.IsNil)
        {
            var literalsArray = executionContext.Memory.GetObject(literalsArrayOop);
            if (literalsArray != null)
            {
                return literalsArray.Fields;
            }
        }
        return [];
    }

    /// <summary>
    /// Gets the next byte from the bytecode stream and advances the program counter.
    /// </summary>
    private byte GetNextByte(MethodContext context)
    {
        var bytecodeArrayOop = executionContext.GetFieldByName(context.Method, "bytecode");
        if (!bytecodeArrayOop.IsNil)
        {
            var bytecodeArray = executionContext.Memory.GetObject(bytecodeArrayOop);
            if (bytecodeArray != null && context.PC < bytecodeArray.Fields.Length)
            {
                var byteValue = bytecodeArray.Fields[context.PC++];
                if (byteValue.IsSmallInteger)
                {
                    return (byte)byteValue.SmallIntegerValue;
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// Stores a value in a receiver variable without popping the stack.
    /// </summary>
    private bool StoreReceiverVariable(MethodContext context, int index, OOP value)
    {
        executionContext.SetFieldByIndex(context.Receiver, index, value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> StoreReceiverVariable #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Stores a value in a temporary variable without popping the stack.
    /// </summary>
    private bool StoreTemporaryVariable(MethodContext context, int index, OOP value)
    {
        if (index < context.Temporaries.Length)
        {
            context.Temporaries[index] = value;
        }
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            Console.WriteLine($"  -> StoreTemporaryVariable #{index} = {value}");
        }
        
        return true;
    }

    /// <summary>
    /// Stores a value in a literal variable (Association) without popping the stack.
    /// </summary>
    private bool StoreLiteralVariable(MethodContext context, int index, OOP value)
    {
        var literals = GetLiterals(context.Method);
        if (index < literals.Length)
        {
            var association = literals[index];
            executionContext.SetFieldByName(association, "value", value);
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> StoreLiteralVariable #{index} = {value}");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Pops and stores a value in a literal variable (Association).
    /// </summary>
    private bool PopAndStoreLiteralVariable(MethodContext context, int index)
    {
        var value = context.Stack.Pop();
        return StoreLiteralVariable(context, index, value);
    }

    /// <summary>
    /// Sends a literal selector with the specified number of arguments.
    /// </summary>
    private bool SendLiteralSelector(MethodContext context, int selectorIndex, int argCount)
    {
        var literals = GetLiterals(context.Method);
        if (selectorIndex < literals.Length)
        {
            var selector = literals[selectorIndex];
            var args = context.Stack.PopN(argCount);
            var receiver = context.Stack.Pop();
            
            if (DebugOptions.EnableMethodTrace)
            {
                var selectorString = executionContext.GetSymbolString(selector);
                var receiverClass = executionContext.GetClassOfObject(receiver);
                var receiverClassName = executionContext.GetClassName(receiverClass);
                Console.WriteLine($"[TRACE] {receiverClassName}>>{selectorString} (args: {argCount})");
            }
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> SendLiteralSelector[{selectorIndex}]={selector} with {argCount} args");
            }
            
            executionContext.Send(receiver, selector, args);
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> SendLiteralSelector[{selectorIndex}] out of range");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Sends a literal selector to the superclass with the specified number of arguments.
    /// </summary>
    private bool SuperSendLiteralSelector(MethodContext context, int selectorIndex, int argCount)
    {
        var literals = GetLiterals(context.Method);
        if (selectorIndex < literals.Length)
        {
            var selector = literals[selectorIndex];
            var args = context.Stack.PopN(argCount);
            var receiver = context.Stack.Pop();
            
            if (DebugOptions.EnableMethodTrace)
            {
                var selectorString = executionContext.GetSymbolString(selector);
                var receiverClass = executionContext.GetClassOfObject(receiver);
                var receiverClassName = executionContext.GetClassName(receiverClass);
                Console.WriteLine($"[TRACE] super {receiverClassName}>>{selectorString} (args: {argCount})");
            }
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> SuperSendLiteralSelector[{selectorIndex}]={selector} with {argCount} args");
            }
            
            executionContext.SuperSend(receiver, selector, args);
        }
        
        return true;
    }

    /// <summary>
    /// Gets an arithmetic selector by index.
    /// </summary>
    private OOP GetArithmeticSelector(int index)
    {
        // These are the standard arithmetic selectors in Smalltalk-80
        var arithmeticSelectors = new[] {
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
        };
        
        if (index < arithmeticSelectors.Length)
        {
            return executionContext.InternSymbol(arithmeticSelectors[index]);
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Gets the argument count for an arithmetic selector.
    /// </summary>
    private int GetArithmeticSelectorArgCount(int index)
    {
        // Most arithmetic selectors take 1 argument
        // Exceptions: @ takes 1 (creates a Point)
        return index switch
        {
            12 => 1, // bitShift:
            14 => 1, // bitAnd:
            15 => 1, // bitOr:
            _ => 1   // All others take 1 argument
        };
    }

    /// <summary>
    /// Gets a special selector by index.
    /// </summary>
    private OOP GetSpecialSelector(int index)
    {
        // These are the standard special selectors in Smalltalk-80
        var specialSelectors = new[] {
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        };
        
        if (index < specialSelectors.Length)
        {
            return executionContext.InternSymbol(specialSelectors[index]);
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Gets the argument count for a special selector.
    /// </summary>
    private int GetSpecialSelectorArgCount(int index)
    {
        return index switch
        {
            0 => 1,  // at:
            1 => 2,  // at:put:
            2 => 0,  // size
            3 => 0,  // next
            4 => 1,  // nextPut:
            5 => 0,  // atEnd
            6 => 1,  // ==
            7 => 0,  // class
            8 => 1,  // blockCopy:
            9 => 0,  // value
            10 => 1, // value:
            11 => 1, // do:
            12 => 0, // new
            13 => 1, // new:
            14 => 0, // x
            15 => 0, // y
            _ => 0
        };
    }

    #endregion
}

/// <summary>
/// Context for bytecode execution, providing VM services to the bytecode dispatcher.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Gets the object memory.
    /// </summary>
    ObjectMemory Memory { get; }
    
    /// <summary>
    /// Gets a special object by index.
    /// </summary>
    /// <param name="index">The special object index.</param>
    /// <returns>The special object OOP.</returns>
    OOP GetSpecialObject(int index);
    
    /// <summary>
    /// Returns from the current context.
    /// </summary>
    /// <param name="context">The context to return from.</param>
    /// <param name="value">The return value.</param>
    void Return(MethodContext context, OOP value);
    
    /// <summary>
    /// Returns from a block context to its home context.
    /// </summary>
    /// <param name="context">The block context.</param>
    /// <param name="value">The return value.</param>
    void ReturnFromBlock(MethodContext context, OOP value);
    
    /// <summary>
    /// Creates a context object for reification.
    /// </summary>
    /// <param name="context">The method context to reify.</param>
    /// <returns>The context object OOP.</returns>
    OOP CreateContextObject(MethodContext context);
    
    /// <summary>
    /// Creates a block context for block closure creation.
    /// </summary>
    /// <param name="homeContext">The home method context.</param>
    /// <param name="blockIndex">The block literal index.</param>
    /// <returns>The block context OOP.</returns>
    OOP CreateBlockContext(MethodContext homeContext, int blockIndex);
    
    /// <summary>
    /// Performs a message send.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    void Send(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Performs a super send.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    void SuperSend(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Interns a symbol string.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>The interned symbol OOP.</returns>
    OOP InternSymbol(string value);
    
    /// <summary>
    /// Gets a field value from an object by index.
    /// </summary>
    /// <param name="objectOop">The object OOP.</param>
    /// <param name="index">The field index.</param>
    /// <returns>The field value.</returns>
    OOP GetFieldByIndex(OOP objectOop, int index);
    
    /// <summary>
    /// Sets a field value in an object by index.
    /// </summary>
    /// <param name="objectOop">The object OOP.</param>
    /// <param name="index">The field index.</param>
    /// <param name="value">The new field value.</param>
    void SetFieldByIndex(OOP objectOop, int index, OOP value);
    
    /// <summary>
    /// Gets a field value from an object by name.
    /// </summary>
    /// <param name="objectOop">The object OOP.</param>
    /// <param name="fieldName">The field name.</param>
    /// <returns>The field value.</returns>
    OOP GetFieldByName(OOP objectOop, string fieldName);
    
    /// <summary>
    /// Sets a field value in an object by name.
    /// </summary>
    /// <param name="objectOop">The object OOP.</param>
    /// <param name="fieldName">The field name.</param>
    /// <param name="value">The new field value.</param>
    void SetFieldByName(OOP objectOop, string fieldName, OOP value);
    
    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    /// <param name="objectOop">The object OOP.</param>
    /// <returns>The class OOP.</returns>
    OOP GetClassOfObject(OOP objectOop);
    
    /// <summary>
    /// Gets the name of a class.
    /// </summary>
    /// <param name="classOop">The class OOP.</param>
    /// <returns>The class name.</returns>
    string GetClassName(OOP classOop);
    
    /// <summary>
    /// Gets a string representation of a symbol.
    /// </summary>
    /// <param name="symbolOop">The symbol OOP.</param>
    /// <returns>The symbol string.</returns>
    string GetSymbolString(OOP symbolOop);
}