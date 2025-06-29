using System;

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
    ///     Continuously executes bytecodes starting from the current context.
    /// </summary>
    public void Execute()
    {
        IsRunning = true;
        while (IsRunning) {
            var opcode = (Bytecode)currentContext.Method.Bytecodes[currentContext.PC++];
            Dispatch(opcode);
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
        currentContext = new Context
        {
            Sender = null!,
            Method = method,
            Receiver = receiver,
            Arguments = Array.Empty<object?>(),
#pragma warning disable CS8619
            Temporaries = temporaries ?? new object[method.TemporariesCount],
#pragma warning restore CS8619
            Stack = new Stack<object?>(),
            PC = 0
        };
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
        Tracer.Log($"Dispatching bytecode: {bytecode} at PC: {currentContext.PC - 1}");
        if (bytecode >= Bytecode.PushReceiverVariable0 && bytecode <= Bytecode.PushReceiverVariable15) {
            int index = (int)bytecode - (int)Bytecode.PushReceiverVariable0;
            currentContext.Stack.Push(currentContext.Receiver.Fields[index]);
        } else if (bytecode >= Bytecode.PushTemporaryVariable0 && bytecode <= Bytecode.PushTemporaryVariable15) {
            int index = (int)bytecode - (int)Bytecode.PushTemporaryVariable0;
            currentContext.Stack.Push(currentContext.Temporaries[index]);
        } else if (bytecode >= Bytecode.PushLiteralConstant0 && bytecode <= Bytecode.PushLiteralConstant31) {
            int index = (int)bytecode - (int)Bytecode.PushLiteralConstant0;
            currentContext.Stack.Push(currentContext.Method.Literals[index]);
        } else if (bytecode >= Bytecode.PushLiteralVariable0 && bytecode <= Bytecode.PushLiteralVariable31) {
            int index = (int)bytecode - (int)Bytecode.PushLiteralVariable0;
            var assoc = (Association)currentContext.Method.Literals[index]!;
            currentContext.Stack.Push(assoc.Value);
        } else if (bytecode >= Bytecode.StoreAndPopReceiverVariable0 && bytecode <= Bytecode.StoreAndPopReceiverVariable7) {
            int index = (int)bytecode - (int)Bytecode.StoreAndPopReceiverVariable0;
            currentContext.Receiver.Fields[index] = currentContext.Stack.Pop();
        } else if (bytecode >= Bytecode.StoreAndPopTemporaryVariable0 && bytecode <= Bytecode.StoreAndPopTemporaryVariable7) {
            int index = (int)bytecode - (int)Bytecode.StoreAndPopTemporaryVariable0;
            currentContext.Temporaries[index] = currentContext.Stack.Pop()!;
        } else if (bytecode >= Bytecode.PushSelf && bytecode <= Bytecode.PushTwo) {
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
                _ => null
            };
            currentContext.Stack.Push(constant);
        } else if (bytecode >= Bytecode.ReturnSelf && bytecode <= Bytecode.ReturnNil) {
            object? value = bytecode switch
            {
                Bytecode.ReturnSelf => currentContext.Receiver,
                Bytecode.ReturnTrue => true,
                Bytecode.ReturnFalse => false,
                Bytecode.ReturnNil => null,
                _ => null
            };
            HandleReturn(value!);
        } else if (bytecode == Bytecode.ReturnTopFromMethod || bytecode == Bytecode.ReturnTopFromBlock) {
            var returnValue = currentContext.Stack.Count > 0
                ? currentContext.Stack.Pop()
                : currentContext.Receiver;
            HandleReturn(returnValue!);
        } else if (bytecode == Bytecode.ExtendedStore) {
            byte descriptor = currentContext.Method.Bytecodes[currentContext.PC++];
            int type = descriptor >> 6;
            int index = descriptor & 0x3F;
            if (type == 3) {
                var assoc = (Association)currentContext.Method.Literals[index]!;
                assoc.Value = currentContext.Stack.Peek();
            } else {
                throw new NotImplementedException($"Extended store type {type} not supported");
            }
        } else if (bytecode == Bytecode.PopStack) {
            _ = currentContext.Stack.Pop();
        } else if (bytecode == Bytecode.DuplicateTop) {
            var top = currentContext.Stack.Peek();
            currentContext.Stack.Push(top);
        } else if (bytecode == Bytecode.PushActiveContext) {
            currentContext.Stack.Push(currentContext);
        } else if (bytecode >= Bytecode.JumpShort0 && bytecode <= Bytecode.JumpShort7) {
            int offset = ((int)bytecode - (int)Bytecode.JumpShort0) + 1;
            currentContext.PC += offset;
        } else if (bytecode >= Bytecode.PopJumpFalseShort0 && bytecode <= Bytecode.PopJumpFalseShort7) {
            int offset = ((int)bytecode - (int)Bytecode.PopJumpFalseShort0) + 1;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b) {
                currentContext.PC += offset;
            }
        } else if (bytecode >= Bytecode.JumpLong0 && bytecode <= Bytecode.JumpLong7) {
            int high = (int)bytecode - (int)Bytecode.JumpLong0;
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            int offset = (high << 8) | low;
            if ((offset & 0x400) != 0) {
                offset -= 0x800;
            }
            currentContext.PC += offset;
        } else if (bytecode >= Bytecode.PopJumpFalseLong0 && bytecode <= Bytecode.PopJumpFalseLong3) {
            int high = (int)bytecode - (int)Bytecode.PopJumpFalseLong0;
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            int offset = (high << 8) | low;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && !b) {
                currentContext.PC += offset;
            }
        } else if (bytecode >= Bytecode.PopJumpTrueLong0 && bytecode <= Bytecode.PopJumpTrueLong3) {
            int high = (int)bytecode - (int)Bytecode.PopJumpTrueLong0;
            byte low = currentContext.Method.Bytecodes[currentContext.PC++];
            int offset = (high << 8) | low;
            var condObj = currentContext.Stack.Pop();
            if (condObj is bool b && b) {
                currentContext.PC += offset;
            }
        } else if ((byte)bytecode >= 128) {
            SendSelector((byte)bytecode);
        } else {
            throw new NotImplementedException($"Unhandled bytecode: {bytecode}");
        }
    }

    /// <summary>
    ///     Returns from the current context with the specified value.
    /// </summary>
    /// <param name="returnValue">The return value.</param>
    public void ReturnFrom(object returnValue)
    {
        if (currentContext.Sender == null) {
            LastResult = returnValue;
            if (ExitOnReturn) {
                Console.WriteLine($"Returned: {returnValue}");
                Environment.Exit(0);
            }
            IsRunning = false;
        } else {
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
        try {
            ReturnFrom(returnValue);
        } catch (NonLocalReturnException ex) {
            while (currentContext != null && currentContext != ex.TargetContext) {
                currentContext = currentContext.Sender!;
            }
            if (currentContext == null) {
                throw new Exception("Non-local return failed: target context not found");
            }
            currentContext.Stack.Push(ex.ReturnValue);
        }
    }

    /// <summary>
    ///     Performs the message send indicated by the specified bytecode.
    /// </summary>
    /// <param name="bytecode">The send bytecode.</param>
    public void SendSelector(byte bytecode)
    {
        int selectorIndex = bytecode - 128;
        var selector = (Symbol)currentContext.Method.Literals[selectorIndex];

        object? argument = currentContext.Stack.Pop();
        object? receiver = currentContext.Stack.Pop();

        if (receiver is int li && argument is int ri && selector.Name == "+") {
            currentContext.Stack.Push(li + ri);
            return;
        }

        if (receiver is SmalltalkObject recvObj) {
            var cls = recvObj.ClassPointer ??
                      throw new InvalidOperationException("Receiver has no class pointer");
            var method = cls.Lookup(selector) ??
                         throw new InvalidOperationException($"Method {selector} not found");

            if (method.HasPrimitive &&
                Primitives.Execute(method.PrimitiveIndex, currentContext)) {
                return;
            }

            var newContext = new Context {
                Sender = currentContext,
                Method = method,
                Receiver = recvObj,
                Arguments = new[] { argument },
                Temporaries = new object[method.TemporariesCount],
                Stack = new Stack<object?>(),
                PC = 0
            };
            currentContext = newContext;
            return;
        }

        throw new InvalidOperationException($"Cannot send {selector} to {receiver}");
    }
}