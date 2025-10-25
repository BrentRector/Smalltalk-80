# Smalltalk-80 Cold Start: Complete Phase-by-Phase Technical Documentation

**Last Updated**: 2025-01-25
**Purpose**: Exhaustive documentation of every operation in every cold start phase for LLM analysis and optimization
**Audience**: LLMs analyzing the cold start process for completeness, correctness, and optimization opportunities

---

## Table of Contents

- [JSON Input Structure](#json-input-structure)
- [Desired Final State](#desired-final-state)
- [Global Data Structures](#global-data-structures)
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
- [Phase 8b: Init Expression Method Creation](#phase-8b-init-expression-method-creation)
- [Phase 9: Method Dictionary Installation](#phase-9-method-dictionary-installation)
- [Phase 10: Patch Application](#phase-10-patch-application)
- [Phase 11: Init Expression Preparation](#phase-11-init-expression-preparation)
- [Phase 12: USTable Population](#phase-12-ustable-population)
- [Phase 13: Global Variable Installation](#phase-13-global-variable-installation)
- [Phase 14: GC Root Setup](#phase-14-gc-root-setup)
- [Phase 15: GC Verification](#phase-15-gc-verification)

---

## JSON Input Structure

### Complete Bundle Format

```json
{
  "version": "1.0.5",
  "symbols": ["Object", "Class", "superclass", "new", "at:", "at:put:", ...],
  "strings": ["This is a comment", "Documentation string", ...],
  "classes": [
    {
      "localId": "class:Object",
      "nameIndex": 0,
      "superNameIndex": null,
      "isMetaclass": false,
      "metaclassOfLocalId": null,
      "format": 16530,
      "instVarNames": [],
      "classVarNames": [],
      "poolLocalIds": [],
      "methodLocalIds": ["method:Object>>new", "method:Object>>==", ...],
      "commentStringIndex": 0
    },
    {
      "localId": "meta:Object",
      "nameIndex": 1,
      "superNameIndex": null,
      "isMetaclass": true,
      "metaclassOfLocalId": "class:Object",
      "format": 16530,
      "instVarNames": [],
      "classVarNames": [],
      "poolLocalIds": [],
      "methodLocalIds": ["method:Object class>>new", ...],
      "commentStringIndex": null
    },
    ... (478 more classes)
  ],
  "objects": [
    {
      "localId": "Smalltalk",
      "classNameIndex": 156,
      "fields": [
        {"type": "objectRef", "localId": "USTable"},
        {"type": "nil"},
        ...
      ]
    },
    {
      "localId": "ProcessorScheduler",
      "classNameIndex": 312,
      "fields": [...]
    }
  ],
  "methods": [
    {
      "localId": "method:Object>>new",
      "classLocalId": "class:Object",
      "selector": 142,
      "header": {
        "type": "compiledMethod",
        "word": 32771,
        "argCount": 0,
        "tempCount": 0,
        "primitiveIndex": 70,
        "literalCount": 1
      },
      "literals": [
        {"type": "symbol", "index": 142}
      ],
      "bytecodes": "iCA="
    },
    ... (10,181 more methods)
  ],
  "initExpressions": [
    {
      "localId": "init:001",
      "source": "Object comment: 'I am the root of the Smalltalk class system.'",
      "header": {...},
      "literals": [...],
      "bytecodes": "..."
    },
    ... (284 more init expressions)
  ]
}
```

### Field Encoding Details

**format** (16-bit word):
```
Bits 0-7:   Instance variable count (0-255)
Bits 8-11:  Size of indexed portion for fixed-size objects (0-15)
Bits 12-13: Instance specification:
            0 = No indexed instance variables (fixed size)
            1 = Indexed instance variables are pointers (PointerIndexed)
            2 = Indexed instance variables are words (WordIndexed)
            3 = Indexed instance variables are bytes (ByteIndexed)
Bits 14-15: Reserved (0)
```

**Example**: `format: 16530` = `0x4092`
```
Binary: 0100 0000 1001 0010
Bits 0-7:   0x92 = 146 (instance variable count)
Bits 8-11:  0 (no fixed indexed size)
Bits 12-13: 01 (PointerIndexed)
Bits 14-15: 0 (reserved)
```

**header.word** (16-bit CompiledMethod header):
```
Bits 0-4:   Argument count (0-31)
Bits 5-9:   Temporary variable count (0-31)
Bit 10:     Large context flag (0=small frame 12 slots, 1=large frame 32 slots)
Bits 11-14: Literal count (0-15, not including mandatory selector)
Bit 15:     Has primitive flag (1=has primitive, 0=no primitive)
```

**Example**: `word: 32771` = `0x8003`
```
Binary: 1000 0000 0000 0011
Bits 0-4:   3 (0 arguments - not encoded correctly, actually 0)
Bits 5-9:   0 (0 temporaries)
Bit 10:     0 (small frame)
Bits 11-14: 0 (0 additional literals besides selector)
Bit 15:     1 (has primitive)
```

### Literal Value Types

**symbol**: Reference to symbols array
```json
{"type": "symbol", "index": 142}
→ Resolves to: SymbolRegistry.GetSymbolOOP(142)
```

**string**: Reference to strings array
```json
{"type": "string", "index": 5}
→ Creates new String object from bundle.Strings[5]
```

**smallInt**: Immediate integer value
```json
{"type": "smallInt", "value": 42}
→ Resolves to: SmalltalkOOP.FromSmallInteger(42)
```

**float**: IEEE 754 double precision
```json
{"type": "float", "value": 3.14159}
→ Allocates Float object with 8 bytes of IEEE 754 data
```

**nil, true, false**: Singleton references
```json
{"type": "nil"}
→ Resolves to: SpecialObjectsArray.Instance.Nil

{"type": "true"}
→ Resolves to: SpecialObjectsArray.Instance.True

{"type": "false"}
→ Resolves to: SpecialObjectsArray.Instance.False
```

**objectRef**: Reference to pre-allocated object
```json
{"type": "objectRef", "localId": "class:Object"}
→ Resolves to: localIdToObjectId["class:Object"]
```

**compiledMethod**: Reference to another method (for block closures)
```json
{"type": "compiledMethod", "localId": "method:Point>>x"}
→ Resolves to: localIdToObjectId["method:Point>>x"]
```

**character**: Unicode code point
```json
{"type": "character", "codePoint": 65}
→ Allocates Character object with value $A
```

**bytes**: Base64-encoded byte array
```json
{"type": "bytes", "encoding": "base64", "data": "AQIDBA=="}
→ Decodes to [1, 2, 3, 4], allocates ByteArray
```

**association**: Key-value pair (for global variable references)
```json
{
  "type": "association",
  "association": {
    "keyIndex": 42,
    "valueRef": {"localId": "class:Object"}
  }
}
→ Allocates Association with key=symbol, value=objectOOP
```

**array**: Nested array of literals
```json
{
  "type": "array",
  "elements": [
    {"type": "smallInt", "value": 1},
    {"type": "symbol", "index": 5}
  ]
}
→ Allocates Array with resolved element OOPs
```

---

## Desired Final State

### Object Count

After all phases complete:
- **Symbols**: 3,834 Symbol objects
- **Classes**: 480 class objects (240 regular + 240 metaclasses)
- **Methods**: 10,182 CompiledMethod objects
- **ByteArrays**: 10,182 ByteArray objects (one per method for bytecodes)
- **Method Dictionaries**: 480 Dictionary objects
- **Singletons**: nil, true, false, Smalltalk, ProcessorScheduler
- **Init Methods**: 285 CompiledMethod objects (+ 285 ByteArrays)
- **Strings**: ~1,000+ String objects (from literals)
- **Associations**: ~549 canonical Association objects
- **Arrays**: Various (instance variable arrays, etc.)
- **Other objects**: Float constants, Characters, etc.

**Total**: Approximately 29,500-30,000 objects

### Memory Layout

```
┌─────────────────────────────────────────────────────────┐
│              Object Table (Segmented)                    │
│  - 15 blocks × 2,048 entries = 30,720 capacity          │
│  - Each entry: 32 bytes (class, format, storage refs)   │
│  - Total: ~1 MB                                          │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│           Managed Storage (3 arrays per entry)           │
│  - ManagedStorage (byte[]) for ByteIndexed              │
│  - ManagedStorage (SmalltalkOOP[]) for PointerIndexed   │
│  - ManagedStorage (ushort[]) for WordIndexed            │
│  - Total: ~10-15 MB                                      │
└─────────────────────────────────────────────────────────┘
```

### Critical Invariants (Post-Coldstart)

**1. Every object has a valid class reference**
```
∀ object in heap:
  object.slots[0] != SmalltalkOOP.Null
  object.slots[0] points to valid class object

Exception: SmallIntegers are immediate values (no slots)
```

**2. Metaclass circle is correctly wired**
```
meta:Object.superclass = class:Class
class:Class.superclass = class:ClassDescription
class:ClassDescription.superclass = class:Behavior
class:Behavior.superclass = class:Object  ← closes the circle
```

**3. Regular class hierarchy is a tree**
```
Object.superclass = nil  ← root
  All other classes have non-nil superclass
  No cycles except the metaclass circle
```

**4. Every class is an instance of its metaclass**
```
class:Object.class = meta:Object
class:Point.class = meta:Point
class:Array.class = meta:Array
...
```

**5. Every metaclass is an instance of Metaclass**
```
meta:Object.class = class:Metaclass
meta:Point.class = class:Metaclass
...
```

**6. All instance variables are initialized**
```
∀ object, ∀ instance variable slot i:
  object.slots[i] = some valid OOP (never 0/null)
  Either explicitly set, or initialized to nil
```

**7. All method dictionaries are properly installed**
```
∀ class:
  class.methodDict != nil
  class.methodDict is a Dictionary instance
  class.methodDict contains all methods for that class
  class.methodDict.tally = actual number of associations
```

**8. All symbols are in USTable**
```
Smalltalk at: #symbols returns Array of all symbols
USTable.size = 3,834
Each element is a Symbol object
```

**9. All classes are in Smalltalk dictionary**
```
Smalltalk at: #Object returns class:Object
Smalltalk at: #Class returns class:Class
... (all 240 classes)
```

**10. GC roots are complete**
```
SpecialObjectsArray contains:
  - Nil (oop to nil singleton)
  - False (oop to false singleton)
  - True (oop to true singleton)
  - Smalltalk (oop to SystemDictionary)
  - ProcessorScheduler (oop to scheduler singleton)
  - InitExpressionMethods (oop to Array of init methods)

All ~30,000 objects reachable from these roots
GC collection = 0 objects (all reachable)
```

---

## Global Data Structures

These data structures are maintained throughout the cold start process:

### 1. `localIdToObjectId`: `Dictionary<string, SmalltalkOOP>`

**Purpose**: Maps JSON localId strings to allocated heap OOPs

**Lifetime**: Coldstart only (not accessible at runtime)

**Population**:
- Phase 2: All class localIds → class OOPs
- Phase 8: All method localIds → method OOPs
- Phase 6/13: Object localIds → object OOPs

**Example**:
```csharp
localIdToObjectId["class:Object"] = oop=7676
localIdToObjectId["meta:Object"] = oop=7678
localIdToObjectId["method:Object>>new"] = oop=12345
```

**Usage**: Resolve `{"type": "objectRef", "localId": "class:Foo"}` literals

---

### 2. `initializedFields`: `HashSet<(SmalltalkOOP objectOOP, int fieldIndex)>`

**Purpose**: Track which instance variable slots have been explicitly set

**Lifetime**: Phases 3-Y

**Why Needed**: Distinguish "not yet set" from "set to nil"

**Population**:
- Phase 3: Metaclass `thisClass` and `superclass` fields
- Phase 4: Class `name`, `superclass`, `format`, `classPool` fields
- Phase 8: CompiledMethod `header`, `literals`, `bytecodes` fields

**Usage in Phase Y**:
```csharp
if (!initializedFields.Contains((objectOOP, fieldIndex))) {
    // Not explicitly set, initialize to nil
    heapManager.SetSlot(objectOOP, fieldIndex + 1, nil);
}
```

---

### 3. `SymbolRegistry`: Static symbol index → OOP mapping

**Purpose**: Fast lookup of symbols by index

**Lifetime**: Entire VM lifetime (frozen after Phase 1)

**API**:
```csharp
// Phase 1: Populate
SymbolRegistry.Register(index: 0, symbolOOP);
SymbolRegistry.Register(index: 1, symbolOOP);
...
SymbolRegistry.Freeze();  // Prevent further modifications

// Later phases: Read-only access
SmalltalkOOP symbolOOP = SymbolRegistry.GetSymbolOOP(index);
```

**Architectural Rule**: After Freeze(), runtime code can ONLY read, never write

---

### 4. `GlobalRegistry`: Static string → OOP mapping

**Purpose**: Fast lookup of classes and globals by name

**Lifetime**: Coldstart only (frozen after Phase 13)

**API**:
```csharp
// Populate during coldstart
GlobalRegistry.Set("class:Object", objectOOP);
GlobalRegistry.Set("nil", nilOOP);
GlobalRegistry.Set("Smalltalk", smalltalkOOP);

// Lookup during coldstart
GlobalRegistry.TryGet("class:Object", out SmalltalkOOP oop);
GlobalRegistry.LookupClassOOP("Object");

// Freeze
GlobalRegistry.Freeze();  // After Phase 13
```

**Architectural Rule**: Runtime code must NOT use GlobalRegistry (use SpecialObjectsArray or heap lookups instead)

---

### 5. `SpecialObjectsArray`: GC roots

**Purpose**: Store fundamental singleton references accessible at runtime

**Lifetime**: Entire VM lifetime

**Fields**:
```csharp
public readonly struct SpecialObjectsArray
{
    public readonly SmalltalkOOP Nil;
    public readonly SmalltalkOOP False;
    public readonly SmalltalkOOP True;
    public readonly SmalltalkOOP Smalltalk;
    public readonly SmalltalkOOP ProcessorScheduler;
    public readonly SmalltalkOOP InitExpressionMethods;
}
```

**Setters** (used during coldstart):
```csharp
SpecialObjectsArray.SetNil(nilOOP);          // Phase 6
SpecialObjectsArray.SetFalse(falseOOP);      // Phase 6
SpecialObjectsArray.SetTrue(trueOOP);        // Phase 6
SpecialObjectsArray.SetSmalltalk(dictOOP);   // Phase 13
SpecialObjectsArray.SetInitExpressionMethods(arrayOOP);  // Phase 8b
```

**Runtime Access**:
```csharp
SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
```

---

### 6. `LayoutResolver`: Class layout cache

**Purpose**: Fast lookup of instance variable counts and field indices

**Lifetime**: Entire VM lifetime

**Populated**: Phase 5

**API**:
```csharp
int count = layoutResolver.GetInstanceVariableCount("class:Object");
int index = layoutResolver.GetFieldIndex("class:Point", "x");
```

**Why Needed**: Runtime code must NEVER hardcode field indices (they can change if class definition changes)

---

### 7. `SemanticAllocator`: High-level allocation wrapper

**Purpose**: Simplify object allocation with automatic size calculation

**Lifetime**: Coldstart only

**Why Separate from HeapManager**:
- SemanticAllocator knows about class layouts (uses LayoutResolver)
- HeapManager is low-level (only knows formats, sizes)

**API**:
```csharp
SmalltalkOOP classOOP = allocator.AllocateFixedObject("Point");
// Internally:
//   1. Lookup "Point" in bundle
//   2. Calculate instance variable count
//   3. Call heapManager.AllocateObject(PointerIndexed, instVarCount, 0)

SmalltalkOOP stringOOP = allocator.AllocateString(classOOP, "Hello");
// Internally:
//   1. Convert string to UTF-8 bytes
//   2. Allocate ByteIndexed object
//   3. Set class reference
//   4. Store bytes
```

**Architectural Rule**: Runtime code must NOT use SemanticAllocator (use HeapManager directly)

---

## Phase 0: Preflight Validation

**File**: `Phase0_PreflightValidation.cs` (conceptual - validation is in Loader.cs)

**Purpose**: Validate JSON bundle structure before starting allocation

**Input State**:
- JSON file loaded and deserialized into `ColdStartBundle` object
- No heap objects allocated
- No data structures initialized

**Operations Performed**:

### Step 1: Validate Bundle Version

```csharp
if (bundle.Version != "1.0.5")
{
    throw new InvalidOperationException(
        $"Unsupported bundle version: {bundle.Version}. Expected: 1.0.5");
}
```

**Why**: Different versions may have incompatible formats

---

### Step 2: Validate Required Arrays Exist

```csharp
if (bundle.Symbols == null || bundle.Symbols.Length == 0)
    throw new InvalidOperationException("Symbols array is missing or empty");

if (bundle.Classes == null || bundle.Classes.Length != 480)
    throw new InvalidOperationException(
        $"Expected 480 classes, got {bundle.Classes.Length}");

if (bundle.Methods == null || bundle.Methods.Length == 0)
    throw new InvalidOperationException("Methods array is missing or empty");

if (bundle.InitExpressions == null || bundle.InitExpressions.Length == 0)
    throw new InvalidOperationException("InitExpressions array is missing or empty");
```

**Why**: Cold start cannot proceed without complete data

---

### Step 3: Validate Class Pairs

```csharp
var regularClasses = bundle.Classes.Where(c => !c.IsMetaclass).ToList();
var metaclasses = bundle.Classes.Where(c => c.IsMetaclass).ToList();

if (regularClasses.Count != metaclasses.Count)
{
    throw new InvalidOperationException(
        $"Class/Metaclass mismatch: {regularClasses.Count} classes vs {metaclasses.Count} metaclasses");
}

// Verify each class has its metaclass
foreach (var cls in regularClasses)
{
    string expectedMetaId = $"meta:{bundle.Symbols[cls.NameIndex]}";
    var meta = metaclasses.FirstOrDefault(m => m.LocalId == expectedMetaId);

    if (meta == null)
    {
        throw new InvalidOperationException(
            $"Missing metaclass: {expectedMetaId} for class {cls.LocalId}");
    }

    if (meta.MetaclassOfLocalId != cls.LocalId)
    {
        throw new InvalidOperationException(
            $"Metaclass {meta.LocalId} has wrong metaclassOfLocalId: " +
            $"{meta.MetaclassOfLocalId}, expected: {cls.LocalId}");
    }
}
```

**Why**: Metaclass wiring (Phase 3) requires perfect class/metaclass pairing

---

### Step 4: Validate Critical Classes Exist

```csharp
string[] requiredClasses = new[]
{
    "Object", "Class", "Metaclass",
    "Behavior", "ClassDescription",
    "CompiledMethod", "MethodContext", "BlockContext",
    "Symbol", "String",
    "Array", "ByteArray",
    "Dictionary", "Association",
    "True", "False", "UndefinedObject",
    "SystemDictionary", "ProcessorScheduler"
};

foreach (string className in requiredClasses)
{
    bool exists = bundle.Classes.Any(c =>
        !c.IsMetaclass && bundle.Symbols[c.NameIndex] == className);

    if (!exists)
    {
        throw new InvalidOperationException(
            $"Required class missing: {className}");
    }
}
```

**Why**: These classes are hard-coded dependencies in the VM

---

### Step 5: Validate Symbol Index References

```csharp
int maxSymbolIndex = bundle.Symbols.Length - 1;

foreach (var cls in bundle.Classes)
{
    // Validate nameIndex
    if (cls.NameIndex < 0 || cls.NameIndex > maxSymbolIndex)
    {
        throw new InvalidOperationException(
            $"Class {cls.LocalId} has invalid nameIndex: {cls.NameIndex}");
    }

    // Validate superNameIndex (if present)
    if (cls.SuperNameIndex.HasValue)
    {
        if (cls.SuperNameIndex.Value < 0 || cls.SuperNameIndex.Value > maxSymbolIndex)
        {
            throw new InvalidOperationException(
                $"Class {cls.LocalId} has invalid superNameIndex: {cls.SuperNameIndex}");
        }
    }

    // Validate instVarNames indices
    foreach (int varIndex in cls.InstVarNames)
    {
        if (varIndex < 0 || varIndex > maxSymbolIndex)
        {
            throw new InvalidOperationException(
                $"Class {cls.LocalId} has invalid instVarName index: {varIndex}");
        }
    }

    // Validate classVarNames indices
    foreach (int varIndex in cls.ClassVarNames)
    {
        if (varIndex < 0 || varIndex > maxSymbolIndex)
        {
            throw new InvalidOperationException(
                $"Class {cls.LocalId} has invalid classVarName index: {varIndex}");
        }
    }
}
```

**Why**: Out-of-bounds symbol references cause crashes in later phases

---

### Step 6: Validate LocalId Uniqueness

```csharp
var allLocalIds = new HashSet<string>();

// Check class localIds
foreach (var cls in bundle.Classes)
{
    if (!allLocalIds.Add(cls.LocalId))
    {
        throw new InvalidOperationException(
            $"Duplicate localId: {cls.LocalId}");
    }
}

// Check method localIds
foreach (var method in bundle.Methods)
{
    if (!allLocalIds.Add(method.LocalId))
    {
        throw new InvalidOperationException(
            $"Duplicate localId: {method.LocalId}");
    }
}

// Check object localIds
foreach (var obj in bundle.Objects)
{
    if (!allLocalIds.Add(obj.LocalId))
    {
        throw new InvalidOperationException(
            $"Duplicate localId: {obj.LocalId}");
    }
}

// Check init expression localIds
foreach (var init in bundle.InitExpressions)
{
    if (!allLocalIds.Add(init.LocalId))
    {
        throw new InvalidOperationException(
            $"Duplicate localId: {init.LocalId}");
    }
}
```

**Why**: `localIdToObjectId` mapping requires unique keys

---

### Step 7: Validate Method References

```csharp
foreach (var cls in bundle.Classes)
{
    foreach (string methodLocalId in cls.MethodLocalIds)
    {
        var method = bundle.Methods.FirstOrDefault(m => m.LocalId == methodLocalId);

        if (method == null)
        {
            throw new InvalidOperationException(
                $"Class {cls.LocalId} references non-existent method: {methodLocalId}");
        }

        if (method.ClassLocalId != cls.LocalId)
        {
            throw new InvalidOperationException(
                $"Method {method.LocalId} has wrong classLocalId: " +
                $"{method.ClassLocalId}, expected: {cls.LocalId}");
        }
    }
}
```

**Why**: Method installation (Phase 8) requires valid method references

---

**Output State**:
- Bundle validated as structurally correct
- No heap allocations yet
- Ready to proceed to Phase 1

**Performance**: < 10ms (pure validation, no allocation)

**Failure Mode**: Throw exception with detailed diagnostic message, abort cold start

**Possible Optimizations**:
1. Cache validation results (skip on subsequent starts if bundle hash unchanged)
2. Parallelize validation (symbols, classes, methods independently)
3. Use JSON schema validation before deserialization
4. Add checksum validation (CRC32 of entire bundle)

**Missing Validations** (potential improvements):
1. Validate method bytecode format (currently trusted)
2. Validate literal value references are resolvable
3. Validate format word encoding is legal
4. Validate no circular superclass chains (except metaclass circle)
5. Validate method selector indices are valid

---

## Phase 1: Symbol Interning

**File**: `Phase1_SymbolInterning.cs`

**Purpose**: Allocate all 3,834 Symbol objects and build the symbol table

**Why First**:
- Class names are symbols
- Method selectors are symbols
- Instance variable names are symbols
- All later phases reference symbols by index
- Must exist before any other objects

**Input State**:
- Validated JSON bundle
- Empty heap (no objects allocated)
- Empty SymbolRegistry
- `localIdToObjectId` empty

**Dependencies**: None (first phase after validation)

**Operations Performed**:

### Step 1: Initialize Symbol Count

```csharp
int symbolCount = bundle.Symbols.Length;  // 3,834
Console.WriteLine($"  Symbols: {symbolCount}");
```

---

### Step 2: Determine Symbol Object Layout

```csharp
// Symbol is a subclass of String
// String has no instance variables in Smalltalk-80
// Symbol has no additional instance variables
// Therefore: Symbol objects have 0 fixed instance variables

int symbolInstVarCount = 0;  // No instance variables

// Symbol format: ByteIndexed (instance spec = 3)
ObjectFormat symbolFormat = ObjectFormat.ByteIndexed;
```

**Why ByteIndexed**: Symbol text is stored as UTF-8 bytes in indexed portion

---

### Step 3: Allocate Each Symbol Object

```csharp
for (int i = 0; i < symbolCount; i++)
{
    string symbolText = bundle.Symbols[i];

    // Convert to UTF-8 bytes
    byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(symbolText);
    int byteCount = utf8Bytes.Length;

    // Allocate ByteIndexed object
    // - fixedSlots: 0 (Symbol has no instance variables)
    // - indexedSize: byteCount (for UTF-8 text)
    SmalltalkOOP symbolOOP = _heapManager.AllocateObject(
        ObjectFormat.ByteIndexed,
        fixedSlots: 0,
        indexedSize: byteCount
    );

    // Store UTF-8 bytes in indexed portion
    for (int j = 0; j < byteCount; j++)
    {
        _heapManager.SetIndexedByte(symbolOOP, j, utf8Bytes[j]);
    }

    // Register in SymbolRegistry for fast lookup
    SymbolRegistry.Register(i, symbolOOP);
}
```

**Critical Note**: Class reference (slot 0) is NOT set here
- `class:Symbol` doesn't exist yet (allocated in Phase 2)
- Will be set in Phase X (Class Reference Initialization)
- For now, symbolOOP.slots[0] = 0 (invalid, but temporary)

---

### Step 4: Freeze SymbolRegistry

```csharp
SymbolRegistry.Freeze();
Console.WriteLine($"[SymbolRegistry] Frozen with {symbolCount} symbols");
```

**Effect**:
- Future calls to `SymbolRegistry.Register()` will throw exception
- Only `SymbolRegistry.GetSymbolOOP(int index)` allowed
- Prevents runtime code from modifying symbol table

**Why Freeze**: Enforce architectural separation (coldstart-only data structure)

---

**Output State**:
- 3,834 Symbol objects allocated in heap
- Each symbol contains UTF-8 bytes of symbol text
- SymbolRegistry populated with index → OOP mapping
- Symbol objects have class reference = 0 (invalid, temporary)

**Memory Allocated**:
```
Average symbol length: ~8 characters
UTF-8 bytes per symbol: ~8 bytes
Object table entry: ~32 bytes
Total per symbol: ~40 bytes
Total memory: 3,834 × 40 = ~153 KB
```

**Performance**: ~20-30ms

**Data Structure State After Phase 1**:
```
SymbolRegistry:
  0 → oop=100 (Symbol: "Object")
  1 → oop=102 (Symbol: "Class")
  2 → oop=104 (Symbol: "superclass")
  ...
  3833 → oop=7766 (Symbol: "zoomAt:")

Heap:
  oop=100: ByteIndexed, 0 fixed, 6 indexed, bytes=[O,b,j,e,c,t], class=0
  oop=102: ByteIndexed, 0 fixed, 5 indexed, bytes=[C,l,a,s,s], class=0
  ...
```

**Validation After Phase 1**:
```csharp
// Should all pass:
Assert(SymbolRegistry.GetSymbolOOP(0) != SmalltalkOOP.Null);
Assert(_heapManager.GetIndexedSize(SymbolRegistry.GetSymbolOOP(0)) == 6);  // "Object"
Assert(_heapManager.GetIndexedByte(SymbolRegistry.GetSymbolOOP(0), 0) == 'O');
```

**Possible Optimizations**:
1. **Batch Allocation**: Pre-calculate total byte count, allocate all symbols in one heap block
2. **String Interning**: Use string pool to avoid duplicate UTF-8 conversions
3. **Compression**: Use symbol prefix compression (many symbols start with "inst", "class", etc.)
4. **Parallel Encoding**: Convert strings to UTF-8 in parallel (CPU-bound)
5. **Skip Short Symbols**: Symbols ≤ 4 chars could be encoded in OOP itself (immediate symbols)

**Missing Operations** (potential improvements):
1. Validate all symbols are valid UTF-8
2. Detect duplicate symbols (shouldn't exist, but validate)
3. Build reverse lookup (text → index) for runtime symbol creation
4. Validate symbol text doesn't contain null bytes

**Error Cases**:
- **Out of Memory**: If heap cannot allocate 3,834 objects
  - **Handling**: Throw OutOfMemoryException, abort coldstart
- **Invalid UTF-8**: If bundle contains invalid Unicode
  - **Handling**: Currently not validated, would fail silently
  - **Should Do**: Validate and throw exception

---

## Phase 2: Class Shells

**File**: `Phase2_ClassShells.cs`

**Purpose**: Allocate empty "shell" objects for all 480 class and metaclass objects

**Why Now**:
- Classes reference each other (superclass, metaclass relationships)
- To resolve circular dependencies, we allocate all class objects first (empty shells)
- Then wire relationships in later phases

**Input State**:
- Symbols allocated (Phase 1)
- No class objects exist yet
- `localIdToObjectId` is empty
- SymbolRegistry is frozen and populated

**Dependencies**:
- Phase 1 (needs symbols for class names)

**Critical Concept**: **Shell Allocation**
- A "shell" is an allocated object with correct structure but uninitialized fields
- All instance variable slots contain 0 (invalid OOP)
- Class reference (slot 0) is also 0 (invalid, will be fixed in Phase X)
- This allows us to get valid OOPs for all classes before setting any references

---

### Step 1: Count Classes

```csharp
int totalClasses = bundle.Classes.Length;  // Should be 480
int regularClasses = bundle.Classes.Count(c => !c.IsMetaclass);  // 240
int metaclasses = bundle.Classes.Count(c => c.IsMetaclass);      // 240

Console.WriteLine($"  Allocating {totalClasses} class objects " +
                  $"({regularClasses} regular + {metaclasses} metaclasses)");
```

**Validation**:
```csharp
if (totalClasses != 480)
    throw new InvalidOperationException($"Expected 480 classes, got {totalClasses}");

if (regularClasses != metaclasses)
    throw new InvalidOperationException(
        $"Class/metaclass count mismatch: {regularClasses} vs {metaclasses}");
```

---

### Step 2: Determine Instance Variable Counts

**For Regular Classes** (template: `class:Class`):

```csharp
// Class inherits from:
//   Class → ClassDescription → Behavior → Object

// Instance variables (from Smalltalk-80):
// From Behavior:
//   - superclass       (index 0)
//   - methodDict       (index 1)
//   - format           (index 2)
// From ClassDescription:
//   - instanceVariables (index 3)
//   - organization      (index 4)
// From Class:
//   - subclasses       (index 5)
//   - name             (index 6)
//   - classPool        (index 7)
//   - sharedPools      (index 8)
//   - environment      (index 9)
//   - category         (index 10)

// Total instance variables for class:Class
int classInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(
    "Class", bundle);

// In practice: 11 instance variables (may vary based on bundle)
```

**For Metaclasses** (template: `class:Metaclass`):

```csharp
// Metaclass inherits from:
//   Metaclass → ClassDescription → Behavior → Object

// Instance variables:
// From Behavior:
//   - superclass       (index 0)
//   - methodDict       (index 1)
//   - format           (index 2)
// From ClassDescription:
//   - instanceVariables (index 3)
//   - organization      (index 4)
// From Metaclass:
//   - thisClass        (index 5) ← points to instance-side class

// Total instance variables for class:Metaclass
int metaclassInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(
    "Metaclass", bundle);

// In practice: 6 instance variables
```

---

### Step 3: Allocate Class Shells

```csharp
foreach (var cls in bundle.Classes)
{
    // Determine template class for this object
    string templateClassName = cls.IsMetaclass ? "Metaclass" : "Class";

    // Calculate total instance variable count (including inherited)
    int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(
        templateClassName, bundle);

    // All class objects are PointerIndexed, no indexed portion
    ObjectFormat format = ObjectFormat.PointerIndexed;
    int indexedSize = 0;

    // Allocate class shell
    SmalltalkOOP classOOP = _heapManager.AllocateObject(
        format,
        instVarCount,
        indexedSize
    );

    // Store in localId → OOP mapping
    _localIdToObjectId[cls.LocalId] = classOOP;
}
```

**What `AllocateObject` Does**:

1. Find free entry in object table
2. Calculate total slots: 1 (class) + instVarCount + indexedSize
3. Allocate storage array (SmalltalkOOP[] for PointerIndexed)
4. Initialize all slots to 0
5. Create object table entry
6. Return tagged OOP

---

**Output State**:

- 480 class objects allocated (240 regular + 240 metaclasses)
- Each has correct instance variable count
- All slots initialized to 0 (invalid OOP)
- `localIdToObjectId` populated with 480 entries

**Memory**: ~52 KB (240 × 128 bytes + 240 × 88 bytes)

**Performance**: ~15-20ms

---
## Phase 3: Metaclass Wiring

**File**: `Phase3_MetaclassWiring.cs`

**Purpose**: Wire the metaclass hierarchy to enable class-side method inheritance

**Why Now**:
- Metaclasses have circular dependencies with classes
- Must be wired before regular class hierarchy (Phase 4)
- Establishes the fundamental "metaclass circle" of Smalltalk-80

**Input State**:
- Symbols allocated (Phase 1)
- Class shells allocated (Phase 2)
- All class instance variables = 0
- nil doesn't exist yet (created in Phase 6)

**Dependencies**:
- Phase 2 (needs class OOPs)

**Critical Concept**: **The Metaclass Circle**

Smalltalk-80's object model has a unique circular structure:
```
meta:Object.superclass → class:Class
class:Class.superclass → class:ClassDescription
class:ClassDescription.superclass → class:Behavior
class:Behavior.superclass → class:Object  ← completes the circle
```

This circle allows class-side methods (like `new`, `basicNew`) defined in Behavior to be inherited by ALL classes through their metaclasses.

---

### Step 1: Set `thisClass` Field for All Metaclasses

**Purpose**: Link each metaclass to its instance-side class

**Field**: `thisClass` at index 5 in Metaclass instances

```csharp
// Find the thisClass field index
int thisClassIndex = ClassHierarchyUtils.FindInstanceVariableIndex(
    "Metaclass", "thisClass", bundle);

if (thisClassIndex < 0)
{
    throw new InvalidOperationException(
        "thisClass field not found in Metaclass - critical metaclass structure missing");
}

Console.WriteLine($"  Setting thisClass: index={thisClassIndex}");

// Process each metaclass
foreach (var metaCls in bundle.Classes.Where(c => c.IsMetaclass))
{
    // Get the metaclass OOP
    SmalltalkOOP metaOOP = _localIdToObjectId[metaCls.LocalId];

    // Get the instance-side class localId
    // e.g., for "meta:Object", this is "class:Object"
    string instanceLocalId = metaCls.MetaclassOfLocalId;

    if (instanceLocalId == null)
    {
        throw new InvalidOperationException(
            $"Metaclass {metaCls.LocalId} has null MetaclassOfLocalId");
    }

    // Get the instance class OOP
    if (!_localIdToObjectId.TryGetValue(instanceLocalId, out SmalltalkOOP instanceOOP))
    {
        throw new InvalidOperationException(
            $"Instance class not found: {instanceLocalId} for {metaCls.LocalId}");
    }

    // Set thisClass field
    _heapManager.SetInstanceVariable(metaOOP, thisClassIndex, instanceOOP);

    // Track this field as initialized
    _initializedFields.Add((metaOOP, thisClassIndex));

    // Diagnostic output for key metaclasses
    if (metaCls.LocalId == "meta:Object" || metaCls.LocalId == "meta:Class")
    {
        Console.WriteLine($"  Setting thisClass: index={thisClassIndex}, " +
                          $"meta={metaOOP}, inst={instanceOOP}");
    }
}
```

**Result After Step 1**:
```
meta:Object.thisClass = class:Object (oop=7676)
meta:Class.thisClass = class:Class (oop=7680)
meta:Point.thisClass = class:Point (oop=8234)
... (240 metaclasses)
```

**Why This Matters**:
- Allows VM to navigate from metaclass to instance class
- Used by primitives like `#class` and `#isKindOf:`
- Critical for reflective operations

---

### Step 2: Wire Fundamental Metaclass Circle

**Purpose**: Establish the core metaclass inheritance that enables class-side method sharing

**Why This is Special**:
- This is the ONLY circular inheritance in the system
- Required by Smalltalk-80 Blue Book specification
- Enables all classes to inherit `new`, `basicNew`, etc.

```csharp
// Get OOPs for the fundamental classes
SmalltalkOOP metaObjectOOP = _localIdToObjectId["meta:Object"];
SmalltalkOOP classClassOOP = _localIdToObjectId["class:Class"];
SmalltalkOOP classDescriptionOOP = _localIdToObjectId["class:ClassDescription"];
SmalltalkOOP classBehaviorOOP = _localIdToObjectId["class:Behavior"];
SmalltalkOOP classObjectOOP = _localIdToObjectId["class:Object"];

// Find superclass field index (inherited from Behavior)
int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex(
    "Class", "superclass", bundle);

if (superclassIndex < 0)
{
    throw new InvalidOperationException(
        "superclass field not found - critical class structure missing");
}

Console.WriteLine($"  Wiring fundamental metaclass hierarchy (superclass index: {superclassIndex}):");

// Wire the circle
Console.WriteLine($"    meta:Object.superclass = class:Class");
_heapManager.SetInstanceVariable(metaObjectOOP, superclassIndex, classClassOOP);
_initializedFields.Add((metaObjectOOP, superclassIndex));

Console.WriteLine($"    class:Class.superclass = class:ClassDescription");
_heapManager.SetInstanceVariable(classClassOOP, superclassIndex, classDescriptionOOP);
_initializedFields.Add((classClassOOP, superclassIndex));

Console.WriteLine($"    class:ClassDescription.superclass = class:Behavior");
_heapManager.SetInstanceVariable(classDescriptionOOP, superclassIndex, classBehaviorOOP);
_initializedFields.Add((classDescriptionOOP, superclassIndex));

Console.WriteLine($"    class:Behavior.superclass = class:Object");
_heapManager.SetInstanceVariable(classBehaviorOOP, superclassIndex, classObjectOOP);
_initializedFields.Add((classBehaviorOOP, superclassIndex));
```

**The Complete Circle Visualized**:
```
                    ┌─────────────────────────┐
                    ↓                         │
    meta:Object → class:Class → class:ClassDescription → class:Behavior → class:Object
                                                                               │
                                                                               └──→ (superclass = nil, set in Phase Y)
```

**Why This Works**:
```
When you call:  Point new

VM lookup path:
1. Point (instance class)
2. Look in meta:Point for #new (not found)
3. Follow meta:Point.superclass → meta:Object (because Point.superclass = Object)
4. Look in meta:Object for #new (not found)
5. Follow meta:Object.superclass → class:Class
6. Look in class:Class for #new (not found)
7. Follow class:Class.superclass → class:ClassDescription
8. Look in class:ClassDescription for #new (not found)
9. Follow class:ClassDescription.superclass → class:Behavior
10. Look in class:Behavior for #new (FOUND! Execute primitive 70)
```

This is why ALL classes can call `new` even though it's only defined once in Behavior.

---

### Step 3: Wire Regular Metaclass Superclass Chains

**Purpose**: For all other metaclasses, set their superclass to follow the pattern

**Rule**: `meta:Foo.superclass = meta:Bar` where Bar = Foo.superclass

**Why**: Metaclass inheritance mirrors instance class inheritance

```csharp
int wiredCount = 0;
int skippedCount = 0;

foreach (var metaCls in bundle.Classes.Where(c => c.IsMetaclass))
{
    // Get the metaclass OOP
    if (!_localIdToObjectId.TryGetValue(metaCls.LocalId, out SmalltalkOOP metaOOP))
    {
        Console.WriteLine($"    WARNING: Metaclass {metaCls.LocalId} not found");
        continue;
    }

    // Find the instance-side class
    var instanceCls = bundle.Classes.FirstOrDefault(
        c => c.LocalId == metaCls.MetaclassOfLocalId);

    if (instanceCls == null)
    {
        Console.WriteLine($"    WARNING: Instance class not found for {metaCls.LocalId}");
        continue;
    }

    // Check if instance class has a superclass
    if (instanceCls.SuperNameIndex is int superIdx)
    {
        // Instance class has a superclass, so metaclass should too
        string superName = bundle.Symbols[superIdx];
        string superMetaLocalId = $"meta:{superName}";

        if (!_localIdToObjectId.TryGetValue(superMetaLocalId, out SmalltalkOOP superMetaOOP))
        {
            Console.WriteLine($"    WARNING: Super-metaclass {superMetaLocalId} not found");
            skippedCount++;
            continue;
        }

        // Set metaclass superclass
        _heapManager.SetInstanceVariable(metaOOP, superclassIndex, superMetaOOP);
        _initializedFields.Add((metaOOP, superclassIndex));
        wiredCount++;
    }
    else
    {
        // Instance class has no superclass
        // This should ONLY be Object
        string instanceClassName = bundle.Symbols[instanceCls.NameIndex];

        if (instanceClassName != "Object")
        {
            Console.WriteLine($"    WARNING: Unexpected class without superclass: {instanceClassName}");
        }

        // meta:Object.superclass already set in fundamental hierarchy
        skippedCount++;
    }
}

Console.WriteLine($"  Wired {wiredCount} metaclass superclass relationships " +
                  $"({skippedCount} skipped - meta:Object already wired in fundamental hierarchy)");
```

**Example Wiring**:
```
Point.superclass = Object
  → meta:Point.superclass = meta:Object

Array.superclass = ArrayedCollection
  → meta:Array.superclass = meta:ArrayedCollection

ArrayedCollection.superclass = SequenceableCollection
  → meta:ArrayedCollection.superclass = meta:SequenceableCollection

SequenceableCollection.superclass = Collection
  → meta:SequenceableCollection.superclass = meta:Collection

Collection.superclass = Object
  → meta:Collection.superclass = meta:Object
```

**Expected Output**:
```
Wired 239 metaclass superclass relationships (1 skipped - meta:Object already wired in fundamental hierarchy)
```

The 1 skipped is meta:Object (already wired in fundamental circle).

---

**Output State**:

1. **All 240 metaclasses have `thisClass` set**:
   ```
   meta:Object.thisClass = class:Object
   meta:Point.thisClass = class:Point
   ... (240 total)
   ```

2. **All 240 metaclasses have `superclass` set**:
   ```
   Fundamental circle (4):
     meta:Object.superclass = class:Class
     class:Class.superclass = class:ClassDescription
     class:ClassDescription.superclass = class:Behavior
     class:Behavior.superclass = class:Object

   Regular metaclasses (239):
     meta:Point.superclass = meta:Object
     meta:Array.superclass = meta:ArrayedCollection
     ...

   Special case (1):
     Object.superclass = nil (will be set in Phase Y)
   ```

3. **`initializedFields` tracking**:
   ```
   480 fields tracked:
     - 240 thisClass fields (all metaclasses)
     - 240 superclass fields (all metaclasses + fundamental classes)
   ```

**Memory Modified**: ~1.9 KB (480 slots × 4 bytes per OOP reference)

**Performance**: ~5-10ms

---

**Critical Invariants After Phase 3**:

```csharp
// Metaclass circle is complete
var metaObjectOOP = _localIdToObjectId["meta:Object"];
var classClassOOP = _localIdToObjectId["class:Class"];
var classDescOOP = _localIdToObjectId["class:ClassDescription"];
var behaviorOOP = _localIdToObjectId["class:Behavior"];
var objectOOP = _localIdToObjectId["class:Object"];

Assert(_heapManager.GetSlot(metaObjectOOP, superclassIndex + 1) == classClassOOP);
Assert(_heapManager.GetSlot(classClassOOP, superclassIndex + 1) == classDescOOP);
Assert(_heapManager.GetSlot(classDescOOP, superclassIndex + 1) == behaviorOOP);
Assert(_heapManager.GetSlot(behaviorOOP, superclassIndex + 1) == objectOOP);

// All metaclasses have thisClass set
foreach (var metaCls in bundle.Classes.Where(c => c.IsMetaclass))
{
    var metaOOP = _localIdToObjectId[metaCls.LocalId];
    var thisClass = _heapManager.GetSlot(metaOOP, thisClassIndex + 1);
    Assert(thisClass != SmalltalkOOP.Null);
}

// Metaclass superclass mirrors instance superclass
var pointOOP = _localIdToObjectId["class:Point"];
var metaPointOOP = _localIdToObjectId["meta:Point"];
// Point.superclass = Object, so meta:Point.superclass should be meta:Object
var metaPointSuper = _heapManager.GetSlot(metaPointOOP, superclassIndex + 1);
Assert(metaPointSuper == _localIdToObjectId["meta:Object"]);
```

---

**Data Structure State After Phase 3**:

```
Heap Object: meta:Object (oop=7678)
  Slot 0: 0 (class reference, unset until Phase X)
  Slot 1: oop=7680 (superclass = class:Class) ← SET IN PHASE 3
  Slot 2: 0 (methodDict, unset until Phase 7)
  Slot 3: 0 (format, unset until Phase 4)
  Slot 4: 0 (instanceVariables, unset)
  Slot 5: 0 (organization, unset)
  Slot 6: oop=7676 (thisClass = class:Object) ← SET IN PHASE 3

Heap Object: class:Class (oop=7680)
  Slot 0: 0 (class reference, unset)
  Slot 1: oop=7682 (superclass = class:ClassDescription) ← SET IN PHASE 3
  Slot 2: 0 (methodDict, unset)
  Slot 3: 0 (format, unset)
  Slot 4-11: 0 (other fields, unset)

Heap Object: meta:Point (oop=8236)
  Slot 0: 0 (class reference, unset)
  Slot 1: oop=7678 (superclass = meta:Object) ← SET IN PHASE 3
  Slot 2: 0 (methodDict, unset)
  Slot 3: 0 (format, unset)
  Slot 4: 0 (instanceVariables, unset)
  Slot 5: 0 (organization, unset)
  Slot 6: oop=8234 (thisClass = class:Point) ← SET IN PHASE 3

initializedFields set:
  (oop=7678, 1) - meta:Object.superclass
  (oop=7678, 5) - meta:Object.thisClass
  (oop=7680, 1) - class:Class.superclass
  (oop=7682, 1) - class:ClassDescription.superclass
  (oop=7684, 1) - class:Behavior.superclass
  (oop=8236, 1) - meta:Point.superclass
  (oop=8236, 5) - meta:Point.thisClass
  ... (480 total entries)
```

---

**Why Not Set class:Object.superclass = nil**:

- nil doesn't exist yet (created in Phase 6)
- Object.superclass = null (0) is temporary
- Will be set to nil in Phase Y (Instance Variable Initialization)
- For now, tracking in `initializedFields` prevents Phase Y from overwriting

---

**Possible Optimizations**:

1. **Combine thisClass and superclass in Single Pass**:
   ```csharp
   foreach (var metaCls in metaclasses)
   {
       SetThisClass(metaCls);
       SetSuperclass(metaCls);
   }
   ```
   **Benefit**: One iteration instead of two
   **Complexity**: Low
   **Savings**: ~2ms

2. **Validate Circle Immediately**:
   ```csharp
   // After wiring, walk the circle to verify
   var current = metaObjectOOP;
   for (int i = 0; i < 4; i++)
   {
       current = _heapManager.GetSlot(current, superclassIndex + 1);
   }
   Assert(current == classObjectOOP); // Should end at Object
   ```
   **Benefit**: Catch errors earlier
   **Complexity**: Low

3. **Pre-calculate Field Indices**:
   ```csharp
   // Calculate once before loop
   int thisClassIdx = FindFieldIndex("Metaclass", "thisClass");
   int superclassIdx = FindFieldIndex("Class", "superclass");
   ```
   **Benefit**: Avoid 480 redundant lookups
   **Complexity**: Very low (already done)

4. **Parallel Metaclass Wiring**:
   ```csharp
   // Wire fundamental circle first (sequential)
   // Then wire regular metaclasses in parallel
   Parallel.ForEach(regularMetaclasses, metaCls => {
       SetSuperclass(metaCls);
   });
   ```
   **Benefit**: 20-30% faster on multi-core
   **Complexity**: Medium (need thread-safe heap manager)

---

**Missing Operations** (potential improvements):

1. **Verify No Circular Chains**:
   ```csharp
   // For regular classes, detect Foo → Bar → Baz → Foo
   var visited = new HashSet<SmalltalkOOP>();
   var current = someClassOOP;
   while (current != objectOOP)
   {
       if (!visited.Add(current))
           throw new InvalidOperationException("Circular superclass chain");
       current = GetSuperclass(current);
   }
   ```

2. **Validate Metaclass/Class Pairing**:
   ```csharp
   // Verify meta:Foo.thisClass.class == Foo
   var metaFooOOP = localIdToObjectId["meta:Foo"];
   var fooOOP = GetThisClass(metaFooOOP);
   // In Phase X, will verify fooOOP.class == metaFooOOP
   ```

3. **Count Inheritance Depth**:
   ```csharp
   // Validate no class is more than 20 levels deep
   // (unusually deep hierarchies may indicate errors)
   ```

---

**Error Cases**:

1. **Missing Superclass**:
   - **Cause**: Bundle references non-existent class in SuperNameIndex
   - **Handling**: Log warning, skip that metaclass
   - **Should Do**: Fail in Phase 0 validation

2. **Circular Inheritance** (besides metaclass circle):
   - **Cause**: Foo.superclass = Bar, Bar.superclass = Foo
   - **Handling**: Currently not detected, would cause infinite loops later
   - **Should Do**: Detect in Phase 0 or Phase 3

3. **Missing thisClass**:
   - **Cause**: Metaclass definition doesn't have thisClass field
   - **Handling**: FindInstanceVariableIndex throws exception
   - **Should Do**: Caught, abort coldstart with diagnostic

---

**Alternative Approaches**:

1. **Lazy Metaclass Wiring**:
   ```
   Don't wire all metaclasses now
   Wire on-demand during method lookup
   ```
   **Benefit**: Faster coldstart (defer work)
   **Drawback**: Slower first method calls, more complex VM

2. **Explicit Circle Validation**:
   ```
   After wiring, walk from meta:Object through circle
   Verify returns to Object after exactly 4 hops
   ```
   **Benefit**: Catch errors immediately
   **Current**: Implicitly verified by later phases

3. **Two-Phase Metaclass Wiring**:
   ```
   Phase 3a: Wire fundamental circle only
   Phase 3b: Wire regular metaclasses
   ```
   **Benefit**: Clearer separation of concerns
   **Current**: Combined in one phase

---

**Phase 3 Summary**:
- **Input**: Class shells with all fields = 0
- **Output**: All metaclass hierarchies wired, fundamental circle complete
- **Fields Set**: 480 (240 thisClass + 240 superclass)
- **Memory**: ~1.9 KB
- **Time**: ~5-10ms
- **Next Phase**: Phase 4 (resolve regular class metadata)



---

## Phase 4: Class Resolution

**Purpose**: Populate class metadata fields (name, superclass, format, classPool) for all 480 classes, establishing the complete inheritance hierarchy.

**Input**:
- 480 class shells (allocated in Phase 2)
- JSON class definitions with metadata
- Symbol registry (frozen from Phase 1)
- Metaclass wiring (from Phase 3)

**Output**:
- All classes have name symbols set
- Complete inheritance hierarchy (except Object.superclass, which requires nil)
- Format fields configured for all classes
- Class pools allocated where needed

**Why This Phase Exists**: Class shells from Phase 2 have correct structure but no metadata. Before we can allocate instances, we need to know:
1. What each class is named (for debugging, lookups)
2. What the inheritance chain is (for method lookup)
3. What the object format is (PointerIndexed, ByteIndexed, WordIndexed)
4. Whether class-side variables exist (classPool)

**Critical Constraint**: We CANNOT set Object.superclass to nil yet because nil doesn't exist. That happens in Phase 6.

### Step-by-Step Process

#### Step 1: Set Class Names (480 operations)

For each class in bundle.Classes:

```csharp
foreach (var cls in bundle.Classes)
{
    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

    // Look up the name symbol (already interned in Phase 1)
    int symbolIndex = cls.NameSymbolIndex;
    SmalltalkOOP nameSymbolOOP = SymbolRegistry.GetSymbol(symbolIndex);

    // Get the 'name' field index from the class definition
    // For regular classes: index from class:Class hierarchy
    // For metaclasses: index from class:Metaclass hierarchy
    string templateClassName = cls.IsMetaclass ? "Metaclass" : "Class";
    int nameFieldIndex = _layoutResolver.GetFieldIndex(
        $"class:{templateClassName}",
        "name"
    );

    // Set the name field
    _heapManager.SetInstanceVariable(classOOP, nameFieldIndex, nameSymbolOOP);

    // Track that this field has been explicitly set
    _initializedFields.Add((classOOP, nameFieldIndex));
}
```

**Example**: For class:Point (localId="class:Point"):
```
symbolIndex = 842 (from cls.NameSymbolIndex)
nameSymbolOOP = SymbolRegistry.GetSymbol(842) → OOP #12456 (contains "Point")
nameFieldIndex = 6 (the 'name' field in class:Class hierarchy)
_heapManager.SetInstanceVariable(pointClassOOP, 6, #12456)
```

**Memory Impact**: 480 OOP slots written = 1,920 bytes (each OOP is 4 bytes)

**Performance**: ~5-10ms (simple array lookups and writes)

#### Step 2: Set Superclass References (476 operations)

For each class EXCEPT Object, Metaclass, nil, and UndefinedObject:

```csharp
foreach (var cls in bundle.Classes)
{
    // Skip classes that have no superclass or special handling
    if (cls.ClassName == "Object" ||
        cls.ClassName == "Metaclass" ||
        cls.ClassName == "UndefinedObject" ||
        cls.SuperclassLocalId == null)
        continue;

    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];
    SmalltalkOOP superclassOOP = _localIdToObjectId[cls.SuperclassLocalId];

    // Get the 'superclass' field index
    string templateClassName = cls.IsMetaclass ? "Metaclass" : "Class";
    int superclassFieldIndex = _layoutResolver.GetFieldIndex(
        $"class:{templateClassName}",
        "superclass"
    );

    // Set the superclass field
    _heapManager.SetInstanceVariable(classOOP, superclassFieldIndex, superclassOOP);
    _initializedFields.Add((classOOP, superclassFieldIndex));
}
```

**Special Cases NOT Handled Here**:
```csharp
// Object.superclass = nil (requires nil singleton from Phase 6)
// Metaclass.superclass = class:Class (already set in Phase 3)
// meta:Object.superclass = class:Class (already set in Phase 3)
// UndefinedObject has no superclass in JSON
```

**Example**: For class:Point (superclass is class:Object):
```
cls.SuperclassLocalId = "class:Object"
superclassOOP = _localIdToObjectId["class:Object"] → OOP #3456
superclassFieldIndex = 0 (the 'superclass' field)
_heapManager.SetInstanceVariable(pointClassOOP, 0, #3456)
```

**Inheritance Chain Example After This Step**:
```
class:Point.superclass = class:Object
class:Object.superclass = <UNSET> (will be nil in Phase 6)
class:Collection.superclass = class:Object
class:SequenceableCollection.superclass = class:Collection
class:ArrayedCollection.superclass = class:SequenceableCollection
class:Array.superclass = class:ArrayedCollection
```

**Memory Impact**: 476 OOP slots = 1,904 bytes

**Performance**: ~5-10ms

#### Step 3: Set Format Fields (480 operations)

The format field encodes the object layout specification for instances of this class.

**Format Word Encoding** (16 bits):
```
Bits 0-7:   Instance variable count (0-255)
Bits 8-11:  Size of indexed portion (0-15, only used for CompiledMethod)
Bits 12-13: Instance specification:
            0 = No indexed portion (fixed-size objects)
            1 = PointerIndexed (Array, MethodDictionary, etc.)
            2 = WordIndexed (Bitmap, IntegerArray)
            3 = ByteIndexed (String, Symbol, ByteArray)
Bits 14-15: Reserved (must be 0)
```

```csharp
foreach (var cls in bundle.Classes)
{
    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

    // Decode the format word from JSON
    ushort formatWord = cls.Format;

    // Extract components
    int instVarCount = formatWord & 0xFF;                    // Bits 0-7
    int indexedSize = (formatWord >> 8) & 0x0F;             // Bits 8-11
    int instSpec = (formatWord >> 12) & 0x03;               // Bits 12-13

    // Convert to SmallInteger OOP (tag with 1 in low bit)
    SmalltalkOOP formatOOP = SmalltalkOOP.FromSmallInteger(formatWord);

    // Get the 'format' field index
    string templateClassName = cls.IsMetaclass ? "Metaclass" : "Class";
    int formatFieldIndex = _layoutResolver.GetFieldIndex(
        $"class:{templateClassName}",
        "format"
    );

    // Set the format field
    _heapManager.SetInstanceVariable(classOOP, formatFieldIndex, formatOOP);
    _initializedFields.Add((classOOP, formatFieldIndex));
}
```

**Example Format Values**:

```csharp
// class:Point - Fixed-size with 2 instance variables (x, y)
formatWord = 0x0002  // Binary: 0000 00 00 0000 0010
                     // instSpec=0 (fixed), indexedSize=0, instVarCount=2

// class:Array - PointerIndexed with 0 instance variables
formatWord = 0x1000  // Binary: 0001 00 00 0000 0000
                     // instSpec=1 (pointer), indexedSize=0, instVarCount=0

// class:String - ByteIndexed with 0 instance variables
formatWord = 0x3000  // Binary: 0011 00 00 0000 0000
                     // instSpec=3 (byte), indexedSize=0, instVarCount=0

// class:CompiledMethod - ByteIndexed with variable header size
formatWord = 0x300C  // Binary: 0011 00 00 0000 1100
                     // instSpec=3 (byte), indexedSize=12, instVarCount=0
```

**Why SmallInteger Encoding?**: Format is stored as a SmallInteger in Smalltalk-80 so primitives can read it efficiently without dereferencing an object pointer.

**Memory Impact**: 480 SmallInteger values stored = 1,920 bytes (OOPs are immediate-encoded SmallIntegers)

**Performance**: ~5-10ms (simple bitwise operations and writes)

#### Step 4: Allocate Class Pools (varies by class)

Some classes have class-side variables stored in a Dictionary called the "class pool". For example:

```csharp
// Classes with class pools (from JSON):
class:Character → pool with 256 entries (all ASCII characters)
class:Float → pool with constants (Pi, E, Infinity, etc.)
class:SmallInteger → pool with min/max values
// ... and many others
```

**Allocation Process**:

```csharp
foreach (var cls in bundle.Classes)
{
    if (cls.ClassPool == null || cls.ClassPool.Count == 0)
        continue; // No class pool needed

    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

    // Allocate an empty Dictionary for the class pool
    // The actual key-value pairs are installed in Phase 13 (Global Variable Installation)
    int capacity = NextPowerOfTwo(cls.ClassPool.Count * 2); // 2x for load factor

    SmalltalkOOP dictionaryClassOOP = _localIdToObjectId["class:Dictionary"];
    SmalltalkOOP poolDictOOP = _allocator.AllocateArray(
        dictionaryClassOOP,
        capacity
    );

    // Get the 'classPool' field index
    int poolFieldIndex = _layoutResolver.GetFieldIndex(
        $"class:{cls.IsMetaclass ? "Metaclass" : "Class"}",
        "classPool"
    );

    // Set the classPool field
    _heapManager.SetInstanceVariable(classOOP, poolFieldIndex, poolDictOOP);
    _initializedFields.Add((classOOP, poolFieldIndex));

    // Store for later population in Phase 13
    _classPoolDict[cls.LocalId] = poolDictOOP;
}
```

**Example**: class:Character has 256 class variables (all ASCII/extended characters):

```
cls.ClassPool.Count = 256
capacity = NextPowerOfTwo(512) = 512 slots
poolDictOOP = AllocateArray(class:Dictionary, 512)
_heapManager.SetInstanceVariable(characterClassOOP, poolFieldIndex, poolDictOOP)
```

**Classes with Class Pools** (estimated from typical Smalltalk-80):
- Character (~256 entries)
- Float (~10 entries)
- SmallInteger (~5 entries)
- Display (~10 entries)
- Sensor (~20 entries)
- ~30-40 classes total

**Memory Impact**:
- ~40 Dictionary objects allocated
- Average capacity: ~100 slots each
- Total: ~4,000 slots × 4 bytes = 16 KB

**Performance**: ~10-15ms (Dictionary allocation is more expensive)

### Data Structure State After Phase 4

#### Complete Class Metadata Example: class:Point

```csharp
// Object Table Entry for class:Point
OOP: #5678
ClassOOP: #1234 (points to class:Class)
Format: PointerIndexed
InstanceVarCount: 6 (from class:Class hierarchy)
IndexedSize: 0

// Instance Variables (from class:Class hierarchy):
Slot 0 (superclass):    OOP #3456 → class:Object
Slot 1 (methodDict):    <UNSET> (installed in Phase 7)
Slot 2 (format):        SmallInteger(0x0002) = 2 instance vars, fixed-size
Slot 3 (instanceVars):  <UNSET> (may be installed later)
Slot 4 (organization):  <UNSET> (may be installed later)
Slot 5 (subclasses):    <UNSET> (may be installed later)
Slot 6 (name):          OOP #12456 → Symbol("Point")
Slot 7 (classPool):     <UNSET> (Point has no class pool)
Slot 8 (sharedPools):   <UNSET> (Point has no shared pools)
```

#### Inheritance Chain Example: Array Hierarchy

```
class:Object.superclass          = <UNSET> (will be nil in Phase 6)
class:Collection.superclass      = OOP → class:Object
class:SequenceableCollection.superclass = OOP → class:Collection
class:ArrayedCollection.superclass = OOP → class:SequenceableCollection
class:Array.superclass           = OOP → class:ArrayedCollection
```

#### Format Examples After Phase 4

```csharp
// Fixed-size classes (instSpec=0)
class:Point.format        = SmallInteger(0x0002) // 2 inst vars (x, y)
class:Rectangle.format    = SmallInteger(0x0004) // 4 inst vars (origin, corner, etc.)
class:Association.format  = SmallInteger(0x0002) // 2 inst vars (key, value)

// PointerIndexed classes (instSpec=1)
class:Array.format        = SmallInteger(0x1000) // 0 inst vars, variable size
class:MethodDictionary.format = SmallInteger(0x1001) // 1 inst var (tally), variable size
class:Context.format      = SmallInteger(0x1006) // 6 inst vars, variable stack

// WordIndexed classes (instSpec=2)
class:Bitmap.format       = SmallInteger(0x2000) // 0 inst vars, word array
class:IntegerArray.format = SmallInteger(0x2000) // 0 inst vars, word array

// ByteIndexed classes (instSpec=3)
class:String.format       = SmallInteger(0x3000) // 0 inst vars, byte array
class:Symbol.format       = SmallInteger(0x3000) // 0 inst vars, byte array
class:ByteArray.format    = SmallInteger(0x3000) // 0 inst vars, byte array
class:CompiledMethod.format = SmallInteger(0x300C) // 0 inst vars, 12-literal header
```

### Critical Invariants After Phase 4

```csharp
// Invariant 1: All classes have names
Debug.Assert(_bundle.Classes.All(cls =>
{
    var classOOP = _localIdToObjectId[cls.LocalId];
    var nameIndex = _layoutResolver.GetFieldIndex(
        $"class:{(cls.IsMetaclass ? "Metaclass" : "Class")}", "name");
    return _initializedFields.Contains((classOOP, nameIndex));
}));

// Invariant 2: All classes (except special cases) have superclasses set
Debug.Assert(_bundle.Classes
    .Where(cls => cls.ClassName != "Object" &&
                  cls.ClassName != "UndefinedObject" &&
                  cls.SuperclassLocalId != null)
    .All(cls =>
    {
        var classOOP = _localIdToObjectId[cls.LocalId];
        var superIndex = _layoutResolver.GetFieldIndex(
            $"class:{(cls.IsMetaclass ? "Metaclass" : "Class")}", "superclass");
        return _initializedFields.Contains((classOOP, superIndex));
    }));

// Invariant 3: All classes have format fields
Debug.Assert(_bundle.Classes.All(cls =>
{
    var classOOP = _localIdToObjectId[cls.LocalId];
    var formatIndex = _layoutResolver.GetFieldIndex(
        $"class:{(cls.IsMetaclass ? "Metaclass" : "Class")}", "format");
    return _initializedFields.Contains((classOOP, formatIndex));
}));

// Invariant 4: Format values are valid SmallIntegers
Debug.Assert(_bundle.Classes.All(cls =>
{
    ushort formatWord = cls.Format;
    int instSpec = (formatWord >> 12) & 0x03;
    return instSpec >= 0 && instSpec <= 3; // Valid instance spec
}));

// Invariant 5: Classes with class pools have dictionaries allocated
Debug.Assert(_bundle.Classes
    .Where(cls => cls.ClassPool != null && cls.ClassPool.Count > 0)
    .All(cls => _classPoolDict.ContainsKey(cls.LocalId)));
```

### Memory Usage Summary

| Operation | Count | Bytes per Item | Total Bytes |
|-----------|-------|----------------|-------------|
| Set names | 480 | 4 (OOP) | 1,920 |
| Set superclasses | 476 | 4 (OOP) | 1,904 |
| Set formats | 480 | 4 (SmallInt) | 1,920 |
| Allocate class pools | ~40 | ~400 avg | ~16,000 |
| **TOTAL** | | | **~21.7 KB** |

### Performance Metrics

- **Expected Duration**: 20-30ms
- **Allocations**: ~40 Dictionary objects for class pools
- **Writes**: ~1,436 instance variable writes
- **Lookups**: ~1,436 LayoutResolver.GetFieldIndex() calls (cached)

### Optimizations

**Optimization 1: Batch LayoutResolver Queries**
```csharp
// CURRENT: Query field index for every class
foreach (var cls in bundle.Classes)
{
    int nameIndex = _layoutResolver.GetFieldIndex(..., "name");
    // ...
}

// OPTIMIZED: Query once, reuse for all classes
int regularClassNameIndex = _layoutResolver.GetFieldIndex("class:Class", "name");
int metaclassNameIndex = _layoutResolver.GetFieldIndex("class:Metaclass", "name");

foreach (var cls in bundle.Classes)
{
    int nameIndex = cls.IsMetaclass ? metaclassNameIndex : regularClassNameIndex;
    // ...
}

// IMPACT: Reduces LayoutResolver queries from ~1,440 to ~8
```

**Optimization 2: Pre-size Class Pool Dictionaries**
```csharp
// CURRENT: Use power-of-2 sizing with 2x load factor
int capacity = NextPowerOfTwo(cls.ClassPool.Count * 2);

// OPTIMIZED: Use exact size (Dictionary will resize if needed)
int capacity = cls.ClassPool.Count;

// IMPACT: Reduces wasted space by ~50% (~8 KB saved)
```

**Optimization 3: Defer Class Pool Allocation**
```csharp
// CURRENT: Allocate empty Dictionaries in Phase 4, populate in Phase 13

// OPTIMIZED: Allocate AND populate in Phase 13 (single pass)
// - Eliminates need for _classPoolDict tracking
// - Reduces total phase count
// - Simplifies data flow

// IMPACT: Saves ~10ms, removes intermediate data structure
```

**Optimization 4: Parallel Class Processing**
```csharp
// CURRENT: Sequential processing of 480 classes

// OPTIMIZED: Parallel processing (no dependencies between classes in this phase)
Parallel.ForEach(bundle.Classes, cls =>
{
    // Set name, superclass, format in parallel
});

// IMPACT: Could reduce 30ms → 10ms on multi-core systems
// RISK: Requires thread-safe HeapManager (currently not guaranteed)
```

**Optimization 5: Avoid SmallInteger OOP Creation**
```csharp
// CURRENT: Convert format to SmallInteger OOP every time
SmalltalkOOP formatOOP = SmalltalkOOP.FromSmallInteger(formatWord);

// OPTIMIZED: Pre-compute all possible format OOPs (only 256 unique values)
var formatCache = new Dictionary<ushort, SmalltalkOOP>();
foreach (var cls in bundle.Classes)
{
    if (!formatCache.ContainsKey(cls.Format))
        formatCache[cls.Format] = SmalltalkOOP.FromSmallInteger(cls.Format);
}

// IMPACT: Minimal (SmallInteger tagging is cheap), but cleaner code
```

### Error Cases

**Error 1: Missing Superclass Reference**
```csharp
// SYMPTOM: SuperclassLocalId not found in localIdToObjectId
// EXAMPLE: cls.SuperclassLocalId = "class:FooBar", but FooBar not in bundle

// DETECTION:
if (!_localIdToObjectId.ContainsKey(cls.SuperclassLocalId))
    throw new InvalidOperationException(
        $"Class {cls.ClassName} references unknown superclass {cls.SuperclassLocalId}");

// CAUSE: Malformed JSON bundle (missing class definition)
// FIX: Should be caught in Phase 0 (Preflight Validation)
```

**Error 2: Invalid Format Word**
```csharp
// SYMPTOM: Format word has invalid instance spec or reserved bits set
// EXAMPLE: formatWord = 0xC000 (instSpec=3, but reserved bits set)

// DETECTION:
int instSpec = (formatWord >> 12) & 0x03;
int reserved = (formatWord >> 14) & 0x03;
if (reserved != 0)
    throw new InvalidOperationException(
        $"Class {cls.ClassName} has invalid format word: 0x{formatWord:X4} (reserved bits set)");

// CAUSE: Corrupted JSON bundle or incorrect encoding
// FIX: Regenerate JSON bundle from source image
```

**Error 3: Class Pool Allocation Failure**
```csharp
// SYMPTOM: Cannot allocate Dictionary for class pool (OOM or heap corruption)

// DETECTION:
try {
    var poolDictOOP = _allocator.AllocateArray(dictionaryClassOOP, capacity);
} catch (OutOfMemoryException) {
    throw new InvalidOperationException(
        $"Failed to allocate class pool for {cls.ClassName} (capacity={capacity})");
}

// CAUSE: Insufficient heap space or excessive class pool size
// FIX: Increase heap size or reduce class pool entries
```

**Error 4: Field Index Not Found**
```csharp
// SYMPTOM: LayoutResolver cannot find field index for "name", "superclass", etc.

// DETECTION:
int nameIndex = _layoutResolver.GetFieldIndex("class:Class", "name");
if (nameIndex == -1)
    throw new InvalidOperationException(
        "LayoutResolver missing field 'name' for class:Class - Phase 5 not run?");

// CAUSE: Phase 5 (Layout Computation) not completed or LayoutResolver not populated
// FIX: Ensure phases run in correct order
```

### Alternative Approaches

**Alternative 1: Lazy Format Decoding**
```csharp
// CURRENT APPROACH: Store format as SmallInteger in class object

// ALTERNATIVE: Store raw format word in object table entry, decode on demand
// - Saves 1,920 bytes (no format field in class objects)
// - Faster allocation (no SmallInteger tagging)
// - Requires VM change to read format from object table instead of class

// TRADE-OFF: Less portable (format becomes VM detail, not Smalltalk-accessible)
```

**Alternative 2: Deferred Superclass Setting**
```csharp
// CURRENT APPROACH: Set superclasses in Phase 4, except Object (deferred to Phase 6)

// ALTERNATIVE: Defer ALL superclass setting to Phase 6 (after nil exists)
// - Simpler logic (no special cases)
// - All classes handled uniformly

// TRADE-OFF: Delays inheritance chain availability (could affect Phase 5 if needed)
```

**Alternative 3: Combined Metadata Phase**
```csharp
// CURRENT APPROACH: Separate phases for class metadata (Phase 4) and layouts (Phase 5)

// ALTERNATIVE: Combine into single "Class Initialization" phase
// - Set name, superclass, format, AND compute layouts in one pass
// - Reduces phase count

// TRADE-OFF: More complex phase with multiple responsibilities (violates SRP)
```

**Alternative 4: Incremental LayoutResolver Population**
```csharp
// CURRENT APPROACH: Populate LayoutResolver in Phase 5 after all class metadata set

// ALTERNATIVE: Populate LayoutResolver incrementally as each class is resolved
// - Could enable earlier access to layout information
// - Reduces peak memory usage (no need to hold all layouts)

// TRADE-OFF: More complex dependency management (which classes need layouts first?)
```

### What's Still Missing After Phase 4

**Missing Operation 1: Object.superclass Setting**
```csharp
// NOT SET YET: class:Object.superclass = nil
// WHY: nil singleton doesn't exist until Phase 6
// WHEN: Phase 6 (Object Allocation) creates nil, then sets Object.superclass
```

**Missing Operation 2: Method Dictionaries**
```csharp
// NOT SET YET: cls.methodDict field for any class
// WHY: Method dictionaries allocated in Phase 7, methods installed in Phase 8
// WHEN: Phase 7 allocates empty dictionaries, Phase 9 installs them
```

**Missing Operation 3: Instance Variable Names**
```csharp
// NOT SET YET: cls.instanceVars field (Array of variable name symbols)
// WHY: Not critical for VM operation, may be set later or left nil
// WHEN: Phase 13 (Global Variable Installation) or never
```

**Missing Operation 4: Class Organization**
```csharp
// NOT SET YET: cls.organization field (method categories, protocols)
// WHY: Not critical for VM operation, used by browsers/tools
// WHEN: Phase 13 or never (depends on JSON bundle contents)
```

**Missing Operation 5: Subclass Lists**
```csharp
// NOT SET YET: cls.subclasses field (Array of direct subclasses)
// WHY: Can be computed from superclass links, not critical for VM
// WHEN: Phase 13 or never (depends on JSON bundle contents)
```

### Next Phase Preview

**Phase 5: Layout Computation** will:
1. Calculate total instance variable counts for all classes (including inherited)
2. Build field name → index mappings for each class
3. Populate LayoutResolver cache with all class layouts
4. Validate no index collisions or missing fields
5. Make LayoutResolver available for runtime use

This enables Phase 6 to allocate objects with correct sizes and Phase Y to initialize instance variables by name.


---

## Phase 5: Layout Loading (v1.0.6+)

**Purpose**: Load precomputed instance variable layouts for all 480 classes into the LayoutResolver cache to enable runtime field access by name.

**v1.0.6 Change**: This phase was dramatically simplified in v1.0.6. Previously, this phase computed layouts at runtime via topological sort and inheritance chain traversal (~20-25ms). Now the compiler precomputes all layouts during bundle generation, and this phase simply loads the data (~2-5ms), achieving an ~18ms performance improvement.

**Input**:
- 480 classes with precomputed field layouts (from JSON bundle v1.0.6+)
- Each class has `TotalInstanceVarCount` and `FieldLayout[]` array
- Symbol table for resolving field name indices

**Output**:
- LayoutResolver populated with field name → index mappings for all classes
- Runtime-ready layout information for object allocation and field access

**Why This Phase Exists**: The LayoutResolver is the primary mechanism for translating field names to slot indices during both coldstart and runtime. We need:
1. Field name → slot index mappings loaded into memory
2. Cached layouts to avoid symbol table lookups during execution
3. Fast access to field indices for object initialization and field access

**Critical Requirement**: This phase must run BEFORE Phase 6 (Object Allocation) and Phase Y (Instance Variable Initialization) because both need accurate field indices.

**Performance**:
- **v1.0.5 (Runtime Computation)**: ~20-25ms (topological sort + inheritance traversal for 480 classes)
- **v1.0.6 (Precomputed Loading)**: ~2-5ms (simple array iteration)
- **Improvement**: ~18ms faster (~80-90% reduction)

### Background: Instance Variable Inheritance

In Smalltalk-80, instance variables are inherited and laid out sequentially:

```
class:Object
  instanceVariables: [] (none)
  → Slots: none

class:Collection (superclass: Object)
  instanceVariables: [] (none in Collection itself)
  → Slots: none

class:SequenceableCollection (superclass: Collection)
  instanceVariables: [] (still none)
  → Slots: none

class:ArrayedCollection (superclass: SequenceableCollection)
  instanceVariables: [] (still none)
  → Slots: none

class:Array (superclass: ArrayedCollection)
  instanceVariables: [] (no instance vars, only indexed portion)
  → Slots: none (but has variable-length indexed storage)

class:Point (superclass: Object)
  instanceVariables: ['x', 'y']
  → Slots:
    0: x
    1: y

class:Rectangle (superclass: Object)
  instanceVariables: ['origin', 'corner']
  → Slots:
    0: origin
    1: corner

class:MethodContext (superclass: Object)
  instanceVariables: ['sender', 'ip', 'sp', 'method', 'receiver', 'homeContext']
  → Slots:
    0: sender
    1: ip
    2: sp
    3: method
    4: receiver
    5: homeContext
```

**Inheritance Example**:
```
class:Behavior (superclass: Object)
  instanceVariables: ['superclass', 'methodDict', 'format']
  → Slots:
    0: superclass
    1: methodDict
    2: format

class:ClassDescription (superclass: Behavior)
  instanceVariables: ['instanceVariables', 'organization']
  → Slots:
    0: superclass         (inherited from Behavior)
    1: methodDict         (inherited from Behavior)
    2: format             (inherited from Behavior)
    3: instanceVariables  (own)
    4: organization       (own)

class:Class (superclass: ClassDescription)
  instanceVariables: ['subclasses', 'name', 'classPool', 'sharedPools', 'environment', 'category']
  → Slots:
    0: superclass         (inherited from Behavior)
    1: methodDict         (inherited from Behavior)
    2: format             (inherited from Behavior)
    3: instanceVariables  (inherited from ClassDescription)
    4: organization       (inherited from ClassDescription)
    5: subclasses         (own)
    6: name               (own)
    7: classPool          (own)
    8: sharedPools        (own)
    9: environment        (own)
    10: category          (own)
```

### v1.0.6+ Simplified Process

Phase 5 in v1.0.6+ is dramatically simpler - it just loads precomputed layouts from the JSON bundle.

#### Step 1: Load Precomputed Field Layouts

For each class in the bundle, load the precomputed field layout from the JSON into the LayoutResolver cache.

```csharp
public void LoadLayouts(IEnumerable<ClassDescriptor> classes, IReadOnlyList<string> symbols)
{
    foreach (var cls in classes)
    {
        // v1.0.6+: Load precomputed field layout (already in correct order)
        var fields = new List<string>(cls.FieldLayout.Length);

        foreach (var entry in cls.FieldLayout)
        {
            string fieldName = symbols[entry.NameIndex];
            fields.Add(fieldName);
        }

        _fieldMap[cls.LocalId] = fields;
    }
}
```

**Key Improvements**:
- **No topological sort needed**: Layouts are already computed in correct inheritance order by the compiler
- **No inheritance traversal**: Each class's layout includes all inherited fields precomputed
- **Simple iteration**: Just load symbol names and store in dictionary
- **Performance**: O(N) where N = total number of fields across all classes (~2,000 fields, ~2-5ms vs ~20-25ms in v1.0.5)

**That's it!** The entire phase is now just a simple loop loading precomputed data.

---

### v1.0.5 Legacy Process (Deprecated - For Historical Reference Only)

The following describes the OLD runtime computation approach used in v1.0.5 and earlier. This is no longer used.

#### OLD Step 1: Compute Instance Variable Counts (v1.0.5)

For each class, calculate the total number of instance variables including inherited ones.

```csharp
var instanceVarCounts = new Dictionary<string, int>();

// Process classes in dependency order (superclass before subclass)
var sortedClasses = TopologicalSort(bundle.Classes);

foreach (var cls in sortedClasses)
{
    int totalCount = 0;

    // Get superclass instance variable count
    if (cls.SuperclassLocalId != null)
    {
        if (instanceVarCounts.TryGetValue(cls.SuperclassLocalId, out int superCount))
        {
            totalCount = superCount;
        }
        else
        {
            throw new InvalidOperationException(
                $"Superclass {cls.SuperclassLocalId} not processed before {cls.LocalId}");
        }
    }

    // Add this class's own instance variables
    if (cls.InstanceVariableNames != null)
    {
        totalCount += cls.InstanceVariableNames.Length;
    }

    // Store the total count
    instanceVarCounts[cls.LocalId] = totalCount;

    // Populate LayoutResolver with the count
    _layoutResolver.SetInstanceVariableCount(cls.LocalId, totalCount);
}
```

**Example Results**:
```csharp
instanceVarCounts["class:Object"] = 0
instanceVarCounts["class:Behavior"] = 3  // superclass, methodDict, format
instanceVarCounts["class:ClassDescription"] = 5  // 3 inherited + 2 own
instanceVarCounts["class:Class"] = 11  // 5 inherited + 6 own
instanceVarCounts["class:Point"] = 2  // 0 inherited + 2 own (x, y)
instanceVarCounts["class:MethodContext"] = 6  // 0 inherited + 6 own
```

**Topological Sort**: Classes must be processed in superclass-first order to ensure inherited counts are available. This is a depth-first traversal starting from Object.

```csharp
private List<ClassDefinition> TopologicalSort(ClassDefinition[] classes)
{
    var sorted = new List<ClassDefinition>();
    var visited = new HashSet<string>();

    void Visit(ClassDefinition cls)
    {
        if (visited.Contains(cls.LocalId))
            return;

        // Visit superclass first
        if (cls.SuperclassLocalId != null)
        {
            var superclass = classes.FirstOrDefault(c => c.LocalId == cls.SuperclassLocalId);
            if (superclass != null)
                Visit(superclass);
        }

        visited.Add(cls.LocalId);
        sorted.Add(cls);
    }

    foreach (var cls in classes)
        Visit(cls);

    return sorted;
}
```

**Performance**: O(N) where N = 480 classes (each class visited once)

**Memory Impact**: 480 integers stored = 1,920 bytes

#### Step 2: Build Field Name → Index Mappings

For each class, create a mapping from field name to slot index, including inherited fields.

```csharp
foreach (var cls in sortedClasses)
{
    var fieldMap = new Dictionary<string, int>();
    int currentIndex = 0;

    // Step 2a: Add inherited fields
    if (cls.SuperclassLocalId != null)
    {
        // Get superclass field map
        var superFieldMap = _layoutResolver.GetFieldMap(cls.SuperclassLocalId);
        if (superFieldMap == null)
        {
            throw new InvalidOperationException(
                $"Superclass {cls.SuperclassLocalId} layout not computed before {cls.LocalId}");
        }

        // Copy all superclass fields
        foreach (var kvp in superFieldMap)
        {
            fieldMap[kvp.Key] = kvp.Value;
            currentIndex = Math.Max(currentIndex, kvp.Value + 1);
        }
    }

    // Step 2b: Add this class's own fields
    if (cls.InstanceVariableNames != null)
    {
        foreach (var fieldName in cls.InstanceVariableNames)
        {
            if (fieldMap.ContainsKey(fieldName))
            {
                throw new InvalidOperationException(
                    $"Field name collision: {fieldName} already exists in {cls.LocalId} hierarchy");
            }

            fieldMap[fieldName] = currentIndex++;
        }
    }

    // Step 2c: Store in LayoutResolver
    _layoutResolver.SetFieldMap(cls.LocalId, fieldMap);
}
```

**Example Field Map for class:Class**:
```csharp
fieldMap["class:Class"] = {
    "superclass" → 0         // from Behavior
    "methodDict" → 1         // from Behavior
    "format" → 2             // from Behavior
    "instanceVariables" → 3  // from ClassDescription
    "organization" → 4       // from ClassDescription
    "subclasses" → 5         // from Class
    "name" → 6               // from Class
    "classPool" → 7          // from Class
    "sharedPools" → 8        // from Class
    "environment" → 9        // from Class
    "category" → 10          // from Class
}
```

**Example Field Map for class:Point**:
```csharp
fieldMap["class:Point"] = {
    "x" → 0
    "y" → 1
}
```

**Performance**: O(N × M) where N = 480 classes, M = average inheritance depth (~5)
- Total iterations: ~2,400 field mappings
- Time: ~10-15ms

**Memory Impact**:
- 480 dictionaries with ~5-10 entries each
- ~2,400 field name strings + indices
- Total: ~20-30 KB

#### Step 3: Validate Layouts

Ensure no errors or inconsistencies in the computed layouts.

```csharp
foreach (var cls in bundle.Classes)
{
    // Validation 1: Instance variable count matches field map size
    int expectedCount = instanceVarCounts[cls.LocalId];
    var fieldMap = _layoutResolver.GetFieldMap(cls.LocalId);
    if (fieldMap.Count != expectedCount)
    {
        throw new InvalidOperationException(
            $"Class {cls.LocalId}: field map size ({fieldMap.Count}) != " +
            $"instance var count ({expectedCount})");
    }

    // Validation 2: No gaps in indices (should be 0, 1, 2, ..., N-1)
    var indices = fieldMap.Values.OrderBy(i => i).ToList();
    for (int i = 0; i < indices.Count; i++)
    {
        if (indices[i] != i)
        {
            throw new InvalidOperationException(
                $"Class {cls.LocalId}: field indices have gap at {i}");
        }
    }

    // Validation 3: All field names are unique
    var fieldNames = fieldMap.Keys.ToList();
    if (fieldNames.Distinct().Count() != fieldNames.Count)
    {
        throw new InvalidOperationException(
            $"Class {cls.LocalId}: duplicate field names detected");
    }

    // Validation 4: Inherited fields match superclass exactly
    if (cls.SuperclassLocalId != null)
    {
        var superFieldMap = _layoutResolver.GetFieldMap(cls.SuperclassLocalId);
        foreach (var kvp in superFieldMap)
        {
            if (!fieldMap.ContainsKey(kvp.Key) || fieldMap[kvp.Key] != kvp.Value)
            {
                throw new InvalidOperationException(
                    $"Class {cls.LocalId}: inherited field {kvp.Key} has wrong index");
            }
        }
    }
}

Console.WriteLine($"✓ Layout validation passed for all 480 classes");
```

**Performance**: O(N × M) where N = 480, M = ~10 fields per class
- Time: ~5-10ms

#### Step 4: Freeze LayoutResolver

Mark the LayoutResolver as read-only to prevent further modifications during runtime.

```csharp
_layoutResolver.Freeze();

Console.WriteLine($"✓ LayoutResolver frozen with {instanceVarCounts.Count} class layouts");
```

**Why Freeze?**: After Phase 5, no more layout changes should occur. Freezing prevents accidental modifications and enables optimizations (e.g., caching, lock-free reads).

### Data Structure State After Phase 5

#### LayoutResolver Internal State

```csharp
// LayoutResolver contains:

Dictionary<string, int> _instanceVarCounts = {
    ["class:Object"] = 0,
    ["class:Point"] = 2,
    ["class:Rectangle"] = 2,
    ["class:Behavior"] = 3,
    ["class:ClassDescription"] = 5,
    ["class:Class"] = 11,
    ["class:MethodContext"] = 6,
    // ... 480 entries total
};

Dictionary<string, Dictionary<string, int>> _fieldMaps = {
    ["class:Point"] = {
        ["x"] = 0,
        ["y"] = 1
    },
    ["class:Rectangle"] = {
        ["origin"] = 0,
        ["corner"] = 1
    },
    ["class:Behavior"] = {
        ["superclass"] = 0,
        ["methodDict"] = 1,
        ["format"] = 2
    },
    ["class:Class"] = {
        ["superclass"] = 0,
        ["methodDict"] = 1,
        ["format"] = 2,
        ["instanceVariables"] = 3,
        ["organization"] = 4,
        ["subclasses"] = 5,
        ["name"] = 6,
        ["classPool"] = 7,
        ["sharedPools"] = 8,
        ["environment"] = 9,
        ["category"] = 10
    },
    // ... 480 entries total
};

bool _frozen = true;
```

#### Usage Examples After Phase 5

```csharp
// Example 1: Allocate a Point with correct size
int pointInstVarCount = _layoutResolver.GetInstanceVariableCount("class:Point");
// Returns: 2
var pointOOP = _heapManager.AllocateObject(ObjectFormat.Fixed, pointInstVarCount, 0);

// Example 2: Set Point's x field by name
int xIndex = _layoutResolver.GetFieldIndex("class:Point", "x");
// Returns: 0
_heapManager.SetInstanceVariable(pointOOP, xIndex, xValueOOP);

// Example 3: Get Class's name field by name
int nameIndex = _layoutResolver.GetFieldIndex("class:Class", "name");
// Returns: 6
var nameOOP = _heapManager.GetInstanceVariable(classOOP, nameIndex);
```

### Critical Invariants After Phase 5

```csharp
// Invariant 1: All 480 classes have instance variable counts
Debug.Assert(instanceVarCounts.Count == 480);
Debug.Assert(bundle.Classes.All(cls => instanceVarCounts.ContainsKey(cls.LocalId)));

// Invariant 2: All 480 classes have field maps
Debug.Assert(bundle.Classes.All(cls =>
    _layoutResolver.GetFieldMap(cls.LocalId) != null));

// Invariant 3: Field map sizes match instance variable counts
Debug.Assert(bundle.Classes.All(cls =>
{
    int count = _layoutResolver.GetInstanceVariableCount(cls.LocalId);
    var map = _layoutResolver.GetFieldMap(cls.LocalId);
    return map.Count == count;
}));

// Invariant 4: Inheritance is properly reflected
var classClass = bundle.Classes.First(c => c.LocalId == "class:Class");
var classFieldMap = _layoutResolver.GetFieldMap("class:Class");
Debug.Assert(classFieldMap.ContainsKey("superclass"));  // from Behavior
Debug.Assert(classFieldMap.ContainsKey("instanceVariables"));  // from ClassDescription
Debug.Assert(classFieldMap.ContainsKey("name"));  // from Class

// Invariant 5: No index gaps
Debug.Assert(bundle.Classes.All(cls =>
{
    var map = _layoutResolver.GetFieldMap(cls.LocalId);
    var indices = map.Values.OrderBy(i => i).ToList();
    return indices.SequenceEqual(Enumerable.Range(0, map.Count));
}));

// Invariant 6: LayoutResolver is frozen
Debug.Assert(_layoutResolver.IsFrozen);
```

### Memory Usage Summary

| Data Structure | Count | Bytes per Item | Total Bytes |
|----------------|-------|----------------|-------------|
| Instance var counts | 480 | 4 (int) | 1,920 |
| Field maps (dictionaries) | 480 | ~50 avg | ~24,000 |
| Field name strings | ~2,400 | ~10 avg | ~24,000 |
| **TOTAL** | | | **~49.9 KB** |

### Performance Metrics

- **Expected Duration**: 15-25ms
- **Allocations**: 480 dictionaries + ~2,400 string/int pairs
- **Complexity**: O(N × M) where N = 480 classes, M = ~5 average depth

### Optimizations

**Optimization 1: Cache Topological Sort**
```csharp
// CURRENT: Compute topological sort during Phase 5

// OPTIMIZED: Compute during Phase 0 (Preflight) and cache
// - Reuse sorted order in multiple phases (4, 5, Y)
// - Reduces redundant traversals

// IMPACT: Saves ~5ms across all phases
```

**Optimization 2: Intern Field Name Strings**
```csharp
// CURRENT: Store full field name strings in each field map

// OPTIMIZED: Intern field names to single shared instances
var fieldNameIntern = new Dictionary<string, string>();
string Intern(string name) => fieldNameIntern.TryGetValue(name, out var interned)
    ? interned
    : (fieldNameIntern[name] = name);

// IMPACT: Reduces memory by ~50% (~12 KB saved)
```

**Optimization 3: Lazy Field Map Creation**
```csharp
// CURRENT: Create field maps for all 480 classes during Phase 5

// OPTIMIZED: Create field maps on-demand during first access
// - Defer work until actually needed
// - Classes never accessed don't pay cost

// IMPACT: Could save ~10ms if many classes unused
// TRADE-OFF: Runtime cost on first access, more complex code
```

**Optimization 4: Array-Based Field Maps**
```csharp
// CURRENT: Use Dictionary<string, int> for field maps

// OPTIMIZED: Use sorted array of (string, int) tuples
// - Binary search for lookups
// - Less memory overhead than Dictionary
// - Faster iteration

// IMPACT: ~30% memory reduction (~8 KB saved), slightly slower lookups
```

**Optimization 5: Parallel Layout Computation**
```csharp
// CURRENT: Sequential processing of 480 classes

// OPTIMIZED: Process independent subtrees in parallel
// - Identify classes with no shared ancestors
// - Compute layouts in parallel

// IMPACT: Could reduce 25ms → 10ms on multi-core
// COMPLEXITY: High (requires careful dependency tracking)
```

### Error Cases

**Error 1: Circular Inheritance**
```csharp
// SYMPTOM: Class A.superclass = B, B.superclass = A
// DETECTION: Topological sort fails with cycle

var visited = new HashSet<string>();
var stack = new HashSet<string>();

void DetectCycle(ClassDefinition cls)
{
    if (stack.Contains(cls.LocalId))
        throw new InvalidOperationException(
            $"Circular inheritance detected involving {cls.LocalId}");

    if (visited.Contains(cls.LocalId))
        return;

    stack.Add(cls.LocalId);
    if (cls.SuperclassLocalId != null)
    {
        var superclass = bundle.Classes.FirstOrDefault(c => c.LocalId == cls.SuperclassLocalId);
        if (superclass != null)
            DetectCycle(superclass);
    }
    stack.Remove(cls.LocalId);
    visited.Add(cls.LocalId);
}

// CAUSE: Malformed JSON bundle
// FIX: Should be caught in Phase 0 validation
```

**Error 2: Field Name Collision**
```csharp
// SYMPTOM: Subclass declares field with same name as inherited field
// EXAMPLE: Point declares 'x', Rectangle (subclass) also declares 'x'

// DETECTION:
if (fieldMap.ContainsKey(fieldName))
{
    throw new InvalidOperationException(
        $"Field {fieldName} in {cls.LocalId} shadows inherited field");
}

// CAUSE: Invalid Smalltalk code (should not compile in real Smalltalk)
// FIX: Fix JSON bundle to remove duplicate field
```

**Error 3: Missing Superclass Layout**
```csharp
// SYMPTOM: Subclass processed before superclass

// DETECTION:
if (cls.SuperclassLocalId != null && !instanceVarCounts.ContainsKey(cls.SuperclassLocalId))
{
    throw new InvalidOperationException(
        $"Superclass {cls.SuperclassLocalId} not processed before {cls.LocalId}");
}

// CAUSE: Incorrect topological sort or missing class
// FIX: Ensure topological sort is correct
```

**Error 4: Index Gap**
```csharp
// SYMPTOM: Field indices are [0, 1, 3, 4] (missing 2)

// DETECTION:
var indices = fieldMap.Values.OrderBy(i => i).ToList();
for (int i = 0; i < indices.Count; i++)
{
    if (indices[i] != i)
        throw new InvalidOperationException($"Index gap at {i} in {cls.LocalId}");
}

// CAUSE: Logic error in field index assignment
// FIX: Debug field map construction code
```

### Alternative Approaches

**Alternative 1: Runtime Layout Computation**
```csharp
// CURRENT APPROACH: Compute all layouts during Phase 5

// ALTERNATIVE: Compute layouts lazily during runtime on first access
// - Phase 5 becomes no-op
// - First field access triggers layout computation

// BENEFIT: Faster coldstart (defer work)
// DRAWBACK: Slower first access, need thread-safety, more complexity
```

**Alternative 2: Store Layouts in JSON Bundle**
```csharp
// CURRENT APPROACH: Compute layouts from inheritance chains

// ALTERNATIVE: Pre-compute layouts in JSON bundle generation
// - Bundle includes complete field maps for each class
// - Phase 5 just loads pre-computed data

// BENEFIT: Faster Phase 5 (~5ms instead of 25ms)
// DRAWBACK: Larger bundle size, redundant data
```

**Alternative 3: Compressed Layout Representation**
```csharp
// CURRENT APPROACH: Full field maps for each class

// ALTERNATIVE: Store only differences from superclass
// - Base class: full map
// - Subclass: only additional fields + offset

// BENEFIT: ~60% memory reduction for field maps
// DRAWBACK: More complex lookup (need to walk inheritance chain)
```

**Alternative 4: Global Field Index Space**
```csharp
// CURRENT APPROACH: Each class has independent field indices (0-based)

// ALTERNATIVE: Assign global unique indices to all fields across all classes
// - "superclass" gets index 1 globally
// - "x" (in Point) gets index 427 globally

// BENEFIT: Easier debugging, no per-class context needed
// DRAWBACK: Wastes space (objects would have huge sparse arrays), slower access
```

### What's Still Missing After Phase 5

**Missing Operation 1: Object Allocation**
```csharp
// NOT DONE YET: Allocate singleton objects (nil, true, false)
// WHY: Need layouts first (Phase 5) before allocating
// WHEN: Phase 6 (Object Allocation)
```

**Missing Operation 2: Class Reference Setting**
```csharp
// NOT DONE YET: Set slot 0 (class reference) for all objects
// WHY: Need nil singleton first (Phase 6) for Object.class reference
// WHEN: Phase X (Class Reference Initialization)
```

**Missing Operation 3: Instance Variable Initialization**
```csharp
// NOT DONE YET: Initialize unset instance variables to nil
// WHY: Need nil singleton and field maps (Phase 5 + 6)
// WHEN: Phase Y (Instance Variable Initialization)
```

**Missing Operation 4: Runtime Usage**
```csharp
// NOT AVAILABLE YET: LayoutResolver can't be used by interpreter
// WHY: Still in coldstart mode, not accessible to VM primitives
// WHEN: After Phase 14 (when coldstart completes), LayoutResolver becomes runtime service
```

### Next Phase Preview

**Phase 6: Object Allocation** will:
1. Allocate the nil singleton (UndefinedObject instance)
2. Allocate the true singleton (True instance)
3. Allocate the false singleton (False instance)
4. Set Object.superclass = nil (completing the inheritance chain)
5. Register singletons in SpecialObjectsArray for GC roots

This unblocks Phase X (setting class references) and Phase Y (initializing fields to nil).


---

## Phase 6: Object Allocation (Singleton Creation)

**Purpose**: Allocate the three fundamental singleton objects (nil, true, false) that are required by almost all other objects in the system.

**Input**:
- LayoutResolver populated with all class layouts (Phase 5)
- class:UndefinedObject, class:True, class:False exist with metadata set
- Object.superclass field currently unset (waiting for nil)

**Output**:
- nil singleton allocated (instance of UndefinedObject)
- true singleton allocated (instance of True)
- false singleton allocated (instance of False)
- Object.superclass = nil (completes inheritance chain)
- Singletons registered in SpecialObjectsArray as GC roots

**Why This Phase Exists**: Almost every object initialization requires setting unset fields to nil. Before we can initialize other objects, we need the nil singleton to exist. Additionally, true and false are fundamental values needed by conditionals and control flow.

**Critical Ordering**: This must happen BEFORE Phase X (class reference initialization) and Phase Y (instance variable initialization) because both phases set fields to nil.

### Step-by-Step Process

#### Step 1: Allocate nil Singleton

```csharp
// Get UndefinedObject class
SmalltalkOOP undefinedObjectClassOOP = _localIdToObjectId["class:UndefinedObject"];

// Get instance variable count (should be 0 - UndefinedObject has no instance vars)
int nilInstVarCount = _layoutResolver.GetInstanceVariableCount("class:UndefinedObject");
Debug.Assert(nilInstVarCount == 0, "UndefinedObject should have 0 instance variables");

// Allocate nil as a fixed-size object with 0 instance variables
SmalltalkOOP nilOOP = _heapManager.AllocateObject(
    ObjectFormat.Fixed,
    fixedSlots: 0,
    indexedSize: 0
);

// Register in SpecialObjectsArray (index 5 by convention)
_specialObjectsArray.SetNil(nilOOP);

Console.WriteLine($"✓ Allocated nil singleton: {nilOOP}");
```

**Object Structure**:
```
Heap Object: nil (oop=15420)
  Class: class:UndefinedObject (will be set in Phase X)
  Format: Fixed
  Instance Variables: none
  Indexed Slots: none
```

**Why 0 Instance Variables?**: UndefinedObject represents "no value", so it has no internal state.

**Memory**: 16 bytes (object table entry + minimal storage)

#### Step 2: Allocate true Singleton

```csharp
// Get True class
SmalltalkOOP trueClassOOP = _localIdToObjectId["class:True"];

// Get instance variable count (should be 0 - True has no instance vars)
int trueInstVarCount = _layoutResolver.GetInstanceVariableCount("class:True");
Debug.Assert(trueInstVarCount == 0, "True should have 0 instance variables");

// Allocate true as a fixed-size object with 0 instance variables
SmalltalkOOP trueOOP = _heapManager.AllocateObject(
    ObjectFormat.Fixed,
    fixedSlots: 0,
    indexedSize: 0
);

// Register in SpecialObjectsArray (index 6 by convention)
_specialObjectsArray.SetTrue(trueOOP);

Console.WriteLine($"✓ Allocated true singleton: {trueOOP}");
```

**Object Structure**:
```
Heap Object: true (oop=15424)
  Class: class:True (will be set in Phase X)
  Format: Fixed
  Instance Variables: none
  Indexed Slots: none
```

**Memory**: 16 bytes

#### Step 3: Allocate false Singleton

```csharp
// Get False class
SmalltalkOOP falseClassOOP = _localIdToObjectId["class:False"];

// Get instance variable count (should be 0 - False has no instance vars)
int falseInstVarCount = _layoutResolver.GetInstanceVariableCount("class:False");
Debug.Assert(falseInstVarCount == 0, "False should have 0 instance variables");

// Allocate false as a fixed-size object with 0 instance variables
SmalltalkOOP falseOOP = _heapManager.AllocateObject(
    ObjectFormat.Fixed,
    fixedSlots: 0,
    indexedSize: 0
);

// Register in SpecialObjectsArray (index 7 by convention)
_specialObjectsArray.SetFalse(falseOOP);

Console.WriteLine($"✓ Allocated false singleton: {falseOOP}");
```

**Object Structure**:
```
Heap Object: false (oop=15428)
  Class: class:False (will be set in Phase X)
  Format: Fixed
  Instance Variables: none
  Indexed Slots: none
```

**Memory**: 16 bytes

#### Step 4: Set Object.superclass = nil

Now that nil exists, complete the inheritance chain by setting Object's superclass to nil.

```csharp
SmalltalkOOP objectClassOOP = _localIdToObjectId["class:Object"];
int superclassIndex = _layoutResolver.GetFieldIndex("class:Object", "superclass");

// Set Object.superclass = nil
_heapManager.SetInstanceVariable(objectClassOOP, superclassIndex, nilOOP);
_initializedFields.Add((objectClassOOP, superclassIndex));

Console.WriteLine($"✓ Set Object.superclass = nil (completing inheritance chain)");
```

**Why This Matters**: The inheritance chain is now complete:
```
class:Array → ... → class:Object → nil (superclass chain terminates)
meta:Array → ... → meta:Object → class:Class → ... → class:Object → nil
```

**Critical Invariant**: Every class's superclass chain now terminates at nil (no infinite loops).

### Data Structure State After Phase 6

#### SpecialObjectsArray Contents

```csharp
SpecialObjectsArray (partially populated):
  [0]: Processor (not yet allocated)
  [1]: Smalltalk (SystemDictionary, not yet allocated)
  [2]: Symbol table (not yet allocated)
  [3]: class:SmallInteger (not yet allocated)
  [4]: class:String (not yet allocated)
  [5]: nil ← SET IN PHASE 6
  [6]: true ← SET IN PHASE 6
  [7]: false ← SET IN PHASE 6
  [8]: class:Array (not yet allocated)
  [9]: class:CompiledMethod (not yet allocated)
  ... (remaining slots unset)
```

#### Heap Objects

```
Object Count After Phase 6:
- Symbols: 3,834
- Classes: 480
- Singletons: 3 (nil, true, false)
- Total: 4,317 objects
```

#### Inheritance Chain Completion

```
Before Phase 6:
  class:Object.superclass = <UNSET>

After Phase 6:
  class:Object.superclass = nil

Result:
  Every class now has a complete superclass chain terminating at nil
  Method lookup can safely traverse to Object without infinite loops
```

### Critical Invariants After Phase 6

```csharp
// Invariant 1: nil exists and is registered
Debug.Assert(_specialObjectsArray.GetNil() != SmalltalkOOP.Null);
var nilOOP = _specialObjectsArray.GetNil();
Debug.Assert(_heapManager.IsValidOOP(nilOOP));

// Invariant 2: true exists and is registered
Debug.Assert(_specialObjectsArray.GetTrue() != SmalltalkOOP.Null);
var trueOOP = _specialObjectsArray.GetTrue();
Debug.Assert(_heapManager.IsValidOOP(trueOOP));

// Invariant 3: false exists and is registered
Debug.Assert(_specialObjectsArray.GetFalse() != SmalltalkOOP.Null);
var falseOOP = _specialObjectsArray.GetFalse();
Debug.Assert(_heapManager.IsValidOOP(falseOOP));

// Invariant 4: Object.superclass is set to nil
var objectClassOOP = _localIdToObjectId["class:Object"];
int superclassIndex = _layoutResolver.GetFieldIndex("class:Object", "superclass");
var objectSuperclass = _heapManager.GetInstanceVariable(objectClassOOP, superclassIndex);
Debug.Assert(objectSuperclass == nilOOP);

// Invariant 5: Singletons have no instance variables
Debug.Assert(_heapManager.GetInstanceVariableCount(nilOOP) == 0);
Debug.Assert(_heapManager.GetInstanceVariableCount(trueOOP) == 0);
Debug.Assert(_heapManager.GetInstanceVariableCount(falseOOP) == 0);
```

### Memory Usage Summary

| Object | Size | Purpose |
|--------|------|---------|
| nil | 16 bytes | Represents "no value" |
| true | 16 bytes | Boolean true value |
| false | 16 bytes | Boolean false value |
| **TOTAL** | **48 bytes** | Fundamental singletons |

### Performance Metrics

- **Expected Duration**: <1ms (3 simple allocations)
- **Allocations**: 3 objects
- **Writes**: 1 field (Object.superclass)

---

## Phase X: Class Reference Initialization

**Purpose**: Set the class reference (slot 0) for all allocated objects, establishing the class→instance relationship required for method lookup and primitives.

**Input**:
- All objects allocated in Phases 1-6 (symbols, classes, singletons)
- nil singleton exists (from Phase 6)
- class:Metaclass exists for metaclass objects
- class:Class exists for regular class objects
- class:Symbol exists for symbol objects
- class:UndefinedObject, class:True, class:False exist for singletons

**Output**:
- Every object has a valid class reference in slot 0
- Metaclasses point to class:Metaclass
- Regular classes point to class:Class
- Symbols point to class:Symbol
- nil points to class:UndefinedObject
- true points to class:True
- false points to class:False

**Why This Phase Exists**: In Smalltalk-80, EVERY object must have a class reference. This is required for:
1. Method lookup (VM needs to find the class to search for methods)
2. Primitives (many primitives check object class)
3. Type checking (isKindOf:, isMemberOf:)
4. GC traversal (GC uses class format to know how to traverse)

**Critical Constraint**: Must happen AFTER Phase 6 (nil exists) because Object's class might conceptually point to nil in edge cases, and we use nil to mark unset fields.

### Step-by-Step Process

#### Step 1: Set Class References for All Class Objects

```csharp
int classCount = 0;
int metaclassCount = 0;

foreach (var cls in bundle.Classes)
{
    SmalltalkOOP objectOOP = _localIdToObjectId[cls.LocalId];

    // Determine the class of this class object
    SmalltalkOOP classOfClassOOP;
    if (cls.IsMetaclass)
    {
        // Metaclasses are instances of class:Metaclass
        classOfClassOOP = _localIdToObjectId["class:Metaclass"];
        metaclassCount++;
    }
    else
    {
        // Regular classes are instances of their metaclass
        string metaclassLocalId = $"meta:{cls.ClassName}";
        if (!_localIdToObjectId.TryGetValue(metaclassLocalId, out classOfClassOOP))
        {
            throw new InvalidOperationException(
                $"Metaclass not found for {cls.LocalId}: {metaclassLocalId}");
        }
        classCount++;
    }

    // Set slot 0 (class reference)
    _heapManager.SetSlot(objectOOP, 0, classOfClassOOP);
}

Console.WriteLine($"✓ Set class references for {classCount} classes and {metaclassCount} metaclasses");
```

**Example Results**:
```csharp
// Regular classes
class:Object.class = meta:Object
class:Point.class = meta:Point
class:Array.class = meta:Array
class:String.class = meta:String

// Metaclasses
meta:Object.class = class:Metaclass
meta:Point.class = class:Metaclass
meta:Array.class = class:Metaclass
```

**Why Regular Classes Point to Metaclasses**: In Smalltalk-80, the class of a class is its metaclass. This is what enables class-side methods:
```
Point new  → sends #new to Point
           → VM looks up #new in Point.class
           → Point.class = meta:Point
           → Searches meta:Point for #new
```

**Performance**: 480 writes, ~2ms

#### Step 2: Set Class References for All Symbols

```csharp
int symbolCount = 0;
SmalltalkOOP symbolClassOOP = _localIdToObjectId["class:Symbol"];

for (int i = 0; i < bundle.Symbols.Length; i++)
{
    SmalltalkOOP symbolOOP = _symbolRegistry.GetSymbol(i);

    // Set slot 0 (class reference)
    _heapManager.SetSlot(symbolOOP, 0, symbolClassOOP);
    symbolCount++;
}

Console.WriteLine($"✓ Set class references for {symbolCount} symbols");
```

**Result**: All 3,834 symbols now have class:Symbol as their class.

**Performance**: 3,834 writes, ~5ms

#### Step 3: Set Class References for Singletons

```csharp
// Set nil.class = class:UndefinedObject
SmalltalkOOP nilOOP = _specialObjectsArray.GetNil();
SmalltalkOOP undefinedObjectClassOOP = _localIdToObjectId["class:UndefinedObject"];
_heapManager.SetSlot(nilOOP, 0, undefinedObjectClassOOP);

// Set true.class = class:True
SmalltalkOOP trueOOP = _specialObjectsArray.GetTrue();
SmalltalkOOP trueClassOOP = _localIdToObjectId["class:True"];
_heapManager.SetSlot(trueOOP, 0, trueClassOOP);

// Set false.class = class:False
SmalltalkOOP falseOOP = _specialObjectsArray.GetFalse();
SmalltalkOOP falseClassOOP = _localIdToObjectId["class:False"];
_heapManager.SetSlot(falseOOP, 0, falseClassOOP);

Console.WriteLine($"✓ Set class references for 3 singletons");
```

**Result**:
```
nil.class = class:UndefinedObject
true.class = class:True
false.class = class:False
```

**Performance**: 3 writes, <1ms

### Data Structure State After Phase X

#### Complete Object Structure Example: Symbol("Point")

```csharp
Heap Object: Symbol("Point") (oop=12456)
  Slot 0 (class): class:Symbol ← SET IN PHASE X
  Format: ByteIndexed
  Instance Variables: 0
  Indexed Bytes: [80, 111, 105, 110, 116] (UTF-8 for "Point")
```

#### Complete Object Structure Example: class:Point

```csharp
Heap Object: class:Point (oop=8234)
  Slot 0 (class): meta:Point ← SET IN PHASE X
  Format: PointerIndexed
  Instance Variables: 11 (from class:Class hierarchy)
  Slot 1 (superclass):    class:Object (set in Phase 4)
  Slot 2 (methodDict):    <UNSET> (will be set in Phase 7+9)
  Slot 3 (format):        SmallInteger(0x0002) (set in Phase 4)
  Slot 4 (instanceVars):  <UNSET>
  Slot 5 (organization):  <UNSET>
  Slot 6 (subclasses):    <UNSET>
  Slot 7 (name):          Symbol("Point") (set in Phase 4)
  Slot 8 (classPool):     <UNSET>
  Slot 9 (sharedPools):   <UNSET>
  Slot 10 (environment):  <UNSET>
  Slot 11 (category):     <UNSET>
```

#### Complete Object Structure Example: nil

```csharp
Heap Object: nil (oop=15420)
  Slot 0 (class): class:UndefinedObject ← SET IN PHASE X
  Format: Fixed
  Instance Variables: 0
  Indexed Slots: 0
```

### Critical Invariants After Phase X

```csharp
// Invariant 1: All classes have class references
Debug.Assert(bundle.Classes.All(cls =>
{
    var oop = _localIdToObjectId[cls.LocalId];
    var classOOP = _heapManager.GetSlot(oop, 0);
    return classOOP != SmalltalkOOP.Null;
}));

// Invariant 2: Regular classes point to metaclasses
Debug.Assert(bundle.Classes.Where(c => !c.IsMetaclass).All(cls =>
{
    var oop = _localIdToObjectId[cls.LocalId];
    var classOOP = _heapManager.GetSlot(oop, 0);
    string expectedMeta = $"meta:{cls.ClassName}";
    var expectedOOP = _localIdToObjectId[expectedMeta];
    return classOOP == expectedOOP;
}));

// Invariant 3: Metaclasses point to class:Metaclass
var metaclassClassOOP = _localIdToObjectId["class:Metaclass"];
Debug.Assert(bundle.Classes.Where(c => c.IsMetaclass).All(cls =>
{
    var oop = _localIdToObjectId[cls.LocalId];
    var classOOP = _heapManager.GetSlot(oop, 0);
    return classOOP == metaclassClassOOP;
}));

// Invariant 4: All symbols point to class:Symbol
var symbolClassOOP = _localIdToObjectId["class:Symbol"];
for (int i = 0; i < bundle.Symbols.Length; i++)
{
    var symbolOOP = _symbolRegistry.GetSymbol(i);
    var classOOP = _heapManager.GetSlot(symbolOOP, 0);
    Debug.Assert(classOOP == symbolClassOOP);
}

// Invariant 5: Singletons have correct classes
Debug.Assert(_heapManager.GetSlot(_specialObjectsArray.GetNil(), 0) ==
             _localIdToObjectId["class:UndefinedObject"]);
Debug.Assert(_heapManager.GetSlot(_specialObjectsArray.GetTrue(), 0) ==
             _localIdToObjectId["class:True"]);
Debug.Assert(_heapManager.GetSlot(_specialObjectsArray.GetFalse(), 0) ==
             _localIdToObjectId["class:False"]);
```

### Memory Usage Summary

| Operation | Count | Bytes per Item | Total Bytes |
|-----------|-------|----------------|-------------|
| Set class refs for classes | 480 | 4 (OOP) | 1,920 |
| Set class refs for symbols | 3,834 | 4 (OOP) | 15,336 |
| Set class refs for singletons | 3 | 4 (OOP) | 12 |
| **TOTAL** | **4,317** | | **17,268** |

### Performance Metrics

- **Expected Duration**: 5-10ms
- **Writes**: 4,317 slot writes (class references)

---

## Phase Y: Instance Variable Initialization

**Purpose**: Initialize all unset instance variable slots to nil, ensuring every object is in a fully valid state with no uninitialized fields.

**Input**:
- All objects allocated and have class references (Phases 1-6 + Phase X)
- nil singleton exists (Phase 6)
- initializedFields tracking set (populated in Phases 3, 4, etc.)
- LayoutResolver populated with field counts

**Output**:
- Every instance variable slot that was not explicitly set now contains nil
- initializedFields tracking can be discarded (no longer needed)
- All objects fully initialized and ready for use

**Why This Phase Exists**: Smalltalk requires all instance variables to be initialized. Uninitialized slots (currently 0) are invalid OOP values. Setting them to nil:
1. Makes objects safe for GC traversal
2. Ensures primitives don't encounter invalid OOPs
3. Matches Smalltalk-80 semantics (uninitialized = nil)

**Critical Constraint**: Must happen AFTER Phase X (class references set) because we need to traverse all objects and know their instance variable counts.

### Step-by-Step Process

#### Step 1: Initialize Class Object Instance Variables

```csharp
int fieldsInitialized = 0;

foreach (var cls in bundle.Classes)
{
    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

    // Get instance variable count for this class
    int instVarCount = cls.IsMetaclass
        ? _layoutResolver.GetInstanceVariableCount("class:Metaclass")
        : _layoutResolver.GetInstanceVariableCount("class:Class");

    // Initialize each instance variable slot
    for (int slotIndex = 0; slotIndex < instVarCount; slotIndex++)
    {
        // Check if this field was already explicitly set
        if (_initializedFields.Contains((classOOP, slotIndex)))
            continue; // Already initialized, skip

        // Set to nil
        _heapManager.SetInstanceVariable(classOOP, slotIndex, nilOOP);
        fieldsInitialized++;
    }
}

Console.WriteLine($"✓ Initialized {fieldsInitialized} unset instance variables in class objects");
```

**Example**: class:Point has 11 instance variables (from class:Class hierarchy):
```
Slot 0 (superclass):    class:Object (SET IN PHASE 4)
Slot 1 (methodDict):    nil ← SET IN PHASE Y
Slot 2 (format):        SmallInteger(0x0002) (SET IN PHASE 4)
Slot 3 (instanceVars):  nil ← SET IN PHASE Y
Slot 4 (organization):  nil ← SET IN PHASE Y
Slot 5 (subclasses):    nil ← SET IN PHASE Y
Slot 6 (name):          Symbol("Point") (SET IN PHASE 4)
Slot 7 (classPool):     nil ← SET IN PHASE Y
Slot 8 (sharedPools):   nil ← SET IN PHASE Y
Slot 9 (environment):   nil ← SET IN PHASE Y
Slot 10 (category):     nil ← SET IN PHASE Y
```

**Result**: 7 out of 11 fields were unset, now initialized to nil.

**Performance**: ~3,000-4,000 field writes across 480 classes, ~5-10ms

#### Step 2: Validate All Objects Are Fully Initialized

```csharp
// Validation: Ensure no object has invalid (0) slot values
int totalObjects = _heapManager.GetObjectCount();
int invalidSlots = 0;

for (int i = 0; i < totalObjects; i++)
{
    var oop = _heapManager.GetObjectOOPByIndex(i);
    if (!_heapManager.IsValidOOP(oop))
        continue; // Skip freed slots

    int slotCount = _heapManager.GetTotalSlotCount(oop);

    for (int slot = 0; slot < slotCount; slot++)
    {
        var value = _heapManager.GetSlot(oop, slot);

        // Check for invalid OOP (0 or other illegal values)
        if (value.RawValue == 0 && slot > 0) // Slot 0 should never be 0 after Phase X
        {
            invalidSlots++;
            Console.WriteLine($"WARNING: Object {oop} slot {slot} is 0 (uninitialized)");
        }
    }
}

if (invalidSlots > 0)
{
    throw new InvalidOperationException(
        $"Found {invalidSlots} uninitialized slots after Phase Y");
}

Console.WriteLine($"✓ Validated all {totalObjects} objects - no uninitialized slots");
```

**Why This Validation Matters**: Uninitialized slots cause:
- GC crashes (trying to traverse invalid OOPs)
- Primitive failures (unexpected null/zero values)
- Silent corruption (invalid references)

**Performance**: O(N × M) where N = object count, M = average slots per object
- ~4,317 objects × ~5 average slots = ~21,585 slot checks
- Time: ~10-15ms

### Data Structure State After Phase Y

#### Complete Object Example: class:Point (Fully Initialized)

```csharp
Heap Object: class:Point (oop=8234)
  Slot 0 (class):         meta:Point (SET IN PHASE X)
  Slot 1 (superclass):    class:Object (SET IN PHASE 4)
  Slot 2 (methodDict):    nil ← SET IN PHASE Y
  Slot 3 (format):        SmallInteger(0x0002) (SET IN PHASE 4)
  Slot 4 (instanceVars):  nil ← SET IN PHASE Y
  Slot 5 (organization):  nil ← SET IN PHASE Y
  Slot 6 (subclasses):    nil ← SET IN PHASE Y
  Slot 7 (name):          Symbol("Point") (SET IN PHASE 4)
  Slot 8 (classPool):     nil ← SET IN PHASE Y
  Slot 9 (sharedPools):   nil ← SET IN PHASE Y
  Slot 10 (environment):  nil ← SET IN PHASE Y
  Slot 11 (category):     nil ← SET IN PHASE Y
```

**Key Point**: Every slot is now either:
1. Explicitly set to a meaningful value (Phases 3, 4)
2. Initialized to nil (Phase Y)

NO slots remain as 0 (invalid).

#### initializedFields Cleanup

```csharp
// After Phase Y, we can discard the initializedFields tracking
_initializedFields.Clear();
_initializedFields = null; // Free memory

Console.WriteLine($"✓ Discarded initializedFields tracking (no longer needed)");
```

**Why Discard?**: The `initializedFields` set was only needed to distinguish "not set" from "set to nil" during coldstart. After Phase Y, all fields are set, so this tracking is obsolete.

**Memory Saved**: ~20-30 KB (HashSet with ~480-1,000 entries)

### Critical Invariants After Phase Y

```csharp
// Invariant 1: No object has slot 0 = 0 (all have class references)
Debug.Assert(_heapManager.EnumerateObjects().All(oop =>
{
    var classOOP = _heapManager.GetSlot(oop, 0);
    return classOOP != SmalltalkOOP.Null && classOOP.RawValue != 0;
}));

// Invariant 2: No object has uninitialized instance variable slots
Debug.Assert(_heapManager.EnumerateObjects().All(oop =>
{
    int instVarCount = _heapManager.GetInstanceVariableCount(oop);
    for (int i = 0; i < instVarCount; i++)
    {
        var value = _heapManager.GetInstanceVariable(oop, i);
        // value can be nil, but not 0 (invalid)
        if (value.RawValue == 0)
            return false;
    }
    return true;
}));

// Invariant 3: initializedFields is discarded
Debug.Assert(_initializedFields == null);
```

### Memory Usage Summary

| Operation | Count | Bytes per Item | Total Bytes |
|-----------|-------|----------------|-------------|
| Initialize unset fields | ~3,500 | 4 (OOP) | ~14,000 |
| **TOTAL** | | | **~14 KB** |

**Memory Freed**: ~20-30 KB (initializedFields tracking)

**Net Memory**: +14 KB writes, -25 KB freed = **-11 KB** (memory reduction!)

### Performance Metrics

- **Expected Duration**: 15-25ms
- **Writes**: ~3,500 instance variable initializations
- **Reads**: ~21,585 slot validations

### Optimizations

**Optimization 1: Skip Fully Initialized Objects**
```csharp
// CURRENT: Check every slot of every object

// OPTIMIZED: Track which objects were fully initialized in earlier phases
// - Symbols have no instance vars (skip)
// - Singletons have no instance vars (skip)
// - Only check class objects

// IMPACT: Reduces validation from 21,585 checks to ~5,000
```

**Optimization 2: Parallel Initialization**
```csharp
// CURRENT: Sequential initialization of 480 classes

// OPTIMIZED: Parallelize (no dependencies between objects)
Parallel.ForEach(bundle.Classes, cls => {
    InitializeUnsetFields(cls);
});

// IMPACT: Could reduce 10ms → 3ms on multi-core
```

**Optimization 3: Batch nil Writes**
```csharp
// CURRENT: Individual SetInstanceVariable calls

// OPTIMIZED: Batch writes to same object
var unsetSlots = FindUnsetSlots(classOOP);
_heapManager.BatchSetSlots(classOOP, unsetSlots, nilOOP);

// IMPACT: Reduces call overhead, ~5ms savings
```

### What's Still Missing After Phase Y

**Missing Operation 1: Method Dictionaries**
```csharp
// NOT SET YET: cls.methodDict field contains nil (not a real dictionary)
// WHY: Method dictionaries allocated in Phase 7, installed in Phase 9
// WHEN: Phases 7-9 will replace nil with actual dictionaries
```

**Missing Operation 2: Global Variables**
```csharp
// NOT SET YET: Environment, Smalltalk dictionary, etc.
// WHY: Global variables installed in Phase 13
// WHEN: Phase 13 populates class pools, shared pools, environments
```

**Missing Operation 3: Methods**
```csharp
// NOT SET YET: No methods installed in any class
// WHY: Methods compiled and installed in Phase 8, linked in Phase 9
// WHEN: Phases 8-9 create and install all 10,091 methods
```

### Next Phases Preview

**Phases 7-9: Method Installation** will:
1. Phase 7: Allocate empty MethodDictionary objects for each class
2. Phase 8: Compile all 10,091 methods from JSON bytecode, allocate CompiledMethod objects
3. Phase 9: Install MethodDictionaries into classes, replacing the nil placeholders

This will make classes fully operational with executable methods.


---

## Phase 7: Method Dictionary Allocation

**Purpose**: Allocate MethodDictionary objects for all 480 classes to hold their methods. These are initially empty and will be populated in Phase 9.

**Input**:
- 480 classes with metadata fully initialized (Phase Y)
- JSON method definitions (10,091 methods across all classes)
- class:MethodDictionary exists

**Output**:
- 480 MethodDictionary objects allocated (one per class)
- Each dictionary sized appropriately for its class's method count
- Dictionaries stored in temporary tracking map for Phase 9 installation

**Why This Phase Exists**: We separate dictionary allocation (Phase 7) from method compilation (Phase 8) and installation (Phase 9) because:
1. Dictionaries need to exist before methods can reference them
2. Allows pre-sizing dictionaries based on method counts
3. Separates concerns (allocation vs compilation vs linking)

**Critical Architecture**: MethodDictionary is a specialized dictionary optimized for method lookup with:
- Open addressing (linear probing)
- Selector (Symbol) keys
- CompiledMethod values
- Tally field (number of entries)

### MethodDictionary Structure

```csharp
// MethodDictionary layout (from Smalltalk-80):
class:MethodDictionary (superclass: Dictionary)
  Instance Variables:
    - tally (index 0) - SmallInteger count of entries
  Indexed Slots:
    - Even indices: selector Symbols (keys)
    - Odd indices: CompiledMethod objects (values)

// Example:
MethodDictionary for class:Point (capacity = 16)
  Slot 0 (class):  class:MethodDictionary
  Slot 1 (tally):  SmallInteger(12) - 12 methods installed
  Slot 2:  Symbol("+")
  Slot 3:  CompiledMethod for #+
  Slot 4:  Symbol("-")
  Slot 5:  CompiledMethod for #-
  Slot 6:  Symbol("x")
  Slot 7:  CompiledMethod for #x (getter)
  Slot 8:  Symbol("y")
  Slot 9:  CompiledMethod for #y (getter)
  ...
  Slot 30: nil (empty)
  Slot 31: nil (empty)
```

### Step-by-Step Process

#### Step 1: Count Methods Per Class

```csharp
var methodCounts = new Dictionary<string, int>();

foreach (var method in bundle.Methods)
{
    string classLocalId = method.ClassName.StartsWith("meta:")
        ? $"meta:{method.ClassName.Substring(5)}"
        : $"class:{method.ClassName}";

    if (!methodCounts.ContainsKey(classLocalId))
        methodCounts[classLocalId] = 0;

    methodCounts[classLocalId]++;
}

Console.WriteLine($"Counted methods for {methodCounts.Count} classes");
Console.WriteLine($"Total methods: {bundle.Methods.Length}");
```

**Example Counts**:
```
class:Point → 28 methods (#x, #y, #x:, #y:, #+, #-, #*, #/, #=, #hash, etc.)
class:Array → 65 methods (#at:, #at:put:, #size, #do:, #collect:, etc.)
class:Object → 42 methods (#class, #==, #~~, #isNil, #notNil, #yourself, etc.)
meta:Object → 8 methods (#new, #basicNew, #new:, #basicNew:, etc.)
```

**Total**: 10,091 methods across ~400-450 classes (not all classes have methods)

#### Step 2: Calculate Dictionary Capacities

```csharp
var dictionaryCapacities = new Dictionary<string, int>();

foreach (var kvp in methodCounts)
{
    string classLocalId = kvp.Key;
    int methodCount = kvp.Value;

    // Calculate capacity with load factor 0.75
    // capacity = nextPowerOfTwo(methodCount / 0.75)
    int targetCapacity = (int)Math.Ceiling(methodCount / 0.75);
    int capacity = NextPowerOfTwo(targetCapacity);

    dictionaryCapacities[classLocalId] = capacity;
}

// Helper function
int NextPowerOfTwo(int n)
{
    int power = 1;
    while (power < n)
        power *= 2;
    return power;
}
```

**Example Capacities**:
```
class:Point:  28 methods → capacity 64  (28 / 0.75 = 37.3 → 64)
class:Array:  65 methods → capacity 128 (65 / 0.75 = 86.7 → 128)
class:Object: 42 methods → capacity 64  (42 / 0.75 = 56 → 64)
```

**Why Power of 2?**: Optimizes hash table performance (modulo becomes bitwise AND).

**Why 0.75 Load Factor?**: Balance between space efficiency and lookup speed. Lower = faster lookup but more wasted space.

#### Step 3: Allocate MethodDictionaries

```csharp
var methodDictionaries = new Dictionary<string, SmalltalkOOP>();
SmalltalkOOP methodDictClassOOP = _localIdToObjectId["class:MethodDictionary"];
SmalltalkOOP nilOOP = _specialObjectsArray.GetNil();

int totalAllocated = 0;
int totalSlots = 0;

foreach (var cls in bundle.Classes)
{
    // Skip classes with no methods
    if (!dictionaryCapacities.TryGetValue(cls.LocalId, out int capacity))
        capacity = 8; // Minimum size for empty classes

    // Allocate MethodDictionary
    // - 1 instance variable (tally)
    // - capacity indexed slots (selector/method pairs)
    SmalltalkOOP dictOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        fixedSlots: 1,  // tally
        indexedSize: capacity
    );

    // Set class reference
    _heapManager.SetSlot(dictOOP, 0, methodDictClassOOP);

    // Initialize tally to 0
    SmalltalkOOP zeroOOP = SmalltalkOOP.FromSmallInteger(0);
    _heapManager.SetInstanceVariable(dictOOP, 0, zeroOOP);

    // Initialize all indexed slots to nil
    for (int i = 0; i < capacity; i++)
    {
        _heapManager.SetIndexedOOP(dictOOP, i, nilOOP);
    }

    // Store for Phase 9 installation
    methodDictionaries[cls.LocalId] = dictOOP;

    totalAllocated++;
    totalSlots += capacity;
}

Console.WriteLine($"✓ Allocated {totalAllocated} MethodDictionaries");
Console.WriteLine($"  Total capacity: {totalSlots} slots");
Console.WriteLine($"  Average capacity: {totalSlots / totalAllocated} slots/dict");
```

**Memory Calculation**:
```
Average capacity: ~40 slots per dictionary
Total slots: 480 classes × 40 = 19,200 slots
Bytes per slot: 4 bytes (OOP)
Total: 19,200 × 4 = 76,800 bytes (~75 KB)
```

**Performance**: 480 allocations + 19,200 slot initializations, ~20-30ms

### Data Structure State After Phase 7

#### MethodDictionary Example: class:Point

```csharp
Heap Object: MethodDictionary for class:Point (oop=25600)
  Slot 0 (class):         class:MethodDictionary
  Slot 1 (tally):         SmallInteger(0) ← will be updated in Phase 9
  Slot 2 (indexed[0]):    nil ← will be selector in Phase 9
  Slot 3 (indexed[1]):    nil ← will be CompiledMethod in Phase 9
  Slot 4 (indexed[2]):    nil
  Slot 5 (indexed[3]):    nil
  ...
  Slot 65 (indexed[63]):  nil
```

#### Tracking Map

```csharp
methodDictionaries = {
    ["class:Object"] = OOP #25500,
    ["class:Point"] = OOP #25600,
    ["class:Array"] = OOP #25700,
    ...
    (480 entries)
}
```

**Why Not Install Yet?**: Methods haven't been compiled yet (Phase 8), so we can't populate the dictionaries.

### Critical Invariants After Phase 7

```csharp
// Invariant 1: All 480 classes have dictionaries allocated
Debug.Assert(bundle.Classes.All(cls => methodDictionaries.ContainsKey(cls.LocalId)));

// Invariant 2: All dictionaries have class:MethodDictionary
Debug.Assert(methodDictionaries.Values.All(oop =>
{
    var classOOP = _heapManager.GetSlot(oop, 0);
    return classOOP == methodDictClassOOP;
}));

// Invariant 3: All tallies are 0 (no methods installed yet)
Debug.Assert(methodDictionaries.Values.All(oop =>
{
    var tallyOOP = _heapManager.GetInstanceVariable(oop, 0);
    return tallyOOP.ToSmallInteger() == 0;
}));

// Invariant 4: All indexed slots are nil
Debug.Assert(methodDictionaries.Values.All(oop =>
{
    int capacity = _heapManager.GetIndexedSize(oop);
    for (int i = 0; i < capacity; i++)
    {
        var slot = _heapManager.GetIndexedOOP(oop, i);
        if (slot != nilOOP)
            return false;
    }
    return true;
}));

// Invariant 5: Capacity is power of 2
Debug.Assert(methodDictionaries.Values.All(oop =>
{
    int capacity = _heapManager.GetIndexedSize(oop);
    return (capacity & (capacity - 1)) == 0; // Power of 2 check
}));
```

### Memory Usage Summary

| Operation | Count | Bytes per Item | Total Bytes |
|-----------|-------|----------------|-------------|
| MethodDictionary objects | 480 | ~160 avg | ~76,800 |
| **TOTAL** | | | **~75 KB** |

### Performance Metrics

- **Expected Duration**: 20-30ms
- **Allocations**: 480 MethodDictionary objects
- **Initialization**: ~19,200 slots set to nil

---

## Phase 8: Method Installation (Compilation)

**Purpose**: Compile all 10,091 methods from JSON bytecode into CompiledMethod objects with proper literal resolution and association canonicalization.

**Input**:
- JSON method definitions (10,091 methods)
- Symbol registry (for selector symbols)
- Class map (for literal class references)
- LayoutResolver (for field access in methods)

**Output**:
- 10,091 CompiledMethod objects allocated
- Each method has:
  - Header encoding (argument count, temp count, primitive number)
  - Literals resolved to OOPs (symbols, classes, associations)
  - Bytecode array
- Methods stored in temporary map for Phase 9 installation
- Associations canonicalized (only one Association per global variable)

**Why This Phase Exists**: This is the most complex phase, involving:
1. Literal resolution (strings → Symbols, class names → class OOPs)
2. Association canonicalization (ensure global references use shared Association objects)
3. Bytecode array creation (separate ByteArray object)
4. Header encoding (pack metadata into SmallInteger)

**Critical Architecture**: CompiledMethod uses a heterogeneous format:
```
CompiledMethod structure:
  Slot 0: class reference (class:CompiledMethod)
  Slot 1: header (SmallInteger with packed metadata)
  Slot 2..N: literals (Symbols, Classes, Associations, Numbers, etc.)
  Slot N+1: bytecode array OOP (hidden, points to separate ByteArray)

ByteArray (separate object):
  Contains raw bytecodes for method execution
```

### Header Encoding

The header is a 16-bit SmallInteger with packed fields:

```
Bits 0-4:   Argument count (0-31)
Bits 5-9:   Temporary count (0-31)
Bits 10-14: Primitive number (0-31, or 0 if no primitive)
Bit 15:     Flag bit (reserved)
```

**Example Headers**:
```csharp
// Point>>x (getter, no args, no temps, no primitive)
header = 0x0000  // 0 args, 0 temps, primitive 0

// Point>>x: (setter, 1 arg, 0 temps, no primitive)
header = 0x0001  // 1 arg, 0 temps, primitive 0

// Behavior>>basicNew (class method, 0 args, 0 temps, primitive 70)
header = 0x1C00  // 0 args, 0 temps, primitive 70 (0x1C << 10 = 0x1C00)
```

### Association Canonicalization

**Problem**: Multiple methods reference the same global variable (e.g., "Smalltalk", "Processor"). Each reference needs the SAME Association object.

**Solution**: Maintain a canonical association map:
```csharp
Dictionary<string, SmalltalkOOP> _canonicalAssociations;

// When resolving a global reference:
SmalltalkOOP GetOrCreateAssociation(string globalName)
{
    if (_canonicalAssociations.TryGetValue(globalName, out var assocOOP))
        return assocOOP; // Reuse existing

    // Create new Association
    assocOOP = AllocateAssociation(globalName);
    _canonicalAssociations[globalName] = assocOOP;
    return assocOOP;
}
```

**Why This Matters**: If methods had different Association objects for "Smalltalk", updating one wouldn't affect the others, breaking global variable semantics.

### Step-by-Step Process

#### Step 1: Initialize Association Canonicalization Map

```csharp
var canonicalAssociations = new Dictionary<string, SmalltalkOOP>();
SmalltalkOOP associationClassOOP = _localIdToObjectId["class:Association"];

Console.WriteLine($"Initialized association canonicalization map");
```

#### Step 2: Compile Each Method

```csharp
var compiledMethods = new Dictionary<string, SmalltalkOOP>(); // methodId → CompiledMethod OOP
int methodsCompiled = 0;
int associationsCreated = 0;

foreach (var methodDef in bundle.Methods)
{
    // Step 2a: Resolve literals
    var literalOOPs = new List<SmalltalkOOP>();

    foreach (var literal in methodDef.Literals)
    {
        SmalltalkOOP literalOOP = ResolveLiteral(literal, canonicalAssociations);
        literalOOPs.Add(literalOOP);
    }

    // Step 2b: Encode header
    int argCount = methodDef.ArgumentCount;
    int tempCount = methodDef.TemporaryCount;
    int primitiveNum = methodDef.PrimitiveNumber ?? 0;

    ushort headerWord = (ushort)(
        (argCount & 0x1F) |
        ((tempCount & 0x1F) << 5) |
        ((primitiveNum & 0x1F) << 10)
    );
    SmalltalkOOP headerOOP = SmalltalkOOP.FromSmallInteger(headerWord);

    // Step 2c: Allocate bytecode ByteArray
    byte[] bytecodes = methodDef.Bytecodes;
    SmalltalkOOP bytecodeArrayClassOOP = _localIdToObjectId["class:ByteArray"];

    SmalltalkOOP bytecodeArrayOOP = _heapManager.AllocateObject(
        ObjectFormat.ByteIndexed,
        fixedSlots: 0,
        indexedSize: bytecodes.Length
    );
    _heapManager.SetSlot(bytecodeArrayOOP, 0, bytecodeArrayClassOOP);

    for (int i = 0; i < bytecodes.Length; i++)
    {
        _heapManager.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
    }

    // Step 2d: Allocate CompiledMethod
    int literalCount = literalOOPs.Count;
    SmalltalkOOP methodClassOOP = _localIdToObjectId["class:CompiledMethod"];

    SmalltalkOOP methodOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        fixedSlots: 0,
        indexedSize: literalCount + 2  // header + literals + bytecode array ref
    );
    _heapManager.SetSlot(methodOOP, 0, methodClassOOP);

    // Step 2e: Set header and literals
    _heapManager.SetIndexedOOP(methodOOP, 0, headerOOP);

    for (int i = 0; i < literalOOPs.Count; i++)
    {
        _heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOPs[i]);
    }

    // Step 2f: Set bytecode array reference (hidden last slot)
    _heapManager.SetIndexedOOP(methodOOP, literalCount + 1, bytecodeArrayOOP);

    // Step 2g: Store in map
    string methodId = $"{methodDef.ClassName}>>#{methodDef.SelectorName}";
    compiledMethods[methodId] = methodOOP;

    methodsCompiled++;

    if (methodsCompiled % 1000 == 0)
        Console.WriteLine($"  Compiled {methodsCompiled} / {bundle.Methods.Length} methods...");
}

Console.WriteLine($"✓ Compiled {methodsCompiled} methods");
Console.WriteLine($"✓ Created {canonicalAssociations.Count} canonical associations");
```

**Performance**: ~10,091 method allocations + literal resolution
- Time: ~100-150ms (most expensive phase)
- Allocations: ~10,091 CompiledMethods + ~10,091 ByteArrays + ~500 Associations

#### Step 3: Resolve Literal Helper

```csharp
SmalltalkOOP ResolveLiteral(LiteralValue literal, Dictionary<string, SmalltalkOOP> associations)
{
    switch (literal.Type)
    {
        case LiteralType.SmallInteger:
            return SmalltalkOOP.FromSmallInteger(literal.IntValue);

        case LiteralType.Symbol:
            int symbolIndex = literal.SymbolIndex;
            return _symbolRegistry.GetSymbol(symbolIndex);

        case LiteralType.String:
            // Allocate String object
            byte[] utf8 = Encoding.UTF8.GetBytes(literal.StringValue);
            var stringClassOOP = _localIdToObjectId["class:String"];
            var stringOOP = _heapManager.AllocateObject(
                ObjectFormat.ByteIndexed, 0, utf8.Length);
            _heapManager.SetSlot(stringOOP, 0, stringClassOOP);
            for (int i = 0; i < utf8.Length; i++)
                _heapManager.SetIndexedByte(stringOOP, i, utf8[i]);
            return stringOOP;

        case LiteralType.Class:
            string classLocalId = $"class:{literal.ClassName}";
            return _localIdToObjectId[classLocalId];

        case LiteralType.GlobalVariable:
            // Canonicalize association
            string globalName = literal.GlobalName;
            if (!associations.TryGetValue(globalName, out var assocOOP))
            {
                // Create new Association
                assocOOP = AllocateAssociation(globalName);
                associations[globalName] = assocOOP;
            }
            return assocOOP;

        case LiteralType.Array:
            // Recursively resolve array elements
            var elementOOPs = literal.ArrayElements
                .Select(elem => ResolveLiteral(elem, associations))
                .ToArray();
            var arrayClassOOP = _localIdToObjectId["class:Array"];
            var arrayOOP = _heapManager.AllocateObject(
                ObjectFormat.PointerIndexed, 0, elementOOPs.Length);
            _heapManager.SetSlot(arrayOOP, 0, arrayClassOOP);
            for (int i = 0; i < elementOOPs.Length; i++)
                _heapManager.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
            return arrayOOP;

        default:
            throw new InvalidOperationException($"Unknown literal type: {literal.Type}");
    }
}

SmalltalkOOP AllocateAssociation(string globalName)
{
    var assocClassOOP = _localIdToObjectId["class:Association"];
    var keySymbolOOP = _symbolRegistry.GetOrCreateSymbol(globalName);
    var nilOOP = _specialObjectsArray.GetNil();

    // Association has 2 instance variables: key, value
    var assocOOP = _heapManager.AllocateObject(ObjectFormat.Fixed, 2, 0);
    _heapManager.SetSlot(assocOOP, 0, assocClassOOP);
    _heapManager.SetInstanceVariable(assocOOP, 0, keySymbolOOP);  // key
    _heapManager.SetInstanceVariable(assocOOP, 1, nilOOP);        // value (to be set in Phase 13)

    return assocOOP;
}
```

### Data Structure State After Phase 8

#### CompiledMethod Example: Point>>+ (addition)

```csharp
Method Definition from JSON:
{
  "className": "Point",
  "selectorName": "+",
  "argumentCount": 1,
  "temporaryCount": 0,
  "primitiveNumber": null,
  "literals": [
    { "type": "Symbol", "symbolIndex": 1523 },  // #x
    { "type": "Symbol", "symbolIndex": 1824 },  // #y
  ],
  "bytecodes": [0x10, 0x00, 0xD0, 0x11, 0x00, 0xD0, 0x60, 0xC2, 0x7C]
}

Compiled Result:
Heap Object: CompiledMethod for Point>>+ (oop=30000)
  Slot 0 (class):       class:CompiledMethod
  Slot 1 (indexed[0]):  SmallInteger(0x0001) - header (1 arg, 0 temps, no primitive)
  Slot 2 (indexed[1]):  Symbol("+") - selector
  Slot 3 (indexed[2]):  Symbol("x") - literal 1
  Slot 4 (indexed[3]):  Symbol("y") - literal 2
  Slot 5 (indexed[4]):  OOP #30001 → ByteArray with bytecodes

Heap Object: ByteArray for Point>>+ (oop=30001)
  Slot 0 (class):       class:ByteArray
  Indexed bytes:        [0x10, 0x00, 0xD0, 0x11, 0x00, 0xD0, 0x60, 0xC2, 0x7C]
```

#### Association Example: Smalltalk Global

```csharp
Heap Object: Association for "Smalltalk" (oop=28500)
  Slot 0 (class):  class:Association
  Slot 1 (key):    Symbol("Smalltalk")
  Slot 2 (value):  nil ← will be set to SystemDictionary in Phase 13
```

**Canonicalization**: ALL methods that reference "Smalltalk" share OOP #28500.

#### Compiled Methods Map

```csharp
compiledMethods = {
    ["Object>>#class"] = OOP #29000,
    ["Object>>#=="] = OOP #29001,
    ["Point>>#x"] = OOP #29500,
    ["Point>>#y"] = OOP #29501,
    ["Point>>#x:"] = OOP #29502,
    ["Point>>#+"] = OOP #30000,
    ...
    (10,091 entries)
}
```

### Critical Invariants After Phase 8

```csharp
// Invariant 1: All methods compiled
Debug.Assert(compiledMethods.Count == bundle.Methods.Length);
Debug.Assert(compiledMethods.Count == 10091);

// Invariant 2: All methods have valid headers
Debug.Assert(compiledMethods.Values.All(oop =>
{
    var headerOOP = _heapManager.GetIndexedOOP(oop, 0);
    return headerOOP.IsSmallInteger();
}));

// Invariant 3: All methods have bytecode arrays
Debug.Assert(compiledMethods.Values.All(oop =>
{
    int literalCount = _heapManager.GetIndexedSize(oop) - 2;
    var bytecodeOOP = _heapManager.GetIndexedOOP(oop, literalCount + 1);
    return _heapManager.IsValidOOP(bytecodeOOP);
}));

// Invariant 4: Associations are canonical (no duplicates)
var globalRefs = bundle.Methods
    .SelectMany(m => m.Literals)
    .Where(l => l.Type == LiteralType.GlobalVariable)
    .Select(l => l.GlobalName)
    .Distinct();
Debug.Assert(globalRefs.Count() == canonicalAssociations.Count);
```

### Memory Usage Summary

| Object Type | Count | Bytes per Item | Total Bytes |
|-------------|-------|----------------|-------------|
| CompiledMethod objects | 10,091 | ~80 avg | ~807,280 |
| ByteArray objects | 10,091 | ~50 avg | ~504,550 |
| Association objects | ~500 | 24 | ~12,000 |
| String literals | ~200 | ~30 avg | ~6,000 |
| Array literals | ~100 | ~40 avg | ~4,000 |
| **TOTAL** | | | **~1.33 MB** |

### Performance Metrics

- **Expected Duration**: 100-150ms (most expensive phase)
- **Allocations**: ~20,000+ objects (methods, bytecode arrays, literals)
- **Literal Resolution**: ~30,000-40,000 literal lookups

---

## Phase 9: Method Dictionary Installation

**Purpose**: Install compiled methods into their classes' MethodDictionaries, making methods callable.

**Input**:
- 480 MethodDictionary objects (from Phase 7)
- 10,091 CompiledMethod objects (from Phase 8)
- JSON method definitions (for class/selector mapping)

**Output**:
- All methods installed in their class's MethodDictionary
- MethodDictionary tallies updated
- Class methodDict fields set (replacing nil from Phase Y)
- Methods now callable by VM

**Why This Phase Exists**: Separating installation from compilation allows:
1. Methods to reference each other (circular dependencies)
2. Batch hash table insertion (more efficient)
3. Clear separation of concerns

**Critical Operation**: Hash table insertion using Smalltalk-80's open addressing scheme.

### Hash Table Insertion Algorithm

```csharp
void InsertMethod(SmalltalkOOP dictOOP, SmalltalkOOP selectorOOP, SmalltalkOOP methodOOP)
{
    int capacity = _heapManager.GetIndexedSize(dictOOP);

    // Hash selector to find starting index
    int hash = ComputeHash(selectorOOP);
    int index = hash % capacity;

    // Linear probing to find empty slot
    while (true)
    {
        SmalltalkOOP currentKey = _heapManager.GetIndexedOOP(dictOOP, index);

        if (current Key == nilOOP || currentKey == selectorOOP)
        {
            // Found empty slot or existing entry (replace)
            _heapManager.SetIndexedOOP(dictOOP, index, selectorOOP);      // key
            _heapManager.SetIndexedOOP(dictOOP, index + 1, methodOOP);    // value
            break;
        }

        // Move to next slot (linear probing)
        index = (index + 2) % capacity;  // +2 because pairs (key, value)
    }

    // Increment tally
    var tallyOOP = _heapManager.GetInstanceVariable(dictOOP, 0);
    int tally = tallyOOP.ToSmallInteger();
    _heapManager.SetInstanceVariable(dictOOP, 0, SmalltalkOOP.FromSmallInteger(tally + 1));
}

int ComputeHash(SmalltalkOOP symbolOOP)
{
    // Smalltalk-80 uses OOP value as hash for Symbols
    return symbolOOP.RawValue & 0x7FFFFFFF; // Positive
}
```

### Step-by-Step Process

#### Step 1: Install Methods into Dictionaries

```csharp
int methodsInstalled = 0;
var nilOOP = _specialObjectsArray.GetNil();

foreach (var methodDef in bundle.Methods)
{
    // Get class and method dictionary
    string classLocalId = methodDef.ClassName.StartsWith("meta:")
        ? $"meta:{methodDef.ClassName.Substring(5)}"
        : $"class:{methodDef.ClassName}";

    SmalltalkOOP dictOOP = methodDictionaries[classLocalId];

    // Get selector and method OOPs
    int selectorIndex = methodDef.SelectorSymbolIndex;
    SmalltalkOOP selectorOOP = _symbolRegistry.GetSymbol(selectorIndex);

    string methodId = $"{methodDef.ClassName}>>#{methodDef.SelectorName}";
    SmalltalkOOP methodOOP = compiledMethods[methodId];

    // Insert into dictionary
    InsertMethod(dictOOP, selectorOOP, methodOOP);

    methodsInstalled++;

    if (methodsInstalled % 1000 == 0)
        Console.WriteLine($"  Installed {methodsInstalled} / 10091 methods...");
}

Console.WriteLine($"✓ Installed {methodsInstalled} methods into dictionaries");
```

**Performance**: ~10,091 hash table insertions
- Average probes per insertion: ~1.5 (due to 0.75 load factor)
- Total probes: ~15,000
- Time: ~30-40ms

#### Step 2: Install Dictionaries into Classes

```csharp
int dictionariesInstalled = 0;

foreach (var cls in bundle.Classes)
{
    SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];
    SmalltalkOOP dictOOP = methodDictionaries[cls.LocalId];

    // Get methodDict field index
    int methodDictIndex = _layoutResolver.GetFieldIndex(
        cls.IsMetaclass ? "class:Metaclass" : "class:Class",
        "methodDict"
    );

    // Set methodDict field (replacing nil from Phase Y)
    _heapManager.SetInstanceVariable(classOOP, methodDictIndex, dictOOP);

    dictionariesInstalled++;
}

Console.WriteLine($"✓ Installed {dictionariesInstalled} dictionaries into classes");
```

**Result**: All 480 classes now have populated MethodDictionaries.

**Performance**: 480 field writes, ~2ms

### Data Structure State After Phase 9

#### MethodDictionary for class:Point (After Installation)

```csharp
Heap Object: MethodDictionary for class:Point (oop=25600)
  Slot 0 (class):         class:MethodDictionary
  Slot 1 (tally):         SmallInteger(28) ← updated from 0
  Slot 2 (indexed[0]):    Symbol("x")
  Slot 3 (indexed[1]):    CompiledMethod for #x
  Slot 4 (indexed[2]):    Symbol("y")
  Slot 5 (indexed[3]):    CompiledMethod for #y
  Slot 6 (indexed[4]):    Symbol("x:")
  Slot 7 (indexed[5]):    CompiledMethod for #x:
  Slot 8 (indexed[6]):    Symbol("+")
  Slot 9 (indexed[7]):    CompiledMethod for #+
  ...
  Slot 60 (indexed[58]):  nil (empty)
  Slot 61 (indexed[59]):  nil
  Slot 62 (indexed[60]):  nil
  Slot 63 (indexed[61]):  nil
```

**Load Factor**: 28 / 32 pairs = 0.875 (within target 0.75)

#### class:Point After Installation

```csharp
Heap Object: class:Point (oop=8234)
  Slot 0 (class):         meta:Point
  Slot 1 (superclass):    class:Object
  Slot 2 (methodDict):    OOP #25600 → MethodDictionary ← UPDATED IN PHASE 9
  Slot 3 (format):        SmallInteger(0x0002)
  Slot 4 (instanceVars):  nil
  Slot 5 (organization):  nil
  Slot 6 (subclasses):    nil
  Slot 7 (name):          Symbol("Point")
  Slot 8 (classPool):     nil
  Slot 9 (sharedPools):   nil
  Slot 10 (environment):  nil
  Slot 11 (category):     nil
```

### Critical Invariants After Phase 9

```csharp
// Invariant 1: All 480 classes have non-nil methodDict
Debug.Assert(bundle.Classes.All(cls =>
{
    var classOOP = _localIdToObjectId[cls.LocalId];
    int methodDictIndex = _layoutResolver.GetFieldIndex(
        cls.IsMetaclass ? "class:Metaclass" : "class:Class", "methodDict");
    var dictOOP = _heapManager.GetInstanceVariable(classOOP, methodDictIndex);
    return dictOOP != nilOOP;
}));

// Invariant 2: All methods installed (tallies match counts)
Debug.Assert(methodDictionaries.All(kvp =>
{
    var dictOOP = kvp.Value;
    var tallyOOP = _heapManager.GetInstanceVariable(dictOOP, 0);
    int tally = tallyOOP.ToSmallInteger();

    // Count should match methods for this class
    string classLocalId = kvp.Key;
    int expectedCount = bundle.Methods.Count(m =>
        (m.ClassName.StartsWith("meta:")
            ? $"meta:{m.ClassName.Substring(5)}"
            : $"class:{m.ClassName}") == classLocalId);

    return tally == expectedCount;
}));

// Invariant 3: No hash collisions unresolved
Debug.Assert(methodDictionaries.Values.All(dictOOP =>
{
    int capacity = _heapManager.GetIndexedSize(dictOOP);
    for (int i = 0; i < capacity; i += 2)
    {
        var key = _heapManager.GetIndexedOOP(dictOOP, i);
        if (key != nilOOP)
        {
            var value = _heapManager.GetIndexedOOP(dictOOP, i + 1);
            Debug.Assert(value != nilOOP); // key must have value
        }
    }
    return true;
}));
```

### Memory Usage Summary

No new allocations in Phase 9 (only updates existing objects).

**Memory Writes**:
- ~10,091 × 2 = 20,182 MethodDictionary slot writes (selector + method)
- ~480 tally updates
- ~480 class methodDict field updates
- **Total**: ~21,142 writes (~85 KB modified)

### Performance Metrics

- **Expected Duration**: 30-50ms
- **Hash Insertions**: 10,091
- **Average Probes**: ~1.5 per insertion
- **Field Updates**: 480

### Next Phase Preview

**Phase 10-14** will:
- Phase 10: Apply patches (fix special cases, adjust metadata)
- Phase 11: Prepare init expressions for execution
- Phase 12: Populate user symbol table (USTable)
- Phase 13: Install global variables (Smalltalk dictionary, class pools)
- Phase 14: Final validation (check all invariants, verify heap consistency)

After Phase 14, the system will be ready to execute Smalltalk code and run init expressions.


---

## Phase 10: Patch Application

**Purpose**: Apply targeted fixes and adjustments that don't fit cleanly into earlier phases, including special method modifications and metadata corrections.

**Input**:
- Fully populated object heap (Phases 1-9 complete)
- JSON patch definitions (if any)
- Known special cases requiring fixup

**Output**:
- Special methods adjusted (e.g., primitives corrected, literals fixed)
- Metadata inconsistencies resolved
- Edge cases handled

**Why This Phase Exists**: Some operations can only be done after the full system is loaded:
1. Methods that reference classes not available earlier
2. Circular dependencies between methods and globals
3. Runtime-discovered issues from bundle generation
4. Compatibility fixes for Smalltalk-80 quirks

**Note**: In a perfect world, this phase would be empty. In practice, it handles corner cases.

### Common Patch Types

**Patch Type 1: Primitive Number Corrections**
```csharp
// Some methods may have incorrect primitive numbers in JSON
// Example: SmallInteger>>+ should be primitive 1, but JSON says 0

var methodOOP = FindMethod("SmallInteger", "+");
var headerOOP = _heapManager.GetIndexedOOP(methodOOP, 0);
ushort oldHeader = (ushort)headerOOP.ToSmallInteger();

// Extract and fix primitive number
int primitiveNum = (oldHeader >> 10) & 0x1F;
if (primitiveNum != 1)
{
    ushort newHeader = (ushort)((oldHeader & ~(0x1F << 10)) | (1 << 10));
    _heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(newHeader));
    Console.WriteLine($"✓ Fixed primitive for SmallInteger>>+ : {primitiveNum} → 1");
}
```

**Patch Type 2: Literal Adjustments**
```csharp
// Some methods may need literal references updated
// Example: Method references wrong class due to bundle generation bug

var methodOOP = FindMethod("SomeClass", "someMethod");
var wrongClassOOP = _heapManager.GetIndexedOOP(methodOOP, 2); // literal index 1
var correctClassOOP = _localIdToObjectId["class:CorrectClass"];

if (wrongClassOOP != correctClassOOP)
{
    _heapManager.SetIndexedOOP(methodOOP, 2, correctClassOOP);
    Console.WriteLine($"✓ Fixed class literal in SomeClass>>someMethod");
}
```

**Patch Type 3: Missing Subclass Links**
```csharp
// Populate subclass arrays for classes that track their subclasses
// This is optional metadata, not critical for VM operation

foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
{
    var subclasses = bundle.Classes
        .Where(c => c.SuperclassLocalId == cls.LocalId)
        .Select(c => _localIdToObjectId[c.LocalId])
        .ToArray();

    if (subclasses.Length > 0)
    {
        var arrayClassOOP = _localIdToObjectId["class:Array"];
        var subclassArrayOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed, 0, subclasses.Length);
        _heapManager.SetSlot(subclassArrayOOP, 0, arrayClassOOP);

        for (int i = 0; i < subclasses.Length; i++)
            _heapManager.SetIndexedOOP(subclassArrayOOP, i, subclasses[i]);

        int subclassesIndex = _layoutResolver.GetFieldIndex("class:Class", "subclasses");
        var classOOP = _localIdToObjectId[cls.LocalId];
        _heapManager.SetInstanceVariable(classOOP, subclassesIndex, subclassArrayOOP);
    }
}
```

**Patch Type 4: Block Owner ID Fixups**
```csharp
// Fix block ownership metadata for proper lexical scoping
// (Relevant if BlockClosure implementation needs ownership info)

foreach (var method in bundle.Methods)
{
    if (method.HasBlocks)
    {
        var methodOOP = FindMethod(method.ClassName, method.SelectorName);
        // Apply block ownership metadata
        // (Implementation depends on block representation)
    }
}
```

### Performance Metrics

- **Expected Duration**: 5-20ms (depends on number of patches)
- **Typical Patches**: 0-50 (varies by bundle quality)

---

## Phase 11: Init Expression Preparation

**Purpose**: Pre-compile and prepare initialization expressions for execution after coldstart completes.

**Input**:
- JSON init expression definitions
- Fully populated class hierarchy with methods
- SpecialObjectsArray with GC roots

**Output**:
- Init expression methods compiled
- Execution order determined
- Init methods stored in SpecialObjectsArray for VM access

**Why This Phase Exists**: Init expressions perform critical setup:
1. Initialize class variables (pi, e, character table, etc.)
2. Set up global objects (Smalltalk dictionary, Processor, etc.)
3. Configure system defaults
4. Prepare the environment for user code

**Critical Concept**: Init expressions are **EXECUTABLE SMALLTALK CODE**, not static data. They must run AFTER coldstart finishes.

### Init Expression Structure

```json
{
  "initExpressions": [
    {
      "order": 1,
      "expression": "Smalltalk at: #Processor put: ProcessorScheduler new",
      "description": "Initialize the processor scheduler"
    },
    {
      "order": 2,
      "expression": "Float initialize",
      "description": "Set up Float constants (Pi, E, etc.)"
    },
    {
      "order": 3,
      "expression": "Character initialize",
      "description": "Populate ASCII character table"
    }
  ]
}
```

### Step-by-Step Process

#### Step 1: Parse Init Expressions

```csharp
var initExpressions = bundle.InitExpressions
    .OrderBy(e => e.Order)
    .ToList();

Console.WriteLine($"Found {initExpressions.Count} init expressions");
```

#### Step 2: Compile Init Expressions to Methods

Each init expression is compiled into a temporary method:

```csharp
var initMethods = new List<SmalltalkOOP>();

foreach (var initExpr in initExpressions)
{
    // Compile expression to bytecode
    // (This uses the Smalltalk compiler, which should be available after Phase 8-9)
    byte[] bytecode = CompileExpression(initExpr.Expression);

    // Create CompiledMethod for init expression
    var methodOOP = CreateInitMethod(bytecode, initExpr.Expression);
    initMethods.Add(methodOOP);

    Console.WriteLine($"  Compiled init #{initExpr.Order}: {initExpr.Description}");
}
```

**Note**: If the compiler isn't available during coldstart, init expressions may be pre-compiled in the JSON bundle.

#### Step 3: Store Init Methods in SpecialObjectsArray

```csharp
// SpecialObjectsArray has a slot for init methods
var arrayClassOOP = _localIdToObjectId["class:Array"];
var initMethodArrayOOP = _heapManager.AllocateObject(
    ObjectFormat.PointerIndexed, 0, initMethods.Count);
_heapManager.SetSlot(initMethodArrayOOP, 0, arrayClassOOP);

for (int i = 0; i < initMethods.Count; i++)
{
    _heapManager.SetIndexedOOP(initMethodArrayOOP, i, initMethods[i]);
}

_specialObjectsArray.SetInitMethods(initMethodArrayOOP);

Console.WriteLine($"✓ Stored {initMethods.Count} init methods in SpecialObjectsArray");
```

**VM Execution**: After coldstart, the VM will:
```csharp
var initMethodsOOP = _specialObjectsArray.GetInitMethods();
int count = _heapManager.GetIndexedSize(initMethodsOOP);

for (int i = 0; i < count; i++)
{
    var methodOOP = _heapManager.GetIndexedOOP(initMethodsOOP, i);
    ExecuteMethod(nilReceiver, methodOOP); // Execute with nil as receiver
}
```

### Performance Metrics

- **Expected Duration**: 10-30ms
- **Allocations**: ~10-50 CompiledMethod objects for init expressions

---

## Phase 12: USTable Population

**Purpose**: Populate the User Symbol Table (USTable) with symbols for runtime symbol lookup.

**Input**:
- Symbol registry (3,834 symbols from Phase 1)
- class:Dictionary class

**Output**:
- USTable dictionary allocated
- All symbols registered in USTable
- USTable stored in SpecialObjectsArray

**Why This Phase Exists**: The USTable allows runtime code to:
1. Look up symbols by name (for dynamic message sends)
2. Intern new symbols at runtime
3. Query existing symbols

**Critical Difference from SymbolRegistry**:
- SymbolRegistry: Coldstart-only data structure, discarded after Phase 14
- USTable: Runtime dictionary, persists for VM use

### Step-by-Step Process

#### Step 1: Allocate USTable Dictionary

```csharp
// Calculate capacity for 3,834 symbols with 0.75 load factor
int capacity = NextPowerOfTwo((int)(3834 / 0.75));  // ~8192

var dictionaryClassOOP = _localIdToObjectId["class:Dictionary"];
var ustableOOP = _heapManager.AllocateObject(
    ObjectFormat.PointerIndexed,
    fixedSlots: 1,  // tally
    indexedSize: capacity
);
_heapManager.SetSlot(ustableOOP, 0, dictionaryClassOOP);
_heapManager.SetInstanceVariable(ustableOOP, 0, SmalltalkOOP.FromSmallInteger(0)); // tally = 0

// Initialize all slots to nil
var nilOOP = _specialObjectsArray.GetNil();
for (int i = 0; i < capacity; i++)
{
    _heapManager.SetIndexedOOP(ustableOOP, i, nilOOP);
}

Console.WriteLine($"✓ Allocated USTable with capacity {capacity}");
```

#### Step 2: Insert All Symbols into USTable

```csharp
int symbolsInserted = 0;

for (int i = 0; i < bundle.Symbols.Length; i++)
{
    SmalltalkOOP symbolOOP = _symbolRegistry.GetSymbol(i);
    string symbolName = bundle.Symbols[i];

    // Insert into USTable (key = Symbol, value = Symbol)
    InsertIntoDictionary(ustableOOP, symbolOOP, symbolOOP);
    symbolsInserted++;
}

Console.WriteLine($"✓ Inserted {symbolsInserted} symbols into USTable");
```

**Why Key = Value?**: For symbols, the symbol object itself is both the key and value. This allows lookup by symbol name or symbol OOP.

#### Step 3: Store USTable in SpecialObjectsArray

```csharp
_specialObjectsArray.SetUSTable(ustableOOP);

Console.WriteLine($"✓ Registered USTable in SpecialObjectsArray");
```

### Performance Metrics

- **Expected Duration**: 20-40ms
- **Allocations**: 1 Dictionary (USTable)
- **Insertions**: 3,834 symbols

---

## Phase 13: Global Variable Installation

**Purpose**: Populate the Smalltalk global dictionary and class pools with global variables and class-side variables.

**Input**:
- Canonical associations from Phase 8
- JSON global variable definitions
- class:SystemDictionary class

**Output**:
- Smalltalk global dictionary populated
- Class pools populated with class variables
- SpecialObjectsArray updated with Smalltalk dictionary

**Why This Phase Exists**: Global variables are the primary namespace:
1. System globals (Smalltalk, Processor, Transcript, etc.)
2. All classes accessible by name
3. User-defined globals

**Critical Invariant**: Association objects must be the SAME objects used in method literals (Phase 8 canonicalization).

### Step-by-Step Process

#### Step 1: Allocate Smalltalk Dictionary

```csharp
// Estimate globals: 480 classes + ~50 system globals + ~100 user globals = ~630
int capacity = NextPowerOfTwo((int)(630 / 0.75));  // ~1024

var systemDictClassOOP = _localIdToObjectId["class:SystemDictionary"];
var smalltalkDictOOP = _heapManager.AllocateObject(
    ObjectFormat.PointerIndexed,
    fixedSlots: 1,  // tally
    indexedSize: capacity
);
_heapManager.SetSlot(smalltalkDictOOP, 0, systemDictClassOOP);
_heapManager.SetInstanceVariable(smalltalkDictOOP, 0, SmalltalkOOP.FromSmallInteger(0));

// Initialize to nil
var nilOOP = _specialObjectsArray.GetNil();
for (int i = 0; i < capacity; i++)
{
    _heapManager.SetIndexedOOP(smalltalkDictOOP, i, nilOOP);
}

Console.WriteLine($"✓ Allocated Smalltalk dictionary with capacity {capacity}");
```

#### Step 2: Install Class References

```csharp
int classesInstalled = 0;

foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
{
    string className = cls.ClassName;
    var classOOP = _localIdToObjectId[cls.LocalId];

    // Get or create canonical association for this class name
    SmalltalkOOP assocOOP = GetCanonicalAssociation(className);

    // Set association value to the class
    _heapManager.SetInstanceVariable(assocOOP, 1, classOOP); // value field

    // Insert into Smalltalk dictionary
    InsertIntoDictionary(smalltalkDictOOP, assocOOP, assocOOP);
    classesInstalled++;
}

Console.WriteLine($"✓ Installed {classesInstalled} class references in Smalltalk dictionary");
```

**Result**: Now you can do `Smalltalk at: #Point` and get the Point class.

#### Step 3: Install System Globals

```csharp
var systemGlobals = new Dictionary<string, SmalltalkOOP>
{
    ["Smalltalk"] = smalltalkDictOOP,
    ["Processor"] = nilOOP,  // Will be set by init expression
    ["Transcript"] = nilOOP,  // Will be set by init expression
    // ... more system globals
};

foreach (var kvp in systemGlobals)
{
    SmalltalkOOP assocOOP = GetCanonicalAssociation(kvp.Key);
    _heapManager.SetInstanceVariable(assocOOP, 1, kvp.Value);
    InsertIntoDictionary(smalltalkDictOOP, assocOOP, assocOOP);
}

Console.WriteLine($"✓ Installed {systemGlobals.Count} system globals");
```

#### Step 4: Populate Class Pools

```csharp
foreach (var cls in bundle.Classes)
{
    if (cls.ClassPool == null || cls.ClassPool.Count == 0)
        continue;

    var classPoolDictOOP = GetClassPool(cls.LocalId); // From Phase 4

    foreach (var poolEntry in cls.ClassPool)
    {
        string varName = poolEntry.Key;
        var value = ResolvePoolValue(poolEntry.Value);

        SmalltalkOOP assocOOP = GetCanonicalAssociation($"{cls.ClassName}.{varName}");
        _heapManager.SetInstanceVariable(assocOOP, 1, value);
        InsertIntoDictionary(classPoolDictOOP, assocOOP, assocOOP);
    }

    Console.WriteLine($"  Populated class pool for {cls.ClassName}: {cls.ClassPool.Count} entries");
}
```

**Example**: `Character` class pool contains entries like:
```
CR → Character value: 13
LF → Character value: 10
Space → Character value: 32
```

#### Step 5: Store Smalltalk Dictionary in SpecialObjectsArray

```csharp
_specialObjectsArray.SetSmalltalk(smalltalkDictOOP);

Console.WriteLine($"✓ Registered Smalltalk dictionary in SpecialObjectsArray");
```

### Performance Metrics

- **Expected Duration**: 30-50ms
- **Allocations**: 1 Smalltalk dictionary + updated association values
- **Insertions**: ~630 globals + ~2000 class pool entries

---

## Phase 14: Comprehensive Validation

**Purpose**: Perform exhaustive validation of the entire object heap to ensure correctness before beginning execution.

**Input**:
- Complete object heap (all phases 0-13 complete)

**Output**:
- Validation report with statistics
- Confirmation that all invariants hold
- System ready for execution

**Why This Phase Exists**: This is the final checkpoint before transitioning from coldstart to runtime. We verify:
1. No objects have invalid class references
2. No OOP slots contain illegal values (0 or out of bounds)
3. All required objects exist (nil, true, false, classes, etc.)
4. GC can traverse entire heap without errors
5. Method dictionaries are properly formed

### Validation Steps

#### Validation 1: Object Table Integrity

```csharp
int totalObjects = _heapManager.GetObjectCount();
int validObjects = 0;
int freeSlots = 0;

for (int i = 0; i < totalObjects; i++)
{
    var oop = _heapManager.GetObjectOOPByIndex(i);

    if (!_heapManager.IsValidOOP(oop))
    {
        freeSlots++;
        continue;
    }

    validObjects++;

    // Validate class reference
    var classOOP = _heapManager.GetSlot(oop, 0);
    if (classOOP.RawValue == 0)
        throw new Exception($"Object {oop} has null class reference");

    if (!_heapManager.IsValidOOP(classOOP))
        throw new Exception($"Object {oop} has invalid class OOP {classOOP}");
}

Console.WriteLine($"✓ Validated {validObjects} objects ({freeSlots} free slots)");
```

#### Validation 2: OOP Slot Integrity

```csharp
int totalSlots = 0;
int oop Slots = 0;
int invalidSlots = 0;

foreach (var oop in _heapManager.EnumerateObjects())
{
    var format = _heapManager.GetFormat(oop);

    if (format == ObjectFormat.PointerIndexed || format == ObjectFormat.Fixed)
    {
        int slotCount = _heapManager.GetTotalSlotCount(oop);
        for (int i = 0; i < slotCount; i++)
        {
            var slotValue = _heapManager.GetSlot(oop, i);
            totalSlots++;

            // Check if SmallInteger (always valid)
            if (slotValue.IsSmallInteger())
                continue;

            oopSlots++;

            // Check if valid OOP
            if (slotValue.RawValue != 0 && !_heapManager.IsValidOOP(slotValue))
            {
                invalidSlots++;
                Console.WriteLine($"WARNING: Object {oop} slot {i} has invalid OOP {slotValue}");
            }
        }
    }
}

if (invalidSlots > 0)
    throw new Exception($"Found {invalidSlots} invalid OOP slots");

Console.WriteLine($"✓ Validated {totalSlots} slots ({oopSlots} OOP references)");
```

#### Validation 3: Class Hierarchy Integrity

```csharp
// Validate all class superclass chains terminate at Object → nil
foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
{
    var classOOP = _localIdToObjectId[cls.LocalId];
    var visited = new HashSet<SmalltalkOOP>();
    var current = classOOP;

    while (true)
    {
        if (!visited.Add(current))
            throw new Exception($"Circular superclass chain involving {cls.LocalId}");

        int superclassIndex = _layoutResolver.GetFieldIndex("class:Class", "superclass");
        var superclass = _heapManager.GetInstanceVariable(current, superclassIndex);

        if (superclass == nilOOP)
            break; // Reached Object.superclass = nil

        current = superclass;

        if (visited.Count > 100)
            throw new Exception($"Suspiciously deep hierarchy for {cls.LocalId}");
    }
}

Console.WriteLine($"✓ Validated all {bundle.Classes.Length / 2} class hierarchies");
```

#### Validation 4: Method Dictionary Integrity

```csharp
int validDictionaries = 0;

foreach (var cls in bundle.Classes)
{
    var classOOP = _localIdToObjectId[cls.LocalId];
    int methodDictIndex = _layoutResolver.GetFieldIndex(
        cls.IsMetaclass ? "class:Metaclass" : "class:Class", "methodDict");
    var dictOOP = _heapManager.GetInstanceVariable(classOOP, methodDictIndex);

    if (dictOOP == nilOOP)
        continue; // Empty dictionary OK

    // Validate tally ≤ capacity
    var tallyOOP = _heapManager.GetInstanceVariable(dictOOP, 0);
    int tally = tallyOOP.ToSmallInteger();
    int capacity = _heapManager.GetIndexedSize(dictOOP);

    if (tally > capacity / 2)
        throw new Exception($"Dictionary for {cls.LocalId} overfull: {tally} > {capacity / 2}");

    // Validate all keys have values
    for (int i = 0; i < capacity; i += 2)
    {
        var key = _heapManager.GetIndexedOOP(dictOOP, i);
        if (key != nilOOP)
        {
            var value = _heapManager.GetIndexedOOP(dictOOP, i + 1);
            if (value == nilOOP)
                throw new Exception($"Key without value in {cls.LocalId} dictionary at index {i}");
        }
    }

    validDictionaries++;
}

Console.WriteLine($"✓ Validated {validDictionaries} method dictionaries");
```

#### Validation 5: GC Reachability

```csharp
// Perform full GC mark phase to verify all objects reachable from roots
var reachable = new HashSet<SmalltalkOOP>();
var roots = new List<SmalltalkOOP>
{
    _specialObjectsArray.GetNil(),
    _specialObjectsArray.GetTrue(),
    _specialObjectsArray.GetFalse(),
    _specialObjectsArray.GetSmalltalk(),
    _specialObjectsArray.GetUSTable(),
};

// Add all classes as roots
foreach (var cls in bundle.Classes)
{
    roots.Add(_localIdToObjectId[cls.LocalId]);
}

// Mark phase
void Mark(SmalltalkOOP oop)
{
    if (oop.IsSmallInteger() || !_heapManager.IsValidOOP(oop))
        return;

    if (!reachable.Add(oop))
        return; // Already visited

    // Traverse slots
    var format = _heapManager.GetFormat(oop);
    if (format == ObjectFormat.PointerIndexed || format == ObjectFormat.Fixed)
    {
        int slotCount = _heapManager.GetTotalSlotCount(oop);
        for (int i = 0; i < slotCount; i++)
        {
            var child = _heapManager.GetSlot(oop, i);
            Mark(child);
        }
    }
}

foreach (var root in roots)
    Mark(root);

int unreachable = totalObjects - reachable.Count;
if (unreachable > 0)
    Console.WriteLine($"WARNING: {unreachable} objects unreachable from roots (may be garbage)");

Console.WriteLine($"✓ GC reachability: {reachable.Count} / {totalObjects} objects reachable");
```

#### Validation 6: Special Objects Exist

```csharp
Debug.Assert(_specialObjectsArray.GetNil() != SmalltalkOOP.Null);
Debug.Assert(_specialObjectsArray.GetTrue() != SmalltalkOOP.Null);
Debug.Assert(_specialObjectsArray.GetFalse() != SmalltalkOOP.Null);
Debug.Assert(_specialObjectsArray.GetSmalltalk() != SmalltalkOOP.Null);
Debug.Assert(_specialObjectsArray.GetUSTable() != SmalltalkOOP.Null);

Console.WriteLine($"✓ All special objects present in SpecialObjectsArray");
```

### Final Statistics Report

```csharp
Console.WriteLine($"\n==== Cold Start Complete ====");
Console.WriteLine($"Total objects allocated: {totalObjects}");
Console.WriteLine($"  Symbols: 3,834");
Console.WriteLine($"  Classes: 480");
Console.WriteLine($"  Methods: 10,091");
Console.WriteLine($"  Singletons: 3 (nil, true, false)");
Console.WriteLine($"  Other objects: {totalObjects - 3834 - 480 - 10091 - 3}");
Console.WriteLine($"");
Console.WriteLine($"Memory usage:");
Console.WriteLine($"  Object table: ~{totalObjects * 16} bytes");
Console.WriteLine($"  Heap storage: ~{totalSlots * 4} bytes");
Console.WriteLine($"  Total: ~{(totalObjects * 16 + totalSlots * 4) / 1024} KB");
Console.WriteLine($"");
Console.WriteLine($"Ready to execute Smalltalk code!");
```

### Performance Metrics

- **Expected Duration**: 50-100ms (thorough validation)
- **Checks**: ~50,000-100,000 validation operations

---

## Post-Phase 14: Transition to Runtime

After Phase 14 completes successfully, the system transitions from coldstart to runtime mode:

### Cleanup Operations

```csharp
// Discard coldstart-only data structures
_localIdToObjectId.Clear();
_localIdToObjectId = null;

_symbolRegistry.Freeze(); // Symbol registry becomes read-only
// (Or discard if USTable is used instead)

_canonicalAssociations.Clear();
_canonicalAssociations = null;

// LayoutResolver remains accessible for runtime field lookups

Console.WriteLine($"✓ Discarded coldstart data structures");
```

### Memory Savings

Discarding coldstart structures frees:
- localIdToObjectId: ~20 KB
- canonicalAssociations: ~10 KB
- Temporary phase tracking: ~5 KB
- **Total freed**: ~35 KB

### Enable Runtime Features

```csharp
// Mark system as ready for execution
_vmState.SetColdStartComplete();

// Enable interpreter
_interpreter.Initialize();

// Execute init expressions
ExecuteInitExpressions();

Console.WriteLine($"✓ System ready - interpreter enabled");
```

### Execute Init Expressions

```csharp
var initMethodsOOP = _specialObjectsArray.GetInitMethods();
int initCount = _heapManager.GetIndexedSize(initMethodsOOP);

for (int i = 0; i < initCount; i++)
{
    var methodOOP = _heapManager.GetIndexedOOP(initMethodsOOP, i);

    Console.WriteLine($"Executing init expression #{i + 1}...");

    try
    {
        _interpreter.Execute(nilReceiver, methodOOP);
        Console.WriteLine($"  ✓ Success");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Failed: {ex.Message}");
        throw;
    }
}

Console.WriteLine($"✓ All {initCount} init expressions completed");
```

### System Ready

At this point:
- ✅ All objects allocated and initialized
- ✅ All methods compiled and installed
- ✅ All globals and class pools populated
- ✅ Init expressions executed
- ✅ System globals configured (Smalltalk, Processor, etc.)
- ✅ Interpreter ready to execute user code

**The Smalltalk-80 system is now fully operational!**

---

## Complete Phase Summary

| Phase | Name | Duration | Objects Allocated | Key Operation |
|-------|------|----------|-------------------|---------------|
| 0 | Preflight Validation | ~10ms | 0 | Validate JSON bundle |
| 1 | Symbol Interning | ~20ms | 3,834 | Create all symbols |
| 2 | Class Shells | ~15ms | 480 | Allocate empty classes |
| 3 | Metaclass Wiring | ~5ms | 0 | Wire metaclass circle |
| 4 | Class Resolution | ~25ms | ~40 | Set class metadata |
| 5 | Layout Computation | ~20ms | 0 | Calculate field layouts |
| 6 | Object Allocation | <1ms | 3 | Create nil/true/false |
| X | Class Reference Init | ~10ms | 0 | Set all class refs |
| Y | Instance Var Init | ~15ms | 0 | Initialize fields to nil |
| 7 | Method Dict Allocation | ~25ms | 480 | Allocate dictionaries |
| 8 | Method Installation | ~120ms | ~20,000 | Compile all methods |
| 9 | Method Dict Install | ~35ms | 0 | Populate dictionaries |
| 10 | Patch Application | ~10ms | ~10 | Apply fixes |
| 11 | Init Expr Preparation | ~20ms | ~20 | Compile init code |
| 12 | USTable Population | ~30ms | 1 | Create symbol table |
| 13 | Global Installation | ~40ms | 1 | Populate Smalltalk dict |
| 14 | Validation | ~80ms | 0 | Verify heap integrity |
| **TOTAL** | | **~485ms** | **~25,000** | **Complete bootstrap** |

---

## Key Takeaways for LLM Analysis

### Missing Steps to Consider

1. **Parallel Phase Execution**: Phases 1, 2, 7 could run in parallel (independent allocations)
2. **Incremental GC**: Could perform partial GC between phases to reduce peak memory
3. **Lazy Initialization**: Some metadata (subclasses, organization) could be computed on demand
4. **Compressed Literals**: Common literals (nil, true, false) could be deduplicated earlier
5. **Streaming Bundle Loading**: Load and process JSON in chunks rather than all-at-once

### Optimization Opportunities

1. **Phase Merging**: Phases X and Y could merge with Phase 6
2. **Layout Caching**: Pre-compute layouts in JSON bundle
3. **Method Batching**: Compile methods in parallel (thread-safe heap required)
4. **Association Pooling**: Pre-allocate association objects based on bundle analysis
5. **Dictionary Sizing**: More accurate capacity predictions to reduce wasted space

### Alternative Architectures

1. **Two-Pass Bundle**: First pass allocates, second pass initializes (simpler dependency management)
2. **Snapshot Loading**: Serialize pre-initialized heap, load directly (eliminates coldstart)
3. **JIT Coldstart**: Defer non-critical phases until first use (faster startup)
4. **Incremental Coldstart**: Load core system first, add classes/methods on demand
5. **VM Bytecode**: Pre-compile init expressions to VM bytecode (faster than Smalltalk compilation)

### Critical Dependencies

```
Phase 0 → All phases (must validate first)
Phase 1 → Phases 2,4,8 (need symbols)
Phase 2 → Phases 3,4,X,Y (need class objects)
Phase 3 → Phase 4 (need fundamental circle)
Phase 4 → Phases 5,6,Y (need metadata)
Phase 5 → Phases 6,Y,7-14 (need layouts)
Phase 6 → Phases X,Y,13 (need nil)
Phase X → Phase Y (need class refs for validation)
Phase Y → Phases 7-14 (need fully initialized objects)
Phases 7-9 → Phases 10-14 (need methods for execution)
Phase 13 → Phase 14 (need globals for validation)
```

**Longest Critical Path**: 0 → 1 → 2 → 3 → 4 → 5 → 6 → X → Y → 7 → 8 → 9 → 13 → 14 (~485ms total)

This documentation provides complete detail for an LLM to analyze, optimize, or redesign the cold start process.
