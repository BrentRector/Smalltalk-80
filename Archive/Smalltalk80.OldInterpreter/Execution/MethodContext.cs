using System;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Config;

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
    /// Temporary variables (includes arguments at indices 0..argCount-1).
    /// In Smalltalk-80, arguments are the first temporary variables.
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
        
        // ENHANCED DEBUG OUTPUT FOR ARGUMENT TRACKING
        if (DebugOptions.EnableDebugOutput)
        {
            Console.WriteLine($"[DEBUG] MethodContext Constructor:");
            Console.WriteLine($"  - Receiver: {receiver}");
            Console.WriteLine($"  - Method: {method}");
            Console.WriteLine($"  - Argument count: {Arguments.Length}");
            Console.WriteLine($"  - Temp count: {tempCount}");
            Console.WriteLine($"  - Stack size: {stackSize}");
            
            // Show each argument being passed in
            for (int i = 0; i < Arguments.Length; i++)
            {
                var arg = Arguments[i];
                Console.WriteLine($"  - Argument[{i}]: {arg}");
                Console.WriteLine($"    Is nil? {arg.IsNil}");
                Console.WriteLine($"    Is SmallInteger? {arg.IsSmallInteger}");
                if (arg.IsSmallInteger)
                {
                    Console.WriteLine($"    SmallInteger value: {arg.SmallIntegerValue}");
                }
                else if (!arg.IsNil)
                {
                    Console.WriteLine($"    OOP raw value: {arg.RawValue}");
                }
            }
        }
        
        // In Smalltalk-80, arguments are stored as the first temporary variables
        // Total temporaries = arguments + additional temps
        Temporaries = new OOP[Arguments.Length + tempCount];
        
        // Copy arguments to the beginning of temporaries
        for (int i = 0; i < Arguments.Length; i++)
        {
            Temporaries[i] = Arguments[i];
            
            // DEBUG: Verify the copy
            if (DebugOptions.EnableDebugOutput)
            {
                if (!Temporaries[i].Equals(Arguments[i]))
                {
                    Console.WriteLine($"[ERROR] Argument copy failed at index {i}!");
                    Console.WriteLine($"  - Original: {Arguments[i]}");
                    Console.WriteLine($"  - Copied: {Temporaries[i]}");
                }
                else
                {
                    Console.WriteLine($"  - Copied Argument[{i}] to Temporaries[{i}]: {Temporaries[i]}");
                }
            }
        }
        
        // Initialize remaining temporaries to nil
        for (int i = Arguments.Length; i < Temporaries.Length; i++)
        {
            Temporaries[i] = OOP.Nil;
            
            if (DebugOptions.EnableDebugOutput)
            {
                Console.WriteLine($"  - Initialized Temporaries[{i}] to nil");
            }
        }
        
        if (DebugOptions.EnableDebugOutput)
        {
            Console.WriteLine($"  - Total Temporaries array length: {Temporaries.Length}");
        }
        
        Stack = new Stack(stackSize);
        PC = 0;
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