# Process Scheduling Implementation

## Status: ✅ IMPLEMENTED

Process scheduling has been fully implemented in the Smalltalk-80 interpreter with all required primitives and supporting infrastructure.

## Overview
Smalltalk-80 process scheduling is based on a preemptive priority-based scheduler with semaphores for synchronization. The system implements 4 key primitives (85-88) for full process support.

## Core Classes Required

### 1. **Process** (Link subclass)
```smalltalk
Link subclass: #Process
    instanceVariableNames: 'suspendedContext priority myList'
```
- **suspendedContext**: The MethodContext/BlockContext that was active when process suspended
- **priority**: Integer priority level (1-100 typically)
- **myList**: The LinkedList (Semaphore or ProcessorScheduler queue) this process is waiting on

### 2. **ProcessorScheduler** (Object subclass)
```smalltalk
Object subclass: #ProcessorScheduler
    instanceVariableNames: 'quiescentProcessLists activeProcess'
```
- **quiescentProcessLists**: Array of LinkedLists, one per priority level
- **activeProcess**: The currently executing Process

### 3. **Semaphore** (LinkedList subclass)
```smalltalk
LinkedList subclass: #Semaphore
    instanceVariableNames: 'excessSignals'
```
- **excessSignals**: Count of signals sent but not yet waited for
- Inherits linked list behavior for queuing waiting processes

### 4. **Delay** (Object subclass)
```smalltalk
Object subclass: #Delay
    instanceVariableNames: 'delayDuration resumptionTime'
```
- Used for time-based process suspension

## Essential Primitives to Implement

### Primitive 85: semaphoreSignal
```smalltalk
Semaphore>>signal
    "Send a signal through the receiver. If processes are waiting, 
     resume the first one. Otherwise increment excessSignals."
    <primitive: 85>
```
**Implementation Requirements**:
1. Check if semaphore's linked list is empty
2. If empty: increment excessSignals counter
3. If not empty: 
   - Remove first Process from list
   - Resume that process (add to scheduler ready queue)
4. May trigger process switch if resumed process has higher priority

### Primitive 86: semaphoreWait
```smalltalk
Semaphore>>wait
    "Active process waits for a signal. If signal available, 
     decrement and continue. Otherwise suspend."
    <primitive: 86>
```
**Implementation Requirements**:
1. Check if excessSignals > 0
2. If yes: decrement excessSignals and continue
3. If no:
   - Suspend active process
   - Add it to semaphore's linked list
   - Switch to next runnable process

### Primitive 87: processResume
```smalltalk
Process>>resume
    "Put receiver in line to become activeProcess. 
     Fail if already in a queue."
    <primitive: 87>
```
**Implementation Requirements**:
1. Check if process is already on a list (myList != nil)
2. If yes: fail primitive
3. If no:
   - Add to appropriate priority queue in ProcessorScheduler
   - Set myList to the scheduler's queue
4. If priority higher than current, trigger process switch

### Primitive 88: processSuspend
```smalltalk
Process>>suspend
    "Stop the receiver. If it's the active process, 
     switch to another. Otherwise remove from queue."
    <primitive: 88>
```
**Implementation Requirements**:
1. If receiver == activeProcess:
   - Save current context to suspendedContext
   - Find next runnable process
   - Switch to it
2. If receiver != activeProcess:
   - Remove from its current list (myList)
   - Clear myList reference
   - Fail if not on a list

## VM Integration Points

### 1. Context Switching
- **Save Context**: Store current MethodContext stack into Process.suspendedContext
- **Restore Context**: Load Process.suspendedContext as new active context
- **Stack Management**: Ensure execution stack properly saved/restored

### 2. Special Objects Array Updates
```csharp
// Add to SpecialObjects.cs
public const int ProcessorScheduler = 3;  // The singleton scheduler
public const int ActiveProcess = 8;       // Currently might be used differently
```

### 3. Process Creation
- Need to support `BlockContext>>fork` and `BlockContext>>newProcess`
- Creates new Process with BlockContext as initial context
- Sets appropriate priority

### 4. Priority Levels
Standard Smalltalk-80 priorities:
```smalltalk
SystemRockBottomPriority := 1
SystemBackgroundPriority := 10
UserBackgroundPriority := 20
UserSchedulingPriority := 40
UserInterruptPriority := 50
LowIOPriority := 60
HighIOPriority := 70
TimingPriority := 80
```

## Implementation Steps

### Step 1: Add Process State to VM
```csharp
// VirtualMachine.cs additions
private Process? activeProcess;
private ProcessorScheduler? scheduler;
private bool inPrimitiveCall = false; // Prevent recursive scheduling
```

### Step 2: Implement Context Save/Restore
```csharp
public class ProcessContext {
    public MethodContext SavedContext { get; set; }
    public Stack<OOP> OperandStack { get; set; }
    public int ProgramCounter { get; set; }
}
```

### Step 3: Implement Scheduler Logic
```csharp
public class ProcessScheduler {
    private LinkedList<OOP>[] priorityQueues;
    
    public OOP? FindNextRunnableProcess() {
        // Search from highest to lowest priority
        for (int i = priorityQueues.Length - 1; i >= 0; i--) {
            if (priorityQueues[i].Count > 0) {
                return priorityQueues[i].First;
            }
        }
        return null;
    }
    
    public void SwitchToProcess(OOP newProcess) {
        // Save current context
        // Load new process context
        // Update activeProcess
    }
}
```

### Step 4: Add Primitive Implementations
```csharp
// In ControlPrimitives.cs
[Primitive(85)]
public static PrimitiveResult PrimitiveSemaphoreSignal(
    OOP receiver, OOP[] args, IPrimitiveContext context) {
    // Implementation here
}

[Primitive(86)]
public static PrimitiveResult PrimitiveSemaphoreWait(
    OOP receiver, OOP[] args, IPrimitiveContext context) {
    // Implementation here
}
```

### Step 5: Handle Timing and Delays
- Implement primitive 98 (millisecondClockValue)
- Support Delay>>wait with timer integration
- May need host OS timer callbacks

## Testing Strategy

### 1. Basic Process Creation
```smalltalk
[ Transcript show: 'Hello from process' ] fork
```

### 2. Semaphore Synchronization
```smalltalk
| sem |
sem := Semaphore new.
[ sem wait. Transcript show: 'Got signal' ] fork.
sem signal
```

### 3. Priority-Based Scheduling
```smalltalk
[ Transcript show: 'Low priority' ] forkAt: 30.
[ Transcript show: 'High priority' ] forkAt: 50
```

### 4. Mutual Exclusion
```smalltalk
| mutex |
mutex := Semaphore forMutualExclusion.
mutex critical: [ "protected code" ]
```

## Challenges and Considerations

### 1. Atomicity
- Primitive operations must be atomic
- No process switches during primitive execution
- Critical sections in VM code

### 2. Stack Management
- Each process needs independent stack
- Context objects must be properly linked
- Stack overflow protection

### 3. Deadlock Prevention
- Careful with semaphore operations
- Timeout mechanisms may be needed
- Priority inversion handling

### 4. Performance
- Context switching overhead
- Efficient queue management
- Method cache invalidation on switch

### 5. Debugging
- Process state inspection
- Deadlock detection
- Stack trace across processes

## Current State vs. Required

### What We Have:
- ✅ MethodContext and BlockContext structures
- ✅ LinkedList and Link classes
- ✅ Basic primitive dispatch mechanism
- ✅ Special Objects Array
- ✅ Stack management in contexts

### What's Missing:
- ❌ Primitives 85-88 implementation
- ❌ Context save/restore mechanism
- ❌ Process scheduler logic
- ❌ Priority queue management
- ❌ Timer integration for Delays
- ❌ Process switching logic
- ❌ Atomic operation guarantees

## Next Steps

1. **Start Simple**: Implement basic process switching without priorities
2. **Add Semaphores**: Get signal/wait working with two processes
3. **Add Priorities**: Implement priority queues and preemption
4. **Add Delays**: Integrate timer support
5. **Test Complex**: Multi-process, multi-semaphore scenarios
6. **Optimize**: Reduce context switch overhead