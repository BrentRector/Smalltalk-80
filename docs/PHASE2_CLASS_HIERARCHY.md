# Phase 2 Class Hierarchy

Generated after Phase 2 bootstrap completion.
Last updated: After fixing superclass lookup for Phase 2 classes.

## Instance Side Hierarchy

```
nil (ProtoObject has no superclass)
 └── ProtoObject
      └── Object
           ├── UndefinedObject
           ├── Behavior
           │    └── ClassDescription
           │         ├── Class
           │         └── Metaclass
           ├── Array
           │    └── ByteArray
           ├── String
           │    └── Symbol
           ├── Message
           ├── CompiledMethod
           ├── MethodContext
           └── MethodDictionary
```

## Metaclass Side Hierarchy

```
Class (all metaclasses inherit from Class through their respective metaclass chain)
 └── ProtoObject class
      └── Object class
           ├── UndefinedObject class
           ├── Behavior class
           │    └── ClassDescription class
           │         ├── Class class
           │         └── Metaclass class (special: class pointer points to itself)
           ├── Array class
           │    └── ByteArray class
           ├── String class
           │    └── Symbol class
           ├── Message class
           ├── CompiledMethod class
           ├── MethodContext class
           └── MethodDictionary class
```

## Special Relationships

### The Metaclass Loop
- **Metaclass class** is its own class: `Metaclass class.class == Metaclass class`
- **Metaclass class** superclass: `ClassDescription class`

### Class Pointer Rules
1. Every instance has a class pointer to its class
2. Every class has a class pointer to its metaclass
3. Every metaclass has a class pointer to Metaclass (except Metaclass class which points to itself)

## Issues Fixed ✓

Previously, Phase 2 classes were showing nil superclasses because the superclass lookup only checked namedObjects and didn't look up classes created in Phase 1 from the VM registry. This has been fixed by updating CreatePhase2Class to:
1. First check namedObjects for the superclass
2. If not found, lookup from VM registry (for Phase 1 classes)
3. Apply the same logic for metaclass superclasses

All Phase 2 classes now correctly inherit from their specified superclasses.

## Phase 1 vs Phase 2 Classes

### Created in Phase 1 (minimal versions):
- ProtoObject, Object, Class, Metaclass, UndefinedObject
- Behavior, ClassDescription
- Array, Symbol (minimal, for instance variables)

### Enhanced in Phase 2:
- Array (marked as variable-sized, methods added)
- Symbol (superclass changed from Object to String)

### Created fresh in Phase 2:
- String, Message, CompiledMethod, MethodContext
- ByteArray, MethodDictionary

## Next Steps

The nil superclass issue for Phase 2 classes needs to be fixed. These classes should have their superclass set to Object (or the appropriate class as specified in the JSON).