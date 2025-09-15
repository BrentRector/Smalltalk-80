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
    ///     The message dispatcher for handling method dispatch and lookup.
    /// </summary>
    private readonly MessageDispatcher messageDispatcher;

    /// <summary>
    ///     The method tracer for handling execution tracing.
    /// </summary>
    private readonly MethodTracer methodTracer;

    /// <summary>
    ///     Initializes a new instance of the Interpreter class.
    /// </summary>
    /// <param name="enableTracing">Whether to enable method tracing.</param>
    public Interpreter(bool enableTracing = false)
    {
        messageDispatcher = new MessageDispatcher(MethodCache);
        methodTracer = new MethodTracer(enableTracing);
    }

    /// <summary>
    ///     Gets the context currently being executed by the interpreter.
    /// </summary>
    internal Context CurrentContext => currentContext;

    /// <summary>
    ///     Sets the current context. Used by MessageDispatcher for method dispatch.
    /// </summary>
    /// <param name="context">The new current context.</param>
    internal void SetCurrentContext(Context context)
    {
        currentContext = context;
    }

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
        
        // Track current context as transient root for GC
        if (currentContext?.Receiver != null)
        {
            ObjectMemory.AddTransientRoot(currentContext.Receiver);
        }
        
        try
        {
            int bytecodesSinceGC = 0;
            const int GC_CHECK_INTERVAL = 10000; // Check every 10k bytecodes
            
            while (IsRunning)
            {
                // Check for process switches at the start of each cycle
                // as specified in the Smalltalk-80 interpreter specification
                Scheduler?.CheckProcessSwitch();

                if (currentContext?.Method == null)
                    throw new InvalidOperationException("Context or method is null");
                var opcode = (Bytecode)currentContext.Method.Bytecodes[currentContext.PC++];
                Dispatch(opcode);
                
                // Periodically check if GC should run
                if (++bytecodesSinceGC >= GC_CHECK_INTERVAL)
                {
                    bytecodesSinceGC = 0;
                    // The ManualGC will only run if thresholds are exceeded
                    // This is a cheap check that doesn't impact performance
                    if (ObjectMemory.AllocatedObjectCount > 40000)
                    {
                        ObjectMemory.ManualGC();
                    }
                }
            }
        }
        catch (NonLocalReturnException ex)
        {
            // Handle non-local returns by setting the result and stopping execution
            LastResult = ex.ReturnValue;
            IsRunning = false;
        }
        finally
        {
            // Clear transient roots when execution completes
            ObjectMemory.ClearTransientRoots();
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
        // Method entry tracing handled by MessageDispatcher
        
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
        
        // Method exit tracing handled in ReturnFrom
        
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
        // Bytecode tracing removed
        
        // Use the Blue Book compatible dispatcher for all bytecodes
        BytecodeDispatcher.Dispatch(this, (byte)bytecode);
    }

    /// <summary>
    ///     Returns from the current context with the specified value.
    /// </summary>
    /// <param name="returnValue">The return value.</param>
    public void ReturnFrom(object returnValue)
    {
        // Trace method exit if we're returning from a method context
        if (currentContext is MethodContext methodCtx && methodCtx.Method != null)
        {
            var className = GetClassName(methodCtx.Receiver?.ClassPointer) ?? "Unknown";
            var selector = ExtractSelectorFromMethod(methodCtx.Method) ?? "unknown";
            
            methodTracer.TraceMethodExit(className, selector, returnValue);
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
            
            // Clear transient roots when returning from top-level
            ObjectMemory.ClearTransientRoots();
            
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
            
            // Update transient root to new current context
            if (currentContext?.Receiver != null)
            {
                ObjectMemory.ClearTransientRoots();
                ObjectMemory.AddTransientRoot(currentContext.Receiver);
            }
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

            // Debug output removed
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
        messageDispatcher.PerformSendWithObject(this, selectorObject, argCount, superSend);
    }

    /// <summary>
    ///     Sends a message with the provided selector and argument count.
    /// </summary>
    /// <param name="selector">The selector being invoked.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    internal void PerformSend(Symbol selector, int argCount, bool superSend)
    {
        messageDispatcher.PerformSend(this, selector, argCount, superSend);
    }

    /// <summary>
    ///     Gets the class name from a Smalltalk class object.
    /// </summary>
    /// <param name="cls">The class object.</param>
    /// <returns>The class name or null if not found.</returns>
    private static string? GetClassName(SmalltalkObject? cls)
    {
        if (cls == null) return null;
        
        // Try to get the name from slot 5 (for Class objects)
        if (cls.Fields.Length > 5 && cls.Fields[5] is Symbol nameSymbol)
        {
            return nameSymbol.Value;
        }
        
        // For metaclasses, try to get the thisClass and then its name
        if (cls.Fields.Length > 5 && cls.Fields[5] is SmalltalkObject thisClass)
        {
            return GetClassName(thisClass) + " class";
        }
        
        return null;
    }

    /// <summary>
    ///     Extracts the selector from a compiled method.
    /// </summary>
    /// <param name="method">The compiled method.</param>
    /// <returns>The selector name or null if not found.</returns>
    private static string? ExtractSelectorFromMethod(CompiledMethod method)
    {
        // Look for a Symbol in the literals that could be the selector
        foreach (var literal in method.Literals)
        {
            if (literal is Symbol symbol)
            {
                return symbol.Name;
            }
        }
        return null;
    }

    /// <summary>
    ///     Gets the argument count for a selector.
    /// </summary>
    /// <param name="selector">The selector symbol.</param>
    /// <returns>The number of arguments expected.</returns>
    private static int GetArgumentCount(Symbol selector)
    {
        var name = selector.Name;
        
        // Binary selectors always take 1 argument
        if (name.Length <= 2 && name.All(c => "+-*/<>=~@%|&!?\\".Contains(c)))
            return 1;
            
        // Keyword selectors: count the colons
        if (name.Contains(':'))
            return name.Count(c => c == ':');
            
        // Unary selectors take no arguments
        return 0;
    }
}
