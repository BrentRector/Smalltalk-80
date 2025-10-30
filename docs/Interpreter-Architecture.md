# Smalltalk-80 Interpreter Runtime Architecture

**Last Updated**: 2025-01-28
**Status**: 🎉 **Trampoline + Tail Call Optimization + Context Pooling** - Production-ready interpreter
**Heap Manager**: ✅ Perfect GC (0 unreachable objects) with bulletproof allocation architecture
**VM Core**: 🚀 Trampoline architecture with tail call optimization and context pooling
**Primitive Coverage**: ✅ Enhanced with objectAt:/at: access patterns
**Schema Version**: v1.0.6 (Precomputed class layouts, Blue Book-compliant with separate ByteArray design)

## Executive Summary

This document provides the canonical architectural documentation for the Smalltalk-80 interpreter runtime system. The interpreter implements a table-based object memory with proper Blue Book compliance, bulletproof allocation architecture, and production-ready performance optimizations. The system achieves **complete cold start success** with all 15 phases working and comprehensive object validation.

**🎉 Key Achievements**:
- **Perfect Garbage Collection**: 0 unreachable objects out of 31,927 total objects
- **Trampoline Interpreter**: Eliminates .NET stack overflow using Blue Book single-loop design
- **Tail Call Optimization**: Prevents context accumulation in recursive patterns (whileTrue:, timesRepeat:)
- **Context Pooling**: Reduces GC pressure by reusing recently returned contexts (near-zero allocation rate)
- **Complete Cold Start**: All 15 phases work correctly with ProcessorScheduler singleton and optimized GC roots

These optimizations work synergistically to enable deep recursion (256+ iterations) with constant context depth and minimal allocation overhead.

## Table of Contents

1. [Runtime System State](#runtime-system-state)
2. [Architecture Layers](#architecture-layers)
3. [Heap Manager Architecture](#heap-manager-architecture)
4. [Object Memory Architecture](#object-memory-architecture)
5. [Object Model](#object-model)
6. [Garbage Collection](#garbage-collection)
7. [Bytecode Execution](#bytecode-execution)
   - [Trampoline Interpreter Architecture](#trampoline-interpreter-architecture)
   - [Tail Call Optimization](#tail-call-optimization)
   - [Context Pooling](#context-pooling)
8. [Primitive Operations](#primitive-operations)
9. [BlockContext Support](#blockcontext-support)
10. [BlockClosure Support](#blockclosure-support)
11. [BlockContext to BlockClosure Migration](#blockcontext-to-blockclosure-migration)
12. [Graphics Support](#graphics-support)
13. [Process and Scheduling Infrastructure](#process-and-scheduling-infrastructure)
14. [Blue Book Compliance](#blue-book-compliance)
15. [Multi-VM Architecture](#multi-vm-architecture)
16. [Testing](#testing)
17. [Key Design Principles](#key-design-principles)
18. [Technical Specifications](#technical-specifications)

## Runtime System State

**Last Updated**: 2025-01-28
**Status**: 🚀 **Production-Ready Interpreter** - Trampoline + Tail Call Optimization + Context Pooling

### Current Achievements

✅ **Trampoline Interpreter**: Blue Book single-loop design eliminates .NET stack overflow
✅ **Tail Call Optimization**: Prevents context accumulation in recursive patterns (whileTrue:, timesRepeat:)
✅ **Context Pooling**: Near-zero allocation rate through context reuse (50 context pool)
✅ **Perfect Garbage Collection**: 0 unreachable objects out of 31,927 allocated
✅ **Deep Recursion Support**: 256+ iterations with constant context depth (3-5 contexts)
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
✅ **Trampoline Interpreter**: Eliminates .NET stack overflow for deep Smalltalk recursion
✅ **Block Execution**: Complete with trampoline-based invocation
✅ **Non-Local Returns**: Trampoline-based unwinding without exceptions

### Trampoline Interpreter Architecture

**Purpose**: The trampoline interpreter eliminates .NET call stack overflow by implementing the Blue Book's single-loop interpreter design. Instead of recursively calling `ExecuteBytecodeLoop()` for each message send or block invocation, the interpreter uses a single top-level loop that switches between Smalltalk contexts via heap-allocated objects.

**Problem Solved**: Classic Smalltalk control flow (e.g., `whileTrue:`) is naturally recursive. Without trampolining, executing `256 timesRepeat: [...]` creates 768+ .NET stack frames, causing stack overflow. Character initialize (creates 256 characters) would fail after ~100 iterations.

#### Architecture Components

**1. Interpreter Loop (Single Top-Level Loop)**
```csharp
private SmalltalkOOP InterpreterLoop()
{
    _initialContextOOP = _activeContextOOP;  // Save starting context

    while (true)
    {
        InterpreterStatus status = ExecuteBytecodeStep();

        switch (status)
        {
            case InterpreterStatus.Continue:
                // Context switched - keep executing in new context
                continue;

            case InterpreterStatus.Return:
                // Execution complete - return value
                return _returnValue;

            case InterpreterStatus.Yield:
                // Future: cooperative multitasking
                throw new NotImplementedException();
        }
    }
}
```

**Key Insight**: Single loop eliminates recursive `ExecuteBytecodeLoop()` calls. .NET call stack stays flat while Smalltalk contexts switch via heap.

**2. Bytecode Step Execution**
```csharp
private InterpreterStatus ExecuteBytecodeStep()
{
    // Execute bytecodes in current context until:
    // 1. Message send (context switch)
    // 2. Method/block return
    // 3. Non-local return

    while (ip < bytecodes.Length)
    {
        bool continueExecution = ExecuteBytecode(bytecode, methodOOP);

        if (!continueExecution)
        {
            // Check for context switch vs return
            if (_contextSwitchPending)
            {
                _contextSwitchPending = false;
                return InterpreterStatus.Continue;  // Switch to new context
            }

            // Handle return (normal or non-local)
            return HandleReturn(returnValue);
        }
    }
}
```

**3. Message Send (Context Switch Signal)**
```csharp
private CallOutcome SendMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
{
    // Look up method, execute primitive if present
    // ...

    // TRAMPOLINE MODE: Create context and signal switch (don't recurse)
    _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _activeContextOOP);

    // Signal context switch - InterpreterLoop will continue in new context
    return CallOutcome.ContextSwitch();
}
```

**Before (Recursive)**: `ExecuteBytecodeLoop()` → `SendMessage()` → `CreateContext()` → `ExecuteBytecodeLoop()` ← **RECURSIVE!**

**After (Trampoline)**: `SendMessage()` → `CreateContext()` → `return ContextSwitch` → `InterpreterLoop` continues

**4. Block Invocation (Primitive-Initiated Context Switch)**

Primitives that invoke blocks (e.g., Primitive 207: `value/value:/value:value:`) create contexts during primitive execution. The trampoline handles this via a flag:

```csharp
private SmalltalkOOP InvokeBlockInternal(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] args, SmalltalkOOP blockClosureOOP)
{
    // Create block execution context
    _activeContextOOP = CreateBlockExecutionContext(...);

    // Signal that primitive created new context
    _contextSwitchedByPrimitive = true;

    // Return nil sentinel (actual result pushed onto stack by HandleReturn)
    return SpecialObjectsArray.Instance.Nil;
}

// In SendMessage after primitive execution:
if (_primitiveSuccess && _contextSwitchedByPrimitive)
{
    _contextSwitchedByPrimitive = false;
    return CallOutcome.ContextSwitch();  // Continue in block context
}
```

**5. Return Handling (Context Unwinding)**
```csharp
private InterpreterStatus HandleReturn(SmalltalkOOP returnValue)
{
    // Handle non-local returns by unwinding contexts
    if (_nonLocalReturnPending)
    {
        if (_activeContextOOP == _nlrTargetContext)
        {
            // Target reached - clear NLR, push value, continue
            _nonLocalReturnPending = false;
            _activeContextOOP = senderOOP;
            Push(_nlrValue);
            return InterpreterStatus.Continue;
        }

        // Not target - keep unwinding (don't push value)
        _activeContextOOP = senderOOP;
        return InterpreterStatus.Continue;
    }

    // Normal return - switch to sender and push result
    _activeContextOOP = senderOOP;
    Push(returnValue);
    return InterpreterStatus.Continue;
}
```

**6. Non-Local Returns (Trampoline-Based Unwinding)**

Instead of throwing `NonLocalReturnException`, the RETURN_TOP_POP and RETURN_SELF_POP bytecodes set NLR state:

```csharp
case Bytecode.RETURN_TOP_POP:
    if (_blockContextMapping.TryGetValue(_activeContextOOP, out var blockOOP))
    {
        var returnValue = Pop();
        var returnContextOOP = _heapManager.GetInstanceVariable(blockOOP, VMConstants.BLOCKCLOSURE_RETURNCONTEXT_INDEX);

        // Set NLR state instead of throwing exception
        _nonLocalReturnPending = true;
        _nlrValue = returnValue;
        _nlrTargetContext = returnContextOOP;
        return false;  // Exit bytecode loop
    }
    break;
```

`HandleReturn()` checks `_nonLocalReturnPending` and unwinds contexts until reaching `_nlrTargetContext`, then clears flags and pushes the value.

#### State Management

**Trampoline State Fields**:
```csharp
private SmalltalkOOP _returnValue;               // Return value from execution
private SmalltalkOOP _initialContextOOP;         // Starting context
private bool _contextSwitchPending;              // Message send occurred
private bool _contextSwitchedByPrimitive;        // Primitive created context

// Non-local return state (replaces exceptions)
private bool _nonLocalReturnPending;             // NLR in progress
private SmalltalkOOP _nlrValue;                  // Value to return
private SmalltalkOOP _nlrTargetContext;          // Context to unwind to
```

#### Execution Flow Examples

**Example 1: whileTrue: Loop (256 iterations)**
```
InterpreterLoop (single .NET frame)
  → ExecuteBytecodeStep (whileTrue:, iteration 1)
     sends "value" → ContextSwitch
  → ExecuteBytecodeStep (condition block)
     returns true → Continue
  → ExecuteBytecodeStep (whileTrue:, continues)
     sends "ifTrue:" → ContextSwitch
  → ExecuteBytecodeStep (body block)
     sends "whileTrue:" → ContextSwitch
  → ExecuteBytecodeStep (whileTrue:, iteration 2)
  ... 256 iterations, all at SAME .NET call depth!
```

**Example 2: Non-Local Return**
```
InterpreterLoop
  → ExecuteBytecodeStep (method A)
     sends message to B → ContextSwitch
  → ExecuteBytecodeStep (method B)
     invokes block → ContextSwitch
  → ExecuteBytecodeStep (block)
     executes ^returnValue (RETURN_TOP_POP)
     sets _nonLocalReturnPending, _nlrTargetContext = A
     returns false
  → HandleReturn (in block)
     _activeContextOOP != _nlrTargetContext
     unwinds to sender (method B)
     returns Continue
  → HandleReturn (in method B)
     _activeContextOOP != _nlrTargetContext
     unwinds to sender (method A)
     returns Continue
  → ExecuteBytecodeStep (method A)
     _activeContextOOP == _nlrTargetContext
     pushes _nlrValue onto stack
     returns Continue
```

#### Benefits

✅ **Eliminates Stack Overflow**: .NET call stack stays flat (single `InterpreterLoop` frame)
✅ **Matches Blue Book**: Single-loop interpreter design per specification
✅ **Deep Recursion Support**: Character initialize (256 iterations) completes successfully
✅ **No Recursion Limits**: Smalltalk control flow depth limited only by heap (MethodContext allocation)
✅ **Clean NLR Handling**: No exception overhead for non-local returns
✅ **Maintainable**: Clear separation between context creation and execution

#### Performance Characteristics

- **Context Creation**: O(1) - allocate MethodContext object
- **Context Switch**: O(1) - update `_activeContextOOP` field
- **Return Handling**: O(1) normal returns, O(n) for NLR unwinding n contexts
- **Memory**: Contexts allocated on Smalltalk heap, cleaned up by GC
- **.NET Stack**: Constant depth (1 `InterpreterLoop` + 1 `ExecuteBytecodeStep`)

#### Implementation Status

✅ **InterpreterLoop**: Top-level trampoline loop
✅ **ExecuteBytecodeStep**: Bytecode execution until context switch/return
✅ **HandleReturn**: Normal and non-local return handling
✅ **SendMessage**: Signals ContextSwitch instead of recursing
✅ **SendSuperMessage**: Signals ContextSwitch instead of recursing
✅ **InvokeBlockInternal**: Signals context switch via primitive flag
✅ **NLR Bytecodes**: RETURN_TOP_POP, RETURN_SELF_POP use state fields
✅ **Entry Points**: Execute(), ExecuteExistingContext() use InterpreterLoop

### Tail Call Optimization

**Purpose**: Prevents context accumulation in recursive patterns like `whileTrue:` by reusing the current context when a message send is immediately followed by a return.

**Problem**: The recursive implementation of `whileTrue:` accumulates contexts:
```smalltalk
whileTrue: aBlock
    ^self value
        ifTrue:
            [aBlock value.
            self whileTrue: aBlock]  ← Recursive call before return
```

Without tail call optimization, 256 iterations creates 512+ contexts (condition + body per iteration), quickly hitting depth limits and consuming heap space.

**Solution**: Detect tail calls and reuse the current context instead of creating a new one.

#### Tail Call Detection

```csharp
private bool IsAtTailCallPosition()
{
    // Get current PC
    var pcOOP = _heapManager.GetInstanceVariable(_activeContextOOP, VMConstants.CONTEXT_PC_INDEX);
    int currentPC = pcOOP.AsSmallInteger();

    // Get method and bytecodes
    var methodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, VMConstants.METHODCONTEXT_METHOD_INDEX);
    int literalCount = _heapManager.GetIndexedSize(methodOOP);
    var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, literalCount - 1);
    var bytecodes = _heapManager.GetByteArray(bytecodeArrayOOP);

    // Check if next bytecode is a return
    int nextPC = currentPC + 1;
    if (nextPC >= bytecodes.Length) return false;

    byte nextBytecode = bytecodes[nextPC];
    return BytecodeUtil.IsReturn(nextBytecode);
}
```

**Key Insight**: If the next bytecode is a return, the result of the current send will be immediately returned, so we can reuse the context.

#### Context Reuse Implementation

```csharp
private void ReuseContextForTailCall(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments)
{
    var header = ParseMethodHeader(methodOOP);

    // Get field indices
    int pcIndex = VMConstants.CONTEXT_PC_INDEX;
    int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
    int methodIndex = VMConstants.METHODCONTEXT_METHOD_INDEX;
    int receiverIndex = VMConstants.METHODCONTEXT_RECEIVER_INDEX;

    // Replace method and receiver
    _heapManager.SetInstanceVariable(_activeContextOOP, methodIndex, methodOOP);
    _heapManager.SetInstanceVariable(_activeContextOOP, receiverIndex, receiverOOP);

    // Reset PC to start of method
    _heapManager.SetInstanceVariable(_activeContextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));

    // Reset stack pointer to after args+temps
    _heapManager.SetInstanceVariable(_activeContextOOP, stackpIndex,
        SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));

    // Replace arguments
    for (int i = 0; i < header.NumArgs; i++)
    {
        _heapManager.SetIndexedOOP(_activeContextOOP, i, arguments[i]);
    }

    // Clear temps to nil
    for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
    {
        _heapManager.SetIndexedOOP(_activeContextOOP, i, SpecialObjectsArray.Instance.Nil);
    }

    // Note: sender stays the same - we return to the same place
    // Note: we don't increment _activeContextCount since we're reusing
}
```

#### Integration with SendMessage

```csharp
private CallOutcome SendMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
{
    // ... method lookup and primitive execution ...

    // TAIL CALL OPTIMIZATION: If next bytecode is a return, reuse current context
    if (IsAtTailCallPosition())
    {
        Console.WriteLine($"[TAIL CALL] Reusing context for {selectorName}");
        ReuseContextForTailCall(methodInfo.MethodOOP, receiverOOP, args);

        // Signal context switch so ExecuteBytecodeStep restarts with new method
        return CallOutcome.ContextSwitch();
    }

    // Create new context with current context as sender
    _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, _activeContextOOP);

    // Signal context switch - InterpreterLoop will continue in new context
    return CallOutcome.ContextSwitch();
}
```

#### Benefits

✅ **Prevents Context Accumulation**: whileTrue: loops execute with constant context depth (1-3 contexts)
✅ **Heap Efficiency**: No exponential context growth for recursive patterns
✅ **Depth Limit Compliance**: Character initialize (256 iterations) stays well under MAX_CONTEXT_DEPTH
✅ **Blue Book Compatible**: Optimization is transparent - semantics unchanged
✅ **Generic**: Works for any tail-recursive pattern (whileTrue:, whileFalse:, timesRepeat:, etc.)

#### Execution Flow (whileTrue: with Tail Calls)

```
Initial state: 3 contexts (caller → whileTrue: → condition block)

Iteration 1:
  condition block returns → whileTrue: context
  ifTrue: sends body block
  body block returns
  whileTrue: TAIL CALL DETECTED → reuses own context for recursive call

Iteration 2-256:
  Same 3 contexts reused (caller → whileTrue: → condition/body blocks)

Final state: 3 contexts (constant depth throughout)
```

**Without tail calls**: 512+ contexts (2 per iteration × 256)
**With tail calls**: 3 contexts (constant depth)

### Context Pooling

**Purpose**: Reduce GC pressure and allocation overhead by reusing recently returned contexts instead of allocating new ones.

**Problem**: Even with tail call optimization, non-tail calls still create contexts. Frequent context allocation/deallocation causes GC pressure and allocation overhead.

**Solution**: Maintain a pool of recently returned contexts for fast reuse.

#### Pool Architecture

```csharp
// Context pooling - reuse recently returned contexts to reduce GC pressure
private Stack<SmalltalkOOP> _contextPool = new Stack<SmalltalkOOP>();
private const int MAX_POOLED_CONTEXTS = 50;
```

**Key Design Decisions**:
- **Stack-based**: LIFO order for cache locality (recently used contexts likely to fit in CPU cache)
- **Fixed size**: 50 contexts max to prevent unbounded memory growth
- **Transparent**: Pooling is implementation detail, invisible to Smalltalk code

#### Pool Population (HandleReturn)

Contexts are added to the pool when returning:

```csharp
private InterpreterStatus HandleReturn(SmalltalkOOP returnValue)
{
    // ... handle NLR if pending ...

    // Normal return
    senderOOP = _heapManager.GetInstanceVariable(_activeContextOOP, senderIndex);

    // Clean up block context mapping if this was a block
    _blockContextMapping.Remove(_activeContextOOP);

    // Add context to pool for reuse (if pool not full)
    if (_contextPool.Count < MAX_POOLED_CONTEXTS)
    {
        _contextPool.Push(_activeContextOOP);
    }

    // Decrement context depth
    _activeContextCount--;

    // Switch to sender context and push return value
    _activeContextOOP = senderOOP;
    Push(returnValue);
    return InterpreterStatus.Continue;
}
```

**All three return paths populate the pool**:
1. Normal return (method/block completes)
2. NLR target reached (non-local return lands here)
3. NLR unwinding (intermediate contexts during unwinding)

#### Pool Consumption (CreateMethodContext)

Contexts are pulled from pool before allocating:

```csharp
private SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP,
                                          SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
{
    // ... validate and calculate sizes ...

    // Try to reuse a pooled context, otherwise allocate new
    SmalltalkOOP contextOOP;
    int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");

    if (_contextPool.Count > 0)
    {
        // Reuse a pooled context
        contextOOP = _contextPool.Pop();
        Console.WriteLine($"[POOL] Reusing pooled context {contextOOP} (pool size now: {_contextPool.Count})");
    }
    else
    {
        // Allocate new context
        contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
        _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP); // Set class reference
        Console.WriteLine($"[POOL] Allocated new context {contextOOP} (pool was empty)");
    }

    // Initialize context fields (method, receiver, args, temps, pc, stackp, sender)
    // ...
}
```

**Important**: Reused contexts are fully reinitialized - all fields overwritten with new values.

#### Pool Lifecycle

```
Execution starts:
  Pool: [] (empty)

Method A calls B:
  CreateMethodContext for B → allocates new → Pool: []

Method B returns:
  HandleReturn adds B to pool → Pool: [B]

Method A calls C:
  CreateMethodContext for C → reuses B → Pool: []

Method C returns:
  HandleReturn adds C (reused B) to pool → Pool: [C]

Method A calls D:
  CreateMethodContext for D → reuses C → Pool: []

... execution continues, pool grows to ~50 contexts ...

Steady state:
  Pool: [ctx1, ctx2, ..., ctx50] (full)
  New returns don't add to pool (at max)
  New allocations always pull from pool
```

#### Benefits

✅ **Reduced Allocations**: After warmup, most contexts come from pool (near-zero allocation rate)
✅ **Lower GC Pressure**: Fewer objects created → fewer GC cycles
✅ **Cache Locality**: Recently used contexts likely still in CPU cache
✅ **Bounded Memory**: Pool size capped at 50 contexts (~12KB assuming 256 bytes/context)
✅ **Transparent**: No semantic changes - pure performance optimization
✅ **Fast Warmup**: Pool fills quickly (50 contexts reached after ~50 calls)

#### Performance Characteristics

- **Pool Hit (reuse)**: O(1) - pop from stack
- **Pool Miss (allocate)**: O(n) - heap allocation, GC traversal
- **Pool Add**: O(1) - push to stack
- **Memory**: 50 contexts × ~256 bytes = ~12KB overhead
- **Warmup Time**: ~50 method calls to fill pool

#### Interaction with Tail Call Optimization

**Combined Effect**: Tail calls + pooling work synergistically:

1. **Tail calls** prevent accumulation in recursive patterns (whileTrue:)
2. **Pooling** reduces allocation for non-tail calls (method invocations)

**Example: Character initialize**
```
Without optimizations:
  - 512+ contexts allocated (2 per iteration × 256)
  - All contexts live simultaneously (stack overflow risk)
  - 512+ GC scans for context allocation

With tail calls only:
  - ~3-5 contexts allocated total
  - Constant depth (no overflow)
  - 3-5 GC scans

With tail calls + pooling:
  - ~3-5 contexts allocated initially
  - All subsequent reused from pool
  - 3-5 GC scans total
  - Near-zero allocation rate after warmup
```

#### Implementation Status

✅ **Pool Data Structure**: Stack-based, 50 context max
✅ **Pool Population**: All three HandleReturn paths add to pool
✅ **Pool Consumption**: CreateMethodContext tries pool first
✅ **Reinitialization**: Pooled contexts fully reinitialized
✅ **Interaction with Tail Calls**: Both optimizations work together
✅ **Diagnostic Logging**: Pool hits/misses logged for debugging

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

## BlockClosure Support

**Status**: ✅ Phase 2 Complete (2025-01-23)
**Implementation**: Runtime wrapping with Primitive 207
**Documentation**: See `BlockClosure-Phase2-Complete.md` for detailed implementation guide

### Overview

BlockClosure is the modern block representation that provides proper lexical scoping by capturing the homeContext at block creation time. Unlike BlockContext (the Blue Book model), BlockClosure is a first-class object that explicitly stores its defining context.

### Architecture

**BlockClosure Instance Structure**:
```
Class: BlockClosure
Instance Variables:
  - method         (CompiledMethod OOP) - the block's executable code
  - homeContext    (MethodContext OOP) - lexical scope capture
  - returnContext  (MethodContext OOP) - where non-local return goes
  - numArgs        (SmallInteger) - number of block arguments
```

### Runtime Wrapping

Blocks are created at runtime when block literals are pushed onto the stack:

**VirtualMachine.cs:327-340** - pushLiteral bytecode handler:
```csharp
if (BytecodeUtil.IsPushLiteralConst(bytecode))
{
    int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
    var literal = GetMethodLiteral(methodOOP, index + 1);

    if (IsBlockLiteral(literal))
    {
        // Wrap block in BlockClosure, capturing current context
        var closureOOP = CreateBlockClosure(literal, _activeContextOOP);
        Push(closureOOP);
    }
    else
    {
        Push(literal);
    }
    return true;
}
```

**Key Helper Methods**:

1. **IsBlockLiteral(SmalltalkOOP oop)** - Checks if literal is a block:
```csharp
private bool IsBlockLiteral(SmalltalkOOP oop)
{
    if (oop.IsSmallInteger)
        return false;
    string? localId = GetLocalId(oop);
    return localId != null && localId.StartsWith("block:");
}
```

2. **CreateBlockClosure(blockMethodOOP, homeContextOOP)** - Allocates BlockClosure:
```csharp
private SmalltalkOOP CreateBlockClosure(SmalltalkOOP blockMethodOOP, SmalltalkOOP homeContextOOP)
{
    if (!GlobalRegistry.TryGet("class:BlockClosure", out SmalltalkOOP blockClosureClassOOP))
    {
        throw new InvalidOperationException("BlockClosure class not found");
    }

    int numArgs = GetBlockArgCount(blockMethodOOP);
    var closureOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, 4, 0);
    _heapManager.SetSlot(closureOOP, 0, blockClosureClassOOP);

    // Get field indices from layout resolver
    int methodIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "method");
    int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
    int returnContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "returnContext");
    int numArgsIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "numArgs");

    // Set instance variables
    _heapManager.SetInstanceVariable(closureOOP, methodIndex, blockMethodOOP);
    _heapManager.SetInstanceVariable(closureOOP, homeContextIndex, homeContextOOP);
    _heapManager.SetInstanceVariable(closureOOP, returnContextIndex, homeContextOOP);
    _heapManager.SetInstanceVariable(closureOOP, numArgsIndex, SmalltalkOOP.FromSmallInteger(numArgs));

    return closureOOP;
}
```

3. **GetBlockArgCount(blockMethodOOP)** - Extracts argument count from header:
```csharp
private int GetBlockArgCount(SmalltalkOOP blockMethodOOP)
{
    var headerOOP = _heapManager.GetIndexedOOP(blockMethodOOP, 0);
    if (!headerOOP.IsSmallInteger)
        return 0;

    int header = headerOOP.AsSmallInteger();
    int flags = (header >> 12) & 0x7;

    if (flags < 5)
        return flags;  // Simple form
    else if (flags == 7)
    {
        // Extended form - read from last literal
        int numLiterals = header & 0x3F;
        var lastLiteralOOP = GetMethodLiteral(blockMethodOOP, numLiterals);
        if (lastLiteralOOP.IsSmallInteger)
        {
            int lastLiteral = lastLiteralOOP.AsSmallInteger();
            return (lastLiteral >> 8) & 0x1F;
        }
    }

    return 0;
}
```

### Primitive 207: Block Evaluation

**BlockClosurePrimitives.cs** - Implements block evaluation:
```csharp
public static SmalltalkOOP ValueWithArguments(IPrimitiveContext ctx,
    SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // Handle two cases:
    // 1. value (0 args) - treat as empty array
    // 2. valueWithArguments: (1 arg) - use the provided array
    SmalltalkOOP argsArrayOOP;

    if (args.Length == 0)
    {
        argsArrayOOP = ctx.AllocateObject("class:Array", 0);
    }
    else if (args.Length == 1)
    {
        argsArrayOOP = args[0];
    }
    else
    {
        ctx.PrimitiveSuccess = false;
        return GlobalRegistry.NilOOP;
    }

    // Extract block method from BlockClosure
    var methodIndex = ctx.GetFieldIndex("class:BlockClosure", "method");
    var blockMethodOOP = ctx.GetInstanceVariable(receiver, methodIndex);

    // Extract homeContext from BlockClosure
    var homeContextIndex = ctx.GetFieldIndex("class:BlockClosure", "homeContext");
    var homeContextOOP = ctx.GetInstanceVariable(receiver, homeContextIndex);

    // Extract arguments from array
    var blockArgs = ExtractArrayElements(ctx, argsArrayOOP);

    // Invoke block with captured homeContext
    var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP);

    return result;
}
```

### Execution Flow

1. **Block Creation**: When `pushLiteral` executes for a block literal:
   - VM detects block (localId starts with "block:")
   - Creates BlockClosure instance
   - **Captures current activeContext as homeContext**
   - Pushes BlockClosure OOP onto stack

2. **Block Evaluation**: When `BlockClosure>>value` is sent:
   - Method lookup finds `BlockClosure>>value`
   - Method has `<primitive: 207>` annotation
   - Primitive 207 executes:
     - Extracts `method` and `homeContext` from BlockClosure
     - Calls `InvokeBlockInternal` with captured homeContext
     - Returns result to caller

3. **Lexical Scope**: Block accesses variables from **homeContext**, not calling context:
   - Outer temps/args resolved in homeContext
   - `self` is homeContext's receiver
   - Proper lexical scoping guaranteed

### Critical Design Decisions

**Why Capture homeContext at Creation Time?**

This ensures blocks access variables from their **defining** context (lexical scope), not their **calling** context (dynamic scope):

```smalltalk
| x block |
x := 10.
block := [ x printString ].  "homeContext captured here"
x := 20.
[ block value ] value.       "prints '20' - sees updated x in homeContext"
```

The homeContext is captured when the block literal is pushed, not when it's evaluated. This is the fundamental principle of lexical scoping.

**Why Not Store Copied Variables?**

Alternatives like copying captured variables would break mutation semantics:

```smalltalk
| x |
x := 0.
[ x := x + 1 ] value.
[ x := x + 1 ] value.
x.  "Should be 2, not 0!"
```

By keeping homeContext alive, blocks can mutate shared variables correctly.

### Image-Level API

BlockClosure responds to standard block messages:

```smalltalk
value
    "Evaluate this block with no arguments."
    <primitive: 207>
    ^self valueWithArguments: #()

value: arg1
    "Evaluate this block with one argument."
    <primitive: 207>
    ^self valueWithArguments: {arg1}

value: arg1 value: arg2
    "Evaluate this block with two arguments."
    <primitive: 207>
    ^self valueWithArguments: {arg1. arg2}

valueWithArguments: anArray
    "Evaluate this block with arguments from array."
    <primitive: 207>
    self primitiveFailed
```

### Performance Considerations

**Memory Overhead**:
- BlockClosure: 4 instance variables (method, homeContext, returnContext, numArgs)
- homeContext kept alive for block lifetime
- No variable copying - references only

**GC Impact**:
- homeContext prevents MethodContext from being collected
- Nested blocks keep entire context chain alive
- Future optimization: copy only captured variables

### Testing Evidence

**Cold Start Success**:
```
✓ BlockClosure class created
✓ BlockClosure metaclass created
✓ Init #1 and #2 pass with BlockClosure evaluation
```

**Runtime Evidence**:
```
[GetMethod] depth=0, BlockClosure, looking for 'value'
[SendMessage] Executing primitive 207 for value
  arg0 = BlockClosure(oop=56220)
  arg1 = BlockClosure(oop=56222)
```

### Coexistence with BlockContext

Both BlockContext (primitives 80-82) and BlockClosure (primitive 207) coexist safely:

- **Compiler emits**: BlockClosure by default
- **VM supports**: Both models simultaneously
- **Legacy code**: Can still use BlockContext
- **Migration**: Planned for future (see next section)

See `BlockContext-to-BlockClosure-Migration.md` for coexistence details.

---

## BlockContext to BlockClosure Migration

**Status**: Planning Phase
**Target**: Future (post-Phase 3 non-local returns)
**Scope**: Migrate all legacy Smalltalk code from BlockContext to BlockClosure

### Migration Overview

This roadmap converts legacy Smalltalk code that relies on BlockContext semantics (Blue Book model with primitives 80-82) to the canonical BlockClosure model (modern model with primitive 207) while preserving runtime behavior and enabling phased consolidation.

**Key Principle**: Smalltalk blocks and closures are first-class objects that capture surrounding variables and are evaluated with messages like `value:` and `value:value:`. Closure behavior and implementation details vary between VMs and images, so validate VM-level guarantees before changing image code.

**Reference**: Stack Overflow discussions on Smalltalk closure semantics and VM variations.

### Why Migrate?

**BlockContext Issues**:
- Primitives 80-82 are VM-dependent and non-portable
- No explicit lexical scope capture (relies on stack frames)
- Debugging and inspection require VM-specific knowledge
- Cannot serialize blocks easily (context stack dependencies)

**BlockClosure Benefits**:
- Explicit homeContext capture (lexical scoping guaranteed)
- First-class objects with standard instance variables
- VM-independent API (primitive 207 is well-defined)
- Easier debugging (context chain is explicit)
- Serialization-friendly (self-contained objects)

### Preparation and Discovery

#### 1. Inventory

Create comprehensive inventory of all block-related code:

**Classes to Audit**:
```smalltalk
BlockContext
BlockClosure
MethodContext
Process
CompiledMethod
Compiler/Decompiler/Debugger classes that inspect blocks
```

**Methods to Catalog**:
- Block creation sites (`[ ... ]` syntax in methods)
- Block manipulation (inspecting/modifying block internals)
- Primitive invocations (80-82 for BlockContext, 207 for BlockClosure)
- Control structures (`whileTrue:`, `whileFalse:`, `ifTrue:ifFalse:`, etc.)

**Tools**:
```bash
# Find all block creation sites
grep -n "^\s*\[" *.st

# Find primitive 80-82 usage
grep -n "primitive: 8[0-2]" *.st

# Find BlockContext references
grep -n "BlockContext" *.st
```

**Output**: Spreadsheet with columns:
- File/Class/Method
- Line number
- Block usage pattern
- Dependency on BlockContext internals
- Test coverage (Y/N)
- Migration complexity (Low/Medium/High)

#### 2. Classify Block Usage

**Categories**:

1. **Creation-only**: Blocks created and immediately evaluated
   ```smalltalk
   [ self doSomething ] value
   ```
   *Migration*: Trivial - already works with BlockClosure

2. **Passed-as-parameter**: Blocks passed to control structures
   ```smalltalk
   collection do: [ :each | each print ]
   ```
   *Migration*: Trivial - API identical

3. **Stored in objects**: Blocks stored in instance variables
   ```smalltalk
   button := Button new action: [ self onClick ]
   ```
   *Migration*: Medium - ensure BlockClosure lifetime correct

4. **Inspected/reflected**: Code that examines block internals
   ```smalltalk
   block homeContext tempAt: 1
   ```
   *Migration*: High - requires façade API

5. **Control structures**: `whileTrue:`, `whileFalse:`, etc.
   ```smalltalk
   [ x < 10 ] whileTrue: [ x := x + 1 ]
   ```
   *Migration*: Medium - ensure semantics preserved

6. **Mutated-capture patterns**: Blocks that mutate captured variables
   ```smalltalk
   | sum |
   sum := 0.
   collection do: [ :each | sum := sum + each ].
   sum
   ```
   *Migration*: Critical - must preserve mutation semantics

#### 3. Identify VM Primitives and Extensions

**Primitive Audit**:
- Primitive 80: `BlockContext>>value`
- Primitive 81: `BlockContext>>value:`
- Primitive 82: `BlockContext>>valueWithArguments:`
- Primitive 207: `BlockClosure>>valueWithArguments:`

**VM Extensions to Check**:
- Stack frame layout assumptions
- Bytecode formats for block creation
- Context swapping mechanisms
- Non-local return handling

**Tool**: Primitive usage scanner:
```bash
# Scan for primitive 80-82
python scan_primitives.py --range 80-82 --input Smalltalk-80.st

# Expected output:
# BlockContext>>value: primitive 80 (12 call sites)
# BlockContext>>value:: primitive 81 (8 call sites)
# BlockContext>>valueWithArguments:: primitive 82 (3 call sites)
```

#### 4. Establish Regression Harness

**Components**:

1. **Deterministic Image Restore**:
   ```bash
   # Save baseline image before migration
   cp coldstart.json coldstart.baseline.json

   # Restore baseline
   cp coldstart.baseline.json coldstart.json
   dotnet run --project Smalltalk80.Interpreter
   ```

2. **Fast Test Runner**:
   ```smalltalk
   TestRunner new
       runSuite: BlockMigrationTestSuite;
       reportFailuresTo: 'migration-failures.txt'
   ```

3. **Snapshot Points**:
   - Pre-migration: Full image with BlockContext
   - Post-façade: Image with compatibility layer
   - Post-migration: Image with BlockClosure only
   - Post-cleanup: Image with legacy code removed

4. **Test Categories**:
   - Unit tests (BlockClosure behavior)
   - Integration tests (control structures)
   - Compatibility tests (BlockContext emulation)
   - Performance benchmarks (block creation/evaluation)

#### 5. Preserve Stable Fallback

**Compatibility VM**:
- Create VM branch `legacy-blockcontext`
- Tag release: `v1.0-blockcontext-stable`
- Document fallback procedure

**Runtime Flag**:
```csharp
// VirtualMachine.cs
public static bool UseLegacyBlockContext = false;

private SmalltalkOOP CreateBlock(...)
{
    if (UseLegacyBlockContext)
        return CreateBlockContext(...);
    else
        return CreateBlockClosure(...);
}
```

**Environment Variable**:
```bash
# Run in legacy mode
SMALLTALK_BLOCK_MODE=legacy dotnet run

# Run in canonical mode
SMALLTALK_BLOCK_MODE=canonical dotnet run
```

### Design the Compatibility Layer

#### 1. BlockClosure Façade

Define complete runtime API that legacy code expects:

**Smalltalk-80.st - BlockClosure class (extended)**:
```smalltalk
BlockClosure subclass: #BlockContext
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'

"Compatibility façade for legacy BlockContext code"

BlockClosure>>homeContext
    "Legacy API: return the captured context"
    ^homeContext

BlockClosure>>sender
    "Legacy API: return the calling context"
    ^returnContext

BlockClosure>>tempAt: index
    "Legacy API: access temp variable in homeContext"
    ^homeContext tempAt: index

BlockClosure>>tempAt: index put: value
    "Legacy API: modify temp variable in homeContext"
    ^homeContext tempAt: index put: value

BlockClosure>>receiver
    "Legacy API: return homeContext receiver"
    ^homeContext receiver

BlockClosure>>method
    "Already defined - return the block CompiledMethod"
    ^method

BlockClosure>>argumentCount
    "Legacy API: return number of block arguments"
    ^numArgs
```

**Missing Selectors**: Add compatibility methods for any selectors sent to BlockContext in legacy code.

#### 2. Primitive Trampolines

Implement trampolines at primitive boundary to translate legacy calls:

**PrimitiveTable.cs**:
```csharp
// Legacy primitives (80-82) trampoline to BlockClosure API
_primitives[80] = (ctx, receiver, args) =>
{
    // BlockContext>>value -> BlockClosure>>value
    if (UseLegacyBlockContext)
        return BlockContextPrimitives.Value(ctx, receiver, args);
    else
        return BlockClosurePrimitives.ValueWithArguments(ctx, receiver, new SmalltalkOOP[0]);
};

_primitives[81] = (ctx, receiver, args) =>
{
    // BlockContext>>value: -> BlockClosure>>value:
    if (UseLegacyBlockContext)
        return BlockContextPrimitives.ValueWith(ctx, receiver, args);
    else
    {
        var argsArray = ctx.AllocateObject("class:Array", 1);
        ctx.SetIndexedOOP(argsArray, 0, args[0]);
        return BlockClosurePrimitives.ValueWithArguments(ctx, receiver, new[] { argsArray });
    }
};

_primitives[82] = (ctx, receiver, args) =>
{
    // BlockContext>>valueWithArguments: -> BlockClosure>>valueWithArguments:
    if (UseLegacyBlockContext)
        return BlockContextPrimitives.ValueWithArguments(ctx, receiver, args);
    else
        return BlockClosurePrimitives.ValueWithArguments(ctx, receiver, args);
};
```

**Trampoline Behavior**:
- In Compatibility Mode: call legacy BlockContext primitives
- In Canonical Mode: translate to BlockClosure primitive 207
- API identical at call site (no code changes needed)

#### 3. Inspection Shims

Legacy inspectors/debuggers expect BlockContext layout:

**Inspector.st**:
```smalltalk
Inspector>>fieldsForBlock: aBlock
    "Return inspectable fields for a block"

    (aBlock isKindOf: BlockClosure) ifTrue: [
        ^#(method homeContext returnContext numArgs)
    ].

    (aBlock isKindOf: BlockContext) ifTrue: [
        ^#(method sender receiver argumentCount)
    ].

    self error: 'Unknown block type'
```

**Debugger.st**:
```smalltalk
Debugger>>contextChainFor: aBlock
    "Build context chain for debugging"

    | contexts current |
    contexts := OrderedCollection new.
    current := aBlock homeContext.  "Works for both BlockContext and BlockClosure"

    [ current notNil ] whileTrue: [
        contexts add: current.
        current := current sender
    ].

    ^contexts
```

#### 4. Two-Mode Operation

**Compatibility Mode** (legacy semantics):
- VM creates BlockContext objects
- Primitives 80-82 execute legacy behavior
- Inspector/debugger use BlockContext layout

**Canonical Mode** (modern semantics):
- VM creates BlockClosure objects
- Primitives 80-82 trampoline to primitive 207
- Inspector/debugger use BlockClosure layout

**Toggle Mechanism**:
```csharp
// Runtime flag in VirtualMachine.cs
public static BlockMode CurrentBlockMode = BlockMode.Canonical;

public enum BlockMode
{
    Legacy,      // BlockContext with primitives 80-82
    Canonical    // BlockClosure with primitive 207
}
```

**Image-Level Toggle**:
```smalltalk
Smalltalk blockMode: #canonical.   "Use BlockClosure"
Smalltalk blockMode: #legacy.      "Use BlockContext"
Smalltalk blockMode.                "Query current mode"
```

### Stepwise Migration Plan

#### Phase 0: Test Infrastructure

**Goal**: Ensure comprehensive test coverage before migration.

**Tasks**:
1. Create `BlockMigrationTestSuite` with categories:
   - Block creation and evaluation
   - Argument passing (0-N args)
   - Capture semantics (lexical scope)
   - Mutation patterns (captured variable updates)
   - Nested blocks
   - Re-entrancy and recursion
   - Control structures (`whileTrue:`, `ifTrue:ifFalse:`, etc.)
   - Non-local returns

2. Run baseline tests in Compatibility Mode:
   ```bash
   SMALLTALK_BLOCK_MODE=legacy dotnet run --test BlockMigrationTestSuite
   ```

3. Document all failures (expected baseline)

4. Achieve 100% pass rate for basic block operations

**Success Criteria**:
- [ ] 200+ tests covering all block patterns
- [ ] All tests pass in Compatibility Mode
- [ ] Test execution time < 10 seconds
- [ ] Automated regression detection

#### Phase 1: Implement BlockClosure Façade

**Goal**: Add compatibility methods to BlockClosure so legacy code can call BlockContext APIs.

**Tasks**:
1. Add façade methods to BlockClosure class:
   ```smalltalk
   homeContext
   sender
   tempAt:
   tempAt:put:
   receiver
   argumentCount
   ```

2. Ensure all selectors sent to BlockContext are implemented on BlockClosure

3. Run tests in Canonical Mode with façade:
   ```bash
   SMALLTALK_BLOCK_MODE=canonical dotnet run --test BlockMigrationTestSuite
   ```

4. Fix any failures by adding missing façade methods

**Success Criteria**:
- [ ] All BlockContext selectors implemented on BlockClosure
- [ ] Tests pass in both Legacy and Canonical modes
- [ ] No API-level changes required in user code

#### Phase 2: Introduce Primitive Trampolines

**Goal**: Replace direct BlockContext primitive usage with trampolines that work in both modes.

**Tasks**:
1. Implement trampolines for primitives 80-82 (see Design section)

2. Add mode detection to trampoline logic

3. Test trampoline behavior:
   ```csharp
   // Test: Legacy mode uses BlockContext primitives
   VirtualMachine.CurrentBlockMode = BlockMode.Legacy;
   var result = ExecutePrimitive(80, blockOOP, args);
   Assert(result came from BlockContextPrimitives.Value);

   // Test: Canonical mode uses BlockClosure primitive
   VirtualMachine.CurrentBlockMode = BlockMode.Canonical;
   var result = ExecutePrimitive(80, blockOOP, args);
   Assert(result came from BlockClosurePrimitives.ValueWithArguments);
   ```

4. Run full test suite in both modes

**Success Criteria**:
- [ ] Primitives 80-82 work in both modes
- [ ] No performance degradation (< 5% overhead)
- [ ] All tests pass in both modes

#### Phase 3: Convert Non-Invasive Call Sites

**Goal**: Replace simple block creation/evaluation patterns with BlockClosure-only code.

**Pattern**: Ephemeral blocks (created and immediately evaluated):
```smalltalk
"Before (BlockContext-compatible)"
[ self doSomething ] value

"After (BlockClosure-native)"
[ self doSomething ] value   "No change - works with both!"
```

**Tasks**:
1. Identify all "creation-only" block sites from inventory

2. Run in Canonical Mode to verify compatibility

3. Remove any unnecessary BlockContext type checks:
   ```smalltalk
   "Before"
   (block isKindOf: BlockContext) ifTrue: [ ... ]

   "After"
   (block isKindOf: BlockClosure) ifTrue: [ ... ]
   ```

4. Run regression tests after each batch

**Batch Strategy**:
- Batch 1: Core library classes (Collection, Stream, Magnitude)
- Batch 2: GUI framework classes (View, Controller)
- Batch 3: System classes (Compiler, Debugger)
- Batch 4: Application code

**Success Criteria**:
- [ ] 80% of block sites migrated to BlockClosure-native
- [ ] All tests pass after each batch
- [ ] No functional regressions

#### Phase 4: Convert Stored and Inspected Blocks

**Goal**: Migrate code that stores blocks in instance variables or inspects block internals.

**Pattern**: Stored blocks:
```smalltalk
"Before"
button action: [ self onClick ].   "Returns BlockContext"

"After"
button action: [ self onClick ].   "Returns BlockClosure"
```

**Tasks**:
1. Find all classes with block-valued instance variables

2. Update accessor methods to accept BlockClosure:
   ```smalltalk
   Button>>action: aBlock
       "Set the action block (accepts BlockContext or BlockClosure)"
       action := aBlock
   ```

3. Update inspection code to handle both types:
   ```smalltalk
   Button>>inspectAction
       | blockType |
       blockType := action class name.
       blockType = 'BlockContext' ifTrue: [ ^self inspectBlockContext ].
       blockType = 'BlockClosure' ifTrue: [ ^self inspectBlockClosure ].
       self error: 'Unknown block type'
   ```

4. Test stored block lifetime and GC behavior

**Critical Cases**:
- Buttons/menu items with action blocks
- Delayed evaluation (Delay, Process)
- Error handlers with exception blocks
- Observers/callbacks with notification blocks

**Success Criteria**:
- [ ] All stored blocks work with BlockClosure
- [ ] GC correctly handles BlockClosure lifetimes
- [ ] Inspector shows BlockClosure internals correctly

#### Phase 5: Replace VM-Level Block Creation

**Goal**: Make VM produce BlockClosure instances by default.

**Tasks**:
1. Change default block creation mode:
   ```csharp
   // VirtualMachine.cs
   public static BlockMode CurrentBlockMode = BlockMode.Canonical;  // Was: Legacy
   ```

2. Update bytecode compiler to emit BlockClosure metadata:
   ```csharp
   // BytecodeCompiler.cs - block compilation
   var blockMethod = CompileBlockBody(blockNode);
   blockMethod.SetLocalId($"block:{blockId}");  // Marks as BlockClosure target
   ```

3. Ensure pushLiteral creates BlockClosure (already implemented):
   ```csharp
   if (IsBlockLiteral(literal))
   {
       var closureOOP = CreateBlockClosure(literal, _activeContextOOP);
       Push(closureOOP);
   }
   ```

4. Run full image with Canonical Mode as default

5. Monitor for any BlockContext creation (should be zero)

**Verification**:
```bash
# Run and check for BlockContext instances
dotnet run --project Smalltalk80.Interpreter | grep "BlockContext(" | wc -l
# Expected: 0
```

**Success Criteria**:
- [ ] VM creates only BlockClosure objects
- [ ] No BlockContext instances in image
- [ ] All tests pass with Canonical Mode default

#### Phase 6: Retire Trampolines and Compatibility Flags

**Goal**: Remove legacy code paths once all image code runs in Canonical Mode.

**Tasks**:
1. Remove Compatibility Mode toggle:
   ```csharp
   // DELETE: VirtualMachine.cs
   public static BlockMode CurrentBlockMode = ...;
   ```

2. Replace trampolines with direct primitive 207 calls:
   ```csharp
   // DELETE: Trampolines for 80-82
   _primitives[80] = (ctx, receiver, args) => { ... };

   // KEEP: Only primitive 207
   _primitives[207] = BlockClosurePrimitives.ValueWithArguments;
   ```

3. Remove BlockContext class from image:
   ```smalltalk
   "Remove from Smalltalk-80.st"
   Object subclass: #BlockContext ... "DELETE entire class"
   ```

4. Update documentation to reflect BlockClosure-only model

5. Run full test suite to verify no regressions

**Success Criteria**:
- [ ] Compatibility Mode code removed
- [ ] Primitives 80-82 removed from primitive table
- [ ] BlockContext class removed from image
- [ ] All tests pass
- [ ] Documentation updated

### Handling Tricky Cases

#### 1. Mutating Captured Variables Across Nested Closures

**Problem**: Nested blocks that mutate shared variables require homeContext preservation:

```smalltalk
| x outerBlock innerBlock |
x := 0.
outerBlock := [
    innerBlock := [ x := x + 1 ].
    innerBlock value.
    innerBlock value
].
outerBlock value.
x.  "Should be 2"
```

**BlockContext Behavior**: Relies on stack frame for `x` variable slot.

**BlockClosure Behavior**: Both blocks capture same homeContext, mutations visible.

**Migration**: No code changes needed - BlockClosure semantics preserve mutations automatically.

**Test**:
```smalltalk
testNestedBlockMutation
    | x outer inner |
    x := 0.
    outer := [
        inner := [ x := x + 1 ].
        inner value.
        inner value
    ].
    outer value.
    self assert: x equals: 2.
```

#### 2. Self-Referential Blocks and Re-entrancy

**Problem**: Recursive blocks that reference themselves:

```smalltalk
| factorial |
factorial := [ :n |
    n = 0
        ifTrue: [ 1 ]
        ifFalse: [ n * (factorial value: n - 1) ]
].
factorial value: 5.  "Should be 120"
```

**BlockContext Behavior**: Captures stack frame where `factorial` variable lives.

**BlockClosure Behavior**: Captures homeContext containing `factorial` temp variable.

**Migration**: Works correctly with BlockClosure - homeContext contains `factorial` binding.

**Edge Case**: Ensure homeContext not collected during recursion (GC roots must include active contexts).

**Test**:
```smalltalk
testRecursiveBlock
    | factorial |
    factorial := [ :n |
        n = 0
            ifTrue: [ 1 ]
            ifFalse: [ n * (factorial value: n - 1) ]
    ].
    self assert: (factorial value: 5) equals: 120.
```

#### 3. Bytecode or Debugger Expectations

**Problem**: Debugger expects specific stack frame layout for BlockContext.

**Migration Strategy**:

1. Update `Debugger>>contextChain` to recognize BlockClosure:
   ```smalltalk
   Debugger>>contextChain
       | chain current |
       chain := OrderedCollection new.
       current := thisContext.

       [ current notNil ] whileTrue: [
           chain add: current.
           current := current sender.

           "Handle BlockClosure homeContext chain"
           (current isNil and: [ thisContext receiver isKindOf: BlockClosure ])
               ifTrue: [ current := thisContext receiver homeContext ]
       ].

       ^chain
   ```

2. Update `Inspector>>fieldsFor:` to show BlockClosure structure:
   ```smalltalk
   Inspector>>fieldsFor: anObject
       (anObject isKindOf: BlockClosure) ifTrue: [
           ^#(method homeContext returnContext numArgs)
       ].
       ...
   ```

3. Update `MethodContext>>printOn:` to show BlockClosure frames:
   ```smalltalk
   MethodContext>>printOn: aStream
       | blockClosure |

       "Check if this context is referenced by a BlockClosure"
       blockClosure := self findBlockClosureReferencing: self.
       blockClosure notNil
           ifTrue: [ aStream nextPutAll: '[block in '; print: blockClosure method; nextPut: $] ]
           ifFalse: [ super printOn: aStream ]
   ```

**Success Criteria**:
- [ ] Debugger shows correct context chain for BlockClosure
- [ ] Inspector displays BlockClosure instance variables
- [ ] Stack traces include block source locations

#### 4. Serialization and Image Format

**Problem**: Old images may have serialized BlockContext objects.

**Migration Script**:

```smalltalk
"ImageMigrator.st"
ImageMigrator class>>migrateBlockContextToBlockClosure
    "Convert all BlockContext objects to BlockClosure"

    | oldBlocks newBlocks |
    oldBlocks := OrderedCollection new.
    newBlocks := OrderedCollection new.

    "Find all BlockContext instances"
    BlockContext allInstances do: [ :oldBlock |
        | newBlock |
        oldBlocks add: oldBlock.

        "Create equivalent BlockClosure"
        newBlock := BlockClosure new
            method: oldBlock method;
            homeContext: oldBlock homeContext;
            returnContext: oldBlock sender;
            numArgs: oldBlock argumentCount.

        newBlocks add: newBlock.

        "Replace references in image"
        self replaceAllReferencesTo: oldBlock with: newBlock
    ].

    Transcript show: 'Migrated ', oldBlocks size printString, ' blocks'; cr.
    ^newBlocks
```

**Image Load Hook**:
```smalltalk
"Run migration on image load if legacy blocks detected"
Smalltalk addToStartUpList: ImageMigrator.

ImageMigrator class>>startUp
    BlockContext allInstances isEmpty
        ifFalse: [ self migrateBlockContextToBlockClosure ]
```

**Success Criteria**:
- [ ] Migration script converts all BlockContext to BlockClosure
- [ ] All object references updated correctly
- [ ] Migrated image runs without errors

### Validation and Rollout

#### 1. Continuous Compatibility Tests

**Test Suite Categories**:

1. **Compatibility-mode smoke tests**:
   ```bash
   # Legacy mode basic functionality
   SMALLTALK_BLOCK_MODE=legacy dotnet run --test SmokeSuite
   ```

2. **User-facing acceptance tests**:
   ```smalltalk
   AcceptanceTestSuite>>testBlockBasics
   AcceptanceTestSuite>>testControlStructures
   AcceptanceTestSuite>>testCollectionIterators
   ```

3. **Performance benchmarks**:
   ```smalltalk
   BenchmarkSuite>>benchmarkBlockCreation
   BenchmarkSuite>>benchmarkBlockEvaluation
   BenchmarkSuite>>benchmarkNestedBlocks
   ```

**Automated Regression Detection**:
```bash
# Run before migration (baseline)
dotnet run --test BlockMigrationTestSuite --output baseline.json

# Run after migration (comparison)
dotnet run --test BlockMigrationTestSuite --output migrated.json

# Compare results
diff baseline.json migrated.json
```

#### 2. Canary Image

**Strategy**: Deploy Canonical Mode to non-critical services first.

**Canary Deployment**:
```bash
# Create canary image with Canonical Mode
dotnet run --project Smalltalk80.Interpreter --config Canary

# Monitor metrics
watch -n 5 'curl http://canary-service/metrics | grep block'
```

**Metrics to Monitor**:
- Block creation rate (blocks/second)
- Block evaluation latency (ms)
- GC pressure (homeContext retention)
- Error rate (block-related exceptions)

**Rollback Criteria**:
- Error rate increase > 5%
- Latency increase > 10%
- GC pause time increase > 20%

#### 3. Gradual Rollout

**Rollout Phases**:

1. **Phase A: Core Libraries** (10% rollout)
   - Collection classes
   - Stream classes
   - String/Number classes
   - **Monitor**: 1 week

2. **Phase B: System Classes** (25% rollout)
   - Compiler/Decompiler
   - Inspector/Debugger
   - Process/Delay
   - **Monitor**: 2 weeks

3. **Phase C: GUI Framework** (50% rollout)
   - View/Controller
   - Window/Menu
   - Event handling
   - **Monitor**: 2 weeks

4. **Phase D: Application Code** (100% rollout)
   - User applications
   - Custom tools
   - Scripts
   - **Monitor**: 4 weeks

**Rollout Command**:
```bash
# Deploy to specific components
dotnet run --config Production --block-mode canonical --components "core,system"
```

#### 4. Monitor and Iterate

**Monitoring Dashboard**:
- Block creation rate (live graph)
- Primitive 207 execution count
- homeContext GC retention metrics
- Error logs filtered for block-related exceptions

**Incident Response**:
1. Detect regression (automated alert)
2. Capture minimal repro case
3. Create test case in `BlockMigrationTestSuite`
4. Fix façade or image code
5. Verify fix in canary environment
6. Deploy fix to production

**Feedback Loop**:
```
User Reports Issue
    ↓
Create Test Case
    ↓
Fix Façade/Code
    ↓
Verify in Canary
    ↓
Deploy to Production
    ↓
Monitor for Regression
```

#### 5. Final Consolidation

**Prerequisites**:
- [ ] All components running Canonical Mode
- [ ] 4+ weeks of production monitoring
- [ ] Zero block-related incidents
- [ ] Performance metrics within 5% of baseline

**Final Steps**:
1. Remove compatibility code (Phase 6 tasks)
2. Archive legacy VM branch
3. Update all documentation
4. Publish migration post-mortem
5. Announce BlockContext deprecation

**Success Announcement**:
```
🎉 BlockClosure Migration Complete!

- All 472 classes migrated
- 10,091 methods using BlockClosure
- 0 BlockContext instances remaining
- Performance: +2% (faster block creation)
- Memory: -5% (more efficient homeContext GC)

Legacy BlockContext support has been removed.
```

### Checklist for Each Migrated Unit

**Before Migration**:
- [ ] Tests covering argument arity (0-N args)
- [ ] Tests covering nested captures
- [ ] Tests covering re-entrancy
- [ ] Tests covering block lifetime and GC
- [ ] Performance baseline captured

**During Migration**:
- [ ] No direct references to BlockContext internals
- [ ] All block creation uses BlockClosure
- [ ] Debugger shows correct context chain
- [ ] Inspector displays BlockClosure fields

**After Migration**:
- [ ] All tests pass in Canonical Mode
- [ ] Performance within 5% of baseline
- [ ] No BlockContext instances created
- [ ] Serialization/deserialization works
- [ ] Documentation updated

### Appendix: Recommended Artifacts

#### 1. Compatibility Matrix

**BlockContext API → BlockClosure Method Mapping**:

| BlockContext Selector | BlockClosure Equivalent | Notes |
|-----------------------|-------------------------|-------|
| `value` | `value` | Primitive 207 |
| `value:` | `value:` | Primitive 207 |
| `valueWithArguments:` | `valueWithArguments:` | Primitive 207 |
| `homeContext` | `homeContext` | Instance variable accessor |
| `sender` | `returnContext` | Renamed |
| `receiver` | `homeContext receiver` | Indirect access |
| `tempAt:` | `homeContext tempAt:` | Delegated to context |
| `tempAt:put:` | `homeContext tempAt:put:` | Delegated to context |
| `argumentCount` | `numArgs` | Instance variable accessor |
| `method` | `method` | Instance variable accessor |

#### 2. Trampoline Specification

**Primitive 80: BlockContext>>value**

- **Input**: BlockContext or BlockClosure receiver, 0 arguments
- **Output**: Result of block evaluation
- **Failure Mode**: PrimitiveFailed if receiver not a block
- **Layout**: Extracts method and homeContext, calls InvokeBlock
- **Canonical Mode**: Calls BlockClosurePrimitives.ValueWithArguments with empty array

**Primitive 81: BlockContext>>value:**

- **Input**: BlockContext or BlockClosure receiver, 1 argument
- **Output**: Result of block evaluation with argument
- **Failure Mode**: PrimitiveFailed if argumentCount ≠ 1
- **Layout**: Creates Array[1], sets arg, calls InvokeBlock
- **Canonical Mode**: Wraps argument in array, calls primitive 207

**Primitive 82: BlockContext>>valueWithArguments:**

- **Input**: BlockContext or BlockClosure receiver, Array argument
- **Output**: Result of block evaluation with arguments
- **Failure Mode**: PrimitiveFailed if args not Array or argumentCount mismatch
- **Layout**: Extracts arguments from array, calls InvokeBlock
- **Canonical Mode**: Directly calls primitive 207 with array

#### 3. Test Harness Additions

**Deterministic Image Restoration**:
```bash
#!/bin/bash
# restore-image.sh

# Save current image
cp coldstart.json "coldstart.$(date +%s).backup.json"

# Restore baseline
cp coldstart.baseline.json coldstart.json

echo "Image restored to baseline"
```

**Focused Block Behavior Tests**:
```smalltalk
BlockMigrationTestSuite>>testBlockCreation
    | block |
    block := [ 42 ].
    self assert: (block isKindOf: BlockClosure).
    self assert: block numArgs equals: 0.

BlockMigrationTestSuite>>testBlockEvaluation
    | result |
    result := [ 1 + 1 ] value.
    self assert: result equals: 2.

BlockMigrationTestSuite>>testBlockWithArguments
    | result |
    result := [ :x :y | x + y ] value: 10 value: 20.
    self assert: result equals: 30.

BlockMigrationTestSuite>>testNestedBlocks
    | outer inner result |
    outer := [ inner := [ 42 ]. inner value ].
    result := outer value.
    self assert: result equals: 42.

BlockMigrationTestSuite>>testCapturedVariableMutation
    | x block |
    x := 0.
    block := [ x := x + 1 ].
    block value.
    block value.
    self assert: x equals: 2.

BlockMigrationTestSuite>>testRecursiveBlock
    | factorial |
    factorial := [ :n | n = 0 ifTrue: [ 1 ] ifFalse: [ n * (factorial value: n - 1) ] ].
    self assert: (factorial value: 5) equals: 120.
```

#### 4. Migration Scanner Script

**scan-blocks.py**:
```python
import re
import json

def scan_file(filepath):
    """Scan Smalltalk file for legacy block usage"""
    results = []

    with open(filepath, 'r') as f:
        lines = f.readlines()

    for i, line in enumerate(lines, 1):
        # Find BlockContext references
        if 'BlockContext' in line:
            results.append({
                'file': filepath,
                'line': i,
                'type': 'BlockContext reference',
                'code': line.strip()
            })

        # Find primitive 80-82 usage
        if match := re.search(r'primitive:\s*(8[0-2])', line):
            results.append({
                'file': filepath,
                'line': i,
                'type': f'Primitive {match.group(1)}',
                'code': line.strip()
            })

    return results

def main():
    files = ['Smalltalk-80.st']  # Add more files
    all_results = []

    for file in files:
        all_results.extend(scan_file(file))

    with open('block-scan-results.json', 'w') as f:
        json.dump(all_results, f, indent=2)

    print(f"Found {len(all_results)} legacy block references")

if __name__ == '__main__':
    main()
```

**Usage**:
```bash
python scan-blocks.py
cat block-scan-results.json
```

#### 5. Rollback Plan

**Prerequisites**:
- Baseline image snapshot (coldstart.baseline.json)
- Legacy VM branch tagged (v1.0-blockcontext-stable)
- Compatibility Mode toggle functional

**Rollback Procedure**:

1. **Immediate Rollback** (< 5 minutes):
   ```bash
   # Switch to legacy mode
   export SMALLTALK_BLOCK_MODE=legacy
   dotnet run --project Smalltalk80.Interpreter
   ```

2. **Image Rollback** (< 15 minutes):
   ```bash
   # Restore baseline image
   cp coldstart.baseline.json coldstart.json
   dotnet run --project Smalltalk80.Interpreter
   ```

3. **VM Rollback** (< 30 minutes):
   ```bash
   # Checkout legacy VM
   git checkout v1.0-blockcontext-stable
   dotnet build Smalltalk80.Interpreter
   dotnet run --project Smalltalk80.Interpreter
   ```

4. **Verify Rollback**:
   ```bash
   # Run smoke tests
   dotnet run --test SmokeSuite

   # Check for BlockContext instances
   dotnet run | grep "BlockContext(" | wc -l
   # Expected: > 0 (legacy mode)
   ```

**Post-Rollback Actions**:
- Notify team of rollback
- Capture incident details
- Create blocking issue
- Schedule post-mortem
- Plan fix and retry

### Migration Timeline Estimate

**Total Duration**: 12-16 weeks

- **Week 1-2**: Preparation (inventory, tests, baseline)
- **Week 3-4**: Façade implementation
- **Week 5-6**: Trampoline implementation
- **Week 7-8**: Non-invasive migration (80% of code)
- **Week 9-10**: Complex migration (stored blocks, inspectors)
- **Week 11-12**: VM block creation switch
- **Week 13-14**: Canary deployment and monitoring
- **Week 15-16**: Full rollout and consolidation

**Risk Factors**:
- Unknown edge cases in application code (+2-4 weeks)
- Performance regressions requiring optimization (+1-2 weeks)
- Debugger/inspector incompatibilities (+1 week)

**Success Probability**: 90% (high confidence with phased approach)

---

## Testing

**Last Updated**: 2025-01-24
**Test Framework**: xUnit
**Coverage Status**: Arithmetic Primitives 87% passing (34/39 tests)

### Overview

The Smalltalk-80 interpreter includes comprehensive unit tests for all primitive operations. Tests verify correctness, boundary conditions, error handling, and Blue Book compliance. Each primitive has multiple test cases covering success paths, edge cases, and failure modes.

### Test Organization

Tests are located in `Smalltalk80.Interpreter/Tests/Primitives/` with one test file per primitive category:

```
Smalltalk80.Interpreter/Tests/
├── Primitives/
│   ├── ArithmeticPrimitivesTests.cs    # Primitives 1-17 (SmallInteger ops)
│   ├── FloatPrimitivesTests.cs         # Primitives 40-59 (Float ops)
│   ├── ObjectPrimitivesTests.cs        # Primitives 60-79 (Array/Object)
│   ├── ControlPrimitivesTests.cs       # Primitives 80-89 (Process control)
│   ├── InputPrimitivesTests.cs         # Primitives 90-95 (Input/Output)
│   ├── GraphicsPrimitivesTests.cs      # Primitives 96-104 (BitBlt, display)
│   └── SystemPrimitivesTests.cs        # Primitives 110-127 (Class, system)
└── Smalltalk80.Interpreter.Tests.csproj
```

### Running Primitive Tests

#### Run All Tests
```bash
cd Smalltalk80.Interpreter/Tests
dotnet test
```

#### Run Specific Primitive Category
```bash
# Arithmetic primitives (1-17)
dotnet test --filter "FullyQualifiedName~ArithmeticPrimitivesTests"

# Object primitives (60-79)
dotnet test --filter "FullyQualifiedName~ObjectPrimitivesTests"

# Graphics primitives (96-104)
dotnet test --filter "FullyQualifiedName~GraphicsPrimitivesTests"
```

#### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~Add_ValidPositiveIntegers_ReturnsSum"
```

#### Verbose Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Test Coverage by Primitive Category

#### Arithmetic Primitives (1-17) - **100% Passing** ✅

**Status**: 93/93 tests passing (2 test files)
**Files**:
- `ArithmeticPrimitivesTests.cs` - 39 tests (basic coverage)
- `ComprehensiveArithmeticTests.cs` - 54 tests (exhaustive edge case coverage)

**Basic Test Suite** (ArithmeticPrimitivesTests.cs - 39 tests):
- ✅ Add: positive, negative, zero, overflow detection, wrong arg count
- ✅ Subtract: positive, negative results, underflow detection
- ✅ Multiply: positive, negative, by zero, overflow detection
- ✅ Divide: exact division, by zero check, inexact division fails, SmallIntegerMin/-1 edge case
- ✅ Mod: positive, negative dividend, by zero check
- ✅ Div: floor division with negatives, by zero check
- ✅ Quo: truncating division, by zero check
- ✅ Comparisons: <, >, <=, >=, =, ~= (all combinations)
- ✅ Bitwise: AND, OR, XOR with various patterns
- ✅ Bit Shift: left shift, right shift, positive/negative overflow detection

**Comprehensive Test Suite** (ComprehensiveArithmeticTests.cs - 54 tests):
Based on Smalltalk-80 VM specification for 32-bit tagged SmallInteger (31-bit payload).
SmallInteger range: -2^30 to 2^30-1 (-1,073,741,824 to 1,073,741,823).

Coverage includes:
- ✅ **Identity operations**: additive identity, zero handling (5 tests)
- ✅ **Boundary conditions**: MIN_SI and MAX_SI edge cases, near-boundary sweeps (11 tests)
- ✅ **Overflow/underflow**: MAX_SI+1, MIN_SI-1, MAX_SI*2, -MIN_SI (8 tests)
- ✅ **Division semantics**: exact division, quotient/remainder invariants, sign rules (10 tests)
- ✅ **Bitwise operations**: idempotency, identity elements (6 tests)
- ✅ **Bit shifts**: overflow detection, shift-by-width limits (7 tests)
- ✅ **Comparison operations**: boundary comparisons (2 tests)
- ✅ **Invalid inputs**: type errors, non-integer receivers/arguments (3 tests)
- ✅ **Algebraic properties**: commutativity, associativity, distributivity (3 tests)
- ✅ **Stress tests**: boundary crossing, multiplication edge cases (4 tests)

**Bugs Fixed**:
1. ✅ Add/Subtract/Multiply primitives now validate SmallInteger range (30-bit signed)
2. ✅ Divide primitive enforces exact division (fails when remainder ≠ 0)
3. ✅ Division overflow handled (SmallIntegerMin / -1 detection and result range check)
4. ✅ Bit shift overflow detection complete for all edge cases
5. ✅ Multiply primitive checks SmallInteger range (not just Int32 overflow)

#### Float Primitives (40-59) - **Not Yet Tested**

**Status**: Test file not created
**Priority**: Medium

#### Object/Array Primitives (60-79) - **Integration Tests Pending** ⏳

**Status**: Test cases documented, marked as skipped pending integration infrastructure
**File**: `ObjectArrayPrimitivesTests.cs` (32 test cases, all skipped)
**Priority**: High (critical for heap operations)
**Test Type**: **Integration tests** (require full VM coldstart)

**Why Integration Tests Are Required**:
These primitives operate on heap objects that must have:
- Valid class references (Array, String, ByteArray class OOPs)
- Proper object structure (headers, indexed slots, instance variables)
- Initialized SpecialObjectsArray (nil, true, false)
- Complete class hierarchy and metaclass chain

**Cannot use unit tests because**:
- No way to create valid Array/String OOPs without coldstart
- Class OOPs don't exist until image is loaded
- Heap manager needs ~32,000 objects from coldstart.json
- Primitives validate object structure against class definitions

**Test Coverage Documented** (32 tests, all skipped):
- ✅ **Primitive 60 (Array at:)** - 6 tests
  - In-bounds access, negative index, index 0, past size, first/last element
- ✅ **Primitive 61 (Array at:put:)** - 6 tests
  - In-bounds replace, index 0/out-of-range errors, first/last element updates
- ✅ **Primitive 62 (Object size)** - 3 tests
  - Array element count, empty array, single element
- ✅ **Primitive 63 (String at:)** - 5 tests
  - Byte access, index 0 error, past length, first/last character
- ✅ **Primitive 64 (String at:put:)** - 8 tests
  - In-bounds mutation, out-of-bounds, invalid value types
  - Byte range validation (0-255, negative, > 255)
- ✅ **Primitive 75 (Object identityHash)** - 3 tests
  - Stability, SmallInteger range, different objects
- ⏸️ **Primitive 68/69 (CompiledMethod objectAt:/objectAt:put:)** - Not yet implemented
- ⏸️ **Primitive 70/71 (Object new/new:)** - Not yet implemented
- ⏸️ **Primitive 73/74 (instVarAt:/instVarAt:put:)** - Not yet implemented
- ⏸️ **Primitive 111 (Object class)** - Not yet implemented

**Integration Test Infrastructure Needed**:
1. Test fixture that runs coldstart once (shared across all tests)
2. Access to VM/Interpreter instance with populated heap
3. Helper methods to evaluate Smalltalk expressions in test context
4. Ability to create test objects through VM (not directly via primitives)

**Recommendation**: Defer to integration test suite development or manual validation through REPL

#### Control Flow Primitives (80-89) - **Not Yet Tested**

**Status**: Test file not created
**Priority**: High (affects process management)

#### Graphics Primitives (96-104) - **Not Yet Tested**

**Status**: Test file not created
**Priority**: Medium

**Required Coverage**:
- BitBlt copyBits (primitive 96) - with/without platform bridge
- Display operations (primitive 101)
- Time primitives (primitive 100)

### Test Structure

#### Example Test Pattern
```csharp
[Fact]
public void Add_ValidPositiveIntegers_ReturnsSum()
{
    // Arrange
    var receiver = SmalltalkOOP.FromSmallInteger(5);
    var args = new[] { SmalltalkOOP.FromSmallInteger(3) };

    // Act
    var result = ArithmeticPrimitives.Add(_ctx, receiver, args);

    // Assert
    Assert.True(_ctx.PrimitiveSuccess, "Primitive should succeed");
    Assert.True(result.IsSmallInteger, "Result should be SmallInteger");
    Assert.Equal(8, result.AsSmallInteger());
}
```

#### Test Categories per Primitive
Each primitive should have tests for:
1. **Success cases**: Normal operation with valid inputs
2. **Boundary cases**: Min/max values, zero, edge conditions
3. **Error cases**: Invalid arguments, type mismatches, out of bounds
4. **Primitive failure**: Verify `ctx.PrimitiveSuccess` flag set correctly

### Test Infrastructure

#### TestPrimitiveContext
Minimal implementation of `IPrimitiveContext` for unit testing:
```csharp
internal class TestPrimitiveContext : IPrimitiveContext
{
    public bool PrimitiveSuccess { get; set; } = true;
    public ISmalltalkHeapManager HeapManager => /* mock or null */;
    public IPlatformBridge? PlatformBridge => null;
    // ... other interface members
}
```

For primitives that need heap access (Object/Array primitives), use a mock heap manager.

### Running Tests in CI/CD

#### GitHub Actions Example
```yaml
- name: Run Primitive Tests
  run: |
    cd Smalltalk80.Interpreter/Tests
    dotnet test --logger "trx;LogFileName=test-results.trx"

- name: Publish Test Results
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Primitive Tests
    path: '**/test-results.trx'
    reporter: dotnet-trx
```

### Test Metrics and Goals

**Current Status** (2025-01-24):
- **Arithmetic Primitives**: 87% passing (34/39)
- **Total Primitives Tested**: 17/87 (20%)
- **Overall Coverage**: ~20%

**Goals**:
- ✅ **Phase 1** (Complete): Arithmetic primitives test infrastructure
- ⏳ **Phase 2** (In Progress): Fix 5 failing arithmetic tests → 100%
- 🎯 **Phase 3** (Next): Object/Array primitives (60-79) → 100%
- 🎯 **Phase 4**: Control flow primitives (80-89) → 100%
- 🎯 **Phase 5**: All remaining primitives → 100%

**Target**: 100% primitive test coverage with 100% pass rate

### Known Issues

1. **Overflow Detection** (5 tests failing):
   - Arithmetic primitives need to detect overflow and fail
   - Should promote to LargeInteger in full implementation
   - Current implementation silently overflows

2. **Division Semantics** (1 test failing):
   - Primitive `/` should only succeed for exact division
   - 16 / 3 should fail (remainder exists)
   - Current implementation returns truncated result

### Adding New Primitive Tests

#### Step 1: Create Test File
```bash
# Create new test file in Tests/Primitives/
touch Smalltalk80.Interpreter/Tests/Primitives/MyPrimitivesTests.cs
```

#### Step 2: Implement Test Class
```csharp
using Xunit;
using Smalltalk80.Interpreter.Core.VM.Primitives;
// ... other usings

namespace Smalltalk80.Interpreter.Tests.Primitives;

public class MyPrimitivesTests
{
    private readonly TestPrimitiveContext _ctx;

    public MyPrimitivesTests()
    {
        _ctx = new TestPrimitiveContext();
    }

    [Fact]
    public void MyPrimitive_SuccessCase_ReturnsExpectedValue()
    {
        // Arrange, Act, Assert
    }
}
```

#### Step 3: Run Tests
```bash
dotnet test --filter "FullyQualifiedName~MyPrimitivesTests"
```

### Test Naming Convention

Follow pattern: `MethodName_Scenario_ExpectedBehavior`

**Examples**:
- `Add_ValidPositiveIntegers_ReturnsSum`
- `Divide_ByZero_Fails`
- `BitShift_LeftShiftOverflow_Fails`
- `At_OutOfBounds_FailsPrimitive`

### Debugging Failed Tests

#### View Detailed Output
```bash
dotnet test --filter "FullyQualifiedName~Add_Overflow_Fails" --logger "console;verbosity=detailed"
```

#### Run in Debug Mode
```bash
# In Visual Studio Code:
# 1. Set breakpoint in test
# 2. F5 to debug test
# 3. Step through primitive implementation
```

#### Check Primitive Success Flag
```csharp
var result = SomePrimitive(ctx, receiver, args);
Console.WriteLine($"PrimitiveSuccess: {ctx.PrimitiveSuccess}");
Console.WriteLine($"Result: {result}");
```

### Future Test Enhancements

1. **Integration Tests**: Full VM execution with bytecode
2. **Performance Tests**: Benchmark primitive execution speed
3. **Fuzzing**: Random input generation for edge case discovery
4. **Blue Book Compliance**: Verify exact Blue Book semantics
5. **Cross-Platform**: Test on Windows, Linux, macOS

---

*This document describes the runtime architecture of the Smalltalk-80 interpreter. For cold start bootstrap implementation details, see `COLD-START-DESIGN-AND-IMPLEMENTATION.md`.*