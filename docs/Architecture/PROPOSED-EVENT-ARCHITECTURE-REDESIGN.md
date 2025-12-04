# Proposed Event Architecture Redesign

**Date**: 2025-12-04
**Status**: PROPOSAL (v4 - with concurrency hardening and formal invariants)
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

---

## Design Principles

### 1. Edge-Triggered, Idempotent Signaling

**Problem with level-triggered approach**: Re-signaling InputSemaphore at every periodic check while events are pending causes unnecessary wakeups if InputProcess is already runnable or draining. This inflates semaphore counts and creates wake cycles with no new work.

**Solution**: Use a VM-local atomic flag `_inputInterruptPendingInt` that:
- Is set atomically by the UI thread on enqueue (edge-arm)
- Is checked and cleared atomically by the periodic check
- Ensures exactly one InputSemaphore signal per event batch

**Gap: volatile bool is not sufficient** for multi-threaded edge semantics if the UI thread and VM thread race; you can get lost edges or double-clears.

**Fix: Use atomic operations** to make the "arm once, clear once" behavior explicit:

```
Enqueue (UI thread):
  queue.Add(event)
  Interlocked.Exchange(ref _inputInterruptPendingInt, 1)  // Edge-arm; atomic

Periodic check (VM thread):
  if (HasPendingEvents() && Interlocked.Exchange(ref _inputInterruptPendingInt, 0) == 1):
    SignalInputSemaphore()  // One signal per batch
```

This assures one signal per batch regardless of write reordering or concurrent writes; it also prevents the VM thread from clearing a freshly armed edge before signaling.

### 1a. "Already Runnable" Guard

**Gap**: You may re-signal InputSemaphore while the InputProcess is runnable (or mid-drain), causing count inflation and redundant preemption.

**Fix**: Track a VM-local boolean `_inputProcessRunnableOrDraining`. Only signal if it's false.
- Set `true` immediately before signaling
- Set `false` when InputProcess parks (after full drain)

The InputProcess calls a primitive at the end of its drain to notify the VM:

```
VM side:
  _inputProcessRunnableOrDraining = true  // before signal
  SignalInputSemaphore()

InputProcess (Smalltalk):
  "At end of drain, tell VM we are parking again"
  self primInputDrainComplete  // primitive clears the flag
```

### 2. Full Queue Drain Per Wake (InputProcess Drain Invariant)

**Problem**: If InputProcess only processes one event per wake, and multiple events arrive, semaphore counts inflate and create unnecessary context switches.

**Solution**: InputProcess drains the entire queue before waiting again:

**Drain-all policy**: The InputProcess, upon waking, must:
1. Dequeue all pending OS events
2. Translate each to Smalltalk Mouse/Keyboard events
3. Signal ControllerSemaphore once per translated event

**Return-to-wait**: When the queue becomes empty, the InputProcess waits on InputSemaphore. No further VM signaling occurs until the flag is re-armed by new arrivals.

```smalltalk
InputState>>run
    [true] whileTrue: [
        InputSemaphore wait.
        "Drain entire queue in one wake cycle"
        [self primInputWord notNil] whileTrue: [
            self processOneEvent.
            self enqueueMouseEventIfNeeded.
            ControllerSemaphore signal].
        "Queue empty - return to wait; flag will be re-armed by next enqueue"
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
3. **After non-trivial primitives** (display, arithmetic bursts)
4. **After context switch operations** (sends that switch active process)

**Config**: Make each check site enable/disable via flags for tuning.

### 5. Event Batching, Coalescing, and Fairness

#### Mouse Move Coalescing

**Gap**: High-frequency motion floods the queue with redundant moves, bloating ControllerSemaphore counts and increasing wake loops for consumers.

**Fix**: Coalesce consecutive `#move` events:
- In the producer: if last queued event is a move, update its position instead of enqueueing a new item
- In the InputProcess drain: collapse multiple moves into the latest point for the same button state before signaling controllers

**Policy**: Maintain ordering of button transitions; moves can be squashed as long as they don't skip over transitions.

#### Batch Signaling Rate Limiting

**Gap**: Very high enqueue rates could make the VM oscillate at the boundary of drains even with edge-triggering.

**Fix**: After signaling InputSemaphore, throttle new signals during the current drain window:
- Keep `_inputProcessRunnableOrDraining = true` until the drain completes
- UI thread continues enqueueing, but signals only resume once the InputProcess parks

#### Keyboard Fairness

**Gap**: Keyboard events and pointer events share one ControllerSemaphore but may have different urgency. Pointer move coalescing risks starving keypress delivery.

**Fix**: Use separate logical queues for keyboard and mouse, but one ControllerSemaphore. The InputProcess drain alternates or prioritizes discrete transitions first (down/up) before bulk moves.

### 6. Timer and Input Ordering

**Gap**: Timer signaling can race with input signaling and reorder events in a way that surprises controllers.

**Fix**: Enforce ordering: perform input signaling before timer signaling inside each periodic cycle, or vice versa, but document and assert it.

**Rule**: "User input preempts timers" - input signaling occurs first in each check cycle.

### 7. Low-Space and GC Interactions

#### Interrupt Precedence

**Gap**: Blue Book mentions low-space interrupt; if added later, its signal can starve input or vice versa.

**Fix**: Define a precedence order:
1. Input (highest urgency for UI responsiveness)
2. Timer
3. Low-space

Consider delivering low-space to a dedicated handler at priority 7 so it preempts input only when necessary.

#### GC-Safe Signaling

**Gap**: If GC compacts while input arrives, ensure the InputProcess wake isn't delivered mid-compaction with heap locks held.

**Fix**: Block input signaling during GC critical sections and re-arm the edge flag afterward; then perform a single signal post-GC.

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

**Contract**: Consumers must not assume a wake delivers exactly one event; they must confirm queue state explicitly. Use `Sensor hasMouseEvent` and `Sensor nextMouseEvent` as the sanctioned API.

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

---

## Reentrancy and Priority Safety

### Priority Discipline

| Process | Priority | Role |
|---------|----------|------|
| InputProcess | 6 (lowIOPriority) | Sole producer of controller signals for input |
| Controllers | 4 (userSchedulingPriority) | Consumers of mouse/keyboard events |
| Background | 1 (systemRockBottomPriority) | Idle sentinel |

**Rule**: Only the InputProcess should run at priority 6; controller code runs at priority 4 or below. This ensures preemption works and prevents controller code from starving input.

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

These diagnostics protect blocking semantics and help identify protocol violations.

### Counters (for performance monitoring)

```csharp
// VM diagnostic counters
public class EventDiagnostics
{
    // Core flow counters
    public long InputInterruptsSignaled;     // Edge-trigger signals to InputSemaphore
    public long InputBatchesDrained;         // Number of full drains by InputProcess
    public long ControllerSignalsEmitted;    // Signals sent to ControllerSemaphore (must equal translated events)
    public long EventsProcessed;             // Total events through queue
    public long IdleParks;                   // Times VM entered idle
    public long IdleWakes;                   // Times WaitAny returned

    // Histogram: events per drain batch
    public int[] EventsPerBatchHistogram;    // [0]=0 events, [1]=1-5, [2]=6-20, [3]=21+

    // Anomaly detection
    public long EmptyDrainWakes;             // InputProcess woke but queue empty (bug indicator)
    public long MissedFlagSets;              // HasPendingEvents true but flag false (race indicator)
    public long ControllerSemaphoreUnderflows; // Consumer wait but no events (mismatch indicator)

    // Latency probes
    public long LastEnqueueTimestamp;        // Timestamp on enqueue
    public long LastInputWakeTimestamp;      // Timestamp when InputProcess wakes
    public long MaxEnqueueToWakeLatency;     // Max observed latency
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

// Nested loop audit: trace when controller process blocks and wakes
TraceEventFlow("CTRL_BLOCK", processId, semaphoreId);
TraceEventFlow("CTRL_WAKE", processId, semaphoreId, eventsPending);
```

### Latency Measurement

Timestamp on enqueue and InputProcess wake to ensure consistent preemption under tight VM loops:

```csharp
// In Enqueue:
_diagnostics.LastEnqueueTimestamp = Stopwatch.GetTimestamp();

// In InputProcess wake (Smalltalk or VM hook):
var wakeTime = Stopwatch.GetTimestamp();
var latency = wakeTime - _diagnostics.LastEnqueueTimestamp;
_diagnostics.MaxEnqueueToWakeLatency = Math.Max(_diagnostics.MaxEnqueueToWakeLatency, latency);
```

### Assertions (debug builds)

```csharp
// No double wake loops
Debug.Assert(
    !(justSignaledInputSemaphore && queueWasEmpty),
    "InputProcess signaled but queue was empty - interrupt flag race?"
);

// Count invariant (I3)
Debug.Assert(
    ControllerSemaphoreCount == MouseEventQueueSize,
    $"Count mismatch: semaphore={ControllerSemaphoreCount}, queue={MouseEventQueueSize}"
);
```

### Promoted Guards (release builds)

These assertions are promoted to corrective paths in release mode:

```csharp
// Self-healing missed edge guard
// Promotes assertion to corrective action in release builds
private void CheckAndHealMissedEdge()
{
    if (_platformBridge.HasPendingEvents() &&
        Interlocked.CompareExchange(ref _inputInterruptPendingInt, 1, 0) == 0 &&
        InputProcessIsBlocked())
    {
        // Edge was missed - self-heal by arming and signaling
        _diagnostics.MissedFlagSets++;
        if (!_inputProcessRunnableOrDraining)
        {
            _inputProcessRunnableOrDraining = true;
            SignalInputSemaphoreIfRegistered();
        }
    }
}

// Spurious wake guard
// If InputProcess wakes to empty queue too often, disable non-essential check sites
private void TrackSpuriousWake(bool queueWasEmpty)
{
    if (queueWasEmpty)
    {
        _diagnostics.EmptyDrainWakes++;
        if (_diagnostics.EmptyDrainWakes > EventConfig.SpuriousWakeThresholdPerMinute)
        {
            // Too many spurious wakes - disable aggressive checks
            EventConfig.EnableBackwardBranchCheck = false;
            Console.WriteLine("[WARN] Spurious wakes exceeded threshold - disabling backward branch check");
        }
    }
}
```

### Storm Detector

Detects if signaling is happening during drains (indicates re-signaling bug):

```csharp
// Storm detection metric
// If InputInterruptsSignaled / InputBatchesDrained deviates from ~1.0 under burst,
// you're re-signaling during drains
public double SignalTodrainRatio =>
    InputBatchesDrained == 0 ? 0 : (double)InputInterruptsSignaled / InputBatchesDrained;

// Should be close to 1.0; > 1.5 indicates potential storm
public bool StormDetected => SignalToDrainRatio > 1.5 && InputBatchesDrained > 10;
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

## Appendix C: State Diagrams and Configuration

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

---

## Conclusion

This refined design addresses all identified gaps and provides robust support for nested blocking input loops with concurrency hardening:

### Core Design Achievements

1. ✅ **Atomic edge-triggering**: `Interlocked.Exchange` ensures race-free flag operations
2. ✅ **"Already runnable" guard**: Prevents re-signaling during drains via `_inputProcessRunnableOrDraining` latch
3. ✅ **No count inflation**: One signal per batch, InputProcess drains fully
4. ✅ **Storm control**: Drain-all policy prevents preemption cascades
5. ✅ **Idle correctness**: Flag guard with `HasPendingEvents()` truth source prevents bounce
6. ✅ **Count invariant**: ControllerSemaphore count == MouseEventQueue size
7. ✅ **Diagnostics**: Counters, traces, storm detector, and promoted guards

### Nested Blocking Loop Support

8. ✅ **Preemption guarantee**: InputProcess (priority 6) preempts controllers (priority 4) on event arrival
9. ✅ **Blocking semantics**: `waitForControllerEvent` blocks without busy-waiting
10. ✅ **Nest-safe consumption**: Popup menus, dialogs, and trackers share consistent event stream
11. ✅ **Priority discipline**: Clear separation of InputProcess (producer) and Controllers (consumers)

### Concurrency Hardening

12. ✅ **Atomic operations**: `Interlocked.Exchange` for flag arm/clear prevents lost edges
13. ✅ **ManualResetEventSlim**: Explicit reset after wake prevents signal stacking issues
14. ✅ **Self-healing guards**: Release builds correct missed edges automatically
15. ✅ **PlatformBridge contract**: Clear semantics for `HasPendingEvents()` vs `TryDequeue()`
16. ✅ **GC-safe signaling**: Block input signaling during GC critical sections

### Formal Invariants

17. ✅ **I1-I5 documented**: Edge signaling, drain, counting, no-spurious-wake, and idle invariants

**Recommendation**: Proceed with implementation following the phased plan, with checkpoint gates ensuring correctness before performance optimization.
