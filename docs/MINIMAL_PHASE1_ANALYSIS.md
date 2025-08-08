# Minimal Phase 1 Analysis for Message-Based Bootstrap

## The Bootstrap Paradox
To create classes via message sends, we need:
1. Classes that can receive messages
2. Methods installed in those classes
3. A method lookup mechanism
4. Primitive method execution

## Dependency Analysis for Class Creation

### To Create Any Class via Messages, We Need:

#### 1. Core Class/Metaclass Infrastructure
```
ProtoObject ← Object ← Behavior ← ClassDescription ← Class
                                                   ← Metaclass
```

These must exist with:
- Proper class pointers
- Superclass chains
- Method dictionaries

#### 2. Method Dictionary Infrastructure
```
MethodDictionary (with at:put: method)
├── Needs: Association or key-value storage
└── Needs: Primitive for dictionary operations
```

#### 3. Compiled Method Infrastructure
```
CompiledMethod (with new, selector:, bytecodes:, literals:)
├── Needs: ByteArray for bytecode storage
├── Needs: Array for literals
└── Needs: Symbol for selectors
```

#### 4. Essential Primitives
- **Primitive 70**: `basicNew` - allocate instance
- **Primitive 71**: `basicNew:` - allocate indexed instance
- **Primitive 461**: Dictionary `at:put:` (or equivalent)

## Minimal Phase 1 Classes

### Absolutely Required (Cannot Bootstrap Without)
1. **ProtoObject** - Root of hierarchy
2. **Object** - Basic object behavior
3. **Behavior** - Class behavior, method lookup
4. **ClassDescription** - Instance variable management
5. **Class** - Class creation protocol
6. **Metaclass** - Metaclass behavior
7. **MethodDictionary** - Method storage
8. **CompiledMethod** - Method representation
9. **Symbol** - Method selectors
10. **Array** - Literal storage
11. **ByteArray** - Bytecode storage
12. **UndefinedObject** - nil
13. **SmallInteger** - Array indexing
14. **Association** - Dictionary entries

### Required Methods in Phase 1

#### On Metaclass class:
```smalltalk
new
    "Create a new metaclass"
    <primitive: 70>
    ^self primitiveFailed
```

#### On Metaclass:
```smalltalk
superclass: aClass
methodDictionary: aDict
format: anInteger
name: aSymbol
```

#### On Class class:
```smalltalk
new
    "Create a new class"
    <primitive: 70>
    ^self primitiveFailed
```

#### On Behavior:
```smalltalk
new
    "Create instance of receiver"
    <primitive: 70>
    ^self primitiveFailed

methodDictionary: aDict
superclass: aClass
```

#### On MethodDictionary:
```smalltalk
at: key put: value
    "Store method at selector"
    <primitive: 461>
    ^self primitiveFailed

new
    "Create new dictionary"
    <primitive: 70>
    ^self primitiveFailed
```

#### On CompiledMethod class:
```smalltalk
new
    "Create new compiled method"
    <primitive: 70>
    ^self primitiveFailed
```

#### On CompiledMethod:
```smalltalk
selector: aSymbol
bytecodes: aByteArray
literals: anArray
primitive: anInteger
methodClass: aClass
```

## The Transition Point

### Phase 1 Must Create (Direct Memory Manipulation):
1. Core class lattice (ProtoObject through Metaclass)
2. Method storage classes (MethodDictionary, CompiledMethod)
3. Basic data types (Symbol, Array, ByteArray, SmallInteger)
4. Install minimal methods listed above

### Phase 2 Can Then Create (Via Messages):
Everything else! Once we have:
- `Metaclass new` working
- `MethodDictionary new` and `at:put:` working
- `CompiledMethod new` with setters working
- Basic primitives (70, 71, 461) installed

We can create any other class using this sequence:

```smalltalk
"1. Create metaclass"
metaClass := Metaclass new.
metaClass superclass: superclass class.
metaClass methodDictionary: MethodDictionary new.
metaClass format: 0.

"2. Create class"
newClass := metaClass new.
newClass superclass: superclass.
newClass methodDictionary: MethodDictionary new.
newClass format: format.
newClass setName: className.

"3. Link them"
metaClass thisClass: newClass.

"4. Install in system"
Smalltalk at: className put: newClass.
```

## Validation Checklist

After Phase 1, verify these work:
- [ ] `Metaclass new` returns a metaclass
- [ ] `someMetaclass new` returns a class
- [ ] `MethodDictionary new` returns empty dictionary
- [ ] `dict at: #selector put: method` stores method
- [ ] `CompiledMethod new` returns method object
- [ ] `Behavior new` allocates instance
- [ ] Method lookup traverses superclass chain
- [ ] Primitives 70, 71, 461 execute correctly

## Recommended Phase 1 Scope

### Minimal Set (14 classes):
```
ProtoObject, Object, Behavior, ClassDescription, Class, Metaclass,
UndefinedObject, MethodDictionary, CompiledMethod, Symbol, Array,
ByteArray, SmallInteger, Association
```

### Plus These Methods:
- Object: `class`, `initialize`
- Behavior: `new`, `basicNew`, `superclass:`, `methodDictionary:`
- Class: `name:`, `category:`
- Metaclass: `new`, `thisClass:`
- MethodDictionary: `new`, `at:put:`, `at:`
- CompiledMethod: `new`, selector/bytecode/literal setters
- Symbol: `new:` or intern mechanism
- Array/ByteArray: `new:`, `at:`, `at:put:`

## Conclusion

The absolute minimal Phase 1 creates:
1. **14 core classes** with proper structure
2. **~25 essential methods** for object/class creation
3. **3-5 primitives** (70, 71, 461, plus array access)

Once these exist and work via message sends, Phase 2 can create everything else using pure Smalltalk code without any direct memory manipulation.