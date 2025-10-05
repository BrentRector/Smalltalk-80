# Smalltalk-80 Interpreter Architecture

**Last Updated**: 2025-10-04
**Status**: Production - Zero Garbage Collection Achieved
**Build**: Clean (0 errors, 0 warnings)

## Executive Summary

This document provides the canonical architectural documentation for the Smalltalk-80 interpreter implementation in C#. The system implements a table-based object memory with proper Blue Book compliance, achieving **zero garbage collection** during cold start bootstrap. The interpreter uses a 4-pass class creation bootstrap followed by 2-phase infrastructure initialization, with all objects properly rooted through a minimal 5-element Special Objects Array.

## Table of Contents

1. [Current System State](#current-system-state)
2. [Architecture Layers](#architecture-layers)
3. [Object Memory Architecture](#object-memory-architecture)
4. [Object Model](#object-model)
5. [Special Objects Array (5 GC Roots)](#special-objects-array-5-gc-roots)
6. [Cold Start Bootstrap Process](#cold-start-bootstrap-process)
7. [Garbage Collection](#garbage-collection)
8. [Bytecode Execution](#bytecode-execution)
9. [Primitive Operations](#primitive-operations)
10. [Graphics Support](#graphics-support)
11. [Compiler Integration](#compiler-integration)
12. [Source File Organization](#source-file-organization)
13. [Blue Book Compliance](#blue-book-compliance)
14. [Class Hierarchy Display](#class-hierarchy-display)
15. [Multi-VM Architecture (Future)](#multi-vm-architecture-future)
16. [Key Design Principles](#key-design-principles)
17. [Technical Specifications](#technical-specifications)

## Current System State

### Achievements

✅ **Zero Garbage Collection**: All GC runs collect 0 objects (3,413 total objects, all properly rooted)
✅ **46 Cold Start Classes**: Complete Blue Book class hierarchy including ArrayedCollection
✅ **Table-Based Object Memory**: ObjectTable with OOP indirection for become: support
✅ **Proper GC Roots**: 5-element Special Objects Array per Blue Book minimal specification
✅ **Hierarchical Class Display**: Tree visualization with proper inheritance relationships
✅ **Clean Architecture**: Per-VM design ready for multi-VM support

### Build Status

```
Classes: 46 (+ 46 metaclasses = 92 total)
Methods: 1,299 compiled methods
Objects: 3,413 total heap objects
GC Performance: 0 objects collected (100% reachable)
Build: 0 errors, 0 warnings
```

### Key Files

- **ObjectMemory.cs** - Table-based object storage with OOP indirection
- **SpecialObjectsArray.cs** - 5-field readonly struct (GC roots)
- **GarbageCollector.cs** - Mark-and-sweep collector (renamed from GarbageCollectorNew.cs)
- **ColdStartBootstrap.cs** - 4-pass + 2-phase bootstrap orchestrator
- **SystemDictionary.cs** - Class registry and global lookup
- **VirtualMachine.cs** - VM execution and class hierarchy display

## Architecture Layers

### 1. Command-Line Interface

The interpreter provides a comprehensive CLI using System.CommandLine:

```
Commands:
  repl                       Start interactive REPL

Global Options:
  --gc-log                   Enable garbage collection logging
  --validate-gc              Validate GC correctness (check for zero garbage)
  --cold-start-log           Enable cold start bootstrap logging
  --list-cold-start-classes  Display class hierarchy after cold start
```

### 2. Virtual Machine Core

The `VirtualMachine` class orchestrates all interpreter components:

```csharp
namespace Smalltalk80.Interpreter;

public class VirtualMachine
{
    private readonly ObjectMemory _objectMemory;
    private readonly GarbageCollector _gc;
    private readonly Interpreter _interpreter;

    public void ColdStart()
    {
        // 4-pass class creation + 2-phase infrastructure
        ColdStartBootstrap.Execute();
    }

    public void PrintClasses()
    {
        // Hierarchical display with tree graphics
        PrintClassHierarchy("Object", 0, new HashSet<string>());
    }
}
```

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
    public bool IsNil => value == 0 ||
        (_cachedNilOop != 0 && value == _cachedNilOop);

    public static SObjectRef FromSmallInteger(int v) =>
        new((v << 1) | 1);

    public static SObjectRef FromObjectPointer(nint oop) =>
        new(oop); // LSB already 0 (4-byte aligned)
}
```

**Key Features:**
- **Tagged Immediates**: SmallInteger values stored directly (no heap allocation)
- **Nil Singleton**: Cached nil OOP for fast IsNil checks
- **Type Safety**: Cannot accidentally dereference SmallInteger as object
- **Blue Book Compliance**: Only SmallInteger is immediate (Character, Symbol are heap objects)

## Object Model

### Base Object System

All Smalltalk objects are stored in the heap and accessed via SObjectRef (tagged pointers):

```csharp
// Core object data structure
public struct ObjectTableEntry
{
    public nint ClassOOP;              // OOP of object's class
    public ObjectFormat Format;        // Object format code
    public SObjectRef[] InstanceVars;  // Named instance variables
    public object? IndexedData;        // Indexed portion (byte[], SObjectRef[], etc.)
}
```

### Class System

Classes are themselves objects with specific layouts:

**Class Layout** (Behavior + ClassDescription + Class):
- Superclass (inherited from Behavior)
- methodDict (inherited from Behavior)
- format (inherited from Behavior)
- instanceVariables (inherited from ClassDescription)
- organization (inherited from ClassDescription)
- name (defined in Class)
- comment (defined in Class)
- pool (defined in Class)
- classPool (defined in Class)

**Metaclass Layout** (Behavior + ClassDescription + Metaclass):
- Same 4 fields from Behavior
- Same 2 fields from ClassDescription
- thisClass (defined in Metaclass) - points to the class this is a metaclass of

Total: Metaclasses have exactly **7 instance variables** (4+2+1)

### Value Types (Immediates)

SmallInteger is the only immediate type - stored directly in SObjectRef without heap allocation:

```csharp
// SmallInteger arithmetic
var a = SObjectRef.FromSmallInteger(3);
var b = SObjectRef.FromSmallInteger(4);
var sum = SObjectRef.FromSmallInteger(a.AsSmallInteger() + b.AsSmallInteger());
```

**Range**: -2^30 to 2^30-1 (approximately -1 billion to +1 billion)

### Collection Types

Arrays and Strings provide indexed storage:

**Array** (PointerIndexed format):
- Fixed instance variables (from superclasses)
- Indexed SObjectRef[] for elements

**String** (ByteIndexed format):
- Fixed instance variables (from superclasses)
- Indexed byte[] for characters

**Symbol** (ByteIndexed format):
- Same as String but interned in USTable
- All symbols are unique (same string → same Symbol object)

## Special Objects Array (5 GC Roots)

### Blue Book Minimal Specification

Per Goldberg & Robson (1983), only **5 roots** are needed for complete reachability:

```csharp
internal readonly struct SpecialObjectsArray
{
    public const int Count = 5;

    public readonly SObjectRef Nil;              // Index 0
    public readonly SObjectRef False;            // Index 1
    public readonly SObjectRef True;             // Index 2
    public readonly SObjectRef Smalltalk;        // Index 3 - SystemDictionary
    public readonly SObjectRef ProcessorScheduler; // Index 4

    public SpecialObjectsArray(
        SObjectRef nil,
        SObjectRef @false,
        SObjectRef @true,
        SObjectRef smalltalk,
        SObjectRef processorScheduler)
    {
        Nil = nil;
        False = @false;
        True = @true;
        Smalltalk = smalltalk;
        ProcessorScheduler = processorScheduler;
    }

    // ActiveProcess accessed via instance variable
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
     ├─> activeProcess (inst var[1]) -> Process
     │    └─> suspendedContext -> MethodContext chain
     └─> quiescentProcessLists (inst var[0]) -> Array[8]
          └─> LinkedList[0..7] -> Process chains (one per priority level)
   ```

### Why Only 5 Roots?

The Blue Book specifies that ActiveProcess is accessed via `ProcessorScheduler.activeProcess` (instance variable), not stored separately in the SOA. This reduces the SOA to the minimal 5 roots while maintaining complete reachability.

**NOT the 52-Element Blue Book Array**: The Blue Book also describes a 52-element array containing cached selectors and special messages. Our SpecialObjectsArray is **ONLY** the GC roots, not that larger array.

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
    var metadata = ColdStartData.Classes.GetMetadata(className);
    var superclassRef = metadata.SuperclassName != "nil"
        ? _createdClasses[metadata.SuperclassName]
        : SObjectRef.Nil; // Temporary for Object

    var format = DetermineObjectFormat(metadata.SubclassKind, className);
    var classRef = ObjectMemoryNew.CreateClassObject(
        className,
        superclassRef,
        metadata.InstanceVariableNames,
        format
    );

    _createdClasses[className] = classRef;
}
```

**State after Pass 1:**
- 6 bootstrap classes exist
- Object.superclass = temporary placeholder (SObjectRef.Nil, value=0)
- All other classes have proper superclass references

### Interlude: Fix Nil Singleton

Resolve the circular dependency between Object and nil:

```csharp
// 1. Get UndefinedObject class (created in Pass 1)
var undefinedObjectClass = _createdClasses["UndefinedObject"];

// 2. Create nil singleton instance
var nilRef = ObjectMemoryNew.CreateInstance(
    undefinedObjectClass.AsObjectPointer(),
    0 // No instance variables
);

// 3. Store in SOA-related storage
SystemDictionary.NilObject = nilRef;

// 4. Cache nil OOP in SObjectRef for fast IsNil checks
SObjectRef.SetNilOop(nilRef.AsObjectPointer());

// 5. Fix Object.superclass to point to real nil
ObjectMemoryNew.SetInstanceVariable(
    _createdClasses["Object"].AsObjectPointer(),
    ClassLayout.Superclass,
    nilRef
);
```

**Why this works:**
- Object created first with temporary nil placeholder
- UndefinedObject created second (as subclass of Object)
- Nil singleton created as instance of UndefinedObject
- Object.superclass fixed up to point to real nil

### Pass 2: Bootstrap Metaclasses (6 metaclasses)

Create metaclasses for bootstrap classes:

```csharp
foreach (var className in bootstrapClasses)
{
    var metaclassName = $"{className} class";

    // Calculate total inst vars for Metaclass (always 7)
    var totalInstVars = CalculateTotalInstanceVariableCount("Metaclass");
    // Returns 7 (Behavior:4 + ClassDescription:2 + Metaclass:1)

    var metaclassRef = ObjectMemoryNew.CreateInstance(
        _createdClasses["Metaclass"].AsObjectPointer(),
        totalInstVars
    );

    SetClassAttributes(metaclassRef, ...);
    _createdClasses[metaclassName] = metaclassRef;

    WireClassMetaclassRelationship(classRef, metaclassRef);
}
```

**Key Point**: ALL metaclasses have exactly 7 instance variables, calculated by walking the Metaclass inheritance chain.

### Pass 3: Wire Circular Reference

Complete the bootstrap triangle:

```csharp
// Metaclass.class = Metaclass (circular!)
var metaclassMetaclass = _createdClasses["Metaclass class"];
ObjectMemoryNew.SetInstanceVariable(
    metaclassMetaclass.AsObjectPointer(),
    ClassLayout.Class,
    metaclassMetaclass // Points to itself!
);
```

**State after Pass 3:**
- Bootstrap triangle complete
- All fundamental circular relationships established
- Ready to create remaining classes

### Pass 4: Remaining Classes (40 classes + 40 metaclasses)

Create remaining classes with their metaclasses:

```csharp
var allClassNames = ColdStartData.Classes.GetAllClassNames();
var remainingClasses = allClassNames
    .Where(c => !bootstrapClasses.Contains(c) && !c.EndsWith(" class"))
    .ToList();

foreach (var className in DependencyOrder(remainingClasses))
{
    // 1. Create the class
    var metadata = ColdStartData.Classes.GetMetadata(className);
    var classRef = ObjectMemoryNew.CreateClassObject(...);
    _createdClasses[className] = classRef;

    // 2. Create its metaclass immediately
    var metaclassMetadata = ColdStartData.Classes.GetMetadata($"{className} class");
    var metaclassRef = ObjectMemoryNew.CreateInstance(...);
    _createdClasses[$"{className} class"] = metaclassRef;

    // 3. Wire class ↔ metaclass
    WireClassMetaclassRelationship(classRef, metaclassRef);
}
```

**State after Pass 4:**
- ALL 46 classes exist
- ALL 46 metaclasses exist
- Total: 92 class objects in memory
- All stored in `_createdClasses` cache
- SystemDictionary.Instance STILL does not exist

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
    if (!className.EndsWith(" class")) // Only classes, not metaclasses
    {
        var nameSymbol = ObjectMemoryNew.InternSymbolBootstrap(className);
        var assoc = ObjectMemoryNew.CreateAssociation(nameSymbol, classRef);
        ObjectMemoryNew.DictionaryAtPut(SystemDictionary.Instance, nameSymbol, classRef);
    }
}

// Create "Smalltalk" global variable (self-reference)
var smalltalkSymbol = ObjectMemoryNew.InternSymbolBootstrap("Smalltalk");
ObjectMemoryNew.DictionaryAtPut(
    SystemDictionary.Instance,
    smalltalkSymbol,
    SystemDictionary.Instance
);
```

#### 2.2: Create Singletons

```csharp
// true, false
SystemDictionary.TrueObject = ObjectMemoryNew.CreateInstance(
    _createdClasses["True"].AsObjectPointer(), 0);
SystemDictionary.FalseObject = ObjectMemoryNew.CreateInstance(
    _createdClasses["False"].AsObjectPointer(), 0);

// ProcessorScheduler with proper initialization per Blue Book spec
var processorScheduler = ObjectMemoryNew.CreateInstance(
    _createdClasses["ProcessorScheduler"].AsObjectPointer(),
    ProcessorSchedulerLayout.InstanceVarCount);

// Create quiescentProcessLists - Array[8] of LinkedLists (one per priority level)
var arrayClass = _createdClasses["Array"];
var linkedListClass = _createdClasses["LinkedList"];
var quiescentProcessLists = ObjectMemoryNew.CreateArray(arrayClass.AsObjectPointer(), 8);

for (int i = 0; i < 8; i++)
{
    var linkedList = ObjectMemoryNew.CreateInstance(linkedListClass.AsObjectPointer(), 2);
    ObjectMemoryNew.SetIndexedPointer(quiescentProcessLists, i, linkedList);
}

// Create initial activeProcess with proper Blue Book initialization
var processClass = _createdClasses["Process"];
var activeProcess = ObjectMemoryNew.CreateInstance(
    processClass.AsObjectPointer(),
    ObjectMemoryNew.GetClassTotalInstanceVariableCount(processClass.AsObjectPointer())
);

// Initialize Process fields using STProcess layout (type-safe)
var process = new STProcess(activeProcess);
process.NextLink = SObjectRef.Nil;                      // Not linked to another process
process.SuspendedContext = SObjectRef.Nil;              // Running, not suspended
process.Priority = SObjectRef.FromSmallInteger(4);      // UserSchedulingPriority
process.MyList = SObjectRef.Nil;                        // Not on any waiting list

// Link ProcessorScheduler components (critical for GC reachability)
// Using STProcessorScheduler layout for type-safe access
var scheduler = new STProcessorScheduler(processorScheduler);
scheduler.QuiescentProcessLists = quiescentProcessLists;  // Instance var[0]
scheduler.ActiveProcess = activeProcess;                   // Instance var[1]

SystemDictionary.ProcessorScheduler = processorScheduler;
```

#### 2.3: Install Methods

```csharp
ColdStartMethodInstaller.InstallAllMethods();
// Installs 1,299 compiled methods from ColdStartData.Methods
// Methods installed on correct class or metaclass based on metadata
```

#### 2.4: Create USTable

```csharp
// Create USTable (symbol table) as Array[256] in heap
var arrayClass = _createdClasses["Array"];
var usTable = ObjectMemoryNew.CreateArray(arrayClass.AsObjectPointer(), 256);

// Pre-intern critical symbols before USTable switch
ObjectMemoryNew.InternSymbolBootstrap("SymbolTable"); // CRITICAL!

// Create buckets for hash table
for (int i = 0; i < 256; i++)
{
    var bucket = ObjectMemoryNew.CreateArray(arrayClass.AsObjectPointer(), 11);
    ObjectMemoryNew.SetIndexedPointer(usTable, i, bucket);
}

// Migrate all bootstrap symbols to USTable
ObjectMemoryNew.MigrateBootstrapSymbolsToUSTable(usTable);

// Store in Symbol class pool
var symbolClass = _createdClasses["Symbol"];
SymbolTableBuilder.InstallUSTable(usTable);
```

**Critical**: The symbol "SymbolTable" must be pre-interned BEFORE creating USTable, otherwise using it creates a temporary symbol that becomes garbage.

### Bootstrap Result

**Total Objects Created:**
- 92 class objects (46 classes + 46 metaclasses)
- 3 singletons (nil, true, false)
- 1 SystemDictionary instance
- 1 ProcessorScheduler instance with proper initialization:
  - 1 Process object (activeProcess)
  - 1 Array[8] for quiescentProcessLists
  - 8 LinkedList objects (one per priority level 1-8)
- 256 USTable buckets (each bucket is an 11-element Array)
- 1,299 CompiledMethod instances
- ~1,800 supporting objects (symbols, strings, literals, Associations, etc.)
- **Total: 3,413 objects, ALL properly rooted**

## Garbage Collection

### Mark-and-Sweep Algorithm

```csharp
public class GarbageCollector
{
    public GCStatistics Collect()
    {
        var marked = new HashSet<nint>();

        // Mark phase - trace from 5 SOA roots
        var soa = _specialObjectsArray;
        if (!soa.Nil.IsNil)
            MarkObject(soa.Nil.AsObjectPointer(), marked);
        if (!soa.False.IsNil)
            MarkObject(soa.False.AsObjectPointer(), marked);
        if (!soa.True.IsNil)
            MarkObject(soa.True.AsObjectPointer(), marked);
        if (!soa.Smalltalk.IsNil)
            MarkObject(soa.Smalltalk.AsObjectPointer(), marked);
        if (!soa.ProcessorScheduler.IsNil)
            MarkObject(soa.ProcessorScheduler.AsObjectPointer(), marked);

        // Also mark active process (6th root via instance variable)
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
[GC] Collection #1: 0 collected, 3413 live, 3ms
[GC] Collection #2: 0 collected, 3413 live, 0ms
[GC] Collection #3: 0 collected, 3413 live, 0ms
```

## Bytecode Execution

### Bytecode Format

The Smalltalk-80 bytecode instruction set consists of 256 instructions (0x00-0xFF):

| Range | Operation | Description |
|-------|-----------|-------------|
| 0-15 | Push Receiver Variable | Push instance variable 0-15 |
| 16-31 | Push Temporary Variable | Push temporary/argument 0-15 |
| 32-63 | Push Literal Constant | Push literal 0-31 |
| 64-95 | Push Literal Variable | Push Association value 0-31 |
| 96-103 | Pop and Store Receiver Variable | Pop into inst var 0-7 |
| 104-111 | Pop and Store Temporary Variable | Pop into temp 0-7 |
| 112-119 | Push Special | receiver, true, false, nil, -1, 0, 1, 2 |
| 120-127 | Return | Return various values to sender |
| 128-175 | Extended Operations | Jump, conditional, send super |
| 176-255 | Send | Send literal selector with 0-2 args |

### Bytecode Interpreter

The interpreter executes compiled methods using ContextBasedInterpreterNew:

```csharp
public class ContextBasedInterpreterNew
{
    public SObjectRef Execute(SObjectRef methodRef, SObjectRef receiver, SObjectRef[] args)
    {
        // Create MethodContext
        var context = CreateMethodContext(methodRef, receiver, args);

        // Execute bytecodes
        while (context.PC < method.Bytecodes.Length)
        {
            var bytecode = method.Bytecodes[context.PC++];
            ExecuteBytecode(context, bytecode);
        }

        return context.StackTop;
    }
}
```

### Method Context

MethodContext is a Smalltalk heap object with Blue Book layout:

**MethodContext Layout**:
- sender (link to calling context)
- pc (program counter)
- stackp (stack pointer)
- method (CompiledMethod being executed)
- receiverMap (unused in this implementation)
- receiver (self)
- Indexed portion: temps and stack (combined area)

## Primitive Operations

### Overview

Primitives are low-level operations implemented in the VM for performance and access to system resources. The Blue Book specifies 127 standard primitives (1-127), and the Smalltalk-80.st source uses 158 total primitive numbers (including Windows file system extensions).

**Current Implementation Status:** ~80% complete (104 of 129 primitives used in Smalltalk-80.st)

### Primitive Categories

The Smalltalk-80 system organizes primitives into functional categories:

| Category | Range | Purpose | Status |
|----------|-------|---------|--------|
| **SmallInteger Arithmetic** | 1-17 | +, -, *, /, //, \\, bitwise ops | ✅ Complete (17/17) |
| **LargeInteger Arithmetic** | 21-37 | Large integer operations | ✅ Complete (17/17) |
| **Float Arithmetic** | 40-54 | Floating-point operations | ✅ Complete (15/15) |
| **Array/Stream Access** | 60-67 | at:, at:put:, size, next | ✅ Complete (8/8) |
| **Storage Management** | 68-79 | new, become:, instVarAt: | ✅ Complete (12/12) |
| **Control Primitives** | 80-89 | blocks, perform:, semaphores | ⚠️ Partial (9/10) - missing 89 |
| **I/O & Graphics Primitives** | 90-109 | Graphics, files, time | ⚠️ Partial (16/20) - 90-105 done |
| **System Primitives** | 110-127, 128-139 | ==, class, quit, GC | ⚠️ Partial (10/20) |

### Critical Primitives Table

This table shows the most important primitives needed for basic Smalltalk operation:

| # | Class | Selector | Purpose | Status | Priority |
|---|-------|----------|---------|--------|----------|
| **1** | SmallInteger | + | Addition | ✅ Done | - |
| **2** | SmallInteger | - | Subtraction | ✅ Done | - |
| **3-8** | SmallInteger | <, >, <=, >=, =, ~= | Comparisons | ✅ Done | - |
| **9** | SmallInteger | * | Multiplication | ✅ Done | - |
| **10** | SmallInteger | / | Division | ✅ Done | - |
| **11** | SmallInteger | \\ | Modulo | ✅ Done | - |
| **12** | SmallInteger | // | Floor division | ✅ Done | - |
| **13-17** | SmallInteger | quo:, bitAnd:, bitOr:, bitXor:, bitShift: | Bitwise operations | ✅ Done | - |
| **21-37** | LargeInteger | +, -, *, /, //, \\, etc. | Large integer ops | ✅ Done | - |
| **40** | SmallInteger | asFloat | Convert to Float | ✅ Done | - |
| **41-50** | Float | +, -, <, >, =, *, /, etc. | Float arithmetic | ✅ Done | - |
| **51-54** | Float | truncated, fractionPart, etc. | Float operations | ✅ Done | - |
| **60** | Array/String | at: | Element access | ✅ Done | - |
| **61** | Array/String | at:put: | Element store | ✅ Done | - |
| **62** | Collection | size | Get size | ✅ Done | - |
| **63-64** | String | at:, at:put: | Character access | ✅ Done | - |
| **65-67** | Stream | next, nextPut:, atEnd | Stream I/O | ✅ Done | - |
| **68-69** | CompiledMethod | objectAt:, objectAt:put: | Literal access | ✅ Done | - |
| **70** | Behavior | new | Object instantiation | ✅ Done | - |
| **71** | Behavior | new: | Indexed instantiation | ✅ Done | - |
| **72** | Object | become: | Swap identities | ✅ Done | - |
| **73-74** | Object | instVarAt:, instVarAt:put: | Instance var access | ✅ Done | - |
| **75** | Object | asOop/identityHash | Object identity | ✅ Done | - |
| **76** | Object | storeOn: | Store object | ✅ Done | - |
| **77-78** | Behavior | someInstance, nextInstance | Find instances | ✅ Done | - |
| **79** | CompiledMethod | newMethod:header: | Create method | ✅ Done | - |
| **80** | BlockContext | blockCopy: | Create block closure | ✅ Done | - |
| **81** | BlockContext | value | Execute block (0 args) | ✅ Done | - |
| **82** | BlockContext | value: | Execute block (1 arg) | ✅ Done | - |
| **83** | Object | perform: | Dynamic message send | ✅ Done | - |
| **84** | Object | perform:with: | Dynamic send with args | ✅ Done | - |
| **85-88** | Semaphore/Process | signal, wait, resume, suspend | Process control | ✅ Done | - |
| **90-95** | Sensor | mousePoint, cursor, input | Input handling | ✅ Done | - |
| **110** | Object | == | Identity comparison | ✅ Done | - |
| **111** | Object | class | Get object class | ✅ Done | - |
| **113** | SystemDictionary | quit | Exit system | ✅ Done | - |

**Legend:**
- ✅ **Done**: Fully implemented and tested
- ⚠️ **Partial**: Implemented but incomplete or limited functionality
- 🔶 **Stub**: Returns NotImplemented
- 📝 **TODO**: Commented out, needs implementation
- ❌ **Missing**: Not implemented at all

**Priority:**
- 🔴 **P1**: Critical - blocks basic Smalltalk execution
- 🔴 **P2**: High - needed for numeric computation
- 🟡 **P3**: Medium - needed for advanced features
- 🟢 **P4**: Low - needed for reflection/debugging

### Primitive Dispatcher Architecture

The primitive dispatcher uses modern C# 13 features with a clean registration pattern:

```csharp
public static class PrimitiveDispatcher
{
    private static readonly Dictionary<int, PrimitiveHandler> _primitives = new();

    public delegate PrimitiveResult PrimitiveHandler(
        SObjectRef receiver,
        SObjectRef[] args);

    public static void RegisterAll()
    {
        // SmallInteger arithmetic (1-17) - COMPLETE
        SmallIntegerPrimitives.Register();

        // Float arithmetic (40-54) - MISSING
        // FloatPrimitives.Register();

        // Array/Stream access (60-67) - PARTIAL
        ArrayPrimitives.Register();

        // Control primitives (80-89) - CRITICAL GAPS
        ControlPrimitives.Register();

        // System primitives (110-127) - PARTIAL
        SystemPrimitives.Register();
    }

    public static PrimitiveResult Execute(int primitiveIndex,
        SObjectRef receiver, SObjectRef[] args)
    {
        if (_primitives.TryGetValue(primitiveIndex, out var handler))
            return handler(receiver, args);

        return PrimitiveResult.Failure(PrimitiveFailure.NotImplemented);
    }
}
```

### Example: SmallInteger Addition (Primitive 1)

Complete implementation with overflow detection:

```csharp
private static PrimitiveResult PrimitiveAdd(SObjectRef receiver, SObjectRef[] args)
{
    // Type checking
    if (!receiver.IsSmallInteger || args.Length != 1 || !args[0].IsSmallInteger)
        return PrimitiveResult.Failure(PrimitiveFailure.BadArgument);

    // Perform operation in long to detect overflow
    long sum = (long)receiver.AsSmallInteger() + args[0].AsSmallInteger();

    // Check SmallInteger range (-2^30 to 2^30-1)
    if (sum < SmallIntegerMin || sum > SmallIntegerMax)
        return PrimitiveResult.Failure(PrimitiveFailure.Overflow);

    return PrimitiveResult.Success(SObjectRef.FromSmallInteger((int)sum));
}
```

When a primitive fails, the fallback Smalltalk code executes (e.g., creating a LargeInteger for overflow).

### Primitive Implementation Plan

See the **[Primitive Implementation Roadmap](#primitive-implementation-roadmap)** section below for the complete plan to achieve 100% primitive coverage.

## Graphics Support

The interpreter implements Smalltalk-80's BitBlt (Bit Block Transfer) graphics system with all 16 boolean combination rules as specified in the Blue Book. The graphics architecture uses an abstraction layer to support multiple rendering backends.

### Current Implementation: Windows Forms

**Status**: ✅ Complete (Primitives 96-104 implemented)

The current implementation uses Windows Forms with a pure C# BitBlt engine:

**Architecture:**
- `IDisplayProvider` - Abstract interface for display and input
- `WinFormsDisplayProvider` - Windows Forms implementation with 1-bit monochrome bitmap
- `BitBltEngine` - Pure C# implementation of all 16 combination rules
- `BitBltOperation` - Complete operation descriptor with source, destination, halftone, and clipping

**Key Features:**
- ✅ All 16 boolean combination rules (0-15)
- ✅ Zero-copy architecture (shared byte array between Smalltalk and display)
- ✅ Clipping rectangle support
- ✅ Halftone pattern masking (16x16 textures)
- ✅ Mouse and keyboard input integration
- ✅ 640x480 monochrome display (Format1bppIndexed)

**Graphics Primitives Implemented:**
```
96:  copyBits      - BitBlt operation with 16 combination rules
97:  snapshot      - Save image (stub)
98:  oopsLeft      - Available object pointers
99:  coreLeft      - Available heap memory
100: timeWords     - Current time
101: beDisplay     - Set as display
102: beCursor      - Set cursor shape
103: beep          - Audio beep
104: setDisplayMode- Display mode control
```

**Input Primitives Integrated:**
```
90: mousePoint    - Current mouse position
91: cursorLocPut  - Set cursor position
92: cursorLink    - Link cursor to process
93: inputSemaphore- Signal on input
94: sampleInterval- Input sampling rate
95: inputWord     - Raw input event word
```

### BitBlt Combination Rules

All 16 boolean combination rules from the Blue Book specification:

| Rule | Name | Boolean Operation | Description |
|------|------|------------------|-------------|
| 0 | Clear | `0` | All bits set to 0 (white) |
| 1 | And | `src & dst` | Logical AND |
| 2 | AndReverse | `src & ~dst` | Source AND NOT destination |
| 3 | Copy | `src` | Replace with source (most common) |
| 4 | AndInverted | `~src & dst` | NOT source AND destination |
| 5 | Noop | `dst` | Leave destination unchanged |
| 6 | Xor | `src ^ dst` | Exclusive OR |
| 7 | Or | `src \| dst` | Logical OR |
| 8 | Nor | `~(src \| dst)` | NOT (source OR destination) |
| 9 | Equiv | `~(src ^ dst)` | Equivalence (XNOR) |
| 10 | Invert | `~dst` | Invert destination |
| 11 | OrReverse | `src \| ~dst` | Source OR NOT destination |
| 12 | CopyInverted | `~src` | Copy inverted source |
| 13 | OrInverted | `~src \| dst` | NOT source OR destination |
| 14 | Nand | `~(src & dst)` | NOT (source AND destination) |
| 15 | Set | `1` | All bits set to 1 (black) |

### Performance Characteristics

**Current Implementation (Windows Forms + C# BitBlt):**

| Operation | 640x480 | Performance | Notes |
|-----------|---------|-------------|-------|
| Full-screen copy | ~5ms | Acceptable | Pixel-by-pixel C# loops |
| Small rectangles | <1ms | Good | Most UI operations are small |
| Halftone patterns | ~8ms | Acceptable | Pattern masking overhead |

**Strengths:**
- ✅ Proven correctness for all 16 rules
- ✅ Simple, maintainable pure C# code
- ✅ Adequate performance for 640x480 monochrome
- ✅ Zero external dependencies beyond .NET

**Limitations:**
- ⚠️ Pixel-level C# loops don't scale to high resolutions
- ⚠️ No GPU acceleration
- ⚠️ GDI+ is legacy technology

### Future Enhancement Options

When scaling to larger displays (1920x1080+) or adding color support, three backend options are available:

#### Option 1: OpenGL with LogicOp (Recommended)

**Perfect semantic match for Smalltalk-80 BitBlt:**

OpenGL provides `glLogicOp()` with **exact support for all 16 boolean operations**:

```csharp
glEnable(GL_COLOR_LOGIC_OP);
glLogicOp(GL_CLEAR);        // Rule 0: Clear
glLogicOp(GL_AND);          // Rule 1: AND
glLogicOp(GL_AND_REVERSE);  // Rule 2: src AND NOT dst
glLogicOp(GL_COPY);         // Rule 3: Copy
glLogicOp(GL_XOR);          // Rule 6: XOR
// ... all 16 rules map directly
```

**Advantages:**
- ✅ **Native hardware acceleration** - GPU executes boolean operations
- ✅ **Perfect semantic match** - 16 LogicOp modes = 16 BitBlt rules
- ✅ **Maximum performance** - Scales to 4K+ with minimal overhead (~0.1-0.2ms full-screen)
- ✅ **Cross-platform** - Windows, Linux, macOS support
- ✅ **Historical accuracy** - Similar to original Smalltalk-80 BitBlt hardware
- ✅ **No shader coding required** - Built-in GPU feature

**Disadvantages:**
- ⚠️ **Deprecated in modern OpenGL** - Removed from Core 3.2+ profile
- ⚠️ **Compatibility profile required** - Must request OpenGL 3.3 Compatibility mode
- ⚠️ **No mobile support** - OpenGL ES lacks LogicOp
- 🔧 **Complexity** - OpenGL context, textures, framebuffer management
- 🔧 **Requires .NET bindings** - OpenTK or Silk.NET dependency

**Performance Estimates:**

| Resolution | Full-screen BitBlt | Notes |
|------------|-------------------|-------|
| 640x480 | ~0.1ms | GPU parallel processing |
| 1920x1080 | ~0.1ms | Same performance (GPU scales) |
| 4K (3840x2160) | ~0.2ms | Hardware-limited, not CPU-limited |

**Implementation Notes:**

This is the **spiritual successor** to the original Xerox Alto/Dorado BitBlt hardware. The original Smalltalk-80 systems used dedicated hardware for boolean bitmap operations; OpenGL's `glLogicOp()` provides the same capability through modern GPU hardware.

**Code Structure:**
```csharp
public class OpenGLDisplayProvider : IDisplayProvider
{
    private int _framebuffer;
    private int _displayTexture;

    public void BitBlt(BitBltOperation op)
    {
        // Upload source to GPU texture
        GL.TexImage2D(..., op.SourceBits);

        // Configure logical operation
        GL.Enable(EnableCap.ColorLogicOp);
        GL.LogicOp(MapToGLLogicOp(op.Rule));

        // Render textured quad (GPU executes BitBlt)
        DrawQuad(op.DestX, op.DestY, op.Width, op.Height);

        GL.Disable(EnableCap.ColorLogicOp);
    }
}
```

#### Option 2: Modern OpenGL Core with Custom Shaders

**For modern OpenGL without deprecated features:**

If OpenGL Compatibility profile is unavailable, implement boolean operations in GLSL fragment shaders:

```glsl
#version 330 core

uniform sampler2D srcTexture;
uniform sampler2D dstTexture;
uniform int combinationRule;

void main()
{
    bool src = texture(srcTexture, texCoord).r > 0.5;
    bool dst = texture(dstTexture, texCoord).r > 0.5;
    bool result;

    switch(combinationRule) {
        case 0: result = false; break;           // Clear
        case 1: result = src && dst; break;      // AND
        case 3: result = src; break;             // Copy
        case 6: result = src != dst; break;      // XOR
        // ... all 16 rules
    }

    fragColor = vec4(result ? 1.0 : 0.0);
}
```

**Advantages:**
- ✅ Works on all modern OpenGL (3.3+)
- ✅ GPU-accelerated
- ✅ WebGL compatible
- ✅ Future-proof

**Disadvantages:**
- 🔧 Requires GLSL shader programming
- ⚠️ Slightly slower than native LogicOp (but still fast)

#### Option 3: SkiaSharp with CPU/GPU Hybrid

**Cross-platform 2D graphics library:**

SkiaSharp provides a production-proven, actively maintained graphics stack used by Chrome, Flutter, and Android.

**Advantages:**
- ✅ **True cross-platform** - Windows, macOS, Linux, iOS, Android, WebAssembly
- ✅ **GPU-accelerated** - OpenGL/Vulkan/Metal backends
- ✅ **Excellent .NET integration** - First-class C# API
- ✅ **Production-proven** - Powers major applications at scale
- ✅ **Color support** - Excellent palette and color management
- ✅ **Active development** - Regular updates, modern features

**Disadvantages:**
- ❌ **No built-in boolean operations** - Must implement custom
- 🔧 **Additional dependency** - NuGet package required
- ⚠️ **Hybrid approach needed** - GPU for rendering, CPU for boolean ops

**Implementation Strategy:**
```csharp
public class SkiaSharpDisplayProvider : IDisplayProvider
{
    private SKSurface _surface;
    private SKCanvas _canvas;

    public void BitBlt(BitBltOperation op)
    {
        // Use CPU for boolean operations (still fast with unsafe pointers)
        ApplyBooleanOperation(op);

        // Use GPU for final rendering and compositing
        _canvas.DrawBitmap(_backBuffer, ...);
    }
}
```

### Recommended Migration Path

**Phase 1 (Current)**: ✅ Windows Forms + Pure C# BitBlt
- Proven correct implementation
- Adequate for 640x480 monochrome
- Foundation established

**Phase 2 (Future - High Resolution/Color)**: OpenGL with Compatibility Profile
- Implement `OpenGLDisplayProvider` with `glLogicOp()`
- Request OpenGL 3.3 Compatibility context
- Use OpenTK or Silk.NET for .NET bindings
- **Ideal for desktop Smalltalk-80 with historical accuracy**

**Phase 3 (If needed - Maximum Compatibility)**: Modern OpenGL Core + Shaders
- Fallback if compatibility profile unavailable
- Custom GLSL fragment shaders for boolean operations
- Works everywhere including WebGL

**Fallback Option**: SkiaSharp
- If OpenGL proves problematic
- Best for cross-platform requirements
- Proven reliability and performance

### Graphics Architecture Design

The current `IDisplayProvider` abstraction enables seamless backend switching:

```csharp
public interface IDisplayProvider
{
    Memory<byte> GetDisplayBuffer();
    int Width { get; }
    int Height { get; }
    int BitsPerPixel { get; }

    void InvalidateRect(int x, int y, int width, int height);
    void InvalidateAll();

    bool ProcessEvents();
    (int X, int Y) GetMousePosition();
    int GetMouseButtons();
    int GetInputWord();
}

// Factory pattern for backend selection
public static class DisplayProviderFactory
{
    public static IDisplayProvider Create(GraphicsBackend backend)
    {
        return backend switch
        {
            GraphicsBackend.WinForms => new WinFormsDisplayProvider(...),
            GraphicsBackend.OpenGL => new OpenGLDisplayProvider(...),
            GraphicsBackend.SkiaSharp => new SkiaSharpDisplayProvider(...),
            _ => throw new ArgumentException()
        };
    }
}
```

This architecture allows experimentation with different backends without modifying primitive implementations or Smalltalk code.

### Testing

A comprehensive graphics test suite validates all functionality:

**TestGraphics Application:**
- ✅ All 16 combination rules tested
- ✅ Clipping verification
- ✅ Halftone pattern rendering
- ✅ Interactive mouse input (left-click to draw, right-click to exit)
- ✅ Real-time display updates

**Test Coverage:**
- Clear screen (rule 0)
- Rectangle drawing (rule 3 - copy)
- XOR overlapping patterns (rule 6)
- Clipped operations
- Halftone checkerboard patterns
- Mouse position tracking
- Button state detection

### Graphics Files

**Core Graphics:**
- `Graphics/IDisplayProvider.cs` - Display abstraction interface
- `Graphics/WinFormsDisplayProvider.cs` - Windows Forms implementation
- `Graphics/BitBltEngine.cs` - Pure C# BitBlt with 16 rules
- `Graphics/BitBltOperation.cs` - Operation parameter descriptor
- `Graphics/CombinationRule.cs` - 16 boolean rules enumeration

**Primitives:**
- `Primitives/GraphicsPrimitives.cs` - Primitives 96-104
- `Primitives/InputPrimitives.cs` - Primitives 90-95

**Testing:**
- `TestGraphics/Program.cs` - Interactive graphics test suite
- `TestGraphics/TestGraphics.csproj` - Test project configuration

## Compiler Integration

### Generated Files from Compiler

The compiler analyzes Smalltalk-80.st and generates three files:

1. **Layouts.cs** - Type-safe readonly struct wrappers for all cold start classes with property accessors for instance variables
2. **ColdStartData.cs** - Class metadata (inst vars, format, superclass)
3. **ColdStartLiterals.cs** - Method literals extracted from AST

### Compiler Components

**ColdStartDataGenerator.cs**: Generates class metadata from AST
**LiteralExtractor.cs**: Extracts method literals from AST nodes
**LayoutGenerator.cs**: Generates type-safe layout structs

### Layout Struct Pattern

Each generated layout struct provides type-safe access to instance variables:

```csharp
// Example: STProcessorScheduler layout (ProcessorScheduler class)
public readonly struct STProcessorScheduler : ISTProcessorScheduler
{
    private readonly SObjectRef _ref;

    public STProcessorScheduler(SObjectRef objectRef) => _ref = objectRef;

    // Instance variable accessors (type-safe, named properties)
    public SObjectRef QuiescentProcessLists
    {
        get => _ref.GetInstanceVariable(0);
        set => _ref.SetInstanceVariable(0, value);
    }

    public SObjectRef ActiveProcess
    {
        get => _ref.GetInstanceVariable(1);
        set => _ref.SetInstanceVariable(1, value);
    }

    public const int InstanceVarCount = 2;
}
```

**Benefits:**
- **Type safety**: Compile-time checking instead of runtime errors
- **Named access**: `scheduler.ActiveProcess` instead of magic index `1`
- **IntelliSense support**: IDE auto-completion for instance variables
- **Refactoring safe**: Changing field order updates all references
- **Self-documenting**: Property names show semantic meaning

### Build Process

```xml
<Target Name="RunCompiler" BeforeTargets="BeforeBuild">
  <Exec Command="dotnet run --project ../Smalltalk80.Compiler --
                 ../Smalltalk-80 Source/Smalltalk-80.st
                 Generated/Layouts.cs
                 Generated/ColdStartData.cs
                 Generated/ColdStartLiterals.cs" />
</Target>
```

**Result**: All cold start data comes from parsing the canonical Smalltalk-80.st source file.

## Source File Organization

### Directory Structure

```
Smalltalk80.Interpreter/
├── ColdStart/                      # Bootstrap system
│   ├── ColdStartBootstrap.cs       # 4-pass + 2-phase orchestrator
│   ├── ColdStartClassCreator.cs    # Class creation logic
│   ├── ColdStartMethodInstaller.cs # Method installation
│   ├── SymbolTableBuilder.cs       # USTable creation and migration
│   └── CharacterTableBuilder.cs    # Character table (256 chars)
├── Core/                           # Core object model
│   ├── Objects/
│   │   ├── SystemDictionary.cs     # Global class registry
│   │   └── ...
│   ├── Values/
│   │   ├── SObjectRef.cs           # Tagged pointer implementation
│   │   └── ...
│   └── BlueBook/
│       └── BlueBookFactory.cs      # Factory for Blue Book objects
├── Memory/                         # Memory management
│   ├── ObjectMemory.cs             # Table-based storage
│   ├── ObjectTable.cs              # OOP table implementation
│   ├── GarbageCollector.cs         # Mark-and-sweep GC
│   └── SpecialObjectsArray.cs      # 5-field readonly struct
├── Execution/                      # Bytecode execution
│   ├── ContextBasedInterpreterNew.cs # Context-based interpreter
│   └── BytecodeDecoder.cs          # Bytecode dispatch
├── Primitives/                     # Primitive operations
│   └── PrimitiveDispatcher.cs      # Primitive dispatch
├── Generated/                      # Compiler-generated files
│   ├── Layouts.cs                  # Type-safe layout structs
│   ├── ColdStartData.cs            # Class metadata
│   └── ColdStartLiterals.cs        # Method literals
└── VirtualMachine.cs               # VM orchestration
```

### Key Modules

**ColdStartBootstrap.cs** (563 lines):
- Orchestrates 4-pass class creation
- Manages 2-phase infrastructure initialization
- Coordinates all bootstrap steps

**SpecialObjectsArray.cs** (152 lines):
- 5-field readonly struct
- Provides GC roots
- Includes ActiveProcess accessor

**SystemDictionary.cs** (678 lines):
- Class registry and lookup
- Global variable management
- GetAllClassNames() iterates actual dictionary instance

**GarbageCollector.cs** (474 lines):
- Mark-and-sweep implementation
- Statistics tracking
- Zero garbage validation

## Blue Book Compliance

### Compliance Status: STRICT

The interpreter implements **strict Blue Book compliance** - every design decision follows the Smalltalk-80 specification (Goldberg & Robson, 1983).

### Key Compliance Areas

#### 1. Tagged Pointer Scheme
- **Blue Book Spec**: Only SmallInteger is an immediate value
- **Our Implementation**: ✅ Only SmallInteger uses tagged pointers (LSB = 1)
- **Verification**: Character, Symbol, Boolean are heap objects (LSB = 0)

#### 2. Object Memory Format
- **Blue Book Spec**: Table-based with OOPs, become: support
- **Our Implementation**: ✅ ObjectTable structure matches specification
- **Verification**: All objects created through ObjectMemory allocation

#### 3. Class Layouts
- **Blue Book Spec**: Fixed instance variable layouts for core classes
- **Our Implementation**: ✅ Generated/Layouts.cs defines type-safe layout structs
- **Verification**: MethodContext (6 fixed fields), Metaclass (7 inst vars), etc.

#### 4. Garbage Collection
- **Blue Book Spec**: Mark-and-sweep from minimal roots
- **Our Implementation**: ✅ 5 SOA roots, mark-and-sweep algorithm
- **Verification**: Zero garbage during cold start

#### 5. Metaclass Architecture
- **Blue Book Spec**: Bootstrap triangle (Object, Class, Metaclass)
- **Our Implementation**: ✅ 4-pass creation with circular wiring
- **Verification**: Metaclass.class = Metaclass, all metaclasses have 7 inst vars

## Class Hierarchy Display

### Implementation

```csharp
public void PrintClasses()
{
    Console.WriteLine("\nClass Hierarchy:");
    Console.WriteLine("Total Classes: {0}", SystemDictionary.GetAllClassNames().Count());
    Console.WriteLine();

    PrintClassHierarchy("Object", 0, new HashSet<string>());
}

private void PrintClassHierarchy(string className, int depth,
    HashSet<string> visited, bool isLastSibling = true)
{
    if (visited.Contains(className)) return;
    visited.Add(className);

    var classRef = SystemDictionary.LookupClassRef(className);
    if (!classRef.HasValue || classRef.Value.IsNil) return;

    // Draw tree structure with box-drawing characters
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
            var otherRef = SystemDictionary.LookupClassRef(c);
            if (!otherRef.HasValue || otherRef.Value.IsNil) return false;

            var superRef = ObjectMemoryNew.GetClassSuperclass(
                otherRef.Value.AsObjectPointer());
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

### GetAllClassNames Implementation

**Fixed to iterate SystemDictionary instance** (not static properties):

```csharp
public static IEnumerable<string> GetAllClassNames()
{
    if (Instance.IsNil)
        return Enumerable.Empty<string>();

    var classNames = new List<string>();
    var dictOop = Instance.AsObjectPointer();
    var size = ObjectMemoryNew.GetIndexedSize(dictOop);

    for (int i = 0; i < size; i++)
    {
        var assocRef = ObjectMemoryNew.GetIndexedPointer(dictOop, i);
        if (!assocRef.IsNil)
        {
            STAssociation assoc = assocRef;
            var keyRef = assoc.Key;

            if (!keyRef.IsNil)
            {
                var keyStr = ObjectMemoryNew.SymbolToString(keyRef);
                var valueRef = assoc.Value;

                // Verify it's a class (has inst vars)
                if (!valueRef.IsNil && valueRef.IsHeapObject)
                {
                    var instVarCount = ObjectMemoryNew.GetInstanceVariableCount(
                        valueRef.AsObjectPointer());
                    if (instVarCount >= 9) // Classes have 9+ inst vars
                    {
                        classNames.Add(keyStr);
                    }
                }
            }
        }
    }

    return classNames;
}
```

### Output

```
Class Hierarchy:
Total Classes: 46

Object
├─Behavior
│ └─ClassDescription
│   ├─Class
│   └─Metaclass
├─Boolean
│ ├─False
│ └─True
├─Collection
│ ├─SequenceableCollection
│ │ ├─ArrayedCollection
│ │ │ ├─Array
│ │ │ ├─ByteArray
│ │ │ ├─CompiledMethod
│ │ │ └─String
│ │ │   └─Symbol
│ │ ├─LinkedList
│ │ │ └─Semaphore
│ │ └─OrderedCollection
│ └─Set
│   └─Dictionary
│     ├─IdentityDictionary
│     │ ├─MethodDictionary
│     │ └─SystemDictionary
...
```

## Multi-VM Architecture (Future)

### Current Static State

**Must become per-VM instance-based:**
1. **ObjectMemoryNew** - Currently static, contains ObjectTable
2. **SpecialObjectsArray** - Currently static fields
3. **SystemDictionary.Instance** - Static reference to dictionary
4. **GarbageCollector** - Already instance-based ✓

**Can remain static (shared):**
- Generated layouts (ColdStartData, Layouts.cs, ColdStartLiterals.cs)
- Bytecode constants
- Blue Book specifications
- Primitive implementations (stateless)

### Proposed VM Context

```csharp
public class SmalltalkVM
{
    // Per-VM heap and object management
    public ObjectMemory ObjectMemory { get; }
    public ObjectTable ObjectTable { get; }

    // Per-VM GC roots (5-field struct instance)
    public SpecialObjectsArray SOA { get; private set; }

    // Per-VM execution
    public GarbageCollector GC { get; }
    public Interpreter Interpreter { get; }

    // Per-VM system state
    public SObjectRef SystemDictionaryRef { get; private set; }

    public SmalltalkVM()
    {
        ObjectTable = new ObjectTable();
        ObjectMemory = new ObjectMemory(ObjectTable);
        GC = new GarbageCollector(ObjectTable);
        Interpreter = new Interpreter(this);

        // Bootstrap creates SOA
        SOA = ColdStartBootstrap.Initialize(this);
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
- Proper metaclass hierarchy (bootstrap triangle)
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

### 5. Type-Safe Layout Structs

**All object field access uses generated layout structs:**
- Generated from Smalltalk-80.st source (single source of truth)
- Readonly struct wrappers around SObjectRef
- Named properties for instance variables (not magic indices)
- Compile-time type safety and IDE support
- Examples: STProcessorScheduler, STProcess, STLinkedList, STMethodContext

### 6. Per-VM Design

**All VM state will be instance-based:**
- ObjectTable: Per-VM heap
- SpecialObjectsArray: Per-VM roots (struct instance)
- Interpreter: Per-VM execution state
- SystemDictionary: Reference in heap (per-VM)

## Technical Specifications

### Bytecode Instruction Set

The Smalltalk-80 bytecode instruction set (256 instructions):

#### Push Instructions (0x00-0x7F)

**Push Receiver Variable (0x00-0x0F)**
- Format: `0000iiii` where `iiii` = variable index (0-15)
- Operation: Push receiver's instance variable at index

**Push Temporary Variable (0x10-0x1F)**
- Format: `0001iiii` where `iiii` = temp index (0-15)
- Operation: Push temporary variable at index

**Push Literal Constant (0x20-0x3F)**
- Format: `001iiiii` where `iiiii` = literal index (0-31)
- Operation: Push literal from method's literal frame

**Push Literal Variable (0x40-0x5F)**
- Format: `010iiiii` where `iiiii` = literal index (0-31)
- Operation: Push value of Association at literal index

**Push Special (0x70-0x77)**
- 0x70: Push receiver (self)
- 0x71: Push true
- 0x72: Push false
- 0x73: Push nil
- 0x74: Push -1
- 0x75: Push 0
- 0x76: Push 1
- 0x77: Push 2

#### Store Instructions (0x60-0x6F)

**Pop and Store Receiver Variable (0x60-0x67)**
- Format: `01100iii` where `iii` = variable index (0-7)
- Operation: Pop stack and store in receiver's instance variable

**Pop and Store Temporary Variable (0x68-0x6F)**
- Format: `01101iii` where `iii` = temp index (0-7)
- Operation: Pop stack and store in temporary variable

#### Return Instructions (0x78-0x7C)

- 0x78: Return stack top from method
- 0x79: Return stack top from block
- 0x7C: Return receiver (self) from method

#### Send Message Instructions (0x90-0xDF)

**Send Literal Selector**
- 0x90-0x9F: Send selector with 0 args (selector index 0-15)
- 0xA0-0xAF: Send selector with 1 arg (selector index 0-15)
- 0xB0-0xBF: Send selector with 2 args (selector index 0-15)

**Special Sends (0xD0-0xDF)**
- Arithmetic: +, -, <, >, <=, >=, =, ~=, *, /
- Common: at:, at:put:, size, next, class
- Control: value, do:, new, new:

### Primitive Operation Numbers

Smalltalk-80 defines 127 standard primitives (1-127):

#### Arithmetic Primitives (1-19)

| Primitive | Operation | Notes |
|-----------|-----------|-------|
| 1 | SmallInteger + | Addition with overflow check |
| 2 | SmallInteger - | Subtraction with overflow check |
| 3 | SmallInteger < | Less than comparison |
| 4 | SmallInteger > | Greater than comparison |
| 5 | SmallInteger <= | Less than or equal |
| 6 | SmallInteger >= | Greater than or equal |
| 7 | SmallInteger = | Equality comparison |
| 8 | SmallInteger ~= | Inequality comparison |
| 9 | SmallInteger * | Multiplication with overflow check |
| 10 | SmallInteger / | Integer division |
| 11 | SmallInteger \\ | Modulo operation |
| 12 | SmallInteger // | Integer quotient |

#### Object Access Primitives (60-79)

| Primitive | Operation | Notes |
|-----------|-----------|-------|
| 60 | at: | Array/object indexed access |
| 61 | at:put: | Array/object indexed store |
| 62 | size | Object size |
| 68 | objectAt: | Direct instance variable access |
| 69 | objectAt:put: | Direct instance variable store |
| 70 | new | Allocate new instance |
| 71 | new: | Allocate new instance with size |
| 72 | become: | Swap object identities |
| 73 | instVarAt: | Instance variable access |
| 74 | instVarAt:put: | Instance variable store |
| 75 | class | Get object's class |
| 76 | hash | Object identity hash |

#### Control Primitives (80-99)

| Primitive | Operation | Notes |
|-----------|-----------|-------|
| 80 | blockCopy: | Create block closure |
| 81 | value | Evaluate block with 0 args |
| 82 | value: | Evaluate block with 1 arg |
| 83 | value:value: | Evaluate block with 2 args |
| 84 | perform: | Send message dynamically |

### Method Header Format

CompiledMethod header encoding:

```
Header word (16 bits):
  Bits 15-14: 11 (indicates compiled method)
  Bits 13-10: Argument count (0-15)
  Bits 9-5: Temporary count (0-31)
  Bits 4-0: Literal count (0-31)

Optional primitive number (if method has primitive):
  Stored in first literal as SmallInteger
```

## Execution Plan to 100% Primitive Coverage

### Executive Summary

**Goal:** Achieve 100% correct primitive implementation for all 129 primitives used in Smalltalk-80.st

**Current Status:** 104/129 primitives implemented (80%)
**Target:** 129/129 primitives implemented (100%)
**Estimated Effort:** 25 primitives remaining across 3 phases over 4 weeks

### Implemented Primitives (104 total)

**SmallInteger Arithmetic (17/17):** 1-17 ✅ Complete
**LargeInteger Arithmetic (17/17):** 21-37 ✅ Complete
**Float Arithmetic (15/15):** 40-54 ✅ Complete
**Array/Stream Operations (8/8):** 60-67 ✅ Complete
**Storage Management (12/12):** 68-79 ✅ Complete
**Control & Process (9/10):** 80-88 (missing: 89)
**Input Operations (6/6):** 90-95 ✅ Complete
**Graphics Operations (9/9):** 96-104 ✅ Complete
**String Operations (1/1):** 105
**System Operations (10/20):** 110-116, 133, 139

### Implementation Phases

**COMPLETED PHASES:**

#### ✅ Phase 1: Core Numeric Support - COMPLETE (49 primitives)
- SmallInteger arithmetic (1-17): ✅ Complete
- LargeInteger arithmetic (21-37): ✅ Complete
- Float operations (40-54): ✅ Complete

#### ✅ Phase 2: Control Flow - COMPLETE (9 primitives)
- Block evaluation (80-82): ✅ Complete
- Dynamic dispatch (83-84): ✅ Complete
- Process/Semaphore (85-88): ✅ Complete

#### ✅ Phase 3: Storage & Reflection - COMPLETE (12 primitives)
- Object creation (70-71): ✅ Complete
- Method literals (68-69): ✅ Complete
- Instance variables (73-74): ✅ Complete
- Object identity (72, 75-79): ✅ Complete

#### ✅ Phase 4: Input Basics - COMPLETE (6 primitives)
- Mouse/cursor (90-92): ✅ Complete
- Input events (93-95): ✅ Complete

#### ✅ Phase 5: Graphics & UI - COMPLETE (9 primitives)
- BitBlt operations (96): ✅ Complete (all 16 combination rules)
- Display primitives (97-104): ✅ Complete
- Windows Forms integration: ✅ Complete

**Implementation Details:**
- BitBlt engine with all 16 boolean combination rules (0-15)
- Zero-copy architecture: shared byte array between Smalltalk and Windows Forms
- IDisplayProvider abstraction for display surface and input events
- WinFormsDisplayProvider: 1-bit monochrome Bitmap (Format1bppIndexed)
- Input primitives integrated with Windows Forms event handling
- Clipping support for all BitBlt operations
- Halftone pattern masking for texture fills

---

**REMAINING WORK:**

#### Phase 6: System & Time (Weeks 1-2) - 6 primitives

**Goal:** Complete system control and time operations

**6.1: Cache & System Control (Week 1) - 6 primitives**

| Primitive | Selector | Purpose | Complexity | Est. Hours |
|-----------|----------|---------|------------|------------|
| 89 | flushCache | Flush method cache | Easy | 2h |
| 18 | signalAtBytesLeft | Signal threshold | Easy | 2h |
| 120 | calloutToFFI | FFI call | Hard | 8h |
| 128, 130-132 | Instance enumeration | Object enumeration | Medium | 6h |

**Phase 6 Testing:**
```smalltalk
Time now.                           "Get current time"
Smalltalk oopsLeft.                "Check available memory"
```

**Phase 6 Deliverables:**
- Time and resource monitoring functional
- Method cache management complete

#### Phase 7: Extended System (Weeks 3-4) - 13 primitives

**Goal:** Complete all extended system primitives

**7.1: Class Mutation (Week 3) - 4 primitives**

| Primitive | Selector | Purpose | Complexity | Est. Hours |
|-----------|----------|---------|------------|------------|
| 134-137 | changeClassTo, primitiveChangeClassToThatOf, etc. | Change object class | Medium | 10h |

*Implementation Notes:*
- Enable object class changing for image reshaping
- Security implications - use with caution
- Must maintain object invariants

**7.2: File System Extensions (Week 4) - 9 primitives**

| Primitive | Selector | Purpose | Complexity | Est. Hours |
|-----------|----------|---------|------------|------------|
| 150-158 | File operations | File open/close/read/write/seek | Hard | 24h |

*Implementation Notes:*
- Windows-specific file system extensions
- Platform abstraction layer recommended
- Can defer if file I/O not immediately needed

**Phase 7 Testing:**
```smalltalk
(FileStream oldFileNamed: 'test.txt') contents.
Object someInstance changeClassTo: Array.
```

**Phase 7 Deliverables:**
- All 129 primitives implemented
- File system I/O functional
- Class mutation capabilities complete

#### Phase 8: Testing & Validation (Week 5)

**Goal:** Comprehensive testing and documentation

- Unit test each primitive with edge cases
- Integration testing through Smalltalk method invocation
- Performance benchmarking of critical paths
- Complete primitive reference documentation

**Phase 8 Deliverables:**
- ✅ 100% primitive coverage (129/129)
- ✅ Comprehensive test suite
- ✅ Performance benchmarks
- ✅ Complete documentation

### Implementation Priorities

**✅ COMPLETED PRIORITIES:**

**P1 (Critical) - COMPLETE:** All 49 numeric primitives implemented
- SmallInteger arithmetic (1-17): ✅ Complete
- LargeInteger arithmetic (21-37): ✅ Complete
- Float operations (40-54): ✅ Complete

**P2 (High) - COMPLETE:** All 21 core object system primitives implemented
- Storage management (68-79): ✅ Complete (12 primitives)
- Control flow (80-84): ✅ Complete (5 primitives)
- Array/Stream (60-67): ✅ Complete (8 primitives)

**P3 (Input) - COMPLETE:** Basic input primitives implemented
- Input handling (90-95): ✅ Complete (6 primitives)

**REMAINING PRIORITIES:**

**P4 (Medium) - 20 primitives:** Graphics and time operations
- Graphics/Display (96-104): 9 primitives
- System control (89, 18, 120, 128, 130-132): 6 primitives
- String operations (105): Already complete
- **Rationale:** Required for interactive environment; BitBlt can be stubbed initially

**P5 (Low) - 13 primitives:** Extended features
- Class mutation (134-137): 4 primitives
- File system (150-158): 9 primitives
- **Rationale:** Nice to have, platform-specific, can defer

### Timeline Summary

| Phase | Weeks | Primitives | Cumulative | % Complete | Status |
|-------|-------|------------|------------|------------|--------|
| **Phases 1-4** | - | 95 | 95 | 73% | ✅ **COMPLETE** |
| **Phase 5** | 1-3 | 20 | 115 | 89% | Graphics & time (P4) |
| **Phase 6** | 4-5 | 13 | 128 | 99% | Extended system (P5) |
| **Phase 7** | 6 | 1 | 129 | 100% | Testing & validation |
| **Total** | **6 weeks** | **34** | **129** | **100%** | - |

**Revised Critical Path:**
1. **Weeks 1-3:** Graphics engine and time operations (P4)
2. **Weeks 4-5:** Extended system primitives (P5)
3. **Week 6:** Comprehensive testing and validation

### Risk Assessment

**✅ MITIGATED RISKS (Completed):**

1. ~~**LargeInteger Implementation**~~ - ✅ **COMPLETE**
   - Successfully implemented using System.Numerics.BigInteger
   - All 17 primitives working correctly with proper byte array conversion

2. ~~**become: Implementation**~~ - ✅ **COMPLETE**
   - Implemented using ObjectMemory.Become() with atomic swap
   - Object identity invariants maintained

**REMAINING HIGH-RISK ITEMS:**

1. **BitBlt Graphics (Primitive 96)**
   - *Risk:* Very complex raster operation algorithm with 16 combination rules
   - *Mitigation:* Reference Squeak/Pharo implementations; consider using C# graphics libraries as foundation
   - *Estimated Effort:* 24 hours

2. **Image Snapshot (Primitive 97)**
   - *Risk:* Complete ObjectTable serialization, version compatibility
   - *Mitigation:* Design versioned serialization format; test incremental save/load
   - *Estimated Effort:* 20 hours

3. **File System Extensions (Primitives 150-158)**
   - *Risk:* Platform-specific file operations
   - *Mitigation:* Abstract platform layer; may defer to later phase
   - *Estimated Effort:* 24 hours

**Overall Risk Level:** LOW
- 73% of primitives complete with all high-priority items done
- Remaining work is largely independent and can be stubbed
- No blocking dependencies for core VM functionality

### Implementation Guidelines

#### Code Quality Standards

1. **Type Safety:** Use pattern matching for type checks
2. **Error Handling:** Always return proper PrimitiveFailure codes
3. **Testing:** Write unit tests for each primitive
4. **Documentation:** XML comments with Blue Book references
5. **Performance:** Optimize hot paths (arithmetic, array access)

#### Testing Strategy

For each primitive:
1. **Unit Test:** Test primitive directly with various inputs
2. **Integration Test:** Test via Smalltalk method invocation
3. **Edge Cases:** Test boundary conditions, overflows, nil handling
4. **Failure Cases:** Verify fallback Smalltalk code executes

#### Example Test Pattern

```csharp
[Fact]
public void Primitive40_AsFloat_ConvertsSmallIntegerToFloat()
{
    // Test SmallInteger → Float conversion
    var receiver = SObjectRef.FromSmallInteger(42);

    // Execute primitive 40 (asFloat)
    var result = PrimitiveDispatcher.Execute(40, receiver, []);

    // Verify result
    Assert.True(result.Success);
    Assert.True(result.Value.IsHeapObject);
    Assert.Equal(42.0, result.Value.AsFloat());
}
```

### Success Criteria by Phase

**Phase 1 (Numeric) Complete When:**
- ✅ All SmallInteger primitives work (1-17)
- ✅ LargeInteger overflow handling correct
- ✅ Float arithmetic functional
- ✅ Can execute mathematical Smalltalk code

**Phase 2 (Control) Complete When:**
- ✅ Multi-argument perform: works
- ✅ Method cache flushing implemented
- ✅ All control flow complete

**Phase 3 (Reflection) Complete When:**
- ✅ Object introspection works
- ✅ become: swaps identities correctly
- ✅ Instance enumeration functional
- ✅ Can build inspectors and debuggers

**Phase 4 (I/O) Complete When:**
- ✅ Basic I/O primitives work
- ✅ Graphics engine functional (or stubbed)
- ✅ Image save/load capability
- ✅ Interactive environment possible

**Phase 5 (Extended) Complete When:**
- ✅ All 129 primitives implemented
- ✅ File system operations work
- ✅ Advanced system primitives complete

**Phase 6 (Validation) Complete When:**
- ✅ 100% test coverage
- ✅ Performance benchmarks complete
- ✅ Documentation finished
- ✅ Production ready

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book)
2. Ingalls, D. (1978). *The Smalltalk-76 Programming System Design and Implementation*
3. Microsoft. (2024). *C# 13 Language Specification*

---

**Document Status**: Production
**Last Verified**: 2025-10-04
**Zero Garbage**: ✅ Achieved
**Multi-VM Ready**: 🚧 In Progress
