using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages the execution stack in the current execution context.
/// Provides Push, Pop, and Peek operations for the Smalltalk-80 VM.
/// </summary>
public interface IStackManager
{
    /// <summary>
    /// Pushes a value onto the operand stack of the current context.
    /// </summary>
    /// <param name="valueOOP">Value to push onto the stack.</param>
    /// <exception cref="InvalidOperationException">Thrown when stack overflow occurs.</exception>
    void Push(SmalltalkOOP valueOOP);

    /// <summary>
    /// Pops a value from the operand stack of the current context.
    /// </summary>
    /// <returns>The popped value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when stack underflow occurs (stack empty).</exception>
    SmalltalkOOP Pop();

    /// <summary>
    /// Peeks at the top value on the operand stack without removing it.
    /// </summary>
    /// <returns>The top stack value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when stack is empty.</exception>
    SmalltalkOOP Peek();
}
