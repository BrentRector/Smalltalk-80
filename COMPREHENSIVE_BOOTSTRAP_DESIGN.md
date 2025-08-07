# Comprehensive Smalltalk-80 Bootstrap Design

## Executive Summary

This document describes the precise implementation of a minimal Smalltalk-80 bootstrap that creates a self-hosting system using only VM primitives. The bootstrap creates exactly 9 essential classes, establishes the metaclass loop, and installs the minimal methods required for Smalltalk to define its own classes via message sends.

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [The 9 Essential Classes](#the-9-essential-classes)
3. [Slot Structure and Memory Layout](#slot-structure-and-memory-layout)
4. [The Metaclass Loop](#the-metaclass-loop)
5. [Bootstrap Phases](#bootstrap-phases)
6. [Primitive Operations](#primitive-operations)
7. [Transition to Self-Hosting](#transition-to-self-hosting)
8. [Implementation Details](#implementation-details)
9. [Testing and Verification](#testing-and-verification)

## Core Concepts

### What is Bootstrapping?

Bootstrapping is the process of creating the minimal set of classes and methods required for Smalltalk to become self-hosting - able to define new classes and methods using its own message-sending mechanism rather than VM primitives.

### Why These Specific Classes?

The 9 essential classes form the absolute minimum required for:
- **Object model**: Object, Class, Metaclass, Behavior
- **Method storage**: CompiledMethod, Dictionary
- **Naming**: Symbol
- **Collections**: Array
- **Special values**: UndefinedObject (nil)

## The 9 Essential Classes

### 🧱 Class Overview Table

| Class | Purpose | Slot Count | Slot Names | Notes |
|-------|---------|------------|------------|-------|
| **Object** | Root of all classes | 0 | (none initially) | May gain slots via subclassing |
| **Behavior** | Base for Class/Metaclass | 5 | superclass, methodDict, format, instanceVariables, organization | Defines class structure |
| **Class** | Creates class instances | 8 | (inherits 5) + name, classPool, sharedPools | Adds class-specific fields |
| **Metaclass** | Class of classes | 6 | (inherits 5) + thisClass | Points to its sole instance |
| **Symbol** | Interned strings | Variable | characters (string data) | Used for selectors, names |
| **Dictionary** | Key-value storage | 2 | tally, array | Holds methods, variables |
| **Array** | Indexed collection | Variable | (indexed elements) | Basic collection |
| **CompiledMethod** | Executable code | Variable | header, literals, bytecodes | Contains compiled code |
| **UndefinedObject** | Singleton nil | 0 | (none) | Only instance is nil |

## Slot Structure and Memory Layout

### Behavior Hierarchy Slot Layout

```
Behavior (5 slots):
  [0] superclass       - Reference to superclass or nil
  [1] methodDict       - Dictionary of Symbol -> CompiledMethod
  [2] format          - SmallInteger encoding instance layout
  [3] instanceVariables - Array of Symbol (instance variable names)
  [4] organization     - ClassOrganizer (categories for methods)

Class extends Behavior (8 slots total):
  [0-4] (inherited from Behavior)
  [5] name            - Symbol (class name)
  [6] classPool       - Dictionary (class variables)
  [7] sharedPools     - Array of Dictionary (shared pools)

Metaclass extends Behavior (6 slots total):
  [0-4] (inherited from Behavior)
  [5] thisClass       - Reference to the class it's a metaclass for
```

### Collection Classes Layout

```
Dictionary (2 slots):
  [0] tally           - SmallInteger (number of entries)
  [1] array           - Array of Association or nil

Association (2 slots):
  [0] key             - Any object (usually Symbol)
  [1] value           - Any object

Symbol (variable size):
  [0..n-1]            - Character data (interned string)

Array (variable size):
  [0..n-1]            - Indexed elements

CompiledMethod (variable size):
  [header]            - Method header (encoded as SmallInteger)
  [literals]          - Array of literal objects
  [bytecodes]         - Bytes of compiled code
```

## The Metaclass Loop

### 🔁 Visual Diagram

```
                    ┌─────────────────┐
                    │   Metaclass     │
                    ├─────────────────┤
                    │ superclass: Behavior
                    │ thisClass: (varies)
                    └─────────────────┘
                            ▲
                            │ class
                    ┌───────┴────────┐
                    │ Metaclass class│ ← instance of Metaclass (!)
                    └────────────────┘
                            ▲
                            │ thisClass
                    ┌─────────────────┐
                    │     Class       │
                    ├─────────────────┤
                    │ superclass: Behavior
                    └─────────────────┘
                            ▲
                            │ class
                    ┌───────┴────────┐
                    │  Class class   │ ← instance of Metaclass
                    └────────────────┘
                            
                    ┌─────────────────┐
                    │    Behavior     │
                    ├─────────────────┤
                    │ superclass: Object
                    └─────────────────┘
                            ▲
                            │ superclass
                    ┌─────────────────┐
                    │     Object      │
                    ├─────────────────┤
                    │ superclass: nil │
                    └─────────────────┘
                            ▲
                            │ class
                    ┌───────┴────────┐
                    │  Object class  │ ← instance of Metaclass
                    └────────────────┘
```

### 🧠 Key Relationships Explained

1. **Every object has a class**: Accessed via the `class` pointer
2. **Every class is an object**: Classes are instances of their metaclass
3. **Every metaclass is a class**: Metaclasses are instances of Metaclass
4. **Metaclass is its own instance**: Metaclass class is an instance of Metaclass (circular!)
5. **Object is the root**: All classes ultimately inherit from Object
6. **Class defines class behavior**: Methods for creating instances, subclasses
7. **Metaclass defines metaclass behavior**: Methods for class-side behavior

### The Critical Loop

```
Object class → is instance of → Metaclass
Class class → is instance of → Metaclass  
Metaclass class → is instance of → Metaclass (itself!)
```

This circularity is what makes the system self-describing and self-hosting.

## Bootstrap Phases

### Phase 0: VM Primitives Only

At this stage, we have only VM-level operations:
- Memory allocation
- Field access (get/set)
- Class pointer manipulation

### Phase 1: Create Core Objects

```smalltalk
"Step 1: Create nil"
nil := primitiveAllocate: 0.

"Step 2: Create essential class objects (as raw memory)"
objectClass := primitiveAllocate: 8.       "Class has 8 slots"
behaviorClass := primitiveAllocate: 8.
classClass := primitiveAllocate: 8.
metaclassClass := primitiveAllocate: 8.
symbolClass := primitiveAllocate: 8.
dictionaryClass := primitiveAllocate: 8.
arrayClass := primitiveAllocate: 8.
compiledMethodClass := primitiveAllocate: 8.
undefinedObjectClass := primitiveAllocate: 8.
```

### Phase 2: Create Metaclasses

```smalltalk
"Step 3: Create metaclass objects"
objectMetaclass := primitiveAllocate: 6.      "Metaclass has 6 slots"
behaviorMetaclass := primitiveAllocate: 6.
classMetaclass := primitiveAllocate: 6.
metaclassMetaclass := primitiveAllocate: 6.
"... etc for all classes"

"Step 4: Set class pointers"
primitiveSetClass: objectClass to: objectMetaclass.
primitiveSetClass: behaviorClass to: behaviorMetaclass.
primitiveSetClass: classClass to: classMetaclass.
primitiveSetClass: metaclassClass to: metaclassMetaclass.

"Step 5: All metaclasses are instances of Metaclass"
primitiveSetClass: objectMetaclass to: metaclassClass.
primitiveSetClass: behaviorMetaclass to: metaclassClass.
primitiveSetClass: classMetaclass to: metaclassClass.
primitiveSetClass: metaclassMetaclass to: metaclassClass.  "Circular!"
```

### Phase 3: Initialize Slots

```smalltalk
"Step 6: Set superclass hierarchy"
primitiveSetSlot: objectClass at: 0 to: nil.              "Object has no superclass"
primitiveSetSlot: behaviorClass at: 0 to: objectClass.    "Behavior → Object"
primitiveSetSlot: classClass at: 0 to: behaviorClass.     "Class → Behavior"
primitiveSetSlot: metaclassClass at: 0 to: behaviorClass. "Metaclass → Behavior"

"Step 7: Set metaclass superclass hierarchy (parallel)"
primitiveSetSlot: objectMetaclass at: 0 to: classClass.           "Object class → Class"
primitiveSetSlot: behaviorMetaclass at: 0 to: objectMetaclass.    "Behavior class → Object class"
primitiveSetSlot: classMetaclass at: 0 to: behaviorMetaclass.     "Class class → Behavior class"
primitiveSetSlot: metaclassMetaclass at: 0 to: behaviorMetaclass. "Metaclass class → Behavior class"

"Step 8: Set thisClass for metaclasses"
primitiveSetSlot: objectMetaclass at: 5 to: objectClass.
primitiveSetSlot: behaviorMetaclass at: 5 to: behaviorClass.
primitiveSetSlot: classMetaclass at: 5 to: classClass.
primitiveSetSlot: metaclassMetaclass at: 5 to: metaclassClass.

"Step 9: Create and set method dictionaries"
objectMethodDict := primitiveNewDictionary.
primitiveSetSlot: objectClass at: 1 to: objectMethodDict.
"... etc for all classes"

"Step 10: Create and set classPools"
objectClassPool := primitiveNewDictionary.
primitiveSetSlot: objectClass at: 6 to: objectClassPool.
"... etc for all classes"

"Step 11: Set names"
primitiveSetSlot: objectClass at: 5 to: (internSymbol: 'Object').
primitiveSetSlot: behaviorClass at: 5 to: (internSymbol: 'Behavior').
"... etc"
```

### Phase 4: Install Essential Methods

```smalltalk
"Critical methods for self-hosting"

"Object class>>subclass:instanceVariableNames:..."
method := compileMethod: 'subclass: name instanceVariableNames: ivs 
    classVariableNames: cvs poolDictionaries: pools category: cat
    "Create a new subclass"
    <primitive: 254>'.
installMethod: method in: objectMetaclass selector: #subclass:instanceVariableNames:...

"Behavior>>compile:"
method := compileMethod: 'compile: sourceString
    "Compile and install a method"
    <primitive: 255>'.
installMethod: method in: behaviorClass selector: #compile:.

"Behavior>>new"
method := compileMethod: 'new
    "Create an instance"
    <primitive: 70>'.
installMethod: method in: behaviorClass selector: #new.

"Dictionary>>at:put:"
method := compileMethod: 'at: key put: value
    "Store a key-value pair"
    <primitive: 251>'.
installMethod: method in: dictionaryClass selector: #at:put:.
```

### Phase 5: Create Global Smalltalk Dictionary

```smalltalk
"Create SystemDictionary (subclass of Dictionary)"
systemDictClass := Class new: 8.
primitiveSetSlot: systemDictClass at: 0 to: dictionaryClass.
primitiveSetSlot: systemDictClass at: 5 to: (internSymbol: 'SystemDictionary').

"Create the Smalltalk global"
Smalltalk := systemDictClass new.

"Add all classes"
Smalltalk at: #Object put: objectClass.
Smalltalk at: #Class put: classClass.
Smalltalk at: #Metaclass put: metaclassClass.
"... etc"

"Add special objects"
Smalltalk at: #nil put: nil.
Smalltalk at: #true put: true.
Smalltalk at: #false put: false.
Smalltalk at: #Smalltalk put: Smalltalk.  "Self-reference!"
```

## Primitive Operations

### Required VM Primitives

| Primitive | Name | Description | Example |
|-----------|------|-------------|---------|
| 70 | new | Allocate object with default size | `Point new` |
| 71 | new: | Allocate object with N slots | `Array new: 10` |
| 60 | at: | Get field at index | `obj at: 1` |
| 61 | at:put: | Set field at index | `obj at: 1 put: value` |
| 111 | class | Return object's class | `obj class` |
| 254 | createSubclass | Create subclass (custom) | Used in `subclass:...` |
| 255 | compileMethod | Compile source (custom) | Used in `compile:` |
| 251 | dictAt:put: | Dictionary store (custom) | Used in `at:put:` |

### Internal VM Operations

```c
// Set class pointer
void setClass(Object* obj, Object* class) {
    obj->class = class;
}

// Allocate raw memory
Object* allocate(int slotCount) {
    Object* obj = malloc(sizeof(ObjectHeader) + slotCount * sizeof(Object*));
    obj->size = slotCount;
    for (int i = 0; i < slotCount; i++) {
        obj->slots[i] = nil;
    }
    return obj;
}

// Symbol interning
Object* internSymbol(const char* string) {
    // Check symbol table for existing
    // Create new if not found
    // Add to symbol table
    // Return symbol object
}
```

## Transition to Self-Hosting

### The Magic Moment

Once the bootstrap is complete, this becomes possible:

```smalltalk
Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Geometry'.
```

This works because:

1. **Message lookup works**: `Object` receives `subclass:...` message
2. **Method found**: In `Object class` (the metaclass)
3. **Method executes**: Creates new class using Smalltalk mechanisms
4. **Class registered**: Added to Smalltalk global dictionary
5. **Ready to use**: Can add methods, create instances

### Adding Methods

```smalltalk
Point compile: 'x
    ^x'.

Point compile: 'y
    ^y'.

Point compile: 'x: aNumber
    x := aNumber'.

Point compile: 'y: aNumber
    y := aNumber'.
```

### Creating Instances

```smalltalk
p := Point new.
p x: 10.
p y: 20.
p x.  "Returns 10"
```

## Implementation Details

### C# Implementation Structure

```csharp
public class SmalltalkObject {
    public SmalltalkClass ClassPointer { get; set; }
    public object?[] Fields { get; }
    
    public SmalltalkObject(SmalltalkClass? cls, int fieldCount) {
        ClassPointer = cls;
        Fields = new object?[fieldCount];
    }
}

public class SmalltalkClass : SmalltalkObject {
    // Convenience accessors for Behavior fields
    public SmalltalkClass? Superclass {
        get => Fields[0] as SmalltalkClass;
        set => Fields[0] = value;
    }
    
    public SmalltalkObject? MethodDictionary {
        get => Fields[1] as SmalltalkObject;
        set => Fields[1] = value;
    }
    
    // ... etc
}
```

### Bootstrap Sequence in C#

```csharp
public static class MinimalBootstrap {
    public static void Initialize() {
        // Phase 1: Allocate objects
        var nil = AllocateObject(0);
        var objectClass = AllocateObject(8);
        var behaviorClass = AllocateObject(8);
        // ... etc
        
        // Phase 2: Create metaclasses
        var objectMetaclass = AllocateObject(6);
        SetClassPointer(objectClass, objectMetaclass);
        SetClassPointer(objectMetaclass, metaclassClass);
        // ... etc
        
        // Phase 3: Initialize slots
        SetSlot(objectClass, SUPERCLASS, nil);
        SetSlot(objectClass, METHODDICT, CreateEmptyDictionary());
        SetSlot(objectClass, NAME, InternSymbol("Object"));
        // ... etc
        
        // Phase 4: Install methods
        InstallMethod(objectMetaclass, "subclass:...", CreateSubclassMethod());
        InstallMethod(behaviorClass, "compile:", CreateCompileMethod());
        // ... etc
        
        // Phase 5: Create Smalltalk global
        var smalltalk = CreateSystemDictionary();
        AddToDict(smalltalk, "Object", objectClass);
        // ... etc
    }
}
```

## Method Encoding

### 🧪 Encoding Smalltalk Methods

The bootstrap must be able to encode methods into CompiledMethod format. Let's walk through encoding a minimal method like `initialize`.

#### Target Method: initialize

```smalltalk
initialize
    ^self
```

This method:
- Has no arguments
- Returns the receiver (self)
- Is typically used to set up an object after creation

### 🧱 CompiledMethod Structure

A CompiledMethod object contains:

| Component | Description | Example |
|-----------|-------------|---------||
| **Header** | Encodes literals count, temps, stack size, primitive index | 16 or 32-bit word |
| **Literals Array** | Symbols, selectors, constants used in bytecode | `#(#initialize)` |
| **Bytecodes** | Encoded VM instructions | `#[112 124]` |

### Smalltalk-80 Bytecode Set

#### Common Bytecodes

| Bytecode | Hex | Meaning | Stack Effect |
|----------|-----|---------|-------------|
| 0-15 | 0x00-0x0F | Push receiver variable 0-15 | → value |
| 16-31 | 0x10-0x1F | Push temporary variable 0-15 | → value |
| 32-63 | 0x20-0x3F | Push literal constant 0-31 | → value |
| 64-95 | 0x40-0x5F | Push literal variable 0-31 | → value |
| 96-103 | 0x60-0x67 | Pop and store receiver variable 0-7 | value → |
| 104-111 | 0x68-0x6F | Pop and store temporary variable 0-7 | value → |
| 112 | 0x70 | Push receiver (self) | → self |
| 113 | 0x71 | Push true | → true |
| 114 | 0x72 | Push false | → false |
| 115 | 0x73 | Push nil | → nil |
| 116 | 0x74 | Push -1 | → -1 |
| 117 | 0x75 | Push 0 | → 0 |
| 118 | 0x76 | Push 1 | → 1 |
| 119 | 0x77 | Push 2 | → 2 |
| 120 | 0x78 | Return receiver (self) | |
| 121 | 0x79 | Return true | |
| 122 | 0x7A | Return false | |
| 123 | 0x7B | Return nil | |
| 124 | 0x7C | Return top of stack | top → |
| 125 | 0x7D | Return top from block | top → |
| 135 | 0x87 | Pop stack | top → |
| 136 | 0x88 | Duplicate top | top → top, top |
| 137 | 0x89 | Push active context | → context |

#### Send Bytecodes

| Bytecode | Hex | Meaning |
|----------|-----|---------|
| 176-191 | 0xB0-0xBF | Send arithmetic selector |
| 192-207 | 0xC0-0xCF | Send common selector |
| 208-223 | 0xD0-0xDF | Send literal selector 0-15 with 0 args |
| 224-239 | 0xE0-0xEF | Send literal selector 0-15 with 1 arg |
| 240-255 | 0xF0-0xFF | Send literal selector 0-15 with 2 args |

### Method Header Format

The method header is a SmallInteger encoding:

```
┌─────────────────────────────────────┐
│ Primitive │ Temps │ Large │ Literals │
│  Index    │ Count │ Context│ Count   │
├───────────┼───────┼────────┼─────────┤
│ 10 bits   │ 5 bits│ 1 bit  │ 6 bits  │
└─────────────────────────────────────┘
```

For our `initialize` method:
- 0 primitive index
- 0 temporaries
- 0 large context flag
- 0 literals (or 1 if we include #initialize)

### 🧩 Step-by-Step Encoding Example

#### 1. Create Literals Array

```smalltalk
"For initialize method, we might not need any literals"
literals := primitiveNewArrayOfSize: 0.

"Or if we want to include the selector:"
literals := primitiveNewArrayOfSize: 1.
literals at: 1 put: (primitiveInternSymbol: 'initialize').
```

#### 2. Create Bytecode Array

```smalltalk
"initialize just returns self"
bytecodes := #[112 124].  "Push self (112), Return top (124)"

"Alternative: use return self directly"
bytecodes := #[120].      "Return self (120)"
```

#### 3. Encode Header

```smalltalk
"Header encoding (implementation specific)"
"Format: | primitive(10) | temps(5) | large(1) | literals(6) |"
header := 0.              "No primitive, no temps, no literals"
```

#### 4. Create CompiledMethod

```smalltalk
compiledMethod := primitiveNew: compiledMethodClass size: 3.
primitiveSetSlot: compiledMethod slot: 0 value: header.
primitiveSetSlot: compiledMethod slot: 1 value: literals.
primitiveSetSlot: compiledMethod slot: 2 value: bytecodes.
```

#### 5. Install in Method Dictionary

```smalltalk
methodDict := primitiveGetSlot: objectClass slot: 1.  "Get methodDict"
initializeSymbol := primitiveInternSymbol: 'initialize'.
methodDict at: initializeSymbol put: compiledMethod.
```

### More Complex Method Examples

#### Accessor Method: x

```smalltalk
x
    ^x  "Return instance variable x"
```

Encoding:
```smalltalk
bytecodes := #[0 124].    "Push ivar 0, Return top"
"Or use shortcut:"
bytecodes := #[0 120].    "Push ivar 0, Return self"
```

#### Setter Method: x:

```smalltalk
x: aNumber
    x := aNumber
```

Encoding:
```smalltalk
bytecodes := #[16 96 120]. "Push temp 0, Store in ivar 0, Return self"
```

#### Method with Message Send

```smalltalk
add: aNumber
    ^self + aNumber
```

Encoding:
```smalltalk
literals := #(#+).         "Literal 0 is #+"
bytecodes := #[
    112      "Push self"
    16       "Push temp 0 (aNumber)"
    224      "Send literal 0 with 1 arg (#+)"
    124      "Return top"
].
```

### Method Compilation During Bootstrap

During bootstrap, we need to manually encode several essential methods:

#### Essential Bootstrap Methods

1. **Object>>class**
```smalltalk
class
    <primitive: 111>
    ^self primitiveFailed
```

2. **Behavior>>new**
```smalltalk
new
    <primitive: 70>
    ^self basicNew initialize
```

3. **Behavior>>compile:**
```smalltalk
compile: sourceString
    <primitive: 255>  "Custom compile primitive"
    ^self primitiveFailed
```

4. **Dictionary>>at:put:**
```smalltalk
at: key put: value
    <primitive: 251>  "Custom dictionary primitive"
    ^self primitiveFailed
```

### C# Implementation for Method Encoding

```csharp
public static class MethodEncoder {
    // Bytecode constants
    public const byte PushReceiver = 112;        // 0x70
    public const byte PushTrue = 113;            // 0x71
    public const byte PushFalse = 114;           // 0x72
    public const byte PushNil = 115;             // 0x73
    public const byte ReturnReceiver = 120;      // 0x78
    public const byte ReturnTrue = 121;          // 0x79
    public const byte ReturnFalse = 122;         // 0x7A
    public const byte ReturnNil = 123;           // 0x7B
    public const byte ReturnTop = 124;           // 0x7C
    
    public static CompiledMethod EncodeInitialize() {
        var header = new MethodHeader(0, 0);  // No args, no temps
        var literals = new object[0];         // No literals needed
        var bytecodes = new byte[] { ReturnReceiver };
        
        return new CompiledMethod(header, literals, bytecodes, "initialize");
    }
    
    public static CompiledMethod EncodeAccessor(int ivarIndex) {
        var header = new MethodHeader(0, 0);
        var literals = new object[0];
        var bytecodes = new byte[] {
            (byte)(0 + ivarIndex),  // Push instance variable
            ReturnTop               // Return it
        };
        
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    public static CompiledMethod EncodeSetter(int ivarIndex) {
        var header = new MethodHeader(1, 0);  // 1 argument
        var literals = new object[0];
        var bytecodes = new byte[] {
            16,                              // Push temp 0 (argument)
            (byte)(96 + ivarIndex),          // Pop and store in ivar
            ReturnReceiver                   // Return self
        };
        
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    public static CompiledMethod EncodePrimitive(int primitiveIndex) {
        var header = new MethodHeader(0, 0, true, primitiveIndex);
        var literals = new object[0];
        var bytecodes = new byte[] { ReturnReceiver };  // Fallback
        
        return new CompiledMethod(header, literals, bytecodes, null);
    }
}
```

### Method Dictionary Structure

During bootstrap, method dictionaries are created as:

```smalltalk
"Create empty method dictionary"
methodDict := Dictionary new.

"Add methods"
methodDict at: #initialize put: initializeMethod.
methodDict at: #class put: classMethod.
methodDict at: #new put: newMethod.

"Install in class"
primitiveSetSlot: behaviorClass slot: 1 value: methodDict.
```

### ✅ Verification

After encoding and installing methods:

```smalltalk
"Test that methods are found"
object := Object new.
object class.           "Should return Object"
object initialize.      "Should return object"

"Test method compilation"
Object compile: 'test ^42'.
Object new test.        "Should return 42"
```

## 🧠 Advanced Method Encoding

### Arguments and Temporaries

Methods with arguments and temporary variables require careful encoding of the method header and proper bytecode generation.

#### Method Header Format (Detailed)

The method header in Smalltalk-80 is typically a 16-bit or 32-bit word:

```
┌─────────────────────────────────────────────┐
│ Extended Header (32-bit)                    │
├──────────────┬──────────┬─────────┬─────────┤
│ Primitive    │ Args     │ Temps   │ Flags   │
│ Index        │ Count    │ Count   │ & Lits  │
├──────────────┼──────────┼─────────┼─────────┤
│ 10 bits      │ 5 bits   │ 5 bits  │ 12 bits │
└──────────────┴──────────┴─────────┴─────────┘

Simple Header (16-bit):
┌──────────┬──────────┬─────────┬──────────┐
│ Stack    │ Temps    │ Args    │ Primitive│
│ Depth    │ Count    │ Count   │ Flag     │
├──────────┼──────────┼─────────┼──────────┤
│ 6 bits   │ 5 bits   │ 4 bits  │ 1 bit    │
└──────────┴──────────┴─────────┴──────────┘
```

This allows:
- Up to 31 arguments
- Up to 31 temporaries
- Up to 63 stack depth
- Primitive index if needed

### Example: Method with Arguments and Temps

```smalltalk
myMethod: x with: y
    | temp result |
    temp := x + y.
    result := temp * 2.
    ^result
```

Encoding breakdown:
- **Arguments**: x, y → 2 arguments
- **Temporaries**: temp, result → 2 temporaries
- **Stack depth**: 2 (for binary operations)

#### Bytecode Generation

```smalltalk
"Variable indices:"
"  Arguments: x=0, y=1"
"  Temporaries: temp=2, result=3"

literals := #(#+ #*).

bytecodes := #[
    16      "Push arg 0 (x)"
    17      "Push arg 1 (y)"
    224     "Send #+ (literal 0, 1 arg)"
    104     "Pop and store temp 0 (temp)"
    
    18      "Push temp 0 (temp)"
    118     "Push constant 2"
    225     "Send #* (literal 1, 1 arg)"
    105     "Pop and store temp 1 (result)"
    
    19      "Push temp 1 (result)"
    124     "Return top"
].

header := (2 << 10) | (2 << 5) | (2 << 0).  "2 args, 2 temps, stack=2"
```

### 📦 Variable Access Patterns

#### Variable Types and Their Bytecodes

| Variable Type | Push Bytecode | Store Bytecode | Index Range |
|--------------|---------------|----------------|-------------|
| **Instance Variables** | 0-15 | 96-103 | 0-15 direct |
| **Arguments** | 16-31 | N/A (read-only) | 0-15 direct |
| **Temporaries** | 16-31 | 104-111 | 0-15 direct |
| **Literals** | 32-63 | N/A | 0-31 direct |
| **Literal Variables** | 64-95 | N/A | 0-31 direct |

For indices beyond direct range, extended bytecodes are used:
- **Extended Push**: 128 + next byte
- **Extended Store**: 129 + next byte
- **Extended Store and Pop**: 130 + next byte

### 🔁 Control Flow Encoding

Control flow in Smalltalk is implemented through message sends to Boolean objects and blocks.

#### ifTrue:ifFalse: Pattern

```smalltalk
testMethod: flag
    flag ifTrue: [
        self doSomething
    ] ifFalse: [
        self doSomethingElse
    ]
```

##### Optimized Bytecode (Jump-Based)

Many Smalltalk VMs optimize boolean messages into jumps:

```smalltalk
bytecodes := #[
    16          "Push arg 0 (flag)"
    152         "Jump on false to offset +5"
    5           "Jump offset"
    112         "Push self"
    208         "Send #doSomething (0 args)"
    164         "Jump to offset +4"
    4           "Jump offset"
    112         "Push self"
    209         "Send #doSomethingElse (0 args)"
    135         "Pop result"
    120         "Return self"
].
```

##### Special Jump Bytecodes

| Bytecode | Operation | Description |
|----------|-----------|-------------|
| 152-159 | Jump on false | Skip N bytes if false |
| 160-167 | Jump on true | Skip N bytes if true |
| 168-175 | Jump unconditional | Always skip N bytes |

#### whileTrue: Pattern

```smalltalk
whileExample
    | count |
    count := 0.
    [count < 10] whileTrue: [
        count := count + 1
    ]
```

Compiled with backward jump:

```smalltalk
bytecodes := #[
    117         "Push 0"
    104         "Store temp 0 (count)"
    
    "Loop start (byte 2)"
    18          "Push temp 0 (count)"
    33          "Push literal 10"
    178         "Send #< (special arithmetic)"
    152         "Jump on false +8"
    8
    
    18          "Push temp 0 (count)"
    118         "Push 1"
    176         "Send #+"
    104         "Store temp 0 (count)"
    163         "Jump backward -12"
    244         "Jump offset (256-12)"
    
    120         "Return self"
].
```

### 🧪 Block Encoding

Blocks are compiled as separate CompiledMethod objects and referenced in the enclosing method's literal frame.

#### Block Structure

```smalltalk
blockExample: x
    | block |
    block := [:y | x + y].
    ^block value: 10
```

This creates two methods:

1. **Main method**:
```smalltalk
literals := #(
    blockMethod  "CompiledMethod for block"
    #value:
).

bytecodes := #[
    137         "Push active context"
    118         "Push 1 (num block args)"
    200         "Send blockCopy:"
    32          "Push literal 0 (block method)"
    104         "Store temp 0 (block var)"
    
    18          "Push temp 0 (block)"
    34          "Push literal 10"
    225         "Send #value: (literal 1)"
    124         "Return top"
].
```

2. **Block method**:
```smalltalk
"Block has access to:"
"  Outer context arg: x (via context)"
"  Block arg: y (index 0)"

blockBytecodes := #[
    16          "Push block arg 0 (y)"
    140         "Push outer arg 0 (x) via context"
    176         "Send #+"
    125         "Return from block"
].
```

### 🧮 Literal Frame Organization

The literal frame stores all constants and references needed by the method:

```smalltalk
"Example literal frame for complex method"
literals := #(
    #selector1          "Index 0: Symbol"
    #selector2          "Index 1: Symbol"
    42                  "Index 2: SmallInteger"
    'a string'          "Index 3: String"
    Float               "Index 4: Class reference"
    blockMethod1        "Index 5: CompiledMethod for block"
    #(1 2 3)           "Index 6: Array literal"
    Association         "Index 7: Variable binding"
).
```

### 🧰 Complete Encoding Example

#### Source Method

```smalltalk
calculate: base withMultiplier: mult
    | temp result |
    temp := base * mult.
    result := temp > 100
        ifTrue: [temp / 2]
        ifFalse: [temp * 2].
    ^result rounded
```

#### Full Encoding

```smalltalk
"Header: 2 args, 2 temps, stack depth 3"
header := (2 << 10) | (2 << 5) | 3.

"Literals"
literals := #(
    #*              "0: multiply"
    #>              "1: greater than"
    100             "2: constant"
    #/              "3: divide"
    2               "4: constant"
    #rounded        "5: rounded"
    ifTrueBlock     "6: CompiledMethod for true block"
    ifFalseBlock    "7: CompiledMethod for false block"
).

"Main method bytecodes"
bytecodes := #[
    16          "Push arg 0 (base)"
    17          "Push arg 1 (mult)"
    224         "Send #* (literal 0, 1 arg)"
    104         "Store temp 0 (temp)"
    
    18          "Push temp 0 (temp)"
    34          "Push literal 2 (100)"
    225         "Send #> (literal 1, 1 arg)"
    
    "Optimized ifTrue:ifFalse:"
    153         "Jump on false +6"
    6
    18          "Push temp 0 (temp)"
    36          "Push literal 4 (2)"
    227         "Send #/ (literal 3, 1 arg)"
    164         "Jump +5"
    5
    18          "Push temp 0 (temp)"
    36          "Push literal 4 (2)"
    224         "Send #* (literal 0, 1 arg)"
    
    105         "Store temp 1 (result)"
    19          "Push temp 1 (result)"
    229         "Send #rounded (literal 5, 0 args)"
    124         "Return top"
].
```

### 📊 Summary Table

| Feature | Encoding | Max Value | Notes |
|---------|----------|-----------|-------|
| **Arguments** | Header bits 10-14 | 31 | Accessed via bytecodes 16-31 |
| **Temporaries** | Header bits 5-9 | 31 | Accessed via bytecodes 16-31 |
| **Stack Depth** | Header bits 0-4 | 31 | VM stack allocation |
| **Literals** | Separate array | 255 | Extended form for >31 |
| **Instance Vars** | Direct bytecodes | 255 | Extended form for >15 |
| **Blocks** | Literal frame | N/A | Compiled as methods |
| **Jumps** | Offset bytes | ±127 | Extended for longer |

### Method Dictionary Storage

During bootstrap, these encoded methods are stored:

```smalltalk
"Create method object"
method := CompiledMethod new.
method header: header.
method literals: literals.
method bytecodes: bytecodes.

"Store in class method dictionary"
classMethodDict := class methodDict.
classMethodDict at: #calculate:withMultiplier: put: method.
```

### Performance Optimizations

The VM typically optimizes:

1. **Special selectors**: Arithmetic (+, -, *, /), comparison (<, >, =, ~=)
2. **Boolean messages**: ifTrue:, ifFalse:, and:, or:
3. **Block evaluation**: value, value:, whileTrue:, whileFalse:
4. **Common messages**: at:, at:put:, size, class, new

These get dedicated bytecodes or inline code generation for speed.

## Testing and Verification

### Phase 1 Verification: Object Creation

```smalltalk
"Can we create objects?"
nil class.                     "→ UndefinedObject"
Object new.                    "→ an Object"
Array new: 5.                  "→ #(nil nil nil nil nil)"
```

### Phase 2 Verification: Method Installation

```smalltalk
"Can we add methods?"
Object compile: 'test ^42'.
Object new test.               "→ 42"
```

### Phase 3 Verification: Class Creation

```smalltalk
"Can we create new classes?"
Object subclass: #TestClass
    instanceVariableNames: 'a b'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'.

TestClass compile: 'initialize
    a := 0.
    b := 0'.

TestClass compile: 'a ^a'.
TestClass compile: 'b ^b'.

t := TestClass new.
t initialize.
t a.                           "→ 0"
```

### Phase 4 Verification: Full System

```smalltalk
"Create a complete class hierarchy"
Object subclass: #Magnitude
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Magnitude'.

Magnitude subclass: #Number
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Numbers'.

Number subclass: #Float
    instanceVariableNames: 'value'
    classVariableNames: 'Pi E'
    poolDictionaries: ''
    category: 'Kernel-Numbers'.

"System is now self-hosting!"
```

## Success Criteria

The bootstrap is successful when:

1. ✅ All 9 essential classes exist with correct structure
2. ✅ The metaclass loop is properly established
3. ✅ Essential methods are installed and working
4. ✅ `Object subclass: #NewClass ...` creates a new class
5. ✅ `NewClass compile: 'method source'` adds methods
6. ✅ `NewClass new` creates instances
7. ✅ Method lookup and execution work correctly
8. ✅ The system can load additional Smalltalk code

## Conclusion

This bootstrap design creates a minimal but complete Smalltalk-80 system using only:
- 9 essential classes
- ~20 essential methods
- 4-5 VM primitives
- ~200 object allocations

From this foundation, the entire Smalltalk-80 system can be built using Smalltalk itself, making it truly self-hosting and reflective.

## Appendix A: Complete Slot Reference

### All Classes and Their Slots

```
Object (0 slots when used as base class)

UndefinedObject extends Object (0 slots)

Behavior extends Object (5 slots)
  [0] superclass
  [1] methodDict
  [2] format
  [3] instanceVariables
  [4] organization

Class extends Behavior (8 slots)
  [0-4] inherited from Behavior
  [5] name
  [6] classPool
  [7] sharedPools

Metaclass extends Behavior (6 slots)
  [0-4] inherited from Behavior
  [5] thisClass

Symbol extends Object (variable)
  [0..n-1] character data

Array extends Object (variable)
  [0..n-1] elements

Dictionary extends Object (2 slots)
  [0] tally
  [1] array

Association extends Object (2 slots)
  [0] key
  [1] value

CompiledMethod extends Object (variable)
  Contains header, literals, bytecodes
```

## Appendix B: Bootstrap Checklist

- [ ] Create nil object
- [ ] Allocate 9 class objects
- [ ] Allocate 9 metaclass objects
- [ ] Set class pointers (class → metaclass)
- [ ] Set metaclass classes (metaclass → Metaclass)
- [ ] Set superclass hierarchy
- [ ] Set metaclass superclass hierarchy
- [ ] Set thisClass pointers
- [ ] Create method dictionaries
- [ ] Create class pools
- [ ] Set class names
- [ ] Install Object methods
- [ ] Install Class methods
- [ ] Install Behavior methods
- [ ] Install Dictionary methods
- [ ] Create Smalltalk global
- [ ] Register all classes
- [ ] Test class creation
- [ ] Test method compilation
- [ ] Test instance creation

## Appendix C: Common Issues and Solutions

### Issue: Infinite recursion in method lookup
**Cause**: Circular superclass reference
**Solution**: Ensure Object's superclass is nil

### Issue: Class creation fails
**Cause**: Missing subclass:... method
**Solution**: Install method in Object's metaclass, not Object

### Issue: Cannot compile methods
**Cause**: Missing compile: method
**Solution**: Install in Behavior (inherited by all classes)

### Issue: Metaclass class is nil
**Cause**: Forgot to set Metaclass class to itself
**Solution**: `primitiveSetClass: metaclassMetaclass to: metaclassClass`

### Issue: Dictionary lookup fails
**Cause**: No at: or at:put: methods
**Solution**: Install primitive-based Dictionary methods