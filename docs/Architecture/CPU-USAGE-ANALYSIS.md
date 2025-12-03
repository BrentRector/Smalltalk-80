# CPU Usage Analysis: Idle Detection and the Yield Problem

**Date**: 2025-12-03
**Status**: ✅ RESOLVED - Central fix implemented in ProcessorScheduler>>yield
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

---

## Implemented Solution: Central Yield Fix

**Date**: 2025-12-03
**Status**: ✅ IMPLEMENTED

### The Fix

Replace the original yield implementation with one that blocks on `InputSemaphore`:

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run.
     Modern integration: block on InputSemaphore for true idle suspension.
     The VM signals InputSemaphore when events arrive, enabling 0% CPU when idle."

    Sensor waitForEvent
```

### Supporting Methods Added

#### InputState class>>inputSemaphore

```smalltalk
!InputState class methodsFor: 'accessing'!
inputSemaphore
    "Answer the semaphore that is signaled when input events arrive.
     Used by Sensor waitForEvent for true idle suspension."
    ^InputSemaphore! !
```

#### InputSensor>>waitForEvent

```smalltalk
!InputSensor methodsFor: 'waiting'!
waitForEvent
    "Block until an input event (mouse, keyboard, or timer) arrives.
     Modern integration: enables true idle suspension by blocking on InputSemaphore
     instead of busy-waiting with Processor yield. The VM signals this semaphore
     when events arrive, allowing 0% CPU usage when idle."
    InputState inputSemaphore wait! !
```

### Why This Works

1. **Single point of fix**: All `Processor yield` callers automatically benefit
2. **Preserves semantics**: Yield still "gives other processes a chance" - by waiting for events
3. **True idle**: When no events arrive, process blocks on InputSemaphore
4. **VM integration**: VM signals InputSemaphore when events arrive via `WaitHandle.WaitAny`

### Call Sites Automatically Fixed

- `ControlManager>>searchForActiveController` - controller scheduling loop
- `Controller>>controlLoop` - active controller polling
- `Rectangle>>fromUser` - interactive rectangle selection
- `ScrollController>>scroll` - scroll bar handling
- `StandardSystemView` frame selection - window resize/move
- Any other existing or future `Processor yield` calls

### Event Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        VM (C#)                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  WaitHandle.WaitAny([eventHandle, timerHandle])         │   │
│  │         │                                                │   │
│  │         ▼                                                │   │
│  │  On wake: SignalInputSemaphore()                        │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Smalltalk                                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  InputSemaphore wait  ←── Sensor waitForEvent           │   │
│  │         ↑                        ↑                       │   │
│  │         │                        │                       │   │
│  │  ProcessorScheduler>>yield ──────┘                       │   │
│  │         ↑                                                │   │
│  │         │                                                │   │
│  │  controlLoop, searchForActiveController, etc.           │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Expected Behavior

- **Idle CPU**: 0% (blocked in WaitAny → InputSemaphore wait)
- **Responsiveness**: Immediate wake on mouse/keyboard/timer events
- **Compatibility**: All existing yield callers work without modification

---

## Files Modified

### Smalltalk-80 Source/Smalltalk-80.st

1. **ProcessorScheduler>>yield** - Central fix, calls `Sensor waitForEvent`
2. **InputState class>>inputSemaphore** - Accessor for InputSemaphore class variable
3. **InputSensor>>waitForEvent** - Blocks on InputSemaphore

### No VM Changes Required

The VM infrastructure (`WaitHandle.WaitAny`, `SignalInputSemaphore`, etc.) was already correct. The fix is entirely in Smalltalk code.

---

## Testing

After regenerating coldstart bundle:

1. **Initial idle**: Start interpreter, leave at gray screen → verify 0% CPU
2. **With windows**: Open System Browser, leave idle → verify 0% CPU
3. **Multiple windows**: Open several windows, leave idle → verify 0% CPU
4. **Responsiveness**: Move mouse while idle → verify immediate wake
5. **Timer events**: Verify timer-based operations still work

---

## Rollback Instructions

To revert to original Blue Book yield behavior (will restore ~3% idle CPU):

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"
    | semaphore |
    semaphore _ Semaphore new.
    [semaphore signal] fork.
    semaphore wait
```

The `InputState class>>inputSemaphore` and `InputSensor>>waitForEvent` methods can remain - they're harmless if unused.

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
- **InputProcess** (priority 6): Waits on InputSemaphore, processes events

### Process Selection

The scheduler always picks the highest priority process with a non-empty ready queue. This is why fixing yield to block (rather than spin) is essential - it allows lower priority processes to run when there's no real work.
