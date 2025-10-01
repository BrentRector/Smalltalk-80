# SObjectRef-Only Architecture Design

## Core Principle

**There is NO `SObject` class. Only `SObjectRef` exists.**

All Smalltalk object references are `SObjectRef` (tagged pointers).
All object data is stored internally in `ObjectMemory`.
No C# class represents a Smalltalk object - they are pure data structures.

## Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│ User Code (VM, Primitives, Cold Start)                 │
│ - Only sees SObjectRef                                   │
│ - Calls methods on SObjectRef to access object data     │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│ SObjectRef (struct)                                      │
│ - Tagged pointer (SmallInteger or object pointer)       │
│ - Instance methods delegate to ObjectMemory             │
│ - No conversion to/from SObject needed                   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│ ObjectMemory (static class)                              │
│ - Stores all object data internally                      │
│ - Maps OOP → ObjectData                                  │
│ - Provides access methods                                │
│ - Never exposes ObjectData outside this class            │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│ ObjectData (internal class)                              │
│ - Class pointer (SObjectRef)                             │
│ - Format (ObjectFormat enum)                             │
│ - Instance variables (SObjectRef[])                      │
│ - Indexed data (byte[], ushort[], or SObjectRef[])      │
│ - GC mark bit                                             │
└─────────────────────────────────────────────────────────┘
```

## SObjectRef Structure

```csharp
public readonly struct SObjectRef
{
    private readonly nint value;  // Tagged: LSB=1 for SmallInteger, LSB=0 for object pointer

    // Factory methods
    public static SObjectRef FromSmallInteger(int value);
    public static SObjectRef FromObjectPointer(nint oop);
    public static readonly SObjectRef Nil;

    // Type checking
    public bool IsSmallInteger { get; }
    public bool IsHeapObject { get; }
    public bool IsNil { get; }

    // SmallInteger operations
    public int AsSmallInteger();
    public SObjectRef? AddSmallInteger(SObjectRef other);
    public SObjectRef? SubtractSmallInteger(SObjectRef other);
    // ... more arithmetic

    // Object access (delegates to ObjectMemory)
    public SObjectRef GetClass();
    public ObjectFormat GetFormat();

    public SObjectRef GetInstanceVariable(int index);
    public void SetInstanceVariable(int index, SObjectRef value);
    public int GetInstanceVariableCount();

    public byte GetIndexedByte(int index);
    public void SetIndexedByte(int index, byte value);

    public ushort GetIndexedWord(int index);
    public void SetIndexedWord(int index, ushort value);

    public SObjectRef GetIndexedPointer(int index);
    public void SetIndexedPointer(int index, SObjectRef value);

    public int GetIndexedSize();

    // Internal (for ObjectMemory use only)
    internal nint AsObjectPointer();  // Get raw OOP
}
```

## ObjectMemory API

```csharp
public static class ObjectMemory
{
    // Object allocation
    public static nint AllocateObject(
        SObjectRef classRef,
        ObjectFormat format,
        int instanceVarCount,
        int indexedSize);

    // Instance variable access
    public static SObjectRef GetInstanceVariable(nint oop, int index);
    public static void SetInstanceVariable(nint oop, int index, SObjectRef value);
    public static int GetInstanceVariableCount(nint oop);

    // Indexed data access
    public static byte GetIndexedByte(nint oop, int index);
    public static void SetIndexedByte(nint oop, int index, byte value);
    public static ushort GetIndexedWord(nint oop, int index);
    public static void SetIndexedWord(nint oop, int index, ushort value);
    public static SObjectRef GetIndexedPointer(nint oop, int index);
    public static void SetIndexedPointer(nint oop, int index, SObjectRef value);
    public static int GetIndexedSize(nint oop);

    // Object properties
    public static SObjectRef GetClass(nint oop);
    public static ObjectFormat GetFormat(nint oop);

    // GC and become:
    public static void Become(nint oop1, nint oop2);
    public static void CollectGarbage(SObjectRef[] roots);
}
```

## Usage Examples

### Creating an Object

```csharp
// Old way (with SObject):
var character = new SInstance(characterClass, 1, ObjectFormat.FixedPointer, 0);
character.SetInstanceVariable(0, SObjectRef.FromSmallInteger(65));

// New way (SObjectRef only):
nint oop = ObjectMemory.AllocateObject(
    classRef: characterClassRef,  // SObjectRef to Character class
    format: ObjectFormat.FixedPointer,
    instanceVarCount: BlueBookLayouts.Character.FixedFieldCount,
    indexedSize: 0);

SObjectRef character = SObjectRef.FromObjectPointer(oop);
character.SetInstanceVariable(
    BlueBookLayouts.Character.ValueIndex,
    SObjectRef.FromSmallInteger(65));
```

### Accessing Object Data

```csharp
// Old way:
SObject obj = objRef.AsObject();
SInstance inst = (SInstance)obj;
SObjectRef value = inst.GetInstanceVariable(0);

// New way:
SObjectRef value = objRef.GetInstanceVariable(0);
```

### Method Signatures

```csharp
// Old way:
public SObject ExecutePrimitive(int index, SObject[] args);
public void Push(SObject value);

// New way:
public SObjectRef ExecutePrimitive(int index, SObjectRef[] args);
public void Push(SObjectRef value);
```

## What Gets Deleted

### Classes to Delete
- `SObject` - base class, no longer needed
- `SInstance` - replaced by ObjectMemory.ObjectData
- `SMethodContext` - replaced by generic SObjectRef
- `SBlockContext` - replaced by generic SObjectRef
- `SCompiledMethod` - replaced by generic SObjectRef
- `SProcess` - replaced by generic SObjectRef
- `SSemaphore` - replaced by generic SObjectRef
- `SDictionary` - replaced by generic SObjectRef
- `SArray` - replaced by generic SObjectRef
- `SString` - replaced by generic SObjectRef
- `SSymbol` - replaced by generic SObjectRef
- `SCharacter` - replaced by generic SObjectRef
- `SFloat` - replaced by generic SObjectRef
- All other specialized classes

### What Stays
- `SObjectRef` - THE fundamental type
- `SClass` - might stay as special, or become SObjectRef too
- `ObjectFormat` - enum for object formats
- `BlueBookLayouts` - layout constants
- `BlueBookFactory` - but returns SObjectRef instead of SInstance
- `ObjectMemory` - completely rewritten

## Migration Plan

### Phase 1: New ObjectMemory (DONE)
- ✅ Created ObjectMemory.New.cs with internal ObjectData storage

### Phase 2: Expand SObjectRef
- Add all access methods to SObjectRef that delegate to ObjectMemory
- Add internal `AsObjectPointer()` method
- Remove `AsObject()` method

### Phase 3: Update BlueBookFactory
- Change all factory methods to return SObjectRef
- Use ObjectMemory.AllocateObject instead of new SInstance

### Phase 4: Delete Old Classes
- Delete SObject.cs
- Delete SInstance.cs
- Delete all specialized classes (SMethodContext, etc.)
- Delete old ObjectMemory.cs (replace with ObjectMemory.New.cs)

### Phase 5: Update All Code
- Change all method signatures from SObject to SObjectRef
- Remove all .AsObject() calls
- Remove all type casts (SInstance, SMethodContext, etc.)
- Use BlueBookFactory or ObjectMemory.AllocateObject for object creation

### Phase 6: Fix Compilation Errors
- Update all primitives
- Update all bytecode interpreters
- Update all cold start code
- Update GC code

### Phase 7: Testing
- Verify all objects created correctly
- Verify field access works
- Verify cold start works
- Verify primitives work

## Benefits

1. **No type conversions** - Everything is SObjectRef, no AsObject() needed
2. **True encapsulation** - Object storage is completely internal
3. **Matches Smalltalk** - Everything is an OOP (tagged pointer)
4. **Simpler code** - One type for all references
5. **Better performance** - No C# object allocations for immediate values
6. **Cleaner architecture** - Clear separation between VM and object storage
7. **GC control** - Complete control over object lifetime

## Open Questions

1. **What about SClass?**
   - Option A: Keep SClass as special C# class for bootstrap
   - Option B: Make SClass an SObjectRef too, store in ObjectMemory

2. **What about SystemDictionary?**
   - Currently static class with methods
   - Could become methods on ObjectMemory or a separate SymbolTable class

3. **What about method execution?**
   - SCompiledMethod has Execute() method
   - Need to move execution logic to interpreter/primitives

## Decision: Start Implementation

Proceed with Phase 2: Expand SObjectRef with access methods.