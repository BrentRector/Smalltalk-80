# Smalltalk-80 System Startup Implementation Plan

**Version:** 1.0
**Date:** 2025-11-22
**Status:** Prerequisites Complete - Fiber Infrastructure On Hold
**Target:** Full Bluebook-compliant interactive system startup

**Implementation Status:**
- ✅ **Phase 1: Program Refactoring** - COMPLETE (Thread separation implemented)
- ✅ **Phase 2: Window Sizing** - COMPLETE (Dynamic Display sizing working)
- ⏸️ **Phase 3: Fiber Infrastructure** - ON HOLD (Pending other priorities)

---

## Executive Summary

This document specifies the complete implementation plan for bringing the Smalltalk-80 interpreter from its current state (cold start + init expressions) to a fully interactive, running Smalltalk system with process scheduling, controller event loops, and WinForms integration.

**Current State:**
- ✅ Cold start phases 1-6 complete (heap building)
- ✅ 46/47 init expressions passing (98%)
- ✅ 120/120 BlockContext regression tests passing
- ✅ VM core fully operational
- ✅ WinForms display window with event capture
- ✅ 110 primitives implemented
- ⚠️ No process switching (single process executes)
- ⚠️ No event loop integration (events captured but not consumed)
- ⚠️ No controller activation (controllers exist but don't run)

**Target State:**
- ✅ Process scheduler actively switching between Smalltalk processes
- ✅ Controller event loop running (searchForActiveController)
- ✅ InputSensor polling events from WinForms
- ✅ Background process running at priority 1
- ✅ Timing process handling Delays at priority 8
- ✅ Controller process at priority 4 handling UI
- ✅ Full Bluebook-compliant interactive system

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Threading Model: Fibers and Threads](#2-threading-model-fibers-and-threads)
3. [Event Queue Architecture](#3-event-queue-architecture)
4. [Process Scheduler Implementation](#4-process-scheduler-implementation)
5. [Controller Loop Implementation](#5-controller-loop-implementation)
6. [Startup Sequence Implementation](#6-startup-sequence-implementation)
7. [Implementation Phases](#7-implementation-phases)
8. [Testing Strategy](#8-testing-strategy)
9. [Risk Analysis and Mitigation](#9-risk-analysis-and-mitigation)
10. [Success Criteria](#10-success-criteria)

---

## 1. Architecture Overview

### 1.1 System Components

```
┌────────────────────────────────────────────────────────────────┐
│                    Windows OS (Windows 11)                     │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  ┌─────────────────────┐         ┌─────────────────────────┐   │
│  │  WinForms Thread    │         │  Smalltalk Thread       │   │
│  │  (Windows Thread)   │         │  (Windows Thread)       │   │
│  │                     │         │                         │   │
│  │  ┌───────────────┐  │         │  ┌───────────────────┐  │   │
│  │  │ Message Pump  │  │         │  │ Fiber Scheduler   │  │   │
│  │  └───────┬───────┘  │         │  └────────┬──────────┘  │   │
│  │          │          │         │           │             │   │
│  │          ▼          │         │           ▼             │   │
│  │  ┌───────────────┐  │         │  ┌───────────────────┐  │   │
│  │  │ Event Capture │  │         │  │ Main Fiber        │  │   │
│  │  │ (Mouse/Key)   │  │         │  │ (Initial Process) │  │   │
│  │  └───────┬───────┘  │         │  └─────────┬─────────┘  │   │
│  │          │          │         │            │            │   │
│  │          ▼          │         │            ▼            │   │
│  │  ┌───────────────┐  │         │  ┌───────────────────┐  │   │
│  │  │  Enqueue to   │──┼─────────┼──┼─>  Event Fiber    │  │   │
│  │  │ Shared Queue  │  │         │  │  (Ctrl Process)   │  │   │
│  │  └───────────────┘  │         │  └─────────┬─────────┘  │   │
│  │                     │         │            │            │   │
│  │                     │         │            ▼            │   │
│  │                     │         │  ┌───────────────────┐  │   │
│  │                     │         │  │  Timing Fiber     │  │   │
│  │                     │         │  │  (Delay Process)  │  │   │
│  │                     │         │  └─────────┬─────────┘  │   │
│  │                     │         │            │            │   │
│  │                     │         │            ▼            │   │
│  │                     │         │  ┌───────────────────┐  │   │
│  │                     │         │  │ Background Fiber  │  │   │
│  │                     │         │  │ (Idle Process)    │  │   │
│  │                     │         │  └───────────────────┘  │   │
│  └─────────────────────┘         └─────────────────────────┘   │
│                                                                │
│              ┌─────────────────────────────┐                   │
│              │  Thread-Safe Event Queue    │                   │
│              │  (ConcurrentQueue<Event>)   │                   │
│              └─────────────────────────────┘                   │
└────────────────────────────────────────────────────────────────┘
```

### 1.2 Key Design Principles

1. **Two-Thread Model:**
   - **Main Thread (becomes WinForms Thread):**
     - Does cold start (creates Smalltalk heap with Display object)
     - Creates WinForms window
     - Starts Smalltalk thread
     - Becomes Windows message pump, captures events, renders display
   - **Smalltalk Thread:**
     - Runs VM interpreter, converted to fiber
     - Runs all Smalltalk code (init expressions, controller loop, processes)

2. **Fiber-Based Process Scheduling:**
   - Each Smalltalk `Process` maps to a Windows Fiber
   - Cooperative multitasking (fibers never preempt)
   - `Processor yield` switches fibers
   - No locks needed within Smalltalk (single-threaded execution)

3. **Thread-Safe Event Queue:**
   - Boundary between WinForms thread and Smalltalk thread
   - `ConcurrentQueue<SmalltalkEvent>` for thread safety
   - WinForms enqueues, Smalltalk dequeues
   - Preserves Smalltalk's polling semantics

4. **Display Bridge via Primitive 96:**
   - Smalltalk writes to Display Form (bitmap in heap)
   - Primitive 96 (BitBlt) detects writes to Display
   - Copies changed region from heap to local buffer
   - Signals WinForms thread to repaint
   - WinForms OnPaint reads local buffer and renders

5. **Bluebook Compliance:**
   - Follow canonical startup sequence
   - Preserve all Smalltalk semantics
   - No shortcuts or workarounds

---

## 2. Threading Model: Fibers and Threads

### 2.1 Windows Thread 1: WinForms Event Loop

**Purpose:** Host Windows message pump and capture OS events

**Lifecycle:**
```csharp
// In Program.cs (Main thread becomes WinForms thread)
[STAThread]
static void Main(string[] args)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    // ... cold start ...

    // Create Smalltalk thread
    var smalltalkThread = new Thread(RunSmalltalkInterpreter);
    smalltalkThread.SetApartmentState(ApartmentState.MTA);
    smalltalkThread.Start();

    // This thread becomes WinForms event loop
    Application.Run(displayProvider);
}
```

**Responsibilities:**
- Run Windows message pump (`Application.Run`)
- Capture mouse events (`MouseMove`, `MouseDown`, `MouseUp`)
- Capture keyboard events (`KeyDown`, `KeyUp`, `KeyPress`)
- Enqueue events to thread-safe queue
- Update display when signaled by Smalltalk

**No Smalltalk Code Runs Here** - Pure Windows event handling

---

### 2.2 Windows Thread 2: Smalltalk Interpreter Thread

**Purpose:** Run Smalltalk VM and fiber scheduler

**Lifecycle:**
```csharp
static void RunSmalltalkInterpreter()
{
    // Convert this thread to a fiber
    ConvertThreadToFiber(null);

    // Create fiber scheduler
    var scheduler = new FiberScheduler(heapManager, eventQueue);

    // Create initial process fiber
    var initialFiber = CreateFiberEx(
        stackSize: 1024 * 1024,  // 1MB stack
        commitSize: 64 * 1024,    // 64KB initial
        flags: FIBER_FLAG_FLOAT_SWITCH,
        callback: InitialProcessFiberProc,
        parameter: scheduler
    );

    // Switch to initial fiber (never returns)
    SwitchToFiber(initialFiber);
}
```

**Responsibilities:**
- Run bytecode interpreter
- Execute primitive operations
- Switch fibers on `Processor yield`
- Dequeue events from shared queue
- Signal WinForms thread for display updates

**All Smalltalk Code Runs Here** - Single-threaded execution within fibers

---

### 2.3 Fiber Creation and Management

**Fiber Types:**

| Fiber | Smalltalk Process | Priority | Purpose |
|-------|------------------|----------|---------|
| Initial Fiber | Initial Process | 4 | Runs cold start and first init expressions |
| Background Fiber | Background Process | 1 | Idle loop or sweep hand |
| Timing Fiber | Delay Process | 8 | Handles timing and Delay signaling |
| Controller Fiber | Controller Process | 4 | Runs searchForActiveController loop |
| User Fibers | User Processes | 1-8 | Application-created processes |

**Fiber Lifecycle:**

```csharp
public class FiberScheduler
{
    private Dictionary<SmalltalkOOP, IntPtr> _processFibers;
    private IntPtr _currentFiber;
    private SmalltalkOOP _currentProcess;

    public void CreateProcessFiber(SmalltalkOOP processOOP)
    {
        var fiber = CreateFiberEx(
            stackSize: 1024 * 1024,
            commitSize: 64 * 1024,
            flags: FIBER_FLAG_FLOAT_SWITCH,
            callback: ProcessFiberProc,
            parameter: new FiberContext(this, processOOP)
        );

        _processFibers[processOOP] = fiber;
    }

    public void SwitchToProcess(SmalltalkOOP processOOP)
    {
        if (!_processFibers.TryGetValue(processOOP, out var fiber))
        {
            CreateProcessFiber(processOOP);
            fiber = _processFibers[processOOP];
        }

        _currentProcess = processOOP;
        SwitchToFiber(fiber);
        // Control returns here when another fiber switches back
    }
}
```

**Fiber Callback:**

```csharp
private static void ProcessFiberProc(IntPtr parameter)
{
    var context = (FiberContext)GCHandle.FromIntPtr(parameter).Target;
    var scheduler = context.Scheduler;
    var processOOP = context.ProcessOOP;

    // Get suspended context from Process
    var contextOOP = scheduler.HeapManager.GetInstanceVariable(
        processOOP,
        VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX
    );

    // Execute the context (returns when process yields or terminates)
    var vm = scheduler.VirtualMachine;
    vm.ExecuteExistingContext(contextOOP);

    // Process terminated - remove from scheduler
    scheduler.RemoveProcess(processOOP);

    // Switch to next runnable process
    scheduler.Reschedule();
}
```

---

### 2.4 Primitive 87 and 88: Process Switching

**Primitive 87: resume (Add process to run queue)**

```csharp
public class ResumePrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var processOOP = context.Receiver;

        // Get ProcessorScheduler (global Processor)
        var schedulerOOP = context.RuntimeHeapAccess.GetGlobal("Processor");

        // Get priority
        var priorityOOP = context.HeapManager.GetInstanceVariable(
            processOOP,
            VMConstants.PROCESS_PRIORITY_INDEX
        );
        int priority = context.HeapManager.GetSmallIntegerValue(priorityOOP);

        // Get quiescentProcessLists[priority]
        var listsOOP = context.HeapManager.GetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX
        );
        var listOOP = context.HeapManager.GetIndexedField(listsOOP, priority - 1);

        // Add process to LinkedList
        LinkedListOperations.AddLast(context.HeapManager, listOOP, processOOP);

        // Set myList
        context.HeapManager.SetInstanceVariable(
            processOOP,
            VMConstants.PROCESS_MYLIST_INDEX,
            listOOP
        );

        // If higher priority than active, switch now
        var activeProcessOOP = context.HeapManager.GetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX
        );
        var activePriorityOOP = context.HeapManager.GetInstanceVariable(
            activeProcessOOP,
            VMConstants.PROCESS_PRIORITY_INDEX
        );
        int activePriority = context.HeapManager.GetSmallIntegerValue(activePriorityOOP);

        if (priority > activePriority)
        {
            // Trigger context switch (via fiber scheduler)
            context.FiberScheduler.Reschedule();
        }

        return PrimitiveResult.Ok(processOOP);
    }
}
```

**Primitive 88: suspend (Remove process from scheduler)**

```csharp
public class SuspendPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var processOOP = context.Receiver;
        var schedulerOOP = context.RuntimeHeapAccess.GetGlobal("Processor");
        var activeProcessOOP = context.HeapManager.GetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX
        );

        if (processOOP == activeProcessOOP)
        {
            // Suspending active process - switch to next runnable
            context.FiberScheduler.SuspendActiveProcess();
            // Never returns - fiber switch
        }
        else
        {
            // Remove from LinkedList
            var myListOOP = context.HeapManager.GetInstanceVariable(
                processOOP,
                VMConstants.PROCESS_MYLIST_INDEX
            );
            if (myListOOP != context.HeapManager.NilOOP)
            {
                LinkedListOperations.Remove(context.HeapManager, myListOOP, processOOP);
                context.HeapManager.SetInstanceVariable(
                    processOOP,
                    VMConstants.PROCESS_MYLIST_INDEX,
                    context.HeapManager.NilOOP
                );
            }
        }

        return PrimitiveResult.Ok(context.HeapManager.NilOOP);
    }
}
```

**Processor yield Implementation:**

```csharp
// In ProcessorScheduler>>yield (Smalltalk code creates Semaphore and waits)
// This triggers primitive 86 (wait) which calls:

public void Reschedule()
{
    // Save current context
    SaveActiveContext();

    // Find highest priority runnable process
    var nextProcess = FindHighestPriorityRunnableProcess();

    // Switch fiber
    SwitchToProcess(nextProcess);
    // When we return here, this process has been resumed
}
```

---

### 2.4a Primitive-Fiber Integration: The Complete Flow

**Critical Integration Point:** Primitives 87 and 88 are the bridge between Smalltalk's `Process` abstraction and Windows fibers. Here's how they integrate:

#### Flow 1: Creating and Resuming a Process

```
Smalltalk Code:
    | process |
    process := [self doSomething] newProcess.  ← Creates Process object
    process priority: 4.
    process resume.  ← Calls Primitive 87
```

**Step-by-step:**

1. **Smalltalk: `process resume`**
   - Sends `resume` message to Process
   - VM looks up Primitive 87

2. **Primitive 87 Executes:**
   ```csharp
   public PrimitiveResult Execute(PrimitiveExecutionContext context)
   {
       var processOOP = context.Receiver;

       // Add to Processor's run queue (LinkedList at priority 4)
       LinkedListOperations.AddLast(heap, listOOP, processOOP);

       // Check if higher priority than active process
       if (priority > activePriority)
       {
           // FIBER INTEGRATION: Trigger immediate switch
           context.FiberScheduler.Reschedule();
           // Control never returns here if we switched
       }

       return PrimitiveResult.Ok(processOOP);
   }
   ```

3. **FiberScheduler.Reschedule():**
   ```csharp
   public void Reschedule()
   {
       // Save current process's context
       var currentContextOOP = _contextManager.ActiveContext;
       _heap.SetInstanceVariable(
           _currentProcessOOP,
           VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX,
           currentContextOOP
       );

       // Find highest priority runnable process
       var nextProcessOOP = FindHighestPriorityProcess();

       // Update Processor activeProcess
       _heap.SetInstanceVariable(
           _processorOOP,
           VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
           nextProcessOOP
       );

       // Get or create fiber for next process
       if (!_processFibers.TryGetValue(nextProcessOOP, out var nextFiber))
       {
           nextFiber = CreateProcessFiber(nextProcessOOP);
           _processFibers[nextProcessOOP] = nextFiber;
       }

       _currentProcessOOP = nextProcessOOP;

       // FIBER SWITCH: This is the actual Windows fiber switch
       SwitchToFiber(nextFiber);
       // When we return here, another process switched back to us
   }
   ```

4. **Windows Fiber Switch:**
   - CPU execution jumps to new fiber's stack
   - New fiber resumes where it left off (or starts if new)
   - Original fiber is suspended (stack preserved)

5. **New Process Runs:**
   ```csharp
   // ProcessFiberCallback for the new process
   private static void ProcessFiberProc(IntPtr parameter)
   {
       var context = (FiberContext)GCHandle.FromIntPtr(parameter).Target;

       // Get suspended context (where this process left off)
       var contextOOP = context.Heap.GetInstanceVariable(
           context.ProcessOOP,
           VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX
       );

       // Execute from that context
       context.VM.ExecuteExistingContext(contextOOP);
       // Returns when process terminates or yields
   }
   ```

#### Flow 2: Suspending a Process (Processor yield)

```
Smalltalk Code:
    Processor yield.  ← Give other processes a chance
```

**Smalltalk Implementation of `yield`:**
```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run"

    | semaphore |
    semaphore := Semaphore new.
    [semaphore signal] fork.  ← Creates new process that will signal
    semaphore wait.            ← Suspends current process (Primitive 86)
```

**Step-by-step:**

1. **Smalltalk: `semaphore wait`**
   - Calls Primitive 86 (wait on Semaphore)
   - If semaphore has no signals, suspends active process

2. **Primitive 86 (Wait):**
   ```csharp
   public PrimitiveResult Execute(PrimitiveExecutionContext context)
   {
       var semaphoreOOP = context.Receiver;
       var excessSignals = GetExcessSignals(semaphoreOOP);

       if (excessSignals > 0)
       {
           // Semaphore already signaled, just decrement
           SetExcessSignals(semaphoreOOP, excessSignals - 1);
           return PrimitiveResult.Ok(nil);
       }
       else
       {
           // No signals - must wait
           // Add current process to semaphore's waiting list
           var activeProcessOOP = GetActiveProcess();
           AddToFirstLink(semaphoreOOP, activeProcessOOP);

           // FIBER INTEGRATION: Suspend active process
           context.FiberScheduler.SuspendActiveProcess();
           // Never returns - fiber switched away
       }
   }
   ```

3. **FiberScheduler.SuspendActiveProcess():**
   ```csharp
   public void SuspendActiveProcess()
   {
       // Current process is already on semaphore's waiting list
       // Just save context and switch to next runnable

       var currentContextOOP = _contextManager.ActiveContext;
       _heap.SetInstanceVariable(
           _currentProcessOOP,
           VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX,
           currentContextOOP
       );

       // Find next highest priority runnable process
       var nextProcessOOP = FindHighestPriorityProcess();

       if (nextProcessOOP == _heap.NilOOP)
       {
           throw new InvalidOperationException("No runnable process!");
       }

       // Update Processor activeProcess
       _heap.SetInstanceVariable(
           _processorOOP,
           VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
           nextProcessOOP
       );

       // Switch to next process's fiber
       var nextFiber = _processFibers[nextProcessOOP];
       _currentProcessOOP = nextProcessOOP;

       // FIBER SWITCH: Suspend current, resume next
       SwitchToFiber(nextFiber);
       // When we return here, we've been resumed (via Primitive 87)
   }
   ```

4. **Next Process Runs:**
   - Control jumps to next process's fiber
   - That process continues execution
   - Original process's fiber is suspended (stack intact)

#### Flow 3: Resuming After Wait (Semaphore signal)

```
Smalltalk Code:
    semaphore signal.  ← Wake up a waiting process
```

**Step-by-step:**

1. **Smalltalk: `semaphore signal`**
   - Calls Primitive 85 (signal Semaphore)

2. **Primitive 85 (Signal):**
   ```csharp
   public PrimitiveResult Execute(PrimitiveExecutionContext context)
   {
       var semaphoreOOP = context.Receiver;
       var firstLinkOOP = GetFirstLink(semaphoreOOP);

       if (firstLinkOOP == _heap.NilOOP)
       {
           // No waiting processes, increment excessSignals
           var excessSignals = GetExcessSignals(semaphoreOOP);
           SetExcessSignals(semaphoreOOP, excessSignals + 1);
       }
       else
       {
           // Wake up first waiting process
           var processOOP = firstLinkOOP;

           // Remove from waiting list
           RemoveFromFirstLink(semaphoreOOP);

           // RESUME THE PROCESS
           // This is essentially Primitive 87 inline:

           // Add to run queue
           var priority = GetPriority(processOOP);
           var listOOP = GetRunQueueForPriority(priority);
           LinkedListOperations.AddLast(_heap, listOOP, processOOP);

           // If higher priority, switch immediately
           if (priority > GetActivePriority())
           {
               context.FiberScheduler.Reschedule();
           }
       }

       return PrimitiveResult.Ok(_heap.NilOOP);
   }
   ```

3. **Process Resumes:**
   - Process gets added back to run queue
   - If high enough priority, FiberScheduler.Reschedule() switches to it
   - Fiber resumes exactly where it suspended (in Primitive 86)
   - Primitive 86 returns to Smalltalk code
   - `semaphore wait` returns
   - Process continues execution

#### Key Integration Points

**PrimitiveExecutionContext Must Include FiberScheduler:**

```csharp
public class PrimitiveExecutionContext
{
    // Existing fields...
    public ISmalltalkHeap HeapManager { get; set; }
    public VirtualMachine VM { get; set; }

    // NEW: Fiber scheduler reference
    public FiberScheduler FiberScheduler { get; set; }

    // Other fields...
}
```

**VirtualMachine Must Have FiberScheduler:**

```csharp
public class VirtualMachine
{
    private readonly FiberScheduler _fiberScheduler;

    public VirtualMachine(/* ... */, FiberScheduler fiberScheduler)
    {
        _fiberScheduler = fiberScheduler;
        // ...
    }

    public PrimitiveExecutionContext CreatePrimitiveContext(...)
    {
        return new PrimitiveExecutionContext
        {
            FiberScheduler = _fiberScheduler,
            // ... other fields
        };
    }
}
```

**Process State Preserved in Fiber Stack:**

When a fiber is suspended via `SwitchToFiber()`:
- **C# call stack is preserved** (entire .NET stack frame)
- **VM state is preserved** (ActiveContext saved in Process)
- **Smalltalk stack is preserved** (in MethodContext indexed slots)
- **CPU registers are saved** (by Windows fiber mechanism)

When resumed:
- Fiber continues exactly where it left off
- C# execution resumes at the line after `SwitchToFiber()`
- VM state is restored from Process.suspendedContext
- Execution continues seamlessly

#### Example: Complete Process Switch Trace

```
Initial State:
  Active Process: Process A (priority 4) - running in Fiber A
  Run Queue: Empty

Smalltalk Code in Process A:
  | processB |
  processB := [Transcript show: 'Hello from B'] newProcess.
  processB priority: 5.  ← Higher than current
  processB resume.       ← Calls Primitive 87

Primitive 87 Executes:
  1. Adds Process B to run queue at priority 5
  2. Detects priority 5 > priority 4
  3. Calls FiberScheduler.Reschedule()

FiberScheduler.Reschedule():
  1. Saves Process A's context in Process A.suspendedContext
  2. Finds highest priority: Process B (priority 5)
  3. Creates Fiber B for Process B
  4. Sets Processor.activeProcess = Process B
  5. Calls SwitchToFiber(Fiber B) ← WINDOWS FIBER SWITCH

Now Running: Process B in Fiber B
  - Starts executing [Transcript show: 'Hello from B']
  - When done, process terminates
  - ProcessFiberCallback calls Reschedule()

FiberScheduler.Reschedule() (from Process B termination):
  1. Removes Process B from system
  2. Finds highest priority: Process A (priority 4)
  3. Sets Processor.activeProcess = Process A
  4. Calls SwitchToFiber(Fiber A) ← SWITCH BACK

Now Running: Process A in Fiber A
  - Resumes at line AFTER "processB resume"
  - Primitive 87 returns
  - Process A continues execution
```

**This is how Smalltalk's cooperative multitasking maps perfectly to Windows fibers!**

---

### 2.5 Window Creation Timing and Lifecycle

**Critical Design Point:** The WinForms window is created by C# **before** Smalltalk code runs, but the Smalltalk Display object already exists in the heap from cold start.

**Startup Sequence:**

```csharp
[STAThread]
static void Main(string[] args)
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    // STEP 1: Cold start creates Smalltalk heap (on main thread)
    // This creates the Display Form object (640x480 bitmap in heap)
    var bundle = ColdStartBundle.LoadFromFile(coldStartFile);
    var heap = ColdStartOrchestrator.Execute(bundle, diagnosticHub);

    // At this point:
    // - Display global exists (points to a Form object)
    // - Form has a bits ByteArray (38,400 bytes for 640x480)
    // - No window exists yet

    // STEP 2: Create event queue (shared between threads)
    var eventQueue = new SmalltalkEventQueue();

    // STEP 3: Create WinForms window (but don't show it yet)
    var displayProvider = new WinFormsDisplayProvider(eventQueue, heap);
    // displayProvider has:
    // - Reference to heap
    // - Reference to Display OOP
    // - Local display buffer (copy of Display bits)
    // - WinForms Form (window)

    // STEP 4: Create and start Smalltalk thread
    var smalltalkThread = new Thread(() => {
        RunSmalltalkInterpreter(heap, eventQueue, vm);
    });
    smalltalkThread.SetApartmentState(ApartmentState.MTA);
    smalltalkThread.Start();

    // STEP 5: Show the window
    displayProvider.Show();

    // STEP 6: Main thread becomes WinForms message pump
    Application.Run(displayProvider);
    // This blocks until window closes

    // STEP 7: Cleanup
    smalltalkThread.Join();
}

static void RunSmalltalkInterpreter(ISmalltalkHeap heap,
                                   SmalltalkEventQueue eventQueue,
                                   VirtualMachine vm)
{
    // Convert this thread to a fiber
    ConvertThreadToFiber(IntPtr.Zero);

    // Create fiber scheduler
    var fiberScheduler = new FiberScheduler(heap, vm, eventQueue);
    fiberScheduler.Initialize();

    // Run init expressions (in initial process fiber)
    var initExecutor = new InitExpressionExecutor(heap, vm);
    initExecutor.ExecuteAll();

    // Start system processes and controller loop
    // This never returns - fibers run forever
    StartSystemProcesses(heap, vm, fiberScheduler);
}
```

**Key Points:**

1. **Display Form Created During Cold Start**
   - Phase 3 of cold start creates the Display object
   - It's just a Form with a ByteArray of bits
   - Lives entirely in Smalltalk heap

2. **WinForms Window Created After Cold Start**
   - C# code creates actual OS window
   - Window knows about Display OOP (via reference to heap)
   - Window not shown until Smalltalk thread starts

3. **Two Separate Bitmaps**
   - **Smalltalk Display bits:** In heap (38,400 bytes)
   - **WinForms local buffer:** In C# memory (38,400 bytes)
   - Synchronized via Primitive 96 (BitBlt)

4. **No Window Primitives in Smalltalk**
   - Smalltalk doesn't create windows via primitives
   - Smalltalk only draws to Display Form
   - C# bridges Display to actual window

---

### 2.6 Display Bridge Architecture

**The Problem:** Smalltalk draws to Display Form (heap memory), but WinForms needs to render to the screen.

**The Solution:** Primitive 96 (BitBlt) acts as the bridge.

#### Data Flow

```
Smalltalk Process (Controller Fiber)
    ↓
Sends: Display fillWhite  (or any drawing command)
    ↓
Calls: BitBlt>>copyBits
    ↓
Primitive 96 executes:
    1. Performs raster op on Display Form's bits (in heap)
    2. Detects destination is Display
    3. Calls Platform.DisplayRegionChanged(rect)
    ↓
WinFormsDisplayProvider.DisplayRegionChanged (called from Smalltalk thread):
    1. Locks _displayLock
    2. Copies changed region from heap to _localDisplayBuffer
    3. BeginInvoke → Invalidate(rect) (signals WinForms thread)
    ↓
WinFormsDisplayProvider.OnPaint (runs on WinForms thread):
    1. Locks _displayLock
    2. Converts 1bpp → 32bpp for changed region
    3. Draws to screen via GDI+
```

#### Primitive 96 Implementation

```csharp
public class BitBltPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var bitBltOOP = context.Receiver;

        // Get the destination Form
        var destFormOOP = context.HeapManager.GetInstanceVariable(
            bitBltOOP,
            VMConstants.BITBLT_DESTFORM_INDEX
        );

        // Perform the BitBlt operation in Smalltalk memory
        // This modifies the destination Form's bits in the heap
        PerformBitBltOperation(context, bitBltOOP);

        // Check if destination is Display
        var displayOOP = context.RuntimeHeapAccess.GetGlobal("Display");

        if (destFormOOP == displayOOP)
        {
            // Get the affected rectangle
            var rect = GetDestinationRectangle(context, bitBltOOP);

            // Notify platform bridge (thread-safe call)
            context.Platform?.DisplayRegionChanged(rect);
        }

        return PrimitiveResult.Ok(context.Receiver);
    }

    private void PerformBitBltOperation(PrimitiveExecutionContext context,
                                       SmalltalkOOP bitBltOOP)
    {
        // Extract BitBlt parameters
        var sourceForm = GetSourceForm(context, bitBltOOP);
        var destForm = GetDestForm(context, bitBltOOP);
        var sourceRect = GetSourceRect(context, bitBltOOP);
        var destPoint = GetDestPoint(context, bitBltOOP);
        var rule = GetRule(context, bitBltOOP);

        // Perform actual raster operation
        // This operates directly on the ByteArray bits in the heap
        RasterOp(sourceForm, destForm, sourceRect, destPoint, rule);
    }
}
```

#### WinFormsDisplayProvider Implementation

```csharp
public class WinFormsDisplayProvider : Form, IPlatformBridge
{
    private readonly ISmalltalkHeap _heap;
    private readonly SmalltalkOOP _displayOOP;
    private readonly byte[] _localDisplayBuffer; // 38,400 bytes (local copy)
    private readonly Bitmap _backBuffer; // 32bpp for rendering
    private readonly object _displayLock = new object();
    private Rectangle _dirtyRegion = Rectangle.Empty;

    public WinFormsDisplayProvider(SmalltalkEventQueue eventQueue,
                                  ISmalltalkHeap heap)
    {
        _heap = heap;
        _eventQueue = eventQueue;

        // Get Display OOP from Smalltalk globals
        _displayOOP = _heap.LookupGlobal("Display");

        // Create local buffer (copy of Display bits)
        _localDisplayBuffer = new byte[38400]; // 640x480 @ 1bpp

        // Create 32bpp back buffer for rendering
        _backBuffer = new Bitmap(640, 480, PixelFormat.Format32bppArgb);

        // Configure window
        this.ClientSize = new Size(640, 480);
        this.DoubleBuffered = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.Text = "Smalltalk-80";

        // Initial sync from heap
        CopyEntireDisplayFromHeap();
    }

    // Called from Smalltalk thread (Primitive 96)
    public void DisplayRegionChanged(Rectangle region)
    {
        // Copy the changed region from heap to local buffer
        lock (_displayLock)
        {
            CopyDisplayBitsFromHeap(region);

            // Accumulate dirty region
            _dirtyRegion = _dirtyRegion.IsEmpty
                ? region
                : Rectangle.Union(_dirtyRegion, region);
        }

        // Signal WinForms thread to repaint (thread-safe)
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => {
                Invalidate(_dirtyRegion);
                _dirtyRegion = Rectangle.Empty;
            }));
        }
        else
        {
            Invalidate(_dirtyRegion);
            _dirtyRegion = Rectangle.Empty;
        }
    }

    private void CopyDisplayBitsFromHeap(Rectangle region)
    {
        // Get Display Form's bits ByteArray OOP
        var bitsOOP = _heap.GetInstanceVariable(
            _displayOOP,
            VMConstants.FORM_BITS_INDEX
        );

        // Copy bytes from heap to local buffer
        int bytesPerRow = 80; // (640 + 7) / 8
        int startY = region.Y;
        int endY = region.Y + region.Height;
        int startX = region.X / 8; // Byte boundary
        int width = (region.Width + 7) / 8;

        for (int y = startY; y < endY; y++)
        {
            int heapOffset = (y * bytesPerRow) + startX;
            int localOffset = (y * bytesPerRow) + startX;

            for (int x = 0; x < width; x++)
            {
                byte value = _heap.GetByteAt(bitsOOP, heapOffset + x);
                _localDisplayBuffer[localOffset + x] = value;
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        lock (_displayLock)
        {
            // Convert 1bpp _localDisplayBuffer to 32bpp _backBuffer
            CopyBufferToBitmap(e.ClipRectangle);
        }

        // Render to screen
        e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
    }

    private void CopyBufferToBitmap(Rectangle region)
    {
        // Lock bitmap for direct pixel access
        var bmpData = _backBuffer.LockBits(
            region,
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb
        );

        unsafe
        {
            byte* scan0 = (byte*)bmpData.Scan0;
            int stride = bmpData.Stride;

            for (int y = region.Y; y < region.Y + region.Height; y++)
            {
                for (int x = region.X; x < region.X + region.Width; x++)
                {
                    // Get bit from 1bpp buffer
                    int byteIndex = (y * 80) + (x / 8);
                    int bitIndex = 7 - (x % 8); // MSB first
                    byte value = _localDisplayBuffer[byteIndex];
                    bool bit = ((value >> bitIndex) & 1) == 1;

                    // Set pixel in 32bpp buffer
                    // Smalltalk: 1=black, 0=white
                    uint color = bit ? 0xFF000000 : 0xFFFFFFFF;
                    int pixelOffset = (y * stride) + (x * 4);
                    *(uint*)(scan0 + pixelOffset) = color;
                }
            }
        }

        _backBuffer.UnlockBits(bmpData);
    }
}
```

#### Thread Safety

**Three Critical Locks:**

1. **Event Queue Lock** (ConcurrentQueue - lock-free)
   - WinForms thread enqueues
   - Smalltalk thread dequeues
   - No explicit lock needed (ConcurrentQueue is thread-safe)

2. **Display Buffer Lock** (`_displayLock`)
   - Smalltalk thread writes (via `CopyDisplayBitsFromHeap`)
   - WinForms thread reads (in `OnPaint`)
   - Must lock to prevent tearing

3. **Cross-Thread Invocation** (`BeginInvoke`)
   - Smalltalk thread signals via `BeginInvoke`
   - WinForms thread executes `Invalidate`
   - .NET handles marshalling

**Lock Hierarchy:** Event Queue → Display Buffer Lock → BeginInvoke

**Deadlock Prevention:**
- Never call from WinForms thread into Smalltalk thread (one-way only)
- Use `BeginInvoke` (async) not `Invoke` (sync) to prevent blocking
- Lock durations kept minimal (copy only)

#### Optimization: Batched Updates

For better performance, batch display updates at 60 FPS:

```csharp
public class WinFormsDisplayProvider : Form
{
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public WinFormsDisplayProvider(...)
    {
        // ... existing initialization ...

        // Refresh at 60 FPS
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 16; // ~60 FPS
        _refreshTimer.Tick += (s, e) => {
            if (!_dirtyRegion.IsEmpty)
            {
                Invalidate(_dirtyRegion);
                _dirtyRegion = Rectangle.Empty;
            }
        };
        _refreshTimer.Start();
    }

    public void DisplayRegionChanged(Rectangle region)
    {
        lock (_displayLock)
        {
            CopyDisplayBitsFromHeap(region);
            _dirtyRegion = _dirtyRegion.IsEmpty
                ? region
                : Rectangle.Union(_dirtyRegion, region);
        }
        // Timer will trigger repaint (no immediate Invalidate)
    }
}
```

**Benefits:**
- Reduces `OnPaint` calls (batch multiple BitBlts)
- Smooth 60 FPS rendering
- Lower CPU usage

**Trade-off:**
- Maximum 16ms latency (acceptable for UI)

---

## 3. Event Queue Architecture

### 3.1 Event Representation

```csharp
public enum SmalltalkEventType
{
    Mouse,
    Keyboard,
    System
}

public class SmalltalkEvent
{
    public SmalltalkEventType Type { get; set; }

    // Mouse event data
    public int X { get; set; }
    public int Y { get; set; }
    public bool RedButton { get; set; }
    public bool YellowButton { get; set; }
    public bool BlueButton { get; set; }

    // Keyboard event data
    public int KeyCode { get; set; }
    public char Character { get; set; }
    public bool Shift { get; set; }
    public bool Control { get; set; }

    // Timestamp
    public long Milliseconds { get; set; }

    // Smalltalk input word format
    public int ToInputWord()
    {
        if (Type == SmalltalkEventType.Mouse)
        {
            // Bits 0-7: 0 (no character)
            // Bits 8-10: Button state (red=bit8, yellow=bit9, blue=bit10)
            int buttons = (RedButton ? 0x100 : 0) |
                          (YellowButton ? 0x200 : 0) |
                          (BlueButton ? 0x400 : 0);
            return buttons;
        }
        else // Keyboard
        {
            // Bits 0-7: Character code
            // Bits 8-10: Current mouse button state
            return (int)Character | GetCurrentButtonState();
        }
    }
}
```

### 3.2 Thread-Safe Event Queue

```csharp
public class SmalltalkEventQueue
{
    private readonly ConcurrentQueue<SmalltalkEvent> _events;
    private readonly object _lock = new object();
    private SmalltalkEvent _lastMouseState;

    public SmalltalkEventQueue()
    {
        _events = new ConcurrentQueue<SmalltalkEvent>();
    }

    // Called by WinForms thread
    public void Enqueue(SmalltalkEvent evt)
    {
        lock (_lock)
        {
            if (evt.Type == SmalltalkEventType.Mouse)
            {
                _lastMouseState = evt;
            }
            _events.Enqueue(evt);
        }
    }

    // Called by Smalltalk thread (in primitives)
    public bool TryDequeue(out SmalltalkEvent evt)
    {
        return _events.TryDequeue(out evt);
    }

    // Called by Smalltalk thread (Primitive 90: mousePoint)
    public (int x, int y) GetMousePosition()
    {
        lock (_lock)
        {
            if (_lastMouseState != null)
                return (_lastMouseState.X, _lastMouseState.Y);
            return (0, 0);
        }
    }

    // Called by Smalltalk thread (Primitive 95: inputWord)
    public int GetNextInputWord()
    {
        if (TryDequeue(out var evt))
        {
            return evt.ToInputWord();
        }
        return 0; // No event
    }
}
```

### 3.3 WinForms Event Handlers

```csharp
public class WinFormsDisplayProvider : Form
{
    private readonly SmalltalkEventQueue _eventQueue;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = (e.Button & MouseButtons.Left) != 0,
            YellowButton = (e.Button & MouseButtons.Middle) != 0,
            BlueButton = (e.Button & MouseButtons.Right) != 0,
            Milliseconds = Environment.TickCount64
        });
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _eventQueue.Enqueue(CreateMouseEvent(e));
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Keyboard,
            Character = e.KeyChar,
            KeyCode = (int)e.KeyChar,
            Milliseconds = Environment.TickCount64
        });
    }
}
```

### 3.4 Input Primitives Integration

```csharp
// Primitive 90: mousePoint
public class MousePointPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var (x, y) = context.EventQueue.GetMousePosition();

        // Create Point object
        var pointOOP = context.HeapManager.AllocateObject(
            context.RuntimeHeapAccess.GetClass("Point"),
            indexedSize: 0
        );

        context.HeapManager.SetInstanceVariable(pointOOP, 0,
            context.HeapManager.CreateSmallInteger(x));
        context.HeapManager.SetInstanceVariable(pointOOP, 1,
            context.HeapManager.CreateSmallInteger(y));

        return PrimitiveResult.Ok(pointOOP);
    }
}

// Primitive 95: inputWord
public class InputWordPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        int inputWord = context.EventQueue.GetNextInputWord();
        return PrimitiveResult.Ok(context.HeapManager.CreateSmallInteger(inputWord));
    }
}
```

---

## 4. Process Scheduler Implementation

### 4.1 FiberScheduler Core

```csharp
public class FiberScheduler
{
    private readonly ISmalltalkHeap _heap;
    private readonly SmalltalkEventQueue _eventQueue;
    private readonly VirtualMachine _vm;

    // Fiber management
    private readonly Dictionary<SmalltalkOOP, IntPtr> _processFibers;
    private IntPtr _mainFiber; // Converted thread
    private SmalltalkOOP _currentProcessOOP;

    public FiberScheduler(ISmalltalkHeap heap, VirtualMachine vm,
                         SmalltalkEventQueue eventQueue)
    {
        _heap = heap;
        _vm = vm;
        _eventQueue = eventQueue;
        _processFibers = new Dictionary<SmalltalkOOP, IntPtr>();
    }

    public void Initialize()
    {
        // Convert current thread to fiber
        _mainFiber = ConvertThreadToFiber(IntPtr.Zero);
        if (_mainFiber == IntPtr.Zero)
            throw new InvalidOperationException("Failed to convert thread to fiber");
    }

    public void RunInitialProcess(SmalltalkOOP processOOP)
    {
        _currentProcessOOP = processOOP;

        // Create fiber for initial process
        var fiber = CreateProcessFiber(processOOP);
        _processFibers[processOOP] = fiber;

        // Switch to it (never returns to main fiber)
        SwitchToFiber(fiber);
    }

    public void Reschedule()
    {
        // Save current process state
        var schedulerOOP = GetProcessorScheduler();

        // Find highest priority runnable process
        var nextProcessOOP = FindHighestPriorityProcess(schedulerOOP);

        if (nextProcessOOP == _heap.NilOOP)
        {
            throw new InvalidOperationException("No runnable process!");
        }

        // Update Processor activeProcess
        _heap.SetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX,
            nextProcessOOP
        );

        var oldProcessOOP = _currentProcessOOP;
        _currentProcessOOP = nextProcessOOP;

        // Get or create fiber
        if (!_processFibers.TryGetValue(nextProcessOOP, out var nextFiber))
        {
            nextFiber = CreateProcessFiber(nextProcessOOP);
            _processFibers[nextProcessOOP] = nextFiber;
        }

        // Switch fibers
        SwitchToFiber(nextFiber);
        // When we return here, we've been switched back to
    }

    private SmalltalkOOP FindHighestPriorityProcess(SmalltalkOOP schedulerOOP)
    {
        var listsOOP = _heap.GetInstanceVariable(
            schedulerOOP,
            VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX
        );

        // Search from priority 8 down to 1
        for (int priority = 8; priority >= 1; priority--)
        {
            var listOOP = _heap.GetIndexedField(listsOOP, priority - 1);

            if (!LinkedListOperations.IsEmpty(_heap, listOOP))
            {
                // Remove first process from list
                var processOOP = LinkedListOperations.RemoveFirst(_heap, listOOP);

                // Clear myList
                _heap.SetInstanceVariable(
                    processOOP,
                    VMConstants.PROCESS_MYLIST_INDEX,
                    _heap.NilOOP
                );

                return processOOP;
            }
        }

        return _heap.NilOOP;
    }

    private IntPtr CreateProcessFiber(SmalltalkOOP processOOP)
    {
        var context = new FiberContext
        {
            Scheduler = this,
            ProcessOOP = processOOP,
            VM = _vm,
            Heap = _heap
        };

        var handle = GCHandle.Alloc(context);

        return CreateFiberEx(
            stackSize: 1024 * 1024,  // 1MB stack
            commitSize: 64 * 1024,    // 64KB commit
            flags: FIBER_FLAG_FLOAT_SWITCH,
            callback: ProcessFiberCallback,
            parameter: GCHandle.ToIntPtr(handle)
        );
    }

    [UnmanagedCallersOnly]
    private static void ProcessFiberCallback(IntPtr parameter)
    {
        var handle = GCHandle.FromIntPtr(parameter);
        var context = (FiberContext)handle.Target;

        try
        {
            // Get suspended context from Process
            var suspendedContextOOP = context.Heap.GetInstanceVariable(
                context.ProcessOOP,
                VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX
            );

            // Execute until return or yield
            context.VM.ExecuteExistingContext(suspendedContextOOP);

            // Process terminated
            context.Scheduler.RemoveProcess(context.ProcessOOP);
            context.Scheduler.Reschedule();
        }
        catch (Exception ex)
        {
            // Handle fiber exception
            Console.WriteLine($"Fiber exception: {ex}");
            context.Scheduler.Reschedule();
        }
        finally
        {
            handle.Free();
        }
    }
}

public class FiberContext
{
    public FiberScheduler Scheduler { get; set; }
    public SmalltalkOOP ProcessOOP { get; set; }
    public VirtualMachine VM { get; set; }
    public ISmalltalkHeap Heap { get; set; }
}
```

### 4.2 Integration with VM

```csharp
// In VirtualMachine.cs - add FiberScheduler dependency

public class VirtualMachine : IPrimitiveContext
{
    private readonly FiberScheduler _fiberScheduler;

    public VirtualMachine(/* ... existing params ... */,
                         FiberScheduler fiberScheduler)
    {
        // ... existing initialization ...
        _fiberScheduler = fiberScheduler;
    }

    // Called by ProcessFiberCallback
    public SmalltalkOOP ExecuteExistingContext(SmalltalkOOP contextOOP)
    {
        _contextManager.ResetContextDepth();
        _contextManager.SetActiveContext(contextOOP);

        return InterpreterLoop();
    }

    private SmalltalkOOP InterpreterLoop()
    {
        _initialContextOOP = _contextManager.ActiveContext;

        while (true)
        {
            InterpreterStatus status = ExecuteBytecodeStep();

            switch (status)
            {
                case InterpreterStatus.Continue:
                    continue;

                case InterpreterStatus.Return:
                    // Check if returning to initial context
                    if (_contextManager.ActiveContext == _initialContextOOP)
                    {
                        return _returnValue; // Process completes
                    }
                    continue;

                case InterpreterStatus.Yield:
                    // Save context and switch fibers
                    SaveCurrentContext();
                    _fiberScheduler.Reschedule();
                    // When we return, we've been resumed
                    continue;
            }
        }
    }
}
```

---

## 5. Controller Loop Implementation

### 5.1 Controller Startup Sequence

**Smalltalk Code (Already Exists):**

```smalltalk
"In ControlManager>>searchForActiveController"
searchForActiveController
    | aController |
    activeController _ nil.
    activeControllerProcess _ Processor activeProcess.
    [Processor yield.
     aController _ scheduledControllers
        detect: [:c | c isControlWanted and: [c ~~ screenController]]
        ifNone: [screenController isControlWanted
                    ifTrue: [screenController]
                    ifFalse: [nil]].
     aController isNil]
        whileTrue.
    self activeController: aController.
    Processor terminateActive
```

**This creates the main event loop:**
1. Yields processor (`Processor yield` → fiber switch)
2. Searches for controller that wants control
3. If found, activates it
4. When controller terminates, searches again

### 5.2 Creating Controller Process

**Add to Startup Sequence:**

```smalltalk
"After all #initialize methods run:"

"1. Create background process"
ProcessorScheduler hiddenBackgroundProcess.

"2. Create ControlManager and ScreenController"
ScheduledControllers _ ControlManager new.

"3. Start controller search loop"
ScheduledControllers searchForActiveController.
```

**This will:**
- Create a Process at priority 4 (UserSchedulingPriority)
- That Process gets a Fiber
- Fiber runs `searchForActiveController` loop
- Loop yields, allowing other processes to run
- When resumed, checks for controllers wanting control

### 5.3 Controller Loop Integration

```csharp
// No C# changes needed! The Smalltalk code already implements the loop.
// We just need to:
// 1. Ensure Processor yield works (primitive 86 + fiber switching)
// 2. Ensure controller methods work (isControlWanted, etc.)
// 3. Ensure InputSensor works (primitives 90-95 with event queue)
```

---

## 6. Startup Sequence Implementation

### 6.1 Enhanced Init Expression Execution

**Current:** Init expressions run synchronously in initial process

**New:** Create separate processes for some init expressions

```csharp
public class InitExpressionExecutor
{
    private readonly FiberScheduler _fiberScheduler;

    public InitResult ExecuteInitExpression(string initLocalId,
                                           SmalltalkOOP compiledMethodOOP)
    {
        // For standard init expressions, execute in initial process (unchanged)
        if (!IsProcessCreationExpression(initLocalId))
        {
            return ExecuteInCurrentProcess(compiledMethodOOP);
        }

        // For process-creating expressions, create new process
        var processOOP = CreateProcessForInitExpression(compiledMethodOOP);

        // Resume the process (adds to scheduler, may switch fibers)
        ResumePrimitive.Execute(processOOP);

        return InitResult.Success(initLocalId);
    }
}
```

### 6.2 Startup Sequence

**Phase 1: Cold Start (Existing - No Changes)**
- Phases 1-6 build heap
- ProcessorScheduler created with initial Process
- InputSensor initialized

**Phase 2: Init Expressions (Modified)**

```csharp
// In Program.cs, after cold start:

// Initialize fiber scheduler
var fiberScheduler = new FiberScheduler(heapManager, vm, eventQueue);
fiberScheduler.Initialize();

// Run init expressions in initial process (existing)
initExecutor.ExecuteAll();

// NOW: Start the system processes

// 1. Create background process (Smalltalk code)
ExecuteSmalltalk("ProcessorScheduler hiddenBackgroundProcess");

// 2. Create timing process (Smalltalk code - in Delay initialize)
// Already done in init expressions

// 3. Create ControlManager and ScreenController
ExecuteSmalltalk("ScheduledControllers _ ControlManager new");

// 4. Start controller search loop (creates Process at priority 4)
// This call NEVER RETURNS - it switches to controller fiber
ExecuteSmalltalk("ScheduledControllers searchForActiveController");

// The system is now running!
// - Background fiber (priority 1) runs when idle
// - Controller fiber (priority 4) runs the event loop
// - Timing fiber (priority 8) handles Delays
// - All fibers cooperatively multitask via Processor yield
```

**Phase 3: Event Loop (New)**

```
Controller Process (Priority 4):
    searchForActiveController
        loop forever:
            Processor yield  ← switches to other processes
            check if any controller wants control
            if yes:
                activate controller
                controller startUp  ← runs controlLoop
                    while isControlActive:
                        Processor yield  ← switches to other processes
                        controlActivity  ← polls Sensor, updates view
            if controller terminates, loop again

Background Process (Priority 1):
    [true] whileTrue: []  ← runs when nothing else wants CPU

Timing Process (Priority 8):
    handles Delay>>wait via Semaphore signaling
```

---

## 7. Implementation Phases

### Phase 1: Fiber Infrastructure (Week 1)

**Tasks:**
1. Create `FiberScheduler` class
2. Implement `ConvertThreadToFiber` integration
3. Implement `CreateProcessFiber` and `SwitchToFiber` wrappers
4. Add fiber P/Invoke declarations
5. Create `FiberContext` structure
6. Test basic fiber creation and switching (no Smalltalk yet)

**Deliverables:**
- `Smalltalk80.Interpreter/Platform/Windows/FiberScheduler.cs`
- `Smalltalk80.Interpreter/Platform/Windows/NativeMethods.Fibers.cs`
- Unit tests for fiber creation/switching

**Success Criteria:**
- Can convert thread to fiber
- Can create and switch between 2 fibers
- Fibers can return control to each other
- No crashes or stack corruption

---

### Phase 2: Process-Fiber Mapping (Week 2)

**Tasks:**
1. Modify `PrimitiveExecutionContext` to include `FiberScheduler`
2. Implement Primitive 87 (resume) with fiber creation
3. Implement Primitive 88 (suspend) with fiber switching
4. Implement `Processor yield` support
5. Test process creation and switching

**Deliverables:**
- Updated `ControlPrimitives.cs` (primitives 87-88)
- Updated `VirtualMachine.cs` (fiber scheduler integration)
- Process switching tests

**Success Criteria:**
- Can create Smalltalk Process and map to fiber
- Primitive 87 creates fiber and switches
- Primitive 88 saves context and switches to next process
- `Processor yield` works (creates Semaphore, signals, waits)

---

### Phase 3: Event Queue Integration (Week 2)

**Tasks:**
1. Create `SmalltalkEventQueue` class
2. Create `SmalltalkEvent` structure
3. Modify `WinFormsDisplayProvider` to enqueue events
4. Update Primitive 90 (mousePoint) to use queue
5. Update Primitive 95 (inputWord) to use queue
6. Test event capture and retrieval

**Deliverables:**
- `Smalltalk80.Interpreter/Platform/EventQueue/SmalltalkEventQueue.cs`
- `Smalltalk80.Interpreter/Platform/EventQueue/SmalltalkEvent.cs`
- Updated `WinFormsDisplayProvider.cs`
- Updated `InputPrimitives.cs`

**Success Criteria:**
- WinForms events enqueued from UI thread
- Smalltalk can dequeue events from interpreter thread
- Mouse position updates correctly
- Keyboard events captured correctly
- No race conditions or deadlocks

---

### Phase 4: Background Process (Week 3)

**Tasks:**
1. Implement `ProcessorScheduler>>background:`
2. Test background process creation
3. Test idle loop (priority 1 process)
4. Verify process switching between priority levels

**Deliverables:**
- Smalltalk code for background process (already in source)
- Tests for background process creation
- Verification that background runs when no other process wants CPU

**Success Criteria:**
- Can create background process at priority 1
- Background process runs when controller yields
- Can switch between priority 1 and priority 4
- No infinite loops or starvation

---

### Phase 5: Controller Loop (Week 3-4)

**Tasks:**
1. Execute `ControlManager new` to create ScheduledControllers
2. Execute `searchForActiveController` to start controller loop
3. Test controller wants control detection
4. Test controller activation and termination
5. Test view updates and display integration

**Deliverables:**
- Working controller event loop
- Screen updates from Smalltalk
- Mouse tracking working
- System menu accessible (yellow button on gray)

**Success Criteria:**
- Controller loop runs continuously
- `Processor yield` allows process switching
- `isControlWanted` correctly detects cursor in view
- ScreenController responds to yellow button
- No freezes or hangs

---

### Phase 6: Delay and Timing (Week 4)

**Tasks:**
1. Implement Primitive 100 (signal:atMilliseconds:)
2. Test `Delay initialize` (creates timing process at priority 8)
3. Test `Delay>>wait` (suspends process until time expires)
4. Verify timing accuracy

**Deliverables:**
- Working Delay class
- Timing process at priority 8
- Accurate delay timing (within 10ms)

**Success Criteria:**
- `(Delay forMilliseconds: 1000) wait` pauses for 1 second
- Timing process runs at priority 8
- Multiple delays can be scheduled
- Delays work correctly across process switches

---

### Phase 7: Display Integration (Week 5)

**Tasks:**
1. Implement Primitive 96 (BitBlt) - actual raster operations
2. Implement Primitive 101 (beDisplay) - connect Form to display
3. Implement Primitive 102 (beCursor) - set cursor shape
4. Test Smalltalk drawing to display
5. Test cursor updates

**Deliverables:**
- Working BitBlt operations (copy, XOR, AND, OR)
- Display Form connected to WinForms bitmap
- Custom cursor shapes working

**Success Criteria:**
- Can draw from Smalltalk to display
- BitBlt operations render correctly
- Display updates visible in WinForms window
- Cursor shape changes work

---

### Phase 8: Integration and Testing (Week 6)

**Tasks:**
1. End-to-end testing of full startup
2. Performance profiling
3. Memory leak detection
4. Stress testing (many processes, many events)
5. Bug fixes and optimization

**Deliverables:**
- Full system startup working
- Performance benchmarks
- Memory usage analysis
- Bug fix documentation

**Success Criteria:**
- System starts and runs indefinitely
- No memory leaks over 1 hour
- Event latency < 50ms
- Stable under load (100+ processes)

---

## 8. Testing Strategy

### 8.1 Unit Tests

**Fiber Tests:**
```csharp
[Test]
public void FiberCreation_CreatesValidFiber()
{
    ConvertThreadToFiber(IntPtr.Zero);
    var fiber = CreateFiberEx(...);
    Assert.That(fiber, Is.Not.EqualTo(IntPtr.Zero));
}

[Test]
public void FiberSwitch_SwitchesAndReturns()
{
    ConvertThreadToFiber(IntPtr.Zero);
    bool fiber1Ran = false;
    bool fiber2Ran = false;

    var fiber1 = CreateFiber(() => {
        fiber1Ran = true;
        SwitchToFiber(fiber2);
        // Returns here when fiber2 switches back
    });

    var fiber2 = CreateFiber(() => {
        fiber2Ran = true;
        SwitchToFiber(fiber1);
    });

    SwitchToFiber(fiber1);

    Assert.That(fiber1Ran, Is.True);
    Assert.That(fiber2Ran, Is.True);
}
```

**Event Queue Tests:**
```csharp
[Test]
public void EventQueue_EnqueueDequeue_ThreadSafe()
{
    var queue = new SmalltalkEventQueue();
    var enqueueTask = Task.Run(() => {
        for (int i = 0; i < 1000; i++)
            queue.Enqueue(new SmalltalkEvent { Type = SmalltalkEventType.Mouse });
    });

    var dequeueTask = Task.Run(() => {
        int count = 0;
        while (count < 1000)
        {
            if (queue.TryDequeue(out _))
                count++;
        }
        return count;
    });

    Task.WaitAll(enqueueTask, dequeueTask);
    Assert.That(dequeueTask.Result, Is.EqualTo(1000));
}
```

### 8.2 Integration Tests

**Process Creation Test:**
```smalltalk
| semaphore process |
semaphore := Semaphore new.
process := [semaphore signal] newProcess.
process priority: Processor userSchedulingPriority.
process resume.
semaphore wait.
"If we get here, process ran successfully"
```

**Process Switching Test:**
```smalltalk
| results |
results := OrderedCollection new.
[results add: 'Process 1 start'.
 Processor yield.
 results add: 'Process 1 resume'] fork.
[results add: 'Process 2 start'.
 Processor yield.
 results add: 'Process 2 resume'] fork.
Processor yield.
results inspect.
"Should see interleaved execution"
```

**Controller Test:**
```smalltalk
"Start controller loop"
ScheduledControllers searchForActiveController.
"Should enter event loop and respond to input"
```

### 8.3 Regression Tests

**Always run:**
- BlockContext regression tests (120 tests)
- C# unit tests (388 tests)
- Init expression tests (46 expressions)

**After each phase:**
- Verify no regressions in existing tests
- Add new tests for new functionality

---

## 9. Risk Analysis and Mitigation

### 9.1 Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Fiber stack overflow | Medium | High | Use 1MB stack, monitor usage |
| Fiber switching bugs | High | High | Extensive unit tests, logging |
| Race conditions in queue | Medium | High | Use ConcurrentQueue, test thoroughly |
| Memory leaks in fibers | Medium | Medium | GCHandle management, profiling |
| Event starvation | Low | Medium | Priority-based scheduling |
| Display rendering bugs | Medium | Low | Incremental BitBlt implementation |
| Deadlocks | Low | High | Single-threaded Smalltalk execution |

### 9.2 Contingency Plans

**If fibers don't work:**
- Fallback: Simulate process switching in C# (trampoline pattern)
- Impact: More complex code, but same semantics

**If event queue has race conditions:**
- Fallback: Use locks instead of ConcurrentQueue
- Impact: Slight performance hit

**If display updates are slow:**
- Fallback: Batch updates, use double buffering
- Impact: Slight visual lag

---

## 10. Success Criteria

### 10.1 Minimum Viable Product (MVP)

✅ **System starts and runs indefinitely**
- No crashes or freezes
- Clean shutdown possible

✅ **Process scheduling works**
- Background process runs at priority 1
- Controller process runs at priority 4
- Timing process runs at priority 8
- Processes switch correctly via `Processor yield`

✅ **Event loop works**
- Mouse movement tracked
- Keyboard input captured
- Events consumed by Smalltalk

✅ **Controller loop works**
- `searchForActiveController` runs
- ScreenController responds to yellow button
- System menu accessible

✅ **Display updates work**
- Can draw from Smalltalk to screen
- BitBlt operations work
- Display refreshes correctly

### 10.2 Full Product

✅ **All MVP criteria**

✅ **Advanced features**
- Multiple scheduled controllers
- View hierarchy navigation
- Workspace and Browser operational
- Delay timing accurate
- Custom cursor shapes

✅ **Performance**
- Event latency < 50ms
- Display refresh rate > 30 FPS
- Process switch overhead < 1ms
- No memory leaks over 24 hours

✅ **Stability**
- Can run 1000+ processes
- Handles 10,000+ events/second
- Graceful error recovery
- Clean shutdown from any state

---

## Appendix A: File Structure

```
Smalltalk80.Interpreter/
├── Platform/
│   ├── Windows/
│   │   ├── FiberScheduler.cs                 [NEW]
│   │   ├── NativeMethods.Fibers.cs           [NEW]
│   │   ├── WinFormsDisplayProvider.cs        [MODIFIED]
│   │   └── ...
│   └── EventQueue/
│       ├── SmalltalkEventQueue.cs            [NEW]
│       └── SmalltalkEvent.cs                 [NEW]
├── Core/
│   ├── VM/
│   │   ├── VirtualMachine.cs                 [MODIFIED]
│   │   ├── Primitives/
│   │   │   ├── ControlPrimitives.cs          [MODIFIED]
│   │   │   ├── InputPrimitives.cs            [MODIFIED]
│   │   │   └── ...
│   │   └── ...
│   └── ...
├── Engine/
│   ├── InitExecution/
│   │   ├── InitExpressionExecutor.cs         [MODIFIED]
│   │   └── ...
│   └── ...
└── Program.cs                                 [MODIFIED]
```

---

## Appendix B: P/Invoke Declarations

```csharp
// NativeMethods.Fibers.cs

using System.Runtime.InteropServices;

namespace Smalltalk80.Interpreter.Platform.Windows;

internal static partial class NativeMethods
{
    private const string KERNEL32 = "kernel32.dll";

    // Fiber flags
    public const uint FIBER_FLAG_FLOAT_SWITCH = 0x1;

    // Convert thread to fiber
    [DllImport(KERNEL32, SetLastError = true)]
    public static extern IntPtr ConvertThreadToFiber(IntPtr lpParameter);

    // Create fiber
    [DllImport(KERNEL32, SetLastError = true)]
    public static extern IntPtr CreateFiberEx(
        nuint dwStackCommitSize,
        nuint dwStackReserveSize,
        uint dwFlags,
        FiberProc lpStartAddress,
        IntPtr lpParameter
    );

    // Switch to fiber
    [DllImport(KERNEL32)]
    public static extern void SwitchToFiber(IntPtr lpFiber);

    // Delete fiber
    [DllImport(KERNEL32)]
    public static extern void DeleteFiber(IntPtr lpFiber);

    // Fiber callback delegate
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void FiberProc(IntPtr lpParameter);
}
```

---

## Appendix C: Key Constants

```csharp
// VMConstants.cs additions

public static class VMConstants
{
    // Process instance variable indices
    public const int PROCESS_SUSPENDEDCONTEXT_INDEX = 1;
    public const int PROCESS_PRIORITY_INDEX = 2;
    public const int PROCESS_MYLIST_INDEX = 3;

    // ProcessorScheduler instance variable indices
    public const int PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX = 0;
    public const int PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX = 1;

    // Priority levels
    public const int SYSTEM_ROCK_BOTTOM_PRIORITY = 1;
    public const int SYSTEM_BACKGROUND_PRIORITY = 2;
    public const int USER_BACKGROUND_PRIORITY = 3;
    public const int USER_SCHEDULING_PRIORITY = 4;
    public const int USER_INTERRUPT_PRIORITY = 5;
    public const int LOW_IO_PRIORITY = 6;
    public const int HIGH_IO_PRIORITY = 7;
    public const int TIMING_PRIORITY = 8;
}
```

---

## Appendix D: References

**Bluebook:**
- Chapter 26: Protocol for Classes (Process, ProcessorScheduler)
- Chapter 27: Protocol for Controllers
- Chapter 30: Multiple Processes

**Smalltalk-80 Source:**
- `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st`
  - Lines 32217-32611: Process and ProcessorScheduler
  - Lines 12664-13100: Controller and ControlManager

**Current Implementation:**
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Platform/Windows/WinFormsDisplayProvider.cs`

**Windows Fibers:**
- https://learn.microsoft.com/en-us/windows/win32/procthread/fibers
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-convertthreadtofiber
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfiberex

---

**END OF PLAN**
