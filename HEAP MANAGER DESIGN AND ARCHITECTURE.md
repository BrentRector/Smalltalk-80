# Heap Manager Design and Architecture

**Last Updated**: 2025-01-15  
**Status**: ✅ Production-ready with perfect GC (0 unreachable objects) and bulletproof allocation architecture

## Overview

This document describes the design and architecture of the Smalltalk-80 heap manager. The heap manager is responsible for low-level object allocation and provides the foundation for garbage collection, but deliberately avoids any knowledge of high-level Smalltalk class semantics.

## Recent Achievements (2025-01-15)

### 🎉 Perfect GC - Zero Unreachable Objects

**Achievement**: Eliminated literal handling double-tagging bug, achieving **0 unreachable objects** out of 31,927 total objects during cold start.

**Root Cause**: Code was mixing raw object table indices (`int`) with tagged OOP values (`SmalltalkOOP`):
```csharp
// BUG: Double-tagging literals
private int AllocateArray(int[] elementIds)  // elementIds already contained RawValue from tagged OOPs!
{
    for (int i = 0; i < elementIds.Length; i++)
    {
        var elementOOP = SmalltalkOOP.FromObjectTableIndex(elementIds[i]); // WRONG - creates invalid OOP!
        _allocator.SetIndexedOOP(arrayOOP, i, elementOOP);
    }
}
```

**Critical Principle Violated**: **"Only OOPs (tagged values) should be stored, never raw object indices"**

**Solution**: Changed all literal handling from `int[]` to `SmalltalkOOP[]`:
```csharp
// FIXED: Use tagged OOPs directly
private int AllocateArray(SmalltalkOOP[] elementOOPs)  // Already tagged!
{
    for (int i = 0; i < elementOOPs.Length; i++)
    {
        _allocator.SetIndexedOOP(arrayOOP, i, elementOOPs[i]); // CORRECT - direct use
    }
}
```

**Files Modified**:
- `Phase8_MethodInstallation.cs`: Changed `ResolveLiteralValueAsOOP()` to return `SmalltalkOOP`, updated all array allocation methods
- `Phase11_InitExecution.cs`: Changed `ResolveLiteralAsOOP()` to return `SmalltalkOOP`, updated all array allocation methods  
- `SemanticAllocator.cs`: Changed `AllocateCompiledMethod()` signature from `int[] literalRawValues` to `SmalltalkOOP[] literalOOPs`

**Results**:
- ✅ **Perfect GC**: 31,927 objects allocated, 31,927 reachable, **0 unreachable**
- ✅ **No double-tagging**: All OOP values correctly tagged once
- ✅ **All phases working**: Complete cold start success maintained
- ✅ **Clean codebase**: All debug output removed

### 🎉 NULL_CLASS Bug Elimination (2025-01-13)

**Problem**: Phase 7 (Method Dictionary Allocation) was creating objects with `classOOP=0` (NULL_CLASS), causing massive GC traversal failures.

**Root Cause**: Error-prone allocation pattern:
```csharp
// ERROR-PRONE PATTERN:
var methodDictOOP = _allocator.AllocateArray("MethodDictionary", capacity);
_allocator.SetSlot(methodDictOOP, 0, methodDictClassOOP); // Easy to forget!
```

**Solution**: Bulletproof dual API design:
```csharp
// BULLETPROOF PATTERN (automatically sets class reference):
var methodDictOOP = _allocator.AllocateArray(methodDictClassOOP, capacity);
// Class reference automatically set - impossible to forget
```

**Results**:
- ✅ **NULL_CLASS issue completely eliminated**
- ✅ **GC improvement**: 406→201→0 unreachable objects  
- ✅ **100% validation success**: All 10,535 objects pass Phase 13

### ProcessorScheduler Singleton (2025-01-15)

**Implementation**: Added ProcessorScheduler singleton instance for future process management:

**Phase 6 Changes** (`Phase6_ObjectAllocation.cs`):
```csharp
// Create ProcessorScheduler singleton
var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
int processorSchedulerId = processorSchedulerOOP.AsObjectTableIndex();
GlobalRegistry.Register("ProcessorScheduler", processorSchedulerId);
```

**Phase Y Changes** (`PhaseY_InstanceVariableInitialization.cs`):
```csharp
// Initialize ProcessorScheduler instance variables to nil
InitializeSingletonInstanceVariables("ProcessorScheduler", "ProcessorScheduler", nilOOP);
```

**Phase 12 Changes** (`Phase12_GlobalVariableInstallation.cs`):
```csharp
// Install Processor -> ProcessorScheduler singleton
// Note: By convention, the global is named "Processor" not "ProcessorScheduler"
private void InstallProcessorGlobal(int processorSchedulerId)
{
    int processorSymbolId = GetSymbolObjectId("Processor");
    int associationId = AllocateAssociation(processorSymbolId, processorSchedulerId);
    InstallAssociationInSystemDictionary(associationId, "Processor");
}
```

**Results**:
- ✅ ProcessorScheduler singleton created and registered
- ✅ Instance variables properly initialized to nil
- ✅ Accessible via `Smalltalk at: #Processor`
- ✅ Object count increased to 31,927 (ProcessorScheduler + Association)
- ✅ Still maintains 0 unreachable objects

### GC Root Configuration (2025-01-15)

**Updated GC Roots**: Removed USTable from direct GC roots since it's reachable via SystemDictionary:

**Before**:
```csharp
// 5 direct GC roots
var gcRootsList = new List<(string name, int objectId)>
{
    ("nil", GlobalRegistry.NilObjectId),
    ("false", GlobalRegistry.FalseObjectId),
    ("true", GlobalRegistry.TrueObjectId),
    ("Smalltalk", GlobalRegistry.SystemDictionaryId),
    ("USTable", ustableId)  // ❌ Redundant - reachable via Smalltalk
};
```

**After**:
```csharp
// 4 fundamental GC roots only
var gcRootsList = new List<(string name, int objectId)>
{
    ("nil", GlobalRegistry.NilObjectId),
    ("false", GlobalRegistry.FalseObjectId),
    ("true", GlobalRegistry.TrueObjectId),
    ("Smalltalk", GlobalRegistry.SystemDictionaryId)
    // USTable reachable via: Smalltalk at: #USTable
    // ProcessorScheduler reachable via: Smalltalk at: #Processor
};
```

**Results**:
- ✅ Cleaner GC root configuration (4 roots instead of 5)
- ✅ All objects still reachable (0 collected)
- ✅ Proper Smalltalk semantics (globals accessed via SystemDictionary)

### Dynamic Class Name Resolution (2025-01-15)

**Problem**: Class names displayed as "UnknownClass" because heap manager had hard-coded instance variable index.

**Solution**: Dynamic index lookup from coldstart.json metadata:

**SmalltalkHeapManager Changes**:
```csharp
// Field to store dynamically-determined index
private int _classNameFieldIndex = -1;

// Configuration method called during cold start
public void SetClassNameFieldIndex(int index)
{
    _classNameFieldIndex = index;
}

// GetClassName now uses dynamic index
if (_classNameFieldIndex >= 0 && instVarCount > _classNameFieldIndex)
{
    var classNameSymbolOOP = GetInstanceVariable(classOOP, _classNameFieldIndex);
    // Extract class name from Symbol object...
}
```

**Loader Changes**:
```csharp
// After Phase 0, configure heap manager with class name field index
if (_heapManager is SmalltalkHeapManager concreteHeapManager)
{
    int nameFieldIndex = LoaderPhases.ClassHierarchyUtils.FindInstanceVariableIndex("Class", "name", _bundle);
    if (nameFieldIndex >= 0)
    {
        concreteHeapManager.SetClassNameFieldIndex(nameFieldIndex);
    }
}
```

**Results**:
- ✅ Class names display correctly: UndefinedObject, False, True, SystemDictionary, Array
- ✅ No hard-coded indices - robust to Smalltalk source code changes
- ✅ All indices determined from coldstart.json at runtime

## Core Design Principles

### 🚨 1. BLACK BOX PRINCIPLE (CRITICAL)
**The heap manager is a BLACK BOX that returns effectively random object IDs.**

**Key Implications**:
- **NEVER assume sequential allocation patterns** - object IDs will change between runs
- **NEVER assume specific object ID values** - use semantic references only
- **NEVER depend on allocation order** - heap manager can use any strategy (sequential, random, best-fit, etc.)
- **NEVER hardcode object ID relationships** - use GlobalRegistry, LocalId mappings, or semantic slot finding

**Required Practices**:
- ✅ Use `GlobalRegistry.LookupClassObjectId(className)` for well-known objects
- ✅ Use LocalId mappings for bundle objects (`_localIdToObjectId[localId]`)
- ✅ Use semantic slot finding (search for nil slots, not sequential indexing)
- ❌ NEVER use patterns like `smalltalkIndex++` or assume object ID sequence

### 2. OOP Type Safety
**The OOP tagging system is the fundamental contract. Raw object indices should NEVER be stored or passed between methods - only tagged SmalltalkOOP values.**

**Key Principles**:
- All methods should accept/return `SmalltalkOOP`, never `int`
- Only use `.AsObjectTableIndex()` at the lowest heap manager boundary
- Use `.RawValue` only for serialization or debugging
- This prevents double-tagging bugs and maintains type safety

### 3. Minimal Responsibilities
The heap manager operates at the machine level and has minimal responsibilities:
- Allocate contiguous memory for heap objects with correct alignment
- Write object headers at fixed offsets as the VM expects
- Return tagged OOPs using the VM's pointer encoding
- Support GC operations through ObjectTable management
- Maintain ObjectTable indirection for all heap objects

### 4. No Semantic Knowledge
The heap manager explicitly avoids hardcoding knowledge about:
- Smalltalk class layouts, instance variable names, or method tables
- High-level class semantics (what Array, String, Float mean)
- Business logic or Smalltalk-specific invariants

### 5. Header-Driven Design
The caller (object allocator/VM layer) provides all semantic information:
- Class OOP (stored opaquely)
- Object format bits (how to interpret the body)
- Size requirements (fixed + indexed slots)

## Object-Oriented Pointer (OOP) System

### OOP Encoding (32-bit)
```
┌─────────────────────────────────┬─┐
│        31 bits of data          │T│
└─────────────────────────────────┴─┘
                                  │
                                  └─ Tag bit (bit 0)
```

**Tag Bit Interpretation:**
- **Bit 0 = 1**: SmallInteger immediate value
  - Data = (OOP >> 1) gives the integer value
  - No heap allocation, no ObjectTable entry
- **Bit 0 = 0**: Object reference
  - Data = (OOP >> 1) gives ObjectTable index
  - Points to ObjectTableEntry containing metadata and memory pointer

### OOP Examples
```
0x00000005 = SmallInteger 2    (binary: ...0101, (5>>1) = 2)
0x0000000A = SmallInteger 5    (binary: ...1010, (10>>1) = 5)
0x00000004 = Object at index 2 (binary: ...0100, (4>>1) = 2)
0x00000008 = Object at index 4 (binary: ...1000, (8>>1) = 4)
```

## Critical Usage Patterns

### ⚠️ Nil Object Handling in Association Loops
**CRITICAL LESSON LEARNED**: Never compare OOPs to fixed values - object-to-OOP mappings change between runs.

**Problem**: Code was checking `if (assocOOP.AsObjectTableIndex() == 0)` to skip empty association slots, but this is fundamentally wrong for two reasons:
1. **Nil is never equivalent to 0** - nil object has its own dynamically assigned OOP
2. **Fixed OOP comparisons are anti-pattern** - object-to-OOP mappings change between runs

```csharp
// ❌ WRONG - compares to fixed values, breaks when mappings change
for (int i = 0; i < sysDictSize; i++)
{
    var assocOOP = _heapManager.GetIndexedOOP(sysDictOOP, i);
    if (assocOOP.AsObjectTableIndex() == 0) continue; // NEVER compare to fixed values!
    
    var key = _heapManager.GetInstanceVariable(assocOOP, 0); // ERROR if assocOOP is nil
}

// ✅ CORRECT - uses semantic comparison through GlobalRegistry
for (int i = 0; i < sysDictSize; i++)
{
    var assocOOP = _heapManager.GetIndexedOOP(sysDictOOP, i);
    if (assocOOP.AsObjectTableIndex() == GlobalRegistry.NilObjectId) continue;
    
    var key = _heapManager.GetInstanceVariable(assocOOP, 0); // Safe
}
```

**Root Causes**: 
1. nil object (UndefinedObject instance) has 0 instance variables, so accessing index 0 is invalid
2. SystemDictionary slots are initialized with nil object, not "empty" (0)
3. Object-to-OOP mappings are dynamic and change between runs

**Key Principle**: Always use `GlobalRegistry.NilObjectId` for nil comparisons, never hardcoded values.

## ObjectTable Indirection

### Purpose
The ObjectTable provides indirection between OOPs and actual memory addresses:
- **Enables non-moving GC**: Objects never move in memory
- **Supports become: operation**: Swap ObjectTable entries, not memory
- **Simplifies GC**: Mark/sweep in ObjectTable, let .NET handle memory reclamation

### ObjectTable Growth Strategy
```
Initial ObjectTable:
┌─────────────────┐
│   Block 0       │  indices 0-1023
│  [1024 entries] │
└─────────────────┘

When full, allocate new block:
┌─────────────────┐    ┌─────────────────┐
│   Block 0       │───▶│   Block 1       │
│  [1024 entries] │    │  [1024 entries] │
└─────────────────┘    └─────────────────┘
 indices 0-1023          indices 1024-2047

Index arithmetic treats as continuous: index = block_num * 1024 + offset
```

### ObjectTableEntry Structure (Space-Efficient Design)
```csharp
public sealed class ObjectTableEntry
{
    // Memory management
    public IntPtr MemoryPointer { get; set; }    // Points to allocated object
    
    // Packed metadata word for space efficiency
    public uint PackedMetadata { get; set; }     // Size + format + GC flags + other metadata
    
    // Garbage collection
    public bool IsMarked { get; set; }           // GC mark bit
}
```

### PackedMetadata Encoding (32-bit word)
```
┌─────────┬─────────┬─────────┬─────────┬─────────┬─────────────┐
│ Bits    │  31-24  │  23-16  │  15-12  │  11-4   │    3-0      │
├─────────┼─────────┼─────────┼─────────┼─────────┼─────────────┤
│ Content │   GC    │  Size   │ Format  │Reserved │   Flags     │
│         │  Flags  │ (words) │  Type   │         │             │
└─────────┴─────────┴─────────┴─────────┴─────────┴─────────────┘

Format Type (4 bits):
  0 = Pointer           - Fixed instance variables only
  1 = PointerIndexed    - Fixed + indexed pointer fields  
  2 = ByteIndexed       - Fixed + indexed byte fields
  3 = WordIndexed       - Fixed + indexed word fields
  4 = CompiledMethod    - Special method format
  5-15 = Reserved for future formats

Size (8 bits): Total object size in words (0-255 words = 0-1020 bytes)
GC Flags (8 bits): Mark bits, generation, pin status, forwarding info
Flags (4 bits): Identity hash cached, immutable marker, etc.
Reserved (8 bits): Future expansion
```

## Object Memory Layout

### Standard Object Layout
Every heap object has this structure in allocated memory:

```
┌─────────────────────────────────────────────────────┐
│                    Object Header                    │
├─────────────────────────────────────────────────────┤
│                  Fixed Slots                        │
│                (Instance Variables)                 │  
├─────────────────────────────────────────────────────┤
│                 Variable Slots                      │
│                 (Indexed Data)                      │
└─────────────────────────────────────────────────────┘
```

### Object Header (Always Present)
```
Offset  Size  Field        Description
------  ----  -----------  ---------------------------
0x00    4     Class OOP    32-bit OOP pointing to class object
```

**Why Class OOP is in object header:**
- Every Smalltalk object must have a class
- Required for method dispatch and type checking  
- Ensures minimum 4-byte allocation (no zero-byte objects)
- Natural location that every object access can find

### Complete Object Example
Object with 2 fixed slots and 4 variable slots:

```
Memory Address: 0x12345000
┌─────────┬──────┬─────────────────┬──────────────────────────┐
│ Offset  │ Size │     Field       │       Description        │
├─────────┼──────┼─────────────────┼──────────────────────────┤
│  0x00   │  4   │   Class OOP     │ 0x00002468 (class ref)   │
│  0x04   │  4   │ Fixed Slot 0    │ 0x00000000 (initialized) │
│  0x08   │  4   │ Fixed Slot 1    │ 0x00000000 (initialized) │
│  0x0C   │  4   │ Variable Slot 0 │ 0x00000000 (initialized) │
│  0x10   │  4   │ Variable Slot 1 │ 0x00000000 (initialized) │
│  0x14   │  4   │ Variable Slot 2 │ 0x00000000 (initialized) │
│  0x18   │  4   │ Variable Slot 3 │ 0x00000000 (initialized) │
└─────────┴──────┴─────────────────┴──────────────────────────┘
Total: 28 bytes (7 words)

ObjectTable[1234] = {
  MemoryPointer: 0x12345000,
  PackedMetadata: 0x00070001, // Size=7 words, Format=PointerIndexed, no flags
  IsMarked: false
}

Returned OOP: 0x000009A8 (1234 << 1)
```

## Object Format Types

### ObjectFormat Enumeration
```csharp
public enum ObjectFormat
{
    Pointer = 0,           // Fixed instance variables only
    PointerIndexed = 1,    // Fixed + indexed pointer fields  
    ByteIndexed = 2,       // Fixed + indexed byte fields
    WordIndexed = 3,       // Fixed + indexed word fields
    CompiledMethod = 4     // Special format for methods
}
```

### Format-Specific Layouts

**Pointer Objects (format = 0)**
```
┌─────────────┐
│ Class OOP   │ ← Always present
├─────────────┤
│ Fixed Slot 0│ ← Instance variables (OOPs)
├─────────────┤
│ Fixed Slot 1│
├─────────────┤
│    ...      │
└─────────────┘
No indexed portion
```

**PointerIndexed Objects (format = 1)**
```
┌─────────────┐
│ Class OOP   │ ← Always present  
├─────────────┤
│ Fixed Slot 0│ ← Instance variables (OOPs)
├─────────────┤
│ Fixed Slot 1│
├─────────────┤
│Index Slot 0 │ ← Variable portion (OOPs)
├─────────────┤
│Index Slot 1 │ ← Used for Arrays, OrderedCollections
├─────────────┤
│    ...      │
└─────────────┘
```

**ByteIndexed Objects (format = 2)**
```
┌─────────────┐
│ Class OOP   │ ← Always present
├─────────────┤
│ Fixed Slot 0│ ← Instance variables (OOPs)  
├─────────────┤
│ Fixed Slot 1│
├─────────────┤
│  byte[0-3]  │ ← Variable portion (raw bytes)
├─────────────┤
│  byte[4-7]  │ ← Used for Strings, ByteArrays, Floats
├─────────────┤
│    ...      │
└─────────────┘
```

## Memory Initialization

### .NET Zero-Initialization Guarantee
The .NET runtime provides strong guarantees about memory initialization:
- All allocated managed memory is zero-initialized before being handed to user code
- Array elements are initialized to default values (0 for value types, null for reference types)
- This is mandated by the ECMA-335 specification
- The CLR optimizes this through pre-zeroed memory segments

### Heap Manager Initialization Strategy
The heap manager leverages .NET's guarantee:
1. **Allocate memory**: `new byte[totalSize]` - guaranteed zero-filled
2. **Write header**: Set Class OOP at offset 0x00  
3. **Leave body**: Fixed and variable slots remain zero-initialized
4. **Higher layer responsibility**: VM/allocator sets pointer slots to proper nil OOP

## Garbage Collection Design

### Non-Moving Mark-and-Sweep
The GC design avoids object movement entirely:

```
Phase 1: Mark
┌─────────────────┐    ┌─────────────────┐
│ Mark all        │    │ Walk object     │
│ ObjectTable     │───▶│ graph from      │
│ entries as      │    │ roots, mark     │
│ unreachable     │    │ reachable       │
└─────────────────┘    └─────────────────┘

Phase 2: Sweep  
┌─────────────────┐    ┌─────────────────┐
│ Scan ObjectTable│    │ Set MemoryPtr   │
│ for unmarked    │───▶│ = null for      │
│ entries         │    │ unreachable     │
└─────────────────┘    └─────────────────┘

Phase 3: .NET Cleanup
┌─────────────────┐
│ .NET GC will    │
│ eventually      │
│ reclaim memory  │
│ with null refs  │
└─────────────────┘
```

### Perfect GC Achievement (2025-01-15)

The system now achieves **perfect garbage collection** with 0 unreachable objects:

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

### GC Benefits
- **No object copying**: Objects never move in memory
- **Simple become: operation**: Copy MemoryPointer between ObjectTable entries
- **No forwarding pointers**: Never needed since objects don't move
- **Concurrent friendly**: Can mark ObjectTable entries safely
- **Leverages .NET**: Let .NET handle actual memory management
- **Perfect Reachability**: 100% of allocated objects are reachable from GC roots

## Heap Manager API

### Core Allocation Interface

**Heap Manager API (current - minimal semantic knowledge for GC):**
```csharp
public interface ISmalltalkHeapManager 
{
    /// <summary>
    /// Allocates object with specified memory layout.
    /// Memory is zero-initialized by .NET guarantee.
    /// </summary>
    /// <param name="format">Object format for GC traversal</param>
    /// <param name="fixedSlots">Number of fixed slots (always treated as OOPs)</param>
    /// <param name="indexedSize">Size of indexed portion (format-specific)</param>
    /// <returns>Tagged OOP referencing new object</returns>
    SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize);

    /// <summary>
    /// Raw slot access - gets OOP from any slot (semantic-agnostic).
    /// </summary>
    SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex);

    /// <summary>
    /// Raw slot access - sets OOP in any slot (semantic-agnostic).
    /// </summary>
    void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value);
    
    /// <summary>
    /// Configures the instance variable index for the class name field.
    /// This must be called after bundle is loaded to enable GetClassName() functionality.
    /// </summary>
    void SetClassNameFieldIndex(int index);
    
    /// <summary>
    /// Gets the class name for display/debugging purposes.
    /// Returns "UnknownClass" if class name field index not configured.
    /// </summary>
    string GetClassName(SmalltalkOOP objectOOP);
}
```

**Key Design Principles:**
- **Minimal Knowledge**: Heap manager only knows memory layout for allocation and GC traversal
- **No Semantics**: Doesn't know what objects represent (String, Array, etc.) - only format
- **GC Safety**: ObjectFormat ensures correct traversal (OOPs vs raw bytes/words)
- **Size Preservation**: Original indexed sizes preserved for validation
- **Dynamic Configuration**: Class name field index determined at runtime from coldstart.json

**GC Traversal by Format:**
- **Pointer/PointerIndexed**: GC traverses all fixed + all indexed slots as OOPs
- **ByteIndexed**: GC traverses fixed slots as OOPs, skips byte data  
- **WordIndexed**: GC traverses fixed slots as OOPs, skips word data
- **Critical**: Prevents GC from interpreting raw bytes/words as invalid OOP references

### Format-Aware Access Methods (with bounds checking)

#### Instance Variable Access
```csharp
/// <summary>
/// Gets an instance variable by index. Instance variables are always OOPs.
/// Validates index is within range [0, instanceVarCount-1].
/// </summary>
SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);

/// <summary>
/// Sets an instance variable by index. Instance variables are always OOPs.
/// Validates index is within range [0, instanceVarCount-1].
/// </summary>
void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);
```

#### Format-Specific Indexed Access (Type-Safe with Validation)
```csharp
/// <summary>
/// Gets indexed OOP from PointerIndexed objects (Arrays, OrderedCollections).
/// Validates format is PointerIndexed and index is within range.
/// </summary>
SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index);

/// <summary>
/// Sets indexed OOP in PointerIndexed objects (Arrays, OrderedCollections).
/// Validates format is PointerIndexed and index is within range.
/// </summary>
void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

/// <summary>
/// Gets indexed byte from ByteIndexed objects (Strings, ByteArrays, Floats).
/// Validates format is ByteIndexed/CompiledMethod and index is within range.
/// </summary>
byte GetIndexedByte(SmalltalkOOP objectOOP, int index);

/// <summary>
/// Sets indexed byte in ByteIndexed objects (Strings, ByteArrays, Floats).
/// Validates format is ByteIndexed/CompiledMethod and index is within range.
/// </summary>
void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value);

/// <summary>
/// Gets indexed word from WordIndexed objects (WordArrays, Bitmaps).
/// Validates format is WordIndexed and index is within range.
/// </summary>
ushort GetIndexedWord(SmalltalkOOP objectOOP, int index);

/// <summary>
/// Sets indexed word in WordIndexed objects (WordArrays, Bitmaps).
/// Validates format is WordIndexed and index is within range.
/// </summary>
void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value);
```

#### GC Support
```csharp
/// <summary>
/// Sets the GC root objects (replaces any previous roots).
/// </summary>
void SetGCRoots(SmalltalkOOP[] rootObjects);

/// <summary>
/// Performs mark-and-sweep garbage collection with detailed statistics.
/// </summary>
GarbageCollectionResult GarbageCollect();

/// <summary>
/// Gets current heap statistics without performing collection.
/// </summary>
HeapStatistics GetHeapStatistics();
```

#### GC Result Types
```csharp
/// <summary>
/// Comprehensive garbage collection results with timing and object counts.
/// </summary>
public sealed class GarbageCollectionResult
{
    public int TotalObjects { get; init; }        // Total objects before collection
    public int LiveObjects { get; init; }          // Objects marked as reachable
    public int CollectedObjects { get; init; }     // Objects collected (unreachable)
    public double MarkTimeMs { get; init; }        // Time spent in mark phase
    public double SweepTimeMs { get; init; }       // Time spent in sweep phase
    public double TotalTimeMs { get; init; }       // Total GC time
    public long EstimatedBytesFreed { get; init; } // Estimated memory freed
}
```

## Semantic Layer Integration

### SemanticAllocator Architecture

The **SemanticAllocator** provides a high-level wrapper over the semantic-agnostic heap manager, implementing Smalltalk object semantics and primitive access patterns.

**Key Responsibilities**:
- Translates class names to instance variable counts
- Implements Smalltalk primitive access (`objectAt:`, `at:`, etc.)
- Handles complex object allocation (CompiledMethod with separate ByteArray)
- Manages index translation (Smalltalk 1-based ↔ C# 0-based)
- **🎉 NEW**: Bulletproof allocation API that prevents NULL_CLASS bugs

```csharp
public class SemanticAllocator
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly ColdStartBundle _bundle;
    
    // 🎉 BULLETPROOF DUAL API (prevents class reference bugs)
    // Legacy API for early phases (0-6) - bootstrap only
    public SmalltalkOOP AllocateArray(string className, int elementCount);
    
    // New API for later phases (7+) - automatic class reference setting
    public SmalltalkOOP AllocateArray(SmalltalkOOP classOOP, int elementCount);
    
    // Other high-level allocation methods
    public SmalltalkOOP AllocateFixedObject(string className);
    public SmalltalkOOP AllocateString(string value);
    public SmalltalkOOP AllocateCompiledMethod(string className, int headerWord, 
                                               SmalltalkOOP[] literalOOPs, byte[] bytecodes);
    
    // Primitive access with Smalltalk semantics
    public SmalltalkOOP ObjectAt(SmalltalkOOP objectOOP, int smalltalkIndex);
    public void ObjectAtPut(SmalltalkOOP objectOOP, int smalltalkIndex, SmalltalkOOP value);
    public byte At(SmalltalkOOP objectOOP, int smalltalkIndex);
    public void AtPut(SmalltalkOOP objectOOP, int smalltalkIndex, byte value);
}
```

### Multi-Pass Cold Start Architecture

**Problem**: Circular dependencies between objects (classes need nil, nil needs classes)

**Solution**: Optimized phase ordering resolves dependencies early:

```
BOOTSTRAP PHASES (0-5):
- Allocate core objects without cross-references

MULTI-PASS INITIALIZATION:
- Phase 6: Object Allocation (creates nil/true/false/ProcessorScheduler singletons)
- Phase X: Class Reference Initialization (after nil exists)  
- Phase Y: Instance Variable Initialization (after class refs set)

FULL ALLOCATION + INITIALIZATION (7-14):
- All subsequent objects allocated + fully initialized in one step
- No placeholder OOP usage needed
```

**Benefits**:
- ✅ Eliminates placeholder OOP management
- ✅ Objects always in valid state after creation
- ✅ Simpler allocation code
- ✅ Better performance (no second pass for most objects)

### CompiledMethod Architecture

**Challenge**: C# cannot easily handle Smalltalk's heterogeneous CompiledMethod format (PointerIndexed + ByteIndexed in one object)

**Solution**: Separate ByteArray design based on Smalltalk-80.st analysis:

```csharp
// CompiledMethod: PointerIndexed
[classOOP, header, literal1, literal2, ..., literalN, bytecodeArrayOOP]
 slot 0    slot 1   slot 2    slot 3         slot N+1   slot N+2
 (hidden)  (objectAt: 1)      (objectAt: 2)            (hidden)

// Separate ByteArray: ByteIndexed  
[byte1, byte2, byte3, ...]
 (at: 1) (at: 2) (at: 3)
```

**Primitive Access Implementation**:
```csharp
// objectAt: 1 → header (Smalltalk-80.st: "^self objectAt: 1")
public SmalltalkOOP ObjectAt(SmalltalkOOP methodOOP, int smalltalkIndex)
{
    int csharpIndex = smalltalkIndex - 1; // 1-based to 0-based
    // Hide last slot (bytecode array reference) from Smalltalk
    int visibleSlots = _heapManager.GetIndexedSize(methodOOP) - 1;
    if (csharpIndex >= visibleSlots) throw new ArgumentOutOfRangeException();
    return _heapManager.GetIndexedOOP(methodOOP, csharpIndex);
}

// at: 1 → first bytecode (via hidden reference)
public byte At(SmalltalkOOP methodOOP, int smalltalkIndex)
{
    int csharpIndex = smalltalkIndex - 1;
    var bytecodeArrayOOP = GetHiddenBytecodeArray(methodOOP);
    return _heapManager.GetIndexedByte(bytecodeArrayOOP, csharpIndex);
}
```

**GC Safety**: Tagged SmallInteger headers (LSB=1) automatically ignored during traversal.

## Division of Responsibilities

### Heap Manager (Low-Level - Minimal Semantic Knowledge)
- Memory allocation with ObjectFormat + fixed slots + indexed size
- ObjectTable management and growth (block chaining)
- OOP encoding/decoding with SmallInteger tagging
- Raw slot access (GetSlot/SetSlot) with bounds checking
- GC mark/sweep with correct format-based traversal
- Original indexed size preservation for validation
- Comprehensive error reporting with object context
- Dynamic class name field index configuration
- **Minimal knowledge**: Only memory layout, no object semantics

### Semantic Allocator Layer (High-Level)  
- Class semantics and layout knowledge
- Object format interpretation (Pointer, PointerIndexed, ByteIndexed, etc.)
- Instance variable counting and slot calculation
- Class header management (setting slot 0)
- nil OOP value management and object initialization
- Format-aware access methods (instance variables, indexed data)
- CompiledMethod allocation with separate ByteArray

### VM/Primitive Layer (Highest-Level)
- Method dispatch and type checking
- Primitive operations requiring class knowledge
- Instance creation protocols (new, new:, etc.)
- Business logic and Smalltalk-specific operations

### Clear Boundaries
```
┌─────────────────────────────────────────────────┐
│              VM / Primitive Layer               │
│  • Method dispatch and type checking           │
│  • Primitive operations                        │  
│  • Instance creation protocols                 │
├─────────────────────────────────────────────────┤
│            Semantic Allocator Layer             │
│  • Class semantics (Array, String, Float)      │
│  • Object format interpretation                │
│  • Instance variable layout                    │
│  • Class header management                     │
│  • Bulletproof allocation APIs                 │
├─────────────────────────────────────────────────┤
│         Heap Manager (Semantic-Agnostic)       │
│  • Raw memory allocation (N slots)             │
│  • ObjectTable management                       │
│  • OOP encoding/decoding                        │ 
│  • GC mark/sweep (perfect 0 unreachable)       │
│  • Raw slot access (no semantic knowledge)     │
│  • Dynamic class name resolution               │
└─────────────────────────────────────────────────┘
```

## Implementation Status

### Current Achievements
- ✅ ObjectTableEntry uses PackedMetadata for space-efficient storage
- ✅ Format-aware access methods with comprehensive validation
- ✅ Non-moving GC design leverages .NET memory management
- ✅ **Perfect GC**: 0 unreachable objects out of 31,927 allocated
- ✅ **Bulletproof Allocation**: NULL_CLASS bug eliminated
- ✅ **Dynamic Configuration**: Class name field index from metadata
- ✅ **ProcessorScheduler Singleton**: Created and properly initialized
- ✅ **Clean GC Roots**: 4 fundamental roots (nil, false, true, Smalltalk)

### Testing Strategy
- **OOP System**: Unit tests for SmallInteger tagging and ObjectTable indexing
- **Object Allocation**: Tests for all ObjectFormat types with various sizes
- **Bounds Checking**: Validation of all range and format checks
- **Memory Layout**: Verification of object header and slot arrangements
- **GC Correctness**: Mark/sweep with complex object graphs achieving 0 unreachable objects
- **GC Statistics**: Verification of object counts and timing measurements
- **Heap Statistics**: Testing ObjectTable utilization and memory usage calculations
- **Error Conditions**: Exception handling for invalid operations
- **Performance**: Benchmarks for allocation, access patterns, and GC efficiency

## Conclusion

This heap manager design provides a clean separation between low-level memory management and high-level Smalltalk semantics. By keeping the heap manager minimal and delegating semantic knowledge to higher layers, we achieve a maintainable, testable, and efficient foundation for the Smalltalk-80 runtime system.

The design leverages .NET's memory management strengths while providing the ObjectTable indirection needed for Smalltalk's object model. The non-moving GC approach simplifies implementation while maintaining compatibility with Smalltalk semantics like become: operations.

**Achievement**: The system now achieves **perfect garbage collection** with 0 unreachable objects, demonstrating the correctness of the allocation architecture and GC root configuration. This milestone validates the bulletproof allocation API design and proper OOP type safety throughout the codebase.
