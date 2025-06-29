namespace Smalltalk80;

/// <summary>
///     Represents an execution frame for a Smalltalk method. In addition to the
///     evaluation stack it stores the receiver, temporary variables and literal
///     frame required when interpreting bytecodes.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>
    ///     Gets or sets the program counter indicating the next bytecode to execute.
    /// </summary>
    public int ProgramCounter { get; set; }

    /// <summary>
    ///     Gets or sets the current stack pointer within <see cref="Stack"/>.
    /// </summary>
    public int StackPointer { get; set; }

    /// <summary>
    ///     Gets the evaluation stack used by the interpreter.
    /// </summary>
    public object[] Stack { get; }

    /// <summary>
    ///     Gets or sets the current message receiver.
    /// </summary>
    public object? Receiver { get; set; }

    /// <summary>
    ///     Gets the array of instance variables belonging to the receiver.
    ///     This interpreter stores them in a simple array for demonstration purposes.
    /// </summary>
    public object[] InstanceVariables { get; }

    /// <summary>
    ///     Gets the temporary variable frame for the active method.
    /// </summary>
    public object[] Temporaries { get; }

    /// <summary>
    ///     Gets the literal frame for the active method.
    /// </summary>
    public object[] Literals { get; }

    /// <summary>
    ///     Gets the sender context. This is the context to which <c>return</c>
    ///     bytecodes will transfer control.
    /// </summary>
    public ExecutionContext? Sender { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// </summary>
    /// <param name="stackSize">Size of the evaluation stack.</param>
    /// <param name="instanceVariableCount">Number of instance variables in the receiver.</param>
    /// <param name="temporaryCount">Number of temporary variables (including arguments).</param>
    /// <param name="literalCount">Number of literals in the method's literal frame.</param>
    /// <param name="sender">Optional sender context.</param>
    public ExecutionContext(
        int stackSize = 1024,
        int instanceVariableCount = 0,
        int temporaryCount = 0,
        int literalCount = 0,
        ExecutionContext? sender = null,
        ExecutionContext? homeContext = null,
        Type? methodClass = null)
    {
        Stack = new object[stackSize];
        InstanceVariables = new object[instanceVariableCount];
        Temporaries = new object[temporaryCount];
        Literals = new object[literalCount];
        Sender = sender;
        HomeContext = homeContext ?? this;
        MethodClass = methodClass;
    }

    /// <summary>
    ///     Gets the home context for block execution. For a method context this
    ///     is the context itself; for a block context it references the method
    ///     context in which the block was defined.
    /// </summary>
    public ExecutionContext HomeContext { get; }

    /// <summary>
    ///     Gets or sets the class in whose message dictionary the active method
    ///     was found. This is required to correctly perform <c>super</c> sends.
    /// </summary>
    public Type? MethodClass { get; set; }
}

