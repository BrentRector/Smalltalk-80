# Complete Smalltalk Bootstrap Process

A comprehensive, step-by-step breakdown of the full end-to-end bootstrapping process for a Smalltalk system, divided into three distinct phases. Each phase includes explicit details of class creation, method installation, and system functionality, with no assumptions.

## 🧱 Phase 1: Primitive-Level Bootstrap

This phase builds the minimal class lattice using direct VM primitives. No messaging, no bytecode—just raw object creation and pointer patching.

### 🔹 Step-by-Step Class Creation

#### 1. Create Metaclass (self-referential)
```
primitiveNewClass() → metaclassId
primitiveSetSlot(metaclassId, CLASS_SLOT, metaclassId)
primitiveSetNamedSlot(metaclassId, "name", intern("Metaclass"))
```

#### 2. Create Class
```
primitiveNewClass() → classId
primitiveSetSlot(classId, CLASS_SLOT, metaclassId)
primitiveSetNamedSlot(classId, "superclass", behaviorId)
primitiveSetNamedSlot(classId, "name", intern("Class"))
```

#### 3. Create Behavior
```
primitiveNewClass() → behaviorId
primitiveSetSlot(behaviorId, CLASS_SLOT, classId)
primitiveSetNamedSlot(behaviorId, "superclass", objectId)
primitiveSetNamedSlot(behaviorId, "methodDict", primitiveNewDictionary())
primitiveSetNamedSlot(behaviorId, "instanceVariables", primitiveNewArray(0))
```

#### 4. Create Object
```
primitiveNewClass() → objectId
primitiveSetSlot(objectId, CLASS_SLOT, objectMetaclassId)
primitiveSetNamedSlot(objectId, "superclass", protoObjectId)
primitiveSetNamedSlot(objectId, "name", intern("Object"))
```

#### 5. Create ProtoObject
```
primitiveNewClass() → protoObjectId
primitiveSetSlot(protoObjectId, CLASS_SLOT, protoObjectMetaclassId)
primitiveSetNamedSlot(protoObjectId, "superclass", null)
primitiveSetNamedSlot(protoObjectId, "name", intern("ProtoObject"))
```

#### 6. Create Symbol, Dictionary
Same pattern: `primitiveNewClass()`, set superclass, name, methodDict, instanceVariables.

#### 7. Install Methods
For each method:
```
primitiveInstallMethod(classId, selector, sourceCode)
```
Use `intern("selector")` for symbol resolution.

### Phase 1 Result
- Core class lattice exists
- Basic primitives wired up
- Method lookup functional
- No messaging capability yet

## 🧠 Phase 2: Bytecode-Driven Bootstrap

Now that messaging and method dispatch work, you can define new classes and methods using Smalltalk source and execute them via bytecode.

### 🔹 Classes to Define via Messaging
- Message
- UndefinedObject
- Nil (global instance)
- Boolean, True, False
- Array, String, OrderedCollection
- SystemDictionary, Smalltalk
- Compiler, Parser, Scanner

### 🔹 How to Instantiate
1. Use `Class >> new` to create class objects
2. Use `addMethod:source:` to install methods
3. Use `perform:` to send messages like:

```smalltalk
Object subclass: #Boolean
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel'.
```

4. Use `Symbol >> intern:` and `Dictionary >> at:put:` to bind globals

### 🔹 Execution Flow
1. Parse `bootstrap_phase2.st`
2. Compile methods to bytecode or use source strings
3. Send messages to define classes and install methods
4. Use `Smalltalk at: #ClassName put: classObject` to register

### Phase 2 Result
- Message objects for reflection
- Complete control structures
- Basic collections operational
- Compiler stubs in place
- System can create classes via messaging

## 🧩 Phase 3: Headless System Bootstrap

Goal: Load the full Smalltalk class library required for a non-GUI, headless system.

### 🔹 Required Classes
- Integer, Float, Character
- BlockClosure, Context
- Process, Scheduler
- FileStream, Transcript
- SystemUtilities, Error, Exception

### 🔹 How to Load

#### 1. Use Phase 2 Compiler
```smalltalk
Compiler >> compile:sourceString forClass: classSymbol
```
Produces compiled method objects

#### 2. Use File Reader
Define `FileStream >> nextLine`, `atEnd`, etc.
Read `.st` files line-by-line

#### 3. Parse and Evaluate
Use Parser and Scanner to tokenize and parse
Evaluate class definitions and method bodies

#### 4. Install Methods
Use `addMethod:` or `installMethod:` to populate classes

#### 5. Bind Globals
Use SystemDictionary to register new classes

### Phase 3 Result
- Full compiler and parser
- Complete collection hierarchy
- Stream and file system
- Error handling framework
- System is fully self-hosting

## 🧠 Summary

| Phase | Mechanism | Capabilities |
|-------|-----------|--------------|
| 1 | VM Primitives | Class lattice, slot patching |
| 2 | Bytecode Execution | Messaging, class creation, dispatch |
| 3 | In-Image Compiler | Full class library loading |

## 📊 Implementation Metrics

### Phase 1: Primitive Bootstrap
- ~15 core classes created
- ~50 primitive methods installed
- ~500 lines of VM primitive calls
- Zero messaging capability

### Phase 2: Bytecode Bootstrap
- ~50 classes defined
- ~200 methods installed
- Messaging fully operational
- Basic compiler functional

### Phase 3: Headless System
- ~300+ classes loaded
- ~3000+ methods compiled
- Full file I/O operational
- Complete error handling

## 🔑 Critical Invariants

1. **Metaclass Self-Reference**: Metaclass's class pointer points to itself
2. **ProtoObject Root**: ProtoObject has no superclass (nil)
3. **Class Variables**: Must be at slot index 6 throughout all phases
4. **Method Dictionary**: Must be at slot index 2 for method lookup
5. **Symbol Interning**: All selectors must be interned symbols

## 🎯 Success Criteria

### Phase 1 Complete When:
✅ ProtoObject → Object → Behavior → Class → Metaclass chain exists
✅ Method lookup via primitive works
✅ Basic primitives (new, at:, at:put:) installed

### Phase 2 Complete When:
✅ Can send messages between objects
✅ Can create classes via `subclass:` family
✅ Boolean control structures work
✅ Basic collections operational

### Phase 3 Complete When:
✅ Can compile methods from source strings
✅ Can load .st files
✅ Error handling works
✅ System can recreate itself from source

## 🚀 Automation Support

The process can be automated using JSON manifests:
- `Phase1Bootstrap.json`: VM primitive orchestration
- `Phase2Bootstrap.json`: Bytecode-driven definitions
- `Phase3Bootstrap.json`: File loading commands
- `bootstrap_phase2.st`: Smalltalk source for Phase 2

These manifests provide machine-readable instructions for each bootstrap phase, enabling fully automated system construction from bare VM to self-hosting Smalltalk environment.