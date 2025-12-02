using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Interface for managing timer-based semaphore signals.
/// Used by primitive 100 (signal:atMilliseconds:) to implement Delay.
/// </summary>
public interface ITimerManager
{
    /// <summary>
    /// Schedules a semaphore to be signaled when the millisecond clock reaches the target time.
    /// If a timer is already pending, it is replaced.
    /// </summary>
    /// <param name="semaphoreOOP">The semaphore to signal.</param>
    /// <param name="targetMilliseconds">The millisecond clock value at which to signal.</param>
    void ScheduleSignal(SmalltalkOOP semaphoreOOP, uint targetMilliseconds);

    /// <summary>
    /// Cancels any pending timer signal.
    /// Called when signal:atMilliseconds: is called with nil semaphore.
    /// </summary>
    void CancelTimer();

    /// <summary>
    /// Gets the semaphore that should be signaled now, if any timer has expired.
    /// Returns default if no timer has expired.
    /// This should be called periodically by the VM to check for expired timers.
    /// </summary>
    /// <returns>The semaphore OOP to signal, or default if none.</returns>
    SmalltalkOOP GetExpiredTimerSemaphore();

    /// <summary>
    /// Gets the number of milliseconds until the next timer expires.
    /// Returns -1 if no timer is pending.
    /// Used by the VM idle loop to determine how long to wait.
    /// </summary>
    int GetMillisecondsUntilNextTimer();

    /// <summary>
    /// Checks if there is a pending timer.
    /// </summary>
    bool HasPendingTimer { get; }
}
