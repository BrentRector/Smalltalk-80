# Bootstrap Sequence Update

## Critical Changes Required

The bootstrap sequence has been updated to follow a more correct ordering that resolves circular dependencies. The key changes are:

1. **ProtoObject is now the root** (not Object)
2. **Class must be created before Metaclass**
3. **Specific creation order must be followed**

## New Bootstrap Sequence

### 🧩 Class Bootstrap Sequence (Minimal Core)

| Step | Class | Superclass | Metaclass Superclass | Notes |
|------|-------|------------|---------------------|-------|
| 1️⃣ | Class | Behavior | Metaclass | Needed to create other classes |
| 2️⃣ | Metaclass | Class | Metaclass | Self-referential metaclass |
| 3️⃣ | Behavior | ProtoObject | Class | Superclass of Class |
| 4️⃣ | ProtoObject | nil | Class | Root of all classes |
| 5️⃣ | Object | ProtoObject | Class | First usable class |
| 6️⃣ | Symbol | Object | Class | Needed for naming |
| 7️⃣ | Dictionary | Object | Class | Needed for method dictionaries |
| 8️⃣ | Smalltalk | Dictionary | Class | Global namespace |

## Implementation Strategy

### Phase 1: Pre-Bootstrap Setup
Before creating any classes, we need placeholders:

```csharp
// Create placeholder objects that will be properly initialized later
SmalltalkObject classClass = Primitive71_New(null, 8);
SmalltalkObject metaclassClass = Primitive71_New(null, 8);
SmalltalkObject behaviorClass = Primitive71_New(null, 8);
SmalltalkObject protoObjectClass = Primitive71_New(null, 8);

// Create their metaclasses
SmalltalkObject classMetaclass = Primitive71_New(null, 6);
SmalltalkObject metaclassMetaclass = Primitive71_New(null, 6);
SmalltalkObject behaviorMetaclass = Primitive71_New(null, 6);
SmalltalkObject protoObjectMetaclass = Primitive71_New(null, 6);
```

### Phase 2: Establish Core Relationships

```csharp
// Step 1: Class extends Behavior
Primitive61_AtPut(classClass, BEHAVIOR_SUPERCLASS + 1, behaviorClass);
SetClassPointer(classClass, classMetaclass);

// Step 2: Metaclass extends Class (creates the loop!)
Primitive61_AtPut(metaclassClass, BEHAVIOR_SUPERCLASS + 1, classClass);
SetClassPointer(metaclassClass, metaclassMetaclass);

// Step 3: Behavior extends ProtoObject
Primitive61_AtPut(behaviorClass, BEHAVIOR_SUPERCLASS + 1, protoObjectClass);
SetClassPointer(behaviorClass, behaviorMetaclass);

// Step 4: ProtoObject has no superclass (root)
Primitive61_AtPut(protoObjectClass, BEHAVIOR_SUPERCLASS + 1, nil);
SetClassPointer(protoObjectClass, protoObjectMetaclass);
```

### Phase 3: Metaclass Hierarchy

```csharp
// All metaclasses are instances of Metaclass
SetClassPointer(classMetaclass, metaclassClass);
SetClassPointer(metaclassMetaclass, metaclassClass);  // Self-reference!
SetClassPointer(behaviorMetaclass, metaclassClass);
SetClassPointer(protoObjectMetaclass, metaclassClass);

// Metaclass hierarchy parallels class hierarchy
Primitive61_AtPut(classMetaclass, BEHAVIOR_SUPERCLASS + 1, behaviorMetaclass);
Primitive61_AtPut(metaclassMetaclass, BEHAVIOR_SUPERCLASS + 1, classMetaclass);
Primitive61_AtPut(behaviorMetaclass, BEHAVIOR_SUPERCLASS + 1, protoObjectMetaclass);
Primitive61_AtPut(protoObjectMetaclass, BEHAVIOR_SUPERCLASS + 1, classClass); // Loop back to Class!
```

### Phase 4: Set thisClass References

```csharp
Primitive61_AtPut(classMetaclass, METACLASS_THISCLASS + 1, classClass);
Primitive61_AtPut(metaclassMetaclass, METACLASS_THISCLASS + 1, metaclassClass);
Primitive61_AtPut(behaviorMetaclass, METACLASS_THISCLASS + 1, behaviorClass);
Primitive61_AtPut(protoObjectMetaclass, METACLASS_THISCLASS + 1, protoObjectClass);
```

## Key Differences from Previous Design

### 1. ProtoObject vs Object
- **Old**: Object was the root with superclass = nil
- **New**: ProtoObject is the root, Object extends ProtoObject
- **Impact**: Better separation of minimal protocol from common protocol

### 2. Creation Order
- **Old**: Could create Object first, then build up
- **New**: MUST create Class/Metaclass/Behavior/ProtoObject first
- **Impact**: Resolves circular dependency issues

### 3. Metaclass Loop
- **Old**: Object class → Class was problematic
- **New**: ProtoObject class → Class creates cleaner loop
- **Impact**: More standard Smalltalk-80 architecture

## Field Layouts

### ProtoObject (Root)
```
Fields: none (minimal protocol only)
```

### Object
```
Fields: (inherits from ProtoObject, adds none initially)
Class Variables: DependentsFields
```

### Behavior
```
0: superclass
1: methodDict
2: format
3: instanceVariables (added by ClassDescription)
4: organization (added by ClassDescription)
```

### Class
```
0-4: (inherited from Behavior)
5: name
6: classPool        // CRITICAL for class variables!
7: sharedPools
```

### Metaclass
```
0-4: (inherited from Behavior)
5: thisClass
```

## Updated Bootstrap.st Requirements

The Bootstrap.st file must define classes in this exact order:

```smalltalk
"Step 1: Core metaclass infrastructure"
Behavior subclass: #Class
    instanceVariableNames: 'name classPool sharedPools'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Classes'!

Class subclass: #Metaclass
    instanceVariableNames: 'thisClass'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Classes'!

ProtoObject subclass: #Behavior
    instanceVariableNames: 'superclass methodDict format'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Classes'!

nil subclass: #ProtoObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

"Step 2: First usable class"
ProtoObject subclass: #Object
    instanceVariableNames: ''
    classVariableNames: 'DependentsFields'
    poolDictionaries: ''
    category: 'Kernel-Objects'!

"Step 3: Essential support classes"
Object subclass: #Symbol
    instanceVariableNames: ''
    classVariableNames: 'SymbolTable'
    poolDictionaries: ''
    category: 'Collections-Text'!

Object subclass: #Dictionary
    instanceVariableNames: 'tally'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Unordered'!

Dictionary subclass: #Smalltalk
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Support'!
```

## Validation Checklist

After bootstrap, verify:

- [ ] ProtoObject superclass == nil
- [ ] Object superclass == ProtoObject
- [ ] Behavior superclass == ProtoObject
- [ ] Class superclass == Behavior
- [ ] Metaclass superclass == Class
- [ ] ProtoObject class class == Metaclass
- [ ] Class class class == Metaclass
- [ ] Metaclass class class == Metaclass (self-loop)
- [ ] ProtoObject class superclass == Class
- [ ] Object classPool is a real Dictionary at field index 6

## Impact on UpdatedBootstrapGenerator

The generator must be updated to:

1. Change root class from Object to ProtoObject
2. Follow the exact 8-step sequence
3. Handle the Metaclass self-reference correctly
4. Ensure ProtoObject class → Class loop is established

This sequence ensures all circular dependencies are resolved and the metaclass system is properly initialized.