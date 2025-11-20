using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Views;

/// <summary>
/// Type-safe view of a MethodContext per Blue Book Chapter 27.
/// Encapsulates field access without exposing raw OOP indices.
///
/// MethodContext layout:
///   Fixed fields [0-4]: sender, pc, stackp, method, receiver
///   Indexed slots: [0..n] temps/args/stack
/// </summary>
public readonly struct MethodContextView
{
    private readonly SmalltalkOOP _oop;
    private readonly ISmalltalkHeapManager _heap;

    /// <summary>
    /// Creates a view of a MethodContext.
    /// </summary>
    /// <param name="oop">The MethodContext OOP.</param>
    /// <param name="heap">The heap manager for accessing fields.</param>
    public MethodContextView(SmalltalkOOP oop, ISmalltalkHeapManager heap)
    {
        _oop = oop;
        _heap = heap;
    }

    /// <summary>Gets the underlying OOP.</summary>
    public SmalltalkOOP OOP => _oop;

    /// <summary>Gets the sender context (or nil if top-level).</summary>
    public SmalltalkOOP Sender => _heap.GetInstanceVariable(_oop, VMConstants.CONTEXT_SENDER_INDEX);

    /// <summary>Gets the program counter (instruction pointer).</summary>
    public int PC => _heap.GetInstanceVariable(_oop, VMConstants.CONTEXT_PC_INDEX).AsSmallInteger();

    /// <summary>Gets the stack pointer.</summary>
    public int StackPointer => _heap.GetInstanceVariable(_oop, VMConstants.CONTEXT_STACKP_INDEX).AsSmallInteger();

    /// <summary>Gets the method being executed.</summary>
    public SmalltalkOOP Method => _heap.GetInstanceVariable(_oop, VMConstants.CONTEXT_METHOD_INDEX);

    /// <summary>Gets the receiver object.</summary>
    public SmalltalkOOP Receiver => _heap.GetInstanceVariable(_oop, VMConstants.CONTEXT_RECEIVER_INDEX);

    /// <summary>Sets the sender context.</summary>
    public void SetSender(SmalltalkOOP sender) =>
        _heap.SetInstanceVariable(_oop, VMConstants.CONTEXT_SENDER_INDEX, sender);

    /// <summary>Sets the program counter.</summary>
    public void SetPC(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.CONTEXT_PC_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the stack pointer.</summary>
    public void SetStackPointer(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.CONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the method.</summary>
    public void SetMethod(SmalltalkOOP method) =>
        _heap.SetInstanceVariable(_oop, VMConstants.CONTEXT_METHOD_INDEX, method);

    /// <summary>Sets the receiver.</summary>
    public void SetReceiver(SmalltalkOOP receiver) =>
        _heap.SetInstanceVariable(_oop, VMConstants.CONTEXT_RECEIVER_INDEX, receiver);

    /// <summary>Gets a temporary variable by index.</summary>
    public SmalltalkOOP GetTemporary(int index) => _heap.GetIndexedOOP(_oop, index);

    /// <summary>Sets a temporary variable by index.</summary>
    public void SetTemporary(int index, SmalltalkOOP value) => _heap.SetIndexedOOP(_oop, index, value);

    /// <summary>Gets a stack value relative to current stack pointer.</summary>
    public SmalltalkOOP GetStackValue(int offset) => _heap.GetIndexedOOP(_oop, StackPointer - offset);

    /// <summary>Sets a stack value relative to current stack pointer.</summary>
    public void SetStackValue(int offset, SmalltalkOOP value) => _heap.SetIndexedOOP(_oop, StackPointer - offset, value);

    /// <summary>Pushes a value onto the stack.</summary>
    public void Push(SmalltalkOOP value)
    {
        int newSP = StackPointer + 1;
        SetStackPointer(newSP);
        _heap.SetIndexedOOP(_oop, newSP, value);
    }

    /// <summary>Pops a value from the stack.</summary>
    public SmalltalkOOP Pop()
    {
        int sp = StackPointer;
        var value = _heap.GetIndexedOOP(_oop, sp);
        SetStackPointer(sp - 1);
        return value;
    }

    /// <summary>Peeks at the top of the stack without popping.</summary>
    public SmalltalkOOP Peek() => _heap.GetIndexedOOP(_oop, StackPointer);

    /// <summary>Gets a view of the method being executed.</summary>
    public CompiledMethodView GetMethodView() => new(Method, _heap);

    /// <summary>Checks if this is a valid context (not nil).</summary>
    public bool IsValid => _oop != _heap.GetNil() && !_oop.IsSmallInteger;
}
