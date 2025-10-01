namespace Smalltalk80.Interpreter.Execution;

using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// High-performance bytecode interpreter using tagged immediates.
/// This implementation eliminates heap allocation for SmallInteger and Character operations.
/// </summary>
public sealed class TaggedBytecodeInterpreter
{
    // Interpreter state - all using SObjectRef for zero allocation
    private SObjectRef receiver;
    private SCompiledMethod method = null!;
    private SObjectRef[] arguments = null!;
    private readonly Stack<SObjectRef> stack = new();
    private SObjectRef[] temporaries = null!;
    private int pc;
    
    // Cached special values
    private static readonly SObjectRef NilRef = SObjectRef.Nil;
    private static readonly SObjectRef TrueRef = SObjectRef.FromObject(STrue.Instance);
    private static readonly SObjectRef FalseRef = SObjectRef.FromObject(SFalse.Instance);
    
    /// <summary>
    /// Executes a compiled method with the given receiver and arguments.
    /// </summary>
    public SObjectRef Execute(SCompiledMethod compiledMethod, SObjectRef receiver, SObjectRef[] arguments)
    {
        // Method tracing
        if (DebugOptions.EnableMethodTrace)
        {
            Console.WriteLine($"[TRACE] Executing method: {compiledMethod.Selector}");
            Console.WriteLine($"        Receiver: {receiver}");
            if (arguments.Length > 0)
            {
                Console.WriteLine($"        Arguments: {string.Join(", ", arguments.Select(a => a.ToString()))}");
            }
        }
        
        // Initialize interpreter state
        this.receiver = receiver;
        this.method = compiledMethod;
        this.arguments = arguments;
        this.pc = 0;
        this.stack.Clear();
        
        // Initialize temporaries (arguments + method temps)
        temporaries = new SObjectRef[compiledMethod.NumArgs + compiledMethod.NumTemps];
        Array.Copy(arguments, temporaries, arguments.Length);
        
        // Initialize remaining temps to nil
        for (int i = arguments.Length; i < temporaries.Length; i++)
        {
            temporaries[i] = NilRef;
        }
        
        // Execute bytecodes
        while (pc < method.Bytecodes.Length)
        {
            var bytecode = method.Bytecodes[pc];
            
            // Bytecode tracing
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"[BYTECODE] PC={pc:D3} Bytecode=0x{bytecode:X2}");
                if (stack.Count > 0)
                {
                    Console.WriteLine($"           Stack Top: {stack.Peek()}");
                }
            }
            
            pc++;
            var result = ExecuteBytecode(bytecode);
            
            if (result.HasValue)
            {
                // Method returned
                if (DebugOptions.EnableMethodTrace)
                {
                    Console.WriteLine($"[TRACE] Method {compiledMethod.Selector} returning: {result.Value}");
                }
                return result.Value;
            }
        }
        
        // If we reach here without explicit return, return receiver
        if (DebugOptions.EnableMethodTrace)
        {
            Console.WriteLine($"[TRACE] Method {compiledMethod.Selector} returning receiver: {receiver}");
        }
        return receiver;
    }
    
    /// <summary>
    /// Executes a single bytecode instruction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private SObjectRef? ExecuteBytecode(byte bytecode)
    {
        // Decode bytecode ranges
        switch (bytecode)
        {
            // Push receiver variable 0-15
            case <= 0x0F:
                PushReceiverVariable(bytecode);
                break;
                
            // Push temporary variable 0-15
            case <= 0x1F:
                PushTemporaryVariable(bytecode - 0x10);
                break;
                
            // Push literal constant 0-31
            case <= 0x3F:
                PushLiteralConstant(bytecode - 0x20);
                break;
                
            // Push literal variable 0-31
            case <= 0x5F:
                PushLiteralVariable(bytecode - 0x40);
                break;
                
            // Pop and store receiver variable 0-7
            case <= 0x67:
                PopAndStoreReceiverVariable(bytecode - 0x60);
                break;
                
            // Pop and store temporary variable 0-7
            case <= 0x6F:
                PopAndStoreTemporaryVariable(bytecode - 0x68);
                break;
                
            // Push special constants
            case 0x70: stack.Push(receiver); break;           // Push receiver
            case 0x71: stack.Push(TrueRef); break;            // Push true
            case 0x72: stack.Push(FalseRef); break;           // Push false
            case 0x73: stack.Push(NilRef); break;             // Push nil
            case 0x74: stack.Push(SObjectRef.FromSmallInteger(-1)); break; // Push -1
            case 0x75: stack.Push(SObjectRef.FromSmallInteger(0)); break;  // Push 0
            case 0x76: stack.Push(SObjectRef.FromSmallInteger(1)); break;  // Push 1
            case 0x77: stack.Push(SObjectRef.FromSmallInteger(2)); break;  // Push 2
                
            // Return operations
            case 0x78: return stack.Pop();                    // Return stack top from message
            case 0x79: return receiver;                       // Return receiver from message
            case 0x7A: return TrueRef;                        // Return true from message
            case 0x7B: return FalseRef;                       // Return false from message
            case 0x7C: return NilRef;                         // Return nil from message
            case 0x7D: return stack.Pop();                    // Return stack top from block
            case 0x7E: return NilRef;                         // Return nil from block (not used)
            case 0x7F: break;                                 // Unknown/unused
                
            // Extended operations
            case >= 0x80 and <= 0x8F:
                ExecuteExtendedOperation(bytecode);
                break;
                
            // Jump operations
            case >= 0x90 and <= 0x97:
                Jump((bytecode & 0x07) + 1);
                break;
                
            case >= 0x98 and <= 0x9F:
                ConditionalJump(false, (bytecode & 0x07) + 1);
                break;
                
            case >= 0xA0 and <= 0xA7:
                {
                    var offset = ((bytecode & 0x07) - 4) * 256 + method.Bytecodes[pc++];
                    Jump(offset);
                }
                break;
                
            case >= 0xA8 and <= 0xAB:
                {
                    var offset = ((bytecode & 0x03) + 1) * 256 + method.Bytecodes[pc++];
                    ConditionalJump(bytecode < 0xAA, offset);
                }
                break;
                
            case >= 0xAC and <= 0xAF:
                {
                    var offset = ((bytecode & 0x03) - 2) * 256 + method.Bytecodes[pc++];
                    ConditionalJump(bytecode < 0xAE, offset);
                }
                break;
                
            // Send arithmetic selectors (special send bytecodes)
            case >= 0xB0 and <= 0xBF:
                ExecuteArithmeticSend(bytecode - 0xB0);
                break;
                
            // Send special selectors
            case >= 0xC0 and <= 0xDF:
                ExecuteSpecialSend(bytecode - 0xC0);
                break;
                
            // Send literal selector with 0-3 arguments
            case >= 0xE0 and <= 0xFF:
                {
                    var literalIndex = bytecode - 0xE0;
                    var numArgs = literalIndex / 8;
                    literalIndex = literalIndex % 8;
                    SendLiteralSelector(literalIndex, numArgs);
                }
                break;
        }
        
        return null; // Continue execution
    }
    
    /// <summary>
    /// Executes arithmetic operations using fast tagged arithmetic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteArithmeticSend(int selectorIndex)
    {
        // These are the special arithmetic selectors
        var selector = selectorIndex switch
        {
            0 => "+",
            1 => "-",
            2 => "<",
            3 => ">",
            4 => "<=",
            5 => ">=",
            6 => "=",
            7 => "~=",
            8 => "*",
            9 => "/",
            10 => "\\\\",
            11 => "@",
            12 => "bitShift:",
            13 => "//",
            14 => "bitAnd:",
            15 => "bitOr:",
            _ => throw new InvalidOperationException($"Invalid arithmetic selector index: {selectorIndex}")
        };
        
        // Try fast path for integer arithmetic
        if (selectorIndex <= 10 && stack.Count >= 2)
        {
            var arg = stack.Pop();
            var rcvr = stack.Pop();
            
            SObjectRef? result = selector switch
            {
                "+" => SObjectRef.FastIntegerAdd(rcvr, arg),
                "-" => SObjectRef.FastIntegerSubtract(rcvr, arg),
                "*" => SObjectRef.FastIntegerMultiply(rcvr, arg),
                _ => null
            };
            
            if (result.HasValue)
            {
                stack.Push(result.Value);
                return;
            }
            
            // Fast path for comparisons
            if (rcvr.IsSmallInteger && arg.IsSmallInteger)
            {
                var boolResult = selector switch
                {
                    "<" => SObjectRef.FastIntegerLessThan(rcvr, arg),
                    ">" => SObjectRef.FastIntegerLessThan(arg, rcvr),
                    "<=" => !SObjectRef.FastIntegerLessThan(arg, rcvr),
                    ">=" => !SObjectRef.FastIntegerLessThan(rcvr, arg),
                    "=" => SObjectRef.AreIdentical(rcvr, arg),
                    "~=" => !SObjectRef.AreIdentical(rcvr, arg),
                    _ => (bool?)null
                };
                
                if (boolResult.HasValue)
                {
                    stack.Push(boolResult.Value ? TrueRef : FalseRef);
                    return;
                }
            }
            
            // Push back for slow path
            stack.Push(rcvr);
            stack.Push(arg);
        }
        
        // Fall back to regular message send
        var numArgs = selector.Contains(':') ? 1 : 0;
        SendSelector(selector, numArgs);
    }
    
    /// <summary>
    /// Pushes a receiver variable onto the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushReceiverVariable(int index)
    {
        if (!receiver.IsObject)
        {
            throw new InvalidOperationException("Cannot access instance variables of immediate value");
        }

        var obj = receiver.AsObject();
        if (obj is SInstance instance && index < instance.InstanceVariableCount)
        {
            var value = instance.GetInstanceVariable(index);
            stack.Push(value);
        }
        else
        {
            throw new InvalidOperationException($"Invalid receiver variable index: {index}");
        }
    }
    
    /// <summary>
    /// Pushes a temporary variable onto the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushTemporaryVariable(int index)
    {
        if (index < temporaries.Length)
        {
            stack.Push(temporaries[index]);
        }
        else
        {
            throw new InvalidOperationException($"Invalid temporary variable index: {index}");
        }
    }
    
    /// <summary>
    /// Pushes a literal constant onto the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushLiteralConstant(int index)
    {
        if (index < method.Literals.Size)
        {
            var literal = method.Literals.At(index + 1);
            
            // Optimize for common literal types
            if (literal is SmallInteger si)
            {
                stack.Push(SObjectRef.FromSmallInteger(si.Value));
            }
            else if (literal is Character ch)
            {
                stack.Push(SObjectRef.FromObject(ch));
            }
            else if (literal == null)
            {
                stack.Push(NilRef);
            }
            else
            {
                stack.Push(SObjectRef.FromObject(literal));
            }
        }
        else
        {
            throw new InvalidOperationException($"Invalid literal index: {index}");
        }
    }
    
    /// <summary>
    /// Pushes the value of a literal variable onto the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushLiteralVariable(int index)
    {
        // This would access global variables through associations
        // For now, simplified implementation
        PushLiteralConstant(index);
    }
    
    /// <summary>
    /// Pops a value from the stack and stores it in a receiver variable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopAndStoreReceiverVariable(int index)
    {
        var value = stack.Pop();

        if (!receiver.IsObject)
        {
            throw new InvalidOperationException("Cannot set instance variables of immediate value");
        }

        var obj = receiver.AsObject();
        if (obj is SInstance instance && index < instance.InstanceVariableCount)
        {
            instance.SetInstanceVariable(index, value);
        }
        else
        {
            throw new InvalidOperationException($"Invalid receiver variable index: {index}");
        }
    }
    
    /// <summary>
    /// Pops a value from the stack and stores it in a temporary variable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopAndStoreTemporaryVariable(int index)
    {
        if (index < temporaries.Length)
        {
            temporaries[index] = stack.Pop();
        }
        else
        {
            throw new InvalidOperationException($"Invalid temporary variable index: {index}");
        }
    }
    
    /// <summary>
    /// Executes extended operations (two-byte bytecodes).
    /// </summary>
    private void ExecuteExtendedOperation(byte bytecode)
    {
        var extByte = method.Bytecodes[pc++];
        var operation = (bytecode & 0x0E) >> 1;
        
        switch (operation)
        {
            case 0: // Extended push
                if ((bytecode & 0x01) == 0)
                {
                    PushReceiverVariable(extByte);
                }
                else
                {
                    PushTemporaryVariable(extByte);
                }
                break;
                
            case 1: // Extended store
                if ((bytecode & 0x01) == 0)
                {
                    PopAndStoreReceiverVariable(extByte);
                }
                else
                {
                    PopAndStoreTemporaryVariable(extByte);
                }
                break;
                
            case 2: // Extended pop and store
                if ((bytecode & 0x01) == 0)
                {
                    PopAndStoreReceiverVariable(extByte);
                    stack.Pop(); // Extra pop
                }
                else
                {
                    PopAndStoreTemporaryVariable(extByte);
                    stack.Pop(); // Extra pop
                }
                break;
                
            case 3: // Extended send
                SendLiteralSelector(extByte & 0x1F, extByte >> 5);
                break;
                
            case 4: // Extended send super
                // Not implemented yet
                break;
                
            case 5: // Extended push literal
                PushLiteralConstant(extByte);
                break;
                
            case 6: // Extended push literal variable
                PushLiteralVariable(extByte);
                break;
                
            case 7: // Extended store literal variable
                // Not implemented yet
                break;
        }
    }
    
    /// <summary>
    /// Performs an unconditional jump.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Jump(int offset)
    {
        pc += offset;
    }
    
    /// <summary>
    /// Performs a conditional jump based on the top of stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ConditionalJump(bool jumpOnTrue, int offset)
    {
        var condition = stack.Pop();
        
        // Check for boolean
        var isTrue = SObjectRef.AreIdentical(condition, TrueRef);
        var isFalse = SObjectRef.AreIdentical(condition, FalseRef);
        
        if (!isTrue && !isFalse)
        {
            throw new InvalidOperationException("Conditional jump requires boolean");
        }
        
        if ((jumpOnTrue && isTrue) || (!jumpOnTrue && isFalse))
        {
            pc += offset;
        }
    }
    
    /// <summary>
    /// Executes a special selector send.
    /// </summary>
    private void ExecuteSpecialSend(int selectorIndex)
    {
        // These are common message patterns
        var (selector, numArgs) = selectorIndex switch
        {
            0 => ("at:", 1),
            1 => ("at:put:", 2),
            2 => ("size", 0),
            3 => ("next", 0),
            4 => ("nextPut:", 1),
            5 => ("atEnd", 0),
            6 => ("==", 1),
            7 => ("class", 0),
            8 => ("blockCopy:", 1),
            9 => ("value", 0),
            10 => ("value:", 1),
            11 => ("do:", 1),
            12 => ("new", 0),
            13 => ("new:", 1),
            14 => ("x", 0),
            15 => ("y", 0),
            _ => throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}")
        };
        
        SendSelector(selector, numArgs);
    }
    
    /// <summary>
    /// Sends a literal selector from the method's literal frame.
    /// </summary>
    private void SendLiteralSelector(int literalIndex, int numArgs)
    {
        if (literalIndex >= method.Literals.Size)
        {
            throw new InvalidOperationException($"Invalid literal selector index: {literalIndex}");
        }
        
        var selector = method.Literals.At(literalIndex + 1) as SSymbol;
        if (selector == null)
        {
            throw new InvalidOperationException($"Literal at index {literalIndex} is not a symbol");
        }
        
        SendSelector(selector.Value, numArgs);
    }
    
    /// <summary>
    /// Sends a message with the specified selector and arguments.
    /// </summary>
    private void SendSelector(string selector, int numArgs)
    {
        // Pop arguments from stack
        var args = new SObjectRef[numArgs];
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = stack.Pop();
        }
        
        // Pop receiver
        var messageReceiver = stack.Pop();
        
        // Look up method in receiver's class
        var receiverClass = messageReceiver.GetClass();
        var targetMethod = receiverClass.LookupMethod(selector);
        
        if (targetMethod == null)
        {
            // Handle doesNotUnderstand:
            throw new MessageNotUnderstoodException($"Message not understood: {selector}");
        }
        
        // Check for primitive
        if (targetMethod.PrimitiveIndex > 0)
        {
            var primitiveResult = ExecutePrimitive(targetMethod.PrimitiveIndex, messageReceiver, args);
            if (primitiveResult.HasValue)
            {
                stack.Push(primitiveResult.Value);
                return;
            }
            // Primitive failed, execute method normally
        }
        
        // Create new interpreter for the method (in production, use context switching)
        var interpreter = new TaggedBytecodeInterpreter();
        var result = interpreter.Execute(targetMethod, messageReceiver, args);
        stack.Push(result);
    }
    
    /// <summary>
    /// Executes a primitive operation.
    /// </summary>
    private SObjectRef? ExecutePrimitive(int primitiveIndex, SObjectRef receiver, SObjectRef[] arguments)
    {
        try
        {
            return primitiveIndex switch
            {
                // SmallInteger arithmetic - use fast tagged operations
                1 => arguments.Length == 1 ? SObjectRef.FastIntegerAdd(receiver, arguments[0]) : null,
                2 => arguments.Length == 1 ? SObjectRef.FastIntegerSubtract(receiver, arguments[0]) : null,
                9 => arguments.Length == 1 ? SObjectRef.FastIntegerMultiply(receiver, arguments[0]) : null,
                
                // Comparisons
                3 => arguments.Length == 1 && receiver.IsSmallInteger && arguments[0].IsSmallInteger
                    ? (SObjectRef.FastIntegerLessThan(receiver, arguments[0]) ? TrueRef : FalseRef)
                    : null,
                
                // Object identity
                110 => arguments.Length == 1
                    ? (SObjectRef.AreIdentical(receiver, arguments[0]) ? TrueRef : FalseRef)
                    : null,
                    
                _ => null // Unknown or unimplemented primitive
            };
        }
        catch
        {
            return null; // Primitive failed
        }
    }
}