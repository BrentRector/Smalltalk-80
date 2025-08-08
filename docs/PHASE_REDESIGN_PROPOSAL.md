# Phase 1/2 Redesign Proposal

## Current Problem
The VM has special-case code for creating objects of specific types (SmallInteger, Symbol, etc.) which:
1. Creates coupling between VM and specific classes
2. Often creates objects without proper class pointers
3. Duplicates logic that should be in the classes themselves

## Proposed Solution

### Phase 1: Complete Self-Sufficient Kernel
Phase 1 should establish a complete object system where **all objects can be created via message sends**.

#### Classes in Phase 1:
- **Kernel**: ProtoObject, Object, Behavior, ClassDescription, Class, Metaclass, UndefinedObject
- **Basic Types**: SmallInteger, Boolean, True, False, Character, Symbol, String, Array, ByteArray  
- **Method Infrastructure**: CompiledMethod, MethodContext, BlockContext, MethodDictionary

#### Key Methods Installed in Phase 1:
- `Behavior class>>basicNew` (primitive 70)
- `Behavior class>>basicNew:` (primitive 71)
- `Behavior class>>new` (sends basicNew then initialize)
- `SmallInteger class>>new:` (creates integer with value)
- `Symbol class>>new:` (interns symbol)
- `String class>>new:` (creates string)

### VM Changes

#### Remove:
```csharp
// OLD - VM knows about SmallInteger
public int PrimitiveNewSmallInteger(int value) {
    var intObj = new SmalltalkObject(null, 1);
    intObj.SetField(0, value);
    return intObj.Oop;
}
```

#### Replace With:
```csharp
// NEW - VM just sends messages
public int CreateSmallInteger(int value) {
    var smallIntClass = PrimitiveLookupClass("SmallInteger");
    var newSelector = PrimitiveInternSymbol("new:");
    var valueObj = /* wrap value as object */;
    return PrimitiveSendMessage(smallIntClass, newSelector, new[] { valueObj });
}
```

### Benefits

1. **Consistency**: All objects created the same way - via message sends
2. **Proper Class Pointers**: Classes manage their own instance creation
3. **Less VM Complexity**: VM doesn't need special knowledge of class internals
4. **True OO**: The system becomes self-describing and self-modifying

### Phase 2 Becomes Simpler

Phase 2 then only needs to add:
- Collection classes (Set, Dictionary, IdentityDictionary)
- Stream classes
- Additional behavior

Everything in Phase 2 is created by sending messages to Phase 1 classes.

## Implementation Steps

1. Create new `bootstrap_phase1_complete.json` with all fundamental classes
2. Ensure all classes have proper `new`/`basicNew` methods
3. Update VM adapter to use message sends instead of direct object creation
4. Test that after Phase 1:
   - `SmallInteger new: 42` works
   - `Symbol new: 'test'` works  
   - `Array new: 10` works
   - etc.

## Example Usage After Phase 1

```smalltalk
"Create a SmallInteger"
anInteger := SmallInteger new: 42.

"Create a Symbol"
aSymbol := Symbol new: 'mySelector'.

"Create an Array"
anArray := Array new: 10.

"Create a String"
aString := String new: 'Hello World'.

"All via message sends - no special VM primitives!"
```

## Migration Path

1. Keep existing bootstrap files for compatibility
2. Create new enhanced Phase 1
3. Test thoroughly
4. Gradually migrate VM code to use message sends
5. Eventually deprecate special-case object creation

This makes the system truly object-oriented from the ground up!