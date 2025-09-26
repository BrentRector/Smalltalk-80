using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Dispatches and executes Smalltalk-80 bytecodes according to the Blue Book specification.
/// Implements the complete bytecode set as defined in Chapter 28 of the Blue Book (1983).
/// </summary>
/// <remarks>
/// Performance optimizations:
/// - Uses ReadOnlySpan&lt;T&gt; for literal arrays to reduce bounds checking
/// - Caches frequently accessed special objects (true, false)
/// - Pre-allocates static arrays for selector names
/// - Uses stackalloc for small temporary buffers in jump operations
/// - Aggressive inlining on hot path methods
/// - Optimized switch dispatch with range checks
/// - Minimal allocations in bytecode execution path
/// </remarks>
public sealed class BytecodeDispatcher
{
    private readonly IExecutionContext executionContext;
    private readonly ILogger<BytecodeDispatcher>? logger;
    
    // Cache frequently used special objects to avoid repeated lookups
    // These are lazily initialized on first use since they may not exist during VM initialization
    private OOP cachedTrueObject;
    private OOP cachedFalseObject;
    private bool specialObjectsCached = false;
    
    // Pre-allocated arrays for common selector names to avoid allocations
    private static readonly string[] ArithmeticSelectors = [
        "+", "-", "<", ">", "<=", ">=", "=", "~=",
        "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
    ];
    
    private static readonly string[] SpecialSelectors = [
        "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
        "==", "class", "blockCopy:", "value", "value:", "do:",
        "new", "new:", "x", "y"
    ];
    
    // Pre-computed argument counts for special selectors
    private static readonly byte[] SpecialSelectorArgCounts = [
        1, 2, 0, 0, 1, 0, 1, 0, 1, 0, 1, 1, 0, 1, 0, 0
    ];
    
    /// <summary>
    /// Creates a new bytecode dispatcher.
    /// </summary>
    /// <param name="context">The execution context for VM services.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public BytecodeDispatcher(IExecutionContext context, ILogger<BytecodeDispatcher>? logger = null)
    {
        executionContext = context ?? throw new ArgumentNullException(nameof(context));
        this.logger = logger;
        
        // Special objects will be cached on first use (lazy initialization)
        // This is necessary because during VM initialization, special objects may not exist yet
    }
    
    /// <summary>
    /// Ensures special objects are cached for performance.
    /// Called lazily on first use to avoid issues during VM initialization.
    /// </summary>
    private void EnsureSpecialObjectsCached()
    {
        if (!specialObjectsCached)
        {
            cachedTrueObject = executionContext.GetSpecialObject(SpecialObjects.TrueObject);
            cachedFalseObject = executionContext.GetSpecialObject(SpecialObjects.FalseObject);
            specialObjectsCached = true;
        }
    }

    /// <summary>
    /// Executes a single bytecode instruction according to Blue Book specification.
    /// Optimized with aggressive inlining and cached values.
    /// </summary>
    /// <param name="context">The method context in which to execute.</param>
    /// <param name="bytecode">The bytecode instruction to execute.</param>
    /// <returns>True to continue execution, false to stop (e.g., return).</returns>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExecuteBytecode(MethodContext context, byte bytecode)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        // Trace bytecode execution if enabled
        if (DebugOptions.EnableBytecodeTrace)
        {
            logger?.LogTrace("[BYTECODE] PC={PC}, Bytecode={Bytecode:X2} ({BytecodeDecimal}), Stack depth={StackDepth}",
                context.PC - 1, bytecode, bytecode, context.Stack.Count);
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
            113 => PushTrueObject(context),
            114 => PushFalseObject(context),
            115 => PushConstant(context, OOP.Nil),
            116 => PushConstant(context, OOP.FromSmallInteger(-1)),
            117 => PushConstant(context, OOP.FromSmallInteger(0)),
            118 => PushConstant(context, OOP.FromSmallInteger(1)),
            119 => PushConstant(context, OOP.FromSmallInteger(2)),
            
            // 120-123: Return (receiver, true, false, nil) From Message
            120 => ReturnReceiver(context),
            121 => ReturnTrueObject(context),
            122 => ReturnFalseObject(context),
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
            
            // 160: Medium Jump NN + 1 where NN is next byte (range: 1-256)
            160 => UnconditionalJump(context, bytecode),
            
            // 161-167: Long Jump with N operand bytes where N = bytecode - 159 (N = 2-8)
            >= 161 and <= 167 => UnconditionalJump(context, bytecode),
            
            // 168: Medium Conditional Jump On False (1 operand byte)
            168 => MediumConditionalJumpOnFalse(context),
            
            // 169-175: Long Conditional Jump On False with N operand bytes where N = bytecode - 168
            >= 169 and <= 175 => LongConditionalJumpOnFalse(context, bytecode),
            
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool PushTemporaryVariable(MethodContext context, int index)
    {
        var value = index < context.Temporaries.Length ? context.Temporaries[index] : OOP.Nil;
        context.Stack.Push(value);
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            logger?.LogTrace("  -> PushTemporaryVariable #{Index} = {Value}", index, value);
            
            // Warn about nil values only in trace mode
            if (value.IsNil && index >= context.Temporaries.Length)
            {
                logger?.LogWarning("PushTemporaryVariable: Index {Index} is out of bounds (Temporaries length = {Length})",
                    index, context.Temporaries.Length);
            }
        }
        
        return true;
    }

    /// <summary>
    /// Pushes the receiver onto the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Optimized with Span-based literal access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
            {
                Console.WriteLine($"[DEBUG] PushLiteralVariable: Association OOP = {association}");
                
                var assocObj = executionContext.Memory.GetObject(association);
                if (assocObj != null && assocObj.Fields.Length >= 2)
                {
                    var keyField = assocObj.Fields[0];
                    var valueField = assocObj.Fields[1];
                    var keyStr = executionContext.GetSymbolString(keyField);
                    
                    // Comprehensive debug output
                    Console.WriteLine($"[DEBUG] Association Details:");
                    Console.WriteLine($"  - OOP: {association}");
                    Console.WriteLine($"  - Key field (index 0): {keyField}");
                    Console.WriteLine($"  - Key string: '{keyStr}'");
                    Console.WriteLine($"  - Value field (index 1): {valueField}");
                    Console.WriteLine($"  - Object class: {assocObj.Header.ClassOOP}");
                    Console.WriteLine($"  - Field count: {assocObj.Fields.Length}");
                    
                    // Special detailed trace for problem globals
                    if (keyStr == "Cursor" || keyStr == "ThumbsUp" || keyStr == "ThumbsDown" || 
                        keyStr == "BinaryChoiceView" || keyStr == "Normal" || keyStr == "Origin")
                    {
                        Console.WriteLine($"[DEBUG] *** PROBLEM GLOBAL DETECTED: {keyStr} ***");
                        Console.WriteLine($"[DEBUG] Association memory address: {association.RawValue}");
                        Console.WriteLine($"[DEBUG] Expected value should be a class OOP, got: {valueField}");
                        
                        if (valueField.IsNil)
                        {
                            Console.WriteLine($"[DEBUG] ERROR: Association for {keyStr} has NIL value!");
                            Console.WriteLine($"[DEBUG] This Association was likely created during method installation");
                            Console.WriteLine($"[DEBUG] Something has cleared or failed to set the value field");
                            
                            // Try to dump all fields for extra debugging
                            Console.WriteLine($"[DEBUG] All Association fields:");
                            for (int i = 0; i < assocObj.Fields.Length; i++)
                            {
                                Console.WriteLine($"[DEBUG]   Field[{i}] = {assocObj.Fields[i]}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Value field is NOT nil: {valueField}");
                            // Check if it's a valid class OOP
                            var valueObj = executionContext.Memory.GetObject(valueField);
                            if (valueObj != null)
                            {
                                Console.WriteLine($"[DEBUG] Value object class: {valueObj.Header.ClassOOP}");
                            }
                        }
                    }
                    
                    if (DebugOptions.EnableBytecodeTrace)
                    {
                        Console.WriteLine($"  -> PushLiteralVariable #{index}: Association key='{keyStr}', value={valueField}");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG] WARNING: Association object is invalid!");
                    Console.WriteLine($"[DEBUG]   Association OOP: {association}");
                    if (assocObj == null)
                    {
                        Console.WriteLine($"[DEBUG]   Object is NULL in memory!");
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG]   Object has {assocObj.Fields.Length} fields (expected >= 2)");
                    }
                }
            }
            
            var value = executionContext.GetFieldByName(association, "value");
            
            // Debug: Check if value is valid after retrieval
            if (DebugOptions.EnableDebugOutput)
            {
                var assocObj = executionContext.Memory.GetObject(association);
                if (assocObj != null && assocObj.Fields.Length >= 2)
                {
                    var keyField = assocObj.Fields[0];
                    var keyStr = executionContext.GetSymbolString(keyField);
                    
                    // Check problem globals
                    if (keyStr == "Cursor" || keyStr == "ThumbsUp" || keyStr == "ThumbsDown" || 
                        keyStr == "BinaryChoiceView" || keyStr == "Normal" || keyStr == "Origin")
                    {
                        Console.WriteLine($"[DEBUG] After GetFieldByName for {keyStr}:");
                        Console.WriteLine($"[DEBUG]   Retrieved value: {value}");
                        Console.WriteLine($"[DEBUG]   Direct field access: {assocObj.Fields[1]}");
                        
                        if (value.IsNil)
                        {
                            Console.WriteLine($"[DEBUG] CRITICAL ERROR: {keyStr} Association has nil value!");
                            Console.WriteLine($"[DEBUG]   Association OOP: {association}");
                            Console.WriteLine($"[DEBUG]   This is the same Association? Let's verify:");
                            
                            // Double-check by direct field access
                            var directValue = assocObj.Fields[1];
                            Console.WriteLine($"[DEBUG]   Direct field[1] access gives: {directValue}");
                            
                            if (!directValue.IsNil)
                            {
                                Console.WriteLine($"[DEBUG] INCONSISTENCY: GetFieldByName returned nil but direct access shows {directValue}!");
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG] Both GetFieldByName and direct access show nil value");
                                Console.WriteLine($"[DEBUG] The Association was created with value but it's been cleared!");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] SUCCESS: {keyStr} has non-nil value: {value}");
                        }
                    }
                }
                
                // General nil value warning
                if (value.IsNil)
                {
                    Console.WriteLine($"[DEBUG] WARNING: PushLiteralVariable #{index} pushing nil value!");
                }
            }
            
            context.Stack.Push(value);
            
            if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
            {
                if (DebugOptions.EnableBytecodeTrace)
                {
                    Console.WriteLine($"  -> Pushed value: {value}");
                    // Extra check for PC 12-15 range
                    if (context.PC >= 13 && context.PC <= 16)
                    {
                        Console.WriteLine($"[STACK] After push at PC={context.PC - 1}: Stack top = {context.Stack.Top}, depth = {context.Stack.Count}");
                    }
                }
                
                // Final verification for problem globals
                if (DebugOptions.EnableDebugOutput)
                {
                    var assocObj = executionContext.Memory.GetObject(association);
                    if (assocObj != null && assocObj.Fields.Length >= 2)
                    {
                        var keyStr = executionContext.GetSymbolString(assocObj.Fields[0]);
                        if (keyStr == "Cursor" || keyStr == "ThumbsUp" || keyStr == "ThumbsDown" || 
                            keyStr == "BinaryChoiceView" || keyStr == "Normal" || keyStr == "Origin")
                        {
                            Console.WriteLine($"[DEBUG] FINAL: Pushed {keyStr} value: {value} to stack");
                            if (value.IsNil)
                            {
                                Console.WriteLine($"[DEBUG] ERROR: Pushed NIL for {keyStr} - this will cause a MNU error!");
                            }
                        }
                    }
                }
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    /// Optimized with cached false object comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShortJumpOnFalse(MethodContext context, int jumpDistance)
    {
        var condition = context.Stack.Pop();
        EnsureSpecialObjectsCached();
        if (condition == cachedFalseObject)
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
    /// Unconditional Jump: Handles both medium jumps (bytecode 160) and long jumps (bytecodes 161-167).
    /// Implements the exact Blue Book specification for unconditional jump bytecodes.
    /// Optimized with stackalloc for small byte arrays.
    /// </summary>
    /// <remarks>
    /// According to the Blue Book specification:
    /// 
    /// - Bytecode 160: Medium Jump
    ///   - Has 1 operand byte NN
    ///   - Jumps NN + 1 bytes forward
    ///   - Range: 1-256 bytes (NN = 0-255, jump = 1-256)
    /// 
    /// - Bytecodes 161-167: Long Jumps
    ///   - N = bytecode - 159 gives the number of operand bytes (2-8 bytes)
    ///   - Read N bytes as big-endian unsigned integer NN
    ///   - Jump NN + 1 bytes forward
    ///   - Range depends on N:
    ///     - 161 (N=2): 1-65,536 bytes
    ///     - 162 (N=3): 1-16,777,216 bytes
    ///     - ... up to ...
    ///     - 167 (N=8): 1-2^64 bytes (theoretical)
    /// 
    /// Note: Short jumps (144-151) jump 1-8 bytes and are handled separately.
    /// </remarks>
    private bool UnconditionalJump(MethodContext context, byte bytecode)
    {
        int jumpDistance;
        
        if (bytecode == 160)
        {
            // Medium jump: Read 1 byte NN and jump NN + 1
            var nn = GetNextByte(context);
            jumpDistance = nn + 1;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> MediumJump (bytecode 160) NN={nn}, distance={jumpDistance}, new PC={context.PC + jumpDistance}");
            }
        }
        else
        {
            // Long jump: bytecodes 161-167
            // N = bytecode - 159 gives number of operand bytes (2-8)
            int n = bytecode - 159;
            
            // Use stackalloc for small byte buffer to avoid allocations
            Span<byte> buffer = stackalloc byte[8];
            
            // Read N bytes into buffer
            for (int i = 0; i < n; i++)
            {
                buffer[i] = GetNextByte(context);
            }
            
            // Convert big-endian bytes to integer
            uint nn = 0;
            for (int i = 0; i < n; i++)
            {
                nn = (nn << 8) | buffer[i];
            }
            
            // Jump NN + 1 bytes
            jumpDistance = (int)(nn + 1);
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongJump (bytecode {bytecode}) N={n} operand bytes, NN={nn}, distance={jumpDistance}, new PC={context.PC + jumpDistance}");
            }
        }
        
        context.PC += jumpDistance;
        return true;
    }

    /// <summary>
    /// Medium Conditional Jump On False: Pop and Jump On False with 1 operand byte.
    /// Blue Book specification: Bytecode 168 with nextByte + 1 jump distance (1-256).
    /// Optimized with cached false object.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MediumConditionalJumpOnFalse(MethodContext context)
    {
        var nextByte = GetNextByte(context);
        var jumpDistance = nextByte + 1;  // Offset = nextByte + 1
        
        var condition = context.Stack.Pop();
        EnsureSpecialObjectsCached();
        if (condition == cachedFalseObject)
        {
            context.PC += jumpDistance;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> MediumConditionalJumpOnFalse taken, distance={jumpDistance}, new PC={context.PC}");
            }
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> MediumConditionalJumpOnFalse not taken, condition={condition}");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Long Conditional Jump On False: Pop and Jump On False with variable-length operand.
    /// Blue Book specification: Bytecodes 169-175 use N operand bytes where N = bytecode - 168.
    /// Optimized with cached false object and stackalloc for small buffers.
    /// </summary>
    private bool LongConditionalJumpOnFalse(MethodContext context, int bytecode)
    {
        // Calculate number of operand bytes: N = bytecode - 168
        // Bytecode 169 = 1 byte, 170 = 2 bytes, ..., 175 = 7 bytes
        // But wait, 169-168=1, which means 169 uses 1 operand byte
        // Actually for long jumps, 169 should use 2 bytes minimum
        // Let me correct: For 169-175, N = bytecode - 167 gives 2-8 bytes
        int operandBytes = bytecode - 167;  // Gives 2-8 bytes for 169-175
        
        // Read N bytes as big-endian unsigned integer
        int jumpDistance = 0;
        for (int i = 0; i < operandBytes; i++)
        {
            jumpDistance = (jumpDistance << 8) | GetNextByte(context);
        }
        
        // Add 1 to get actual offset (Blue Book: offset = NN + 1)
        jumpDistance += 1;
        
        var condition = context.Stack.Pop();
        EnsureSpecialObjectsCached();
        if (condition == cachedFalseObject)
        {
            context.PC += jumpDistance;
            
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongConditionalJumpOnFalse (bytecode {bytecode}) taken, distance={jumpDistance}, new PC={context.PC}");
            }
        }
        else
        {
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  -> LongConditionalJumpOnFalse (bytecode {bytecode}) not taken, condition={condition}");
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
        
        // Use SendWithResult to get the return value and push it onto the stack
        var result = executionContext.SendWithResult(receiver, selector, args);
        context.Stack.Push(result);
        
        if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
        {
            Console.WriteLine($"[DEBUG] Pushed send result: {result}");
        }
        
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
        
        // Enhanced debugging for new: special message
        if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
        {
            if (selectorIndex == 13) // new:
            {
                Console.WriteLine($"[DEBUG] About to send new: - Stack contents (depth={context.Stack.Count}):");
                // We can't iterate the stack directly, but we know what should be there
                Console.WriteLine($"  - Will pop {argCount} arguments, then receiver");
            }
        }
        
        var args = context.Stack.PopN(argCount);
        var receiver = context.Stack.Pop();
        
        // Debug what we popped for new:
        if ((DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput) && selectorIndex == 13)
        {
            Console.WriteLine($"[DEBUG] Popped for new: send:");
            Console.WriteLine($"  - Receiver: {receiver}");
            if (args.Length > 0)
                Console.WriteLine($"  - Arg[0]: {args[0]}");
        }
        
        if (DebugOptions.EnableBytecodeTrace)
        {
            var selectorString = executionContext.GetSymbolString(selector);
            Console.WriteLine($"  -> SendSpecialMessage #{selectorIndex} ({selectorString}) with {argCount} args");
        }
        
        // Use SendWithResult to get the return value and push it onto the stack
        var result = executionContext.SendWithResult(receiver, selector, args);
        context.Stack.Push(result);
        
        if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
        {
            Console.WriteLine($"[DEBUG] Pushed send result: {result}");
        }
        
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
    /// Optimized to return a ReadOnlySpan for better performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<OOP> GetLiterals(OOP method)
    {
        var literalsArrayOop = executionContext.GetFieldByName(method, "literals");
        if (!literalsArrayOop.IsNil)
        {
            var literalsArray = executionContext.Memory.GetObject(literalsArrayOop);
            if (literalsArray != null)
            {
                return literalsArray.Fields.AsSpan();
            }
        }
        return ReadOnlySpan<OOP>.Empty;
    }

    /// <summary>
    /// Gets the next byte from the bytecode stream and advances the program counter.
    /// Optimized with aggressive inlining and minimal bounds checking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            
            // Enhanced debugging for stack state
            if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
            {
                Console.WriteLine($"[DEBUG] SendLiteralSelector: Stack count before pop = {context.Stack.Count}, need {argCount + 1} items");
                Console.WriteLine($"  - Selector index: {selectorIndex}");
                Console.WriteLine($"  - Arg count: {argCount}");
            }
            
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
            
            // Send the message
            // The return mechanism in the called method will push the result to our stack
            // But we need to track the depth to ensure it happened
            var depthBefore = context.Stack.Count;
            var result = executionContext.SendWithResult(receiver, selector, args);
            
            // Check if the return pushed to our stack
            if (context.Stack.Count == depthBefore)
            {
                // No push happened (e.g., primitive or method without proper return)
                // Push the result ourselves
                context.Stack.Push(result);
                if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
                {
                    Console.WriteLine($"[DEBUG] Pushed send result (no auto-push): {result}, stack depth = {context.Stack.Count}");
                }
            }
            else
            {
                if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
                {
                    Console.WriteLine($"[DEBUG] Return already pushed result: {result}, stack depth = {context.Stack.Count}");
                }
            }
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
            
            // Use SuperSendWithResult to get the return value and push it onto the stack
            var result = executionContext.SuperSendWithResult(receiver, selector, args);
            context.Stack.Push(result);
            
            if (DebugOptions.EnableBytecodeTrace || DebugOptions.EnableDebugOutput)
            {
                Console.WriteLine($"[DEBUG] Pushed super send result: {result}");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Gets an arithmetic selector by index.
    /// Optimized with pre-cached static array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OOP GetArithmeticSelector(int index)
    {
        if ((uint)index < (uint)ArithmeticSelectors.Length)
        {
            return executionContext.InternSymbol(ArithmeticSelectors[index]);
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Gets the argument count for an arithmetic selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetArithmeticSelectorArgCount(int index)
    {
        // Most arithmetic selectors take 1 argument
        // All arithmetic selectors in Smalltalk-80 take exactly 1 argument
        return 1;
    }

    /// <summary>
    /// Gets a special selector by index.
    /// Optimized with pre-cached static array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OOP GetSpecialSelector(int index)
    {
        if ((uint)index < (uint)SpecialSelectors.Length)
        {
            return executionContext.InternSymbol(SpecialSelectors[index]);
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Gets the argument count for a special selector.
    /// Optimized with pre-cached static array lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSpecialSelectorArgCount(int index)
    {
        return (uint)index < (uint)SpecialSelectorArgCounts.Length 
            ? SpecialSelectorArgCounts[index] 
            : 0;
    }

    #endregion
    
    #region Special Object Helper Methods
    
    /// <summary>
    /// Pushes the cached true object onto the stack, ensuring special objects are cached.
    /// </summary>
    private bool PushTrueObject(MethodContext context)
    {
        EnsureSpecialObjectsCached();
        return PushConstant(context, cachedTrueObject);
    }
    
    /// <summary>
    /// Pushes the cached false object onto the stack, ensuring special objects are cached.
    /// </summary>
    private bool PushFalseObject(MethodContext context)
    {
        EnsureSpecialObjectsCached();
        return PushConstant(context, cachedFalseObject);
    }
    
    /// <summary>
    /// Returns the cached true object, ensuring special objects are cached.
    /// </summary>
    private bool ReturnTrueObject(MethodContext context)
    {
        EnsureSpecialObjectsCached();
        return ReturnConstant(context, cachedTrueObject);
    }
    
    /// <summary>
    /// Returns the cached false object, ensuring special objects are cached.
    /// </summary>
    private bool ReturnFalseObject(MethodContext context)
    {
        EnsureSpecialObjectsCached();
        return ReturnConstant(context, cachedFalseObject);
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
    /// Performs a message send and returns the result.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    OOP SendWithResult(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Performs a super send.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    void SuperSend(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Performs a super send and returns the result.
    /// </summary>
    /// <param name="receiver">The message receiver.</param>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the super send.</returns>
    OOP SuperSendWithResult(OOP receiver, OOP selector, OOP[] arguments);
    
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