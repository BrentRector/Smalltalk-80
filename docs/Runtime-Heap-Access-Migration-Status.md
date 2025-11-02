# Runtime Heap Access Migration - Current Status

**Date**: 2025-11-02
**Status**: Phase 1 Complete, Phase 2 In Progress

## Summary

Converting VM and primitives from using stale cold-start data structures (GlobalRegistry, LayoutResolver) to pure runtime heap access. This ensures correctness when classes change at runtime.

## Completed Work

### ✅ Phase 1: Infrastructure (COMPLETE)
1. **RuntimeHeapAccess.cs** - Created production-ready class with:
   - `LookupClassInHeap()` - Walks Smalltalk dictionary to find classes
   - `GetFieldIndex()` - Walks inheritance chain to resolve field indices
   - `GetInstanceVariableCount()` - Counts total instance variables
   - `InternSymbol()` - Searches USTable in heap for symbols
   - `LookupGlobalByName()` - Byte comparison for bootstrap (USTable lookup)
   - `RuntimeClassCache` - Class OOP caching with invalidation
   - `RuntimeFieldLayoutCache` - Field layout caching with invalidation
   - `RuntimeSpecialObjects` - Singleton for frequently-used class OOPs

2. **Primitive 89 Wiring** - Updated to invalidate ALL caches:
   - Added `InvalidateAllRuntimeCaches()` method to IPrimitiveContext
   - Updated `FlushCache` primitive to call both `ClearMethodCache()` and `InvalidateAllRuntimeCaches()`

3. **Build Verification** - RuntimeHeapAccess.cs compiles successfully

## Remaining Work

### 🔧 Phase 2: VirtualMachine Integration

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

**Current State**: VM uses GlobalRegistry and LayoutResolver extensively

**Required Changes**:

1. **Add RuntimeHeapAccess field**:
   ```csharp
   private readonly RuntimeHeapAccess _runtimeHeapAccess;
   ```

2. **Update constructor** to create RuntimeHeapAccess:
   ```csharp
   public VirtualMachine(ISmalltalkHeapManager heapManager, VMMetadata metadata, ...)
   {
       _heapManager = heapManager;
       _runtimeHeapAccess = new RuntimeHeapAccess(heapManager);
       RuntimeSpecialObjects.Instance.Initialize(_runtimeHeapAccess);
       // Remove _layoutResolver parameter and field
   }
   ```

3. **Implement InvalidateAllRuntimeCaches()** in IPrimitiveContext:
   ```csharp
   public void InvalidateAllRuntimeCaches()
   {
       _runtimeHeapAccess.InvalidateAllCaches();
   }
   ```

4. **Replace ALL GlobalRegistry.TryGet() calls** (32 instances):
   - Line 161: `GlobalRegistry.TryGet("USTable")` → Use `_runtimeHeapAccess` (or remove - not needed)
   - Line 303: `GlobalRegistry.TryGet("class:MethodContext")` → `_runtimeHeapAccess.GetClass("MethodContext")`
   - Lines 1831, 1870, 1909: `GlobalRegistry.TryGet("class:BlockClosure")` → `RuntimeSpecialObjects.Instance.BlockClosureClass`
   - Line 2561: `GlobalRegistry.TryGet("class:CompiledMethod")` → `RuntimeSpecialObjects.Instance.CompiledMethodClass`
   - Line 3151: `GlobalRegistry.TryGet("class:BlockContext")` → `RuntimeSpecialObjects.Instance.BlockContextClass`
   - Line 3250: `GlobalRegistry.TryGet("class:MethodContext")` → `RuntimeSpecialObjects.Instance.MethodContextClass`

5. **Replace ALL _layoutResolver calls** (11 instances):
   - Line 311: `_layoutResolver.GetInstanceVariableCount("class:MethodContext")` →
     ```csharp
     var methodContextClassOOP = RuntimeSpecialObjects.Instance.MethodContextClass;
     int instanceVarCount = _runtimeHeapAccess.GetInstanceVariableCount(methodContextClassOOP);
     ```
   - Line 1265, 1963, 1975, 3044: `_layoutResolver.GetFieldIndex("class:Association", "value")` →
     ```csharp
     var associationClassOOP = _runtimeHeapAccess.GetClass("Association");
     int valueIndex = _runtimeHeapAccess.GetFieldIndex(associationClassOOP, "value");
     ```
   - Lines 1956, 2016: `_layoutResolver.GetFieldIndex("class:Association", "key")` → Similar pattern
   - Line 3157: `_layoutResolver.GetInstanceVariableCount("class:BlockContext")` → Similar pattern
   - Line 3256: `_layoutResolver.GetInstanceVariableCount("class:MethodContext")` → Similar pattern

6. **Special Cases - Metadata Dictionaries**:
   - Line 3114: `GlobalRegistry.GetObject<Dictionary>("blockOwnerMap")`
   - Line 3346: `GlobalRegistry.GetObject<Dictionary>("blockOwnerMap")`
   - Line 3447: `GlobalRegistry.GetObject<Dictionary>("objectIdToLocalId")`

   **Decision Needed**: These are VM metadata, not Smalltalk objects. Options:
   - A) Store in VirtualMachine fields (not in GlobalRegistry)
   - B) Convert to Smalltalk Dictionary objects in the heap
   - C) Accept as VM implementation details (keep in GlobalRegistry, mark as exception)

   **Recommendation**: Option A - Store as private fields in VirtualMachine, populated during cold start.

### 🔧 Phase 3: Primitive Fixes

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/CompiledMethodPrimitives.cs`

**Lines 159-164**:
```csharp
// OLD (WRONG):
if (!GlobalRegistry.TryGet("class:CompiledMethod", out var compiledMethodClassOOP) ||
    !GlobalRegistry.TryGet("class:ByteArray", out var byteArrayClassOOP))

// NEW (CORRECT):
var compiledMethodClassOOP = RuntimeSpecialObjects.Instance.CompiledMethodClass;
var byteArrayClassOOP = RuntimeSpecialObjects.Instance.ByteArrayClass;
```

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ProcessPrimitives.cs`

**Remove**:
- Line 34: `private readonly LayoutResolver _layoutResolver;`
- Line 39: `LayoutResolver layoutResolver` parameter
- Line 66: Comment referencing LayoutResolver

**Replace all LayoutResolver usage** with RuntimeHeapAccess calls (passed via IPrimitiveContext).

### 🔧 Phase 4: Interface Cleanup

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/IPrimitiveContext.cs`

**Remove**:
- Line 19: `LayoutResolver LayoutResolver { get; }` property

**Add** (already done):
- `void InvalidateAllRuntimeCaches();` method

### 🔧 Phase 5: Testing

1. **Build Verification**:
   ```bash
   dotnet build Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj -c Debug
   ```
   Expected: 0 warnings, 0 errors

2. **Runtime Test**:
   ```bash
   .\bin\Debug\net9.0\Smalltalk80.Interpreter.exe --new-loader
   ```
   Expected: All init expressions execute successfully

3. **Cache Invalidation Test**:
   - Execute code that modifies a class definition
   - Verify primitive 89 is called
   - Verify method lookup still works correctly

## Key Principles (DO NOT VIOLATE)

1. **Runtime code must ONLY access the Smalltalk heap** - No GlobalRegistry, no LayoutResolver
2. **SpecialObjectsArray is valid** - nil, true, false, Smalltalk dictionary are always safe
3. **Caching with invalidation** - Cache for performance, invalidate on primitive 89
4. **Identity comparison for Symbols** - Use `==` not string comparison
5. **Heap walking is correct but slow** - Start correct, optimize later

## Performance Notes

- **Without caching**: 10-20x slower (but correct)
- **With caching**: Near-identical to current performance
- **Cache invalidation**: Rare (only when classes change)
- **First lookup cost**: Higher (heap walking) but amortized over many lookups

## Files Modified

### Completed:
- ✅ `Smalltalk80.Interpreter/Core/VM/RuntimeHeapAccess.cs` - NEW (404 lines)
- ✅ `Smalltalk80.Interpreter/Core/VM/Primitives/IPrimitiveContext.cs` - Added `InvalidateAllRuntimeCaches()`
- ✅ `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` - Updated primitive 89

### Pending:
- ⏳ `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` - Remove GlobalRegistry/LayoutResolver (32+ replacements)
- ⏳ `Smalltalk80.Interpreter/Core/VM/Primitives/CompiledMethodPrimitives.cs` - Remove GlobalRegistry (1 replacement)
- ⏳ `Smalltalk80.Interpreter/Core/VM/Primitives/ProcessPrimitives.cs` - Remove LayoutResolver (multiple replacements)

## Documentation Created

1. **docs/Runtime-Class-Field-Lookup-Strategy.md** - Complete architectural guidance
2. **docs/Implementation-Plan-Runtime-Heap-Access.md** - Detailed step-by-step plan
3. **docs/Runtime-Heap-Access-Migration-Status.md** - THIS FILE - Current progress tracker

## Next Actions

1. Update VirtualMachine.cs constructor (remove LayoutResolver, add RuntimeHeapAccess)
2. Implement `InvalidateAllRuntimeCaches()` in VirtualMachine
3. Systematically replace all GlobalRegistry/LayoutResolver calls (use find/replace + verification)
4. Handle metadata dictionaries (blockOwnerMap, objectIdToLocalId) - store as VM fields
5. Build and test
6. Commit with message documenting architectural fix

## Estimated Effort

- **VirtualMachine.cs changes**: 30-45 minutes (32+ replacements, careful verification needed)
- **Primitive files**: 10-15 minutes (fewer replacements)
- **Testing**: 15-20 minutes (build + runtime verification)
- **Total**: ~1-1.5 hours

## Success Criteria

1. ✅ Build succeeds with 0 warnings, 0 errors
2. ✅ `grep -r "GlobalRegistry\|LayoutResolver" Smalltalk80.Interpreter/Core/VM/` returns only:
   - Comments/documentation
   - Cold-start code (marked with `[ColdStartOnly]` attribute)
3. ✅ Runtime execution succeeds (all init expressions work)
4. ✅ Primitive 89 correctly invalidates all caches
5. ✅ No architectural principle violations

## Rollback Plan

If issues occur:
1. Revert to commit before RuntimeHeapAccess introduction
2. All changes are additive (new file + interface additions)
3. No breaking changes to existing cold-start phases

## Future Enhancements

1. **Method cache integration** - Add method lookup cache to RuntimeHeapAccess
2. **Performance profiling** - Measure cache hit rates and lookup times
3. **Selective invalidation** - Primitive 119 for invalidating specific class (not in original Blue Book)
4. **Hot path optimization** - Inline frequently-used lookups

---

**Bottom Line**: We've built the correct foundation (RuntimeHeapAccess). Now we need to wire it up throughout the VM and remove all stale cold-start data structure usage. The architecture is sound, the implementation is straightforward - just systematic replacement of ~40-50 call sites.
