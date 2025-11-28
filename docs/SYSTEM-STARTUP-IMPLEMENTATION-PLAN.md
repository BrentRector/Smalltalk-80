# Smalltalk-80 System Startup Implementation Plan

**Version:** 2.0
**Date:** 2025-11-28
**Status:** Phase 3 Ready - MVC and Process Scheduling
**Target:** Full Bluebook-compliant interactive system startup

---

## Executive Summary

This document specifies the implementation plan for bringing the Smalltalk-80 interpreter from its current state (cold start + init expressions) to a fully interactive, running Smalltalk system with MVC controller event loops and process scheduling.

**Current State:**
- ✅ Cold start phases 1-6 complete (heap building)
- ✅ 47/48 init expressions passing (98%)
- ✅ 120/120 BlockContext regression tests passing
- ✅ VM core fully operational
- ✅ WinForms display window with event capture
- ✅ 110+ primitives implemented
- ✅ Thread separation (WinForms + Smalltalk threads)
- ✅ Event queue infrastructure in place
- ⚠️ No controller event loop running
- ⚠️ No MVC interaction

**Target State:**
- ✅ Controller event loop running (searchForActiveController)
- ✅ InputSensor polling events from WinForms
- ✅ Background process running at priority 1
- ✅ Controller process at priority 4 handling UI
- ✅ Full Bluebook-compliant interactive system

---

## 1. Architecture Overview

### 1.1 Two-Thread Model

```
┌─────────────────────────────────────────────────────────────┐
│                    Operating System                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────────────────┐         ┌────────────────────────┐ │
│  │  WinForms Thread    │         │  Smalltalk Thread      │ │
│  │                     │         │                        │ │
│  │  - Message pump     │         │  - VM execution        │ │
│  │  - Event capture    │◄───────►│  - Controller loop     │ │
│  │  - Display render   │ events  │  - Process scheduling  │ │
│  │                     │         │                        │ │
│  └─────────────────────┘         └────────────────────────┘ │
│                                                              │
│              ┌─────────────────────────────┐                 │
│              │  Thread-Safe Event Queue    │                 │
│              │  (ConcurrentQueue<Event>)   │                 │
│              └─────────────────────────────┘                 │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Process Scheduling Model

Smalltalk-80 uses **cooperative multitasking** at the Smalltalk level:

- **Processor yield** creates a Semaphore, forks a signaling process, and waits
- Processes voluntarily give up control via `Processor yield`
- The interpreter executes one process at a time
- Process switching happens through Smalltalk primitives (85-88)

**No OS-level threading/fibers needed** - all process switching is managed by Smalltalk code and primitives.

### 1.3 Priority Levels

| Priority | Name | Purpose |
|----------|------|---------|
| 8 | TimingPriority | Real-time clock, Delay processes |
| 7 | HighIOPriority | Time-critical I/O |
| 6 | LowIOPriority | Keyboard, mouse input |
| 5 | UserInterruptPriority | User interrupt processes |
| 4 | UserSchedulingPriority | **Controller event loop** |
| 3 | UserBackgroundPriority | User background tasks |
| 2 | SystemBackgroundPriority | GC, status checks |
| 1 | SystemRockBottomPriority | **Background idle process** |

---

## 2. Canonical Startup Sequence

The Smalltalk-80 system starts with this 3-step sequence:

```smalltalk
"Step 1: Create background process (idle loop at priority 1)"
Processor hiddenBackgroundProcess.

"Step 2: Create ControlManager and install as global"
Smalltalk at: #ScheduledControllers put: ControlManager new.

"Step 3: Start controller event loop (runs forever)"
ScheduledControllers searchForActiveController.
```

### 2.1 Step 1: Background Process

**ProcessorScheduler>>hiddenBackgroundProcess** (lines 32951-32955):
```smalltalk
hiddenBackgroundProcess
    "Install a default background process which is invisible."
    self background:
        [[true] whileTrue: []]
```

This creates a Process at priority 1 that runs an infinite loop, ensuring the scheduler always has something to run.

### 2.2 Step 2: ControlManager Creation

**ControlManager>>initialize** (lines 13187-13194):
```smalltalk
initialize
    "Initialize the receiver to refer to only the background controller."
    | screenView |
    screenController _ ScreenController new.
    screenView _ FormView new.
    screenView model: (InfiniteForm with: Form gray) controller: screenController.
    screenView window: Display boundingBox.
    scheduledControllers _ OrderedCollection with: screenController
```

Creates the ScreenController (background gray pattern) and initializes the scheduledControllers collection.

### 2.3 Step 3: Controller Event Loop

**ControlManager>>searchForActiveController** (lines 13329-13349):
```smalltalk
searchForActiveController
    "Find a scheduled controller that wants control and give control to it."

    | aController |
    activeController _ nil.
    activeControllerProcess _ Processor activeProcess.
    [Processor yield.                              "1. Yield to other processes"
     aController _ scheduledControllers
        detect:                                    "2. Find controller wanting control"
            [:c | c isControlWanted and: [c ~~ screenController]]
        ifNone:
            [screenController isControlWanted
                ifTrue: [screenController]
                ifFalse: [nil]].
     aController isNil]
        whileTrue.                                 "3. Loop until controller found"
    self activeController: aController.            "4. Activate controller"
    Processor terminateActive                      "5. Terminate search process"
```

This creates the main event loop that:
1. Yields to give other processes CPU time
2. Polls all scheduled controllers via `isControlWanted`
3. Activates the first controller that wants control
4. Runs forever

---

## 3. Process Switching Mechanism

### 3.1 How Processor yield Works

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"
    | semaphore |
    semaphore := Semaphore new.
    [semaphore signal] fork.    "Create process to signal"
    semaphore wait              "Wait (suspends current process)"
```

This is pure Smalltalk code that uses:
- **Primitive 85** (signal): Wakes up waiting process
- **Primitive 86** (wait): Suspends current process if no signals
- **Primitive 87** (resume): Adds process to run queue
- **Primitive 88** (suspend): Removes process from run queue

### 3.2 Primitive Implementation Requirements

**Primitive 85 (Semaphore>>signal)**:
- If waiting processes exist, wake first one (add to run queue)
- Otherwise increment excessSignals
- If woken process has higher priority, switch to it

**Primitive 86 (Semaphore>>wait)**:
- If excessSignals > 0, decrement and return
- Otherwise add current process to waiting list and suspend

**Primitive 87 (Process>>resume)**:
- Add process to appropriate priority queue
- If higher priority than active, switch immediately

**Primitive 88 (Process>>suspend)**:
- Remove process from scheduler
- Find and switch to highest priority runnable process

### 3.3 Context Switching

When switching processes:
1. Save current process's `suspendedContext` (activeContext)
2. Find highest priority runnable process
3. Update `Processor activeProcess`
4. Restore new process's `suspendedContext` as activeContext
5. Continue execution from that context

All of this happens within the C# interpreter - no OS-level context switching needed.

---

## 4. Implementation Plan

### Phase 1: Verify Process Primitives (Current)

**Tasks:**
1. Verify primitives 85-88 are fully implemented
2. Test basic process creation with `[code] fork`
3. Test `Processor yield` works correctly
4. Test semaphore signal/wait

**Test Code:**
```smalltalk
"Test process creation"
| results |
results := OrderedCollection new.
[results add: 'forked'] fork.
Processor yield.
results inspect.
```

### Phase 2: Background Process Creation

**Tasks:**
1. Execute `Processor hiddenBackgroundProcess`
2. Verify BackgroundProcess class variable is set
3. Verify process is at priority 1

**Verification:**
```smalltalk
ProcessorScheduler BackgroundProcess notNil.  "=> true"
ProcessorScheduler BackgroundProcess priority. "=> 1"
```

### Phase 3: ControlManager Creation

**Tasks:**
1. Execute `Smalltalk at: #ScheduledControllers put: ControlManager new`
2. Verify ScreenController created
3. Verify scheduledControllers collection initialized

**Verification:**
```smalltalk
ScheduledControllers class name.  "=> ControlManager"
ScheduledControllers scheduledControllers size.  "=> 1"
```

### Phase 4: Controller Event Loop

**Tasks:**
1. Execute `ScheduledControllers searchForActiveController`
2. Verify loop runs (yields, polls controllers)
3. Verify ScreenController responds to mouse input
4. Verify system menu appears on yellow button click

**Success Criteria:**
- Controller loop runs continuously
- `Processor yield` allows process switching
- `isControlWanted` correctly detects cursor position
- ScreenController responds to yellow button (system menu)

---

## 5. Event Integration

### 5.1 InputSensor Polling

Controllers check for input via `Sensor` (InputSensor instance):

```smalltalk
Controller>>isControlWanted
    "Answer whether the cursor is inside the receiver's view"
    ^self viewHasCursor

Controller>>viewHasCursor
    ^view containsPoint: sensor cursorPoint
```

The `sensor cursorPoint` call uses **Primitive 90** (mousePoint) which reads from the event queue.

### 5.2 Event Queue Flow

```
WinForms Thread                    Smalltalk Thread
     │                                   │
     │ MouseMove event                   │
     │      │                            │
     │      ▼                            │
     │ Enqueue to                        │
     │ SmalltalkEventQueue ──────────────► sensor cursorPoint
     │                                   │ (Primitive 90)
     │                                   │      │
     │                                   │      ▼
     │                                   │ Controller checks
     │                                   │ isControlWanted
```

### 5.3 Already Implemented

- ✅ `SmalltalkEventQueue` class
- ✅ `SmalltalkEvent` class
- ✅ Primitive 90 (mousePoint)
- ✅ Primitive 91 (cursorLocPut:)
- ✅ Primitive 93 (inputSemaphore:)
- ✅ Primitive 95 (inputWord)
- ✅ WinForms event handlers enqueuing events

---

## 6. Testing Strategy

### 6.1 Unit Tests

**Process Creation:**
```smalltalk
| semaphore result |
semaphore := Semaphore new.
result := nil.
[result := 42. semaphore signal] fork.
semaphore wait.
result = 42  "=> true"
```

**Process Yield:**
```smalltalk
| results |
results := OrderedCollection new.
[results add: 1. Processor yield. results add: 3] fork.
[results add: 2] fork.
Processor yield. Processor yield.
results asArray  "=> #(1 2 3) or similar interleaving"
```

### 6.2 Integration Tests

**Controller Loop:**
```smalltalk
"After executing startup sequence, move mouse"
"ScreenController should respond to isControlWanted"
"Yellow button should show system menu"
```

### 6.3 Regression Tests

Always run after changes:
- BlockContext regression tests (120 tests, returns 1191)
- C# unit tests (388 tests)
- Init expression tests (47 expressions)

---

## 7. Success Criteria

### Minimum Viable Product

✅ **System starts and runs**
- Background process created at priority 1
- Controller loop running at priority 4
- No crashes or freezes

✅ **Process scheduling works**
- `Processor yield` switches between processes
- Semaphore signal/wait works correctly
- Priority-based scheduling correct

✅ **Controller loop works**
- `searchForActiveController` runs continuously
- Controllers polled via `isControlWanted`
- ScreenController can be activated

✅ **Event integration works**
- Mouse position tracked
- Controllers respond to cursor position
- Yellow button shows system menu

---

## 8. Key Files Reference

**Smalltalk Source** (`Smalltalk-80 Source/Smalltalk-80.st`):
- Process: Lines 32577-32696
- ProcessorScheduler: Lines 32772-32973
- Controller: Lines 13018-13170
- ControlManager: Lines 13170-13397

**C# Implementation**:
- Process primitives: `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`
- Input primitives: `Smalltalk80.Interpreter/Core/VM/Primitives/InputPrimitives.cs`
- Event queue: `Smalltalk80.Interpreter/Platform/SmalltalkEventQueue.cs`
- Startup: `Smalltalk80.Interpreter/SmalltalkInterpreterThread.cs`

---

**END OF PLAN**
