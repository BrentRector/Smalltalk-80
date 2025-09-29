namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a block context (block closure activation) in the Smalltalk-80 virtual machine.
/// This is a proper Smalltalk object following the Blue Book specification.
/// </summary>
/// <remarks>
/// From the Blue Book page 580:
/// BlockContext instances represent the execution state of a block.
/// They share the home context's method and temporaries but have their own stack.
/// 
/// Instance variables (as per Blue Book):
/// - caller: The context that activated this block
/// - instructionPointer: The current bytecode position
/// - stackPointer: The current stack position  
/// - argumentCount: Number of block arguments
/// - initialIP: The initial instruction pointer for the block
/// - home: The home MethodContext that created this block
/// </remarks>
public sealed class SBlockContext : SInstance
{
    // Blue Book standard field indices for BlockContext
    private const int CallerIndex = 0;
    private const int InstructionPointerIndex = 1;
    private const int StackPointerIndex = 2;
    private const int ArgumentCountIndex = 3;
    private const int InitialIPIndex = 4;
    private const int HomeIndex = 5;
    // Stack starts at index 6
    
    /// <summary>
    /// Initializes a new instance of the SBlockContext class.
    /// </summary>
    /// <param name="contextClass">The BlockContext class.</param>
    /// <param name="argumentCount">Number of arguments the block expects.</param>
    /// <param name="initialIP">The initial instruction pointer for the block.</param>
    /// <param name="home">The home MethodContext.</param>
    /// <param name="stackSize">Size of the stack for this block.</param>
    public SBlockContext(SClass contextClass, int argumentCount, int initialIP, 
        SMethodContext home, int stackSize = 20)
        : base(contextClass, 6 + stackSize)  // Total size: 6 fixed fields + stack
    {
        
        // Initialize fixed fields
        SetCaller(null);
        SetInstructionPointer(SmallInteger.From(initialIP));
        SetStackPointer(SmallInteger.From(6)); // Stack starts at index 6
        SetArgumentCount(SmallInteger.From(argumentCount));
        SetInitialIP(SmallInteger.From(initialIP));
        SetHome(home);
        
        // Initialize stack to nil
        for (int i = 0; i < stackSize; i++)
        {
            SetInstanceVariable(6 + i, SNil.Instance);
        }
    }
    
    /// <summary>
    /// Gets or sets the caller context.
    /// </summary>
    public SObject? Caller
    {
        get => GetInstanceVariable(CallerIndex);
        set => SetInstanceVariable(CallerIndex, value ?? SNil.Instance);
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
        get => (SmallInteger)(GetInstanceVariable(StackPointerIndex) ?? SmallInteger.From(6));
        set => SetInstanceVariable(StackPointerIndex, value);
    }
    
    /// <summary>
    /// Gets the number of arguments this block expects.
    /// </summary>
    public SmallInteger ArgumentCount
    {
        get => (SmallInteger)(GetInstanceVariable(ArgumentCountIndex) ?? SmallInteger.From(0));
    }
    
    /// <summary>
    /// Gets the initial instruction pointer for the block.
    /// </summary>
    public SmallInteger InitialIP
    {
        get => (SmallInteger)(GetInstanceVariable(InitialIPIndex) ?? SmallInteger.From(0));
    }
    
    /// <summary>
    /// Gets the home MethodContext.
    /// </summary>
    public SMethodContext Home
    {
        get => (SMethodContext)(GetInstanceVariable(HomeIndex) ?? 
            throw new InvalidOperationException("Home context is null"));
    }
    
    /// <summary>
    /// Gets the method from the home context.
    /// </summary>
    public SCompiledMethod Method => Home.Method;
    
    /// <summary>
    /// Gets the receiver from the home context.
    /// </summary>
    public SObject Receiver => Home.Receiver;
    
    /// <summary>
    /// Sets the caller context.
    /// </summary>
    private void SetCaller(SObject? caller) => SetInstanceVariable(CallerIndex, caller ?? SNil.Instance);
    
    /// <summary>
    /// Sets the instruction pointer.
    /// </summary>
    private void SetInstructionPointer(SmallInteger ip) => SetInstanceVariable(InstructionPointerIndex, ip);
    
    /// <summary>
    /// Sets the stack pointer.
    /// </summary>
    private void SetStackPointer(SmallInteger sp) => SetInstanceVariable(StackPointerIndex, sp);
    
    /// <summary>
    /// Sets the argument count.
    /// </summary>
    private void SetArgumentCount(SmallInteger count) => SetInstanceVariable(ArgumentCountIndex, count);
    
    /// <summary>
    /// Sets the initial IP.
    /// </summary>
    private void SetInitialIP(SmallInteger ip) => SetInstanceVariable(InitialIPIndex, ip);
    
    /// <summary>
    /// Sets the home context.
    /// </summary>
    private void SetHome(SMethodContext home) => SetInstanceVariable(HomeIndex, home);
    
    /// <summary>
    /// Gets a temporary variable from the home context.
    /// </summary>
    public SObject GetTemporary(int index)
    {
        return Home.GetTemporary(index);
    }
    
    /// <summary>
    /// Sets a temporary variable in the home context.
    /// </summary>
    public void SetTemporary(int index, SObject value)
    {
        Home.SetTemporary(index, value);
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
        if (sp < 6) // Stack starts at index 6
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
        if (sp < 6)
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
        return (int)(StackPointer.Value - 6);
    }
    
    /// <summary>
    /// Copies arguments from the caller's stack to this block's stack.
    /// </summary>
    public void CopyArgumentsFrom(SObject[] arguments)
    {
        if (arguments.Length != ArgumentCount.Value)
        {
            throw new ArgumentException($"Block expects {ArgumentCount.Value} arguments but got {arguments.Length}");
        }
        
        // Push arguments onto the block's stack
        foreach (var arg in arguments)
        {
            Push(arg);
        }
    }
    
    /// <summary>
    /// Activates this block with the given arguments.
    /// </summary>
    public void Activate(SObject? caller, SObject[] arguments)
    {
        SetCaller(caller);
        SetInstructionPointer(InitialIP);
        SetStackPointer(SmallInteger.From(6)); // Reset stack
        CopyArgumentsFrom(arguments);
    }
    
    /// <summary>
    /// Performs a non-local return from this block.
    /// </summary>
    /// <remarks>
    /// Non-local returns must return from the home method context,
    /// not just from the block itself.
    /// </remarks>
    public (SMethodContext homeContext, SObject returnValue) NonLocalReturn(SObject returnValue)
    {
        return (Home, returnValue);
    }
    
    /// <summary>
    /// Checks if this is a block context (always true for BlockContext).
    /// </summary>
    public bool IsBlockContext => true;
    
    /// <summary>
    /// Returns a string representation of this context.
    /// </summary>
    public override string ToDisplayString()
    {
        var homeMethod = Home?.Method?.Selector ?? "unknown";
        var className = Home?.Receiver?.Class?.Name ?? "Unknown";
        return $"[] in {className}>>{homeMethod}";
    }
}