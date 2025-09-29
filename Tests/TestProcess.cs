using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Unit tests for the SProcess class following the Blue Book specification.
/// </summary>
public class TestProcess
{
    private readonly SClass _processClass;
    private readonly SClass _methodContextClass;

    public TestProcess()
    {
        // Create mock classes for testing
        _processClass = new SClass { Name = "Process" };
        _methodContextClass = new SClass { Name = "MethodContext" };
    }

    [Fact]
    public void Process_InitialState_IsCorrect()
    {
        // Arrange & Act
        var process = new SProcess(_processClass);

        // Assert
        Assert.Equal(SProcess.UserSchedulingPriority, process.Priority);
        Assert.True(process.IsTerminated);
        Assert.False(process.IsSuspended);
        Assert.Equal(SNil.Instance, process.SuspendedContext);
        Assert.Equal(SNil.Instance, process.MyList);
        Assert.Equal(SNil.Instance, process.NextLink);
    }

    [Fact]
    public void Process_CreatedWithContext_HasCorrectState()
    {
        // Arrange
        var context = new SMethodContext(_methodContextClass);
        const int priority = SProcess.IOPriority;

        // Act
        var process = new SProcess(_processClass, context, priority);

        // Assert
        Assert.Equal(priority, process.Priority);
        Assert.False(process.IsTerminated);
        Assert.Equal(context, process.SuspendedContext);
    }

    [Theory]
    [InlineData(SProcess.SystemBackgroundPriority)]
    [InlineData(SProcess.UserBackgroundPriority)]
    [InlineData(SProcess.UserSchedulingPriority)]
    [InlineData(SProcess.UserInterruptPriority)]
    [InlineData(SProcess.IOPriority)]
    [InlineData(SProcess.LowIOPriority)]
    [InlineData(SProcess.HighIOPriority)]
    [InlineData(SProcess.TimerPriority)]
    public void Process_SetPriority_AcceptsValidValues(int priority)
    {
        // Arrange
        var process = new SProcess(_processClass);

        // Act
        process.Priority = priority;

        // Assert
        Assert.Equal(priority, process.Priority);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void Process_SetPriority_RejectsInvalidValues(int priority)
    {
        // Arrange
        var process = new SProcess(_processClass);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => process.Priority = priority);
    }

    [Fact]
    public void Process_Suspend_SavesContext()
    {
        // Arrange
        var process = new SProcess(_processClass);
        var context = new SMethodContext(_methodContextClass);

        // Act
        process.Suspend(context);

        // Assert
        Assert.Equal(context, process.SuspendedContext);
        Assert.False(process.IsTerminated);
    }

    [Fact]
    public void Process_Resume_ReturnsContext()
    {
        // Arrange
        var context = new SMethodContext(_methodContextClass);
        var process = new SProcess(_processClass, context, SProcess.UserSchedulingPriority);

        // Act
        var resumedContext = process.Resume();

        // Assert
        Assert.Equal(context, resumedContext);
    }

    [Fact]
    public void Process_Resume_ThrowsIfTerminated()
    {
        // Arrange
        var process = new SProcess(_processClass);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => process.Resume());
    }

    [Fact]
    public void Process_Terminate_ClearsContext()
    {
        // Arrange
        var context = new SMethodContext(_methodContextClass);
        var process = new SProcess(_processClass, context, SProcess.UserSchedulingPriority);
        var list = new SLinkedList(_processClass); // Using process class as placeholder
        list.AddLast(process);

        // Act
        process.Terminate();

        // Assert
        Assert.True(process.IsTerminated);
        Assert.Equal(SNil.Instance, process.SuspendedContext);
        Assert.Equal(SNil.Instance, process.MyList);
        Assert.Equal(SNil.Instance, process.NextLink);
    }

    [Fact]
    public void Process_ShouldPreempt_ComparesPriorities()
    {
        // Arrange
        var highPriority = new SProcess(_processClass) { Priority = SProcess.IOPriority };
        var lowPriority = new SProcess(_processClass) { Priority = SProcess.UserBackgroundPriority };

        // Act & Assert
        Assert.True(highPriority.ShouldPreempt(lowPriority));
        Assert.False(lowPriority.ShouldPreempt(highPriority));
        Assert.False(highPriority.ShouldPreempt(highPriority)); // Same priority doesn't preempt
    }

    [Fact]
    public void Process_ShouldPreempt_HandlesNull()
    {
        // Arrange
        var process = new SProcess(_processClass);

        // Act & Assert
        Assert.True(process.ShouldPreempt(null));
    }

    [Fact]
    public void Process_IsSuspended_WhenInList()
    {
        // Arrange
        var context = new SMethodContext(_methodContextClass);
        var process = new SProcess(_processClass, context, SProcess.UserSchedulingPriority);
        var list = new SLinkedList(_processClass);

        // Act
        list.AddLast(process);

        // Assert
        Assert.True(process.IsSuspended);
        Assert.False(process.IsTerminated);
    }

    [Fact]
    public void Process_ToString_ReflectsState()
    {
        // Arrange
        var terminatedProcess = new SProcess(_processClass);
        var suspendedProcess = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.IOPriority);
        var list = new SLinkedList(_processClass);
        list.AddLast(suspendedProcess);

        // Act
        var terminatedStr = terminatedProcess.ToString();
        var suspendedStr = suspendedProcess.ToString();

        // Assert
        Assert.Contains("terminated", terminatedStr);
        Assert.Contains("priority=3", terminatedStr);
        Assert.Contains("suspended", suspendedStr);
        Assert.Contains("priority=5", suspendedStr);
    }

    [Fact]
    public void Process_FieldIndices_MatchBlueBook()
    {
        // Assert that field indices match Blue Book specification
        Assert.Equal(0, SProcess.SuspendedContextIndex);
        Assert.Equal(1, SProcess.PriorityIndex);
        Assert.Equal(2, SProcess.MyListIndex);
        Assert.Equal(3, SProcess.NextLinkIndex);
    }

    [Fact]
    public void Process_PriorityConstants_MatchBlueBook()
    {
        // Assert that priority constants match Blue Book specification
        Assert.Equal(1, SProcess.SystemBackgroundPriority);
        Assert.Equal(2, SProcess.UserBackgroundPriority);
        Assert.Equal(3, SProcess.UserSchedulingPriority);
        Assert.Equal(4, SProcess.UserInterruptPriority);
        Assert.Equal(5, SProcess.IOPriority);
        Assert.Equal(6, SProcess.LowIOPriority);
        Assert.Equal(7, SProcess.HighIOPriority);
        Assert.Equal(8, SProcess.TimerPriority);
    }
}