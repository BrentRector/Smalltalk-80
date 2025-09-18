using System;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Execution;
/// <summary>
/// Execution context for a method invocation.
/// Represents the activation record on the execution stack.
/// </summary>
public class MethodContext
{
    /// <summary>
    /// The sender context (caller).
    /// </summary>
    public MethodContext? Sender { get; set; }

    /// <summary>
    /// The receiver of the message.
    /// </summary>
    public OOP Receiver { get; }

    /// <summary>
    /// The compiled method being executed.
    /// </summary>
    public OOP Method { get; }

    /// <summary>
    /// Method arguments.
    /// </summary>
    public OOP[] Arguments { get; }

    /// <summary>
    /// Temporary variables.
    /// </summary>
    public OOP[] Temporaries { get; }

    /// <summary>
    /// Execution stack for this context.
    /// </summary>
    public Stack Stack { get; }

    /// <summary>
    /// Program counter (index into bytecode).
    /// </summary>
    public int PC { get; set; }

    /// <summary>
    /// Creates a new method context.
    /// </summary>
    public MethodContext(
        MethodContext? sender,
        OOP receiver,
        OOP method,
        OOP[] arguments,
        int tempCount,
        int stackSize)
    {
        Sender = sender;
        Receiver = receiver;
        Method = method;
        Arguments = arguments ?? [];
        Temporaries = new OOP[tempCount];
        Stack = new Stack(stackSize);
        PC = 0;

        // Initialize temporaries to nil
        Array.Fill(Temporaries, OOP.Nil);
    }

    /// <summary>
    /// Gets the home context (for blocks, otherwise self).
    /// </summary>
    public virtual MethodContext GetHome() => this; // For method contexts, home is self

    /// <summary>
    /// Checks if this context can be returned from.
    /// </summary>
    public bool IsReturnable => Sender != null;

    /// <summary>
    /// Gets a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"MethodContext[Receiver={Receiver}, Method={Method}, PC={PC}, Stack={Stack.Count}]";
}

/// <summary>
/// Execution stack for operands.
/// </summary>
/// <param name="capacity">Stack capacity.</param>
public sealed class Stack(int capacity)
{
    private readonly OOP[] items = capacity > 0
        ? new OOP[capacity]
        : throw new ArgumentException("Stack capacity must be positive", nameof(capacity));
    private int sp = -1; // Stack pointer, -1 means empty

    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    public void Push(OOP value)
    {
        if (sp >= items.Length - 1)
            throw new InvalidOperationException("Stack overflow");
        
        items[++sp] = value;
    }

    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    public OOP Pop() => sp < 0
        ? throw new InvalidOperationException("Stack underflow")
        : items[sp--];

    /// <summary>
    /// Peeks at the top value without removing it.
    /// </summary>
    public OOP Top => sp < 0
        ? throw new InvalidOperationException("Stack is empty")
        : items[sp];

    /// <summary>
    /// Gets the number of items on the stack.
    /// </summary>
    public int Count => sp + 1;

    /// <summary>
    /// Checks if the stack is empty.
    /// </summary>
    public bool IsEmpty => sp < 0;

    /// <summary>
    /// Clears the stack.
    /// </summary>
    public void Clear() => sp = -1;

    /// <summary>
    /// Duplicates the top value on the stack.
    /// </summary>
    public void Duplicate()
    {
        if (sp < 0)
            throw new InvalidOperationException("Stack is empty");
        
        Push(items[sp]);
    }

    /// <summary>
    /// Pops multiple values from the stack.
    /// </summary>
    public OOP[] PopN(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        
        if (count > Count)
            throw new InvalidOperationException("Not enough items on stack");
        
        var result = new OOP[count];
        for (int i = count - 1; i >= 0; i--)
        {
            result[i] = Pop();
        }
        return result;
    }
}