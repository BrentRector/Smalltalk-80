namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;
using System.Collections.Generic;

/// <summary>
/// The Smalltalk ProcessorScheduler - manages all processes in the system.
/// </summary>
/// <remarks>
/// From the Blue Book page 251:
/// The ProcessorScheduler is a unique object that manages all the processes
/// in the system. It maintains lists of processes at different priority levels
/// and determines which process should run next.
/// 
/// Instance variables:
/// - quiescentProcessLists: Array of linked lists, one for each priority
/// - activeProcess: The currently running process
/// </remarks>
public sealed class SProcessScheduler : SInstance
{
    // Instance variable indices
    private const int QuiescentProcessListsIndex = 0;
    private const int ActiveProcessIndex = 1;
    
    // Priority levels (Blue Book standard)
    /// <summary>
    /// The lowest system priority level (1).
    /// </summary>
    public const int SystemRockBottomPriority = 1;
    /// <summary>
    /// The system background priority level (2).
    /// </summary>
    public const int SystemBackgroundPriority = 2;
    /// <summary>
    /// The user background priority level (3).
    /// </summary>
    public const int UserBackgroundPriority = 3;
    /// <summary>
    /// The user scheduling priority level (4).
    /// </summary>
    public const int UserSchedulingPriority = 4;
    /// <summary>
    /// The user interrupt priority level (5).
    /// </summary>
    public const int UserInterruptPriority = 5;
    /// <summary>
    /// The low I/O priority level (6).
    /// </summary>
    public const int LowIOPriority = 6;
    /// <summary>
    /// The high I/O priority level (7).
    /// </summary>
    public const int HighIOPriority = 7;
    /// <summary>
    /// The timing priority level (8).
    /// </summary>
    public const int TimingPriority = 8;
    
    private readonly List<Queue<SProcess>>[] priorityQueues;
    private SProcess? activeProcess;
    
    /// <summary>
    /// The singleton instance of ProcessorScheduler.
    /// </summary>
    private static SProcessScheduler? instance;
    
    /// <summary>
    /// Initializes a new instance of the SProcessScheduler class.
    /// </summary>
    /// <param name="schedulerClass">The ProcessorScheduler class.</param>
    private SProcessScheduler(SClass schedulerClass)
        : base(schedulerClass, 2) // 2 instance variables
    {
        // Initialize priority queues (8 priorities in standard Smalltalk)
        priorityQueues = new List<Queue<SProcess>>[9]; // Index 0 unused, 1-8 are priorities
        for (int i = 1; i <= 8; i++)
        {
            priorityQueues[i] = new List<Queue<SProcess>> { new Queue<SProcess>() };
        }
        
        // Create the quiescent process lists array
        var listsClass = SystemDictionary.LookupClass("Array");
        if (listsClass != null)
        {
            var lists = new SArray(8);
            SetInstanceVariable(QuiescentProcessListsIndex, lists);
        }
        
        SetInstanceVariable(ActiveProcessIndex, SNil.Instance);
    }
    
    /// <summary>
    /// Gets the singleton instance of ProcessorScheduler.
    /// </summary>
    public static SProcessScheduler GetInstance()
    {
        if (instance == null)
        {
            var schedulerClass = SystemDictionary.LookupClass("ProcessorScheduler") 
                ?? throw new InvalidOperationException("ProcessorScheduler class not found");
            instance = new SProcessScheduler(schedulerClass);
        }
        return instance;
    }
    
    /// <summary>
    /// Gets or sets the active process.
    /// </summary>
    public SProcess? ActiveProcess
    {
        get => activeProcess;
        private set
        {
            activeProcess = value;
            SetInstanceVariable(ActiveProcessIndex, value ?? (SObject)SNil.Instance);
        }
    }
    
    /// <summary>
    /// Schedules a process at the given priority.
    /// </summary>
    public void Schedule(SProcess process, int priority)
    {
        if (priority < 1 || priority > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be between 1 and 8");
        }
        
        process.Priority = SmallInteger.From(priority);
        process.MyList = this;
        priorityQueues[priority][0].Enqueue(process);
    }
    
    /// <summary>
    /// Yields control to the next process at the same or higher priority.
    /// </summary>
    public SProcess? Yield()
    {
        if (activeProcess == null)
        {
            return ScheduleNextProcess();
        }
        
        var currentPriority = (int)activeProcess.Priority.Value;
        
        // Put the current process at the end of its priority queue
        priorityQueues[currentPriority][0].Enqueue(activeProcess);
        
        // Schedule the next process
        return ScheduleNextProcess();
    }
    
    /// <summary>
    /// Suspends the active process.
    /// </summary>
    public SProcess? SuspendActive(SObject suspendedContext)
    {
        if (activeProcess != null)
        {
            activeProcess.Suspend(suspendedContext);
            activeProcess.MyList = SNil.Instance;
        }
        
        return ScheduleNextProcess();
    }
    
    /// <summary>
    /// Resumes a suspended process.
    /// </summary>
    public void Resume(SProcess process)
    {
        if (process.IsTerminated())
        {
            throw new InvalidOperationException("Cannot resume a terminated process");
        }
        
        var priority = (int)process.Priority.Value;
        Schedule(process, priority);
        
        // If the resumed process has higher priority than active, switch to it
        if (activeProcess != null && priority > activeProcess.Priority.Value)
        {
            // Preempt current process
            var current = activeProcess;
            ActiveProcess = process;
            
            // Put preempted process back in queue
            Schedule(current, (int)current.Priority.Value);
        }
    }
    
    /// <summary>
    /// Terminates a process.
    /// </summary>
    public SProcess? Terminate(SProcess process)
    {
        process.Terminate();
        
        if (process == activeProcess)
        {
            return ScheduleNextProcess();
        }
        
        // Remove from any queue it might be in
        var priority = (int)process.Priority.Value;
        if (priority >= 1 && priority <= 8)
        {
            var queue = priorityQueues[priority][0];
            var tempQueue = new Queue<SProcess>();
            
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p != process)
                {
                    tempQueue.Enqueue(p);
                }
            }
            
            priorityQueues[priority][0] = tempQueue;
        }
        
        return activeProcess;
    }
    
    /// <summary>
    /// Schedules the next highest priority process.
    /// </summary>
    private SProcess? ScheduleNextProcess()
    {
        // Find the highest priority non-empty queue
        for (int priority = 8; priority >= 1; priority--)
        {
            var queue = priorityQueues[priority][0];
            if (queue.Count > 0)
            {
                ActiveProcess = queue.Dequeue();
                return activeProcess;
            }
        }
        
        // No processes to schedule
        ActiveProcess = null;
        return null;
    }
    
    /// <summary>
    /// Creates a new process with the given block and priority.
    /// </summary>
    public SProcess Fork(SBlockContext block, int priority = UserSchedulingPriority)
    {
        var processClass = SystemDictionary.LookupClass("Process")
            ?? throw new InvalidOperationException("Process class not found");
            
        var process = new SProcess(processClass, block, priority);
        Schedule(process, priority);
        
        return process;
    }
    
    /// <summary>
    /// Waits on a semaphore.
    /// </summary>
    public void Wait(SSemaphore semaphore, SObject context)
    {
        if (activeProcess == null)
        {
            throw new InvalidOperationException("No active process to wait");
        }
        
        // Add process to semaphore's waiting list
        semaphore.AddWaitingProcess(activeProcess);
        
        // Suspend the active process
        SuspendActive(context);
    }
    
    /// <summary>
    /// Signals a semaphore.
    /// </summary>
    public void Signal(SSemaphore semaphore)
    {
        var process = semaphore.RemoveWaitingProcess();
        if (process != null)
        {
            Resume(process);
        }
        else
        {
            // Increment excess signals
            semaphore.IncrementExcessSignals();
        }
    }
    
    /// <summary>
    /// Gets the number of processes at a given priority.
    /// </summary>
    public int ProcessCountAt(int priority)
    {
        if (priority < 1 || priority > 8)
        {
            return 0;
        }
        
        return priorityQueues[priority][0].Count;
    }
    
    /// <summary>
    /// Returns a string representation of the scheduler.
    /// </summary>
    public override string ToDisplayString()
    {
        var activeDesc = activeProcess != null ? 
            $"active={activeProcess.ToDisplayString()}" : "no active process";
            
        var queueCounts = new List<string>();
        for (int i = 1; i <= 8; i++)
        {
            var count = ProcessCountAt(i);
            if (count > 0)
            {
                queueCounts.Add($"P{i}={count}");
            }
        }
        
        var queues = queueCounts.Count > 0 ? 
            $", queues=[{string.Join(", ", queueCounts)}]" : "";
            
        return $"ProcessorScheduler({activeDesc}{queues})";
    }
}