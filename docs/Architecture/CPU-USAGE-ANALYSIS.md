# CPU Usage Analysis: Why Idle Detection Fails

**Date**: 2025-12-03
**Status**: UNRESOLVED - Fundamental architecture issue identified
**Purpose**: Brief another LLM on the problem, analysis, and discarded solutions

---

## Problem Statement

The Smalltalk-80 interpreter consumes **~3% CPU when completely idle** (gray screen, no user interaction). The goal is **0% CPU** via kernel-level blocking (`WaitHandle.WaitAny`).

---

## Current VM Architecture

### The Intended Idle Detection

The VM attempts to detect idle by checking if the **background process** (priority 1) becomes active:

```csharp
// VirtualMachine.cs - InterpreterLoop (lines 319-337)
while (true)
{
    // Check for idle EVERY iteration - when background process is active,
    // block in WaitAny until a real process switch occurs.
    if (_platformBridge != null && _backgroundProcessOOP != default)
    {
        var activeProcess = GetActiveProcess();
        if (activeProcess == _backgroundProcessOOP)
        {
            // Idle detected - block until process switch (true 0% CPU)
            EnterIdleWait(activeProcess);
            continue;  // Skip bytecode execution
        }
    }

    // ... execute bytecodes ...
}
```

### The Background Process

Created by `ProcessorScheduler>>hiddenBackgroundProcess`:

```smalltalk
hiddenBackgroundProcess
    "Install a default background process which is invisible."
    self background: [[true] whileTrue: []]
```

This creates a Process at **priority 1** (lowest) that runs an infinite empty loop. The **intent** is that when no other processes are runnable, the scheduler selects this process, and the VM can detect "idle".

### The Idle Wait Loop

When idle is detected, the VM blocks on kernel handles:

```csharp
// VirtualMachine.cs - EnterIdleWait (lines 379-453)
private void EnterIdleWait(SmalltalkOOP backgroundProcess)
{
    var eventHandle = _platformBridge?.EventAvailableHandle;
    var timerHandle = _timerManager.TimerExpiredHandle;

    while (true)
    {
        CheckAndSignalExpiredTimer();  // May wake a process

        var currentProcess = GetActiveProcess();
        if (currentProcess != backgroundProcess)
            return;  // Process switch occurred - exit idle

        // Block indefinitely (0% CPU)
        var handles = new WaitHandle[] { eventHandle, timerHandle, ShutdownToken.WaitHandle };
        int waitResult = WaitHandle.WaitAny(handles);

        if (waitResult == 0)  // Event handle
        {
            SignalInputSemaphoreIfRegistered();  // Wake InputProcess
            _platformBridge!.ForceResetEventSignal();
        }
    }
}
```

---

## Why Idle Detection NEVER Triggers

### The Fundamental Flaw

The VM checks `GetActiveProcess() == backgroundProcess`, but **this condition is never true** because:

1. `ControlManager>>searchForActiveController` runs at priority 4
2. It loops calling `Processor yield`
3. Due to how `yield` works, there's **always** a priority 4 process in the ready queue
4. The scheduler **never** selects the background process (priority 1)

### The Controller Loop

```smalltalk
ControlManager>>searchForActiveController
    "Find a scheduled controller that wants control and give control to it."

    | aController |
    activeController _ nil.
    activeControllerProcess _ Processor activeProcess.
    [Processor yield.
     aController _ scheduledControllers
        detect: [:aController | aController isControlWanted and: [aController ~~ screenController]]
        ifNone: [screenController isControlWanted
                    ifTrue: [screenController]
                    ifFalse: [nil]].
     aController isNil]
        whileTrue.
    self activeController: aController.
    Processor terminateActive
```

This loop:
1. Calls `Processor yield`
2. Checks if any controller wants control (`isControlWanted`)
3. If none found, loops back

When no controller wants control (e.g., mouse not over any view on gray screen), **this loops indefinitely**.

### The Yield Implementation

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
2. Fork `[semaphore signal]` - creates new Process at priority 4, adds to ready queue
3. Current process calls `semaphore wait`:
   - excessSignals=0, so process is **suspended**
   - Added to semaphore's wait list
   - Scheduler runs `SwitchToNextRunnableProcess`
4. Scheduler finds forked process (priority 4) and activates it
5. Forked process executes `semaphore signal`:
   - Wakes original process, adds it back to ready queue
6. Forked process block finishes, process **terminates**
7. Scheduler runs `SwitchToNextRunnableProcess`
8. Finds original process (priority 4) in ready queue - **NOT the background process**
9. Original process resumes, continues the loop

**Key insight**: At step 5, the signal adds the original process back to the ready queue **before** the forked process terminates. So at step 7, there's always a priority 4 process available.

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

The ready queue at priority 4 is **never empty**, so the background process (priority 1) is **never selected**.

---

## Smalltalk Process Scheduling Architecture

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

### Process Selection (VM)

```csharp
// VirtualMachine.ProcessScheduling.cs - FindHighestPriorityRunnableProcess
public SmalltalkOOP FindHighestPriorityRunnableProcess()
{
    // Search from highest priority (8) down to lowest (1)
    for (int priority = 8; priority >= 1; priority--)
    {
        var priorityListOOP = _heapManager.GetIndexedOOP(processListsOOP, priority - 1);
        var firstLinkOOP = _heapManager.GetInstanceVariable(priorityListOOP,
            VMConstants.LINKEDLIST_FIRSTLINK_INDEX);

        if (firstLinkOOP != _metadata.KnownObjects.Nil)
            return firstLinkOOP;  // Found a runnable process
    }
    return _metadata.KnownObjects.Nil;  // No runnable process (shouldn't happen)
}
```

The scheduler always picks the **highest priority** process. Background (priority 1) only runs when priorities 2-8 are all empty.

### The InputProcess

```smalltalk
InputState>>install
    "Initialize and connect the receiver to the hardware."
    InputProcess == nil ifFalse: [InputProcess terminate].
    self initState.
    InputSemaphore _ Semaphore new.
    InputProcess _ [self run] newProcess.
    InputProcess priority: Processor lowIOPriority.  "Priority 6"
    InputProcess resume.
    self primInputSemaphore: InputSemaphore

InputState>>run
    "Process input events. Waits on InputSemaphore."
    | word type param |
    [true] whileTrue:
        [InputSemaphore wait.  "Blocks here until VM signals"
         word _ self primInputWord.
         "... process event ..."]
```

The InputProcess at priority 6 **does** block correctly on `InputSemaphore`. The VM signals this when events arrive. But this doesn't help because:
- InputProcess wakes, processes event, waits again
- Controller at priority 4 keeps running its yield loop
- Background at priority 1 never selected

---

## Solutions Explored and Discarded

### Solution 1: Background Process Detection (Current Implementation)

**Approach**: Detect when `GetActiveProcess() == backgroundProcess`, then block in `WaitAny`.

**Why it fails**: Background process **never becomes active** due to the yield loop keeping priority 4 always occupied.

**Status**: DOES NOT WORK

### Solution 2: VM-Level Yield-Spin Throttling

**Approach**: Detect rapid process cycling and introduce `Thread.Sleep` delays.

```csharp
private void ThrottleIfYieldSpinning()
{
    bool hasRecentInput = _platformBridge?.HasPendingEvents() ?? false;
    if (hasRecentInput)
    {
        _lastInputEventTime = Environment.TickCount64;
        return;
    }

    long timeSinceLastInput = Environment.TickCount64 - _lastInputEventTime;
    if (timeSinceLastInput > 100)  // No input for 100ms
    {
        Thread.Sleep(5);  // Throttle
    }
}
```

**Why discarded**:
- Arbitrary heuristic (when to throttle?)
- Doesn't achieve true 0% CPU
- Adds latency to responsive operations
- Masks the real problem rather than fixing it

**Status**: IMPLEMENTED AND REVERTED

### Solution 3: Modify Smalltalk Code

**Approach**: Change `searchForActiveController` to wait on a semaphore instead of polling with yield.

```smalltalk
"Hypothetical fix - NOT IMPLEMENTED"
searchForActiveController
    | aController |
    [aController _ self findControllerWantingControl.
     aController isNil] whileTrue:
        [Sensor waitForEvent].  "Block on input semaphore"
    self activeController: aController
```

**Why not pursued**:
- Smalltalk-80.st is canonical Blue Book source
- Modifying it changes semantics
- May break compatibility

**Status**: NOT ATTEMPTED (policy decision)

### Solution 4: Enhanced Wait Primitive

**Approach**: Modify primitive 86 (wait) to detect "yield waits" (semaphore signaled immediately) and track them separately.

**Why not pursued**:
- Complex to implement
- Requires understanding Smalltalk-level patterns at VM level
- Fragile - depends on yield implementation details

**Status**: NOT ATTEMPTED

---

## Current State of the Code

### VirtualMachine.cs - InterpreterLoop

```csharp
private SmalltalkOOP InterpreterLoop()
{
    _initialContextOOP = _contextManager.ActiveContext;
    _initialProcessOOP = GetActiveProcess();

    // Cache background process OOP for idle detection
    _backgroundProcessOOP = FindBackgroundProcess();
    if (_backgroundProcessOOP != default)
    {
        Console.WriteLine($"[InterpreterLoop] Background process detected: {_backgroundProcessOOP}");
    }

    int periodicCheckCounter = 0;
    const int PeriodicCheckInterval = 1000;

    while (true)
    {
        // Check for idle EVERY iteration
        // NOTE: This check NEVER succeeds because yield keeps priority 4 active
        if (_platformBridge != null && _backgroundProcessOOP != default)
        {
            var activeProcess = GetActiveProcess();
            if (activeProcess == _backgroundProcessOOP)
            {
                EnterIdleWait(activeProcess);  // Never reached!
                continue;
            }
        }

        if (++periodicCheckCounter >= PeriodicCheckInterval)
        {
            periodicCheckCounter = 0;
            if (ShutdownToken.IsCancellationRequested)
                throw new OperationCanceledException();
            CheckAndSignalExpiredTimer();
        }

        InterpreterStatus status = ExecuteBytecodeStep();
        // ... handle status ...
    }
}
```

### VirtualMachine.cs - EnterIdleWait

```csharp
private void EnterIdleWait(SmalltalkOOP backgroundProcess)
{
    var eventHandle = _platformBridge?.EventAvailableHandle;
    var timerHandle = _timerManager.TimerExpiredHandle;

    Console.WriteLine("[IDLE] Entering idle wait (background process active)");

    while (true)
    {
        CheckAndSignalExpiredTimer();

        var currentProcess = GetActiveProcess();
        if (currentProcess != backgroundProcess)
        {
            Console.WriteLine("[IDLE] Exiting idle (process switch detected)");
            return;
        }

        if (eventHandle != null)
        {
            var handles = new WaitHandle[] { eventHandle, timerHandle, ShutdownToken.WaitHandle };
            int waitResult = WaitHandle.WaitAny(handles);

            if (waitResult == 0)  // Event
            {
                SignalInputSemaphoreIfRegistered();
                _platformBridge!.ForceResetEventSignal();
            }
        }
        else
        {
            Thread.Sleep(10);  // Fallback
        }

        if (ShutdownToken.IsCancellationRequested)
            throw new OperationCanceledException();
    }
}
```

### Diagnostic Output (What We See)

When running the interpreter at idle:
- `[InterpreterLoop] Background process detected: 69158` - Background process IS found
- `[IDLE]` messages **never appear** - `EnterIdleWait` is never called
- CPU usage: ~3%

When adding logging to Wait primitive:
```
[WAIT] excessSignals=0, actually suspending process
[WAIT] excessSignals=0, actually suspending process
[WAIT] excessSignals=0, actually suspending process
... (continuous)
```

Processes ARE waiting on semaphores, but they're the yield semaphores, and they get signaled immediately by the forked process.

---

## The Real Issue

The problem is **architectural** at the Smalltalk level:

1. **Blue Book design assumes 100% CPU is acceptable** - Original Xerox systems ran on dedicated hardware
2. **`searchForActiveController` is a polling loop** - It checks `isControlWanted` repeatedly, yielding between checks
3. **`yield` prevents starvation but not spinning** - It lets other same-priority processes run, but the caller always resumes quickly
4. **No event-driven controller scheduling** - Controllers poll sensor state rather than blocking on input semaphore

The VM's idle detection is **architecturally correct** - if the background process ran, we'd block properly. But the Smalltalk code **never lets** the background process run.

---

## Possible Future Approaches

### Approach A: Detect "Yield-Only" Processes

Track which processes are only doing yield waits (semaphore created, immediately signaled). When ALL runnable processes are in yield-wait state, treat system as idle.

**Complexity**: High - requires VM to understand Smalltalk-level patterns.

### Approach B: Time-Based Idle Detection

If no "real work" (non-yield bytecodes? input events?) for N milliseconds, consider system idle and block.

**Complexity**: Medium - but what is "real work"?

### Approach C: Modify Yield Semantics

Make `yield` check if any OTHER process at same priority exists. If not, don't create forked process - just return immediately or block.

**Risk**: Changes Blue Book semantics.

### Approach D: Accept the CPU Usage

Document that ~3% idle CPU is expected behavior for canonical Smalltalk-80.

---

## Summary for Next LLM

**The Problem**: VM idle detection checks if background process (priority 1) is active. But `searchForActiveController` loops with `Processor yield`, which keeps priority 4 always occupied. Background never runs. VM never enters idle wait. CPU burns at ~3%.

**Root Cause**: `yield` creates a forked process that signals immediately, ensuring the original process is always back in the ready queue before the forked process terminates. Priority 4 is never empty.

**What Works**:
- `WaitHandle.WaitAny` infrastructure is correct
- Background process detection is correct
- Input semaphore signaling is correct

**What Doesn't Work**:
- Background process **never becomes active**
- Smalltalk-level polling loop defeats VM-level idle detection

**Constraint**: Cannot modify Smalltalk-80.st (canonical source).

**Status**: Unresolved. Need a way to detect idle at VM level that doesn't depend on background process becoming active.
