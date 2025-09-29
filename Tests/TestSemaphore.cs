using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Unit tests for the SSemaphore class following the Blue Book specification.
/// </summary>
public class TestSemaphore
{
    private readonly SClass _semaphoreClass;
    private readonly SClass _processClass;
    private readonly SClass _methodContextClass;
    private readonly SClass _schedulerClass;
    private readonly SClass _linkedListClass;

    public TestSemaphore()
    {
        // Create mock classes for testing
        _semaphoreClass = new SClass { Name = "Semaphore" };
        _processClass = new SClass { Name = "Process" };
        _methodContextClass = new SClass { Name = "MethodContext" };
        _schedulerClass = new SClass { Name = "ProcessScheduler" };
        _linkedListClass = new SClass { Name = "LinkedList" };
    }

    [Fact]
    public void Semaphore_InitialState_HasZeroSignals()
    {
        // Arrange & Act
        var semaphore = new SSemaphore(_semaphoreClass);

        // Assert
        Assert.Equal(0, semaphore.ExcessSignals);
        Assert.False(semaphore.HasWaitingProcesses);
        Assert.True(semaphore.IsEmpty);
    }

    [Fact]
    public void Semaphore_InitializedWithSignals_HasCorrectCount()
    {
        // Arrange & Act
        var semaphore = new SSemaphore(_semaphoreClass, 3);

        // Assert
        Assert.Equal(3, semaphore.ExcessSignals);
        Assert.False(semaphore.HasWaitingProcesses);
    }

    [Fact]
    public void Semaphore_Signal_IncrementsExcessWhenNoWaiters()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);

        // Act
        var result = semaphore.Signal();

        // Assert
        Assert.Equal(1, semaphore.ExcessSignals);
        Assert.Equal(SNil.Instance, result);
    }

    [Fact]
    public void Semaphore_Signal_WakesWaitingProcess()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        var process = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var scheduler = new ProcessScheduler(_schedulerClass, _linkedListClass);
        
        // Add process to waiting queue
        semaphore.Wait(process, scheduler);

        // Act
        var result = semaphore.Signal(scheduler);

        // Assert
        Assert.Equal(process, result);
        Assert.Equal(0, semaphore.ExcessSignals);
        Assert.False(semaphore.HasWaitingProcesses);
    }

    [Fact]
    public void Semaphore_Wait_ConsumesExcessSignal()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass, 2);
        var process = new SProcess(_processClass);

        // Act
        var canContinue = semaphore.Wait(process, null);

        // Assert
        Assert.True(canContinue);
        Assert.Equal(1, semaphore.ExcessSignals);
        Assert.False(semaphore.HasWaitingProcesses);
    }

    [Fact]
    public void Semaphore_Wait_SuspendsWhenNoSignals()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        var process = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);

        // Act
        var canContinue = semaphore.Wait(process, null);

        // Assert
        Assert.False(canContinue);
        Assert.Equal(0, semaphore.ExcessSignals);
        Assert.True(semaphore.HasWaitingProcesses);
        Assert.Equal(process, semaphore.First());
    }

    [Fact]
    public void Semaphore_MultipleWaiters_QueuedInOrder()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        var process1 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var process2 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var process3 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);

        // Act
        semaphore.Wait(process1, null);
        semaphore.Wait(process2, null);
        semaphore.Wait(process3, null);

        // Assert
        Assert.Equal(3, semaphore.Size());
        Assert.Equal(process1, semaphore.Signal());
        Assert.Equal(process2, semaphore.Signal());
        Assert.Equal(process3, semaphore.Signal());
        Assert.True(semaphore.IsEmpty);
    }

    [Fact]
    public void Semaphore_SignalWait_MaintainsBalance()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        var process = new SProcess(_processClass);

        // Act - Signal then wait
        semaphore.Signal();
        var canContinue = semaphore.Wait(process, null);

        // Assert
        Assert.True(canContinue);
        Assert.Equal(0, semaphore.ExcessSignals);
        Assert.False(semaphore.HasWaitingProcesses);
    }

    [Fact]
    public void Semaphore_RemoveProcess_RemovesFromQueue()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        var process1 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var process2 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        
        semaphore.Wait(process1, null);
        semaphore.Wait(process2, null);

        // Act
        var removed = semaphore.RemoveProcess(process1);

        // Assert
        Assert.Equal(process1, removed);
        Assert.Equal(1, semaphore.Size());
        Assert.Equal(process2, semaphore.First());
    }

    [Fact]
    public void Semaphore_InitializeForMutex_SetsOneSignal()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);

        // Act
        semaphore.InitializeForMutex();

        // Assert
        Assert.Equal(1, semaphore.ExcessSignals);
    }

    [Fact]
    public void Semaphore_Critical_ExecutesWithMutex()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        semaphore.InitializeForMutex();
        var process = new SProcess(_processClass);
        bool executed = false;

        // Act
        semaphore.Critical(() => { executed = true; }, process, null);

        // Assert
        Assert.True(executed);
        Assert.Equal(1, semaphore.ExcessSignals); // Should be restored after critical section
    }

    [Fact]
    public void Semaphore_Critical_RestoresSignalOnException()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);
        semaphore.InitializeForMutex();
        var process = new SProcess(_processClass);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            semaphore.Critical(() => throw new InvalidOperationException(), process, null));
        
        // Signal should be restored even after exception
        Assert.Equal(1, semaphore.ExcessSignals);
    }

    [Fact]
    public void Semaphore_Reset_ClearsWaitingProcesses()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass, 2);
        var process1 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var process2 = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        
        semaphore.Wait(process1, null);
        semaphore.Wait(process2, null);
        semaphore.Wait(process1, null); // This one will be queued

        // Act
        semaphore.Reset();

        // Assert
        Assert.Equal(0, semaphore.ExcessSignals);
        Assert.True(semaphore.IsEmpty);
        Assert.False(semaphore.HasWaitingProcesses);
    }

    [Fact]
    public void Semaphore_ExcessSignals_CannotBeNegative()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => semaphore.ExcessSignals = -1);
    }

    [Fact]
    public void Semaphore_ToString_ReflectsState()
    {
        // Arrange
        var semaphore = new SSemaphore(_semaphoreClass, 3);
        var process = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        
        // Act
        var initialStr = semaphore.ToString();
        semaphore.Wait(process, null);
        semaphore.Wait(process, null);
        semaphore.Wait(process, null);
        semaphore.Wait(process, null); // This one queues
        var afterWaitStr = semaphore.ToString();

        // Assert
        Assert.Contains("signals=3", initialStr);
        Assert.Contains("waiting=0", initialStr);
        Assert.Contains("signals=0", afterWaitStr);
        Assert.Contains("waiting=1", afterWaitStr);
    }

    [Fact]
    public void Semaphore_FieldIndices_MatchBlueBook()
    {
        // Assert that field indices match Blue Book specification
        // Semaphore inherits from LinkedList which has firstLink=0, lastLink=1
        Assert.Equal(2, SSemaphore.ExcessSignalsIndex);
    }

    [Fact]
    public void Semaphore_ProducerConsumer_Pattern()
    {
        // Arrange - Classic producer/consumer with semaphore
        var bufferSemaphore = new SSemaphore(_semaphoreClass, 0); // Items available
        var spaceSemaphore = new SSemaphore(_semaphoreClass, 5);  // Buffer space
        
        var producer = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);
        var consumer = new SProcess(_processClass, new SMethodContext(_methodContextClass), SProcess.UserSchedulingPriority);

        // Act - Producer produces 3 items
        for (int i = 0; i < 3; i++)
        {
            Assert.True(spaceSemaphore.Wait(producer, null)); // Wait for space
            bufferSemaphore.Signal(); // Signal item available
        }

        // Assert - Consumer can consume 3 items
        for (int i = 0; i < 3; i++)
        {
            Assert.True(bufferSemaphore.Wait(consumer, null)); // Wait for item
            spaceSemaphore.Signal(); // Signal space available
        }

        // Consumer can't consume more
        Assert.False(bufferSemaphore.Wait(consumer, null));
        
        // Final state
        Assert.Equal(5, spaceSemaphore.ExcessSignals);
        Assert.Equal(0, bufferSemaphore.ExcessSignals);
    }
}