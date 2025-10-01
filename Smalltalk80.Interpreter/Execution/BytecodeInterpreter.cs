namespace Smalltalk80.Interpreter.Execution;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// The Smalltalk-80 bytecode interpreter.
/// </summary>
public class BytecodeInterpreter
{
    private SObject receiver = null!;
    private SCompiledMethod method = null!;
    private SObject[] arguments = null!;
    private readonly Stack<SObject> stack = new();
    private SObject?[] temporaries = null!;
    private int pc;
    
    /// <summary>
    /// Executes a compiled method with the given receiver and arguments.
    /// </summary>
    public SObject Execute(SCompiledMethod compiledMethod, SObject receiver, SObject[] arguments)
    {
        // Method tracing
        if (DebugOptions.EnableMethodTrace)
        {
            Console.WriteLine($"[TRACE] Executing method: {compiledMethod.Selector}");
            Console.WriteLine($"        Receiver: {receiver}");
            if (arguments.Length > 0)
            {
                Console.WriteLine($"        Arguments: {string.Join(", ", arguments.Select(a => a?.ToString() ?? "nil"))}");
            }
        }
        
        // Initialize interpreter state
        this.receiver = receiver;
        this.method = compiledMethod;
        this.arguments = arguments;
        this.pc = 0;
        this.stack.Clear();
        
        // Initialize temporaries (arguments + method temps)
        temporaries = new SObject[compiledMethod.NumArgs + compiledMethod.NumTemps];
        Array.Copy(arguments, temporaries, arguments.Length);
        
        // Show bytecode if requested
        if (DebugOptions.EnableVMBytecodeMessages)
        {
            Console.WriteLine($"[VM] Executing bytecode for {compiledMethod.Selector}:");
            Console.WriteLine($"     Bytecodes: {BitConverter.ToString(compiledMethod.Bytecodes)}");
        }
        
        // Execute bytecodes
        while (pc < method.Bytecodes.Length)
        {
            var bytecode = method.Bytecodes[pc];
            
            // Bytecode tracing
            if (DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"[BYTECODE] PC={pc:D3} Bytecode=0x{bytecode:X2} {GetBytecodeMnemonic(bytecode)}");
                if (stack.Count > 0)
                {
                    Console.WriteLine($"           Stack Top: {stack.Peek()}");
                }
            }
            
            pc++;
            var result = ExecuteBytecode(bytecode);
            
            if (result != null)
            {
                // Method returned
                if (DebugOptions.EnableMethodTrace)
                {
                    Console.WriteLine($"[TRACE] Method {compiledMethod.Selector} returning: {result}");
                }
                return result;
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
    /// Gets the mnemonic for a bytecode.
    /// </summary>
    private string GetBytecodeMnemonic(byte bytecode)
    {
        if (!DebugOptions.ShowBytecodeWithMnemonics)
            return "";
            
        // Push receiver variable 0-15
        if (bytecode <= 0x0F)
            return $"pushReceiverVariable: {bytecode}";
        
        // Push temporary 0-15
        if (bytecode >= 0x10 && bytecode <= 0x1F)
            return $"pushTemporary: {bytecode - 0x10}";
        
        // Push literal constant 0-31
        if (bytecode >= 0x20 && bytecode <= 0x3F)
            return $"pushLiteral: {bytecode - 0x20}";
        
        // Push literal variable 0-31
        if (bytecode >= 0x40 && bytecode <= 0x5F)
            return $"pushLiteralVariable: {bytecode - 0x40}";
        
        // Pop and store receiver variable 0-7
        if (bytecode >= 0x60 && bytecode <= 0x67)
            return $"popIntoReceiverVariable: {bytecode - 0x60}";
        
        // Pop and store temporary 0-7
        if (bytecode >= 0x68 && bytecode <= 0x6F)
            return $"popIntoTemporary: {bytecode - 0x68}";
        
        // Special constants and returns
        return bytecode switch
        {
            0x70 => "pushReceiver",
            0x71 => "pushTrue",
            0x72 => "pushFalse",
            0x73 => "pushNil",
            0x74 => "pushMinusOne",
            0x75 => "pushZero",
            0x76 => "pushOne",
            0x77 => "pushTwo",
            0x78 => "returnReceiver",
            0x79 => "returnTrue",
            0x7A => "returnFalse",
            0x7B => "returnNil",
            0x7C => "returnTopFromMethod",
            0x7D => "returnTopFromBlock",
            0x7E => "pushActiveContext",
            0x7F => "nop",
            // Arithmetic
            0xB0 => "add",
            0xB1 => "subtract",
            0xB2 => "lessThan",
            0xB3 => "greaterThan",
            0xB4 => "lessOrEqual",
            0xB5 => "greaterOrEqual",
            0xB6 => "equal",
            0xB7 => "notEqual",
            0xB8 => "multiply",
            0xB9 => "divide",
            0xBA => "mod",
            0xBB => "bitShift",
            0xBC => "integerDivide",
            0xBD => "bitAnd",
            0xBE => "bitOr",
            _ => $"bytecode_{bytecode:X2}"
        };
    }
    
    /// <summary>
    /// Executes a single bytecode.
    /// </summary>
    private SObject? ExecuteBytecode(byte bytecode)
    {
        // Push receiver variable 0-15
        if (bytecode <= 0x0F)
        {
            PushReceiverVariable(bytecode);
            return null;
        }
        
        // Push temporary 0-15
        if (bytecode >= 0x10 && bytecode <= 0x1F)
        {
            PushTemporary(bytecode - 0x10);
            return null;
        }
        
        // Push literal constant 0-31
        if (bytecode >= 0x20 && bytecode <= 0x3F)
        {
            PushLiteral(bytecode - 0x20);
            return null;
        }
        
        // Push literal variable 0-31
        if (bytecode >= 0x40 && bytecode <= 0x5F)
        {
            PushLiteralVariable(bytecode - 0x40);
            return null;
        }
        
        // Pop and store receiver variable 0-7
        if (bytecode >= 0x60 && bytecode <= 0x67)
        {
            PopAndStoreReceiverVariable(bytecode - 0x60);
            return null;
        }
        
        // Pop and store temporary 0-7
        if (bytecode >= 0x68 && bytecode <= 0x6F)
        {
            PopAndStoreTemporary(bytecode - 0x68);
            return null;
        }
        
        // Special constants and returns
        switch (bytecode)
        {
            case Bytecodes.PushReceiver:
                stack.Push(receiver);
                return null;
                
            case Bytecodes.PushTrue:
                stack.Push(STrue.Instance);
                return null;
                
            case Bytecodes.PushFalse:
                stack.Push(SFalse.Instance);
                return null;
                
            case Bytecodes.PushNil:
                stack.Push(SObjectRef.Nil.AsObject()!);
                return null;

            case Bytecodes.PushMinusOne:
                stack.Push(SObjectRef.FromSmallInteger(-1).AsObject()!);
                return null;

            case Bytecodes.PushZero:
                stack.Push(SObjectRef.FromSmallInteger(0).AsObject()!);
                return null;

            case Bytecodes.PushOne:
                stack.Push(SObjectRef.FromSmallInteger(1).AsObject()!);
                return null;

            case Bytecodes.PushTwo:
                stack.Push(SObjectRef.FromSmallInteger(2).AsObject()!);
                return null;
                
            case Bytecodes.ReturnReceiver:
                return receiver;
                
            case Bytecodes.ReturnTrue:
                return STrue.Instance;
                
            case Bytecodes.ReturnFalse:
                return SFalse.Instance;
                
            case Bytecodes.ReturnNil:
                return SObjectRef.Nil.AsObject();
                
            case Bytecodes.ReturnTopFromMethod:
                return stack.Pop();
                
            case Bytecodes.PopStackBytecode:
                stack.Pop();
                return null;
        }
        
        // Jumps
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            // Short unconditional jump 1-8
            pc += (bytecode - 0x90) + 1;
            return null;
        }
        
        if (bytecode >= 0x98 && bytecode <= 0x9F)
        {
            // Short conditional jump 1-8 if false
            var condition = stack.Pop();
            if (condition == SFalse.Instance)
            {
                pc += (bytecode - 0x98) + 1;
            }
            return null;
        }
        
        // Arithmetic selectors
        if (bytecode >= 0xB0 && bytecode <= 0xBF)
        {
            ExecuteArithmeticSelector(bytecode - 0xB0);
            return null;
        }
        
        // Special selectors
        if (bytecode >= 0xC0 && bytecode <= 0xCF)
        {
            ExecuteSpecialSelector(bytecode - 0xC0);
            return null;
        }
        
        // Send literal selector with 0 args
        if (bytecode >= 0xD0 && bytecode <= 0xDF)
        {
            SendLiteralSelector(bytecode - 0xD0, 0);
            return null;
        }
        
        // Send literal selector with 1 arg
        if (bytecode >= 0xE0 && bytecode <= 0xEF)
        {
            SendLiteralSelector(bytecode - 0xE0, 1);
            return null;
        }
        
        // Send literal selector with 2 args
        if (bytecode >= 0xF0 && bytecode <= 0xFF)
        {
            SendLiteralSelector(bytecode - 0xF0, 2);
            return null;
        }
        
        throw new NotImplementedException($"Bytecode {bytecode:X2} not implemented");
    }
    
    private void PushReceiverVariable(int index)
    {
        if (receiver is SInstance instance)
        {
            var value = instance.GetInstanceVariable(index);
            stack.Push(value.AsObject()!);
        }
        else
        {
            // For non-instance receivers (like SmallInteger), push nil
            stack.Push(SObjectRef.Nil.AsObject()!);
        }
    }
    
    private void PushTemporary(int index)
    {
        if (index >= temporaries.Length)
        {
            throw new InvalidOperationException($"Temporary index {index} out of range");
        }
        stack.Push(temporaries[index]!);
    }
    
    private void PushLiteral(int index)
    {
        var literal = method.Literals.At(index + 1) ;
        stack.Push(literal!);
    }
    
    private void PushLiteralVariable(int index)
    {
        var association = method.Literals.At(index + 1);
        if (association is SArray array && array.Size == 2)
        {
            var value = array.At(2) ;
            stack.Push(value!);
        }
        else
        {
            // Try as global variable name
            if (association is SSymbol symbol)
            {
                var global = SystemDictionary.LookupGlobal(symbol.Value) ;
                stack.Push(global!);
            }
            else
            {
                stack.Push(SObjectRef.Nil.AsObject()!);
            }
        }
    }
    
    private void PopAndStoreReceiverVariable(int index)
    {
        var value = stack.Pop();
        if (receiver is SInstance instance)
        {
            instance.SetInstanceVariable(index, SObjectRef.FromObject(value));
        }
    }
    
    private void PopAndStoreTemporary(int index)
    {
        if (index >= temporaries.Length)
        {
            throw new InvalidOperationException($"Temporary index {index} out of range");
        }
        temporaries[index] = stack.Pop();
    }
    
    private void ExecuteArithmeticSelector(int index)
    {
        var selector = Bytecodes.ArithmeticSelectors[index];
        var argCount = selector.Count(c => c == ':');
        
        // Pop arguments
        var args = new SObject[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = stack.Pop();
        }
        
        // Pop receiver
        var messageReceiver = stack.Pop();
        
        // Try primitive arithmetic
        if (messageReceiver is SmallInteger intReceiver)
        {
            var result = ExecuteIntegerArithmetic(intReceiver, selector, args);
            if (result != null)
            {
                stack.Push(result);
                return;
            }
        }
        
        // Fall back to message send
        var sendResult = messageReceiver.Send(selector, args);
        stack.Push(sendResult);
    }
    
    private void ExecuteSpecialSelector(int index)
    {
        var selector = Bytecodes.SpecialSelectors[index];
        var argCount = selector.Count(c => c == ':');
        
        // Pop arguments
        var args = new SObject[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = stack.Pop();
        }
        
        // Pop receiver
        var messageReceiver = stack.Pop();
        
        // Handle special selectors
        switch (selector)
        {
            case "==":
                // Identity comparison
                var isIdentical = argCount > 0 && ReferenceEquals(messageReceiver, args[0]);
                stack.Push(isIdentical ? STrue.Instance : SFalse.Instance);
                return;
                
            case "class":
                stack.Push(messageReceiver.Class);
                return;
                
            case "size":
                if (messageReceiver is SArray array)
                {
                    stack.Push(SObjectRef.FromSmallInteger(array.Size).AsObject()!);
                    return;
                }
                else if (messageReceiver is SString str)
                {
                    stack.Push(SObjectRef.FromSmallInteger(str.Length).AsObject()!);
                    return;
                }
                break;
        }
        
        // Fall back to message send
        var result = messageReceiver.Send(selector, args);
        stack.Push(result);
    }
    
    private void SendLiteralSelector(int literalIndex, int argCount)
    {
        var selector = method.Literals.At(literalIndex + 1);
        if (selector is not SSymbol symbol)
        {
            throw new InvalidOperationException("Selector must be a symbol");
        }
        
        // Pop arguments
        var args = new SObject[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = stack.Pop();
        }
        
        // Pop receiver
        var messageReceiver = stack.Pop();
        
        // Send message
        var result = messageReceiver.Send(symbol.Value, args);
        stack.Push(result);
    }
    
    private SObject? ExecuteIntegerArithmetic(SmallInteger receiver, string selector, SObject[] args)
    {
        if (args.Length > 0 && args[0] is SmallInteger arg)
        {
            try
            {
                return selector switch
                {
                    "+" => receiver.Add(arg),
                    "-" => receiver.Subtract(arg),
                    "*" => receiver.Multiply(arg),
                    "/" => receiver.Divide(arg),
                    "\\\\" => receiver.Modulo(arg),
                    "<" => receiver < arg ? STrue.Instance : SFalse.Instance,
                    ">" => receiver > arg ? STrue.Instance : SFalse.Instance,
                    "<=" => receiver <= arg ? STrue.Instance : SFalse.Instance,
                    ">=" => receiver >= arg ? STrue.Instance : SFalse.Instance,
                    "=" => receiver == arg ? STrue.Instance : SFalse.Instance,
                    "~=" => receiver != arg ? STrue.Instance : SFalse.Instance,
                    _ => null
                };
            }
            catch (OverflowException)
            {
                // Would promote to LargeInteger in real Smalltalk
                return null;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Disposes of interpreter resources.
    /// </summary>
    public void Dispose()
    {
        // Clear any resources
        stack.Clear();
        receiver = null!;
        method = null!;
        arguments = null!;
        temporaries = null!;
    }

    /// <summary>
    /// Gets the processor (ProcessorScheduler).
    /// NOTE: This is a simplified stub - proper implementation would return the actual ProcessorScheduler singleton.
    /// </summary>
    public SObject GetProcessor()
    {
        // Return the ProcessorScheduler singleton
        // In a full implementation, this would be properly initialized
        return SystemDictionary.ProcessorSchedulerClass?.CreateInstance() ?? SObjectRef.Nil.AsObject()!;
    }
}