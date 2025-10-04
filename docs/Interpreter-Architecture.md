# Smalltalk-80 Interpreter Architecture

**Last Updated**: 2025-10-04
**Status**: Production - Zero Garbage Collection Achieved
**Build**: Clean (0 errors, minimal warnings)

## Executive Summary

This document provides the canonical architectural documentation for the Smalltalk-80 interpreter implementation in C#. The system implements a table-based object memory with proper Blue Book compliance, achieving **zero garbage collection** during cold start bootstrap. The interpreter uses a 4-pass class creation bootstrap followed by 2-phase infrastructure initialization, with all objects properly rooted through a minimal 5-element Special Objects Array.

## Table of Contents

1. [Current System State](#current-system-state)
2. [Object Memory Architecture](#object-memory-architecture)
3. [Special Objects Array (5 GC Roots)](#special-objects-array-5-gc-roots)
4. [Cold Start Bootstrap Process](#cold-start-bootstrap-process)
5. [Garbage Collection](#garbage-collection)
6. [Class Hierarchy Display](#class-hierarchy-display)
7. [Multi-VM Architecture (Future)](#multi-vm-architecture-future)
8. [Key Design Principles](#key-design-principles)

## Current System State

### Achievements

✅ **Zero Garbage Collection**: All GC runs collect 0 objects (3,404 total objects, all properly rooted)
✅ **46 Cold Start Classes**: Complete Blue Book class hierarchy including ArrayedCollection
✅ **Table-Based Object Memory**: ObjectTable with OOP indirection for become: support
✅ **Proper GC Roots**: 5-element Special Objects Array per Blue Book minimal specification
✅ **Hierarchical Class Display**: Tree visualization with proper inheritance relationships
✅ **Clean Architecture**: Per-VM design ready for multi-VM support

### Build Status

```
Classes: 46 (+ 46 metaclasses = 92 total)
Methods: 1,299 compiled methods
Objects: 3,404 total heap objects
GC Performance: 0 objects collected (100% reachable)
Build: 0 errors, minimal warnings
```

### Key Files

- **ObjectMemory.cs** - Table-based object storage with OOP indirection
- **SpecialObjectsArray.cs** - 5-field readonly struct (GC roots)
- **GarbageCollectorNew.cs** - Mark-and-sweep collector
- **ColdStartBootstrap.cs** - 4-pass + 2-phase bootstrap orchestrator
- **SystemDictionary.cs** - Class registry and global lookup
- **VirtualMachine.cs** - VM execution and class hierarchy display

## Object Memory Architecture

### Table-Based Design

The object memory uses a **table-based architecture** with Object-Oriented Pointers (OOPs) that index into an ObjectTable. This provides:

1. **Stable Object Identity**: OOPs remain constant even when objects move
2. **become: Support**: Swap object identities by swapping table entries
3. **Efficient GC**: Mark bits in table, no object header modifications
4. **Blue Book Compliance**: OOP-based object references as specified

### ObjectTable Structure

```csharp
public class ObjectTable
{
    private readonly List<ObjectTableEntry> _entries;
    private readonly Stack<nint> _freeList;
    private nint _nextOOP;

    public nint AllocateOOP()
    {
        if (_freeList.Count > 0)
            return _freeList.Pop();

        var oop = _nextOOP;
        _nextOOP += 4; // OOPs are 4-byte aligned
        _entries.Add(new ObjectTableEntry());
        return oop;
    }

    public void FreeOOP(nint oop)
    {
        var index = oop / 4;
        _entries[index] = new ObjectTableEntry(); // Clear entry
        _freeList.Push(oop);
    }
}

public struct ObjectTableEntry
{
    public nint ClassOOP;              // OOP of object's class
    public ObjectFormat Format;        // Object format code
    public SObjectRef[] InstanceVars;  // Instance variables
    public object? IndexedData;        // byte[], int[], or null
    public bool IsMarked;              // GC mark bit
}
```

### Object Formats

| Format | Name | Structure | Examples |
|--------|------|-----------|----------|
| 0 | FixedPointer | Fixed inst vars only | Point, Association |
| 1 | PointerIndexed | Inst vars + SObjectRef[] | Array, MethodContext |
| 2 | ByteIndexed | Inst vars + byte[] | String, ByteArray, Symbol |
| 3 | WordIndexed | Inst vars + ushort[] | CompiledMethod literals |
| 4 | CompiledMethod | Special encoding | CompiledMethod |

### SObjectRef (Tagged Pointers)

All object references use `SObjectRef`, a tagged pointer type:

```csharp
public readonly struct SObjectRef
{
    private readonly nint value;

    // SmallInteger: LSB = 1, value shifted left 1 bit
    // Object: LSB = 0, value is OOP

    public bool IsSmallInteger => (value & 1) == 1;
    public bool IsObject => (value & 1) == 0;

    public static SObjectRef FromSmallInteger(int v) =>
        new((v << 1) | 1);

    public static SObjectRef FromObjectPointer(nint oop) =>
        new(oop); // LSB already 0 (4-byte aligned)
}
```

## Special Objects Array (5 GC Roots)

### Blue Book Minimal Specification

Per Goldberg & Robson (1983), only **5 roots** are needed for complete reachability:

```csharp
public readonly struct SpecialObjectsArray
{
    public const int Count = 5;

    public readonly SObjectRef Nil;              // Index 0
    public readonly SObjectRef False;            // Index 1
    public readonly SObjectRef True;             // Index 2
    public readonly SObjectRef Smalltalk;        // Index 3 - SystemDictionary
    public readonly SObjectRef ProcessorScheduler; // Index 4

    public SObjectRef ActiveProcess =>
        ProcessorScheduler.GetInstanceVariable(
            ProcessorSchedulerLayout.ActiveProcess);
}
```

### Complete Reachability Graph

**From these 5 roots, ALL objects are reachable:**

1. **nil, false, true** (indices 0-2) - Singletons for bytecode operations

2. **Smalltalk** (index 3) - SystemDictionary instance
   ```
   SystemDictionary
     ├─> Association(#Object -> Object class)
     │    └─> methodDict -> CompiledMethod instances
     ├─> Association(#Symbol -> Symbol class)
     │    └─> classPool -> #USTable -> ALL symbols
     ├─> Association(#Character -> Character class)
     │    └─> classPool -> #CharacterTable -> 256 characters
     └─> ALL other classes and globals
   ```

3. **ProcessorScheduler** (index 4) - Process management
   ```
   ProcessorScheduler
     ├─> activeProcess -> Process
     │    └─> suspendedContext -> MethodContext chain
     └─> quiescentProcessLists -> Array[100]
          └─> LinkedList -> Process chains
   ```

### Why Only 5 Roots?

The Blue Book specifies that ActiveProcess is accessed via `ProcessorScheduler.activeProcess` (instance variable), not stored separately in the SOA. This reduces the SOA to the minimal 5 roots while maintaining complete reachability.

## Cold Start Bootstrap Process

### Overview

The bootstrap creates all 46 classes + 46 metaclasses (92 total class objects) before creating any other instances, using a **4-pass class creation** followed by **2-phase infrastructure initialization**.

### Pass 1: Bootstrap Classes (6 classes)

Create the fundamental bootstrap triangle:

```csharp
var bootstrapClasses = new[] {
    "Object",           // Root of hierarchy
    "UndefinedObject",  // Class of nil
    "Behavior",         // Method lookup
    "ClassDescription", // Abstract class description
    "Class",            // Concrete classes
    "Metaclass"         // Metaclasses
};

foreach (var className in bootstrapClasses)
{
    var metadata = ReadClassMetadata(className);
    var superclassRef = metadata.SuperclassName != "nil"
        ? _createdClasses[metadata.SuperclassName]
        : SObjectRef.Nil; // Temporary for Object

    var classRef = ObjectMemoryNew.CreateClassObject(
        className,
        superclassRef,
        metadata.InstanceVariableNames,
        DetermineObjectFormat(metadata.SubclassKind)
    );

    _createdClasses[className] = classRef;
}
```

### Interlude: Fix Nil Singleton

Resolve the circular dependency:

```csharp
// 1. Create nil singleton (instance of UndefinedObject)
var nilRef = ObjectMemoryNew.CreateInstance(
    _createdClasses["UndefinedObject"].AsObjectPointer(),
    0 // No instance variables
);

// 2. Store in SOA
SystemDictionary.NilObject = nilRef;

// 3. Fix Object.superclass to point to real nil
ObjectMemoryNew.SetInstanceVariable(
    _createdClasses["Object"].AsObjectPointer(),
    ClassLayout.Superclass,
    nilRef
);
```

### Pass 2: Bootstrap Metaclasses (6 metaclasses)

Create metaclasses for bootstrap classes:

```csharp
foreach (var className in bootstrapClasses)
{
    var metaclassName = $"{className} class";
    var totalInstVars = CalculateTotalInstanceVariableCount("Metaclass");

    var metaclassRef = ObjectMemoryNew.CreateInstance(
        _createdClasses["Metaclass"].AsObjectPointer(),
        totalInstVars // All metaclasses have 7 inst vars
    );

    SetClassAttributes(metaclassRef, ...);
    _createdClasses[metaclassName] = metaclassRef;

    WireClassMetaclassRelationship(classRef, metaclassRef);
}
```

### Pass 3: Wire Circular Reference

Complete the bootstrap triangle:

```csharp
// Metaclass.class = Metaclass (circular!)
var metaclassMetaclass = _createdClasses["Metaclass class"];
ObjectMemoryNew.SetInstanceVariable(
    metaclassMetaclass.AsObjectPointer(),
    ClassLayout.Class,
    metaclassMetaclass
);
```

### Pass 4: Remaining Classes (40 classes + 40 metaclasses)

Create remaining classes with their metaclasses:

```csharp
var remainingClasses = allClasses
    .Where(c => !bootstrapClasses.Contains(c) && !c.EndsWith(" class"))
    .ToList();

foreach (var className in DependencyOrder(remainingClasses))
{
    // 1. Create the class
    var classRef = ObjectMemoryNew.CreateClassObject(...);
    _createdClasses[className] = classRef;

    // 2. Create its metaclass immediately
    var metaclassRef = ObjectMemoryNew.CreateInstance(...);
    _createdClasses[$"{className} class"] = metaclassRef;

    // 3. Wire class ↔ metaclass
    WireClassMetaclassRelationship(classRef, metaclassRef);
}
```

### Phase 2: Create Instances and Infrastructure

#### 2.1: Create SystemDictionary

```csharp
var dictionaryClass = _createdClasses["Dictionary"];
SystemDictionary.Instance = ObjectMemoryNew.CreateInstance(
    dictionaryClass.AsObjectPointer(),
    initialCapacity: 100
);

// Register all 46 classes
foreach (var (className, classRef) in _createdClasses)
{
    if (!className.EndsWith(" class"))
    {
        var nameSymbol = ObjectMemoryNew.InternSymbolBootstrap(className);
        ObjectMemoryNew.DictionaryAtPut(
            SystemDictionary.Instance,
            nameSymbol,
            classRef
        );
    }
}
```

#### 2.2: Create Singletons

```csharp
// true, false
SystemDictionary.TrueObject = _createdClasses["True"].CreateInstance();
SystemDictionary.FalseObject = _createdClasses["False"].CreateInstance();

// ProcessorScheduler
SystemDictionary.ProcessorScheduler =
    _createdClasses["ProcessorScheduler"].CreateInstance();
```

#### 2.3: Install Methods

```csharp
ColdStartMethodInstaller.InstallAllMethods();
// Installs 1,299 compiled methods across all classes
```

#### 2.4: Create USTable

```csharp
// Create symbol table in heap
var usTable = ObjectMemoryNew.CreateArray(256);

// Migrate bootstrap symbols to USTable
ObjectMemoryNew.MigrateBootstrapSymbolsToUSTable(usTable);

// Store in Symbol class pool
var symbolClass = _createdClasses["Symbol"];
ObjectMemoryNew.SetClassPool(symbolClass, "USTable", usTable);
```

### Bootstrap Result

**Total Objects Created:**
- 92 class objects (46 classes + 46 metaclasses)
- 3 singletons (nil, true, false)
- 1 SystemDictionary instance
- 1 ProcessorScheduler instance
- 256 USTable buckets
- 1,299 CompiledMethod instances
- ~1,800 supporting objects (symbols, strings, literals, etc.)
- **Total: 3,404 objects, ALL properly rooted**

## Garbage Collection

### Mark-and-Sweep Algorithm

```csharp
public class GarbageCollectorNew
{
    public GCStatistics Collect()
    {
        var marked = new HashSet<nint>();

        // Mark phase - trace from 5 SOA roots
        var soa = _specialObjectsArray;
        MarkObject(soa.Nil.AsObjectPointer(), marked);
        MarkObject(soa.False.AsObjectPointer(), marked);
        MarkObject(soa.True.AsObjectPointer(), marked);
        MarkObject(soa.Smalltalk.AsObjectPointer(), marked);
        MarkObject(soa.ProcessorScheduler.AsObjectPointer(), marked);

        // Also mark active process (6th root)
        var activeProcess = soa.ActiveProcess;
        if (!activeProcess.IsNil)
            MarkObject(activeProcess.AsObjectPointer(), marked);

        // Sweep phase - collect unmarked
        int collected = 0;
        foreach (var oop in _objectTable.GetAllOOPs())
        {
            if (!marked.Contains(oop))
            {
                _objectTable.FreeOOP(oop);
                collected++;
            }
        }

        return new GCStatistics {
            ObjectsCollected = collected,
            ObjectsRemaining = marked.Count
        };
    }

    private void MarkObject(nint oop, HashSet<nint> marked)
    {
        if (marked.Contains(oop)) return;
        marked.Add(oop);

        var data = _objectTable.GetObjectData(oop);

        // Mark instance variables
        foreach (var instVar in data.InstanceVariables)
        {
            if (instVar.IsObject && !instVar.IsNil)
                MarkObject(instVar.AsObjectPointer(), marked);
        }

        // Mark indexed pointers
        if (data.IndexedData is SObjectRef[] indexedRefs)
        {
            foreach (var indexedRef in indexedRefs)
            {
                if (indexedRef.IsObject && !indexedRef.IsNil)
                    MarkObject(indexedRef.AsObjectPointer(), marked);
            }
        }
    }
}
```

### Zero Garbage Achievement

**Critical Requirements:**
1. **Deterministic OOP Allocation**: Same objects get same OOPs every run
2. **No Temporary Objects**: Cold start creates NO throwaway objects
3. **Proper Symbol Pre-Interning**: All symbols used after USTable creation must be pre-interned
4. **Complete Reachability**: Every object must be reachable from one of the 5 SOA roots

**Validation:**
```
[GC] Collection #1: 0 objects collected, 3404 visited
[GC] Collection #2: 0 objects collected, 3404 visited
[GC] Collection #3: 0 objects collected, 3404 visited
```

## Class Hierarchy Display

### Implementation

```csharp
public void PrintClasses()
{
    Console.WriteLine("\nClass Hierarchy:");
    Console.WriteLine("Total Classes: {0}", SystemDictionary.GetAllClassNames().Count());

    PrintClassHierarchy("Object", 0, new HashSet<string>());
}

private void PrintClassHierarchy(string className, int depth,
    HashSet<string> visited, bool isLastSibling = true)
{
    if (visited.Contains(className)) return;
    visited.Add(className);

    // Draw tree structure
    string indent = "";
    if (depth > 0)
    {
        indent = new string(' ', (depth - 1) * 2);
        indent += isLastSibling ? "└─" : "├─";
    }

    Console.WriteLine($"{indent}{className}");

    // Find all direct subclasses
    var subclasses = SystemDictionary.GetAllClassNames()
        .Where(c => {
            var classRef = SystemDictionary.LookupClassRef(c);
            if (!classRef.HasValue || classRef.Value.IsNil) return false;

            var superRef = ObjectMemoryNew.GetClassSuperclass(
                classRef.Value.AsObjectPointer());
            if (superRef.IsNil) return false;

            var superName = ObjectMemoryNew.GetClassName(
                superRef.AsObjectPointer());
            return superName == className;
        })
        .OrderBy(c => c)
        .ToList();

    // Recursively print subclasses
    for (int i = 0; i < subclasses.Count; i++)
    {
        bool isLast = (i == subclasses.Count - 1);
        PrintClassHierarchy(subclasses[i], depth + 1, visited, isLast);
    }
}
```

### Output

```
Class Hierarchy:
Total Classes: 46

Object
├─Behavior
  └─ClassDescription
    ├─Class
    └─Metaclass
├─Boolean
  ├─False
  └─True
├─Collection
  ├─SequenceableCollection
    ├─ArrayedCollection
      ├─Array
      ├─ByteArray
        └─CompiledMethod
      └─String
        └─Symbol
    ├─LinkedList
      └─Semaphore
    └─OrderedCollection
  └─Set
    └─Dictionary
      ├─IdentityDictionary
        └─MethodDictionary
      └─SystemDictionary
...
```

## Multi-VM Architecture (Future)

### Current Static State

**Must become per-VM instance-based:**
1. **ObjectTable** - Object storage (currently static)
2. **SpecialObjectsArray** - GC roots (currently static)
3. **SystemDictionary** - Class registry (instance reference currently static)
4. **GarbageCollector** - Already instance-based ✓

**Can remain static (shared):**
- Generated layouts (ColdStartData, Layouts.cs)
- Bytecode constants
- Blue Book specifications
- Primitive implementations

### Proposed VM Context

```csharp
public class SmalltalkVM
{
    // Per-VM heap and object management
    public ObjectTable ObjectTable { get; }

    // Per-VM GC roots (5-field struct)
    public SpecialObjectsArray SOA { get; private set; }

    // Per-VM execution
    public GarbageCollector GC { get; }
    public Interpreter Interpreter { get; }

    // Per-VM system state
    public SObjectRef SystemDictionaryRef { get; private set; }

    public SmalltalkVM()
    {
        ObjectTable = new ObjectTable();
        GC = new GarbageCollector(ObjectTable);
        Interpreter = new Interpreter(this);

        // Bootstrap creates SOA
        ColdStartBootstrap.Initialize(this);
    }
}
```

### Benefits

✅ **Multiple Independent VMs**: Run isolated Smalltalk environments
✅ **Clean Separation**: Each VM has its own heap and roots
✅ **Thread Safety**: No shared mutable state between VMs
✅ **Testing**: Easy to create/destroy VM instances for tests

## Key Design Principles

### 1. Blue Book Compliance

**Every design decision follows the Smalltalk-80 specification:**
- 5-element Special Objects Array (minimal roots)
- Table-based object memory with OOP indirection
- Tagged pointers for SmallInteger only
- Proper metaclass hierarchy
- Mark-and-sweep garbage collection

### 2. Zero Garbage

**Cold start must create NO temporary objects:**
- Pre-intern all symbols before using them
- Pre-size all dictionaries correctly
- No temporary C# collections during bootstrap
- Deterministic OOP allocation

### 3. Reachability

**Every object must be reachable from SOA roots:**
- Classes → SystemDictionary (root 3)
- Methods → Class.methodDict
- Symbols → Symbol.classPool.#USTable
- Contexts → ProcessorScheduler.activeProcess (root 4)

### 4. Separation of Concerns

**Clear boundaries between subsystems:**
- ObjectMemory: Storage and OOP management
- GarbageCollector: Reachability and collection
- ColdStartBootstrap: Class creation and initialization
- VirtualMachine: Execution and coordination

### 5. Per-VM Design

**All VM state is instance-based (or will be):**
- ObjectTable: Per-VM heap
- SpecialObjectsArray: Per-VM roots
- Interpreter: Per-VM execution state
- SystemDictionary: Reference in heap (per-VM)

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book)
2. Ingalls, D. (1978). *The Smalltalk-76 Programming System Design and Implementation*
3. Microsoft. (2024). *C# 13 Language Specification*

---

**Document Status**: Production
**Last Verified**: 2025-10-04
**Zero Garbage**: ✅ Achieved
**Multi-VM Ready**: 🚧 In Progress
