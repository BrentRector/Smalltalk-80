using System;

/// <summary>
///     Executes Smalltalk bytecodes and maintains runtime state.
/// </summary>
public class Interpreter {
    /// <summary>
    ///     The currently active execution <see cref="Context"/>.
    /// </summary>
    public Context currentContext;

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

    public void Execute() {
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
    public object? Evaluate(CompiledMethod method, SmalltalkObject receiver) {
        var previousExit = ExitOnReturn;
        ExitOnReturn = false;
        LastResult = null;
        currentContext = new Context {
            Sender = null!,
            Method = method,
            Receiver = receiver,
            Arguments = Array.Empty<object>(),
            Temporaries = new object[method.TemporariesCount],
            Stack = new Stack<object>(),
            PC = 0
        };
        Execute();
        ExitOnReturn = previousExit;
        return LastResult;
    }

    public void Dispatch(byte bytecode) {
        Tracer.Log($"Dispatching bytecode: {bytecode} at PC: {currentContext.PC - 1}");
        if (bytecode <= 15) {
            currentContext.Stack.Push(currentContext.Receiver.Fields[bytecode]);
        } else if (bytecode <= 31) {
            currentContext.Stack.Push(currentContext.Temporaries[bytecode - 16]);
        } else if (bytecode <= 63) {
            currentContext.Stack.Push(currentContext.Method.Literals[bytecode - 32]);
        } else if (bytecode == 124) {
            HandleReturn(currentContext.Receiver);
        } else if (bytecode >= 128) {
            SendSelector(bytecode);
        } else {
            throw new NotImplementedException($"Unhandled bytecode: {bytecode}");
        }
    }

    public void ReturnFrom(Object returnValue) {
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

    public void HandleReturn(Object returnValue) {
        try {
            ReturnFrom(returnValue);
        } catch (NonLocalReturnException ex) {
            while (currentContext != null && currentContext != ex.TargetContext) {
                currentContext = currentContext.Sender;
            }
            if (currentContext == null) {
                throw new Exception("Non-local return failed: target context not found");
            }
            currentContext.Stack.Push(ex.ReturnValue);
        }
    }

    public void SendSelector(byte bytecode) {
        int selectorIndex = bytecode - 128;
        Symbol selector = currentContext.Method.Literals[selectorIndex] as Symbol;
        int argCount = (bytecode - 128) % 16;

        if (currentContext.Method.HasPrimitive &&
            Primitives.Execute(currentContext.Method.PrimitiveIndex, currentContext)) {
            return;
        }

        Context newContext = Context.CreateFor(selector, argCount, currentContext);
        currentContext = newContext;
    }
}