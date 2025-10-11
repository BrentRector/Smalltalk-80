# Phase 1: Init Expression Execution - Detailed Implementation Roadmap

**Created**: 2025-01-10
**Priority**: CRITICAL
**Est. Duration**: 2-3 days
**Goal**: Execute 285 initialization expressions (DoIts) to complete cold-start image setup

---

## Executive Summary

Init expressions are compiled bytecode methods that perform one-time initialization:
- Set up global variables (e.g., `Smalltalk at: #Display put: Display`)
- Initialize class variables (e.g., `Character initializeClassVariables`)
- Populate pool dictionaries (e.g., `TextConstants initializeTextConstants`)
- Configure system state (e.g., setting up default fonts, cursors)

**Current State**: Phase 8 **prepares** init expression CompiledMethods (creates objects, resolves literals) but does **NOT execute** them. The 285 methods sit in memory unused.

**Desired State**: Phase 11 **executes** each init expression using the VirtualMachine, completing image initialization.

---

## Architecture Analysis

### Current Flow (Loader.cs)
```
Phase 0: Preflight → Phase 1: Symbols → Phase 2: Classes → ... → Phase 10: USTable
→ Create Special Objects Array → GC Verification → Phase 12: Bytecode Validation
→ [MISSING: Phase 11 - Execute Init Expressions]
```

### What Phase 8 Does Now
`Phase8_InitExecution.Run()`:
1. Iterates through `_bundle.InitExpressions` (285 entries)
2. For each init expression:
   - Resolves literals (symbols, strings, objectRefs, etc.)
   - Decodes bytecodes (JSON array or base64 string)
   - Creates a `CompiledMethod` object via `_heap.CreateCompiledMethod()`
   - Stores method ID in `_localIdToObjectId` map
3. Returns array of init method IDs for GC root storage
4. **Does NOT execute anything**

### What We Need to Add: Phase 11
```
Phase 11: Execute Init Expressions
├── Retrieve init expression array from Special Objects (index 6)
├── Create VirtualMachine instance with heap + LayoutResolver
├── For each init method in array:
│   ├── Determine receiver (nil or Smalltalk global)
│   ├── Call VM.Execute(methodId, receiverId, args=[])
│   ├── Capture result/exception
│   ├── Log progress (X/285 completed)
│   └── Handle errors (abort or continue based on policy)
└── Report completion statistics
```

---

## Implementation Tasks

### Task 1.1: Create InitExpressionExecutor Class
**File**: `Smalltalk80.Interpreter/LoaderPhases/Phase11_InitExpressionExecution.cs` (NEW)

**Purpose**: Execute prepared init expression methods using the VM

**API Design**:
```csharp
public class Phase11_InitExpressionExecution
{
    private readonly ISmalltalkHeap _heap;
    private readonly LayoutResolver _layoutResolver;
    private readonly int _initArrayId; // From Phase 8

    public Phase11_InitExpressionExecution(
        ISmalltalkHeap heap,
        LayoutResolver layoutResolver,
        int initArrayId)
    {
        _heap = heap;
        _layoutResolver = layoutResolver;
        _initArrayId = initArrayId;
    }

    public InitExecutionReport Run()
    {
        // Implementation
    }
}

public record InitExecutionReport(
    int Total,
    int Succeeded,
    int Failed,
    List<InitFailure> Failures
);

public record InitFailure(
    string LocalId,
    int MethodId,
    string ErrorMessage,
    string? StackTrace
);
```

**Implementation Steps**:
1. Get init method array from `_initArrayId`
2. Create `VirtualMachine` instance:
   ```csharp
   var vm = new VirtualMachine(_heap, _layoutResolver);
   ```
3. Determine receiver for init expressions:
   - **Option A**: Use `nil` (GlobalRegistry.NilObjectId)
   - **Option B**: Use `Smalltalk` global (lookup in SystemDictionary)
   - **Recommendation**: Use `nil` (simpler, standard for DoIts)
4. Loop through init methods:
   ```csharp
   int count = _heap.GetIndexedSize(_initArrayId);
   for (int i = 0; i < count; i++)
   {
       int methodId = _heap.GetIndexedField(_initArrayId, i);
       try
       {
           int result = vm.Execute(methodId, receiverId, Array.Empty<int>());
           // Log success
       }
       catch (Exception ex)
       {
           // Record failure
       }
   }
   ```
5. Return report with statistics

---

### Task 1.2: Wire Phase 11 into Loader
**File**: `Smalltalk80.Interpreter/Loader.cs`

**Changes**:
1. Add Phase 11 execution **after** Phase 10 (USTable) but **before** Special Objects Array creation:
   ```csharp
   // Phase 10: USTable Population
   RunPhase("Phase 10: USTable Population", () => { ... });

   // NEW: Phase 11: Execute Init Expressions
   InitExecutionReport initReport = null!;
   RunPhase("Phase 11: Execute Init Expressions", () =>
   {
       var phase = new Phase11_InitExpressionExecution(
           _heap,
           _layoutResolver!,
           initExpressionsArrayId
       );
       initReport = phase.Run();
   });

   Console.WriteLine($"  Executed: {initReport.Succeeded}/{initReport.Total} expressions");
   if (initReport.Failed > 0)
   {
       Console.WriteLine($"  ⚠ {initReport.Failed} failures:");
       foreach (var failure in initReport.Failures.Take(5))
       {
           Console.WriteLine($"    - {failure.LocalId}: {failure.ErrorMessage}");
       }
   }
   ```

2. Keep init array in Special Objects Array during execution:
   ```csharp
   // Keep init expressions reachable during execution
   initExpressionsArrayId // Already stored at index 6
   ```

3. After Phase 11 completes, can optionally remove from Special Objects:
   ```csharp
   // Init expressions executed - can be garbage collected now
   specialObjects[6] = GlobalRegistry.NilObjectId;
   ```

---

### Task 1.3: Handle Init Expression Receiver Context
**File**: `Smalltalk80.Interpreter/VM/VirtualMachine.cs`

**Current Issue**: `Execute()` method expects a receiver, but init expressions are "DoIts" that conceptually execute in a nil context.

**Options**:

**Option A: Use nil as receiver (RECOMMENDED)**
```csharp
// In Phase 11
int receiverId = GlobalRegistry.NilObjectId;
vm.Execute(methodId, receiverId, Array.Empty<int>());
```
- Simplest approach
- Standard Smalltalk DoIt semantics
- `self` in init expressions refers to nil

**Option B: Use UndefinedObject class**
```csharp
// Get UndefinedObject singleton (nil's class)
int receiverId = _heap.GetClass(GlobalRegistry.NilObjectId);
vm.Execute(methodId, receiverId, Array.Empty<int>());
```
- More semantically correct
- `self` is UndefinedObject class (for class-side DoIts)

**Option C: Use Smalltalk global object**
```csharp
// Lookup Smalltalk in SystemDictionary
int smalltalkId = GlobalRegistry.GetGlobal("Smalltalk");
vm.Execute(methodId, smalltalkId, Array.Empty<int>());
```
- Gives init expressions access to Smalltalk namespace
- More powerful but non-standard

**Decision**: Use **Option A** (nil) unless init expressions fail, then try Option B.

---

### Task 1.4: Add Error Handling and Diagnostics

**Failure Modes**:
1. **Bytecode Execution Exception**: Invalid bytecode, stack underflow, etc.
2. **Primitive Failure**: Primitive returns failure without fallback
3. **Message Not Understood**: Selector lookup fails
4. **Timeout**: Init expression runs too long (optional)

**Error Handling Strategy**:

**Phase 1a: Abort on First Error (SIMPLE)**
```csharp
try
{
    int result = vm.Execute(methodId, receiverId, Array.Empty<int>());
    succeeded++;
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Init expression failed: {localId}");
    Console.WriteLine($"  Error: {ex.Message}");
    throw new InvalidOperationException(
        $"Cold start failed at Phase 11: Init expression '{localId}'", ex);
}
```

**Phase 1b: Continue on Error with Report (ROBUST)**
```csharp
try
{
    int result = vm.Execute(methodId, receiverId, Array.Empty<int>());
    succeeded++;
}
catch (Exception ex)
{
    failed++;
    failures.Add(new InitFailure(
        LocalId: localId,
        MethodId: methodId,
        ErrorMessage: ex.Message,
        StackTrace: ex.StackTrace
    ));
    // Continue to next init expression
}
```

**Recommendation**: Start with Phase 1a (abort on error) for debugging, then switch to Phase 1b once stable.

---

### Task 1.5: Add Progress Logging

**Inline Progress** (during execution):
```csharp
Console.Write($"Phase 11: Execute Init Expressions... ");
for (int i = 0; i < count; i++)
{
    // Execute init expression
    if (i % 50 == 0 && i > 0)
    {
        Console.Write($"{i}/{count}...");
    }
}
Console.WriteLine("✓");
```

**Summary Report** (after completion):
```csharp
Console.WriteLine($"  Executed: {succeeded}/{total} expressions");
Console.WriteLine($"  Duration: {elapsed.TotalSeconds:F2}s");
if (failed > 0)
{
    Console.WriteLine($"  ⚠ Failures: {failed}");
}
```

---

## Testing Strategy

### Unit Tests
**File**: `Smalltalk80.Interpreter.Tests/Phase11_InitExpressionExecutionTests.cs` (NEW)

**Test Cases**:
1. **UT-Init-01**: Execute single trivial init expression (pushes literal, returns)
   ```csharp
   // Bytecode: PUSH_ONE, RETURN_TOP
   // Expected: Returns SmallInteger(1)
   ```

2. **UT-Init-02**: Execute init expression that sets global variable
   ```csharp
   // Bytecode: PUSH_LITERAL (value), STORE_LITERAL_VAR (globalAssoc), RETURN_SELF
   // Expected: Global variable set, returns nil
   ```

3. **UT-Init-03**: Execute init expression with primitive call
   ```csharp
   // Bytecode: Calls primitive (e.g., Array new: 10)
   // Expected: Returns allocated array
   ```

4. **UT-Init-04**: Execute init expression that throws exception
   ```csharp
   // Bytecode: Sends message that triggers error
   // Expected: Exception caught, failure reported
   ```

### Integration Tests
**File**: `Smalltalk80.Interpreter.Tests/FullColdStartTests.cs`

**Test Cases**:
1. **IT-Init-01**: Full cold start with all 285 init expressions
   ```csharp
   [Fact]
   public void FullColdStart_ExecutesAllInitExpressions()
   {
       var bundle = ColdStartBundle.LoadFromFile("coldstart.json");
       var heap = new SmalltalkHeap();
       var loader = new Loader(bundle, heap);

       loader.Run(); // Should complete Phase 11

       // Verify: All 285 init expressions executed
       // Verify: GC still collects 0 objects
   }
   ```

2. **IT-Init-02**: Verify global variables initialized
   ```csharp
   [Fact]
   public void AfterInitExpressions_GlobalsAreSet()
   {
       // Execute cold start
       // Lookup Smalltalk global
       // Assert: Smalltalk has expected keys (Display, Transcript, etc.)
   }
   ```

### Manual Testing
```bash
# Run interpreter and check output
dotnet run --project Smalltalk80.Interpreter

# Expected output:
# Phase 11: Execute Init Expressions... ✓
#   Executed: 285/285 expressions
#   Duration: 1.23s
```

---

## Potential Issues and Solutions

### Issue 1: VM Not Ready for Execution
**Symptom**: VirtualMachine throws NullReferenceException or "LayoutResolver not initialized"

**Solution**: Ensure `_layoutResolver` is set in Loader before Phase 11:
```csharp
// In Loader.cs Phase 5
_layoutResolver = new LayoutResolver();
_layoutResolver.ComputeLayouts(_bundle.Classes, _bundle.Symbols);
```

---

### Issue 2: Init Expressions Reference Non-Existent Globals
**Symptom**: Message send fails with "Method not found"

**Root Cause**: Init expressions may reference classes/globals not yet installed

**Solution**: Check `InstallOrder` in JSON - init expressions should run AFTER all classes/methods installed

---

### Issue 3: Primitive Failures
**Symptom**: Init expression calls primitive that fails

**Debugging**:
1. Check which primitive is being called (inspect bytecode)
2. Verify primitive is implemented (PrimitiveTable.cs)
3. Add logging to primitive execution:
   ```csharp
   Console.WriteLine($"Calling primitive {primitiveNumber}");
   ```

---

### Issue 4: Stack Imbalance
**Symptom**: "Stack underflow" or "Stack overflow" during init execution

**Solution**: Run Phase 12 (Bytecode Validation) on init expressions to verify stack balance before execution

---

## Implementation Checklist

### Core Implementation
- [ ] Create `Phase11_InitExpressionExecution.cs`
- [ ] Implement `Run()` method with VM execution loop
- [ ] Add `InitExecutionReport` record class
- [ ] Wire Phase 11 into `Loader.cs`
- [ ] Add progress logging (X/285 completed)
- [ ] Implement error handling (abort on first error)

### Testing
- [ ] Write unit test: UT-Init-01 (trivial expression)
- [ ] Write unit test: UT-Init-02 (global variable assignment)
- [ ] Write integration test: IT-Init-01 (full 285 expressions)
- [ ] Manual test: Run interpreter and verify Phase 11 completes

### Refinement (Optional, Phase 1b)
- [ ] Implement continue-on-error policy
- [ ] Add detailed failure report with stack traces
- [ ] Add timeout support (optional)
- [ ] Add structured JSON logging of init results
- [ ] Integrate with validator (pre-validate init expressions)

---

## Success Criteria

### Minimum Viable (Phase 1a)
✅ All 285 init expressions execute without errors
✅ Phase 11 completes successfully
✅ GC still collects 0 objects after execution
✅ Manual test passes

### Production Ready (Phase 1b)
✅ Continue-on-error policy implemented
✅ Detailed failure reports generated
✅ Unit tests pass (4/4)
✅ Integration tests pass (2/2)
✅ Structured logging for CI/CD

---

## Timeline Estimate

### Day 1 (Core Implementation)
- Morning: Create Phase11 class, implement basic execution loop
- Afternoon: Wire into Loader, add progress logging
- Evening: Manual testing, debug first issues

### Day 2 (Error Handling & Testing)
- Morning: Implement error handling, add diagnostics
- Afternoon: Write unit tests, fix bugs
- Evening: Integration testing, verify GC correctness

### Day 3 (Refinement & Documentation)
- Morning: Add continue-on-error policy, structured logging
- Afternoon: Final testing, update documentation
- Evening: Code review, merge to main

---

## Next Steps After Phase 1

Once Phase 11 is complete:
1. **Phase 2**: Implement REPL (execute user-provided expressions)
2. **Verify**: Check that initialized globals are accessible
3. **Test**: Try simple Smalltalk expressions like `3 + 4`

---

## References

- **Blue Book**: Goldberg & Robson, "Smalltalk-80: The Language and its Implementation"
  - Chapter 30: The Interpreter (message sending, context activation)
- **Current Implementation**:
  - [Loader.cs](../Smalltalk80.Interpreter/Loader.cs) - Cold start orchestrator
  - [Phase8_InitExecution.cs](../Smalltalk80.Interpreter/LoaderPhases/Phase8_InitExecution.cs) - Init preparation
  - [VirtualMachine.cs](../Smalltalk80.Interpreter/VM/VirtualMachine.cs) - Bytecode execution
- **Schema**:
  - [coldstart-v1.0.5.schema.json](../docs/schemas/coldstart-v1.0.5.schema.json) - Init expression format

---

## Appendix: Example Init Expression Bytecode

**Example**: `Smalltalk at: #Display put: aDisplayObject`

**Bytecode**:
```
PUSH_LITERAL_VAR 1    // Push Smalltalk global
PUSH_LITERAL 2        // Push #Display symbol
PUSH_LITERAL_VAR 3    // Push Display object
SEND_2 4              // Send at:put: (2 args)
POP                   // Discard result
RETURN_SELF           // Return nil
```

**Effect**: Installs `Display` key in `Smalltalk` dictionary pointing to the Display object.
