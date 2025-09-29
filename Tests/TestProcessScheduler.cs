using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Unit tests for the ProcessScheduler class following the Blue Book specification.
/// </summary>
public class TestProcessScheduler
{
    private readonly SClass _schedulerClass;
    private readonly SClass _processClass;
    private readonly SClass _methodContextClass;
    private readonly SClass _linkedListClass;
    private readonly SClass _arrayClass;

    public TestProcessScheduler()
    {
        // Create mock classes for testing
        _schedulerClass = new SClass { Name = "ProcessScheduler" };
        _processClass = new SClass { Name = "Process" };
        _methodContextClass = new SClass { Name = "MethodContext" };
        _linkedListClass = new SClass { Name = "LinkedList" };
        _arrayClass = new SClass { Name = "Array" };
    }

    private ProcessScheduler CreateScheduler()
    {
        return new ProcessScheduler(_schedulerClass, _linkedListClass);
    }

    private SProcess CreateProcess(int priority = SProcess.UserSchedulingPriority)
    {
        var context = new SMethodContext(_methodContextClass);
        return new SProcess(_processClass, context, priority);
    }

    [Fact]
    public void Scheduler_InitialState_HasEmptyQueues()
    {
        // Arrange & Act
        var scheduler = CreateScheduler();

        // Assert
        Assert.Null(scheduler.ActiveProcess);
        Assert.NotNull(scheduler.QuiescentProcessLists);
        Assert.Equal(0, scheduler.HighestPriority);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
    }

    [Fact]
    public void Scheduler_AddProcess_AddsToCorrectPriorityQueue()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var lowProcess = CreateProcess(SProcess.UserBackgroundPriority);
        var medProcess = CreateProcess(SProcess.UserSchedulingPriority);
        var highProcess = CreateProcess(SProcess.IOPriority);

        // Act
        scheduler.AddProcess(lowProcess);
        scheduler.AddProcess(medProcess);
        scheduler.AddProcess(highProcess);

        // Assert
        Assert.Equal(1, scheduler.ProcessesAtPriority(SProcess.UserBackgroundPriority));
        Assert.Equal(1, scheduler.ProcessesAtPriority(SProcess.UserSchedulingPriority));
        Assert.Equal(1, scheduler.ProcessesAtPriority(SProcess.IOPriority));
        Assert.Equal(3, scheduler.TotalReadyProcesses());
        Assert.Equal(SProcess.IOPriority, scheduler.HighestPriority);
    }

    [Fact]
    public void Scheduler_Resume_SchedulesHighestPriority()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var lowProcess = CreateProcess(SProcess.UserBackgroundPriority);
        var highProcess = CreateProcess(SProcess.IOPriority);

        // Act
        scheduler.Resume(lowProcess);
        scheduler.Resume(highProcess);

        // Assert
        Assert.Equal(highProcess, scheduler.ActiveProcess);
        Assert.Equal(1, scheduler.TotalReadyProcesses()); // Low priority still in queue
    }

    [Fact]
    public void Scheduler_Preemption_OccursForHigherPriority()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var lowProcess = CreateProcess(SProcess.UserBackgroundPriority);
        var highProcess = CreateProcess(SProcess.IOPriority);

        // Act
        scheduler.Resume(lowProcess); // Low priority becomes active
        var activeAfterLow = scheduler.ActiveProcess;
        scheduler.Resume(highProcess); // High priority should preempt

        // Assert
        Assert.Equal(lowProcess, activeAfterLow);
        Assert.Equal(highProcess, scheduler.ActiveProcess);
        Assert.Equal(1, scheduler.TotalReadyProcesses()); // Low priority back in queue
    }

    [Fact]
    public void Scheduler_NoPreemption_ForSamePriority()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var process1 = CreateProcess(SProcess.UserSchedulingPriority);
        var process2 = CreateProcess(SProcess.UserSchedulingPriority);

        // Act
        scheduler.Resume(process1);
        var activeAfterFirst = scheduler.ActiveProcess;
        scheduler.Resume(process2); // Same priority should not preempt

        // Assert
        Assert.Equal(process1, activeAfterFirst);
        Assert.Equal(process1, scheduler.ActiveProcess); // Still process1
        Assert.Equal(1, scheduler.TotalReadyProcesses()); // process2 in queue
    }

    [Fact]
    public void Scheduler_Yield_RotatesProcessesAtSamePriority()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var process1 = CreateProcess(SProcess.UserSchedulingPriority);
        var process2 = CreateProcess(SProcess.UserSchedulingPriority);
        var process3 = CreateProcess(SProcess.UserSchedulingPriority);

        scheduler.Resume(process1);
        scheduler.Resume(process2);
        scheduler.Resume(process3);

        // Act
        scheduler.Yield();

        // Assert
        Assert.Equal(process2, scheduler.ActiveProcess);
        Assert.Equal(2, scheduler.TotalReadyProcesses()); // process3 and process1 in queue
    }

    [Fact]
    public void Scheduler_Suspend_RemovesFromActive()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var process = CreateProcess();
        scheduler.Resume(process);

        // Act
        scheduler.Suspend(process);

        // Assert
        Assert.Null(scheduler.ActiveProcess);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
    }

    [Fact]
    public void Scheduler_Terminate_RemovesProcess()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        
        scheduler.Resume(process1);
        scheduler.Resume(process2);

        // Act
        scheduler.Terminate(process1);

        // Assert
        Assert.Equal(process2, scheduler.ActiveProcess);
        Assert.True(process1.IsTerminated);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
    }

    [Fact]
    public void Scheduler_Fork_CreatesNewProcess()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var blockContext = new SBlockContext(_methodContextClass);

        // Act
        var newProcess = scheduler.Fork(_processClass, blockContext, SProcess.UserSchedulingPriority);

        // Assert
        Assert.NotNull(newProcess);
        Assert.Equal(blockContext, newProcess.SuspendedContext);
        Assert.Equal(SProcess.UserSchedulingPriority, newProcess.Priority);
        Assert.Equal(newProcess, scheduler.ActiveProcess);
    }

    [Fact]
    public void Scheduler_RemoveProcess_RemovesFromQueue()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var process = CreateProcess();
        scheduler.AddProcess(process);

        // Act
        var removed = scheduler.RemoveProcess(process);

        // Assert
        Assert.Equal(process, removed);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
    }

    [Fact]
    public void Scheduler_MultipleQueues_SchedulesCorrectly()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var processes = new List<SProcess>();

        // Create processes at different priorities
        for (int priority = 1; priority <= ProcessScheduler.PriorityLevels; priority++)
        {
            var process = CreateProcess(priority);
            processes.Add(process);
            scheduler.AddProcess(process);
        }

        // Act & Assert - Should schedule from highest to lowest
        for (int priority = ProcessScheduler.PriorityLevels; priority >= 1; priority--)
        {
            Assert.Equal(priority, scheduler.HighestPriority);
            scheduler.Resume(processes[priority - 1]);
            scheduler.Terminate(scheduler.ActiveProcess!);
        }

        Assert.Equal(0, scheduler.TotalReadyProcesses());
        Assert.Null(scheduler.ActiveProcess);
    }

    [Fact]
    public void Scheduler_ProcessesAtPriority_ReturnsCorrectCount()
    {
        // Arrange
        var scheduler = CreateScheduler();
        const int testPriority = SProcess.UserSchedulingPriority;
        const int processCount = 5;

        for (int i = 0; i < processCount; i++)
        {
            scheduler.AddProcess(CreateProcess(testPriority));
        }

        // Act
        var count = scheduler.ProcessesAtPriority(testPriority);

        // Assert
        Assert.Equal(processCount, count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void Scheduler_ProcessesAtPriority_HandlesInvalidPriority(int priority)
    {
        // Arrange
        var scheduler = CreateScheduler();

        // Act
        var count = scheduler.ProcessesAtPriority(priority);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void Scheduler_ToString_ReflectsState()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var activeProcess = CreateProcess();
        scheduler.Resume(activeProcess);
        scheduler.AddProcess(CreateProcess());
        scheduler.AddProcess(CreateProcess());

        // Act
        var str = scheduler.ToString();

        // Assert
        Assert.Contains("active=Process", str);
        Assert.Contains("ready=2", str);
    }

    [Fact]
    public void Scheduler_FieldIndices_MatchBlueBook()
    {
        // Assert that field indices match Blue Book specification
        Assert.Equal(0, ProcessScheduler.QuiescentProcessListsIndex);
        Assert.Equal(1, ProcessScheduler.ActiveProcessIndex);
    }

    [Fact]
    public void Scheduler_ComplexSchedulingScenario()
    {
        // Arrange - Complex scenario with multiple processes and priorities
        var scheduler = CreateScheduler();
        var ioProcess = CreateProcess(SProcess.IOPriority);
        var userProcess1 = CreateProcess(SProcess.UserSchedulingPriority);
        var userProcess2 = CreateProcess(SProcess.UserSchedulingPriority);
        var backgroundProcess = CreateProcess(SProcess.UserBackgroundPriority);

        // Act - Add processes in mixed order
        scheduler.Resume(userProcess1);
        scheduler.Resume(backgroundProcess);
        scheduler.Resume(ioProcess); // Should preempt
        scheduler.Resume(userProcess2);

        // Assert - IO process should be active
        Assert.Equal(ioProcess, scheduler.ActiveProcess);
        Assert.Equal(3, scheduler.TotalReadyProcesses());

        // Terminate IO process
        scheduler.Terminate(ioProcess);

        // User process should now be active
        Assert.Equal(userProcess1, scheduler.ActiveProcess);
        Assert.Equal(2, scheduler.TotalReadyProcesses());

        // Yield to rotate between user processes
        scheduler.Yield();
        Assert.Equal(userProcess2, scheduler.ActiveProcess);

        // Yield again
        scheduler.Yield();
        Assert.Equal(userProcess1, scheduler.ActiveProcess);

        // Terminate all user processes
        scheduler.Terminate(userProcess1);
        scheduler.Terminate(userProcess2);

        // Background process should now be active
        Assert.Equal(backgroundProcess, scheduler.ActiveProcess);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
    }
}