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
            byte bytecode = currentContext.Method.Bytecodes[currentContext.PC++];
            Dispatch(bytecode);
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
            Temporaries = temporaries ?? new object[method.TemporariesCount],
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
    public void Dispatch(byte bytecode)
    {
        Tracer.Log($"Dispatching bytecode: {bytecode} at PC: {currentContext.PC - 1}");
        if (bytecode <= 15) {
            currentContext.Stack.Push(currentContext.Receiver.Fields[bytecode]);
        } else if (bytecode <= 31) {
            currentContext.Stack.Push(currentContext.Temporaries[bytecode - 16]);
        } else if (bytecode <= 63) {
            currentContext.Stack.Push(currentContext.Method.Literals[bytecode - 32]);
        } else if (bytecode == 124) {
            var returnValue = currentContext.Stack.Count > 0
                ? currentContext.Stack.Pop()
                : currentContext.Receiver;
            HandleReturn(returnValue!);
        } else if (bytecode >= 128) {
            SendSelector(bytecode);
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