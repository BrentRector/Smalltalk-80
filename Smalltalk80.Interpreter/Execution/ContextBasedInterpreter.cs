namespace Smalltalk80.Interpreter.Execution;

using System.Linq;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Blue Book compliant context-based bytecode interpreter.
/// This interpreter uses MethodContext and BlockContext objects for execution,
/// not C# stacks, making it fully compliant with the Smalltalk-80 specification.
/// </summary>
/// <remarks>
/// From the Blue Book:
/// The interpreter executes bytecodes in the context of MethodContext and BlockContext objects.
/// Each context maintains its own instruction pointer, stack pointer, and evaluation stack.
/// Context switching is done by changing the active context, not by using a call stack.
/// </remarks>
public sealed class ContextBasedInterpreter
{
    private SObject? activeContext;
    private readonly ILogger<ContextBasedInterpreter>? logger;
    private readonly PrimitiveDispatcherBase primitiveDispatcher;
    private bool tracingEnabled = false;
    private int traceIndentLevel = 0;
    
    /// <summary>
    /// Gets or sets the special objects array.
    /// </summary>
    public SArray? SpecialObjectsArray { get; set; }
    
    /// <summary>
    /// Gets the current active context (supports thisContext).
    /// </summary>
    public SObject? ActiveContext => activeContext;
    
    /// <summary>
    /// Enables or disables execution tracing.
    /// </summary>
    public bool TracingEnabled 
    { 
        get => tracingEnabled;
        set => tracingEnabled = value;
    }
    
    /// <summary>
    /// Initializes a new instance of the ContextBasedInterpreter.
    /// </summary>
    public ContextBasedInterpreter(ILogger<ContextBasedInterpreter>? logger = null)
    {
        this.logger = logger;
        this.primitiveDispatcher = new BluBookPrimitiveDispatcher(this);
    }
    
    /// <summary>
    /// Executes a method in a new method context.
    /// </summary>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="arguments">The arguments to the method.</param>
    /// <returns>The result of the method execution.</returns>
    public SObject Execute(SCompiledMethod method, SObject receiver, SObject[] arguments)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(arguments);
        
        if (tracingEnabled)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            Console.WriteLine($"{indent}==> EXECUTE SELECTOR: {method.Selector}");
            Console.WriteLine($"{indent}    Receiver: {receiver.Class?.Name ?? "Unknown"} ({receiver.ToDisplayString()})");
            Console.WriteLine($"{indent}    Arguments: [{string.Join(", ", arguments.Select(a => $"{a.Class?.Name ?? "Unknown"} ({a.ToDisplayString()})"))}]");
            Console.WriteLine($"{indent}    Bytecodes: {method.Bytecodes.Length} bytes");
            
            // Show literals if there are any
            if (method.Literals.Size > 0)
            {
                Console.WriteLine($"{indent}    Literals: {method.Literals.Size} literals");
                for (int i = 0; i < method.Literals.Size; i++)
                {
                    var literal = method.GetLiteral(i);
                    var literalDisplay = literal switch
                    {
                        SSymbol sym => $"#{sym.Value}",
                        SmallInteger si => si.Value.ToString(),
                        SString str => $"\"{str.Value}\"",
                        SArray arr => $"Array[{arr.Size}]",
                        null => "nil",
                        _ => literal.ToDisplayString()
                    };
                    Console.WriteLine($"{indent}      [{i}]: {literalDisplay}");
                }
            }
            else
            {
                Console.WriteLine($"{indent}    Literals: 0 literals");
            }
            
            Console.WriteLine($"{indent}    Primitive: {(method.PrimitiveIndex > 0 ? $"#{method.PrimitiveIndex}" : "none")}");
            traceIndentLevel++;
        }
        
        // Create a new method context as a proper Smalltalk object
        var newContext = SMethodContext.CreateMethodContext(method, receiver, arguments);
        
        if (tracingEnabled)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            try
            {
                Console.WriteLine($"{indent}Created MethodContext: {newContext.ToDisplayString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{indent}Created MethodContext (error displaying: {ex.Message})");
                Console.WriteLine($"{indent}  Method: {method?.Selector ?? "null"}");
                Console.WriteLine($"{indent}  Receiver: {receiver?.ToDisplayString() ?? "null"}");
            }
        }
        
        // Set the sender if we have an active context
        if (activeContext != null)
        {
            newContext.Sender = activeContext;
        }
        
        // Make this the active context
        var previousContext = activeContext;
        activeContext = newContext;
        
        try
        {
            // Check for primitive
            if (method!.PrimitiveIndex > 0)
            {
                if (tracingEnabled)
                {
                    var indent = new string(' ', traceIndentLevel * 2);
                    Console.WriteLine($"{indent}Attempting primitive #{method.PrimitiveIndex}...");
                }
                
                var primitiveResult = primitiveDispatcher.ExecutePrimitive(
                    method.PrimitiveIndex, receiver!, arguments);
                    
                if (primitiveResult != null)
                {
                    // Primitive succeeded
                    if (tracingEnabled)
                    {
                        var indent = new string(' ', traceIndentLevel * 2);
                        Console.WriteLine($"{indent}Primitive succeeded, returning: {primitiveResult.Class?.Name ?? "Unknown"} ({primitiveResult.ToDisplayString()})");
                        traceIndentLevel--;
                        Console.WriteLine($"{new string(' ', traceIndentLevel * 2)}<== RETURN from {method.Selector}");
                    }
                    return primitiveResult;
                }
                // Primitive failed, continue with bytecode execution
                if (tracingEnabled)
                {
                    var indent = new string(' ', traceIndentLevel * 2);
                    Console.WriteLine($"{indent}Primitive failed, executing bytecode...");
                }
            }
            
            // Execute bytecodes
            if (tracingEnabled)
            {
                var indent = new string(' ', traceIndentLevel * 2);
                Console.WriteLine($"{indent}Executing bytecodes...");
            }
            
            var result = ExecuteBytecodes();
            
            if (tracingEnabled)
            {
                traceIndentLevel--;
                var indent = new string(' ', traceIndentLevel * 2);
                Console.WriteLine($"{indent}<== RETURN from {method.Selector}: {result.Class?.Name ?? "Unknown"} ({result.ToDisplayString()})");
            }
            
            return result;
        }
        finally
        {
            // Restore previous context
            activeContext = previousContext;
        }
    }
    
    /// <summary>
    /// Executes a block in a new block context.
    /// </summary>
    public SObject ExecuteBlock(SBlockContext blockContext, SObject[] arguments)
    {
        // Activate the block with arguments
        blockContext.Activate(activeContext, arguments);
        
        // Make this the active context
        var previousContext = activeContext;
        activeContext = blockContext;
        
        try
        {
            return ExecuteBytecodes();
        }
        finally
        {
            // Restore previous context
            activeContext = previousContext;
        }
    }
    
    /// <summary>
    /// Executes bytecodes in the current active context.
    /// </summary>
    private SObject ExecuteBytecodes()
    {
        if (activeContext == null)
        {
            throw new InvalidOperationException("No active context");
        }
        
        // Determine if we're in a method or block context
        var isMethodContext = activeContext is SMethodContext;
        SMethodContext methodContext = isMethodContext 
            ? (SMethodContext)activeContext! 
            : ((SBlockContext)activeContext!).Home;
        dynamic context = activeContext!;
        
        var method = methodContext!.Method;
        var bytecodes = method.Bytecodes;
        
        while (true)
        {
            var ip = context.InstructionPointer.Value;
            
            if (ip >= bytecodes.Length)
            {
                // End of bytecodes without explicit return
                return methodContext.Receiver;
            }
            
            var bytecode = bytecodes[ip];
            context.InstructionPointer = SmallInteger.From(ip + 1);
            
            if (DebugOptions.EnableBytecodeTrace || tracingEnabled)
            {
                if (tracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 1) * 2);
                    Console.WriteLine($"{indent}[PC={ip:D3}] Bytecode: 0x{bytecode:X2} ({GetBytecodeName(bytecode)})");
                }
                else
                {
                    logger?.LogDebug($"PC={ip:D3} Bytecode=0x{bytecode:X2}");
                }
            }
            
            // Execute bytecode
            var shouldReturn = ExecuteSingleBytecode(bytecode, context, methodContext);
            
            if (shouldReturn)
            {
                // Method or block returned
                var returnValue = context.Pop();
                if (tracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 1) * 2);
                    Console.WriteLine($"{indent}Returning: {returnValue.Class?.Name ?? "Unknown"} ({returnValue.ToDisplayString()})");
                }
                return returnValue;
            }
        }
    }
    
    /// <summary>
    /// Executes a single bytecode instruction.
    /// </summary>
    /// <returns>True if the method should return, false to continue.</returns>
    private bool ExecuteSingleBytecode(byte bytecode, dynamic context, SMethodContext homeContext)
    {
        // Push receiver variable 0-15
        if (bytecode <= 0x0F)
        {
            var index = bytecode & 0x0F;
            var receiver = homeContext.Receiver;
            if (receiver is SInstance instance)
            {
                context.Push(instance.GetInstanceVariable(index) ?? SNil.Instance);
            }
            else
            {
                context.Push(SNil.Instance);
            }
            return false;
        }
        
        // Push temporary variable 0-15
        if (bytecode >= 0x10 && bytecode <= 0x1F)
        {
            var index = bytecode & 0x0F;
            context.Push(homeContext.GetTemporary(index));
            return false;
        }
        
        // Push literal constant 0-31
        if (bytecode >= 0x20 && bytecode <= 0x3F)
        {
            var index = bytecode & 0x1F;
            var literal = homeContext.Method.GetLiteral(index);
            context.Push(literal ?? SNil.Instance);
            return false;
        }
        
        // Push literal variable 0-31
        if (bytecode >= 0x40 && bytecode <= 0x5F)
        {
            var index = bytecode & 0x1F;
            var association = homeContext.Method.GetLiteral(index);
            if (association is SArray arr && arr.Size >= 2)
            {
                context.Push(arr.At(1) ?? SNil.Instance);
            }
            else
            {
                context.Push(SNil.Instance);
            }
            return false;
        }
        
        // Store and pop receiver variable 0-7
        if (bytecode >= 0x60 && bytecode <= 0x67)
        {
            var index = bytecode & 0x07;
            var value = context.Pop();
            var receiver = homeContext.Receiver;
            if (receiver is SInstance instance)
            {
                instance.SetInstanceVariable(index, value);
            }
            return false;
        }
        
        // Store and pop temporary variable 0-7  
        if (bytecode >= 0x68 && bytecode <= 0x6F)
        {
            var index = bytecode & 0x07;
            var value = context.Pop();
            homeContext.SetTemporary(index, value);
            return false;
        }
        
        // Push special constants
        switch (bytecode)
        {
            case 0x70: // pushReceiver
                context.Push(homeContext.Receiver);
                return false;
                
            case 0x71: // pushTrue
                context.Push(STrue.Instance);
                return false;
                
            case 0x72: // pushFalse
                context.Push(SFalse.Instance);
                return false;
                
            case 0x73: // pushNil
                context.Push(SNil.Instance);
                return false;
                
            case 0x74: // pushMinusOne
                context.Push(SmallInteger.From(-1));
                return false;
                
            case 0x75: // pushZero
                context.Push(SmallInteger.From(0));
                return false;
                
            case 0x76: // pushOne
                context.Push(SmallInteger.From(1));
                return false;
                
            case 0x77: // pushTwo
                context.Push(SmallInteger.From(2));
                return false;
                
            case 0x78: // return receiver
                context.Push(homeContext.Receiver);
                return true;
                
            case 0x79: // return true
                context.Push(STrue.Instance);
                return true;
                
            case 0x7A: // return false
                context.Push(SFalse.Instance);
                return true;
                
            case 0x7B: // return nil
                context.Push(SNil.Instance);
                return true;
                
            case 0x7C: // return top
                // Top is already on stack
                return true;
                
            case 0x7D: // block return (return from block to caller)
                if (context is SBlockContext blockCtx)
                {
                    // Return from block to its caller
                    return true;
                }
                throw new InvalidOperationException("blockReturn used outside block context");
                
            case 0x7E: case 0x7F: // extended bytecodes
                return ExecuteExtendedBytecode(bytecode, context, homeContext);
                
            case 0x87: // pop
                context.Pop();
                return false;
                
            case 0x88: // dup
                var top = context.Top();
                context.Push(top);
                return false;
                
            case 0x89: // pushActiveContext (thisContext)
                context.Push(activeContext!);
                return false;
        }
        
        // Store into literal variable 0-31
        if (bytecode >= 0x80 && bytecode <= 0x8F && (bytecode & 0x08) == 0)
        {
            var index = bytecode & 0x07;
            var value = context.Top(); // Don't pop
            var association = homeContext.Method.GetLiteral(index);
            if (association is SArray arr && arr.Size >= 2)
            {
                arr.AtPut(1, value);
            }
            return false;
        }
        
        // Store and pop into literal variable 0-7
        if (bytecode >= 0x88 && bytecode <= 0x8F)
        {
            var index = bytecode & 0x07;
            var value = context.Pop();
            var association = homeContext.Method.GetLiteral(index);
            if (association is SArray arr && arr.Size >= 2)
            {
                arr.AtPut(1, value);
            }
            return false;
        }
        
        // Send arithmetic messages
        if (bytecode >= 0xB0 && bytecode <= 0xBF)
        {
            ExecuteSendArithmetic(bytecode, context);
            return false;
        }
        
        // Send special messages
        if (bytecode >= 0xC0 && bytecode <= 0xCF)
        {
            ExecuteSendSpecial(bytecode, context);
            return false;
        }
        
        // Send literal selector with 0-3 arguments
        if (bytecode >= 0xD0 && bytecode <= 0xDF)
        {
            var numArgs = (bytecode >> 2) & 0x03;
            var selectorIndex = ((bytecode & 0x03) << 5) | GetNextByte(context, homeContext);
            ExecuteSend(selectorIndex, numArgs, context, homeContext);
            return false;
        }
        
        // Send literal selector with 0-7 arguments
        if (bytecode >= 0xE0 && bytecode <= 0xEF)
        {
            var numArgs = bytecode & 0x07;
            var selectorIndex = GetNextByte(context, homeContext);
            ExecuteSend(selectorIndex, numArgs, context, homeContext);
            return false;
        }
        
        // Jump bytecodes
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            var offset = (bytecode & 0x07) + 1;
            context.InstructionPointer = SmallInteger.From(
                context.InstructionPointer.Value + offset);
            return false;
        }
        
        // Conditional jump on false
        if (bytecode >= 0x98 && bytecode <= 0x9F)
        {
            var offset = (bytecode & 0x07) + 1;
            var condition = context.Pop();
            if (condition == SFalse.Instance)
            {
                context.InstructionPointer = SmallInteger.From(
                    context.InstructionPointer.Value + offset);
            }
            return false;
        }
        
        // Long jumps and conditional jumps (0xA0-0xAF)
        if (bytecode >= 0xA0 && bytecode <= 0xAF)
        {
            return ExecuteLongJump(bytecode, context, homeContext);
        }
        
        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not implemented");
    }
    
    /// <summary>
    /// Gets the next byte from the bytecode stream.
    /// </summary>
    private byte GetNextByte(dynamic context, SMethodContext homeContext)
    {
        var ip = context.InstructionPointer.Value;
        var bytecodes = homeContext.Method.Bytecodes;
        
        if (ip >= bytecodes.Length)
        {
            throw new InvalidOperationException("Bytecode stream exhausted");
        }
        
        var nextByte = bytecodes[ip];
        context.InstructionPointer = SmallInteger.From(ip + 1);
        return nextByte;
    }
    
    /// <summary>
    /// Executes extended bytecodes.
    /// </summary>
    private bool ExecuteExtendedBytecode(byte bytecode, dynamic context, SMethodContext homeContext)
    {
        var extendedType = GetNextByte(context, homeContext);
        
        if (bytecode == 0x7E) // Single extended
        {
            var operation = extendedType >> 5;
            var operand = extendedType & 0x1F;
            
            switch (operation)
            {
                case 0: // Extended push receiver variable
                    var receiver = homeContext.Receiver;
                    if (receiver is SInstance instance)
                    {
                        context.Push(instance.GetInstanceVariable(operand) ?? SNil.Instance);
                    }
                    return false;
                    
                case 1: // Extended push temporary
                    context.Push(homeContext.GetTemporary(operand));
                    return false;
                    
                case 2: // Extended push literal
                    var literal = homeContext.Method.GetLiteral(operand);
                    context.Push(literal ?? SNil.Instance);
                    return false;
                    
                case 3: // Extended push literal variable
                    var assoc = homeContext.Method.GetLiteral(operand);
                    if (assoc is SArray arr && arr.Size >= 2)
                    {
                        context.Push(arr.At(1) ?? SNil.Instance);
                    }
                    return false;
                    
                case 4: // Extended store receiver variable
                    var value = context.Top();
                    var rcvr = homeContext.Receiver;
                    if (rcvr is SInstance inst)
                    {
                        inst.SetInstanceVariable(operand, value);
                    }
                    return false;
                    
                case 5: // Extended store temporary
                    homeContext.SetTemporary(operand, context.Top());
                    return false;
                    
                case 6: // Extended store and pop receiver variable
                    var val = context.Pop();
                    var rec = homeContext.Receiver;
                    if (rec is SInstance ins)
                    {
                        ins.SetInstanceVariable(operand, val);
                    }
                    return false;
                    
                case 7: // Extended store and pop temporary
                    homeContext.SetTemporary(operand, context.Pop());
                    return false;
            }
        }
        else if (bytecode == 0x7F) // Double extended
        {
            var operand = GetNextByte(context, homeContext);
            var operation = extendedType >> 5;
            
            switch (operation)
            {
                case 0: // Extended send
                    ExecuteSend(operand, extendedType & 0x1F, context, homeContext);
                    return false;
                    
                case 1: // Extended super send
                    ExecuteSuperSend(operand, extendedType & 0x1F, context, homeContext);
                    return false;
                    
                case 2: // Extended push receiver variable
                    var receiver = homeContext.Receiver;
                    if (receiver is SInstance instance)
                    {
                        context.Push(instance.GetInstanceVariable(operand) ?? SNil.Instance);
                    }
                    return false;
                    
                case 3: // Extended push literal
                    var literal = homeContext.Method.GetLiteral(operand);
                    context.Push(literal ?? SNil.Instance);
                    return false;
                    
                case 4: // Extended push literal variable
                    var assoc = homeContext.Method.GetLiteral(operand);
                    if (assoc is SArray arr && arr.Size >= 2)
                    {
                        context.Push(arr.At(1) ?? SNil.Instance);
                    }
                    return false;
                    
                case 5: // Extended store receiver variable
                    var value = context.Top();
                    var rcvr = homeContext.Receiver;
                    if (rcvr is SInstance inst)
                    {
                        inst.SetInstanceVariable(operand, value);
                    }
                    return false;
                    
                case 6: // Extended store and pop receiver variable
                    var val = context.Pop();
                    var rec = homeContext.Receiver;
                    if (rec is SInstance ins)
                    {
                        ins.SetInstanceVariable(operand, val);
                    }
                    return false;
                    
                case 7: // Extended store literal variable
                    var assocObj = homeContext.Method.GetLiteral(operand);
                    if (assocObj is SArray ar && ar.Size >= 2)
                    {
                        ar.AtPut(1, context.Top());
                    }
                    return false;
            }
        }
        
        throw new NotImplementedException($"Extended bytecode 0x{bytecode:X2} {extendedType:X2} not implemented");
    }
    
    /// <summary>
    /// Executes long jump bytecodes.
    /// </summary>
    private bool ExecuteLongJump(byte bytecode, dynamic context, SMethodContext homeContext)
    {
        var type = (bytecode >> 2) & 0x03;
        var offsetHigh = (bytecode & 0x03) << 8;
        var offsetLow = GetNextByte(context, homeContext);
        var offset = offsetHigh | offsetLow;
        
        switch (type)
        {
            case 0: // Long unconditional jump
                context.InstructionPointer = SmallInteger.From(
                    context.InstructionPointer.Value + offset);
                return false;
                
            case 1: // Long conditional jump on true
                var condTrue = context.Pop();
                if (condTrue == STrue.Instance)
                {
                    context.InstructionPointer = SmallInteger.From(
                        context.InstructionPointer.Value + offset);
                }
                return false;
                
            case 2: // Long conditional jump on false
                var condFalse = context.Pop();
                if (condFalse == SFalse.Instance)
                {
                    context.InstructionPointer = SmallInteger.From(
                        context.InstructionPointer.Value + offset);
                }
                return false;
                
            case 3: // Long extended operations
                // This would include block operations
                return ExecuteBlockOperation(offsetLow, context, homeContext);
        }
        
        return false;
    }
    
    /// <summary>
    /// Executes block-related operations.
    /// </summary>
    private bool ExecuteBlockOperation(byte operand, dynamic context, SMethodContext homeContext)
    {
        // Block creation and execution would go here
        // For now, just a placeholder
        throw new NotImplementedException("Block operations not yet implemented");
    }
    
    /// <summary>
    /// Executes arithmetic send bytecodes.
    /// </summary>
    private void ExecuteSendArithmetic(byte bytecode, dynamic context)
    {
        var selector = (bytecode & 0x0F) switch
        {
            0x00 => "+",
            0x01 => "-",
            0x02 => "<",
            0x03 => ">",
            0x04 => "<=",
            0x05 => ">=",
            0x06 => "=",
            0x07 => "~=",
            0x08 => "*",
            0x09 => "/",
            0x0A => "\\\\",
            0x0B => "@",
            0x0C => "bitShift:",
            0x0D => "//",
            0x0E => "bitAnd:",
            0x0F => "bitOr:",
            _ => throw new InvalidOperationException($"Unknown arithmetic bytecode: 0x{bytecode:X2}")
        };
        
        var numArgs = selector.EndsWith(':') ? 1 : (selector == "@" ? 1 : 1);
        var args = new SObject[numArgs];
        
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }
        
        var receiver = context.Pop();
        var result = Send(selector, receiver, args);
        context.Push(result);
    }
    
    /// <summary>
    /// Executes special send bytecodes.
    /// </summary>
    private void ExecuteSendSpecial(byte bytecode, dynamic context)
    {
        var selectorIndex = bytecode & 0x0F;
        var specialSelectors = new[]
        {
            "at:", "at:put:", "size", "next", "nextPut:", "atEnd",
            "==", "class", "blockCopy:", "value", "value:", "do:",
            "new", "new:", "x", "y"
        };
        
        if (selectorIndex >= specialSelectors.Length)
        {
            throw new InvalidOperationException($"Invalid special selector index: {selectorIndex}");
        }
        
        var selector = specialSelectors[selectorIndex];
        var numArgs = selector.Count(c => c == ':');
        var args = new SObject[numArgs];
        
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }
        
        var receiver = context.Pop();
        var result = Send(selector, receiver, args);
        context.Push(result);
    }
    
    /// <summary>
    /// Executes a general send operation.
    /// </summary>
    private void ExecuteSend(int selectorIndex, int numArgs, dynamic context, SMethodContext homeContext)
    {
        var selector = homeContext.Method.GetLiteral(selectorIndex);
        if (selector == null)
        {
            throw new InvalidOperationException($"No selector at index {selectorIndex}");
        }
        
        var args = new SObject[numArgs];
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }
        
        var receiver = context.Pop();
        var selectorString = selector switch
        {
            SSymbol sym => sym.Value,
            SString str => str.Value,
            _ => selector.ToString() ?? "unknown"
        };
        
        var result = Send(selectorString, receiver, args);
        context.Push(result);
    }
    
    /// <summary>
    /// Executes a super send operation.
    /// </summary>
    private void ExecuteSuperSend(int selectorIndex, int numArgs, dynamic context, SMethodContext homeContext)
    {
        var selector = homeContext.Method.GetLiteral(selectorIndex);
        if (selector == null)
        {
            throw new InvalidOperationException($"No selector at index {selectorIndex}");
        }
        
        var args = new SObject[numArgs];
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = context.Pop();
        }
        
        var receiver = context.Pop();
        var selectorString = selector switch
        {
            SSymbol sym => sym.Value,
            SString str => str.Value,
            _ => selector.ToString() ?? "unknown"
        };
        
        // For super send, start lookup in superclass
        var startClass = homeContext.Method.MethodClass?.Superclass;
        var result = SendToClass(selectorString, receiver, args, startClass);
        context.Push(result);
    }
    
    /// <summary>
    /// Performs a message send.
    /// </summary>
    private SObject Send(string selector, SObject receiver, SObject[] arguments)
    {
        return SendToClass(selector, receiver, arguments, receiver.Class);
    }
    
    /// <summary>
    /// Performs a message send starting from a specific class.
    /// </summary>
    private SObject SendToClass(string selector, SObject receiver, SObject[] arguments, SClass? startClass)
    {
        if (startClass == null)
        {
            throw new InvalidOperationException($"Cannot send {selector} to object with no class");
        }
        
        // Look up the method
        var method = startClass.LookupMethod(selector);
        if (method == null)
        {
            // Method not found - should send doesNotUnderstand:
            logger?.LogWarning($"Method not found: {selector} in {startClass.Name}");
            throw new InvalidOperationException($"Method not found: {selector}");
        }
        
        // Execute the method
        return Execute(method, receiver, arguments);
    }
    
    /// <summary>
    /// Gets a human-readable name for a bytecode.
    /// </summary>
    private string GetBytecodeName(byte bytecode)
    {
        return bytecode switch
        {
            <= 0x0F => $"pushReceiverVariable:{bytecode & 0x0F}",
            >= 0x10 and <= 0x1F => $"pushTemporary:{bytecode & 0x0F}",
            >= 0x20 and <= 0x3F => $"pushLiteral:{bytecode & 0x1F}",
            >= 0x40 and <= 0x5F => $"pushLiteralVariable:{bytecode & 0x1F}",
            >= 0x60 and <= 0x67 => $"popIntoReceiverVariable:{bytecode & 0x07}",
            >= 0x68 and <= 0x6F => $"popIntoTemporary:{bytecode & 0x07}",
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
            0x7C => "returnTop",
            0x7D => "returnTopFromBlock",
            0x80 => "extendedPush",
            0x81 => "extendedStore",
            0x82 => "extendedStoreAndPop",
            0x83 => "singleExtendedSend",
            0x84 => "doubleExtendedDoAnything",
            0x85 => "singleExtendedSuperSend",
            0x86 => "secondExtendedSend",
            0x87 => "pop",
            0x88 => "duplicateTop",
            0x89 => "pushActiveContext",
            >= 0x90 and <= 0x97 => $"shortJump:{(bytecode & 0x07) + 1}",
            >= 0x98 and <= 0x9F => $"shortConditionalJump:{(bytecode & 0x07) + 1}",
            >= 0xA0 and <= 0xA7 => $"longJump:{(bytecode & 0x07) * 256}",
            >= 0xA8 and <= 0xAB => $"longConditionalJump:{(bytecode & 0x03) * 256}",
            >= 0xB0 and <= 0xBF => $"sendArithmetic:{GetArithmeticSelectorName(bytecode)}",
            >= 0xC0 and <= 0xCF => $"sendSpecial:{bytecode & 0x0F}",
            >= 0xD0 and <= 0xDF => $"sendLiteral:{bytecode & 0x0F}",
            >= 0xE0 and <= 0xEF => $"sendLiteralWith0Args:{bytecode & 0x0F}",
            >= 0xF0 and <= 0xFF => $"sendLiteralWith1Arg:{bytecode & 0x0F}",
            _ => $"unknown:0x{bytecode:X2}"
        };
    }
    
    private string GetArithmeticSelectorName(byte bytecode)
    {
        var index = bytecode & 0x0F;
        return index switch
        {
            0x00 => "+",
            0x01 => "-",
            0x02 => "<",
            0x03 => ">",
            0x04 => "<=",
            0x05 => ">=",
            0x06 => "=",
            0x07 => "~=",
            0x08 => "*",
            0x09 => "/",
            0x0A => "\\\\",
            0x0B => "@",
            0x0C => "bitShift:",
            0x0D => "//",
            0x0E => "bitAnd:",
            0x0F => "bitOr:",
            _ => $"unknown:{index}"
        };
    }
}