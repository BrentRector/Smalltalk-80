# Init Expression Execution Implementation Plan

**Created**: 2025-01-15  
**Updated**: 2025-01-15 (Revised based on Smalltalk-80.st source analysis)  
**Status**: Implementation Phase - Revised Approach  
**Goal**: Implement proper Process/ProcessorScheduler integration for init expression execution

## Executive Summary

This document provides a comprehensive plan for implementing initialization expression execution in the Smalltalk-80 interpreter with proper Process and ProcessorScheduler integration. The implementation follows **actual Smalltalk-80.st source code patterns** and Blue Book semantics.

**Key Revision**: After analyzing Smalltalk-80.st source, we now understand that:
1. **Primitives manage activeProcess**, not user code
2. Process resume/suspend (primitives 87/88) handle all activeProcess manipulation
3. Init expressions should follow the BlockContext→newProcess→resume pattern
4. We don't manually set ProcessorScheduler.activeProcess

## Table of Contents

1. [Overview and Design Goals](#overview-and-design-goals)
2. [ProcessorScheduler Architecture](#processorscheduler-architecture)
3. [Process Object Design](#process-object-design)
4. [Implementation Recipe](#implementation-recipe)
5. [Detailed Implementation Steps](#detailed-implementation-steps)
6. [GC Root Management Strategy](#gc-root-management-strategy)
7. [Testing and Verification](#testing-and-verification)
8. [Implementation Checklist](#implementation-checklist)

---

## Overview and Design Goals

### Primary Goals

1. **Blue Book Compliance**: Implement Process/ProcessorScheduler exactly as specified in Smalltalk-80
2. **Proper GC Rooting**: Ensure active Process and MethodContext are GC roots during execution
3. **Clean Lifecycle**: Proper creation, execution, and cleanup of transient Process objects
4. **Minimal Changes**: Integrate with existing VirtualMachine and InitExpressionExecutor
5. **Diagnostic Support**: Maintain structured error reporting and timeout enforcement

### Current State

- ✅ **ProcessorScheduler Singleton**: Created in Phase 6, initialized in Phase Y, installed as "Processor" global in Phase 12
- ✅ **VirtualMachine**: Has CreateMethodContext and execution loop
- ✅ **InitExpressionExecutor**: Exists but doesn't use Process/ProcessorScheduler
- ⚠️ **ProcessorScheduler State**: Not initialized (quiescentProcessLists = nil, activeProcess = nil)
- ⚠️ **Process Class**: Exists in image but no instances created

### Design Approach (Revised)

Based on analysis of **Smalltalk-80.st source code**, we will implement the **canonical Smalltalk pattern**:

1. Create a **BlockContext** for each init expression (already done - CompiledMethod exists)
2. Create a **Process** via `BlockContext newProcess` pattern
3. Call **primitive 87 (resume)** on the Process, which:
   - Sets ProcessorScheduler.activeProcess to this Process
   - Begins execution from Process.suspendedContext
   - Runs until completion or yield
4. Process terminates, primitive clears activeProcess
5. No manual activeProcess manipulation (primitives handle it)

**Key Insight**: The Smalltalk-80 primitives already implement proper Process management. We just need to:
- Implement **primitive 87 (resume)** to set activeProcess and execute
- Implement **primitive 88 (suspend)** for completeness
- Follow the BlockContext→Process→resume pattern from the source

---

## ProcessorScheduler Architecture

### Role and Core Responsibilities

The ProcessorScheduler maintains process queues, selects the next runnable Process, and exposes primitives for enqueueing, yielding, preemption, termination, and querying process lists.

**Key Responsibilities**:
- Maintain ready queues organized by priority
- Hold strong references to active Processes (GC roots)
- Select next runnable Process
- Track activeProcess during execution
- Provide process lifecycle management (create, schedule, terminate)

### ProcessorScheduler Instance Variables

Based on Blue Book specification and Smalltalk-80.st source:

```csharp
public class ProcessorSchedulerLayout
{
    // Instance variables (from coldstart.json class definition):
    public SmalltalkOOP quiescentProcessLists;  // Array of LinkedLists, indexed by priority
    public SmalltalkOOP activeProcess;          // Currently executing Process (or nil)
    
    // Typical priorities: 0-7, where lower numbers = higher priority
    // quiescentProcessLists[i] = LinkedList of Processes at priority i
}
```

### Initialization Requirements

ProcessorScheduler initialization in Phase 6:

```csharp
// Initialize ProcessorScheduler during cold start (Phase 6)
void InitializeProcessorSchedulerFields(SmalltalkOOP processorSchedulerOOP, SmalltalkOOP nilOOP)
{
    // Initialize all instance variables to nil
    // - activeProcess: nil (no process running)
    // - quiescentProcessLists: nil (for now - can be Array new: 8 later)
    
    int instVarCount = CalculateTotalInstanceVariableCount("ProcessorScheduler", _bundle);
    for (int i = 0; i < instVarCount; i++)
    {
        _allocator.SetInstanceVariable(processorSchedulerOOP, i, nilOOP);
    }
}
```

✅ **Already implemented** in Phase 6 (Phase6_ObjectAllocation.cs)

### Primitives Handle activeProcess

**Critical Understanding from Smalltalk-80.st**:

The **primitives manage activeProcess**, not user code:

```smalltalk
Process>>resume
    "Primitive 87 - sets activeProcess to receiver and begins execution"
    <primitive: 87>
    self primitiveFailed

Process>>suspend  
    "Primitive 88 - removes from activeProcess, saves context"
    <primitive: 88>
    Processor activeProcess == self
        ifTrue: [self primitiveFailed]
        ifFalse: [Processor remove: self ifAbsent: [...]]
```

We must implement these primitives to properly manage activeProcess.

---

## Process Object Design

### Essential Fields

Based on Blue Book specification:

```csharp
public class ProcessLayout
{
    // Instance variables (from coldstart.json class definition):
    public SmalltalkOOP suspendedContext;   // MethodContext (top frame) when not running
    public SmalltalkOOP priority;           // SmallInteger (0-7, lower = higher priority)
    public SmalltalkOOP myList;             // LinkedList this Process belongs to (or nil)
    
    // For full scheduler:
    // public SmalltalkOOP nextLink;        // Next Process in linked list
    // public SmalltalkOOP name;            // Symbol (debugging)
}
```

### Lifecycle States

For init expression execution:

1. **Created**: Process object allocated, suspendedContext set to initial MethodContext
2. **Active**: ProcessorScheduler.activeProcess points to this Process, VM executing
3. **Terminated**: Execution complete, ProcessorScheduler.activeProcess set back to nil

We do NOT implement: #ready, #blocked, #waiting states (no concurrency needed).

### Process Creation Pattern

```csharp
int CreateProcessForInitExpression(int methodContextId)
{
    // 1. Get Process class
    if (!GlobalRegistry.TryGet("class:Process", out int processClassId))
        throw new InvalidOperationException("Process class not found");
    
    // 2. Allocate Process instance
    var processOOP = _heapManager.AllocateObject(
        classOOP: SmalltalkOOP.FromObjectTableIndex(processClassId),
        format: ObjectFormat.Pointer,
        fixedSlots: 3,  // suspendedContext, priority, myList
        indexedSize: 0
    );
    int processId = processOOP.AsObjectTableIndex();
    
    // 3. Initialize fields
    SetNamedSlot(processId, "suspendedContext", methodContextId);
    SetNamedSlot(processId, "priority", EncodeSmallInteger(4));  // Default priority
    SetNamedSlot(processId, "myList", GlobalRegistry.NilObjectId);  // Not in any list
    
    return processId;
}
```

---

## Implementation Recipe (Revised Based on Smalltalk-80.st)

This recipe follows the **actual Smalltalk-80.st pattern** for executing code via Process/ProcessorScheduler.

### Pattern from Smalltalk-80.st

```smalltalk
"How BlockContext creates and executes a Process:"
BlockContext>>newProcess
    "Answer a new process running the code in the receiver."
    ^Process
        forContext: 
            [self value.
            Processor terminateActive]
        priority: Processor activePriority

BlockContext>>fork
    "Create and schedule a process running the code in the receiver."
    self newProcess resume
```

### Step 1: Get CompiledMethod (Already Done)

The init expression CompiledMethod exists from cold start Phase 8:

```csharp
// Get init expression method from localIdToObjectId map
if (!_localIdToObjectId.TryGetValue(initLocalId, out int methodId))
    throw new InvalidOperationException($"Init expression {initLocalId} not found");
```

### Step 2: Create BlockContext/MethodContext

Create an initial context for the init expression:

```csharp
// Determine receiver for init expression
// Typically: Smalltalk global (SystemDictionary) or nil
int receiverId = GlobalRegistry.SystemDictionaryId;

// Create MethodContext for the init expression
// This is the "suspendedContext" that will be stored in Process
int contextId = CreateMethodContextForInitExpression(methodId, receiverId);
```

### Step 3: Create Process (Following Smalltalk Pattern)

Create a Process following the `Process forContext:priority:` pattern:

```csharp
// Create Process instance
int processId = CreateProcess(
    suspendedContext: contextId,
    priority: 4  // UserSchedulingPriority
);
```

### Step 4: Execute via Primitive 87 (resume)

Call **primitive 87** which handles everything:

```csharp
try
{
    // Invoke primitive 87 (Process>>resume)
    // This primitive will:
    // 1. Set ProcessorScheduler.activeProcess to this Process
    // 2. Execute from Process.suspendedContext
    // 3. Run until return/yield
    // 4. Clear activeProcess when done
    
    int resultId = InvokePrimitive87_Resume(processId);
    
    return InitResult.Success(initLocalId, elapsedMs, resultId);
}
catch (Exception ex)
{
    return InitResult.Failed(initLocalId, ex);
}
```

### Step 5: Primitive 87 Implementation

The primitive handles all activeProcess management:

```csharp
// Primitive 87: Process>>resume
private int Primitive87_Resume(int processOOP)
{
    // 1. Get ProcessorScheduler singleton
    int schedulerId = GlobalRegistry.Get("ProcessorScheduler");
    
    // 2. Set activeProcess to this Process
    int activeProcessIndex = GetInstanceVariableIndex("ProcessorScheduler", "activeProcess");
    SetInstanceVariable(schedulerId, activeProcessIndex, processOOP);
    
    // 3. Add Process to GC roots (activeProcess is now root via Processor global)
    AddProcessorSchedulerToGCRoots(schedulerId);
    
    // 4. Get Process.suspendedContext
    int suspendedContextIndex = GetInstanceVariableIndex("Process", "suspendedContext");
    int contextId = GetInstanceVariable(processOOP, suspendedContextIndex).AsObjectTableIndex();
    
    // 5. Execute the context
    int resultId = ExecuteContext(contextId);
    
    // 6. Clear activeProcess (process terminated)
    SetInstanceVariable(schedulerId, activeProcessIndex, GlobalRegistry.NilObjectId);
    
    return resultId;
}
```

### Step 6: Cleanup (Automatic)

No manual cleanup needed - the primitive handles it:
- activeProcess cleared by primitive when Process terminates
- Process becomes unreachable and GC collects it
- MethodContext also collected

---

## Detailed Implementation Steps (Revised)

### Phase 1: Initialize ProcessorScheduler ✅ COMPLETE

**File**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase6_ObjectAllocation.cs`

**Status**: ✅ **Already implemented** (lines 56-64, 403-430)

ProcessorScheduler.activeProcess and quiescentProcessLists are initialized to nil in Phase 6.

---

### Phase 2: Implement Primitive 87 (Process>>resume)

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ProcessPrimitives.cs` (NEW)

**Purpose**: Implement the resume primitive that manages activeProcess

```csharp
namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Implements Process-related primitives (87-88) following Blue Book specification.
/// These primitives manage ProcessorScheduler.activeProcess and process execution.
/// </summary>
public class ProcessPrimitives
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly VirtualMachine _vm;
    
    public ProcessPrimitives(
        ISmalltalkHeapManager heapManager, 
        LayoutResolver layoutResolver,
        VirtualMachine vm)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _vm = vm;
    }
    
    /// <summary>
    /// Primitive 87: Process>>resume
    /// Resumes the receiver Process by setting it as activeProcess and executing its suspendedContext.
    /// </summary>
    public int Resume(int processOOP)
    {
        // 1. Get ProcessorScheduler singleton (Processor global)
        if (!GlobalRegistry.TryGet("ProcessorScheduler", out int schedulerId))
            throw new InvalidOperationException("ProcessorScheduler not found");
        
        var schedulerOOP = SmalltalkOOP.FromObjectTableIndex(schedulerId);
        
        // 2. Get current activeProcess (should be nil for init expressions)
        int activeProcessIndex = _layoutResolver.GetFieldIndex("class:ProcessorScheduler", "activeProcess");
        var currentActiveOOP = _heapManager.GetInstanceVariable(schedulerOOP, activeProcessIndex);
        
        if (currentActiveOOP.AsObjectTableIndex() != GlobalRegistry.NilObjectId)
        {
            // Another process is active - this is process switching (not needed for init expressions)
            throw new InvalidOperationException("Process switching not yet implemented");
        }
        
        // 3. Set activeProcess to receiver Process
        _heapManager.SetInstanceVariable(
            schedulerOOP, 
            activeProcessIndex, 
            SmalltalkOOP.FromObjectTableIndex(processOOP)
        );
        
        // 4. Get Process.suspendedContext
        int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
        var processOOPObj = SmalltalkOOP.FromObjectTableIndex(processOOP);
        var suspendedContextOOP = _heapManager.GetInstanceVariable(processOOPObj, suspendedContextIndex);
        int contextId = suspendedContextOOP.AsObjectTableIndex();
        
        // 5. Execute the suspended context
        // Note: ProcessorScheduler is already reachable via "Processor" global in SystemDictionary
        // So activeProcess is automatically a GC root
        int resultId = ExecuteContext(contextId);
        
        // 6. Clear activeProcess (process terminated normally)
        _heapManager.SetInstanceVariable(
            schedulerOOP, 
            activeProcessIndex, 
            SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId)
        );
        
        return resultId;
    }
    
    /// <summary>
    /// Executes a MethodContext until it returns.
    /// </summary>
    private int ExecuteContext(int contextId)
    {
        // Get method and receiver from context
        int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
        int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");
        
        var contextOOP = SmalltalkOOP.FromObjectTableIndex(contextId);
        var methodOOP = _heapManager.GetInstanceVariable(contextOOP, methodIndex);
        var receiverOOP = _heapManager.GetInstanceVariable(contextOOP, receiverIndex);
        
        // Execute via VirtualMachine
        // Note: VM.Execute() will use the pre-created context instead of creating a new one
        return _vm.ExecuteExistingContext(contextId);
    }
}
```

---

### Phase 3: Add ExecuteExistingContext to VirtualMachine

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

**Purpose**: Execute a pre-created MethodContext (used by primitive 87)

```csharp
namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages Process lifecycle and ProcessorScheduler integration.
/// Provides simplified process management for init expression execution.
/// </summary>
public class ProcessManager
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    
    public ProcessManager(ISmalltalkHeapManager heapManager, LayoutResolver layoutResolver)
    {
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
    }
    
    /// <summary>
    /// Creates a Process for executing the given MethodContext.
    /// </summary>
    public int CreateProcess(int methodContextId, int priority = 4)
    {
        // Get Process class
        if (!GlobalRegistry.TryGet("class:Process", out int processClassId))
            throw new InvalidOperationException("Process class not found");
        
        // Allocate Process instance
        var processOOP = _heapManager.AllocateObject(
            classOOP: SmalltalkOOP.FromObjectTableIndex(processClassId),
            format: ObjectFormat.Pointer,
            fixedSlots: GetInstanceVariableCount("Process"),
            indexedSize: 0
        );
        int processId = processOOP.AsObjectTableIndex();
        
        // Initialize suspendedContext
        int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
        _heapManager.SetInstanceVariable(processOOP, suspendedContextIndex, 
            SmalltalkOOP.FromObjectTableIndex(methodContextId));
        
        // Initialize priority
        int priorityIndex = _layoutResolver.GetFieldIndex("class:Process", "priority");
        _heapManager.SetInstanceVariable(processOOP, priorityIndex, 
            SmalltalkOOP.FromSmallInteger(priority));
        
        // Initialize myList to nil
        int myListIndex = _layoutResolver.GetFieldIndex("class:Process", "myList");
        _heapManager.SetInstanceVariable(processOOP, myListIndex, 
            SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId));
        
        return processId;
    }
    
    /// <summary>
    /// Sets the active process in ProcessorScheduler.
    /// </summary>
    public void SetActiveProcess(int processId)
    {
        if (!GlobalRegistry.TryGet("ProcessorScheduler", out int schedulerId))
            throw new InvalidOperationException("ProcessorScheduler not found");
        
        int activeProcessIndex = _layoutResolver.GetFieldIndex("class:ProcessorScheduler", "activeProcess");
        _heapManager.SetInstanceVariable(
            SmalltalkOOP.FromObjectTableIndex(schedulerId),
            activeProcessIndex,
            SmalltalkOOP.FromObjectTableIndex(processId)
        );
    }
    
    /// <summary>
    /// Clears the active process in ProcessorScheduler.
    /// </summary>
    public void ClearActiveProcess()
    {
        SetActiveProcess(GlobalRegistry.NilObjectId);
    }
    
    /// <summary>
    /// Adds Process to GC roots temporarily.
    /// Returns the original roots for restoration.
    /// </summary>
    public SmalltalkOOP[] AddProcessToGCRoots(int processId)
    {
        var originalRoots = _heapManager.GetGCRoots();
        var activeRoots = originalRoots.Append(SmalltalkOOP.FromObjectTableIndex(processId)).ToArray();
        _heapManager.SetGCRoots(activeRoots);
        return originalRoots;
    }
    
    private int GetInstanceVariableCount(string className)
    {
        // Use LayoutResolver to get instance variable count
        var layout = _layoutResolver.GetLayout($"class:{className}");
        return layout?.InstanceVariables.Length ?? 0;
    }
}
```

---

### Phase 4: Update InitExpressionExecutor (Simplified)

**File**: `Smalltalk80.Interpreter/Engine/InitExecution/InitExpressionExecutor.cs`

**Changes**: Simplified - use primitive 87 instead of manual activeProcess management

```csharp
private readonly ProcessPrimitives _processPrimitives;

public InitExpressionExecutor(
    ISmalltalkHeapManager heapManager,
    VirtualMachine vm,
    LayoutResolver layoutResolver,
    Dictionary<string, int> localIdToObjectId,
    InitPolicy policy = InitPolicy.AbortOnFirstError,
    int defaultTimeoutMs = 5000)
{
    _heapManager = heapManager;
    _vm = vm;
    _localIdToObjectId = localIdToObjectId;
    _policy = policy;
    _defaultTimeoutMs = defaultTimeoutMs;
    _processPrimitives = new ProcessPrimitives(heapManager, layoutResolver, vm);
}

private InitResult ExecuteOne(int methodId, string initLocalId, CancellationToken cancellationToken)
{
    var sw = Stopwatch.StartNew();
    
    try
    {
        // 1. Determine receiver (Smalltalk global)
        int receiverId = GlobalRegistry.SystemDictionaryId;
        
        // 2. Create MethodContext for init expression
        int contextId = _vm.CreateMethodContext(
            methodId: methodId,
            receiverId: receiverId,
            arguments: new int[0],
            callerContextId: 0
        );
        
        // 3. Create Process with this context
        int processId = CreateProcess(contextId, priority: 4);
        
        // 4. Execute via primitive 87 (resume)
        // This handles all activeProcess management automatically
        int resultId = _processPrimitives.Resume(processId);
        
        sw.Stop();
        return InitResult.Success(initLocalId, sw.ElapsedMilliseconds, resultId);
    }
    catch (TimeoutException)
    {
        sw.Stop();
        return InitResult.Timeout(initLocalId, sw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        sw.Stop();
        return InitResult.Failed(initLocalId, ex);
    }
    // No finally needed - primitive handles cleanup
}

private int CreateProcess(int suspendedContextId, int priority)
{
    // Get Process class
    if (!GlobalRegistry.TryGet("class:Process", out int processClassId))
        throw new InvalidOperationException("Process class not found");
    
    // Allocate Process instance
    var processOOP = _heapManager.AllocateObject(
        classOOP: SmalltalkOOP.FromObjectTableIndex(processClassId),
        format: ObjectFormat.Pointer,
        fixedSlots: 3,  // suspendedContext, priority, myList
        indexedSize: 0
    );
    
    // Initialize fields
    int suspendedContextIndex = _layoutResolver.GetFieldIndex("class:Process", "suspendedContext");
    int priorityIndex = _layoutResolver.GetFieldIndex("class:Process", "priority");
    int myListIndex = _layoutResolver.GetFieldIndex("class:Process", "myList");
    
    _heapManager.SetInstanceVariable(processOOP, suspendedContextIndex, 
        SmalltalkOOP.FromObjectTableIndex(suspendedContextId));
    _heapManager.SetInstanceVariable(processOOP, priorityIndex, 
        SmalltalkOOP.FromSmallInteger(priority));
    _heapManager.SetInstanceVariable(processOOP, myListIndex, 
        SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId));
    
    return processOOP.AsObjectTableIndex();
}
```

---

## GC Root Management Strategy

### Root Management Lifecycle

1. **Before Execution**:
   ```
   GC Roots: [nil, false, true, Smalltalk]
   ```

2. **During Execution** (Process created):
   ```
   GC Roots: [nil, false, true, Smalltalk, Process]
   ```
   
   The Process keeps alive:
   - Process.suspendedContext (MethodContext)
   - MethodContext.method (CompiledMethod)
   - MethodContext.receiver (init expression receiver)
   - MethodContext stack/temporaries (any objects created during execution)

3. **After Execution** (Process removed):
   ```
   GC Roots: [nil, false, true, Smalltalk]
   ```
   
   The Process, MethodContext, and any temporary objects become unreachable and will be collected on next GC.

### Why This Works

- **ProcessorScheduler.activeProcess** is NOT a GC root directly (it's reachable via Smalltalk global)
- We explicitly add the **Process** to GC roots during execution
- This ensures the entire activation chain is kept alive
- After cleanup, removing Process from GC roots makes everything collectible

### Alternative Approach (Future)

Instead of manipulating GC roots directly, we could:
1. Add ProcessorScheduler to GC roots permanently
2. Rely on ProcessorScheduler.activeProcess to keep Process alive
3. This is more "correct" but requires careful GC root setup

For init expressions, explicit root management is simpler and more explicit.

---

## Testing and Verification

### Test 1: Single Init Expression

```csharp
[Test]
public void TestProcessCreationForInitExpression()
{
    // Setup: Load coldstart.json, run cold start phases 0-15
    var bundle = ColdStartBundle.LoadFromFile("coldstart.json");
    var heapManager = new SmalltalkHeapManager();
    var loader = new Loader(bundle, heapManager);
    loader.Run();
    
    // Create ProcessManager
    var processManager = new ProcessManager(heapManager, loader.LayoutResolver);
    
    // Get first init expression
    string initLocalId = "init:000";
    int methodId = loader.LocalIdToObjectId[initLocalId];
    
    // Create MethodContext
    var vm = new VirtualMachine(heapManager, loader.LayoutResolver);
    int receiverId = GlobalRegistry.SystemDictionaryId;
    int methodContextId = vm.CreateMethodContext(methodId, receiverId, new int[0], 0);
    
    // Create Process
    int processId = processManager.CreateProcess(methodContextId);
    
    // Verify Process structure
    Assert.Greater(processId, 0);
    
    // Verify suspendedContext field set correctly
    var processOOP = SmalltalkOOP.FromObjectTableIndex(processId);
    int suspendedContextIndex = loader.LayoutResolver.GetFieldIndex("class:Process", "suspendedContext");
    var suspendedContextOOP = heapManager.GetInstanceVariable(processOOP, suspendedContextIndex);
    Assert.AreEqual(methodContextId, suspendedContextOOP.AsObjectTableIndex());
}
```

### Test 2: Full Init Expression Execution

```csharp
[Test]
public void TestInitExpressionExecutionWithProcess()
{
    // Setup: Load and run cold start
    var bundle = ColdStartBundle.LoadFromFile("coldstart.json");
    var heapManager = new SmalltalkHeapManager();
    var loader = new Loader(bundle, heapManager);
    loader.Run();
    
    // Create VM and executor
    var vm = new VirtualMachine(heapManager, loader.LayoutResolver);
    var executor = new InitExpressionExecutor(
        heapManager,
        vm,
        loader.LayoutResolver,
        loader.LocalIdToObjectId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        InitPolicy.ContinueAndCollect
    );
    
    // Execute first init expression
    string initLocalId = "init:000";
    var report = executor.ExecuteOne(initLocalId);
    
    // Verify execution
    Assert.AreEqual(InitStatus.Success, report.Status);
    Assert.Greater(report.ElapsedMilliseconds, 0);
    
    // Verify ProcessorScheduler.activeProcess cleared
    int schedulerId = GlobalRegistry.Get("ProcessorScheduler");
    var schedulerOOP = SmalltalkOOP.FromObjectTableIndex(schedulerId);
    int activeProcessIndex = loader.LayoutResolver.GetFieldIndex("class:ProcessorScheduler", "activeProcess");
    var activeProcessOOP = heapManager.GetInstanceVariable(schedulerOOP, activeProcessIndex);
    Assert.AreEqual(GlobalRegistry.NilObjectId, activeProcessOOP.AsObjectTableIndex());
}
```

### Test 3: GC Verification

```csharp
[Test]
public void TestProcessGarbageCollection()
{
    // Setup and execute init expression
    // ... (same as Test 2)
    
    executor.ExecuteOne("init:000");
    
    // Count objects before GC
    var statsBefore = heapManager.GetGCStatistics();
    int objectsBeforeGC = statsBefore.TotalObjects;
    
    // Force GC
    var gcStats = heapManager.GarbageCollect();
    
    // Verify Process and MethodContext collected
    // (should collect at least 2 objects: Process + MethodContext)
    Assert.Greater(gcStats.CollectedObjects, 0);
    Assert.Less(gcStats.LiveObjects, objectsBeforeGC);
}
```

---

## Implementation Checklist (Revised)

### Phase 1: ProcessorScheduler Initialization ✅ COMPLETE
- [x] Initialize ProcessorScheduler fields in Phase 6
- [x] Verify ProcessorScheduler.activeProcess = nil after Phase 6
- [x] Verify ProcessorScheduler.quiescentProcessLists = nil after Phase 6
- [x] Build and test cold start with ProcessorScheduler initialization

### Phase 2: Implement Primitive 87 (Process>>resume)
- [ ] Create ProcessPrimitives.cs class
- [ ] Implement Resume() method following Smalltalk-80.st pattern
- [ ] Handle activeProcess setting/clearing
- [ ] Integrate with VirtualMachine for context execution
- [ ] Add error handling for invalid process states

### Phase 3: VirtualMachine Integration
- [ ] Add ExecuteExistingContext() method to VirtualMachine
- [ ] Support executing pre-created MethodContext (not creating new one)
- [ ] Ensure proper stack frame handling
- [ ] Test context execution independently

### Phase 4: InitExpressionExecutor Update
- [ ] Add ProcessPrimitives to InitExpressionExecutor
- [ ] Implement CreateProcess() helper method
- [ ] Update ExecuteOne() to use primitive 87
- [ ] Remove manual activeProcess manipulation
- [ ] Remove manual GC root management (primitive handles it)

### Phase 5: Testing and Validation
- [ ] Test Process creation with proper field initialization
- [ ] Test primitive 87 sets activeProcess correctly
- [ ] Test primitive 87 clears activeProcess after execution
- [ ] Run first init expression (init:000) successfully
- [ ] Verify GC after execution (Process collected)
- [ ] Verify 0 unreachable objects

### Phase 6: Documentation
- [x] Update INIT-EXPRESSION-EXECUTION-PLAN.md with revised approach
- [ ] Update CLAUDE.md with primitive 87 implementation
- [ ] Document ProcessPrimitives API
- [ ] Add Smalltalk-80.st source references

---

## Future Enhancements

### Full ProcessorScheduler Implementation

For a complete Smalltalk system (beyond init expressions), implement:

1. **Ready Queues**:
   - Allocate quiescentProcessLists as Array of 8 LinkedLists
   - Implement add/remove operations per priority
   - Implement highestPriorityReadyProcess selection

2. **Context Switching**:
   - Implement yield primitive (save current context, switch to next Process)
   - Implement suspend/resume primitives
   - Handle Process state transitions (#ready, #running, #blocked, #terminated)

3. **Preemption**:
   - Implement timeslice tracking
   - Add timer interrupt handling
   - Force context switch at safe bytecode boundaries

4. **Semaphores**:
   - Implement Semaphore wait/signal primitives
   - Maintain blocked Process lists per Semaphore
   - Handle Process wake-up on signal

5. **Process Primitives**:
   - Primitive 76: fork (create new Process)
   - Primitive 77: yield (switch to next Process)
   - Primitive 78: resume (activate a Process)
   - Primitive 79: suspend (block current Process)

### BlockContext Integration

Process system is essential for BlockContext execution:
- Blocks that fork need Process creation
- Non-local returns need Process unwinding
- Block scheduling needs ProcessorScheduler integration

---

## Key Insights from Smalltalk-80.st Source Analysis

### ProcessorScheduler Instance Variables

```smalltalk
ProcessorScheduler comment: 'Instance Variables: 
    quiescentProcessLists    <Array of LinkedLists> on which to suspend processes
    activeProcess            <Process> currently executing process
```

### Process Instance Variables

```smalltalk
Process comment: 'Instance Variables: 
    suspendedContext    <Context> activeContext at time of process suspension
    priority            <Integer> partial indication of relative scheduling
    myList              <LinkedList> on which I am suspended
```

### Process Execution Pattern

```smalltalk
BlockContext>>fork
    "Create and schedule a process running the code in the receiver."
    self newProcess resume

BlockContext>>newProcess
    "Answer a new process running the code in the receiver."
    ^Process
        forContext: 
            [self value.
            Processor terminateActive]
        priority: Processor activePriority
```

### Critical Primitives

```smalltalk
Process>>resume
    "Primitive 87 - sets activeProcess and executes"
    <primitive: 87>
    self primitiveFailed

Process>>suspend  
    "Primitive 88 - saves context and removes from activeProcess"
    <primitive: 88>
    ...
```

**Key Takeaway**: The **primitives manage activeProcess**, not user code. Our implementation must follow this pattern.

## References

- **Smalltalk-80.st Source** - Definitive reference for Process/ProcessorScheduler behavior
- **Smalltalk-80 Blue Book** - Goldberg & Robson (1983) - Process and ProcessorScheduler specification
- **Primitive 87/88** - Process resume/suspend primitives
- **BlockContext>>newProcess** - Pattern for creating Processes from code blocks

---

*This document provides a comprehensive plan for implementing init expression execution following **actual Smalltalk-80.st source patterns**. Implementation uses primitive 87 (resume) to properly manage activeProcess, following Blue Book semantics.*
