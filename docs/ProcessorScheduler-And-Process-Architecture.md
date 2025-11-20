# ProcessorScheduler and Process Architecture Documentation

## Overview

The `ProcessorScheduler` class is a singleton object named `Processor` that manages the scheduling and execution of multiple `Process` objects in the Smalltalk-80 interpreter. The system implements a priority-based process scheduler with 8 priority levels (1-8).

---

## 1. Process Class Structure

### Class Definition (Smalltalk-80.st, lines 31790-31909)

```smalltalk
Link subclass: #Process
    instanceVariableNames: 'suspendedContext priority myList'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Processes'!
```

### Instance Variables

| Variable | Type | Purpose |
|----------|------|---------|
| `suspendedContext` | Context | The execution context (stack frame) suspended when the process was paused. For the active process, this is nil. |
| `priority` | Integer (1-8) | Priority level determining scheduling order. Higher numbers = higher priority. |
| `myList` | LinkedList | Reference to the LinkedList in ProcessorScheduler.quiescentProcessLists on which this process is suspended. nil if active. |

### Process Inheritance

- **Superclass**: `Link`
- **Link instance variables**: `nextLink` (pointer to next element in linked list)
- **Usage**: Processes form a chain within LinkedLists (one per priority level)

### Key Methods

**Changing Process State**
- `resume` - Schedule process to run (primitive 87)
- `suspend` - Stop process so it can be restarted later (primitive 88)
- `terminate` - Kill the process permanently

**Changing Suspended State**
- `install: aContext` - Replace suspendedContext with new context
- `popTo: aContext` - Pop contexts and release them up to specified context

**Accessing**
- `priority` - Get process priority
- `priority: anInteger` - Set process priority (validated against highestPriority)
- `suspendedContext` - Get suspended context
- `suspendingList` - Get the LinkedList this process is suspended on
- `offList` - Notify process it's been removed from list (sets myList to nil)

---

## 2. ProcessorScheduler Class Structure

### Class Definition (Smalltalk-80.st, lines 31985-32186)

```smalltalk
Object subclass: #ProcessorScheduler
    instanceVariableNames: 'quiescentProcessLists activeProcess'
    classVariableNames: 'BackgroundProcess HighIOPriority LowIOPriority 
                         SystemBackgroundPriority SystemRockBottomPriority 
                         TimingPriority UserBackgroundPriority UserInterruptPriority 
                         UserSchedulingPriority'
    poolDictionaries: ''
    category: 'Kernel-Processes'!
```

### Instance Variables

| Variable | Type | Purpose |
|----------|------|---------|
| `quiescentProcessLists` | Array[LinkedList] | Array of 8 LinkedLists (one per priority 1-8). Each LinkedList contains Process objects waiting at that priority. |
| `activeProcess` | Process | The currently executing Process object. This process is NOT on any LinkedList. |

### Class Variables (Priority Levels)

| Variable | Default Value | Purpose |
|----------|---------------|---------|
| `SystemRockBottomPriority` | 1 | Lowest priority - background tasks |
| `SystemBackgroundPriority` | 2 | System background processes (GC, status checks) |
| `UserBackgroundPriority` | 3 | User background processes |
| `UserSchedulingPriority` | 4 | Window scheduler (main UI event loop) |
| `UserInterruptPriority` | 5 | User processes needing immediate service |
| `LowIOPriority` | 6 | Most I/O processes (keyboard, mouse, network input) |
| `HighIOPriority` | 7 | Time-critical I/O (network) |
| `TimingPriority` | 8 | Highest priority - real-time clock processes |
| `BackgroundProcess` | nil | Current background process (set via ProcessorScheduler class>>background:) |

### Singleton Pattern

ProcessorScheduler is instantiated **exactly once** as the `Processor` global variable. The `new` method is overridden to prevent additional instances:

```smalltalk
!ProcessorScheduler class methodsFor: 'instance creation'!
new
    "New instances of ProcessorScheduler should not be created."
    self error: 'New ProcessSchedulers should not be created since
the integrity of the system depends on a unique scheduler'! !
```

### Key Methods

**Accessing**
- `activePriority` - Answer priority of currently running Process
- `activeProcess` - Answer the currently executing Process
- `highestPriority` - Answer number of priority levels (8)
- `highestPriority: newHighestPriority` - Change number of priority levels

**Removing**
- `remove: aProcess ifAbsent: aBlock` - Remove Process from its wait list

**Process State Change**
- `suspendFirstAt: aPriority` - Suspend first Process at given priority
- `suspendFirstAt: aPriority ifNone: noneBlock` - Same with block if none
- `terminateActive` - Terminate the active process
- `yield` - Give other processes at current priority a chance to run

**Timing**
- `signal: aSemaphore atTime: timeInterval` - Signal Semaphore at given time
- `signal: aSemaphore atMilliseconds: milliseconds` (primitive 100) - Low-level timing

**Priority Name Accessors** (return class variable values)
- `timingPriority` → 8
- `highIOPriority` → 7
- `lowIOPriority` → 6
- `userInterruptPriority` → 5
- `userSchedulingPriority` → 4
- `userBackgroundPriority` → 3
- `systemBackgroundPriority` → 2

**Background Process**
- `background: aBlock` - Install background process running aBlock
- `hiddenBackgroundProcess` - Install invisible background process
- `sweepHandBackgroundProcess` - Install background process showing sweep animation

**Class Initialization**
```smalltalk
!ProcessorScheduler class methodsFor: 'class initialization'!
initialize
    SystemRockBottomPriority _ 1.
    SystemBackgroundPriority _ 2.
    UserBackgroundPriority _ 3.
    UserSchedulingPriority _ 4.
    UserInterruptPriority _ 5.
    LowIOPriority _ 6.
    HighIOPriority _ 7.
    TimingPriority _ 8! !

ProcessorScheduler initialize!  "Call at load time"
```

---

## 3. Cold Start Initialization (Phase 3)

Location: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_ObjectAllocation.cs`

### ProcessorScheduler Allocation (Lines 183-194)

1. **Allocate ProcessorScheduler singleton** (line 184):
   ```csharp
   var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
   ```

2. **Set class reference** (lines 187-188):
   ```csharp
   var processorSchedulerClassOOP = GlobalRegistry.LookupClassOOP("ProcessorScheduler");
   _allocator.SetObjectClass(processorSchedulerOOP, processorSchedulerClassOOP);
   ```

3. **Register in GlobalRegistry** (line 191):
   ```csharp
   GlobalRegistry.Register("ProcessorScheduler", processorSchedulerOOP);
   ```

4. **Initialize fields** (line 194):
   ```csharp
   InitializeProcessorSchedulerFields(processorSchedulerOOP, nilOOP);
   ```

### Field Initialization Details (Lines 222-255)

#### Step 1: Create quiescentProcessLists (Instance Variable 0)

```csharp
// Create Array of 8 LinkedLists (one per priority level)
var linkedListClassOOP = GlobalRegistry.LookupClassOOP("LinkedList");
var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
var arrayOOP = _allocator.AllocateArray(arrayClassOOP, 8);

// Initialize each LinkedList with firstLink = nil, lastLink = nil
for (int i = 0; i < 8; i++)
{
    var linkedListOOP = _allocator.AllocateFixedObject("LinkedList");
    _allocator.SetObjectClass(linkedListOOP, linkedListClassOOP);
    // LinkedList instance variables: [0]=firstLink, [1]=lastLink
    _allocator.SetInstanceVariable(linkedListOOP, 0, nilOOP);  // firstLink
    _allocator.SetInstanceVariable(linkedListOOP, 1, nilOOP);  // lastLink
    _allocator.SetIndexedOOP(arrayOOP, i, linkedListOOP);
}

// Set ProcessorScheduler.quiescentProcessLists (index 0)
_allocator.SetInstanceVariable(processorSchedulerOOP, 0, arrayOOP);
```

**Result**: quiescentProcessLists = Array of 8 empty LinkedLists, one for each priority

#### Step 2: Create activeProcess (Instance Variable 1)

```csharp
// Create main/initial Process
var processClassOOP = GlobalRegistry.LookupClassOOP("Process");
var processOOP = _allocator.AllocateFixedObject("Process");
_allocator.SetObjectClass(processOOP, processClassOOP);

// Initialize Process instance variables:
// [0] suspendedContext = nil (main process is active, not suspended)
_allocator.SetInstanceVariable(processOOP, 0, nilOOP);

// [1] priority = 4 (UserSchedulingPriority)
_allocator.SetInstanceVariable(processOOP, 1, SmalltalkOOP.FromSmallInteger(4));

// [2] myList = nil (active process not on any list)
_allocator.SetInstanceVariable(processOOP, 2, nilOOP);

// Set ProcessorScheduler.activeProcess (index 1)
_allocator.SetInstanceVariable(processorSchedulerOOP, 1, processOOP);
```

**Result**: activeProcess = Process with priority 4, no suspended context, not on any list

### What Cold Start Initializes vs. Smalltalk

| Item | Cold Start | Smalltalk |
|------|-----------|-----------|
| ProcessorScheduler singleton allocation | ✓ YES (Phase 3) | Not needed (already allocated) |
| ProcessorScheduler.quiescentProcessLists | ✓ YES (8 empty LinkedLists) | Not needed (setup at startup) |
| ProcessorScheduler.activeProcess | ✓ YES (main Process) | Can modify during execution |
| Process priority values (class variables) | ✗ NO | ✓ YES (ProcessorScheduler initialize) |
| BackgroundProcess (class variable) | ✗ NO | ✓ YES (set to nil initially) |
| Processor global variable | ✓ PARTIAL (OOP created, registered) | ✓ COMPLETE (Association created in Phase 3.5) |

### Processor Global Registration (Phase 3.5, Lines 1016-1042)

The ProcessorScheduler singleton is registered as a global variable through the Association system:

```csharp
// Pre-create canonical Association for "ProcessorScheduler"
var resultAssocOOP = AssociationRegistry.GetOrCreate("ProcessorScheduler", () =>
{
    SmalltalkOOP valueOOP;
    if (GlobalRegistry.TryGet("ProcessorScheduler", out var preCreatedValue))
    {
        valueOOP = preCreatedValue;  // Use the pre-created object from Phase 3
    }
    else
    {
        valueOOP = nilOOP;
    }
    
    var assocOOP = _dictionaryInstaller.AllocateAssociation(keySymbolOOP, valueOOP);
    return assocOOP;
});
```

This creates an Association in the Undeclared dictionary mapping the Symbol "ProcessorScheduler" to the ProcessorScheduler singleton OOP.

---

## 4. Scheduling Algorithm

### Priority Queue Structure

```
ProcessorScheduler.quiescentProcessLists
  [0] -> LinkedList -> Process(priority=1) <-> Process(priority=1) <-> ...
  [1] -> LinkedList -> Process(priority=2) <-> Process(priority=2) <-> ...
  [2] -> LinkedList -> Process(priority=3) <-> ...
  ...
  [7] -> LinkedList -> Process(priority=8) <-> ...
```

### Scheduling Rules

1. **Active Process**: Only ONE process is activeProcess
2. **Suspended Processes**: Other processes are suspended on LinkedLists, indexed by priority
3. **Selection**: When activeProcess yields/suspends, the next process is selected from the highest-priority non-empty LinkedList
4. **Within-Priority Scheduling**: Processes at the same priority are executed FIFO (first in LinkedList, first out)

### Example Execution Flow

```
Initial: activeProcess = Process(priority=4)
         quiescentProcessLists[4] = []
         quiescentProcessLists[5] = [ProcessA, ProcessB]
         quiescentProcessLists[7] = [ProcessC]

Action:  activeProcess.yield

Result:  activeProcess = ProcessC (highest priority with processes)
         quiescentProcessLists[7] = []
         quiescentProcessLists[5] = [ProcessA, ProcessB]
         quiescentProcessLists[4] = [original process]
```

---

## 5. LinkedList Structure (Supporting Data)

LinkedList is used to implement the wait queues for each priority level.

### LinkedList Instance Variables

| Variable | Type | Purpose |
|----------|------|---------|
| `firstLink` | Link/Process | First element in the chain |
| `lastLink` | Link/Process | Last element in the chain |

### LinkedList Methods

- `isEmpty` - Check if list is empty
- `first` - Get first element
- `last` - Get last element
- `add: aLink` / `addLast: aLink` - Add to end
- `addFirst: aLink` - Add to beginning
- `remove: aLink ifAbsent: aBlock` - Remove element
- `removeFirst` - Remove first element
- `removeLast` - Remove last element

---

## 6. Integration Points

### Process Creation

```smalltalk
" In Block/Smalltalk code:
[some code] fork.                              "Create and schedule process immediately"
[some code] forkAt: Processor lowIOPriority.   "Create at specific priority"
Process forContext: aContext priority: aPriority.  "Create manually"
```

### Process Suspension/Resumption

```smalltalk
aProcess suspend.           "Remove from scheduler, save context"
aProcess resume.            "Schedule to run again (primitive 87)"
aProcess priority: 6.       "Change priority"
aProcess terminate.         "Kill process"
```

### Timing Integration

```smalltalk
Processor signal: aSemaphore atTime: timeValue.  "Wake semaphore at time"
Processor yield.                                   "Let other processes run"
Processor highestPriority.                         "Get max priority (8)"
```

---

## 7. Key Files

- **Smalltalk Source**: `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st`
  - Process class: Lines 31790-31909
  - ProcessorScheduler class: Lines 31985-32186
  - ProcessorScheduler class>>initialize: Lines 32135-32145

- **Cold Start Code**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_ObjectAllocation.cs`
  - Allocation: Lines 183-194
  - Field initialization: Lines 222-255
  - Association registration: Lines 1016-1042

---

## 8. Summary

| Aspect | Details |
|--------|---------|
| **Singleton Pattern** | Processor global = single ProcessorScheduler instance |
| **Instance Variables** | quiescentProcessLists (8 LinkedLists), activeProcess (Process) |
| **Priority Levels** | 8 levels (1=lowest, 8=highest). Timing priority=8, User scheduling=4 |
| **Cold Start** | Phase 3 allocates Processor singleton and creates 8 empty LinkedLists + main Process |
| **Runtime Setup** | ProcessorScheduler initialize() sets priority class variables |
| **Scheduling** | FIFO within priority, strict priority preemption across priorities |
| **Data Structure** | Array of LinkedLists (queues), each queue chains Processes via Link.nextLink |

