# Cold Start Implementation Comparison

## Current Implementation vs Specification

### ✅ What We Have Right

1. **JSON Schema** - Fully defined with polymorphic types in `ColdStartJsonSchema.cs`
2. **Phase-based approach** - Partially implemented in `JsonColdStartBootstrap.cs`
3. **LocalId resolution** - Implemented in `ColdStartJsonLoader.cs`
4. **Symbol interning** - Implemented in `SymbolInterning.cs`
5. **Patch system** - Working in `PatchApplicator.cs`

### ❌ What Needs Fixing

1. **No formal VM interface** - Classes directly call `ObjectMemory` instead of going through an abstraction
2. **Phases mixed together** - Logic spread across multiple files without clear phase boundaries
3. **No USTable population phase** - Missing Phase 9 from specification
4. **Tight coupling** - Cold start code is tightly coupled to `ObjectMemory` implementation
5. **No validation phase** - Missing Phase 0 preflight checks

### 📊 Current vs Proposed Structure

| Current Structure | Proposed Structure |
|-------------------|-------------------|
| `ColdStart/JsonColdStartBootstrap.cs` | `ColdStartLoader/Loader.cs` |
| `ColdStart/ClassAllocator.cs` | `ColdStartLoader/LoaderPhases/Phase1_ClassShells.cs` |
| `ColdStart/ClassPopulator.cs` | `ColdStartLoader/LoaderPhases/Phase3_ClassResolution.cs` |
| `ColdStart/ObjectAllocator.cs` | `ColdStartLoader/LoaderPhases/Phase5_ObjectAllocation.cs` |
| `ColdStart/MethodInstaller.cs` | `ColdStartLoader/LoaderPhases/Phase6_MethodInstallation.cs` |
| `ColdStart/PatchApplicator.cs` | `ColdStartLoader/LoaderPhases/Phase7_PatchApplication.cs` |
| `ColdStart/InitExpressionExecutor.cs` | `ColdStartLoader/LoaderPhases/Phase8_InitExecution.cs` |
| `ColdStart/SymbolInterning.cs` | `ColdStartLoader/LoaderPhases/Phase9_USTablePopulation.cs` |
| ❌ Missing | `ColdStartLoader/LoaderPhases/Phase0_Preflight.cs` |
| ❌ Missing | `ColdStartLoader/LoaderPhases/Phase2_MetaclassWiring.cs` |
| ❌ Missing | `ColdStartLoader/LoaderPhases/Phase4_MethodDictionaries.cs` |
| `ColdStart/ColdStartJsonSchema.cs` | `ColdStartLoader/Schema/*.cs` (split into multiple files) |
| ❌ Missing | `ColdStartLoader/VM/ISmalltalkHeap.cs` |
| ❌ Missing | `ColdStartLoader/VM/HeapAdapter.cs` (implements ISmalltalkHeap) |

## Benefits of Proposed Restructuring

### 1. **Separation of Concerns**
- Cold start loader doesn't know about ObjectMemory internals
- VM implementation can change without affecting loader
- Easier to test phases in isolation

### 2. **Clear Phase Boundaries**
- Each phase is a separate class with single responsibility
- Easy to understand order of operations
- Can add logging/profiling per phase

### 3. **Testability**
- Can mock `ISmalltalkHeap` for unit tests
- Can test each phase independently
- Can validate phase outputs before proceeding

### 4. **Maintainability**
- New developers can understand structure quickly
- Spec maps directly to code structure
- Easy to add new phases or modify existing ones

### 5. **Flexibility**
- Could target different VMs by implementing different ISmalltalkHeap
- Could support different cold start formats
- Could parallelize independent phases

## Migration Plan

### Step 1: Create VM Abstraction (Non-Breaking)
- Create `ISmalltalkHeap` interface
- Create `ObjectMemoryHeapAdapter` that implements it
- Both old and new code can coexist

### Step 2: Create Phase Classes (Non-Breaking)
- Create empty phase classes following spec
- Move logic from existing files incrementally
- Keep old files until migration complete

### Step 3: Create New Loader Entry Point (Non-Breaking)
- Create `Loader.cs` that orchestrates phases
- Run both old and new loaders side-by-side for validation

### Step 4: Switch and Cleanup (Breaking)
- Switch VM to use new loader
- Delete old cold start files
- Update documentation

## Next Steps

1. ✅ Define `ISmalltalkHeap` interface
2. ✅ Implement `ObjectMemoryHeapAdapter`
3. ✅ Create phase directory structure
4. ✅ Migrate each phase one at a time
5. ✅ Add Phase 0 (Preflight validation)
6. ✅ Add Phase 9 (USTable population)
7. ✅ Test end-to-end
8. ✅ Remove old code
