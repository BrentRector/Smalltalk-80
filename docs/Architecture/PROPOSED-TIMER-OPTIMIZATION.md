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

## Implementation Notes (2025-12-02)

**WaitHandle.WaitAny Issue**: Initial implementation using `WaitHandle.WaitAny([eventHandle, timerHandle], timeout)` failed because:
- `SmalltalkEventQueue` uses `AutoResetEvent` which auto-resets when WaitAny returns
- This "consumes" the event signal before the event can be processed
- Result: Input events (mouse clicks, etc.) were lost - popup menus stopped working

**Current Implementation Status**:
- ✅ TimerManager uses interrupt-driven `System.Threading.Timer`
- ✅ ITimerManager extended with `TimerExpiredHandle` and `IDisposable`
- ✅ Idle loop uses simple `WaitForEvents(timeout)` polling (reliable)
- ❌ WaitHandle.WaitAny integration deferred - causes event loss

**Future Fix**: To use WaitHandle.WaitAny, would need to:
1. Change `SmalltalkEventQueue` to use `ManualResetEventSlim` instead of `AutoResetEvent`
2. Manually reset the event after checking `HasPendingEvents`
3. Handle race conditions between reset and new event arrival

## Recommendation

**Keep current hybrid implementation**:
- Interrupt-driven timer provides precision improvements ✅
- Simple polling-based wait is reliable for event handling ✅
- WaitHandle.WaitAny integration is not worth the complexity

**Priority**: COMPLETE - Timer precision improved, event handling reliable.

## Conclusion

The hybrid implementation provides the best of both worlds:
- **Timer precision**: Interrupt-driven `System.Threading.Timer` signals immediately when delay expires
- **Event reliability**: Simple polling via `WaitForEvents()` ensures no events are lost
- **CPU efficiency**: Still achieves near-zero CPU during idle via timeout-based waiting

The unified `WaitHandle.WaitAny` approach was attempted but caused event loss. The current design is stable and performant.
