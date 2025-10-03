# Process and Scheduling Infrastructure Report for Smalltalk-80 Cold Start

## Executive Summary

This report details the complete Process and scheduling infrastructure required for executing precompiled methods after cold start in the Smalltalk-80 interpreter. Based on analysis of the source code and Blue Book specification, we identify the minimal requirements for basic method execution without full process scheduling.

## 1. Process Class Infrastructure

### 1.1 Core Classes Required

Based on the Smalltalk-80.st source analysis, the following classes are essential:

#### **Process** (Link subclass)
- **Instance Variables:**
  - `nextLink` (offset 0) - inherited from Link
  - `suspendedContext` (offset 1) - the MethodContext where this process is suspended
  - `priority` (offset 2) - SmallInteger priority level (1-8)
  - `myList` (offset 3) - the LinkedList this process is waiting on (or nil)

#### **ProcessorScheduler** (Object subclass)
- **Instance Variables:**
  - `quiescentProcessLists` (offset 0) - Array of LinkedLists, one per priority
  - `activeProcess` (offset 1) - the currently executing Process
- **Class Variables:**
  - `SystemRockBottomPriority` = 1
  - `SystemBackgroundPriority` = 2
  - `UserBackgroundPriority` = 3
  - `UserSchedulingPriority` = 4
  - `UserInterruptPriority` = 5
  - `LowIOPriority` = 6
  - `HighIOPriority` = 7
  - `TimingPriority` = 8

#### **Semaphore** (LinkedList subclass)
- **Instance Variables:**
  - `firstLink` (offset 0) - inherited from LinkedList
  - `lastLink` (offset 1) - inherited from LinkedList
  - `excessSignals` (offset 2) - SmallInteger count of excess signals

#### **LinkedList** (SequenceableCollection subclass)
- **Instance Variables:**
  - `firstLink` (offset 0) - first Link in the list
  - `lastLink` (offset 1) - last Link in the list

#### **Link** (Object subclass)
- **Instance Variables:**
  - `nextLink` (offset 0) - next Link in the chain

### 1.2 Minimal Required Methods

For basic execution without full scheduling:

**Process:**
- `forContext:priority:` (class method) - create Process for a context
- `resume` - primitive 87
- `suspend` - primitive 88
- `terminate` - clean up contexts

**ProcessorScheduler:**
- `activeProcess` - return the active process
- Instance must be accessible as global `Processor`

**Semaphore:**
- `signal` - primitive 85
- `wait` - primitive 86
- `initSignals` - initialize excessSignals to 0

## 2. Special Objects Array Requirements

### 2.1 Process-Related SOA Indices

Based on SpecialObjectsArray.cs analysis:

- **Index 7**: ProcessListIndex - Contains all active processes (can be nil for minimal)
- **Index 8**: ProcessorSchedulerIndex - The ProcessorScheduler singleton instance

### 2.2 ProcessorScheduler Contents

The ProcessorScheduler at index 8 must contain:
- `quiescentProcessLists`: Array of 8 LinkedLists (one per priority level)
- `activeProcess`: Reference to the currently executing Process

### 2.3 Active Process Tracking

The activeProcess is tracked as:
1. Instance variable in ProcessorScheduler
2. Must be a valid Process object with:
   - A suspendedContext pointing to current MethodContext
   - A priority (typically UserSchedulingPriority = 4)
   - myList = nil (not on any waiting list)

## 3. MethodContext Requirements

### 3.1 MethodContext Fields (from STContextLayouts.cs)

**Fixed Fields:**
- Offset 0: `sender` - link to calling context (or nil)
- Offset 1: `instructionPointer` - SmallInteger PC
- Offset 2: `stackPointer` - SmallInteger stack index
- Offset 3: `method` - CompiledMethod being executed
- Offset 4: `receiverMap` - nil (unused, for future multiple inheritance)
- Offset 5: `receiver` - the object receiving the message

**Variable Part:**
- Offsets 6+: Temporaries and stack space

### 3.2 BlockContext Fields

**Fixed Fields:**
- Offset 0: `sender` - link to calling context
- Offset 1: `instructionPointer` - SmallInteger PC
- Offset 2: `stackPointer` - SmallInteger stack index
- Offset 3: `nargs` - SmallInteger argument count
- Offset 4: `startpc` - SmallInteger starting PC
- Offset 5: `home` - MethodContext that created this block

### 3.3 Context Chain Requirements

For simple execution:
1. Root MethodContext has sender = nil
2. Each called method creates new MethodContext with sender pointing to caller
3. Returns restore previous context via sender chain

## 4. Minimal Execution Path

### 4.1 Simplified Execution Without Full Scheduling

For executing `testObject testSelector` after cold start:

```smalltalk
"Minimal setup pseudocode"
1. Create ProcessorScheduler instance
2. Set ProcessorScheduler at SOA index 8
3. Create initial Process with priority 4
4. Create root MethodContext for test method
5. Set Process suspendedContext to root context
6. Set ProcessorScheduler activeProcess to Process
7. Execute bytecodes in context
```

### 4.2 Required vs Optional Components

**Required for Basic Execution:**
- Process class with suspendedContext
- ProcessorScheduler with activeProcess
- MethodContext structure
- Basic primitives: 81-84 (block/perform)

**Can Be Stubbed/Bypassed:**
- Full process scheduling (primitives 87-88)
- Semaphore synchronization (primitives 85-86)
- Process priorities and switching
- Timer support (primitive 100)
- Multiple process queues

### 4.3 Bypass Strategy

Instead of full scheduling, implement a "single-process mode":
1. Create one Process object
2. Set it as activeProcess in ProcessorScheduler
3. Never actually suspend/resume
4. Context switching happens only via method calls/returns
5. No preemption or time-slicing needed

## 5. Blue Book Compliance

### 5.1 Blue Book References

**Process Architecture (Blue Book pages):**
- Page 571-580: Process and ProcessorScheduler specifications
- Page 251-252: Context structures (MethodContext, BlockContext)
- Page 583-584: Semaphore implementation
- Page 611-612: Primitive specifications for processes

### 5.2 Essential Primitives

**Absolutely Required:**
- None for single-process execution

**Required for Full Scheduling:**
- Primitive 85: Semaphore signal
- Primitive 86: Semaphore wait
- Primitive 87: Process resume
- Primitive 88: Process suspend
- Primitive 100: Timer signal at milliseconds

### 5.3 What Can Be Stubbed

For initial cold start testing:
1. **Primitives 85-88**: Return failure, let fallback code handle
2. **Primitive 100**: Ignore timer requests
3. **Process switching**: Stay on single process
4. **Semaphore synchronization**: Not needed without multiple processes
5. **Priority queues**: Use single process at fixed priority

## 6. Implementation Recommendations

### 6.1 Phased Approach

**Phase 1: Single-Process Execution**
```csharp
// Minimal setup in C#
var scheduler = CreateProcessorScheduler();
var process = CreateProcess(priority: 4);
var context = CreateMethodContext(method, receiver, args);
process.SuspendedContext = context;
scheduler.ActiveProcess = process;
SpecialObjectsArray.Set(8, scheduler);
// Execute in context
```

**Phase 2: Add Process Primitives**
- Implement primitives 87-88 for suspend/resume
- Add process queue management
- Support process termination

**Phase 3: Full Scheduling**
- Implement semaphores (primitives 85-86)
- Add timer support (primitive 100)
- Enable preemptive scheduling

### 6.2 Critical Setup Steps

1. **Create Classes in Order:**
   - Link → LinkedList → Semaphore
   - Link → Process
   - Object → ProcessorScheduler

2. **Initialize ProcessorScheduler:**
   ```smalltalk
   quiescentProcessLists := Array new: 8.
   1 to: 8 do: [:i | quiescentProcessLists at: i put: LinkedList new].
   ```

3. **Create Initial Process:**
   ```smalltalk
   initialProcess := Process forContext: nil priority: 4.
   Processor activeProcess: initialProcess.
   ```

4. **Wire to SOA:**
   ```csharp
   SpecialObjectsArray.Set(ProcessorSchedulerIndex, scheduler);
   ```

### 6.3 Validation Checklist

- [ ] ProcessorScheduler instance exists at SOA[8]
- [ ] ProcessorScheduler has valid activeProcess
- [ ] activeProcess has suspendedContext (can be nil initially)
- [ ] MethodContext has all 6 fixed fields
- [ ] Context sender chains work properly
- [ ] Receiver and method are set in contexts
- [ ] Stack pointer management is correct

## 7. Test Execution Example

For executing a simple test after cold start:

```csharp
// C# implementation outline
public SObjectRef ExecuteTest(SObjectRef receiver, string selector, params SObjectRef[] args)
{
    // 1. Look up method
    var method = LookupMethod(receiver.Class, selector);

    // 2. Create context (Process infrastructure already set up)
    var context = CreateMethodContext(method, receiver, args);

    // 3. Get current process (already exists from cold start)
    var processor = SpecialObjectsArray.Get(ProcessorSchedulerIndex);
    var activeProcess = processor.GetInstanceVariable(1); // activeProcess

    // 4. Set process context
    activeProcess.SetInstanceVariable(1, context); // suspendedContext

    // 5. Execute
    return ExecuteBytecodes(context);
}
```

## Conclusion

The minimal Process infrastructure for cold start method execution requires:
1. Basic Process and ProcessorScheduler objects
2. Proper MethodContext structure
3. Single active process (no scheduling needed)
4. SOA wiring for ProcessorScheduler

Full process scheduling with semaphores, priorities, and preemption can be added incrementally after basic execution works. The key insight is that Smalltalk code can execute with just one Process object and no actual process switching - the context mechanism handles all the stack management needed for method calls and returns.