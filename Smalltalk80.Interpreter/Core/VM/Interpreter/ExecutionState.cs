using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Interpreter;

/// <summary>
/// Immutable state for the interpreter trampoline loop.
/// Groups related execution state that was previously scattered across VirtualMachine fields.
/// </summary>
internal record ExecutionState
{
    /// <summary>
    /// The starting context for this execution.
    /// Execution returns when we get back to this context.
    /// </summary>
    public SmalltalkOOP InitialContext { get; init; } = SmalltalkOOP.Null;

    /// <summary>
    /// The return value from execution.
    /// Set when execution completes normally or via NLR.
    /// </summary>
    public SmalltalkOOP ReturnValue { get; init; } = SmalltalkOOP.Null;

    /// <summary>
    /// Signal that a context switch occurred and the trampoline should continue.
    /// Set by message sends and returns.
    /// </summary>
    public bool ContextSwitchPending { get; init; }

    /// <summary>
    /// Signal that a primitive created a new context (e.g., block invocation).
    /// Used to coordinate between primitive execution and interpreter loop.
    /// </summary>
    public bool ContextSwitchedByPrimitive { get; init; }

    /// <summary>
    /// Creates a new ExecutionState for starting execution in a context.
    /// </summary>
    public static ExecutionState ForContext(SmalltalkOOP initialContext)
        => new() { InitialContext = initialContext };
}

/// <summary>
/// Immutable state for non-local return (NLR) handling.
/// Replaces exception-based unwinding with explicit state machine.
/// </summary>
internal record NonLocalReturnState
{
    /// <summary>
    /// Whether an NLR is currently in progress.
    /// </summary>
    public bool IsPending { get; init; }

    /// <summary>
    /// The value being returned via NLR.
    /// </summary>
    public SmalltalkOOP Value { get; init; } = SmalltalkOOP.Null;

    /// <summary>
    /// The target context to unwind to.
    /// This is the home MethodContext of the block performing the return.
    /// </summary>
    public SmalltalkOOP TargetContext { get; init; } = SmalltalkOOP.Null;

    /// <summary>
    /// Default state: no NLR in progress.
    /// </summary>
    public static readonly NonLocalReturnState None = new();

    /// <summary>
    /// Creates NLR state for initiating a non-local return.
    /// </summary>
    public static NonLocalReturnState Initiate(SmalltalkOOP targetContext, SmalltalkOOP value)
        => new()
        {
            IsPending = true,
            Value = value,
            TargetContext = targetContext
        };

    /// <summary>
    /// Returns state indicating NLR is complete.
    /// </summary>
    public NonLocalReturnState Complete() => None;
}

/// <summary>
/// Outcome of executing bytecodes in a context.
/// Used by the trampoline to determine next action.
/// </summary>
internal enum InterpreterStatus
{
    /// <summary>Context switched (message send or return) - keep executing.</summary>
    Continue,

    /// <summary>Execution complete - return the value.</summary>
    Return,

    /// <summary>Temporary pause (for future process scheduling).</summary>
    Yield
}

/// <summary>
/// Outcome of a message send or return operation.
/// Replaces the nested struct in VirtualMachine with a standalone type.
/// </summary>
internal readonly struct CallOutcome
{
    public readonly CallOutcomeKind Kind;
    public readonly SmalltalkOOP Value;
    public readonly SmalltalkOOP TargetContext;
    public readonly Exception? VMException;

    private CallOutcome(CallOutcomeKind kind, SmalltalkOOP value, SmalltalkOOP target, Exception? vmEx)
    {
        Kind = kind;
        Value = value;
        TargetContext = target;
        VMException = vmEx;
    }

    /// <summary>Normal completion with a return value.</summary>
    public static CallOutcome Normal(SmalltalkOOP value)
        => new(CallOutcomeKind.Normal, value, SmalltalkOOP.Null, null);

    /// <summary>Non-local return to a target context.</summary>
    public static CallOutcome NonLocalReturn(SmalltalkOOP target, SmalltalkOOP value)
        => new(CallOutcomeKind.NonLocalReturn, value, target, null);

    /// <summary>VM exception during execution.</summary>
    public static CallOutcome VMError(Exception ex)
        => new(CallOutcomeKind.VMException, SmalltalkOOP.Null, SmalltalkOOP.Null, ex);

    /// <summary>Context switch occurred (e.g., block invocation).</summary>
    public static CallOutcome ContextSwitch()
        => new(CallOutcomeKind.ContextSwitch, SmalltalkOOP.Null, SmalltalkOOP.Null, null);
}

/// <summary>
/// Kind of call outcome for the trampoline state machine.
/// </summary>
internal enum CallOutcomeKind
{
    /// <summary>Normal return with value.</summary>
    Normal,

    /// <summary>Non-local return in progress.</summary>
    NonLocalReturn,

    /// <summary>VM exception occurred.</summary>
    VMException,

    /// <summary>Context switch (e.g., block invocation).</summary>
    ContextSwitch
}
