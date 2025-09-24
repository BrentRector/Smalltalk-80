using Core.Memory;

namespace Core.Process;

/// <summary>
/// Manages Smalltalk process scheduling according to the Blue Book specification.
/// </summary>
public interface IProcessScheduler
{
    /// <summary>
    /// Gets the currently active process.
    /// </summary>
    OOP ActiveProcess { get; }
    
    /// <summary>
    /// Gets whether the scheduler is enabled and running.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Enables or disables the scheduler.
    /// </summary>
    void SetEnabled(bool enabled);
    
    /// <summary>
    /// Creates a new process with the given block and priority.
    /// </summary>
    /// <param name="block">The block to execute.</param>
    /// <param name="priority">The process priority (1-8).</param>
    /// <returns>The new process OOP.</returns>
    OOP CreateProcess(OOP block, int priority);
    
    /// <summary>
    /// Schedules a process for execution.
    /// </summary>
    /// <param name="process">The process to schedule.</param>
    void Schedule(OOP process);
    
    /// <summary>
    /// Suspends the currently active process.
    /// </summary>
    void SuspendActiveProcess();
    
    /// <summary>
    /// Resumes a suspended process.
    /// </summary>
    /// <param name="process">The process to resume.</param>
    void Resume(OOP process);
    
    /// <summary>
    /// Terminates a process.
    /// </summary>
    /// <param name="process">The process to terminate.</param>
    void Terminate(OOP process);
    
    /// <summary>
    /// Yields control from the current process to allow others to run.
    /// </summary>
    void Yield();
    
    /// <summary>
    /// Performs a scheduling cycle to select the next process.
    /// </summary>
    /// <returns>True if a process switch occurred.</returns>
    bool ScheduleNext();
    
    /// <summary>
    /// Gets processes at the specified priority level.
    /// </summary>
    /// <param name="priority">The priority level (1-8).</param>
    /// <returns>Collection of process OOPs at that priority.</returns>
    IReadOnlyList<OOP> GetProcessesAtPriority(int priority);
    
    /// <summary>
    /// Changes the priority of a process.
    /// </summary>
    /// <param name="process">The process to modify.</param>
    /// <param name="newPriority">The new priority level.</param>
    void ChangePriority(OOP process, int newPriority);
}

/// <summary>
/// Smalltalk process priorities according to the Blue Book.
/// </summary>
public static class ProcessPriority
{
    public const int SystemBackground = 1;
    public const int UserBackground = 2;
    public const int UserScheduling = 3;
    public const int UserInterrupt = 4;
    public const int SystemForeground = 5;
    public const int HighIOPriority = 6;
    public const int LowIOPriority = 7;
    public const int TimingPriority = 8;
    
    public const int Min = SystemBackground;
    public const int Max = TimingPriority;
    public const int Default = UserScheduling;
}