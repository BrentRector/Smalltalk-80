using Xunit;
using Smalltalk80.Interpreter.Core.VM.Interpreter;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Tests.VM;

/// <summary>
/// Comprehensive unit tests for ExecutionState and related types.
/// Tests execution state management and call outcome handling.
/// </summary>
public class ExecutionStateTests
{
    #region ExecutionState Tests

    [Fact]
    public void ExecutionState_Default_HasNullValues()
    {
        // Arrange & Act
        var state = new ExecutionState();

        // Assert
        Assert.Equal(SmalltalkOOP.Null, state.InitialContext);
        Assert.Equal(SmalltalkOOP.Null, state.ReturnValue);
        Assert.False(state.ContextSwitchPending);
        Assert.False(state.ContextSwitchedByPrimitive);
    }

    [Fact]
    public void ExecutionState_ForContext_SetsInitialContext()
    {
        // Arrange
        var contextOOP = new SmalltalkOOP(200);

        // Act
        var state = ExecutionState.ForContext(contextOOP);

        // Assert
        Assert.Equal(contextOOP, state.InitialContext);
        Assert.Equal(SmalltalkOOP.Null, state.ReturnValue);
        Assert.False(state.ContextSwitchPending);
        Assert.False(state.ContextSwitchedByPrimitive);
    }

    [Fact]
    public void ExecutionState_WithReturnValue_SetsValue()
    {
        // Arrange
        var contextOOP = new SmalltalkOOP(200);
        var returnValue = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var state = ExecutionState.ForContext(contextOOP) with { ReturnValue = returnValue };

        // Assert
        Assert.Equal(contextOOP, state.InitialContext);
        Assert.Equal(returnValue, state.ReturnValue);
    }

    [Fact]
    public void ExecutionState_WithContextSwitchPending_SetsFlag()
    {
        // Arrange & Act
        var state = new ExecutionState { ContextSwitchPending = true };

        // Assert
        Assert.True(state.ContextSwitchPending);
    }

    [Fact]
    public void ExecutionState_WithContextSwitchedByPrimitive_SetsFlag()
    {
        // Arrange & Act
        var state = new ExecutionState { ContextSwitchedByPrimitive = true };

        // Assert
        Assert.True(state.ContextSwitchedByPrimitive);
    }

    [Fact]
    public void ExecutionState_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var context1 = new SmalltalkOOP(200);
        var context2 = new SmalltalkOOP(200);

        // Act
        var state1 = ExecutionState.ForContext(context1);
        var state2 = ExecutionState.ForContext(context2);

        // Assert
        Assert.Equal(state1, state2);
    }

    #endregion

    #region NonLocalReturnState Tests

    [Fact]
    public void NonLocalReturnState_None_HasDefaultValues()
    {
        // Arrange & Act
        var state = NonLocalReturnState.None;

        // Assert
        Assert.False(state.IsPending);
        Assert.Equal(SmalltalkOOP.Null, state.Value);
        Assert.Equal(SmalltalkOOP.Null, state.TargetContext);
    }

    [Fact]
    public void NonLocalReturnState_Initiate_SetsPendingAndValues()
    {
        // Arrange
        var targetContext = new SmalltalkOOP(300);
        var returnValue = SmalltalkOOP.FromSmallInteger(99);

        // Act
        var state = NonLocalReturnState.Initiate(targetContext, returnValue);

        // Assert
        Assert.True(state.IsPending);
        Assert.Equal(returnValue, state.Value);
        Assert.Equal(targetContext, state.TargetContext);
    }

    [Fact]
    public void NonLocalReturnState_Complete_ReturnsNone()
    {
        // Arrange
        var targetContext = new SmalltalkOOP(300);
        var returnValue = SmalltalkOOP.FromSmallInteger(99);
        var state = NonLocalReturnState.Initiate(targetContext, returnValue);

        // Act
        var completed = state.Complete();

        // Assert
        Assert.False(completed.IsPending);
        Assert.Equal(SmalltalkOOP.Null, completed.Value);
        Assert.Equal(SmalltalkOOP.Null, completed.TargetContext);
    }

    [Fact]
    public void NonLocalReturnState_Initiate_DifferentValues_CreatesDistinctStates()
    {
        // Arrange
        var target1 = new SmalltalkOOP(300);
        var target2 = new SmalltalkOOP(400);
        var value1 = SmalltalkOOP.FromSmallInteger(1);
        var value2 = SmalltalkOOP.FromSmallInteger(2);

        // Act
        var state1 = NonLocalReturnState.Initiate(target1, value1);
        var state2 = NonLocalReturnState.Initiate(target2, value2);

        // Assert
        Assert.NotEqual(state1.TargetContext, state2.TargetContext);
        Assert.NotEqual(state1.Value, state2.Value);
    }

    #endregion

    #region InterpreterStatus Tests

    [Fact]
    public void InterpreterStatus_Continue_HasCorrectValue()
    {
        // Assert
        Assert.Equal(0, (int)InterpreterStatus.Continue);
    }

    [Fact]
    public void InterpreterStatus_Return_HasCorrectValue()
    {
        // Assert
        Assert.Equal(1, (int)InterpreterStatus.Return);
    }

    [Fact]
    public void InterpreterStatus_Yield_HasCorrectValue()
    {
        // Assert
        Assert.Equal(2, (int)InterpreterStatus.Yield);
    }

    #endregion

    #region CallOutcome Tests

    [Fact]
    public void CallOutcome_Normal_CreatesCorrectOutcome()
    {
        // Arrange
        var value = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var outcome = CallOutcome.Normal(value);

        // Assert
        Assert.Equal(CallOutcomeKind.Normal, outcome.Kind);
        Assert.Equal(value, outcome.Value);
        Assert.Equal(SmalltalkOOP.Null, outcome.TargetContext);
        Assert.Null(outcome.VMException);
    }

    [Fact]
    public void CallOutcome_NonLocalReturn_CreatesCorrectOutcome()
    {
        // Arrange
        var target = new SmalltalkOOP(300);
        var value = SmalltalkOOP.FromSmallInteger(99);

        // Act
        var outcome = CallOutcome.NonLocalReturn(target, value);

        // Assert
        Assert.Equal(CallOutcomeKind.NonLocalReturn, outcome.Kind);
        Assert.Equal(value, outcome.Value);
        Assert.Equal(target, outcome.TargetContext);
        Assert.Null(outcome.VMException);
    }

    [Fact]
    public void CallOutcome_VMError_CreatesCorrectOutcome()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var outcome = CallOutcome.VMError(exception);

        // Assert
        Assert.Equal(CallOutcomeKind.VMException, outcome.Kind);
        Assert.Equal(SmalltalkOOP.Null, outcome.Value);
        Assert.Equal(SmalltalkOOP.Null, outcome.TargetContext);
        Assert.Same(exception, outcome.VMException);
    }

    [Fact]
    public void CallOutcome_ContextSwitch_CreatesCorrectOutcome()
    {
        // Act
        var outcome = CallOutcome.ContextSwitch();

        // Assert
        Assert.Equal(CallOutcomeKind.ContextSwitch, outcome.Kind);
        Assert.Equal(SmalltalkOOP.Null, outcome.Value);
        Assert.Equal(SmalltalkOOP.Null, outcome.TargetContext);
        Assert.Null(outcome.VMException);
    }

    [Fact]
    public void CallOutcome_Normal_WithNil_CreatesCorrectOutcome()
    {
        // Arrange
        var nil = new SmalltalkOOP(2); // Assume this is nil OOP

        // Act
        var outcome = CallOutcome.Normal(nil);

        // Assert
        Assert.Equal(CallOutcomeKind.Normal, outcome.Kind);
        Assert.Equal(nil, outcome.Value);
    }

    [Fact]
    public void CallOutcome_Normal_WithSmallInteger_CreatesCorrectOutcome()
    {
        // Arrange
        var intValue = SmalltalkOOP.FromSmallInteger(-1);

        // Act
        var outcome = CallOutcome.Normal(intValue);

        // Assert
        Assert.Equal(CallOutcomeKind.Normal, outcome.Kind);
        Assert.True(outcome.Value.IsSmallInteger);
        Assert.Equal(-1, outcome.Value.AsSmallInteger());
    }

    #endregion

    #region CallOutcomeKind Tests

    [Fact]
    public void CallOutcomeKind_Normal_HasCorrectValue()
    {
        // Assert
        Assert.Equal(0, (int)CallOutcomeKind.Normal);
    }

    [Fact]
    public void CallOutcomeKind_NonLocalReturn_HasCorrectValue()
    {
        // Assert
        Assert.Equal(1, (int)CallOutcomeKind.NonLocalReturn);
    }

    [Fact]
    public void CallOutcomeKind_VMException_HasCorrectValue()
    {
        // Assert
        Assert.Equal(2, (int)CallOutcomeKind.VMException);
    }

    [Fact]
    public void CallOutcomeKind_ContextSwitch_HasCorrectValue()
    {
        // Assert
        Assert.Equal(3, (int)CallOutcomeKind.ContextSwitch);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ExecutionState_WithNLRState_IntegrationTest()
    {
        // Arrange
        var initialContext = new SmalltalkOOP(200);
        var targetContext = new SmalltalkOOP(300);
        var returnValue = SmalltalkOOP.FromSmallInteger(42);

        // Act
        var execState = ExecutionState.ForContext(initialContext);
        var nlrState = NonLocalReturnState.Initiate(targetContext, returnValue);

        // Assert
        Assert.Equal(initialContext, execState.InitialContext);
        Assert.True(nlrState.IsPending);
        Assert.Equal(targetContext, nlrState.TargetContext);
        Assert.Equal(returnValue, nlrState.Value);
    }

    [Fact]
    public void CallOutcome_FlowThroughExecution_IntegrationTest()
    {
        // This test simulates a flow through different call outcomes

        // Start with normal execution
        var normalOutcome = CallOutcome.Normal(SmalltalkOOP.FromSmallInteger(1));
        Assert.Equal(CallOutcomeKind.Normal, normalOutcome.Kind);

        // Context switch during execution
        var switchOutcome = CallOutcome.ContextSwitch();
        Assert.Equal(CallOutcomeKind.ContextSwitch, switchOutcome.Kind);

        // Non-local return
        var nlrOutcome = CallOutcome.NonLocalReturn(new SmalltalkOOP(300), SmalltalkOOP.FromSmallInteger(2));
        Assert.Equal(CallOutcomeKind.NonLocalReturn, nlrOutcome.Kind);

        // Error during execution
        var errorOutcome = CallOutcome.VMError(new InvalidOperationException("Test"));
        Assert.Equal(CallOutcomeKind.VMException, errorOutcome.Kind);
    }

    #endregion
}
