using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Scheduling;

/// <summary>
/// Manages timer-based semaphore signals for the Delay mechanism.
/// Uses System.Threading.Timer for interrupt-driven precision timing.
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
    private volatile bool _timerExpired = false;
    private readonly object _lock = new();

    // Interrupt-driven timer and wait handle
    private System.Threading.Timer? _timer;
    private readonly ManualResetEventSlim _timerExpiredEvent = new(false);
    private bool _disposed = false;

    /// <summary>
    /// Gets a WaitHandle that is signaled when a timer expires.
    /// Used with WaitHandle.WaitAny for efficient multi-source waiting.
    /// </summary>
    public WaitHandle TimerExpiredHandle => _timerExpiredEvent.WaitHandle;

    /// <summary>
    /// Schedules a semaphore to be signaled when the millisecond clock reaches the target time.
    /// Uses System.Threading.Timer for sub-millisecond precision.
    /// </summary>
    public void ScheduleSignal(SmalltalkOOP semaphoreOOP, uint targetMilliseconds)
    {
        lock (_lock)
        {
            // Dispose old timer before creating new one
            _timer?.Dispose();
            _timer = null;

            _pendingSemaphore = semaphoreOOP;
            _targetMilliseconds = targetMilliseconds;
            _timerExpired = false;
            _timerExpiredEvent.Reset();

            uint currentMs = (uint)(Environment.TickCount64 & 0xFFFFFFFF);
            uint diff = targetMilliseconds - currentMs;

            // If already expired or very close (wraparound: diff >= 0x80000000),
            // signal immediately
            if (diff >= 0x80000000 || diff == 0)
            {
                _timerExpired = true;
                _timerExpiredEvent.Set();
                return;
            }

            // Schedule timer callback
            int delayMs = diff > int.MaxValue ? int.MaxValue : (int)diff;
            _timer = new System.Threading.Timer(
                OnTimerExpired,
                null,
                delayMs,
                Timeout.Infinite);  // One-shot timer
        }
    }

    /// <summary>
    /// Timer callback - runs on thread pool thread.
    /// Only sets a flag and signals the wait handle; actual semaphore signaling
    /// is done on the VM thread for thread safety.
    /// </summary>
    private void OnTimerExpired(object? state)
    {
        lock (_lock)
        {
            if (!_disposed && _pendingSemaphore != default)
            {
                _timerExpired = true;
                _timerExpiredEvent.Set();
            }
        }
    }

    /// <summary>
    /// Cancels any pending timer signal.
    /// </summary>
    public void CancelTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _pendingSemaphore = default;
            _targetMilliseconds = 0;
            _timerExpired = false;
            _timerExpiredEvent.Reset();
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
            if (!_timerExpired)
            {
                // Also check polling-style for backwards compatibility
                // and to handle race conditions
                if (_pendingSemaphore == default)
                    return default;

                uint currentMs = (uint)(Environment.TickCount64 & 0xFFFFFFFF);
                uint diff = currentMs - _targetMilliseconds;
                if (diff >= 0x80000000) // Not yet expired
                    return default;

                // Timer has expired but callback hasn't run yet
                _timerExpired = true;
            }

            // Timer has expired - return and clear the semaphore
            var semaphore = _pendingSemaphore;
            _pendingSemaphore = default;
            _timerExpired = false;
            _timerExpiredEvent.Reset();
            _timer?.Dispose();
            _timer = null;
            return semaphore;
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
            if (_pendingSemaphore == default)
                return -1;

            if (_timerExpired)
                return 0;

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
                return _pendingSemaphore != default;
            }
        }
    }

    /// <summary>
    /// Disposes of timer resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            _timerExpiredEvent.Dispose();
        }
    }
}
