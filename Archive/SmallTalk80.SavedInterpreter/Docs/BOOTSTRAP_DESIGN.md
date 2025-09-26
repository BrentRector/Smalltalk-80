# Bootstrap Design

## Overview

The Smalltalk-80 interpreter uses a multi-phase bootstrap process to construct the class hierarchy and method dictionaries from JSON manifests. The key innovation is **Phase -1**, which pre-computes all field mappings before any objects are created, eliminating the need for hardcoded field indices.

## Phase -1: Field Mapping Construction (NEW)

### Purpose
Before creating any objects, we analyze the complete class hierarchy from the JSON manifest to build a comprehensive field mapping table. This allows all bootstrap code to use field names instead of hardcoded indices.

### Process
1. **Parse Manifest**: Read all class definitions from JSON
2. **Build Inheritance Tree**: Construct the superclass relationships
3. **Calculate Field Offsets**: 
   - Walk each class's superclass chain
   - Accumulate instance variables from each level
   - Assign indices based on inheritance order
4. **Create Mappings**:
   - Instance-side: `(className, fieldName) -> index`
   - Class-side: `(className + " class", fieldName) -> index`

### Example Field Calculation
```
Class hierarchy: Object -> Collection -> Set -> Dictionary
Instance variables:
  Object: []
  Collection: []
  Set: [tally]
  Dictionary: []

Dictionary field mapping:
  ("Dictionary", "tally") -> 0  // inherited from Set
  
IdentityDictionary extends Dictionary:
  Instance variables: [tally, valueArray]
  ("IdentityDictionary", "tally") -> 0      // from Set
  ("IdentityDictionary", "valueArray") -> 1 // own field
```

### Metaclass Field Mapping
Metaclasses follow the same pattern but inherit from the metaclass hierarchy:
```
Metaclass hierarchy: Behavior -> ClassDescription -> Class -> Metaclass

All metaclasses have these fields:
  From Behavior: [superclass, methodDict, format]
  From ClassDescription: [instanceVariables, organization]  
  From Class: [name, classPool, sharedPools]
  From Metaclass: [thisClass]

Example for "Dictionary class":
  ("Dictionary class", "superclass") -> 0
  ("Dictionary class", "methodDict") -> 1
  ("Dictionary class", "thisClass") -> 9
```

## Bootstrap Phases

### Phase 1: Allocate Stubs
- Create raw memory objects for all classes and metaclasses
- Objects have temporary/invalid class pointers
- **Uses Phase -1 mapping** to determine object sizes

### Phase 2: Wire Class Pointers
- Set proper class pointers: object -> class -> metaclass -> Metaclass
- **Uses Phase -1 mapping** to access the class pointer field

### Phase 3: Wire Superclasses  
- Establish superclass chains for classes and metaclasses
- **Uses Phase -1 mapping** to set superclass field by name

### Phase 4: Set Names
- Store class name symbols in the name field
- **Uses Phase -1 mapping** to access name field

### Phase 5: Set Instance Variables
- Create arrays of instance variable names
- **Uses Phase -1 mapping** to set instanceVariables field

## Benefits of Phase -1 Approach

### 1. No Hardcoded Indices
All bootstrap code uses symbolic field names:
```csharp
// Old way (fragile):
classObj.Fields[0] = superclassOop;  // What if field order changes?

// New way (robust):
vm.SetFieldBootstrap(classOop, "Class", "superclass", superclassOop);
```

### 2. Validation
The field mapping can validate that:
- All field names are valid for the target class
- Field indices are within bounds
- No field name conflicts in inheritance

### 3. Consistency
The same field mapping is used:
- During bootstrap (static from JSON)
- At runtime (dynamic for new classes)
- By all VM components

### 4. Documentation
The field mapping serves as documentation of the object layout for each class.

## Integration with VirtualMachine

The FieldLookup component is passed to the VirtualMachine during initialization:
```csharp
// Phase -1: Build mapping
var fieldLookup = new FieldLookup();
fieldLookup.RegisterManifest(manifest);

// Pass to VM
var vm = new VirtualMachine(memory, fieldLookup);

// Now ClassBuilder can use field names
classBuilder.SetSuperclass(classOop, superclassOop); // Uses mapping internally
```

## Runtime Considerations

### Dynamic Class Creation
When Smalltalk code creates new classes at runtime:
1. The VM intercepts the class creation
2. Updates the field mapping with the new class
3. Future field accesses use the updated mapping

### Performance
- Bootstrap: Field lookups are compile-time constant (from static mapping)
- Runtime: Field indices are cached after first lookup
- Bytecodes: Can use direct index access for instance variables

### Debugging
With field names instead of indices, debugging output is much clearer:
```
Setting Dictionary.tally = 0
Getting Association.value
```

## Migration Path

To complete the Phase -1 implementation:
1. ✅ Create FieldLookup class (DONE)
2. ✅ Populate from manifest in Phase -1 (DONE - RegisterManifest)
3. ⚠️ Update ClassBuilder to use field mapping (IN PROGRESS)
4. ⚠️ Pass complete mapping to VirtualMachine
5. ⚠️ Remove all remaining hardcoded indices

## Summary

Phase -1 transforms the bootstrap process from a fragile, index-based system to a robust, name-based system. By pre-computing all field mappings from the JSON manifest, we achieve type safety and maintainability without sacrificing performance.