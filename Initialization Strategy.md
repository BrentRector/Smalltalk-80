# Smalltalk-80 Cold Start Loader: Initialization Strategy

**Version**: 1.0
**Last Updated**: 2025-01-16
**Status**: Production - All 15 phases operational

## Table of Contents

1. [The Fundamental Challenge](#the-fundamental-challenge)
2. [Phase-by-Phase Architecture](#phase-by-phase-architecture)
   - [Phase 0: Preflight Validation](#phase-0-preflight-validation)
   - [Phase 1: Symbol Interning](#phase-1-symbol-interning)
   - [Phase 2: Class Shells](#phase-2-class-shells)
   - [Phase 3: Metaclass Wiring](#phase-3-metaclass-wiring)
   - [Phase 4: Class Resolution](#phase-4-class-resolution)
   - [Phase 5: Layout Computation](#phase-5-layout-computation)
   - [Phase 6: Object Allocation](#phase-6-object-allocation)
   - [Phase X: Class Reference Initialization](#phase-x-class-reference-initialization)
   - [Phase Y: Instance Variable Initialization](#phase-y-instance-variable-initialization)
   - [Phase 7: Method Dictionary Allocation](#phase-7-method-dictionary-allocation)
   - [Phase 8: Method Installation](#phase-8-method-installation)
   - [Phase 9: Method Dictionary Installation](#phase-9-method-dictionary-installation)
   - [Phase 10: Patch Application](#phase-10-patch-application)
   - [Phase 11: USTable Population](#phase-11-ustable-population)
   - [Phase 12: Global Variable Installation](#phase-12-global-variable-installation)
   - [Phase 13: Comprehensive Validation](#phase-13-comprehensive-validation)
   - [Phase 14: GC Root Setup](#phase-14-gc-root-setup)
   - [Phase 15: GC Verification](#phase-15-gc-verification)
3. [Key Architectural Principles](#key-architectural-principles)

---

## The Fundamental Challenge

The Smalltalk-80 object memory presents a circular dependency problem that requires careful orchestration to resolve:

### Circular Dependencies

1. **Every object has a class** (stored in its header slot 0)
2. **Classes are objects** (they need a class too - their metaclass)
3. **Metaclasses are objects** (they need a class too - the Metaclass class)
4. **Instance variables should be initialized to nil** (but nil is itself an object that doesn't exist initially)

### Critical Constraints

1. **OOP value 0 is valid** - Cannot be used as a sentinel for "uninitialized"
2. **Forward references exist** - Some objects reference others not yet allocated
3. **Memory layout matters** - Header slot 0 ≠ Instance variable 0
   - `SetSlot(obj, 0)` sets memory offset 0 (header - class reference)
   - `SetInstanceVariable(obj, 0)` sets memory offset 4+ (first instance variable)

### Solution Architecture

A carefully orchestrated multi-pass bootstrap process:

1. **Allocate skeleton objects first** - Minimal initialization, leave most fields undefined
2. **Wire critical relationships incrementally** - Link classes and metaclasses in phases
3. **Initialize remaining fields to nil** - Only after nil singleton exists
4. **Track initialized fields explicitly** - Cannot rely on 0 as "uninitialized" marker

The loader uses a field tracking system to prevent phase scope creep:

```csharp
private readonly HashSet<(int objectOOP, int fieldIndex)> _initializedFields = new();
```

This HashSet is the authoritative source of truth for "which fields have been set" and prevents later phases from overwriting earlier work.

---

## Phase-by-Phase Architecture

### Phase 0: Preflight Validation

**Purpose**: Verify the JSON bundle is well-formed before attempting cold start.

**Responsibility**: Data integrity validation only - no allocations, no mutations.

**Actions**:
- Validates all class references exist in the bundle
- Validates all symbol/string indices are in bounds
- Checks for duplicate class definitions
- Verifies metaclass relationships are properly defined
- Ensures all superclass references are resolvable

**Output**: None (throws exception if validation fails)

**Example Validations**:
```csharp
// Verify class references
foreach (var cls in _bundle.Classes)
{
    if (cls.SuperNameIndex.HasValue)
    {
        var superName = _bundle.Symbols[cls.SuperNameIndex.Value];
        if (!_bundle.Classes.Any(c => _bundle.Symbols[c.NameIndex] == superName))
            throw new InvalidOperationException($"Superclass '{superName}' not found");
    }
}

// Verify metaclass relationships
foreach (var meta in _bundle.Classes.Where(c => c.IsMetaclass))
{
    if (meta.MetaclassOfLocalId == null)
        throw new InvalidOperationException($"Metaclass '{meta.LocalId}' missing metaclassOf reference");
}
```

**Why First**: Fail fast before any object allocation if bundle is corrupted.

---

### Phase 1: Symbol Interning

**Purpose**: Create the symbol table - the foundation of Smalltalk's identity-based symbol system.

**Responsibility**: Symbol allocation only - establishes the global symbol namespace.

**Why This Phase is Critical**:
- Symbols must exist before classes because class names are symbols
- Symbols are identity-compared (same symbol = same OOP), not value-compared
- Every selector, class name, and instance variable name is a symbol

**Actions**:

1. For each symbol in `_bundle.Symbols`:
   ```csharp
   var symbolOOP = _allocator.AllocateString(symbolText);
   SymbolRegistry.RegisterSymbol(index, symbolOOP);
   ```

2. Store in SymbolRegistry with index mapping:
   ```
   Symbol[0] = "Object" → OOP 100
   Symbol[1] = "Array" → OOP 102
   Symbol[2] = "class" → OOP 104
   Symbol[3] = "superclass" → OOP 106
   ...
   ```

**Memory State After Phase 1**:
```
Symbol #Object (OOP 100):
  header[0] = class:String (will be set in Phase X)
  indexed[0] = 'O' (byte)
  indexed[1] = 'b' (byte)
  indexed[2] = 'j' (byte)
  indexed[3] = 'e' (byte)
  indexed[4] = 'c' (byte)
  indexed[5] = 't' (byte)
```

**Symbol Lookup**:
```csharp
// Later phases can now resolve symbols by index
var nameSymbolOOP = SymbolRegistry.GetSymbolOOP(cls.NameIndex);
```

---

### Phase 2: Class Shells

**Purpose**: Allocate skeleton class objects with ONLY their header (class reference) set.

**Responsibility**: Minimal class allocation - just enough structure for later phases to wire together.

**The Bootstrap Paradox**:
```
To create class:Object, we need its class (meta:Object)
To create meta:Object, we need its class (meta:Metaclass)
To create meta:Metaclass, we need Class and Metaclass to exist
```

**Solution**: Allocate all class shells first with minimal initialization.

**Actions**:

#### 2.1: Instance-Side Class Allocation

For each instance-side class (e.g., "Object", "Array", "Point"):

1. **Determine allocation strategy**:
   - Fundamental classes (Object, Class, etc.) may need fixed OOP positions
   - Regular classes can use dynamic allocation

2. **Calculate instance variable count**:
   ```csharp
   int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);
   ```
   This includes inherited instance variables from the entire superclass chain.

3. **Allocate class object**:
   ```csharp
   var classOOP = _allocator.AllocateClass(className, instVarCount);
   ```

   This internally:
   - Allocates memory for class object with appropriate size
   - Sets header slot 0 to its metaclass OOP (looked up by "meta:{className}")
   - Registers in GlobalRegistry as "class:{className}"
   - **Does NOT set instance variables** (no nil exists yet!)

4. **Register for later lookup**:
   ```csharp
   GlobalRegistry.Register($"class:{className}", classOOP);
   _localIdToObjectId[cls.LocalId] = classOOP;
   ```

#### 2.2: Metaclass Allocation

For each metaclass (e.g., "meta:Object", "meta:Array"):

1. **Calculate metaclass instance variable count**:
   ```csharp
   // All metaclasses are instances of Metaclass, which has 6 instance variables
   int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("Metaclass", _bundle);
   ```

2. **Allocate metaclass object**:
   ```csharp
   var metaOOP = _allocator.AllocateMetaclass(className, instVarCount);
   ```

   This internally:
   - Allocates memory for metaclass object
   - Sets header slot 0 to meta:Metaclass OOP
   - Registers in GlobalRegistry as "meta:{className}"
   - **Does NOT set instance variables**

3. **Register for later lookup**:
   ```csharp
   GlobalRegistry.Register($"meta:{className}", metaOOP);
   _localIdToObjectId[meta.LocalId] = metaOOP;
   ```

**Critical Constraint**: Instance variables are left uninitialized (undefined values) because nil doesn't exist yet.

**Memory State After Phase 2**:

```
class:Object (OOP 7836):
  header[0] = meta:Object (OOP 7838) ✓
  instVar[0] = ??? (superclass - uninitialized)
  instVar[1] = ??? (methodDict - uninitialized)
  instVar[2] = ??? (format - uninitialized)
  instVar[3] = ??? (name - uninitialized)
  instVar[4] = ??? (instanceVariables - uninitialized)
  instVar[5] = ??? (organization - uninitialized)

meta:Object (OOP 7838):
  header[0] = meta:Metaclass (OOP 8412) ✓
  instVar[0] = ??? (superclass - uninitialized)
  instVar[1] = ??? (methodDict - uninitialized)
  instVar[2] = ??? (format - uninitialized)
  instVar[3] = ??? (name - uninitialized)
  instVar[4] = ??? (instanceVariables - uninitialized)
  instVar[5] = ??? (organization - uninitialized)
  instVar[6] = ??? (thisClass - uninitialized)

class:Point (OOP 8528):
  header[0] = meta:Point (OOP 8530) ✓
  instVar[0] = ??? (superclass - uninitialized)
  instVar[1] = ??? (methodDict - uninitialized)
  instVar[2] = ??? (format - uninitialized)
  instVar[3] = ??? (name - uninitialized)
  instVar[4] = ??? (instanceVariables - uninitialized)
  instVar[5] = ??? (organization - uninitialized)

meta:Point (OOP 8530):
  header[0] = meta:Metaclass (OOP 8412) ✓
  instVar[0] = ??? (superclass - uninitialized)
  instVar[1] = ??? (methodDict - uninitialized)
  instVar[2] = ??? (format - uninitialized)
  instVar[3] = ??? (name - uninitialized)
  instVar[4] = ??? (instanceVariables - uninitialized)
  instVar[5] = ??? (organization - uninitialized)
  instVar[6] = ??? (thisClass - uninitialized)
```

**What We Have Now**:
- ✓ All classes exist as objects
- ✓ All metaclasses exist as objects
- ✓ Header slot 0 set (class reference)
- ✗ Instance variables uninitialized
- ✗ No inheritance relationships established
- ✗ No method dictionaries
- ✗ No class names set

---

### Phase 3: Metaclass Wiring

**Purpose**: Establish the bidirectional metaclass relationships and fundamental inheritance hierarchy.

**Responsibility**: Wire critical metaclass relationships ONLY - tracks all set fields to prevent Phase Y from overwriting.

**Why This Phase is Critical**:
- Enables method lookup through superclass chains
- Establishes the "metaclass circle" that allows Behavior methods to be inherited
- Sets up thisClass field for class variable lookup

**Critical Relationships Set**:

#### 3.1: Bidirectional Class ↔ Metaclass Links

For each metaclass/instance pair (e.g., meta:Point ↔ class:Point):

```csharp
if (cls.MetaclassOfLocalId is string instanceId)
{
    SmalltalkOOP metaOOP = _localIdToObjectId[cls.LocalId];
    SmalltalkOOP instOOP = _localIdToObjectId[instanceId];

    // Instance class → Metaclass (if class has 'class' instance variable)
    var instClassDef = FindClassDefinitionForObject(instOOP);
    if (instClassDef != null && FindInstanceVariableIndex(instClassDef, "class") != -1)
    {
        SetAndTrackInstanceVariable(instOOP, "class", metaOOP);
    }

    // Metaclass → Instance class (if metaclass has 'metaclassOf' instance variable)
    var metaClassDef = FindClassDefinitionForObject(metaOOP);
    if (metaClassDef != null && FindInstanceVariableIndex(metaClassDef, "metaclassOf") != -1)
    {
        SetAndTrackInstanceVariable(metaOOP, "metaclassOf", instOOP);
    }
}
```

**Field Tracking**: Each set field is recorded in `_initializedFields` HashSet:

```csharp
private void SetAndTrackInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value)
{
    _heapManager.SetInstanceVariable(objectOOP, fieldIndex, value);
    _initializedFields.Add((objectOOP.RawValue, fieldIndex));
}
```

This prevents Phase Y from overwriting these with nil.

#### 3.2: Critical thisClass Field

Every metaclass instance has a `thisClass` field (index 6 in Metaclass class) pointing to its instance-side class:

```csharp
// Find the thisClass field index in Metaclass
int thisClassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Metaclass", "thisClass", _bundle);

if (thisClassIndex >= 0)
{
    // For meta:Point:
    SetAndTrackInstanceVariable(metaOOP=8530, thisClassIndex=6, instOOP=8528);
}
```

**Why Critical**: Class variable lookup requires finding the instance-side class from a metaclass. Without thisClass, class variables (like `Float.Pi`) are inaccessible.

**Example**:
```
When executing: Float class >> Pi
1. Start with meta:Float (the class of Float class)
2. Need to find class variables in Float's classPool
3. Read meta:Float.thisClass → get class:Float
4. Read class:Float.classPool → get Dictionary with Pi binding
```

#### 3.3: Fundamental Metaclass Hierarchy

Creates the "metaclass circle" that allows Behavior methods to be inherited by all metaclasses:

```csharp
private void WireFundamentalMetaclassHierarchy()
{
    // Get OOPs for fundamental classes
    SmalltalkOOP metaObjectOOP = _localIdToObjectId["meta:Object"];
    SmalltalkOOP classClassOOP = _localIdToObjectId["class:Class"];
    SmalltalkOOP classDescriptionOOP = _localIdToObjectId["class:ClassDescription"];
    SmalltalkOOP classBehaviorOOP = _localIdToObjectId["class:Behavior"];
    SmalltalkOOP classObjectOOP = _localIdToObjectId["class:Object"];

    // Find superclass field index (inherited from Behavior through Class)
    int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);

    // Wire the metaclass circle
    SetAndTrackInstanceVariable(metaObjectOOP, superclassIndex, classClassOOP);
    SetAndTrackInstanceVariable(classClassOOP, superclassIndex, classDescriptionOOP);
    SetAndTrackInstanceVariable(classDescriptionOOP, superclassIndex, classBehaviorOOP);
    SetAndTrackInstanceVariable(classBehaviorOOP, superclassIndex, classObjectOOP);
}
```

**The Metaclass Circle**:
```
meta:Object.superclass = class:Class
class:Class.superclass = class:ClassDescription
class:ClassDescription.superclass = class:Behavior
class:Behavior.superclass = class:Object
```

**Why This Matters**:

When looking up a class-side method like `Point new`:
```
1. Start at meta:Point.methodDict - not found
2. Follow meta:Point.superclass → meta:Object - not found
3. Follow meta:Object.superclass → class:Class - not found
4. Follow class:Class.superclass → class:ClassDescription - not found
5. Follow class:ClassDescription.superclass → class:Behavior
6. Search class:Behavior.methodDict → FOUND 'new' method!
```

This allows class-side methods like `new` (defined in Behavior) to be inherited by all classes through the metaclass hierarchy.

#### 3.4: Computed Metaclass Superclasses

For each metaclass (e.g., meta:ActionMenu):

**Rule**: If metaclass M is the class of instance class I, and I has superclass S, then M's superclass is the metaclass of S.

```csharp
private void WireMetaclassSuperclasses()
{
    int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);

    foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
    {
        if (metaCls.MetaclassOfLocalId is not string instanceLocalId)
            continue;

        // Find the instance-side class descriptor
        var instanceCls = _bundle.Classes.FirstOrDefault(c => c.LocalId == instanceLocalId);
        if (instanceCls == null)
            continue;

        // If instance class has a superclass, set metaclass's superclass to superclass's metaclass
        if (instanceCls.SuperNameIndex is int superNameIdx)
        {
            string superName = _bundle.Symbols[superNameIdx];
            string superMetaLocalId = $"meta:{superName}";

            if (_localIdToObjectId.TryGetValue(superMetaLocalId, out SmalltalkOOP superMetaOOP))
            {
                SmalltalkOOP metaOOP = _localIdToObjectId[metaCls.LocalId];
                SetAndTrackInstanceVariable(metaOOP, superclassIndex, superMetaOOP);
            }
        }
    }
}
```

**Example**:
```
ActionMenu.superclass = PopUpMenu
→ ActionMenu class.superclass = PopUpMenu class

Point.superclass = Object
→ Point class.superclass = Object class
```

**All Fields Tracked**: Every `SetInstanceVariable` call adds to `_initializedFields`.

**Memory State After Phase 3**:

```
meta:Point (OOP 8530):
  header[0] = meta:Metaclass (OOP 8412)
  instVar[0] = meta:Object (superclass) ✓ TRACKED (8530, 0)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = ??? (format - still uninitialized)
  instVar[3] = ??? (name - still uninitialized)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)
  instVar[6] = class:Point (thisClass) ✓ TRACKED (8530, 6)

class:Object (OOP 7836):
  header[0] = meta:Object (OOP 7838)
  instVar[0] = ??? (superclass - Object has no superclass)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = ??? (format - still uninitialized)
  instVar[3] = ??? (name - still uninitialized)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)

meta:Object (OOP 7838):
  header[0] = meta:Metaclass (OOP 8412)
  instVar[0] = class:Class (superclass) ✓ TRACKED (7838, 0)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = ??? (format - still uninitialized)
  instVar[3] = ??? (name - still uninitialized)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)
  instVar[6] = class:Object (thisClass) ✓ TRACKED (7838, 6)

_initializedFields contains:
  (8530, 0)  // meta:Point.superclass
  (8530, 6)  // meta:Point.thisClass
  (7838, 0)  // meta:Object.superclass = class:Class
  (7838, 6)  // meta:Object.thisClass = class:Object
  (8052, 0)  // class:Class.superclass = class:ClassDescription
  (8072, 0)  // class:ClassDescription.superclass = class:Behavior
  (7936, 0)  // class:Behavior.superclass = class:Object
  ... (236 metaclass superclass relationships tracked)
```

**What We Have Now**:
- ✓ Superclass chains established (method lookup will work)
- ✓ thisClass fields set (class variable lookup will work)
- ✓ Metaclass circle wired (class-side methods inherited correctly)
- ✓ All set fields tracked in _initializedFields
- ✗ Class names, formats not set yet (Phase 4)
- ✗ Method dictionaries not set yet (Phases 7-9)
- ✗ Remaining instance variables still uninitialized (Phase Y)

---

### Phase 4: Class Resolution

**Purpose**: Set specific class-defining fields (name, format, classPool, superclass for instance classes).

**Responsibility**: Set class-specific metadata ONLY - tracks all set fields, does NOT initialize remaining fields to nil.

**Fields Set**:

#### 4.1: Class Name (all classes)

```csharp
string className = _bundle.Symbols[cls.NameIndex];
var nameSymbolOOP = SymbolRegistry.GetSymbolOOP(cls.NameIndex);
int nameFieldIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "name", _bundle);

SetInstanceVariableUsingPrimitive(classOOP, nameFieldIndex, nameSymbolOOP);
// Tracks: (classOOP, nameFieldIndex)
```

**Example**:
```
class:Point.name = #Point (Symbol OOP 234)
```

#### 4.2: Format Field (all classes)

Encodes the class's memory layout (pointers vs. bytes, indexed vs. fixed):

```csharp
int format = CalculateFormat(cls);
var formatOOP = SmalltalkOOP.FromSmallInteger(format);
int formatFieldIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "format", _bundle);

SetInstanceVariableUsingPrimitive(classOOP, formatFieldIndex, formatOOP);
// Tracks: (classOOP, formatFieldIndex)
```

**Format Encoding** (per Smalltalk-80 source expectations, differs from Blue Book):
- Bits 0-7: fixedFields (number of fixed instance variables, 0-255)
- Bits 8-11: reserved
- Bit 12: isIndexable (1=has indexed fields, 0=fixed size only)
- Bit 13: isWords (1=word-addressed, 0=byte-addressed)
- Bit 14: isPointers (1=contains pointers, 0=contains values)
- Bit 15: reserved

> **Note**: This matches what Smalltalk-80.st source code expects (instSize, isBits, isBytes, isVariable).

**Example**:
```
Point has 2 instance variables (x, y), fixed pointer format
format = 2 | 0x4000 = 16386 (decimal) = 0100 0000 0000 0010 (binary)
  Bits 0-7: 2 (fixedFields)
  Bit 14: 1 (isPointers)
```

#### 4.3: ClassPool Dictionary (instance classes only, if has class variables)

Allocates empty Dictionary for class variables:

```csharp
if (!cls.IsMetaclass && cls.ClassVarNames.Length > 0)
{
    var classPoolOOP = AllocateEmptyDictionary();
    int classPoolIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "classPool", _bundle);

    SetInstanceVariableUsingPrimitive(classOOP, classPoolIndex, classPoolOOP);
    // Tracks: (classOOP, classPoolIndex)
}
```

**Empty Dictionary State**:
```
Dictionary (OOP 10234):
  instVar[0] = 0 (tally - SmallInteger)
  instVar[1] = Array of keys (indexed slots uninitialized)
  instVar[2] = Array of values (indexed slots uninitialized)
```

**Important**: Dictionary indexed slots are uninitialized here. Phase Y will initialize them to nil.

#### 4.4: Superclass (instance classes only, if has superclass)

```csharp
if (!cls.IsMetaclass && cls.SuperNameIndex.HasValue)
{
    string superName = _bundle.Symbols[cls.SuperNameIndex.Value];
    string superLocalId = $"class:{superName}";
    var superOOP = _localIdToObjectId[superLocalId];
    int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);

    SetInstanceVariableUsingPrimitive(classOOP, superclassIndex, superOOP);
    // Tracks: (classOOP, superclassIndex)
}
```

**Critical**: Metaclass superclasses were already set in Phase 3 and are NOT touched here.

**Example**:
```
class:Point.superclass = class:Object (OOP 7836)
```

**Field Tracking Implementation**:

```csharp
private void SetInstanceVariableUsingPrimitive(SmalltalkOOP objectOOP, int zeroBasedIndex, SmalltalkOOP valueOOP)
{
    // Use primitive interface with 1-based indexing (Smalltalk convention)
    int oneBasedIndex = zeroBasedIndex + 1;
    _primitives.InstVarAtPut(objectOOP, oneBasedIndex, valueOOP);

    // Track this field as initialized
    _initializedFields.Add((objectOOP.RawValue, zeroBasedIndex));
}
```

**What Phase 4 Does NOT Do**:
- ❌ Does NOT initialize methodDict (Phase 7 allocates, Phase 9 populates)
- ❌ Does NOT initialize remaining fields to nil (Phase Y)
- ❌ Does NOT touch metaclass superclass (Phase 3 already set it)
- ❌ Does NOT call any "initialize all fields to nil" method

**Memory State After Phase 4**:

```
class:Point (OOP 8528):
  header[0] = meta:Point (OOP 8530)
  instVar[0] = class:Object (superclass) ✓ TRACKED (8528, 0)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = SmallInt(2) (format) ✓ TRACKED (8528, 2)
  instVar[3] = #Point (name) ✓ TRACKED (8528, 3)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)

class:Float (OOP 8248):
  header[0] = meta:Float (OOP 8250)
  instVar[0] = class:Number (superclass) ✓ TRACKED (8248, 0)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = SmallInt(1) (format) ✓ TRACKED (8248, 2)
  instVar[3] = #Float (name) ✓ TRACKED (8248, 3)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)
  instVar[6] = Dictionary (classPool) ✓ TRACKED (8248, 6)

meta:Point (OOP 8530):
  header[0] = meta:Metaclass (OOP 8412)
  instVar[0] = meta:Object (superclass) ✓ TRACKED in Phase 3 (8530, 0)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = SmallInt(7) (format) ✓ TRACKED (8530, 2)
  instVar[3] = #"Point class" (name) ✓ TRACKED (8530, 3)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)
  instVar[6] = class:Point (thisClass) ✓ TRACKED in Phase 3 (8530, 6)

_initializedFields now contains:
  ... (all Phase 3 entries) ...
  (8528, 0)  // class:Point.superclass
  (8528, 2)  // class:Point.format
  (8528, 3)  // class:Point.name
  (8248, 0)  // class:Float.superclass
  (8248, 2)  // class:Float.format
  (8248, 3)  // class:Float.name
  (8248, 6)  // class:Float.classPool
  (8530, 2)  // meta:Point.format
  (8530, 3)  // meta:Point.name
  ... (474 classes × ~3 fields each)
```

**What We Have Now**:
- ✓ Class names set (debugging works)
- ✓ Format fields set (object allocation knows layouts)
- ✓ Superclass chains complete (both instance and metaclass)
- ✓ ClassPool dictionaries allocated (empty, awaiting initialization)
- ✓ All set fields tracked in _initializedFields
- ✗ Method dictionaries not allocated yet (Phase 7)
- ✗ Remaining instance variables still uninitialized (Phase Y)

---

### Phase 5: Layout Computation

**Purpose**: Build runtime metadata for fast instance variable lookup.

**Responsibility**: Compute layouts and build lookup tables - no allocations, no heap mutations.

**Actions**:

For each class, compute the full instance variable layout including inherited variables:

```csharp
_layoutResolver = new LayoutResolver();
_layoutResolver.ComputeLayouts(_bundle.Classes, _bundle.Symbols);
```

**Layout Computation Algorithm**:

1. **Build superclass chain**:
   ```csharp
   List<ClassDescriptor> chain = new();
   ClassDescriptor? current = classDescriptor;
   while (current != null)
   {
       chain.Add(current);
       current = FindSuperclass(current);
   }
   chain.Reverse(); // Root → Subclass order
   ```

2. **Collect instance variables from root to subclass**:
   ```csharp
   List<string> allInstVars = new();
   foreach (var cls in chain)
   {
       foreach (int varIndex in cls.InstVarNames)
       {
           allInstVars.Add(_bundle.Symbols[varIndex]);
       }
   }
   ```

3. **Build index mapping**:
   ```csharp
   for (int i = 0; i < allInstVars.Count; i++)
   {
       _layouts[$"class:{className}"][allInstVars[i]] = i;
   }
   ```

**Example - Point class**:

```
Point instance variables: [x, y]
Object instance variables: []

Layout computation:
  Superclass chain: Object → Point
  Collected variables: [x, y]

LayoutResolver["class:Point"]["x"] = 0
LayoutResolver["class:Point"]["y"] = 1
```

**Example - ActionMenu class**:

```
ActionMenu instance variables: [actionSelectors]
PopUpMenu instance variables: [labels, lines]
View instance variables: [superView, subViews, transformation, ...]
Object instance variables: []

Layout computation:
  Superclass chain: Object → View → PopUpMenu → ActionMenu
  Collected variables: [superView, subViews, ..., labels, lines, actionSelectors]

LayoutResolver["class:ActionMenu"]["superView"] = 0
LayoutResolver["class:ActionMenu"]["subViews"] = 1
...
LayoutResolver["class:ActionMenu"]["labels"] = 15
LayoutResolver["class:ActionMenu"]["lines"] = 16
LayoutResolver["class:ActionMenu"]["actionSelectors"] = 17
```

**Why Needed**:

Fast instance variable access during execution:
```csharp
// Without LayoutResolver:
int fieldIndex = SlowWalkSuperclassChainToFindFieldIndex(classOOP, "x");

// With LayoutResolver:
int fieldIndex = _layoutResolver.GetFieldIndex("class:Point", "x"); // O(1) lookup
```

**Output**: LayoutResolver populated with all class layouts.

**No Heap Mutations**: This phase only builds lookup tables in memory.

---

### Phase 6: Object Allocation

**Purpose**: Allocate critical singleton objects and regular objects from JSON.

**Responsibility**: Allocate singletons (nil, true, false) and regular objects - instance variables left uninitialized.

**Critical Singletons Created**:

#### 6.1: nil (UndefinedObject instance)

```csharp
var nilOOP = _allocator.AllocateFixedObject("UndefinedObject");
GlobalRegistry.Register("nil", nilOOP);
```

**CRITICAL MILESTONE**: Nil now exists! Subsequent phases can initialize fields to nil.

**Memory State**:
```
nil (OOP 9254):
  header[0] = class:UndefinedObject (OOP will be set in Phase X)
  (UndefinedObject has 0 instance variables)
```

#### 6.2: true/false Singletons

```csharp
var trueOOP = _allocator.AllocateFixedObject("True");
GlobalRegistry.Register("true", trueOOP);

var falseOOP = _allocator.AllocateFixedObject("False");
GlobalRegistry.Register("false", falseOOP);
```

**Memory State**:
```
true (OOP 9256):
  header[0] = class:True (OOP will be set in Phase X)
  (True has 0 instance variables)

false (OOP 9258):
  header[0] = class:False (OOP will be set in Phase X)
  (False has 0 instance variables)
```

#### 6.3: ProcessorScheduler Singleton

```csharp
var schedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
GlobalRegistry.Register("ProcessorScheduler", schedulerOOP);
```

#### 6.4: Regular Objects from JSON

For each object in `_bundle.Objects`:

```csharp
string className = _bundle.Symbols[obj.ClassNameIndex];
var objectOOP = _allocator.AllocateFixedObject(className);
_localIdToObjectId[obj.LocalId] = objectOOP;
```

**Instance variables NOT set yet** - will be initialized in Phase Y.

**Example Objects**:
```json
{
  "localId": "obj:cursor-normal",
  "classNameIndex": 45,  // "Cursor"
  "fields": [
    { "type": "objectRef", "localId": "obj:cursor-mask-1" },
    { "type": "objectRef", "localId": "obj:cursor-offset-1" }
  ]
}
```

Becomes:
```
obj:cursor-normal (OOP 12345):
  header[0] = class:Cursor (will be set in Phase X)
  instVar[0] = ??? (mask - will be set in Phase Y)
  instVar[1] = ??? (offset - will be set in Phase Y)
```

**Memory State After Phase 6**:

```
nil (OOP 9254) EXISTS! ✓
true (OOP 9256) EXISTS! ✓
false (OOP 9258) EXISTS! ✓
ProcessorScheduler (OOP 9260) EXISTS! ✓

Regular objects allocated:
someObject (OOP 12345):
  header[0] = ??? (class reference not set yet)
  instVar[0] = ??? (can NOW be set to nil in Phase Y!)
  instVar[1] = ??? (can NOW be set to nil in Phase Y!)
```

**What We Have Now**:
- ✓ nil exists (can initialize fields to nil now!)
- ✓ true/false exist (boolean operations possible)
- ✓ All objects from JSON allocated
- ✗ Object class references not set yet (Phase X)
- ✗ Object instance variables not initialized yet (Phase Y)

---

### Phase X: Class Reference Initialization

**Purpose**: Set the class reference (header slot 0) for all objects now that their classes exist.

**Responsibility**: Set header slot 0 for all objects - does NOT set instance variables.

**Why Separate Phase**: In early phases, some objects were allocated before their class objects existed (forward references). Also, nil/true/false were allocated in Phase 6 before their classes had proper metadata.

**Actions**:

#### X.1: Set Class References for All Classes

For each class object:
```csharp
string className = _bundle.Symbols[cls.NameIndex];
var classOOP = _localIdToObjectId[cls.LocalId];

if (cls.IsMetaclass)
{
    // Metaclasses have class = meta:Metaclass
    var metaMetaclassOOP = GlobalRegistry.Get("meta:Metaclass");
    _allocator.SetObjectClass(classOOP, metaMetaclassOOP);
}
else
{
    // Instance classes have class = meta:{ClassName}
    string metaLocalId = $"meta:{className}";
    var metaclassOOP = _localIdToObjectId[metaLocalId];
    _allocator.SetObjectClass(classOOP, metaclassOOP);
}
```

**Example**:
```
class:Point.class = meta:Point
meta:Point.class = meta:Metaclass
```

#### X.2: Set Class References for Regular Objects

For each object from JSON:
```csharp
string className = _bundle.Symbols[obj.ClassNameIndex];
var objectOOP = _localIdToObjectId[obj.LocalId];
var classOOP = GlobalRegistry.LookupClassOOP(className);

_allocator.SetObjectClass(objectOOP, classOOP);
```

#### X.3: Set Class References for Singletons

```csharp
_allocator.SetObjectClass(GlobalRegistry.NilOOP, GlobalRegistry.LookupClassOOP("UndefinedObject"));
_allocator.SetObjectClass(GlobalRegistry.TrueOOP, GlobalRegistry.LookupClassOOP("True"));
_allocator.SetObjectClass(GlobalRegistry.FalseOOP, GlobalRegistry.LookupClassOOP("False"));
_allocator.SetObjectClass(GlobalRegistry.SchedulerOOP, GlobalRegistry.LookupClassOOP("ProcessorScheduler"));
```

**SetObjectClass Implementation**:

```csharp
public void SetObjectClass(SmalltalkOOP objectOOP, SmalltalkOOP classOOP)
{
    _heapManager.SetSlot(objectOOP, 0, classOOP); // Header slot 0
}
```

**Critical Distinction**:
- `SetSlot(obj, 0)` sets memory offset 0 (header - class reference)
- `SetInstanceVariable(obj, 0)` sets memory offset 4+ (first instance variable)
- These are DIFFERENT address spaces

**NOT Tracked in _initializedFields**: Header slot 0 is separate from instance variables, so it's not tracked. Phase Y only initializes instance variables, never touches header.

**Memory State After Phase X**:

```
class:Point (OOP 8528):
  header[0] = meta:Point (OOP 8530) ✓ SET
  instVar[0] = class:Object (superclass - from Phase 4)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = SmallInt(2) (format - from Phase 4)
  instVar[3] = #Point (name - from Phase 4)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)

meta:Point (OOP 8530):
  header[0] = meta:Metaclass (OOP 8412) ✓ SET
  instVar[0] = meta:Object (superclass - from Phase 3)
  instVar[1] = ??? (methodDict - still uninitialized)
  instVar[2] = SmallInt(7) (format - from Phase 4)
  instVar[3] = #"Point class" (name - from Phase 4)
  instVar[4] = ??? (instanceVariables - still uninitialized)
  instVar[5] = ??? (organization - still uninitialized)
  instVar[6] = class:Point (thisClass - from Phase 3)

nil (OOP 9254):
  header[0] = class:UndefinedObject ✓ SET
  (no instance variables)

someObject (OOP 12345):
  header[0] = class:SomeClass ✓ SET
  instVar[0] = ??? (will be set to nil in Phase Y)
  instVar[1] = ??? (will be set to nil in Phase Y)
```

**What We Have Now**:
- ✓ All objects have correct class references
- ✓ GetClassName() debugging works
- ✓ GC traversal can read object headers
- ✗ Instance variables still mostly uninitialized (Phase Y next)

---

### Phase Y: Instance Variable Initialization

**Purpose**: Initialize ALL remaining uninitialized instance variables to nil.

**Responsibility**: Set all untouched instance variable fields to nil - NEVER overwrites fields set by prior phases.

**The Core Challenge**:

We need to set all untouched fields to nil, but we've already set some fields in Phase 3 and Phase 4. We cannot use `RawValue == 0` to detect uninitialized fields because **OOP 0 is valid**.

**Example of Why OOP 0 is Valid**:
```
During cold start, objects are allocated sequentially:
  First object allocated → OOP 0 (perfectly valid!)
  Second object allocated → OOP 2
  Third object allocated → OOP 4
  ...

If we checked `if (fieldValue.RawValue == 0)` to mean "uninitialized":
  someObject.field = OOP 0 (reference to first object)
  Phase Y sees: RawValue == 0
  Phase Y INCORRECTLY thinks: "uninitialized, set to nil"
  Phase Y overwrites valid OOP 0 with nil → BUG!
```

**Solution**: Use the `_initializedFields` HashSet as the authoritative source of truth.

**For Each Object Type**:

#### Y.1: Class Objects

```csharp
foreach (var cls in _bundle.Classes)
{
    if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
        continue;

    // Get the number of instance variables for this type of class object
    string classOfClassName = cls.IsMetaclass ? "Metaclass" : "Class";
    int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(classOfClassName, _bundle);

    // Initialize ONLY uninitialized instance variables to nil
    for (int i = 0; i < instVarCount; i++)
    {
        if (!_initializedFields.Contains((classOOP.RawValue, i))) // Only set if not already initialized
        {
            _allocator.SetInstanceVariable(classOOP, i, nilOOP);
            instanceVariablesInitialized++;
        }
    }
}
```

**Example - meta:Point**:
```
meta:Point (OOP 8530) has 7 instance variables (from Metaclass):
  instVar[0] = meta:Object (Phase 3) → SKIP (8530, 0) in _initializedFields
  instVar[1] = uninitialized → SET TO NIL (8530, 1) NOT in _initializedFields
  instVar[2] = SmallInt(7) (Phase 4) → SKIP (8530, 2) in _initializedFields
  instVar[3] = #"Point class" (Phase 4) → SKIP (8530, 3) in _initializedFields
  instVar[4] = uninitialized → SET TO NIL (8530, 4) NOT in _initializedFields
  instVar[5] = uninitialized → SET TO NIL (8530, 5) NOT in _initializedFields
  instVar[6] = class:Point (Phase 3) → SKIP (8530, 6) in _initializedFields
```

**Example - class:Point**:
```
class:Point (OOP 8528) has 6 instance variables (from Class):
  instVar[0] = class:Object (Phase 4) → SKIP (8528, 0) in _initializedFields
  instVar[1] = uninitialized → SET TO NIL (8528, 1) NOT in _initializedFields
  instVar[2] = SmallInt(2) (Phase 4) → SKIP (8528, 2) in _initializedFields
  instVar[3] = #Point (Phase 4) → SKIP (8528, 3) in _initializedFields
  instVar[4] = uninitialized → SET TO NIL (8528, 4) NOT in _initializedFields
  instVar[5] = uninitialized → SET TO NIL (8528, 5) NOT in _initializedFields
```

#### Y.2: Singleton Objects

```csharp
private void InitializeSingletonInstanceVariables(string globalName, string className, SmalltalkOOP nilOOP)
{
    if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
    {
        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);

        // Only initialize uninitialized instance variables
        for (int i = 0; i < instVarCount; i++)
        {
            if (!_initializedFields.Contains((objectId.RawValue, i)))
            {
                _allocator.SetInstanceVariable(objectId, i, nilOOP);
            }
        }
    }
}

// Called for each singleton:
InitializeSingletonInstanceVariables("nil", "UndefinedObject", nilOOP);
InitializeSingletonInstanceVariables("true", "True", nilOOP);
InitializeSingletonInstanceVariables("false", "False", nilOOP);
InitializeSingletonInstanceVariables("ProcessorScheduler", "ProcessorScheduler", nilOOP);
```

**Example - ProcessorScheduler**:
```
ProcessorScheduler has 2 instance variables: [quiescentProcessLists, activeProcess]
Both are uninitialized → SET TO NIL
```

#### Y.3: Regular Objects from JSON

For objects defined in JSON with specific field values:

```csharp
foreach (var obj in _bundle.Objects)
{
    if (!_localIdToObjectId.TryGetValue(obj.LocalId, out SmalltalkOOP objectOOP))
        continue;

    string className = _bundle.Symbols[obj.ClassNameIndex];
    int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);

    // First: Initialize ALL instance variables to nil
    for (int i = 0; i < instVarCount; i++)
    {
        _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
        instanceVariablesInitialized++;
    }

    // Second: Override with JSON field values
    for (int i = 0; i < obj.Fields.Length; i++)
    {
        var valueOOP = ResolveFieldValueAsOOP(obj.Fields[i]);
        _allocator.SetInstanceVariable(objectOOP, i, valueOOP);
    }

    // Third: Set indexed fields if any
    for (int i = 0; i < obj.IndexedFields.Length; i++)
    {
        var valueOOP = ResolveFieldValueAsOOP(obj.IndexedFields[i]);
        _allocator.SetIndexedOOP(objectOOP, i, valueOOP);
    }
}
```

**Why Initialize All Then Override**:

Objects may have more instance variables than JSON fields (inherited variables). Initialize all to nil first, then set specific values.

**Example - Cursor Object**:
```json
{
  "localId": "obj:cursor-normal",
  "classNameIndex": 45,  // "Cursor"
  "fields": [
    { "type": "objectRef", "localId": "obj:cursor-mask-1" },
    { "type": "objectRef", "localId": "obj:cursor-offset-1" }
  ]
}
```

Cursor inherits from Object:
```
Object: (no instance variables)
Cursor: [mask, offset]
Total: 2 instance variables
```

Processing:
```
1. Initialize all to nil:
   instVar[0] = nil
   instVar[1] = nil

2. Override with JSON values:
   instVar[0] = obj:cursor-mask-1 (OOP 12346)
   instVar[1] = obj:cursor-offset-1 (OOP 12347)
```

#### Y.4: SystemDictionary and Special Objects

```csharp
private void InitializeSpecialObjectInstanceVariables(SmalltalkOOP nilOOP)
{
    // Initialize SystemDictionary instance variables
    if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictId))
    {
        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("SystemDictionary", _bundle);

        // Only initialize uninitialized instance variables
        for (int i = 0; i < instVarCount; i++)
        {
            if (!_initializedFields.Contains((systemDictId.RawValue, i)))
            {
                _allocator.SetInstanceVariable(systemDictId, i, nilOOP);
            }
        }
    }
}
```

#### Y.5: Dictionary Indexed Slots

ClassPool dictionaries allocated in Phase 4 have uninitialized indexed slots:

```csharp
private void InitializeDictionaryIndexedSlots(SmalltalkOOP nilOOP)
{
    var dictClassOOP = GlobalRegistry.Get("class:Dictionary");

    // Iterate through all class objects
    foreach (var cls in _bundle.Classes)
    {
        if (cls.IsMetaclass)
            continue; // Metaclasses don't have classPool

        if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            continue;

        // Find classPool instance variable
        int classPoolIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "classPool", _bundle);
        if (classPoolIndex < 0)
            continue;

        // Get the classPool Dictionary OOP
        var classPoolOOP = _allocator.GetInstanceVariable(classOOP, classPoolIndex);
        if (classPoolOOP.RawValue == 0)
            continue; // No classPool allocated (class has no class variables)

        // Check if this object is a Dictionary
        var objectClassOOP = _heapManager.GetClassOOP(classPoolOOP);
        if (objectClassOOP.RawValue != dictClassOOP.RawValue)
            continue;

        // Initialize all indexed slots to nil
        int indexedSize = _allocator.GetIndexedSize(classPoolOOP);
        for (int i = 0; i < indexedSize; i++)
        {
            _allocator.SetIndexedOOP(classPoolOOP, i, nilOOP);
        }
    }
}
```

**No Tracking Needed for Indexed Slots**: Indexed slots are separate from instance variables and can only be initialized here. There's no risk of overwriting previous work.

**Example - Float classPool Dictionary**:
```
Float.classPool = Dictionary (OOP 10234)

Dictionary structure:
  instVar[0] = 0 (tally)
  instVar[1] = keys Array (OOP 10236)
  instVar[2] = values Array (OOP 10238)

keys Array (OOP 10236):
  indexed[0] = ??? → SET TO NIL
  indexed[1] = ??? → SET TO NIL
  indexed[2] = ??? → SET TO NIL
  ... (all 32 slots set to nil)

values Array (OOP 10238):
  indexed[0] = ??? → SET TO NIL
  indexed[1] = ??? → SET TO NIL
  indexed[2] = ??? → SET TO NIL
  ... (all 32 slots set to nil)
```

**Memory State After Phase Y**:

```
meta:Point (OOP 8530):
  header[0] = meta:Metaclass
  instVar[0] = meta:Object (from Phase 3) ✓
  instVar[1] = nil (Phase Y) ✓
  instVar[2] = SmallInt(7) (from Phase 4) ✓
  instVar[3] = #"Point class" (from Phase 4) ✓
  instVar[4] = nil (Phase Y) ✓
  instVar[5] = nil (Phase Y) ✓
  instVar[6] = class:Point (from Phase 3) ✓

class:Point (OOP 8528):
  header[0] = meta:Point
  instVar[0] = class:Object (from Phase 4) ✓
  instVar[1] = nil (Phase Y) ✓ (methodDict will be set in Phase 9)
  instVar[2] = SmallInt(2) (from Phase 4) ✓
  instVar[3] = #Point (from Phase 4) ✓
  instVar[4] = nil (Phase Y) ✓
  instVar[5] = nil (Phase Y) ✓

class:Float.classPool Dictionary:
  instVar[0] = 0 (tally)
  instVar[1] = keys Array
  instVar[2] = values Array

  keys Array indexed slots: ALL nil ✓
  values Array indexed slots: ALL nil ✓

obj:cursor-normal (OOP 12345):
  header[0] = class:Cursor
  instVar[0] = obj:cursor-mask-1 (from JSON) ✓
  instVar[1] = obj:cursor-offset-1 (from JSON) ✓
```

**ALL objects now have valid state** - no uninitialized fields!

**What We Have Now**:
- ✓ All instance variables either set to meaningful values OR nil
- ✓ No uninitialized memory
- ✓ Phase 3 work preserved (superclass, thisClass)
- ✓ Phase 4 work preserved (name, format, classPool, superclass)
- ✓ Dictionary indexed slots initialized
- ✓ JSON object field values set
- ✗ Method dictionaries still empty (Phases 7-9 next)

---

### Phase 7: Method Dictionary Allocation

**Purpose**: Allocate empty MethodDictionary objects for each class.

**Responsibility**: Allocate methodDict structures ONLY - does NOT populate them with methods.

**Why Separate from Phase 9**: Separates allocation from population for cleaner phase boundaries.

**Actions**:

For each class:

```csharp
int methodCount = cls.Methods.Length;
int capacity = CalculateDictionaryCapacity(methodCount);

// Get MethodDictionary class OOP
var methodDictClassOOP = GlobalRegistry.LookupClassOOP("MethodDictionary");

// Allocate methodDict array
var methodDictOOP = _allocator.AllocateArray(methodDictClassOOP, capacity);

// Allocate keys and values arrays
var keysArrayOOP = _allocator.AllocateArray(GlobalRegistry.Get("class:Array"), capacity);
var valuesArrayOOP = _allocator.AllocateArray(GlobalRegistry.Get("class:Array"), capacity);

// Initialize MethodDictionary structure
int tallyIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "tally");
int keysIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "keys");
int valuesIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "values");

_allocator.SetInstanceVariable(methodDictOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(0));
_allocator.SetInstanceVariable(methodDictOOP, keysIndex, keysArrayOOP);
_allocator.SetInstanceVariable(methodDictOOP, valuesIndex, valuesArrayOOP);

// Install in class
int methodDictFieldIndex = _layoutResolver.GetFieldIndex(classTypeName, "methodDict");
_allocator.SetInstanceVariable(classOOP, methodDictFieldIndex, methodDictOOP);
```

**Dictionary Capacity Calculation**:
```csharp
private int CalculateDictionaryCapacity(int methodCount)
{
    // Use next power of 2 that's at least 2x the method count
    int capacity = 8;
    while (capacity < methodCount * 2)
        capacity *= 2;
    return capacity;
}
```

**Example - Point class with 15 methods**:
```
methodCount = 15
capacity = 32 (next power of 2 >= 30)

MethodDictionary (OOP 10240):
  header[0] = class:MethodDictionary
  instVar[0] = 0 (tally - SmallInteger)
  instVar[1] = keys Array (OOP 10242)
  instVar[2] = values Array (OOP 10244)

keys Array (OOP 10242):
  header[0] = class:Array
  indexed[0] = nil
  indexed[1] = nil
  ... (all 32 slots = nil)

values Array (OOP 10244):
  header[0] = class:Array
  indexed[0] = nil
  indexed[1] = nil
  ... (all 32 slots = nil)

class:Point.methodDict = MethodDictionary (OOP 10240)
```

**Memory State After Phase 7**:

```
class:Point (OOP 8528):
  header[0] = meta:Point
  instVar[0] = class:Object (superclass)
  instVar[1] = MethodDictionary (OOP 10240) ✓ NOW SET
  instVar[2] = SmallInt(2) (format)
  instVar[3] = #Point (name)
  instVar[4] = nil
  instVar[5] = nil

MethodDictionary is allocated but EMPTY (tally=0, all slots nil)
```

**What We Have Now**:
- ✓ All classes have methodDict allocated
- ✓ MethodDict structure initialized (tally, keys, values)
- ✗ No methods installed yet (Phase 8-9)

---

### Phase 8: Method Installation

**Purpose**: Allocate and populate CompiledMethod objects.

**Responsibility**: Create CompiledMethod objects with literals and bytecodes - does NOT install in methodDicts yet.

**CompiledMethod Structure** (heterogeneous design):

```
CompiledMethod (PointerIndexed):
  indexed[0] = header (SmallInteger encoding primitive, arg count, temp count, etc.)
  indexed[1] = literal1 (OOP)
  indexed[2] = literal2 (OOP)
  ...
  indexed[N] = literalN (OOP)
  indexed[N+1] = bytecodeArray (OOP to separate ByteArray)
```

**Why Separate ByteArray**: C# cannot easily handle heterogeneous objects with mixed pointer/byte data. Solution: Store bytecodes in separate ByteArray, reference from method.

**Actions**:

For each method in each class:

```csharp
// 1. Resolve all literals to OOPs
var literalOOPs = new SmalltalkOOP[method.Literals.Length];
for (int i = 0; i < method.Literals.Length; i++)
{
    literalOOPs[i] = ResolveLiteralValueAsOOP(method.Literals[i]);
}

// 2. Allocate CompiledMethod with literals
var methodOOP = _allocator.AllocateCompiledMethod(
    className,
    primitiveIndex: method.PrimitiveIndex,
    numArgs: method.NumArgs,
    numTemps: method.NumTemps,
    literalOOPs: literalOOPs,
    bytecodes: method.Bytecodes
);

// 3. Store for later installation
_localIdToObjectId[method.LocalId] = methodOOP;
```

**Literal Resolution**:

Literals can be:
- SmallIntegers (immediate values)
- Symbols (from symbol table)
- Associations (global variable bindings)
- Arrays (literal arrays)
- Other objects

```csharp
private SmalltalkOOP ResolveLiteralValueAsOOP(LiteralValue literal)
{
    return literal.Type switch
    {
        "smallInt" => SmalltalkOOP.FromSmallInteger(literal.IntValue),
        "symbol" => SymbolRegistry.GetSymbolOOP(literal.SymbolIndex),
        "association" => AllocateOrGetCanonicalAssociation(literal.Key),
        "array" => AllocateArray(literal.Elements),
        "nil" => GlobalRegistry.NilOOP,
        "true" => GlobalRegistry.TrueOOP,
        "false" => GlobalRegistry.FalseOOP,
        _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
    };
}
```

**Association Canonicalization**:

Critical optimization - ensures only one Association object exists per global variable:

```csharp
private readonly Dictionary<string, SmalltalkOOP> _canonicalAssociations = new();

private SmalltalkOOP AllocateOrGetCanonicalAssociation(string key)
{
    // Check if we already created an Association for this key
    if (_canonicalAssociations.TryGetValue(key, out var existingOOP))
        return existingOOP;

    // Create new Association
    var keyOOP = SymbolRegistry.GetSymbolOOP(key);
    var associationOOP = _allocator.AllocateFixedObject("Association");

    // Initialize Association
    _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);  // key
    _allocator.SetInstanceVariable(associationOOP, 1, GlobalRegistry.NilOOP);  // value (initially nil)

    // Cache for future use
    _canonicalAssociations[key] = associationOOP;
    return associationOOP;
}
```

**Why Canonicalization Matters**:

Without canonicalization:
```
Method1 references global 'Smalltalk' → allocates Association #1
Method2 references global 'Smalltalk' → allocates Association #2
Method3 references global 'Smalltalk' → allocates Association #3
...
1000 methods reference 'Smalltalk' → 1000 duplicate Association objects!
```

With canonicalization:
```
Method1 references global 'Smalltalk' → allocates Association #1
Method2 references global 'Smalltalk' → reuses Association #1
Method3 references global 'Smalltalk' → reuses Association #1
...
1000 methods reference 'Smalltalk' → 1 Association object shared by all!
```

**Memory Savings**:
```
Total Association literals: 3725
Canonical Associations created: 691
Duplicate Associations avoided: 3034
Canonicalization ratio: 18.6%
Memory reduction: 81.4%
```

**Example - Point >> x Method**:

```smalltalk
x
    "Answer the receiver's x coordinate"
    ^x
```

Compiled to:
```json
{
  "localId": "method:Point>>x",
  "selectorIndex": 234,  // "x"
  "numArgs": 0,
  "numTemps": 0,
  "primitiveIndex": 0,
  "literals": [],
  "bytecodes": [0, 124]  // push instVar 0, return top
}
```

Becomes:
```
CompiledMethod (OOP 15234):
  header[0] = SmallInt(header encoding)
  indexed[0] = SmallInt(0) (no literals marker)
  indexed[1] = ByteArray (OOP 15236)

ByteArray (OOP 15236):
  header[0] = class:ByteArray
  indexed[0] = 0 (push instVar 0)
  indexed[1] = 124 (return top)
```

**Memory State After Phase 8**:

```
All CompiledMethod objects allocated:
  method:Point>>x (OOP 15234)
  method:Point>>y (OOP 15240)
  method:Point>>setX:setY: (OOP 15246)
  ... (10,059 methods total)

All methods stored in _localIdToObjectId for Phase 9 installation
```

**What We Have Now**:
- ✓ All CompiledMethod objects created
- ✓ All literals resolved
- ✓ All bytecodes stored
- ✓ Association canonicalization complete
- ✗ Methods not installed in methodDicts yet (Phase 9)

---

### Phase 9: Method Dictionary Installation

**Purpose**: Populate each class's methodDict with its compiled methods.

**Responsibility**: Install methods into hash tables - uses open addressing with linear probing.

**Actions**:

For each class:

```csharp
// Get the methodDict allocated in Phase 7
int methodDictIndex = _layoutResolver.GetFieldIndex(classTypeName, "methodDict");
var methodDictOOP = _allocator.GetInstanceVariable(classOOP, methodDictIndex);

// Get keys and values arrays
int keysIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "keys");
int valuesIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "values");
int tallyIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "tally");

var keysArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, keysIndex);
var valuesArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, valuesIndex);

int capacity = _allocator.GetIndexedSize(keysArrayOOP);
int methodCount = 0;

// Install each method
foreach (var method in cls.Methods)
{
    string selector = _bundle.Symbols[method.SelectorIndex];
    var selectorOOP = SymbolRegistry.GetSymbolOOP(selector);
    var methodOOP = _localIdToObjectId[method.LocalId];

    // Compute hash index
    int index = ComputeHashIndex(selector, capacity);

    // Linear probing to find empty slot
    while (true)
    {
        var keyOOP = _allocator.GetIndexedOOP(keysArrayOOP, index);
        if (keyOOP.RawValue == GlobalRegistry.NilOOP.RawValue)
        {
            // Found empty slot
            _allocator.SetIndexedOOP(keysArrayOOP, index, selectorOOP);
            _allocator.SetIndexedOOP(valuesArrayOOP, index, methodOOP);
            methodCount++;
            break;
        }

        // Collision - try next slot
        index = (index + 1) % capacity;
    }
}

// Update tally
_allocator.SetInstanceVariable(methodDictOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(methodCount));
```

**Hash Function**:

```csharp
private int ComputeHashIndex(string selector, int capacity)
{
    int hash = 0;
    foreach (char c in selector)
    {
        hash = (hash * 31 + c) & 0x7FFFFFFF; // Keep positive
    }
    return hash % capacity;
}
```

**Example - Point class MethodDictionary**:

```
Capacity: 32
Methods: 15

Installing "x" (hash=24):
  keys[24] = #x
  values[24] = method:Point>>x (OOP 15234)

Installing "y" (hash=25):
  keys[25] = #y
  values[25] = method:Point>>y (OOP 15240)

Installing "setX:setY:" (hash=7):
  keys[7] = #setX:setY:
  values[7] = method:Point>>setX:setY: (OOP 15246)

... (15 methods total)

Final state:
  tally = 15
  keys Array has 15 non-nil slots (selectors)
  values Array has 15 non-nil slots (methods)
  17 slots remain nil (for future growth)
```

**Method Lookup at Runtime**:

```csharp
public SmalltalkOOP? GetMethod(SmalltalkOOP classOOP, string selector)
{
    int methodDictIndex = _layoutResolver.GetFieldIndex(className, "methodDict");
    var methodDictOOP = _allocator.GetInstanceVariable(classOOP, methodDictIndex);

    int keysIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "keys");
    int valuesIndex = _layoutResolver.GetFieldIndex("class:MethodDictionary", "values");

    var keysArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, keysIndex);
    var valuesArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, valuesIndex);

    int capacity = _allocator.GetIndexedSize(keysArrayOOP);
    int index = ComputeHashIndex(selector, capacity);

    // Linear probing to find selector
    for (int probe = 0; probe < capacity; probe++)
    {
        var keyOOP = _allocator.GetIndexedOOP(keysArrayOOP, index);

        if (keyOOP.RawValue == GlobalRegistry.NilOOP.RawValue)
            return null; // Not found

        string keySelector = _allocator.GetSymbolString(keyOOP);
        if (keySelector == selector)
        {
            // Found!
            return _allocator.GetIndexedOOP(valuesArrayOOP, index);
        }

        index = (index + 1) % capacity;
    }

    return null; // Not found after full table scan
}
```

**Memory State After Phase 9**:

```
class:Point (OOP 8528):
  header[0] = meta:Point
  instVar[0] = class:Object (superclass)
  instVar[1] = MethodDictionary (OOP 10240) ✓ NOW POPULATED
  instVar[2] = SmallInt(2) (format)
  instVar[3] = #Point (name)
  instVar[4] = nil
  instVar[5] = nil

MethodDictionary (OOP 10240):
  instVar[0] = 15 (tally) ✓
  instVar[1] = keys Array (OOP 10242) ✓
  instVar[2] = values Array (OOP 10244) ✓

keys Array (OOP 10242):
  indexed[7] = #setX:setY:
  indexed[24] = #x
  indexed[25] = #y
  ... (15 selectors total, 17 nil slots)

values Array (OOP 10244):
  indexed[7] = method:Point>>setX:setY:
  indexed[24] = method:Point>>x
  indexed[25] = method:Point>>y
  ... (15 methods total, 17 nil slots)
```

**What We Have Now**:
- ✓ All methodDicts populated
- ✓ Method lookup fully operational
- ✓ Hash table collision resolution working
- ✓ All 10,059 methods installed

---

### Phase 10: Patch Application

**Purpose**: Apply post-initialization fixes specified in JSON.

**Responsibility**: Apply patches ONLY - fixes circular references and special cases not handled during normal initialization.

**Why Needed**: Some relationships can't be set during initial allocation due to forward references or circular dependencies.

**Patch Structure**:

```json
{
  "patches": [
    {
      "objectLocalId": "class:Point",
      "field": "superclass",
      "value": { "type": "objectRef", "localId": "class:Object" }
    },
    {
      "objectLocalId": "obj:someArray",
      "indexedSlot": 5,
      "value": { "type": "objectRef", "localId": "obj:circularRef" }
    }
  ]
}
```

**Actions**:

```csharp
foreach (var patch in _bundle.Patches)
{
    var objectOOP = _localIdToObjectId[patch.ObjectLocalId];
    var valueOOP = ResolveFieldValue(patch.Value);

    if (patch.Field != null)
    {
        // Instance variable patch
        string className = GetClassName(objectOOP);
        int fieldIndex = _layoutResolver.GetFieldIndex(className, patch.Field);
        _allocator.SetInstanceVariable(objectOOP, fieldIndex, valueOOP);
    }
    else if (patch.IndexedSlot.HasValue)
    {
        // Indexed slot patch
        _allocator.SetIndexedOOP(objectOOP, patch.IndexedSlot.Value, valueOOP);
    }
}
```

**Example Patches**:

1. **Circular Reference Fix**:
   ```
   Object A references Object B
   Object B references Object A

   During allocation, A is created first with B unset
   Patch: A.field = B (now that B exists)
   ```

2. **Special Object Override**:
   ```
   Some classes need special singleton behavior
   Patch: override default nil with specific object
   ```

**Memory State After Phase 10**:

All circular references and special cases resolved.

---

### Phase 11: USTable Population

**Purpose**: Populate the global symbol table (UniversalSymbolTable).

**Responsibility**: Install all symbols in USTable - makes symbols globally accessible.

**Why Needed**: Runtime code expects to find all symbols in the USTable for dynamic symbol lookup.

**Actions**:

```csharp
var usTableOOP = GlobalRegistry.USTableOOP;

for (int i = 0; i < _bundle.Symbols.Length; i++)
{
    var symbolOOP = SymbolRegistry.GetSymbolOOP(i);
    _allocator.SetIndexedOOP(usTableOOP, i, symbolOOP);
}
```

**Example**:

```
USTable (Array) (OOP 7828):
  indexed[0] = #Object (Symbol OOP 100)
  indexed[1] = #Array (Symbol OOP 102)
  indexed[2] = #class (Symbol OOP 104)
  indexed[3] = #superclass (Symbol OOP 106)
  ... (3914 symbols total)
```

**Memory State After Phase 11**:

All symbols reachable from USTable.

---

### Phase 12: Global Variable Installation

**Purpose**: Install global variables into the SystemDictionary (Smalltalk).

**Responsibility**: Install classes, singletons, and global objects in Smalltalk dictionary.

**Why Needed**: Makes all classes and globals accessible via `Smalltalk at: #ClassName`.

**Actions**:

```csharp
var smalltalkOOP = GlobalRegistry.SystemDictionaryOOP;

// Install classes
foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
{
    string name = _bundle.Symbols[cls.NameIndex];
    var classOOP = _localIdToObjectId[cls.LocalId];
    InstallGlobal(smalltalkOOP, name, classOOP);
}

// Install singletons
InstallGlobal(smalltalkOOP, "nil", GlobalRegistry.NilOOP);
InstallGlobal(smalltalkOOP, "true", GlobalRegistry.TrueOOP);
InstallGlobal(smalltalkOOP, "false", GlobalRegistry.FalseOOP);
InstallGlobal(smalltalkOOP, "Smalltalk", smalltalkOOP); // Self-reference

// Install special objects
InstallGlobal(smalltalkOOP, "Processor", GlobalRegistry.SchedulerOOP);
```

**InstallGlobal Implementation**:

```csharp
private void InstallGlobal(SmalltalkOOP dictionaryOOP, string key, SmalltalkOOP valueOOP)
{
    // Get or create Association for this key
    var keySymbolOOP = SymbolRegistry.GetSymbolOOP(key);
    var associationOOP = GetOrCreateAssociation(keySymbolOOP, valueOOP);

    // Install in dictionary using hash table
    int capacity = GetDictionaryCapacity(dictionaryOOP);
    int index = ComputeHashIndex(key, capacity);

    // Find empty slot (linear probing)
    var keysArrayOOP = GetDictionaryKeys(dictionaryOOP);
    while (GetIndexedOOP(keysArrayOOP, index).RawValue != NilOOP.RawValue)
    {
        index = (index + 1) % capacity;
    }

    // Install association
    SetIndexedOOP(keysArrayOOP, index, associationOOP);
    IncrementDictionaryTally(dictionaryOOP);
}
```

**Example**:

```
Smalltalk (SystemDictionary):
  ... hash table with Associations ...

  Association for 'Object':
    key = #Object
    value = class:Object (OOP 7836)

  Association for 'Point':
    key = #Point
    value = class:Point (OOP 8528)

  Association for 'nil':
    key = #nil
    value = nil (OOP 9254)

  ... (474 classes + singletons + globals)
```

**Memory State After Phase 12**:

All classes and globals reachable from `Smalltalk` dictionary.

---

### Phase 13: Comprehensive Validation

**Purpose**: Verify the heap is fully initialized and matches JSON specification.

**Responsibility**: Validate heap integrity - no mutations, only checks.

**Validations**:

#### 13.1: Class Validation

```csharp
foreach (var cls in _bundle.Classes)
{
    var classOOP = _localIdToObjectId[cls.LocalId];

    // Verify class has methodDict
    var methodDictOOP = GetInstanceVariable(classOOP, methodDictIndex);
    if (methodDictOOP.RawValue == NilOOP.RawValue)
        throw new InvalidOperationException($"Class {cls.LocalId} has nil methodDict");

    // Verify methodDict tally matches method count
    int tally = GetDictionaryTally(methodDictOOP);
    if (tally != cls.Methods.Length)
        throw new InvalidOperationException($"Class {cls.LocalId} tally mismatch");
}
```

#### 13.2: Method Validation

```csharp
foreach (var method in allMethods)
{
    var methodOOP = _localIdToObjectId[method.LocalId];

    // Verify method has bytecodes
    var bytecodeArrayOOP = GetMethodBytecodes(methodOOP);
    if (bytecodeArrayOOP.RawValue == NilOOP.RawValue)
        throw new InvalidOperationException($"Method {method.LocalId} has nil bytecodes");

    // Verify bytecode length
    int length = GetByteArrayLength(bytecodeArrayOOP);
    if (length != method.Bytecodes.Length)
        throw new InvalidOperationException($"Method {method.LocalId} bytecode length mismatch");
}
```

#### 13.3: Special Object Validation

```csharp
// Verify critical singletons exist and have correct class
ValidateObject(NilOOP, "UndefinedObject");
ValidateObject(TrueOOP, "True");
ValidateObject(FalseOOP, "False");
ValidateObject(SystemDictionaryOOP, "SystemDictionary");
```

#### 13.4: Cross-Reference Validation

```csharp
// Verify all object references are valid
foreach (var obj in allAllocatedObjects)
{
    foreach (var fieldValue in GetAllFieldValues(obj))
    {
        if (!IsSmallInteger(fieldValue) && !IsValidOOP(fieldValue))
            throw new InvalidOperationException($"Invalid OOP reference: {fieldValue}");
    }
}
```

**Output**:

```
Classes: 474 validated
Methods: 10059 validated
Special objects: 5 validated
Cross-references: 10536 objects checked
✓ All heap objects match JSON definitions
```

---

### Phase 14: GC Root Setup

**Purpose**: Configure garbage collector roots.

**Responsibility**: Set GC roots ONLY - no allocations.

**Why These Roots**: Everything else is reachable from SystemDictionary which contains all classes and globals.

**Actions**:

```csharp
var gcRoots = new[] {
    GlobalRegistry.NilOOP,           // Always reachable
    GlobalRegistry.FalseOOP,         // Always reachable
    GlobalRegistry.TrueOOP,          // Always reachable
    GlobalRegistry.SystemDictionaryOOP  // Contains all classes and globals
};

_heapManager.SetGCRoots(gcRoots);
```

**Reachability Graph**:

```
GC Roots:
  → nil (OOP 9254)
  → false (OOP 9258)
  → true (OOP 9256)
  → Smalltalk (SystemDictionary, OOP 7830)
      → class:Object (via Association)
          → meta:Object (via header)
              → class:Metaclass (via header)
                  → meta:Metaclass (via header)
                      → ... (metaclass circle)
          → methodDict
              → method:Object>>class
                  → literals
                  → bytecodes
              → ... (all Object methods)
          → superclass = nil
      → class:Point (via Association)
          → meta:Point (via header)
          → superclass = class:Object
          → methodDict
              → method:Point>>x
              → method:Point>>y
              → ... (all Point methods)
      → ... (all 474 classes)
      → global variables
      → ... (everything reachable!)
```

**Output**:

```
GC Roots (4 objects):
  nil (oop=ObjectPointer(oop=9254), class=UndefinedObject)
  false (oop=ObjectPointer(oop=9258), class=False)
  true (oop=ObjectPointer(oop=9256), class=True)
  Smalltalk (oop=ObjectPointer(oop=7830), class=SystemDictionary)
```

---

### Phase 15: GC Verification

**Purpose**: Final sanity check - verify ALL objects are reachable.

**Responsibility**: Run GC and verify 0 objects collected - ultimate validation.

**Test**:

```csharp
var gcStats = _heapManager.GarbageCollect();

if (gcStats.CollectedObjects > 0)
{
    throw new InvalidOperationException(
        $"COLD START BUG: GC collected {gcStats.CollectedObjects} objects. " +
        $"All objects should be reachable from GC roots.");
}
```

**Success Criteria**:
- **0 objects collected** = 100% reachable = perfect cold start!
- Any objects collected = BUG in cold start loader

**Output**:

```
GC Results: 0/29130 objects collected (0.0% efficiency) in 10.34ms
Object table size: 29130
Active objects: 29130
✓ PERFECT - All objects reachable
```

**What This Proves**:
- ✓ All allocations properly linked
- ✓ No orphaned objects
- ✓ All phases wired correctly
- ✓ Cold start 100% correct

---

## Key Architectural Principles

### 1. Circular Dependency Resolution

**Problem**: Classes need classes, metaclasses need metaclasses.

**Solution**: Multi-phase allocation and wiring.

- Phase 2: Allocate all class shells (empty)
- Phase 3: Wire metaclass relationships
- Phase 4: Set class metadata
- Phases 7-9: Install methods

### 2. Nil Allocation Timing

**Problem**: Cannot initialize to nil before nil exists.

**Solution**: Delay nil initialization until Phase Y.

- Phases 0-5: Leave fields uninitialized
- Phase 6: Create nil singleton
- Phase Y: Initialize all remaining fields to nil

### 3. OOP 0 is Valid

**Problem**: Cannot use 0 as "uninitialized" sentinel.

**Example**:
```
First object allocated → OOP 0 (valid!)
someObject.field = OOP 0 (valid reference!)

if (field.RawValue == 0) → WRONG! This is valid, not uninitialized!
```

**Solution**: Explicit field tracking with HashSet.

```csharp
private readonly HashSet<(int objectOOP, int fieldIndex)> _initializedFields = new();

// Phase 3 sets field:
_heapManager.SetInstanceVariable(objectOOP, fieldIndex, value);
_initializedFields.Add((objectOOP.RawValue, fieldIndex));

// Phase Y checks before setting:
if (!_initializedFields.Contains((objectOOP.RawValue, i)))
{
    _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
}
```

### 4. Phase Separation of Concerns

**Principle**: Each phase has ONE job. **Never** overwrite previous phase's work.

**Anti-Pattern**:
```csharp
// Phase 4 (BAD - scope creep!)
foreach (var cls in classes)
{
    SetClassName(cls);
    SetFormat(cls);
    InitializeAllFieldsToNil(cls); // ❌ WRONG - overwrites Phase 3's work!
}
```

**Correct Pattern**:
```csharp
// Phase 4 (GOOD - focused responsibility)
foreach (var cls in classes)
{
    SetInstanceVariableUsingPrimitive(classOOP, nameIndex, nameOOP);
    // Tracks: (classOOP, nameIndex)

    SetInstanceVariableUsingPrimitive(classOOP, formatIndex, formatOOP);
    // Tracks: (classOOP, formatIndex)

    // Does NOT initialize other fields - Phase Y will handle that
}
```

### 5. Field Initialization Tracking

**Authoritative Source**: `HashSet<(int objectOOP, int fieldIndex)>`

**Contract**:
- Phase 3: Tracks metaclass wiring fields (superclass, thisClass)
- Phase 4: Tracks class metadata fields (name, format, classPool, superclass)
- Phase Y: Only initializes fields NOT in tracking set

**Enforcement**:
```csharp
private void SetAndTrackInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value)
{
    _heapManager.SetInstanceVariable(objectOOP, fieldIndex, value);
    _initializedFields.Add((objectOOP.RawValue, fieldIndex));
}
```

### 6. Header vs Instance Variables

**Critical Distinction**:

- **Header slot 0**: Class reference (memory offset 0)
  - Set via: `SetSlot(obj, 0, classOOP)`
  - Read via: `GetSlot(obj, 0)`

- **Instance variable 0**: First instance variable (memory offset 4+)
  - Set via: `SetInstanceVariable(obj, 0, valueOOP)`
  - Read via: `GetInstanceVariable(obj, 0)`

**Memory Layout**:
```
Object memory layout:
  [0] = class reference (header)
  [4] = instance variable 0
  [8] = instance variable 1
  [12] = instance variable 2
  ...
```

**Never Confused**:
```csharp
// Set class reference (header):
_allocator.SetObjectClass(objectOOP, classOOP);
  → internally calls SetSlot(objectOOP, 0, classOOP)

// Set first instance variable:
_allocator.SetInstanceVariable(objectOOP, 0, valueOOP);
  → writes to memory offset 4+
```

### 7. Progressive Initialization

**Architecture**: Objects transition through states:

1. **Skeleton** (Phase 2): Header set, instance variables undefined
2. **Wired** (Phases 3-4): Critical relationships set, most fields still undefined
3. **Initialized** (Phase Y): All fields either meaningful values or nil
4. **Functional** (Phases 7-9): Methods installed, fully operational

**Guarantee**: Never in invalid state that would crash VM or cause incorrect behavior.

---

## Summary

This 15-phase architecture ensures every object in the Smalltalk-80 system is properly initialized through a carefully orchestrated bootstrap process that resolves circular dependencies, respects initialization ordering constraints, and maintains perfect heap integrity as verified by garbage collection.

The key insight is **field tracking** - by explicitly tracking which fields have been set, we avoid the impossible task of using sentinel values (like 0) to detect uninitialized fields when all values are valid.

Each phase has a focused responsibility and never overwrites work from previous phases, ensuring clean separation of concerns and making the loader maintainable and debuggable.

The final GC verification (Phase 15) proves the correctness of the entire bootstrap: **0 objects collected = 100% reachable = perfect cold start**.
