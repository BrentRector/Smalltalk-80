public class Interpreter {
    public Context currentContext;

    public void Execute() {
        while (true) {
            byte bytecode = currentContext.Method.Bytecodes[currentContext.PC++];
            Dispatch(bytecode);
        }
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
            Console.WriteLine($"Returned: {returnValue}");
            Environment.Exit(0);
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