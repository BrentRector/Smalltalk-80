# Proposed Timer and Event Optimization

**Last Updated**: 2025-12-03
**Status**: ⚠️ PARTIAL - WaitAny works but idle detection needs improvement (see CPU-USAGE-ANALYSIS.md)
**Constraint**: No changes to Smalltalk-80.st source code (except defensive nil checks)

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

## Implementation Notes (2025-12-03)

**WaitHandle.WaitAny - FIXED**:

Initial implementation using `WaitHandle.WaitAny([eventHandle, timerHandle], timeout)` failed because:
- `SmalltalkEventQueue` used `AutoResetEvent` which auto-resets when WaitAny returns
- This "consumed" the event signal before the event could be processed
- Result: Input events (mouse clicks, etc.) were lost - popup menus stopped working

**Solution Implemented**:
1. Changed `SmalltalkEventQueue` to use `ManualResetEventSlim` instead of `AutoResetEvent`
2. Added `ResetSignalIfEmpty()` method with double-check pattern to safely reset the signal
3. Added `ResetEventSignalIfEmpty()` to `IPlatformBridge` interface
4. Updated InterpreterLoop to use `WaitHandle.WaitAny` with both event and timer handles

**Final Implementation Status**:
- ✅ TimerManager uses interrupt-driven `System.Threading.Timer`
- ✅ ITimerManager extended with `TimerExpiredHandle` and `IDisposable`
- ✅ SmalltalkEventQueue uses `ManualResetEventSlim` with double-check reset pattern
- ✅ InterpreterLoop uses `WaitHandle.WaitAny([eventHandle, timerHandle], timeout)`
- ✅ Process scheduling methods refactored to `VirtualMachine.ProcessScheduling.cs`

**Code Organization (2025-12-03)**:
Process scheduling methods extracted to partial class for maintainability:
- `CheckAndSignalExpiredTimer()` - Timer expiration and semaphore signaling
- `AddProcessToReadyQueue()` - Add process to priority queue
- `GetActiveProcess()` - Get current process from ProcessorScheduler
- `SwitchToNextRunnableProcess()` - Context switching on process termination
- `FindHighestPriorityRunnableProcess()` - Priority-based process selection
- `RemoveProcessFromList()` - LinkedList manipulation
- `DumpProcessQueues()` - Diagnostic output

**Double-Check Reset Pattern**:
```csharp
public void ResetSignalIfEmpty()
{
    // Only reset if queue appears empty
    if (!HasPendingEvents)
    {
        _eventSignal.Reset();
        // Double-check: producer might have added after our check but before reset
        if (HasPendingEvents)
        {
            _eventSignal.Set();
        }
    }
}
```

This pattern handles race conditions:
1. If producer adds event between check and reset, their Set() comes after our Reset(), so it stays set
2. If producer adds after reset but before double-check, we detect it and re-signal
3. If producer adds after double-check, their Set() is effective

## Final Recommendation

**Full WaitHandle.WaitAny implementation achieved**:
- Interrupt-driven timer provides precision improvements ✅
- WaitHandle.WaitAny provides unified multi-source waiting ✅
- ManualResetEventSlim with double-check pattern ensures no event loss ✅
- Code is well-organized across partial class files ✅

**Status**: ✅ COMPLETE - Timer precision improved, WaitAny working, event handling reliable.

## Conclusion

The unified implementation provides optimal behavior:
- **Timer precision**: Interrupt-driven `System.Threading.Timer` signals immediately when delay expires
- **Unified waiting**: `WaitHandle.WaitAny` waits on both event queue and timer in single call
- **Event reliability**: ManualResetEventSlim with double-check reset pattern ensures no events are lost
- **CPU efficiency**: Near-zero CPU during idle via kernel-level waiting on multiple handles
- **Maintainability**: Process scheduling code extracted to dedicated partial class

The key insight was switching from AutoResetEvent to ManualResetEventSlim, which requires explicit reset but allows proper event handling after WaitAny returns.

## ⚠️ Remaining Issue: 3% Idle CPU (2025-12-03)

**Problem**: Despite WaitAny working correctly, idle CPU is ~3% instead of 0%.

**Key Insight**: The system should be **100% idle** when the background process is active. We should **never execute the background process's bytecodes**. The only wake sources are:
- UI event → signal input semaphore → process switch → WaitAny returns
- Timer expires → signal timer semaphore → process switch → WaitAny returns

**Root Causes**:

1. **Breaking on `HasPendingEvents()`** - WRONG! Events don't wake processes directly; they sit in the queue until the input semaphore is signaled. Breaking on pending events causes a busy-loop.

2. **Executing background process bytecodes** - The background process `[true] whileTrue: []` is a sentinel, not work. Its bytecodes should never execute. Currently, after exiting the idle loop (due to the HasPendingEvents bug), we execute bytecodes unnecessarily.

**The Fix**:
1. **Remove `HasPendingEvents()` check entirely** - only exit idle when `GetActiveProcess()` changes
2. **Check for background process every iteration** - when detected, enter idle immediately
3. **Never execute background process bytecodes** - skip `ExecuteBytecodeStep()` when idle
4. **Stay in WaitAny** until a real process switch occurs

**Full Analysis**: See `docs/Architecture/CPU-USAGE-ANALYSIS.md` for complete implementation.
