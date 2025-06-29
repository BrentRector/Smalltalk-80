using System;
using System.Collections.Generic;

public class Context {
    public Context Sender;
    public CompiledMethod Method;
    public SmalltalkObject Receiver;
    public Object[] Arguments;
    public Object[] Temporaries;
    public Stack<Object> Stack;
    public int PC;

    public static Context CreateFor(Symbol selector, int argCount, Context sender) {
        SmalltalkClass cls = sender.Receiver.ClassPointer;
        CompiledMethod method = cls.Lookup(selector);

        Object[] args = new Object[argCount];
        for (int i = argCount - 1; i >= 0; i--) {
            args[i] = sender.Stack.Pop();
        }

        return new Context {
            Sender = sender,
            Method = method,
            Receiver = sender.Receiver,
            Arguments = args,
            Temporaries = new Object[method.TemporariesCount],
            Stack = new Stack<Object>(),
            PC = 0
        };
    }
}