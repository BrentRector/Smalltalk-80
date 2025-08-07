# Revised Step-by-Step Bootstrap (VM Primitive Operations)

## Overview

This document describes the exact sequence for bootstrapping the Smalltalk-80 class hierarchy using VM primitive operations. The bootstrap orchestrates primitive calls to construct actual Smalltalk objects in the VM's object memory - it does NOT create C# shadow objects.

## 🔹 Step 1: Create Metaclass (self-referential)

```csharp
int metaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(metaclassId, metaclassId); // SELF-REFERENTIAL!
vm.PrimitiveSetSlot(metaclassId, SLOT_NAME, vm.PrimitiveInternSymbol("Metaclass"));
vm.PrimitiveSetSlot(metaclassId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());
```

**Why**: Metaclass must exist first because all metaclasses are instances of Metaclass. The self-reference breaks the infinite regress.

## 🔹 Step 2: Create Class (instance of Metaclass)

```csharp
int classId = vm.PrimitiveNewClass();
vm.PrimitiveSetSlot(classId, SLOT_NAME, vm.PrimitiveInternSymbol("Class"));
vm.PrimitiveSetSlot(classId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());

// Create Class's metaclass
int classMetaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(classMetaclassId, metaclassId);
vm.PrimitiveSetSlot(classMetaclassId, SLOT_THISCLASS, classId);
vm.PrimitiveSetClass(classId, classMetaclassId);
```

**Why**: Class must exist before we can create other classes, as all non-meta classes are instances of Class.

## 🔹 Step 3: Create Behavior (instance of Class)

```csharp
int behaviorId = vm.PrimitiveNewClass();
vm.PrimitiveSetClass(behaviorId, classId);
vm.PrimitiveSetSlot(behaviorId, SLOT_NAME, vm.PrimitiveInternSymbol("Behavior"));
vm.PrimitiveSetSlot(behaviorId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());

// Create Behavior's metaclass
int behaviorMetaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(behaviorMetaclassId, metaclassId);
vm.PrimitiveSetSlot(behaviorMetaclassId, SLOT_THISCLASS, behaviorId);
vm.PrimitiveSetClass(behaviorId, behaviorMetaclassId);
```

**Why**: Behavior is the superclass of Class, providing basic class functionality.

## 🔹 Step 4: Fix Superclass Relationships

```csharp
// Now that all core classes exist, fix their superclass relationships
vm.PrimitiveSetSlot(classId, SLOT_SUPERCLASS, behaviorId);
vm.PrimitiveSetSlot(behaviorId, SLOT_SUPERCLASS, protoObjectId); // created next
vm.PrimitiveSetSlot(metaclassId, SLOT_SUPERCLASS, classId);

// Fix metaclass superclasses (parallel hierarchy)
vm.PrimitiveSetSlot(classMetaclassId, SLOT_SUPERCLASS, behaviorMetaclassId);
vm.PrimitiveSetSlot(behaviorMetaclassId, SLOT_SUPERCLASS, protoObjectClassId); // created next
```

## 🔹 Step 5: Create ProtoObject (the root)

```csharp
int protoObjectId = vm.PrimitiveNewClass();
vm.PrimitiveSetClass(protoObjectId, classId);
vm.PrimitiveSetSlot(protoObjectId, SLOT_NAME, vm.PrimitiveInternSymbol("ProtoObject"));
vm.PrimitiveSetSlot(protoObjectId, SLOT_SUPERCLASS, vm.PrimitiveNil()); // ROOT!
vm.PrimitiveSetSlot(protoObjectId, SLOT_METHODDICT, vm.PrimitiveNewDictionary());

// Create ProtoObject's metaclass
int protoObjectClassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(protoObjectClassId, metaclassId);
vm.PrimitiveSetSlot(protoObjectClassId, SLOT_THISCLASS, protoObjectId);
vm.PrimitiveSetSlot(protoObjectClassId, SLOT_SUPERCLASS, classId); // CRITICAL LOOP!
vm.PrimitiveSetClass(protoObjectId, protoObjectClassId);
```

**Why**: ProtoObject is the root of the instance hierarchy. Its metaclass's superclass is Class, creating the essential loop.

## 🔹 Step 6: Create Object (common protocol)

```csharp
int objectId = vm.PrimitiveNewClass();
vm.PrimitiveSetClass(objectId, classId);
vm.PrimitiveSetSlot(objectId, SLOT_NAME, vm.PrimitiveInternSymbol("Object"));
vm.PrimitiveSetSlot(objectId, SLOT_SUPERCLASS, protoObjectId);

// Set class variables (DependentsFields) at slot 6!
int classPool = vm.PrimitiveNewDictionary();
vm.PrimitiveDictionaryAtPut(classPool, 
    vm.PrimitiveInternSymbol("DependentsFields"), vm.PrimitiveNil());
vm.PrimitiveSetSlot(objectId, SLOT_CLASSPOOL, classPool);

// Automatically create Object's metaclass
int objectClassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(objectClassId, metaclassId);
vm.PrimitiveSetSlot(objectClassId, SLOT_THISCLASS, objectId);
vm.PrimitiveSetSlot(objectClassId, SLOT_SUPERCLASS, protoObjectClassId);
vm.PrimitiveSetClass(objectId, objectClassId);
```

## 🔹 Step 7: Create Essential Kernel Classes

For each kernel class, use the helper function:

```csharp
void CreateClass(ISmalltalkVM vm, string name, string superName, 
                 string[] instVars, string[] classVars)
{
    int classOop = vm.PrimitiveNewClass();
    vm.PrimitiveSetSlot(classOop, SLOT_NAME, vm.PrimitiveInternSymbol(name));
    vm.PrimitiveSetSlot(classOop, SLOT_SUPERCLASS, lookupClass(superName));
    
    // Set class variables at slot 6 if present
    if (classVars.Length > 0) {
        int classPool = vm.PrimitiveNewDictionary();
        foreach (var cv in classVars) {
            vm.PrimitiveDictionaryAtPut(classPool, 
                vm.PrimitiveInternSymbol(cv), vm.PrimitiveNil());
        }
        vm.PrimitiveSetSlot(classOop, SLOT_CLASSPOOL, classPool);
    }
    
    // Automatically create metaclass
    int metaclassOop = vm.PrimitiveNewMetaclass();
    vm.PrimitiveSetClass(metaclassOop, metaclassId);
    vm.PrimitiveSetSlot(metaclassOop, SLOT_THISCLASS, classOop);
    vm.PrimitiveSetClass(classOop, metaclassOop);
    
    // Store OOPs
    classOops[name] = classOop;
    metaclassOops[name] = metaclassOop;
}

// Use it for all classes:
CreateClass(vm, "UndefinedObject", "ProtoObject", new string[0], new string[0]);

// MessageSend
var messageSend = VM.CreateClass(
    name: "MessageSend",
    superclass: protoObject,
    instanceVariables: ["receiver", "selector", "arguments"],
    classVariables: [],
    isMetaclass: false
);

var messageSendMetaclass = VM.CreateMetaclass(
    thisClass: messageSend,
    superclass: protoObjectMetaclass
);

messageSend.SetClassPointer(messageSendMetaclass);
messageSendMetaclass.SetClassPointer(metaclass);

// MethodContext
var methodContext = VM.CreateClass(
    name: "MethodContext",
    superclass: protoObject,
    instanceVariables: ["sender", "receiver", "method", "pc", "stack"],
    classVariables: [],
    isMetaclass: false
);

var methodContextMetaclass = VM.CreateMetaclass(
    thisClass: methodContext,
    superclass: protoObjectMetaclass
);

methodContext.SetClassPointer(methodContextMetaclass);
methodContextMetaclass.SetClassPointer(metaclass);
```

## 🔹 Step 8: Create Behavior Hierarchy Extensions

```csharp
// ClassDescription
var classDescription = VM.CreateClass(
    name: "ClassDescription",
    superclass: behavior,
    instanceVariables: ["instanceVariables", "organization"],
    classVariables: [],
    isMetaclass: false
);

// Update Class and Metaclass to inherit from ClassDescription
classClass.SetSuperclass(classDescription);
metaclass.SetSuperclass(classDescription);
```

## 🔹 Step 9: Install Core Methods

```csharp
// Install methods via VM primitive compilation
vm.PrimitiveInstallMethod(protoObjectId, "==", 
    "== anObject\n\t<primitive: 110>\n\tself primitiveFailed");
vm.PrimitiveInstallMethod(protoObjectId, "~~", 
    "~~ anObject\n\t^(self == anObject) not");
vm.PrimitiveInstallMethod(protoObjectId, "class", 
    "class\n\t<primitive: 111>\n\tself primitiveFailed");
vm.PrimitiveInstallMethod(protoObjectId, "doesNotUnderstand:", 
    "doesNotUnderstand: aMessage\n\tself error: 'Message not understood'");

// MessageSend methods
VM.InstallMethod(messageSend, "value",
    CompiledMethod.FromSource("value ^receiver perform: selector withArguments: arguments"));

// MessageSend class methods (on metaclass)
VM.InstallMethod(messageSendMetaclass, "receiver:selector:arguments:",
    CompiledMethod.FromSource("receiver: r selector: s arguments: a ^self new..."));

// UndefinedObject methods
VM.InstallMethod(undefinedObject, "isNil",
    CompiledMethod.FromSource("isNil ^true"));
VM.InstallMethod(undefinedObject, "notNil",
    CompiledMethod.FromSource("notNil ^false"));
```

## 🔹 Step 10: Create Essential Support Classes

```csharp
// Boolean hierarchy
var boolean = VM.CreateClass("Boolean", object, [], []);
var trueClass = VM.CreateClass("True", boolean, [], []);
var falseClass = VM.CreateClass("False", boolean, [], []);

// SmallInteger
var smallInteger = VM.CreateClass("SmallInteger", object, [], []);

// Symbol
var symbol = VM.CreateClass("Symbol", object, [], ["SymbolTable"]);

// Array
var array = VM.CreateClass("Array", object, [], []);

// Dictionary
var dictionary = VM.CreateClass("Dictionary", object, ["tally", "array"], []);

// Association
var association = VM.CreateClass("Association", object, ["key", "value"], []);

// CompiledMethod
var compiledMethod = VM.CreateClass("CompiledMethod", object, 
    ["header", "literals", "bytecodes"], []);

// BlockContext
var blockContext = VM.CreateClass("BlockContext", object, 
    ["nargs", "startpc", "home"], []);
```

## 🔹 Step 11: Create Singleton Instances

```csharp
// Create singleton instances via VM primitives
int nilId = vm.PrimitiveNewInstance(undefinedObjectId);
vm.PrimitiveRegisterGlobal("nil", nilId);

int trueId = vm.PrimitiveNewInstance(trueClassId);
vm.PrimitiveRegisterGlobal("true", trueId);

int falseId = vm.PrimitiveNewInstance(falseClassId);
vm.PrimitiveRegisterGlobal("false", falseId);
```

## 🔹 Step 12: Create Smalltalk Global Dictionary

```csharp
// Create Smalltalk as instance of SystemDictionary (or Dictionary)
var smalltalk = VM.CreateInstance(dictionary);
VM.SetInstanceVariable(smalltalk, "tally", 0);
VM.SetInstanceVariable(smalltalk, "array", VM.CreateInstance(array, 101));

// Register all classes in Smalltalk
VM.DictionaryAtPut(smalltalk, "ProtoObject", protoObject);
VM.DictionaryAtPut(smalltalk, "Object", object);
VM.DictionaryAtPut(smalltalk, "Class", classClass);
VM.DictionaryAtPut(smalltalk, "Metaclass", metaclass);
VM.DictionaryAtPut(smalltalk, "Behavior", behavior);
// ... etc for all classes

VM.RegisterGlobal("Smalltalk", smalltalk);
```

## 🔧 Summary of VM Primitives

| Primitive Name | Purpose | Returns |
|---------------|---------|----------|
| `PrimitiveNewClass()` | Create a class object | OOP (int) |
| `PrimitiveNewMetaclass()` | Create a metaclass object | OOP (int) |
| `PrimitiveNewInstance(classId)` | Create instance of class | OOP (int) |
| `PrimitiveSetSlot(oop, index, value)` | Set slot at index | void |
| `PrimitiveSetClass(oop, classOop)` | Set class pointer | void |
| `PrimitiveInternSymbol(name)` | Intern a symbol | OOP (int) |
| `PrimitiveNewArray(size)` | Create array | OOP (int) |
| `PrimitiveNewDictionary()` | Create dictionary | OOP (int) |
| `PrimitiveInstallMethod(class, sel, src)` | Compile & install method | void |
| `PrimitiveRegisterGlobal(name, oop)` | Register global | void |
| `PrimitiveNil()` | Get nil OOP | OOP (int) |

## Validation Checklist

After bootstrap, verify:

- [ ] `Metaclass class == Metaclass` (self-referential)
- [ ] `ProtoObject superclass == nil` (root)
- [ ] `Object superclass == ProtoObject`
- [ ] `Class superclass == Behavior` (or ClassDescription)
- [ ] `Behavior superclass == ProtoObject`
- [ ] `ProtoObject class superclass == Class` (loop!)
- [ ] `nil class == UndefinedObject`
- [ ] `true class == True`
- [ ] `false class == False`
- [ ] All classes have methodDict at slot 1
- [ ] All Class instances have classPool at slot 6

## Critical Insights

1. **Metaclass must be created first** and made self-referential
2. **ProtoObject class → Class** creates the essential loop
3. **Parallel hierarchies** must be maintained for metaclasses
4. **ClassDescription can be inserted later** between Behavior and Class/Metaclass
5. **Instance variables must match** the expected Smalltalk-80 layout
6. **Method installation** happens after all classes exist
7. **Singletons** (nil, true, false) are created as instances after their classes exist

This bootstrap sequence creates a minimal but complete Smalltalk-80 system capable of:
- Creating new classes via message sends
- Method lookup through the class hierarchy
- Metaclass protocol for class-side behavior
- Proper nil, true, false singletons
- Global namespace (Smalltalk dictionary)