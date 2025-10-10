namespace Smalltalk80.Interpreter.Execution;

using System;
using System.Linq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Blue Book compliant context-based bytecode interpreter using LayoutManagement.
/// This interpreter uses MethodContext and BlockContext objects for execution,
/// not C# stacks, making it fully compliant with the Smalltalk-80 specification.
///
/// Rewritten to use LayoutManagement.Layout instead of ST* structs.
/// </summary>
public sealed class Interpreter
{
    private SObjectRef activeContext = SObjectRef.Nil;
    private readonly ILogger<Interpreter>? logger;
    private bool tracingEnabled = false;
    private int traceIndentLevel = 0;
    private GarbageCollector? garbageCollector;
    private readonly PrimitiveTable primitiveTable;
    private readonly InterpreterPrimitiveContext primitiveContext;

    // Cached references for performance
    private static SObjectRef? cachedTrueRef;
    private static SObjectRef? cachedFalseRef;

    /// <summary>
    /// Gets the current active context (supports thisContext).
    /// </summary>
    public SObjectRef ActiveContext => activeContext;

    /// <summary>
    /// Enables or disables execution tracing.
    /// Checks DebugOptions if not explicitly set.
    /// </summary>
    public bool TracingEnabled
    {
        get => tracingEnabled || DebugOptions.EnableMethodTrace || DebugOptions.EnableBytecodeTrace;
        set => tracingEnabled = value;
    }

    /// <summary>
    /// Gets or sets the garbage collector for primitive 133.
    /// </summary>
    public GarbageCollector? GarbageCollector
    {
        get => garbageCollector;
        set => garbageCollector = value;
    }

    /// <summary>
    /// Initializes a new instance of the Interpreter.
    /// </summary>
    public Interpreter(ILogger<Interpreter>? logger = null)
    {
        this.logger = logger;
        this.primitiveTable = new PrimitiveTable();
        this.primitiveContext = new InterpreterPrimitiveContext(this);

        // Register all primitives
        PrimitiveRegistry.RegisterAll(primitiveTable);
    }

    /// <summary>
    /// Executes a method in a new method context.
    /// </summary>
    /// <param name="methodRef">The compiled method to execute.</param>
    /// <param name="receiverRef">The receiver of the message.</param>
    /// <param name="argumentRefs">The arguments to the method.</param>
    /// <returns>The result of the method execution.</returns>
    public SObjectRef Execute(SObjectRef methodRef, SObjectRef receiverRef, SObjectRef[] argumentRefs)
    {
        if (methodRef.IsNil)
            throw new ArgumentException("Method cannot be nil");
        if (receiverRef.IsNil)
            throw new ArgumentException("Receiver cannot be nil");
        if (argumentRefs == null)
            throw new ArgumentNullException(nameof(argumentRefs));

        if (TracingEnabled)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            var selector = ObjectMemory.GetCompiledMethodSelector(methodRef.AsObjectPointer());
            Console.WriteLine($"{indent}==> EXECUTE SELECTOR: {selector}");
            Console.WriteLine($"{indent}    Receiver: {FormatRef(receiverRef)}");
            Console.WriteLine($"{indent}    Arguments: [{string.Join(", ", argumentRefs.Select(FormatRef))}]");
            traceIndentLevel++;
        }

        try
        {
            // Create new method context
            var newContext = CreateMethodContext(methodRef, receiverRef, SObjectRef.Nil, argumentRefs);

            // Set as active context
            activeContext = newContext;

            // Execute bytecode loop
            var result = InterpretLoop();

            return result;
        }
        finally
        {
            if (TracingEnabled)
            {
                traceIndentLevel--;
            }
        }
    }

    /// <summary>
    /// Creates a new MethodContext for method execution.
    /// </summary>
    private SObjectRef CreateMethodContext(SObjectRef methodRef, SObjectRef receiverRef, SObjectRef senderRef, SObjectRef[] arguments)
    {
        var methodOop = methodRef.AsObjectPointer();
        // TODO: Get MethodContext class from SystemDictionary or class registry
        // For now, allocate without class (will need fixing for full implementation)
        var methodClass = SObjectRef.Nil;

        // Get method info
        var numArgs = ObjectMemory.GetCompiledMethodArgumentCount(methodOop);
        var numTemps = ObjectMemory.GetCompiledMethodTemporaryCount(methodOop);

        // Calculate stack size: instance vars + (args + temps) + extra stack space
        var stackSize = LayoutManagement.Layout.MethodContext_InstanceVarCount + numArgs + numTemps + 32;

        // Allocate MethodContext
        var contextOop = ObjectMemory.AllocateObject(
            methodClass,
            ObjectFormat.PointerIndexed,
            LayoutManagement.Layout.MethodContext_InstanceVarCount,
            stackSize - LayoutManagement.Layout.MethodContext_InstanceVarCount
        );

        // Initialize MethodContext fields
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Sender, senderRef);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer, SObjectRef.FromSmallInteger(0));
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(LayoutManagement.Layout.MethodContext_InstanceVarCount + numArgs + numTemps));
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Method, methodRef);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver, receiverRef);

        // Push arguments onto stack
        for (int i = 0; i < arguments.Length && i < numArgs; i++)
        {
            ObjectMemory.SetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = arguments.Length; i < numArgs + numTemps; i++)
        {
            ObjectMemory.SetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + i, SObjectRef.Nil);
        }

        return SObjectRef.FromObjectPointer(contextOop);
    }

    /// <summary>
    /// Main bytecode interpretation loop.
    /// </summary>
    private SObjectRef InterpretLoop()
    {
        const int MAX_ITERATIONS = 1000000; // Safety limit
        int iterations = 0;

        while (!activeContext.IsNil)
        {
            if (iterations++ > MAX_ITERATIONS)
                throw new InvalidOperationException("Interpretation exceeded maximum iteration count");

            var contextOop = activeContext.AsObjectPointer();

            // Get current instruction pointer
            var ipRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer);
            if (!ipRef.IsSmallInteger)
                throw new InvalidOperationException("Instruction pointer must be a SmallInteger");

            var ip = ipRef.AsSmallInteger();

            // Get method
            var methodRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Method);
            if (methodRef.IsNil)
                throw new InvalidOperationException("Method context has nil method");

            var methodOop = methodRef.AsObjectPointer();
            var bytecodes = ObjectMemory.GetCompiledMethodBytecodes(methodOop);

            // Check for method return
            if (ip >= bytecodes.Length)
            {
                // Method finished without explicit return - return receiver
                var receiver = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver);
                return receiver;
            }

            // Fetch and decode bytecode
            var bytecode = bytecodes[ip];

            // Increment IP before execution (some bytecodes may override this)
            ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer,
                SObjectRef.FromSmallInteger(ip + 1));

            if (TracingEnabled && DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  [{ip:D4}] Bytecode: {bytecode:X2}");
            }

            // Execute bytecode
            var shouldContinue = ExecuteBytecode(bytecode, bytecodes, ref ip);
            if (!shouldContinue)
            {
                // Method returned - pop context and return value
                var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
                var sp = spRef.AsSmallInteger();
                if (sp > LayoutManagement.Layout.MethodContext_InstanceVarCount)
                {
                    return ObjectMemory.GetIndexedPointer(contextOop, sp - 1);
                }
                return SObjectRef.Nil;
            }
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false to return from method.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, byte[] bytecodes, ref int ip)
    {
        var contextOop = activeContext.AsObjectPointer();
        var methodRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Method);
        var methodOop = methodRef.AsObjectPointer();
        var receiverRef = GetSelf();

        // Push receiver variable 0-15 (0x00-0x0F)
        if (BytecodeUtil.IsPushIvar(bytecode))
        {
            var index = BytecodeUtil.PushIvarIndex(bytecode);
            if (receiverRef.IsHeapObject)
            {
                var instVar = ObjectMemory.GetInstanceVariable(receiverRef.AsObjectPointer(), index);
                Push(instVar);
            }
            else
            {
                Push(SObjectRef.Nil);
            }
            return true;
        }

        // Push temporary variable 0-15 (0x10-0x1F)
        if (BytecodeUtil.IsPushTemp(bytecode))
        {
            var index = BytecodeUtil.PushTempIndex(bytecode);
            var value = ObjectMemory.GetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + index);
            Push(value);
            return true;
        }

        // Push literal constant 0-31 (0x20-0x3F)
        // FIXED: Add +1 to skip header at literal[0]
        if (BytecodeUtil.IsPushLiteralConst(bytecode))
        {
            var index = BytecodeUtil.PushLiteralConstIndex(bytecode);
            var literal = ObjectMemory.GetCompiledMethodLiteral(methodOop, index + 1); // +1 to skip header
            Push(literal);
            return true;
        }

        // Push literal variable 0-31 (0x40-0x5F)
        // FIXED: Add +1 to skip header at literal[0]
        if (BytecodeUtil.IsPushLiteralVar(bytecode))
        {
            var index = BytecodeUtil.PushLiteralVarIndex(bytecode);
            var associationRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, index + 1); // +1 to skip header

            // Association is a 2-element object: [key, value]
            // Get the value at instance variable index 1
            if (!associationRef.IsNil && associationRef.IsHeapObject)
            {
                var assocOop = associationRef.AsObjectPointer();
                // Assuming Association has layout: [key at index 0, value at index 1]
                var value = ObjectMemory.GetInstanceVariable(assocOop, 1);
                Push(value);
            }
            else
            {
                Push(SObjectRef.Nil);
            }
            return true;
        }

        // Pop and store receiver variable 0-7 (0x60-0x67)
        if (BytecodeUtil.IsPopStoreIvar(bytecode))
        {
            var index = BytecodeUtil.PopStoreIvarIndex(bytecode);
            var value = Pop();

            if (receiverRef.IsHeapObject)
            {
                ObjectMemory.SetInstanceVariable(receiverRef.AsObjectPointer(), index, value);
            }
            return true;
        }

        // Pop and store temporary variable 0-7 (0x68-0x6F)
        if (BytecodeUtil.IsPopStoreTemp(bytecode))
        {
            var index = BytecodeUtil.PopStoreTempIndex(bytecode);
            var value = Pop();
            ObjectMemory.SetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + index, value);
            return true;
        }

        // Push special constants & returns (0x70-0x7F)
        switch ((Bytecode)bytecode)
        {
            case Bytecode.PUSH_SELF:
                Push(receiverRef);
                return true;

            case Bytecode.PUSH_TRUE:
                Push(GetTrue());
                return true;

            case Bytecode.PUSH_FALSE:
                Push(GetFalse());
                return true;

            case Bytecode.PUSH_NIL:
                Push(SObjectRef.Nil);
                return true;

            case Bytecode.PUSH_MINUS_ONE:
                Push(SObjectRef.FromSmallInteger(-1));
                return true;

            case Bytecode.PUSH_ZERO:
                Push(SObjectRef.FromSmallInteger(0));
                return true;

            case Bytecode.PUSH_ONE:
                Push(SObjectRef.FromSmallInteger(1));
                return true;

            case Bytecode.PUSH_TWO:
                Push(SObjectRef.FromSmallInteger(2));
                return true;

            case Bytecode.RETURN_SELF:
                ReturnValueTo(receiverRef, GetSender());
                return false;

            case Bytecode.RETURN_TRUE:
                ReturnValueTo(GetTrue(), GetSender());
                return false;

            case Bytecode.RETURN_FALSE:
                ReturnValueTo(GetFalse(), GetSender());
                return false;

            case Bytecode.RETURN_NIL:
                ReturnValueTo(SObjectRef.Nil, GetSender());
                return false;

            case Bytecode.RETURN_TOP:
                var topValue = Pop();
                ReturnValueTo(topValue, GetSender());
                return false;

            case (Bytecode)0x87: // POP
                Pop();
                return true;

            case (Bytecode)0x88: // DUP
                var top = Pop();
                Push(top);
                Push(top);
                return true;

            case (Bytecode)0x89: // PUSH_ACTIVE_CONTEXT
                Push(activeContext);
                return true;
        }

        // Unconditional jumps (0x90-0x97)
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            var offset = (bytecode & 0x07) + 1;
            var ipRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer);
            var currentIp = ipRef.AsSmallInteger();
            ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer,
                SObjectRef.FromSmallInteger(currentIp + offset));
            return true;
        }

        // Conditional jump on false (0x98-0x9F)
        if (bytecode >= 0x98 && bytecode <= 0x9F)
        {
            var offset = (bytecode & 0x07) + 1;
            var condition = Pop();
            if (condition.Equals(GetFalse()))
            {
                var ipRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer);
                var currentIp = ipRef.AsSmallInteger();
                ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer,
                    SObjectRef.FromSmallInteger(currentIp + offset));
            }
            return true;
        }

        // Send arithmetic messages (0xB0-0xBF)
        if (BytecodeUtil.IsSendArithmetic(bytecode))
        {
            ExecuteSendArithmetic(bytecode);
            return true;
        }

        // Send special messages (0xC0-0xCF)
        if (BytecodeUtil.IsSendSpecial(bytecode))
        {
            ExecuteSendSpecial(bytecode);
            return true;
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        // FIXED: Add +1 to skip header at literal[0]
        if (BytecodeUtil.IsSend0(bytecode))
        {
            var selectorIndex = BytecodeUtil.Send0Index(bytecode);
            var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex + 1); // +1 to skip header
            var selector = ObjectMemory.SymbolToString(selectorRef);

            var receiver = Pop();
            var result = SendMessage(receiver, selector, Array.Empty<SObjectRef>());
            Push(result);
            return true;
        }

        // Send literal selector with 1 arg (0xE0-0xEF)
        // FIXED: Add +1 to skip header at literal[0]
        if (BytecodeUtil.IsSend1(bytecode))
        {
            var selectorIndex = BytecodeUtil.Send1Index(bytecode);
            var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex + 1); // +1 to skip header
            var selector = ObjectMemory.SymbolToString(selectorRef);

            var arg = Pop();
            var receiver = Pop();
            var result = SendMessage(receiver, selector, new[] { arg });
            Push(result);
            return true;
        }

        // Send literal selector with 2 args (0xF0-0xFF)
        // FIXED: Add +1 to skip header at literal[0]
        if (BytecodeUtil.IsSend2(bytecode))
        {
            var selectorIndex = BytecodeUtil.Send2Index(bytecode);
            var selectorRef = ObjectMemory.GetCompiledMethodLiteral(methodOop, selectorIndex + 1); // +1 to skip header
            var selector = ObjectMemory.SymbolToString(selectorRef);

            var arg2 = Pop();
            var arg1 = Pop();
            var receiver = Pop();
            var result = SendMessage(receiver, selector, new[] { arg1, arg2 });
            Push(result);
            return true;
        }

        // TODO: Extended bytecodes (0x80-0x86, 0x8A-0x8F, 0xA0-0xAF)
        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not yet implemented");
    }

    // Stack manipulation helpers

    private void Push(SObjectRef value)
    {
        var contextOop = activeContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        ObjectMemory.SetIndexedPointer(contextOop, sp, value);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp + 1));
    }

    private SObjectRef Pop()
    {
        var contextOop = activeContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        if (sp <= LayoutManagement.Layout.MethodContext_InstanceVarCount)
            throw new InvalidOperationException("Stack underflow");

        var value = ObjectMemory.GetIndexedPointer(contextOop, sp - 1);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp - 1));

        return value;
    }

    private void PushReceiverVariable(int index)
    {
        var receiver = GetSelf();
        var receiverOop = receiver.AsObjectPointer();
        var value = ObjectMemory.GetInstanceVariable(receiverOop, index);
        Push(value);
    }

    private void PushTemporaryVariable(int index)
    {
        var contextOop = activeContext.AsObjectPointer();
        var value = ObjectMemory.GetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + index);
        Push(value);
    }

    private void PushSelf()
    {
        Push(GetSelf());
    }

    private SObjectRef GetSelf()
    {
        var contextOop = activeContext.AsObjectPointer();
        return ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver);
    }

    private SObjectRef GetSender()
    {
        var contextOop = activeContext.AsObjectPointer();
        return ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Sender);
    }

    private void ReturnValueTo(SObjectRef value, SObjectRef targetContext)
    {
        if (targetContext.IsNil)
        {
            // Returning to nil sender means we're done
            activeContext = SObjectRef.Nil;
            return;
        }

        // Push value onto target context's stack
        var targetOop = targetContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(targetOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        ObjectMemory.SetIndexedPointer(targetOop, sp, value);
        ObjectMemory.SetInstanceVariable(targetOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp + 1));

        // Switch to target context
        activeContext = targetContext;
    }

    private SObjectRef GetTrue()
    {
        if (!cachedTrueRef.HasValue)
        {
            // TODO: Get true from special objects array or SystemDictionary
            // For now, return nil as placeholder
            cachedTrueRef = SObjectRef.Nil;
        }
        return cachedTrueRef ?? SObjectRef.Nil;
    }

    private SObjectRef GetFalse()
    {
        if (!cachedFalseRef.HasValue)
        {
            // TODO: Get false from special objects array or SystemDictionary
            // For now, return nil as placeholder
            cachedFalseRef = SObjectRef.Nil;
        }
        return cachedFalseRef ?? SObjectRef.Nil;
    }

    // Message send helpers

    private void ExecuteSendArithmetic(byte bytecode)
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

        var numArgs = selector.Count(c => c == ':');
        if (numArgs == 0 && selector != "@") numArgs = 1; // Binary selectors have 1 arg

        var args = new SObjectRef[numArgs];
        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        var receiver = Pop();

        // Try primitive arithmetic for SmallIntegers
        if (receiver.IsSmallInteger && numArgs > 0 && args[0].IsSmallInteger)
        {
            var result = ExecuteIntegerArithmetic(receiver, selector, args[0]);
            if (!result.IsNil || selector == "=" || selector == "~=" || selector == "<" || selector == ">" || selector == "<=" || selector == ">=")
            {
                Push(result);
                return;
            }
        }

        // Fall back to message send
        var sendResult = SendMessage(receiver, selector, args);
        Push(sendResult);
    }

    private void ExecuteSendSpecial(byte bytecode)
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
        var args = new SObjectRef[numArgs];

        for (int i = numArgs - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }

        var receiver = Pop();

        // Handle special selectors with optimizations
        switch (selector)
        {
            case "==":
                // Identity comparison
                var isIdentical = numArgs > 0 && receiver.Equals(args[0]);
                Push(isIdentical ? GetTrue() : GetFalse());
                return;

            case "class":
                var classRef = receiver.IsSmallInteger ? SObjectRef.Nil :
                               receiver.IsNil ? SObjectRef.Nil :
                               ObjectMemory.GetClass(receiver.AsObjectPointer());
                Push(classRef);
                return;

            case "size":
                if (receiver.IsHeapObject)
                {
                    var size = ObjectMemory.GetIndexedSize(receiver.AsObjectPointer());
                    Push(SObjectRef.FromSmallInteger(size));
                    return;
                }
                break;
        }

        // Fall back to message send
        var result = SendMessage(receiver, selector, args);
        Push(result);
    }

    private SObjectRef ExecuteIntegerArithmetic(SObjectRef receiver, string selector, SObjectRef arg)
    {
        if (!receiver.IsSmallInteger || !arg.IsSmallInteger)
            return SObjectRef.Nil;

        var a = receiver.AsSmallInteger();
        var b = arg.AsSmallInteger();

        try
        {
            return selector switch
            {
                "+" => SObjectRef.FromSmallInteger(a + b),
                "-" => SObjectRef.FromSmallInteger(a - b),
                "*" => SObjectRef.FromSmallInteger(a * b),
                "/" => b != 0 ? SObjectRef.FromSmallInteger(a / b) : SObjectRef.Nil,
                "\\\\" => b != 0 ? SObjectRef.FromSmallInteger(a % b) : SObjectRef.Nil,
                "//" => b != 0 ? SObjectRef.FromSmallInteger(a / b) : SObjectRef.Nil,
                "<" => a < b ? GetTrue() : GetFalse(),
                ">" => a > b ? GetTrue() : GetFalse(),
                "<=" => a <= b ? GetTrue() : GetFalse(),
                ">=" => a >= b ? GetTrue() : GetFalse(),
                "=" => a == b ? GetTrue() : GetFalse(),
                "~=" => a != b ? GetTrue() : GetFalse(),
                "bitAnd:" => SObjectRef.FromSmallInteger(a & b),
                "bitOr:" => SObjectRef.FromSmallInteger(a | b),
                "bitShift:" => b >= 0 ? SObjectRef.FromSmallInteger(a << b) : SObjectRef.FromSmallInteger(a >> (-b)),
                _ => SObjectRef.Nil
            };
        }
        catch
        {
            return SObjectRef.Nil;
        }
    }

    private SObjectRef SendMessage(SObjectRef receiverRef, string selector, SObjectRef[] args)
    {
        // Look up receiver's class
        nint classOop;
        if (receiverRef.IsSmallInteger)
        {
            // TODO: Get SmallInteger class from special objects or SystemDictionary
            throw new NotImplementedException("SmallInteger class lookup not yet implemented");
        }
        else if (receiverRef.IsNil)
        {
            // TODO: Get UndefinedObject class from special objects or SystemDictionary
            throw new NotImplementedException("UndefinedObject class lookup not yet implemented");
        }
        else if (receiverRef.IsHeapObject)
        {
            var receiverClass = ObjectMemory.GetClass(receiverRef.AsObjectPointer());
            classOop = receiverClass.AsObjectPointer();
        }
        else
        {
            throw new InvalidOperationException($"Cannot send message to {receiverRef}");
        }

        // Look up method
        var methodRef = ObjectMemory.LookupMethod(classOop, selector);
        if (methodRef.IsNil)
        {
            throw new InvalidOperationException($"Method not found: {selector}");
        }

        // TODO: Check for primitives and execute them

        // Execute method in new context
        return Execute(methodRef, receiverRef, args);
    }

    // Formatting helpers

    private string FormatRef(SObjectRef objRef)
    {
        if (objRef.IsNil)
            return "nil";
        if (objRef.IsSmallInteger)
            return objRef.AsSmallInteger().ToString();
        if (objRef.IsHeapObject)
        {
            var className = ObjectMemory.GetClassName(objRef.AsObjectPointer());
            return $"{className}@{objRef.AsObjectPointer():X}";
        }
        return objRef.ToString() ?? "?";
    }

    /// <summary>
    /// Creates a block context (stub for now).
    /// </summary>
    public SObjectRef CreateBlockContext(int argumentCount)
    {
        throw new NotImplementedException("CreateBlockContext needs full implementation");
    }

    /// <summary>
    /// Activates a block context (stub for now).
    /// </summary>
    public SObjectRef ActivateBlockContext(SObjectRef blockRef, SObjectRef[] arguments)
    {
        throw new NotImplementedException("ActivateBlockContext needs full implementation");
    }
}
