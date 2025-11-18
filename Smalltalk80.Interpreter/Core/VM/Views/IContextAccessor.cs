using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Views;

/// <summary>
/// Unified accessor for context fields, handling MethodContext vs BlockContext differences.
/// Provides a consistent interface for accessing execution context state regardless of type.
/// </summary>
public interface IContextAccessor
{
    /// <summary>Gets the method being executed (traverses home chain for BlockContexts).</summary>
    SmalltalkOOP GetMethod(SmalltalkOOP contextOOP);

    /// <summary>Gets the receiver object (traverses home chain for BlockContexts).</summary>
    SmalltalkOOP GetReceiver(SmalltalkOOP contextOOP);

    /// <summary>Gets the program counter.</summary>
    int GetPC(SmalltalkOOP contextOOP);

    /// <summary>Sets the program counter.</summary>
    void SetPC(SmalltalkOOP contextOOP, int pc);

    /// <summary>Gets the stack pointer.</summary>
    int GetStackPointer(SmalltalkOOP contextOOP);

    /// <summary>Sets the stack pointer.</summary>
    void SetStackPointer(SmalltalkOOP contextOOP, int sp);

    /// <summary>Gets the sender/caller context.</summary>
    SmalltalkOOP GetSender(SmalltalkOOP contextOOP);

    /// <summary>Sets the sender/caller context.</summary>
    void SetSender(SmalltalkOOP contextOOP, SmalltalkOOP sender);

    /// <summary>Checks if the context is a BlockContext.</summary>
    bool IsBlockContext(SmalltalkOOP contextOOP);

    /// <summary>Gets the home MethodContext (returns self if already a MethodContext).</summary>
    SmalltalkOOP GetHomeContext(SmalltalkOOP contextOOP);

    /// <summary>Gets a temporary variable using unified temp frame architecture.</summary>
    SmalltalkOOP GetTemporary(SmalltalkOOP contextOOP, int index);

    /// <summary>Sets a temporary variable using unified temp frame architecture.</summary>
    void SetTemporary(SmalltalkOOP contextOOP, int index, SmalltalkOOP value);

    /// <summary>Pushes a value onto the context's stack.</summary>
    void Push(SmalltalkOOP contextOOP, SmalltalkOOP value);

    /// <summary>Pops a value from the context's stack.</summary>
    SmalltalkOOP Pop(SmalltalkOOP contextOOP);

    /// <summary>Peeks at the top of the context's stack.</summary>
    SmalltalkOOP Peek(SmalltalkOOP contextOOP);

    /// <summary>Gets a MethodContextView for the context (or its home if BlockContext).</summary>
    MethodContextView GetMethodContextView(SmalltalkOOP contextOOP);

    /// <summary>Gets a BlockContextView (only valid for BlockContexts).</summary>
    BlockContextView GetBlockContextView(SmalltalkOOP contextOOP);
}

/// <summary>
/// Default implementation of IContextAccessor using heap manager.
/// </summary>
public class ContextAccessor : IContextAccessor
{
    private readonly ISmalltalkHeapManager _heap;
    private readonly SmalltalkOOP _blockContextClassOOP;

    public ContextAccessor(ISmalltalkHeapManager heap, SmalltalkOOP blockContextClassOOP)
    {
        _heap = heap;
        _blockContextClassOOP = blockContextClassOOP;
    }

    public SmalltalkOOP GetMethod(SmalltalkOOP contextOOP)
    {
        var homeContext = GetHomeContext(contextOOP);
        return _heap.GetInstanceVariable(homeContext, VMConstants.CONTEXT_METHOD_INDEX);
    }

    public SmalltalkOOP GetReceiver(SmalltalkOOP contextOOP)
    {
        var homeContext = GetHomeContext(contextOOP);
        return _heap.GetInstanceVariable(homeContext, VMConstants.CONTEXT_RECEIVER_INDEX);
    }

    public int GetPC(SmalltalkOOP contextOOP)
    {
        int pcIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_PC_INDEX
            : VMConstants.CONTEXT_PC_INDEX;
        return _heap.GetInstanceVariable(contextOOP, pcIndex).AsSmallInteger();
    }

    public void SetPC(SmalltalkOOP contextOOP, int pc)
    {
        int pcIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_PC_INDEX
            : VMConstants.CONTEXT_PC_INDEX;
        _heap.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(pc));
    }

    public int GetStackPointer(SmalltalkOOP contextOOP)
    {
        int spIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_STACKP_INDEX
            : VMConstants.CONTEXT_STACKP_INDEX;
        return _heap.GetInstanceVariable(contextOOP, spIndex).AsSmallInteger();
    }

    public void SetStackPointer(SmalltalkOOP contextOOP, int sp)
    {
        int spIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_STACKP_INDEX
            : VMConstants.CONTEXT_STACKP_INDEX;
        _heap.SetInstanceVariable(contextOOP, spIndex, SmalltalkOOP.FromSmallInteger(sp));
    }

    public SmalltalkOOP GetSender(SmalltalkOOP contextOOP)
    {
        int senderIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_CALLER_INDEX
            : VMConstants.CONTEXT_SENDER_INDEX;
        return _heap.GetInstanceVariable(contextOOP, senderIndex);
    }

    public void SetSender(SmalltalkOOP contextOOP, SmalltalkOOP sender)
    {
        int senderIndex = IsBlockContext(contextOOP)
            ? VMConstants.BLOCKCONTEXT_CALLER_INDEX
            : VMConstants.CONTEXT_SENDER_INDEX;
        _heap.SetInstanceVariable(contextOOP, senderIndex, sender);
    }

    public bool IsBlockContext(SmalltalkOOP contextOOP)
    {
        if (contextOOP.IsSmallInteger || contextOOP == SpecialObjectsArray.Instance.Nil)
            return false;

        var classOOP = _heap.GetClassOOP(contextOOP);
        return classOOP == _blockContextClassOOP;
    }

    public SmalltalkOOP GetHomeContext(SmalltalkOOP contextOOP)
    {
        if (!IsBlockContext(contextOOP))
            return contextOOP;

        return _heap.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    }

    public SmalltalkOOP GetTemporary(SmalltalkOOP contextOOP, int index)
    {
        // All temporaries are stored in the home MethodContext
        var homeContext = GetHomeContext(contextOOP);
        return _heap.GetIndexedOOP(homeContext, index);
    }

    public void SetTemporary(SmalltalkOOP contextOOP, int index, SmalltalkOOP value)
    {
        // All temporaries are stored in the home MethodContext
        var homeContext = GetHomeContext(contextOOP);
        _heap.SetIndexedOOP(homeContext, index, value);
    }

    public void Push(SmalltalkOOP contextOOP, SmalltalkOOP value)
    {
        int sp = GetStackPointer(contextOOP) + 1;
        SetStackPointer(contextOOP, sp);
        _heap.SetIndexedOOP(contextOOP, sp, value);
    }

    public SmalltalkOOP Pop(SmalltalkOOP contextOOP)
    {
        int sp = GetStackPointer(contextOOP);
        var value = _heap.GetIndexedOOP(contextOOP, sp);
        SetStackPointer(contextOOP, sp - 1);
        return value;
    }

    public SmalltalkOOP Peek(SmalltalkOOP contextOOP)
    {
        int sp = GetStackPointer(contextOOP);
        return _heap.GetIndexedOOP(contextOOP, sp);
    }

    public MethodContextView GetMethodContextView(SmalltalkOOP contextOOP)
    {
        var homeContext = GetHomeContext(contextOOP);
        return new MethodContextView(homeContext, _heap);
    }

    public BlockContextView GetBlockContextView(SmalltalkOOP contextOOP)
    {
        return new BlockContextView(contextOOP, _heap);
    }
}
