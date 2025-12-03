using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Interface for Smalltalk-80 process scheduling operations.
/// Provides a unified API for process management used by both the VM interpreter loop
/// and primitive implementations, eliminating code duplication and preventing divergence.
///
/// Key operations:
/// - Process activation/suspension with context switching
/// - Ready queue management (add/remove processes)
/// - Priority-based process selection
/// - Semaphore signaling with preemption
/// </summary>
public interface IProcessScheduler
{
    /// <summary>
    /// Gets the currently active process OOP from ProcessorScheduler.
    /// </summary>
    SmalltalkOOP GetActiveProcess();

    /// <summary>
    /// Activates a process - sets it as the active process and switches to its context.
    /// Handles unactivated BlockContexts (PC is nil) by initializing them.
    /// </summary>
    /// <param name="processOOP">The process to activate</param>
    /// <param name="reason">Logging reason (e.g., "preemption", "termination", "resume")</param>
    /// <returns>True if successfully activated, false if context is invalid</returns>
    bool ActivateProcess(SmalltalkOOP processOOP, string reason);

    /// <summary>
    /// Suspends the current active process - saves its context and optionally returns it to ready queue.
    /// Used when preempting, yielding, or waiting on a semaphore.
    /// </summary>
    /// <param name="returnToQueue">If true, adds the process back to its priority queue</param>
    void SuspendActiveProcess(bool returnToQueue);

    /// <summary>
    /// Adds a process to the appropriate priority ready queue.
    /// </summary>
    /// <param name="processOOP">The process to add</param>
    /// <param name="priority">The process's priority (1-8)</param>
    void AddProcessToReadyQueue(SmalltalkOOP processOOP, int priority);

    /// <summary>
    /// Removes a process from a linked list (LinkedList structure).
    /// Used when removing from semaphore wait lists or ready queues.
    /// </summary>
    /// <param name="processOOP">The process to remove</param>
    /// <param name="listOOP">The LinkedList to remove from</param>
    void RemoveProcessFromList(SmalltalkOOP processOOP, SmalltalkOOP listOOP);

    /// <summary>
    /// Finds the highest priority process that is runnable (on a quiescentProcessList).
    /// Searches from priority 8 down to priority 1.
    /// </summary>
    /// <returns>The highest priority runnable process, or nil if none found</returns>
    SmalltalkOOP FindHighestPriorityRunnableProcess();

    /// <summary>
    /// Switches execution to the next runnable process when the current process terminates or suspends.
    /// Finds highest priority process, removes it from queue, and activates it.
    /// </summary>
    /// <param name="terminatingProcess">The process that is terminating/suspending</param>
    /// <returns>True if successfully switched, false if no runnable processes found</returns>
    bool SwitchToNextRunnableProcess(SmalltalkOOP terminatingProcess);

    /// <summary>
    /// Checks if a process should preempt the currently active process.
    /// Returns true if the given process has higher priority than the active process.
    /// </summary>
    /// <param name="processOOP">The process to check</param>
    /// <returns>True if processOOP has higher priority than active process</returns>
    bool ShouldPreempt(SmalltalkOOP processOOP);

    /// <summary>
    /// Signals a semaphore with preemption handling.
    /// If processes are waiting, wakes first waiter. If woken process has higher priority, preempts.
    /// If no waiters, increments excessSignals.
    /// </summary>
    /// <param name="semaphoreOOP">The semaphore to signal</param>
    void SignalSemaphoreWithPreemption(SmalltalkOOP semaphoreOOP);

    /// <summary>
    /// Adds a process to a semaphore's wait list and switches to next runnable process.
    /// Used by Semaphore>>wait when no excess signals available.
    /// </summary>
    /// <param name="semaphoreOOP">The semaphore to wait on</param>
    /// <param name="processOOP">The process to add to wait list</param>
    /// <returns>True if successfully added and switched, false on error</returns>
    bool WaitOnSemaphore(SmalltalkOOP semaphoreOOP, SmalltalkOOP processOOP);

    /// <summary>
    /// Gets the priority of a process.
    /// </summary>
    /// <param name="processOOP">The process</param>
    /// <returns>The priority (1-8), or 0 if invalid</returns>
    int GetProcessPriority(SmalltalkOOP processOOP);

    /// <summary>
    /// Gets the myList field of a process (which queue/list it's on).
    /// </summary>
    /// <param name="processOOP">The process</param>
    /// <returns>The LinkedList OOP, or nil if not on any list</returns>
    SmalltalkOOP GetProcessMyList(SmalltalkOOP processOOP);

    /// <summary>
    /// Sets the myList field of a process.
    /// </summary>
    /// <param name="processOOP">The process</param>
    /// <param name="listOOP">The LinkedList OOP (or nil)</param>
    void SetProcessMyList(SmalltalkOOP processOOP, SmalltalkOOP listOOP);
}
