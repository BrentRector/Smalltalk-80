// <summary>
// Manual context layout definitions that correctly represent the Blue Book specification.
// These types properly handle the inheritance hierarchy:
// InstructionStream -> ContextPart -> BlockContext/MethodContext
// </summary>

using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Correct layout for MethodContext objects following Blue Book specification.
/// Inherits from ContextPart which inherits from InstructionStream.
/// </summary>
public readonly struct STMethodContextCorrect
{
    private readonly SObjectRef _ref;

    /// <summary>
    /// Gets the underlying object reference.
    /// </summary>
    public SObjectRef AsRef => _ref;

    /// <summary>
    /// Initializes a new instance of <see cref="STMethodContextCorrect"/>.
    /// </summary>
    /// <param name="objectRef">The object reference.</param>
    public STMethodContextCorrect(SObjectRef objectRef) => _ref = objectRef;

    /// <summary>
    /// Implicitly converts a <see cref="STMethodContextCorrect"/> to <see cref="SObjectRef"/>.
    /// </summary>
    public static implicit operator SObjectRef(STMethodContextCorrect obj) => obj._ref;

    /// <summary>
    /// Implicitly converts a <see cref="SObjectRef"/> to <see cref="STMethodContextCorrect"/>.
    /// </summary>
    public static implicit operator STMethodContextCorrect(SObjectRef r) => new(r);

    /// <summary>
    /// Implicitly converts from nint to <see cref="STMethodContextCorrect"/>.
    /// </summary>
    public static implicit operator STMethodContextCorrect(nint oop) => new(SObjectRef.FromObjectPointer(oop));

    // Fields from InstructionStream
    /// <summary>
    /// Gets or sets the sender (return pointer) - from InstructionStream at offset 0.
    /// </summary>
    public SObjectRef Sender
    {
        get => _ref.GetInstanceVariable(0);
        set => _ref.SetInstanceVariable(0, value);
    }

    /// <summary>
    /// Gets or sets the program counter - from InstructionStream at offset 1.
    /// </summary>
    public SObjectRef InstructionPointer
    {
        get => _ref.GetInstanceVariable(1);
        set => _ref.SetInstanceVariable(1, value);
    }

    /// <summary>
    /// Gets or sets the InstructionPointer as an integer.
    /// </summary>
    public int IP
    {
        get => InstructionPointer.IsSmallInteger ? InstructionPointer.AsSmallInteger() : 0;
        set => InstructionPointer = SObjectRef.FromSmallInteger(value);
    }

    // Fields from ContextPart
    /// <summary>
    /// Gets or sets the stack pointer - from ContextPart at offset 2.
    /// </summary>
    public SObjectRef StackPointer
    {
        get => _ref.GetInstanceVariable(2);
        set => _ref.SetInstanceVariable(2, value);
    }

    /// <summary>
    /// Gets or sets the StackPointer as an integer.
    /// </summary>
    public int SP
    {
        get => StackPointer.IsSmallInteger ? StackPointer.AsSmallInteger() : 0;
        set => StackPointer = SObjectRef.FromSmallInteger(value);
    }

    // Fields from MethodContext
    /// <summary>
    /// Gets or sets the method - from MethodContext at offset 3.
    /// </summary>
    public SObjectRef Method
    {
        get => _ref.GetInstanceVariable(3);
        set => _ref.SetInstanceVariable(3, value);
    }

    /// <summary>
    /// Gets or sets the receiverMap (unused, for future multiple inheritance) - from MethodContext at offset 4.
    /// </summary>
    public SObjectRef ReceiverMap
    {
        get => _ref.GetInstanceVariable(4);
        set => _ref.SetInstanceVariable(4, value);
    }

    /// <summary>
    /// Gets or sets the receiver - from MethodContext at offset 5.
    /// </summary>
    public SObjectRef Receiver
    {
        get => _ref.GetInstanceVariable(5);
        set => _ref.SetInstanceVariable(5, value);
    }

    // Stack operations
    /// <summary>
    /// Pushes a value onto the context's stack.
    /// </summary>
    public void Push(SObjectRef value)
    {
        var sp = SP;
        _ref.SetInstanceVariable(sp, value);
        SP = sp + 1;
    }

    /// <summary>
    /// Pops a value from the context's stack.
    /// </summary>
    public SObjectRef Pop()
    {
        var sp = SP;
        if (sp <= 6) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack underflow");

        sp--;
        SP = sp;
        return _ref.GetInstanceVariable(sp);
    }

    /// <summary>
    /// Gets the top value from the context's stack without popping.
    /// </summary>
    public SObjectRef Top()
    {
        var sp = SP;
        if (sp <= 6) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack is empty");

        return _ref.GetInstanceVariable(sp - 1);
    }

    /// <summary>
    /// Gets a temporary variable from the context.
    /// Temporaries start after the fixed fields (at index 6).
    /// </summary>
    public SObjectRef GetTemporary(int index)
    {
        return _ref.GetInstanceVariable(6 + index);
    }

    /// <summary>
    /// Sets a temporary variable in the context.
    /// Temporaries start after the fixed fields (at index 6).
    /// </summary>
    public void SetTemporary(int index, SObjectRef value)
    {
        _ref.SetInstanceVariable(6 + index, value);
    }

    /// <summary>
    /// The number of fixed instance variables for MethodContext.
    /// </summary>
    internal const int FixedFieldCount = 6;
}

/// <summary>
/// Correct layout for BlockContext objects following Blue Book specification.
/// Inherits from ContextPart which inherits from InstructionStream.
/// </summary>
public readonly struct STBlockContextCorrect
{
    private readonly SObjectRef _ref;

    /// <summary>
    /// Gets the underlying object reference.
    /// </summary>
    public SObjectRef AsRef => _ref;

    /// <summary>
    /// Initializes a new instance of <see cref="STBlockContextCorrect"/>.
    /// </summary>
    /// <param name="objectRef">The object reference.</param>
    public STBlockContextCorrect(SObjectRef objectRef) => _ref = objectRef;

    /// <summary>
    /// Implicitly converts a <see cref="STBlockContextCorrect"/> to <see cref="SObjectRef"/>.
    /// </summary>
    public static implicit operator SObjectRef(STBlockContextCorrect obj) => obj._ref;

    /// <summary>
    /// Implicitly converts a <see cref="SObjectRef"/> to <see cref="STBlockContextCorrect"/>.
    /// </summary>
    public static implicit operator STBlockContextCorrect(SObjectRef r) => new(r);

    /// <summary>
    /// Implicitly converts from nint to <see cref="STBlockContextCorrect"/>.
    /// </summary>
    public static implicit operator STBlockContextCorrect(nint oop) => new(SObjectRef.FromObjectPointer(oop));

    // Fields from InstructionStream
    /// <summary>
    /// Gets or sets the sender (return pointer) - from InstructionStream at offset 0.
    /// </summary>
    public SObjectRef Sender
    {
        get => _ref.GetInstanceVariable(0);
        set => _ref.SetInstanceVariable(0, value);
    }

    /// <summary>
    /// Gets or sets the program counter - from InstructionStream at offset 1.
    /// </summary>
    public SObjectRef InstructionPointer
    {
        get => _ref.GetInstanceVariable(1);
        set => _ref.SetInstanceVariable(1, value);
    }

    /// <summary>
    /// Gets or sets the InstructionPointer as an integer.
    /// </summary>
    public int IP
    {
        get => InstructionPointer.IsSmallInteger ? InstructionPointer.AsSmallInteger() : 0;
        set => InstructionPointer = SObjectRef.FromSmallInteger(value);
    }

    // Fields from ContextPart
    /// <summary>
    /// Gets or sets the stack pointer - from ContextPart at offset 2.
    /// </summary>
    public SObjectRef StackPointer
    {
        get => _ref.GetInstanceVariable(2);
        set => _ref.SetInstanceVariable(2, value);
    }

    /// <summary>
    /// Gets or sets the StackPointer as an integer.
    /// </summary>
    public int SP
    {
        get => StackPointer.IsSmallInteger ? StackPointer.AsSmallInteger() : 0;
        set => StackPointer = SObjectRef.FromSmallInteger(value);
    }

    // Fields from BlockContext
    /// <summary>
    /// Gets or sets the number of block arguments - from BlockContext at offset 3.
    /// </summary>
    public SObjectRef Nargs
    {
        get => _ref.GetInstanceVariable(3);
        set => _ref.SetInstanceVariable(3, value);
    }

    /// <summary>
    /// Gets or sets the Nargs as an integer.
    /// </summary>
    public int NumArgs
    {
        get => Nargs.IsSmallInteger ? Nargs.AsSmallInteger() : 0;
        set => Nargs = SObjectRef.FromSmallInteger(value);
    }

    /// <summary>
    /// Gets or sets the start PC - from BlockContext at offset 4.
    /// </summary>
    public SObjectRef StartPC
    {
        get => _ref.GetInstanceVariable(4);
        set => _ref.SetInstanceVariable(4, value);
    }

    /// <summary>
    /// Gets or sets the StartPC as an integer.
    /// </summary>
    public int StartPCValue
    {
        get => StartPC.IsSmallInteger ? StartPC.AsSmallInteger() : 0;
        set => StartPC = SObjectRef.FromSmallInteger(value);
    }

    /// <summary>
    /// Gets or sets the home context - from BlockContext at offset 5.
    /// </summary>
    public SObjectRef Home
    {
        get => _ref.GetInstanceVariable(5);
        set => _ref.SetInstanceVariable(5, value);
    }

    // Stack operations
    /// <summary>
    /// Pushes a value onto the context's stack.
    /// </summary>
    public void Push(SObjectRef value)
    {
        var sp = SP;
        _ref.SetInstanceVariable(sp, value);
        SP = sp + 1;
    }

    /// <summary>
    /// Pops a value from the context's stack.
    /// </summary>
    public SObjectRef Pop()
    {
        var sp = SP;
        if (sp <= 6) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack underflow");

        sp--;
        SP = sp;
        return _ref.GetInstanceVariable(sp);
    }

    /// <summary>
    /// Gets the top value from the context's stack without popping.
    /// </summary>
    public SObjectRef Top()
    {
        var sp = SP;
        if (sp <= 6) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack is empty");

        return _ref.GetInstanceVariable(sp - 1);
    }

    /// <summary>
    /// The number of fixed instance variables for BlockContext.
    /// </summary>
    internal const int FixedFieldCount = 6;
}