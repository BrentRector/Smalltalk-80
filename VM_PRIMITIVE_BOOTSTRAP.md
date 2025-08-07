# VM Primitive Bootstrap Architecture

## Overview

The Smalltalk-80 bootstrap creates the foundational class hierarchy by orchestrating primitive VM operations. The bootstrap does NOT create C# shadow objects - it constructs actual Smalltalk objects inside the VM's object memory using primitive operations.

## Core Concept: Object IDs (OOPs)

The VM works with **Object-Oriented Pointers (OOPs)** - integer IDs that reference objects in the VM's object memory. All bootstrap operations work with these OOPs, not C# objects.

## ISmalltalkVM Interface

The bootstrap communicates with the VM through a primitive interface:

```csharp
public interface ISmalltalkVM
{
    // Object creation primitives
    int PrimitiveNewClass();           // Create a class object
    int PrimitiveNewMetaclass();       // Create a metaclass object
    int PrimitiveNewInstance(int classId); // Create instance of class
    
    // Object manipulation primitives
    void PrimitiveSetSlot(int objectId, int slotIndex, int valueId);
    void PrimitiveSetClass(int objectId, int classId);
    
    // Symbol and collection primitives
    int PrimitiveInternSymbol(string name);
    int PrimitiveNewArray(int size);
    int PrimitiveNewDictionary();
    void PrimitiveArrayAtPut(int arrayId, int index, int valueId);
    void PrimitiveDictionaryAtPut(int dictId, int keyId, int valueId);
    
    // Method compilation primitive
    void PrimitiveInstallMethod(int classId, string selector, string source);
    
    // Special values
    int PrimitiveNil();
    
    // Global registration
    void PrimitiveRegisterGlobal(string name, int valueId);
}
```

## VM Object Layout

### Slot Indices (Critical!)

```
Object slots:
[0] = class pointer (for all objects)

Class object slots:
[0] = superclass
[1] = methodDict
[2] = format
[3] = instanceVariables (array of symbols)
[4] = organization
[5] = name (symbol)
[6] = classPool (Dictionary) ⚠️ CRITICAL for class variables!
[7] = sharedPools

Metaclass object slots:
[0] = superclass
[1] = methodDict
[2] = format
[3] = instanceVariables
[4] = organization
[5] = thisClass (the class this metaclass describes)
```

## Bootstrap Sequence

### Phase 1: Core Metaclass System

```csharp
// 1. Create Metaclass (self-referential)
metaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(metaclassId, metaclassId); // Points to itself!
vm.PrimitiveSetSlot(metaclassId, 5, vm.PrimitiveInternSymbol("Metaclass"));

// 2. Create Class
classId = vm.PrimitiveNewClass();
classMetaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(classId, classMetaclassId);
vm.PrimitiveSetClass(classMetaclassId, metaclassId);
vm.PrimitiveSetSlot(classMetaclassId, 5, classId); // thisClass

// 3. Create Behavior
behaviorId = vm.PrimitiveNewClass();
behaviorMetaclassId = vm.PrimitiveNewMetaclass();
// ... setup similar to Class

// 4. Create ProtoObject (root)
protoObjectId = vm.PrimitiveNewClass();
protoObjectClassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetSlot(protoObjectId, 0, vm.PrimitiveNil()); // No superclass!
vm.PrimitiveSetSlot(protoObjectClassId, 0, classId); // CRITICAL LOOP!
```

### Phase 2: Class Hierarchy Setup

```csharp
// Fix superclass relationships
vm.PrimitiveSetSlot(behaviorId, 0, protoObjectId);
vm.PrimitiveSetSlot(classDescriptionId, 0, behaviorId);
vm.PrimitiveSetSlot(classId, 0, classDescriptionId);
vm.PrimitiveSetSlot(metaclassId, 0, classDescriptionId);

// Parallel metaclass hierarchy
vm.PrimitiveSetSlot(behaviorMetaclassId, 0, protoObjectClassId);
// ... etc
```

### Phase 3: Essential Classes

For each class defined in Bootstrap.st:

```csharp
void CreateClass(ISmalltalkVM vm, string name, string superName, 
                 string[] instVars, string[] classVars)
{
    // Create the class
    int classOop = vm.PrimitiveNewClass();
    vm.PrimitiveSetSlot(classOop, 5, vm.PrimitiveInternSymbol(name));
    
    // Set superclass
    vm.PrimitiveSetSlot(classOop, 0, lookupClass(superName));
    
    // Set class variables at slot 6!
    if (classVars.Length > 0) {
        int classPool = vm.PrimitiveNewDictionary();
        foreach (var cv in classVars) {
            vm.PrimitiveDictionaryAtPut(classPool, 
                vm.PrimitiveInternSymbol(cv), vm.PrimitiveNil());
        }
        vm.PrimitiveSetSlot(classOop, 6, classPool);
    }
    
    // Automatically create metaclass
    int metaclassOop = vm.PrimitiveNewMetaclass();
    vm.PrimitiveSetClass(metaclassOop, metaclassId);
    vm.PrimitiveSetSlot(metaclassOop, 5, classOop); // thisClass
    vm.PrimitiveSetClass(classOop, metaclassOop);
}
```

### Phase 4: Method Installation

```csharp
// Install methods by compiling source
vm.PrimitiveInstallMethod(protoObjectId, "==", 
    "== anObject\n\t<primitive: 110>\n\tself primitiveFailed");

vm.PrimitiveInstallMethod(protoObjectClassId, "new",
    "new\n\t^self basicNew");
```

### Phase 5: Singleton Creation

```csharp
// Create nil, true, false instances
nilId = vm.PrimitiveNewInstance(undefinedObjectId);
trueId = vm.PrimitiveNewInstance(trueClassId);
falseId = vm.PrimitiveNewInstance(falseClassId);

// Register as globals
vm.PrimitiveRegisterGlobal("nil", nilId);
vm.PrimitiveRegisterGlobal("true", trueId);
vm.PrimitiveRegisterGlobal("false", falseId);
```

## Critical Invariants

1. **Metaclass is self-referential**: `Metaclass class == Metaclass`
2. **ProtoObject is the root**: `ProtoObject superclass == nil`
3. **ProtoObject class creates the loop**: `ProtoObject class superclass == Class`
4. **Class variables at slot 6**: All Class objects store classPool at index 6
5. **Metaclasses are never explicit**: Created automatically when classes are defined
6. **Parallel hierarchies**: Metaclass hierarchy mirrors the class hierarchy

## File Structure

- `ISmalltalkVM.cs` - VM primitive interface definition
- `VMPrimitiveBootstrapGenerator.cs` - Parses Bootstrap.st and generates primitive calls
- `GeneratedVMBootstrap.cs` - Generated code that orchestrates VM primitives
- `Bootstrap.st` - Smalltalk source defining bootstrap classes (no explicit metaclasses!)

## Validation

After bootstrap, verify:

```csharp
// Metaclass self-reference
assert(vm.GetClass(metaclassId) == metaclassId);

// ProtoObject is root
assert(vm.GetSuperclass(protoObjectId) == nilId);

// The loop exists
assert(vm.GetSuperclass(vm.GetClass(protoObjectId)) == classId);

// Class variables accessible
assert(vm.GetSlot(objectId, 6) != nilId); // Object has DependentsFields
```

## Key Differences from C# Shadow Approach

❌ **OLD (Wrong)**: Create C# SmalltalkClass objects, manipulate in C#
✅ **NEW (Correct)**: Orchestrate VM primitives, construct real Smalltalk objects

The bootstrap is now a **choreographer** of VM operations, not a creator of proxy objects. Every primitive call manipulates actual Smalltalk objects in the VM's object memory.