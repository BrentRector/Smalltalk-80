# Bootstrap Class Creation Process

## Overview
This document describes how classes are created during the Smalltalk-80 bootstrap process and how this will transition to bytecode-based creation post-bootstrap.

## Objects Created for Each New Class

When bootstrapping a class like Dictionary, the following objects are created and configured:

### 1. The Class Object (Dictionary)
- Created via `vm.PrimitiveNewClass()`
- This is an instance of its metaclass (DictionaryClass)
- **Slots configured:**
  - Slot 0: Superclass pointer (e.g., Set for Dictionary)
  - Slot 1: Method dictionary (for instance methods)
  - Slot 2: Format field (instance specification)
  - Slot 3: Instance variables array
  - Slot 5: Class name symbol (#Dictionary)

### 2. The Metaclass Object (DictionaryClass)
- Created via `vm.PrimitiveNewMetaclass()`
- This is an instance of Metaclass
- **Slots configured:**
  - Slot 0: Superclass pointer (e.g., SetClass for DictionaryClass)
  - Slot 1: Method dictionary (for class methods)
  - Slot 5: The class it's a metaclass for (Dictionary)

### 3. Method Dictionaries (2 instances)
- One for instance methods (stored in Dictionary's slot 1)
- One for class methods (stored in DictionaryClass's slot 1)
- Created as variable-sized arrays with class set to MethodDictionary
- **Structure:** `[tally, key1, value1, key2, value2, ...]`
- Pre-sized with 1.5x capacity for hash table efficiency

### 4. CompiledMethod Objects (multiple)
- One for each method being installed
- **Slots configured:**
  - Slot 0: Selector symbol
  - Slot 1: Bytecode array
  - Slot 2: Literals array
  - Slot 3: Header (encodes primitive number, temp count)

### 5. Supporting Arrays
- Instance variables array (if class has instance vars)
- Literals arrays for each method
- Bytecode arrays for each method

## Low-Level Slot Operations Currently Used

The bootstrap currently performs these direct slot manipulations:

### Class Object Setup
- `vm.PrimitiveSetSlot(classOop, 0, superclassOop)` - Set superclass
- `vm.PrimitiveSetSlot(classOop, 1, methodDictOop)` - Set method dictionary
- `vm.PrimitiveSetSlot(classOop, 2, formatSpec)` - Set format
- `vm.PrimitiveSetSlot(classOop, 3, instVarsArray)` - Set instance variables
- `vm.PrimitiveSetSlot(classOop, 5, nameSymbol)` - Set class name

### Metaclass Setup
- `vm.PrimitiveSetClass(classOop, metaclassOop)` - Link class to its metaclass
- `vm.PrimitiveSetClass(metaclassOop, MetaclassClassOop)` - Set metaclass's class
- `vm.PrimitiveSetSlot(metaclassOop, 0, superMetaclassOop)` - Set metaclass superclass
- `vm.PrimitiveSetSlot(metaclassOop, 5, classOop)` - Link metaclass to its class

### Method Dictionary Operations
- `vm.PrimitiveArrayAtPut(dictOop, 0, tallyOop)` - Initialize tally
- `vm.PrimitiveDictionaryAtPut(dictOop, selectorOop, methodOop)` - Install methods

### Global Registration
- `vm.PrimitiveRegisterGlobal(className, classOop)` - Register in VM globals
- `vm.PrimitiveDictionaryAtPut(SmalltalkOop, symbolOop, classOop)` - Add to Smalltalk dictionary

## Mapping to Post-Bootstrap Selector Calls

Since the bootstrap process installs all setter methods on the core classes, these methods are available post-bootstrap for creating new classes via bytecode execution.

### Object Creation
```
CURRENT: vm.PrimitiveNewClass()
FUTURE:  Class new  (or Class basicNew)

CURRENT: vm.PrimitiveNewMetaclass()  
FUTURE:  Metaclass new  (or Metaclass basicNew)

CURRENT: vm.PrimitiveNewArray(size)
FUTURE:  Array new: size  (or Array basicNew: size)

CURRENT: vm.PrimitiveNewByteArray(size)
FUTURE:  ByteArray new: size  (or ByteArray basicNew: size)

CURRENT: vm.PrimitiveNewDictionary()
FUTURE:  Dictionary new

CURRENT: vm.PrimitiveNewInstance(compiledMethodClassOop)
FUTURE:  CompiledMethod new  (or CompiledMethod basicNew)

CURRENT: vm.PrimitiveInternSymbol(string)
FUTURE:  Symbol intern: string  (or string asSymbol)

CURRENT: vm.PrimitiveNewSmallInteger(value)
FUTURE:  (SmallIntegers are immediate values, created via literals)
```

### Method Dictionary Creation
```
CURRENT: methodDictOop = vm.PrimitiveNewArray(dictSize)
         vm.PrimitiveSetClass(methodDictOop, methodDictClassOop)
FUTURE:  MethodDictionary new: dictSize
```

### Class Object Configuration
```
CURRENT: vm.PrimitiveSetSlot(classOop, 0, superclassOop)
FUTURE:  classObject superclass: superclassObject

CURRENT: vm.PrimitiveSetSlot(classOop, 1, methodDictOop)  
FUTURE:  classObject methodDictionary: methodDictObject

CURRENT: vm.PrimitiveSetSlot(classOop, 2, formatSpec)
FUTURE:  classObject format: formatInteger

CURRENT: vm.PrimitiveSetSlot(classOop, 3, instVarsArray)
FUTURE:  classObject instanceVariables: varsArray

CURRENT: vm.PrimitiveSetSlot(classOop, 5, nameSymbol)
FUTURE:  classObject name: nameSymbol
```

### Metaclass Configuration
```
CURRENT: vm.PrimitiveSetClass(classOop, metaclassOop)
FUTURE:  classObject class: metaclassObject

CURRENT: vm.PrimitiveSetClass(metaclassOop, MetaclassClassOop)  
FUTURE:  metaclassObject class: Metaclass

CURRENT: vm.PrimitiveSetSlot(metaclassOop, 0, superMetaclassOop)
FUTURE:  metaclassObject superclass: superMetaclassObject

CURRENT: vm.PrimitiveSetSlot(metaclassOop, 5, classOop)
FUTURE:  metaclassObject thisClass: classObject
```

### CompiledMethod Creation
```
CURRENT: var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop)
         vm.PrimitiveSetSlot(methodOop, 0, selectorOop)
         vm.PrimitiveSetSlot(methodOop, 1, bytecodeOop)
         vm.PrimitiveSetSlot(methodOop, 2, literalsOop)
         vm.PrimitiveSetSlot(methodOop, 3, headerOop)
FUTURE:  method := CompiledMethod new.
         method selector: selectorSymbol.
         method bytecode: bytecodeArray.
         method literals: literalsArray.
         method header: headerInteger
```

### Method Installation
```
CURRENT: vm.PrimitiveArrayAtPut(methodDictOop, index, methodOop)
FUTURE:  methodDictionary at: index put: compiledMethod

CURRENT: vm.PrimitiveDictionaryAtPut(dictOop, selectorOop, methodOop)
FUTURE:  methodDictionary at: selectorSymbol put: compiledMethod
```

### Global Registration
```
CURRENT: vm.PrimitiveRegisterGlobal(className, classOop)
FUTURE:  (This is VM-internal, might remain primitive)

CURRENT: vm.PrimitiveDictionaryAtPut(SmalltalkOop, symbolOop, classOop)
FUTURE:  Smalltalk at: #ClassName put: classObject
```

## Complete Ordered Sequence for Post-Bootstrap Class Creation

Here's the complete ordered sequence of selector calls to create a new class (using Dictionary as an example) equivalent to the bootstrap process:

### Step 1: Create the Metaclass
```smalltalk
metaclass := Metaclass new.
```

### Step 2: Create the Class
```smalltalk
class := Class new.
```

### Step 3: Link Class and Metaclass
```smalltalk
class class: metaclass.
metaclass class: Metaclass.
metaclass thisClass: class.
```

### Step 4: Set Class Name
```smalltalk
nameSymbol := Symbol intern: 'Dictionary'.
class name: nameSymbol.
```

### Step 5: Set Superclass Relationships
```smalltalk
"Assuming Set is the superclass of Dictionary"
superclass := Smalltalk at: #Set.
superMetaclass := superclass class.

class superclass: superclass.
metaclass superclass: superMetaclass.
```

### Step 6: Set Instance Variables
```smalltalk
"Dictionary has 'tally' instance variable"
instVarArray := Array new: 1.
instVarSymbol := Symbol intern: 'tally'.
instVarArray at: 0 put: instVarSymbol.
class instanceVariables: instVarArray.
```

### Step 7: Set Format (for variable-sized classes)
```smalltalk
"Dictionary is variable-sized (words indexable)"
class format: 2.  "2 = words indexable"
```

### Step 8: Create Method Dictionaries
```smalltalk
"Instance method dictionary"
instanceMethodDict := MethodDictionary new: 50.  "Pre-sized for expected methods"
class methodDictionary: instanceMethodDict.

"Class method dictionary"  
classMethodDict := MethodDictionary new: 20.
metaclass methodDictionary: classMethodDict.
```

### Step 9: Create and Install Instance Methods
```smalltalk
"For each instance method (e.g., at:put:)"
method := CompiledMethod new.
selectorSymbol := Symbol intern: 'at:put:'.
method selector: selectorSymbol.

"Create bytecode array"
bytecodeArray := ByteArray new: 10.
"... populate bytecode bytes ..."
method bytecode: bytecodeArray.

"Create literals array"
literalsArray := Array new: 2.
"... populate literals ..."
method literals: literalsArray.

"Set method header (primitive, temps, etc.)"
headerValue := 123.  "Encoded header value"
method header: headerValue.

"Install in method dictionary"
instanceMethodDict at: selectorSymbol put: method.
```

### Step 10: Create and Install Class Methods
```smalltalk
"For each class method (e.g., new)"
classMethod := CompiledMethod new.
classSelectorSymbol := Symbol intern: 'new'.
classMethod selector: classSelectorSymbol.

"Create bytecode array"
classBytecodeArray := ByteArray new: 5.
"... populate bytecode bytes ..."
classMethod bytecode: classBytecodeArray.

"Create literals array"
classLiteralsArray := Array new: 1.
"... populate literals ..."
classMethod literals: classLiteralsArray.

"Set method header"
classHeaderValue := 456.  "Encoded header value"
classMethod header: classHeaderValue.

"Install in metaclass method dictionary"
classMethodDict at: classSelectorSymbol put: classMethod.
```

### Step 11: Register in Global Namespace
```smalltalk
Smalltalk at: #Dictionary put: class.
```

### Step 12: Initialize Method Dictionary Tally (if needed)
```smalltalk
"Set the tally for method dictionaries"
tallyValue := 0.  "SmallInteger"
instanceMethodDict at: 0 put: tallyValue.
classMethodDict at: 0 put: tallyValue.
```

## Summary of Object Creation Order

1. **Metaclass** object (instance of Metaclass)
2. **Class** object (instance of the metaclass)
3. **Symbol** objects for names and selectors
4. **Array** objects for instance variables and literals
5. **MethodDictionary** objects (2 - one for instance, one for class)
6. **CompiledMethod** objects (one per method)
7. **ByteArray** objects for bytecode (one per method)
8. **SmallInteger** values for headers and tallies

This sequence ensures all objects exist before they're referenced and all methods are available through the normal message sending mechanism post-bootstrap.

## Key Insights

1. **All setter methods are installed during bootstrap** - Methods like `superclass:`, `methodDictionary:`, `name:`, etc. are installed on Behavior, Class, and Metaclass during Phase 2, making them available for post-bootstrap use.

2. **Bootstrap dependency resolved** - Since the bootstrap installs these methods, there's no circular dependency problem when transitioning to bytecode-based class creation.

3. **Order matters** - The sequence ensures objects exist before being referenced and maintains proper class/metaclass relationships.

4. **Format specification** - Variable-sized classes (Arrays, Dictionaries) need their format field set to indicate they're indexable.

5. **Method dictionary pre-sizing** - Dictionaries are created with 1.5x expected capacity for efficient hashing.