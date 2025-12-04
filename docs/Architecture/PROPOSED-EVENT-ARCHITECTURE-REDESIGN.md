# Proposed Event Architecture Redesign

**Date**: 2025-12-04
**Status**: PROPOSAL (v8 - streamlined specification)
**Authors**: Collaborative analysis by multiple expert agents
**Implementation**: Clean-slate refactor (~840 lines, 10 phases)
**Appendices**: A (Concurrency), B (Blue Book), C (Rollout Notes), D (Timeline), E (Diagrams)

---

## Executive Summary

The current event handling system in our Smalltalk-80 virtual machine suffers from a critical flaw: input events are only signaled when the VM is idle. This design works for simple cases but fails whenever nested consumers—such as popup menus—need to block and wait for input while another controller is active. The result is a priority inversion: the InputProcess never wakes, events accumulate, and the waiting controller deadlocks.

This proposal introduces a clean-slate redesign based on **software interrupt simulation**, modeled after the Blue Book's hardware interrupt semantics. The key innovation is an **edge-triggered, idempotent signaling** mechanism that ensures:

- Input events are delivered promptly even when the VM is busy
- The InputProcess can always preempt lower-priority tasks to drain the queue
- Each wake cycle processes all pending events, eliminating missed transitions
- Idle periods remain at true 0% CPU, preserving efficiency on modern systems

The redesign establishes formal invariants for correctness (no lost events, no spurious wakes, preserved down/up transitions, fairness across input classes) and provides a phased rollout plan. Early phases deliver the critical fix for deadlocks and idle CPU usage; later phases add fairness, diagnostics, and OS integration edge-case handling.

By aligning the VM's input architecture with canonical interrupt semantics, this proposal restores responsiveness, eliminates deadlocks, and creates a maintainable foundation for future extensions.

---

## Core Guarantees for Nested Blocking Loops

The design must let Smalltalk processes synchronously await controller input without burning CPU, while ensuring the InputProcess can always preempt, drain, and deliver events.

| Guarantee | Description |
|-----------|-------------|
| **Preemption** | InputProcess (priority 6) must be able to preempt any active controller process (priority 4) when input arrives |
| **Deterministic delivery** | Every enqueued input event is translated into exactly one Smalltalk-level event and signaled to controllers without loss or duplication |
| **Blocking semantics** | Smalltalk code using `waitForControllerEvent` can block on a Semaphore and wake when relevant events exist, without busy-waiting |
| **No spurious wake storms** | InputSemaphore is edge-triggered; one signal wakes the InputProcess to drain the entire event batch, then it re-arms |
| **Nest-safe consumption** | Nested loops (popup inside another interaction) see the same consistent event stream and can block without starving the InputProcess |

---

## Formal Invariants

These are the "canonical truths" that maintainers must rely on. Check them in debug builds.

### I1: Edge Signaling Invariant

For any interval where the producer queue transitions from empty→non-empty at time t, the VM will signal InputSemaphore at least once in (t, t + Δ], with Δ bounded by the largest check latency.

**Enforced by**: Periodic check, backward branch check, method return check

### I2: Drain Invariant

Each InputSemaphore wake is followed by a complete drain of the producer queue before the InputProcess waits again.

**Enforced by**: InputProcess loop structure in `InputState>>run`

### I3: Counting Invariant

ControllerSemaphore.count equals the number of controller events available to consumers; this holds across all nested blocking consumers.

**Enforced by**: InputProcess signals exactly once per translated event; consumers wait once per event consumed

### I4: No-Spurious-Wake Invariant

The number of times InputProcess wakes to find zero events is ≤ ε per minute under normal operation (ε small, e.g., 1).

**Enforced by**: Edge-triggered flag prevents re-signaling during drain

### I5: Idle Invariant

When the active process is the background process and there are no pending events or timers, CPU usage is ~0%; the VM remains parked until the next interrupt.

**Enforced by**: `WaitHandle.WaitAny` in idle path with proper flag guards

### I6: Down/Up Preservation Invariant

Every button-down has exactly one subsequent button-up, in order. Discrete transitions (button down/up, key down/up) are never coalesced or reordered.

**Enforced by**: Only move events are coalesced; transitions preserved exactly in queue order

### I7: Fairness Invariant

Keyboard events are processed within F milliseconds even under pointer move bursts. No input class starves another.

**Enforced by**: Event class ordering in drain (discrete transitions first, then moves)

---

## Design Principles

This section groups the design decisions into thematic categories. For canonical code implementations, see **Detailed Architecture** below.

---

### Concurrency

#### Edge-Triggered Signaling

Level-triggered signaling (re-signaling while events are pending) causes unnecessary wakeups and count inflation. Instead, use an **edge-triggered** approach:

- UI thread atomically arms `_inputInterruptPendingInt = 1` on enqueue
- VM thread atomically clears and checks: if previous value was 1, signal once
- `Interlocked.Exchange` guarantees exactly one signal per event batch

#### Runnable/Draining Latch

To prevent re-signaling while InputProcess is active, track `_inputProcessRunnableOrDraining`:

- Set `true` immediately before signaling InputSemaphore
- Cleared by InputProcess via `primInputDrainComplete` at end of drain
- Also cleared by scheduler hook when switching away from a blocked InputProcess

#### Scheduler Context Switch Hook

If the active process changes before InputProcess runs, the latch could become stale. Clear it on any scheduler switch away from InputProcess when InputProcess is blocked.

```csharp
// In TransferTo() or process switch logic:
private void OnProcessSwitch(SmalltalkOOP fromProcess, SmalltalkOOP toProcess)
{
    // If switching away from InputProcess and it's now blocked, clear latch
    if (fromProcess == _inputProcessOOP && InputProcessIsBlocked())
    {
        _inputProcessRunnableOrDraining = false;
    }
}
```

---

### Drain Invariant

InputProcess drains the **entire queue** per wake, not one event at a time:

1. Dequeue all pending OS events
2. Translate each to Smalltalk Mouse/Keyboard events
3. Signal ControllerSemaphore once per translated event
4. Call `primInputDrainComplete` to clear latch and re-arm for next batch

This enforces **I2** (Drain Invariant) and prevents semaphore count inflation.

---

### Idle Path

The idle path must coalesce burst events into a single wake:

- Park only when: `ActiveProcessPriority == 1 && !HasPendingEvents() && flag == 0`
- Use `ManualResetEventSlim` with explicit `Reset()` after wake
- Apply the same edge-triggered guard: one signal per batch
- Self-healing: if events pending but flag is 0, re-arm and signal

See **Detailed Architecture > Idle Path** for the hardened implementation.

---

### Strategic Check Placement

The 1000-bytecode periodic check may miss tight loops. Add checks at:

| Check Point | Purpose |
|-------------|---------|
| Periodic counter (every 1000 bytecodes) | Baseline coverage |
| Backward branch (offset < 0) | Catches tight loops immediately |
| Method return | Natural yield point |
| After non-trivial primitives | Optional, for display-heavy code |

Each check site is gated by a configuration flag for tuning.

---

### Event Batching, Coalescing, and Fairness

#### Move Coalescing

High-frequency mouse moves can flood the queue. Coalesce consecutive `#move` events by updating the previous move's position instead of enqueueing a new item. **Never coalesce across button transitions.**

#### Keyboard Fairness

Use separate logical queues for keyboard and pointer events, but one ControllerSemaphore. The drain prioritizes:
1. Discrete transitions (down/up)
2. Wheel/scroll events
3. Pointer moves (coalesced)

This enforces **I7** (Fairness Invariant).

---

### Timer and Input Ordering

Input signaling occurs **before** timer signaling in each check cycle. Precedence order:

1. **Input** (highest urgency for UI responsiveness)
2. **Timer**
3. **Low-space** (if implemented)

---

### GC and Interrupt Masking

GC and long primitives must not be interrupted mid-operation. Use `_interruptsMaskedCounter`:

```csharp
private int _interruptsMaskedCounter;

public void MaskInterrupts() => _interruptsMaskedCounter++;

public void UnmaskInterrupts()
{
    if (--_interruptsMaskedCounter == 0)
    {
        // Consolidated signal for any events that arrived while masked
        if (_inputInterruptPendingInt == 1)
            CheckAndSignalPendingEvents();
    }
}

// Usage in GC:
public void CollectGarbage()
{
    MaskInterrupts();
    try { /* ... compaction ... */ }
    finally { UnmaskInterrupts(); }
}
```

This prevents InputProcess wake during heap compaction or other critical sections.

---

### Event Semantics Consistency

| Semantic | Rule |
|----------|------|
| Down/up transitions | Never coalesced; preserved exactly (I6) |
| Double-click timing | Per-button timing record; `event.timestamp` for threshold |
| Keyboard autorepeat | Never coalesced; preserved as discrete events |
| Modifier state | Each event carries a snapshot; no reliance on global state |

---

### OS Integration Edge Cases

| Scenario | Resolution |
|----------|------------|
| Focus/capture loss | Enqueue synthetic "cancel" event; popup menus exit cleanly |
| System suspend/resume | Re-check `HasPendingEvents()`, re-arm flag, force single signal, clear stale diagnostics |
| Backpressure overflow | Soft cap (default 100 moves); aggressive coalescing, log `DroppedMoves` metric |

---

## Detailed Architecture

### Interrupt Flag Design (Atomic)

```csharp
// VM-local state - use int for Interlocked operations
private int _inputInterruptPendingInt;  // 0 or 1
private bool _inputProcessRunnableOrDraining;
private readonly ManualResetEventSlim _idleWake = new ManualResetEventSlim(false);

// UI thread (producer)
public void EnqueueEvent(SmalltalkEvent evt)
{
    _eventQueue.Enqueue(evt);
    Interlocked.Exchange(ref _inputInterruptPendingInt, 1);  // Edge-arm; atomic

    // Preserve idle wake path
    if (_isIdle)
        _idleWake.Set();
}

// VM thread (periodic check)
private void CheckAndSignalPendingEvents()
{
    if (_platformBridge == null || !_platformBridge.HasPendingEvents())
        return;

    // Edge-triggered with "already runnable" guard
    if (Interlocked.Exchange(ref _inputInterruptPendingInt, 0) == 1
        && !_inputProcessRunnableOrDraining)
    {
        _inputProcessRunnableOrDraining = true;  // Latch until drain completes
        SignalInputSemaphoreIfRegistered();
        _diagnostics.InputInterruptsSignaled++;
    }
}

// Called by InputProcess via primitive when drain completes
public void InputDrainComplete()
{
    _inputProcessRunnableOrDraining = false;
    _diagnostics.InputBatchesDrained++;
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

### Idle Path (Hardened with ManualResetEventSlim)

```csharp
private void EnterIdleWait(SmalltalkOOP backgroundProcess)
{
    while (true)
    {
        // Check timers first (input before timer per ordering rule)
        CheckAndSignalPendingEvents();
        CheckAndSignalExpiredTimer();

        // Exit if process switch occurred
        if (GetActiveProcess() != backgroundProcess)
            return;

        // Guard: don't park if events pending
        // Use HasPendingEvents() as truth source, atomic flag prevents multiple signals
        if (_platformBridge.HasPendingEvents())
        {
            // Self-heal: if flag is 0 but events pending, re-arm and signal
            if (Interlocked.Exchange(ref _inputInterruptPendingInt, 0) == 1
                && !_inputProcessRunnableOrDraining)
            {
                _inputProcessRunnableOrDraining = true;
                SignalInputSemaphoreIfRegistered();
            }
            continue;
        }

        // Safe to park - no pending work
        _isIdle = true;
        _idleWake.Wait(ShutdownToken);  // Blocks here (0% CPU)
        _idleWake.Reset();  // Explicit reset for ManualResetEventSlim
        _isIdle = false;

        // Single wake for entire batch
        if (_platformBridge.HasPendingEvents() &&
            Interlocked.Exchange(ref _inputInterruptPendingInt, 0) == 1 &&
            !_inputProcessRunnableOrDraining)
        {
            _inputProcessRunnableOrDraining = true;
            SignalInputSemaphoreIfRegistered();
        }

        // Timer handling continues in loop...
    }
}
```

**Key changes**:
- Uses `ManualResetEventSlim` with explicit `Reset()` after wake
- Atomic flag operations prevent lost edges and double-clears
- `HasPendingEvents()` is the truth source; flag prevents re-signaling
- Self-healing guard catches edge cases in release builds

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

Consumers MUST treat ControllerSemaphore count as a **wake budget**, not a single wake.

#### Two Canonical Consumer Patterns

**Gap**: Mixing patterns without a contract can cause subtle bugs.

**Pattern 1: Wake-once, process-one** (for state machines that require stepwise transitions):
```smalltalk
Controller>>eventLoop
    [self isActive] whileTrue: [
        Sensor waitForControllerEvent.  "Consumes one signal"
        self processOneMouseEvent.      "Dequeues one event"
    ]
```

**Pattern 2: Wake-once, drain-many** (for trackers/menus):
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

**Contract**: Consumers must not assume a wake delivers exactly one event; they must confirm queue state explicitly.

**Sanctioned APIs** (use only these):
- `Sensor hasMouseEvent` / `Sensor nextMouseEvent`
- `Sensor hasKeyboardEvent` / `Sensor nextKeyboardEvent`
- `Sensor waitForControllerEvent`

**Forbidden**: Direct access to `InputState mouseEventQueue` or `InputState keyboardEventQueue`. All event consumption must go through the sanctioned APIs to maintain count invariant (I3).

#### Popup Menu State Machine Guards

**Gap**: On yellow-button up, ensure selection resolution is atomic relative to concurrent mouse moves queued after the up.

**Fix**: In the drain, process events in arrival order and resolve selection immediately upon yellow-up; subsequent moves in the same batch must not override selection. Document this in the popup's state machine.

---

## Smalltalk-Level Patterns for Blocking Loops

This section documents canonical patterns for Smalltalk code that needs to block and wait for input events without burning CPU.

### Popup Menu Nested Loop (Canonical Pattern)

The popup menu is the primary example of a nested blocking input loop. The menu appears while another controller is active, and must track mouse movement and detect button release.

**Loop structure**:
1. **Block**: `waitForControllerEvent` (which internally waits on ControllerSemaphore)
2. **Drain**: Fetch all available controller events (mouse move, button up/down)
3. **State update**: Track menu selection highlight and "armed" states
4. **Exit condition**: On yellow-button up within a highlighted item, exit with selection; otherwise continue to block

**No polling**: Never spin-check `anyButtonPressed` without a blocking wait.

```smalltalk
PopUpMenu>>startUp: aSymbol
    "Display and make a selection from the receiver as long as the button
     denoted by the symbol, aSymbol, is pressed. Answer the current selection.
     Uses event-driven blocking (0% idle CPU) instead of busy-wait polling."

    self displayAt: Sensor cursorPoint
        during: [
            "Wait for initial button press"
            [self buttonPressed: aSymbol] whileFalse: [
                Sensor waitForControllerEvent].
            "Track while button held"
            [self buttonPressed: aSymbol] whileTrue: [
                self manageMarker.
                Sensor waitForControllerEvent]].
    ^selection
```

### Other Nested Interactions (Dialogs, Drag Loops, Selection Trackers)

Use the same pattern:
1. Block on `waitForControllerEvent`
2. Drain and interpret events into the local state machine
3. Update visual feedback
4. Check exit conditions

```smalltalk
"Drag loop example"
Controller>>trackDrag
    | ev |
    [true] whileTrue: [
        Sensor waitForControllerEvent.
        ev := Sensor nextMouseEvent.
        ev isNil ifTrue: [^self].
        ev kind = #move ifTrue: [self updateDragTo: ev position].
        (ev kind = #up and: [ev button = #yellow]) ifTrue: [
            ^self finalizeDrag]]

"Wait for button release"
InputSensor>>waitNoButton
    "Wait for the user to release any mouse button.
     Uses event-driven blocking (0% CPU) instead of busy-wait polling."
    [self anyButtonPressed] whileTrue: [self waitForControllerEvent].
    ^self cursorPoint

"Wait for button press"
InputSensor>>waitButton
    "Wait for the user to press any mouse button.
     Uses event-driven blocking (0% CPU) instead of busy-wait polling."
    [self anyButtonPressed] whileFalse: [self waitForControllerEvent].
    ^self cursorPoint
```

### Avoiding Dual Waits

If your loop sometimes waits on additional semaphores (e.g., timer), multiplex through a single controller event demultiplexer, or use separate waits with clear priority assumptions. The InputProcess must remain the sole producer of controller signals for input.

### Explicit Event Drain APIs

Consumers choose one of the two canonical patterns and use the proper API; forbid direct queue peeks outside these:

```smalltalk
"Keyboard events"
Sensor>>hasKeyboardEvent
    ^InputState keyboardEventQueue isEmpty not

Sensor>>nextKeyboardEvent
    ^InputState keyboardEventQueue isEmpty
        ifTrue: [nil]
        ifFalse: [InputState keyboardEventQueue next]

"Pointer events"
Sensor>>hasPointerEvent
    ^InputState mouseEventQueue isEmpty not

Sensor>>nextPointerEvent
    ^InputState mouseEventQueue isEmpty
        ifTrue: [nil]
        ifFalse: [InputState mouseEventQueue next]
```

### Popup Cancel and Escape

Popup menus should treat ESC key or loss of focus as cancel:

```smalltalk
PopUpMenu>>startUp: aSymbol
    "...existing setup..."
    self displayAt: Sensor cursorPoint
        during: [
            [self buttonPressed: aSymbol] whileFalse: [
                Sensor waitForControllerEvent.
                self checkForCancel ifTrue: [^0]].  "Cancel returns 0"
            [self buttonPressed: aSymbol] whileTrue: [
                self manageMarker.
                Sensor waitForControllerEvent.
                self checkForCancel ifTrue: [^0]]].
    ^selection

PopUpMenu>>checkForCancel
    "ESC key or focus loss cancels the menu"
    Sensor hasKeyboardEvent ifTrue: [
        | ev |
        ev := Sensor nextKeyboardEvent.
        ev keyCode = 27 "ESC" ifTrue: [^true]].
    Sensor focusLost ifTrue: [^true].
    ^false
```

### Drag Threshold and Hover

Standardize movement threshold before starting drags:

```smalltalk
Controller>>trackPotentialDrag
    | startPoint |
    startPoint := Sensor cursorPoint.
    [Sensor anyButtonPressed] whileTrue: [
        Sensor waitForControllerEvent.
        (Sensor cursorPoint - startPoint) r > DragThreshold ifTrue: [
            ^self startDragFrom: startPoint]].
    ^self  "Released before threshold - not a drag"
```

**Hover events**: Derived from move + timer, not a busy loop. Timer fairness rule ensures hover timers don't starve input.

---

## Reentrancy and Priority Safety

### Priority Discipline

| Process | Priority | Role |
|---------|----------|------|
| InputProcess | 6 (lowIOPriority) | Sole producer of controller signals for input |
| Controllers | 4 (userSchedulingPriority) | Consumers of mouse/keyboard events |
| Background | 1 (systemRockBottomPriority) | Idle sentinel |

**Rules**:
- Only InputProcess runs at priority 6; controller code runs at priority 4 or below
- Any process waiting on ControllerSemaphore must have priority ≤6 (typically 4)
- Transient helper processes spawned by controllers must stay ≤4
- Debug builds assert priority violations on ControllerSemaphore wait

This ensures preemption works and prevents nested controllers from starving each other.

### No Heap Access from UI Thread

The UI thread enqueues raw events and arms the flag; it **never** touches Smalltalk heap or semaphores.

| Thread | Can Access | Cannot Access |
|--------|------------|---------------|
| UI (WinForms) | `_eventQueue`, `_inputInterruptPending` flag, `_idleWaitHandle` | Smalltalk heap, Smalltalk semaphores |
| VM (Smalltalk) | Everything | (owns all state) |

### Idempotent Signaling

Because InputSemaphore is used as an interrupt gate, treat it as **"wake to drain"** rather than "one signal per event." The ControllerSemaphore remains strictly **one signal per event** to preserve counting semantics.

| Semaphore | Signaling Semantics |
|-----------|---------------------|
| InputSemaphore | Edge-triggered gate: "wake to drain entire batch" |
| ControllerSemaphore | Counting: "one signal per translated event" |

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

Diagnostics protect blocking semantics and help identify protocol violations. Split into **Counters** for monitoring and **Guards** for failure recovery.

---

### Counters & Metrics

```csharp
public class EventDiagnostics
{
    // Core flow (for verifying I1-I3)
    public long InputInterruptsSignaled;      // Edge-trigger signals to InputSemaphore
    public long InputBatchesDrained;          // Full drains by InputProcess
    public long ControllerSignalsEmitted;     // Signals to ControllerSemaphore
    public long EventsProcessed;              // Total events through queue

    // Idle path (for verifying I5)
    public long IdleParks;                    // Times VM entered idle
    public long IdleWakes;                    // Times WaitAny returned

    // Anomaly detection (for verifying I4)
    public long EmptyDrainWakes;              // InputProcess woke but queue empty
    public long MissedFlagSets;               // HasPendingEvents true but flag false

    // Latency (for SLA monitoring)
    public int[] LatencyHistogram;            // Buckets: <1ms, 1-5ms, 5-20ms, >20ms
    public long MaxEnqueueToWakeLatency;      // Peak observed latency

    // Button balance (for verifying I6)
    public long[] ButtonDowns;                // Per-button down counts
    public long[] ButtonUps;                  // Per-button up counts

    // Queue metrics
    public int PeakMouseQueueSize;            // High water mark
    public long CoalescedMoves;               // Coalesced move count
    public long DroppedMoves;                 // Dropped under backpressure

    // Storm detection
    public double SignalToDrainRatio =>
        InputBatchesDrained == 0 ? 0 : (double)InputInterruptsSignaled / InputBatchesDrained;
    public bool StormDetected => SignalToDrainRatio > 1.5 && InputBatchesDrained > 10;
}
```

### Trace Hooks

Use `[Conditional("TRACE_EVENTS")]` to emit flow traces:

| Event | Parameters | Purpose |
|-------|------------|---------|
| `ENQUEUE` | eventType, flagState | Track producer activity |
| `SIGNAL_INPUT` | — | Edge-trigger fired |
| `INPUT_WAKE` | queueSize | InputProcess gained CPU |
| `INPUT_DRAIN_COMPLETE` | eventsProcessed | Drain finished |
| `CTRL_SIGNAL` | eventSeq | ControllerSemaphore signaled |
| `CTRL_BLOCK` | processId | Controller waiting |
| `CTRL_WAKE` | processId, eventsPending | Controller resumed |

---

### Guards & Recovery

#### Debug Assertions

```csharp
// I4: No spurious wakes
Debug.Assert(!(justSignaledInputSemaphore && queueWasEmpty),
    "InputProcess signaled but queue was empty");

// I3: Count invariant
Debug.Assert(ControllerSemaphoreCount == MouseEventQueueSize,
    $"Count mismatch: semaphore={ControllerSemaphoreCount}, queue={MouseEventQueueSize}");
```

#### Self-Healing Guards (Release Builds)

| Guard | Trigger | Action |
|-------|---------|--------|
| Missed edge | `HasPendingEvents() && flag==0 && InputProcess blocked` | Re-arm flag, signal once |
| Spurious wakes | `EmptyDrainWakes > threshold/min` | Disable backward-branch check |
| Storm detected | `SignalToDrainRatio > 1.5` | Log anomaly, reduce check frequency |
| Latency SLA breach | `>20ms latency repeated` | Dump recent trace, log alarm |

```csharp
// Latency SLA monitoring
private void CheckLatencySLA(long latencyTicks)
{
    var latencyMs = latencyTicks * 1000.0 / Stopwatch.Frequency;

    // Update histogram bucket
    int bucket = latencyMs < 1 ? 0 : latencyMs < 5 ? 1 : latencyMs < 20 ? 2 : 3;
    _diagnostics.LatencyHistogram[bucket]++;

    // Alarm if >20ms latency occurs repeatedly
    if (bucket == 3 && _diagnostics.LatencyHistogram[3] > 10)
    {
        Console.WriteLine($"[LATENCY ALARM] {latencyMs:F1}ms");
        DumpRecentEventTrace();
    }
}
```

Guards degrade gracefully rather than crashing, dumping diagnostic snapshots for postmortem.

### Proof Obligations

Each invariant ties to specific diagnostics:

| Invariant | Proof Mechanism |
|-----------|-----------------|
| I1 (Edge signaling) | `LatencyHistogram`: max latency ≤ Δ for all buckets |
| I2 (Drain invariant) | `InputInterruptsSignaled == InputBatchesDrained` over time |
| I3 (Counting invariant) | At drain end: `ControllerSemaphore.count == MouseEventQueue.size` |
| I4 (No spurious wake) | `EmptyDrainWakes` ≤ ε/minute |
| I5 (Idle) | `IdleParks` durations show near-0% CPU |
| I6 (Down/up preservation) | `ButtonDowns[i] - ButtonUps[i] ∈ {0, 1}` for all buttons |
| I7 (Fairness) | Keyboard latency histogram independent of pointer load |

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

### 6. Focus/Capture Loss During Popup

**Objective**: Verify popup menus handle focus loss gracefully

**Procedure**:
1. Open popup menu with yellow button
2. Simulate losing focus (click outside window, Alt-Tab)
3. Verify popup cancels gracefully

**Success criteria**:
- Popup menu closes with selection = 0 (cancel)
- No deadlocks or hung processes
- Controller resumes normal operation

### 7. Mixed Input Burst Test

**Objective**: Verify keyboard events are not delayed behind large move batches

**Procedure**:
1. Interleave rapid mouse moves with keypresses (1kHz moves, 10Hz keys)
2. Verify keyboard events are processed promptly

**Success criteria**:
- Keyboard events processed within F milliseconds (I7 fairness invariant)
- No keyboard event starvation
- Event ordering preserved within each class

### 8. Suspend/Resume Test

**Objective**: Verify correct behavior after system suspend/resume

**Procedure**:
1. Start interpreter with popup menu
2. Trigger programmatic suspend
3. Resume system
4. Verify first event after resume is handled correctly

**Success criteria**:
- Edge flag is re-armed if events pending
- Single signal post-resume
- No missed first event

### 9. GC Under Load Test

**Objective**: Verify interrupt masking during GC prevents reentrancy violations

**Procedure**:
1. Generate 1-2 kHz mouse moves
2. Trigger GC during burst
3. Verify single consolidated input wake post-GC

**Success criteria**:
- No InputProcess wake during GC critical section
- Single signal after GC completes
- No reentrancy violations

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

This is a **clean-slate refactor** of the event architecture. No backward compatibility with the current buggy implementation is required. The goal is to implement the complete design as specified in this document.

---

### Phase 1: Core VM Infrastructure

**Goal**: Establish atomic edge-triggered interrupt flag and runnable guard

#### Step 1.1: Add Atomic Interrupt Flag
- [ ] Add `private int _inputInterruptPendingInt` field to `VirtualMachine.cs` (use int for Interlocked operations)
- [ ] Add `private bool _inputProcessRunnableOrDraining` latch field
- [ ] Initialize both fields to 0/false in VM constructor

#### Step 1.2: Update Event Queue Enqueue
- [ ] In `SmalltalkEventQueue.Enqueue()`, add `Interlocked.Exchange(ref vm._inputInterruptPendingInt, 1)` after queue add
- [ ] Pass VM reference to SmalltalkEventQueue constructor (or use callback/delegate pattern)
- [ ] Verify thread safety: UI thread only writes, VM thread only clears

#### Step 1.3: Implement CheckAndSignalPendingEvents
- [ ] Create `CheckAndSignalPendingEvents()` method in VirtualMachine:
  ```csharp
  private void CheckAndSignalPendingEvents()
  {
      if (_platformBridge == null || !_platformBridge.HasPendingEvents())
          return;
      if (Interlocked.Exchange(ref _inputInterruptPendingInt, 0) == 1
          && !_inputProcessRunnableOrDraining)
      {
          _inputProcessRunnableOrDraining = true;
          SignalInputSemaphoreIfRegistered();
          _diagnostics.InputInterruptsSignaled++;
      }
  }
  ```
- [ ] Call from periodic check in interpreter loop (every 1000 bytecodes)

#### Step 1.4: Implement InputDrainComplete Primitive
- [ ] Add new primitive (e.g., primitive 199) for `InputDrainComplete`
- [ ] Implementation: `_inputProcessRunnableOrDraining = false`
- [ ] Increment `_diagnostics.InputBatchesDrained`
- [ ] Register in InputPrimitives.cs

**Files**: `VirtualMachine.cs`, `SmalltalkEventQueue.cs`, `InputPrimitives.cs`
**Estimated lines**: ~60

---

### Phase 2: Idle Path Hardening

**Goal**: Fix idle wait to use ManualResetEventSlim with proper guards

#### Step 2.1: Replace AutoResetEvent
- [ ] Replace any `AutoResetEvent` for idle wake with `ManualResetEventSlim`
- [ ] Rename to `_idleWake` for clarity
- [ ] Add `_isIdle` volatile bool to track idle state

#### Step 2.2: Rewrite EnterIdleWait Loop
- [ ] Implement the hardened idle loop from "Idle Path (Hardened with ManualResetEventSlim)" section:
  - Check pending events before parking
  - Set `_isIdle = true` before Wait
  - Call `_idleWake.Reset()` explicitly after wake
  - Set `_isIdle = false` after wake
  - Apply edge-triggered signal with runnable guard
- [ ] Add self-healing guard: if `HasPendingEvents()` but flag is 0 and InputProcess blocked, re-arm

#### Step 2.3: Update UI Thread Enqueue
- [ ] In `EnqueueEvent()`, add: `if (_isIdle) _idleWake.Set()` after arming flag
- [ ] Ensure Set() is called regardless of flag state (idle wake is separate from interrupt flag)

#### Step 2.4: Remove Legacy Code
- [ ] Remove `ForceResetEventSignal()` if present (no-op/misleading)
- [ ] Remove any old polling/busy-wait logic in idle path
- [ ] Remove any duplicate signaling paths

**Files**: `VirtualMachine.cs`, `SmalltalkEventQueue.cs`
**Estimated lines**: ~50

---

### Phase 3: Strategic Check Points

**Goal**: Add event checks at backward branches and method returns for tight loop responsiveness

#### Step 3.1: Backward Branch Check
- [ ] In bytecode dispatch for Jump bytecodes, detect backward branch (offset < 0)
- [ ] Call `CheckAndSignalPendingEvents()` on backward branch
- [ ] Gate with `EventConfig.EnableBackwardBranchCheck` flag

#### Step 3.2: Method Return Check
- [ ] In `HandleReturn()` or equivalent, call `CheckAndSignalPendingEvents()`
- [ ] Call before context switch operations
- [ ] Gate with `EventConfig.EnableMethodReturnCheck` flag

#### Step 3.3: Primitive Return Check (Optional)
- [ ] After non-trivial primitives (display operations, heavy computation), optionally check
- [ ] Gate with `EventConfig.EnablePrimitiveReturnCheck` flag (default false)

#### Step 3.4: Configuration Infrastructure
- [ ] Create `EventConfig` static class with:
  - `PeriodicCheckInterval` (default 1000)
  - `EnableBackwardBranchCheck` (default true)
  - `EnableMethodReturnCheck` (default true)
  - `EnablePrimitiveReturnCheck` (default false)
- [ ] Add `ApplyProfile()` method for Conservative/Responsive/Stress profiles

**Files**: `VirtualMachine.cs`, new `EventConfig.cs`
**Estimated lines**: ~80

---

### Phase 4: Scheduler Context Switch Hook

**Goal**: Clear runnable latch when process switches away from InputProcess

#### Step 4.1: Identify InputProcess OOP
- [ ] Store InputProcess OOP reference when it's created/registered
- [ ] Add `private SmalltalkOOP _inputProcessOOP` field

#### Step 4.2: Add Context Switch Hook
- [ ] In `TransferTo()` or process switch logic, detect switch away from InputProcess
- [ ] When switching FROM InputProcess TO another process:
  - If InputProcess is now blocked, call `InputDrainComplete()` to clear latch
- [ ] Prevent stale latch from blocking future signals

#### Step 4.3: Test Latch Behavior
- [ ] Verify: after InputProcess drains and waits, latch is false
- [ ] Verify: if InputProcess preempted mid-drain, latch stays true until explicit clear
- [ ] Verify: context switch clears stale latches

**Files**: `VirtualMachine.cs`, `VirtualMachine.ProcessScheduling.cs`
**Estimated lines**: ~30

---

### Phase 5: Interrupt Masking

**Goal**: Prevent signaling during GC and long primitives

#### Step 5.1: Add Interrupt Mask Counter
- [ ] Add `private int _interruptsMaskedCounter` field to VM
- [ ] Add `MaskInterrupts()`: increments counter
- [ ] Add `UnmaskInterrupts()`: decrements counter; if 0 and flag armed, consolidate signal

#### Step 5.2: Wrap GC with Masking
- [ ] Call `MaskInterrupts()` before GC compaction/critical section
- [ ] Call `UnmaskInterrupts()` after GC completes
- [ ] Ensure no InputProcess wake during heap locks

#### Step 5.3: Wrap Long Primitives (Optional)
- [ ] Identify primitives that hold heap locks (file I/O, display operations)
- [ ] Wrap with `MaskInterrupts()`/`UnmaskInterrupts()` if needed
- [ ] Document which primitives are masked

**Files**: `VirtualMachine.cs`, `HeapManager.cs`, potentially primitive files
**Estimated lines**: ~40

---

### Phase 6: Diagnostics Infrastructure

**Goal**: Add comprehensive monitoring and debugging support

#### Step 6.1: Create EventDiagnostics Class
- [ ] Create new `EventDiagnostics.cs` with counters:
  - `InputInterruptsSignaled`, `InputBatchesDrained`
  - `ControllerSignalsEmitted`, `EventsProcessed`
  - `IdleParks`, `IdleWakes`
  - `EmptyDrainWakes`, `MissedFlagSets`
  - `ButtonDowns[]`, `ButtonUps[]`, `ButtonMismatches`
  - `PeakMouseQueueSize`, `PeakKeyboardQueueSize`
  - `CoalescedMoves`, `DroppedMoves`
- [ ] Add latency histogram: `LatencyHistogram[]` for <1ms, 1-5ms, 5-20ms, >20ms
- [ ] Add `SignalToDrainRatio` computed property
- [ ] Add `StormDetected` computed property

#### Step 6.2: Add Trace Hooks
- [ ] Add `[Conditional("TRACE_EVENTS")] TraceEventFlow()` method
- [ ] Instrument: ENQUEUE, SIGNAL_INPUT, INPUT_WAKE, INPUT_DRAIN_COMPLETE, CTRL_SIGNAL, CTRL_BLOCK, CTRL_WAKE

#### Step 6.3: Add Debug Assertions
- [ ] Assert queue not empty on InputProcess wake (I4)
- [ ] Assert ControllerSemaphore count == MouseEventQueue size (I3)
- [ ] Assert discrete transitions never coalesced (I6)

#### Step 6.4: Add Self-Healing Guards (Release Builds)
- [ ] Implement `CheckAndHealMissedEdge()` for release builds
- [ ] Implement `TrackSpuriousWake()` to disable aggressive checks if spurious wakes exceed threshold

#### Step 6.5: Add Latency SLA Monitoring
- [ ] Track `LastEnqueueTimestamp`, `LastInputWakeTimestamp`
- [ ] Compute latency and update histogram
- [ ] Log alarm if >20ms latency repeated

**Files**: New `EventDiagnostics.cs`, modifications to `VirtualMachine.cs`
**Estimated lines**: ~200

---

### Phase 7: Smalltalk-Side Implementation

**Goal**: Implement InputProcess drain loop and consumer protocols

#### Step 7.1: InputState Class Updates
- [ ] Ensure `InputState>>run` drains entire queue per wake:
  ```smalltalk
  InputState>>run
      [true] whileTrue: [
          InputSemaphore wait.
          [self primInputWord notNil] whileTrue: [
              self processOneEvent.
              self enqueueMouseEventIfNeeded.
              ControllerSemaphore signal].
          self primInputDrainComplete]
  ```
- [ ] Add `primInputDrainComplete` method calling new primitive
- [ ] Ensure keyboard and mouse events go to separate logical queues

#### Step 7.2: InputSensor Drain APIs
- [ ] Implement `hasKeyboardEvent`, `nextKeyboardEvent`
- [ ] Implement `hasPointerEvent`, `nextPointerEvent`
- [ ] Ensure `nextMouseEvent` dequeues from MouseEventQueue (not polling bitState)

#### Step 7.3: Sensor Wait Methods
- [ ] Verify `waitForControllerEvent` blocks on ControllerSemaphore
- [ ] Implement `waitButton` with blocking (no busy-wait)
- [ ] Implement `waitNoButton` with blocking (no busy-wait)

#### Step 7.4: PopUpMenu Refactor
- [ ] Refactor `PopUpMenu>>startUp:` to use event-driven blocking pattern
- [ ] Implement `checkForCancel` for ESC key and focus loss
- [ ] Ensure yellow-button up resolves selection atomically

#### Step 7.5: Controller Patterns
- [ ] Ensure controllers use one of two canonical patterns:
  - Wake-once, process-one (state machines)
  - Wake-once, drain-many (trackers/menus)
- [ ] Add `trackPotentialDrag` with DragThreshold constant

#### Step 7.6: MouseEvent Enhancement
- [ ] Add `modifierSnapshot` field to MouseEvent objects
- [ ] Capture modifier state at event creation time
- [ ] Add `modifiers` accessor method

**Files**: `Smalltalk-80.st`
**Estimated lines**: ~150

---

### Phase 8: Event Coalescing and Fairness

**Goal**: Implement move coalescing and event class ordering

#### Step 8.1: Move Coalescing (Optional, Gated)
- [ ] In producer or InputProcess, detect consecutive `#move` events
- [ ] Replace previous move with latest position (don't enqueue new item)
- [ ] Never coalesce across button transitions
- [ ] Gate with `EventConfig.MoveCoalescingEnabled` (default false)

#### Step 8.2: Event Class Ordering
- [ ] In InputProcess drain, process events in order:
  1. Discrete transitions (button down/up, key down/up)
  2. Wheel/scroll events
  3. Pointer moves (coalesced)
- [ ] Use separate sub-queues or priority markers

#### Step 8.3: Backpressure
- [ ] Add soft cap for pending moves (default: 100)
- [ ] Beyond cap: aggressive coalescing, drop intermediate moves
- [ ] Increment `DroppedMoves` metric
- [ ] Log warning on first drop per window

#### Step 8.4: Keyboard Fairness
- [ ] Ensure keyboard events not starved by move bursts
- [ ] If keyboard queue has events, prioritize over moves in same drain cycle

**Files**: `SmalltalkEventQueue.cs`, `Smalltalk-80.st`
**Estimated lines**: ~80

---

### Phase 9: OS Integration

**Goal**: Handle focus loss, capture loss, suspend/resume

#### Step 9.1: Focus/Capture Loss Events
- [ ] In PlatformBridge, detect window focus loss
- [ ] Enqueue synthetic "cancel" event on focus loss
- [ ] Enqueue synthetic "cancel" event on mouse capture loss

#### Step 9.2: Popup Cancel Handling
- [ ] Update `PopUpMenu>>checkForCancel` to check `Sensor focusLost`
- [ ] On cancel, return selection = 0 and exit cleanly
- [ ] Ensure no deadlock on focus loss mid-popup

#### Step 9.3: Suspend/Resume Handling
- [ ] On system resume, call `CheckAndHealMissedEdge()`
- [ ] Re-evaluate `HasPendingEvents()` and re-arm flag if needed
- [ ] Clear stale diagnostic counters that span suspend window

**Files**: `PlatformBridge` implementations, `VirtualMachine.cs`, `Smalltalk-80.st`
**Estimated lines**: ~50

---

### Phase 10: Testing and Validation

**Goal**: Execute test plan and validate all invariants

#### Step 10.1: Unit Tests
- [ ] Test atomic flag operations (race scenarios)
- [ ] Test runnable guard latch behavior
- [ ] Test interrupt masking during simulated GC
- [ ] Test event ordering (discrete before moves)

#### Step 10.2: Integration Tests
- [ ] Popup menu nested test (no deadlock)
- [ ] 0% idle CPU test (60-second idle periods)
- [ ] Burst stress test (1-2 kHz mouse moves for 3 seconds)
- [ ] Backward-loop latency test (<1ms response)

#### Step 10.3: Regression Tests
- [ ] BlockContextTest returns 1191
- [ ] Timer/Delay mechanism works correctly
- [ ] Keyboard input works correctly
- [ ] Display updates work correctly

#### Step 10.4: Advanced Tests
- [ ] Focus/capture loss during popup
- [ ] Mixed input burst test (keyboard fairness, I7)
- [ ] Suspend/resume test
- [ ] GC under load test

#### Step 10.5: Decision Checkpoint Validation
- [ ] Checkpoint 1 (Correctness): All core criteria pass
- [ ] Checkpoint 2 (Performance): ~0% idle CPU, no signal storms
- [ ] Checkpoint 3 (Robustness): <1ms tight loop latency, stress test passes

**Files**: Test scripts, possibly new `EventTests.cs`
**Estimated lines**: ~100

---

### Implementation Order and Dependencies

```
Phase 1 (Core Infrastructure)
    │
    ├──► Phase 2 (Idle Path) ─────────────┐
    │                                      │
    ├──► Phase 3 (Check Points) ──────────┼──► Phase 6 (Diagnostics)
    │                                      │         │
    └──► Phase 4 (Scheduler Hook)         │         │
              │                            │         ▼
              └──► Phase 5 (Interrupt     │    Phase 10 (Testing)
                   Masking) ──────────────┘         ▲
                                                    │
Phase 7 (Smalltalk) ──────────────────────────────►│
    │                                               │
    └──► Phase 8 (Coalescing/Fairness)             │
              │                                     │
              └──► Phase 9 (OS Integration) ───────┘
```

**Recommended implementation sequence**:
1. Phases 1-4 (core VM changes) - Must be done first, in order
2. Phase 5 (interrupt masking) - Can parallel with Phase 6
3. Phase 6 (diagnostics) - Enables debugging of later phases
4. Phase 7 (Smalltalk) - Core Smalltalk changes
5. Phase 8 (coalescing) - Optional optimization
6. Phase 9 (OS integration) - Edge case handling
7. Phase 10 (testing) - Continuous throughout, final validation

**Total estimated lines**: ~840 lines of new/modified code

---

## Minimal Code Changes Summary

### VM Changes (C#)

| Component | Change |
|-----------|--------|
| `VirtualMachine.cs` | Add `_inputInterruptPending` volatile flag |
| `VirtualMachine.cs` | Add `CheckAndSignalPendingEvents()` with edge semantics |
| `VirtualMachine.cs` | Call check at periodic counter and two strategic yield points (backward branch, method return) |
| `VirtualMachine.cs` | Ensure idle path uses single wake-to-signal pattern with full drain by InputProcess |
| `SmalltalkEventQueue.cs` | Set `_inputInterruptPending = true` on enqueue (via callback or shared reference) |

### Smalltalk Changes

| Component | Change |
|-----------|--------|
| `InputSensor>>waitForControllerEvent` | Ensure blocks on ControllerSemaphore (already correct) |
| `InputState>>run` | Verify drains entire queue per wake (already correct) |
| `PopUpMenu`, trackers, dialogs | Refactor loops to drain events on wake and avoid busy waiting |
| Consumer state machines | Document so multiple events per wake are correctly handled and exit conditions are explicit |

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
| UI (WinForms) | `_eventQueue`, `_inputInterruptPendingInt` flag, `_idleWake` handle | Smalltalk heap, Smalltalk semaphores |
| VM (Smalltalk) | Everything | (owns all state) |

### Atomic Operations

| Operation | Atomicity | Notes |
|-----------|-----------|-------|
| `Interlocked.Exchange(ref _inputInterruptPendingInt, 1)` | Atomic | UI thread arms flag |
| `Interlocked.Exchange(ref _inputInterruptPendingInt, 0)` | Atomic | VM thread clears flag (returns previous value) |
| `_eventQueue.Enqueue()` | Thread-safe | ConcurrentQueue |
| `_eventQueue.TryDequeue()` | Thread-safe | ConcurrentQueue |
| `SignalSemaphore()` | VM thread only | No cross-thread access |
| `_idleWake.Set()` | Thread-safe | ManualResetEventSlim |
| `_idleWake.Reset()` | VM thread only | After wake, before repark |

### PlatformBridge Contract Clarity

**Gap**: `HasPendingEvents()` semantics must be precise: does it reflect the producer queue, the VM-visible queue, or both?

**Definition**:
- `HasPendingEvents()` checks the producer queue (UI thread queue)
- `TryDequeue()` drains from a lock-free queue accessible to the VM thread
- If you have a staging buffer, ensure no time window exists where `HasPendingEvents()` is false while items are in the staging buffer

### Idle Handle Semantics

**Gap**: Multiple `Set()` calls before a `WaitAny()` can stack differently depending on handle type.

**Fix**: Use `ManualResetEventSlim` for wake gating in idle, and explicitly `Reset()` after the VM handles the wake, with the edge flag preventing re-signal loops.

### Race Condition Analysis

**Scenario**: UI thread enqueues while VM thread checks flag

```
UI thread:                              VM thread:
_queue.Enqueue(e)                       if (HasPending() &&
Interlocked.Exchange(_flag, 1)             Interlocked.Exchange(_flag, 0) == 1)
                                               Signal()
```

**Result**: Safe with Interlocked operations. Either:
1. VM clears flag before UI sets it → UI sets flag → next check catches it
2. VM clears flag after UI sets it → returns 1 → signal fires correctly
3. Both racing → Interlocked guarantees exactly one Exchange returns 1

**Self-healing guard** (in release builds) catches case where events pile up with flag=0 and InputProcess blocked.

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

## Appendix C: Implementation Notes & Rollout Plan

This appendix provides critical implementation guidance, failure mode handling, and proof obligations for maintainers.

### Minimal Path Checklist

Critical changes required to fix the deadlock and preserve 0% idle CPU:

| # | Change | Purpose |
|---|--------|---------|
| 1 | Edge-trigger flag with atomic operations (`Interlocked.Exchange`) | Race-free interrupt simulation |
| 2 | Runnable/draining latch (`_inputProcessRunnableOrDraining`) + end-of-drain primitive | Prevent re-signaling during drains |
| 3 | Hardened idle path using `ManualResetEventSlim` | Proper wake semantics |
| 4 | Periodic + method-return checks | Interrupt simulation for tight loops |
| 5 | Basic diagnostics counters | Observable correctness |

**Everything else (move coalescing, fairness ordering, GC masking) can be phased in after the minimal fix is validated.**

### Ordering Contract

Canonical event ordering for fairness and correctness:

| Priority | Event Class | Coalescing |
|----------|-------------|------------|
| 1 (highest) | Discrete transitions (mouse/keyboard down/up) | Never |
| 2 | Wheel/scroll events | Never |
| 3 | Pointer moves | May coalesce |
| 4 | Timers | N/A |
| 5 (lowest) | Low-space interrupts | N/A |

**Rule**: Transitions are never coalesced or reordered. Moves may be coalesced, but only within batch boundaries and never across transitions.

### Failure Mode Guards

| Failure Mode | Detection | Mitigation |
|--------------|-----------|------------|
| Storm detected | `SignalToDrainRatio > 1.5` | Auto-disable backward-branch check; log anomaly |
| Empty drain wakes > threshold | `EmptyDrainWakes > ε/minute` | Log anomaly, degrade gracefully |
| Latch stuck true | Latch remains true > T ms with InputSemaphore count = 0 | Watchdog clears latch; log diagnostic snapshot |
| Focus/capture loss | Platform event | Synthetic cancel event overrides subsequent transitions in same batch |
| Input during masked interrupts | Large burst during GC | On unmask: re-check `HasPendingEvents()` and force single signal |
| Backpressure overflow | Queue size > soft cap | Aggressive coalescing; drop intermediate moves (stress profile only) |

### Proof Obligations

Each invariant must be observable via diagnostics:

| Invariant | Proof Mechanism |
|-----------|-----------------|
| I1 (Edge signaling) | Enqueue→Signal latency histogram; max latency ≤ Δ |
| I2 (Drain invariant) | Every `SIGNAL_INPUT` paired with `INPUT_DRAIN_COMPLETE`; count equality |
| I3 (Counting invariant) | At drain end: `ControllerSemaphore.count == MouseEventQueue.size` |
| I4 (No spurious wake) | `EmptyDrainWakes` counter ≤ ε/minute |
| I5 (Idle) | `IdleParks` durations show near-0% CPU; no wake without external cause |
| I6 (Down/up preservation) | Per-button: `Downs − Ups ∈ {0, 1}`; dump last 10 events on violation |
| I7 (Fairness) | Keyboard latency histogram independent of pointer load; SLO <10ms 99th percentile |

### Profiles & Config Summary

| Profile | Backward Branch | Method Return | Primitive Return | Move Coalescing | Use Case |
|---------|-----------------|---------------|------------------|-----------------|----------|
| **Conservative** | ❌ | ❌ | ❌ | ❌ | Rollback/baseline; idle-only signaling |
| **Responsive** | ✅ | ✅ | ❌ | ✅ | Recommended for interactive use |
| **Stress** | ✅ | ✅ | ✅ | Aggressive | Testing, high-frequency input |

Profiles are selectable via environment variable `SMALLTALK_EVENT_PROFILE` or config file.

### Rollback & Recovery

| Mechanism | Purpose |
|-----------|---------|
| `EnableInterruptSimulation=false` | Disables periodic checks; falls back to idle-only signaling |
| Health check | If anomalies exceed thresholds twice in one minute, auto-disable aggressive checks |
| Crash minimization | On invariant breach in release builds: degrade-and-log instead of assert; dump diagnostic snapshot to file |

---

## Appendix D: Phase-by-Phase Rollout Timeline

This appendix provides a **high-level rollout strategy** separating the **urgent fix** (Phase 1) from **robustness** (Phases 2-4), **optimization** (Phases 5-6), and **maintainability** (Phases 7-10).

> **Note**: For detailed technical steps, file names, and code snippets, see the **Implementation Plan** section in the main body of this document. This appendix focuses on rollout phasing, validation criteria, and ship gates.

### Phase 1: Minimal Fix (Critical Path)

**Goal**: Eliminate deadlock and preserve 0% idle CPU.

**Changes**:
- Implement edge-trigger flag with atomic ops (`Interlocked.Exchange`)
- Add `_inputProcessRunnableOrDraining` latch + end-of-drain primitive
- Harden idle path with `ManualResetEventSlim`
- Add periodic + method-return checks
- Basic diagnostics counters

**Validation**:
- [ ] Popup menus work without deadlock
- [ ] CPU usage ~0% when idle
- [ ] No missed events in stress test

**Ship criteria**: All validation passes. This is the urgent fix.

---

### Phase 2: Fairness & Ordering

**Goal**: Ensure balanced delivery across event classes.

**Changes**:
- Explicit event ordering: transitions → wheel → moves
- Separate logical queues for keyboard vs pointer
- Preserve down/up transitions exactly; coalesce only moves

**Validation**:
- [ ] Keyboard latency histogram shows <10 ms even under pointer bursts
- [ ] Down/up balance invariant holds (I6)

---

### Phase 3: Interrupt Masking & GC Safety

**Goal**: Prevent unsafe signaling during GC or long primitives.

**Changes**:
- Add `_interruptsMaskedCounter`
- On unmask, force consolidated signal if events pending
- Scheduler hook clears latch on context switch away from InputProcess

**Validation**:
- [ ] GC under load test: single consolidated wake post-GC
- [ ] No invariant breaches during long primitive execution

---

### Phase 4: OS Integration Edge Cases

**Goal**: Handle focus/capture loss, suspend/resume gracefully.

**Changes**:
- Synthetic cancel event on focus/capture loss
- Resume handler re-arms edge flag and forces signal if events pending

**Validation**:
- [ ] Popup menus cancel cleanly on ESC or focus loss
- [ ] First post-resume input delivered correctly

---

### Phase 5: Backpressure & Coalescing

**Goal**: Prevent memory ballooning under extreme load.

**Changes**:
- Soft cap on pending moves (default 100)
- Aggressive coalescing/dropping only in "stress" profile
- Log `CoalescedMoves` and `DroppedMoves` metrics

**Validation**:
- [ ] Stress test shows bounded queue size
- [ ] Normal profile preserves UX (no jitter)

---

### Phase 6: Diagnostics Expansion

**Goal**: Make invariants observable and provable.

**Changes**:
- Latency histograms (enqueue→signal, signal→wake)
- Per-button down/up counters
- Rolling trace buffer for last N events

**Validation**:
- [ ] Proof obligations (I1-I7) verifiable via logs
- [ ] Storm detection ratio ~1.0 under burst

---

### Phase 7: Profiles & Config

**Goal**: Allow runtime tuning.

**Changes**:
- Profiles: Conservative, Responsive, Stress
- Feature toggles for check sites and coalescing
- Environment variable and config file support

**Validation**:
- [ ] Switching profiles logs marker; traces show context
- [ ] Conservative profile matches legacy idle-only behavior

---

### Phase 8: Failure Mode Guards

**Goal**: Degrade gracefully under anomalies.

**Changes**:
- Auto-disable backward-branch check if spurious wakes exceed threshold
- Watchdog clears stuck latch after T ms
- Degrade-and-log instead of assert in release builds

**Validation**:
- [ ] Health check triggers under synthetic anomaly injection
- [ ] System continues without crash

---

### Phase 9: Consumer API Standardization

**Goal**: Enforce canonical drain patterns.

**Changes**:
- Add `Sensor>>hasKeyboardEvent`, `nextKeyboardEvent`, `hasPointerEvent`, `nextPointerEvent`
- Forbid direct queue peeks outside sanctioned APIs
- Add `Sensor>>safeWaitAndProcessOne` for legacy migration

**Validation**:
- [ ] Legacy consumers migrated to APIs
- [ ] No underflow/overflow mismatches

---

### Phase 10: Regression & Long-Term Tests

**Goal**: Ensure stability across all workloads.

**Tests**:
- [ ] BlockContextTest returns 1191
- [ ] Timer/Delay correctness
- [ ] Keyboard autorepeat preserved
- [ ] Hover/drag threshold behavior correct
- [ ] Suspend/resume, focus loss, nested menus

**Validation**:
- [ ] All invariants hold
- [ ] CPU overhead <0.1% active, ~0% idle

---

### Rollout Summary

```
┌─────────────────────────────────────────────────────────────────┐
│ URGENT FIX                                                      │
│ Phase 1: Minimal Fix → Fixes deadlock, preserves 0% idle       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ ROBUSTNESS                                                      │
│ Phase 2: Fairness & Ordering                                   │
│ Phase 3: Interrupt Masking & GC Safety                         │
│ Phase 4: OS Integration Edge Cases                             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ OPTIMIZATION                                                    │
│ Phase 5: Backpressure & Coalescing                             │
│ Phase 6: Diagnostics Expansion                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│ MAINTAINABILITY                                                 │
│ Phase 7: Profiles & Config                                     │
│ Phase 8: Failure Mode Guards                                   │
│ Phase 9: Consumer API Standardization                          │
│ Phase 10: Regression & Long-Term Tests                         │
└─────────────────────────────────────────────────────────────────┘
```

---

## Appendix E: State Diagrams and Configuration

This appendix provides visual state diagrams and detailed configuration code for the event architecture.

> **Note**: For a summary of configuration profiles, see **Appendix C: Profiles & Config Summary**. This appendix contains the detailed implementation code.

### VM Interrupt State Machine

```
                  ┌─────────┐
                  │  IDLE   │◄──────────────────┐
                  │ (parked)│                   │
                  └────┬────┘                   │
                       │ WaitAny returns        │
                       ▼                        │
                  ┌─────────┐                   │
                  │  ARMED  │                   │
                  │(flag=1) │                   │
                  └────┬────┘                   │
                       │ CheckAndSignal         │
                       │ clears flag            │
                       ▼                        │
                  ┌─────────┐                   │
                  │SIGNALED │                   │
                  │(runnable│                   │
                  │ latched)│                   │
                  └────┬────┘                   │
                       │ InputProcess           │
                       │ gains CPU              │
                       ▼                        │
                  ┌─────────┐                   │
                  │DRAINING │                   │
                  │(full    │                   │
                  │ drain)  │                   │
                  └────┬────┘                   │
                       │ primInputDrainComplete │
                       │ clears latch           │
                       └────────────────────────┘
```

### Popup Menu State Machine

```
                  ┌──────────────┐
                  │   DISPLAY    │
                  │ (show menu)  │
                  └──────┬───────┘
                         │
                         ▼
                  ┌──────────────┐
             ┌───►│ WAIT_BUTTON  │◄───┐
             │    │ (block on    │    │
             │    │  semaphore)  │    │
             │    └──────┬───────┘    │
             │           │ wake       │
             │           ▼            │
             │    ┌──────────────┐    │
             │    │ PROCESS_EVT  │    │
             │    │ (drain queue)│    │
             │    └──────┬───────┘    │
             │           │            │
             │     ┌─────┴─────┐      │
             │     │           │      │
             │     ▼           ▼      │
             │  #move       #up       │
             │     │           │      │
             │     ▼           ▼      │
             │  UPDATE     RESOLVE    │
             │  MARKER     SELECTION  │
             │     │           │      │
             └─────┘           ▼      │
                        ┌──────────┐  │
                        │  DONE    │  │
                        │ (return) │  │
                        └──────────┘  │
                               │      │
                               │ more │
                               │events│
                               └──────┘
```

### Configuration Constants

Centralize these for tuning and A/B testing:

```csharp
public static class EventConfig
{
    // Check placement
    public const int PeriodicCheckInterval = 1000;
    public static bool EnableBackwardBranchCheck = true;
    public static bool EnableMethodReturnCheck = true;
    public static bool EnablePrimitiveReturnCheck = false;

    // Coalescing
    public static bool MoveCoalescingEnabled = false;  // Start conservative
    public const int MaxCoalescedMoves = 10;

    // Diagnostics
    public static bool EnableEventTracing = false;
    public static bool EnableLatencyProbes = false;
    public const int SpuriousWakeThresholdPerMinute = 5;
}
```

### Feature Flags

Gate new checks and coalescing with flags so you can A/B test quickly:

| Flag | Default | Purpose |
|------|---------|---------|
| `EnableBackwardBranchCheck` | true | Check at loop back-edges |
| `EnableMethodReturnCheck` | true | Check at method returns |
| `EnablePrimitiveReturnCheck` | false | Check after non-trivial primitives |
| `MoveCoalescingEnabled` | false | Coalesce consecutive mouse moves |
| `EnableEventTracing` | false | Emit TRACE_EVENTS output |
| `EnableLatencyProbes` | false | Track enqueue-to-wake latency |

### Configuration Profiles

A single environment variable or INI section switches profiles for rapid A/B testing:

| Profile | Backward Branch | Method Return | Primitive Return | Move Coalescing | Use Case |
|---------|-----------------|---------------|------------------|-----------------|----------|
| **Conservative** | ❌ | ❌ | ❌ | ❌ | Maximum compatibility, baseline |
| **Responsive** | ✅ | ✅ | ❌ | ✅ | Recommended for interactive use |
| **Stress** | ✅ | ✅ | ✅ | Aggressive | Testing, high-frequency input |

```csharp
public static void ApplyProfile(string profile)
{
    switch (profile.ToLower())
    {
        case "conservative":
            EnableBackwardBranchCheck = false;
            EnableMethodReturnCheck = false;
            EnablePrimitiveReturnCheck = false;
            MoveCoalescingEnabled = false;
            break;
        case "responsive":
            EnableBackwardBranchCheck = true;
            EnableMethodReturnCheck = true;
            EnablePrimitiveReturnCheck = false;
            MoveCoalescingEnabled = true;
            break;
        case "stress":
            EnableBackwardBranchCheck = true;
            EnableMethodReturnCheck = true;
            EnablePrimitiveReturnCheck = true;
            MoveCoalescingEnabled = true;
            MaxCoalescedMoves = 5;  // Aggressive coalescing
            break;
    }
}
```

---

## Conclusion

This refined design addresses all identified gaps and provides robust support for nested blocking input loops with concurrency hardening:

### Core Design Achievements

1. ✅ **Atomic edge-triggering**: `Interlocked.Exchange` ensures race-free flag operations
2. ✅ **"Already runnable" guard**: Prevents re-signaling during drains via `_inputProcessRunnableOrDraining` latch
3. ✅ **Scheduler context switch hook**: Clears latch on process switch away from InputProcess
4. ✅ **No count inflation**: One signal per batch, InputProcess drains fully
5. ✅ **Storm control**: Drain-all policy prevents preemption cascades
6. ✅ **Idle correctness**: Flag guard with `HasPendingEvents()` truth source prevents bounce
7. ✅ **Count invariant**: ControllerSemaphore count == MouseEventQueue size
8. ✅ **Diagnostics**: Counters, traces, storm detector, latency SLA alarms, and promoted guards

### Nested Blocking Loop Support

9. ✅ **Preemption guarantee**: InputProcess (priority 6) preempts controllers (priority 4) on event arrival
10. ✅ **Blocking semantics**: `waitForControllerEvent` blocks without busy-waiting
11. ✅ **Nest-safe consumption**: Popup menus, dialogs, and trackers share consistent event stream
12. ✅ **Priority discipline**: Clear separation of InputProcess (producer) and Controllers (consumers)
13. ✅ **Cancel semantics**: ESC key and focus loss handled with synthetic cancel events
14. ✅ **Explicit drain APIs**: `hasKeyboardEvent`/`nextKeyboardEvent`, `hasPointerEvent`/`nextPointerEvent`

### Concurrency Hardening

15. ✅ **Atomic operations**: `Interlocked.Exchange` for flag arm/clear prevents lost edges
16. ✅ **ManualResetEventSlim**: Explicit reset after wake prevents signal stacking issues
17. ✅ **Self-healing guards**: Release builds correct missed edges automatically
18. ✅ **PlatformBridge contract**: Clear semantics for `HasPendingEvents()` vs `TryDequeue()`
19. ✅ **Interrupt masking**: Long primitives and GC block signaling, consolidated signal on unmask

### Event Semantics and Fairness

20. ✅ **Down/up preservation**: Discrete transitions never coalesced (I6)
21. ✅ **Fairness guarantee**: Keyboard events processed within F ms even under move bursts (I7)
22. ✅ **Modifier snapshots**: Each event carries modifier state at time of event
23. ✅ **Event class ordering**: Discrete transitions first, then wheel, then moves
24. ✅ **Backpressure**: Soft cap with aggressive coalescing under extreme load

### OS Integration

25. ✅ **Focus/capture loss**: Synthetic cancel event triggers popup exit
26. ✅ **Suspend/resume**: Re-arm edge flag and single signal post-resume
27. ✅ **Configuration profiles**: Conservative, Responsive, Stress for A/B testing

### Formal Invariants

28. ✅ **I1-I7 documented**: Edge signaling, drain, counting, no-spurious-wake, idle, down/up preservation, and fairness invariants

**Recommendation**: Proceed with implementation following the phased plan. Lock down atomic flag ops, add the runnable/draining latch, explicitly order event classes (no coalescing of discrete transitions), handle focus/capture loss with a cancel, and add interrupt masking around GC/long primitives. Then ship with the new invariants and tests covering mixed bursts, focus loss, and suspend/resume.
