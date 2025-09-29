namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a method context (activation record) in the Smalltalk-80 virtual machine.
/// This is a proper Smalltalk object following the Blue Book specification.
/// </summary>
/// <remarks>
/// From the Blue Book page 580:
/// MethodContext instances represent the execution state of a CompiledMethod.
/// They contain the receiver, arguments, temporaries, stack, and control flow information.
/// 
/// Instance variables (as per Blue Book):
/// - sender: The context that sent the message (or nil)
/// - instructionPointer: The current bytecode position
/// - stackPointer: The current stack position
/// - method: The CompiledMethod being executed
/// - receiver: The receiver of the message
/// - arguments: Array of arguments (stored in first part of temporaries)
/// - temporaries: Array for arguments and temporary variables
/// - stack: The evaluation stack
/// </remarks>
public sealed class SMethodContext : SInstance
{
    // Blue Book standard field indices for MethodContext
    private const int SenderIndex = 0;
    private const int InstructionPointerIndex = 1;
    private const int StackPointerIndex = 2;
    private const int MethodIndex = 3;
    private const int ReceiverIndex = 4;
    // Note: Arguments and temporaries start at index 5
    // The stack follows the temporaries
    
    /// <summary>
    /// Initializes a new instance of the SMethodContext class.
    /// </summary>
    /// <param name="contextClass">The MethodContext class.</param>
    /// <param name="method">The CompiledMethod being executed.</param>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="arguments">The arguments to the method.</param>
    public SMethodContext(SClass contextClass, SCompiledMethod method, SObject receiver, SObject[] arguments)
        : base(contextClass, 5 + method.NumArgs + method.NumTemps + method.StackSize)  // 5 fixed fields + temps + stack
    {
        // Calculate sizes for reference
        var tempSize = method.NumArgs + method.NumTemps;
        var stackSize = method.StackSize;
        
        // Initialize fixed fields
        SetSender(null);
        SetInstructionPointer(SmallInteger.From(0));
        SetStackPointer(SmallInteger.From(tempSize)); // Stack starts after temporaries
        SetMethod(method);
        SetReceiver(receiver);
        
        // Copy arguments into temporaries area
        for (int i = 0; i < arguments.Length && i < method.NumArgs; i++)
        {
            SetTemporary(i, arguments[i]);
        }
        
        // Initialize remaining temporaries to nil
        for (int i = arguments.Length; i < tempSize; i++)
        {
            SetTemporary(i, SNil.Instance);
        }
        
        // Stack is initially empty (all nil)
        for (int i = 0; i < stackSize; i++)
        {
            Push(SNil.Instance);
        }
        SetStackPointer(SmallInteger.From(tempSize)); // Reset stack pointer
    }
    
    /// <summary>
    /// Gets or sets the sender context.
    /// </summary>
    public SObject? Sender
    {
        get => GetInstanceVariable(SenderIndex);
        set => SetInstanceVariable(SenderIndex, value ?? SNil.Instance);
    }
    
    /// <summary>
    /// Gets or sets the instruction pointer as a SmallInteger.
    /// </summary>
    public SmallInteger InstructionPointer
    {
        get => (SmallInteger)(GetInstanceVariable(InstructionPointerIndex) ?? SmallInteger.From(0));
        set => SetInstanceVariable(InstructionPointerIndex, value);
    }
    
    /// <summary>
    /// Gets or sets the stack pointer as a SmallInteger.
    /// </summary>
    public SmallInteger StackPointer
    {
        get => (SmallInteger)(GetInstanceVariable(StackPointerIndex) ?? SmallInteger.From(0));
        set => SetInstanceVariable(StackPointerIndex, value);
    }
    
    /// <summary>
    /// Gets the CompiledMethod being executed.
    /// </summary>
    public SCompiledMethod Method
    {
        get => (SCompiledMethod)(GetInstanceVariable(MethodIndex) ?? throw new InvalidOperationException("Method is null"));
    }
    
    /// <summary>
    /// Gets the receiver of the message.
    /// </summary>
    public SObject Receiver
    {
        get => GetInstanceVariable(ReceiverIndex) ?? SNil.Instance;
    }
    
    /// <summary>
    /// Sets the sender context.
    /// </summary>
    private void SetSender(SObject? sender) => SetInstanceVariable(SenderIndex, sender ?? SNil.Instance);
    
    /// <summary>
    /// Sets the instruction pointer.
    /// </summary>
    private void SetInstructionPointer(SmallInteger ip) => SetInstanceVariable(InstructionPointerIndex, ip);
    
    /// <summary>
    /// Sets the stack pointer.
    /// </summary>
    private void SetStackPointer(SmallInteger sp) => SetInstanceVariable(StackPointerIndex, sp);
    
    /// <summary>
    /// Sets the method.
    /// </summary>
    private void SetMethod(SCompiledMethod method) => SetInstanceVariable(MethodIndex, method);
    
    /// <summary>
    /// Sets the receiver.
    /// </summary>
    private void SetReceiver(SObject receiver) => SetInstanceVariable(ReceiverIndex, receiver);
    
    /// <summary>
    /// Gets a temporary variable by index.
    /// </summary>
    public SObject GetTemporary(int index)
    {
        return GetInstanceVariable(5 + index) ?? SNil.Instance;
    }
    
    /// <summary>
    /// Sets a temporary variable by index.
    /// </summary>
    public void SetTemporary(int index, SObject value)
    {
        SetInstanceVariable(5 + index, value);
    }
    
    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    public void Push(SObject value)
    {
        var sp = StackPointer.Value;
        SetInstanceVariable((int)sp, value);
        SetStackPointer(SmallInteger.From(sp + 1));
    }
    
    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    public SObject Pop()
    {
        var sp = StackPointer.Value - 1;
        if (sp < Method.NumArgs + Method.NumTemps)
        {
            throw new InvalidOperationException("Stack underflow");
        }
        SetStackPointer(SmallInteger.From(sp));
        return GetInstanceVariable((int)sp) ?? SNil.Instance;
    }
    
    /// <summary>
    /// Gets the value at the top of the stack without removing it.
    /// </summary>
    public SObject Top()
    {
        var sp = StackPointer.Value - 1;
        if (sp < Method.NumArgs + Method.NumTemps)
        {
            throw new InvalidOperationException("Stack is empty");
        }
        return GetInstanceVariable((int)sp) ?? SNil.Instance;
    }
    
    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    public int StackDepth()
    {
        var sp = StackPointer.Value;
        var baseStack = Method.NumArgs + Method.NumTemps;
        return (int)(sp - baseStack);
    }
    
    /// <summary>
    /// Creates a new home context for a block.
    /// </summary>
    public static SMethodContext CreateHomeContext(SClass contextClass, SCompiledMethod method, 
        SObject receiver, SObject[] arguments)
    {
        return new SMethodContext(contextClass, method, receiver, arguments);
    }
    
    /// <summary>
    /// Checks if this is a block context (always false for MethodContext).
    /// </summary>
    public bool IsBlockContext => false;
    
    /// <summary>
    /// Returns a string representation of this context.
    /// </summary>
    public override string ToDisplayString()
    {
        var methodName = Method?.Selector ?? "unknown";
        var className = Receiver?.Class?.Name ?? "Unknown";
        return $"{className}>>{methodName}";
    }
}