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
        SetInstructionPointer(SObjectRef.FromSmallInteger(initialIP));
        SetStackPointer(SObjectRef.FromSmallInteger(6)); // Stack starts at index 6
        SetArgumentCount(SObjectRef.FromSmallInteger(argumentCount));
        SetInitialIP(SObjectRef.FromSmallInteger(initialIP));
        SetHome(home);

        // Initialize stack to nil
        for (int i = 0; i < stackSize; i++)
        {
            SetInstanceVariable(6 + i, SObjectRef.Nil);
        }
    }
    
    /// <summary>
    /// Gets or sets the caller context.
    /// </summary>
    public SObjectRef Caller
    {
        get => GetInstanceVariable(CallerIndex);
        set => SetInstanceVariable(CallerIndex, value);
    }
    
    /// <summary>
    /// Gets or sets the instruction pointer as a SmallInteger.
    /// </summary>
    public int InstructionPointer
    {
        get => GetInstanceVariable(InstructionPointerIndex).AsSmallInteger();
        set => SetInstanceVariable(InstructionPointerIndex, SObjectRef.FromSmallInteger(value));
    }

    /// <summary>
    /// Gets or sets the stack pointer as a SmallInteger.
    /// </summary>
    public int StackPointer
    {
        get => GetInstanceVariable(StackPointerIndex).AsSmallInteger();
        set => SetInstanceVariable(StackPointerIndex, SObjectRef.FromSmallInteger(value));
    }

    /// <summary>
    /// Gets the number of arguments this block expects.
    /// </summary>
    public int ArgumentCount
    {
        get => GetInstanceVariable(ArgumentCountIndex).AsSmallInteger();
    }

    /// <summary>
    /// Gets the initial instruction pointer for the block.
    /// </summary>
    public int InitialIP
    {
        get => GetInstanceVariable(InitialIPIndex).AsSmallInteger();
    }

    /// <summary>
    /// Gets the home MethodContext.
    /// </summary>
    public SMethodContext Home
    {
        get
        {
            var homeRef = GetInstanceVariable(HomeIndex);
            if (!homeRef.IsHeapObject)
                throw new InvalidOperationException("Home context is not a heap object");
            return (SMethodContext)homeRef.AsObject()!;
        }
    }
    
    /// <summary>
    /// Gets the method from the home context.
    /// </summary>
    public SCompiledMethod Method => Home.Method;
    
    /// <summary>
    /// Gets the receiver from the home context.
    /// </summary>
    public SObjectRef Receiver => Home.Receiver;

    /// <summary>
    /// Sets the caller context.
    /// </summary>
    private void SetCaller(SObject? caller) => SetInstanceVariable(CallerIndex, caller != null ? SObjectRef.FromObject(caller) : SObjectRef.Nil);

    /// <summary>
    /// Sets the instruction pointer.
    /// </summary>
    private void SetInstructionPointer(SObjectRef ip) => SetInstanceVariable(InstructionPointerIndex, ip);

    /// <summary>
    /// Sets the stack pointer.
    /// </summary>
    private void SetStackPointer(SObjectRef sp) => SetInstanceVariable(StackPointerIndex, sp);

    /// <summary>
    /// Sets the argument count.
    /// </summary>
    private void SetArgumentCount(SObjectRef count) => SetInstanceVariable(ArgumentCountIndex, count);

    /// <summary>
    /// Sets the initial IP.
    /// </summary>
    private void SetInitialIP(SObjectRef ip) => SetInstanceVariable(InitialIPIndex, ip);

    /// <summary>
    /// Sets the home context.
    /// </summary>
    private void SetHome(SMethodContext home) => SetInstanceVariable(HomeIndex, SObjectRef.FromObject(home));
    
    /// <summary>
    /// Gets a temporary variable from the home context.
    /// </summary>
    public SObjectRef GetTemporary(int index)
    {
        return Home.GetTemporary(index);
    }

    /// <summary>
    /// Sets a temporary variable in the home context.
    /// </summary>
    public void SetTemporary(int index, SObjectRef value)
    {
        Home.SetTemporary(index, value);
    }

    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    public void Push(SObjectRef value)
    {
        var sp = StackPointer;
        SetInstanceVariable(sp, value);
        StackPointer = sp + 1;
    }

    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    public SObjectRef Pop()
    {
        var sp = StackPointer - 1;
        if (sp < 6) // Stack starts at index 6
        {
            throw new InvalidOperationException("Stack underflow");
        }
        StackPointer = sp;
        return GetInstanceVariable(sp);
    }

    /// <summary>
    /// Gets the value at the top of the stack without removing it.
    /// </summary>
    public SObjectRef Top()
    {
        var sp = StackPointer - 1;
        if (sp < 6)
        {
            throw new InvalidOperationException("Stack is empty");
        }
        return GetInstanceVariable(sp);
    }
    
    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    public int StackDepth()
    {
        return StackPointer - 6;
    }

    /// <summary>
    /// Copies arguments from the caller's stack to this block's stack.
    /// </summary>
    public void CopyArgumentsFrom(SObjectRef[] arguments)
    {
        if (arguments.Length != ArgumentCount)
        {
            throw new ArgumentException($"Block expects {ArgumentCount} arguments but got {arguments.Length}");
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
    public void Activate(SObject? caller, SObjectRef[] arguments)
    {
        SetCaller(caller);
        InstructionPointer = InitialIP;
        StackPointer = 6; // Reset stack
        CopyArgumentsFrom(arguments);
    }

    /// <summary>
    /// Performs a non-local return from this block.
    /// </summary>
    /// <remarks>
    /// Non-local returns must return from the home method context,
    /// not just from the block itself.
    /// </remarks>
    public (SMethodContext homeContext, SObjectRef returnValue) NonLocalReturn(SObjectRef returnValue)
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
        var homeMethod = Home.Method.Selector ?? "unknown";
        var receiverRef = Home.Receiver;
        var className = receiverRef.IsHeapObject && receiverRef.AsObject() != null
            ? receiverRef.AsObject()!.Class?.Name ?? "Unknown"
            : "Unknown";
        return $"[] in {className}>>{homeMethod}";
    }
}