using Core.Memory;

namespace Core.Abstractions.Execution;

/// <summary>
/// Manages stack operations within contexts.
/// Key design decision from Interpreter Architecture.md: 
/// Stack frames are separate from Smalltalk Context objects.
/// </summary>
public interface IStackFrame
{
    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    /// <param name="value">The value to push.</param>
    void Push(OOP value);
    
    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    /// <returns>The popped value.</returns>
    /// <exception cref="StackUnderflowException">If the stack is empty.</exception>
    OOP Pop();
    
    /// <summary>
    /// Peeks at the top value without removing it.
    /// </summary>
    /// <returns>The top value.</returns>
    /// <exception cref="StackUnderflowException">If the stack is empty.</exception>
    OOP Peek();
    
    /// <summary>
    /// Safely attempts to pop a value.
    /// </summary>
    /// <param name="value">The popped value if successful.</param>
    /// <returns>True if pop succeeded, false if stack was empty.</returns>
    bool TryPop(out OOP value);
    
    /// <summary>
    /// Safely attempts to peek at the top value.
    /// </summary>
    /// <param name="value">The top value if successful.</param>
    /// <returns>True if peek succeeded, false if stack was empty.</returns>
    bool TryPeek(out OOP value);
    
    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    int Depth { get; }
    
    /// <summary>
    /// Gets whether the stack is empty.
    /// </summary>
    bool IsEmpty { get; }
    
    /// <summary>
    /// Clears all values from the stack.
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Duplicates the top value on the stack.
    /// </summary>
    void Dup();
    
    /// <summary>
    /// Pops multiple values from the stack.
    /// </summary>
    /// <param name="count">Number of values to pop.</param>
    /// <returns>Array of popped values in reverse order (top of stack first).</returns>
    OOP[] PopMultiple(int count);
    
    /// <summary>
    /// Pushes multiple values onto the stack.
    /// </summary>
    /// <param name="values">Values to push.</param>
    void PushMultiple(params OOP[] values);
}

/// <summary>
/// Exception thrown when a stack underflow occurs.
/// </summary>
public sealed class StackUnderflowException : Exception
{
    public int PC { get; }
    public int StackDepth { get; }
    
    public StackUnderflowException(string message, int pc = 0, int depth = 0) 
        : base(message)
    {
        PC = pc;
        StackDepth = depth;
    }
}