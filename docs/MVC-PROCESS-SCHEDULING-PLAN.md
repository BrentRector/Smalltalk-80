# MVC and Process Scheduling Implementation Plan

**Version:** 1.0
**Date:** 2025-11-28
**Status:** Ready for Implementation
**Target:** Full Bluebook-compliant interactive system with MVC and cooperative multitasking

---

## Executive Summary

This document specifies the implementation plan for transitioning the Smalltalk-80 interpreter from cold start initialization to a fully interactive, running system with MVC controller event loops and Smalltalk-level process scheduling.

**Key Insight:** Smalltalk-80's process scheduling is implemented entirely at the Smalltalk level using primitives 85-88. No OS-level fibers or threading is required for process switching - the C# interpreter executes one process at a time, and process switching happens through Smalltalk's Semaphore primitives.

---

## 1. Current State

### What Works

| Component | Status | Notes |
|-----------|--------|-------|
| Cold start phases 1-6 | ✅ | Full heap construction |
| Init expressions | ✅ 47/48 | 98% success rate |
| BlockContext tests | ✅ 120/120 | Core execution verified |
| VM primitives | ✅ 110+ | All essential primitives |
| Display output | ✅ | Graphics and text rendering |
| Thread separation | ✅ | WinForms + Smalltalk threads |
| Event queue | ✅ | SmalltalkEventQueue implemented |
| Input primitives 90-95 | ✅ | Mouse and keyboard events |
| ProcessorScheduler | ✅ | Created in Phase 3 with 8 priority queues |
| Initial Process | ✅ | Created at priority 4 |

### What's Missing

| Component | Status | Notes |
|-----------|--------|-------|
| Background process | ❌ | `Processor hiddenBackgroundProcess` not called |
| ControlManager | ❌ | `ScheduledControllers` global not created |
| Controller event loop | ❌ | `searchForActiveController` not running |
| Process switching | ⚠️ | Primitives 85-88 need verification |

---

## 2. Target State

After implementation:

```
┌─────────────────────────────────────────────────────────────┐
│                    Running Smalltalk-80 System               │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Priority 8: [empty - Delay processes when needed]           │
│  Priority 4: Controller Process (searchForActiveController)  │
│  Priority 1: Background Process ([true] whileTrue: [])       │
│                                                              │
│  Controller Loop:                                            │
│    1. Processor yield (gives other processes a chance)       │
│    2. Poll controllers via isControlWanted                   │
│    3. Activate controller that wants control                 │
│    4. Controller runs controlLoop                            │
│    5. When controller releases, search again                 │
│                                                              │
│  Event Flow:                                                 │
│    WinForms → EventQueue → InputSensor → Controller          │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Canonical Startup Sequence

The Smalltalk-80 system starts with this 3-step sequence:

```smalltalk
"Step 1: Create background process (idle loop at priority 1)"
Processor hiddenBackgroundProcess.

"Step 2: Create ControlManager and install as global"
Smalltalk at: #ScheduledControllers put: ControlManager new.

"Step 3: Start controller event loop (runs forever)"
ScheduledControllers searchForActiveController.
```

### Step 1: Background Process

**ProcessorScheduler>>hiddenBackgroundProcess**:
```smalltalk
hiddenBackgroundProcess
    "Install a default background process which is invisible."
    self background: [[true] whileTrue: []]
```

**ProcessorScheduler>>background:**:
```smalltalk
background: aBlock
    "Replace the background process with a process running aBlock."
    BackgroundProcess == nil ifFalse: [BackgroundProcess terminate].
    BackgroundProcess := aBlock newProcess.
    BackgroundProcess priority: SystemRockBottomPriority.  "priority = 1"
    BackgroundProcess resume
```

**Effect:** Creates a Process at priority 1 that runs forever, ensuring the scheduler always has something to run.

### Step 2: ControlManager Creation

**ControlManager>>initialize**:
```smalltalk
initialize
    "Initialize the receiver to refer to only the background controller."
    | screenView |
    screenController := ScreenController new.
    screenView := FormView new.
    screenView model: (InfiniteForm with: Form gray) controller: screenController.
    screenView window: Display boundingBox.
    scheduledControllers := OrderedCollection with: screenController
```

**Effect:** Creates:
- `ScreenController` (the gray background controller)
- `FormView` displaying the gray screen pattern
- `scheduledControllers` collection with the screen controller

### Step 3: Controller Event Loop

**ControlManager>>searchForActiveController**:
```smalltalk
searchForActiveController
    "Find a scheduled controller that wants control and give control to it."

    | aController |
    activeController := nil.
    activeControllerProcess := Processor activeProcess.
    [Processor yield.                              "1. Yield to other processes"
     aController := scheduledControllers
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

**Effect:** Creates the main event loop that:
1. Yields to give other processes CPU time
2. Polls all scheduled controllers via `isControlWanted`
3. Activates the first controller that wants control
4. Runs forever (terminates current process when controller takes over)

---

## 4. Process Scheduling Architecture

### How Processor yield Works

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"
    | semaphore |
    semaphore := Semaphore new.
    [semaphore signal] fork.    "Create process to signal"
    semaphore wait              "Wait (suspends current process)"
```

This is pure Smalltalk code using:
- **Primitive 85** (signal): Wakes up waiting process
- **Primitive 86** (wait): Suspends current process if no signals
- **Primitive 87** (resume): Adds process to run queue
- **Primitive 88** (suspend): Removes process from run queue

### Context Switching Flow

When `Processor yield` executes:

```
1. Current process (priority 4) creates Semaphore
2. [semaphore signal] fork creates new Process (priority 4)
   - Primitive 87 (resume) adds new process to queue
3. semaphore wait (Primitive 86) checks excessSignals
   - excessSignals = 0, so:
   - Add current process to semaphore's waiting list
   - Find highest priority runnable process
   - Switch to it by setting activeContext
4. New forked process runs
   - Executes semaphore signal (Primitive 85)
   - Original process wakes up (back in run queue)
   - Forked process terminates
5. Original process eventually resumes
   - Continues after the yield call
```

### Priority Queue Structure

```
ProcessorScheduler
  └─ quiescentProcessLists: Array[8]
       ├─ [1] LinkedList (priority 1 - SystemRockBottomPriority)
       ├─ [2] LinkedList (priority 2 - SystemBackgroundPriority)
       ├─ [3] LinkedList (priority 3 - UserBackgroundPriority)
       ├─ [4] LinkedList (priority 4 - UserSchedulingPriority) ← Controllers
       ├─ [5] LinkedList (priority 5 - UserInterruptPriority)
       ├─ [6] LinkedList (priority 6 - LowIOPriority)
       ├─ [7] LinkedList (priority 7 - HighIOPriority)
       └─ [8] LinkedList (priority 8 - TimingPriority)
  └─ activeProcess: Process (currently executing)
```

---

## 5. Implementation Plan

### Phase 1: Verify Process Primitives

**Goal:** Ensure primitives 85-88 correctly implement process scheduling.

**Tasks:**

1. **Review Primitive 85 (Semaphore>>signal)**
   - File: `ControlPrimitives.cs`
   - Check: If waiting processes exist, wake first one
   - Check: If woken process has higher priority, switch immediately
   - Check: Otherwise increment excessSignals

2. **Review Primitive 86 (Semaphore>>wait)**
   - Check: If excessSignals > 0, decrement and return
   - Check: Otherwise add current process to waiting list
   - Check: Find and switch to highest priority runnable process

3. **Review Primitive 87 (Process>>resume)**
   - Check: Add process to appropriate priority queue
   - Check: If higher priority than active, switch immediately

4. **Review Primitive 88 (Process>>suspend)**
   - Check: Remove process from scheduler
   - Check: If active process, find and switch to next runnable

**Test Code:**
```smalltalk
"Test basic process creation"
| results semaphore |
results := OrderedCollection new.
semaphore := Semaphore new.
[results add: 'forked'. semaphore signal] fork.
semaphore wait.
results first = 'forked'  "=> true"
```

### Phase 2: Implement Startup Sequence

**Goal:** Execute the canonical 3-step startup.

**Tasks:**

1. **Add startup code to SmalltalkInterpreterThread**
   - After init expressions complete
   - Use MessageSender API to send messages

2. **Implement Step 1: Background Process**
   ```csharp
   // Send: Processor hiddenBackgroundProcess
   _messageSender.SendUnary(
       receiverOOP: _processorOOP,
       selector: "hiddenBackgroundProcess"
   );
   ```

3. **Implement Step 2: ControlManager**
   ```csharp
   // Send: ControlManager new
   var controlManagerOOP = _messageSender.SendUnary(
       receiverClassName: "ControlManager",
       selector: "new"
   );

   // Send: Smalltalk at: #ScheduledControllers put: controlManager
   _messageSender.Send(
       receiverOOP: _smalltalkDictionaryOOP,
       selector: "at:put:",
       args: [scheduledControllersSymbol, controlManagerOOP]
   );
   ```

4. **Implement Step 3: Controller Loop**
   ```csharp
   // Send: ScheduledControllers searchForActiveController
   // This NEVER returns - enters the controller event loop
   _messageSender.SendUnary(
       receiverOOP: controlManagerOOP,
       selector: "searchForActiveController"
   );
   ```

### Phase 3: Verify InputSensor Integration

**Goal:** Ensure controllers can poll input state.

**Tasks:**

1. **Verify Primitive 90 (mousePoint)**
   - Returns current cursor position as Point
   - Reads from SmalltalkEventQueue

2. **Verify controller polling chain**
   ```
   Controller>>isControlWanted
     → self viewHasCursor
       → view containsPoint: sensor cursorPoint
         → Primitive 90 reads from event queue
   ```

3. **Verify WinForms event capture**
   - MouseMove events enqueued
   - MouseDown/MouseUp events enqueued
   - Keyboard events enqueued

### Phase 4: Test Controller Activation

**Goal:** Verify ScreenController responds to input.

**Tasks:**

1. **Test cursor tracking**
   - Move mouse over window
   - Verify `sensor cursorPoint` returns correct position

2. **Test isControlWanted**
   - ScreenController should want control when yellow button pressed
   - Other controllers want control when cursor in their view

3. **Test system menu (yellow button)**
   - Yellow button (middle mouse) on gray background
   - Should activate ScreenController
   - Should show system menu

---

## 6. Key Classes and Methods

### ProcessorScheduler

| Method | Purpose |
|--------|---------|
| `hiddenBackgroundProcess` | Install invisible idle process |
| `background:` | Create process at priority 1 |
| `yield` | Give other processes a chance |
| `activeProcess` | Return currently running process |
| `terminateActive` | Terminate current process |

### Process

| Method | Primitive | Purpose |
|--------|-----------|---------|
| `resume` | 87 | Add to run queue |
| `suspend` | 88 | Remove from scheduler |
| `terminate` | - | Stop process permanently |
| `priority:` | - | Set scheduling priority |

### Semaphore

| Method | Primitive | Purpose |
|--------|-----------|---------|
| `signal` | 85 | Wake waiting process or increment count |
| `wait` | 86 | Wait for signal or suspend |

### ControlManager

| Method | Purpose |
|--------|---------|
| `initialize` | Create ScreenController and scheduledControllers |
| `searchForActiveController` | Main event loop |
| `activeController:` | Activate a controller |
| `scheduleActive:` | Add controller to scheduled list |

### Controller

| Method | Purpose |
|--------|---------|
| `isControlWanted` | Check if controller wants control |
| `controlLoop` | Main control loop (polls sensor) |
| `controlActivity` | Handle single input event |
| `startUp` | Initialize, loop, terminate |

---

## 7. Testing Strategy

### Unit Tests

**Process Creation:**
```smalltalk
| process |
process := [3 + 4] newProcess.
process class name = 'Process'.
process priority = 4.  "Default priority"
```

**Semaphore Signal/Wait:**
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
Processor yield. Processor yield. Processor yield.
results size >= 3  "=> true, order may vary"
```

### Integration Tests

**Background Process:**
```smalltalk
Processor hiddenBackgroundProcess.
ProcessorScheduler BackgroundProcess notNil.  "=> true"
ProcessorScheduler BackgroundProcess priority = 1.  "=> true"
```

**ControlManager:**
```smalltalk
| cm |
cm := ControlManager new.
cm scheduledControllers size = 1.  "=> true (screenController)"
cm screenController class name = 'ScreenController'.  "=> true"
```

### Manual Tests

1. **Window Responsiveness**
   - Start interpreter
   - Verify window responds to resize/move during init expressions

2. **Mouse Tracking**
   - Move cursor over window
   - Verify `Sensor cursorPoint` updates

3. **System Menu**
   - Click yellow button (middle mouse) on gray area
   - Verify system menu appears

---

## 8. Success Criteria

### Minimum Viable Product

- [ ] Background process created at priority 1
- [ ] ControlManager created with ScreenController
- [ ] Controller event loop running
- [ ] `Processor yield` switches between processes
- [ ] Mouse position tracked via `Sensor cursorPoint`
- [ ] ScreenController responds to yellow button

### Full Implementation

- [ ] All above criteria
- [ ] System menu accessible and functional
- [ ] Multiple controllers can be scheduled
- [ ] View hierarchy navigation works
- [ ] No crashes or freezes under normal use

---

## 9. File References

### Smalltalk Source

| Class | File Location | Lines |
|-------|---------------|-------|
| Process | `Smalltalk-80 Source/Smalltalk-80.st` | 32577-32696 |
| ProcessorScheduler | `Smalltalk-80 Source/Smalltalk-80.st` | 32772-32973 |
| Controller | `Smalltalk-80 Source/Smalltalk-80.st` | 13018-13170 |
| ControlManager | `Smalltalk-80 Source/Smalltalk-80.st` | 13170-13397 |
| Semaphore | `Smalltalk-80 Source/Smalltalk-80.st` | ~32450-32576 |

### C# Implementation

| Component | File |
|-----------|------|
| Process primitives 85-88 | `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` |
| Input primitives 90-95 | `Smalltalk80.Interpreter/Core/VM/Primitives/InputPrimitives.cs` |
| Event queue | `Smalltalk80.Interpreter/Platform/SmalltalkEventQueue.cs` |
| Interpreter thread | `Smalltalk80.Interpreter/SmalltalkInterpreterThread.cs` |
| Message sender | `Smalltalk80.Interpreter/Core/VM/MessageSender.cs` |

---

## 10. Next Steps

1. **Immediate:** Review primitives 85-88 implementation
2. **Then:** Add test for basic process fork/yield
3. **Then:** Implement startup sequence in SmalltalkInterpreterThread
4. **Then:** Test controller loop activation
5. **Finally:** Debug any issues with controller polling

---

**END OF PLAN**
