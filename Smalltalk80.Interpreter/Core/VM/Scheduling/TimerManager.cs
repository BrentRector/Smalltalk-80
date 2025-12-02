using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Manages timer-based semaphore signals for the Delay mechanism.
///
/// The Smalltalk Delay class works as follows:
/// 1. Delay>>wait calls Processor signal:TimingSemaphore atTime:resumptionTime
/// 2. This schedules a timer via primitive 100
/// 3. When the timer expires, the VM signals the TimingSemaphore
/// 4. The Delay handler process wakes up and resumes the delayed process
///
/// Note: Smalltalk uses a single timer at a time. The Delay class maintains
/// a sorted list of pending delays and only schedules the soonest one.
/// </summary>
public class TimerManager : ITimerManager
{
    private SmalltalkOOP _pendingSemaphore = default;
    private uint _targetMilliseconds = 0;
    private bool _timerActive = false;
    private readonly object _lock = new();

    /// <summary>
    /// Schedules a semaphore to be signaled when the millisecond clock reaches the target time.
    /// </summary>
    public void ScheduleSignal(SmalltalkOOP semaphoreOOP, uint targetMilliseconds)
    {
        lock (_lock)
        {
            _pendingSemaphore = semaphoreOOP;
            _targetMilliseconds = targetMilliseconds;
            _timerActive = true;
        }
    }

    /// <summary>
    /// Cancels any pending timer signal.
    /// </summary>
    public void CancelTimer()
    {
        lock (_lock)
        {
            _pendingSemaphore = default;
            _targetMilliseconds = 0;
            _timerActive = false;
        }
    }

    /// <summary>
    /// Gets the semaphore that should be signaled now, if the timer has expired.
    /// Returns default if no timer has expired.
    /// </summary>
    public SmalltalkOOP GetExpiredTimerSemaphore()
    {
        lock (_lock)
        {
            if (!_timerActive)
                return default;

            uint currentMs = (uint)(Environment.TickCount64 & 0xFFFFFFFF);

            // Handle wraparound: if target is far in the "past" (large negative difference),
            // it's actually in the future due to clock wraparound
            // We consider a timer expired if: (current - target) < 0x80000000
            // This means the target is at most ~24 days in the past
            uint diff = currentMs - _targetMilliseconds;
            if (diff < 0x80000000) // Timer has expired
            {
                var semaphore = _pendingSemaphore;
                _pendingSemaphore = default;
                _timerActive = false;
                return semaphore;
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the number of milliseconds until the next timer expires.
    /// Returns -1 if no timer is pending.
    /// </summary>
    public int GetMillisecondsUntilNextTimer()
    {
        lock (_lock)
        {
            if (!_timerActive)
                return -1;

            uint currentMs = (uint)(Environment.TickCount64 & 0xFFFFFFFF);
            uint diff = _targetMilliseconds - currentMs;

            // If diff is very large (> 0x80000000), the timer has already expired
            if (diff >= 0x80000000)
                return 0;

            // Cap at int.MaxValue to avoid overflow
            return diff > int.MaxValue ? int.MaxValue : (int)diff;
        }
    }

    /// <summary>
    /// Checks if there is a pending timer.
    /// </summary>
    public bool HasPendingTimer
    {
        get
        {
            lock (_lock)
            {
                return _timerActive;
            }
        }
    }
}
