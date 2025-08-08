# Minimal Bootstrap Specification

## Overview

This document describes the minimal set of classes and methods required to bootstrap a functional Smalltalk-80 system. Each class and method is annotated with the VM primitive it requires.

## Class Hierarchy

```
ProtoObject (root - superclass is nil)
├── Object (common protocol)
│   ├── Behavior (class infrastructure)
│   │   ├── ClassDescription (organization)
│   │   │   ├── Class (regular classes)
│   │   │   └── Metaclass (class of classes)
│   ├── Symbol (unique strings)
│   ├── Array (indexed collections)
│   ├── Dictionary (key-value mapping)
│   │   └── MethodDictionary (method storage)
│   ├── CompiledMethod (bytecode)
│   ├── Boolean (abstract)
│   │   ├── True
│   │   └── False
│   └── SmallInteger (immediate numbers)
└── UndefinedObject (nil's class)
```

## Core Classes and Their Primitives

### ProtoObject - The Absolute Root

**Purpose**: Minimal protocol that all objects understand

**Key Methods**:
- `class` → primitive 111 (get class pointer from slot 0)
- `basicAt:` → primitive 60 (indexed variable access)
- `basicAt:put:` → primitive 61 (indexed variable store)
- `==` → primitive 110 (identity test)
- `doesNotUnderstand:` → primitive 131 (message trap)

**VM Operations**:
```csharp
// Create ProtoObject
int protoObjectId = vm.PrimitiveNewClass();
vm.PrimitiveSetSlot(protoObjectId, SLOT_SUPERCLASS, vm.PrimitiveNil());
vm.PrimitiveSetSlot(protoObjectId, SLOT_NAME, vm.PrimitiveInternSymbol("ProtoObject"));
```

### Object - Common Protocol

**Purpose**: Standard protocol inherited by most classes

**Key Methods**:
- `perform:` → primitive 83 (message send)
- `perform:with:` → primitive 84 (message send with arg)
- `basicNew` → primitive 70 (instance creation)
- `basicNew:` → primitive 71 (indexed instance creation)
- `instVarAt:` → primitive 73 (named variable access)
- `instVarAt:put:` → primitive 74 (named variable store)

### Behavior - Class Infrastructure

**Purpose**: Foundation for all class-like objects

**Instance Variables**:
- `superclass` - The class's superclass
- `methodDict` - Dictionary mapping selectors to CompiledMethods
- `format` - Instance specification

**Key Methods**:
- `compile:` → primitive 117 (method compilation)
- `basicNew` → primitive 70 (create instances)

**Critical**: methodDict must be at slot 1 for method lookup!

### Class - Regular Classes

**Purpose**: Non-metaclass classes

**Instance Variables**:
- `name` - Symbol naming the class
- `classPool` - Dictionary of class variables (SLOT 6!)
- `sharedPools` - Shared variable pools

**Key Methods**:
- `subclass:instanceVariableNames:...` → primitive 177 (class creation)

**Critical**: classPool MUST be at slot 6 for class variable access!

### Metaclass - The Class of Classes

**Purpose**: Every class's class is a metaclass

**Instance Variables**:
- `thisClass` - The class this metaclass describes

**Special Property**: Metaclass is self-referential
```csharp
vm.PrimitiveSetClass(metaclassId, metaclassId); // Points to itself!
```

### Symbol - Unique Strings

**Purpose**: Interned strings for selectors and names

**Key Methods**:
- `intern:` → primitive 91 (symbol table lookup/creation)
- `=` - Symbols are unique, equality is identity

### Dictionary & MethodDictionary

**Purpose**: Key-value mapping for method storage

**Key Methods**:
- `at:put:` → primitive 96 (dictionary store)
- `at:ifAbsent:` → primitive 97 (dictionary lookup)

**MethodDictionary**:
- `at:put:` → primitive 98 (specialized for method storage)

### CompiledMethod - Bytecode

**Purpose**: Compiled method representation

**Instance Variables**:
- `header` - Encodes numArgs, numTemps, primitive index
- `literals` - Literal pool (selector is last)
- `bytecodes` - Actual bytecode instructions

### UndefinedObject - nil's Class

**Purpose**: The class of nil

**Key Methods**:
- `isNil` → returns true
- `notNil` → returns false

### Boolean Hierarchy

**Purpose**: true and false singletons

**Key Methods**:
- `ifTrue:` / `ifFalse:` / `ifTrue:ifFalse:` - Control flow
- `not` - Logical negation

### SmallInteger

**Purpose**: Immediate integer values

**Key Methods**:
- `+` → primitive 1 (addition)
- `-` → primitive 2 (subtraction)
- `<` → primitive 3 (less than)
- `=` → primitive 7 (equality)

## VM Primitive Summary

| Primitive | Name | Purpose |
|-----------|------|---------|
| 1 | SmallInteger+ | Integer addition |
| 2 | SmallInteger- | Integer subtraction |
| 3 | SmallInteger< | Integer comparison |
| 7 | SmallInteger= | Integer equality |
| 60 | basicAt: | Indexed variable access |
| 61 | basicAt:put: | Indexed variable store |
| 62 | size | Collection size |
| 70 | basicNew | Instance creation |
| 71 | basicNew: | Indexed instance creation |
| 73 | instVarAt: | Named variable access |
| 74 | instVarAt:put: | Named variable store |
| 76 | hash | Object hash |
| 83 | perform: | Message send |
| 84 | perform:with: | Message send with arg |
| 91 | intern: | Symbol interning |
| 96 | at:put: | Dictionary store |
| 97 | at:ifAbsent: | Dictionary lookup |
| 98 | methodAt:put: | Method dictionary store |
| 110 | == | Identity test |
| 111 | class | Get class pointer |
| 117 | compile: | Method compilation |
| 131 | doesNotUnderstand: | Message trap |
| 177 | subclass:... | Class creation |

## Bootstrap Process

1. **Create ProtoObject** (root with no superclass)
2. **Create core metaclass system** (Metaclass, Class, Behavior)
3. **Fix circular references** (Metaclass points to itself)
4. **Create Object** (common protocol)
5. **Create essential classes** (Symbol, Dictionary, Array)
6. **Install primitive methods** (using VM compilation)
7. **Create singletons** (nil, true, false)

## Critical Invariants

1. **ProtoObject has no superclass**: `ProtoObject superclass == nil`
2. **Metaclass is self-referential**: `Metaclass class == Metaclass`
3. **Method dictionary at slot 1**: For method lookup
4. **Class variables at slot 6**: For class variable access
5. **ProtoObject class loop**: `ProtoObject class superclass == Class`

## Minimal Working System

With these classes and methods, you can:
- Create new classes (`subclass:...`)
- Create instances (`new`, `basicNew`)
- Send messages (`perform:`)
- Store methods (`compile:`)
- Handle errors (`doesNotUnderstand:`)
- Use basic control flow (`ifTrue:ifFalse:`)
- Perform arithmetic (`+`, `-`, `<`, `=`)

This is the absolute minimum needed for a self-hosting Smalltalk system.