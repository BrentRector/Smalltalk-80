using System;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Dispatches and executes Smalltalk-80 bytecodes.
/// Based on Blue Book bytecode specification.
/// </summary>
public sealed class BytecodeDispatcher
{
    private readonly IExecutionContext executionContext;
    
    // Special selector table for bytecodes 224-255
    private static readonly (string selector, int argCount)[] SpecialSelectors =
    [
        // Arithmetic selectors 0-15
        ("+", 1), ("-", 1), ("<", 1), (">", 1),
        ("<=", 1), (">=", 1), ("=", 1), ("~=", 1),
        ("*", 1), ("/", 1), ("\\", 1), ("@", 1),
        ("bitShift:", 1), ("//", 1), ("bitAnd:", 1), ("bitOr:", 1),
        // Common selectors 16-31
        ("at:", 1), ("at:put:", 2), ("size", 0), ("next", 0),
        ("nextPut:", 1), ("atEnd", 0), ("==", 1), ("class", 0),
        ("blockCopy:", 1), ("value", 0), ("value:", 1), ("do:", 1),
        ("new", 0), ("new:", 1), ("x", 0), ("y", 0)
    ];

    /// <summary>
    /// Creates a new bytecode dispatcher.
    /// </summary>
    /// <param name="context">The execution context for VM services.</param>
    public BytecodeDispatcher(IExecutionContext context)
    {
        executionContext = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// </summary>
    /// <returns>True to continue execution, false to stop (e.g., return).</returns>
    public bool ExecuteBytecode(MethodContext context, byte bytecode)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        // Dispatch based on bytecode ranges (Blue Book specification)
        return bytecode switch
        {
            // Stack bytecodes
            <= 15 => PushReceiverVariable(context, bytecode),
            <= 31 => PushTemporaryVariable(context, bytecode - 16),
            <= 63 => PushLiteralConstant(context, bytecode - 32),
            <= 95 => PushLiteralVariable(context, bytecode - 64),
            <= 103 => StoreAndPopReceiverVariable(context, bytecode - 96),
            <= 111 => StoreAndPopTemporaryVariable(context, bytecode - 104),
            
            // Push special constants
            112 => PushReceiver(context),
            113 => PushConstant(context, executionContext.GetSpecialObject(SpecialObjects.TrueObject)),
            114 => PushConstant(context, executionContext.GetSpecialObject(SpecialObjects.FalseObject)),
            115 => PushConstant(context, OOP.Nil),
            116 => PushConstant(context, OOP.FromSmallInteger(-1)),
            117 => PushConstant(context, OOP.FromSmallInteger(0)),
            118 => PushConstant(context, OOP.FromSmallInteger(1)),
            119 => PushConstant(context, OOP.FromSmallInteger(2)),
            
            // Return bytecodes
            120 => ReturnTop(context),
            121 => ReturnConstant(context, OOP.Nil),
            122 => ReturnConstant(context, executionContext.GetSpecialObject(SpecialObjects.TrueObject)),
            123 => ReturnConstant(context, executionContext.GetSpecialObject(SpecialObjects.FalseObject)),
            124 => ReturnReceiver(context),
            125 => ReturnTopFromBlock(context),
            126 or 127 => BlockReturn(context), // Extended return
            
            // Extended bytecodes
            128 => ExecuteExtendedPush(context),
            129 => ExecuteExtendedStore(context),
            130 => ExecuteExtendedStoreAndPop(context),
            131 => ExecuteExtendedSend(context),
            132 => ExecuteDoubleExtended(context),
            133 => ExecuteExtendedSuperSend(context),
            
            // Stack operations
            134 => PopStackTop(context),
            135 => DuplicateTop(context),
            136 => PushActiveContext(context),
            137 => true, // Unused in Blue Book
            
            // Jump bytecodes
            >= 138 and <= 143 => true, // Unused in Blue Book
            >= 144 and <= 151 => ShortConditionalJump(context, bytecode - 144 + 1),
            >= 152 and <= 159 => ShortUnconditionalJump(context, bytecode - 152 + 1),
            >= 160 and <= 167 => LongJumpIfTrue(context, bytecode - 160),
            >= 168 and <= 171 => LongJumpIfFalse(context, bytecode - 168),
            >= 172 and <= 175 => LongUnconditionalJump(context, bytecode - 172),
            
            // Send bytecodes
            >= 176 and <= 191 => SendLiteralSelector(context, bytecode - 176, 0),
            >= 192 and <= 207 => SendLiteralSelector(context, bytecode - 192, 1),
            >= 208 and <= 223 => SendLiteralSelector(context, bytecode - 208, 2),
            >= 224 => SendSpecialSelector(context, bytecode - 224),
        };
    }

    #region Stack Operations

    private bool PushReceiverVariable(MethodContext context, int index)
    {
        var value = executionContext.GetFieldByIndex(context.Receiver, index);
        context.Stack.Push(value);
        return true;
    }

    private bool PushTemporaryVariable(MethodContext context, int index)
    {
        if (index < context.Temporaries.Length)
        {
            context.Stack.Push(context.Temporaries[index]);
        }
        else
        {
            context.Stack.Push(OOP.Nil);
        }
        return true;
    }

    private bool PushLiteralConstant(MethodContext context, int index)
    {
        var literals = GetLiterals(context.Method);
        if (index < literals.Length)
        {
            context.Stack.Push(literals[index]);
        }
        else
        {
            context.Stack.Push(OOP.Nil);
        }
        return true;
    }

    private bool PushLiteralVariable(MethodContext context, int index)
    {
        var literals = GetLiterals(context.Method);
        if (index < literals.Length)
        {
            // For Association objects, push the value field
            var value = executionContext.GetFieldByName(literals[index], "value");
            context.Stack.Push(value);
        }
        else
        {
            context.Stack.Push(OOP.Nil);
        }
        return true;
    }

    private bool StoreAndPopReceiverVariable(MethodContext context, int index)
    {
        var value = context.Stack.Pop();
        executionContext.SetFieldByIndex(context.Receiver, index, value);
        return true;
    }

    private bool StoreAndPopTemporaryVariable(MethodContext context, int index)
    {
        var value = context.Stack.Pop();
        if (index < context.Temporaries.Length)
        {
            context.Temporaries[index] = value;
        }
        return true;
    }

    private bool PushReceiver(MethodContext context)
    {
        context.Stack.Push(context.Receiver);
        return true;
    }

    private bool PushConstant(MethodContext context, OOP constant)
    {
        context.Stack.Push(constant);
        return true;
    }

    private bool PopStackTop(MethodContext context)
    {
        context.Stack.Pop();
        return true;
    }

    private bool DuplicateTop(MethodContext context)
    {
        context.Stack.Duplicate();
        return true;
    }

    private bool PushActiveContext(MethodContext context)
    {
        var contextOop = executionContext.CreateContextObject(context);
        context.Stack.Push(contextOop);
        return true;
    }

    #endregion

    #region Return Operations

    private bool ReturnTop(MethodContext context)
    {
        var returnValue = context.Stack.Pop();
        executionContext.Return(context, returnValue);
        return false;
    }

    private bool ReturnConstant(MethodContext context, OOP constant)
    {
        executionContext.Return(context, constant);
        return false;
    }

    private bool ReturnReceiver(MethodContext context)
    {
        executionContext.Return(context, context.Receiver);
        return false;
    }

    private bool ReturnTopFromBlock(MethodContext context)
    {
        var returnValue = context.Stack.Pop();
        executionContext.ReturnFromBlock(context, returnValue);
        return false;
    }

    private bool BlockReturn(MethodContext context)
    {
        var returnValue = context.Stack.Pop();
        executionContext.ReturnFromBlock(context, returnValue);
        return false;
    }

    #endregion

    #region Jump Operations

    private bool ShortConditionalJump(MethodContext context, int offset)
    {
        var value = context.Stack.Pop();
        if (value == executionContext.GetSpecialObject(SpecialObjects.FalseObject))
        {
            context.PC += offset;
        }
        return true;
    }

    private bool ShortUnconditionalJump(MethodContext context, int offset)
    {
        context.PC += offset;
        return true;
    }

    private bool LongJumpIfTrue(MethodContext context, int highBits)
    {
        byte nextByte = GetNextByte(context);
        int offset = (highBits << 8) | nextByte;
        var value = context.Stack.Pop();
        
        if (value == executionContext.GetSpecialObject(SpecialObjects.TrueObject))
        {
            context.PC += offset;
        }
        return true;
    }

    private bool LongJumpIfFalse(MethodContext context, int highBits)
    {
        byte nextByte = GetNextByte(context);
        int offset = (highBits << 8) | nextByte;
        var value = context.Stack.Pop();
        
        if (value == executionContext.GetSpecialObject(SpecialObjects.FalseObject))
        {
            context.PC += offset;
        }
        return true;
    }

    private bool LongUnconditionalJump(MethodContext context, int highBits)
    {
        byte nextByte = GetNextByte(context);
        int offset = (highBits << 8) | nextByte;
        context.PC += offset;
        return true;
    }

    #endregion

    #region Send Operations

    private bool SendLiteralSelector(MethodContext context, int literal, int argCount)
    {
        var literals = GetLiterals(context.Method);
        if (literal < literals.Length)
        {
            var selector = literals[literal];
            var args = context.Stack.PopN(argCount);
            var receiver = context.Stack.Pop();
            
            executionContext.Send(receiver, selector, args);
        }
        return true;
    }

    private bool SendSpecialSelector(MethodContext context, int selectorIndex)
    {
        if (selectorIndex < SpecialSelectors.Length)
        {
            var (selectorName, argCount) = SpecialSelectors[selectorIndex];
            var selector = executionContext.InternSymbol(selectorName);
            var args = context.Stack.PopN(argCount);
            var receiver = context.Stack.Pop();
            
            executionContext.Send(receiver, selector, args);
        }
        return true;
    }

    #endregion

    #region Extended Operations

    private bool ExecuteExtendedPush(MethodContext context)
    {
        byte descriptor = GetNextByte(context);
        int variableType = descriptor >> 6;
        int index = descriptor & 0x3F;
        
        return variableType switch
        {
            0 => PushReceiverVariable(context, index),
            1 => PushTemporaryVariable(context, index),
            2 => PushLiteralConstant(context, index),
            3 => PushLiteralVariable(context, index),
            _ => true
        };
    }

    private bool ExecuteExtendedStore(MethodContext context)
    {
        byte descriptor = GetNextByte(context);
        int variableType = descriptor >> 6;
        int index = descriptor & 0x3F;
        
        var value = context.Stack.Top; // Don't pop
        
        switch (variableType)
        {
            case 0: // Receiver variable
                executionContext.SetFieldByIndex(context.Receiver, index, value);
                break;
                
            case 1: // Temporary variable
                if (index < context.Temporaries.Length)
                {
                    context.Temporaries[index] = value;
                }
                break;
                
            case 2: // Illegal
            case 3: // Literal variable
                var literals = GetLiterals(context.Method);
                if (index < literals.Length)
                {
                    // Set Association's value field
                    executionContext.SetFieldByName(literals[index], "value", value);
                }
                break;
        }
        
        return true;
    }

    private bool ExecuteExtendedStoreAndPop(MethodContext context)
    {
        ExecuteExtendedStore(context);
        context.Stack.Pop();
        return true;
    }

    private bool ExecuteExtendedSend(MethodContext context)
    {
        byte descriptor = GetNextByte(context);
        int literal = descriptor & 0x1F;
        int argCount = (descriptor >> 5) & 0x07;
        
        return SendLiteralSelector(context, literal, argCount);
    }

    private bool ExecuteExtendedSuperSend(MethodContext context)
    {
        byte descriptor = GetNextByte(context);
        int literal = descriptor & 0x1F;
        int argCount = (descriptor >> 5) & 0x07;
        
        // Super send - similar to regular send but starts lookup in superclass
        var literals = GetLiterals(context.Method);
        if (literal < literals.Length)
        {
            var selector = literals[literal];
            var args = context.Stack.PopN(argCount);
            var receiver = context.Stack.Pop();
            
            executionContext.SuperSend(receiver, selector, args);
        }
        return true;
    }

    private bool ExecuteDoubleExtended(MethodContext context)
    {
        byte type = GetNextByte(context);
        byte index = GetNextByte(context);
        
        switch (type >> 5)
        {
            case 0: // Send literal selector
                int argCount = type & 0x1F;
                return SendLiteralSelector(context, index, argCount);
                
            case 1: // Super send literal selector
                argCount = type & 0x1F;
                var literals = GetLiterals(context.Method);
                if (index < literals.Length)
                {
                    var selector = literals[index];
                    var args = context.Stack.PopN(argCount);
                    var receiver = context.Stack.Pop();
                    executionContext.SuperSend(receiver, selector, args);
                }
                return true;
                
            case 2: // Push receiver variable
                return PushReceiverVariable(context, index);
                
            case 3: // Push literal constant
                return PushLiteralConstant(context, index);
                
            case 4: // Push literal variable
                return PushLiteralVariable(context, index);
                
            case 5: // Store receiver variable
                var value = context.Stack.Top;
                executionContext.SetFieldByIndex(context.Receiver, index, value);
                return true;
                
            case 6: // Store and pop receiver variable
                value = context.Stack.Pop();
                executionContext.SetFieldByIndex(context.Receiver, index, value);
                return true;
                
            case 7: // Store literal variable
                value = context.Stack.Top;
                literals = GetLiterals(context.Method);
                if (index < literals.Length)
                {
                    // Set Association's value field
                    executionContext.SetFieldByName(literals[index], "value", value);
                }
                return true;
                
            default:
                return true;
        }
    }

    #endregion

    #region Helper Methods

    private OOP[] GetLiterals(OOP method)
    {
        // CompiledMethod has fields: selector, bytecode, literals, header
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

    private byte GetNextByte(MethodContext context)
    {
        // CompiledMethod has fields: selector, bytecode, literals, header
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

    #endregion
}

/// <summary>
/// Context for bytecode execution.
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
    OOP GetSpecialObject(int index);
    
    /// <summary>
    /// Returns from the current context.
    /// </summary>
    void Return(MethodContext context, OOP value);
    
    /// <summary>
    /// Returns from a block context.
    /// </summary>
    void ReturnFromBlock(MethodContext context, OOP value);
    
    /// <summary>
    /// Creates a context object for reification.
    /// </summary>
    OOP CreateContextObject(MethodContext context);
    
    /// <summary>
    /// Performs a message send.
    /// </summary>
    void Send(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Performs a super send.
    /// </summary>
    void SuperSend(OOP receiver, OOP selector, OOP[] arguments);
    
    /// <summary>
    /// Interns a symbol string.
    /// </summary>
    OOP InternSymbol(string value);
    
    /// <summary>
    /// Gets a field value from an object by index.
    /// </summary>
    OOP GetFieldByIndex(OOP objectOop, int index);
    
    /// <summary>
    /// Sets a field value in an object by index.
    /// </summary>
    void SetFieldByIndex(OOP objectOop, int index, OOP value);
    
    /// <summary>
    /// Gets a field value from an object by name.
    /// </summary>
    OOP GetFieldByName(OOP objectOop, string fieldName);
    
    /// <summary>
    /// Sets a field value in an object by name.
    /// </summary>
    void SetFieldByName(OOP objectOop, string fieldName, OOP value);
}