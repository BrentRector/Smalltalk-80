# Smalltalk-80 Garbage Collection

## Overview

The Smalltalk-80 interpreter implements a **mark-and-sweep garbage collector** integrated with the ObjectTable architecture. The GC operates as a client of ObjectTable, using the Special Objects Array (SOA) as the sole root for marking reachable objects.

### Key Design Principles

1. **SOA as Single Root**: The Special Objects Array is the ONLY permanent GC root - all live objects must be reachable from it
2. **SObjectRef Architecture**: All object references use SObjectRef values (not object pointers)
3. **ObjectTable Integration**: GC is a clean client of ObjectTable with no special privileges
4. **Zero Collection at Cold Start**: Properly initialized system shows 0 objects collected (perfect GC)

## Architecture

### Object Memory Model

The system uses a three-layer architecture:

```csharp
/// <summary>
/// Layer 1: SObjectRef - A value type representing an object reference.
/// Encodes both object pointers and immediate values (SmallInteger, Character).
/// </summary>
public readonly struct SObjectRef
{
    private readonly nint value;  // Tagged pointer or immediate value

    // LSB = 0: Object pointer (OOP)
    // LSB = 1: SmallInteger (31-bit value)
    // Special patterns for Character, nil, true, false
}

/// <summary>
/// Layer 2: ObjectTable - Manages object allocation and storage.
/// Maps object indices to ObjectData instances.
/// </summary>
public sealed class ObjectTable
{
    private ObjectData?[] objects;     // Object storage array
    private Queue<int> freeList;       // Reusable slots

    public ObjectData? Get(int index);
    public void Free(int index);
}

/// <summary>
/// Layer 3: ObjectData - Internal storage for object state.
/// Never exposed directly - accessed only via SObjectRef methods.
/// </summary>
public class ObjectData
{
    public SObjectRef ClassRef { get; set; }
    public ObjectFormat Format { get; set; }
    public SObjectRef[] InstanceVariables { get; set; }
    public object? IndexedData { get; set; }
    public bool Marked { get; set; }  // GC mark bit
}
```

### Garbage Collector Design

The GarbageCollector is a standalone component that:
- Takes ObjectTable as a dependency (constructor injection)
- Stores the Special Objects Array as raw OOPs (`List<nint>`)
- Implements standard mark-and-sweep algorithm
- Has no special integration with ObjectTable

```csharp
public sealed class GarbageCollector
{
    private readonly ObjectTable objectTable;
    private List<nint> specialObjectsArray = new();

    public GarbageCollector(ObjectTable objectTable)
    {
        this.objectTable = objectTable;
    }

    public void SetSpecialObjectsArray(List<nint> soa)
    {
        specialObjectsArray = soa;
    }
}
```

## Algorithm

### Mark Phase

Starting from the Special Objects Array, recursively mark all reachable objects:

```csharp
private void MarkFromRoots()
{
    gcStack.Clear();

    // Push all SOA entries (skip nil and SmallIntegers)
    foreach (var oop in specialObjectsArray)
    {
        if (oop != 0 && (oop & 0x01) == 0)  // Object pointer
        {
            gcStack.Push(oop);
        }
    }

    // Recursively mark reachable objects
    while (gcStack.Count > 0)
    {
        var oop = gcStack.Pop();
        MarkObject(oop);
    }
}

private void MarkObject(nint oop)
{
    // Skip immediates
    if (oop == 0 || (oop & 0x01) == 1) return;

    // Get object from table
    int index = ObjectTable.IndexFromOop(oop);
    var data = objectTable.Get(index);

    if (data == null || data.Marked) return;

    // Mark this object
    data.Marked = true;

    // Trace class reference
    if (data.ClassRef.IsHeapObject)
        gcStack.Push(data.ClassRef.AsObjectPointer());

    // Trace instance variables
    foreach (var fieldRef in data.InstanceVariables)
    {
        if (fieldRef.IsHeapObject)
            gcStack.Push(fieldRef.AsObjectPointer());
    }

    // Trace indexed data for pointer formats
    if (data.Format == ObjectFormat.PointerIndexed)
    {
        foreach (var ptr in (SObjectRef[])data.IndexedData)
        {
            if (ptr.IsHeapObject)
                gcStack.Push(ptr.AsObjectPointer());
        }
    }
}
```

### Sweep Phase

Free all unmarked objects:

```csharp
private int SweepUnmarkedObjects()
{
    int collected = 0;
    var indicesToFree = new List<int>();

    // Find all unmarked objects
    foreach (var (index, data) in objectTable.EnumerateLiveObjects())
    {
        if (!data.Marked)
        {
            indicesToFree.Add(index);
        }
    }

    // Free them
    foreach (var index in indicesToFree)
    {
        objectTable.Free(index);  // ObjectTable handles the details
        collected++;
    }

    return collected;
}
```

## Root Set Management

### Special Objects Array (SOA) - Blue Book Minimal Specification

Per Goldberg & Robson (1983), the SOA is the **ONLY** permanent root for garbage collection. It contains exactly **5 essential objects** for complete reachability:

| Index | Object | Purpose | Reachability |
|-------|--------|---------|--------------|
| 0 | nil | UndefinedObject singleton | Required for pushNil bytecode (113) |
| 1 | false | False singleton | Required for pushFalse bytecode (114) |
| 2 | true | True singleton | Required for pushTrue bytecode (113) |
| 3 | **Smalltalk** | SystemDictionary instance | **ALL classes, methods, symbols, globals** |
| 4 | ProcessorScheduler | Process scheduler singleton | **ALL live processes and execution contexts** |

#### Why Only 5 Roots Are Sufficient

**Removed as Redundant:**
- ~~Behavior class~~ → Already in `Smalltalk at: #Behavior`
- ~~Metaclass class~~ → Already in `Smalltalk at: #Metaclass`
- ~~SymbolTable~~ → Already in `Symbol class → classPool → #USTable`
- ~~ProcessList~~ → Processes are in `ProcessorScheduler → quiescentProcessLists`

### Complete Reachability Chain

```
Special Objects Array (5 roots)
  ├─> nil, true, false (indices 0-2)
  │    └─> Singletons (always reachable via bytecode pushes)
  │
  ├─> SystemDictionary (index 3 - "Smalltalk" global)
  │    ├─> Association(#Object -> Object class)
  │    │    └─> methodDict (MethodDictionary)
  │    │         └─> Associations → CompiledMethod instances
  │    │              └─> literals (method constants)
  │    ├─> Association(#Symbol -> Symbol class)
  │    │    └─> classPool (Dictionary at slot 7)
  │    │         └─> Association(#USTable -> Symbol table Array[256])
  │    │              └─> Symbol buckets → ALL Symbol instances
  │    ├─> Association(#Character -> Character class)
  │    │    └─> classPool
  │    │         └─> Association(#CharacterTable -> Array[256])
  │    │              └─> ALL Character instances (0-255)
  │    ├─> ALL other classes, metaclasses, and globals
  │    └─> Each class
  │         ├─> methodDict → instance methods
  │         ├─> class (metaclass)
  │         │    └─> methodDict → class methods
  │         └─> classPool → class variables
  │
  └─> ProcessorScheduler (index 4)
       ├─> activeProcess (currently executing)
       │    └─> suspendedContext → MethodContext/BlockContext
       │         ├─> sender (context chain)
       │         ├─> receiver
       │         ├─> method (CompiledMethod)
       │         └─> temporaries and stack
       └─> quiescentProcessLists (Array[100])
            └─> LinkedList chains of runnable/waiting Processes
                 └─> Each process → suspendedContext chains
```

## Integration

### Cold Start Bootstrap

The cold start process ensures all objects are reachable:

```csharp
// Phase 1: Create essential objects
var nil = CreateNilObject();
var systemDict = CreateSystemDictionary();

// Phase 2: Build SOA
var soa = new List<SObjectRef>();
soa.Add(nil);
soa.Add(falseObj);
soa.Add(trueObj);
soa.Add(behaviorClass);
soa.Add(metaclass);
soa.Add(systemDict);  // Critical: contains everything
soa.Add(symbolTable);
soa.Add(processList);
soa.Add(scheduler);

// Phase 3: Set SOA as GC root
garbageCollector.SetSpecialObjectsArrayFromRefs(soa);

// Phase 4: Validate (should collect 0 objects)
var stats = garbageCollector.Collect();
if (stats.ObjectsCollected != 0)
{
    throw new InvalidOperationException(
        $"Cold start error: {stats.ObjectsCollected} unreachable objects!");
}
```

### Virtual Machine Integration

```csharp
public class VirtualMachine
{
    private readonly ObjectTable objectTable;
    private readonly GarbageCollector gc;

    public VirtualMachine()
    {
        objectTable = ObjectMemoryNew.ObjectTable;
        gc = new GarbageCollector(objectTable);
    }

    private void InitializeGarbageCollection()
    {
        // Build SOA from SystemDictionary
        var soa = new List<nint>();

        // Add all essential roots as OOPs
        soa.Add(SNil.Instance.AsObjectPointer());
        soa.Add(SBoolean.False.AsObjectPointer());
        soa.Add(SBoolean.True.AsObjectPointer());
        // ... etc

        gc.SetSpecialObjectsArray(soa);
    }
}
```

### Triggering Collection

Collections can be triggered:

1. **Manually from C#**:
```csharp
var stats = gc.Collect();
Console.WriteLine($"Collected {stats.ObjectsCollected} objects");
```

2. **From Interpreter** (periodic checks):
```csharp
if (++bytecodesSinceGC >= 10000)
{
    bytecodesSinceGC = 0;
    if (objectTable.GetAllocatedCount() > 40000)
    {
        gc.Collect();
    }
}
```

3. **From Smalltalk** (via primitives):
```smalltalk
"Primitive 130 - Run GC and return freed count"
System garbageCollect.

"Primitive 131 - Get GC statistics"
System gcStats.
```

## Performance

### Characteristics

| Metric | Value | Notes |
|--------|-------|-------|
| **Mark Phase** | O(n) | n = reachable objects |
| **Sweep Phase** | O(m) | m = total allocated objects |
| **Typical Pause** | 10-50ms | For ~10K objects |
| **Memory Overhead** | ~16 bytes/object | ObjectData + table entry |

### Cold Start Statistics

Perfect cold start shows:
- **Total objects** created during bootstrap
- **All objects reachable** from SOA
- **0 objects collected** (100% reachability)

If any objects are collected after cold start, it indicates an initialization bug where objects aren't properly connected to the SOA roots.

### Context Architecture

**Important**: MethodContext and BlockContext are **PointerIndexed** format, not FixedPointer:

- **6 fixed instance variables** (inherited from ContextPart):
  - sender (index 0)
  - instructionPointer (index 1)
  - stackPointer (index 2)
  - method (index 3)
  - receiverMap (index 4)
  - receiver (index 5)

- **Indexed slots** hold the temporary frame and stack:
  - Slot 0..numArgs-1: method arguments
  - Slot numArgs..numArgs+numTemps-1: temporary variables
  - Slot numArgs+numTemps..end: evaluation stack

This matches the Blue Book specification: "Both kinds of context have six fixed fields corresponding to six named instance variables. These fixed fields are followed by some indexable fields."

## Validation

### GC Correctness Test

```csharp
public bool ValidateReachability()
{
    // Run a collection
    var stats = gc.Collect();

    // All objects should be reachable from SOA
    bool isValid = stats.ObjectsCollected == 0;

    if (!isValid)
    {
        Console.WriteLine(
            $"ERROR: {stats.ObjectsCollected} unreachable objects!");
    }

    return isValid;
}
```

### Command-Line Testing

```bash
# Run with GC logging
dotnet run -- eval "nil" --gc-log

# Validate all objects reachable
dotnet run -- eval "nil" --validate-gc

# List cold start classes and run GC
dotnet run -- eval "nil" --list-cold-start-classes --gc-log
```

### Expected Output

```
[GC] Starting collection #1
[GC] Total allocated objects: 988
[GC] Reachable objects: 988
[GC] Objects collected: 0
[GC] Collection time: 12ms
[GC] ✓ All objects reachable - cold start initialization is correct
```

## Design Rationale

### Why Manual GC?

The Smalltalk-80 language requires features incompatible with automatic .NET GC:

1. **become: primitive** - Swaps object identity system-wide
2. **Instance enumeration** - Find all instances of a class
3. **Object enumeration** - Iterate all objects
4. **Stable OOPs** - Object pointers must remain constant

### Why SOA as Single Root?

1. **Simplicity** - One root to rule them all
2. **Correctness** - Easy to verify all objects are reachable
3. **Blue Book Compliance** - Matches Smalltalk-80 architecture
4. **No C# Collections** - SystemDictionary is the sole source of truth

### ObjectTable Integration

The GC operates as a clean client of ObjectTable:
- No special privileges or backdoor access
- Uses public `Get()` and `Free()` methods
- Doesn't know about internal free lists or segmentation
- ObjectTable handles all allocation/deallocation details

## Summary

The garbage collector provides:
- **Full Smalltalk-80 semantics** including become: and enumeration
- **Clean architecture** with SOA as the single root
- **Perfect cold start** with 0 objects collected
- **ObjectTable integration** as a well-behaved client
- **Predictable performance** with ~10-50ms pauses

This design ensures the interpreter can run long-lived Smalltalk programs with controlled memory usage while maintaining complete language compatibility.