# IPrimitiveContext API Reference

**Date**: 2025-01-24
**Status**: Complete API Documentation
**Purpose**: Reference for primitive implementations

## Overview

The `IPrimitiveContext` interface provides all operations available to primitive implementations. Primitives must ONLY access the Smalltalk heap through these methods - never use cold start proxy data.

## Core Properties

### HeapManager
```csharp
ISmalltalkHeapManager HeapManager { get; }
```
**Purpose**: Low-level heap access
**Usage**: Direct object allocation and slot manipulation
**See**: ISmalltalkHeapManager API below

### LayoutResolver
```csharp
LayoutResolver LayoutResolver { get; }
```
**Purpose**: Dynamic field index lookups
**Usage**: `GetFieldIndex("class:Point", "x")` returns instance variable index
**Note**: Always use this instead of hardcoded indices

### PrimitiveSuccess
```csharp
bool PrimitiveSuccess { get; set; }
```
**Purpose**: Signals primitive success/failure to VM
**Contract**:
- VM sets to `true` before calling primitive
- Primitive sets to `false` on failure → VM executes fallback bytecode
- Primitive sets to `true` explicitly on success (optional but recommended)

## Field Access Methods

### GetFieldValue
```csharp
SmalltalkOOP GetFieldValue(SmalltalkOOP oop, string className, string fieldName)
```
**Purpose**: Read instance variable by name
**Example**: `ctx.GetFieldValue(pointOOP, "class:Point", "x")`
**Returns**: Field value as OOP
**Throws**: If field doesn't exist or wrong class

### SetFieldValue
```csharp
void SetFieldValue(SmalltalkOOP oop, string className, string fieldName, SmalltalkOOP value)
```
**Purpose**: Write instance variable by name
**Example**: `ctx.SetFieldValue(pointOOP, "class:Point", "x", SmalltalkOOP.FromSmallInteger(10))`
**Note**: Uses write barrier for GC safety

### GetFieldIndex
```csharp
int GetFieldIndex(string className, string fieldName)
```
**Purpose**: Get instance variable index for a class
**Example**: `int xIndex = ctx.GetFieldIndex("class:Point", "x")`
**Usage**: When you need index for HeapManager direct access
**Returns**: 0-based index

### GetInstanceVariable
```csharp
SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index)
```
**Purpose**: Read instance variable by index
**Example**: `var xValue = ctx.GetInstanceVariable(pointOOP, 0)`
**Note**: Prefer GetFieldValue unless you already have index

## Byte Array Access

### GetByteArray
```csharp
Memory<byte> GetByteArray(SmalltalkOOP oop)
```
**Purpose**: Get byte data from ByteIndexed objects
**Usage**: Strings, ByteArrays, Float internal bytes
**Returns**: Memory<byte> span (empty if not ByteIndexed)
**Example**:
```csharp
var bytes = ctx.GetByteArray(stringOOP);
string value = System.Text.Encoding.Latin1.GetString(bytes.Span);
```

## Object Allocation

### AllocateObject
```csharp
SmalltalkOOP AllocateObject(string className, int indexedSize)
```
**Purpose**: Allocate new instance of a class
**Parameters**:
- `className`: e.g., "class:Point", "class:Array"
- `indexedSize`: Number of indexed elements (0 for fixed-size objects)
**Returns**: New object OOP with class reference already set
**Example**:
```csharp
var pointOOP = ctx.AllocateObject("class:Point", 0);  // Fixed-size Point
var arrayOOP = ctx.AllocateObject("class:Array", 10); // Array with 10 slots
```

## Indexed Access

### GetIndexedSize
```csharp
int GetIndexedSize(SmalltalkOOP objectOOP)
```
**Purpose**: Get number of indexed elements
**Returns**: Size of indexed portion (0 if none)

### GetIndexedOOP
```csharp
SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index)
```
**Purpose**: Read indexed OOP from PointerIndexed objects
**Usage**: Arrays, OrderedCollections
**Index**: 0-based
**Throws**: If wrong format or out of bounds

## Message Sending

### SendMessage
```csharp
SmalltalkOOP SendMessage(SmalltalkOOP receiverOop, SmalltalkOOP selectorOop, SmalltalkOOP argumentsArray)
```
**Purpose**: Dynamic message send (for perform primitives)
**Parameters**:
- `receiverOop`: Object receiving message
- `selectorOop`: Symbol OOP of selector
- `argumentsArray`: Array OOP containing arguments
**Returns**: Result of message send

## Block Support

### GetActiveContext
```csharp
SmalltalkOOP GetActiveContext()
```
**Purpose**: Get currently executing context
**Returns**: MethodContext or BlockContext OOP

### GetInstructionPointer
```csharp
int GetInstructionPointer()
```
**Purpose**: Get current bytecode PC
**Returns**: Instruction pointer value

### CreateBlockContext
```csharp
SmalltalkOOP CreateBlockContext(SmalltalkOOP homeContextOop, int startPC, int argCount)
```
**Purpose**: Create BlockContext for a block
**Usage**: Block primitives (80, 81, 82)

### ActivateBlockContext
```csharp
SmalltalkOOP ActivateBlockContext(SmalltalkOOP blockContextOop, SmalltalkOOP argumentsArray)
```
**Purpose**: Execute a BlockContext
**Returns**: Result of block execution

### InvokeBlock
```csharp
SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] arguments, SmalltalkOOP homeContextOOP, SmalltalkOOP blockClosureOOP)
```
**Purpose**: Invoke BlockClosure (primitive 207)
**Parameters**:
- `blockMethodOOP`: CompiledMethod of block
- `arguments`: Array of argument OOPs
- `homeContextOOP`: Home context for lexical scoping
- `blockClosureOOP`: The BlockClosure being invoked
**Returns**: Result of block execution

## Object Enumeration

### GetAllObjects
```csharp
IEnumerable<SmalltalkOOP> GetAllObjects()
```
**Purpose**: Enumerate all objects in heap
**Usage**: someObject nextObject primitive
**Returns**: All non-SmallInteger OOPs

## Object Identity

### BecomeOneWay
```csharp
void BecomeOneWay(SmalltalkOOP fromOop, SmalltalkOOP toOop)
```
**Purpose**: One-way become - all references to `from` become `to`
**Usage**: Object replacement

### BecomeTwoWay
```csharp
void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2)
```
**Purpose**: Two-way become - swap object identities
**Usage**: Object swapping

## Symbol Interning

### InternSymbol
```csharp
SmalltalkOOP InternSymbol(string symbolName)
```
**Purpose**: Lookup existing Symbol in USTable
**Returns**: Symbol OOP if found, Nil if not found
**Note**: Does NOT create new symbols - primitive should fail if Nil returned

---

# ISmalltalkHeapManager API Reference

## Object Allocation

### AllocateObject (Low-Level)
```csharp
SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize)
```
**Purpose**: Low-level allocation with explicit format
**Parameters**:
- `format`: Pointer, PointerIndexed, ByteIndexed, WordIndexed
- `fixedSlots`: Instance variables (always OOPs)
- `indexedSize`: Indexed portion size
**Returns**: New object OOP
**Note**: Does NOT set class reference - use SetClassOOP after

### AllocateInstanceOf (High-Level)
```csharp
SmalltalkOOP AllocateInstanceOf(SmalltalkOOP classOOP, int indexedSize)
```
**Purpose**: Allocate instance reading format from class
**Parameters**:
- `classOOP`: Class object (reads format field)
- `indexedSize`: Indexed elements (0 for 'new', n for 'new: n')
**Returns**: New object OOP with class reference already set
**Note**: PREFERRED method for primitive allocation

## Slot Access (Low-Level)

### GetSlot / SetSlot
```csharp
SmalltalkOOP GetSlot(SmalltalkOOP objectOOP, int slotIndex)
void SetSlot(SmalltalkOOP objectOOP, int slotIndex, SmalltalkOOP value)
```
**Purpose**: Raw slot access (semantic-agnostic)
**Index**: 0-based, includes class reference at slot 0
**Warning**: No validation - use with care

## Instance Variable Access

### GetInstanceVariable / SetInstanceVariable
```csharp
SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index)
void SetInstanceVariable(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
```
**Purpose**: Type-safe instance variable access
**Index**: 0-based, excludes class reference
**Validates**: Index in range [0, instanceVarCount-1]

## Indexed Access (Type-Safe)

### GetIndexedOOP / SetIndexedOOP
```csharp
SmalltalkOOP GetIndexedOOP(SmalltalkOOP objectOOP, int index)
void SetIndexedOOP(SmalltalkOOP objectOOP, int index, SmalltalkOOP value)
```
**Purpose**: Access PointerIndexed objects (Arrays)
**Validates**: Format is PointerIndexed, index in range

### GetIndexedByte / SetIndexedByte
```csharp
byte GetIndexedByte(SmalltalkOOP objectOOP, int index)
void SetIndexedByte(SmalltalkOOP objectOOP, int index, byte value)
```
**Purpose**: Access ByteIndexed objects (Strings, ByteArrays, Floats)
**Validates**: Format is ByteIndexed, index in range

### GetIndexedWord / SetIndexedWord
```csharp
ushort GetIndexedWord(SmalltalkOOP objectOOP, int index)
void SetIndexedWord(SmalltalkOOP objectOOP, int index, ushort value)
```
**Purpose**: Access WordIndexed objects (Bitmaps)
**Validates**: Format is WordIndexed, index in range

## Object Metadata

### GetClassOOP / SetClassOOP
```csharp
SmalltalkOOP GetClassOOP(SmalltalkOOP objectOOP)
void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
```
**Purpose**: Get/set class reference (slot 0)
**Note**: GetClassOOP is deprecated - use GetSlot(oop, 0)

### GetObjectFormat
```csharp
ObjectFormat GetObjectFormat(SmalltalkOOP objectOOP)
```
**Purpose**: Get object's memory format
**Returns**: Pointer, PointerIndexed, ByteIndexed, or WordIndexed

### GetInstanceVariableCount
```csharp
int GetInstanceVariableCount(SmalltalkOOP objectOOP)
```
**Purpose**: Get number of instance variables
**Returns**: Count of fixed OOP slots (excludes class ref)

### GetIndexedSize
```csharp
int GetIndexedSize(SmalltalkOOP objectOOP)
```
**Purpose**: Get indexed portion size
**Returns**: Number of indexed elements (0 if none)

## Common Patterns

### Allocate Point
```csharp
var pointOOP = ctx.AllocateObject("class:Point", 0);
ctx.SetFieldValue(pointOOP, "class:Point", "x", SmalltalkOOP.FromSmallInteger(10));
ctx.SetFieldValue(pointOOP, "class:Point", "y", SmalltalkOOP.FromSmallInteger(20));
return pointOOP;
```

### Read String Bytes
```csharp
var bytes = ctx.GetByteArray(stringOOP);
string value = System.Text.Encoding.Latin1.GetString(bytes.Span);
```

### Check Stream At End
```csharp
int position = ctx.GetFieldValue(streamOOP, "class:Stream", "position").SmallIntegerValue;
int readLimit = ctx.GetFieldValue(streamOOP, "class:Stream", "readLimit").SmallIntegerValue;
bool atEnd = position > readLimit;
```

### Arithmetic with Overflow Detection
```csharp
try {
    int result = checked(a + b);
    return SmalltalkOOP.FromSmallInteger(result);
}
catch (OverflowException) {
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;  // Fallback to LargeInteger
}
```

## Methods That DON'T Exist (Common Errors)

❌ `ctx.GetByteArray(oop)` - EXISTS ✅
❌ `ctx.HeapManager.SetClassOOP()` - EXISTS ✅
❌ `ctx.HeapManager.GetObjectFormat()` - EXISTS ✅
❌ `ctx.HeapManager.SetIndexedByte()` - EXISTS ✅

All methods used in primitives are confirmed to exist!

## Nil Accessor (Canonical)

**Use**: `SpecialObjectsArray.Instance.Nil`
**Don't use**: `GlobalRegistry.NilOOP` (no longer exists)

## Write Barrier

For runtime heap mutations, SetInstanceVariable automatically uses write barrier.
For loader-only code, use SetInstanceVariableNoBarrier (performance optimization).
