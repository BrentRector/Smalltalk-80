# Cold Start / Runtime Isolation Plan

**Created**: 2025-12-27
**Status**: Partially Implemented
**Updated**: 2025-12-27

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

## Implemented: Two-Stage Lifecycle

All three registries now implement a two-stage lifecycle:

1. **MakeImmutable()** - Blocks writes, allows reads
   - `SymbolRegistry.MakeImmutable()` - Called at end of Phase 1
   - `AssociationRegistry.MakeImmutable()` - Called at end of Phase 5
   - `GlobalRegistry.MakeImmutable()` - Called at end of Phase 6

2. **Dispose()** - Blocks ALL access, clears data
   - Called by `ColdStartOrchestrator.DisposeRegistries()` after `BuildVMMetadata()`
   - All subsequent access throws `ObjectDisposedException`

**Key Change**: `BuildVMMetadata()` is now called AS THE FINAL STEP of cold start, inside `ColdStartOrchestrator.Run()`. This extracts all needed OOPs into `VMMetadata`, then `DisposeRegistries()` is called immediately after to block all future access.

## Implementation Progress

### Phase 1: Enforce Freeze on Reads ✅ COMPLETE

1. ✅ `GlobalRegistry` - Two-stage lifecycle (MakeImmutable + Dispose)
2. ✅ `SymbolRegistry` - Two-stage lifecycle (MakeImmutable + Dispose)
3. ✅ `AssociationRegistry` - Two-stage lifecycle (MakeImmutable + Dispose)

### Phase 4: Clear Transient Data ✅ COMPLETE

All registries are disposed after `BuildVMMetadata()` extracts needed OOPs:

```csharp
// In ColdStartOrchestrator.Run() - final steps
VMMetadata = BuildVMMetadata();      // Extract needed OOPs
DisposeRegistries();                  // Block all future access

public static void DisposeRegistries()
{
    SymbolRegistry.Dispose();
    GlobalRegistry.Dispose();
    AssociationRegistry.Dispose();
}
```

### Dead Code Removed ✅ COMPLETE

Removed from `Program.cs`:
- `ResolveLiteralValueAsOOP()` - Duplicated in `LiteralResolver`
- `AllocateCharacter()`, `AllocateByteArray()`, `AllocateArray()` - Unused helpers
- `ResolveAssociation()` - Duplicated in `LiteralResolver`
- `ConvertHexStringToBytes()`, `GetIntValue()`, `GetFloatValue()` - Unused helpers
- `FormatReturnValue()` - Unused
- `DumpInitExpressionDebugInfo()` - Unused
- Duplicate `BuildVMMetadata()` - Now only in orchestrator

### Phase 2: Fix Violations (Remaining)

These may already be resolved by the disposal approach - registries throw after cold start:

1. **ObjectGraphAllocator.cs** - Only used during cold start (before dispose)
2. **InitExpressionExecutor.cs** - Runs during cold start (before dispose)
3. **VirtualMachine.cs** - Should not access registries (will throw if it does)
4. **SmalltalkInterpreterThread.cs** - Should not access registries
5. **DiagnosticInspector.cs** - May need to use VMMetadata instead

### Phase 3: Relocate Cold Start Structures (Future)

This provides compile-time enforcement but is lower priority now that runtime enforcement is in place:

1. Create `Engine/ColdStart/` directory
2. Move registry files with new namespace
3. Core/VM code will get compile errors if it tries to use them

## Verification Checklist

- [x] `GlobalRegistry` throws on all access after dispose
- [x] `SymbolRegistry` throws on all access after dispose
- [x] `AssociationRegistry` throws on all access after dispose
- [x] Transient data cleared after cold start
- [x] `BuildVMMetadata()` consolidated into orchestrator
- [x] Dead code removed from Program.cs
- [ ] Cold start structures moved to `Engine/ColdStart/`
- [ ] `Core/VM/` code compiles without cold start imports
- [ ] Tests pass
- [ ] Documentation updated

## Files Modified

### Completed
- `Core/VM/GlobalRegistry.cs` - Two-stage lifecycle
- `Core/VM/SymbolRegistry.cs` - Two-stage lifecycle
- `Core/VM/AssociationRegistry.cs` - Two-stage lifecycle
- `Engine/ColdStartOrchestrator.cs` - BuildVMMetadata + DisposeRegistries
- `Engine/LoaderPhases/Phase1_PreflightAndSymbols.cs` - MakeImmutable()
- `Engine/LoaderPhases/Phase5/Phase5Orchestrator.cs` - MakeImmutable()
- `Program.cs` - Removed duplicate BuildVMMetadata, dead code cleanup
