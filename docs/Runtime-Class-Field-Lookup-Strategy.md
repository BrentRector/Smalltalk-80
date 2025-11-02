# Runtime Class and Field Lookup Strategy for Smalltalk-80 Interpreter

## Executive Summary

The current VM violates the fundamental principle that runtime code must access ONLY the Smalltalk heap. This document provides the correct Blue Book approach for runtime class and field lookups, eliminating all dependency on cold-start data structures.

## Current Violations

### 1. CompiledMethodPrimitives.cs (Lines 159-164)
```csharp
// VIOLATION: Using GlobalRegistry at runtime
if (!GlobalRegistry.TryGet("class:CompiledMethod", out var compiledMethodClassOOP) ||
    !GlobalRegistry.TryGet("class:ByteArray", out var byteArrayClassOOP))
```

### 2. VirtualMachine.cs (Multiple violations)
- Line 161: `GlobalRegistry.TryGet("USTable")`
- Line 303: `GlobalRegistry.TryGet("class:MethodContext")`
- Lines 1831, 1870, 1909: `GlobalRegistry.TryGet("class:BlockClosure")`
- Line 3114: `GlobalRegistry.GetObject<Dictionary>("blockOwnerMap")`
- Line 3447: `GlobalRegistry.GetObject<Dictionary>("objectIdToLocalId")`
- Multiple uses of `_layoutResolver.GetFieldIndex()` and `_layoutResolver.GetInstanceVariableCount()`

## Blue Book Approach

### 1. Class Lookup at Runtime

According to the Blue Book (pages 588-592), the VM should:

#### Primary Method: Walk Smalltalk Global Dictionary
```csharp
public SmalltalkOOP LookupClassInHeap(string className)
{
    // Step 1: Get Smalltalk dictionary from SpecialObjectsArray
    var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;

    // Step 2: Intern the class name as a Symbol
    var symbolOOP = InternSymbol(className);

    // Step 3: Look up the symbol in the Dictionary
    return DictionaryLookup(smalltalkDictOOP, symbolOOP);
}
```

#### Caching Strategy
The Blue Book explicitly supports caching with invalidation:
- **Method Cache**: Selector → (Class, CompiledMethod) pairs
- **Class Cache**: Class name → Class OOP pairs
- **Field Layout Cache**: Class → field index mappings

**Primitive 89 (flushCache)** invalidates ALL caches when:
- A class definition changes
- Methods are added/removed
- Instance variables are modified

### 2. Field Index Resolution

The Blue Book approach (pages 270-275) for determining field indices:

```csharp
public int GetFieldIndexFromHeap(SmalltalkOOP classOOP, string fieldName)
{
    // Step 1: Walk up the inheritance chain collecting all instance variable names
    var fieldNames = new List<string>();
    var currentClass = classOOP;

    while (currentClass != SpecialObjectsArray.Instance.Nil)
    {
        // Get instanceVariables from the class (slot 4 in ClassDescription)
        var instVarsOOP = _heapManager.GetInstanceVariable(currentClass, 4);

        // Parse the instance variable names (stored as a String)
        var instVarNames = ParseInstanceVariableString(instVarsOOP);

        // Prepend to maintain inheritance order (superclass fields first)
        fieldNames.InsertRange(0, instVarNames);

        // Move to superclass (slot 0 in Behavior)
        currentClass = _heapManager.GetInstanceVariable(currentClass, 0);
    }

    // Step 2: Find the index of the requested field
    for (int i = 0; i < fieldNames.Count; i++)
    {
        if (fieldNames[i] == fieldName)
            return i;
    }

    throw new InvalidOperationException($"Field {fieldName} not found in class");
}
```

### 3. Caching Architecture

#### Class OOP Cache
```csharp
public class RuntimeClassCache
{
    private Dictionary<string, SmalltalkOOP> _classCache = new();
    private bool _cacheValid = true;

    public SmalltalkOOP GetClass(string className)
    {
        if (!_cacheValid || !_classCache.TryGetValue(className, out var classOOP))
        {
            classOOP = LookupClassInHeap(className);
            if (_cacheValid)
                _classCache[className] = classOOP;
        }
        return classOOP;
    }

    public void InvalidateCache()  // Called by primitive 89
    {
        _cacheValid = false;
        _classCache.Clear();
    }
}
```

#### Field Layout Cache
```csharp
public class RuntimeFieldLayoutCache
{
    private Dictionary<SmalltalkOOP, Dictionary<string, int>> _layoutCache = new();
    private bool _cacheValid = true;

    public int GetFieldIndex(SmalltalkOOP classOOP, string fieldName)
    {
        if (!_cacheValid)
        {
            _layoutCache.Clear();
            _cacheValid = true;
        }

        if (!_layoutCache.TryGetValue(classOOP, out var fieldMap))
        {
            fieldMap = BuildFieldMap(classOOP);
            _layoutCache[classOOP] = fieldMap;
        }

        return fieldMap[fieldName];
    }

    public void InvalidateCache()  // Called by primitive 89
    {
        _cacheValid = false;
    }
}
```

### 4. SpecialObjectsArray Extension Pattern

For frequently-accessed classes, cache them AFTER first heap lookup:

```csharp
public class RuntimeSpecialObjects
{
    private static RuntimeSpecialObjects? _instance;
    public static RuntimeSpecialObjects Instance => _instance ??= new();

    // Cached class OOPs (populated on first use)
    private SmalltalkOOP? _compiledMethodClass;
    private SmalltalkOOP? _byteArrayClass;
    private SmalltalkOOP? _methodContextClass;
    private SmalltalkOOP? _blockContextClass;
    private SmalltalkOOP? _blockClosureClass;

    public SmalltalkOOP CompiledMethodClass =>
        _compiledMethodClass ??= LookupClassInHeap("CompiledMethod");

    public SmalltalkOOP ByteArrayClass =>
        _byteArrayClass ??= LookupClassInHeap("ByteArray");

    // ... similar for other frequently-used classes

    public void InvalidateCache()  // Called by primitive 89
    {
        _compiledMethodClass = null;
        _byteArrayClass = null;
        _methodContextClass = null;
        _blockContextClass = null;
        _blockClosureClass = null;
    }
}
```

## Implementation Plan

### Phase 1: Core Infrastructure
1. Implement `LookupClassInHeap()` method
2. Implement `GetFieldIndexFromHeap()` method
3. Implement `DictionaryLookup()` helper
4. Implement `ParseInstanceVariableString()` helper

### Phase 2: Caching Layer
1. Create `RuntimeClassCache` class
2. Create `RuntimeFieldLayoutCache` class
3. Create `RuntimeSpecialObjects` singleton
4. Wire up primitive 89 to call `InvalidateCache()` on all caches

### Phase 3: Migration
1. Replace all `GlobalRegistry.TryGet("class:X")` with `RuntimeClassCache.GetClass("X")`
2. Replace all `_layoutResolver.GetFieldIndex()` with `RuntimeFieldLayoutCache.GetFieldIndex()`
3. Replace all `_layoutResolver.GetInstanceVariableCount()` with heap-based counting
4. Remove `GlobalRegistry` and `LayoutResolver` dependencies from VM and primitives

### Phase 4: Special Cases
1. **blockOwnerMap**: This is cold-start metadata, not Smalltalk data. Solutions:
   - Store in a Smalltalk Dictionary during cold start
   - OR: Redesign to embed ownership in CompiledMethod objects
   - OR: Accept that block ownership is a VM implementation detail

2. **objectIdToLocalId**: Same as blockOwnerMap - cold-start metadata that needs redesign

## Performance Considerations

### Blue Book Balance
The Blue Book (page 592) explicitly states:
> "The virtual machine may cache the results of method and variable lookups to improve performance, but must invalidate these caches when the class hierarchy or method dictionaries change."

### Recommended Strategy
1. **Initial Implementation**: Pure heap walking (correct but slow)
2. **Optimization Pass 1**: Add class OOP caching (significant improvement)
3. **Optimization Pass 2**: Add field layout caching (moderate improvement)
4. **Optimization Pass 3**: Add method lookup cache (major improvement)

### Cache Invalidation Triggers
- Primitive 89: `flushCache` - Invalidates ALL caches
- Primitive 119: `flushCacheSelectively` (if implemented) - Invalidates specific class
- Class definition changes (detected by VM)
- Method dictionary modifications (detected by VM)

## Dictionary Lookup Implementation

```csharp
private SmalltalkOOP DictionaryLookup(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
{
    // Dictionary has 2 indexed slots: keys array and values array
    var keysArrayOOP = _heapManager.GetIndexedOOP(dictOOP, 0);
    var valuesArrayOOP = _heapManager.GetIndexedOOP(dictOOP, 1);

    // Linear search through keys (Dictionary uses Set's hashing internally)
    int size = _heapManager.GetIndexedSize(keysArrayOOP);
    for (int i = 0; i < size; i++)
    {
        var key = _heapManager.GetIndexedOOP(keysArrayOOP, i);
        if (key == keyOOP)  // Identity comparison for Symbols
        {
            return _heapManager.GetIndexedOOP(valuesArrayOOP, i);
        }
    }

    return SpecialObjectsArray.Instance.Nil;  // Not found
}
```

## Critical Implementation Notes

1. **Never store raw indices**: Always resolve dynamically from heap
2. **Identity comparison for Symbols**: Use `==` not string comparison
3. **Nil checks everywhere**: Heap traversal can encounter nil at any point
4. **Cache invalidation is critical**: Stale caches cause incorrect behavior
5. **Bootstrap special case**: During cold start phases 0-14, GlobalRegistry is valid

## Testing Strategy

1. **Unit Tests**: Test each heap lookup method independently
2. **Integration Tests**: Verify correct behavior when classes change at runtime
3. **Performance Tests**: Measure impact of caching vs pure heap walking
4. **Stress Tests**: Rapid class modifications with cache invalidation

## Conclusion

The transition from GlobalRegistry/LayoutResolver to pure heap access is essential for correctness. The Blue Book provides clear guidance: runtime code must access only the Smalltalk heap, with optional caching for performance. Primitive 89 provides the cache invalidation mechanism to maintain correctness when the system changes.

This approach ensures:
- ✅ Correct behavior when classes change at runtime
- ✅ No dependency on stale cold-start data
- ✅ Full compliance with Blue Book architecture
- ✅ Reasonable performance with caching
- ✅ Clear invalidation semantics via primitive 89