# Implementation Plan: Runtime Heap Access Migration

## Summary of Findings

### Blue Book Primitives for Cache Management

1. **Primitive 89 (flushCache)**:
   - **Status**: ✅ Already implemented in `ControlPrimitives.cs`
   - **Current behavior**: Calls `ClearMethodCache()` which is currently a no-op
   - **Blue Book page**: 650
   - **Purpose**: Invalidates ALL caches when class definitions change

2. **Primitive 119 (flushCacheSelectively)**:
   - **Status**: ❌ Not implemented
   - **Blue Book**: Not in original Blue Book (added in later Smalltalk versions)
   - **Purpose**: Selective cache invalidation for specific class
   - **Recommendation**: Not needed for initial implementation

### Current Architecture Issues

1. **GlobalRegistry Usage**: 17 violations in VirtualMachine.cs, 2 in CompiledMethodPrimitives.cs
2. **LayoutResolver Usage**: Used throughout for field indices and instance variable counts
3. **Cold-start metadata**: `blockOwnerMap` and `objectIdToLocalId` are not Smalltalk objects

## Implementation Strategy

### Phase 1: Foundation (Immediate)

1. **Add RuntimeHeapAccess to VirtualMachine**:
```csharp
public partial class VirtualMachine : IPrimitiveContext
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly RuntimeHeapAccess _runtimeAccess;  // NEW
    // Remove: private readonly LayoutResolver _layoutResolver;

    public VirtualMachine(ISmalltalkHeapManager heapManager, VMMetadata metadata, ...)
    {
        _heapManager = heapManager;
        _runtimeAccess = new RuntimeHeapAccess(heapManager);  // NEW
        RuntimeSpecialObjects.Instance.Initialize(_runtimeAccess);  // NEW
    }
}
```

2. **Update ClearMethodCache()**:
```csharp
public void ClearMethodCache()
{
    // Invalidate all runtime caches
    _runtimeAccess.InvalidateAllCaches();
}
```

### Phase 2: Replace GlobalRegistry Calls (Priority Order)

#### High Priority (Core VM Operations)
1. **Line 161**: USTable lookup → Already in `RuntimeHeapAccess.InternSymbol()`
2. **Line 303**: MethodContext class → `RuntimeSpecialObjects.Instance.MethodContextClass`
3. **Lines 1831, 1870, 1909**: BlockClosure class → `RuntimeSpecialObjects.Instance.BlockClosureClass`
4. **Line 3151**: BlockContext class → `RuntimeSpecialObjects.Instance.BlockContextClass`

#### Medium Priority (Method Compilation)
5. **CompiledMethodPrimitives.cs:159-164**:
```csharp
// OLD:
if (!GlobalRegistry.TryGet("class:CompiledMethod", out var compiledMethodClassOOP) ||
    !GlobalRegistry.TryGet("class:ByteArray", out var byteArrayClassOOP))

// NEW:
var compiledMethodClassOOP = RuntimeSpecialObjects.Instance.CompiledMethodClass;
var byteArrayClassOOP = RuntimeSpecialObjects.Instance.ByteArrayClass;
if (compiledMethodClassOOP == SpecialObjectsArray.Instance.Nil ||
    byteArrayClassOOP == SpecialObjectsArray.Instance.Nil)
```

### Phase 3: Replace LayoutResolver Calls

Replace all `_layoutResolver` calls with `_runtimeAccess`:
- `_layoutResolver.GetFieldIndex()` → `_runtimeAccess.GetFieldIndex()`
- `_layoutResolver.GetInstanceVariableCount()` → `_runtimeAccess.GetInstanceVariableCount()`

### Phase 4: Handle Cold-Start Metadata

#### Option A: Store in Smalltalk Dictionary (Recommended)
```csharp
// During cold start Phase 5:
CreateSmalltalkDictionary("BlockOwnerMap", blockOwnerMap);
CreateSmalltalkDictionary("ObjectIdToLocalId", objectIdToLocalId);

// At runtime:
var blockOwnerMapDict = _runtimeAccess.GetGlobal("BlockOwnerMap");
var ownerLocalId = DictionaryLookup(blockOwnerMapDict, blockLocalId);
```

#### Option B: Keep as VM Implementation Detail
- Accept that block ownership is VM metadata, not Smalltalk data
- Store in VM instance fields populated during initialization
- Document as architectural exception

## Testing Plan

### Unit Tests
1. Test `RuntimeHeapAccess.LookupClassInHeap()` with known classes
2. Test `RuntimeHeapAccess.GetFieldIndex()` with known field layouts
3. Test cache invalidation via `ClearMethodCache()`

### Integration Tests
1. Execute init expressions with new runtime access
2. Verify correct method dispatch after class changes
3. Test primitive 89 (flushCache) behavior

### Regression Tests
1. Ensure all existing tests pass
2. Verify no performance degradation for common operations

## Migration Checklist

- [ ] Create `RuntimeHeapAccess.cs` (✅ Done)
- [ ] Add `RuntimeHeapAccess` field to `VirtualMachine`
- [ ] Initialize `RuntimeSpecialObjects` singleton
- [ ] Wire up `ClearMethodCache()` to invalidate caches
- [ ] Replace `GlobalRegistry.TryGet("USTable")` (line 161)
- [ ] Replace `GlobalRegistry.TryGet("class:MethodContext")` (line 303)
- [ ] Replace `GlobalRegistry.TryGet("class:BlockClosure")` (lines 1831, 1870, 1909)
- [ ] Replace `GlobalRegistry.TryGet("class:BlockContext")` (line 3151)
- [ ] Update `CompiledMethodPrimitives.cs` (lines 159-164)
- [ ] Replace all `_layoutResolver.GetFieldIndex()` calls
- [ ] Replace all `_layoutResolver.GetInstanceVariableCount()` calls
- [ ] Handle `blockOwnerMap` migration
- [ ] Handle `objectIdToLocalId` migration
- [ ] Remove `LayoutResolver` parameter from VM constructor
- [ ] Update all VM instantiation sites
- [ ] Run unit tests
- [ ] Run integration tests
- [ ] Update documentation

## Performance Impact

### Initial Implementation (No Caching)
- **Expected**: 10-20x slower for class/field lookups
- **Mitigation**: Temporary, caching will restore performance

### With Caching Enabled
- **Expected**: Near-identical performance to current implementation
- **Benefit**: Correct behavior when classes change at runtime

### Cache Invalidation Cost
- **Primitive 89**: ~1ms to clear all caches
- **Frequency**: Rare (only when modifying classes)

## Risk Assessment

### Low Risk
- Foundation classes (RuntimeHeapAccess) are additive
- Cache invalidation via existing primitive 89
- Can be tested incrementally

### Medium Risk
- Performance impact until caching is fully implemented
- Need to ensure USTable and Smalltalk dictionary are always accessible

### High Risk
- blockOwnerMap/objectIdToLocalId redesign may require significant changes
- Must ensure SpecialObjectsArray entries are set before use

## Recommendation

Proceed with phased implementation:
1. **Week 1**: Implement RuntimeHeapAccess foundation
2. **Week 2**: Replace high-priority GlobalRegistry calls
3. **Week 3**: Replace LayoutResolver usage
4. **Week 4**: Performance optimization and testing

This approach provides:
- ✅ Correct runtime behavior per Blue Book specification
- ✅ Cache invalidation via primitive 89
- ✅ Incremental migration path
- ✅ Performance parity with caching
- ✅ Future-proof architecture for runtime class changes