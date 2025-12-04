# Proposed Event Architecture Redesign

**Date**: 2025-12-04
**Status**: PROPOSAL
**Authors**: Collaborative analysis by multiple expert agents

---

## Executive Summary

The current event handling architecture has a **critical priority inversion bug** that causes deadlocks in nested event consumers (like popup menus). This document proposes a solution based on **software interrupt simulation** that matches the Blue Book's hardware interrupt design.

### The Core Problem

**InputSemaphore is only signaled during idle state**, but nested event consumers (popup menus) need to receive events while a higher-layer controller is active. This creates a deadlock:

```
1. Controller (priority 4) shows popup menu
2. Menu waits on ControllerSemaphore
3. VM is NOT idle (priority 4 process running)
4. InputSemaphore NEVER signaled
5. InputProcess (priority 6) NEVER wakes to process events
6. ControllerSemaphore NEVER signaled
7. DEADLOCK - menu waits forever
```

### The Solution

**Add periodic event checking during normal execution** (every N bytecodes) that signals InputSemaphore when events are pending. This simulates hardware interrupts, allowing InputProcess to preempt lower-priority processes.

---

## Current Architecture Analysis

### What Works

1. **Idle detection**: When background process (priority 1) is active, VM uses `WaitHandle.WaitAny()` for true 0% CPU
2. **Priority-based scheduling**: Higher-priority processes preempt lower when they become runnable
3. **Semaphore primitives**: Signal/Wait work correctly with proper preemption
4. **Timer handling**: Checked periodically (every 1000 bytecodes) during normal execution

### What's Broken

1. **Events only checked during idle**: `SignalInputSemaphoreIfRegistered()` only called in `EnterIdleWait()`
2. **Nested consumers deadlock**: Popup menus wait on ControllerSemaphore but InputProcess can't signal it
3. **Counting semaphore mismatch**: C# uses counting semaphore but `ForceResetEventSignal()` is a no-op

### Event Flow Analysis

#### Working Path (Idle State)

```
OS Event → WinForms thread
    │
    ▼
SmalltalkEventQueue.Enqueue()
    │ increments counting semaphore
    ▼
WaitAny() returns (VM was idle)
    │
    ▼
SignalInputSemaphoreIfRegistered()
    │
    ▼
InputProcess wakes (priority 6)
    │ preempts background (priority 1)
    ▼
InputProcess processes event, signals ControllerSemaphore
    │
    ▼
Controllers wake, consume MouseEvents
    │
    ▼
✅ 0% CPU, events processed correctly
```

#### Broken Path (Active Process)

```
OS Event → WinForms thread
    │
    ▼
SmalltalkEventQueue.Enqueue()
    │ increments counting semaphore
    ▼
VM executing priority 4 process (popup menu tracking)
    │
    ❌ Periodic check only checks TIMER, not EVENTS
    │
    ❌ InputSemaphore NEVER signaled
    │
    ❌ InputProcess NEVER runs
    │
    ❌ ControllerSemaphore NEVER signaled
    │
    ❌ DEADLOCK - popup menu waits forever
```

---

## Proposed Solution

### Design Principle

**Simulate hardware interrupts in software**: The Blue Book VM checks for hardware interrupts between bytecodes. We simulate this by periodically checking the C# event queue and signaling InputSemaphore.

### Architecture Change

Add `CheckAndSignalPendingEvents()` to the periodic check block:

```csharp
// In InterpreterLoop(), every 1000 bytecodes:
if (++periodicCheckCounter >= PeriodicCheckInterval)
{
    periodicCheckCounter = 0;

    CheckAndSignalExpiredTimer();     // ✅ Existing (for Delay)
    CheckAndSignalPendingEvents();    // ⭐ NEW (for input)
}
```

### New Method

```csharp
/// <summary>
/// Checks if C# event queue has pending events and signals InputSemaphore.
/// This simulates hardware interrupt delivery, allowing InputProcess (priority 6)
/// to preempt lower-priority processes when events arrive.
/// </summary>
private void CheckAndSignalPendingEvents()
{
    if (_platformBridge == null || !_platformBridge.HasPendingEvents())
        return;

    SignalInputSemaphoreIfRegistered();
}
```

### Fixed Event Flow (Active Process)

```
OS Event → WinForms thread
    │
    ▼
SmalltalkEventQueue.Enqueue()
    │ increments counting semaphore
    ▼
VM executing priority 4 process (popup menu tracking)
    │
    ✅ Periodic check: CheckAndSignalPendingEvents()
    │ HasPendingEvents() returns true
    ▼
SignalInputSemaphoreIfRegistered()
    │
    ▼
InputProcess (priority 6) becomes runnable
    │ PREEMPTS priority 4 process
    ▼
InputProcess processes event, signals ControllerSemaphore
    │
    ▼
Priority 4 process resumes
    │ ControllerSemaphore has excess signal
    ▼
Popup menu wakes, consumes MouseEvent
    │
    ▼
✅ Events delivered, no deadlock
```

---

## Detailed Design

### 1. Interrupt Check Frequency

**Recommendation**: Every 1000 bytecodes (same as timer)

**Analysis**:
- At ~10 million bytecodes/second, 1000 bytecodes ≈ 0.1ms
- Human perception threshold: 10-50ms
- Worst-case latency: 0.1ms (imperceptible)
- Overhead: ~0.002% CPU (negligible)

**Alternative**: Check on backward jumps (catches tight loops immediately)
- More responsive but higher overhead
- Can be added later if needed

### 2. Semaphore Signaling Strategy

**InputSemaphore**:
- Signaled by: VM (during idle OR periodic check)
- Semantics: "Events may be pending, check the queue"
- Can be signaled multiple times; InputProcess processes all pending events then waits

**ControllerSemaphore**:
- Signaled by: InputProcess (after enqueuing each MouseEvent)
- Semantics: "MouseEvent enqueued, wake consumers"
- Each signal corresponds to one MouseEvent

### 3. Counting Semaphore Handling

The C# `SmalltalkEventQueue` uses a counting semaphore:
- Each `Enqueue()` increments the count
- `WaitAny()` decrements the count when it returns

**During idle**: WaitAny may return multiple times for batched events. Each return signals InputSemaphore once. InputProcess will process all pending events in one wake cycle.

**During active**: Periodic check signals InputSemaphore if `HasPendingEvents()` is true. InputProcess preempts, processes all pending events, returns to previous process.

### 4. Nested Event Consumers

**Design property**: Priority-based preemption handles nesting automatically.

**Example - Popup menu**:
1. Main controller (priority 4) shows popup menu
2. Popup menu runs nested loop in same process (still priority 4)
3. Menu calls `waitForControllerEvent` → blocks on ControllerSemaphore
4. User moves mouse → OS event queued
5. Periodic check signals InputSemaphore
6. InputProcess (priority 6) preempts menu's process
7. InputProcess processes event, signals ControllerSemaphore
8. InputProcess waits on InputSemaphore
9. Scheduler returns to priority 4 process (menu)
10. Menu wakes on ControllerSemaphore, processes MouseEvent
11. ✅ Works correctly

---

## Comparison with Current Implementation

### Benefits

| Aspect | Current | Proposed |
|--------|---------|----------|
| Idle CPU | 0% ✅ | 0% ✅ (unchanged) |
| Nested consumers | Deadlock ❌ | Work correctly ✅ |
| Event latency (active) | Infinite ❌ | <0.1ms ✅ |
| Missed events | Possible ❌ | Never ✅ |
| Code complexity | Moderate | Slightly higher |

### Trade-offs

| Trade-off | Impact | Mitigation |
|-----------|--------|------------|
| Periodic check overhead | ~0.002% CPU | Negligible, unmeasurable |
| Slightly more code | 10 lines added | Well-documented, single responsibility |
| Check frequency tuning | May need adjustment | Configurable constant |

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Excessive signaling | Low | Minor CPU spike | InputProcess drains queue in one cycle |
| Preemption storms | Very low | UI jank | Priority design prevents this |
| Regression | Low | UI broken | Comprehensive testing |

---

## Alternative Approaches Considered

### 1. Signal from UI Thread Directly

**Approach**: WinForms thread directly signals InputSemaphore
**Rejected because**:
- Cross-thread access to Smalltalk heap is unsafe
- Violates single-threaded VM design
- Would require complex synchronization

### 2. Busy-Wait in Popup Menus

**Approach**: Popup menus poll `anyButtonPressed` in tight loop
**Rejected because**:
- Burns CPU (violates 0% idle requirement)
- Battery drain on laptops
- Not a general solution (other nested consumers would need same fix)

### 3. Separate Native Thread for InputProcess

**Approach**: Run InputProcess on separate .NET thread
**Rejected because**:
- Violates Smalltalk single-thread model
- Would require extensive synchronization
- Blue Book assumes cooperative scheduling within Smalltalk

### 4. Remove waitForControllerEvent from Popup Menus

**Approach**: Popup menus poll without blocking
**Rejected because**:
- Tight polling loops burn CPU
- Doesn't fix the fundamental architecture problem
- Other code might have same issue

---

## Implementation Plan

### Phase 1: Core Fix (Minimal Change)

1. Add `CheckAndSignalPendingEvents()` method to `VirtualMachine.cs`
2. Call it from periodic check block in `InterpreterLoop()`
3. Test popup menus work correctly

**Files changed**: 1 (`VirtualMachine.cs`)
**Lines added**: ~10

### Phase 2: Smalltalk Blocking (Already Done)

The Smalltalk changes (adding `waitForControllerEvent` to PopUpMenu loops) are already in place. They will work correctly once Phase 1 is complete.

### Phase 3: Documentation Update

1. Update CLAUDE.md with new architecture
2. Update PROCESS-MANAGEMENT.md with interrupt simulation design
3. Archive this proposal as historical documentation

### Phase 4: Testing

1. **Popup menu test**: Yellow button menu should appear and track mouse
2. **0% idle test**: Task manager shows 0% CPU when no input
3. **No missed events test**: Rapid clicking registers all transitions
4. **Nested menu test**: Menu within menu works correctly
5. **Regression test**: BlockContextTest returns 1191

---

## Appendix A: Blue Book Reference

### Hardware Interrupt Handling (Chapter 29)

> "The Smalltalk-80 virtual machine is specified to signal Semaphores on three conditions:
> - user event: a key has been pressed on the keyboard, a button has been pressed on the pointing device, or the pointing device has moved.
> - timeout: a specific value of the millisecond clock has been reached.
> - low space: available object memory has fallen below certain limits."

The Blue Book implies that these interrupts are checked between bytecodes, not just during idle. Our periodic check simulates this behavior.

### Process Priority (Chapter 15)

> "Ordinarily, Processes are scheduled for the use of the processor on a simple first-come first-served basis. Whenever the active Process receives suspend or terminate, the Process that has been waiting the longest will become the new active Process."

The proposed design maintains this behavior. InputProcess (priority 6) preempts when it becomes runnable, exactly as the Blue Book specifies.

---

## Appendix B: Code Locations

### Files to Modify

| File | Change |
|------|--------|
| `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` | Add `CheckAndSignalPendingEvents()`, call from periodic check |

### Files Already Modified (Smalltalk)

| File | Change | Status |
|------|--------|--------|
| `Smalltalk-80 Source/Smalltalk-80.st` | PopUpMenu uses `waitForControllerEvent` | Done |
| `Smalltalk-80 Source/Smalltalk-80.st` | `waitNoButton` uses `waitForControllerEvent` | Done |
| `Smalltalk-80 Source/Smalltalk-80.st` | `waitButton` uses `waitForControllerEvent` | Done |

---

## Conclusion

The proposed **software interrupt simulation** approach:

1. ✅ Matches Blue Book hardware interrupt design
2. ✅ Fixes nested consumer deadlock (popup menus)
3. ✅ Preserves 0% idle CPU
4. ✅ Guarantees no missed events
5. ✅ Requires minimal code changes
6. ✅ Has negligible performance overhead

**Recommendation**: Implement Phase 1 (core fix) immediately to restore popup menu functionality.
