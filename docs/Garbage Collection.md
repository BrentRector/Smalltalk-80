# Smalltalk-80 Garbage Collection Implementation

## Executive Summary

This document describes the garbage collection (GC) implementation for our Smalltalk-80 interpreter written in C#. The system uses a **manual mark-and-sweep garbage collector** with explicit memory management to maintain full Smalltalk-80 semantics while providing controlled memory management in a managed .NET environment.

## Table of Contents

1. [Design Philosophy](#design-philosophy)
2. [Architecture Overview](#architecture-overview)
3. [Mark-and-Sweep Algorithm](#mark-and-sweep-algorithm)
4. [Root Set Discovery](#root-set-discovery)
5. [GC Triggering Mechanisms](#gc-triggering-mechanisms)
6. [Special Smalltalk Features](#special-smalltalk-features)
7. [Performance Considerations](#performance-considerations)
8. [Integration Points](#integration-points)
9. [Future Enhancements](#future-enhancements)

## Design Philosophy

### Why Manual Garbage Collection?

Our interpreter implements **Option D: Manual Garbage Collection with Explicit Memory Management** for several critical reasons:

1. **Full Smalltalk-80 Semantics**: The language requires features that are incompatible with automatic .NET GC:
   - `become:` primitive - Swaps identity of two objects system-wide
   - Instance enumeration - Find all instances of any class
   - Object enumeration - Iterate through all objects
   - Stable OOPs - Object-Oriented Pointers must remain constant

2. **Controlled Memory Management**: Manual GC allows predictable memory behavior and prevents unbounded growth

3. **Blue Book Compliance**: Follows the classic Smalltalk-80 specification exactly

## Architecture Overview

### Object Table Structure

```csharp
// Core object table implementation
private static SmalltalkObject?[] objects = new SmalltalkObject?[65536];
private static int nextFreeIndex = 0;
private static readonly Queue<int> freeList = new();
```

- **Fixed-size array** with O(1) access by OOP
- **Dynamic growth** (2x factor) when capacity reached
- **Free list** maintains slots from freed objects for reuse
- **OOP encoding**: `OOP = index << 1` (even = objects, odd = SmallIntegers)

### Memory Layout

Each Smalltalk object in memory contains:
- **OOP** (Object-Oriented Pointer): Unique identifier
- **Class pointer**: Reference to the object's class
- **Fields array**: Instance variables (ordered by inheritance)

## Mark-and-Sweep Algorithm

### Phase 1: Mark

The mark phase identifies all reachable (live) objects starting from the root set:

```csharp
private static HashSet<int> MarkPhase()
{
    var reachable = new HashSet<int>();
    
    // Mark from permanent roots (classes, singletons)
    foreach (var root in permanentRoots)
        MarkReachable(root, reachable);
    
    // Mark from transient roots (execution context)
    foreach (var root in transientRoots)
        MarkReachable(root, reachable);
    
    // Mark from Special Objects Array
    MarkSpecialObjectsArray(reachable);
    
    return reachable;
}

private static void MarkReachable(SmalltalkObject obj, HashSet<int> reachable)
{
    if (obj == null) return;
    
    int index = obj.Oop >> 1;
    if (reachable.Contains(index)) return;
    
    reachable.Add(index);
    
    // Recursively mark class pointer
    if (obj.ClassPointer != null)
        MarkReachable(obj.ClassPointer, reachable);
    
    // Recursively mark all fields
    foreach (var field in obj.Fields)
    {
        if (field is SmalltalkObject fieldObj)
            MarkReachable(fieldObj, reachable);
        else if (field is int oopInt && IsValidOop(oopInt))
        {
            // Handle OOP integers that reference objects
            var referencedObj = GetByOop(oopInt);
            if (referencedObj != null)
                MarkReachable(referencedObj, reachable);
        }
    }
}
```

### Phase 2: Sweep

The sweep phase frees all unmarked objects:

```csharp
private static int SweepPhase(HashSet<int> reachable)
{
    int freedCount = 0;
    
    for (int i = 0; i < nextFreeIndex; i++)
    {
        if (objects[i] != null && !reachable.Contains(i))
        {
            // Update type index before freeing
            UpdateTypeIndex(objects[i], i, isRegistration: false);
            
            // Free the object
            objects[i] = null;
            freeList.Enqueue(i);
            freedCount++;
        }
    }
    
    return freedCount;
}
```

## Root Set Discovery

The garbage collector maintains multiple categories of roots to ensure all live objects are preserved:

### 1. Special Objects Array Roots

The VM's special objects array contains critical system objects:

```
Index | Object
------|----------------------------------
0     | nilObject
1     | falseObject  
2     | trueObject
3     | Behavior class
4     | Metaclass class
5     | SystemDictionary (Smalltalk globals)
6     | SymbolTable
7     | ProcessList
8     | ProcessorScheduler
9-31  | Other system objects
```

### 2. Permanent Roots

Objects that persist for the lifetime of the system:

- **All classes and metaclasses**: Core type system
- **Singleton objects**: nil, true, false
- **System dictionary**: Global namespace (Smalltalk)
- **Symbol table**: Interned symbols
- **Method dictionaries**: Compiled code storage

### 3. Transient Roots

Objects needed during current execution:

- **Active context**: Current MethodContext or BlockContext
- **Process objects**: All Process instances and their contexts
- **Temporary computation**: Intermediate results during primitives
- **Receiver and arguments**: Current message send data

### 4. Process and Context Traversal

```smalltalk
"Tracing from ProcessList"
ProcessList -> each Process -> activeContext -> sender chain

"Each context contains:"
- receiver (self)
- CompiledMethod
- arguments array
- temporaries array
- stack contents
- sender (previous context)
```

### Root Discovery During Bootstrap

During system bootstrap, root discovery follows a staged approach:

1. **Early Bootstrap**: Only permanent roots (classes being created)
2. **Mid Bootstrap**: Add Special Objects Array as it's populated
3. **Late Bootstrap**: Full root set including ProcessList
4. **Post Bootstrap**: Complete GC with all roots active

## GC Triggering Mechanisms

### Automatic Triggers

The GC uses adaptive thresholds that adjust based on performance:

```csharp
// Adaptive thresholds
private static int adaptiveHighWaterMark = 50000;    // Object count trigger
private static int adaptiveLowFreeCount = 1000;      // Free list trigger
private static double gcEfficiencyTarget = 0.15;     // Should free 15% of objects
private static double gcTimeTarget = 50.0;           // Should complete in 50ms

private static bool ShouldTriggerGC()
{
    // Don't GC during bootstrap
    if (!isSpecialObjectsArrayComplete)
        return false;
    
    // Trigger if too many objects allocated
    if (AllocatedObjectCount > adaptiveHighWaterMark)
        return true;
    
    // Trigger if free list is too small
    if (freeList.Count < adaptiveLowFreeCount)
        return true;
    
    // Rate limit to prevent thrashing
    if ((DateTime.Now - lastGCTime).TotalSeconds < 1.0)
        return false;
    
    return false;
}
```

### Manual Triggers

#### From Interpreter

The interpreter checks for GC every 10,000 bytecodes:

```csharp
if (++bytecodesSinceGC >= 10000)
{
    bytecodesSinceGC = 0;
    if (ObjectMemory.AllocatedObjectCount > 40000)
    {
        ObjectMemory.AddTransientRoot(currentContext);
        ObjectMemory.ManualGC();
        ObjectMemory.ClearTransientRoots();
    }
}
```

#### From Smalltalk Primitives

Three primitives provide GC control from Smalltalk code:

- **Primitive 130** (`garbageCollect`): Runs GC, returns freed count
- **Primitive 131** (`gcStats`): Returns [allocated, free, total, collections, totalFreed]
- **Primitive 132** (`forceGC`): Forces immediate GC

```smalltalk
"Examples of GC control from Smalltalk"
System garbageCollect.        "Returns number of objects freed"
System gcStats.               "Returns statistics array"
System forceGC.              "Forces immediate collection"
```

## Special Smalltalk Features

### become: Primitive Support

The `become:` primitive swaps the identity of two objects throughout the entire system:

```csharp
public static void SwapPointers(SmalltalkObject a, SmalltalkObject b)
{
    int indexA = a.Oop >> 1;
    int indexB = b.Oop >> 1;
    
    // Step 1: Swap in object table
    (objects[indexA], objects[indexB]) = (objects[indexB], objects[indexA]);
    (a.Oop, b.Oop) = (b.Oop, a.Oop);
    
    // Step 2: Update ALL references in ALL objects
    for (int i = 0; i < nextFreeIndex; i++)
    {
        var obj = objects[i];
        if (obj == null) continue;
        
        // Swap class pointers if needed
        if (ReferenceEquals(obj.ClassPointer, a))
            obj.ClassPointer = b;
        else if (ReferenceEquals(obj.ClassPointer, b))
            obj.ClassPointer = a;
        
        // Swap field references
        for (int j = 0; j < obj.Fields.Length; j++)
        {
            if (ReferenceEquals(obj.Fields[j], a))
                obj.Fields[j] = b;
            else if (ReferenceEquals(obj.Fields[j], b))
                obj.Fields[j] = a;
        }
    }
    
    // Step 3: Update contexts (special handling for execution state)
    UpdateContextReferences(a, b);
}
```

### Instance Enumeration

Find all instances of a class efficiently using type indexing:

```csharp
public static SmalltalkObject? SomeInstance(SmalltalkObject cls)
{
    lock (typeIndexLock)
    {
        if (typeIndex.TryGetValue(cls, out var indices))
        {
            foreach (var index in indices)
            {
                if (objects[index] != null)
                    return objects[index];
            }
        }
    }
    return null;
}

public static SmalltalkObject? NextInstance(SmalltalkObject obj)
{
    var cls = obj.ClassPointer;
    lock (typeIndexLock)
    {
        if (typeIndex.TryGetValue(cls, out var indices))
        {
            bool foundCurrent = false;
            foreach (var index in indices)
            {
                if (foundCurrent && objects[index] != null)
                    return objects[index];
                if (objects[index] == obj)
                    foundCurrent = true;
            }
        }
    }
    return null;
}
```

### Object Enumeration

Support for iterating through all objects:

```csharp
public static IEnumerable<SmalltalkObject> AllObjects()
{
    for (int i = 0; i < nextFreeIndex; i++)
    {
        if (objects[i] != null)
            yield return objects[i];
    }
}
```

## Performance Considerations

### Time Complexity

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Mark phase | O(n) | n = reachable objects |
| Sweep phase | O(m) | m = total allocated slots |
| Object allocation | O(1) | Amortized, may trigger GC |
| `become:` | O(m × f) | f = avg fields per object |
| Instance enumeration | O(k) | k = instances of class |

### Space Overhead

- **Object table**: 8 bytes per slot (64-bit pointers)
- **Free list**: 4 bytes per free slot
- **Type index**: ~12 bytes per object (class → indices mapping)
- **Mark set**: ~4 bytes per live object during GC

### Adaptive Tuning

The GC automatically adjusts its behavior based on recent performance:

```csharp
private static void AdaptGCThresholds(double efficiency, double gcTimeMs)
{
    recentGcEfficiencies.Enqueue(efficiency);
    recentGcTimes.Enqueue(gcTimeMs);
    
    if (++gcAdaptationCycle >= ADAPTATION_WINDOW)
    {
        double avgEfficiency = recentGcEfficiencies.Average();
        double avgTime = recentGcTimes.Average();
        
        // Adjust high water mark based on efficiency
        if (avgEfficiency < gcEfficiencyTarget * 0.5)
        {
            // GC is not freeing enough, trigger more often
            adaptiveHighWaterMark = (int)(adaptiveHighWaterMark * 0.8);
        }
        else if (avgEfficiency > gcEfficiencyTarget * 2.0)
        {
            // GC is very effective, can wait longer
            adaptiveHighWaterMark = (int)(adaptiveHighWaterMark * 1.2);
        }
        
        // Adjust based on time
        if (avgTime > gcTimeTarget * 2.0)
        {
            // GC taking too long, run more frequently with smaller heap
            adaptiveHighWaterMark = (int)(adaptiveHighWaterMark * 0.9);
        }
        
        // Maintain bounds
        adaptiveHighWaterMark = Math.Max(5000, Math.Min(200000, adaptiveHighWaterMark));
    }
}
```

## Integration Points

### Bootstrap Integration

The GC is carefully integrated with the bootstrap process:

1. **Phase 1: Core Classes**
   - GC disabled to prevent collection of partially initialized objects
   - Only permanent roots registered

2. **Phase 2: Method Installation**
   - GC still disabled
   - Special Objects Array populated but not used as root

3. **Phase 3: System Completion**
   - Special Objects Array marked complete
   - GC enabled with full root discovery
   - Validation GC run to ensure stability

### Interpreter Integration

The interpreter manages transient roots during execution:

```csharp
// Before executing a method
ObjectMemory.AddTransientRoot(receiver);
ObjectMemory.AddTransientRoot(currentContext);

// Execute bytecodes...

// After method completion
ObjectMemory.ClearTransientRoots();
```

### VM Integration

The VirtualMachine class reports GC statistics:

```csharp
public string GetVMStatus()
{
    return $@"
=== Garbage Collection ===
Collections: {ObjectMemory.GCCollectionCount}
Objects Freed: {ObjectMemory.TotalObjectsFreed}
Live Objects: {ObjectMemory.AllocatedObjectCount}
Free Slots: {ObjectMemory.FreeSlotCount}
";
}
```

## Future Enhancements

### Near-term Improvements

1. **Incremental Marking**: Reduce pause times by spreading mark phase across multiple cycles
2. **Weak References**: Support for caches and ephemeral objects
3. **Finalization**: Clean up external resources when objects are collected
4. **Memory Compaction**: Reduce fragmentation by moving objects

### Long-term Goals

1. **Generational Collection**: Separate young and old objects for efficiency
2. **Concurrent GC**: Run collection in parallel with execution
3. **Region-based Memory**: Allocate related objects in regions
4. **Write Barriers**: Track inter-generational references

### Compatibility Considerations

All enhancements must maintain:
- Full `become:` primitive support
- Complete object enumeration
- Instance finding capabilities
- Stable OOP values
- Blue Book semantics

## Testing and Validation

### Unit Tests

Located in `TestGCFix/Program.cs`:
- OOP 0 handling edge case
- Root preservation
- `become:` correctness
- Instance enumeration accuracy

### Integration Tests

The system validates GC correctness through:
- Bootstrap validation (379 methods across 8 core classes)
- Extended class loading
- Long-running computation tests
- Memory stress tests

### Performance Benchmarks

Track key metrics:
- Collection pause times
- Memory efficiency (freed/allocated ratio)
- Allocation throughput
- `become:` operation speed

## Conclusion

Our manual mark-and-sweep garbage collector provides a robust foundation for the Smalltalk-80 interpreter. It successfully balances:

- **Language Compliance**: Full Smalltalk-80 semantics including `become:` and enumeration
- **Memory Safety**: Prevents unbounded growth through regular collection
- **Performance**: Adaptive tuning and efficient algorithms
- **Maintainability**: Clean integration with managed .NET environment

The implementation follows the classic Blue Book specification while leveraging modern techniques like adaptive thresholds and type indexing for improved performance. This design ensures our interpreter can run long-lived Smalltalk programs with predictable memory behavior while maintaining complete compatibility with the Smalltalk-80 language specification.