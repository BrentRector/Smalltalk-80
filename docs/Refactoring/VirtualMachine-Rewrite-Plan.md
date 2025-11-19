# VirtualMachine Rewrite Plan

**Date**: 2025-11-18
**Status**: ✅ COMPLETED (Phases 1-4)
**Actual Effort**: ~2 hours

## Completion Summary

The VirtualMachine rewrite was partially completed, achieving significant improvements:

### Components Extracted

| Component | File | Purpose | Lines Moved |
|-----------|------|---------|-------------|
| ExecutionState | `Core/VM/Interpreter/ExecutionState.cs` | State records (CallOutcome, InterpreterStatus, NonLocalReturnState) | ~100 |
| ExecutionTracer | `Core/VM/Diagnostics/ExecutionTracer.cs` | All debug/trace state and methods | ~220 |
| SelectorCache | `Core/VM/Caching/SelectorCache.cs` | Arithmetic/special selector OOP caches | ~100 |
| NonLocalReturnHandler | `Core/VM/Interpreter/NonLocalReturnHandler.cs` | NLR state machine encapsulation | ~120 |

### Phases Completed

- **Phase 1**: State records extracted ✅
- **Phase 2**: ExecutionTracer integrated ✅
- **Phase 3**: SelectorCache extracted ✅
- **Phase 4**: NonLocalReturnHandler extracted ✅
- **Phase 5**: InterpreterEngine - SKIPPED (high risk, execution loop tightly coupled)
- **Phase 6**: Interface cleanup - EVALUATED (interfaces follow ISP, minimal gain)
- **Phase 7**: Documentation updated ✅

### Results

- **VirtualMachine.cs**: Reduced by ~300 lines
- **State coupling**: NLR and trace state now encapsulated in dedicated handlers
- **Testability**: Components can be unit tested independently
- **Maintainability**: Clear single-responsibility components

### What Wasn't Done

- **InterpreterEngine extraction**: The core execution loop (InterpreterLoop, ExecuteBytecodeStep, HandleReturn) remains in VirtualMachine due to deep coupling with trampoline state
- **Interface reduction**: The 5 interfaces remain (IPrimitiveContext, IBytecodeContext, IVMDebugHelpers, ICacheManager, IPrimitiveMessageSender) as they follow Interface Segregation Principle

---

## Original Analysis

### File Sizes (Total: 2,732 lines)

| File | Lines | Purpose |
|------|-------|---------|
| VirtualMachine.cs | 2,096 | Core execution, all interfaces |
| VirtualMachine.MessageSending.cs | 438 | SendMessage, ConsumeCallOutcome |
| VirtualMachine.VariableAccess.cs | 198 | Temp frame access, literals |

### Interface Implementations (5 interfaces = God Object)

| Interface | Members | Purpose |
|-----------|---------|---------|
| `IPrimitiveContext` | 2 | Heap access for primitives |
| `IBytecodeContext` | 25+ | Everything needed by BytecodeInterpreter |
| `IVMDebugHelpers` | 6 | Debug info (duplicates IBytecodeContext) |
| `ICacheManager` | 2 | Cache invalidation |
| `IPrimitiveMessageSender` | 1 | Perform primitive support |

### State Variables (Grouped by Responsibility)

**Injected Dependencies (readonly)**
- `_heapManager`, `_runtimeHeapAccess`, `_layoutResolver`, `_metadata`
- `_platformBridge`, `_stackManager`, `_contextManager`
- `_messageDispatcher`, `_bytecodeInterpreter`, `_debugger`, `_primitiveRegistry`

**Trampoline/Interpreter State**
- `_returnValue` - Result from execution
- `_initialContextOOP` - Starting context for return detection
- `_contextSwitchPending` - Signal for trampoline
- `_contextSwitchedByPrimitive` - Block invocation flag

**Non-Local Return State**
- `_nonLocalReturnPending` - NLR in progress
- `_nlrValue` - Value being returned
- `_nlrTargetContext` - Target to unwind to

**Debug/Trace State**
- `_verboseDebugMode` - Current debug level
- `_instructionCount` - Execution counter
- `_selectorTraceEnabled` - Trace flag
- `_bytecodeInstructionCounter`, `_pushTraceCounter`, `_selectorTraceDepth`
- `_selectorTraceStack` - Call trace stack
- `_methodSources` - Source cache

**Cache State**
- `_arithmeticSelectorOOPs` - Cached selectors for 0xB0-0xBF
- `_specialSelectorOOPs` - Cached selectors for 0xC0-0xCF

**Primitive State**
- `_primitiveSuccess` - Blue Book success flag

### Key Methods

| Method | Lines | Responsibility |
|--------|-------|----------------|
| `InterpreterLoop()` | 30 | Top-level trampoline |
| `ExecuteBytecodeStep()` | 150 | Single step execution |
| `HandleReturn()` | 140 | Return/NLR processing |
| `ExecuteBytecodeLoop()` | 190 | **DEPRECATED** |
| `InvokeBlock()` / `InvokeBlockInternal()` | 50 | Block execution |
| Interface implementations | 200+ | Delegation boilerplate |

---

## Problems with Current Design

### 1. God Object Anti-Pattern
VirtualMachine implements 5 interfaces, making it the central coupling point for the entire interpreter. Any change risks affecting multiple subsystems.

### 2. Entangled State
NLR state, trampoline state, and debug state are all mutable fields in the same class. They interact in complex ways that are hard to understand and test.

### 3. Interface Segregation Violation
`IBytecodeContext` has 25+ members mixing:
- Stack operations (Push, Pop, Peek)
- NLR state (3 properties)
- Message sending (2 methods)
- Variable access (Get/SetTemporaryVariable)
- Diagnostics (DumpStack, IsInSenderChain)
- Metadata access (GetClass, GetClassName, etc.)

### 4. Deprecated Code
`ExecuteBytecodeLoop()` is marked deprecated but still present (190 lines).

### 5. Boilerplate Overhead
200+ lines of explicit interface implementations that just delegate to private methods.

---

## Target Architecture

### Design Principles

1. **Single Responsibility** - Each component has one reason to change
2. **Explicit State** - State is grouped in records, not scattered fields
3. **Interface Segregation** - Small, focused interfaces
4. **Testability** - Components can be tested in isolation

### Component Structure

```
Core/VM/
├── VirtualMachine.cs              (~300 lines) - Orchestrator only
├── Interpreter/
│   ├── InterpreterEngine.cs       (~400 lines) - Execution loop
│   ├── ExecutionState.cs          (~50 lines)  - State record
│   └── NonLocalReturnHandler.cs   (~150 lines) - NLR state machine
├── Caching/
│   └── SelectorCache.cs           (~100 lines) - Selector caches
└── Diagnostics/
    └── ExecutionTracer.cs         (~200 lines) - All debug/trace logic
```

### New Interfaces

```csharp
// Replaces IBytecodeContext (focused on what BytecodeInterpreter actually needs)
internal interface IBytecodeExecutionContext
{
    // Stack operations
    void Push(SmalltalkOOP value);
    SmalltalkOOP Pop();
    SmalltalkOOP Peek();

    // Message dispatch
    bool SendMessageAndContinue(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] args);
    bool SendSuperMessageAndContinue(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] args);

    // Context access
    SmalltalkOOP ActiveContext { get; }
}

// Separate interface for metadata queries
internal interface IObjectMetadataAccessor
{
    SmalltalkOOP GetClass(SmalltalkOOP objectOOP);
    string GetClassName(SmalltalkOOP classOOP);
    string? GetSymbolName(SmalltalkOOP symbolOOP);
    MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP);
}

// NLR state - make it a record, not scattered properties
internal record NonLocalReturnState(
    bool IsPending,
    SmalltalkOOP Value,
    SmalltalkOOP TargetContext);

// Execution state - groups related trampoline state
internal record ExecutionState(
    SmalltalkOOP InitialContext,
    SmalltalkOOP ReturnValue,
    bool ContextSwitchPending);
```

### Component Responsibilities

#### VirtualMachine.cs (~300 lines)
- **Public API**: `Execute()`, `ExecuteExistingContext()`
- **Component coordination**: Creates and wires components
- **Interface adapter**: Implements only `IPrimitiveContext` and `IPrimitiveMessageSender`

#### InterpreterEngine.cs (~400 lines)
- **Trampoline loop**: `InterpreterLoop()`, `ExecuteBytecodeStep()`
- **Return handling**: `HandleReturn()`, NLR unwinding
- **Context management**: Quick-return optimizations

#### NonLocalReturnHandler.cs (~150 lines)
- **NLR state machine**: Initiate, propagate, complete
- **Validation**: Check sender chain
- **State**: `NonLocalReturnState` record

#### SelectorCache.cs (~100 lines)
- **Cache management**: Initialize, lookup, invalidate
- **State**: Arithmetic and special selector arrays

#### ExecutionTracer.cs (~200 lines)
- **Trace output**: Selector trace, instruction trace
- **Counters**: Instruction count, push trace count
- **State**: Trace stack, enable flags

---

## Implementation Phases

### Phase 1: Extract State Records (Day 1 Morning)

Create immutable state records without changing behavior.

**Tasks**:
- [ ] Create `NonLocalReturnState` record in new file
- [ ] Create `ExecutionState` record for trampoline state
- [ ] Add conversion methods to/from fields
- [ ] Verify build and tests pass

### Phase 2: Extract ExecutionTracer (Day 1 Afternoon)

Move all debug/trace logic to dedicated component.

**Tasks**:
- [ ] Create `Core/VM/Diagnostics/ExecutionTracer.cs`
- [ ] Move trace fields: `_selectorTraceEnabled`, counters, stack
- [ ] Move trace methods: `TraceSelectorEntry`, `TraceSelectorExit`
- [ ] Move `_verboseDebugMode` and `_instructionCount`
- [ ] Update VirtualMachine to use ExecutionTracer
- [ ] Verify build and tests pass

### Phase 3: Extract SelectorCache (Day 2 Morning)

Move selector cache management to dedicated component.

**Tasks**:
- [ ] Create `Core/VM/Caching/SelectorCache.cs`
- [ ] Move `_arithmeticSelectorOOPs` and `_specialSelectorOOPs`
- [ ] Move `InitializeSelectorCaches()`
- [ ] Implement `ICacheManager` in SelectorCache
- [ ] Update BytecodeInterpreter to use SelectorCache
- [ ] Verify build and tests pass

### Phase 4: Extract NonLocalReturnHandler (Day 2 Afternoon)

Move NLR state machine to dedicated component.

**Tasks**:
- [ ] Create `Core/VM/Interpreter/NonLocalReturnHandler.cs`
- [ ] Move NLR fields and methods
- [ ] Create clean NLR API: `Initiate()`, `Propagate()`, `Complete()`
- [ ] Update `HandleReturn()` to use NLR handler
- [ ] Verify build and tests pass

### Phase 5: Extract InterpreterEngine (Day 3)

Move execution loop to dedicated component.

**Tasks**:
- [ ] Create `Core/VM/Interpreter/InterpreterEngine.cs`
- [ ] Move `InterpreterLoop()`, `ExecuteBytecodeStep()`, `HandleReturn()`
- [ ] Move `InvokeBlock()`, `InvokeBlockInternal()`
- [ ] Remove deprecated `ExecuteBytecodeLoop()`
- [ ] Create focused `IBytecodeExecutionContext` interface
- [ ] Update VirtualMachine to delegate to InterpreterEngine
- [ ] Verify build and tests pass

### Phase 6: Interface Cleanup (Day 4)

Reduce interface count and eliminate duplication.

**Tasks**:
- [ ] Remove `IVMDebugHelpers` (use `IObjectMetadataAccessor`)
- [ ] Split `IBytecodeContext` into focused interfaces
- [ ] Remove explicit interface implementation boilerplate
- [ ] Update all consumers of old interfaces
- [ ] Verify build and tests pass

### Phase 7: Final Cleanup (Day 5)

Polish and documentation.

**Tasks**:
- [ ] Remove any remaining deprecated code
- [ ] Add XML documentation to new components
- [ ] Update architecture documentation
- [ ] Run full test suite
- [ ] Measure final line counts

---

## Test Checkpoints

### After Each Phase

```bash
cd Smalltalk80.Interpreter
dotnet build                           # Must pass with 0 errors
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

### Critical Test Points

| Phase | What to Verify |
|-------|----------------|
| 1 | State records compile, no behavior change |
| 2 | Trace output still works with `--diagnostics method` |
| 3 | Arithmetic/special selectors resolve correctly |
| 4 | Non-local returns unwind properly |
| 5 | All 120 BlockContext tests pass |
| 6 | All interfaces work after consolidation |

### Regression Test Cases

1. **Basic execution**: Simple arithmetic init expressions
2. **Message sending**: Method lookup and dispatch
3. **Block execution**: value, value:, value:value:
4. **Non-local return**: ^return from nested blocks
5. **Exception handling**: doesNotUnderstand:
6. **Selector caching**: Performance-critical paths

---

## Risk Mitigation

### Risk 1: Breaking NLR Semantics

**Mitigation**: Extract NLR handler without changing logic. Use same field names initially, then rename.

### Risk 2: Performance Regression

**Mitigation**: Keep hot paths inline. Only extract cold paths (tracing, error handling).

### Risk 3: Interface Compatibility

**Mitigation**: Create new interfaces alongside old ones, migrate incrementally, then remove old.

### Risk 4: Incomplete Extraction

**Mitigation**: Each phase is independently testable. Can pause and resume.

---

## Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| VirtualMachine.cs lines | 2,096 | ~300 |
| Interfaces implemented | 5 | 2 |
| IBytecodeContext members | 25+ | 8-10 |
| Deprecated code | 190 lines | 0 |
| Component count | 1 | 5 |

---

## Rollback Plan

If the rewrite causes issues:

1. Each phase creates new files - can delete without affecting old code
2. Old interface implementations remain until Phase 6
3. Can revert individual phases via git

---

## Dependencies

This rewrite should be done **after**:
- [ ] 1.2 Remove duplicated DisassembleBytecodes ✅
- [ ] 2.1 Remove IVMDebugHelpers overlap

This rewrite **enables**:
- Easier testing of interpreter components
- Clear separation of concerns
- Better documentation
- Future optimization opportunities

---

## Approval

Before starting implementation:
- [ ] Review this plan
- [ ] Confirm approach (rewrite vs. incremental)
- [ ] Identify any blockers or concerns
