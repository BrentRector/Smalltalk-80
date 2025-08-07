# Metaclass Auto-Creation Process

## The Fundamental Rule

**Metaclasses are NEVER explicitly defined in Smalltalk source code.** They are automatically created by the system when a class is defined.

## ❌ WRONG: Explicit Metaclass Definition

```smalltalk
"THIS IS WRONG - Never do this!"
Class subclass: #ProtoObject class
    instanceVariableNames: ''
    classVariableNames: ''
    package: 'Bootstrap-Core'
```

## ✅ CORRECT: Regular Class Definition Only

```smalltalk
"Define only the class - metaclass is automatic"
nil subclass: #ProtoObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'
```

## How Metaclass Creation Works

When you write:
```smalltalk
ProtoObject subclass: #MessageSend
    instanceVariableNames: 'receiver selector arguments'
    classVariableNames: ''
    package: 'Bootstrap-Core'
```

The system automatically:

1. **Creates MessageSend** (the class)
   - Instance of: MessageSend class (its metaclass)
   - Superclass: ProtoObject

2. **Creates MessageSend class** (the metaclass)
   - Instance of: Metaclass
   - Superclass: ProtoObject class
   - Contains class-side methods

## The Metaclass Hierarchy

For every class hierarchy:
```
ProtoObject → Object → Collection → Array
```

There's a parallel metaclass hierarchy:
```
ProtoObject class → Object class → Collection class → Array class
```

## What the VM Primitive Bootstrap Must Do

### Phase 1: Manual Creation (Core Classes Only)

For the bootstrap classes (ProtoObject, Behavior, Class, Metaclass), manually create both using VM primitives:

```csharp
// Create ProtoObject
int protoObjectId = vm.PrimitiveNewClass();
vm.PrimitiveSetSlot(protoObjectId, SLOT_NAME, vm.PrimitiveInternSymbol("ProtoObject"));
vm.PrimitiveSetSlot(protoObjectId, SLOT_SUPERCLASS, vm.PrimitiveNil());

// Create ProtoObject class (its metaclass)
int protoObjectClassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetSlot(protoObjectClassId, SLOT_THISCLASS, protoObjectId);
vm.PrimitiveSetSlot(protoObjectClassId, SLOT_SUPERCLASS, classId); // → Class

// Link them
vm.PrimitiveSetClass(protoObjectId, protoObjectClassId);
```

### Phase 2: Automatic Creation (All Other Classes)

For classes defined in Bootstrap.st, parse and auto-create using VM primitives:

```csharp
void CreateClass(ISmalltalkVM vm, string className, string superclassName)
{
    // 1. Create the class
    int classOop = vm.PrimitiveNewClass();
    int superclassOop = classOops[superclassName];
    vm.PrimitiveSetSlot(classOop, SLOT_SUPERCLASS, superclassOop);
    vm.PrimitiveSetSlot(classOop, SLOT_NAME, vm.PrimitiveInternSymbol(className));
    
    // 2. AUTOMATICALLY create its metaclass
    int metaclassOop = vm.PrimitiveNewMetaclass();
    int superMetaclassOop = metaclassOops[superclassName];
    vm.PrimitiveSetSlot(metaclassOop, SLOT_SUPERCLASS, superMetaclassOop);
    vm.PrimitiveSetSlot(metaclassOop, SLOT_THISCLASS, classOop);
    
    // 3. Link them
    vm.PrimitiveSetClass(classOop, metaclassOop);
    vm.PrimitiveSetClass(metaclassOop, metaclassId); // All metaclasses are instances of Metaclass
    
    // 4. Store OOPs for later reference
    classOops[className] = classOop;
    metaclassOops[className] = metaclassOop;
}
```

## Example: Creating Object

When Bootstrap.st contains:
```smalltalk
ProtoObject subclass: #Object
    instanceVariableNames: ''
    classVariableNames: 'DependentsFields'
```

The generator must:

1. **Parse** the definition
2. **Create Object**
   - Superclass: ProtoObject
   - Instance variables: (none)
   - Class variables: DependentsFields

3. **Automatically create Object class**
   - Instance of: Metaclass
   - Superclass: ProtoObject class
   - thisClass: Object

4. **Link them**
   - Object's class → Object class
   - Object class's class → Metaclass

## Class-Side Methods

When you see:
```smalltalk
!Association class methodsFor: 'instance creation'!
key: aKey value: aValue
    ^self new key: aKey; value: aValue; yourself! !
```

This method goes into **Association class** (the metaclass), which was automatically created when Association was defined.

## The Special Cases

### Metaclass itself
- **Metaclass** is an instance of **Metaclass class**
- **Metaclass class** is an instance of **Metaclass** (circular!)

### ProtoObject class
- Superclass: **Class** (not nil!)
- This creates the loop: ProtoObject class → Class → Behavior → ProtoObject

## Validation

After bootstrap, verify using VM queries:

```csharp
// Every class has a metaclass
Assert(vm.GetClass(protoObjectId) == protoObjectClassId);
Assert(vm.GetClass(objectId) == objectClassId);

// Metaclasses form parallel hierarchy
Assert(vm.GetSlot(objectClassId, SLOT_SUPERCLASS) == protoObjectClassId);

// All metaclasses are instances of Metaclass
Assert(vm.GetClass(objectClassId) == metaclassId);
Assert(vm.GetClass(protoObjectClassId) == metaclassId);

// Metaclass is self-referential
Assert(vm.GetClass(metaclassId) == metaclassId);
```

## Common Mistakes to Avoid

1. **Never define metaclasses explicitly** in .st files
2. **Don't confuse class methods with metaclass definition**
3. **Remember the parallel hierarchy** - if A subclasses B, then A class subclasses B class
4. **ProtoObject class superclass is Class**, not nil
5. **Metaclass is the only self-referential class** (its class is itself)

## Summary

The key insight: **Metaclasses are implementation details that are automatically managed by the system.** When you define a class in Smalltalk, you get its metaclass for free. The bootstrap generator must replicate this automatic creation process.