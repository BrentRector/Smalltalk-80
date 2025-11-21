using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Views;

/// <summary>
/// Type-safe view of a BlockContext per Blue Book Chapter 27.
/// Encapsulates field access without exposing raw OOP indices.
///
/// BlockContext layout:
///   Fixed fields [0-5]: sender, pc, stackp, argumentCount, initialIP, home
///   Extension field [6]: argStartIndex (VM extension for Primitive 81)
///   Indexed slots: Expression stack only (NO variables - stored in home MethodContext)
/// </summary>
public readonly struct BlockContextView
{
    private readonly SmalltalkOOP _oop;
    private readonly ISmalltalkHeap _heap;

    /// <summary>
    /// Creates a view of a BlockContext.
    /// </summary>
    /// <param name="oop">The BlockContext OOP.</param>
    /// <param name="heap">The heap manager for accessing fields.</param>
    public BlockContextView(SmalltalkOOP oop, ISmalltalkHeap heap)
    {
        _oop = oop;
        _heap = heap;
    }

    /// <summary>Gets the underlying OOP.</summary>
    public SmalltalkOOP OOP => _oop;

    /// <summary>Gets the caller context (or nil if top-level).</summary>
    public SmalltalkOOP Caller => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_CALLER_INDEX);

    /// <summary>Gets the program counter (instruction pointer).</summary>
    public int PC => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_PC_INDEX).AsSmallInteger();

    /// <summary>Gets the stack pointer.</summary>
    public int StackPointer => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_STACKP_INDEX).AsSmallInteger();

    /// <summary>Gets the number of arguments the block expects.</summary>
    public int ArgumentCount => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_NARGS_INDEX).AsSmallInteger();

    /// <summary>Gets the initial instruction pointer (start of block body).</summary>
    public int InitialIP => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_STARTPC_INDEX).AsSmallInteger();

    /// <summary>Gets the home MethodContext where variables are stored.</summary>
    public SmalltalkOOP Home => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_HOME_INDEX);

    /// <summary>Gets the argument start index in home context (VM extension for Primitive 81).</summary>
    public int ArgStartIndex => _heap.GetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_ARG_START_INDEX).AsSmallInteger();

    /// <summary>Sets the caller context.</summary>
    public void SetCaller(SmalltalkOOP caller) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_CALLER_INDEX, caller);

    /// <summary>Sets the program counter.</summary>
    public void SetPC(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_PC_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the stack pointer.</summary>
    public void SetStackPointer(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the argument count.</summary>
    public void SetArgumentCount(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_NARGS_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the initial IP.</summary>
    public void SetInitialIP(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_STARTPC_INDEX, SmalltalkOOP.FromSmallInteger(value));

    /// <summary>Sets the home context.</summary>
    public void SetHome(SmalltalkOOP home) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_HOME_INDEX, home);

    /// <summary>Sets the argument start index.</summary>
    public void SetArgStartIndex(int value) =>
        _heap.SetInstanceVariable(_oop, VMConstants.BLOCKCONTEXT_ARG_START_INDEX, SmalltalkOOP.FromSmallInteger(value));

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

    /// <summary>Gets a view of the home MethodContext.</summary>
    public MethodContextView GetHomeView() => new(Home, _heap);

    /// <summary>Checks if this is a valid context (not nil).</summary>
    public bool IsValid => _oop != _heap.GetNil() && !_oop.IsSmallInteger;
}
