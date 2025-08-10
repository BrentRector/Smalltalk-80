using System;
using System.Linq;

/// <summary>
///     Executes Smalltalk bytecodes and maintains runtime state.
/// </summary>
public class Interpreter
{
    /// <summary>
    ///     The currently active execution <see cref="Context"/>.
    /// </summary>
    private Context currentContext = null!;

    /// <summary>
    ///     The process scheduler for managing multiple execution contexts.
    /// </summary>
    public ProcessScheduler? Scheduler { get; set; }

    /// <summary>
    ///     The method cache for improving message send performance.
    /// </summary>
    public MethodCache MethodCache { get; } = new();

    /// <summary>
    ///     Gets the context currently being executed by the interpreter.
    /// </summary>
    internal Context CurrentContext => currentContext;

    /// <summary>
    ///     Indicates whether the interpreter should exit the process when the
    ///     top-level context returns.
    /// </summary>
    public bool ExitOnReturn { get; set; } = true;

    /// <summary>
    ///     Gets a value indicating whether the interpreter is actively executing.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    ///     Gets the value returned by the most recently executed top-level
    ///     method.
    /// </summary>
    public object? LastResult { get; private set; }

    /// <summary>
    ///     Table of special selector symbols used by arithmetic and
    ///     special message send bytecodes.
    /// </summary>
    private static readonly Symbol[] SpecialSelectors =
    [
        // Arithmetic selectors 0-15
        new Symbol("+"),
        new Symbol("-"),
        new Symbol("<"),
        new Symbol(">"),
        new Symbol("<="),
        new Symbol(">="),
        new Symbol("="),
        new Symbol("~="),
        new Symbol("*"),
        new Symbol("/"),
        new Symbol("\\"),
        new Symbol("@"),
        new Symbol("bitShift:"),
        new Symbol("//"),
        new Symbol("bitAnd:"),
        new Symbol("bitOr:"),
        // Special selectors 16-31
        new Symbol("at:"),
        new Symbol("at:put:"),
        new Symbol("size"),
        new Symbol("next"),
        new Symbol("nextPut:"),
        new Symbol("atEnd"),
        new Symbol("=="),
        new Symbol("class"),
        new Symbol("blockCopy:"),
        new Symbol("value"),
        new Symbol("value:"),
        new Symbol("do:"),
        new Symbol("new"),
        new Symbol("new:"),
        new Symbol("x"),
        new Symbol("y")
    ];

    /// <summary>
    ///     Argument counts corresponding to <see cref="SpecialSelectors"/>.
    /// </summary>
    private static readonly int[] SpecialSelectorArgCounts =
    [
        1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,
        1,2,0,0,1,0,1,0,1,0,1,1,0,1,0,0
    ];

    /// <summary>
    ///     Continuously executes bytecodes starting from the current context.
    /// </summary>
    public void Execute()
    {
        IsRunning = true;
        try
        {
            while (IsRunning)
            {
                // Check for process switches at the start of each cycle
                // as specified in the Smalltalk-80 interpreter specification
                Scheduler?.CheckProcessSwitch();

                var opcode = (Bytecode)currentContext.Method.Bytecodes[currentContext.PC++];
                Dispatch(opcode);
            }
        }
        catch (NonLocalReturnException ex)
        {
            // Handle non-local returns by setting the result and stopping execution
            LastResult = ex.ReturnValue;
            IsRunning = false;
        }
    }

    /// <summary>
    ///     Executes a compiled method and returns the result without exiting the process.
    /// </summary>
    /// <param name="method">The compiled method to run.</param>
    /// <param name="receiver">The receiver for the method.</param>
    /// <returns>The value returned by the method.</returns>
    public object? Evaluate(CompiledMethod method, SmalltalkObject receiver)
        => Evaluate(method, receiver, null);

    /// <summary>
    ///     Executes a compiled method with explicit temporary variable values
    ///     and returns the result without exiting the process.
    /// </summary>
    /// <param name="method">The compiled method to run.</param>
    /// <param name="receiver">The receiver for the method.</param>
    /// <param name="temporaries">
    ///     Optional array of initial temporary values. If <c>null</c> a new array
    ///     of the required size is created.
    /// </param>
    /// <returns>The value returned by the method.</returns>
    public object? Evaluate(
        CompiledMethod method,
        SmalltalkObject receiver,
        object?[]? temporaries)
    {
        // Log method entry for debugging
        var className = GetClassName(receiver?.ClassPointer) ?? "Unknown";
        var selector = ExtractSelectorFromMethod(method) ?? "<unnamed method>";
        MethodTracer.LogMethodEntry(className, selector, receiver);
        
        var previousExit = ExitOnReturn;
        ExitOnReturn = false;
        LastResult = null;
        currentContext = new MethodContext
        {
            Sender = null!,
            Method = method,
            Receiver = receiver!,
            Arguments = Array.Empty<object?>(),
            Temporaries = temporaries ?? new object?[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
        Execute();
        ExitOnReturn = previousExit;
        
        // Log method exit
        MethodTracer.LogMethodExit(className, selector, LastResult);
        
        return LastResult;
    }

    /// <summary>
    ///     Executes the provided <see cref="Context"/> until it returns.
    /// </summary>
    /// <param name="context">The context to execute.</param>
    /// <returns>The value returned by the context.</returns>
    public object? Evaluate(Context context)
    {
        var previousExit = ExitOnReturn;
        ExitOnReturn = false;
        LastResult = null;
        currentContext = context;
        Execute();
        ExitOnReturn = previousExit;
        return LastResult;
    }

    /// <summary>
    ///     Sets up the interpreter's active context for method execution.
    ///     Used by execution testing infrastructure.
    /// </summary>
    /// <param name="receiver">The object receiving the message.</param>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="arguments">Optional arguments to the method.</param>
    public void SetActiveContext(SmalltalkObject receiver, CompiledMethod method, params object[] arguments)
    {
        var temps = new object?[method.TemporariesCount];

        // Copy arguments into temporary array
        for (int i = 0; i < arguments.Length && i < temps.Length; i++)
        {
            temps[i] = arguments[i];
        }

        currentContext = new MethodContext
        {
            Sender = null,
            Method = method,
            Receiver = receiver,
            Arguments = arguments,
            Temporaries = temps,
            Stack = new Stack<object?>(),
            PC = 0
        };
    }

    /// <summary>
    ///     Executes the currently active context method and returns the result.
    ///     Used by execution testing infrastructure.
    /// </summary>
    /// <returns>The value returned by the method execution.</returns>
    public object? ExecuteMethod()
    {
        var previousExit = ExitOnReturn;
        ExitOnReturn = false;
        LastResult = null;
        Execute();
        ExitOnReturn = previousExit;
        return LastResult;
    }

    /// <summary>
    ///     Handles the given bytecode in the context of the running method.
    /// </summary>
    /// <param name="bytecode">The bytecode to execute.</param>
    public void Dispatch(Bytecode bytecode)
    {
        // Use MethodTracer for bytecode tracing instead of general Tracer
        MethodTracer.LogBytecode(bytecode, currentContext.PC - 1, currentContext);
        
        // Keep old trace for compatibility but only if Tracer is enabled
        if (Tracer.Enabled)
        {
            Tracer.Log($"Dispatching bytecode: {bytecode} ({(byte)bytecode}) at PC: {currentContext.PC - 1}");
        }
        
        // Special handling for bytecode 10: Method Lookup
        if ((byte)bytecode == 10)
        {
            // OP_LOOKUP_METHOD: Pop selector and receiver, lookup method, push result
            var selector = currentContext.Stack.Pop() as Symbol;
            var receiver = currentContext.Stack.Pop() as SmalltalkObject;
            
            if (selector != null && receiver != null && receiver.ClassPointer != null)
            {
                var method = receiver.ClassPointer.Lookup(selector);
                currentContext.Stack.Push(method);
            }
            else
            {
                throw new InvalidOperationException($"Method lookup requires Symbol selector and SmalltalkObject receiver with valid class");
            }
        }
        else
        {
            // Use the Blue Book compatible dispatcher for all other bytecodes
            BytecodeDispatcher.Dispatch(this, (byte)bytecode);
        }
    }

    /// <summary>
    ///     Returns from the current context with the specified value.
    /// </summary>
    /// <param name="returnValue">The return value.</param>
    public void ReturnFrom(object returnValue)
    {
        // Log method exit if we're returning from a method context
        if (currentContext is MethodContext methodCtx && methodCtx.Method != null)
        {
            var className = GetClassName(methodCtx.Receiver?.ClassPointer) ?? "Unknown";
            var selector = ExtractSelectorFromMethod(methodCtx.Method) ?? "unknown";
            
            MethodTracer.LogMethodExit(className, selector, returnValue);
        }
        
        if (currentContext.Sender == null)
        {
            if (returnValue is MethodContext mc)
            {
                returnValue = new Context
                {
                    Sender = mc.Sender,
                    Method = mc.Method,
                    Receiver = mc.Receiver,
                    Arguments = mc.Arguments,
                    Temporaries = mc.Temporaries,
                    Stack = mc.Stack,
                    PC = mc.PC
                };
            }
            LastResult = returnValue;
            if (ExitOnReturn)
            {
                Console.WriteLine($"Returned: {returnValue}");
                Environment.Exit(0);
            }
            IsRunning = false;
        }
        else
        {
            currentContext = currentContext.Sender;
            currentContext.Stack.Push(returnValue);
        }
    }

    /// <summary>
    ///     Handles both local and non-local returns.
    /// </summary>
    /// <param name="returnValue">The value to return.</param>
    public void HandleReturn(object returnValue)
    {
        try
        {
            ReturnFrom(returnValue);
        }
        catch (NonLocalReturnException ex)
        {
            if (ex.TargetContext == null)
            {
                LastResult = ex.ReturnValue;
                IsRunning = false;
                if (ExitOnReturn)
                {
                    Console.WriteLine($"Returned: {ex.ReturnValue}");
                    Environment.Exit(0);
                }
                return;
            }
            while (currentContext != null && currentContext != ex.TargetContext)
            {
                currentContext = currentContext.Sender!;
            }
            if (currentContext == null)
            {
                throw new Exception("Non-local return failed: target context not found");
            }
            currentContext.Stack.Push(ex.ReturnValue);
        }
    }

    /// <summary>
    ///     Handles returns from a <see cref="BlockContext"/>. Non-local
    ///     returns unwind to the block's home context sender.
    /// </summary>
    /// <param name="returnValue">The value being returned.</param>
    private void HandleBlockReturn(object returnValue)
    {
        if (currentContext is BlockContext block)
        {
            if (block.Sender == block.HomeContext)
            {
                HandleReturn(returnValue);
            }
            else
            {
                throw new NonLocalReturnException(returnValue, block.HomeContext.Sender);
            }
        }
        else
        {
            HandleReturn(returnValue);
        }
    }

    /// <summary>
    ///     Stores the stack top into a variable described by the extended
    ///     store descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor byte.</param>
    /// <param name="popAfter">Whether to pop the value after storing.</param>
    private void HandleExtendedStore(byte descriptor, bool popAfter)
    {
        int type = descriptor >> 6;
        int index = descriptor & 0x3F;
        object? value = currentContext.Stack.Peek();

        switch (type)
        {
            case 0:
                currentContext.Receiver.Fields[index] = value;
                break;
            case 1:
                currentContext.Temporaries[index] = value!;
                break;
            case 2:
                throw new InvalidOperationException("Cannot store into a literal constant");
            case 3:
                var assoc = (Association)currentContext.Method.Literals[index]!;
                assoc.Value = value;
                break;
            default:
                throw new InvalidOperationException($"Invalid extended store type {type}");
        }

        if (popAfter)
        {
            _ = currentContext.Stack.Pop();
        }
    }

    /// <summary>
    ///     Handles messages sent to a <see cref="BlockContext"/> instance.
    /// </summary>
    /// <param name="block">The block context receiving the message.</param>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="arguments">The argument values.</param>
    private void HandleBlockMessage(BlockContext block, Symbol selector, object?[] arguments)
    {
        if (selector.Name.StartsWith("value"))
        {
            if (arguments.Length != block.ArgumentCount)
            {
                throw new InvalidOperationException("Incorrect number of block arguments");
            }

            var newContext = BlockContext.Create(
                block.HomeContext,
                block.StartPC,
                block.ArgumentCount,
                arguments,
                currentContext);

            currentContext = newContext;
            return;
        }

        throw new InvalidOperationException($"BlockContext does not understand {selector}");
    }

    /// <summary>
    ///     Handles messages sent to a <see cref="Context"/> instance.
    /// </summary>
    /// <param name="ctx">The context receiving the message.</param>
    /// <param name="selector">The selector being sent.</param>
    /// <param name="arguments">The argument values.</param>
    private void HandleContextMessage(Context ctx, Symbol selector, object?[] arguments)
    {
        if (selector.Name == "blockCopy:" && arguments.Length == 1 && arguments[0] is int argCount)
        {
            int startPc = currentContext.PC + 3;
            var block = new BlockContext
            {
                HomeContext = ctx,
                StartPC = startPc,
                Sender = null,
                Method = ctx.Method,
                Receiver = ctx.Receiver,
                Arguments = Array.Empty<object?>(),
                ArgumentCount = argCount,
                Temporaries = ctx.Temporaries,
                Stack = new Stack<object?>(),
                PC = startPc
            };

            currentContext.Stack.Push(block);
            return;
        }

        throw new InvalidOperationException($"Context does not understand {selector}");
    }

    /// <summary>
    ///     Performs the message send indicated by the specified bytecode.
    /// </summary>
    /// <param name="bytecode">The send bytecode.</param>
    public void SendSelector(byte bytecode)
    {
        int selectorIndex = bytecode - 128;
        var selector = (Symbol)currentContext.Method.Literals[selectorIndex];

        int argCount = GetArgumentCount(selector);
        PerformSend(selector, argCount, false);
    }

    /// <summary>
    ///     Sends a message with a Smalltalk selector object.
    /// </summary>
    /// <param name="selectorObject">The Smalltalk Symbol object for the selector.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    internal void PerformSendWithObject(object selectorObject, int argCount, bool superSend)
    {
        // The selector object is a Smalltalk Symbol - we need to bridge to the existing system
        // This is a temporary bridge until the whole system works with Smalltalk objects natively
        Symbol selector;
        
        if (selectorObject is SmalltalkObject symbolObj)
        {
            // Extract the selector name for the C# method lookup system
            // This is the minimal bridge needed until method lookup works with Smalltalk objects
            if (symbolObj.TryGetStringValue(out var selectorName))
            {
                selector = new Symbol(selectorName);
            }
            else
            {
                throw new InvalidOperationException($"Cannot extract selector name from Smalltalk Symbol object");
            }
        }
        else if (selectorObject is Symbol sym)
        {
            // Already a C# Symbol (from Phase 1/2)
            selector = sym;
        }
        else
        {
            throw new InvalidOperationException($"Selector must be a Symbol, got {selectorObject?.GetType()}");
        }
        
        PerformSend(selector, argCount, superSend);
    }

    /// <summary>
    ///     Sends a message with the provided selector and argument count.
    /// </summary>
    /// <param name="selector">The selector being invoked.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    internal void PerformSend(Symbol selector, int argCount, bool superSend)
    {
        var arguments = new object?[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            arguments[i] = currentContext.Stack.Pop();
        }

        object? receiver = currentContext.Stack.Pop();

        var message = new Message
        {
            Selector = selector,
            Receiver = receiver,
            Arguments = arguments,
            SuperSend = superSend
        };

        PerformSend(message);
    }

    /// <summary>
    ///     Sends the specified message object to its receiver.
    /// </summary>
    /// <param name="message">The message to execute.</param>
    private void PerformSend(Message message)
    {
        var selector = message.Selector;
        var arguments = message.Arguments;
        object? receiver = message.Receiver;
        bool superSend = message.SuperSend;
        int argCount = arguments.Length;

        if (receiver is int li &&
            argCount == 1 &&
            arguments[0] is int ri &&
            selector.Name == "+")
        {
            currentContext.Stack.Push(li + ri);
            return;
        }

        if (receiver is BlockContext blk)
        {
            HandleBlockMessage(blk, selector, arguments);
            return;
        }

        if (receiver is Context ctx)
        {
            HandleContextMessage(ctx, selector, arguments);
            return;
        }

        if (receiver is SmalltalkObject recvObj)
        {
            SmalltalkObject? startClass;
            if (superSend)
            {
                if (currentContext.Method.Literals.Length == 0 ||
                    currentContext.Method.Literals[^1] is not SmalltalkObject methodClass)
                {
                    throw new InvalidOperationException("Super send missing method class literal");
                }

                startClass = methodClass.Superclass ??
                             throw new InvalidOperationException("Method class has no superclass");
            }
            else
            {
                startClass = recvObj.ClassPointer;
            }

            var cls = startClass ??
                      throw new InvalidOperationException("Receiver has no class pointer");

            // Try method cache first for performance
            var method = MethodCache.Lookup(cls, selector);
            if (method == null)
            {
                // Cache miss - perform full lookup
                method = cls.Lookup(selector);

                if (method == null)
                    throw new InvalidOperationException($"Method {selector} not found");

                // Cache the result for future use
                MethodCache.Store(cls, selector, method);
            }

            if (method.HasPrimitive)
            {
                currentContext.Stack.Push(receiver);
                foreach (var arg in arguments)
                {
                    currentContext.Stack.Push(arg);
                }

                if (Primitives.Execute(method.PrimitiveIndex, currentContext))
                {
                    return;
                }

                for (int i = 0; i <= argCount; i++)
                {
                    _ = currentContext.Stack.Pop();
                }
            }

            var temps = new object?[method.TemporariesCount];
            for (int i = 0; i < arguments.Length && i < temps.Length; i++)
                temps[i] = arguments[i];

            // Log method entry with actual selector name 
            var className = GetClassName(startClass) ?? "Unknown";
            MethodTracer.LogMethodEntry(className, selector.Name, recvObj);
            
            var newContext = new MethodContext
            {
                Sender = currentContext,
                Method = method,
                Receiver = recvObj,
                Arguments = arguments,
                Temporaries = temps,
                Stack = new Stack<object?>(),
                PC = 0
            };
            currentContext = newContext;
            return;
        }

        // Handle .NET primitive types by mapping them to Smalltalk classes
        var primitiveClass = GetClassForPrimitive(receiver);
        if (primitiveClass != null)
        {
            // Convert .NET primitive to proper Smalltalk object
            var smalltalkReceiver = ConvertPrimitiveToSmalltalkObject(receiver, primitiveClass);
            if (smalltalkReceiver != null)
            {
                // Recursively call PerformSend with the Smalltalk object
                var smalltalkMessage = new Message
                {
                    Selector = selector,
                    Receiver = smalltalkReceiver,
                    Arguments = arguments,
                    SuperSend = false
                };
                PerformSend(smalltalkMessage);
                return;
            }

            // Perform method lookup on the mapped Smalltalk class for primitive methods
            var method = primitiveClass.Lookup(selector);
            if (method == null)
                throw new InvalidOperationException($"Method {selector} not found on primitive type");

            // Execute primitive method directly
            if (method.HasPrimitive)
            {
                currentContext.Stack.Push(receiver);
                foreach (var arg in arguments)
                {
                    currentContext.Stack.Push(arg);
                }

                if (Primitives.Execute(method.PrimitiveIndex, currentContext))
                {
                    return;
                }

                // If primitive failed, fall through to normal method execution
                for (int i = 0; i <= argCount; i++)
                {
                    _ = currentContext.Stack.Pop();
                }
            }

            // For now, we'll just handle primitive methods for primitive types
            throw new InvalidOperationException($"Non-primitive method {selector} not supported on primitive type {receiver?.GetType().Name}");
        }

        throw new InvalidOperationException($"Cannot send {selector} to {receiver}");
    }

    /// <summary>
    /// Maps .NET primitive types to their corresponding Smalltalk classes.
    /// </summary>
    /// <param name="primitive">The .NET primitive object.</param>
    /// <returns>The corresponding Smalltalk class, or null if no mapping exists.</returns>
    private static SmalltalkObject? GetClassForPrimitive(object? primitive)
    {
        // Stub implementation - returns null since shadow classes removed
        // In a full implementation, this would look up class objects in ObjectMemory
        return null;
    }

    /// <summary>
    /// Converts .NET primitive values to proper Smalltalk objects.
    /// </summary>
    /// <param name="primitive">The .NET primitive value.</param>
    /// <param name="smalltalkClass">The corresponding Smalltalk class.</param>
    /// <returns>A Smalltalk object, or null if conversion is not needed/possible.</returns>
    private static SmalltalkObject? ConvertPrimitiveToSmalltalkObject(object? primitive, SmalltalkObject? smalltalkClass)
    {
        return primitive switch
        {
            bool b => ConvertBooleanToSmalltalkObject(b),
            int i => ConvertIntegerToSmalltalkObject(i),
            double d => ConvertFloatToSmalltalkObject(d),
            char c => ConvertCharacterToSmalltalkObject(c),
            string s => ConvertStringToSmalltalkObject(s),
            _ => null
        };
    }

    /// <summary>
    /// Converts a .NET boolean to the appropriate Smalltalk singleton object (True or False).
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The True or False singleton object.</returns>
    private static SmalltalkObject? ConvertBooleanToSmalltalkObject(bool value)
    {
        // TODO: Return proper true/false singleton from object memory
        // For now, create a minimal object
        return new SmalltalkObject(null, 0);
    }

    /// <summary>
    /// Converts a .NET integer to a Smalltalk SmallInteger object.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A SmallInteger object.</returns>
    private static SmalltalkObject? ConvertIntegerToSmalltalkObject(int value)
    {
        // TODO: Look up SmallInteger class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET double to a Smalltalk Float object.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>A Float object.</returns>
    private static SmalltalkObject? ConvertFloatToSmalltalkObject(double value)
    {
        // TODO: Look up Float class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET char to a Smalltalk Character object.
    /// </summary>
    /// <param name="value">The character value.</param>
    /// <returns>A Character object.</returns>
    private static SmalltalkObject? ConvertCharacterToSmalltalkObject(char value)
    {
        // TODO: Look up Character class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET string to a Smalltalk String object.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A String object.</returns>
    private static SmalltalkObject? ConvertStringToSmalltalkObject(string value)
    {
        // TODO: Look up String class from object memory
        // For now, store string directly in single field
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    ///     Determines the number of arguments expected by the specified selector.
    /// </summary>
    /// <param name="selector">The selector being sent.</param>
    /// <returns>The number of argument values that should be popped from the stack.</returns>
    private static int GetArgumentCount(Symbol selector)
    {
        var name = selector.Name;
        if (name.Contains(':'))
        {
            return name.Count(c => c == ':');
        }

        return name.All(char.IsLetterOrDigit) ? 0 : 1;
    }
    
    /// <summary>
    /// Gets the name of a class from a SmalltalkObject that represents a class.
    /// </summary>
    private static string? GetClassName(SmalltalkObject? classObj)
    {
        if (classObj == null) return null;
        
        // First, try to look up this class OOP in the global dictionary
        // This works for both classes and metaclasses
        string? globalName = LookupClassNameInGlobals(classObj);
        if (globalName != null && IsLikelyClassName(globalName))
            return globalName;
        
        // Try to get the name from the class's name field if it exists
        // In Smalltalk-80, classes have a 'name' instance variable
        if (classObj.TryGetField("name", out var nameObj))
        {
            if (nameObj is Symbol sym) return sym.Name;
            if (nameObj is string str) return str;
            if (nameObj is SmalltalkObject nameStrObj && nameStrObj.TryGetStringValue(out var nameStr)) 
                return nameStr;
        }
        
        // For class objects, try to extract the name from field 5 (typical class name location)
        if (classObj.Fields.Length > 5 && classObj.Fields[5] != null)
        {
            var nameField = classObj.Fields[5];
            if (nameField is Symbol nameSym) return nameSym.Name;
            if (nameField is string nameStr) return nameStr;
            if (nameField is SmalltalkObject nameFieldObj && nameFieldObj.TryGetStringValue(out var extractedName))
                return extractedName;
        }
        
        // Check other common name field locations (field 0, 1, etc.)
        for (int i = 0; i < Math.Min(classObj.Fields.Length, 10); i++)
        {
            var field = classObj.Fields[i];
            if (field is Symbol fieldSym && IsLikelyClassName(fieldSym.Name))
                return fieldSym.Name;
            if (field is string fieldStr && IsLikelyClassName(fieldStr))
                return fieldStr;
            if (field is SmalltalkObject fieldObj && fieldObj.TryGetStringValue(out var fieldName) && IsLikelyClassName(fieldName))
                return fieldName;
        }
        
        // Fallback to OOP for debugging
        return $"Class@{classObj.Oop}";
    }
    
    /// <summary>
    /// Looks up a class object in the global Smalltalk dictionary to find its name.
    /// </summary>
    private static string? LookupClassNameInGlobals(SmalltalkObject classObj)
    {
        try
        {
            // Try to find this class in the global dictionary by iterating through all objects
            // This is a simplified approach - in a full implementation we'd access the Smalltalk dictionary directly
            
            // Look for common class names and their expected OOP patterns
            var commonClasses = new Dictionary<string, int[]>
            {
                {"Array", new[] {16, 18}},     // Array class might be at these OOPs
                {"String", new[] {14, 20}},    // String class might be at these OOPs  
                {"Symbol", new[] {12, 22}},    // Symbol class might be at these OOPs
                {"Object", new[] {2, 26}},     // Object class might be at these OOPs
                {"Class", new[] {4, 28}},      // Class class might be at these OOPs
                {"Metaclass", new[] {6, 30}}, // Metaclass class might be at these OOPs
                {"ByteArray", new[] {18, 24}}, // ByteArray class might be at these OOPs
                {"MethodDictionary", new[] {10, 32}} // MethodDictionary might be at these OOPs
            };
            
            foreach (var kvp in commonClasses)
            {
                foreach (var oop in kvp.Value)
                {
                    if (classObj.Oop == oop)
                        return kvp.Key;
                }
            }
            
            // For metaclasses, try to find the pattern (metaclass is typically class + even offset)
            foreach (var kvp in commonClasses)
            {
                foreach (var classOop in kvp.Value)
                {
                    // Metaclasses are often at class OOP + some offset
                    if (classObj.Oop == classOop + 2 || classObj.Oop == classOop + 10 || classObj.Oop == classOop + 14)
                        return kvp.Key + " class"; // e.g., "Array class"
                }
            }
        }
        catch
        {
            // If lookup fails, return null to fall back to other methods
        }
        
        return null;
    }
    
    /// <summary>
    /// Determines if a string looks like a Smalltalk class name.
    /// </summary>
    private static bool IsLikelyClassName(string candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        
        // Handle metaclass names (e.g., "Array class")
        if (candidate.EndsWith(" class"))
        {
            var baseName = candidate.Substring(0, candidate.Length - 6);
            return baseName.Length > 0 && 
                   char.IsUpper(baseName[0]) && 
                   baseName.All(c => char.IsLetterOrDigit(c));
        }
        
        // Class names start with uppercase letter and contain only letters/digits
        return candidate.Length > 0 && 
               char.IsUpper(candidate[0]) && 
               candidate.All(c => char.IsLetterOrDigit(c)) &&
               candidate.Length <= 30; // Reasonable length limit
    }
    
    /// <summary>
    /// Extracts the selector name from a CompiledMethod by examining its literals.
    /// </summary>
    private static string? ExtractSelectorFromMethod(CompiledMethod method)
    {
        // First check if we have the selector name explicitly set (for VM method execution)
        if (!string.IsNullOrEmpty(method.SelectorName))
            return method.SelectorName;
        
        if (method.Literals == null) return null;
        
        // Look for Symbol literals that might be the selector
        // In Smalltalk-80 methods, selector symbols are often in the literals array
        foreach (var literal in method.Literals)
        {
            // First check for C# Symbol objects (from Phase 1/2 shadow objects)
            if (literal is Symbol sym) 
                return sym.Name;
            
            // Handle Smalltalk Symbol objects from Phase 2 bootstrap
            if (literal is SmalltalkObject symbolObj)
            {
                // Try to extract string from Smalltalk Symbol object
                if (symbolObj.TryGetStringValue(out var selectorName) && !string.IsNullOrEmpty(selectorName))
                {
                    // Heuristic: likely selectors contain colons, letters, or common operators
                    if (IsLikelySelector(selectorName))
                    {
                        return selectorName;
                    }
                }
            }
            
            // Handle direct string literals (unlikely but possible)
            if (literal is string str && IsLikelySelector(str))
                return str;
        }
        
        return null;
    }
    
    /// <summary>
    /// Determines if a string looks like a Smalltalk selector.
    /// </summary>
    private static bool IsLikelySelector(string candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;
        
        // Common Smalltalk selectors patterns:
        // 1. Keywords with colons: "at:", "at:put:", "new:", etc.
        // 2. Unary messages: "size", "class", "new", "copy", etc.
        // 3. Binary messages: "+", "-", "=", "<=", ">=", etc.
        
        // Keywords (contain colons)
        if (candidate.Contains(':'))
            return true;
            
        // Binary operators
        if (candidate.Length <= 2 && candidate.All(c => "+-*/<>=~@%|&!?\\".Contains(c)))
            return true;
            
        // Unary messages (letters only, reasonable length)
        if (candidate.Length <= 20 && candidate.All(char.IsLetter))
            return true;
            
        return false;
    }
}