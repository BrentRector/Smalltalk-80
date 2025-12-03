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

1. **InputSemaphore** - VM signals this when raw input arrives
   - Only InputProcess (priority 6) waits on this
   - InputProcess reads event, updates sensor state

2. **ControllerSemaphore** - InputProcess signals this after updating state
   - Controllers wait on this (via Processor yield → Sensor waitForEvent)
   - Guarantees controllers see correct button/key state

### Priority-Based Ordering

The priority system guarantees correct ordering:

1. **VM signals InputSemaphore** on raw input arrival
2. **InputProcess (priority 6)** wakes, reads event, updates sensor state
3. **InputProcess signals ControllerSemaphore**
4. **Controllers (priority 4)** wake with correct state

Since InputProcess runs at priority 6 and controllers at priority 4, InputProcess always completes before any controller runs.

### The Fix

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run.
     Modern integration: block on ControllerSemaphore for true idle suspension.
     InputProcess signals this semaphore AFTER updating sensor state."
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

#### InputState>>run (signals ControllerSemaphore after each event)

```smalltalk
run
    "This is the loop that actually processes input events.
     After processing each event, signal ControllerSemaphore to wake
     any controllers waiting via Processor yield."
    | word type param |
    [true]
        whileTrue:
            [InputSemaphore wait.
            "Test for mouse X/Y events here to avoid an activation."
            word _ self primInputWord.
            type _ word bitShift: -12.
            param _ word bitAnd: 4095.
            type = 1
                ifTrue:
                    [x _ param "Mouse X"]
                ifFalse:
                    [type = 2
                        ifTrue: [y _ param "Mouse Y"]
                        ifFalse: [self nextEvent: type with: param]].
            "Signal controller semaphore after updating sensor state.
             This ensures controllers see correct state when they wake."
            ControllerSemaphore signal]
```

#### InputState class>>controllerSemaphore

```smalltalk
controllerSemaphore
    "Answer the semaphore that is signaled by InputProcess after processing events.
     Used by Processor yield (via Sensor waitForEvent) for true idle suspension.
     Separate from InputSemaphore to avoid contention with InputProcess."
    ^ControllerSemaphore
```

#### InputSensor>>waitForEvent

```smalltalk
waitForEvent
    "Block until an input event arrives.
     Modern integration: enables true idle suspension by blocking on ControllerSemaphore.
     InputProcess signals this semaphore AFTER updating sensor state, ensuring
     controllers always see correct button/key state when they wake.

     Ordering guarantee:
     1. VM signals InputSemaphore on raw input arrival
     2. InputProcess (priority 6) wakes, updates sensor state
     3. InputProcess signals ControllerSemaphore
     4. Controllers (priority 4) wake with correct state

     IMPORTANT: Return immediately if user input is active (button pressed or key held).
     This allows controller loops to keep processing continuous input states."
    (self anyButtonPressed or: [self keyboardPressed]) ifTrue: [^self].
    InputState controllerSemaphore wait
```

### Event Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          VM (C#)                                     │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  WaitHandle.WaitAny([eventHandle, timerHandle])               │  │
│  │         │                                                      │  │
│  │         ▼                                                      │  │
│  │  On wake: SignalSemaphoreWithPreemption(InputSemaphore)       │  │
│  │         │  (standard signaling - wakes ONE waiter)            │  │
│  └─────────┼─────────────────────────────────────────────────────┘  │
└────────────┼────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Smalltalk                                      │
│                                                                      │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │  InputProcess (priority 6)                                     │  │
│  │         │                                                      │  │
│  │         ├── InputSemaphore wait                               │  │
│  │         ├── primInputWord (read raw event)                    │  │
│  │         ├── Update x, y, bitState, etc.                       │  │
│  │         └── ControllerSemaphore signal ─────────────┐         │  │
│  └─────────────────────────────────────────────────────┼─────────┘  │
│                                                         │            │
│  ┌──────────────────────────────────────────────────────┼────────┐  │
│  │  Controllers (priority 4)                            ▼        │  │
│  │         │                                                      │  │
│  │         ├── Processor yield                                   │  │
│  │         ├── Sensor waitForEvent                               │  │
│  │         └── InputState controllerSemaphore wait               │  │
│  │                                                               │  │
│  │  (Wake with CORRECT sensor state - no missed transitions)    │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Why This Works

1. **No contention**: InputProcess and controllers wait on different semaphores
2. **Ordering guaranteed**: Priority 6 > Priority 4, so InputProcess always runs first
3. **State consistency**: Controllers always wake after sensor state is updated
4. **No missed transitions**: Button down/up events are never missed
5. **True 0% CPU**: When idle, both processes block on their respective semaphores

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
2. **InputState>>install** - Creates both semaphores
3. **InputState>>run** - Signals ControllerSemaphore after each event
4. **InputState class>>controllerSemaphore** - Accessor
5. **InputState class>>inputSemaphore** - Accessor (for InputProcess use)
6. **InputSensor>>waitForEvent** - Waits on ControllerSemaphore
7. **ProcessorScheduler>>yield** - Calls `Sensor waitForEvent`

### VirtualMachine.cs

- **SignalInputSemaphoreIfRegistered** - Uses standard `SignalSemaphoreWithPreemption` (not SignalAllWaiters)

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
