# Plan to Remove Obsolete AsObject() and FromObject() Methods

## Current Situation

The codebase is in a transitional state between two architectures:

### Old Architecture (Phase 1-3)
- Uses `SObject` C# classes (SArray, SString, SSymbol, SClass, etc.)
- Objects are registered in `ObjectMemoryNew.bootstrapObjectMap`
- `SObjectRef.AsObject()` retrieves the SObject from the bootstrap map
- `SObjectRef.FromObject()` looks up the OOP for an SObject

### New Architecture (Phase 4 - Incomplete)
- Uses only `SObjectRef` values
- All object data stored internally in `ObjectMemoryNew.objectTable`
- Direct access via `SObjectRef` methods (GetInstanceVariable, SetInstanceVariable, etc.)
- No SObject C# classes needed

## Problem

The obsolete methods `AsObject()` and `FromObject()` have 113 call sites across the codebase. They are marked obsolete but are essential for current functionality because:

1. **Bootstrap objects** (created during cold start) are still SObject instances
2. Many parts of the VM still work with SObject classes directly
3. The migration to pure SObjectRef is incomplete

## Scope of Refactoring

### Files Using AsObject() (81 usages)
1. VirtualMachine.cs (3 usages)
2. Primitives/PrimitiveDispatcher.cs (15 usages)
3. Primitives/BluBookPrimitiveDispatcher.cs (8 usages)
4. Memory/GarbageCollector.cs (2 usages)
5. ColdStart/CharacterTableBuilder.cs (5 usages)
6. ColdStart/ColdStartBootstrap.cs (2 usages)
7. Core/Objects/SBlockContext.cs (3 usages)
8. Core/Objects/SDictionary.cs (1 usage)
9. Execution/ContextBasedInterpreter.cs (9 usages)
10. Execution/BytecodeInterpreter.cs (12 usages)
11. Execution/PrimitiveDispatcher.cs (9 usages)
12. Execution/TaggedBytecodeInterpreter.cs (2 usages)
13. Core/Objects/SMethodContext.cs (3 usages)
14. Core/Values/SObjectRef.cs (1 usage - in the method itself)

### Files Using FromObject() (32 usages)
1. Primitives/PrimitiveDispatcher.cs (1 usage)
2. Primitives/BluBookPrimitiveDispatcher.cs (1 usage)
3. Execution/BytecodeInterpreter.cs (1 usage)
4. Execution/TaggedBytecodeInterpreter.cs (4 usages)
5. Execution/PrimitiveDispatcher.cs (1 usage)
6. ColdStart/CharacterTableBuilder.cs (1 usage)
7. Execution/ContextBasedInterpreter.cs (1 usage)
8. Core/Objects/SBlockContext.cs (2 usages)
9. Core/Objects/SMethodContext.cs (4 usages)
10. Core/Objects/SSemaphore.cs (1 usage)
11. Core/Objects/SProcessScheduler.cs (3 usages)
12. Core/Objects/SProcess.cs (2 usages)
13. Core/Values/SObjectRef.cs (1 usage - in the method itself)

## Refactoring Strategy

To properly remove these methods, we need to complete the Phase 4 migration to pure SObjectRef architecture:

### Phase 1: Eliminate SObject Usage in Bootstrap (Large Task)

**Goal**: Convert all bootstrap object creation from SObject classes to pure SObjectRef/ObjectMemoryNew

**Steps**:
1. Modify `SystemDictionary.Initialize()` to create classes using `ObjectMemoryNew.AllocateObject()` instead of `new SClass()`
2. Store class references as `SObjectRef` instead of `SClass` instances
3. Update `ColdStartBootstrap` to work entirely with `SObjectRef`
4. Update all method installers to work with `SObjectRef` instead of `SObject`

**Files to Refactor**:
- Core/Objects/SystemDictionary.cs
- ColdStart/ColdStartBootstrap.cs
- ColdStart/KernelMethodsInstaller.cs
- ColdStart/MethodInstallers/*.cs (all method installer files)
- ColdStart/CharacterTableBuilder.cs

### Phase 2: Eliminate SObject Classes (Medium Task)

**Goal**: Remove all SObject C# class definitions since data will be stored in ObjectMemoryNew

**Files to Delete/Refactor**:
- Core/Objects/SArray.cs - Replace with SObjectRef + ObjectMemoryNew pointer indexing
- Core/Objects/SString.cs - Replace with SObjectRef + ObjectMemoryNew byte indexing
- Core/Objects/SSymbol.cs - Replace with SObjectRef + special symbol table
- Core/Objects/SClass.cs - Replace with SObjectRef + class metadata in ObjectMemoryNew
- Core/Objects/SCompiledMethod.cs - Replace with SObjectRef + compiled method format
- Core/Objects/SMethodContext.cs - Replace with SObjectRef + context fields
- Core/Objects/SBlockContext.cs - Replace with SObjectRef + block fields
- Core/Objects/SProcess.cs - Replace with SObjectRef + process fields
- Core/Objects/SProcessScheduler.cs - Replace with SObjectRef + scheduler fields
- Core/Objects/SSemaphore.cs - Replace with SObjectRef + semaphore fields
- Core/Objects/SDictionary.cs - Replace with SObjectRef + dictionary operations
- Core/Objects/SInstance.cs - Replace with SObjectRef + instance variable access

### Phase 3: Update All VM Operations (Large Task)

**Goal**: Convert all code that currently uses `.AsObject()` to use direct SObjectRef operations

**Pattern Replacements**:

```csharp
// OLD PATTERN 1: Get instance variable via SObject
var obj = someRef.AsObject() as SClass;
var value = obj.GetInstanceVariable(index);

// NEW PATTERN 1: Direct SObjectRef access
var value = someRef.GetInstanceVariable(index);

// OLD PATTERN 2: Call method on SObject
var method = methodRef.AsObject() as SCompiledMethod;
var bytecodes = method.Bytecodes;

// NEW PATTERN 2: Direct ObjectMemoryNew access
var bytecodes = ObjectMemoryNew.GetCompiledMethodBytecodes(methodRef.AsObjectPointer());

// OLD PATTERN 3: Create and wrap object
var newObj = new SArray(size);
var objRef = SObjectRef.FromObject(newObj);

// NEW PATTERN 3: Direct allocation
var arrayOop = ObjectMemoryNew.AllocateObject(
    SystemDictionary.ArrayClassRef,
    ObjectFormat.PointerIndexed,
    instanceVarCount: 0,
    indexedSize: size);
var objRef = SObjectRef.FromObjectPointer(arrayOop);
```

**Files to Refactor**:
- VirtualMachine.cs
- Execution/BytecodeInterpreter.cs
- Execution/ContextBasedInterpreter.cs
- Execution/TaggedBytecodeInterpreter.cs
- Execution/PrimitiveDispatcher.cs
- Primitives/PrimitiveDispatcher.cs
- Primitives/BluBookPrimitiveDispatcher.cs
- Memory/GarbageCollector.cs

### Phase 4: Remove Bridge Methods (Small Task)

**Goal**: Delete the obsolete methods once nothing uses them

**Steps**:
1. Remove `SObjectRef.AsObject()`
2. Remove `SObjectRef.FromObject()`
3. Remove `ObjectMemoryNew.GetObject()`
4. Remove `ObjectMemoryNew.GetOop()`
5. Remove `ObjectMemoryNew.RegisterBootstrapObject()`
6. Remove `ObjectMemoryNew.bootstrapObjectMap`

## Estimated Effort

- **Phase 1**: 40-60 hours (large architectural change)
- **Phase 2**: 20-30 hours (requires careful data structure design)
- **Phase 3**: 30-40 hours (many call sites to update)
- **Phase 4**: 1-2 hours (cleanup)

**Total**: 91-132 hours of development work

## Risks

1. **Breaking existing functionality**: The bootstrap process is delicate
2. **Performance impact**: Need to ensure new patterns are as efficient
3. **Testing complexity**: Need comprehensive tests for all object types
4. **Debugging difficulty**: Harder to inspect objects without C# classes

## Recommendation

Given the scope and complexity, this refactoring should be:
1. **Planned carefully** with design documents for each phase
2. **Tested incrementally** with full test suite runs after each change
3. **Done in a separate branch** to avoid breaking the main codebase
4. **Reviewed thoroughly** to ensure architectural consistency

Alternatively, consider **keeping the bridge methods** as a permanent feature of the mixed architecture, since they work correctly for bootstrap objects and enable gradual migration.
