# CPU Usage Analysis: Idle Detection and the Yield Problem

**Date**: 2025-12-03
**Status**: ⚠️ SUPERSEDED - See MOUSE-EVENT-QUEUE.md for current design
**Purpose**: Document the idle CPU problem, analysis, and original handshake solution

---

> **IMPORTANT**: The handshake-based design documented below has been superseded by a queue-based architecture.
> The handshake approach caused deadlocks when controllers were busy (e.g., menu tracking).
> See **[MOUSE-EVENT-QUEUE.md](MOUSE-EVENT-QUEUE.md)** for the current queue-based design that fixes these issues.

---

---

## Problem Statement

The Smalltalk-80 interpreter consumed **~3% CPU when completely idle** (gray screen, no user interaction). The goal was **0% CPU** via kernel-level blocking (`WaitHandle.WaitAny`).

---

## Root Cause Analysis

### The Original Yield Implementation

The Blue Book `ProcessorScheduler>>yield` was designed for cooperative multitasking:

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"
    | semaphore |
    semaphore _ Semaphore new.
    [semaphore signal] fork.
    semaphore wait
```

**Step-by-step execution:**

1. Create new Semaphore (excessSignals=0)
2. Fork `[semaphore signal]` - creates new Process at same priority, adds to ready queue
3. Current process calls `semaphore wait`:
   - excessSignals=0, so process is **suspended**
   - Added to semaphore's wait list
   - Scheduler runs `SwitchToNextRunnableProcess`
4. Scheduler finds forked process and activates it
5. Forked process executes `semaphore signal`:
   - Wakes original process, adds it back to ready queue
6. Forked process block finishes, process **terminates**
7. Scheduler runs `SwitchToNextRunnableProcess`
8. Finds original process in ready queue - **NOT the background process**
9. Original process resumes, continues the loop

**Key insight**: At step 5, the signal adds the original process back to the ready queue **before** the forked process terminates. The ready queue at the calling priority is **never empty**.

### The Timing Problem Visualized

```
Time →

Controller:  [yield]─────[wait]═══════════════════════[resume]─[loop]─[yield]...
                           │                              ↑
                           ▼                              │
Forked:                 [run]─[signal]─[terminate]        │
                                  │                       │
                                  └───────────────────────┘
                                  (adds to ready queue)

Ready Queue (priority 4):
  Before yield:  [Controller]
  After wait:    [Forked]
  After signal:  [Forked, Controller]  ← Controller added back!
  After term:    [Controller]          ← Never empty!
```

### Why VM Idle Detection Failed

The VM's idle detection was architecturally correct - it checked if the background process (priority 1) became active:

```csharp
if (activeProcess == _backgroundProcessOOP)
{
    EnterIdleWait(activeProcess);  // Block in WaitAny - true 0% CPU
}
```

But the background process **never became active** because:
1. Controller loops (like `searchForActiveController` and `controlLoop`) call `Processor yield`
2. Due to yield's implementation, priority 4 always has a process in the ready queue
3. Scheduler always picks highest priority, so background (priority 1) never runs

---

## Implemented Solution: Dual-Semaphore Architecture with Handshake

**Date**: 2025-12-03
**Status**: ✅ IMPLEMENTED AND VALIDATED

### Architecture Overview

Three semaphores with distinct purposes:

1. **InputSemaphore** (primitive 93) - VM signals when raw input arrives
   - Only InputProcess (priority 6) waits on this
   - InputProcess reads event words, updates sensor state

2. **ControllerSemaphore** (primitive 160) - InputProcess signals on button state CHANGES
   - Controllers wait on this (via `Sensor waitForControllerEvent`)
   - Signaled only when `bitState bitAnd: 7` changes (not every event)
   - Guarantees controllers see correct button state

3. **HandshakeSemaphore** - Formal synchronization between InputProcess and Controller
   - InputProcess waits on this after signaling ControllerSemaphore
   - Controller signals this immediately upon waking
   - Ensures InputProcess doesn't process more events until controller acknowledges

### Key Design Decision: Processed State vs Polled State

**Critical Fix**: `InputSensor>>buttons` now returns processed state from `bitState`, NOT polled current state:

```smalltalk
InputSensor>>buttons
    "Return button state from processed events (bitState), not polled current state.
     This ensures controllers see button state as of when the event was processed."
    ^CurrentInputState mouseButtons
```

**Why this matters**:
- Primitive 199 (`primMouseButtons`) polls the **current** OS state from the queue
- If user clicks quickly, button may be released by the time controller checks
- Using `bitState` (updated by InputProcess) gives the state **at event processing time**
- Combined with the handshake, controllers ALWAYS see correct button state

### The Handshake Protocol

```
Button State Change Flow:
═══════════════════════════════════════════════════════════════════

1. User presses yellow button
   └─► OS event queued, counting semaphore released

2. VM wakes from WaitAny
   └─► ProcessOneEvent() generates words + sentinel
   └─► SignalSemaphore(InputSemaphore)

3. InputProcess (priority 6) wakes
   └─► Processes words until sentinel (type 6)
   └─► Updates x, y, bitState via nextEvent:with:
   └─► Detects button change: newBits ~= prevBits
   └─► Signals ControllerSemaphore
   └─► Waits on HandshakeSemaphore ◄── BLOCKS HERE

4. Controller (priority 4) wakes
   └─► Signals HandshakeSemaphore ◄── InputProcess unblocked (but not running)
   └─► Checks Sensor yellowButtonPressed
   └─► Returns CurrentInputState mouseButtons = bitState = BUTTON PRESSED ✓
   └─► Shows popup menu...

5. Eventually controller waits on ControllerSemaphore
   └─► InputProcess resumes, processes next events
```

### Implementation Details

#### InputState>>run (composite events + state-change handshake)

```smalltalk
run
    "Process input events using composite event model.
     Each InputSemaphore wait corresponds to ONE OS event.
     Dequeue composite event via primInputWord, update state, signal if buttons changed.

     Handshake protocol on button state change:
     1. Signal ControllerSemaphore (controller added to ready queue)
     2. Wait on HandshakeSemaphore (InputProcess blocks, controller runs)
     3. Controller signals HandshakeSemaphore (InputProcess unblocked)
     4. Controller continues (cooperative multitasking)

     Controllers use bitState (processed state) so they see correct button values."

    | event eventType newButtons keyCode isKeyDown prevBits |
    prevBits _ bitState bitAnd: 7.
    [true]
        whileTrue:
            [InputSemaphore wait.
            event _ self primInputWord.  "Returns Array or nil"
            event == nil ifFalse:
                [eventType _ event at: 1.
                eventType = 1 ifTrue:
                    ["Mouse event: update position and buttons"
                    x _ event at: 2.
                    y _ event at: 3.
                    newButtons _ event at: 4.
                    "Update bitState from button mask"
                    bitState _ (bitState bitAnd: -8) bitOr: newButtons.
                    "Signal controllers if button state changed"
                    newButtons ~= prevBits ifTrue:
                        [prevBits _ newButtons.
                         ControllerSemaphore signal.
                         HandshakeSemaphore wait]].
                eventType = 2 ifTrue:
                    ["Keyboard event: process key"
                    keyCode _ event at: 6.
                    isKeyDown _ event at: 7.
                    isKeyDown
                        ifTrue: [self keyAt: keyCode put: 1]
                        ifFalse: [self keyAt: keyCode put: 0]]]]
```

**Note**: Primitive 95 (`primInputWord`) now returns a composite event Array:
`#(eventType x y buttonMask hasTransition keyCode isKeyDown)` or nil if no events.
This replaces the old word-based format with sentinel parsing.

#### InputState>>install (creates all three semaphores)

```smalltalk
install
    "Initialize and connect the receiver to the hardware. Terminate the old input
    process if any. Dual-semaphore architecture:
     - InputSemaphore: VM signals on ALL raw input, wakes InputProcess
     - ControllerSemaphore: InputProcess signals on button changes, wakes controllers
     - HandshakeSemaphore: Controller signals to acknowledge, InputProcess waits"

    InputProcess == nil ifFalse: [InputProcess terminate].
    self initState.
    InputSemaphore _ Semaphore new.
    ControllerSemaphore _ Semaphore new.
    HandshakeSemaphore _ Semaphore new.
    InputProcess _ [self run] newProcess.
    InputProcess priority: Processor lowIOPriority.
    InputProcess resume.
    self primInputSemaphore: InputSemaphore.
    self primControllerSemaphore: ControllerSemaphore
```

#### InputSensor>>waitForControllerEvent (handshake acknowledgment)

```smalltalk
waitForControllerEvent
    "Block until a controller-relevant event (button transition) occurs.
     Used by controller loops (searchForActiveController, controlLoop).

     Handshake protocol:
     1. InputProcess signals ControllerSemaphore, waits on HandshakeSemaphore
     2. Controller wakes here, immediately signals HandshakeSemaphore
     3. Controller continues (cooperative multitasking - signal doesn't preempt)

     Controllers use bitState (processed state) so they see correct button values.
     Return immediately if user input is active (button pressed or key held)."
    (self anyButtonPressed or: [self keyboardPressed]) ifTrue: [^self].
    InputState controllerSemaphore wait.
    InputState handshakeSemaphore signal
```

### Event Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                       WinForms (UI Thread)                          │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Mouse/Keyboard event arrives                                 │  │
│  │         │                                                      │  │
│  │         ▼                                                      │  │
│  │  SmalltalkEventQueue.Enqueue(event)                           │  │
│  │         │                                                      │  │
│  │         ├── Queue composite event (x, y, buttons, keyCode)    │  │
│  │         └── _eventSemaphore.Release()   ← counting semaphore  │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          VM (C#)                                     │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  WaitHandle.WaitAny([eventSemaphore, timerHandle])            │  │
│  │         │                                                      │  │
│  │         ▼                                                      │  │
│  │  On wake (counting semaphore decrements):                     │  │
│  │    SignalSemaphoreWithPreemption(InputSemaphore)              │  │
│  │    (Event stays in queue - dequeued by primitive 95)          │  │
│  └─────────┼─────────────────────────────────────────────────────┘  │
└────────────┼────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Smalltalk                                      │
│                                                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  InputProcess (priority 6)       ← runs first due to priority │  │
│  │         │                                                      │  │
│  │         ├── InputSemaphore wait                               │  │
│  │         ├── primInputWord → DequeueCompositeEvent()           │  │
│  │         │      └── Returns Array #(type x y buttons ...)      │  │
│  │         │      └── Update x, y, bitState from Array           │  │
│  │         │                                                      │  │
│  │         ├── newButtons ~= prevBits?                           │  │
│  │         │      YES: ┌── ControllerSemaphore signal            │  │
│  │         │           └── HandshakeSemaphore wait ◄── BLOCKS    │  │
│  │         │                        │                             │  │
│  └─────────┼────────────────────────┼───────────────────────────┘  │
│            │                        │                               │
│            │                        ▼                               │
│  ┌─────────┼────────────────────────────────────────────────────┐  │
│  │  Controller (priority 4)        ← runs (InputProcess blocked) │  │
│  │         │                                                      │  │
│  │         ├── (wakes from ControllerSemaphore wait)             │  │
│  │         ├── HandshakeSemaphore signal ◄── InputProcess freed  │  │
│  │         │   (but doesn't run - cooperative multitasking)      │  │
│  │         │                                                      │  │
│  │         ├── Sensor yellowButtonPressed                        │  │
│  │         │   └── buttons → CurrentInputState mouseButtons      │  │
│  │         │   └── Returns bitState bitAnd: 7 = CORRECT STATE ✓  │  │
│  │         │                                                      │  │
│  │         ├── Process button event (show popup menu, etc.)      │  │
│  │         │                                                      │  │
│  │         └── Eventually: ControllerSemaphore wait ◄── BLOCKS   │  │
│  └─────────┼─────────────────────────────────────────────────────┘  │
│            │                                                         │
│            ▼                                                         │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  InputProcess resumes           ← (highest priority ready)    │  │
│  │         │                                                      │  │
│  │         └── Continue to next InputSemaphore wait              │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Why This Works

1. **Counting semaphore**: One OS event = one semaphore release = one InputSemaphore wait
2. **Composite events**: Each event is a single Array - no sentinel parsing needed
3. **State-change detection**: Handshake only on `(bitState bitAnd: 7)` changes
4. **Processed state**: `buttons` returns bitState, not polled current state
5. **Handshake protocol**: Formal synchronization without priority tricks
6. **Cooperative multitasking**: Smalltalk `signal` doesn't preempt - only `wait` does
7. **State consistency**: Controllers ALWAYS see correct button state
8. **True 0% CPU**: When idle, all processes block on their respective semaphores

### Blue Book Compliance

**Validated by smalltalk-bluebook-expert**: ✅ **FULLY COMPLIANT**

- All InputSensor protocol messages present and correct
- Button bit encoding matches Blue Book (red=4, yellow=2, blue=1)
- All controller and menu code works unchanged
- Proper Blue Book semaphore protocol usage
- Non-breaking extension (existing polling code continues to work)

---

## Files Modified

### Smalltalk-80 Source/Smalltalk-80.st

1. **InputState class variables** - `ControllerSemaphore`, `HandshakeSemaphore`
2. **InputState>>install** - Creates all three semaphores
3. **InputState>>primInputWord** - Returns composite event Array (primitive 95)
4. **InputState>>run** - Uses composite events, no inner loop/sentinel
5. **InputState>>primControllerSemaphore:** - Registers ControllerSemaphore (primitive 160)
6. **InputState class>>controllerSemaphore** - Accessor
7. **InputState class>>handshakeSemaphore** - Accessor
8. **InputState class>>inputSemaphore** - Accessor
9. **InputSensor>>buttons** - Returns `CurrentInputState mouseButtons` (processed state)
10. **InputSensor>>waitForControllerEvent** - Waits + handshake signal
11. **InputSensor>>waitForEvent** - Waits on InputSemaphore
12. **ControlManager>>searchForActiveController** - Uses `Sensor waitForControllerEvent`
13. **ProcessorScheduler>>yield** - Calls `Sensor waitForEvent`

### VM (C#)

- **SmalltalkEventQueue.cs** - Counting Semaphore, `DequeueCompositeEvent()` returns event Array
- **VirtualMachine.cs** - `SignalInputSemaphoreIfRegistered` signals InputSemaphore only
- **IPlatformBridge.cs** - `DequeueCompositeEvent()`, `SetControllerSemaphore`, `GetControllerSemaphore`
- **WinFormsDisplayProvider.cs** - Delegates to event queue for composite events
- **InputPrimitives.cs** - Primitive 95 returns composite Array, primitive 160 for controller semaphore
- **PrimitiveRegistry.cs** - Registered primitives 95, 160, 199

---

## Testing

After regenerating coldstart bundle:

1. **Initial idle**: Start interpreter, leave at gray screen → verify 0% CPU
2. **With windows**: Open System Browser, leave idle → verify 0% CPU
3. **Multiple windows**: Open several windows, leave idle → verify 0% CPU
4. **Responsiveness**: Move mouse while idle → verify immediate wake
5. **Mouse transitions**: Click buttons → verify no missed down/up events
6. **Yellow button menu**: Right-click → verify popup menu appears reliably
7. **Timer events**: Verify timer-based operations still work
8. **Rapid clicking**: Click rapidly → verify all transitions detected

---

## Smalltalk Process Scheduling Reference

### Priority Levels

```smalltalk
ProcessorScheduler class>>initialize
    SystemRockBottomPriority _ 1.    "Background process"
    SystemBackgroundPriority _ 2.
    UserBackgroundPriority _ 3.
    UserSchedulingPriority _ 4.      "Controller processes"
    UserInterruptPriority _ 5.
    LowIOPriority _ 6.               "InputProcess"
    HighIOPriority _ 7.
    TimingPriority _ 8
```

### Key Processes

- **Background process** (priority 1): `[true] whileTrue: []` - idle sentinel
- **Controller process** (priority 4): Runs `searchForActiveController` loop
- **InputProcess** (priority 6): Waits on InputSemaphore, processes events, signals ControllerSemaphore

### Process Selection

The scheduler always picks the highest priority process with a non-empty ready queue. The dual-semaphore architecture with handshake leverages this to ensure correct ordering and state consistency.
