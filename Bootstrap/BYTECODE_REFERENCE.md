# Smalltalk-80 Bytecode Reference for Bootstrap

## Overview
Phase 2 bootstrap uses explicit bytecode arrays to construct methods without a compiler.
Every method is created via `CompiledMethod>>newWithSelector:bytecodes:` or `CompiledMethod>>newWithSelector:primitive:bytecodes:`.

## Bytecode Categories

### Stack Operations
- `112` - Push receiver (self)
- `113` - Push true
- `114` - Push false
- `115` - Push nil
- `116` - Push -1
- `117` - Push 0
- `118` - Push 1
- `119` - Push 2
- `120-127` - Push receiver variable 0-7
- `16` - Push first argument
- `17` - Push second argument

### Return Operations
- `120` - Return receiver (self)
- `121` - Return true
- `122` - Return false
- `123` - Return nil
- `124` - Return top of stack
- `125` - Return top from block
- `135` - Primitive return

### Send Operations
- `176` - Send arithmetic/special selector 0 (+)
- `177` - Send arithmetic/special selector 1 (-)
- `178` - Send arithmetic/special selector 2 (<)
- `179` - Send arithmetic/special selector 3 (>)
- `180` - Send arithmetic/special selector 4 (<=)
- `181` - Send arithmetic/special selector 5 (>=)
- `182` - Send arithmetic/special selector 6 (=)
- `183` - Send arithmetic/special selector 7 (~=)
- `184` - Send arithmetic/special selector 8 (*)
- `201` - Send literal selector 1 (extended)
- `209` - Send literal selector with 1 arg

## Common Method Patterns

### Simple Accessor Methods
```
Selector        Bytecodes           Meaning
initialize      [124]               ^self
yourself        [124]               ^self
class           [16, 135]           ^self class (primitive)
methodDict      [16, 135]           ^instVar[0]
format          [16, 135]           ^instVar[0]
superclass      [16, 135]           ^instVar[0]
name            [16, 135]           ^instVar[0]
thisClass       [16, 135]           ^instVar[0]
```

### Boolean Control Methods
```
Selector        Bytecodes           Meaning
True>>ifTrue:   [16, 201, 124]      ^trueBlock value
True>>ifFalse:  [123]               ^nil
False>>ifTrue:  [123]               ^nil
False>>ifFalse: [16, 201, 124]      ^falseBlock value
```

### Primitive Methods with Fallback
```
Selector        Primitive   Bytecodes           Meaning
+               1           [112, 16, 176, 124] self + arg (send on fail)
-               2           [112, 16, 177, 124] self - arg (send on fail)
*               9           [112, 16, 184, 124] self * arg (send on fail)
<               3           [112, 16, 178, 124] self < arg (send on fail)
=               7           [112, 16, 182, 124] self = arg (send on fail)
==              110         [16, 17, 182, 135]  identity comparison
at:             60          [112, 209, 124]     self primitiveFailed
at:put:         61          [112, 209, 124]     self primitiveFailed
size            62          [112, 209, 124]     self primitiveFailed
value           81          [112, 209, 124]     self primitiveFailed
```

## Phase 2 Method Installation Process

1. **Get method dictionary**: Send `methodDict` to class
2. **Create compiled method**: Send `newWithSelector:bytecodes:` to CompiledMethod
3. **Install in dictionary**: Send `at:put:` to method dictionary

Example:
```json
{
  "receiver": "ObjectClass",
  "selector": "methodDict",
  "resultName": "ObjectMethodDict"
},
{
  "receiver": "CompiledMethod",
  "selector": "newWithSelector:bytecodes:",
  "arguments": ["initialize", [124]],
  "resultName": "InitializeMethod"
},
{
  "receiver": "ObjectMethodDict",
  "selector": "at:put:",
  "arguments": ["initialize", "InitializeMethod"]
}
```

## Primitive Numbers

Core primitives used in bootstrap:
- **1**: SmallInteger addition
- **2**: SmallInteger subtraction
- **3**: SmallInteger less than
- **7**: SmallInteger equality
- **9**: SmallInteger multiplication
- **60**: Array/Object at:
- **61**: Array/Object at:put:
- **62**: Array/Object size
- **70**: Behavior basicNew
- **71**: Behavior basicNew:
- **81**: BlockContext value
- **110**: Object identity (==)
- **111**: Object class

## Class Hierarchy Built in Phase 2

```
ProtoObject
└── Object
    ├── Behavior
    │   └── ClassDescription
    │       ├── Class
    │       └── Metaclass
    ├── UndefinedObject
    ├── Boolean
    │   ├── True
    │   └── False
    ├── Magnitude
    │   ├── Number
    │   │   └── Integer
    │   │       └── SmallInteger
    │   └── LookupKey
    │       └── Association
    ├── Collection
    │   ├── SequenceableCollection
    │   │   └── ArrayedCollection
    │   │       ├── Array
    │   │       └── String
    │   └── Set
    │       └── Dictionary
    │           └── IdentityDictionary
    │               └── MethodDictionary
    ├── Symbol
    ├── BlockContext
    └── CompiledMethod
```

## Notes

- All bytecode arrays are concrete, not symbolic
- Phase 1 must provide VM primitives for object allocation and pointer manipulation
- Phase 2 uses only message sending to build the class hierarchy
- No compiler is used - all methods are explicit bytecode arrays
- The bytecode format follows Smalltalk-80 Blue Book specification