namespace Smalltalk80.Interpreter.Execution;

using System.Linq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Blue Book compliant context-based bytecode interpreter using SObjectRef only.
/// This interpreter uses MethodContext and BlockContext objects for execution,
/// not C# stacks, making it fully compliant with the Smalltalk-80 specification.
///
/// This is the NEW implementation that works entirely with SObjectRef and ObjectMemoryNew,
/// eliminating all dependencies on SObject classes.
/// </summary>
public sealed class ContextBasedInterpreterNew
{
    private SObjectRef activeContext = SObjectRef.Nil;
    private readonly ILogger<ContextBasedInterpreterNew>? logger;
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
    /// Initializes a new instance of the ContextBasedInterpreterNew.
    /// </summary>
    public ContextBasedInterpreterNew(ILogger<ContextBasedInterpreterNew>? logger = null)
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
            var selector = ObjectMemoryNew.GetCompiledMethodSelector(methodRef.AsObjectPointer());
            Console.WriteLine($"{indent}==> EXECUTE SELECTOR: {selector}");
            Console.WriteLine($"{indent}    Receiver: {FormatRef(receiverRef)}");
            Console.WriteLine($"{indent}    Arguments: [{string.Join(", ", argumentRefs.Select(FormatRef))}]");

            var bytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(methodRef.AsObjectPointer());
            Console.WriteLine($"{indent}    Bytecodes: {bytecodes.Length} bytes");

            traceIndentLevel++;
        }

        // Create a new method context
        var newContext = CreateMethodContext(methodRef, receiverRef, argumentRefs);

        if (TracingEnabled)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            Console.WriteLine($"{indent}Created MethodContext: {FormatRef(newContext)}");
        }

        // Set the sender if we have an active context
        if (!activeContext.IsNil)
        {
            ObjectMemoryNew.SetContextSender(newContext.AsObjectPointer(), activeContext);
        }

        // Make this the active context
        var previousContext = activeContext;
        activeContext = newContext;

        // Link to process for GC reachability
        UpdateProcessSuspendedContext(newContext);

        try
        {
            // Check for primitive
            var header = ObjectMemoryNew.GetCompiledMethodHeader(methodRef.AsObjectPointer());
            var primitiveIndex = ObjectMemoryNew.CompiledMethodHeader.GetPrimitiveIndex(header);

            if (primitiveIndex > 0)
            {
                if (TracingEnabled)
                {
                    var indent = new string(' ', traceIndentLevel * 2);
                    Console.WriteLine($"{indent}Attempting primitive #{primitiveIndex}...");
                }

                var primitiveResult = ExecutePrimitive(primitiveIndex, receiverRef, argumentRefs);

                if (!primitiveResult.IsNil)
                {
                    // Primitive succeeded
                    if (TracingEnabled)
                    {
                        var indent = new string(' ', traceIndentLevel * 2);
                        Console.WriteLine($"{indent}Primitive succeeded, returning: {FormatRef(primitiveResult)}");
                        traceIndentLevel--;
                    }
                    return primitiveResult;
                }
                // Primitive failed, continue with bytecode execution
                if (TracingEnabled)
                {
                    var indent = new string(' ', traceIndentLevel * 2);
                    Console.WriteLine($"{indent}Primitive failed, executing bytecode...");
                }
            }

            // Execute bytecodes
            if (TracingEnabled)
            {
                var indent = new string(' ', traceIndentLevel * 2);
                Console.WriteLine($"{indent}Executing bytecodes...");
            }

            var result = ExecuteBytecodes();

            if (TracingEnabled)
            {
                traceIndentLevel--;
                var indent = new string(' ', traceIndentLevel * 2);
                var selector = ObjectMemoryNew.GetCompiledMethodSelector(methodRef.AsObjectPointer());
                Console.WriteLine($"{indent}<== RETURN from {selector}: {FormatRef(result)}");
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
    /// Creates a new method context.
    /// </summary>
    private SObjectRef CreateMethodContext(SObjectRef methodRef, SObjectRef receiverRef, SObjectRef[] argumentRefs)
    {
        // Get method details
        var methodOop = methodRef.AsObjectPointer();
        var numTemps = ObjectMemoryNew.GetCompiledMethodTemporaryCount(methodOop);
        var numArgs = ObjectMemoryNew.GetCompiledMethodArgumentCount(methodOop);

        // Calculate indexed size: args + temps + stack
        // MethodContext has 6 instance variables (fixed fields), indexed slots hold temp frame + stack
        var stackSize = 16; // Default stack size
        var indexedSize = numArgs + numTemps + stackSize;

        // Create the context object (PointerIndexed format)
        var contextRef = ObjectMemoryNew.CreateInstance(
            SystemDictionary.MethodContextClassRef.AsObjectPointer(),
            indexedSize);

        var contextOop = contextRef.AsObjectPointer();

        // Initialize fixed instance variables (indices 0-5)
        ObjectMemoryNew.SetContextSender(contextOop, SObjectRef.Nil);
        ObjectMemoryNew.SetContextInstructionPointer(contextOop, 0);
        ObjectMemoryNew.SetContextStackPointer(contextOop, 6 + numArgs + numTemps); // Stack starts after instance vars + temp frame
        ObjectMemoryNew.SetContextMethod(contextOop, methodRef);
        ObjectMemoryNew.SetContextReceiver(contextOop, receiverRef);

        // Copy arguments into temporaries area
        for (int i = 0; i < argumentRefs.Length && i < numArgs; i++)
        {
            ObjectMemoryNew.SetContextTemporary(contextOop, i, argumentRefs[i]);
        }

        // Initialize remaining temporaries to nil
        for (int i = argumentRefs.Length; i < numArgs + numTemps; i++)
        {
            ObjectMemoryNew.SetContextTemporary(contextOop, i, SObjectRef.Nil);
        }

        return contextRef;
    }

    /// <summary>
    /// Executes bytecodes in the current active context.
    /// </summary>
    private SObjectRef ExecuteBytecodes()
    {
        if (activeContext.IsNil)
        {
            throw new InvalidOperationException("No active context");
        }

        var contextOop = activeContext.AsObjectPointer();
        var methodRef = ObjectMemoryNew.GetContextMethod(contextOop);
        var methodOop = methodRef.AsObjectPointer();
        var bytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(methodOop);

        while (true)
        {
            var ip = ObjectMemoryNew.GetContextInstructionPointer(contextOop);

            if (ip >= bytecodes.Length)
            {
                // End of bytecodes without explicit return
                return ObjectMemoryNew.GetContextReceiver(contextOop);
            }

            var bytecode = bytecodes[ip];
            ObjectMemoryNew.SetContextInstructionPointer(contextOop, ip + 1);

            if (DebugOptions.EnableBytecodeTrace || tracingEnabled)
            {
                if (TracingEnabled)
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
            var shouldReturn = ExecuteSingleBytecode(bytecode, contextOop);

            if (shouldReturn)
            {
                // Method or block returned
                var returnValue = ObjectMemoryNew.ContextPop(contextOop);
                if (TracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 1) * 2);
                    Console.WriteLine($"{indent}Returning: {FormatRef(returnValue)}");
                }
                return returnValue;
            }
        }
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// </summary>
    /// <returns>True if the method should return, false to continue.</returns>
    private bool ExecuteSingleBytecode(byte bytecode, nint contextOop)
    {
        var methodRef = ObjectMemoryNew.GetContextMethod(contextOop);
        var methodOop = methodRef.AsObjectPointer();

        // Push receiver variable 0-15
        if (bytecode <= 0x0F)
        {
            var index = bytecode & 0x0F;
            var receiverRef = ObjectMemoryNew.GetContextReceiver(contextOop);

            // Check if receiver has instance variables (not SmallInteger or nil)
            if (receiverRef.IsHeapObject)
            {
                var instVar = ObjectMemoryNew.GetInstanceVariable(receiverRef.AsObjectPointer(), index);
                ObjectMemoryNew.ContextPush(contextOop, instVar);
            }
            else
            {
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.Nil);
            }
            return false;
        }

        // Push temporary variable 0-15
        if (bytecode >= 0x10 && bytecode <= 0x1F)
        {
            var index = bytecode & 0x0F;
            var temp = ObjectMemoryNew.GetContextTemporary(contextOop, index);
            ObjectMemoryNew.ContextPush(contextOop, temp);
            return false;
        }

        // Push literal constant 0-31
        if (bytecode >= 0x20 && bytecode <= 0x3F)
        {
            var index = bytecode & 0x1F;
            var literal = ObjectMemoryNew.GetCompiledMethodLiteral(methodOop, index);
            ObjectMemoryNew.ContextPush(contextOop, literal);
            return false;
        }

        // Push literal variable 0-31
        if (bytecode >= 0x40 && bytecode <= 0x5F)
        {
            var index = bytecode & 0x1F;
            var association = ObjectMemoryNew.GetCompiledMethodLiteral(methodOop, index);

            // Association is an array with key at index 0, value at index 1
            if (!association.IsNil && association.IsHeapObject)
            {
                var assocOop = association.AsObjectPointer();
                var format = ObjectMemoryNew.GetFormat(assocOop);

                if (format == ObjectFormat.PointerIndexed || format == ObjectFormat.FixedPointer)
                {
                    Generated.Layouts.STAssociation assoc = association;
                    var value = assoc.Value;
                    ObjectMemoryNew.ContextPush(contextOop, value);
                }
                else
                {
                    ObjectMemoryNew.ContextPush(contextOop, SObjectRef.Nil);
                }
            }
            else
            {
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.Nil);
            }
            return false;
        }

        // Store and pop receiver variable 0-7
        if (bytecode >= 0x60 && bytecode <= 0x67)
        {
            var index = bytecode & 0x07;
            var value = ObjectMemoryNew.ContextPop(contextOop);
            var receiverRef = ObjectMemoryNew.GetContextReceiver(contextOop);

            if (receiverRef.IsHeapObject)
            {
                ObjectMemoryNew.SetInstanceVariable(receiverRef.AsObjectPointer(), index, value);
            }
            return false;
        }

        // Store and pop temporary variable 0-7
        if (bytecode >= 0x68 && bytecode <= 0x6F)
        {
            var index = bytecode & 0x07;
            var value = ObjectMemoryNew.ContextPop(contextOop);
            ObjectMemoryNew.SetContextTemporary(contextOop, index, value);
            return false;
        }

        // Push special constants
        switch (bytecode)
        {
            case 0x70: // pushReceiver
                var receiver = ObjectMemoryNew.GetContextReceiver(contextOop);
                ObjectMemoryNew.ContextPush(contextOop, receiver);
                return false;

            case 0x71: // pushTrue
                ObjectMemoryNew.ContextPush(contextOop, GetTrueRef());
                return false;

            case 0x72: // pushFalse
                ObjectMemoryNew.ContextPush(contextOop, GetFalseRef());
                return false;

            case 0x73: // pushNil
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.Nil);
                return false;

            case 0x74: // pushMinusOne
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.FromSmallInteger(-1));
                return false;

            case 0x75: // pushZero
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.FromSmallInteger(0));
                return false;

            case 0x76: // pushOne
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.FromSmallInteger(1));
                return false;

            case 0x77: // pushTwo
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.FromSmallInteger(2));
                return false;

            case 0x78: // return receiver
                var rcvr = ObjectMemoryNew.GetContextReceiver(contextOop);
                ObjectMemoryNew.ContextPush(contextOop, rcvr);
                return true;

            case 0x79: // return true
                ObjectMemoryNew.ContextPush(contextOop, GetTrueRef());
                return true;

            case 0x7A: // return false
                ObjectMemoryNew.ContextPush(contextOop, GetFalseRef());
                return true;

            case 0x7B: // return nil
                ObjectMemoryNew.ContextPush(contextOop, SObjectRef.Nil);
                return true;

            case 0x7C: // return top
                // Top is already on stack
                return true;

            case 0x87: // pop
                ObjectMemoryNew.ContextPop(contextOop);
                return false;

            case 0x88: // dup
                var top = ObjectMemoryNew.ContextTop(contextOop);
                ObjectMemoryNew.ContextPush(contextOop, top);
                return false;

            case 0x89: // pushActiveContext (thisContext)
                ObjectMemoryNew.ContextPush(contextOop, activeContext);
                return false;
        }

        // Send arithmetic messages
        if (bytecode >= 0xB0 && bytecode <= 0xBF)
        {
            ExecuteSendArithmetic(bytecode, contextOop);
            return false;
        }

        // Send special messages
        if (bytecode >= 0xC0 && bytecode <= 0xCF)
        {
            ExecuteSendSpecial(bytecode, contextOop);
            return false;
        }

        // Send literal selector with 0 args (0xD0-0xDF)
        if (bytecode >= 0xD0 && bytecode <= 0xDF)
        {
            var selectorIndex = bytecode & 0x0F;
            var selectorRef = ObjectMemoryNew.GetCompiledMethodLiteral(methodOop, selectorIndex);
            var selector = ObjectMemoryNew.SymbolToString(selectorRef);

            // Pop receiver
            var receiver = ObjectMemoryNew.ContextPop(contextOop);

            // Perform message send with 0 arguments
            var result = SendMessage(receiver, selector, Array.Empty<SObjectRef>());
            ObjectMemoryNew.ContextPush(contextOop, result);
            return false;
        }

        // Jump bytecodes
        if (bytecode >= 0x90 && bytecode <= 0x97)
        {
            var offset = (bytecode & 0x07) + 1;
            var ip = ObjectMemoryNew.GetContextInstructionPointer(contextOop);
            ObjectMemoryNew.SetContextInstructionPointer(contextOop, ip + offset);
            return false;
        }

        // Conditional jump on false
        if (bytecode >= 0x98 && bytecode <= 0x9F)
        {
            var offset = (bytecode & 0x07) + 1;
            var condition = ObjectMemoryNew.ContextPop(contextOop);
            if (condition == GetFalseRef())
            {
                var ip = ObjectMemoryNew.GetContextInstructionPointer(contextOop);
                ObjectMemoryNew.SetContextInstructionPointer(contextOop, ip + offset);
            }
            return false;
        }

        throw new NotImplementedException($"Bytecode 0x{bytecode:X2} not implemented");
    }

    /// <summary>
    /// Executes arithmetic send bytecodes.
    /// </summary>
    private void ExecuteSendArithmetic(byte bytecode, nint contextOop)
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
            args[i] = ObjectMemoryNew.ContextPop(contextOop);
        }

        var receiver = ObjectMemoryNew.ContextPop(contextOop);

        // Try primitive arithmetic for SmallIntegers
        if (receiver.IsSmallInteger && numArgs > 0 && args[0].IsSmallInteger)
        {
            var result = ExecuteIntegerArithmetic(receiver, selector, args[0]);
            if (!result.IsNil)
            {
                ObjectMemoryNew.ContextPush(contextOop, result);
                return;
            }
        }

        // Fall back to message send
        var sendResult = Send(selector, receiver, args);
        ObjectMemoryNew.ContextPush(contextOop, sendResult);
    }

    /// <summary>
    /// Executes special send bytecodes.
    /// </summary>
    private void ExecuteSendSpecial(byte bytecode, nint contextOop)
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
            args[i] = ObjectMemoryNew.ContextPop(contextOop);
        }

        var receiver = ObjectMemoryNew.ContextPop(contextOop);

        // Handle special selectors with optimizations
        switch (selector)
        {
            case "==":
                // Identity comparison
                var isIdentical = numArgs > 0 && receiver.Equals(args[0]);
                ObjectMemoryNew.ContextPush(contextOop, isIdentical ? GetTrueRef() : GetFalseRef());
                return;

            case "class":
                var classRef = receiver.GetClassRef();
                ObjectMemoryNew.ContextPush(contextOop, classRef);
                return;

            case "size":
                if (receiver.IsHeapObject)
                {
                    var size = ObjectMemoryNew.GetIndexedSize(receiver.AsObjectPointer());
                    ObjectMemoryNew.ContextPush(contextOop, SObjectRef.FromSmallInteger(size));
                    return;
                }
                break;
        }

        // Fall back to message send
        var result = Send(selector, receiver, args);
        ObjectMemoryNew.ContextPush(contextOop, result);
    }

    /// <summary>
    /// Performs a message send.
    /// </summary>
    private SObjectRef Send(string selector, SObjectRef receiver, SObjectRef[] arguments)
    {
        var classRef = receiver.GetClassRef();
        if (classRef.IsNil)
        {
            throw new InvalidOperationException($"Cannot send {selector} to object with no class");
        }

        // Look up the method
        var methodRef = ObjectMemoryNew.LookupMethod(classRef.AsObjectPointer(), selector);
        if (methodRef.IsNil)
        {
            // Method not found - should send doesNotUnderstand:
            logger?.LogWarning($"Method not found: {selector}");
            throw new InvalidOperationException($"Method not found: {selector}");
        }

        // Execute the method
        return Execute(methodRef, receiver, arguments);
    }

    /// <summary>
    /// Executes primitive integer arithmetic.
    /// </summary>
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
                "<" => a < b ? GetTrueRef() : GetFalseRef(),
                ">" => a > b ? GetTrueRef() : GetFalseRef(),
                "<=" => a <= b ? GetTrueRef() : GetFalseRef(),
                ">=" => a >= b ? GetTrueRef() : GetFalseRef(),
                "=" => a == b ? GetTrueRef() : GetFalseRef(),
                "~=" => a != b ? GetTrueRef() : GetFalseRef(),
                _ => SObjectRef.Nil
            };
        }
        catch
        {
            return SObjectRef.Nil;
        }
    }

    /// <summary>
    /// Executes a primitive using the primitive table.
    /// </summary>
    private SObjectRef ExecutePrimitive(int primitiveIndex, SObjectRef receiver, SObjectRef[] arguments)
    {
        // Special handling for primitive 133 (forceGC) - requires GarbageCollector access
        if (primitiveIndex == 133)
        {
            if (arguments.Length != 0)
                return SObjectRef.Nil; // Failure

            if (TracingEnabled)
            {
                var indent = new string(' ', (traceIndentLevel + 2) * 2);
                Console.WriteLine($"{indent}[GC] Forcing garbage collection...");
            }

            // Trigger GC and report results
            if (garbageCollector != null)
            {
                var stats = garbageCollector.ForceCollection();

                if (TracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 2) * 2);
                    var remaining = ObjectMemoryNew.ObjectTable.Count;
                    Console.WriteLine($"{indent}[GC] Collected {stats.ObjectsCollected} objects, {remaining} remain");
                }
            }

            // Return receiver to indicate success
            return receiver;
        }

        // Use the primitive table to execute other primitives
        var result = primitiveTable.Execute(primitiveIndex, receiver, arguments, primitiveContext);

        if (result.Success)
        {
            return result.Value;
        }

        // Primitive failed - return nil to signal failure
        // This will cause the method's bytecodes to execute
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Sends a message to a receiver by looking up the method and executing it.
    /// </summary>
    private SObjectRef SendMessage(SObjectRef receiverRef, string selector, SObjectRef[] args)
    {
        if (TracingEnabled)
        {
            var indent = new string(' ', (traceIndentLevel + 2) * 2);
            Console.WriteLine($"{indent}[SEND] {selector} to {FormatRef(receiverRef)}");
        }

        // Look up receiver's class
        nint classOop;
        if (receiverRef.IsSmallInteger)
        {
            classOop = SystemDictionary.SmallIntegerClassRef.AsObjectPointer();
        }
        else if (receiverRef.IsNil)
        {
            classOop = SystemDictionary.UndefinedObjectClassRef.AsObjectPointer();
        }
        else if (receiverRef.IsHeapObject)
        {
            var receiverClass = ObjectMemoryNew.GetClass(receiverRef.AsObjectPointer());
            classOop = receiverClass.AsObjectPointer();
        }
        else
        {
            throw new InvalidOperationException($"Cannot send message to {receiverRef}");
        }

        // Look up method
        var methodRef = ObjectMemoryNew.LookupMethod(classOop, selector);
        if (methodRef.IsNil)
        {
            throw new InvalidOperationException($"Method not found: {selector}");
        }

        // Check for primitive (bit 15 of header indicates extended header with primitive)
        var methodOop = methodRef.AsObjectPointer();
        var header = ObjectMemoryNew.GetCompiledMethodHeader(methodOop);
        var hasPrimitive = (header & 0x8000) != 0; // Bit 15 set means extended header

        if (hasPrimitive)
        {
            // Extended header: primitive index is in bytes 2-3 of the bytecode data
            var bytecodeData = ObjectMemoryNew.GetByteIndexedData(methodOop);
            if (bytecodeData.Length >= 4)
            {
                // Bytes 0-1: header, Bytes 2-3: primitive index, Bytes 4+: bytecodes
                var primitiveIndex = bytecodeData[2] | (bytecodeData[3] << 8);

                if (TracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 2) * 2);
                    Console.WriteLine($"{indent}[PRIMITIVE] Trying primitive #{primitiveIndex}");
                }

                var primitiveResult = ExecutePrimitive(primitiveIndex, receiverRef, args);
                if (!primitiveResult.IsNil)
                {
                    if (TracingEnabled)
                    {
                        var indent = new string(' ', (traceIndentLevel + 2) * 2);
                        Console.WriteLine($"{indent}[PRIMITIVE] Succeeded, returning {FormatRef(primitiveResult)}");
                    }
                    return primitiveResult;
                }

                if (TracingEnabled)
                {
                    var indent = new string(' ', (traceIndentLevel + 2) * 2);
                    Console.WriteLine($"{indent}[PRIMITIVE] Failed, executing bytecodes");
                }
            }
        }

        // Execute method in new context
        return Execute(methodRef, receiverRef, args);
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
            0x87 => "pop",
            0x88 => "duplicateTop",
            0x89 => "pushActiveContext",
            >= 0x90 and <= 0x97 => $"shortJump:{(bytecode & 0x07) + 1}",
            >= 0x98 and <= 0x9F => $"shortConditionalJump:{(bytecode & 0x07) + 1}",
            >= 0xB0 and <= 0xBF => $"sendArithmetic:{GetArithmeticSelectorName(bytecode)}",
            >= 0xC0 and <= 0xCF => $"sendSpecial:{bytecode & 0x0F}",
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

    /// <summary>
    /// Gets the cached reference to the true object.
    /// </summary>
    private static SObjectRef GetTrueRef()
    {
        if (cachedTrueRef == null || cachedTrueRef.Value.IsNil)
        {
            // Try to get from SystemDictionary if available
            if (!SystemDictionary.TrueClassRef.IsNil)
            {
                // Create true singleton instance
                cachedTrueRef = ObjectMemoryNew.CreateInstance(
                    SystemDictionary.TrueClassRef.AsObjectPointer(), 0);
            }
            else
            {
                // Bootstrap fallback - create a placeholder
                // This would be properly initialized during cold start
                cachedTrueRef = SObjectRef.Nil;
            }
        }
        return cachedTrueRef.Value;
    }

    /// <summary>
    /// Updates the active process's suspendedContext to ensure GC reachability.
    /// This links the executing context chain to the process system roots.
    /// Reachability path: SOA → Processor (global) → activeProcess → suspendedContext → context chain
    /// </summary>
    private void UpdateProcessSuspendedContext(SObjectRef contextRef)
    {
        // Get ProcessorScheduler from global "Processor"
        var schedulerRef = SystemDictionary.LookupGlobalRef("Processor");
        if (!schedulerRef.HasValue || schedulerRef.Value.IsNil || !schedulerRef.Value.IsHeapObject)
        {
            if (TracingEnabled)
                Console.WriteLine("    [WARNING] ProcessorScheduler not found!");
            return;
        }

        // Get activeProcess from ProcessorScheduler (instance var 1)
        var activeProcessRef = ObjectMemoryNew.GetInstanceVariable(schedulerRef.Value.AsObjectPointer(), 1);
        if (activeProcessRef.IsNil || !activeProcessRef.IsHeapObject)
        {
            if (TracingEnabled)
                Console.WriteLine("    [WARNING] activeProcess not found!");
            return;
        }

        // Update Process.suspendedContext (instance var 1) to link to current context
        // This ensures the entire context chain (via sender field) remains reachable
        ObjectMemoryNew.SetInstanceVariable(activeProcessRef.AsObjectPointer(), 1, contextRef);

        if (TracingEnabled && DebugOptions.EnableBytecodeTrace)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            Console.WriteLine($"{indent}[PROCESS] Linked {FormatRef(contextRef)} to Process.suspendedContext");
        }
    }

    /// <summary>
    /// Gets the cached reference to the false object.
    /// </summary>
    private static SObjectRef GetFalseRef()
    {
        if (cachedFalseRef == null || cachedFalseRef.Value.IsNil)
        {
            // Try to get from SystemDictionary if available
            if (!SystemDictionary.FalseClassRef.IsNil)
            {
                // Create false singleton instance
                cachedFalseRef = ObjectMemoryNew.CreateInstance(
                    SystemDictionary.FalseClassRef.AsObjectPointer(), 0);
            }
            else
            {
                // Bootstrap fallback - create a placeholder
                // This would be properly initialized during cold start
                cachedFalseRef = SObjectRef.Nil;
            }
        }
        return cachedFalseRef.Value;
    }

    /// <summary>
    /// Formats an SObjectRef for display without calling ToString (which requires GetObject).
    /// </summary>
    private static string FormatRef(SObjectRef objRef)
    {
        if (objRef.IsNil)
            return "nil";
        if (objRef.IsSmallInteger)
            return objRef.AsSmallInteger().ToString();
        if (objRef.IsHeapObject)
            return $"OOP({objRef.AsObjectPointer()})";
        return "Unknown";
    }
}