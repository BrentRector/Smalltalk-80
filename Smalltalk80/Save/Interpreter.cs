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
        var previousExit = ExitOnReturn;
        ExitOnReturn = false;
        LastResult = null;
        currentContext = new MethodContext
        {
            Sender = null!,
            Method = method,
            Receiver = receiver,
            Arguments = Array.Empty<object?>(),
            Temporaries = temporaries ?? new object?[method.TemporariesCount],
            Stack = new Stack<object?>(),
            PC = 0
        };
        Execute();
        ExitOnReturn = previousExit;
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
        Tracer.Log($"Dispatching bytecode: {bytecode} ({(byte)bytecode}) at PC: {currentContext.PC - 1}");
        if (bytecode >= Bytecode.PushReceiverVariable0 && bytecode <= Bytecode.PushReceiverVariable15)
        {
            int index = (int)bytecode - (int)Bytecode.PushReceiverVariable0;
            currentContext.Stack.Push(currentContext.Receiver.Fields[index]);
        }
        else if (bytecode >= Bytecode.PushTemporaryVariable0 && bytecode <= Bytecode.PushTemporaryVariable15)
        {
            int index = (int)bytecode - (int)Bytecode.PushTemporaryVariable0;
            currentContext.Stack.Push(currentContext.Temporaries[index]);
        }
        else if (bytecode >= Bytecode.PushLiteralConstant0 && bytecode <= Bytecode.PushLiteralConstant31)
        {
            int index = (int)bytecode - (int)Bytecode.PushLiteralConstant0;
            currentContext.Stack.Push(currentContext.Method.Literals[index]);
        }
        else if (bytecode >= Bytecode.PushLiteralVariable0 && bytecode <= Bytecode.PushLiteralVariable31)
        {
            int index = (int)bytecode - (int)Bytecode.PushLiteralVariable0;
            var assoc = (Association)currentContext.Method.Literals[index]!;
            currentContext.Stack.Push(assoc.Value);
        }
        else if (bytecode >= Bytecode.StoreAndPopReceiverVariable0 && bytecode <= Bytecode.StoreAndPopReceiverVariable7)
        {
            int index = (int)bytecode - (int)Bytecode.StoreAndPopReceiverVariable0;
            currentContext.Receiver.Fields[index] = currentContext.Stack.Pop();
        }
        else if (bytecode >= Bytecode.StoreAndPopTemporaryVariable0 && bytecode <= Bytecode.StoreAndPopTemporaryVariable7)
        {
            int index = (int)bytecode - (int)Bytecode.StoreAndPopTemporaryVariable0;
            currentContext.Temporaries[index] = currentContext.Stack.Pop()!;
        }
        else if ((bytecode >= Bytecode.PushSelf && bytecode <= Bytecode.PushTwo) ||
                   (bytecode >= Bytecode.PushThree && bytecode <= Bytecode.PushTen))
        {
            object? constant = bytecode switch
            {
                Bytecode.PushSelf => currentContext.Receiver,
                Bytecode.PushTrue => true,
                Bytecode.PushFalse => false,
                Bytecode.PushNil => null,
                Bytecode.PushMinusOne => -1,
                Bytecode.PushZero => 0,
                Bytecode.PushOne => 1,
                Bytecode.PushTwo => 2,
                Bytecode.PushThree => 3,
                Bytecode.PushFour => 4,
                Bytecode.PushFive => 5,
                Bytecode.PushSix => 6,
                Bytecode.PushSeven => 7,
                Bytecode.PushEight => 8,
                Bytecode.PushNine => 9,
                Bytecode.PushTen => 10,
                _ => null
            };
            currentContext.Stack.Push(constant);
        }
        else if (bytecode >= Bytecode.ReturnSelf && bytecode <= Bytecode.ReturnNil)
        {
            object? value = bytecode switch
            {
                Bytecode.ReturnSelf => currentContext.Receiver,
                Bytecode.ReturnTrue => true,
                Bytecode.ReturnFalse => false,
                Bytecode.ReturnNil => null,
                _ => null
            };
            HandleReturn(value!);
        }
        else if (bytecode == Bytecode.ReturnTopFromMethod)
        {
            var returnValue = currentContext.Stack.Count > 0
                ? currentContext.Stack.Pop()
                : currentContext.Receiver;
            HandleReturn(returnValue!);
        }
        else if (bytecode == Bytecode.ReturnTopFromBlock)
        {
            var returnValue = currentContext.Stack.Count > 0
                ? currentContext.Stack.Pop()
                : currentContext.Receiver;
            HandleBlockReturn(returnValue!);
        }
        else if (bytecode == Bytecode.ExtendedStore)
        {
            byte descriptor = currentContext.Method.Bytecodes[currentContext.PC++];
            HandleExtendedStore(descriptor, false);
        }
        else if (bytecode == Bytecode.ExtendedStoreAndPop)
        {
            byte descriptor = currentContext.Method.Bytecodes[currentContext.PC++];
            HandleExtendedStore(descriptor, true);
        }
        else if (bytecode == Bytecode.SingleExtendedSend)
        {
            byte descriptor = currentContext.Method.Bytecodes[currentContext.PC++];
            int argCount = descriptor >> 5;
            int selectorIndex = descriptor & 0x1F;
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, argCount, false);
        }
        else if (bytecode == Bytecode.DoubleExtendedSend)
        {
            int argCount = currentContext.Method.Bytecodes[currentContext.PC++];
            int selectorIndex = currentContext.Method.Bytecodes[currentContext.PC++];
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, argCount, false);
        }
        else if (bytecode == Bytecode.SingleExtendedSuper)
        {
            byte descriptor = currentContext.Method.Bytecodes[currentContext.PC++];
            int argCount = descriptor >> 5;
            int selectorIndex = descriptor & 0x1F;
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, argCount, true);
        }
        else if (bytecode == Bytecode.DoubleExtendedSuper)
        {
            int argCount = currentContext.Method.Bytecodes[currentContext.PC++];
            int selectorIndex = currentContext.Method.Bytecodes[currentContext.PC++];
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, argCount, true);
        }
        else if (bytecode == Bytecode.PopStack)
        {
            _ = currentContext.Stack.Pop();
        }
        else if (bytecode == Bytecode.DuplicateTop)
        {
            var top = currentContext.Stack.Peek();
            currentContext.Stack.Push(top);
        }
        else if (bytecode == Bytecode.PushActiveContext)
        {
            currentContext.Stack.Push(currentContext);
        }
        else if (bytecode == Bytecode.JumpShort)
        {
            var offset = (sbyte)currentContext.Method.Bytecodes[currentContext.PC++];
            currentContext.PC += offset;
        }
        else if (bytecode == Bytecode.JumpLong)
        {
            byte high = currentContext.Method.Bytecodes[currentContext.PC++];
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            short offset = (short)((high << 8) | low);
            currentContext.PC += offset;
        }
        else if (bytecode == Bytecode.JumpIfFalseShort)
        {
            var offset = (sbyte)currentContext.Method.Bytecodes[currentContext.PC++];
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode == Bytecode.JumpIfFalseLong)
        {
            byte high = currentContext.Method.Bytecodes[currentContext.PC++];
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            short offset = (short)((high << 8) | low);
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode == Bytecode.JumpIfTrueShort)
        {
            var offset = (sbyte)currentContext.Method.Bytecodes[currentContext.PC++];
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode == Bytecode.JumpIfTrueLong)
        {
            byte high = currentContext.Method.Bytecodes[currentContext.PC++];
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            short offset = (short)((high << 8) | low);
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode >= Bytecode.PopJumpFalseShort1 && bytecode <= Bytecode.PopJumpFalseShort8)
        {
            int offset = (int)bytecode - (int)Bytecode.PopJumpFalseShort1 + 1;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode >= Bytecode.LongJump0 && bytecode <= Bytecode.LongJump7)
        {
            int high = ((int)bytecode - (int)Bytecode.LongJump0) << 8;
            int low = currentContext.Method.Bytecodes[currentContext.PC++];
            int raw = high | low;
            if ((raw & 0x400) != 0)
            {
                raw |= unchecked((int)0xFFFFF800);
            }
            currentContext.PC += raw;
        }
        else if (bytecode >= Bytecode.PopJumpTrueLong0 && bytecode <= Bytecode.PopJumpTrueLong3)
        {
            int high = ((int)bytecode - (int)Bytecode.PopJumpTrueLong0) << 8;
            int low = currentContext.Method.Bytecodes[currentContext.PC++];
            int offset = high | low;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode >= Bytecode.PopJumpFalseLong0 && bytecode <= Bytecode.PopJumpFalseLong3)
        {
            int high = ((int)bytecode - (int)Bytecode.PopJumpFalseLong0) << 8;
            int low = currentContext.Method.Bytecodes[currentContext.PC++];
            int offset = high | low;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b)
            {
                currentContext.PC += offset;
            }
        }
        else if (bytecode >= Bytecode.SendArithmeticSelector0 && bytecode <= Bytecode.SendArithmeticSelector15)
        {
            int index = (int)bytecode - (int)Bytecode.SendArithmeticSelector0;
            var selector = SpecialSelectors[index];
            int argCount = SpecialSelectorArgCounts[index];
            PerformSend(selector, argCount, false);
        }
        else if (bytecode >= Bytecode.SendSpecialSelector0 && bytecode <= Bytecode.SendSpecialSelector15)
        {
            int index = (int)bytecode - (int)Bytecode.SendSpecialSelector0 + 16;
            var selector = SpecialSelectors[index];
            int argCount = SpecialSelectorArgCounts[index];
            PerformSend(selector, argCount, false);
        }
        else if (bytecode >= Bytecode.SendLiteral0Args0 && bytecode <= Bytecode.SendLiteral0Args15)
        {
            int selectorIndex = (int)bytecode - (int)Bytecode.SendLiteral0Args0;
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, 0, false);
        }
        else if (bytecode >= Bytecode.SendLiteral1Arg0 && bytecode <= Bytecode.SendLiteral1Arg15)
        {
            int selectorIndex = (int)bytecode - (int)Bytecode.SendLiteral1Arg0;
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, 1, false);
        }
        else if (bytecode >= Bytecode.SendLiteral2Args0 && bytecode <= Bytecode.SendLiteral2Args15)
        {
            int selectorIndex = (int)bytecode - (int)Bytecode.SendLiteral2Args0;
            var selector = (Symbol)currentContext.Method.Literals[selectorIndex];
            PerformSend(selector, 2, false);
        }
        else if ((byte)bytecode >= 128)
        {
            SendSelector((byte)bytecode);
        }
        else
        {
            throw new NotImplementedException($"Unhandled bytecode: {bytecode}");
        }
    }

    /// <summary>
    ///     Returns from the current context with the specified value.
    /// </summary>
    /// <param name="returnValue">The return value.</param>
    public void ReturnFrom(object returnValue)
    {
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
    ///     Sends a message with the provided selector and argument count.
    /// </summary>
    /// <param name="selector">The selector being invoked.</param>
    /// <param name="argCount">The number of arguments supplied.</param>
    /// <param name="superSend">Whether lookup should start in the superclass of the method class.</param>
    private void PerformSend(Symbol selector, int argCount, bool superSend)
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
            SmalltalkClass? startClass;
            if (superSend)
            {
                if (currentContext.Method.Literals.Length == 0 ||
                    currentContext.Method.Literals[^1] is not SmalltalkClass methodClass)
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
            // Perform method lookup on the mapped Smalltalk class
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
    private static SmalltalkClass? GetClassForPrimitive(object? primitive)
    {
        return primitive switch
        {
            int => SystemDictionary.TryGetClass("SmallInteger", out var intClass) ? intClass : null,
            bool => SystemDictionary.TryGetClass("Boolean", out var boolClass) ? boolClass : null,
            string => SystemDictionary.TryGetClass("String", out var stringClass) ? stringClass : null,
            float or double => SystemDictionary.TryGetClass("Float", out var floatClass) ? floatClass : null,
            _ => null
        };
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
}