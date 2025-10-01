# SObjectRef-Only Architecture Refactoring Progress

## Goal
Eliminate `SObject` entirely. Use only `SObjectRef` throughout the VM.

## Progress: Phases 1-2 Complete ✅

### Phase 1: Design ✅
- Created `SOBJECTREF_ONLY_ARCHITECTURE.md` - Complete architecture design
- Created `ObjectMemory.New.cs` - New internal storage with ObjectData
- Documented all benefits and migration strategy

### Phase 2: Expand SObjectRef ✅
- Added `FromObjectPointer(nint oop)` - Create SObjectRef from OOP
- Added `AsObjectPointer()` - Internal method to get raw OOP
- Added `GetClassRef()` - Returns SObjectRef to class (NEW)
- Added `GetFormat()` - Returns ObjectFormat
- Added `GetInstanceVariable(int)` / `SetInstanceVariable(int, SObjectRef)`
- Added `GetInstanceVariableCount()`
- Added `GetIndexedByte/Word/Pointer(int)` / `SetIndexedByte/Word/Pointer(...)`
- Added `GetIndexedSize()`
- Marked `AsObject()` and `FromObject()` as Obsolete (will be removed in Phase 4)

**File**: `Core/Values/SObjectRef.cs` - UPDATED ✅

### Phase 2.5: Replace ObjectMemory ✅
- Renamed `ObjectMemory.cs` → `ObjectMemory.Old.cs`
- Renamed `ObjectMemory.New.cs` → `ObjectMemory.cs`

**ObjectMemory now stores all object data internally!**

## Remaining Work

### Phase 3: Update BlueBookFactory 🔄
**Status**: IN PROGRESS

Need to update all factory methods to:
1. Return `SObjectRef` instead of `SInstance`
2. Use `ObjectMemory.AllocateObject()` instead of `new SInstance()`
3. Use `SObjectRef.FromObjectPointer()` to wrap OOP

**Example**:
```csharp
// OLD:
public static SInstance CreateCharacter(SClass characterClass, int asciiValue)
{
    var character = new SInstance(characterClass, 1, ObjectFormat.FixedPointer, 0);
    character.SetInstanceVariable(0, SObjectRef.FromSmallInteger(asciiValue));
    return character;
}

// NEW:
public static SObjectRef CreateCharacter(SObjectRef characterClassRef, int asciiValue)
{
    nint oop = ObjectMemory.AllocateObject(
        classRef: characterClassRef,
        format: ObjectFormat.FixedPointer,
        instanceVarCount: BlueBookLayouts.Character.FixedFieldCount,
        indexedSize: 0);

    SObjectRef character = SObjectRef.FromObjectPointer(oop);
    character.SetInstanceVariable(
        BlueBookLayouts.Character.ValueIndex,
        SObjectRef.FromSmallInteger(asciiValue));

    return character;
}
```

**Files to Update**:
- `Core/BlueBook/BlueBookFactory.cs` - All 15 factory methods

### Phase 4: Delete Old Classes 🚫
**Status**: PENDING

Delete these files:
- `Core/Objects/SObject.cs`
- `Core/Objects/SInstance.cs`
- `Core/Objects/SMethodContext.cs`
- `Core/Objects/SBlockContext.cs`
- `Core/Objects/SCompiledMethod.cs`
- `Core/Objects/SProcess.cs`
- `Core/Objects/SSemaphore.cs`
- `Core/Objects/SDictionary.cs`
- `Core/Objects/SArray.cs`
- `Core/Objects/SString.cs`
- `Core/Objects/SSymbol.cs`
- `Core/Objects/SCharacter.cs`
- `Core/Objects/SFloat.cs`
- `Memory/ObjectMemory.Old.cs`

**Question**: What about `SClass`?
- Option A: Keep SClass as special (it's the bootstrap)
- Option B: Make SClass an SObjectRef too
- **Recommendation**: Keep SClass for now, decide later

### Phase 5: Update All Code 📝
**Status**: PENDING

Update every file that:
1. Uses `SObject` or `SInstance` → change to `SObjectRef`
2. Calls `.AsObject()` → remove, use direct SObjectRef methods
3. Calls `FromObject()` → use `FromObjectPointer()` or `ObjectMemory.AllocateObject()`
4. Creates objects with `new SInstance()` → use `ObjectMemory.AllocateObject()`
5. Casts to specialized types → remove casts entirely

**Affected Directories**:
- `ColdStart/` - All cold start code
- `Primitives/` - All primitive implementations
- `Execution/` - All bytecode interpreters
- `Core/Objects/` - SystemDictionary, SClass
- `Memory/` - GarbageCollector, SpecialObjectsArray

**Estimated Files**: 50+ files

### Phase 6: Fix Compilation Errors 🔧
**Status**: PENDING

After phases 3-5, there will be compilation errors:
- Type mismatches (`SObject` vs `SObjectRef`)
- Missing methods (methods on deleted classes)
- Missing using directives (`using Smalltalk80.Interpreter.Memory;`)

**Strategy**:
1. Build and collect all errors
2. Group errors by type
3. Fix systematically:
   - Missing using directives
   - Type conversions
   - Missing methods
   - Method signature mismatches

### Phase 7: Testing 🧪
**Status**: PENDING

Test that:
1. Project compiles without errors
2. ObjectMemory stores objects correctly
3. SObjectRef access methods work
4. BlueBookFactory creates correct objects
5. Cold start can create initial objects
6. Primitives work with new architecture
7. GC works with new ObjectMemory

## Key Architecture Changes

### Before (OLD)
```
User Code
    ↓ uses SObject
SObject (C# class hierarchy)
    ├─ SInstance
    ├─ SMethodContext
    ├─ SCompiledMethod
    └─ etc.
    ↓ contains data
instanceVariables: SObjectRef[]
indexedData: byte[] | ushort[] | SObjectRef[]
```

### After (NEW)
```
User Code
    ↓ uses SObjectRef only
SObjectRef (struct, tagged pointer)
    ↓ delegates to
ObjectMemory (static class)
    ↓ stores
ObjectData (internal, never exposed)
    ├─ ClassRef: SObjectRef
    ├─ Format: ObjectFormat
    ├─ InstanceVariables: SObjectRef[]
    └─ IndexedData: byte[] | ushort[] | SObjectRef[]
```

## Benefits of New Architecture

1. **No conversions** - Everything is SObjectRef, no `.AsObject()` needed
2. **True encapsulation** - Object storage completely internal
3. **Matches Smalltalk** - Everything is an OOP
4. **Simpler** - One type for all references
5. **Better GC** - Complete control over object lifetime
6. **Cleaner** - No parallel C# class hierarchy

## Challenges

### Challenge 1: SystemDictionary
Currently uses static methods returning `SClass`.
Need to decide: should SystemDictionary return `SObjectRef` too?

**Solution**: Add new methods:
```csharp
public static SObjectRef SmallIntegerClassRef { get; }
public static SObjectRef UndefinedObjectClassRef { get; }
// etc.
```

### Challenge 2: SClass
`SClass` is used extensively during bootstrap.
It has methods like `LookupMethod()`, `CreateInstance()`.

**Solution**: Keep SClass for now, but consider it a VM-internal type.
Later, could become an SObjectRef with helper methods on ObjectMemory.

### Challenge 3: Method Execution
`SCompiledMethod` has `Execute()` method.
Need to move this logic somewhere.

**Solution**: Move to interpreter/primitive dispatcher.
Access method data via SObjectRef methods.

### Challenge 4: Cold Start Complexity
Cold start creates many objects before system is fully initialized.

**Solution**: Use BlueBookFactory exclusively.
BlueBookFactory knows how to create bootstrap objects correctly.

## Files Already Completed

✅ `Core/Values/SObjectRef.cs` - Extended with all access methods
✅ `Memory/ObjectMemory.cs` - New internal storage architecture
✅ `Core/BlueBook/BlueBookLayouts.cs` - Layout constants (verified)
✅ `Memory/ObjectMemory.Old.cs` - Backed up old version
✅ `Core/BlueBook/BlueBookFactory.Old.cs` - Backed up old version

## Next Immediate Steps

1. **Finish Phase 3**: Update BlueBookFactory.cs
2. **Start Phase 4**: Delete old SObject classes (creates many errors intentionally)
3. **Execute Phase 5**: Update all code systematically
4. **Execute Phase 6**: Fix all compilation errors
5. **Execute Phase 7**: Test everything works

## Estimated Effort

- Phase 3: 1 hour (15 factory methods)
- Phase 4: 15 minutes (delete files)
- Phase 5: 4-6 hours (50+ files to update)
- Phase 6: 2-4 hours (fix compilation errors)
- Phase 7: 2-3 hours (testing)

**Total**: ~10-15 hours of focused work

## Status Summary

- ✅ Architecture designed
- ✅ SObjectRef expanded
- ✅ New ObjectMemory created
- 🔄 BlueBookFactory updating
- ⏳ Delete old classes
- ⏳ Update all code
- ⏳ Fix compilation errors
- ⏳ Testing

**Progress**: ~25% complete