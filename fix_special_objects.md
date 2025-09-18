# Special Objects Array Refactoring Summary

## Design Change
Moving the Special Objects Array from Smalltalk object memory to a C# VM field. This ensures:
1. The array is never garbage collected
2. It's purely a VM construct for GC roots
3. No need to track it in Smalltalk object memory

## Changes Made

### 1. VirtualMachine.cs
✅ Added `specialObjectsArray` field (C# array of SmalltalkObject)
✅ Added `SpecialObjectIndex` enum with Blue Book indices
✅ Added `SetSpecialObject()` and `GetSpecialObject()` methods
✅ Added `GetSpecialObjectsArray()` for GC access

### 2. ObjectMemory.cs
✅ Removed `specialObjectsArrayOop` and `isSpecialObjectsArrayComplete` 
✅ Added `vm` reference and `isGCEnabled` flag
✅ Updated GC marking to use VM's array instead of object memory array
✅ Added `RegisterVM()` and `EnableGC()` methods
✅ Removed `MarkSpecialObjectsArrayComplete()` method

### 3. SystemImageBootstrapExecutor.cs (IN PROGRESS)
⚠️ Need to complete refactoring of `UpdateSpecialObjectsArrayForClass()`
⚠️ Remove all references to `specialArrayOop`
⚠️ Update to call `virtualMachine.SetSpecialObject()` instead

### 4. VMPrimitiveAdapter.cs
⚠️ Need to remove call to `ObjectMemory.MarkSpecialObjectsArrayComplete()`

## Remaining Issues

The main issue is that during method installation, CompiledMethod objects and their literals are not being marked as reachable. This is likely because:

1. When methods are installed in MethodDictionary, they're stored as OOP integers
2. The GC marking doesn't properly traverse MethodDictionary contents
3. 1922 objects (mainly CompiledMethod and literals) are incorrectly collected

## Next Steps

1. Complete the compilation fixes
2. Test that the VM's Special Objects Array is properly used for GC roots
3. Investigate why MethodDictionary contents aren't being marked