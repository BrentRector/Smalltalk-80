# Implementation Plan: Complete Smalltalk-80 Interpreter Runtime

**Created**: 2025-01-10
**Status**: Planning Phase
**Goal**: Complete the interpreter runtime to execute Smalltalk code interactively

## Current Status Analysis

### ✅ What's Already Working (100%)
All **12 cold start loader phases** are complete:
- Phase 0: Preflight Validation ✅
- Phase 1: Symbol Interning ✅
- Phase 2: Class Shells ✅
- Phase 3: Metaclass Wiring ✅
- Phase 4: Class Resolution ✅
- Phase 5: Layout Computation ✅
- Phase 6: Object Allocation ✅
- Phase 7: Method Installation ✅
- Phase 8: Patch Application ✅
- Phase 9: Init Expression Preparation ✅
- Phase 10: USTable Population ✅
- Phase 12: Bytecode Validation ✅

### ✅ VM Infrastructure Complete (95%)
- **VirtualMachine.cs**: All 256 bytecodes implemented
- **Primitive System**: 94/111 primitives functional (85%)
- **Graphics**: Complete BitBlt engine
- **Block Closures**: Full context switching support
- **GC**: Mark-and-sweep working (0 objects collected = perfect)

### ⚠️ What's Missing: Runtime Execution Layer

The interpreter **loads and validates** everything but can't **execute Smalltalk code** yet. Missing:

1. **REPL/Command Execution** - No way to run Smalltalk expressions
2. **Init Expression Execution** - 285 init expressions prepared but not executed
3. **Process Scheduler** - No process/semaphore support (primitives 85-88 stubbed)
4. **Interactive Loop** - No event loop or user interaction

## Implementation Plan

### Phase 1: Init Expression Execution (CRITICAL - Day 1-2)
**Goal**: Execute the 285 prepared initialization expressions to complete image setup

**Files to Modify**:
1. `Smalltalk80.Interpreter/LoaderPhases/Phase8_InitExecution.cs`
   - Currently only *prepares* init expressions array
   - Add method to actually **execute** each init expression
   - Use VirtualMachine.Execute() to run each expression's bytecode
   - Handle DoIt expressions (temp code that runs once)

2. `Smalltalk80.Interpreter/Loader.cs`
   - Add Phase 11: Execute Init Expressions (after Phase 10)
   - Call VM to run each DoIt method in the init array
   - Log progress (X/285 completed)

**Why Critical**: Init expressions set up global state, class variables, pool dictionaries. Without these, many classes won't work correctly.

**Acceptance Criteria**:
- All 285 init expressions execute without errors
- Global variables properly initialized
- Class variables set up
- Pool dictionaries populated

---

### Phase 2: REPL Implementation (HIGH PRIORITY - Day 3-5)
**Goal**: Interactive Read-Eval-Print Loop for executing Smalltalk code

**New Files to Create**:
1. `Smalltalk80.Interpreter/REPL/ReplEngine.cs`
   - Read user input (Smalltalk expression)
   - Parse expression using compiler (or simple eval)
   - Execute using VirtualMachine
   - Print result
   - Handle errors gracefully

2. `Smalltalk80.Interpreter/REPL/ExpressionCompiler.cs`
   - Mini-compiler for single expressions
   - Generate bytecode on-the-fly
   - Or: Invoke Compiler as library to compile expressions

**Files to Modify**:
1. `Smalltalk80.Interpreter/Program.cs` (line 69)
   - Remove `// TODO: Start REPL or execute startup code`
   - After cold start completes, check if `repl` command specified
   - Create ReplEngine, pass heap + VM
   - Start interactive loop

**REPL Features**:
```
Smalltalk-80> 3 + 4
7
Smalltalk-80> Point x: 10 y: 20
Point(10@20)
Smalltalk-80> Transcript show: 'Hello World'
Hello World
nil
Smalltalk-80> :quit
Goodbye!
```

**Acceptance Criteria**:
- Can evaluate simple expressions (arithmetic)
- Can send messages to objects
- Can access global variables (Smalltalk, Transcript)
- Displays results correctly
- Handles errors without crashing

---

### Phase 3: Process Scheduler Infrastructure (MEDIUM PRIORITY - Day 6-10)
**Goal**: Implement ProcessorScheduler, Process, Semaphore for concurrent execution

**New Files to Create**:
1. `Smalltalk80.Interpreter/VM/ProcessScheduler.cs`
   - Manages run queue of Process objects
   - Switch between active processes (time-slicing)
   - Priority-based scheduling (Blue Book spec)
   - Single active process at a time (cooperative multitasking)

2. `Smalltalk80.Interpreter/VM/ProcessContext.cs`
   - Wrapper around Process object state
   - Save/restore execution context
   - Stack, IP, active context switching

**Files to Modify**:
1. `Smalltalk80.Interpreter/VM/Primitives/ControlPrimitives.cs`
   - **Primitive 85** (signal) - Signal a semaphore, wake waiting process
   - **Primitive 86** (wait) - Wait on semaphore, suspend current process
   - **Primitive 87** (resume) - Resume a suspended process
   - **Primitive 88** (suspend) - Suspend current process
   - Remove "Not implemented" stubs, add real scheduler calls

2. `Smalltalk80.Interpreter/VM/SmalltalkHeap.cs`
   - Add ProcessorScheduler reference (from Special Objects Array)
   - Add process switching support

3. `Smalltalk80.Interpreter/Loader.cs`
   - Initialize ProcessorScheduler after Phase 10
   - Create initial Process for main thread
   - Set up scheduler in Special Objects Array (index 4)

**Key Concepts**:
- **Process**: Executable unit with its own context chain
- **Semaphore**: Synchronization primitive (counter + waiting queue)
- **ProcessorScheduler**: Singleton managing all processes
- **Run Queue**: Processes ready to execute (per priority level)

**Acceptance Criteria**:
- Can create and schedule processes
- Semaphores signal/wait correctly
- Process resume/suspend work
- Time-slicing between processes functions
- No deadlocks or race conditions

---

### Phase 4: Enhanced VM Integration (LOW PRIORITY - Day 11-12)
**Goal**: Fix remaining TODOs in VirtualMachine.cs

**Files to Modify**:
1. `Smalltalk80.Interpreter/VM/VirtualMachine.cs`
   - **Line 273, 279**: Implement non-local returns from blocks
   - **Lines 577, 590, 604, 623**: Implement super send (lookup in superclass)
   - **Line 1080**: Add GetIndexedBytes to SmalltalkHeap for byte arrays
   - **Line 1088, 1123, 1171**: Add class name → class ID mapping
   - **Line 1164**: Proper home context resolution for nested blocks

2. `Smalltalk80.Interpreter/VM/SmalltalkHeap.cs`
   - Add `GetClassByName(string name)` method
   - Add `GetIndexedBytes(int oop)` for byte array primitives
   - Store class name → OOP lookup table (built during cold start)

**Acceptance Criteria**:
- Super sends resolve to correct superclass methods
- Non-local block returns work (escape from nested contexts)
- Byte array primitives functional
- All class lookups by name work

---

### Phase 5: Remaining Primitives (LOW PRIORITY - Day 13-14)
**Goal**: Complete the 17 stubbed primitives

**Files to Modify**:
1. `Smalltalk80.Interpreter/VM/Primitives/StringPrimitives.cs`
   - **Primitive 106** (asSymbol) - Requires symbol interning API

2. `Smalltalk80.Interpreter/VM/Primitives/SystemPrimitives.cs`
   - **Primitive 23** (lowSpaceSemaphore) - Memory monitoring
   - **Primitive 39** (oopsLeft) - Free object slots
   - **Primitive 108** (flushMethodCache) - Method lookup cache
   - **Primitive 112** (quit) - Graceful shutdown
   - **Primitive 113** (exitToDebugger) - Debugger entry
   - Various become: operations (68, 130)

3. `Smalltalk80.Interpreter/VM/Primitives/ObjectPrimitives.cs`
   - **Primitives 128-129, 139** - Object enumeration (someInstance, nextInstance)

**Dependencies**: Process scheduler must be done first (for semaphore primitives)

**Acceptance Criteria**:
- All 111 primitives functional
- 100% primitive coverage
- No "Not implemented" stubs remain

---

### Phase 6: Display Integration (OPTIONAL - Future)
**Goal**: Connect graphics primitives to actual display output

**New Files to Create**:
1. Platform-specific display providers:
   - `Smalltalk80.Interpreter/Display/ConsoleDisplayProvider.cs` (ASCII art)
   - `Smalltalk80.Interpreter/Display/WinFormsDisplayProvider.cs` (Windows GUI)
   - `Smalltalk80.Interpreter/Display/AvaloniaDisplayProvider.cs` (Cross-platform)

**Files to Modify**:
1. `Smalltalk80.Interpreter/VM/Primitives/GraphicsPrimitives.cs`
   - Remove stub implementations
   - Connect to IDisplayProvider interface
   - Forward display updates to platform layer

**Acceptance Criteria**:
- Can display bitmap graphics
- Mouse/keyboard input captured
- BitBlt operations visible on screen

---

## Implementation Order (Priority Sequence)

### Week 1: Core Execution
1. **Day 1-2**: Phase 1 (Init Expression Execution) - CRITICAL
2. **Day 3-5**: Phase 2 (REPL Implementation) - HIGH

### Week 2: Concurrency
3. **Day 6-10**: Phase 3 (Process Scheduler) - MEDIUM

### Week 3: Polish
4. **Day 11-12**: Phase 4 (VM Integration fixes) - LOW
5. **Day 13-14**: Phase 5 (Remaining Primitives) - LOW

### Future (Optional):
6. **Phase 6**: Display Integration (when GUI needed)

---

## Success Metrics

### After Phase 1 (Init Expressions):
```bash
$ dotnet run --project Smalltalk80.Interpreter
...
Phase 11: Execute Init Expressions... ✓
  Executed: 285/285 expressions
Cold start complete!
```

### After Phase 2 (REPL):
```bash
$ dotnet run --project Smalltalk80.Interpreter -- repl
Smalltalk-80>
```

### After Phase 3 (Process Scheduler):
```bash
Smalltalk-80> [Transcript show: 'Background task'] fork.
Smalltalk-80> Processor activeProcess priority
7
```

### After Phases 4-5 (Complete VM):
```bash
- 100% primitive coverage (111/111)
- All bytecode instructions functional
- Super sends working
- Non-local returns working
```

---

## Risk Assessment

### Low Risk:
- **Phase 1** (Init Expressions): Straightforward - just execute DoIt methods
- **Phase 2** (REPL): Well-understood pattern, many examples exist

### Medium Risk:
- **Phase 3** (Process Scheduler): Complex state management, concurrency bugs possible

### Low Risk:
- **Phase 4-5** (VM fixes): Incremental improvements, isolated changes

---

## Testing Strategy

### Unit Tests (New):
1. `Smalltalk80.Interpreter.Tests/ReplEngineTests.cs`
   - Test expression parsing
   - Test result formatting
   - Test error handling

2. `Smalltalk80.Interpreter.Tests/ProcessSchedulerTests.cs`
   - Test process creation/switching
   - Test semaphore signal/wait
   - Test priority scheduling

### Integration Tests:
1. Execute all 285 init expressions without errors
2. REPL can evaluate Blue Book examples
3. Process scheduler passes concurrency tests

### Validation:
- Run existing validator (should still pass 100%)
- GC should still collect 0 objects after init execution
- No memory leaks during REPL session

---

## Notes

- **Cold start is complete** - all 12 phases working perfectly
- **VM is 95% complete** - just missing runtime execution layer
- **Focus on Phases 1-2 first** - these unlock interactive usage
- **Process scheduler can wait** - most code works without it
- **This is the final stretch** - interpreter will be fully functional after Week 1-2
