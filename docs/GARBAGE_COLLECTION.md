# Smalltalk-80 VM Garbage Collection Design

## Overview

This document describes the garbage collection implementation for the Smalltalk-80 VM. The system uses **Option D: Manual Garbage Collection with Explicit Memory Management** to maintain full Smalltalk-80 semantics (including `become:` and instance enumeration) while preventing unbounded memory growth.

## Design Philosophy

### Why Manual GC?

The Smalltalk-80 language requires several features that make automatic garbage collection challenging:

1. **`become:` primitive** - Must find and update ALL references to swapped objects
2. **Instance enumeration** - Must be able to find all instances of any class
3. **Object enumeration** - Must be able to iterate through all objects
4. **Stable OOPs** - Object pointers must remain stable for debugging and identity

These requirements mean we must maintain a global object table that tracks all objects ever created. This prevents automatic collection by .NET's GC since the object table holds strong references.

### Option D: Manual GC Solution

Our implementation maintains a global object table but implements manual garbage collection using a mark-and-sweep algorithm. This provides:

- Full Smalltalk-80 semantics support
- Controlled memory management
- Predictable GC behavior
- No unsafe code required

## Architecture

### Object Table Structure

```csharp
// Object table - uses array for O(1) access by OOP
private static SmalltalkObject?[] objects = new SmalltalkObject?[65536];
private static int nextFreeIndex = 0;
private static readonly Queue<int> freeList = new();
```

- **Fixed-size array** initially (65536 slots)
- **Grows dynamically** when needed (2x growth factor)
- **Free list** maintains available slots from freed objects
- **OOP encoding**: `OOP = index << 1` (even numbers for objects, odd for SmallIntegers)

### Root Set Management

The GC maintains two types of roots:

#### Permanent Roots
Objects that should never be collected:
- All classes and metaclasses
- Singleton objects (nil, true, false)
- System dictionary (Smalltalk globals)
- Critical system objects

```csharp
ObjectMemory.AddPermanentRoot(classObject);
```

#### Transient Roots
Objects needed during current execution:
- Current execution context
- Active method receivers
- Temporary computation results

```csharp
ObjectMemory.AddTransientRoot(currentReceiver);
// ... execution ...
ObjectMemory.ClearTransientRoots();
```

## Garbage Collection Algorithm

### Mark-and-Sweep Implementation

The GC uses a traditional mark-and-sweep algorithm:

#### Phase 1: Mark
1. Start from all root objects (permanent + transient)
2. Recursively mark all reachable objects
3. Track marked objects in a HashSet for efficiency

```csharp
private static void MarkReachable(SmalltalkObject obj, HashSet<int> reachable)
{
    if (obj == null) return;
    
    int index = obj.Oop >> 1;
    if (reachable.Contains(index)) return;
    
    reachable.Add(index);
    
    // Mark class pointer
    if (obj.ClassPointer != null)
        MarkReachable(obj.ClassPointer, reachable);
    
    // Mark all fields
    foreach (var field in obj.Fields)
    {
        if (field is SmalltalkObject fieldObj)
            MarkReachable(fieldObj, reachable);
    }
}
```

#### Phase 2: Sweep
1. Scan entire object table
2. Free unmarked objects (set to null)
3. Add freed slots to free list for reuse

```csharp
for (int i = 0; i < nextFreeIndex; i++)
{
    if (objects[i] != null && !reachable.Contains(i))
    {
        objects[i] = null;
        freeList.Enqueue(i);
        freedCount++;
    }
}
```

## GC Triggering

### Automatic Triggers

GC runs automatically when:
- Allocated object count exceeds HIGH_WATER_MARK (50,000 objects)
- Free list drops below LOW_FREE_COUNT (1,000 slots)
- At least 1 second has passed since last GC (prevents thrashing)

### Manual Triggers

GC can be triggered from:

#### Interpreter Check Points
- Every 10,000 bytecodes executed
- On method returns
- When switching contexts

```csharp
if (++bytecodesSinceGC >= GC_CHECK_INTERVAL)
{
    bytecodesSinceGC = 0;
    if (ObjectMemory.AllocatedObjectCount > 40000)
    {
        ObjectMemory.ManualGC();
    }
}
```

#### Smalltalk Primitives
- **Primitive 130**: `garbageCollect` - Runs GC and returns freed count
- **Primitive 131**: `gcStats` - Returns GC statistics array
- **Primitive 132**: `forceGC` - Forces immediate GC

```smalltalk
"Run garbage collection from Smalltalk"
System garbageCollect.

"Get GC statistics"
System gcStats. "Returns: #(allocated free total collections totalFreed)"

"Force immediate GC"
System forceGC.
```

## Memory Management

### Object Allocation

1. **Check free list** for available slots
2. **Use free slot** if available
3. **Allocate new slot** at end of table if no free slots
4. **Grow table** if at capacity (doubles size)
5. **Trigger GC** if thresholds exceeded

```csharp
internal static void Register(SmalltalkObject obj)
{
    int index;
    if (freeList.Count > 0)
    {
        index = freeList.Dequeue();
        objects[index] = obj;
    }
    else
    {
        if (nextFreeIndex >= objects.Length)
            GrowObjectTable();
        index = nextFreeIndex++;
        objects[index] = obj;
    }
    obj.Oop = index << 1;
    
    if (ShouldTriggerGC())
        ManualGC();
}
```

### Memory Statistics

The system tracks:
- Total allocations
- Total collections performed
- Total objects freed
- Current live object count
- Free slot count

```csharp
var stats = ObjectMemory.GetMemoryStats();
// Returns: (allocated, free, total, collections)
```

## Special Considerations

### become: Support

The `become:` primitive requires scanning ALL live objects to swap references:

```csharp
public static void SwapPointers(SmalltalkObject a, SmalltalkObject b)
{
    // Swap in object table
    (objects[indexA], objects[indexB]) = (objects[indexB], objects[indexA]);
    (a.Oop, b.Oop) = (b.Oop, a.Oop);
    
    // Update all references in all live objects
    for (int i = 0; i < nextFreeIndex; i++)
    {
        var obj = objects[i];
        if (obj != null)
        {
            // Swap references in fields
            for (int j = 0; j < obj.Fields.Length; j++)
            {
                if (ReferenceEquals(obj.Fields[j], a)) 
                    obj.Fields[j] = b;
                else if (ReferenceEquals(obj.Fields[j], b)) 
                    obj.Fields[j] = a;
            }
        }
    }
}
```

### Instance Enumeration

The system supports finding all instances of a class:

```csharp
public static SmalltalkObject? SomeInstance(SmalltalkObject cls)
{
    for (int i = 0; i < nextFreeIndex; i++)
    {
        var obj = objects[i];
        if (obj != null && ReferenceEquals(obj.ClassPointer, cls))
            return obj;
    }
    return null;
}
```

## Performance Characteristics

### Time Complexity
- **Mark phase**: O(n) where n = number of reachable objects
- **Sweep phase**: O(m) where m = total allocated slots
- **become:**: O(m * f) where f = average fields per object
- **Object allocation**: O(1) amortized

### Space Complexity
- **Object table**: 8 bytes per slot (pointer size)
- **Free list**: 4 bytes per free slot
- **Mark set**: ~4 bytes per live object during GC

### GC Pause Times
- Typical collection: 10-50ms for 10,000 objects
- Large collection: 100-500ms for 50,000 objects
- Logged when >100ms or any objects freed

## Configuration

### Tunable Parameters

```csharp
// GC triggers
private const int HIGH_WATER_MARK = 50000;  // Trigger GC above this
private const int LOW_FREE_COUNT = 1000;    // Trigger GC below this

// Growth
private const int GROWTH_FACTOR = 2;        // Table growth multiplier

// Timing
private const int GC_CHECK_INTERVAL = 10000; // Bytecodes between checks
```

### Adjusting for Different Workloads

#### Memory-Constrained Environment
- Lower HIGH_WATER_MARK (e.g., 10000)
- More frequent GC checks
- Smaller initial table size

#### Performance-Critical Environment
- Higher HIGH_WATER_MARK (e.g., 100000)
- Less frequent GC checks
- Larger initial table size

## Debugging Support

### GC Logging

The system logs GC activity when:
- Objects are freed
- Collection takes >100ms
- Forced GC is requested

```
[GC] Collection #1: Freed 1523 objects in 45ms. Live objects: 8477, Total freed: 1523
[ObjectMemory] Grew object table from 65536 to 131072
[GC] Forced collection requested
```

### Memory Leak Detection

To detect leaks:
1. Monitor `AllocatedObjectCount` over time
2. Check GC statistics for low freed counts
3. Use `CountInstancesOf` to find accumulating objects
4. Review root set for unnecessary permanent roots

## Limitations

### Current Implementation
1. **No generational GC** - All objects treated equally
2. **Stop-the-world** - GC pauses execution
3. **No compaction** - Fragmentation possible
4. **Conservative marking** - May retain some garbage

### Future Improvements
1. **Generational collection** - Separate young/old objects
2. **Incremental marking** - Reduce pause times
3. **Weak references** - For caches
4. **Finalizers** - Resource cleanup

## Usage Examples

### From C#
```csharp
// Manual GC
int freed = ObjectMemory.ManualGC();

// Force GC
ObjectMemory.ForceGC();

// Get statistics
var stats = ObjectMemory.GetMemoryStats();

// Add permanent root
ObjectMemory.AddPermanentRoot(systemDictionary);

// Manage transient roots
ObjectMemory.AddTransientRoot(currentContext);
// ... do work ...
ObjectMemory.ClearTransientRoots();
```

### From Smalltalk
```smalltalk
"Run garbage collection"
System garbageCollect.

"Check memory usage"
| stats |
stats := System gcStats.
Transcript show: 'Live objects: ', (stats at: 1) printString; cr.
Transcript show: 'Free slots: ', (stats at: 2) printString; cr.
Transcript show: 'Collections: ', (stats at: 4) printString; cr.

"Force collection"
System forceGC.
```

## Summary

The manual garbage collection system provides:
- **Full Smalltalk-80 semantics** including `become:` and enumeration
- **Controlled memory management** preventing unbounded growth
- **Predictable behavior** with configurable triggers
- **Good performance** for typical Smalltalk programs
- **Debugging support** with statistics and logging

This design balances the requirements of Smalltalk-80 with practical memory management needs, enabling long-running programs while maintaining language compatibility.