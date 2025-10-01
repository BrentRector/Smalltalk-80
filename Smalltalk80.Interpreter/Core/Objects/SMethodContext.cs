namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Generated.Layouts;

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
/// 
/// CRITICAL: MethodContext must be created through SClass.CreateInstance,
/// not directly with new, to ensure proper GC management.
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
    /// Gets this context as a STMethodContextCorrect layout for type-safe access.
    /// </summary>
    private STMethodContextCorrect AsLayout() => new STMethodContextCorrect(SObjectRef.FromObject(this));
    
    /// <summary>
    /// Legacy constructor for backward compatibility - DEPRECATED.
    /// </summary>
    [Obsolete("Use CreateMethodContext factory method instead")]
    public SMethodContext(SClass contextClass, SCompiledMethod method, SObject receiver, SObject[] arguments)
        : base(contextClass, 5 + method.NumArgs + method.NumTemps + method.StackSize)  // 5 fixed fields + temps + stack
    {
        Initialize(method, receiver, arguments);
    }
    
    /// <summary>
    /// Default constructor for creation through SClass.CreateInstance.
    /// </summary>
    internal SMethodContext(SClass contextClass, int size = 32) : base(contextClass, size)
    {
        // Will be initialized through Initialize method
    }
    
    /// <summary>
    /// Initializes this method context with the given data.
    /// </summary>
    public void Initialize(SCompiledMethod method, SObject receiver, SObject[] arguments)
    {
        // Calculate sizes for reference
        var tempSize = method.NumArgs + method.NumTemps;
        var stackSize = method.StackSize;
        
        // Ensure we have enough space
        var requiredSize = 5 + tempSize + stackSize;
        if (InstanceVariableCount < requiredSize)
        {
            // Resize if needed - this should rarely happen with proper pre-allocation
            ResizeInstanceVariables(requiredSize);
        }
        
        // Initialize fixed fields
        SetSender(null);
        SetInstructionPointer(SObjectRef.FromSmallInteger(0));
        SetStackPointer(SObjectRef.FromSmallInteger(5 + tempSize)); // Stack starts after fixed fields (5) and temporaries
        SetMethod(method);
        SetReceiver(receiver);

        // Copy arguments into temporaries area
        for (int i = 0; i < arguments.Length && i < method.NumArgs; i++)
        {
            SetTemporary(i, SObjectRef.FromObject(arguments[i]));
        }

        // Initialize remaining temporaries to nil
        for (int i = arguments.Length; i < tempSize; i++)
        {
            SetTemporary(i, SObjectRef.Nil);
        }

        // Initialize stack area to nil
        for (int i = 0; i < stackSize; i++)
        {
            SetInstanceVariable(5 + tempSize + i, SObjectRef.Nil);
        }
    }
    
    /// <summary>
    /// Gets or sets the sender context.
    /// </summary>
    public SObjectRef Sender
    {
        get => AsLayout().Sender;
        set
        {
            var layout = AsLayout();
            layout.Sender = value;
        }
    }

    /// <summary>
    /// Gets or sets the instruction pointer as a SmallInteger.
    /// </summary>
    public int InstructionPointer
    {
        get => AsLayout().IP;
        set
        {
            var layout = AsLayout();
            layout.IP = value;
        }
    }

    /// <summary>
    /// Gets or sets the stack pointer as a SmallInteger.
    /// </summary>
    public int StackPointer
    {
        get => AsLayout().SP;
        set
        {
            var layout = AsLayout();
            layout.SP = value;
        }
    }
    
    /// <summary>
    /// Gets the CompiledMethod being executed.
    /// </summary>
    public SCompiledMethod Method
    {
        get
        {
            var valueRef = AsLayout().Method;
            if (!valueRef.IsHeapObject)
                throw new InvalidOperationException("Method is not a heap object");
            var value = valueRef.AsObject();
            if (value is not SCompiledMethod compiledMethod)
                throw new InvalidOperationException($"Method is not a CompiledMethod, it's a {value!.GetType().Name}: {value}");
            return compiledMethod;
        }
    }

    /// <summary>
    /// Gets the receiver of the message.
    /// </summary>
    public SObjectRef Receiver
    {
        get => AsLayout().Receiver;
    }
    
    /// <summary>
    /// Sets the sender context.
    /// </summary>
    private void SetSender(SObject? sender)
    {
        var layout = AsLayout();
        layout.Sender = sender != null ? SObjectRef.FromObject(sender) : SObjectRef.Nil;
    }

    /// <summary>
    /// Sets the instruction pointer.
    /// </summary>
    private void SetInstructionPointer(SObjectRef ip)
    {
        var layout = AsLayout();
        layout.InstructionPointer = ip;
    }

    /// <summary>
    /// Sets the stack pointer.
    /// </summary>
    private void SetStackPointer(SObjectRef sp)
    {
        var layout = AsLayout();
        layout.StackPointer = sp;
    }

    /// <summary>
    /// Sets the method.
    /// </summary>
    private void SetMethod(SCompiledMethod method)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method), "Method cannot be null");
        var layout = AsLayout();
        layout.Method = SObjectRef.FromObject(method);

        // Verify it was set correctly
        var stored = layout.Method;
        if (!stored.IsHeapObject || stored.AsObject() != method)
            throw new InvalidOperationException($"Failed to store method. Expected {method}, got {stored}");
    }

    /// <summary>
    /// Sets the receiver.
    /// </summary>
    private void SetReceiver(SObject receiver)
    {
        var layout = AsLayout();
        layout.Receiver = SObjectRef.FromObject(receiver);
    }
    
    /// <summary>
    /// Gets a temporary variable by index.
    /// </summary>
    public SObjectRef GetTemporary(int index)
    {
        return AsLayout().GetTemporary(index);
    }

    /// <summary>
    /// Sets a temporary variable by index.
    /// </summary>
    public void SetTemporary(int index, SObjectRef value)
    {
        AsLayout().SetTemporary(index, value);
    }

    /// <summary>
    /// Pushes a value onto the stack.
    /// </summary>
    public void Push(SObjectRef value)
    {
        AsLayout().Push(value);
    }

    /// <summary>
    /// Pops a value from the stack.
    /// </summary>
    public SObjectRef Pop()
    {
        return AsLayout().Pop();
    }

    /// <summary>
    /// Gets the value at the top of the stack without removing it.
    /// </summary>
    public SObjectRef Top()
    {
        return AsLayout().Top();
    }

    /// <summary>
    /// Gets the current stack depth.
    /// </summary>
    public int StackDepth()
    {
        var sp = StackPointer;
        var baseStack = Method.NumArgs + Method.NumTemps;
        return sp - baseStack;
    }
    
    /// <summary>
    /// Factory method to create a MethodContext as a proper Smalltalk object.
    /// </summary>
    public static SMethodContext CreateMethodContext(SCompiledMethod method, SObject receiver, SObject[] arguments)
    {
        var contextClass = SystemDictionary.LookupClass("MethodContext")
            ?? throw new InvalidOperationException("MethodContext class not found");
        
        // Calculate required size
        var requiredSize = 5 + method.NumArgs + method.NumTemps + method.StackSize;
        
        // Create through proper Smalltalk instantiation
        var context = contextClass.CreateInstance() as SMethodContext;
        if (context == null)
        {
            // Fallback if class doesn't create the right type
            context = new SMethodContext(contextClass, requiredSize);
            
            // Note: GC registration should happen through SClass.CreateInstance
            // or by the caller who has access to the VM
        }
        
        // Initialize the context
        context.Initialize(method, receiver, arguments);
        
        return context;
    }
    
    /// <summary>
    /// Creates a new home context for a block (legacy compatibility).
    /// </summary>
    [Obsolete("Use CreateMethodContext instead")]
    public static SMethodContext CreateHomeContext(SClass contextClass, SCompiledMethod method, 
        SObject receiver, SObject[] arguments)
    {
        return CreateMethodContext(method, receiver, arguments);
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
        var className = Receiver.AsObject()?.Class?.Name ?? "Unknown";
        return $"{className}>>{methodName}";
    }
}