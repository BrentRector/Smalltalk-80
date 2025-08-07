# Smalltalk-80 Bootstrap Strategy

## Overview

This document describes the minimal bootstrap strategy for creating a self-hosting Smalltalk-80 system using only VM primitives.

## Phase 1: Minimal Class Set for Bootstrapping

To reach the point where Smalltalk can define new classes via `Object subclass:`, we need exactly 9 essential classes:

| Class Name | Purpose | Notes |
|------------|---------|-------|
| **Object** | Root of all classes | Must exist before anything else can subclass |
| **Class** | Metaclass for all classes | Needed to instantiate classes like Object |
| **Metaclass** | Class of Class | Enables metaclass hierarchy |
| **Behavior** | Superclass of Class and Metaclass | Defines method dictionary, superclass, etc. |
| **CompiledMethod** | Holds bytecode for methods | Needed for method execution |
| **Symbol** | Used for method names, keys | Required for dictionary keys |
| **Dictionary** | Used for method dictionaries | Needed to store methods |
| **Array** | Used for instance variable storage | Needed for object slots |
| **UndefinedObject** | Singleton nil | Required for default slot values |

## Phase 2: Manual Construction via VM Primitives

Using only VM-level primitives (`primitiveNew`, `primitiveAt:put:`, `primitiveSetClass`), we manually construct the system:

### Step-by-Step Construction

#### 1. Create nil
```smalltalk
nil := primitiveNew: 0.  "No fields"
```

#### 2. Create Symbol Class
Symbols are needed to name methods and variables.
```smalltalk
symbolClass := primitiveNewClassNamed: 'Symbol'.
primitiveSetClass: symbolClass superclass: nil.
primitiveSetClass: symbolClass methodDict: primitiveNewDictionary.
```

#### 3. Create Dictionary Class
Used to hold method dictionaries and globals.
```smalltalk
dictClass := primitiveNewClassNamed: 'Dictionary'.
primitiveSetClass: dictClass superclass: objectClass.
primitiveSetClass: dictClass methodDict: primitiveNewDictionary.
```

#### 4. Create Object Class
This is the root of all classes.
```smalltalk
objectClass := primitiveNewClassNamed: 'Object'.
primitiveSetClass: objectClass superclass: nil.
primitiveSetClass: objectClass methodDict: primitiveNewDictionary.
```

#### 5. Create Behavior, Class, and Metaclass
These define the class hierarchy.
```smalltalk
behaviorClass := primitiveNewClassNamed: 'Behavior'.
classClass := primitiveNewClassNamed: 'Class'.
metaclassClass := primitiveNewClassNamed: 'Metaclass'.

primitiveSetClass: behaviorClass superclass: objectClass.
primitiveSetClass: classClass superclass: behaviorClass.
primitiveSetClass: metaclassClass superclass: behaviorClass.
```

#### 6. Link Class and Metaclass
Set up the metaclass relationships.
```smalltalk
primitiveSetClass: objectClass class: objectMetaclass.
primitiveSetClass: behaviorClass class: behaviorMetaclass.
"... etc for all classes"

"All metaclasses are instances of Metaclass"
primitiveSetClass: objectMetaclass class: metaclassClass.
primitiveSetClass: behaviorMetaclass class: metaclassClass.
"... etc"
```

## Field Layout

### Behavior (3 fields)
```
[0] superclass
[1] methodDict
[2] format
```

### ClassDescription extends Behavior (+2 fields = 5 total)
```
[3] instanceVariables
[4] organization
```

### Class extends ClassDescription (+3 fields = 8 total)
```
[5] name
[6] classPool      <- Dictionary for class variables
[7] sharedPools
```

### Metaclass extends ClassDescription (+1 field = 6 total)
```
[5] thisClass      <- Points to the class it's a metaclass for
```

## Critical Relationships

### Class Hierarchy
```
Object (root - no superclass)
  ├─ Symbol
  ├─ Array
  ├─ Dictionary
  ├─ CompiledMethod
  ├─ UndefinedObject
  └─ Behavior
       ├─ Class
       └─ Metaclass
```

### Metaclass Hierarchy (parallel)
```
Class (Object class inherits from Class!)
  └─ Object class
       ├─ Symbol class
       ├─ Array class
       ├─ Dictionary class
       ├─ CompiledMethod class
       ├─ UndefinedObject class
       └─ Behavior class
            ├─ Class class
            └─ Metaclass class
```

### Instance Relationships
- Every class is an instance of its metaclass
- Every metaclass is an instance of Metaclass
- Metaclass class is an instance of Metaclass (circular!)
- nil is an instance of UndefinedObject

## Phase 3: Install Essential Methods

After the 9 classes exist, we need to install key methods to enable self-hosting:

### Required Methods for Self-Hosting

1. **Behavior>>compile:** - To add methods to classes
2. **Class>>subclass:instanceVariableNames:...** - To create new classes
3. **Symbol>>asSymbol** - For method/variable naming
4. **Dictionary>>at:put:** - For storing methods
5. **Object>>class** - To get an object's class

## Phase 4: Transition to Self-Hosting

Once these methods are installed, Smalltalk can:
1. Create new classes: `Object subclass: #Point ...`
2. Add methods: `Point compile: 'x ^x'`
3. Create instances: `Point new`

From this point, all remaining classes can be defined in Smalltalk itself!

## Implementation Notes

### Primitive Requirements

The bootstrap requires only these VM primitives:
- **Primitive 70**: `new` - Create object with no fields
- **Primitive 71**: `new:` - Create object with N fields
- **Primitive 60**: `at:` - Get field at index
- **Primitive 61**: `at:put:` - Set field at index
- **Class pointer setting** - Internal VM operation

### Dictionary Implementation

For bootstrap, a minimal Dictionary can use linear search:
```
Dictionary (2 fields):
  [0] tally - count of entries
  [1] array - Array of Associations

Association (2 fields):
  [0] key - Symbol
  [1] value - any object
```

### Memory Footprint

Minimal bootstrap creates only:
- 9 class objects (8 fields each) = 72 fields
- 9 metaclass objects (6 fields each) = 54 fields
- Method dictionaries (2 fields each × 18) = 36 fields
- Class pools (2 fields each × 9) = 18 fields
- Total: ~180 object fields + arrays

## Benefits

1. **Minimal**: Only 9 classes vs. 30+ in full bootstrap
2. **Clean**: No unnecessary dependencies
3. **Fast**: Minimal memory allocation
4. **Verifiable**: Easy to validate correctness
5. **Foundation**: Everything else built in Smalltalk

## Testing

After bootstrap, verify:
```smalltalk
"Can create new class"
Object subclass: #TestClass
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'.

"Can add methods"
TestClass compile: 'initialize
    x := 0.
    y := 0'.

"Can create instances"
TestClass new.
```

If this works, the bootstrap is successful and Smalltalk is self-hosting!