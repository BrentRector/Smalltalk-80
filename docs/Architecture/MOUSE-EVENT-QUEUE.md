# Mouse Event Queue Architecture

**Date**: 2025-12-03
**Status**: DESIGN COMPLETE - Ready for implementation
**Supersedes**: Handshake-based design in CPU-USAGE-ANALYSIS.md

---

## Overview

### Problem

State-based mouse handling (`bitState`) loses fast down/up transitions. Controllers poll current state and miss events. The handshake protocol blocks InputProcess when controllers are busy (e.g., menu tracking), causing deadlocks.

### Solution

Mirror keyboard design - provide a FIFO queue of mouse events to controllers. Controllers consume discrete button and motion events in order.

### Outcome

- No handshake needed
- No polling
- No missed transitions
- Still 0% idle CPU
- InputProcess never blocks
- Controllers process at their own pace

---

## Design

### Event Model

**MouseEvent fields:**

| Field | Type | Description |
|-------|------|-------------|
| kind | Symbol | `#down`, `#up`, `#move`, `#wheel` |
| button | Symbol | `#red`, `#yellow`, `#blue` (for down/up) |
| buttonsMask | Integer | Current composite mask after the event |
| position | Point | x@y at event time |
| wheelDelta | Integer | Signed integer for wheel |
| timestamp | Integer | VM-provided milliseconds |
| seq | Integer | Monotonically increasing sequence number |

### Queue Semantics

- **Mouse queue**: FIFO SharedQueue in Smalltalk
- **Keyboard queue**: Unchanged; remains FIFO
- **Ordering**: VM enqueues events in arrival order. Each OS message becomes one composite MouseEvent
- **No locking needed**: Cooperative scheduling ensures mutual exclusion

### Signaling

| Semaphore | Signaled By | Purpose |
|-----------|-------------|---------|
| InputSemaphore | VM | Raw input arrived |
| ControllerSemaphore | InputProcess | Mouse event enqueued |

**Key**: No HandshakeSemaphore. Queue provides natural serialization.

### Priorities

- **InputProcess** (priority 6): Updates queues
- **Controllers** (priority 4): Wake per ControllerSemaphore signals

---

## Why No Locking Is Needed

In classic Smalltalk-80, the scheduler is **cooperative, not preemptive**:

1. Processes only switch at explicit yield points (Processor yield, Semaphore wait, etc.)
2. No true concurrency: only one Smalltalk process is running at a time
3. SharedQueue uses semaphores internally to coordinate producers and consumers

**Implications:**

- **Enqueue side (InputProcess)**: Runs at priority 6. Dequeues OS events from VM bridge, enqueues into MouseEventQueue. Only runs when scheduled, blocks on InputSemaphore between events.
- **Dequeue side (Controller)**: Runs at priority 4. Blocks on ControllerSemaphore until InputProcess signals. When awake, dequeues from MouseEventQueue.
- **No race conditions**: Scheduler is cooperative, so InputProcess and Controller never run simultaneously.

---

## Sequence Diagram: Enqueue/Dequeue Without Races

```
OS Event → VM → InputSemaphore.signal
                 │
                 ▼
InputProcess (prio 6)
  wait → dequeue OS event
  update bitState, cursorPoint
  enqueue MouseEvent → MouseEventQueue
  ControllerSemaphore.signal
  wait again

Controller (prio 4)
  wait → wake
  dequeue MouseEvent → MouseEventQueue
  handle event
  wait again
```

**Key insight**: Single active process at a time. Queue operations are atomic at the Smalltalk level. Semaphores enforce alternation.

---

## Comparison: Handshake vs Queue Model

### Handshake Model (OLD - problematic)

```
OS Event → VM → InputSemaphore.signal
                 │
                 ▼
InputProcess (prio 6)
  dequeue OS event words
  update bitState
  ControllerSemaphore.signal
  HandshakeSemaphore.wait   ← blocks here

Controller (prio 4)
  wait → wake
  read Sensor buttons (bitState)
  HandshakeSemaphore.signal → InputProcess resumes
  act on event
```

**Problems:**
- Controllers only see current bitState
- Handshake needed to pause InputProcess until controller reads state
- Complexity: Multiple semaphores, risk of deadlock
- If controller is busy (menu tracking), InputProcess blocks forever

### Queue-Based Model (NEW - robust)

```
OS Event → VM → InputSemaphore.signal
                 │
                 ▼
InputProcess (prio 6)
  wait → dequeue OS event
  update bitState, cursorPoint
  enqueue MouseEvent → MouseEventQueue
  ControllerSemaphore.signal
  wait again

Controller (prio 4)
  wait → wake
  dequeue MouseEvent from MouseEventQueue
  act on event (Down/Up/Move/Wheel)
```

**Benefits:**
- No handshake: Queue naturally serializes events
- Reliability: Every transition enqueued; controllers consume in order
- Symmetry: Same model as keyboard events
- Simplicity: InputProcess never blocks beyond InputSemaphore
- 0% idle: No polling, no busy loops

---

## Smalltalk Changes

### InputState Additions

**Class variables:**
- `MouseEventQueue` - SharedQueue new
- `EventSeq` - Integer starting at 0

**Initialization:**

```smalltalk
InputState>>install
    "Initialize and connect the receiver to the hardware. Terminate the old input
    process if any. Dual-semaphore architecture with mouse event queue:
     - InputSemaphore: VM signals on ALL raw input, wakes InputProcess
     - ControllerSemaphore: InputProcess signals after enqueuing mouse event"

    InputProcess == nil ifFalse: [InputProcess terminate].
    self initState.
    InputSemaphore _ Semaphore new.
    ControllerSemaphore _ Semaphore new.
    MouseEventQueue _ SharedQueue new.
    EventSeq _ 0.
    InputProcess _ [self run] newProcess.
    InputProcess priority: Processor lowIOPriority.
    InputProcess resume.
    self primInputSemaphore: InputSemaphore
```

### InputState>>run (Queue-based)

```smalltalk
run
    "Process input events using composite event model with mouse event queue.
     Each InputSemaphore wait corresponds to ONE OS event.
     Dequeue composite event via primInputWord, update state, enqueue mouse events.

     Queue protocol on mouse event:
     1. Update bitState FIRST (so Sensor queries return correct state)
     2. Enqueue MouseEvent with all details
     3. Signal ControllerSemaphore (no handshake needed)

     Controllers use both:
     - bitState for quick polling (anyButtonPressed, cursorPoint)
     - MouseEventQueue for discrete event consumption"

    | event eventType newButtons keyCode isKeyDown x y mouseEvent |
    [true]
        whileTrue:
            [InputSemaphore wait.
            event _ self primInputWord.
            event == nil ifFalse:
                [eventType _ event at: 1.
                eventType = 1 ifTrue:
                    ["Mouse event: update position and buttons, enqueue event"
                    x _ event at: 2.
                    y _ event at: 3.
                    newButtons _ event at: 4.

                    "Determine event kind"
                    mouseEvent _ MouseEvent new.
                    mouseEvent position: x @ y.
                    mouseEvent buttonsMask: newButtons.
                    mouseEvent timestamp: self currentTime.
                    EventSeq _ EventSeq + 1.
                    mouseEvent seq: EventSeq.

                    "Classify event kind based on button transitions"
                    (newButtons bitAnd: 7) ~= (bitState bitAnd: 7)
                        ifTrue: [
                            "Button changed - determine which and direction"
                            mouseEvent kind: (self classifyButtonChange: bitState to: newButtons)]
                        ifFalse: [
                            "Position only - move event"
                            mouseEvent kind: #move].

                    "Update processed state"
                    self setX: x.
                    self setY: y.
                    bitState _ (bitState bitAnd: -8) bitOr: newButtons.

                    "Enqueue and signal"
                    MouseEventQueue nextPut: mouseEvent.
                    ControllerSemaphore signal].

                eventType = 2 ifTrue:
                    ["Keyboard event: process key (unchanged)"
                    keyCode _ event at: 6.
                    isKeyDown _ event at: 7.
                    isKeyDown
                        ifTrue: [self keyAt: keyCode put: 1]
                        ifFalse: [self keyAt: keyCode put: 0]]]]
```

### InputSensor API for Controllers

**Dequeue next mouse event:**

```smalltalk
InputSensor>>nextMouseEvent
    "Answer next mouse event or nil if none."
    ^InputState mouseEventQueue isEmpty
        ifTrue: [nil]
        ifFalse: [InputState mouseEventQueue next]
```

**Event-driven wait:**

```smalltalk
InputSensor>>waitForControllerEvent
    "Block until a mouse event is available; signal comes from InputProcess.
     Return immediately if button pressed (for compatibility with existing code)."
    (self anyButtonPressed or: [self keyboardPressed]) ifTrue: [^self].
    InputState controllerSemaphore wait
```

**Processed state accessors (unchanged semantics):**
- `buttons` - returns bitState
- `cursorPoint` - returns x@y

---

## Controller Consumption Pattern

### General Control Loop

```smalltalk
Controller>>controlLoop
    [self isActive] whileTrue: [
        Sensor waitForControllerEvent.
        [| ev |
         ev _ Sensor nextMouseEvent.
         ev isNil] whileFalse: [
            self handleMouseEvent: ev]
    ]
```

### Drag Example

```smalltalk
Controller>>trackDrag
    [true] whileTrue: [
        Sensor waitForControllerEvent.
        | ev |
        ev _ Sensor nextMouseEvent.
        ev isNil ifTrue: [^self].
        (ev kind = #move) ifTrue: [self updateDragTo: ev position].
        (ev kind = #up and: [ev button = #yellow]) ifTrue: [^self finalizeDrag]
    ]
```

---

## Multiple Controllers

### Natural Serialization

- All controllers waiting on ControllerSemaphore become runnable
- Scheduler runs one at a time
- Each controller dequeues from the same MouseEventQueue
- Events are consumed once

### Active Controller Ownership

- ControlManager designates the active controller to consume events
- Secondary controllers should not consume from the queue unless explicitly registered as listeners

### Fan-Out (If Multiple Consumers Needed)

For advanced scenarios requiring multiple simultaneous consumers:

1. **BroadcastQueue**: InputProcess publishes to per-controller queues
2. **Subscription**: Controllers subscribe; InputProcess pushes to all subscribed queues
3. **Default**: Single active controller consumes the shared queue (preserves classic semantics)

---

## VM Integration

### Composite Event Creation

- Convert each OS mouse message to one MouseEvent struct with fields above
- Enqueue into VM-side queue, deliver via primNextEvent to Smalltalk
- Signal InputSemaphore after enqueue

### Signal Policy

- After enqueue, signal InputSemaphore
- No ControllerSemaphore signal from VM; Smalltalk does it after enqueue to maintain ordering

### Optional Coalescing

- Allow VM to coalesce high-frequency Move events by position/timestamp thresholds
- NEVER coalesce Down/Up/Wheel events
- Keep sequence numbers strictly increasing

---

## Edge Cases and Safeguards

### Backpressure

If controller is slow, mouse queue grows. Safeguards:
- Add max depth limit
- Drop oldest Move events while preserving Down/Up/Wheel
- Prevents unbounded memory growth

### Starvation

Ensure only the active controller dequeues. Others wait on activation or on their own subscribed queue if broadcasting is used.

### Lost Wake

Always signal ControllerSemaphore after enqueue to wake controllers promptly.

### Diagnostics

Enable with flags for troubleshooting:
- `[MOUSE IN] seq=N kind=Down/Up/Move pos=(x,y) mask=...`
- `[MOUSE Q] size=S after enqueue`
- `[CTRL] consumed seq=N kind=... size=S-1`

---

## Implementation Checklist

1. [ ] Add MouseEvent class to Smalltalk-80.st
2. [ ] Add MouseEventQueue to InputState class variables
3. [ ] Add EventSeq to InputState class variables
4. [ ] Update InputState>>install to initialize queue and seq
5. [ ] Update InputState>>run to enqueue mouse events
6. [ ] Add InputState>>classifyButtonChange:to: helper
7. [ ] Add InputState class>>mouseEventQueue accessor
8. [ ] Add InputSensor>>nextMouseEvent
9. [ ] Update InputSensor>>waitForControllerEvent (remove handshake signal)
10. [ ] Remove HandshakeSemaphore (no longer needed)
11. [ ] Update controller loops to consume from queue (if needed)
12. [ ] Add optional VM move coalescing
13. [ ] Add queue depth guard
14. [ ] Add diagnostics flags
15. [ ] Regenerate coldstart bundle
16. [ ] Test: yellow button menu appears reliably
17. [ ] Test: rapid clicks all detected
18. [ ] Test: 0% idle CPU maintained

---

## Benefits Summary

| Aspect | Handshake Model | Queue Model |
|--------|-----------------|-------------|
| Missed events | Possible during menu tracking | None - all buffered |
| Synchronization | Manual (fragile) | Implicit (queue) |
| Complexity | High (3 semaphores) | Low (2 semaphores) |
| Deadlock risk | Yes (if controller busy) | No |
| Keyboard symmetry | No | Yes |
| 0% idle | Yes | Yes |

---

## Files to Modify

### Smalltalk-80 Source/Smalltalk-80.st

1. Add `MouseEvent` class
2. Update `InputState` class variables
3. Update `InputState>>install`
4. Update `InputState>>run`
5. Add `InputState>>classifyButtonChange:to:`
6. Add `InputState class>>mouseEventQueue`
7. Update `InputSensor>>nextMouseEvent`
8. Update `InputSensor>>waitForControllerEvent`
9. Remove `HandshakeSemaphore` references

### VM (C# - if needed)

- May need to update composite event format for wheel support
- Optional: Add move event coalescing

---

## Related Documents

- `CPU-USAGE-ANALYSIS.md` - Previous handshake design (superseded by this document)
- `PROCESS-MANAGEMENT.md` - Process scheduling and semaphore usage
- `PROPOSED-TIMER-OPTIMIZATION.md` - Timer and WaitAny implementation
