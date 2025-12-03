# CPU Usage Analysis: Idle Detection and the Yield Problem

**Date**: 2025-12-03
**Status**: ✅ RESOLVED - Dual-semaphore architecture implemented
**Purpose**: Document the idle CPU problem, analysis, and implemented solution

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

## Solutions Explored and Discarded

### Solution 1: VM-Level Yield-Spin Throttling

**Approach**: Detect rapid process cycling and introduce `Thread.Sleep` delays.

**Why discarded**:
- Arbitrary heuristic (when to throttle?)
- Doesn't achieve true 0% CPU
- Adds latency to responsive operations
- Masks the real problem rather than fixing it

**Status**: IMPLEMENTED AND REVERTED

### Solution 2: Modify Individual Call Sites

**Approach**: Change `searchForActiveController` and `controlLoop` to use `Sensor waitForEvent` instead of `Processor yield`.

**Why discarded**:
- Requires modifying each call site individually
- Doesn't fix other yield callers (Rectangle>>fromUser, ScrollController>>scroll, etc.)
- More invasive changes to Blue Book code

**Status**: IMPLEMENTED THEN SUPERSEDED by central fix

### Solution 3: Background Process Detection Only

**Approach**: Detect when background process is active and block.

**Why it fails**: Background process **never becomes active** due to yield keeping higher priorities occupied.

**Status**: INFRASTRUCTURE EXISTS but never triggers without yield fix

### Solution 4: Single Semaphore with SignalAllWaiters

**Approach**: Both InputProcess and controllers wait on InputSemaphore. VM signals all waiters on input arrival.

**Why discarded**:
- Missed mouse down/up transitions due to contention
- When controller wakes first, InputProcess hasn't updated sensor state yet
- Race condition between InputProcess and controller waking

**Status**: IMPLEMENTED AND SUPERSEDED by dual-semaphore architecture

---

## Implemented Solution: Dual-Semaphore Architecture

**Date**: 2025-12-03
**Status**: ✅ IMPLEMENTED

### Architecture Overview

Two separate semaphores with different purposes:

1. **InputSemaphore** (primitive 93) - VM signals this when raw input arrives
   - Only InputProcess (priority 6) waits on this
   - InputProcess reads event, updates sensor state

2. **ControllerSemaphore** (primitive 160) - VM signals this on button transitions
   - Controllers wait on this (via Sensor waitForControllerEvent)
   - Signaled AFTER InputProcess updates state (priority ordering)
   - Guarantees controllers see correct button/key state

### Priority-Based Ordering

The priority system guarantees correct ordering:

1. **VM signals InputSemaphore** on raw input arrival
2. **InputProcess (priority 6)** wakes, reads event, updates sensor state
3. **InputProcess signals ControllerSemaphore**
4. **Controllers (priority 4)** wake with correct state

Since InputProcess runs at priority 6 and controllers at priority 4, InputProcess always completes before any controller runs.

### The Fix

Controller loops use `waitForControllerEvent` which waits on ControllerSemaphore:

```smalltalk
ControlManager>>searchForActiveController
    "Find a scheduled controller that wants control."
    | aController |
    activeController _ nil.
    activeControllerProcess _ Processor activeProcess.
    [aController _ scheduledControllers
        detect: [:c | c isControlWanted and: [c ~~ screenController]]
        ifNone: [screenController isControlWanted
                    ifTrue: [screenController]
                    ifFalse: [nil]].
    aController isNil]
        whileTrue: [Sensor waitForControllerEvent].
    self activeController: aController.
    Processor terminateActive

ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run."
    Sensor waitForEvent
```

### Supporting Methods

#### InputState class variables

```smalltalk
Object subclass: #InputState
    instanceVariableNames: 'x y bitState ...'
    classVariableNames: 'BitMax BitMin ControllerSemaphore CtrlKey InputProcess InputSemaphore LockKey LshiftKey RshiftKey '
    poolDictionaries: ''
    category: 'System-Support'
```

#### InputState>>install (creates both semaphores)

```smalltalk
install
    "Initialize and connect the receiver to the hardware.
     Terminate the old input process if any."
    InputProcess == nil ifFalse: [InputProcess terminate].
    self initState.
    InputSemaphore _ Semaphore new.
    ControllerSemaphore _ Semaphore new.
    InputProcess _ [self run] newProcess.
    InputProcess priority: Processor lowIOPriority.
    InputProcess resume.
    self primInputSemaphore: InputSemaphore
```

#### InputState>>run (counting semaphore + sentinel + yieldToControllers)

```smalltalk
run
    "Process input events using counting semaphore with end-of-event sentinel.
     Each InputSemaphore wait corresponds to ONE OS event (via counting semaphore).
     Process all words for that event until sentinel (type 6) or 0.
     After button events, yield to let controllers see correct state."

    | word type param |
    [true]
        whileTrue:
            [InputSemaphore wait.
            "Process all words for THIS event until sentinel (type 6) or end (0)"
            [word _ self primInputWord.
             word ~= 0 and: [(word bitShift: -12) ~= 6]]
                whileTrue:
                    [type _ word bitShift: -12.
                    param _ word bitAnd: 4095.
                    type = 1
                        ifTrue: [x _ param "Mouse X"]
                        ifFalse:
                            [type = 2
                                ifTrue: [y _ param "Mouse Y"]
                                ifFalse:
                                    [self nextEvent: type with: param.
                                     "After button event: yield to controllers"
                                     ((type = 3 or: [type = 4]) and:
                                        [param >= BitMin and: [param <= (BitMin + 2)]]) ifTrue:
                                        [self yieldToControllers]]]]]

yieldToControllers
    "Suspend InputProcess briefly to let controllers (priority 4) run.
     Fork a low-priority process (priority 3) to wake us after controllers
     have had a chance to process the button state."

    | sem proc |
    sem _ Semaphore new.
    proc _ [sem signal] newProcess.
    proc priority: Processor userBackgroundPriority.  "priority 3, below controllers at 4"
    proc resume.
    sem wait
```

#### InputState class>>controllerSemaphore

```smalltalk
controllerSemaphore
    "Answer the semaphore that is signaled by InputProcess after processing events.
     Used by Processor yield (via Sensor waitForEvent) for true idle suspension.
     Separate from InputSemaphore to avoid contention with InputProcess."
    ^ControllerSemaphore
```

#### InputSensor wait methods

```smalltalk
InputSensor>>waitForControllerEvent
    "Block until a controller-relevant event (button transition) occurs.
     Used by controller loops (searchForActiveController, controlLoop).
     VM signals ControllerSemaphore on button transitions.

     IMPORTANT: Return immediately if user input is active."
    (self anyButtonPressed or: [self keyboardPressed]) ifTrue: [^self].
    InputState controllerSemaphore wait

InputSensor>>waitForEvent
    "Block until any input event arrives.
     Used by general yield callers. VM signals InputSemaphore on raw input."
    InputState inputSemaphore wait
```

### Event Flow Diagram (Counting Semaphore + Sentinel + yieldToControllers)

```
┌─────────────────────────────────────────────────────────────────────┐
│                       WinForms (UI Thread)                          │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Mouse/Keyboard event arrives                                 │  │
│  │         │                                                      │  │
│  │         ▼                                                      │  │
│  │  SmalltalkEventQueue.Enqueue(event)                           │  │
│  │         │                                                      │  │
│  │         ├── Queue event (mouseX, mouseY, buttons)             │  │
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
│  │    ProcessOneEvent() → generates words + sentinel (type 6)    │  │
│  │    SignalSemaphoreWithPreemption(InputSemaphore)              │  │
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
│  │         ├── Inner loop: read words until sentinel (type 6)    │  │
│  │         │      ├── Update x, y, bitState via nextEvent        │  │
│  │         │      └── If button event:                           │  │
│  │         │              ├── ControllerSemaphore signal         │  │
│  │         │              └── yieldToControllers (forks pri 3)   │  │
│  │         │                        │                            │  │
│  │         │                        ▼                            │  │
│  │         │              InputProcess SUSPENDS on private sem   │  │
│  └─────────┼────────────────────────────────────────────────────┘  │
│            │                                                        │
│            ▼                                                        │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  Controllers (priority 4)       ← runs while InputProcess     │  │
│  │         │                        suspended                    │  │
│  │         ├── Sensor waitForControllerEvent                     │  │
│  │         └── InputState controllerSemaphore wait               │  │
│  │                                                               │  │
│  │  (Wake with CORRECT sensor state - no missed transitions)    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Why This Works

1. **Counting semaphore**: One OS event = one semaphore release = one InputSemaphore wait
2. **Sentinel-based processing**: Type 6 marks end of one OS event's words
3. **yieldToControllers**: After button events, InputProcess suspends to let controllers run
4. **Priority fork trick**: Low-priority (3) forked process wakes InputProcess AFTER controller (4)
5. **State consistency**: Controllers always see correct button state (down vs up)
6. **No missed transitions**: Each button down/up triggers controller wake
7. **True 0% CPU**: When idle, all processes block on their respective semaphores

### Call Sites Automatically Fixed

- `ControlManager>>searchForActiveController` - controller scheduling loop
- `Controller>>controlLoop` - active controller polling
- `Rectangle>>fromUser` - interactive rectangle selection
- `ScrollController>>scroll` - scroll bar handling
- `StandardSystemView` frame selection - window resize/move
- Any other existing or future `Processor yield` calls

### Expected Behavior

- **Idle CPU**: 0% (blocked in WaitAny → semaphore waits)
- **Responsiveness**: Immediate wake on mouse/keyboard/timer events
- **Reliability**: No missed mouse down/up transitions
- **Compatibility**: All existing yield callers work without modification

---

## Files Modified

### Smalltalk-80 Source/Smalltalk-80.st

1. **InputState class variables** - Added `ControllerSemaphore`
2. **InputState>>install** - Creates and registers both semaphores (primitives 93 & 160)
3. **InputState>>primControllerSemaphore:** - Registers ControllerSemaphore (primitive 160)
4. **InputState>>run** - Uses counting semaphore + sentinel + yieldToControllers
5. **InputState>>yieldToControllers** - Forks low-priority process to wake InputProcess after controllers
6. **InputState>>nextEvent:with:** - Signals ControllerSemaphore after button events
7. **InputState class>>controllerSemaphore** - Accessor
8. **InputState class>>inputSemaphore** - Accessor
9. **InputSensor>>waitForControllerEvent** - Waits on ControllerSemaphore (for controllers)
10. **InputSensor>>waitForEvent** - Waits on InputSemaphore (for general yield)
11. **ControlManager>>searchForActiveController** - Uses `Sensor waitForControllerEvent`
12. **ProcessorScheduler>>yield** - Calls `Sensor waitForEvent`

### VM (C#)

- **SmalltalkEventQueue.cs** - Uses counting Semaphore, ProcessOneEvent adds sentinel (type 6)
- **VirtualMachine.cs** - `SignalInputSemaphoreIfRegistered` signals InputSemaphore only (ControllerSemaphore signaled by Smalltalk)
- **IPlatformBridge.cs** - Added `SetControllerSemaphore`, `GetControllerSemaphore`, `ConsumeButtonTransition`
- **WinFormsDisplayProvider.cs** - Implements controller semaphore registration
- **InputPrimitives.cs** - Added primitive 160 for controller semaphore registration
- **PrimitiveRegistry.cs** - Registered primitive 160

---

## Testing

After regenerating coldstart bundle:

1. **Initial idle**: Start interpreter, leave at gray screen → verify 0% CPU
2. **With windows**: Open System Browser, leave idle → verify 0% CPU
3. **Multiple windows**: Open several windows, leave idle → verify 0% CPU
4. **Responsiveness**: Move mouse while idle → verify immediate wake
5. **Mouse transitions**: Click buttons → verify no missed down/up events
6. **Yellow button menu**: Right-click → verify popup menu appears
7. **Timer events**: Verify timer-based operations still work

---

## Rollback Instructions

To revert to original Blue Book yield behavior (will restore ~3% idle CPU and potential missed events):

1. Restore original ProcessorScheduler>>yield:
```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"
    | semaphore |
    semaphore _ Semaphore new.
    [semaphore signal] fork.
    semaphore wait
```

2. Remove ControllerSemaphore from InputState class variables
3. Remove ControllerSemaphore signal from InputState>>run
4. Remove InputState class>>controllerSemaphore method

The `InputState class>>inputSemaphore` and original `InputSensor>>waitForEvent` methods can remain.

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

The scheduler always picks the highest priority process with a non-empty ready queue. The dual-semaphore architecture leverages this to ensure correct ordering.
