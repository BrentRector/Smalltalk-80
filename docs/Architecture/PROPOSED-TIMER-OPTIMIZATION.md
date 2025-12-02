# Proposed Timer and Event Optimization

**Last Updated**: 2025-12-02
**Status**: PROPOSAL
**Constraint**: No changes to Smalltalk-80.st source code

## Executive Summary

This document proposes optimizations to the VM's timer and event handling infrastructure. The current polling-based implementation is functional but can be improved using Windows-native interrupt-driven mechanisms for better timer accuracy and reduced CPU overhead.

## Expert Analysis Summary

### Blue Book Compatibility (Smalltalk Expert)

The current implementation is **fully Blue Book compliant**:
- Primitive 100 (`signal:atMilliseconds:`) schedules semaphore signals correctly
- The Blue Book specifies the interface, not the implementation details
- Polling for timer expiration is a legitimate implementation strategy
- Current approach is actually **superior** to Blue Book's implied busy-wait model

**Verdict**: No changes required for Blue Book compliance.

### Windows Optimization Opportunities (C# Expert)

The current implementation can be optimized using native Windows primitives:

| Current Approach | Proposed Approach |
|-----------------|-------------------|
| Polling every N bytecodes for timer expiration | Interrupt-driven timer callback |
| Sequential wait: first events, then check timer | Unified `WaitHandle.WaitAny()` on multiple sources |
| Fixed 10ms max idle wait | Precise timer-driven wakeup |

## Proposed Architecture

### 1. InterruptDrivenTimerManager

Replace polling-based timer check with `System.Threading.Timer`:

```csharp
public class InterruptDrivenTimerManager : ITimerManager, IDisposable
{
    private SmalltalkOOP _pendingSemaphore = default;
    private readonly ManualResetEventSlim _timerExpiredEvent = new(false);
    private System.Threading.Timer? _timer;
    private readonly object _lock = new();
    private volatile bool _timerExpired = false;

    public WaitHandle TimerExpiredHandle => _timerExpiredEvent.WaitHandle;

    public void ScheduleSignal(SmalltalkOOP semaphoreOOP, uint targetMilliseconds)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _pendingSemaphore = semaphoreOOP;
            _timerExpired = false;
            _timerExpiredEvent.Reset();

            uint currentMs = (uint)(Environment.TickCount64 & 0xFFFFFFFF);
            uint diff = targetMilliseconds - currentMs;

            // If already expired or very close, signal immediately
            if (diff >= 0x80000000 || diff == 0)
            {
                _timerExpired = true;
                _timerExpiredEvent.Set();
                return;
            }

            int delayMs = diff > int.MaxValue ? int.MaxValue : (int)diff;
            _timer = new System.Threading.Timer(
                OnTimerExpired,
                null,
                delayMs,
                Timeout.Infinite);
        }
    }

    private void OnTimerExpired(object? state)
    {
        lock (_lock)
        {
            _timerExpired = true;
            _timerExpiredEvent.Set();
        }
    }

    public SmalltalkOOP GetExpiredTimerSemaphore()
    {
        lock (_lock)
        {
            if (!_timerExpired)
                return default;

            var semaphore = _pendingSemaphore;
            _pendingSemaphore = default;
            _timerExpired = false;
            _timerExpiredEvent.Reset();
            _timer?.Dispose();
            _timer = null;
            return semaphore;
        }
    }

    // ... remaining interface methods
}
```

### 2. Unified Wait with WaitHandle.WaitAny

Replace sequential waiting with multi-source wait:

```csharp
// In IPlatformBridge or new IVMEventSource interface
public interface IVMEventSource
{
    WaitHandle[] GetWaitHandles();  // Event queue + timer + shutdown
    void ProcessExpiredSources();
}

// In InterpreterLoop
private void WaitForActivity(int maxWaitMs)
{
    WaitHandle[] handles = new WaitHandle[]
    {
        _eventQueue.EventAvailableHandle,
        _timerManager.TimerExpiredHandle,
        _shutdownEvent  // Optional: for clean shutdown
    };

    int index = WaitHandle.WaitAny(handles, maxWaitMs);

    // index tells us which source woke us:
    // 0 = input event arrived
    // 1 = timer expired
    // 2 = shutdown requested
    // WaitHandle.WaitTimeout = timeout elapsed
}
```

### 3. SmalltalkEventQueue Enhancement

Add `WaitHandle` exposure:

```csharp
public class SmalltalkEventQueue : IDisposable
{
    private readonly AutoResetEvent _eventSignal = new(false);

    // Expose for WaitAny
    public WaitHandle EventAvailableHandle => _eventSignal;

    public void Enqueue(SmalltalkEvent evt)
    {
        _events.Enqueue(evt);
        _eventSignal.Set();  // Wake any waiters
    }
}
```

## Implementation Plan

### Phase 1: Add WaitHandle Exposure (Low Risk)

1. Expose `WaitHandle` properties on `SmalltalkEventQueue`
2. Add `TimerExpiredHandle` to `ITimerManager` interface
3. No behavioral changes, just infrastructure

### Phase 2: Implement InterruptDrivenTimerManager (Medium Risk)

1. Create new `InterruptDrivenTimerManager` class
2. Add `System.Threading.Timer` for precise callbacks
3. Use `ManualResetEventSlim` for thread signaling
4. Implement proper disposal and thread safety

### Phase 3: Integrate WaitHandle.WaitAny (Medium Risk)

1. Modify `InterpreterLoop` idle detection
2. Replace polling loop with `WaitHandle.WaitAny`
3. Handle all wake sources uniformly

### Phase 4: Cleanup (Low Risk)

1. Remove polling-based timer checks
2. Simplify `GetMillisecondsUntilNextTimer()` (no longer needed for wait calculation)
3. Update documentation

## Benefits

| Aspect | Current | Proposed |
|--------|---------|----------|
| Timer Accuracy | ±10ms (polling interval) | Sub-millisecond (interrupt-driven) |
| CPU During Idle | Near-zero (good) | Near-zero (same) |
| Wake Latency | Up to 10ms | Immediate |
| Code Complexity | Simple polling | More complex, but cleaner wait |
| Thread Safety | Lock-based | Lock-based + WaitHandle |

## Risks and Mitigations

### Risk 1: Timer Callback Thread Safety
- **Issue**: `System.Threading.Timer` callbacks run on thread pool threads
- **Mitigation**: Only set a flag and signal WaitHandle; actual semaphore signaling done on VM thread

### Risk 2: Resource Leaks
- **Issue**: Timer and WaitHandle resources need disposal
- **Mitigation**: Implement `IDisposable` pattern throughout

### Risk 3: Race Conditions
- **Issue**: Timer expiration during reschedule
- **Mitigation**: Dispose old timer before creating new one under lock

## Recommendation

**Implement in phases**, starting with Phase 1 (infrastructure) to minimize risk. The current implementation is correct and efficient; these changes provide marginal improvements in timer precision and code cleanliness.

**Priority**: LOW - Current implementation works well. Consider implementing when:
- Animation timing precision becomes important
- Multi-threaded Smalltalk processes are needed
- Complex timer scenarios require sub-10ms accuracy

## Files to Modify

| File | Changes |
|------|---------|
| `ITimerManager.cs` | Add `WaitHandle TimerExpiredHandle { get; }` |
| `TimerManager.cs` | Rename to `PollingTimerManager.cs`, create new `InterruptDrivenTimerManager.cs` |
| `SmalltalkEventQueue.cs` | Add `WaitHandle EventAvailableHandle` property |
| `IPlatformBridge.cs` | Add `WaitHandle` exposure methods |
| `VirtualMachine.cs` | Replace polling with `WaitHandle.WaitAny` in idle loop |

## Conclusion

The current polling-based implementation is **correct, efficient, and Blue Book compliant**. The proposed changes offer:
- Better timer precision (interrupt-driven vs polling)
- Cleaner wait semantics (unified multi-source wait)
- Foundation for future multi-threading enhancements

Implementation is optional and should be prioritized based on actual needs for timer precision in the Smalltalk environment.
