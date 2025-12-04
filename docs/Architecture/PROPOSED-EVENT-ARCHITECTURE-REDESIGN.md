# Proposed Event Architecture Redesign

**Date**: 2025-12-04
**Status**: PROPOSAL (v2 - with expert feedback integrated)
**Authors**: Collaborative analysis by multiple expert agents

---

## Executive Summary

The current event handling architecture has a **critical priority inversion bug** that causes deadlocks in nested event consumers (like popup menus). This document proposes a solution based on **software interrupt simulation** with **edge-triggered, idempotent signaling** that matches the Blue Book's hardware interrupt design.

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

**Add periodic event checking during normal execution** with an **edge-triggered interrupt flag** that ensures:
1. InputProcess can preempt lower-priority processes when events arrive
2. Signaling is idempotent (no duplicate wakes or count inflation)
3. InputProcess drains the entire queue per wake cycle
4. Idle path coalesces burst events into single wake

---

## Design Principles

### 1. Edge-Triggered, Idempotent Signaling

**Problem with level-triggered approach**: Re-signaling InputSemaphore at every periodic check while events are pending causes unnecessary wakeups if InputProcess is already runnable or draining. This inflates semaphore counts and creates wake cycles with no new work.

**Solution**: Use a VM-local flag `_inputInterruptPending` that:
- Is set to `true` by the UI thread on enqueue (edge-arm)
- Is checked and cleared atomically by the periodic check
- Ensures exactly one InputSemaphore signal per event batch

```
Enqueue (UI thread):
  queue.Add(event)
  _inputInterruptPending = true  // Edge-arm; idempotent if already true

Periodic check (VM thread):
  if (_inputInterruptPending && HasPendingEvents()):
    _inputInterruptPending = false  // Clear BEFORE signal
    SignalInputSemaphore()          // One signal per batch
```

### 2. Full Queue Drain Per Wake

**Problem**: If InputProcess only processes one event per wake, and multiple events arrive, semaphore counts inflate and create unnecessary context switches.

**Solution**: InputProcess drains the entire queue before waiting again:

```smalltalk
InputState>>run
    [true] whileTrue: [
        InputSemaphore wait.
        "Drain entire queue in one wake cycle"
        [self primInputWord notNil] whileTrue: [
            self processOneEvent.
            self enqueueMouseEventIfNeeded.
            ControllerSemaphore signal].
    ]
```

### 3. Idle Path Coalescing

**Problem**: WaitAny() can return multiple times for bursty events, causing bounce between idle and active states.

**Solution**: Use the same `_inputInterruptPending` guard in idle path:

```
Idle wait:
  Park only when: ActiveProcessPriority == 1
                  && !_inputInterruptPending
                  && !HasPendingEvents()

  On WaitAny return:
    if (_inputInterruptPending):
      _inputInterruptPending = false
      SignalInputSemaphore()  // One signal, InputProcess drains all
```

### 4. Strategic Check Placement

**Problem**: 1000-bytecode periodic check may miss tight loops with low bytecode counts per iteration.

**Solution**: Add checks at strategic yield points:
1. **Periodic counter** (every 1000 bytecodes) - baseline
2. **Backward branch** (relative jumps to lower IP) - catches tight loops
3. **Method return** (send/return boundaries) - natural yield point

---

## Detailed Architecture

### Interrupt Flag Design

```csharp
// VM-local state
private volatile bool _inputInterruptPending;

// UI thread (producer)
public void EnqueueEvent(SmalltalkEvent evt)
{
    _eventQueue.Enqueue(evt);
    _inputInterruptPending = true;  // Edge-arm; idempotent

    // Preserve idle wake path
    if (_isIdle)
        _idleWaitHandle.Set();
}

// VM thread (periodic check)
private void CheckAndSignalPendingEvents()
{
    if (_platformBridge == null)
        return;

    // Edge-triggered: only signal once per pending batch
    if (_inputInterruptPending && _platformBridge.HasPendingEvents())
    {
        _inputInterruptPending = false;  // Clear BEFORE signal
        SignalInputSemaphoreIfRegistered();
    }
}
```

### Event Flow (Fixed)

```
OS Event → WinForms thread
    │
    ▼
SmalltalkEventQueue.Enqueue()
    │ sets _inputInterruptPending = true
    ▼
VM executing priority 4 process (popup menu tracking)
    │
    ▼
Periodic check: CheckAndSignalPendingEvents()
    │ _inputInterruptPending is true, HasPendingEvents() true
    │ clears flag, signals InputSemaphore ONCE
    ▼
InputProcess (priority 6) becomes runnable
    │ PREEMPTS priority 4 process
    ▼
InputProcess DRAINS ENTIRE QUEUE
    │ for each event: process, enqueue MouseEvent, signal ControllerSemaphore
    │ loop until queue empty
    ▼
InputProcess waits on InputSemaphore
    │ (flag will be re-armed by next enqueue)
    ▼
Priority 4 process resumes
    │ ControllerSemaphore has N excess signals (one per MouseEvent)
    ▼
Consumer drains MouseEventQueue
    │ each wait consumes one excess signal
    │ processes corresponding MouseEvent
    ▼
✅ No deadlock, no wasted wakes, no lost events
```

### Idle Path (Corrected)

```csharp
private void EnterIdleWait(SmalltalkOOP backgroundProcess)
{
    while (true)
    {
        // Check timers first
        CheckAndSignalExpiredTimer();

        // Exit if process switch occurred
        if (GetActiveProcess() != backgroundProcess)
            return;

        // Guard: don't park if interrupt pending
        if (_inputInterruptPending || _platformBridge.HasPendingEvents())
        {
            // Signal and continue (don't park with pending work)
            if (_inputInterruptPending)
            {
                _inputInterruptPending = false;
                SignalInputSemaphoreIfRegistered();
            }
            continue;
        }

        // Safe to park - no pending work
        int waitResult = WaitHandle.WaitAny(handles);

        if (waitResult == 0)  // Event handle
        {
            // Single wake for entire batch
            if (_inputInterruptPending)
            {
                _inputInterruptPending = false;
                SignalInputSemaphoreIfRegistered();
            }
        }
        // ... timer and shutdown handling
    }
}
```

---

## Semaphore Semantics

### InputSemaphore

| Property | Value |
|----------|-------|
| Type | Smalltalk Semaphore (counting) |
| Signaled by | VM (during idle OR periodic check) |
| Waited by | InputProcess (priority 6) |
| Semantics | "Event batch arrived, drain the queue" |
| Signaling policy | Edge-triggered via `_inputInterruptPending` flag |
| Expected count | Usually 0 or 1 (drain prevents accumulation) |

### ControllerSemaphore

| Property | Value |
|----------|-------|
| Type | Smalltalk Semaphore (counting) |
| Signaled by | InputProcess (one per MouseEvent) |
| Waited by | Controllers (priority 4) |
| Semantics | "N MouseEvents in queue, wake N times" |
| Consumer policy | Each wait consumes one signal, processes one event |
| Count invariant | Count == MouseEventQueue size |

### Consumer Protocol

Consumers MUST treat ControllerSemaphore count as a **wake budget**, not a single wake:

```smalltalk
Controller>>eventLoop
    [self isActive] whileTrue: [
        Sensor waitForControllerEvent.  "Consumes one signal"
        self processOneMouseEvent.      "Dequeues one event"
    ]
```

If consumer needs to drain multiple events per iteration:
```smalltalk
Controller>>eventLoop
    [self isActive] whileTrue: [
        Sensor waitForControllerEvent.  "Wake on first event"
        "Drain any additional events"
        [Sensor hasMouseEvent] whileTrue: [
            self processOneMouseEvent.
        ]
    ]
```

---

## Check Placement Strategy

### Recommended Check Points

| Check Point | When | Why |
|-------------|------|-----|
| Periodic counter | Every 1000 bytecodes | Baseline, catches most cases |
| Backward branch | Relative jump to lower IP | Catches tight loops immediately |
| Method return | Send/return boundary | Natural yield point, low overhead |

### Implementation

```csharp
// In bytecode execution:
case Bytecode.Jump:
    int offset = ...;
    if (offset < 0)  // Backward branch
        CheckAndSignalPendingEvents();
    _instructionPointer += offset;
    break;

case Bytecode.Return:
    CheckAndSignalPendingEvents();  // Before context switch
    // ... return handling
    break;
```

### Overhead Analysis

| Check point | Frequency | Cost per check | Total overhead |
|-------------|-----------|----------------|----------------|
| Periodic (1000 bc) | 10K/sec | ~3 cycles | ~0.003% CPU |
| Backward branch | Variable | ~3 cycles | ~0.01% CPU (loops) |
| Method return | Variable | ~3 cycles | ~0.01% CPU (message sends) |
| **Total worst case** | | | **~0.025% CPU** |

---

## Diagnostics and Validation

### Counters (for performance monitoring)

```csharp
// VM diagnostic counters
public class EventDiagnostics
{
    public long InputInterruptsSignaled;     // VM → InputSemaphore signals
    public long InputBatchesDrained;         // InputProcess drain cycles
    public long EventsProcessed;             // Total events through queue
    public long IdleParks;                   // Times VM entered idle
    public long IdleWakes;                   // Times WaitAny returned

    // Histogram: events per drain batch
    public int[] EventsPerBatchHistogram;    // [0]=0 events, [1]=1-5, [2]=6-20, [3]=21+

    // Anomaly detection
    public long EmptyDrainWakes;             // InputProcess woke but queue empty (bug indicator)
    public long MissedFlagSets;              // HasPendingEvents true but flag false (race indicator)
}
```

### Trace Hooks

```csharp
[Conditional("TRACE_EVENTS")]
private void TraceEventFlow(string event, params object[] args)
{
    var timestamp = Stopwatch.GetTimestamp();
    var priority = GetActiveProcessPriority();
    Console.WriteLine($"[{timestamp}] P{priority} {event}: {string.Join(", ", args)}");
}

// Usage:
TraceEventFlow("ENQUEUE", eventType, _inputInterruptPending);
TraceEventFlow("SIGNAL_INPUT", excessSignals);
TraceEventFlow("INPUT_WAKE", queueSize);
TraceEventFlow("INPUT_DRAIN_COMPLETE", eventsProcessed);
TraceEventFlow("CTRL_SIGNAL", mouseEventSeq);
```

### Assertions (debug builds)

```csharp
// No double wake loops
Debug.Assert(
    !(justSignaledInputSemaphore && queueWasEmpty),
    "InputProcess signaled but queue was empty - interrupt flag race?"
);

// Edge-trigger correctness
if (!_inputInterruptPending && _platformBridge.HasPendingEvents() && InputProcessIsBlocked())
{
    Debug.Fail("Events pending but flag not set and InputProcess blocked - missed edge");
    _inputInterruptPending = true;  // Self-heal
    SignalInputSemaphoreIfRegistered();
}

// Count invariant
Debug.Assert(
    ControllerSemaphoreCount == MouseEventQueueSize,
    $"Count mismatch: semaphore={ControllerSemaphoreCount}, queue={MouseEventQueueSize}"
);
```

---

## Test Plan

### 1. Popup Menu Nested Test

**Objective**: Verify nested event consumers work without deadlock

**Procedure**:
1. Open System Browser
2. Click yellow button to show popup menu
3. Move mouse rapidly over menu items
4. Click to select item
5. Repeat with nested menus

**Success criteria**:
- Menu appears immediately
- Selection tracks mouse smoothly
- Selection registers on mouse-up
- No hangs or deadlocks

**Metrics to capture**:
- Events-per-batch histogram
- InputProcess wake count
- ControllerSemaphore signal count

### 2. 0% Idle CPU Test

**Objective**: Verify true idle achieves 0% CPU

**Procedure**:
1. Start interpreter with System Browser
2. Leave idle (no mouse movement, no typing)
3. Monitor CPU usage for 60 seconds
4. Introduce single mouse move, return to idle
5. Monitor for additional 60 seconds

**Success criteria**:
- CPU usage < 0.1% during idle
- Single wake on mouse move
- Return to 0% after event processed

**Metrics to capture**:
- IdleParks count
- IdleWakes count
- Wake reasons distribution

### 3. Burst Stress Test

**Objective**: Verify correct handling of high-frequency events

**Procedure**:
1. Generate 1-2 kHz mouse moves programmatically
2. Run for 3 seconds
3. Stop and verify system state

**Success criteria**:
- No runaway signaling (InputInterruptsSignaled ≈ number of batches, not events)
- InputProcess drains batches fully (EventsProcessed == total events)
- ControllerSemaphore counts match (Count == MouseEvents processed)
- No deadlock or hang

**Metrics to capture**:
- Events per batch histogram
- Peak batch size
- InputInterruptsSignaled vs EventsProcessed ratio

### 4. Backward-Loop Latency Test

**Objective**: Verify responsive event delivery in tight loops

**Procedure**:
1. Execute tight bytecode loop: `[true] whileTrue: []`
2. Introduce mouse click event
3. Measure time from enqueue to InputProcess wake

**Success criteria**:
- Latency < 1ms with backward-branch check
- Latency < 10ms with only periodic check

**Metrics to capture**:
- Enqueue timestamp
- Signal timestamp
- InputProcess wake timestamp
- Total latency

### 5. Regression Tests

**Objective**: Verify no regressions in existing functionality

**Tests**:
- BlockContextTest returns 1191
- Timer/Delay mechanism works correctly
- Keyboard input works correctly
- Display updates work correctly

---

## Decision Checkpoints

### Checkpoint 1: Correctness

**Gate criteria**:
- [ ] Edge-trigger flag integrated
- [ ] InputProcess drains queue fully
- [ ] Idle path coalesces events
- [ ] Popup menu deadlock resolved
- [ ] All regression tests pass

### Checkpoint 2: Performance

**Gate criteria**:
- [ ] CPU remains ~0% when truly idle
- [ ] Signaling counts proportional to event batches (not individual events)
- [ ] No preemption storm under burst load
- [ ] Overhead < 0.1% CPU during active use

### Checkpoint 3: Robustness

**Gate criteria**:
- [ ] No lost transitions under click/move bursts
- [ ] Latency < 1ms in tight loops (with backward-branch check)
- [ ] Latency < 10ms worst case (periodic only)
- [ ] Stress test passes without anomalies

---

## Implementation Plan

### Phase 1: Core Infrastructure

1. Add `_inputInterruptPending` flag to VirtualMachine
2. Modify `SmalltalkEventQueue.Enqueue()` to set flag
3. Add `CheckAndSignalPendingEvents()` with edge-trigger logic
4. Call from periodic check block

**Files**: `VirtualMachine.cs`, `SmalltalkEventQueue.cs`
**Lines changed**: ~30

### Phase 2: Idle Path Correction

1. Add flag check before parking in `EnterIdleWait()`
2. Coalesce wake handling with flag guard
3. Remove `ForceResetEventSignal()` call (no-op, misleading)

**Files**: `VirtualMachine.cs`
**Lines changed**: ~15

### Phase 3: Strategic Check Points

1. Add check at backward branch in bytecode interpreter
2. Add check at method return
3. Make check frequency configurable

**Files**: `BytecodeInterpreter.cs` or `VirtualMachine.cs`
**Lines changed**: ~20

### Phase 4: Diagnostics

1. Add counter infrastructure
2. Add trace hooks (conditional compilation)
3. Add debug assertions

**Files**: New `EventDiagnostics.cs`, modifications to VM
**Lines changed**: ~100

### Phase 5: Smalltalk Verification

1. Verify InputProcess drains queue fully
2. Verify consumers use correct protocol
3. Fix any consumer code that assumes one-wake-one-event

**Files**: `Smalltalk-80.st`
**Lines changed**: ~20

---

## Comparison with Current Implementation

### Benefits

| Aspect | Current | Proposed |
|--------|---------|----------|
| Idle CPU | 0% ✅ | 0% ✅ (preserved) |
| Nested consumers | Deadlock ❌ | Work correctly ✅ |
| Event latency (active) | Infinite ❌ | <1ms ✅ |
| Missed events | Possible ❌ | Never ✅ |
| Wasted wakes | Possible ❌ | Minimized ✅ |
| Semaphore count inflation | Possible ❌ | Prevented ✅ |

### Trade-offs

| Trade-off | Impact | Mitigation |
|-----------|--------|------------|
| Additional flag | Minor complexity | Well-documented, single purpose |
| More check points | ~0.025% CPU | Negligible, configurable |
| Drain-all policy | Burst latency | Events processed in order |

---

## Appendix A: Concurrency Boundaries

### Thread Safety Invariants

| Thread | Can Access | Cannot Access |
|--------|------------|---------------|
| UI (WinForms) | `_eventQueue`, `_inputInterruptPending` flag, `_idleWaitHandle` | Smalltalk heap, Smalltalk semaphores |
| VM (Smalltalk) | Everything | (owns all state) |

### Atomic Operations

| Operation | Atomicity | Notes |
|-----------|-----------|-------|
| `_inputInterruptPending = true` | Volatile write | UI thread only sets true |
| `_inputInterruptPending = false` | Volatile write | VM thread only sets false |
| `_eventQueue.Enqueue()` | Thread-safe | ConcurrentQueue |
| `_eventQueue.TryDequeue()` | Thread-safe | ConcurrentQueue |
| `SignalSemaphore()` | VM thread only | No cross-thread access |

### Race Condition Analysis

**Scenario**: UI thread enqueues while VM thread checks flag

```
UI thread:              VM thread:
_queue.Enqueue(e)       if (_flag && HasPending())
_flag = true               _flag = false
                           Signal()
```

**Result**: Safe. Either:
1. VM sees flag=true before UI sets it → misses this batch → UI sets flag → next check catches it
2. VM sees flag=true after UI sets it → processes batch correctly
3. Flag set racing with clear → worst case: extra wake on next check (harmless)

**Self-healing assertion** catches case where events pile up with flag=false.

---

## Appendix B: Blue Book Reference

### Hardware Interrupt Handling (Chapter 29)

> "The Smalltalk-80 virtual machine is specified to signal Semaphores on three conditions:
> - user event: a key has been pressed on the keyboard, a button has been pressed on the pointing device, or the pointing device has moved.
> - timeout: a specific value of the millisecond clock has been reached.
> - low space: available object memory has fallen below certain limits."

The Blue Book implies interrupts are checked between bytecodes. Our software interrupt simulation with edge-triggered signaling matches this design while avoiding the overhead of checking every bytecode.

### Process Priority (Chapter 15)

> "In order to provide more control of when a Process will run, Processor uses a very simple priority mechanism."

InputProcess at priority 6 naturally preempts controllers at priority 4 when signaled. The edge-triggered flag ensures this preemption happens once per event batch, not once per event or once per check.

---

## Conclusion

This refined design addresses all identified gaps:

1. ✅ **Edge-triggering**: `_inputInterruptPending` flag ensures idempotent signaling
2. ✅ **No count inflation**: One signal per batch, InputProcess drains fully
3. ✅ **Storm control**: Drain-all policy prevents preemption cascades
4. ✅ **Idle correctness**: Flag guard prevents bounce on burst events
5. ✅ **Latency control**: Strategic check points catch tight loops
6. ✅ **Count invariant**: ControllerSemaphore count == MouseEventQueue size
7. ✅ **Diagnostics**: Counters, traces, and assertions for validation

**Recommendation**: Proceed with implementation following the phased plan, with checkpoint gates ensuring correctness before performance optimization.
