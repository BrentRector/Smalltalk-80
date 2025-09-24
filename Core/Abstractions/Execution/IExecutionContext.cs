using Core.Memory;

namespace Core.Abstractions.Execution;

/// <summary>
/// Represents an execution context (method or block) in the Smalltalk virtual machine.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Gets the receiver of this context.
    /// </summary>
    OOP Receiver { get; }

    /// <summary>
    /// Gets or sets the sender context (caller).
    /// </summary>
    IExecutionContext? Sender { get; set; }

    /// <summary>
    /// Gets or sets the current program counter.
    /// </summary>
    int ProgramCounter { get; set; }

    /// <summary>
    /// Gets the current stack pointer.
    /// </summary>
    int StackPointer { get; }

    /// <summary>
    /// Gets the value at the top of the stack.
    /// </summary>
    OOP StackTop { get; }

    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    void Push(OOP value);

    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    OOP Pop();

    /// <summary>
    /// Peeks at a value on the stack without removing it.
    /// </summary>
    OOP Peek(int offset = 0);

    /// <summary>
    /// Duplicates the top stack value.
    /// </summary>
    void Dup();

    /// <summary>
    /// Gets an argument value.
    /// </summary>
    OOP GetArgument(int index);

    /// <summary>
    /// Gets a temporary variable value.
    /// </summary>
    OOP GetTemporary(int index);

    /// <summary>
    /// Sets a temporary variable value.
    /// </summary>
    void SetTemporary(int index, OOP value);

    /// <summary>
    /// Gets a literal value.
    /// </summary>
    OOP GetLiteral(int index);

    /// <summary>
    /// Gets the next bytecode and advances the program counter.
    /// </summary>
    byte GetNextBytecode();

    /// <summary>
    /// Peeks at the next bytecode without advancing the program counter.
    /// </summary>
    byte PeekNextBytecode();

    /// <summary>
    /// Checks if the program counter is at the end of the bytecodes.
    /// </summary>
    bool IsAtEnd();

    /// <summary>
    /// Performs a relative jump.
    /// </summary>
    void Jump(int offset);

    /// <summary>
    /// Performs an absolute jump.
    /// </summary>
    void JumpTo(int address);
}