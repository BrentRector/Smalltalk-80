# Smalltalk-80 Interpreter Runtime Architecture

**Last Updated**: 2025-01-15  
**Status**: 🎉 **Perfect GC - Zero Unreachable Objects** - 15/15 phases working  
**Heap Manager**: ✅ Perfect GC (0 unreachable objects) with bulletproof allocation architecture  
**VM Core**: 🎉 Complete cold start success - 15/15 phases working with 100% validation  
**Primitive Coverage**: ✅ Enhanced with objectAt:/at: access patterns  
**Schema Version**: v1.0.3 (Blue Book-compliant with separate ByteArray design)

## Executive Summary

This document provides the canonical architectural documentation for the Smalltalk-80 interpreter runtime system. The interpreter implements a table-based object memory with proper Blue Book compliance and bulletproof allocation architecture. The system achieves **complete cold start success** with all 15 phases working and comprehensive object validation.

**🎉 Key Achievement**: The runtime system has achieved **perfect garbage collection** with 0 unreachable objects out of 31,927 total objects. All 15 cold start phases work correctly, including ProcessorScheduler singleton creation, optimized GC root configuration, and dynamic class name resolution. The literal handling double-tagging bug has been eliminated, achieving 100% object reachability.

## Table of Contents

1. [Runtime System State](#runtime-system-state)
2. [Architecture Layers](#architecture-layers)
3. [Heap Manager Architecture](#heap-manager-architecture)
4. [Object Memory Architecture](#object-memory-architecture)
5. [Object Model](#object-model)
6. [Garbage Collection](#garbage-collection)
7. [Bytecode Execution](#bytecode-execution)
8. [Primitive Operations](#primitive-operations)
9. [BlockContext Support](#blockcontext-support)
10. [Graphics Support](#graphics-support)
11. [Process and Scheduling Infrastructure](#process-and-scheduling-infrastructure)
12. [Blue Book Compliance](#blue-book-compliance)
13. [Multi-VM Architecture](#multi-vm-architecture)
14. [Key Design Principles](#key-design-principles)
15. [Technical Specifications](#technical-specifications)

## Runtime System State

**Last Updated**: 2025-01-15  
**Status**: 🎉 **Perfect GC Achievement** - 0 unreachable objects, 15/15 phases complete

### Current Achievements

✅ **Perfect Garbage Collection**: 0 unreachable objects out of 31,927 allocated  
✅ **Literal Handling Bug Fixed**: Eliminated double-tagging by using SmalltalkOOP[] instead of int[]  
✅ **NULL_CLASS Bug Eliminated**: Bulletproof allocation API prevents class reference bugs  
✅ **ProcessorScheduler Singleton**: Created and properly initialized for future process management  
✅ **GC Root Optimization**: Reduced to 4 fundamental roots (nil, false, true, Smalltalk)  
✅ **Dynamic Class Name Resolution**: Class name field index determined from coldstart.json  
✅ **Complete Phase Success**: All 15 cold start phases working with 100% validation  
✅ **Clean Architecture**: Multi-pass initialization with optimized phase ordering

### Cold Start Phase Status (15/15 Complete)

**BOOTSTRAP PHASES (0-5): All Working ✅**
- Phase 0: Preflight Validation ✅
- Phase 1: Symbol Interning ✅
- Phase 2: Class Shells ✅
- Phase 3: Metaclass Wiring ✅
- Phase 4: Class Resolution ✅
- Phase 5: Layout Computation ✅

**MULTI-PASS INITIALIZATION: All Working ✅**
- Phase 6: Object Allocation (creates nil/true/false/ProcessorScheduler singletons) ✅
- Phase X: Class Reference Initialization ✅
- Phase Y: Instance Variable Initialization ✅

**FULL ALLOCATION + INITIALIZATION (7-14): All Working ✅**
- Phase 7: Method Dictionary Allocation ✅
- Phase 8: Method Installation ✅
- Phase 9: Method Dictionary Installation ✅
- Phase 10: Patch Application ✅
- Phase 11: USTable Population ✅
- Phase 12: Global Variable Installation ✅
- Phase 13: Comprehensive Validation ✅ (10,535 objects validated!)
- Phase 14: GC Root Setup ✅
- Phase 15: GC Verification ✅ (0 unreachable objects!)

### Build Status

```bash
# Build and run interpreter
dotnet build  # ✅ 0 errors, 2 warnings (non-critical)
dotnet run --project Smalltalk80.Interpreter -- --coldstart bin/Debug/net9.0/coldstart.json

# Expected output:
# === Cold Start Complete ===
# All phases executed successfully and all objects reachable from GC roots.
# 
# ✓ Cold start loader test PASSED!
```

### GC Verification Output

```
Phase 15: GC Verification...
  GC Results: 0/31,927 objects collected (0.0% efficiency) in 9.96ms
  Object table size: 31,927
  Active objects: 31,927
✓

=== Cold Start Complete ===
All phases executed successfully and all objects reachable from GC roots.
```

## Architecture Layers

### System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Smalltalk Application Layer             │
│  Classes, Methods, Objects, Message Sends, Block Closures  │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                     Virtual Machine Core                   │
│    Bytecode Interpreter, Method Dispatch, Primitive Ops    │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                    Object Memory System                    │
│      Heap Manager, GC, Tagged OOPs, Object Table          │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                   .NET Runtime Foundation                  │
│        Memory Management, JIT, Platform Abstraction        │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

#### 1. Virtual Machine Core
**Location**: `Smalltalk80.Interpreter/VM/`  
**Responsibilities**:
- Bytecode interpretation and execution
- Method lookup and dispatch with inheritance
- Primitive operation invocation
- Stack management and execution context
- BlockContext creation and management

#### 2. Object Memory System  
**Location**: `Smalltalk80.Interpreter/Core/HeapManager/`  
**Responsibilities**:
- Low-level object allocation and access
- Tagged OOP management for SmallIntegers
- ObjectTable indirection for heap objects
- Garbage collection with root management
- Format-specific type safety

#### 3. Cold Start System
**Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/`  
**Documentation**: See `COLD-START-DESIGN-AND-IMPLEMENTATION.md`  
**Responsibilities**:
- JSON bundle processing and validation
- 10-phase bootstrap process
- Object memory initialization
- Class hierarchy establishment

## Heap Manager Architecture

### Modern Heap Manager Design

The heap manager provides a clean, low-level API completely separated from Smalltalk semantics. It uses tagged OOPs for SmallIntegers and ObjectTable indirection for heap objects.

#### ISmalltalkHeapManager Interface

```csharp
public interface ISmalltalkHeapManager
{
    // Object Allocation
    SmalltalkOOP AllocateObject(SmalltalkOOP classOOP, ObjectFormat format, 
                               int fixedSlots, int indexedSize);
    
    // Instance Variable Access (always OOPs)
    SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);
    void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);
    
    // Format-Specific Indexed Access
    SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);        // Arrays
    byte GetIndexedByte(SmalltalkOOP objectOOP, int index);               // Strings/Floats  
    ushort GetIndexedWord(SmalltalkOOP objectOOP, int index);             // Bitmaps
    
    // Object Metadata
    SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP);
    ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP);
    int GetInstanceVariableCount(SmalltalkOOP objectOOP);
    int GetIndexedSize(SmalltalkOOP objectOOP);
    
    // Dynamic Configuration
    void SetClassNameFieldIndex(int index);
    string GetClassName(SmalltalkOOP objectOOP);
    
    // GC Root Management
    void SetGCRoots(SmalltalkOOP[] rootObjects);
    void AddGCRoots(SmalltalkOOP[] additionalRoots);
    
    // Garbage Collection
    GCStatistics GarbageCollect();
    GCStatistics GetGCStatistics();
}
```

#### Tagged OOP System

```csharp
public readonly struct SmalltalkOOP
{
    private readonly int _value;
    
    public bool IsSmallInteger => (_value & 1) == 1;
    public bool IsObjectPointer => (_value & 1) == 0;
    
    public int AsSmallInteger() => _value >> 1;
    public int AsObjectTableIndex() => _value >> 1;
    
    public static SmalltalkOOP FromSmallInteger(int value) => new((value << 1) | 1);
    public static SmalltalkOOP FromObjectTableIndex(int index) => new(index << 1);
}
```

#### ObjectFormat Enumeration

```csharp
public enum ObjectFormat : byte
{
    Pointer = 0,          // Fixed instance variables only (Object, Class)
    PointerIndexed = 1,   // Instance variables + indexed OOPs (Array, OrderedCollection, CompiledMethod)
    ByteIndexed = 2,      // Instance variables + indexed bytes (String, ByteArray, Float)
    WordIndexed = 3,      // Instance variables + indexed words (Bitmap, WordArray)
}
```

### Key Architectural Benefits

1. **Semantic Isolation**: Heap manager has no knowledge of Smalltalk classes or special objects
2. **Type Safety**: Format-specific access methods prevent wrong data type access  
3. **Tagged Pointers**: SmallIntegers stored directly in OOPs for efficiency
4. **Bounds Checking**: All array accesses validated against object bounds
5. **Flexible GC**: Configurable root management system

## Object Memory Architecture

### Table-Based Design

The object memory uses ObjectTable indirection to enable garbage collection without moving objects:

```
OOP (32-bit)              ObjectTableEntry              Object Memory
┌─────────────┐          ┌──────────────────┐          ┌─────────────┐
│ Index << 1  │ ─────────▶│ Memory Pointer   │ ────────▶│ Object Data │
│ (LSB = 0)   │          │ PackedMetadata   │          │             │
└─────────────┘          └──────────────────┘          └─────────────┘

SmallInteger OOP
┌─────────────┐
│ Value << 1  │
│ (LSB = 1)   │  ────────── Direct value encoding
└─────────────┘
```

### ObjectTable Structure

```csharp
public class ObjectTableEntry
{
    public IntPtr MemoryPointer { get; }      // Pinned object memory
    public PackedMetadata Metadata { get; }   // Format + size information  
    public bool IsMarked { get; set; }        // GC mark bit
    
    public ObjectFormat Format => Metadata.Format;
    public int SizeInWords => Metadata.SizeInWords;
    public int SizeInBytes => SizeInWords * 4;
}
```

### PackedMetadata Encoding

```csharp
public readonly struct PackedMetadata
{
    private readonly uint _packed;
    
    // Bits 0-3: ObjectFormat (4 bits)
    // Bits 4-11: Size in words (8 bits, max 255 words)
    // Bits 12-31: Reserved for future use
    
    public ObjectFormat Format => (ObjectFormat)(_packed & 0xF);
    public int SizeInWords => (int)((_packed >> 4) & 0xFF);
}
```

### Object Growth Strategy

```csharp
private readonly List<ObjectTableBlock> _objectTableBlocks = new();
private const int ObjectTableBlockSize = 1024;

// Allocate new blocks as needed
private int AllocateObjectTableIndex()
{
    if (_freeObjectTableIndices.Count > 0)
        return _freeObjectTableIndices.Pop();
        
    // Grow table if needed
    if (_nextObjectTableIndex >= _objectTableBlocks.Count * ObjectTableBlockSize)
        _objectTableBlocks.Add(new ObjectTableBlock());
        
    return _nextObjectTableIndex++;
}
```

## Object Model

### Base Object System

All objects in Smalltalk-80 follow a consistent structure:

```
Object Layout:
┌─────────────────┐ ← Object Header (1 word)
│ Class OOP       │   Points to the object's class
├─────────────────┤ ← Instance Variables (0+ words)  
│ Instance Var 0  │   Fixed-size named slots
│ Instance Var 1  │
│ ...             │
├─────────────────┤ ← Indexed Data (0+ words)
│ Indexed Data 0  │   Variable-size data (format-dependent)
│ Indexed Data 1  │
│ ...             │
└─────────────────┘
```

### Class System

The class system establishes the fundamental Smalltalk object model:

```
Fundamental Inheritance Chain:
Object ← Behavior ← ClassDescription ← Class ← Metaclass

Metaclass Circle (The Bootstrap Triangle):
meta:Object → class:Class → class:ClassDescription → class:Object → meta:Object
```

#### Class Object Fields

```csharp
// Class instance variables (from Smalltalk-80 specification)
public class ClassLayout
{
    public SmalltalkOOP superclass;        // Parent class
    public SmalltalkOOP methodDictionary;  // Methods available to instances
    public SmalltalkOOP format;            // Instance format specification
    public SmalltalkOOP name;              // Class name symbol
    public SmalltalkOOP comment;           // Class documentation
    public SmalltalkOOP instanceVariables; // Instance variable names
}
```

### Value Types (Immediates)

SmallIntegers are stored directly in OOPs using tagged encoding:

```csharp
// SmallInteger range: -1,073,741,824 to 1,073,741,823 (30-bit signed)
public static bool IsSmallInteger(int value) => 
    value >= -0x40000000 && value <= 0x3FFFFFFF;

// Encoding/decoding
public static int EncodeSmallInteger(int value) => (value << 1) | 1;
public static int DecodeSmallInteger(int encoded) => encoded >> 1;
```

### Collection Types

#### Array (PointerIndexed)
```csharp
// Array layout: [classOOP] [size] [element0] [element1] ...
var arrayOOP = heapManager.AllocateObject(
    classOOP: arrayClassOOP,
    format: ObjectFormat.PointerIndexed,
    instanceVariableCount: 0,
    indexedSize: elementCount
);

// Access elements
var element = heapManager.GetIndexedOOP(arrayOOP, index);
heapManager.SetIndexedOOP(arrayOOP, index, elementOOP);
```

#### String (ByteIndexed)
```csharp
// String layout: [classOOP] [size] [byte0] [byte1] ...
var stringOOP = heapManager.AllocateObject(
    classOOP: stringClassOOP,
    format: ObjectFormat.ByteIndexed,
    instanceVariableCount: 0,
    indexedSize: utf8Bytes.Length
);

// Access characters as bytes
var byteValue = heapManager.GetIndexedByte(stringOOP, index);
heapManager.SetIndexedByte(stringOOP, index, utf8Byte);
```

#### CompiledMethod (PointerIndexed + Separate ByteArray)

**Architecture Decision**: Due to C#'s difficulty handling heterogeneous objects, we implement CompiledMethod as PointerIndexed with a separate ByteArray for bytecodes, based on analysis of actual Smalltalk-80.st source code.

**Physical C# Layout**:
```
[classOOP, header, literal1, literal2, ..., literalN, bytecodeArrayOOP]
 slot 0    slot 1   slot 2    slot 3         slot N+1   slot N+2
 (hidden)  (objectAt: 1)      (objectAt: 2)            (hidden)
```

**Separate ByteArray**: `[byte1, byte2, byte3, ...]` accessed via `at: 1`, `at: 2`, etc.

**Primitive Access Patterns** (from Smalltalk-80.st analysis):
```csharp
// Header access: "^self objectAt: 1"
public SmalltalkOOP ObjectAt(SmalltalkOOP methodOOP, int smalltalkIndex)
{
    // objectAt: 1 → header (our indexed slot 0)
    // objectAt: 2 → literal1 (our indexed slot 1)
    int csharpIndex = smalltalkIndex - 1; // 1-based to 0-based
    return heapManager.GetIndexedOOP(methodOOP, csharpIndex);
}

// Bytecode access: "self at: index"
public byte At(SmalltalkOOP methodOOP, int smalltalkIndex)
{
    // at: 1 → first bytecode (via hidden ByteArray reference)
    int csharpIndex = smalltalkIndex - 1;
    var bytecodeArrayOOP = GetHiddenBytecodeArray(methodOOP);
    return heapManager.GetIndexedByte(bytecodeArrayOOP, csharpIndex);
}
```

**Benefits**:
- ✅ C#-friendly design avoiding heterogeneous objects
- ✅ Proper Smalltalk primitive access semantics maintained
- ✅ GC safety: Tagged SmallInteger headers ignored during traversal
- ✅ Hidden references provide clean separation of concerns

**Allocation**:
```csharp
var methodOOP = semanticAllocator.AllocateCompiledMethod(
    "CompiledMethod", headerWord, literalIds, bytecodes);
// Creates both CompiledMethod and separate ByteArray automatically
```

## Garbage Collection

### Perfect GC Achievement (2025-01-15)

The system achieves **perfect garbage collection** with 0 unreachable objects:

```bash
# GC Verification Output
Phase 15: GC Verification...
  GC Results: 0/31,927 objects collected (0.0% efficiency) in 9.96ms
  Object table size: 31,927
  Active objects: 31,927
✓

=== Cold Start Complete ===
All phases executed successfully and all objects reachable from GC roots.
```

**How Perfect GC Was Achieved**:
1. **Eliminated Double-Tagging Bug**: Changed literal handling from `int[]` to `SmalltalkOOP[]`
2. **Proper OOP Type Safety**: All methods use `SmalltalkOOP` instead of raw `int`
3. **Correct GC Root Configuration**: 4 fundamental roots (nil, false, true, Smalltalk)
4. **Global Variable Installation**: USTable and Processor accessible via SystemDictionary
5. **NULL_CLASS Bug Fix**: Bulletproof allocation API prevents class reference bugs

### Modern GC Root Management

The heap manager provides flexible GC root management with minimal fundamental roots:

```csharp
// Configure GC roots during bootstrap (Loader.cs:195-201)
var gcRootsList = new List<(string name, int objectId)>
{
    ("nil", GlobalRegistry.NilObjectId),
    ("false", GlobalRegistry.FalseObjectId),
    ("true", GlobalRegistry.TrueObjectId),
    ("Smalltalk", GlobalRegistry.SystemDictionaryId)
};

// Convert to OOPs and set as GC roots
var gcRoots = gcRootsList.Select(r => SmalltalkOOP.FromObjectTableIndex(r.objectId)).ToArray();
_heapManager.SetGCRoots(gcRoots);

// Perform collection with detailed statistics  
var stats = _heapManager.GarbageCollect();
Console.WriteLine($"Collected: {stats.CollectedObjects}, Live: {stats.LiveObjects}");
```

### Mark-and-Sweep Algorithm

```csharp
public GCStatistics GarbageCollect()
{
    // Phase 1: Clear all mark bits
    foreach (var block in _objectTableBlocks)
        foreach (var entry in block.Entries)
            if (entry != null) entry.IsMarked = false;
    
    // Phase 2: Mark from configured roots
    foreach (var rootOOP in _gcRoots)
        MarkObjectRecursively(rootOOP);
    
    // Phase 3: Sweep unmarked objects
    int collected = 0;
    foreach (var block in _objectTableBlocks)
    {
        for (int i = 0; i < block.Entries.Length; i++)
        {
            var entry = block.Entries[i];
            if (entry != null && !entry.IsMarked)
            {
                block.Entries[i] = null;
                block.ActiveCount--;
                collected++;
            }
        }
    }
    
    return new GCStatistics { CollectedObjects = collected, ... };
}
```

### GC Benefits

- **No object copying**: Objects never move in memory
- **Simple become: operation**: Copy MemoryPointer between ObjectTable entries
- **No forwarding pointers**: Never needed since objects don't move
- **Concurrent friendly**: Can mark ObjectTable entries safely
- **Leverages .NET**: Let .NET handle actual memory management
- **Perfect Reachability**: 100% of allocated objects reachable from GC roots

## Bytecode Execution

### Bytecode Format Overview

The Smalltalk-80 bytecode instruction set consists of 256 instructions (0x00-0xFF):

| Range | Operation | Description |
|-------|-----------|-------------|
| 0-15 | Push Receiver Variable | Push instance variable 0-15 |
| 16-31 | Push Temporary Variable | Push temporary/argument 0-15 |
| 32-63 | Push Literal Constant | Push literal 0-31 (skip header at lit[0]) |
| 64-95 | Push Literal Variable | Push Association value 0-31 (skip header) |
| 96-103 | Pop and Store Receiver Variable | Pop into inst var 0-7 |
| 104-111 | Pop and Store Temporary Variable | Pop into temp 0-7 |
| 112-119 | Push Special | receiver, true, false, nil, -1, 0, 1, 2 |
| 120-127 | Return | Return various values to sender |
| 128-175 | Extended Operations | Send literal selector with 0-2 args |
| 176-191 | Arithmetic Send | Hardcoded arithmetic operators |
| 192-207 | Special Send | Special selectors (at:, size, x, y, etc.) |
| 208-223 | Send Literal 0-15 (0 args) | Send literal selector with 0 args |
| 224-239 | Send Literal 0-15 (1 arg) | Send literal selector with 1 arg |
| 240-255 | Send Literal 0-15 (2 args) | Send literal selector with 2 args |

### Bytecode Execution — Full Semantics Reference

This table documents every Smalltalk-80 bytecode the loader/interpreter supports. Each row gives: opcode (hex), mnemonic, operand decoding (if any), stack effect (push/pop counts with receiver and arguments), and precise semantics required by a deterministic cold-start loader and VM.

**Notation**:
- "rcv" = receiver
- "lit[n]" = literal at index n in the method's literal frame (lit[0] is the method header)
- "temp[n]" = temporary/argument slot n
- "ivar[n]" = instance variable slot n
- "TOP" = top of the operand stack
- Stack effect format: (popped → pushed)

**Important**: Literal indices in bytecodes skip the header at lit[0]. When a bytecode references "literal N", it actually accesses lit[N+1] in the array.

| Opcode | Mnemonic | Operand / Decode | Stack Effect | Semantics |
|--------|----------|------------------|--------------|-----------|
| 0x00–0x0F | PUSH_IVAR_0..15 | low4 = ivar index (0–15) | ( — → rcv.ivar[n]) | Push value of instance variable ivar[low4] of the current receiver onto the operand stack. No side effects. |
| 0x10–0x1F | PUSH_TEMP_0..15 | low4 = temp index (0–15) | ( — → temp[n]) | Push value of temporary/argument slot low4 from the current frame onto the stack. If index addresses an argument, treat identically. |
| 0x20–0x3F | PUSH_LITERAL_CONST_0..31 | low5 = literal index (0–31) | ( — → lit[low5+1]) | Push the literal at index low5+1 (skipping header at lit[0]) from the method's literal frame onto the stack. Literal is the literal constant (symbol, number, compiled method, etc.). |
| 0x40–0x5F | PUSH_LITERAL_VAR_0..31 | low5 = literal index (0–31) | ( — → assocValue(lit[low5+1])) | Treat literal[low5+1] as an association (name → value); push the association's value (e.g., global variable value) onto the stack. If literal is not an association, raise loader/VM validation error. |
| 0x60–0x67 | POP_STORE_IVAR_0..7 | low3 = ivar index (0–7) | (val → ) | Pop the stack and store into instance variable ivar[low3] of the current receiver. No value is pushed. |
| 0x68–0x6F | POP_STORE_TEMP_0..7 | low3 = temp index (0–7) | (val → ) | Pop the stack and store into temporary/argument slot low3 in the current frame. |
| 0x70 | PUSH_SELF | none | ( — → rcv) | Push the current receiver (self) onto the operand stack. |
| 0x71 | PUSH_TRUE | none | ( — → true) | Push the true singleton object. |
| 0x72 | PUSH_FALSE | none | ( — → false) | Push the false singleton object. |
| 0x73 | PUSH_NIL | none | ( — → nil) | Push the nil object. |
| 0x74 | PUSH_MINUS_ONE | none | ( — → smallInt(-1)) | Push small integer -1. |
| 0x75 | PUSH_ZERO | none | ( — → smallInt(0)) | Push small integer 0. |
| 0x76 | PUSH_ONE | none | ( — → smallInt(1)) | Push small integer 1. |
| 0x77 | PUSH_TWO | none | ( — → smallInt(2)) | Push small integer 2. |
| 0x78 | RETURN_SELF | none | ( — → returned) | Return receiver (self) to sender. |
| 0x79 | RETURN_TRUE | none | ( — → returned) | Return true to sender. |
| 0x7A | RETURN_FALSE | none | ( — → returned) | Return false to sender. |
| 0x7B | RETURN_NIL | none | ( — → returned) | Return nil to sender. |
| 0x7C | RETURN_TOP | none | (val → returned) | Pop the top of the stack and return it to the sender. |
| 0x7D | BLOCK_RETURN_SELF | none | ( — → returned) | Return self from block context to caller's caller. |
| 0x7E | BLOCK_RETURN_TOP | none | (val → returned) | Pop and return from block context to caller's caller. |
| 0x7F | NOP | none | ( — → ) | No operation. |
| 0x80 | EXTENDED_PREFIX | next byte = subop | varies | Extended operation prefix. The next byte specifies the extended operation from ExtendedSubOp enum (0x00-0x19). See Extended Operations table below. |
| 0x81–0x88 | (reserved) | - | - | **Reserved/Unimplemented** - will throw NotImplementedException if encountered |
| 0x89 | PUSH_ACTIVE_CONTEXT | none | ( — → context) | Push the current execution context (used for block creation). Followed by blockCopy: send. |
| 0x8A–0x8F | (reserved) | - | - | **Reserved/Unimplemented** - will throw NotImplementedException if encountered |
| 0x90–0x97 | SHORT_JUMP_FWD_1..8 | none | ( — → ) | Unconditional forward jump by 1-8 bytes. Bytecode = 144 + (distance - 1). Used to jump around block bodies. |
| 0x98–0x9F | (reserved) | - | - | **Reserved/Unimplemented** - will throw NotImplementedException if encountered |
| 0xA0 | MEDIUM_JUMP_FWD | next byte = distance-1 | ( — → ) | Unconditional forward jump by 1-256 bytes. Jump distance = next_byte + 1. |
| 0xA1–0xA7 | LONG_JUMP_FWD_2..8 | next N bytes = distance-1 | ( — → ) | Long unconditional forward jump. N = bytecode - 159 (N=2 to 8). Distance encoded as N-byte big-endian value + 1. |
| 0xA8–0xAF | (reserved) | - | - | **Reserved/Unimplemented** - will throw NotImplementedException if encountered |
| 0xB0–0xBF | SEND_ARITH | low4 = arith op (0-15) | (rcv, arg → result) | Send hardcoded arithmetic/comparison operator: +, -, <, >, <=, >=, =, ~=, *, /, \\, @, bitShift:, //, bitAnd:, bitOr: |
| 0xC0–0xCF | SEND_SPECIAL | low4 = special index (0-15) | (rcv, args... → result) | Send special selector: at:, at:put:, size, next, nextPut:, atEnd, ==, class, blockCopy:, value, value:, do:, new, new:, x, y |
| 0xD0–0xDF | SEND_LIT_0_ARGS | low4 = lit index (0-15) | (rcv → result) | Send message using literal selector at index low4+1 with 0 arguments. |
| 0xE0–0xEF | SEND_LIT_1_ARG | low4 = lit index (0-15) | (rcv, arg → result) | Send message using literal selector at index low4+1 with 1 argument. |
| 0xF0–0xFF | SEND_LIT_2_ARGS | low4 = lit index (0-15) | (rcv, arg1, arg2 → result) | Send message using literal selector at index low4+1 with 2 arguments. |

### Arithmetic Selectors (0xB0-0xBF)

Bytecodes 176-191 send these hardcoded selectors:

| Index | Selector | Index | Selector |
|-------|----------|-------|----------|
| 0 | + | 8 | * |
| 1 | - | 9 | / |
| 2 | < | 10 | \\ |
| 3 | > | 11 | @ |
| 4 | <= | 12 | bitShift: |
| 5 | >= | 13 | // |
| 6 | = | 14 | bitAnd: |
| 7 | ~= | 15 | bitOr: |

### Special Selectors (0xC0-0xCF)

Bytecodes 192-207 send these special selectors:

| Index | Selector | Args | Index | Selector | Args |
|-------|----------|------|-------|----------|------|
| 0 | at: | 1 | 8 | blockCopy: | 1 |
| 1 | at:put: | 2 | 9 | value | 0 |
| 2 | size | 0 | 10 | value: | 1 |
| 3 | next | 0 | 11 | do: | 1 |
| 4 | nextPut: | 1 | 12 | new | 0 |
| 5 | atEnd | 0 | 13 | new: | 1 |
| 6 | == | 1 | 14 | x | 0 |
| 7 | class | 0 | 15 | y | 0 |

### Extended Operations (0x80 + ExtendedSubOp)

When bytecode 0x80 (EXTENDED_PREFIX) is encountered, the next byte specifies the extended operation:

| SubOp | Hex | Mnemonic | Operands | Stack Effect | Description |
|-------|-----|----------|----------|--------------|-------------|
| 0 | 0x00 | PUSH_IVAR | next byte = index | ( — → ivar[n]) | Push instance variable at index (any index, not limited to 0-15) |
| 1 | 0x01 | STORE_IVAR | next byte = index | (val → val) | Store to instance variable without popping (leaves value on stack) |
| 2 | 0x02 | POP_STORE_IVAR | next byte = index | (val → ) | Pop and store to instance variable at index (any index) |
| 3 | 0x03 | PUSH_TEMP | next byte = index | ( — → temp[n]) | Push temporary/argument at index (any index, not limited to 0-15) |
| 4 | 0x04 | STORE_TEMP | next byte = index | (val → val) | Store to temporary without popping (leaves value on stack) |
| 5 | 0x05 | POP_STORE_TEMP | next byte = index | (val → ) | Pop and store to temporary at index (any index) |
| 6 | 0x06 | PUSH_LITERAL | next byte = index | ( — → lit[n+1]) | Push literal constant at index (skips header) |
| 7 | 0x07 | PUSH_LITERAL_VAR | next byte = index | ( — → assocValue) | Push literal variable (association value) at index |
| 8 | 0x08 | STORE_LITERAL_VAR | next byte = index | (val → val) | Store to literal variable without popping |
| 9 | 0x09 | POP_STORE_LITERAL_VAR | next byte = index | (val → ) | Pop and store to literal variable (global/class var) |
| 10 | 0x0A | SEND_0 | next byte = selector index | (rcv → result) | Send message with 0 args, selector from literals[index+1] |
| 11 | 0x0B | SEND_1 | next byte = selector index | (rcv, arg → result) | Send message with 1 arg, selector from literals[index+1] |
| 12 | 0x0C | SEND_2 | next byte = selector index | (rcv, arg1, arg2 → result) | Send message with 2 args, selector from literals[index+1] |
| 13 | 0x0D | SEND_N | next 2 bytes: selector, argCount | (rcv, args... → result) | Send message with N args (N≥3), selector at literals[index+1] |
| 14 | 0x0E | SEND_SUPER_0 | next byte = selector index | (rcv → result) | Send to super with 0 args |
| 15 | 0x0F | SEND_SUPER_1 | next byte = selector index | (rcv, arg → result) | Send to super with 1 arg |
| 16 | 0x10 | SEND_SUPER_2 | next byte = selector index | (rcv, arg1, arg2 → result) | Send to super with 2 args |
| 17 | 0x11 | SEND_SUPER_N | next 2 bytes: selector, argCount | (rcv, args... → result) | Send to super with N args (N≥3) |
| 18 | 0x12 | POP | none | (val → ) | Pop and discard top of stack |
| 19 | 0x13 | DUP | none | (val → val, val) | Duplicate top of stack |
| 20 | 0x14 | JUMP_TRUE | next 2 bytes = offset | (bool → ) | Pop and jump forward if true |
| 21 | 0x15 | JUMP_FALSE | next 2 bytes = offset | (bool → ) | Pop and jump forward if false |
| 22 | 0x16 | JUMP | next 2 bytes = offset | ( — → ) | Unconditional jump forward |
| 23 | 0x17 | JUMP_LONG_TRUE | next 2 bytes = offset | (bool → ) | Pop and long jump forward if true |
| 24 | 0x18 | JUMP_LONG_FALSE | next 2 bytes = offset | (bool → ) | Pop and long jump forward if false |
| 25 | 0x19 | JUMP_LONG | next 2 bytes = offset | ( — → ) | Unconditional long jump forward |

**Notes:**
- Extended operations are always 2 or more bytes: 0x80 + subop + operands
- STORE_* operations (without POP) leave the value on the stack for chained assignments
- All jump offsets are forward-only and measured in bytes from the instruction following the jump
- Literal indices skip the header: literal 0 → literals[1], literal 1 → literals[2], etc.

### VM Implementation Coverage

**Virtual Machine Bytecode Support**: 226/256 opcodes implemented (88.3%)

**Implemented Ranges**:
- ✅ 0x00-0x0F: Push receiver variables (16 opcodes)
- ✅ 0x10-0x1F: Push temporaries/arguments (16 opcodes)
- ✅ 0x20-0x3F: Push literal constants (32 opcodes)
- ✅ 0x40-0x5F: Push literal variables (32 opcodes)
- ✅ 0x60-0x67: Pop/store receiver variables (8 opcodes)
- ✅ 0x68-0x6F: Pop/store temporaries (8 opcodes)
- ✅ 0x70-0x7F: Special constants & returns (16 opcodes)
- ✅ 0x80: Extended prefix (1 opcode)
- ✅ 0x89: Push active context (1 opcode)
- ✅ 0x90-0x97: Short jumps (8 opcodes)
- ✅ 0xA0: Medium jump (1 opcode)
- ✅ 0xA1-0xA7: Long jumps (7 opcodes)
- ✅ 0xB0-0xBF: Arithmetic sends (16 opcodes)
- ✅ 0xC0-0xCF: Special sends (16 opcodes)
- ✅ 0xD0-0xDF: Send literal 0 args (16 opcodes)
- ✅ 0xE0-0xEF: Send literal 1 arg (16 opcodes)
- ✅ 0xF0-0xFF: Send literal 2 args (16 opcodes)

**Reserved/Unimplemented** (30 opcodes - will throw NotImplementedException):
- 0x81-0x88 (8 opcodes)
- 0x8A-0x8F (6 opcodes)
- 0x98-0x9F (8 opcodes)
- 0xA8-0xAF (8 opcodes)

These reserved opcodes are not used by the Smalltalk-80 compiler and are not part of the Blue Book specification.

### Compiler Bytecode Generation

**Compiler Bytecode Emission**: 207/256 opcodes used (80.9%)

The compiler in `Smalltalk80.Compiler/BytecodeCompiler.cs` generates a subset of the opcodes that the VM implements:

**Opcodes the Compiler Emits** (207 total):
- ✅ 0x00-0x0F: Push receiver variables (16) - via `EmitWithOffset(PUSH_IVAR_0, index)`
- ✅ 0x10-0x1F: Push temporaries (16) - via `EmitWithOffset(PUSH_TEMP_0, index)`
- ✅ 0x20-0x3F: Push literal constants (32) - via `EmitWithOffset(PUSH_LITERAL_CONST_0, index)`
- ✅ 0x40-0x5F: Push literal variables (32) - via `EmitWithOffset(PUSH_LITERAL_VAR_0, index)`
- ✅ 0x60-0x67: Pop/store receiver variables (8) - via `EmitWithOffset(POP_STORE_IVAR_0, index)`
- ✅ 0x68-0x6F: Pop/store temporaries (8) - via `EmitWithOffset(POP_STORE_TEMP_0, index)`
- ✅ 0x70-0x77: Special constants (8) - explicit `Emit(Bytecode.PUSH_SELF)` etc.
- ✅ 0x78-0x7C, 0x7E: Returns (6) - explicit emit calls
- ✅ 0x80: Extended prefix (1) - for extended operations
- ✅ 0xB0-0xBF: Arithmetic sends (16) - via `SpecialSelectors` dictionary
- ✅ 0xC0-0xCF: Special sends (16) - via `SpecialSelectors` dictionary
- ✅ 0xD0-0xDF: Send literal 0 args (16) - via `EmitWithOffset(SEND_LITERAL_0_BASE, index)`
- ✅ 0xE0-0xEF: Send literal 1 arg (16) - via `EmitWithOffset(SEND_LITERAL_1_BASE, index)`
- ✅ 0xF0-0xFF: Send literal 2 args (16) - via `EmitWithOffset(SEND_LITERAL_2_BASE, index)`

**Opcodes VM Implements But Compiler Never Emits** (19 total):

**Jump Instructions** (16 opcodes) - **Deliberately Removed from Compiler**:
- ❌ 0x90-0x97: Short forward jumps (8 opcodes) - **NOT EMITTED**
- ❌ 0xA0: Medium forward jump (1 opcode) - **NOT EMITTED**
- ❌ 0xA1-0xA7: Long forward jumps (7 opcodes) - **NOT EMITTED**

**Other Non-Emitted Opcodes** (3 opcodes):
- ❌ 0x7D: `BLOCK_RETURN_SELF` - **NOT EMITTED**
- ❌ 0x7F: `NOP` / Reserved return - **NOT EMITTED**
- ❌ 0x89: `PUSH_ACTIVE_CONTEXT` - **NOT EMITTED**
### Arithmetic Selectors (0xB0-0xBF)

Bytecodes 176-191 send these hardcoded selectors:

| Index | Selector | Index | Selector |
|-------|----------|-------|----------|
| 0 | + | 8 | * |
| 1 | - | 9 | / |
| 2 | < | 10 | \\ |
| 3 | > | 11 | @ |
| 4 | <= | 12 | bitShift: |
| 5 | >= | 13 | // |
| 6 | = | 14 | bitAnd: |
| 7 | ~= | 15 | bitOr: |

### Special Selectors (0xC0-0xCF)

Bytecodes 192-207 send these special selectors:

| Index | Selector | Args | Index | Selector | Args |
|-------|----------|------|-------|----------|------|
| 0 | at: | 1 | 8 | blockCopy: | 1 |
| 1 | at:put: | 2 | 9 | value | 0 |
| 2 | size | 0 | 10 | value: | 1 |
| 3 | next | 0 | 11 | do: | 1 |
| 4 | nextPut: | 1 | 12 | new | 0 |
| 5 | atEnd | 0 | 13 | new: | 1 |
| 6 | == | 1 | 14 | x | 0 |
| 7 | class | 0 | 15 | y | 0 |

### Extended Operations (0x80 + ExtendedSubOp)

When bytecode 0x80 (EXTENDED_PREFIX) is encountered, the next byte specifies the extended operation:

| SubOp | Hex | Mnemonic | Operands | Stack Effect | Description |
|-------|-----|----------|----------|--------------|-------------|
| 0 | 0x00 | PUSH_IVAR | next byte = index | ( — → ivar[n]) | Push instance variable at index (any index, not limited to 0-15) |
| 1 | 0x01 | STORE_IVAR | next byte = index | (val → val) | Store to instance variable without popping (leaves value on stack) |
| 2 | 0x02 | POP_STORE_IVAR | next byte = index | (val → ) | Pop and store to instance variable at index (any index) |
| 3 | 0x03 | PUSH_TEMP | next byte = index | ( — → temp[n]) | Push temporary/argument at index (any index, not limited to 0-15) |
| 4 | 0x04 | STORE_TEMP | next byte = index | (val → val) | Store to temporary without popping (leaves value on stack) |
| 5 | 0x05 | POP_STORE_TEMP | next byte = index | (val → ) | Pop and store to temporary at index (any index) |
| 6 | 0x06 | PUSH_LITERAL | next byte = index | ( — → lit[n+1]) | Push literal constant at index (skips header) |
| 7 | 0x07 | PUSH_LITERAL_VAR | next byte = index | ( — → assocValue) | Push literal variable (association value) at index |
| 8 | 0x08 | STORE_LITERAL_VAR | next byte = index | (val → val) | Store to literal variable without popping |
| 9 | 0x09 | POP_STORE_LITERAL_VAR | next byte = index | (val → ) | Pop and store to literal variable (global/class var) |
| 10 | 0x0A | SEND_0 | next byte = selector index | (rcv → result) | Send message with 0 args, selector from literals[index+1] |
| 11 | 0x0B | SEND_1 | next byte = selector index | (rcv, arg → result) | Send message with 1 arg, selector from literals[index+1] |
| 12 | 0x0C | SEND_2 | next byte = selector index | (rcv, arg1, arg2 → result) | Send message with 2 args, selector from literals[index+1] |
| 13 | 0x0D | SEND_N | next 2 bytes: selector, argCount | (rcv, args... → result) | Send message with N args (N≥3), selector at literals[index+1] |
| 14 | 0x0E | SEND_SUPER_0 | next byte = selector index | (rcv → result) | Send to super with 0 args |
| 15 | 0x0F | SEND_SUPER_1 | next byte = selector index | (rcv, arg → result) | Send to super with 1 arg |
| 16 | 0x10 | SEND_SUPER_2 | next byte = selector index | (rcv, arg1, arg2 → result) | Send to super with 2 args |
| 17 | 0x11 | SEND_SUPER_N | next 2 bytes: selector, argCount | (rcv, args... → result) | Send to super with N args (N≥3) |
| 18 | 0x12 | POP | none | (val → ) | Pop and discard top of stack |
| 19 | 0x13 | DUP | none | (val → val, val) | Duplicate top of stack |
| 20 | 0x14 | JUMP_TRUE | next 2 bytes = offset | (bool → ) | Pop and jump forward if true |
| 21 | 0x15 | JUMP_FALSE | next 2 bytes = offset | (bool → ) | Pop and jump forward if false |
| 22 | 0x16 | JUMP | next 2 bytes = offset | ( — → ) | Unconditional jump forward |
| 23 | 0x17 | JUMP_LONG_TRUE | next 2 bytes = offset | (bool → ) | Pop and long jump forward if true |
| 24 | 0x18 | JUMP_LONG_FALSE | next 2 bytes = offset | (bool → ) | Pop and long jump forward if false |
| 25 | 0x19 | JUMP_LONG | next 2 bytes = offset | ( — → ) | Unconditional long jump forward |

**Notes:**
- Extended operations are always 2 or more bytes: 0x80 + subop + operands
- STORE_* operations (without POP) leave the value on the stack for chained assignments
- All jump offsets are forward-only and measured in bytes from the instruction following the jump
- Literal indices skip the header: literal 0 → literals[1], literal 1 → literals[2], etc.

### VM Implementation Coverage

**Virtual Machine Bytecode Support**: 226/256 opcodes implemented (88.3%)

**Implemented Ranges**:
- ✅ 0x00-0x0F: Push receiver variables (16 opcodes)
- ✅ 0x10-0x1F: Push temporaries/arguments (16 opcodes)
- ✅ 0x20-0x3F: Push literal constants (32 opcodes)
- ✅ 0x40-0x5F: Push literal variables (32 opcodes)
- ✅ 0x60-0x67: Pop/store receiver variables (8 opcodes)
- ✅ 0x68-0x6F: Pop/store temporaries (8 opcodes)
- ✅ 0x70-0x7F: Special constants & returns (16 opcodes)
- ✅ 0x80: Extended prefix (1 opcode)
- ✅ 0x89: Push active context (1 opcode)
- ✅ 0x90-0x97: Short jumps (8 opcodes)
- ✅ 0xA0: Medium jump (1 opcode)
- ✅ 0xA1-0xA7: Long jumps (7 opcodes)
- ✅ 0xB0-0xBF: Arithmetic sends (16 opcodes)
- ✅ 0xC0-0xCF: Special sends (16 opcodes)
- ✅ 0xD0-0xDF: Send literal 0 args (16 opcodes)
- ✅ 0xE0-0xEF: Send literal 1 arg (16 opcodes)
- ✅ 0xF0-0xFF: Send literal 2 args (16 opcodes)

**Reserved/Unimplemented** (30 opcodes - will throw NotImplementedException):
- 0x81-0x88 (8 opcodes)
- 0x8A-0x8F (6 opcodes)
- 0x98-0x9F (8 opcodes)
- 0xA8-0xAF (8 opcodes)

These reserved opcodes are not used by the Smalltalk-80 compiler and are not part of the Blue Book specification.

### Compiler Bytecode Generation

**Compiler Bytecode Emission**: 207/256 opcodes used (80.9%)

The compiler in `Smalltalk80.Compiler/BytecodeCompiler.cs` generates a subset of the opcodes that the VM implements:

**Opcodes the Compiler Emits** (207 total):
- ✅ 0x00-0x0F: Push receiver variables (16) - via `EmitWithOffset(PUSH_IVAR_0, index)`
- ✅ 0x10-0x1F: Push temporaries (16) - via `EmitWithOffset(PUSH_TEMP_0, index)`
- ✅ 0x20-0x3F: Push literal constants (32) - via `EmitWithOffset(PUSH_LITERAL_CONST_0, index)`
- ✅ 0x40-0x5F: Push literal variables (32) - via `EmitWithOffset(PUSH_LITERAL_VAR_0, index)`
- ✅ 0x60-0x67: Pop/store receiver variables (8) - via `EmitWithOffset(POP_STORE_IVAR_0, index)`
- ✅ 0x68-0x6F: Pop/store temporaries (8) - via `EmitWithOffset(POP_STORE_TEMP_0, index)`
- ✅ 0x70-0x77: Special constants (8) - explicit `Emit(Bytecode.PUSH_SELF)` etc.
- ✅ 0x78-0x7C, 0x7E: Returns (6) - explicit emit calls
- ✅ 0x80: Extended prefix (1) - for extended operations
- ✅ 0xB0-0xBF: Arithmetic sends (16) - via `SpecialSelectors` dictionary
- ✅ 0xC0-0xCF: Special sends (16) - via `SpecialSelectors` dictionary
- ✅ 0xD0-0xDF: Send literal 0 args (16) - via `EmitWithOffset(SEND_LITERAL_0_BASE, index)`
- ✅ 0xE0-0xEF: Send literal 1 arg (16) - via `EmitWithOffset(SEND_LITERAL_1_BASE, index)`
- ✅ 0xF0-0xFF: Send literal 2 args (16) - via `EmitWithOffset(SEND_LITERAL_2_BASE, index)`

**Opcodes VM Implements But Compiler Never Emits** (19 total):

**Jump Instructions** (16 opcodes) - **Deliberately Removed from Compiler**:
- ❌ 0x90-0x97: Short forward jumps (8 opcodes) - **NOT EMITTED**
- ❌ 0xA0: Medium forward jump (1 opcode) - **NOT EMITTED**
- ❌ 0xA1-0xA7: Long forward jumps (7 opcodes) - **NOT EMITTED**

**Other Non-Emitted Opcodes** (3 opcodes):
- ❌ 0x7D: `BLOCK_RETURN_SELF` - **NOT EMITTED**
- ❌ 0x7F: `NOP` / Reserved return - **NOT EMITTED**
- ❌ 0x89: `PUSH_ACTIVE_CONTEXT` - **NOT EMITTED**

**Compiler Design Decision**: The compiler deliberately does not generate jump bytecodes. From `BytecodeCompiler.cs` lines 649-652:

```csharp
// NOTE: Jump operations have been removed.
// With Blue Book block compilation, blocks are separate CompiledMethod objects.
// Control flow (ifTrue:, whileTrue:, etc.) is implemented via message sends,
// NOT via jump bytecodes. The compiler generates zero jump instructions.
```

This design choice means:
- **Control flow** (ifTrue:, ifFalse:, whileTrue:, etc.) is implemented via message sends to blocks
- **Blocks** are compiled as separate CompiledMethod objects
- **No inline bytecode jumps** - cleaner, more maintainable bytecode generation
- **VM still implements jumps** for potential future use or compatibility

**Reserved Opcodes Verification**: ✅ **VERIFIED** - The compiler **NEVER** emits any of the 30 reserved opcodes (0x81-0x88, 0x8A-0x8F, 0x98-0x9F, 0xA8-0xAF).

**Consistency Verification**: ✅ **VERIFIED** - All 207 opcodes the compiler emits are correctly:
1. Implemented in the VM (`VirtualMachine.cs`)
2. Documented in this specification
3. Used in their documented forms with correct operands and stack effects

### Execution Engine

```csharp
public class VirtualMachine
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly Stack<StackFrame> _callStack = new();
    private int _instructionPointer;
    private SmalltalkOOP[] _stack = new SmalltalkOOP[256];
    private int _stackPointer;
    
    public void ExecuteMethod(SmalltalkOOP methodOOP, SmalltalkOOP receiver, SmalltalkOOP[] arguments)
    {
        // Create method context
        var context = CreateMethodContext(methodOOP, receiver, arguments);
        _callStack.Push(context);
        
        // Execute bytecode loop
        while (_instructionPointer < context.Method.BytecodeLength)
        {
            byte bytecode = GetBytecode(_instructionPointer++);
            ExecuteBytecode(bytecode);
        }
    }
}
```

### Method Lookup with Inheritance

```csharp
public SmalltalkOOP GetMethod(SmalltalkOOP classOOP, SmalltalkOOP selectorOOP)
{
    var currentClassOOP = classOOP;
    
    // Walk superclass chain until method found
    while (currentClassOOP.AsObjectTableIndex() != GlobalRegistry.NilObjectId)
    {
        var methodDictOOP = _heapManager.GetInstanceVariable(currentClassOOP, GetMethodDictionaryIndex());
        var methodOOP = LookupInMethodDictionary(methodDictOOP, selectorOOP);
        
        if (methodOOP.AsObjectTableIndex() != GlobalRegistry.NilObjectId)
            return methodOOP;
            
        // Try superclass
        currentClassOOP = _heapManager.GetInstanceVariable(currentClassOOP, GetSuperclassIndex());
    }
    
    return SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId); // Method not found
}
```

### Current Implementation Status

✅ **Basic Bytecode Execution**: Variable access, literals, simple sends  
✅ **Method Dispatch**: Inheritance-based lookup working  
✅ **Primitive Integration**: 94/111 primitives implemented  
✅ **Stack Management**: Proper frame handling  
⚠️ **Block Execution**: BlockContext integration in progress  
⚠️ **Exception Handling**: Not yet implemented  

## Primitive Operations

### Primitive Categories

The system implements 111 Blue Book primitives organized by functionality:

#### Arithmetic Primitives (1-19)
```csharp
// Primitive 1: SmallInteger addition
public SmalltalkOOP SmallIntegerAdd(SmalltalkOOP receiver, SmalltalkOOP argument)
{
    if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        return PrimitiveFailed();
        
    int result = receiver.AsSmallInteger() + argument.AsSmallInteger();
    
    if (SmalltalkOOP.IsSmallInteger(result))
        return SmalltalkOOP.FromSmallInteger(result);
    else
        return PrimitiveFailed(); // Overflow - create LargeInteger
}
```

#### Float Primitives (40-54) ✅ Complete
```csharp
// Primitive 41: Float addition  
public SmalltalkOOP FloatAdd(SmalltalkOOP receiver, SmalltalkOOP argument)
{
    double receiverValue = ExtractFloatValue(receiver);
    double argumentValue = ConvertToFloat(argument); // Handle SmallInteger conversion
    
    double result = receiverValue + argumentValue;
    return AllocateFloat(result);
}
```

#### Object Access Primitives (60-79)
```csharp
// Primitive 60: Object at (indexed access)
public SmalltalkOOP ObjectAt(SmalltalkOOP receiver, SmalltalkOOP indexOOP)
{
    if (!indexOOP.IsSmallInteger)
        return PrimitiveFailed();
        
    int index = indexOOP.AsSmallInteger() - 1; // Convert to 0-based
    
    return _heapManager.GetObjectFormat(receiver) switch
    {
        ObjectFormat.PointerIndexed => _heapManager.GetIndexedOOP(receiver, index),
        ObjectFormat.ByteIndexed => SmalltalkOOP.FromSmallInteger(_heapManager.GetIndexedByte(receiver, index)),
        ObjectFormat.WordIndexed => SmalltalkOOP.FromSmallInteger(_heapManager.GetIndexedWord(receiver, index)),
        _ => PrimitiveFailed()
    };
}
```

### Primitive Status

- ✅ **SmallInteger Arithmetic**: Addition, subtraction, multiplication, division, comparisons
- ✅ **Float Arithmetic**: Complete implementation with proper overflow handling
- ✅ **Object Access**: at:, at:put:, size, basicNew, basicNew:
- ✅ **Control Primitives**: Object equality, class operations
- ⚠️ **Block Primitives**: value, value:, value:value: (requires BlockContext)
- ⚠️ **Process Primitives**: Yield, suspend, resume (future)

## CompiledMethod Object Architecture

### Object Layout Design

CompiledMethod objects use **ObjectFormat.PointerIndexed** to maintain heap manager class-agnosticism while supporting dual access patterns required by Smalltalk-80.

#### Memory Layout

```
CompiledMethod (PointerIndexed):
[classOOP] [instanceVars] [header] [literal1] [literal2] ... [literalN] [bytecodeArrayOOP]
                          ↑                                              ↑
                       index 1                                    hidden from literals
```

#### Dual Access Pattern Support

CompiledMethod objects are accessed through two different primitive operations:

**1. OOP Access via objectAt: (Primitive 68)**
```smalltalk
method objectAt: 1    "→ method header (SmallInteger)"
method objectAt: 2    "→ first literal (any object)"
method objectAt: 3    "→ second literal (any object)"
```

**2. Bytecode Access via at: (Primitive 60)**  
```smalltalk
method at: 1    "→ first bytecode byte"
method at: 2    "→ second bytecode byte"
```

#### Implementation Strategy

1. **Header + Literals**: Stored as OOPs in PointerIndexed slots
2. **Bytecodes**: Stored in separate ByteArray object, referenced as final OOP
3. **Primitive Logic**: 
   - `objectAt:` accesses header and literal OOPs with bounds checking
   - `at:` delegates to the ByteArray object for bytecode access
4. **GC Compatibility**: All literals and bytecode array automatically traversed

#### Primitive Implementation

```csharp
// Primitive 68: objectAt: index
if (index == 1) return headerOOP;
if (index >= 2 && index <= numLiterals + 1) return literalOOP[index-2];
throw IndexOutOfBounds; // Don't expose bytecode array reference

// Primitive 60: at: index
var bytecodeArrayOOP = GetIndexedOOP(methodOOP, numLiterals + 1);  
return PrimitiveAt(bytecodeArrayOOP, index); // Delegate to ByteArray
```

#### Benefits

✅ **Heap Manager Agnostic**: Uses standard PointerIndexed format  
✅ **GC Compatible**: All references (literals + bytecode array) automatically traversed  
✅ **Blue Book Compliant**: Supports expected `objectAt:` and `at:` access patterns  
✅ **Type Safe**: Primitive bounds checking prevents invalid access  
✅ **Memory Efficient**: Bytecodes stored as dense byte arrays  

## BlockContext Support

### v1.0.3 Implementation Strategy

**Core Principle**: Blocks are separate CompiledMethod objects, not inline bytecode constructs.

#### Block Compilation Approach

```json
// Block compiled as separate method
{
  "localId": "method:5678",
  "homeClassLocalId": "",           // Empty for blocks
  "selectorIndex": null,            // No selector for blocks  
  "header": 16384,
  "literals": [...],
  "bytecodes": [144, 32, 124]       // Standard bytecodes, ends with return
}

// Owner method references block via literal
{
  "localId": "method:1234", 
  "literals": [
    {"type": "compiledMethod", "localId": "method:5678"}  // Block reference
  ]
}
```

#### BlockContext Structure

BlockContext objects contain 5 fixed fields plus indexed fields for arguments and temporaries:

```csharp
public class BlockContextLayout
{
    // Fixed fields (instance variables)
    public SmalltalkOOP method;               // The block's CompiledMethod
    public SmalltalkOOP initialIP;           // Starting instruction pointer
    public SmalltalkOOP blockArgumentCount;  // Number of block arguments
    public SmalltalkOOP homeContext;         // Lexical scope context
    public SmalltalkOOP outerContext;        // Enclosing block context
    
    // Indexed fields (variable part)
    // [arg0, arg1, ..., argN, temp0, temp1, ..., tempM]
}
```

#### Block Invocation Flow

1. **Detection**: VM detects `value`/`value:`/`value:value:` sent to CompiledMethod
2. **Owner Resolution**: Look up block owner from blockOwnerMap built during cold start
3. **Home Context**: Determine homeContext (current active MethodContext)  
4. **BlockContext Creation**: Allocate BlockContext with proper linkage
5. **Argument Binding**: Store arguments in BlockContext indexed fields
6. **Execution**: Create MethodContext backed by BlockContext and execute

#### Implementation Status

- ✅ **Block Compilation**: Blocks compile to separate CompiledMethods
- ✅ **Ownership Tracking**: blockOwnerMap built with 5,419 entries during cold start
- ✅ **BlockContext Allocation**: `CreateBlockContext` method implemented in heap manager
- ⚠️ **Block Invocation**: VM detection and execution in progress
- ⚠️ **Self Resolution**: Blocks use homeContext.receiver for self access
- ⚠️ **Non-Local Returns**: Return to home context (if needed)

### Next Implementation Steps

1. **VirtualMachine.SendMessage() Updates**: Detect block invocation patterns
2. **InvokeBlock Method**: Handle block execution with proper context creation
3. **Interpreter Context Resolution**: Temp/arg access through BlockContext
4. **DoIt MethodContext**: Create contexts for init expression execution

## Graphics Support

### Architecture Overview

The graphics system implements Blue Book BitBlt for 2D graphics operations:

```csharp
public class BitBlt
{
    public SmalltalkOOP sourceForm;      // Source bitmap
    public SmalltalkOOP destForm;        // Destination bitmap  
    public SmalltalkOOP halftoneForm;    // Halftone pattern
    public int sourceX, sourceY;        // Source coordinates
    public int destX, destY;            // Destination coordinates
    public int width, height;           // Transfer dimensions
    public int combinationRule;         // How to combine pixels
}
```

### Form Objects

```csharp
public class FormLayout
{
    public SmalltalkOOP width;           // Form width in pixels
    public SmalltalkOOP height;          // Form height in pixels  
    public SmalltalkOOP depth;           // Bits per pixel (1, 2, 4, 8, 16)
    public SmalltalkOOP bits;            // Bitmap data (WordArray)
}
```

### BitBlt Combination Rules

```csharp
public enum CombinationRule
{
    Clear = 0,           // 0
    And = 1,             // source & dest  
    AndReverse = 2,      // source & ~dest
    Copy = 3,            // source
    AndInverted = 4,     // ~source & dest
    NoOp = 5,            // dest
    Xor = 6,             // source ^ dest
    Or = 7,              // source | dest
    // ... 16 total rules
}
```

### Implementation Status

⚠️ **Planned**: Graphics support planned for post-BlockContext implementation

## Process and Scheduling Infrastructure

### ProcessorScheduler Singleton (2025-01-15)

The system now includes a ProcessorScheduler singleton instance for future process management:

**Creation**: Phase 6 creates the singleton after nil/true/false singletons  
**Initialization**: Phase Y initializes all instance variables to nil  
**Installation**: Phase 12 installs as global variable `Processor` in SystemDictionary

**Access Pattern**:
```smalltalk
Processor  "→ the ProcessorScheduler singleton (via Smalltalk at: #Processor)"
```

### Core Classes Required

```csharp
// Process management hierarchy
ProcessorScheduler  // Global process manager singleton ✅ Created
Process             // Individual computation contexts (future)
Semaphore          // Process synchronization (future)
Delay              // Time-based delays (future)
```

### Current Implementation Status

✅ **ProcessorScheduler Singleton**: Created and properly initialized  
✅ **Global Variable**: Accessible via `Smalltalk at: #Processor`  
✅ **Instance Variables**: All initialized to nil  
⚠️ **Process System**: Full implementation planned for future

### Integration Requirements

⚠️ **Future Implementation**: Full process system planned after BlockContext completion

## Blue Book Compliance

### Compliance Status: STRICT

The interpreter maintains strict Blue Book compliance across all implemented features:

#### ✅ Compliant Areas

- **Object Model**: Exact Blue Book object layouts and behavior
- **Bytecode Set**: Standard instruction set with no extensions  
- **Primitive Numbers**: Exact Blue Book primitive operation numbers
- **Class Hierarchy**: Canonical inheritance relationships
- **Method Headers**: Blue Book format word encoding
- **Float Format**: IEEE 754 double precision as specified

#### ⚠️ Planned Compliance

- **Block Closures**: v1.0.3 approach maintains Blue Book semantics
- **Process System**: Will follow Blue Book process model
- **Exception Handling**: Blue Book error handling mechanisms

### Validation Methods

```bash
# Schema validation ensures Blue Book compliance
dotnet run --project Smalltalk80.Validator -- coldstart.json

# Expected: ✓ Validation PASSED (17 validators, 0 errors)
```

## Multi-VM Architecture

### Current Static State

The current implementation uses static global state for simplicity:

```csharp
public static class GlobalRegistry
{
    private static readonly Dictionary<string, int> _globalObjects = new();
    
    public static int NilObjectId { get; set; }
    public static int TrueObjectId { get; set; }
    public static int FalseObjectId { get; set; }
    public static int SystemDictionaryId { get; set; }
}
```

### Proposed VM Context

Future architecture will encapsulate all VM state:

```csharp
public class SmalltalkVMContext
{
    public ISmalltalkHeapManager HeapManager { get; }
    public VirtualMachine Interpreter { get; }
    public Dictionary<string, SmalltalkOOP> WellKnownObjects { get; }
    public SmalltalkOOP[] SpecialObjects { get; }
    
    public void LoadFromColdStart(string jsonPath) { ... }
    public SmalltalkOOP Evaluate(string smalltalkCode) { ... }
}
```

### Benefits

- **Multi-VM Support**: Multiple isolated Smalltalk environments
- **Thread Safety**: Each VM context is independent
- **Testing**: Easy setup/teardown for unit tests
- **Embedding**: Clean API for host applications

## Key Design Principles

### 1. Blue Book Compliance
All implemented features strictly follow the Smalltalk-80 specification without extensions or modifications.

### 2. Zero Garbage  
The system maintains 100% object reachability during normal operation, achieving zero garbage collection.

### 3. Separation of Concerns
- **Heap Manager**: Pure memory management without semantic knowledge
- **Cold Start**: Bootstrap process isolated from runtime
- **VM Core**: Bytecode execution and message dispatch
- **Primitive Operations**: Self-contained operation implementations

### 4. Schema-Driven Development
The JSON schema enforces data integrity and enables versioned evolution of the cold start format.

### 5. Modern Architecture
The codebase uses modern C# features while maintaining Blue Book semantics:
- Tagged structs for type safety
- Immutable value types for OOPs
- LINQ for collection operations
- Async patterns where appropriate

### 6. OOP Type Safety

**The OOP tagging system is the fundamental contract. Raw object indices should NEVER be stored or passed between methods - only tagged SmalltalkOOP values.**

**Key Principles**:
- All methods should accept/return `SmalltalkOOP`, never `int`
- Only use `.AsObjectTableIndex()` at the lowest heap manager boundary
- Use `.RawValue` only for serialization or debugging
- This prevents double-tagging bugs and maintains type safety

**Critical Lesson**: The literal handling double-tagging bug (2025-01-15) demonstrated the importance of maintaining strict OOP type safety. Mixing raw indices with tagged OOPs creates invalid references that break GC traversal.

## Technical Specifications

### Memory Layout

```
Object Header Format:
┌─────────────────┐
│ Class OOP (32b) │ ← Points to object's class
└─────────────────┘

Tagged OOP Format:
┌─────────────────┐
│ Index/Value(31b)│ ← Object table index or SmallInteger value
│ Tag Bit (1b)    │ ← 0=Object pointer, 1=SmallInteger
└─────────────────┘
```

### Bytecode Instruction Set

```
Standard Bytecodes:
0x00-0x0F: Push receiver variable 0-15
0x10-0x1F: Push temporary variable 0-15  
0x20-0x3F: Push literal constant 0-31
0x40-0x5F: Push literal variable 0-31
0x60-0x67: Pop and store receiver variable 0-7
0x68-0x6F: Pop and store temporary variable 0-7
0x70:      Push receiver (self)
0x71:      Push true
0x72:      Push false  
0x73:      Push nil
0x74:      Push -1
0x75:      Push 0
0x76:      Push 1
0x77:      Push 2
0x78:      Return receiver
0x79:      Return true
0x7A:      Return false
0x7B:      Return nil
0x7C:      Return top of stack
0x7D:      Return top of stack to caller
0x7E-0x7F: Unused
0x80-0x8F: Extended instructions (with parameter bytes)
0x90-0x97: Send selector 0-7 with 0 arguments
0x98-0x9F: Send selector 8-15 with 0 arguments
0xA0-0xA7: Send selector 0-7 with 1 argument
0xA8-0xAF: Send selector 8-15 with 1 argument  
0xB0-0xB7: Send selector 0-7 with 2 arguments
0xB8-0xBF: Send selector 8-15 with 2 arguments
0xC0-0xDF: Control flow (jumps, conditional branches)
0xE0-0xFF: Extended operations
```

### Primitive Operation Numbers

```
Standard Blue Book Primitives:
1-19:   SmallInteger arithmetic
20-39:  LargeInteger arithmetic  
40-59:  Float arithmetic (✅ Complete)
60-79:  Object access and modification
80-89:  Control structures
90-99:  Array operations
100-109: Stream operations
110-119: File operations
120-127: Miscellaneous
```

### Object Format Specifications

```csharp
// Format encoding in class format field
public static class ObjectFormatConstants
{
    public const int PointerFormat = 0;          // 0b00: Pointer objects
    public const int PointerIndexedFormat = 1;   // 0b01: Variable pointer objects  
    public const int ByteIndexedFormat = 2;      // 0b10: Variable byte objects
    public const int WordIndexedFormat = 3;      // 0b11: Variable word objects
    
    // Size encoding (bits 2-7 of format field)
    public const int SizeMask = 0x3F << 2;       // 6 bits for size
    public const int SizeShift = 2;
}
```

### Perfect GC Statistics

The system maintains perfect object reachability:

```
Total Objects:     31,927
Reachable:         31,927 (100%)
Unreachable:       0 (0%)
Collection Time:   9.96ms
GC Efficiency:     0.0% (nothing to collect)
```

**GC Root Distribution**:
- nil: 1 object (singleton)
- false: 1 object (singleton)
- true: 1 object (singleton)
- Smalltalk: 1 object (SystemDictionary) → contains 476+ global associations
  - Classes: 474 instance classes accessible
  - USTable: Array of all symbols
  - Processor: ProcessorScheduler singleton
  - Other globals as needed

---

*This document describes the runtime architecture of the Smalltalk-80 interpreter. For cold start bootstrap implementation details, see `COLD-START-DESIGN-AND-IMPLEMENTATION.md`.*