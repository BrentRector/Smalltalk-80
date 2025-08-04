using System;
using System.Collections.Generic;

/// <summary>
/// Implements the Smalltalk-80 process scheduler that manages multiple execution contexts.
/// Reference: Specification lines 814-816, 1564-1580
/// </summary>
public class ProcessScheduler
{
    /// <summary>
    /// The currently active process.
    /// </summary>
    public Process? ActiveProcess { get; private set; }

    /// <summary>
    /// Queue of processes waiting to run at each priority level.
    /// </summary>
    private readonly Dictionary<int, Queue<Process>> processQueues = new();

    /// <summary>
    /// List of processes waiting on semaphores.
    /// </summary>
    private readonly List<Process> waitingProcesses = new();

    /// <summary>
    /// Checks if a process switch should occur and performs it if necessary.
    /// This should be called at the start of each interpreter cycle.
    /// </summary>
    public void CheckProcessSwitch()
    {
        // Implementation needed:
        // 1. Check if higher priority processes are ready
        // 2. Check if current process time slice expired
        // 3. Perform context switch if needed
        
        if (ActiveProcess == null)
        {
            ActiveProcess = GetNextRunnableProcess();
        }
        
        // Time slice management and priority checking would go here
    }

    /// <summary>
    /// Adds a process to the appropriate run queue.
    /// </summary>
    /// <param name="process">The process to schedule.</param>
    public void ScheduleProcess(Process process)
    {
        if (!processQueues.ContainsKey(process.Priority))
        {
            processQueues[process.Priority] = new Queue<Process>();
        }
        
        processQueues[process.Priority].Enqueue(process);
    }

    /// <summary>
    /// Suspends the currently active process.
    /// </summary>
    public void SuspendActiveProcess()
    {
        if (ActiveProcess != null)
        {
            waitingProcesses.Add(ActiveProcess);
            ActiveProcess = GetNextRunnableProcess();
        }
    }

    /// <summary>
    /// Resumes a suspended process.
    /// </summary>
    /// <param name="process">The process to resume.</param>
    public void ResumeProcess(Process process)
    {
        waitingProcesses.Remove(process);
        ScheduleProcess(process);
    }

    private Process? GetNextRunnableProcess()
    {
        // Find highest priority non-empty queue
        foreach (var priority in new[] { 80, 60, 40, 20, 10 }) // Standard Smalltalk priorities
        {
            if (processQueues.ContainsKey(priority) && processQueues[priority].Count > 0)
            {
                return processQueues[priority].Dequeue();
            }
        }
        return null;
    }
}

/// <summary>
/// Represents a Smalltalk process (execution context).
/// </summary>
public class Process
{
    /// <summary>
    /// The execution context for this process.
    /// </summary>
    public Context Context { get; set; } = null!;

    /// <summary>
    /// Priority level (higher numbers = higher priority).
    /// </summary>
    public int Priority { get; set; } = 40;

    /// <summary>
    /// The semaphore this process is waiting on, if any.
    /// </summary>
    public Semaphore? WaitingSemaphore { get; set; }
}

/// <summary>
/// Implements Smalltalk semaphores for process synchronization.
/// </summary>
public class Semaphore
{
    /// <summary>
    /// Current semaphore count.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Queue of processes waiting on this semaphore.
    /// </summary>
    private readonly Queue<Process> waitingProcesses = new();

    /// <summary>
    /// Signals the semaphore, incrementing count or waking a waiting process.
    /// </summary>
    public void Signal()
    {
        if (waitingProcesses.Count > 0)
        {
            var process = waitingProcesses.Dequeue();
            process.WaitingSemaphore = null;
            // Schedule process to run
        }
        else
        {
            Count++;
        }
    }

    /// <summary>
    /// Waits on the semaphore, decrementing count or suspending the process.
    /// </summary>
    /// <param name="process">The process attempting to wait.</param>
    public void Wait(Process process)
    {
        if (Count > 0)
        {
            Count--;
        }
        else
        {
            process.WaitingSemaphore = this;
            waitingProcesses.Enqueue(process);
            // Suspend the process
        }
    }
}
