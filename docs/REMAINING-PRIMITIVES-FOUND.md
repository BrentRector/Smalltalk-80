# Remaining Primitives - Found in Git History

**Date**: 2025-10-06
**Source Commit**: 26e5f0f ("feat: complete graphics and UI implementation")
**Location**: `Archive/Smalltalk80.OldInterpreter/Primitives/`

## Summary

Found complete implementations for the remaining 17 stub primitives in the old interpreter (commit 26e5f0f). These can serve as reference implementations to port to the current architecture.

## Process Management Primitives (85-88)

**Source File**: `Archive/Smalltalk80.OldInterpreter/Primitives/ProcessPrimitives.cs`

### Primitive 85: Semaphore signal
**Implementation Status**: ✅ Complete in old interpreter
**Architecture Used**:
- Direct field access to Semaphore (excessSignals field)
- LinkedList manipulation (firstLink, lastLink)
- VirtualMachine integration for process resumption

**Key Logic**:
```csharp
// If no processes waiting, increment excessSignals
if (firstLink.IsNil)
    excessSignals++;
// Otherwise, remove first waiting process and resume it
else
    ResumeProcess(RemoveFirstLink(semaphore));
```

### Primitive 86: Semaphore wait
**Implementation Status**: ✅ Complete in old interpreter
**Architecture Used**:
- Checks excessSignals counter
- Suspends active process if no signals available
- Adds process to semaphore's waiting list

**Key Logic**:
```csharp
if (excessSignals > 0)
    excessSignals--; // Continue execution
else
    AddLastLink(semaphore, activeProcess); // Suspend and wait
    SuspendProcess(activeProcess);
```

### Primitive 87: Process resume
**Implementation Status**: ✅ Complete in old interpreter
**Architecture Used**:
- Validates process is not already on a list (myList field)
- Adds process to scheduler's ready queue at its priority level
- Handles preemption if resumed process has higher priority

**Key Logic**:
```csharp
// Fail if already scheduled
if (!myList.IsNil)
    return Failure;

// Add to scheduler's priority queue
AddProcessToScheduler(process, priorityLevel);

// Check for preemption
if (priorityLevel > activeProcessPriority)
    // Scheduler handles preemption automatically
```

### Primitive 88: Process suspend
**Implementation Status**: ✅ Complete in old interpreter
**Architecture Used**:
- Different behavior for active vs non-active process
- Active: Requires context switching via VM
- Non-active: Remove from list and clear myList

**Key Logic**:
```csharp
if (process == activeProcess)
    vm.SuspendProcess(process); // Context switch required
else
    RemoveProcessFromList(process, myList);
    process.myList = nil;
```

## Object Enumeration Primitives (128-129, 139)

**Source File**: `Archive/Smalltalk80.OldInterpreter/Primitives/SystemPrimitives.cs`

### Primitive 128: someInstance
**Implementation Status**: ✅ Complete in old interpreter
**Purpose**: Returns an arbitrary instance of a class
**Architecture Used**: Memory enumeration via `GetAllObjects()`

**Key Logic**:
```csharp
foreach (var oop in memory.GetAllObjects())
{
    var obj = memory.GetObject(oop);
    if (obj.ClassOOP == receiverClass)
        return oop; // Return first instance found
}
return nil;
```

### Primitive 129: nextInstance
**Implementation Status**: ✅ Complete in old interpreter
**Purpose**: Returns the next instance of the same class after receiver
**Architecture Used**: Sequential memory enumeration

**Key Logic**:
```csharp
bool foundReceiver = false;
foreach (var oop in memory.GetAllObjects())
{
    if (foundReceiver && obj.ClassOOP == receiverClass)
        return oop; // Return next instance
    if (oop == receiver)
        foundReceiver = true;
}
return nil;
```

### Primitive 139: nextObject
**Implementation Status**: ✅ Complete in old interpreter
**Purpose**: Returns the next object in memory after receiver
**Architecture Used**: Sequential memory enumeration

**Key Logic**:
```csharp
bool foundReceiver = false;
foreach (var oop in memory.GetAllObjects())
{
    if (foundReceiver)
        return oop; // Return next object
    if (oop == receiver)
        foundReceiver = true;
}
return nil;
```

## Object Identity Swapping (68, 130)

**Source File**: `Archive/Smalltalk80.OldInterpreter/Primitives/ObjectPrimitives.cs` (68)
**Source File**: `Archive/Smalltalk80.OldInterpreter/Primitives/SystemPrimitives.cs` (130)

### Primitive 68: become:
**Implementation Status**: ⚠️ Partial in old interpreter (two-way swap documented)
**Purpose**: Swaps object identities (two-way become)
**Architecture Used**: Table-based memory with pointer forwarding

**Concept**: Swap all references between two objects

### Primitive 130: becomeForward:
**Implementation Status**: ✅ Complete in old interpreter
**Purpose**: One-way become (receiver becomes argument)
**Architecture Used**: Forward all references from receiver to other

**Key Logic**:
```csharp
// Cannot become with SmallIntegers
if (receiver.IsSmallInteger || other.IsSmallInteger)
    return Failure;

// Forward all references from receiver to other
memory.ForwardReferences(receiver, other);
return other;
```

## Additional System Primitives

### Primitive 131: instanceCount
**Implementation Status**: ✅ Complete in old interpreter
**Purpose**: Returns count of instances of a class
**Architecture Used**: Memory enumeration with counter

### Primitives 114-116, 134-138
**Status**: Various system operations (snapshot, quit, GC control)
**Implementation**: Some have stubs, some have partial implementations

## Architecture Differences

The old interpreter uses:
1. **OOP struct** instead of `int` for object pointers
2. **ObjectMemory** with direct field access instead of `SmalltalkHeap` with LayoutResolver
3. **VirtualMachine** instance methods for process control
4. **Direct field indices** instead of dynamic field lookup

## Migration Strategy

To port these to the current interpreter:

### 1. Process Primitives (85-88)
**Requirements**:
- ProcessorScheduler support in VirtualMachine
- Process lifecycle methods: `SuspendProcess()`, `ResumeProcess()`
- Active process tracking
- Context switching infrastructure

**Approach**:
- Implement ProcessorScheduler field access via LayoutResolver
- Add VM methods for process control
- Convert OOP field access to `ctx.GetFieldValue()` / `ctx.SetFieldValue()`
- Use LinkedList helper methods for queue management

### 2. Enumeration Primitives (128-129, 139)
**Requirements**:
- `SmalltalkHeap.GetAllObjects()` iterator method
- Class comparison via heap

**Approach**:
- Add `IEnumerable<int>` to SmalltalkHeap for object enumeration
- Convert class comparison to use heap's GetClass() method
- Handle SmallInteger vs heap object distinction

### 3. become: Operations (68, 130)
**Requirements**:
- Pointer forwarding in ObjectTable
- Memory table manipulation

**Approach**:
- Implement `ForwardReferences()` method in ObjectTable
- Scan entire object table and update all references
- Handle special cases (SmallIntegers, nil, true, false)

## Priority Recommendation

**HIGH PRIORITY**: Process Primitives (85-88)
- Required for Smalltalk process scheduler to work
- Enables concurrent execution
- Needed for UI responsiveness (event loop)

**MEDIUM PRIORITY**: Enumeration Primitives (128-129, 139)
- Used by development tools (inspector, debugger)
- Required for class browser "instances" functionality
- Less critical for basic execution

**LOW PRIORITY**: become: Operations (68, 130)
- Rarely used in typical Smalltalk code
- Primarily for advanced metaprogramming
- Table-based architecture already supports this

## Next Steps

1. Review process scheduler implementation in old VM
2. Design ProcessorScheduler integration for new VirtualMachine
3. Implement process lifecycle methods
4. Port process primitives 85-88 one at a time
5. Test with simple semaphore/process examples
6. Add enumeration support to SmalltalkHeap
7. Implement enumeration primitives 128-129, 139
8. Consider become: operations last

## Files to Reference

**Old Interpreter** (commit 26e5f0f):
- `Archive/Smalltalk80.OldInterpreter/Primitives/ProcessPrimitives.cs` - Complete process/semaphore code
- `Archive/Smalltalk80.OldInterpreter/Primitives/SystemPrimitives.cs` - Enumeration and become primitives
- `Archive/Smalltalk80.OldInterpreter/VM/VirtualMachine.cs` - Process control methods

**Current Interpreter** (needs updates):
- `Smalltalk80.Interpreter/VM/Primitives/ControlPrimitives.cs` - Has stubs for 85-88
- `Smalltalk80.Interpreter/VM/Primitives/SystemPrimitives.cs` - Has stubs for enumeration
- `Smalltalk80.Interpreter/VM/VirtualMachine.cs` - Needs ProcessorScheduler integration
- `Smalltalk80.Interpreter/VM/SmalltalkHeap.cs` - Needs GetAllObjects() iterator
