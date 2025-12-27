# Cold Start / Runtime Isolation Plan

**Created**: 2025-12-27
**Status**: Planning

## Problem Statement

Cold start data structures (`GlobalRegistry`, `SymbolRegistry`) are currently:
1. Located in `Core/VM/` making them visible to runtime VM code
2. Allowing read access after `Freeze()` is called
3. Being accessed during runtime execution (stale data risk)

The Smalltalk heap is the source of truth at runtime. Cold start structures become stale as Smalltalk code modifies the heap.

## Architectural Principles

1. **Cold start structures are transient** - Only valid during Phases 1-6
2. **VMMetadata is the handoff point** - Immutable snapshot passed to VM
3. **Runtime code reads the heap** - Via VMMetadata OOPs + RuntimeHeapAccessor
4. **Enforce at compile time** - VM code should not have visibility to cold start types

## Current State

### Cold Start Structures (should be Engine-only)
| Structure | Current Location | Should Be |
|-----------|------------------|-----------|
| `GlobalRegistry` | `Core/VM/` | `Engine/ColdStart/` |
| `SymbolRegistry` | `Core/VM/` | `Engine/ColdStart/` |
| `AssociationRegistry` | `Core/VM/` | `Engine/ColdStart/` |

### Runtime Structures (correct location)
| Structure | Location | Purpose |
|-----------|----------|---------|
| `VMMetadata` | `Core/VM/` | Immutable config from cold start |
| `RuntimeHeapAccessor` | `Core/VM/` | Heap queries at runtime |
| `RuntimeClassCache` | `Core/VM/` | Heap-backed cache with invalidation |

## Post-Freeze Violations Found

### Must Fix (runtime execution paths)
1. **ObjectGraphAllocator.cs:294-522** - `LookupClassOOP("Array")` etc.
   - Should use VMMetadata class OOPs instead

2. **VirtualMachine.cs:1133** - `GetObject<>("objectIdToLocalId")`
   - This is VM metadata, should be in VMMetadata or removed

### Gray Area (init expression phase)
3. **InitExpressionExecutor.cs:423-825** - `Get("Smalltalk")`
   - Runs after freeze but before full runtime
   - Should receive SmalltalkOOP via parameter, not query GlobalRegistry

4. **SmalltalkInterpreterThread.cs:685** - `TryGet("Undeclared")`
   - Used for diagnostics during startup
   - Should receive via parameter or query heap

### Diagnostics (lower priority)
5. **DiagnosticInspector.cs:39-326** - `TryGet("class:Float")` etc.
   - Used for debug output formatting
   - Could use VMMetadata class OOPs or accept stale data for debug

## Implementation Plan

### Phase 1: Enforce Freeze on Reads (Immediate)

1. Modify `GlobalRegistry.Get()`, `TryGet()`, `LookupClassOOP()`, `GetObject()` to throw after freeze
2. Modify `SymbolRegistry` similarly
3. Run tests to identify all violations

### Phase 2: Fix Violations (Short-term)

1. **ObjectGraphAllocator** - Pass VMMetadata or required class OOPs as parameters
2. **InitExpressionExecutor** - Pass SmalltalkOOP for Smalltalk dictionary as parameter
3. **VirtualMachine.GetLocalId** - Move objectIdToLocalId to VMMetadata or remove
4. **SmalltalkInterpreterThread** - Pass required OOPs via constructor/method params
5. **DiagnosticInspector** - Use VMMetadata class OOPs

### Phase 3: Relocate Cold Start Structures (Medium-term)

1. Create `Engine/ColdStart/` directory
2. Move `GlobalRegistry.cs` → `Engine/ColdStart/GlobalRegistry.cs`
3. Move `SymbolRegistry.cs` → `Engine/ColdStart/SymbolRegistry.cs`
4. Move `AssociationRegistry.cs` → `Engine/ColdStart/AssociationRegistry.cs`
5. Update namespaces: `Smalltalk80.Interpreter.Engine.ColdStart`
6. Update all Engine code to use new namespace
7. **Core/VM code will get compile errors if it tries to use them**

### Phase 4: Clear Transient Data (Medium-term)

After VMMetadata is created and passed to VM:

```csharp
// In ColdStartOrchestrator after creating VMMetadata
GlobalRegistry.Dispose();  // Clears all dictionaries, sets to null
SymbolRegistry.Dispose();  // Clears symbol table
// These classes now throw ObjectDisposedException on any access
```

### Phase 5: Documentation (Final)

1. Update CLAUDE.md with cold start vs runtime distinction
2. Add XML docs explaining the architectural boundary
3. Document VMMetadata as the sole handoff mechanism

## Verification Checklist

- [ ] `GlobalRegistry` throws on all access after freeze
- [ ] `SymbolRegistry` throws on all access after freeze
- [ ] All post-freeze violations fixed
- [ ] Cold start structures moved to `Engine/ColdStart/`
- [ ] `Core/VM/` code compiles without cold start imports
- [ ] Transient data cleared after cold start
- [ ] Tests pass
- [ ] Documentation updated

## Files to Modify

### Phase 1
- `Core/VM/GlobalRegistry.cs` - Add throws on read after freeze
- `Core/VM/SymbolRegistry.cs` - Add throws on read after freeze

### Phase 2
- `Core/ObjectGraphAllocator.cs` - Use VMMetadata
- `Engine/InitExecution/InitExpressionExecutor.cs` - Accept OOPs as params
- `Core/VM/VirtualMachine.cs` - Remove GlobalRegistry.GetObject call
- `SmalltalkInterpreterThread.cs` - Accept required OOPs
- `Core/Diagnostics/DiagnosticInspector.cs` - Use VMMetadata

### Phase 3
- Create `Engine/ColdStart/` directory
- Move and rename registry files
- Update all `using` statements in Engine code

### Phase 4
- `GlobalRegistry.cs` - Add Dispose() method
- `SymbolRegistry.cs` - Add Dispose() method
- `Engine/ColdStartOrchestrator.cs` - Call Dispose() after handoff
