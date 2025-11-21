using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Interpreter;

/// <summary>
/// Manages non-local return (NLR) state for the interpreter.
/// Encapsulates the NLR state machine that was previously scattered across VirtualMachine fields.
/// </summary>
/// <remarks>
/// Non-local returns occur when a ^expression is executed inside a block.
/// The return value must be returned from the block's home method, not just the block.
/// This requires unwinding the context stack until the target MethodContext is reached.
/// </remarks>
internal class NonLocalReturnHandler
{
    // Mutable state managed by this handler
    private bool _isPending;
    private SmalltalkOOP _value;
    private SmalltalkOOP _targetContext;
    private readonly SmalltalkOOP _nilOOP;

    /// <summary>Gets whether a non-local return is currently in progress.</summary>
    public bool IsPending => _isPending;

    /// <summary>Gets the value being returned via NLR.</summary>
    public SmalltalkOOP Value => _value;

    /// <summary>Gets the target context to unwind to (the home MethodContext's sender).</summary>
    public SmalltalkOOP TargetContext => _targetContext;

    /// <summary>
    /// Creates a new NonLocalReturnHandler with no NLR in progress.
    /// </summary>
    public NonLocalReturnHandler(SmalltalkOOP nilOOP)
    {
        _nilOOP = nilOOP;
        _isPending = false;
        _value = _nilOOP;
        _targetContext = _nilOOP;
    }

    /// <summary>
    /// Initiates a non-local return to the specified target context.
    /// Called by BytecodeInterpreter when executing ^return in a BlockContext.
    /// </summary>
    /// <param name="targetContext">The home MethodContext's sender to return to.</param>
    /// <param name="value">The value to return.</param>
    public void Initiate(SmalltalkOOP targetContext, SmalltalkOOP value)
    {
        _isPending = true;
        _targetContext = targetContext;
        _value = value;
    }

    /// <summary>
    /// Sets the NLR pending state. Used for interface compatibility.
    /// </summary>
    public void SetPending(bool isPending)
    {
        _isPending = isPending;
    }

    /// <summary>
    /// Sets the NLR value. Used for interface compatibility.
    /// </summary>
    public void SetValue(SmalltalkOOP value)
    {
        _value = value;
    }

    /// <summary>
    /// Sets the NLR target context. Used for interface compatibility.
    /// </summary>
    public void SetTargetContext(SmalltalkOOP targetContext)
    {
        _targetContext = targetContext;
    }

    /// <summary>
    /// Completes the non-local return, clearing all NLR state.
    /// Called when the target context is reached.
    /// </summary>
    /// <returns>The value that was being returned.</returns>
    public SmalltalkOOP Complete()
    {
        var value = _value;
        _isPending = false;
        _value = _nilOOP;
        _targetContext = _nilOOP;
        return value;
    }

    /// <summary>
    /// Checks if the current context is the NLR target.
    /// </summary>
    /// <param name="currentContext">The active context to check.</param>
    /// <returns>True if this is the target context for the NLR.</returns>
    public bool IsTargetReached(SmalltalkOOP currentContext)
    {
        return _isPending && currentContext == _targetContext;
    }

    /// <summary>
    /// Creates an immutable snapshot of the current NLR state.
    /// Useful for debugging and diagnostics.
    /// </summary>
    public NonLocalReturnState ToState()
    {
        if (!_isPending)
            return NonLocalReturnState.None;

        return NonLocalReturnState.Initiate(_targetContext, _value);
    }

    /// <summary>
    /// Restores state from an immutable snapshot.
    /// </summary>
    public void FromState(NonLocalReturnState state)
    {
        _isPending = state.IsPending;
        _value = state.Value;
        _targetContext = state.TargetContext;
    }
}
