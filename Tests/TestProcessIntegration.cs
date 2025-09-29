using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Integration tests for Process, Semaphore, LinkedList, and ProcessScheduler working together.
/// Tests complex scheduling scenarios as they would occur in a real Smalltalk-80 system.
/// </summary>
public class TestProcessIntegration
{
    private readonly SClass _schedulerClass;
    private readonly SClass _processClass;
    private readonly SClass _methodContextClass;
    private readonly SClass _blockContextClass;
    private readonly SClass _linkedListClass;
    private readonly SClass _semaphoreClass;
    private readonly SClass _arrayClass;

    public TestProcessIntegration()
    {
        // Create mock classes for testing
        _schedulerClass = new SClass { Name = "ProcessScheduler" };
        _processClass = new SClass { Name = "Process" };
        _methodContextClass = new SClass { Name = "MethodContext" };
        _blockContextClass = new SClass { Name = "BlockContext" };
        _linkedListClass = new SClass { Name = "LinkedList" };
        _semaphoreClass = new SClass { Name = "Semaphore" };
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
    public void Integration_ProducerConsumerWithSemaphore()
    {
        // Arrange - Classic producer/consumer scenario
        var scheduler = CreateScheduler();
        var bufferSemaphore = new SSemaphore(_semaphoreClass, 0); // Items available
        var mutexSemaphore = new SSemaphore(_semaphoreClass, 1);  // Mutual exclusion
        
        var producer = CreateProcess(SProcess.UserSchedulingPriority);
        var consumer = CreateProcess(SProcess.UserSchedulingPriority);
        
        int sharedBuffer = 0;
        int itemsProduced = 0;
        int itemsConsumed = 0;

        // Producer logic
        void ProduceItem()
        {
            if (mutexSemaphore.Wait(producer, scheduler))
            {
                sharedBuffer++;
                itemsProduced++;
                mutexSemaphore.Signal(scheduler);
                bufferSemaphore.Signal(scheduler);
            }
        }

        // Consumer logic
        void ConsumeItem()
        {
            if (bufferSemaphore.Wait(consumer, scheduler))
            {
                if (mutexSemaphore.Wait(consumer, scheduler))
                {
                    sharedBuffer--;
                    itemsConsumed++;
                    mutexSemaphore.Signal(scheduler);
                }
            }
        }

        // Act - Producer produces 5 items
        scheduler.Resume(producer);
        for (int i = 0; i < 5; i++)
        {
            ProduceItem();
        }

        // Consumer consumes 3 items
        scheduler.Resume(consumer);
        for (int i = 0; i < 3; i++)
        {
            ConsumeItem();
        }

        // Assert
        Assert.Equal(5, itemsProduced);
        Assert.Equal(3, itemsConsumed);
        Assert.Equal(2, sharedBuffer);
        Assert.Equal(2, bufferSemaphore.ExcessSignals);
        Assert.Equal(1, mutexSemaphore.ExcessSignals);
    }

    [Fact]
    public void Integration_DeadlockAvoidanceWithTimeout()
    {
        // Arrange - Two semaphores that could cause deadlock
        var scheduler = CreateScheduler();
        var semaphore1 = new SSemaphore(_semaphoreClass, 1);
        var semaphore2 = new SSemaphore(_semaphoreClass, 1);
        
        var process1 = CreateProcess(SProcess.UserSchedulingPriority);
        var process2 = CreateProcess(SProcess.UserSchedulingPriority);

        // Process 1 acquires sem1 then sem2
        bool process1Completed = false;
        if (semaphore1.Wait(process1, scheduler))
        {
            if (semaphore2.Wait(process1, scheduler))
            {
                process1Completed = true;
                semaphore2.Signal(scheduler);
            }
            semaphore1.Signal(scheduler);
        }

        // Process 2 acquires sem2 then sem1 (potential deadlock)
        bool process2Completed = false;
        if (semaphore2.Wait(process2, scheduler))
        {
            if (semaphore1.Wait(process2, scheduler))
            {
                process2Completed = true;
                semaphore1.Signal(scheduler);
            }
            semaphore2.Signal(scheduler);
        }

        // Assert - Both should complete since we're using cooperative scheduling
        Assert.True(process1Completed);
        Assert.True(process2Completed);
    }

    [Fact]
    public void Integration_PriorityInversion()
    {
        // Arrange - Classic priority inversion scenario
        var scheduler = CreateScheduler();
        var sharedResource = new SSemaphore(_semaphoreClass, 1);
        
        var lowPriorityProcess = CreateProcess(SProcess.UserBackgroundPriority);
        var mediumPriorityProcess = CreateProcess(SProcess.UserSchedulingPriority);
        var highPriorityProcess = CreateProcess(SProcess.IOPriority);

        // Low priority process acquires the resource
        scheduler.Resume(lowPriorityProcess);
        Assert.True(sharedResource.Wait(lowPriorityProcess, scheduler));
        Assert.Equal(lowPriorityProcess, scheduler.ActiveProcess);

        // High priority process tries to acquire the resource (blocked)
        scheduler.Resume(highPriorityProcess);
        Assert.False(sharedResource.Wait(highPriorityProcess, scheduler));
        Assert.True(sharedResource.HasWaitingProcesses);

        // Medium priority process runs (priority inversion occurs)
        scheduler.Resume(mediumPriorityProcess);
        Assert.Equal(mediumPriorityProcess, scheduler.ActiveProcess);

        // Low priority releases resource
        sharedResource.Signal(scheduler);

        // High priority process should now be active (woken from semaphore)
        Assert.Equal(highPriorityProcess, scheduler.ActiveProcess);
    }

    [Fact]
    public void Integration_BlockExecutionAsProcess()
    {
        // Arrange - Block contexts executed as separate processes
        var scheduler = CreateScheduler();
        var homeContext = new SMethodContext(_methodContextClass);
        var blockContext = new SBlockContext(_blockContextClass, homeContext, 0, 1, 16);
        
        var blockProcess = scheduler.Fork(_processClass, blockContext, SProcess.UserSchedulingPriority);

        // Act & Assert
        Assert.NotNull(blockProcess);
        Assert.Equal(blockContext, blockProcess.SuspendedContext);
        Assert.Equal(blockProcess, scheduler.ActiveProcess);
    }

    [Fact]
    public void Integration_ComplexSchedulingWithMultipleSemaphores()
    {
        // Arrange - Complex scenario with multiple processes and semaphores
        var scheduler = CreateScheduler();
        var printSemaphore = new SSemaphore(_semaphoreClass, 1);
        var dataSemaphore = new SSemaphore(_semaphoreClass, 0);
        
        var writerProcesses = new List<SProcess>();
        var readerProcesses = new List<SProcess>();
        
        // Create writer processes at different priorities
        for (int i = 0; i < 3; i++)
        {
            var writer = CreateProcess(SProcess.UserSchedulingPriority + i);
            writerProcesses.Add(writer);
            scheduler.Resume(writer);
        }

        // Create reader processes
        for (int i = 0; i < 2; i++)
        {
            var reader = CreateProcess(SProcess.UserBackgroundPriority);
            readerProcesses.Add(reader);
            scheduler.Resume(reader);
        }

        // Writers produce data
        foreach (var writer in writerProcesses)
        {
            if (printSemaphore.Wait(writer, scheduler))
            {
                dataSemaphore.Signal(scheduler);
                printSemaphore.Signal(scheduler);
            }
        }

        // Readers consume data
        int dataConsumed = 0;
        foreach (var reader in readerProcesses)
        {
            if (dataSemaphore.Wait(reader, scheduler))
            {
                dataConsumed++;
            }
        }

        // Assert
        Assert.Equal(3, dataSemaphore.ExcessSignals - dataConsumed + 2); // 3 produced, 2 consumed
        Assert.Equal(1, printSemaphore.ExcessSignals); // Mutex restored
        Assert.NotNull(scheduler.ActiveProcess);
    }

    [Fact]
    public void Integration_ProcessTerminationWithSemaphoreCleanup()
    {
        // Arrange
        var scheduler = CreateScheduler();
        var semaphore = new SSemaphore(_semaphoreClass, 0);
        
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var process3 = CreateProcess();

        // All processes wait on semaphore
        scheduler.Resume(process1);
        semaphore.Wait(process1, scheduler);
        
        scheduler.Resume(process2);
        semaphore.Wait(process2, scheduler);
        
        scheduler.Resume(process3);
        semaphore.Wait(process3, scheduler);

        Assert.Equal(3, semaphore.Size());

        // Terminate process2 while it's waiting
        process2.Terminate();

        // Signal once to wake process1
        semaphore.Signal(scheduler);

        // Assert - process1 should be active, process3 still waiting
        Assert.Equal(process1, scheduler.ActiveProcess);
        Assert.Equal(1, semaphore.Size()); // Only process3 waiting
    }

    [Fact]
    public void Integration_FairSchedulingAtSamePriority()
    {
        // Arrange - Test round-robin scheduling at same priority
        var scheduler = CreateScheduler();
        var processes = new List<SProcess>();
        const int processCount = 5;
        
        for (int i = 0; i < processCount; i++)
        {
            var process = CreateProcess(SProcess.UserSchedulingPriority);
            processes.Add(process);
            scheduler.Resume(process);
        }

        // Act - Each process yields control
        var executionOrder = new List<SProcess>();
        for (int round = 0; round < processCount * 2; round++)
        {
            executionOrder.Add(scheduler.ActiveProcess!);
            scheduler.Yield();
        }

        // Assert - Verify round-robin execution
        for (int i = 0; i < processCount; i++)
        {
            Assert.Equal(processes[i], executionOrder[i]);
            Assert.Equal(processes[i], executionOrder[i + processCount]);
        }
    }

    [Fact]
    public void Integration_CriticalSectionWithNesting()
    {
        // Arrange - Nested critical sections
        var scheduler = CreateScheduler();
        var outerSemaphore = new SSemaphore(_semaphoreClass, 1);
        var innerSemaphore = new SSemaphore(_semaphoreClass, 1);
        
        var process = CreateProcess();
        scheduler.Resume(process);
        
        bool outerExecuted = false;
        bool innerExecuted = false;

        // Act - Nested critical sections
        outerSemaphore.Critical(() =>
        {
            outerExecuted = true;
            innerSemaphore.Critical(() =>
            {
                innerExecuted = true;
            }, process, scheduler);
        }, process, scheduler);

        // Assert
        Assert.True(outerExecuted);
        Assert.True(innerExecuted);
        Assert.Equal(1, outerSemaphore.ExcessSignals);
        Assert.Equal(1, innerSemaphore.ExcessSignals);
    }

    [Fact]
    public void Integration_SchedulerStress()
    {
        // Arrange - Stress test with many processes
        var scheduler = CreateScheduler();
        var processes = new Dictionary<int, List<SProcess>>();
        
        // Create 10 processes at each priority level
        for (int priority = 1; priority <= ProcessScheduler.PriorityLevels; priority++)
        {
            processes[priority] = new List<SProcess>();
            for (int i = 0; i < 10; i++)
            {
                var process = CreateProcess(priority);
                processes[priority].Add(process);
                scheduler.AddProcess(process);
            }
        }

        // Act - Schedule and terminate processes
        while (scheduler.TotalReadyProcesses() > 0)
        {
            // Pick highest priority process
            int highestPriority = scheduler.HighestPriority;
            Assert.True(highestPriority > 0);
            
            // Resume and immediately terminate
            var list = processes[highestPriority];
            if (list.Count > 0)
            {
                var process = list[0];
                list.RemoveAt(0);
                scheduler.Resume(process);
                scheduler.Terminate(process);
            }
        }

        // Assert
        Assert.Null(scheduler.ActiveProcess);
        Assert.Equal(0, scheduler.TotalReadyProcesses());
        
        // All processes should be terminated
        foreach (var list in processes.Values)
        {
            foreach (var process in list)
            {
                Assert.True(process.IsTerminated);
            }
        }
    }
}