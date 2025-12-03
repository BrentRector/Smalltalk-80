# Process Management Architecture

**Last Updated**: 2025-12-03
**Status**: ✅ COMPLETE - Process scheduling fully operational

## Overview

This document describes how process management operates in the Smalltalk-80 interpreter, including process scheduling, idle detection, timer-based wakeups, and efficient CPU usage during quiescence.

## High-Level Components

### 1. ProcessorScheduler (Smalltalk Object)

The `ProcessorScheduler` is a singleton Smalltalk object (`Processor` global) that manages all process scheduling.

**Instance Variables:**
- `quiescentProcessLists` - Array of 8 LinkedLists, one per priority level (1-8)
- `activeProcess` - The currently running Process

**Key Methods:**
- `yield` - Current process gives up CPU, switches to next runnable
- `signal:atMilliseconds:` - Schedule timer (primitive 100)
- `hiddenBackgroundProcess` - Install idle process at priority 1

### 2. Process (Smalltalk Object)

Each `Process` represents a schedulable unit of execution.

**Instance Variables (from VMConstants):**
```
Index 0: nextLink        (from Link) - for linked list membership
Index 1: suspendedContext (Process)  - saved execution state when not running
Index 2: priority         (Process)  - 1 (lowest) to 8 (highest)
Index 3: myList           (Process)  - which list this process is in
```

**Process States:**
- **Running**: `suspendedContext = nil`, `myList = nil`, is `activeProcess`
- **Ready**: In a priority queue, waiting to run
- **Waiting**: In a semaphore's wait list, blocked on `wait`
- **Suspended**: Has `suspendedContext` but not in any list

### 3. Semaphore (Smalltalk Object)

Semaphores provide synchronization between processes.

**Instance Variables:**
```
Index 0: firstLink      (from LinkedList) - first waiting process
Index 1: lastLink       (from LinkedList) - last waiting process
Index 2: excessSignals  (Semaphore)       - count of unmatched signals
```

**Operations:**
- `signal` (Primitive 85): Increment `excessSignals` or wake first waiter
- `wait` (Primitive 86): Decrement `excessSignals` or block on semaphore

### 4. TimerManager (C# Component)

Manages scheduled timer requests for the Delay mechanism.

**State:**
- `_pendingSemaphore` - Semaphore to signal when timer fires
- `_targetMilliseconds` - When to fire (millisecond clock value)
- `_timerActive` - Whether a timer is pending

**Operations:**
- `ScheduleSignal(semaphoreOOP, targetMs)` - Schedule future signal
- `GetExpiredTimerSemaphore()` - Get semaphore if timer expired
- `GetMillisecondsUntilNextTimer()` - Time until next timer (for wait)

### 5. SmalltalkEventQueue (C# Component)

Thread-safe queue for UI events from WinForms to Smalltalk.

**Key Features:**
- `AutoResetEvent` for efficient blocking
- `WaitOne(timeoutMs)` - Block until event arrives or timeout
- `Enqueue()` - Adds event and signals wait handle

### 6. VirtualMachine.InterpreterLoop (C# Component)

The main bytecode execution loop with integrated idle detection.

## Process Selection

### Priority-Based Scheduling

Smalltalk-80 uses **preemptive priority scheduling**:

1. Higher priority processes always run before lower priority
2. Within the same priority, processes run in FIFO order
3. A process runs until it:
   - Waits on a semaphore
   - Explicitly yields
   - Is preempted by a higher-priority process waking up

### Finding the Next Process

When switching processes, `SwitchToHighestPriorityProcess()`:

```
for priority = 8 down to 1:
    list = quiescentProcessLists[priority - 1]
    if list is not empty:
        process = list.removeFirst()
        return process
return nil  // No runnable process
```

### Context Switching

When switching from process A to process B:

1. Save A's current context in `A.suspendedContext`
2. Add A to appropriate ready queue (unless blocking)
3. Set `Processor.activeProcess = B`
4. Set B's `suspendedContext = nil`
5. Restore VM's active context from B's saved context

## How Processes Go Idle

### Semaphore Wait (Primitive 86)

When a process calls `aSemaphore wait`:

```
if excessSignals > 0:
    excessSignals := excessSignals - 1
    return immediately
else:
    push result onto stack (for when we resume)
    add self to semaphore's waiting list
    switch to highest priority runnable process
```

The waiting process is now blocked on the semaphore and won't run until signaled.

### Voluntary Yield

When a process calls `Processor yield`:

```
[[Delay>>forMilliseconds: wait] fork, or just...]
add self to ready queue at current priority
switch to highest priority runnable process
```

## System Quiescence

### The Background Process

At initialization, Smalltalk installs a background process:

```smalltalk
ProcessorScheduler>>hiddenBackgroundProcess
    self background: [[true] whileTrue: []]
```

This creates a process at **priority 1** (SystemRockBottomPriority) that runs an infinite empty loop. It serves as a fallback when all other processes are blocked.

### Quiescent State

The system is **quiescent** when:
1. All user processes are blocked on semaphores
2. Only the background process (priority 1) is running
3. No timers are pending
4. No input events are available

### VM Idle Detection

The interpreter loop detects quiescence every 1000 bytecodes:

```csharp
// In InterpreterLoop()
if (activeProcess.priority == 1)  // Background process
{
    // Calculate wait time
    int waitMs = 10;  // Max idle wait
    int timerMs = _timerManager.GetMillisecondsUntilNextTimer();
    if (timerMs >= 0 && timerMs < waitMs)
        waitMs = timerMs;

    // Block native thread until event or timer
    _platformBridge.WaitForEvents(waitMs);
}
```

This reduces CPU usage from 100% to near-zero during idle periods.

## Waking Up from Events

### Input Event Flow

```
┌──────────────┐     ┌─────────────────────┐     ┌──────────────┐
│  WinForms    │     │  SmalltalkEventQueue │     │   VM Thread  │
│  UI Thread   │────▶│  (thread-safe queue) │────▶│              │
└──────────────┘     └─────────────────────┘     └──────────────┘
     │                         │                        │
     │ Mouse/Key event         │ Enqueue() +            │ WaitForEvents()
     │                         │ _eventSignal.Set()     │ returns
     │                         │                        │
```

### Sequence of Events

1. **User moves mouse or presses key**
2. **WinForms thread**: Calls `eventQueue.Enqueue(event)`
3. **SmalltalkEventQueue**: Adds to queue, calls `_eventSignal.Set()`
4. **VM Thread**: `WaitForEvents()` returns (was blocked on AutoResetEvent)
5. **InterpreterLoop**: Continues executing background process
6. **InputSensor**: Eventually polls via primitive 95, gets event
7. **InputProcess**: Wakes from semaphore wait, processes input
8. **Controller**: Receives input, handles user action

### Input Semaphore

The input handling process typically waits on an input semaphore:

```smalltalk
"InputSensor waiting loop"
[true] whileTrue: [
    inputSemaphore wait.
    self processEvents
]
```

When events arrive, the VM can signal this semaphore (if registered via primitive 93).

## Waking Up from Timer Events

### Delay Mechanism

The `Delay` class enables time-based scheduling:

```smalltalk
(Delay forMilliseconds: 100) wait
```

### Timer Flow

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│   Delay     │     │ TimerManager │     │   VM Thread     │
│   object    │────▶│  (C#)        │────▶│ InterpreterLoop │
└─────────────┘     └──────────────┘     └─────────────────┘
       │                   │                      │
       │ primitive 100     │                      │ CheckAndSignal
       │ signal:atMs:      │                      │ ExpiredTimer()
       │                   │                      │
```

### Sequence of Events

1. **Delay>>wait** calls `Processor signal:TimingSemaphore atTime:resumptionTime`
2. **Primitive 100**: `_timerManager.ScheduleSignal(semaphore, targetMs)`
3. **Delay process**: Waits on `delaySemaphore`
4. **InterpreterLoop**: Periodically calls `CheckAndSignalExpiredTimer()`
5. **Timer expires**: `GetExpiredTimerSemaphore()` returns the semaphore
6. **VM signals semaphore**: Increments `excessSignals` or wakes waiter
7. **Delay handler wakes**: `TimingSemaphore` signaled
8. **Delay>>resume**: Signals the specific delay's semaphore
9. **Delayed process wakes**: Continues execution

### Wait Time Calculation

When idle, the VM calculates optimal wait time:

```csharp
int waitMs = MaxIdleWaitMs;  // 10ms default
int timerMs = _timerManager.GetMillisecondsUntilNextTimer();
if (timerMs >= 0 && timerMs < waitMs)
    waitMs = Math.Max(1, timerMs);
```

This ensures:
- Timer fires promptly when due
- Still wakes for input events
- Doesn't burn CPU polling

## Staying Quiescent

### Combined Wait Mechanism

When all processes are blocked and only the background process runs:

```csharp
// WaitForEvents blocks on AutoResetEvent
// Returns when:
//   1. Input event enqueued (eventSignal.Set())
//   2. Timeout expires (timer might be due)
//   3. Spurious wakeup (rare, harmless)

_platformBridge.WaitForEvents(waitMs);

// After waking, check timer
CheckAndSignalExpiredTimer();
```

### Why This Works

1. **No timers, no events**: `WaitForEvents(10)` sleeps 10ms, then loops
2. **Timer pending**: `WaitForEvents(timerMs)` sleeps exactly until timer
3. **Event arrives**: `AutoResetEvent` wakes immediately
4. **Timer and event**: Whichever comes first wakes the VM

### CPU Usage

| State | CPU Usage |
|-------|-----------|
| Active user interaction | Proportional to work |
| Idle with pending timer | Near-zero (sleeping) |
| Idle, no timers | Near-zero (10ms sleeps) |
| Background animation | Low (controlled by Delay) |

## Data Structure Summary

```
ProcessorScheduler (singleton)
├── quiescentProcessLists: Array[8]
│   ├── [0] LinkedList (priority 1 - background)
│   ├── [1] LinkedList (priority 2)
│   │   ...
│   └── [7] LinkedList (priority 8 - highest)
└── activeProcess: Process

Process
├── nextLink: Process (for list membership)
├── suspendedContext: MethodContext (saved state)
├── priority: SmallInteger (1-8)
└── myList: LinkedList (current membership)

Semaphore
├── firstLink: Process (first waiter)
├── lastLink: Process (last waiter)
└── excessSignals: SmallInteger (pending signals)

TimerManager (C#)
├── _pendingSemaphore: SmalltalkOOP
├── _targetMilliseconds: uint
└── _timerActive: bool

SmalltalkEventQueue (C#)
├── _events: ConcurrentQueue<SmalltalkEvent>
├── _eventSignal: AutoResetEvent
└── HasPendingEvents: bool
```

## Related Files

- `VirtualMachine.cs` - InterpreterLoop, idle detection, main bytecode execution
- `VirtualMachine.ProcessScheduling.cs` - Process scheduling methods:
  - `CheckAndSignalExpiredTimer` - Timer expiration handling
  - `AddProcessToReadyQueue` - Add process to priority queue
  - `GetActiveProcess` - Get current process from ProcessorScheduler
  - `SwitchToNextRunnableProcess` - Context switching on termination
  - `FindHighestPriorityRunnableProcess` - Priority-based process selection
  - `RemoveProcessFromList` - LinkedList manipulation
  - `DumpProcessQueues` - Diagnostic output
- `ControlPrimitives.cs` - Primitives 85-88 (Signal, Wait, Resume, Suspend)
- `TimePrimitives.cs` - Primitive 100 (signal:atMilliseconds:)
- `Scheduling/TimerManager.cs` - Interrupt-driven timer with System.Threading.Timer
- `Platform/SmalltalkEventQueue.cs` - Event queue with AutoResetEvent
- `VMConstants.cs` - Process, Semaphore, LinkedList field indices

## Recent Changes (2025-12-03)

- **VirtualMachine Refactoring**: Process scheduling methods extracted to `VirtualMachine.ProcessScheduling.cs` partial class (~330 lines)
- **Timer Implementation**: Uses interrupt-driven `System.Threading.Timer` for precise timing
- **Event Handling**: Simple polling via `WaitForEvents()` - WaitHandle.WaitAny was attempted but caused event loss with AutoResetEvent
- **Scroll Fix**: Defensive nil checks added to prevent crash when scrolling (rare edge case where region collection contains nil)
