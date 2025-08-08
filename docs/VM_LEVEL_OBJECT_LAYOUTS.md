# VM-Level Object Layouts

## Object Header Structure

Each object in the Smalltalk VM includes:
- **Header**: GC info, class pointer, format
- **Slots**: Indexed by offset (0-based internally, 1-based in Smalltalk)
- **Tagged pointers**: Symbols, SmallIntegers, etc.

## 🧠 VM-Level Object Layouts

### 🔹 Metaclass (Self-referential)
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Metaclass (self) ⚠️
  GC Flags: 0x00

[Slots]
  0 → superclass        = Class
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #('thisClass')
  4 → organization      = nil
  5 → thisClass         = nil (varies per metaclass instance)
```

### 🔹 Class
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Metaclass
  GC Flags: 0x00

[Slots]
  0 → superclass        = Behavior
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #('name' 'classPool' 'sharedPools')
  4 → organization      = nil
  5 → name              = Symbol #'Class'
  6 → classPool         = Dictionary {} ⚠️ CRITICAL
  7 → sharedPools       = Array #()
```

### 🔹 Behavior
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Class
  GC Flags: 0x00

[Slots]
  0 → superclass        = ProtoObject
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #('superclass' 'methodDict' 'format')
  4 → organization      = nil
  5 → name              = Symbol #'Behavior'
  6 → classPool         = Dictionary {}
  7 → sharedPools       = Array #()
```

### 🔹 ProtoObject (Root)
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Class
  GC Flags: 0x00

[Slots]
  0 → superclass        = nil ⚠️ ROOT
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #()
  4 → organization      = nil
  5 → name              = Symbol #'ProtoObject'
  6 → classPool         = Dictionary {}
  7 → sharedPools       = Array #()
```

### 🔹 Object
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Class
  GC Flags: 0x00

[Slots]
  0 → superclass        = ProtoObject
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #()
  4 → organization      = nil
  5 → name              = Symbol #'Object'
  6 → classPool         = Dictionary {'DependentsFields' → nil}
  7 → sharedPools       = Array #()
```

### 🔹 Symbol
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Class
  GC Flags: 0x00

[Slots]
  0 → superclass        = Object
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #()
  4 → organization      = nil
  5 → name              = Symbol #'Symbol'
  6 → classPool         = Dictionary {'SymbolTable' → Array}
  7 → sharedPools       = Array #()
```

### 🔹 Dictionary
```
[Header]
  Format: Class (8 slots)
  Class Pointer: → Class
  GC Flags: 0x00

[Slots]
  0 → superclass        = Object
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #('tally' 'array')
  4 → organization      = nil
  5 → name              = Symbol #'Dictionary'
  6 → classPool         = Dictionary {}
  7 → sharedPools       = Array #()
```

### 🔹 Smalltalk (Global Dictionary Instance)
```
[Header]
  Format: Dictionary (variable)
  Class Pointer: → Dictionary
  GC Flags: 0x00

[Instance Variables]
  0 → tally = 8
  1 → array = Array [
        Association(#'ProtoObject' → ProtoObject),
        Association(#'Object'      → Object),
        Association(#'Class'       → Class),
        Association(#'Metaclass'   → Metaclass),
        Association(#'Behavior'    → Behavior),
        Association(#'Symbol'      → Symbol),
        Association(#'Dictionary'  → Dictionary),
        Association(#'Smalltalk'   → Smalltalk)
      ]
```

## 🧠 Class/Metaclass Lattice: ProtoObject

### 🔹 ProtoObject (Class Object)
```
[Header]
  Format: Class
  Class Pointer → ProtoObject class (instance of Metaclass)
  GC Flags: 0x00

[Slots]
  0 → superclass        = nil
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #()
  4 → organization      = nil
  5 → name              = Symbol #'ProtoObject'
  6 → classPool         = Dictionary {}
  7 → sharedPools       = Array #()
```

### 🔹 ProtoObject class (Metaclass Instance)
```
[Header]
  Format: Metaclass
  Class Pointer → Metaclass
  GC Flags: 0x00

[Slots]
  0 → superclass        = Class ⚠️ Loop back
  1 → methodDict        = Dictionary {}
  2 → format            = 0
  3 → instanceVariables = Array #()
  4 → organization      = nil
  5 → thisClass         = ProtoObject
```

## 🔁 Pointer Relationships Summary

1. **ProtoObject class** is an instance of **Metaclass**
2. **ProtoObject** is an instance of **ProtoObject class**
3. **ProtoObject class**'s superclass is **Class**
4. **Class**'s superclass is **Behavior**
5. **Behavior**'s superclass is **ProtoObject** (creates the loop!)
6. **Metaclass** is an instance of itself (self-referential)

This loop ensures that the system is self-describing and can define new classes via message sends like `subclass:instanceVariableNames:`.

## Canonical Smalltalk Definitions

### Misleading Standard Form
```smalltalk
Object subclass: #ProtoObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'
```

### Correct Primitive-Level Form
```smalltalk
Class new
    name: #ProtoObject;
    superclass: nil;
    instanceVariableNames: '';
    classVariableNames: '';
    poolDictionaries: '';
    category: 'Kernel-Objects'.
```

The first form is misleading because ProtoObject is NOT a subclass of Object; it's the reverse. The second form assumes `Class new` is available, which only happens after the bootstrap.

## Memory Address Example

Here's a hypothetical memory layout with addresses:

```
0x1000: Metaclass      [class→0x1000, super→0x1100, ...]  // Self-ref!
0x1100: Class          [class→0x1000, super→0x1200, ...]
0x1200: Behavior       [class→0x1100, super→0x1300, ...]
0x1300: ProtoObject    [class→0x1100, super→nil, ...]     // Root!
0x1400: Object         [class→0x1100, super→0x1300, ...]
0x1500: Symbol         [class→0x1100, super→0x1400, ...]
0x1600: Dictionary     [class→0x1100, super→0x1400, ...]
0x1700: Smalltalk      [class→0x1600, tally→8, array→...] // Instance

// Metaclasses
0x2000: ProtoObject class [class→0x1000, super→0x1100, thisClass→0x1300]
0x2100: Object class      [class→0x1000, super→0x2000, thisClass→0x1400]
0x2200: Class class       [class→0x1000, super→0x2200, thisClass→0x1100]
0x2300: Metaclass class   [class→0x1000, super→0x2200, thisClass→0x1000]
```

## Critical Observations

1. **Slot 6 is classPool**: This is where class variables are stored as a Dictionary
2. **Metaclass self-reference**: Metaclass's class pointer points to itself
3. **ProtoObject is root**: Has nil superclass, making it the true root
4. **ProtoObject class → Class**: Creates the essential loop in the metaclass hierarchy
5. **All classes have 8 slots**: Consistent structure for all class objects
6. **Dictionary instances have 2 slots**: tally and array for the hash table

This layout ensures that class variables work correctly (via slot 6) and the metaclass system is fully self-describing.