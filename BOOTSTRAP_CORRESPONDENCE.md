# Bootstrap Correspondence

This document shows the exact 1:1 correspondence between `Bootstrap.st` (Smalltalk source) and `GeneratedBootstrap.cs` (C# implementation).

## Overview

The bootstrap contains exactly **30 classes** and **22 methods** that are compiled into the interpreter binary.

## Classes (30 total)

### Core Object System (2 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Object | SystemDictionary.DefineClass("Object", ...) | (none) |
| UndefinedObject | SystemDictionary.DefineClass("UndefinedObject", ...) | (none) |

### Metaclass System (4 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Behavior | SystemDictionary.DefineClass("Behavior", ...) | superclass, methodDict, format |
| ClassDescription | SystemDictionary.DefineClass("ClassDescription", ...) | instanceVariables, organization |
| Class | SystemDictionary.DefineClass("Class", ...) | name, classPool, sharedPools |
| Metaclass | SystemDictionary.DefineClass("Metaclass", ...) | thisClass |

### Booleans (3 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Boolean | SystemDictionary.DefineClass("Boolean", ...) | (none) |
| True | SystemDictionary.DefineClass("True", ...) | (none) |
| False | SystemDictionary.DefineClass("False", ...) | (none) |

### Numbers (4 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Magnitude | SystemDictionary.DefineClass("Magnitude", ...) | (none) |
| Number | SystemDictionary.DefineClass("Number", ...) | (none) |
| Integer | SystemDictionary.DefineClass("Integer", ...) | (none) |
| SmallInteger | SystemDictionary.DefineClass("SmallInteger", ...) | (none) |

### Symbols and Strings (2 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Symbol | SystemDictionary.DefineClass("Symbol", ...) | (none) |
| String | SystemDictionary.DefineClass("String", ...) | (none) - variableByteSubclass |

### Collections (10 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| Collection | SystemDictionary.DefineClass("Collection", ...) | (none) |
| SequenceableCollection | SystemDictionary.DefineClass("SequenceableCollection", ...) | (none) |
| ArrayedCollection | SystemDictionary.DefineClass("ArrayedCollection", ...) | (none) |
| Array | SystemDictionary.DefineClass("Array", ...) | (none) - variableSubclass |
| Set | SystemDictionary.DefineClass("Set", ...) | tally, array |
| Dictionary | SystemDictionary.DefineClass("Dictionary", ...) | (none) - variableSubclass |
| SystemDictionary | SystemDictionary.DefineClass("SystemDictionary", ...) | (none) - variableSubclass |
| IdentityDictionary | SystemDictionary.DefineClass("IdentityDictionary", ...) | (none) - variableSubclass |
| MethodDictionary | SystemDictionary.DefineClass("MethodDictionary", ...) | (none) |
| LookupKey | SystemDictionary.DefineClass("LookupKey", ...) | key |
| Association | SystemDictionary.DefineClass("Association", ...) | value |

### Execution (2 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| BlockContext | SystemDictionary.DefineClass("BlockContext", ...) | nargs, startpc, home |
| CompiledMethod | SystemDictionary.DefineClass("CompiledMethod", ...) | (none) |

### Organization (2 classes)
| Bootstrap.st | GeneratedBootstrap.cs | Instance Variables |
|-------------|----------------------|-------------------|
| ClassOrganizer | SystemDictionary.DefineClass("ClassOrganizer", ...) | categories |
| SystemOrganizer | SystemDictionary.DefineClass("SystemOrganizer", ...) | categories |

## Methods (22 total)

### Object (4 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| == | 110 | 0x70, 0x7C | Identity comparison |
| class | 111 | 0x71, 0x7C | Return receiver's class |
| isNil | - | 0x79 | Return false |
| notNil | - | 0x78 | Return true |

### UndefinedObject (2 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| isNil | - | 0x78 | Return true |
| notNil | - | 0x79 | Return false |

### Behavior (3 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| basicNew | 70 | 0x46, 0x7C | Create instance |
| basicNew: | 71 | 0x47, 0x7C | Create indexable instance |
| new | - | 0x70, 0xCE | Call basicNew |

### True (2 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| ifTrue: | - | 0x10, 0xCA | Evaluate block |
| ifFalse: | - | 0x7B | Return nil |

### False (2 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| ifTrue: | - | 0x7B | Return nil |
| ifFalse: | - | 0x10, 0xCA | Evaluate block |

### SmallInteger (5 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| + | 1 | 0x01, 0x7C | Addition |
| - | 2 | 0x02, 0x7C | Subtraction |
| * | 9 | 0x09, 0x7C | Multiplication |
| < | 3 | 0x03, 0x7C | Less than |
| = | 7 | 0x07, 0x7C | Equality |

### Array (3 methods)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| at: | 60 | 0x3C, 0x7C | Get element |
| at:put: | 61 | 0x3D, 0x7C | Set element |
| size | 62 | 0x3E, 0x7C | Array size |

### BlockContext (1 method)
| Selector | Primitive | Bytecodes (C#) | Description |
|----------|-----------|----------------|-------------|
| value | 81 | 0x51, 0x7C | Evaluate block |

## Global Variables

The bootstrap also initializes these global variables:

| Variable | Class | Purpose |
|----------|-------|---------|
| nil | UndefinedObject | The nil object |
| true | True | The true object |
| false | False | The false object |
| Smalltalk | SystemDictionary | The system dictionary |
| SystemOrganization | SystemOrganizer | System organization |
| Undeclared | Dictionary | Undefined globals |
| Transcript | Object | Output transcript (placeholder) |

## Verification

To verify the correspondence:

1. Count classes in Bootstrap.st: 30
2. Count classes in GeneratedBootstrap.cs: 30
3. Count methods in Bootstrap.st: 22
4. Count methods in GeneratedBootstrap.cs: 22

The files are in exact correspondence. Any class or method in one MUST be in the other.

## Future Work

Once the BootstrapGenerator tool is working properly, it will:
1. Parse Bootstrap.st
2. Generate GeneratedBootstrap.cs automatically
3. Ensure perfect correspondence

For now, GeneratedBootstrap.cs is handwritten but follows Bootstrap.st exactly.