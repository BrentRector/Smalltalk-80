# Heap Manager Architecture

**Last Updated**: 2025-11-21
**Status**: ✅ Production-ready - Separated assembly with clean three-interface design

---

## Overview

The Smalltalk-80 Heap Manager is a **separate assembly** (`Smalltalk80.Interpreter.HeapManager`) responsible for low-level object allocation, garbage collection, and object identity management. It provides the foundation for the Smalltalk-80 virtual machine without any knowledge of high-level Smalltalk class semantics.

---

## Assembly Structure

### Smalltalk80.Interpreter.HeapManager.dll

**Dependencies**: Only `Smalltalk80.Interpreter.Shared.Types` (SmalltalkOOP, ObjectFormat, FormatWord, etc.)

**Location**: `/Smalltalk80.Interpreter.HeapManager/`

**Files** (11 total):
```
ISmalltalkHeap.cs           - Core heap interface (250 lines)
IGarbageCollector.cs        - GC interface (72 lines)
IBecomeHandler.cs           - Become operations interface (20 lines)
ISmalltalkHeapManager.cs    - Facade interface (backwards compatibility)
SmalltalkHeapManager.cs     - Main implementation (825+ lines)
ObjectTableManager.cs       - Object table block management
GarbageCollector.cs         - Mark-sweep GC implementation
BecomeHandler.cs            - Identity swapping implementation
ObjectTableEntry.cs         - Object metadata structure
IObjectTableManager.cs      - Table management interface
HeapStatistics.cs           - GC statistics structure
```

---

## Three-Interface Architecture

The heap manager exposes **three distinct interfaces** for different subsystems:

### 1. ISmalltalkHeap - Core Heap Operations

**Purpose**: Object allocation and access (used by VM, primitives, cold start)

**Key Operations**:
- **Nil Singleton**: `GetNil()` - Returns placeholder/real nil OOP
- **Object Allocation**:
  - `AllocateObject(format, fixedSlots, indexedSize)` - Low-level allocation
  - `AllocateInstanceOf(classOOP, indexedSize)` - Class-based allocation (reads format from heap)
- **Class Pointer**: `GetClassOOP()`, `SetClassOOP()`
- **Instance Variables**: `GetInstanceVariable()`, `SetInstanceVariable()`, `SetInstanceVariableWithBarrier()`, `SetInstanceVariableNoBarrier()`
- **Indexed Access**:
  - OOPs: `GetIndexedOOP()`, `SetIndexedOOP()` - For Arrays, OrderedCollections
  - Bytes: `GetIndexedByte()`, `SetIndexedByte()` - For Strings, ByteArrays, Floats
  - Words: `GetIndexedWord()`, `SetIndexedWord()` - For WordArrays, Bitmaps
- **Metadata**: `GetObjectFormat()`, `GetObjectSizeInWords()`, `GetInstanceVariableCount()`, `GetIndexedSize()`
- **Validation**: `IsValidObjectOOP()`, `IsValidOOP()`, `EnumerateObjectTable()`
- **Instance Finding**: `FindInstanceOf()`, `FindNextInstanceOf()` - For primitives 77-78
- **Diagnostics**: `GetClassName()`, `ValidateAllObjectClasses()`, `VerboseDebugMode`
- **Subsystem Access**: `GC` property, `Become` property

### 2. IGarbageCollector - Garbage Collection

**Purpose**: Mark-sweep collection and root management

**Key Operations**:
- **Root Management**:
  - `AddGCRoot(oop)` - Register GC root
  - `RemoveGCRoot(oop)` - Unregister GC root
  - `GetGCRoots()` - Get all roots (defensive copy)
  - `ClearGCRoots()` - Clear all roots
- **Collection**:
  - `GarbageCollect()` - Full mark-sweep (4 phases: clear marks, mark reachable, find unreachable, collect)
  - `GetStatistics()` - Current GC statistics without collection
- **Debug/Testing**:
  - `GetUnreachableObjects()` - Performs marking only, returns what would be collected (no actual collection)

### 3. IBecomeHandler - Identity Swapping

**Purpose**: Implements Blue Book primitive 72 (become:)

**Key Operations**:
- `BecomeTwoWay(oop1, oop2)` - Swaps identities of two objects (all references to oop1 now point to oop2 and vice versa)

---

## Component Relationships

```
┌─────────────────────────────────────────────────┐
│      Smalltalk80.Interpreter.HeapManager        │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │       SmalltalkHeapManager (main)          │ │
│  │   (implements ISmalltalkHeap)              │ │
│  │                                             │ │
│  │   Depends on:                              │ │
│  │   ├─► ObjectTableManager                   │ │
│  │   ├─► GarbageCollector                     │ │
│  │   └─► BecomeHandler                        │ │
│  └────────────────────────────────────────────┘ │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │       ObjectTableManager (internal)        │ │
│  │   (implements IObjectTableManager)         │ │
│  │                                             │ │
│  │   Manages:                                 │ │
│  │   ├─ ObjectTable blocks (256 entries each)│ │
│  │   ├─ Free index stack (reuses freed slots)│ │
│  │   └─ Index allocation (1-based indices)   │ │
│  └────────────────────────────────────────────┘ │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │       GarbageCollector (internal)          │ │
│  │   (implements IGarbageCollector)           │ │
│  │                                             │ │
│  │   Algorithm: Mark-Sweep                    │ │
│  │   ├─ Phase 1: Clear all marks             │ │
│  │   ├─ Phase 2: Mark from roots             │ │
│  │   ├─ Phase 3: Find unreachable            │ │
│  │   └─ Phase 4: Collect (free indices)      │ │
│  └────────────────────────────────────────────┘ │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │       BecomeHandler (internal)             │ │
│  │   (implements IBecomeHandler)              │ │
│  │                                             │ │
│  │   Swaps:                                   │ │
│  │   ├─ ObjectTable entries                   │ │
│  │   └─ Updates all references in heap        │ │
│  └────────────────────────────────────────────┘ │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │       ObjectTableEntry (metadata)          │ │
│  │                                             │ │
│  │   Fields:                                  │ │
│  │   ├─ ClassRef (OOP of object's class)     │ │
│  │   ├─ Format (ObjectFormat enum)           │ │
│  │   ├─ InstanceVariableCount                │ │
│  │   ├─ IndexedSize                           │ │
│  │   ├─ InstanceVariableStorage (OOP[])      │ │
│  │   ├─ ManagedStorage (byte[]/ushort[])     │ │
│  │   └─ IsMarked (GC mark bit)               │ │
│  └────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
                    ↓ depends on
┌─────────────────────────────────────────────────┐
│   Smalltalk80.Interpreter.Shared.Types          │
│                                                  │
│   - SmalltalkOOP (tagged value: LSB=0 object,   │
│                   LSB=1 SmallInteger)            │
│   - ObjectFormat (Pointer, PointerIndexed,      │
│                   ByteIndexed, WordIndexed)      │
│   - FormatWord (bit layout for class formats)   │
│   - GCStatistics (collection results)            │
└─────────────────────────────────────────────────┘
```

---

## Key Architectural Principles

### 1. Placeholder Nil Pattern

**Concept**: Heap manager creates a "placeholder nil" OOP at initialization, which is swapped with the real UndefinedObject instance during cold start.

**Timeline**:
1. **Heap Initialization**: `SmalltalkHeapManager()` constructor allocates placeholder nil at index 1, OOP = `0x0002` (tagged object pointer)
2. **Cold Start (Phases 1-5)**: All code uses `heapManager.GetNil()` to get placeholder nil
3. **Phase 3**: Creates real `UndefinedObject` instance
4. **Phase 3**: Calls `heapManager.Become.BecomeTwoWay(placeholderNil, realNil)` to swap identities
5. **Phase 6 GC**: Placeholder nil is now unreachable (no references) and is collected

**Key Point**: `GetNil()` always returns the OOP that semantically represents nil, which is the placeholder before become: and the real UndefinedObject after.

**Expected GC Result**: Phase 6 GC collects exactly **1 object** (the unreachable placeholder nil).

### 2. Runtime vs Cold Start API Separation

**Runtime Code** (VM, primitives, context management):
- **MUST** use `VMConstants` for field indices (e.g., `CONTEXT_SENDER_INDEX = 0`)
- **CANNOT** use `LayoutResolver` or `ClassHierarchyTraversal`
- **MUST** use `heapManager.GetNil()`, never `GlobalRegistry.Get("nil")`

**Cold Start Code** (loader phases):
- **MAY** use `LayoutResolver` to look up field indices dynamically
- **MAY** use `ClassHierarchyTraversal` for inheritance calculations
- **MUST** use `heapManager.GetNil()` for nil OOP

**Example**:
```csharp
// WRONG (runtime using LayoutResolver)
int fieldIndex = layoutResolver.GetFieldIndex("class:MethodContext", "sender");

// CORRECT (runtime using VMConstants)
const int SENDER_INDEX = VMConstants.CONTEXT_SENDER_INDEX;
```

### 3. Write Barriers

**Purpose**: Ensure GC sees all reference updates for concurrent/incremental collection safety

**Two Variants**:
- `SetInstanceVariableWithBarrier()` - **REQUIRED** for all runtime mutations
- `SetInstanceVariableNoBarrier()` - **ONLY** for single-threaded cold start (before GC is active)

**Example**:
```csharp
// Cold Start (Phase 3 allocation) - no GC yet
heapManager.SetInstanceVariableNoBarrier(objectOOP, 0, value);

// Runtime (primitives, VM) - GC may be active
heapManager.SetInstanceVariableWithBarrier(objectOOP, 0, value);
```

### 4. Object Table Structure

**Canonical Mapping**: 1-based indices (reserve 0 for null/special cases)

**Block Organization**:
- Each `ObjectTableBlock` contains **256 entries**
- `_nextObjectTableIndex` starts at 1, increments sequentially
- Block index = `(tableIndex - 1) / 256`
- Entry index = `(tableIndex - 1) % 256`

**Free List**:
- When GC collects object at index N:
  1. Clear ObjectTable entry: `SetObjectTableEntry(N, null)`
  2. Push index to free stack: `_freeObjectTableIndices.Push(N)`
- Next allocation reuses freed indices before allocating new ones

**Critical GC Fix** (2025-11-21):
```csharp
// BEFORE (BUG - double-free):
entry.ManagedStorage = null;
_tableManager.FreeObjectTableIndex(objectId);

// AFTER (CORRECT):
entry.ManagedStorage = null;
_tableManager.SetObjectTableEntry(objectId, null!);  // Remove from table first
_tableManager.FreeObjectTableIndex(objectId);        // Then add to free list
```

Without removing the entry first, the same object can be collected twice, causing duplicate indices in the free list.

### 5. FormatWord Bit Layout

**WARNING**: This layout **differs from Blue Book Chapter 27**. It matches the actual Smalltalk-80.st source code.

**Bit Layout**:
- **Bits 0-7**: `fixedFields` (number of instance variables, 0-255)
- **Bits 8-11**: Reserved
- **Bit 12**: `isIndexable` (1 = has indexed fields, 0 = fixed size only)
- **Bit 13**: `isWords` (1 = word-addressed, 0 = byte-addressed)
- **Bit 14**: `isPointers` (1 = contains pointers, 0 = contains values)

**Type Flag Combinations**:
```
0x4000 = Fixed pointer object (subclass:)
0x5000 = Pointer-indexed (variableSubclass:)
0x3000 = Word-indexed (variableWordSubclass:)
0x1000 = Byte-indexed (variableByteSubclass:)
```

**Example**:
```csharp
var formatWord = new FormatWord(0x5006);  // PointerIndexed class with 6 instance variables
// fixedFields = 6 (bits 0-7)
// isIndexable = true (bit 12 set)
// isPointers = true (bit 14 set)
```

### 6. Init Expression Execution Model

**Architecture**: Each init expression gets a fresh Process and MethodContext, executes directly (no scheduling), returns to C# when complete.

**Steps per Expression**:
1. **Create MethodContext**: Fresh context with 6 instance variables + indexed slots for temps/stack
2. **Create Process**: Fresh process with 4 instance variables (includes inherited `nextLink` from Link)
3. **Execute**: VM runs bytecodes until `sender == nil` (top-level return)
4. **Return**: Control returns to C# synchronously
5. **Abandon**: Process and MethodContext become unreachable, collected by next GC

**No Process Reuse**: Each expression is independent, no state carried between expressions.

**No Scheduling**: Bypasses ProcessorScheduler completely - direct execution from C#.

---

## ObjectFormat Enum

Four memory layouts for different object types:

| Format | Value | Storage | Fixed Slots | Indexed Storage | Examples |
|--------|-------|---------|-------------|-----------------|----------|
| Pointer | 0 | OOPs only | Yes | No | Point, Association, Process |
| PointerIndexed | 1 | OOPs | Yes | OOP array | Array, MethodContext, BlockContext |
| ByteIndexed | 2 | Bytes | Yes | byte array | String, ByteArray, Float (8 bytes) |
| WordIndexed | 3 | Words | Yes | ushort array | WordArray, Bitmap |

**Note**: All formats have fixed slots for instance variables (always stored as OOPs). The indexed portion uses format-specific storage.

---

## GC Algorithm Details

### Mark-Sweep Collection

**Phase 1: Clear Marks**
```csharp
foreach (var block in _objectTableBlocks)
    foreach (var entry in block.Entries)
        if (entry != null)
            entry.IsMarked = false;
```

**Phase 2: Mark Reachable**
```csharp
Stack<SmalltalkOOP> workStack = new();
foreach (var root in gcRoots)
    workStack.Push(root);

while (workStack.Count > 0)
{
    var oop = workStack.Pop();
    var entry = GetObjectTableEntry(oop);

    if (entry.IsMarked) continue;
    entry.IsMarked = true;

    // Mark instance variables (always OOPs)
    foreach (var fieldOOP in entry.InstanceVariableStorage)
        if (!fieldOOP.IsSmallInteger)
            workStack.Push(fieldOOP);

    // Mark indexed OOPs (PointerIndexed only)
    if (entry.Format == ObjectFormat.PointerIndexed)
        foreach (var indexedOOP in GetIndexedOOPs(entry))
            if (!indexedOOP.IsSmallInteger)
                workStack.Push(indexedOOP);
}
```

**Phase 3: Find Unreachable**
```csharp
List<int> unreachable = new();
foreach (var block in _objectTableBlocks)
    foreach (var entry in block.Entries)
        if (entry != null && !entry.IsMarked)
            unreachable.Add(entry.ObjectId);
```

**Phase 4: Collect**
```csharp
foreach (var objectId in unreachable)
{
    var entry = GetObjectTableEntry(objectId);

    // Free managed storage
    entry.ManagedStorage = null;
    entry.InstanceVariableStorage = null;

    // Remove from ObjectTable (CRITICAL - prevents double-free)
    SetObjectTableEntry(objectId, null);

    // Add index to free list for reuse
    FreeObjectTableIndex(objectId);
}
```

### GC Roots

**Registered Roots** (via `AddGCRoot()`):
- Active context chain (sender links)
- Special objects array
- Smalltalk global dictionary
- Symbol table (USTable)
- Any additional VM roots

**SmallIntegers**: Not marked (they are immediate values, not heap-allocated)

---

## Become Operations

### BecomeTwoWay(oop1, oop2)

**Purpose**: Implements Blue Book primitive 72 - swaps identities of two objects

**Algorithm**:
1. Get ObjectTable entries for both OOPs
2. Swap the entire `ObjectTableEntry` contents in the ObjectTable
3. Walk entire heap, updating all references:
   - If reference points to oop1 → change to oop2
   - If reference points to oop2 → change to oop1

**Usage Example** (Phase 3 nil swap):
```csharp
var placeholderNil = heapManager.GetNil();        // OOP = 0x0002
var realNil = // ... create UndefinedObject ...   // OOP = 0x1234

// Swap identities - all references to placeholder now point to real nil
heapManager.Become.BecomeTwoWay(placeholderNil, realNil);

// After become:
heapManager.GetNil();  // Still returns 0x0002, but now points to real UndefinedObject data
```

**Key Point**: The OOP value stays the same, but the ObjectTable entry contents (class, format, storage) are swapped.

---

## Performance Characteristics

### Allocation

**Fast Path**: Reuse freed index from free stack - O(1)
```csharp
if (_freeObjectTableIndices.Count > 0)
    return _freeObjectTableIndices.Pop();  // Instant
```

**Slow Path**: Allocate new block if needed - O(1) amortized
```csharp
int blockIndex = (_nextObjectTableIndex - 1) / 256;
if (blockIndex >= _objectTableBlocks.Count)
    _objectTableBlocks.Add(new ObjectTableBlock());  // 256 entries at once
```

### GC

**Mark Phase**: O(reachable objects) - traverses only reachable objects via work stack

**Sweep Phase**: O(total objects) - must check every ObjectTable entry

**Typical Phase 6 GC**: ~32,000 objects marked, 1 collected (placeholder nil), <10ms

### Object Access

**GetInstanceVariable**: O(1) - direct array index
```csharp
return entry.InstanceVariableStorage[index];  // Direct access
```

**GetIndexedOOP**: O(1) - direct array index
```csharp
return ((SmalltalkOOP[])entry.ManagedStorage!)[index];
```

---

## Integration with VM

### VM Dependencies on Heap Manager

**Bytecode Interpreter**:
- `AllocateInstanceOf()` - For `send` bytecodes (implicit new)
- `GetInstanceVariable()`, `SetInstanceVariable()` - For field access bytecodes
- `GetIndexedOOP()`, `SetIndexedOOP()` - For `at:` and `at:put:` bytecodes

**Primitives** (110 total):
- Arithmetic (1-39): Use heap for Large Integer operations
- Float (40-58): Use `GetIndexedByte()` for IEEE 754 double storage
- Object access (60-67): Use `GetIndexedOOP()`, `SetIndexedOOP()`, `GetIndexedSize()`
- Control (80-84): Use heap for BlockContext creation
- Instance query (77-78): Use `FindInstanceOf()`, `FindNextInstanceOf()`

**Context Manager**:
- `AllocateObject(ObjectFormat.PointerIndexed, 6, stackSize)` - MethodContext creation
- `GetInstanceVariable(context, VMConstants.CONTEXT_SENDER_INDEX)` - Context chain traversal

**Message Dispatcher**:
- `AllocateInstanceOf(classOOP, 0)` - Instance creation for message sends
- `GetInstanceVariable(class, VMConstants.BEHAVIOR_METHODDICT_INDEX)` - Method lookup

### Cold Start Dependencies

**Phase 2 (Class Infrastructure)**:
- `AllocateObject(ObjectFormat.Pointer, 3, 0)` - Create Behavior/Class objects
- `SetInstanceVariableNoBarrier()` - Initialize class fields

**Phase 3 (Object Allocation)**:
- `AllocateObject()` for all object types
- `BecomeTwoWay()` for nil swap

**Phase 4 (Method Compilation)**:
- `AllocateObject(ObjectFormat.ByteIndexed, ...)` - CompiledMethod bytecode storage

**Phase 6 (Validation & GC)**:
- `GC.GarbageCollect()` - First collection after cold start
- Expects exactly 1 collected object (placeholder nil)

---

## Validation and Debugging

### Phase 6 VMConstants Validation

**Purpose**: Catch Smalltalk source changes that require VM updates

**Validates 27 constants across 9 classes**:
```csharp
ValidateConstant("class:MethodContext", "sender", VMConstants.CONTEXT_SENDER_INDEX);
ValidateConstant("class:MethodContext", "pc", VMConstants.CONTEXT_PC_INDEX);
ValidateConstant("class:MethodContext", "stackp", VMConstants.CONTEXT_STACKP_INDEX);
ValidateConstant("class:MethodContext", "method", VMConstants.CONTEXT_METHOD_INDEX);
ValidateConstant("class:MethodContext", "lexicalParent", VMConstants.CONTEXT_LEXICALPARENT_INDEX);
ValidateConstant("class:MethodContext", "receiver", VMConstants.CONTEXT_RECEIVER_INDEX);

ValidateConstant("class:Process", "nextLink", VMConstants.PROCESS_NEXTLINK_INDEX);
ValidateConstant("class:Process", "suspendedContext", VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX);
ValidateConstant("class:Process", "priority", VMConstants.PROCESS_PRIORITY_INDEX);
ValidateConstant("class:Process", "myList", VMConstants.PROCESS_MYLIST_INDEX);

// ... 17 more validations for BlockContext, Association, Dictionary, Metaclass, Behavior
```

**Failure**: Throws exception with details about mismatch - coldstart must be regenerated with updated VMConstants

### Error Messages

**Instance Variable Access**:
```
Instance variable index 5 out of range [0, 3] for object oop=13500 (MethodContext)
```
- Shows: invalid index, valid range, OOP, class name
- Cannot show field name (index is invalid, can't look it up)

**Invalid OOP**:
```
Invalid OOP: 0x1234 (not found in ObjectTable)
```

**Format Mismatch**:
```
Expected PointerIndexed format for GetIndexedOOP, got ByteIndexed
```

### VerboseDebugMode

**When Enabled** (`heapManager.VerboseDebugMode = true`):
- Logs every allocation with OOP and class name
- Logs every GC root addition/removal
- Logs every become operation with OOP pairs
- Logs mark/sweep phases with object counts

**When Disabled** (default):
- Silent operation, only errors/warnings

---

## Testing

### BlockContext Regression Test (MANDATORY)

**After ANY heap manager, compiler, or VM change**:
```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected**: Returns `1191` (120 BlockContext tests pass)

**Validates**:
- Object allocation correctness
- Context creation (MethodContext, BlockContext)
- Instance variable access
- Indexed slot access
- GC doesn't corrupt active objects
- Become doesn't break references

### Unit Tests (85 tests)

```bash
cd Smalltalk80.Interpreter.Tests
dotnet test --filter "Category=HeapManager"
```

**Expected**: `total: 85, failed: 0, succeeded: 85`

**Coverage**:
- Object allocation (all 4 formats)
- Instance variable access (boundary conditions)
- Indexed access (OOP, byte, word)
- GC marking and collection
- Become operations
- ObjectTable management
- Free list reuse

---

## Related Documents

- [PHYSICAL-ARCHITECTURE.md](PHYSICAL-ARCHITECTURE.md) - File system and assembly organization
- [IMPLEMENTATION-ARCHITECTURE.md](IMPLEMENTATION-ARCHITECTURE.md) - VM execution model
- [DEVELOPMENT-GUIDE.md](DEVELOPMENT-GUIDE.md) - How to extend the interpreter
- [../Block-Context-And-Variable-Access-COMPREHENSIVE.md](../Block-Context-And-Variable-Access-COMPREHENSIVE.md) - Unified temp frame architecture

---

## Historical Notes

### Heap Manager Refactoring (2025-11)

**Goal**: Separate heap manager into its own assembly with clean interfaces

**Changes**:
1. Created `Smalltalk80.Interpreter.HeapManager` assembly
2. Created `Smalltalk80.Interpreter.Shared.Types` for shared value types
3. Split into three interfaces: `ISmalltalkHeap`, `IGarbageCollector`, `IBecomeHandler`
4. Moved 67 `GlobalRegistry.Get("nil")` calls to `heapManager.GetNil()`
5. Removed LayoutResolver from all runtime code (ContextManager, etc.)
6. Fixed GC double-free bug (remove entries before freeing indices)
7. Fixed Process format (was PointerIndexed, should be Pointer)
8. Fixed Process instance variable count (3 → 4, includes inherited nextLink)
9. Added Phase 6 VMConstants validation (27 constants across 9 classes)
10. Updated Phase 6 GC validation (expect 1 collected object, not 0)

**Result**: ✅ Clean separation, zero warnings, 44/46 init expressions pass, 120/120 BlockContext tests pass
