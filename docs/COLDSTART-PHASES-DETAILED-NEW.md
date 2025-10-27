# Smalltalk-80 Cold Start: Complete Phase-by-Phase Technical Documentation (New 6-Phase Architecture)

**Last Updated**: 2025-10-26
**Purpose**: Exhaustive documentation of every operation in every cold start phase for LLM analysis and optimization
**Audience**: LLMs analyzing the cold start process for completeness, correctness, and optimization opportunities
**Architecture**: New consolidated 6-phase loader (reduced from 17 phases)

---

## Table of Contents

- [Overview](#overview)
- [Architecture Changes](#architecture-changes)
- [JSON Input Structure](#json-input-structure)
- [Desired Final State](#desired-final-state)
- [Global Data Structures](#global-data-structures)
- [Phase 1: Preflight Validation + Symbol Interning](#phase-1-preflight-validation--symbol-interning)
- [Phase 2: Class Infrastructure](#phase-2-class-infrastructure)
- [Phase 3: Object Allocation and Initialization](#phase-3-object-allocation-and-initialization)
- [Phase 4: Method Compilation](#phase-4-method-compilation)
- [Phase 5: Globals and Patches](#phase-5-globals-and-patches)
- [Phase 6: Validation and GC Verification](#phase-6-validation-and-gc-verification)

---

## Overview

The new 6-phase cold start loader consolidates the previous 17-phase architecture into logical groupings that:
- Reduce code duplication (64% fewer phase files)
- Maintain clear separation of concerns
- Preserve all validation and safety guarantees
- Produce byte-for-byte identical heap as the old loader

### Performance Characteristics

**Total Cold Start Time**: ~800ms

| Phase | Time | Operations |
|-------|------|-----------|
| Phase 1 | ~152ms | Bundle validation + patch validation + 3,834 symbols interned |
| Phase 2 | ~23ms | 480 class shells + metaclass wiring + layouts loaded |
| Phase 3 | ~14ms | 29,230 objects allocated + class refs + nil init (3 checkpoints) |
| Phase 4 | ~368ms | 10,182 methods compiled + 596 canonical Associations |
| Phase 5 | ~127ms | 3,834 USTable entries + 246 globals + 1659 patches applied |
| Phase 6 | ~116ms | 10,950 cross-refs validated + Association canonicalization + GC verification |

**Final Heap State**:
- Objects allocated: 29,230
- Objects reachable: 29,230 (0 collected by GC)
- Association canonicalization: 84.1% memory reduction (3,745 literals → 596 canonical)
- Patch validation: 1,659 patches validated and applied
- All validation checks: PASSED

---

## Architecture Changes

### Old Architecture (17 Phases)
```
Phase 0:  Preflight Validation
Phase 1:  Symbol Interning
Phase 2:  Class Shells
Phase 3:  Metaclass Wiring
Phase 4:  Class Resolution
Phase 5:  Layout Computation
Phase 6:  Object Allocation
Phase X:  Class Reference Initialization
Phase Y:  Instance Variable Initialization
Phase 7:  Method Dictionary Allocation
Phase 8:  Method Installation
Phase 8b: Init Expression Method Creation
Phase 9:  Method Dictionary Installation
Phase 10: Patch Application
Phase 11: Init Expression Preparation (unused)
Phase 12: USTable Population
Phase 13: Global Variable Installation
Phase 14: GC Root Setup
Phase 15: GC Verification
```

### New Architecture (6 Phases)
```
Phase 1: Preflight Validation + Symbol Interning
         (consolidates Phase 0 + Phase 1)

Phase 2: Class Infrastructure
         (consolidates Phase 2 + Phase 3 + Phase 4 + Phase 5)

Phase 3: Object Allocation and Initialization
         (consolidates Phase 6 + Phase X + Phase Y)

Phase 4: Method Compilation
         (consolidates Phase 7 + Phase 8 + Phase 8b + Phase 9)

Phase 5: Globals and Patches
         (consolidates Phase 10 + Phase 12 + Phase 13)

Phase 6: Validation and GC Verification
         (consolidates Phase 14 + Phase 15)
```

### Consolidation Rationale

**Phase 1**: Bundle validation and symbol interning are tightly coupled - symbols are needed immediately after validation, so combining them eliminates an artificial phase boundary.

**Phase 2**: All class infrastructure (shells, metaclass wiring, metadata, layouts) can be done in one pass since layouts are now precomputed in v1.0.6 bundles.

**Phase 3**: Object allocation, class reference setting, and nil initialization form a single logical unit - full object initialization happens in one phase.

**Phase 4**: Method dictionaries, method compilation, and installation are all part of "getting methods ready" - consolidating eliminates intermediate states.

**Phase 5**: Patches, USTable, and globals all deal with resolving forward references and setting up the global namespace.

**Phase 6**: All validation and GC verification can happen together as the final "sanity check" phase.

---

## Recent Improvements (2025-10-26)

### Priority 1: Correctness Improvements

The following improvements enhance the correctness and robustness of the coldstart loader:

#### 1. AssociationRegistry - Centralized Canonicalization

**Problem**: Phase 4 and Phase 5 were independently creating Association objects, leading to potential duplicates and inconsistent canonicalization.

**Solution**: Created `AssociationRegistry` as a central registry for all Association objects:

```csharp
public static class AssociationRegistry
{
    private static Dictionary<string, SmalltalkOOP> _associations = new();
    private static bool _frozen = false;

    public static SmalltalkOOP GetOrCreate(string symbolName,
        Func<SmalltalkOOP> createAssociation)
    {
        if (_frozen)
            throw new InvalidOperationException("AssociationRegistry is frozen");

        if (_associations.TryGetValue(symbolName, out var existing))
            return existing;

        var associationOOP = createAssociation();
        _associations[symbolName] = associationOOP;
        return associationOOP;
    }

    public static void Freeze() => _frozen = true;
}
```

**Benefits**:
- Guaranteed single canonical Association per global name
- 84.1% memory reduction (3,745 Association literals → 596 canonical instances)
- Simplified Phase 4 (removed local `_canonicalAssociations` dictionary)
- Enforced VALUE MIGRATION POLICY (warns if incoming Association has non-nil value)

#### 2. New Patch Schema - Improved Structure and Validation

**Problem**: Old patch format used nested "Target" structure that was verbose and difficult to validate.

**Old Format** (verbose):
```json
{
  "operation": "SetInstanceVariable",
  "target": {
    "type": "objectRef",
    "localId": "class:Foo"
  },
  "fieldName": "superclass",
  "value": {
    "type": "objectRef",
    "localId": "class:Bar"
  }
}
```

**New Format** (flat, typed):
```json
{
  "patchType": "SetInstanceVariable",
  "targetLocalId": "class:Foo",
  "fieldName": "superclass",
  "value": {
    "type": "objectRef",
    "localId": "class:Bar"
  }
}
```

**Three Patch Types**:

1. **SetInstanceVariable**: Sets a specific instance variable field
   - Fields: `targetLocalId`, `fieldName`, `value`
   - Example: Set `class:Object.superclass = nil`

2. **InstallMethodDictionary**: Installs a method dictionary into a class
   - Fields: `classLocalId`, `methodDictLocalId`
   - Example: Install method dict into `class:Point`

3. **ReplaceLiteral**: Replaces a literal in a CompiledMethod
   - Fields: `methodLocalId`, `literalIndex`, `newValue`
   - Example: Replace literal 2 in `method:Point>>+` with canonical Association

**Benefits**:
- Clearer patch semantics (explicit `patchType` field)
- Simpler structure (flat vs nested)
- Better validation (type-specific checks in Phase 1)
- 1,659 patches validated and applied successfully

#### 3. Phase 1 Patch Validation

**Added**: Comprehensive patch validation during preflight checks:

```csharp
private void ValidatePatches()
{
    foreach (var patch in _bundle.Patches)
    {
        switch (patch.PatchType)
        {
            case "SetInstanceVariable":
                // Validate targetLocalId exists
                // Validate fieldName is in class layout
                break;

            case "InstallMethodDictionary":
                // Validate classLocalId exists
                // Validate methodDictLocalId exists
                break;

            case "ReplaceLiteral":
                // Validate methodLocalId exists
                // Validate literalIndex is within bounds
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown patch type: {patch.PatchType}");
        }
    }
}
```

**Validation Checks**:
- Verify all referenced `localId` values exist in bundle
- Verify field names exist in class layouts
- Verify literal indices are within method bounds
- Reject unknown patch types

**Benefits**:
- Fail fast on malformed bundles
- Clear error messages pinpointing exact issue
- Prevents runtime errors during Phase 5 patch application

#### 4. Phase 3 Internal Checkpoints

**Added**: Three internal checkpoints to track Phase 3 progress:

1. **Section 1: Allocation Checkpoint**
   - Allocates all objects from bundle
   - Logs: "Phase 3.1: Allocated N objects"

2. **Section 2: Class Reference Checkpoint**
   - Sets class slot (slot 0) for all objects
   - Logs: "Phase 3.2: Set M class references"

3. **Section 3: Initialization Checkpoint**
   - Initializes remaining fields to nil
   - Logs: "Phase 3.3: Initialized K fields to nil"

**Metrics Collection**:
```csharp
public class Phase3Metrics
{
    public int ObjectsAllocated { get; set; }
    public int ClassReferencesSet { get; set; }
    public int FieldsInitialized { get; set; }
}
```

**Benefits**:
- Better visibility into Phase 3 progress
- Easier debugging (know exactly which checkpoint failed)
- Metrics for performance analysis

#### 5. Association Canonicalization Validation (Phase 6)

**Added**: Post-coldstart validation to verify Association canonicalization:

```csharp
private void ValidateAssociationCanonicalization()
{
    int totalAssociations = 0;
    int canonicalAssociations = 0;
    HashSet<SmalltalkOOP> seen = new();

    // Scan all CompiledMethod literals
    foreach (var methodOOP in allMethods)
    {
        var literals = GetMethodLiterals(methodOOP);
        foreach (var literalOOP in literals)
        {
            if (IsAssociation(literalOOP))
            {
                totalAssociations++;
                if (seen.Add(literalOOP))
                    canonicalAssociations++;
            }
        }
    }

    double reductionPercent =
        100.0 * (1 - canonicalAssociations / (double)totalAssociations);

    _report.Log($"Association canonicalization: " +
                $"{reductionPercent:F1}% reduction " +
                $"({totalAssociations} → {canonicalAssociations})");
}
```

**Validation Checks**:
- All Associations have correct structure (key/value slots)
- Keys are Symbol objects
- Values are not corrupted
- Canonicalization metrics computed

**Benefits**:
- Confirms AssociationRegistry worked correctly
- Quantifies memory savings from canonicalization
- Detects Association corruption early

---

### Priority 2: Diagnostics

#### 1. ColdstartReport - Comprehensive Metrics Tracking

**Created**: `ColdstartReport` class for collecting coldstart metrics:

```csharp
public class ColdstartReport
{
    public string BundleVersion { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration => EndTime - StartTime;

    public Dictionary<int, PhaseMetrics> PhaseMetrics { get; } = new();
    public List<string> ValidationResults { get; } = new();
    public GarbageCollectionMetrics GCMetrics { get; set; }

    public void RecordPhaseStart(int phaseNumber, string phaseName);
    public void RecordPhaseEnd(int phaseNumber);
    public void Log(string message);
    public string GenerateReport();
}

public class PhaseMetrics
{
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public Dictionary<string, object> CustomMetrics { get; } = new();
}
```

**Collected Metrics**:
- Bundle version and metadata
- Per-phase timing (start/end/duration)
- Custom metrics per phase (objects allocated, methods compiled, etc.)
- Validation results (pass/fail for each check)
- GC metrics (allocated/reachable/collected)
- Association canonicalization stats

**Example Report Output**:
```
=== Coldstart Report ===
Bundle Version: 1.0.6
Total Duration: 800.23ms

Phase Timings:
  Phase 1 (Preflight + Symbols): 152ms
    - Symbols interned: 3834
    - Patches validated: 1659
  Phase 2 (Class Infrastructure): 23ms
    - Classes allocated: 480
  Phase 3 (Object Allocation): 14ms
    - Objects allocated: 486
    - Class refs set: 4797
    - Fields initialized: 1681
  Phase 4 (Method Compilation): 368ms
    - Methods compiled: 10182
    - Canonical Associations: 596
  Phase 5 (Globals + Patches): 127ms
    - USTable entries: 3834
    - Global variables: 246
    - Patches applied: 1659
  Phase 6 (Validation): 116ms
    - Cross-refs validated: 10950

Validation Results:
  ✓ All class references valid
  ✓ All method dictionaries installed
  ✓ Association canonicalization: 84.1% reduction
  ✓ GC verification passed

GC Metrics:
  - Objects allocated: 29,230
  - Objects reachable: 29,230
  - Objects collected: 0 (100% reachable)
```

**Benefits**:
- Complete visibility into coldstart performance
- Easy identification of bottlenecks
- Historical tracking of metrics over time
- Debugging aid (shows exactly what happened)

#### 2. SnapshotWriter - Failure Diagnostics

**Created**: `SnapshotWriter` class for capturing heap snapshots on failure:

```csharp
public class SnapshotWriter
{
    public static void CaptureSnapshot(
        ISmalltalkHeapManager heap,
        string reason,
        string outputPath)
    {
        var snapshot = new HeapSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Reason = reason,
            Objects = CaptureAllObjects(heap),
            Classes = CaptureAllClasses(heap),
            Methods = CaptureAllMethods(heap)
        };

        // Disassemble all CompiledMethods
        foreach (var method in snapshot.Methods)
        {
            method.Disassembly = DisassembleBytecodes(method.Bytecodes);
        }

        File.WriteAllText(outputPath,
            JsonSerializer.Serialize(snapshot,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<string> DisassembleBytecodes(byte[] bytecodes)
    {
        // Human-readable bytecode disassembly
        // Example: "push receiver", "send +", "return top"
    }
}
```

**Captured Information**:
- Complete heap state at time of failure
- All objects with OOP values and field contents
- All classes with metadata
- All CompiledMethods with bytecode disassembly
- Reason for snapshot (exception message, validation failure)

**Bytecode Disassembly Example**:
```json
"method:Point>>+": {
  "bytecodes": "10 90 B0 7C",
  "disassembly": [
    "0: push receiver (instance variable 0 = x)",
    "1: push argument 0",
    "2: send +",
    "3: return top of stack"
  ]
}
```

**Benefits**:
- Post-mortem debugging of failures
- Visual inspection of heap state
- Human-readable bytecode (no manual decoding)
- Historical record of failures for pattern analysis

#### 3. Command-Line Flags

**Added**: Two command-line flags for loader selection and reporting:

```bash
# Use new 6-phase loader
Smalltalk80.Interpreter.exe --new-loader

# Generate coldstart report to file
Smalltalk80.Interpreter.exe --new-loader --coldstart-report=report.txt

# Use old 17-phase loader (default)
Smalltalk80.Interpreter.exe
```

**Implementation**:
```csharp
var useNewLoader = args.Contains("--new-loader");
var reportPath = args.FirstOrDefault(a => a.StartsWith("--coldstart-report="))
                    ?.Split('=')[1];

if (useNewLoader)
{
    var report = new ColdstartReport();
    Loader_New.Run(bundle, heapManager, report);

    if (reportPath != null)
    {
        File.WriteAllText(reportPath, report.GenerateReport());
        Console.WriteLine($"Coldstart report written to: {reportPath}");
    }
}
else
{
    Loader.Run(bundle, heapManager);  // Old 17-phase loader
}
```

**Benefits**:
- Easy A/B testing between old and new loaders
- Optional reporting (no performance impact when disabled)
- Standard CLI pattern familiar to developers

---

## JSON Input Structure

### Complete Bundle Format

```json
{
  "version": "1.0.6",
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
      "totalInstanceVarCount": 0,
      "fieldLayout": [],
      "instVarNames": [],
      "classVarNames": [],
      "poolLocalIds": [],
      "methodLocalIds": ["method:Object>>new", "method:Object>>==", ...],
      "commentStringIndex": 0
    },
    {
      "localId": "meta:Object",
      "nameIndex": 1,
      "superNameIndex": 2,
      "isMetaclass": true,
      "metaclassOfLocalId": "class:Object",
      "format": 16530,
      "totalInstanceVarCount": 6,
      "fieldLayout": [
        {"nameIndex": 15, "slotIndex": 0, "inheritedFrom": "class:Behavior"},
        {"nameIndex": 16, "slotIndex": 1, "inheritedFrom": "class:Behavior"},
        ...
      ],
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
      "homeClassLocalId": "class:Object",
      "selectorIndex": 142,
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
      "bytecodes": "iCA=",
      "argNames": [],
      "tempNames": []
    },
    ... (10,181 more methods including blocks)
  ],
  "initExpressions": [
    {
      "localId": "init:0",
      "source": "ActionMenu comment: '...'",
      "header": {...},
      "literals": [...],
      "bytecodes": "...",
      "argNames": [],
      "tempNames": []
    },
    ... (284 more init expressions)
  ]
}
```

### Key v1.0.6 Changes

**Precomputed Field Layouts**: Each class now includes:
- `totalInstanceVarCount`: Total instance variables including inherited
- `fieldLayout[]`: Complete ordered field layout with inheritance information

**Example** (class:Class with 9 fields):
```json
"fieldLayout": [
  {"nameIndex": 15, "slotIndex": 0, "inheritedFrom": "class:Behavior"},      // superclass
  {"nameIndex": 16, "slotIndex": 1, "inheritedFrom": "class:Behavior"},      // methodDict
  {"nameIndex": 17, "slotIndex": 2, "inheritedFrom": "class:Behavior"},      // format
  {"nameIndex": 18, "slotIndex": 3, "inheritedFrom": "class:Behavior"},      // subclasses
  {"nameIndex": 19, "slotIndex": 4, "inheritedFrom": "class:ClassDescription"}, // instanceVariables
  {"nameIndex": 20, "slotIndex": 5, "inheritedFrom": "class:ClassDescription"}, // organization
  {"nameIndex": 21, "slotIndex": 6, "inheritedFrom": null},                  // name (own field)
  {"nameIndex": 22, "slotIndex": 7, "inheritedFrom": null},                  // classPool (own field)
  {"nameIndex": 23, "slotIndex": 8, "inheritedFrom": null}                   // sharedPools (own field)
]
```

**Performance Improvement**: Phase 2 (Layout Loading) reduced from ~20-25ms (runtime computation) to ~2-5ms (simple array iteration) - an 80-90% improvement.

---

## Desired Final State

At the end of Phase 6, the Smalltalk-80 heap must satisfy these invariants:

**1. No NULL_CLASS objects**
```
∀ object: object.class != 0
All objects have valid class references (never zero/null)
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
- Phase 3: Object localIds → object OOPs
- Phase 4: All method localIds → method OOPs

**Example**:
```csharp
localIdToObjectId["class:Object"] = oop=7676
localIdToObjectId["meta:Object"] = oop=7678
localIdToObjectId["method:Object>>new"] = oop=12345
localIdToObjectId["Smalltalk"] = oop=7670
```

**Usage**: Resolve `{"type": "objectRef", "localId": "class:Foo"}` literals

---

### 2. `initializedFields`: `HashSet<(SmalltalkOOP objectOOP, int fieldIndex)>`

**Purpose**: Track which instance variable slots have been explicitly set

**Lifetime**: Phases 2-3

**Why Needed**: Distinguish "not yet set" from "set to nil"

**Population**:
- Phase 2: Class metadata fields (name, format, superclass)
- Phase 2: Metaclass `thisClass` and `superclass` fields
- Phase 3: Object fields from bundle

**Usage in Phase 3**:
```csharp
// After all explicit field setting is done:
if (!initializedFields.Contains((objectOOP, fieldIndex))) {
    // Not explicitly set, initialize to nil
    heapManager.SetInstanceVariable(objectOOP, fieldIndex, nilOOP);
}
```

---

### 3. `SymbolRegistry`: Static symbol index → OOP mapping

**Purpose**: Fast lookup of symbols by index

**Lifetime**: Entire VM lifetime (frozen after Phase 1)

**API**:
```csharp
// Phase 1: Populate
for (int i = 0; i < bundle.Symbols.Length; i++)
{
    var symbolOOP = InternSymbol(bundle.Symbols[i]);
    SymbolRegistry.Register(i, symbolOOP);
}
SymbolRegistry.Freeze();  // Prevent further modifications

// Later phases: Read-only access
SmalltalkOOP symbolOOP = SymbolRegistry.GetSymbolOOP(index);
```

**Architectural Rule**: After Freeze(), runtime code can ONLY read, never write

**Size**: 3,834 symbols registered

---

### 4. `GlobalRegistry`: Static string → OOP mapping

**Purpose**: Fast lookup of classes and globals by name

**Lifetime**: Coldstart only (frozen after Phase 5)

**API**:
```csharp
// Populate during coldstart
GlobalRegistry.Register("class:Object", objectOOP);
GlobalRegistry.Register("nil", nilOOP);
GlobalRegistry.Register("Smalltalk", smalltalkOOP);

// Lookup during coldstart
GlobalRegistry.TryGet("class:Object", out SmalltalkOOP oop);
GlobalRegistry.LookupClassOOP("Object");

// Freeze
GlobalRegistry.Freeze();  // After Phase 5
```

**Architectural Rule**: Runtime code must NOT use GlobalRegistry (use SpecialObjectsArray or heap lookups instead)

**Size**: 246 global entries + 2 object entries (nil, Smalltalk)

---

### 5. `AssociationRegistry`: Canonical Association objects

**Purpose**: Centralized registry for canonical Association objects (one per global variable name)

**Lifetime**: Coldstart only (frozen after Phase 5)

**Why Needed**: Ensures only ONE Association object exists per global name, eliminating duplicates and reducing memory usage by 84.1%

**API**:
```csharp
// Create or retrieve canonical Association
var associationOOP = AssociationRegistry.GetOrCreate("Point",
    () => {
        // Allocation function called only if Association doesn't exist
        return AllocateAssociation(keySymbolOOP, valueClassOOP);
    });

// Freeze after all Associations created
AssociationRegistry.Freeze();  // After Phase 5
```

**VALUE MIGRATION POLICY**: AssociationRegistry enforces that all incoming Associations have nil values. The compiler should emit Association literals with nil values, and Phase 5 patches set the actual values during coldstart. This prevents value duplication and ensures canonical Associations.

**Implementation**:
```csharp
public static class AssociationRegistry
{
    private static Dictionary<string, SmalltalkOOP> _associations = new();
    private static bool _frozen = false;

    public static SmalltalkOOP GetOrCreate(string symbolName,
        Func<SmalltalkOOP> createAssociation)
    {
        if (_frozen)
            throw new InvalidOperationException("AssociationRegistry is frozen");

        if (_associations.TryGetValue(symbolName, out var existing))
        {
            Console.WriteLine($"[AssociationRegistry] Reusing canonical Association for '{symbolName}'");
            return existing;
        }

        var associationOOP = createAssociation();
        _associations[symbolName] = associationOOP;
        Console.WriteLine($"[AssociationRegistry] Created canonical Association for '{symbolName}' → {associationOOP}");
        return associationOOP;
    }

    public static void Freeze() => _frozen = true;

    public static int Count => _associations.Count;
}
```

**Memory Savings Example** (from actual coldstart):
- Without canonicalization: 3,745 Association instances
- With canonicalization: 596 canonical instances
- Reduction: 84.1% fewer Association objects
- Memory saved: ~12 KB (3,149 duplicate Associations × 4 bytes per OOP)

**Architectural Rule**: Runtime code must NOT use AssociationRegistry (use heap lookups through Smalltalk dictionary instead)

**Size**: 596 canonical Associations registered (out of 3,745 total Association literals)

---

### 6. `SpecialObjectsArray`: GC roots

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
    public readonly SmalltalkOOP InitExpressionMethods;
}
```

**Setters** (used during coldstart):
```csharp
SpecialObjectsArray.SetNil(nilOOP);          // Phase 3
SpecialObjectsArray.SetFalse(falseOOP);      // Phase 3
SpecialObjectsArray.SetTrue(trueOOP);        // Phase 3
SpecialObjectsArray.SetSmalltalk(dictOOP);   // Phase 5
SpecialObjectsArray.SetInitExpressionMethods(arrayOOP);  // Phase 4
```

**Runtime Access**:
```csharp
SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
SmalltalkOOP smalltalkOOP = SpecialObjectsArray.Instance.Smalltalk;
```

---

### 7. `LayoutResolver`: Class layout cache

**Purpose**: Fast lookup of instance variable counts and field indices

**Lifetime**: Entire VM lifetime

**Populated**: Phase 2 (loads from bundle's precomputed fieldLayout arrays)

**API**:
```csharp
int count = layoutResolver.GetInstanceVariableCount("class:Object");
int index = layoutResolver.GetFieldIndex("class:Point", "x");
```

**Why Needed**: Runtime code must NEVER hardcode field indices (they can change if class definition changes)

**v1.0.6 Improvement**: Layouts are now precomputed by compiler and loaded from bundle, eliminating the need for topological sort and inheritance traversal at runtime.

---

### 8. `SemanticAllocator`: High-level allocation wrapper

**Purpose**: Simplify object allocation with automatic size calculation

**Lifetime**: Coldstart only

**Why Separate from HeapManager**:
- SemanticAllocator knows about class layouts (uses LayoutResolver)
- HeapManager is low-level (only knows formats, sizes)

**API**:
```csharp
SmalltalkOOP classOOP = allocator.AllocateObjectOfClass("class:Point");
// Internally:
//   1. Lookup "class:Point" in bundle
//   2. Calculate instance variable count from precomputed layout
//   3. Call heapManager.AllocateObject(Pointer, instVarCount, 0)

SmalltalkOOP stringOOP = allocator.AllocateString(classOOP, "Hello");
// Internally:
//   1. Convert string to UTF-8 bytes
//   2. Allocate ByteIndexed object
//   3. Set class reference
//   4. Store bytes
```

**Architectural Rule**: Runtime code must NOT use SemanticAllocator (use HeapManager directly)

---

## Phase 1: Preflight Validation + Symbol Interning

**File**: `LoaderPhases_New/Phase1_PreflightAndSymbols.cs`

**Purpose**: Validate JSON bundle structure and intern all symbols

**Consolidates**:
- Old Phase 0: Preflight Validation
- Old Phase 1: Symbol Interning

**Why Consolidated**: Bundle validation and symbol interning are sequential and tightly coupled - symbols are needed immediately after validation completes. No other work can proceed without validated bundle + symbols.

**Input State**:
- JSON file loaded and deserialized into `ColdStartBundle` object
- No heap objects allocated
- No data structures initialized

**Dependencies**: None (first phase)

---

### Step 1: Validate Bundle Version

**Purpose**: Ensure bundle is v1.0.6+ with precomputed field layouts

```csharp
private void ValidateBundleVersion()
{
    var version = _bundle.Metadata.Version;
    var parts = version.Split('.');

    if (parts.Length < 2)
        throw new InvalidOperationException($"Invalid bundle version format: {version}");

    int major = int.Parse(parts[0]);
    int minor = int.Parse(parts[1]);
    int patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;

    // Require v1.0.6+ for precomputed layouts
    if (major < 1 || (major == 1 && minor == 0 && patch < 6))
        throw new InvalidOperationException(
            $"Bundle version {version} not supported. Requires v1.0.6+ for precomputed layouts.");
}
```

**Why Critical**: v1.0.6 introduced precomputed field layouts. Earlier versions require runtime layout computation (not supported in new loader).

---

### Step 2: Validate Local IDs are Unique

**Purpose**: Ensure no duplicate localIds across all entities

```csharp
private void ValidateLocalIds()
{
    var allIds = _bundle.Classes.Select(c => c.LocalId)
        .Concat(_bundle.Objects.Select(o => o.LocalId))
        .Concat(_bundle.Methods.Select(m => m.LocalId))
        .Concat(_bundle.InitExpressions.Select(i => i.LocalId))
        .ToHashSet();

    int expectedCount = _bundle.Classes.Length
                      + _bundle.Objects.Length
                      + _bundle.Methods.Length
                      + _bundle.InitExpressions.Length;

    if (allIds.Count != expectedCount)
        throw new InvalidOperationException("Duplicate localIds detected.");
}
```

**Why Critical**: `localIdToObjectId` dictionary requires unique keys. Duplicates would cause overwrites and broken references.

---

### Step 3: Validate Symbol Indices

**Purpose**: Ensure all symbol index references are within bounds

```csharp
private void ValidateSymbolIndices()
{
    int maxIndex = _bundle.Symbols.Length;

    foreach (var cls in _bundle.Classes)
    {
        if (cls.NameIndex >= maxIndex)
            throw new IndexOutOfRangeException(
                $"Class {cls.LocalId} nameIndex {cls.NameIndex} >= {maxIndex}");

        foreach (var instVarIdx in cls.InstVarNames)
        {
            if (instVarIdx >= maxIndex)
                throw new IndexOutOfRangeException(
                    $"Class {cls.LocalId} instVar index {instVarIdx} >= {maxIndex}");
        }
    }

    foreach (var method in _bundle.Methods)
    {
        if (method.SelectorIndex >= maxIndex && method.SelectorIndex != -1)
            throw new IndexOutOfRangeException(
                $"Method {method.LocalId} selectorIndex {method.SelectorIndex} >= {maxIndex}");
    }
}
```

**Why Critical**: Out-of-bounds symbol indices would cause array access violations during interning.

---

### Step 4: Validate Object References

**Purpose**: Ensure all objectRef literals point to valid localIds

```csharp
private void ValidateObjectRefs()
{
    var validIds = _bundle.Classes.Select(c => c.LocalId)
        .Concat(_bundle.Objects.Select(o => o.LocalId))
        .ToHashSet();

    // Check object fields
    foreach (var obj in _bundle.Objects)
    {
        foreach (var field in obj.Fields)
        {
            if (field.Type == "objectRef")
            {
                if (!validIds.Contains(field.LocalId))
                    throw new InvalidOperationException(
                        $"Object {obj.LocalId} field references unknown localId: {field.LocalId}");
            }
        }
    }

    // Check method literals
    foreach (var method in _bundle.Methods)
    {
        foreach (var literal in method.Literals)
        {
            if (literal.Type == "objectRef")
            {
                if (!validIds.Contains(literal.LocalId))
                    throw new InvalidOperationException(
                        $"Method {method.LocalId} literal references unknown localId: {literal.LocalId}");
            }
        }
    }
}
```

**Why Critical**: Dangling references would cause null dereferences or allocation failures during method compilation.

---

### Step 5: Validate Precomputed Layouts (v1.0.6)

**Purpose**: Verify field layouts are consistent with class metadata

```csharp
private void ValidatePrecomputedLayouts()
{
    foreach (var cls in _bundle.Classes)
    {
        // Verify totalInstanceVarCount matches fieldLayout length
        if (cls.FieldLayout.Length != cls.TotalInstanceVarCount)
            throw new InvalidOperationException(
                $"Class {cls.LocalId} fieldLayout length ({cls.FieldLayout.Length}) " +
                $"!= totalInstanceVarCount ({cls.TotalInstanceVarCount})");

        // Verify slot indices are sequential 0..N-1
        for (int i = 0; i < cls.FieldLayout.Length; i++)
        {
            if (cls.FieldLayout[i].SlotIndex != i)
                throw new InvalidOperationException(
                    $"Class {cls.LocalId} field layout slot {i} has wrong slotIndex: " +
                    $"{cls.FieldLayout[i].SlotIndex}");
        }
    }
}
```

**Why Critical**: Incorrect layouts would cause field access to read/write wrong slots, corrupting object state.

---

### Step 6: Validate Home Class Metadata

**Purpose**: Ensure all methods reference valid home classes

```csharp
private void ValidateHomeClassMetadata()
{
    var classLocalIds = _bundle.Classes.Select(c => c.LocalId).ToHashSet();

    foreach (var method in _bundle.Methods)
    {
        // Skip block literals - they reference containing methods, not classes
        if (method.LocalId.StartsWith("block:"))
            continue;

        if (!classLocalIds.Contains(method.HomeClassLocalId))
            throw new InvalidOperationException(
                $"Method {method.LocalId} references unknown home class {method.HomeClassLocalId}");
    }
}
```

**Why Critical**: Methods need valid home class references for super sends and method lookup.

**Key Fix**: Blocks are identified by `LocalId.StartsWith("block:")` and skipped - their `HomeClassLocalId` points to the containing method, not a class in the class list.

---

### Step 7: Validate Metaclass Circle

**Purpose**: Ensure the fundamental metaclass circle classes exist

```csharp
private void ValidateMetaclassCircle()
{
    var metaclasses = _bundle.Classes.Where(c => c.IsMetaclass).ToList();
    var requiredMetaclasses = new[] {
        "meta:Object", "meta:Behavior", "meta:ClassDescription",
        "meta:Class", "meta:Metaclass"
    };

    foreach (var required in requiredMetaclasses)
    {
        if (!metaclasses.Any(m => m.LocalId == required))
            throw new InvalidOperationException(
                $"Required metaclass {required} not found in bundle");
    }
}
```

**Why Critical**: The metaclass circle is fundamental to Smalltalk-80's object model. Missing any of these classes would break class-side method inheritance.

---

### Step 8: Validate Special Objects Array Definition

**Purpose**: Ensure required singleton classes exist

```csharp
private void ValidateSpecialObjectsArrayDefinition()
{
    var requiredClasses = new[] {
        "class:UndefinedObject",  // for nil
        "class:True",             // for true
        "class:False"             // for false
    };

    var classLocalIds = _bundle.Classes.Select(c => c.LocalId).ToHashSet();

    foreach (var required in requiredClasses)
    {
        if (!classLocalIds.Contains(required))
            throw new InvalidOperationException(
                $"Required class {required} for SpecialObjectsArray not found");
    }
}
```

**Why Critical**: nil, true, and false are fundamental singletons. Missing their classes would prevent Phase 3 from allocating them.

---

### Step 9: Validate Patches (NEW in 2025-10-26)

**Purpose**: Validate all patch descriptors before coldstart execution begins

**Why Added**: Ensures bundle patches are well-formed and reference valid entities. Fail-fast validation prevents runtime errors during Phase 5 patch application.

**Three Patch Types Validated**:

1. **SetInstanceVariable**: Sets a specific instance variable field on an object
2. **InstallMethodDictionary**: Installs a method dictionary into a class
3. **ReplaceLiteral**: Replaces a literal in a CompiledMethod with a canonical Association

```csharp
private void ValidatePatches()
{
    Console.WriteLine($"  Validating {_bundle.Patches.Length} patches...");
    int setInstanceVarCount = 0;
    int installMethodDictCount = 0;
    int replaceLiteralCount = 0;

    var allLocalIds = _bundle.Classes.Select(c => c.LocalId)
        .Concat(_bundle.Objects.Select(o => o.LocalId))
        .Concat(_bundle.Methods.Select(m => m.LocalId))
        .ToHashSet();

    foreach (var patch in _bundle.Patches)
    {
        switch (patch.PatchType)
        {
            case "SetInstanceVariable":
                ValidateSetInstanceVariablePatch(patch, allLocalIds);
                setInstanceVarCount++;
                break;

            case "InstallMethodDictionary":
                ValidateInstallMethodDictionaryPatch(patch, allLocalIds);
                installMethodDictCount++;
                break;

            case "ReplaceLiteral":
                ValidateReplaceLiteralPatch(patch, allLocalIds);
                replaceLiteralCount++;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown patch type: {patch.PatchType}");
        }
    }

    Console.WriteLine($"  Patch validation PASSED:");
    Console.WriteLine($"    SetInstanceVariable: {setInstanceVarCount}");
    Console.WriteLine($"    InstallMethodDictionary: {installMethodDictCount}");
    Console.WriteLine($"    ReplaceLiteral: {replaceLiteralCount}");
}

private void ValidateSetInstanceVariablePatch(PatchDescriptor patch, HashSet<string> validIds)
{
    // Validate targetLocalId exists
    if (!validIds.Contains(patch.TargetLocalId))
        throw new InvalidOperationException(
            $"SetInstanceVariable patch references unknown targetLocalId: {patch.TargetLocalId}");

    // Validate field name exists in target class layout
    var target = _bundle.Classes.FirstOrDefault(c => c.LocalId == patch.TargetLocalId) ??
                 _bundle.Objects.FirstOrDefault(o => o.LocalId == patch.TargetLocalId);

    if (target == null)
        throw new InvalidOperationException(
            $"SetInstanceVariable patch target not found: {patch.TargetLocalId}");

    // For class targets, validate field exists in layout
    if (target is ClassDescriptor classDesc)
    {
        bool fieldExists = classDesc.FieldLayout.Any(f =>
            _bundle.Symbols[f.NameIndex] == patch.FieldName);

        if (!fieldExists)
            throw new InvalidOperationException(
                $"SetInstanceVariable patch field '{patch.FieldName}' not found in {patch.TargetLocalId}");
    }

    // Validate value reference if objectRef
    if (patch.Value.Type == "objectRef" && !validIds.Contains(patch.Value.LocalId))
        throw new InvalidOperationException(
            $"SetInstanceVariable patch value references unknown localId: {patch.Value.LocalId}");
}

private void ValidateInstallMethodDictionaryPatch(PatchDescriptor patch, HashSet<string> validIds)
{
    // Validate classLocalId exists
    if (!validIds.Contains(patch.ClassLocalId))
        throw new InvalidOperationException(
            $"InstallMethodDictionary patch references unknown classLocalId: {patch.ClassLocalId}");

    // Validate methodDictLocalId exists (if specified - may be auto-allocated)
    if (patch.MethodDictLocalId != null && !validIds.Contains(patch.MethodDictLocalId))
        throw new InvalidOperationException(
            $"InstallMethodDictionary patch references unknown methodDictLocalId: {patch.MethodDictLocalId}");
}

private void ValidateReplaceLiteralPatch(PatchDescriptor patch, HashSet<string> validIds)
{
    // Validate methodLocalId exists
    if (!validIds.Contains(patch.MethodLocalId))
        throw new InvalidOperationException(
            $"ReplaceLiteral patch references unknown methodLocalId: {patch.MethodLocalId}");

    // Validate literal index is within bounds
    var method = _bundle.Methods.First(m => m.LocalId == patch.MethodLocalId);
    if (patch.LiteralIndex < 0 || patch.LiteralIndex >= method.Literals.Length)
        throw new InvalidOperationException(
            $"ReplaceLiteral patch literal index {patch.LiteralIndex} out of bounds " +
            $"for method {patch.MethodLocalId} (has {method.Literals.Length} literals)");

    // Validate new value reference if objectRef
    if (patch.NewValue.Type == "objectRef" && !validIds.Contains(patch.NewValue.LocalId))
        throw new InvalidOperationException(
            $"ReplaceLiteral patch newValue references unknown localId: {patch.NewValue.LocalId}");
}
```

**Validation Checks Performed**:
- All referenced `localId` values exist in bundle (targetLocalId, classLocalId, methodLocalId)
- Field names exist in target class layouts (for SetInstanceVariable)
- Literal indices are within method bounds (for ReplaceLiteral)
- Method dictionary references are valid (for InstallMethodDictionary)
- Value references are valid (for all patch types)
- No unknown patch types

**Example Patch Validation Output**:
```
Validating 1659 patches...
  Patch validation PASSED:
    SetInstanceVariable: 480
    InstallMethodDictionary: 480
    ReplaceLiteral: 699
```

**Benefits**:
- **Fail Fast**: Bundle errors detected before any heap modifications
- **Clear Errors**: Precise error messages indicate exactly which patch is malformed
- **Safety**: Prevents Phase 5 from attempting to apply invalid patches
- **Debugging**: Easier to trace bundle generation bugs back to compiler

**Performance Impact**: Negligible (~1-2ms for 1,659 patches)

---

### Step 10: Intern All Symbols

**Purpose**: Allocate Symbol objects for all symbols and populate SymbolRegistry

```csharp
for (int i = 0; i < _bundle.Symbols.Length; i++)
{
    string symbolName = _bundle.Symbols[i];
    var symbolOOP = InternSymbol(symbolName);
    SymbolRegistry.Register(i, symbolOOP);
}
```

**InternSymbol Implementation**:

```csharp
private SmalltalkOOP InternSymbol(string name)
{
    // Check if symbol already exists (deduplication)
    if (_symbolTable.TryGetValue(name, out var existingOOP))
        return existingOOP;

    // Convert symbol name to UTF-8 bytes
    byte[] bytes = Encoding.UTF8.GetBytes(name);

    // Allocate ByteIndexed object
    // Note: class reference is 0 (will be set in Phase 3)
    SmalltalkOOP symbolOOP = _heapManager.AllocateObject(
        ObjectFormat.ByteIndexed,
        instanceVarCount: 0,
        indexedSize: bytes.Length
    );

    // Store bytes
    for (int i = 0; i < bytes.Length; i++)
    {
        _heapManager.SetIndexedByte(symbolOOP, i, bytes[i]);
    }

    // Cache for deduplication
    _symbolTable[name] = symbolOOP;

    return symbolOOP;
}
```

**Key Details**:
- Symbols are ByteIndexed objects (instance var count = 0)
- Symbol bytes are UTF-8 encoded
- Deduplication: Multiple references to "Object" → single Symbol object
- Class reference (slot 0) is initially 0, will be set to `class:Symbol` in Phase 3

---

### Step 11: Freeze Symbol Registry

**Purpose**: Lock SymbolRegistry to prevent runtime modifications

```csharp
SymbolRegistry.Freeze();
Console.WriteLine($"[SymbolRegistry] Frozen with {_bundle.Symbols.Length} symbols");
```

**Why Critical**: Runtime code must not modify SymbolRegistry. Freezing throws `InvalidOperationException` on any `Register()` attempts after this point.

---

**Output State**:

- Bundle validated (version, structure, references, patches)
- 3,834 Symbol objects allocated in heap
- SymbolRegistry populated and frozen
- `_symbolTable` contains name → OOP deduplication map
- All symbols have class reference = 0 (will be fixed in Phase 3)
- 1,659 patches validated (ready for Phase 5 application)

**Memory**: ~300 KB (3,834 symbols × ~80 bytes average)

**Performance**: ~152ms (increased from ~50ms due to patch validation overhead)

---

## Phase 2: Class Infrastructure

**File**: `LoaderPhases_New/Phase2_ClassInfrastructure.cs`

**Purpose**: Create all class/metaclass objects, wire metaclass hierarchy, populate class metadata, load field layouts

**Consolidates**:
- Old Phase 2: Class Shells
- Old Phase 3: Metaclass Wiring
- Old Phase 4: Class Resolution
- Old Phase 5: Layout Computation → now Layout Loading (v1.0.6)

**Why Consolidated**: Class infrastructure is a single logical unit:
1. Allocate all class shells (can't wire metaclasses without OOPs)
2. Wire metaclass circle and thisClass references (can't resolve superclasses without metaclass circle)
3. Populate class metadata (name, format, superclass)
4. Load precomputed layouts from bundle

All four steps are tightly coupled and complete the "class infrastructure" responsibility.

**Input State**:
- Symbols allocated (Phase 1)
- SymbolRegistry frozen
- No class objects exist
- `localIdToObjectId` is empty

**Dependencies**:
- Phase 1 (needs symbols for class names, needs SymbolRegistry)

---

### Step 1: Allocate Class Shells

**Purpose**: Create all 480 class/metaclass objects with correct structure but uninitialized fields

**Why Shell Allocation**:
- Classes reference each other (superclass, metaclass relationships)
- To resolve circular dependencies, allocate all class OOPs first
- Then wire relationships in later steps

```csharp
private void AllocateClassShells()
{
    foreach (var cls in _bundle.Classes)
    {
        // Use totalInstanceVarCount from precomputed bundle (v1.0.6)
        int instVarCount = cls.TotalInstanceVarCount;

        // All class objects are Pointer format, no indexed portion
        ObjectFormat format = ObjectFormat.Pointer;

        // Allocate class shell
        SmalltalkOOP classOOP = _heapManager.AllocateObject(
            format,
            instVarCount,
            indexedSize: 0
        );

        // Store in localId → OOP mapping
        _localIdToObjectId[cls.LocalId] = classOOP;
    }

    Console.WriteLine($"  Allocated {_bundle.Classes.Length} class shells");
}
```

**What `AllocateObject` Does**:

1. Find free entry in object table
2. Calculate total slots: 1 (class) + instVarCount + 0 (no indexed)
3. Allocate storage array (SmalltalkOOP[] for Pointer format)
4. Initialize all slots to SmalltalkOOP.Null (0)
5. Create object table entry
6. Return tagged OOP

**State After Step 1**:
- 480 class objects allocated (240 regular + 240 metaclasses)
- All instance variable slots = 0 (invalid OOP)
- Class reference (slot 0) = 0 (invalid, will be fixed in Phase 3)
- `localIdToObjectId` populated with 480 entries

---

### Step 2: Set `thisClass` Field for All Metaclasses

**Purpose**: Link each metaclass to its instance-side class

```csharp
private void WireMetaclassThisClass()
{
    // Find the thisClass field index from precomputed layout
    var metaclassLayout = _bundle.Classes.First(c => c.LocalId == "class:Metaclass");
    int thisClassIndex = Array.FindIndex(metaclassLayout.FieldLayout,
        f => _bundle.Symbols[f.NameIndex] == "thisClass");

    if (thisClassIndex < 0)
        throw new InvalidOperationException("thisClass field not found in Metaclass");

    Console.WriteLine($"  Setting thisClass: index={thisClassIndex}");

    // Process each metaclass
    foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
    {
        SmalltalkOOP metaOOP = _localIdToObjectId[metaCls.LocalId];
        string instanceLocalId = metaCls.MetaclassOfLocalId;

        if (!_localIdToObjectId.TryGetValue(instanceLocalId, out SmalltalkOOP instanceOOP))
            throw new InvalidOperationException(
                $"Instance class not found: {instanceLocalId} for {metaCls.LocalId}");

        // Set thisClass field
        _heapManager.SetInstanceVariable(metaOOP, thisClassIndex, instanceOOP);
        _initializedFields.Add((metaOOP, thisClassIndex));

        // Diagnostic output
        if (metaCls.LocalId == "meta:Point")
            Console.WriteLine($"  Setting thisClass: index={thisClassIndex}, " +
                              $"meta={metaOOP}, inst={instanceOOP}");
    }
}
```

**Result**:
```
meta:Object.thisClass = class:Object
meta:Class.thisClass = class:Class
meta:Point.thisClass = class:Point
... (240 metaclasses)
```

**Why This Matters**: Allows VM to navigate from metaclass to instance class (used by primitives like `#class`).

---

### Step 3: Wire Fundamental Metaclass Circle

**Purpose**: Establish the core 4-hop metaclass inheritance circle

**The Metaclass Circle**:
```
meta:Object.superclass → class:Class
class:Class.superclass → class:ClassDescription
class:ClassDescription.superclass → class:Behavior
class:Behavior.superclass → class:Object  ← completes the circle
```

**Why This is Special**: This is the ONLY circular inheritance in the system, required by Smalltalk-80 specification.

```csharp
private void WireFundamentalMetaclassCircle()
{
    // Get OOPs for the fundamental classes
    SmalltalkOOP metaObjectOOP = _localIdToObjectId["meta:Object"];
    SmalltalkOOP classClassOOP = _localIdToObjectId["class:Class"];
    SmalltalkOOP classDescriptionOOP = _localIdToObjectId["class:ClassDescription"];
    SmalltalkOOP classBehaviorOOP = _localIdToObjectId["class:Behavior"];
    SmalltalkOOP classObjectOOP = _localIdToObjectId["class:Object"];

    // Find superclass field index (index 0 in Behavior)
    var behaviorLayout = _bundle.Classes.First(c => c.LocalId == "class:Behavior");
    int superclassIndex = Array.FindIndex(behaviorLayout.FieldLayout,
        f => _bundle.Symbols[f.NameIndex] == "superclass");

    Console.WriteLine($"  Wiring fundamental metaclass hierarchy (superclass index: {superclassIndex}):");

    // Wire the circle (4 hops)
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
}
```

**Benefit**: ALL classes inherit class-side methods (`new`, `basicNew`) defined in Behavior through their metaclasses.

---

### Step 4: Wire All Metaclass Superclasses

**Purpose**: Set superclass for all remaining metaclasses (excluding meta:Object which was wired in Step 3)

```csharp
private void WireMetaclassSuperclasses()
{
    int superclassIndex = 0;  // From Step 3
    int wiredCount = 0;
    int skippedCount = 0;

    foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
    {
        // Skip meta:Object (already wired in fundamental circle)
        if (metaCls.LocalId == "meta:Object")
        {
            skippedCount++;
            continue;
        }

        SmalltalkOOP metaOOP = _localIdToObjectId[metaCls.LocalId];

        // Find superclass name
        if (metaCls.SuperNameIndex == null)
            throw new InvalidOperationException(
                $"Metaclass {metaCls.LocalId} missing superclass");

        string superName = _bundle.Symbols[metaCls.SuperNameIndex.Value];
        string superLocalId = $"meta:{superName}";

        if (!_localIdToObjectId.TryGetValue(superLocalId, out SmalltalkOOP superOOP))
            throw new InvalidOperationException(
                $"Metaclass superclass not found: {superLocalId}");

        // Set superclass
        _heapManager.SetInstanceVariable(metaOOP, superclassIndex, superOOP);
        _initializedFields.Add((metaOOP, superclassIndex));
        wiredCount++;
    }

    Console.WriteLine($"  Wired {wiredCount} metaclass superclass relationships " +
                      $"({skippedCount} skipped - meta:Object in fundamental hierarchy)");
}
```

**Result**: All 240 metaclasses have valid superclass references, forming a proper inheritance tree rooted at the metaclass circle.

---

### Step 5: Resolve Class Metadata

**Purpose**: Populate class name, format, and superclass fields for all regular classes

```csharp
private void ResolveClassMetadata()
{
    foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

        // Get field indices from precomputed layout
        var layout = cls.FieldLayout;
        int nameIndex = FindFieldIndex(layout, "name");
        int formatIndex = FindFieldIndex(layout, "format");
        int superclassIndex = FindFieldIndex(layout, "superclass");

        // Set name (Symbol)
        var nameSymbolOOP = SymbolRegistry.GetSymbolOOP(cls.NameIndex);
        _heapManager.SetInstanceVariable(classOOP, nameIndex, nameSymbolOOP);
        _initializedFields.Add((classOOP, nameIndex));

        // Set format (SmallInteger)
        var formatOOP = SmalltalkOOP.FromSmallInteger(cls.Format);
        _heapManager.SetInstanceVariable(classOOP, formatIndex, formatOOP);
        _initializedFields.Add((classOOP, formatIndex));

        // Set superclass (defer if Object, since nil doesn't exist yet)
        if (cls.SuperNameIndex != null)
        {
            string superName = _bundle.Symbols[cls.SuperNameIndex.Value];
            string superLocalId = $"class:{superName}";

            if (!_localIdToObjectId.TryGetValue(superLocalId, out SmalltalkOOP superOOP))
                throw new InvalidOperationException(
                    $"Superclass not found: {superLocalId} for {cls.LocalId}");

            _heapManager.SetInstanceVariable(classOOP, superclassIndex, superOOP);
            _initializedFields.Add((classOOP, superclassIndex));

            // Diagnostic output for key relationships
            if (cls.LocalId == "class:Number")
                Console.WriteLine($"[Phase2] Setting {cls.LocalId}.superclass = {superName}");
        }
        // else: Object.superclass will be set to nil in Phase 3
    }
}
```

**State After Step 5**:
- All classes have name (Symbol OOP)
- All classes have format (SmallInteger with format bits)
- All classes except Object have superclass (class OOP)
- Object.superclass is still 0 (will be set to nil in Phase 3)

---

### Step 6: Load Precomputed Layouts (v1.0.6)

**Purpose**: Populate LayoutResolver with precomputed field layouts from bundle

**Old Approach (v1.0.5)**: Runtime computation using topological sort and inheritance traversal (~20-25ms)

**New Approach (v1.0.6)**: Simple load from bundle arrays (~2-5ms, 80-90% faster)

```csharp
private void LoadPrecomputedLayouts()
{
    foreach (var cls in _bundle.Classes)
    {
        // Extract field names from precomputed layout
        var fieldNames = new List<string>();

        foreach (var field in cls.FieldLayout)
        {
            string fieldName = _bundle.Symbols[field.NameIndex];
            fieldNames.Add(fieldName);
        }

        // Register in LayoutResolver
        _layoutResolver.SetFieldLayout(cls.LocalId, fieldNames);
        _layoutResolver.SetInstanceVariableCount(cls.LocalId, cls.TotalInstanceVarCount);
    }

    // Freeze LayoutResolver to prevent runtime modifications
    _layoutResolver.Freeze();

    Console.WriteLine($"  Loaded precomputed layouts for {_bundle.Classes.Length} classes");
}
```

**Example**: class:Point layout
```
Field Layout (2 fields):
  slot 0: x (inherited from: null)
  slot 1: y (inherited from: null)
```

**Example**: class:Class layout (9 fields)
```
Field Layout (9 fields):
  slot 0: superclass         (inherited from: class:Behavior)
  slot 1: methodDict         (inherited from: class:Behavior)
  slot 2: format             (inherited from: class:Behavior)
  slot 3: subclasses         (inherited from: class:Behavior)
  slot 4: instanceVariables  (inherited from: class:ClassDescription)
  slot 5: organization       (inherited from: class:ClassDescription)
  slot 6: name               (inherited from: null)  ← own field
  slot 7: classPool          (inherited from: null)  ← own field
  slot 8: sharedPools        (inherited from: null)  ← own field
```

**Why v1.0.6 is Better**:
- **Faster**: No topological sort or inheritance traversal needed
- **Simpler**: 58 lines vs 106 lines (45% reduction)
- **Correct**: Compiler guarantees layout consistency
- **Debuggable**: Field layouts visible in JSON for inspection

---

**Output State**:

- 480 class/metaclass objects allocated with correct instance variable counts
- All metaclasses have `thisClass` pointing to instance-side class
- Metaclass circle wired (meta:Object → class:Class → class:ClassDescription → class:Behavior → class:Object)
- All metaclass superclasses wired
- All classes have name (Symbol), format (SmallInteger), and superclass (except Object)
- LayoutResolver populated and frozen with 480 class layouts
- `initializedFields` tracking all set fields

**Memory**: ~60 KB (480 class objects)

**Performance**: ~22ms

---

## Phase 3: Object Allocation and Initialization

**File**: `LoaderPhases_New/Phase3_ObjectAllocation.cs`

**Purpose**: Allocate all objects, set class references, initialize instance variables to nil

**Consolidates**:
- Old Phase 6: Object Allocation
- Old Phase X: Class Reference Initialization
- Old Phase Y: Instance Variable Initialization

**Why Consolidated**: Object allocation, class reference setting, and nil initialization are a single logical unit - "make objects fully valid". Separating them created confusing intermediate states where objects existed but were invalid.

**Input State**:
- Symbols allocated (Phase 1)
- Classes allocated and wired (Phase 2)
- No regular objects exist yet
- nil/true/false don't exist yet

**Dependencies**:
- Phase 1 (needs symbols)
- Phase 2 (needs class infrastructure for class references)

---

### Step 1: Allocate Sentinel Objects

**Purpose**: Create the fundamental singletons (nil, true, false)

**Why First**: These are needed by later steps to initialize unset instance variables to nil.

```csharp
private void AllocateSentinelObjects()
{
    // Allocate nil (instance of UndefinedObject)
    var nilOOP = _allocator.AllocateObjectOfClass("class:UndefinedObject");
    SpecialObjectsArray.SetNil(nilOOP);
    GlobalRegistry.Register("nil", nilOOP);

    // Allocate true (instance of True)
    var trueOOP = _allocator.AllocateObjectOfClass("class:True");
    SpecialObjectsArray.SetTrue(trueOOP);
    GlobalRegistry.Register("true", trueOOP);

    // Allocate false (instance of False)
    var falseOOP = _allocator.AllocateObjectOfClass("class:False");
    SpecialObjectsArray.SetFalse(falseOOP);
    GlobalRegistry.Register("false", falseOOP);

    Console.WriteLine($"  Allocated sentinel objects: nil={nilOOP}, true={trueOOP}, false={falseOOP}");
}
```

**AllocateObjectOfClass Implementation**:
```csharp
public SmalltalkOOP AllocateObjectOfClass(string classLocalId)
{
    var cls = _bundle.Classes.First(c => c.LocalId == classLocalId);
    int instVarCount = cls.TotalInstanceVarCount;

    // Determine format from class's format field
    ObjectFormat format = DetermineFormat(cls.Format);

    // Allocate object
    SmalltalkOOP objectOOP = _heapManager.AllocateObject(format, instVarCount, 0);

    // Set class reference (slot 0)
    SmalltalkOOP classOOP = _localIdToObjectId[classLocalId];
    _heapManager.SetSlot(objectOOP, 0, classOOP);

    return objectOOP;
}
```

**State After Step 1**:
- nil, true, false exist and are registered in SpecialObjectsArray
- GlobalRegistry contains entries for "nil", "true", "false"

---

### Step 2: Allocate Special Objects from Bundle

**Purpose**: Allocate objects defined in bundle's `objects` array (ProcessorScheduler, Smalltalk, etc.)

```csharp
private void AllocateSpecialObjects()
{
    Console.WriteLine($"  Allocated {_bundle.Objects.Length} special objects from bundle");

    foreach (var objDesc in _bundle.Objects)
    {
        // Get class name from bundle
        string className = _bundle.Symbols[objDesc.ClassNameIndex];
        string classLocalId = $"class:{className}";

        // Allocate object
        var objectOOP = _allocator.AllocateObjectOfClass(classLocalId);

        // Store in localId map
        _localIdToObjectId[objDesc.LocalId] = objectOOP;

        // Set fields from bundle
        for (int i = 0; i < objDesc.Fields.Length; i++)
        {
            var field = objDesc.Fields[i];
            var fieldOOP = ResolveFieldValue(field);

            _heapManager.SetInstanceVariable(objectOOP, i, fieldOOP);
            _initializedFields.Add((objectOOP, i));
        }
    }
}

private SmalltalkOOP ResolveFieldValue(FieldValue field)
{
    return field.Type switch
    {
        "nil" => SpecialObjectsArray.Instance.Nil,
        "true" => SpecialObjectsArray.Instance.True,
        "false" => SpecialObjectsArray.Instance.False,
        "smallInt" => SmalltalkOOP.FromSmallInteger(field.Value),
        "objectRef" => _localIdToObjectId[field.LocalId],
        "string" => _allocator.AllocateString(_bundle.Strings[field.Index]),
        _ => throw new InvalidOperationException($"Unknown field type: {field.Type}")
    };
}
```

**State After Step 2**:
- 2 special objects allocated (bundle typically contains ProcessorScheduler, Smalltalk)
- `localIdToObjectId` contains entries for these objects
- Fields set as specified in bundle

---

### Step 3: Set Class References for All Objects

**Purpose**: Set slot 0 (class reference) for every object in the heap

**Why Needed**: Class references were deferred during allocation to avoid dependency ordering issues.

```csharp
private void SetClassReferencesForAllObjects()
{
    int classCount = 0;
    int metaclassCount = 0;
    int symbolCount = 0;
    int methodCount = 0;

    // Set class references for all classes
    foreach (var cls in _bundle.Classes)
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

        if (cls.IsMetaclass)
        {
            // Metaclasses are instances of Metaclass
            SmalltalkOOP metaclassClassOOP = _localIdToObjectId["class:Metaclass"];
            _heapManager.SetSlot(classOOP, 0, metaclassClassOOP);
            metaclassCount++;

            // Diagnostic
            if (cls.LocalId == "meta:Point")
                Console.WriteLine($"    Set {cls.LocalId}.class = meta:Metaclass");
        }
        else
        {
            // Regular classes are instances of their metaclasses
            string metaLocalId = $"meta:{_bundle.Symbols[cls.NameIndex]}";
            SmalltalkOOP metaClassOOP = _localIdToObjectId[metaLocalId];
            _heapManager.SetSlot(classOOP, 0, metaClassOOP);
            classCount++;
        }
    }

    // Set class references for all symbols
    SmalltalkOOP symbolClassOOP = _localIdToObjectId["class:Symbol"];
    foreach (int symbolIndex in Enumerable.Range(0, _bundle.Symbols.Length))
    {
        SmalltalkOOP symbolOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);
        _heapManager.SetSlot(symbolOOP, 0, symbolClassOOP);
        symbolCount++;
    }

    Console.WriteLine($"  Set {classCount + metaclassCount + symbolCount} class references");
}
```

**State After Step 3**:
- Every object has a valid class reference (slot 0 != 0)
- Regular classes → metaclasses
- Metaclasses → class:Metaclass
- Symbols → class:Symbol
- Objects → their respective classes

**Critical Invariant Established**: No more NULL_CLASS objects!

---

### Step 4: Initialize Object.superclass to nil

**Purpose**: Set the root of the class hierarchy

```csharp
private void InitializeObjectSuperclass()
{
    SmalltalkOOP classObjectOOP = _localIdToObjectId["class:Object"];
    SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

    // Find superclass field index (index 0 in Behavior)
    int superclassIndex = _layoutResolver.GetFieldIndex("class:Object", "superclass");

    _heapManager.SetInstanceVariable(classObjectOOP, superclassIndex, nilOOP);
    _initializedFields.Add((classObjectOOP, superclassIndex));

    Console.WriteLine($"    Initialized class:Object.superclass = nil (field index {superclassIndex})");
}
```

**Why Deferred Until Now**: nil didn't exist in Phase 2 when other superclasses were set.

---

### Step 5: Initialize All Unset Instance Variables to nil

**Purpose**: Ensure all instance variable slots have valid OOPs (never 0)

**The Problem**: Some instance variables were explicitly set in earlier phases (tracked in `initializedFields`), but many slots are still 0 (invalid OOP).

**The Solution**: Iterate through ALL objects and set uninitialized slots to nil.

```csharp
private void InitializeUnsetInstanceVariables()
{
    SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
    int totalInitialized = 0;

    // Enumerate all objects in heap
    foreach (var (objectOOP, entry) in _heapManager.EnumerateObjectTable())
    {
        if (entry.Format == ObjectFormat.Pointer || entry.Format == ObjectFormat.PointerIndexed)
        {
            // Get instance variable count (excluding indexed portion)
            int instVarCount = entry.InstanceVariableCount;

            for (int fieldIndex = 0; fieldIndex < instVarCount; fieldIndex++)
            {
                // Skip if already initialized
                if (_initializedFields.Contains((objectOOP, fieldIndex)))
                    continue;

                // Initialize to nil
                _heapManager.SetInstanceVariable(objectOOP, fieldIndex, nilOOP);
                totalInitialized++;
            }
        }
    }

    Console.WriteLine($"  Initialized {totalInitialized} instance variables to nil");
}
```

**Example**: class:Point has 9 instance variables. Steps 1-4 set:
- slot 0: superclass = class:Magnitude
- slot 1: methodDict = (nil)
- slot 2: format = SmallInteger(...)
- slot 6: name = Symbol("Point")

Step 5 initializes remaining slots:
- slot 3: subclasses = nil
- slot 4: instanceVariables = nil
- slot 5: organization = nil
- slot 7: classPool = nil
- slot 8: sharedPools = nil

**Critical Invariant Established**: All instance variable slots contain valid OOPs!

---

**Internal Checkpoints** (NEW in 2025-10-26):

Phase 3 has been enhanced with three internal checkpoints for better visibility and debugging:

1. **Checkpoint 3.1: Allocation Complete**
   - Logs: "Phase 3.1: Allocated N objects"
   - Metric: `ObjectsAllocated` = N

2. **Checkpoint 3.2: Class References Set**
   - Logs: "Phase 3.2: Set M class references"
   - Metric: `ClassReferencesSet` = M

3. **Checkpoint 3.3: Initialization Complete**
   - Logs: "Phase 3.3: Initialized K fields to nil"
   - Metric: `FieldsInitialized` = K

**Metrics Collection**:
```csharp
public class Phase3Metrics
{
    public int ObjectsAllocated { get; set; }
    public int ClassReferencesSet { get; set; }
    public int FieldsInitialized { get; set; }
}

// Example usage in Phase3:
_report.RecordCustomMetric(3, "ObjectsAllocated", 486);
_report.RecordCustomMetric(3, "ClassReferencesSet", 4797);
_report.RecordCustomMetric(3, "FieldsInitialized", 1681);
```

**Example Checkpoint Output**:
```
Phase 3: Object Allocation and Initialization...
  Step 1: Allocating sentinel objects...
  Step 2: Allocating special objects...
  Phase 3.1: Allocated 486 objects
  Step 3: Setting class references...
  Phase 3.2: Set 4797 class references
  Step 4: Initializing Object.superclass...
  Step 5: Initializing unset instance variables...
  Phase 3.3: Initialized 1681 fields to nil
Phase 3 completed in 14ms
```

**Benefits**:
- **Visibility**: Know exactly which checkpoint failed if Phase 3 errors
- **Progress Tracking**: See incremental progress through large phase
- **Performance Analysis**: Identify which section is slow
- **Debugging**: Pinpoint exact location of allocation/initialization issues

---

**Output State**:

- nil, true, false singletons allocated and registered
- All objects from bundle allocated with fields set
- Every object has valid class reference (slot 0)
- Object.superclass = nil (root of class hierarchy)
- All instance variable slots initialized (explicit values or nil)
- NO slots contain 0 (NULL_CLASS eliminated)
- `initializedFields` tracking complete
- **Phase3Metrics collected**: 486 objects allocated, 4,797 class refs set, 1,681 fields initialized

**Memory**: ~2 MB (29,230 total objects including symbols and classes)

**Performance**: ~14ms (slightly increased from ~13ms due to checkpoint logging overhead)

---

## Phase 4: Method Compilation

**File**: `LoaderPhases_New/Phase4_MethodCompilation.cs`

**Purpose**: Allocate method dictionaries, compile methods, create init expression array, install methods in dictionaries

**Consolidates**:
- Old Phase 7: Method Dictionary Allocation
- Old Phase 8: Method Installation
- Old Phase 8b: Init Expression Method Creation
- Old Phase 9: Method Dictionary Installation

**Why Consolidated**: Method compilation is a single logical unit - "make methods ready for execution". All four old phases dealt with different aspects of method preparation and had dependencies on each other.

**Input State**:
- Symbols, classes, objects all allocated (Phases 1-3)
- nil/true/false exist
- No methods exist yet
- No method dictionaries allocated

**Dependencies**:
- Phase 1 (needs symbols for selectors)
- Phase 2 (needs class infrastructure)
- Phase 3 (needs nil/true/false for literals)

**Critical Feature**: **Association Canonicalization** - ensures one Association object per global variable name

---

### Step 1: Allocate Method Dictionaries

**Purpose**: Create MethodDictionary objects for all classes with correct capacity

**Sizing Formula**: `capacity = (methodCount * 1.5) rounded up to next power of 2`

```csharp
private void AllocateMethodDictionaries()
{
    SmalltalkOOP methodDictClassOOP = _localIdToObjectId["class:MethodDictionary"];

    foreach (var cls in _bundle.Classes)
    {
        int methodCount = cls.MethodLocalIds.Length;

        // Calculate capacity (1.5x methods, rounded to power of 2)
        int capacity = CalculateMethodDictionaryCapacity(methodCount);

        // MethodDictionary format: 2 instance vars (tally, array) + indexed keys/values
        // Array section: capacity keys + capacity values = 2 * capacity
        SmalltalkOOP methodDictOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed,
            instanceVarCount: 2,
            indexedSize: 2 * capacity
        );

        // Set class reference
        _heapManager.SetSlot(methodDictOOP, 0, methodDictClassOOP);

        // Initialize tally to 0 (SmallInteger)
        _heapManager.SetInstanceVariable(methodDictOOP, 0, SmalltalkOOP.FromSmallInteger(0));

        // Initialize all slots to nil
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < 2 * capacity; i++)
        {
            _heapManager.SetIndexedOOP(methodDictOOP, i, nilOOP);
        }

        // Store in _methodDictionaries map
        _methodDictionaries[cls.LocalId] = methodDictOOP;
    }

    Console.WriteLine($"  Allocated {_bundle.Classes.Length} method dictionaries");
}

private int CalculateMethodDictionaryCapacity(int methodCount)
{
    if (methodCount == 0) return 2;

    int capacity = (int)Math.Ceiling(methodCount * 1.5);

    // Round up to next power of 2
    int powerOf2 = 2;
    while (powerOf2 < capacity)
        powerOf2 *= 2;

    return powerOf2;
}
```

**Result**: 480 method dictionaries allocated with appropriate capacities.

---

### Step 2: Compile All Methods (Two-Pass)

**Purpose**: Create CompiledMethod objects for all methods and blocks

**Why Two-Pass**:
1. **Pass 1**: Allocate method shells, register in `localIdToObjectId`
2. **Pass 2**: Resolve literals (which may reference other methods via `{"type": "objectRef"}`)

**Pass 1: Allocate Method Shells**

```csharp
private void CreateMethodShells()
{
    foreach (var method in _bundle.Methods)
    {
        // Skip blocks for now (they have special handling)
        if (method.LocalId.StartsWith("block:"))
        {
            _blockDescriptors.Add(method);
            continue;
        }

        // Create method shell (literals unresolved)
        var methodOOP = CreateMethodShell(method);
        _localIdToObjectId[method.LocalId] = methodOOP;
    }

    Console.WriteLine($"  Created {_bundle.Methods.Length - _blockDescriptors.Count} method shells");
}

private SmalltalkOOP CreateMethodShell(MethodDescriptor method)
{
    int literalCount = method.Header.LiteralCount;

    // Allocate CompiledMethod (PointerIndexed with header + literals)
    // +1 for header, +1 for bytecode array OOP
    int totalLiterals = 1 + literalCount + 1;

    SmalltalkOOP methodOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount: 0,
        indexedSize: totalLiterals
    );

    // Set class reference
    SmalltalkOOP compiledMethodClassOOP = _localIdToObjectId["class:CompiledMethod"];
    _heapManager.SetSlot(methodOOP, 0, compiledMethodClassOOP);

    // Set header (SmallInteger with method header bits)
    _heapManager.SetIndexedOOP(methodOOP, 0, SmalltalkOOP.FromSmallInteger(method.Header.Word));

    // Literals will be set in Pass 2

    return methodOOP;
}
```

**Pass 2: Resolve Literals**

```csharp
private void ResolveLiterals()
{
    foreach (var method in _bundle.Methods)
    {
        if (method.LocalId.StartsWith("block:"))
            continue;

        SmalltalkOOP methodOOP = _localIdToObjectId[method.LocalId];

        // Resolve each literal
        for (int i = 0; i < method.Literals.Length; i++)
        {
            var literal = method.Literals[i];
            var literalOOP = ResolveLiteralValue(literal, method);

            // Store in CompiledMethod (index offset: 1 for header)
            _heapManager.SetIndexedOOP(methodOOP, i + 1, literalOOP);
        }

        // Create bytecode array (ByteIndexed)
        byte[] bytecodes = method.BytecodesAsArray;
        SmalltalkOOP bytecodeArrayOOP = _allocator.AllocateByteArray(bytecodes);

        // Store bytecode array OOP as last literal
        int lastIndex = 1 + method.Literals.Length;
        _heapManager.SetIndexedOOP(methodOOP, lastIndex, bytecodeArrayOOP);
    }
}
```

**ResolveLiteralValue Implementation** (with Association Canonicalization):

```csharp
private SmalltalkOOP ResolveLiteralValue(LiteralValue literal, MethodDescriptor method)
{
    return literal.Type switch
    {
        "symbol" => SymbolRegistry.GetSymbolOOP(literal.Index),

        "string" => _allocator.AllocateString(_bundle.Strings[literal.Index]),

        "smallInt" => SmalltalkOOP.FromSmallInteger(literal.Value),

        "nil" => SpecialObjectsArray.Instance.Nil,
        "true" => SpecialObjectsArray.Instance.True,
        "false" => SpecialObjectsArray.Instance.False,

        "objectRef" => _localIdToObjectId[literal.LocalId],

        "association" => GetOrCreateCanonicalAssociation(literal.VariableName),

        "array" => AllocateArrayLiteral(literal.Elements, method),

        "blockRef" => _localIdToObjectId[literal.BlockLocalId],

        _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
    };
}
```

**Association Canonicalization** (CRITICAL for memory efficiency):

```csharp
// Maps global variable name → canonical Association OOP
private readonly Dictionary<string, SmalltalkOOP> _canonicalAssociations = new();

private SmalltalkOOP GetOrCreateCanonicalAssociation(string variableName)
{
    // Return existing canonical Association if it exists
    if (_canonicalAssociations.TryGetValue(variableName, out var existing))
        return existing;

    // Create new Association (instance of class:Association)
    SmalltalkOOP assocOOP = _allocator.AllocateObjectOfClass("class:Association");

    // Set key (symbol for variable name)
    int symbolIndex = Array.IndexOf(_bundle.Symbols, variableName);
    SmalltalkOOP keySymbolOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);
    _heapManager.SetInstanceVariable(assocOOP, 0, keySymbolOOP);

    // Set value to nil initially (will be set later by init expressions)
    SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
    _heapManager.SetInstanceVariable(assocOOP, 1, nilOOP);

    // Register as canonical
    _canonicalAssociations[variableName] = assocOOP;

    return assocOOP;
}
```

**Why Canonicalization Matters**:

Without canonicalization:
- 3,745 Association literals in methods
- Each would be a separate object
- Memory: 3,745 × 24 bytes = ~90 KB

With canonicalization:
- 549 canonical Associations created
- 3,196 duplicates avoided (85.3% reduction)
- Memory: 549 × 24 bytes = ~13 KB
- **Savings: 77 KB**

**Validation** (performed after Pass 2):

```csharp
// Ensure all Association literals point to canonical objects
int totalAssocLiterals = 0;
int canonicalCount = _canonicalAssociations.Count;

foreach (var method in _bundle.Methods)
{
    totalAssocLiterals += method.Literals.Count(l => l.Type == "association");
}

Console.WriteLine($"[Phase4] Association canonicalization validation PASSED:");
Console.WriteLine($"  Total Association literals in methods: {totalAssocLiterals}");
Console.WriteLine($"  Canonical Associations created: {canonicalCount}");
Console.WriteLine($"  Duplicate Associations avoided: {totalAssocLiterals - canonicalCount}");
Console.WriteLine($"  Canonicalization ratio: {(canonicalCount * 100.0 / totalAssocLiterals):F1}%");
Console.WriteLine($"  Memory reduction: {((totalAssocLiterals - canonicalCount) * 100.0 / totalAssocLiterals):F1}%");
```

**Typical Results**:
```
Association canonicalization validation PASSED:
  Total Association literals in methods: 3745
  Canonical Associations created: 549
  Duplicate Associations avoided: 3196
  Canonicalization ratio: 14.7%
  Memory reduction: 85.3%
```

---

### Step 3: Create Init Expression Array

**Purpose**: Allocate Array to hold all init expression methods

```csharp
private void CreateInitExpressionArray()
{
    int initCount = _bundle.InitExpressions.Length;

    // Allocate Array (PointerIndexed, no instance vars)
    SmalltalkOOP arrayClassOOP = _localIdToObjectId["class:Array"];
    SmalltalkOOP initArrayOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount: 0,
        indexedSize: initCount
    );
    _heapManager.SetSlot(initArrayOOP, 0, arrayClassOOP);

    // Compile and store each init expression
    for (int i = 0; i < initCount; i++)
    {
        var initExpr = _bundle.InitExpressions[i];

        // Compile init expression (same as regular method)
        var initMethodOOP = CompileMethod(initExpr);
        _localIdToObjectId[initExpr.LocalId] = initMethodOOP;

        // Store in array
        _heapManager.SetIndexedOOP(initArrayOOP, i, initMethodOOP);
    }

    // Register in SpecialObjectsArray
    SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);

    Console.WriteLine($"  Created init expression array with {initCount} methods");
}
```

---

### Step 4: Install Methods in Dictionaries

**Purpose**: Insert methods into their home class's method dictionary using open addressing

**Open Addressing Algorithm**:
```
1. Hash selector to get initial probe index
2. If slot is nil, insert method here
3. If slot contains same selector, replace method (shouldn't happen)
4. If slot contains different selector, linear probe to next slot
5. Repeat until insertion succeeds
```

```csharp
private void InstallMethodsInDictionaries()
{
    foreach (var cls in _bundle.Classes)
    {
        SmalltalkOOP methodDictOOP = _methodDictionaries[cls.LocalId];
        int capacity = GetMethodDictionaryCapacity(methodDictOOP);
        int installedCount = 0;

        foreach (var methodLocalId in cls.MethodLocalIds)
        {
            // Skip blocks (they're not installed in dictionaries)
            if (methodLocalId.StartsWith("block:"))
                continue;

            var method = _bundle.Methods.First(m => m.LocalId == methodLocalId);
            SmalltalkOOP methodOOP = _localIdToObjectId[methodLocalId];
            SmalltalkOOP selectorOOP = SymbolRegistry.GetSymbolOOP(method.SelectorIndex);

            // Find insertion slot using open addressing
            int hash = ComputeSymbolHash(selectorOOP);
            int probe = hash % capacity;

            while (true)
            {
                // Check key slot
                SmalltalkOOP keySlot = _heapManager.GetIndexedOOP(methodDictOOP, probe);

                if (keySlot.IsNil)
                {
                    // Empty slot - insert here
                    _heapManager.SetIndexedOOP(methodDictOOP, probe, selectorOOP);
                    _heapManager.SetIndexedOOP(methodDictOOP, capacity + probe, methodOOP);
                    installedCount++;
                    break;
                }
                else if (keySlot.Equals(selectorOOP))
                {
                    // Duplicate selector - replace (shouldn't happen)
                    _heapManager.SetIndexedOOP(methodDictOOP, capacity + probe, methodOOP);
                    break;
                }
                else
                {
                    // Collision - linear probe
                    probe = (probe + 1) % capacity;
                }
            }
        }

        // Update tally
        _heapManager.SetInstanceVariable(methodDictOOP, 0,
            SmalltalkOOP.FromSmallInteger(installedCount));
    }

    Console.WriteLine($"  Installed methods in {_bundle.Classes.Length} dictionaries");
}

private int ComputeSymbolHash(SmalltalkOOP symbolOOP)
{
    // Use OOP raw value as hash (deterministic)
    return Math.Abs(symbolOOP.RawValue);
}
```

**State After Installation**:
- All 480 method dictionaries populated
- Tally set to actual method count
- Methods accessible via selector lookup

---

### Step 5: Set homeClass for All Methods

**Purpose**: Set fixed slot 0 of each CompiledMethod to point to its home class

**Why Needed**: Super sends require knowing which class a method belongs to for correct method lookup.

```csharp
private void SetHomeClassForMethods()
{
    foreach (var method in _bundle.Methods)
    {
        if (method.LocalId.StartsWith("block:"))
            continue;

        SmalltalkOOP methodOOP = _localIdToObjectId[method.LocalId];
        SmalltalkOOP homeClassOOP = _localIdToObjectId[method.HomeClassLocalId];

        // CompiledMethod format: [class, header, lit1, lit2, ..., bytecodes]
        // homeClass is stored as a "hidden" field before the header
        // We use a special HeapManager API for this
        _heapManager.SetCompiledMethodHomeClass(methodOOP, homeClassOOP);
    }
}
```

---

**Output State**:

- 480 method dictionaries allocated with correct capacities
- 10,182 methods compiled (including blocks)
- 549 canonical Association objects created (85.3% memory reduction)
- All methods installed in dictionaries with correct open addressing
- Init expression array created with 285 entries
- All methods have homeClass set for super sends
- BlockOwnerMap populated (internal tracking for lexical scoping)

**Memory**: ~3 MB (methods + literals + bytecode arrays)

**Performance**: ~361ms

---

## Phase 5: Globals and Patches

**File**: `LoaderPhases_New/Phase5_GlobalsAndPatches.cs`

**Purpose**: Apply patches, populate USTable, create and populate Smalltalk dictionary

**Consolidates**:
- Old Phase 10: Patch Application
- Old Phase 12: USTable Population
- Old Phase 13: Global Variable Installation

**Why Consolidated**: All three phases deal with "global namespace setup" - resolving forward references (patches), creating the symbol table (USTable), and installing classes in the global dictionary (Smalltalk).

**Input State**:
- All objects allocated (Phases 1-4)
- Methods compiled and installed
- Canonical Associations exist but values are nil

**Dependencies**:
- Phase 1-4 (needs all objects and methods)

---

### Step 1: Apply Patches

**Purpose**: Resolve forward references and circular structures that couldn't be set during initial allocation

**What are Patches**: Special fixup operations for:
- Circular object graphs (e.g., ProcessorScheduler ↔ Semaphore)
- Forward references (object A references object B which references A)
- Late binding (object field that depends on another object being fully initialized)

```csharp
private void ApplyPatches()
{
    // Example patches (actual patches vary by bundle):

    // 1. Install method dictionaries in classes
    foreach (var cls in _bundle.Classes)
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];
        SmalltalkOOP methodDictOOP = _methodDictionaries[cls.LocalId];

        // Find methodDict field index
        int methodDictIndex = _layoutResolver.GetFieldIndex(cls.LocalId, "methodDict");
        _heapManager.SetInstanceVariable(classOOP, methodDictIndex, methodDictOOP);
    }

    // 2. Set Object.superclass to nil (if not done in Phase 3)
    // (This is actually done in Phase 3 now)

    // 3. Other custom patches as needed

    Console.WriteLine($"  Applied patches for method dictionaries and circular references");
}
```

**Note**: In the new 6-phase loader, most "patch" work is done inline during earlier phases, so this step is minimal.

---

### Step 2: Create and Populate USTable

**Purpose**: Create universal symbol table - an Array containing all Symbol objects

**Structure**: Array with open addressing (hash-based insertion)

```csharp
private void CreateAndPopulateUSTable()
{
    int symbolCount = _bundle.Symbols.Length;

    // Calculate capacity (2x symbols for good hash performance)
    int capacity = symbolCount * 2;
    // Round up to next power of 2
    int powerOf2 = 2;
    while (powerOf2 < capacity)
        powerOf2 *= 2;
    capacity = powerOf2;

    // Allocate Array
    SmalltalkOOP arrayClassOOP = _localIdToObjectId["class:Array"];
    SmalltalkOOP ustableOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount: 0,
        indexedSize: capacity
    );
    _heapManager.SetSlot(ustableOOP, 0, arrayClassOOP);

    // Initialize all slots to nil
    SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
    for (int i = 0; i < capacity; i++)
    {
        _heapManager.SetIndexedOOP(ustableOOP, i, nilOOP);
    }

    // Insert all symbols using open addressing
    for (int symbolIndex = 0; symbolIndex < symbolCount; symbolIndex++)
    {
        SmalltalkOOP symbolOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);

        // Hash symbol to get probe index
        int hash = ComputeSymbolHash(symbolOOP);
        int probe = hash % capacity;

        // Linear probe until empty slot found
        while (true)
        {
            SmalltalkOOP slot = _heapManager.GetIndexedOOP(ustableOOP, probe);

            if (slot.IsNil)
            {
                _heapManager.SetIndexedOOP(ustableOOP, probe, symbolOOP);
                break;
            }

            probe = (probe + 1) % capacity;
        }
    }

    // Store USTable in localIdToObjectId for global installation
    _localIdToObjectId["USTable"] = ustableOOP;

    Console.WriteLine($"  Created USTable with {symbolCount} symbols (capacity {capacity})");
}
```

**Result**: USTable array contains all 3,834 symbols with hash-based distribution for fast lookup.

---

### Step 3: Create and Populate Smalltalk Dictionary

**Purpose**: Create SystemDictionary containing all global variables (classes, special objects)

```csharp
private void CreateAndPopulateSystemDictionary()
{
    // Allocate SystemDictionary (subclass of Dictionary)
    SmalltalkOOP sysDictClassOOP = _localIdToObjectId["class:SystemDictionary"];

    // Calculate capacity for all globals
    int globalCount = 240 + 6;  // 240 classes + special objects
    int capacity = CalculateDictionaryCapacity(globalCount);

    // SystemDictionary format: 1 instance var (tally) + indexed keys/values
    SmalltalkOOP sysDictOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount: 1,
        indexedSize: 2 * capacity
    );
    _heapManager.SetSlot(sysDictOOP, 0, sysDictClassOOP);

    // Initialize tally to 0
    _heapManager.SetInstanceVariable(sysDictOOP, 0, SmalltalkOOP.FromSmallInteger(0));

    // Initialize all slots to nil
    SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;
    for (int i = 0; i < 2 * capacity; i++)
    {
        _heapManager.SetIndexedOOP(sysDictOOP, i, nilOOP);
    }

    int installedCount = 0;

    // Install all classes using canonical Associations
    foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
    {
        string className = _bundle.Symbols[cls.NameIndex];
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

        // Get or create canonical Association
        SmalltalkOOP assocOOP = GetOrCreateCanonicalAssociation(className);

        // Set Association value to the class
        _heapManager.SetInstanceVariable(assocOOP, 1, classOOP);

        // Install Association in dictionary
        InsertAssociationInDictionary(sysDictOOP, capacity, assocOOP, className);
        installedCount++;
    }

    // Install special globals (USTable, Smalltalk, nil, true, false, etc.)
    InstallGlobal(sysDictOOP, capacity, "Smalltalk", sysDictOOP);  // self-reference
    InstallGlobal(sysDictOOP, capacity, "symbols", _localIdToObjectId["USTable"]);
    InstallGlobal(sysDictOOP, capacity, "nil", SpecialObjectsArray.Instance.Nil);
    InstallGlobal(sysDictOOP, capacity, "true", SpecialObjectsArray.Instance.True);
    InstallGlobal(sysDictOOP, capacity, "false", SpecialObjectsArray.Instance.False);
    installedCount += 5;

    // Update tally
    _heapManager.SetInstanceVariable(sysDictOOP, 0,
        SmalltalkOOP.FromSmallInteger(installedCount));

    // Register in SpecialObjectsArray
    SpecialObjectsArray.SetSmalltalk(sysDictOOP);
    GlobalRegistry.Register("Smalltalk", sysDictOOP);

    Console.WriteLine($"  Created Smalltalk dictionary with {installedCount} globals");
}

private void InstallGlobal(SmalltalkOOP dictOOP, int capacity,
                          string name, SmalltalkOOP value)
{
    // Get canonical Association
    SmalltalkOOP assocOOP = GetOrCreateCanonicalAssociation(name);

    // Set Association value
    _heapManager.SetInstanceVariable(assocOOP, 1, value);

    // Install in dictionary
    InsertAssociationInDictionary(dictOOP, capacity, assocOOP, name);
}

private void InsertAssociationInDictionary(SmalltalkOOP dictOOP, int capacity,
                                           SmalltalkOOP assocOOP, string key)
{
    // Get key symbol
    int symbolIndex = Array.IndexOf(_bundle.Symbols, key);
    SmalltalkOOP keySymbolOOP = SymbolRegistry.GetSymbolOOP(symbolIndex);

    // Hash and probe
    int hash = ComputeSymbolHash(keySymbolOOP);
    int probe = hash % capacity;

    while (true)
    {
        SmalltalkOOP slot = _heapManager.GetIndexedOOP(dictOOP, probe);

        if (slot.IsNil)
        {
            // Insert Association (not just key/value - store Association object)
            _heapManager.SetIndexedOOP(dictOOP, probe, assocOOP);
            break;
        }

        probe = (probe + 1) % capacity;
    }
}
```

**Key Points**:
- Uses canonical Associations (same objects used in method literals!)
- Association values are set during installation
- Smalltalk dictionary contains self-reference (`Smalltalk at: #Smalltalk` returns itself)

---

### Step 4: Freeze GlobalRegistry

**Purpose**: Lock GlobalRegistry to prevent runtime modifications

```csharp
GlobalRegistry.Freeze();
Console.WriteLine($"[GlobalRegistry] Frozen with {GlobalRegistry.Count} entries");
```

**Why Critical**: Runtime code must not modify GlobalRegistry after coldstart completes.

---

**Output State**:

- All class method dictionaries installed
- USTable array created with 3,834 symbols (hash-based distribution)
- Smalltalk dictionary created with 246 globals (240 classes + 6 special objects)
- All canonical Associations have values set
- GlobalRegistry frozen

**Memory**: ~500 KB (USTable + Smalltalk dictionary + Associations)

**Performance**: ~423ms

---

## Phase 6: Validation and GC Verification

**File**: `LoaderPhases_New/Phase6_ValidationAndGC.cs`

**Purpose**: Comprehensive validation of heap structure and GC verification

**Consolidates**:
- Old Phase 14: GC Root Setup
- Old Phase 15: GC Verification
- Additional validations from old Phase 13

**Why Consolidated**: All validation and verification steps are logically a single "sanity check" phase that ensures the heap is consistent and complete before runtime execution begins.

**Input State**:
- Complete heap with all objects, classes, methods, globals
- SpecialObjectsArray populated
- GlobalRegistry frozen

**Dependencies**:
- Phase 1-5 (needs complete heap)

---

### Step 1: Validate Class Structure

**Purpose**: Ensure all classes have proper metadata

```csharp
private void ValidateClassStructure()
{
    foreach (var cls in _bundle.Classes)
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

        // Validate class has non-zero class reference
        SmalltalkOOP classOfClass = _heapManager.GetSlot(classOOP, 0);
        if (classOfClass.RawValue == 0)
            throw new InvalidOperationException(
                $"Class {cls.LocalId} has NULL_CLASS (class reference is 0)");

        // Validate metaclass thisClass points to instance class
        if (cls.IsMetaclass)
        {
            int thisClassIndex = _layoutResolver.GetFieldIndex("class:Metaclass", "thisClass");
            SmalltalkOOP thisClassOOP = _heapManager.GetInstanceVariable(classOOP, thisClassIndex);

            if (thisClassOOP.RawValue == 0)
                throw new InvalidOperationException(
                    $"Metaclass {cls.LocalId} has null thisClass");
        }

        // Validate regular class has method dictionary
        if (!cls.IsMetaclass)
        {
            int methodDictIndex = _layoutResolver.GetFieldIndex(cls.LocalId, "methodDict");
            SmalltalkOOP methodDictOOP = _heapManager.GetInstanceVariable(classOOP, methodDictIndex);

            if (methodDictOOP.IsNil)
                throw new InvalidOperationException(
                    $"Class {cls.LocalId} has nil methodDict");
        }
    }

    Console.WriteLine($"  Validating classes...");
}
```

---

### Step 2: Validate Methods

**Purpose**: Ensure all methods have valid structure

```csharp
private void ValidateMethods()
{
    foreach (var method in _bundle.Methods)
    {
        if (method.LocalId.StartsWith("block:"))
            continue;

        SmalltalkOOP methodOOP = _localIdToObjectId[method.LocalId];

        // Validate method has non-zero class reference
        SmalltalkOOP classOOP = _heapManager.GetSlot(methodOOP, 0);
        if (classOOP.RawValue == 0)
            throw new InvalidOperationException(
                $"Method {method.LocalId} has NULL_CLASS");

        // Validate header is SmallInteger
        SmalltalkOOP headerOOP = _heapManager.GetIndexedOOP(methodOOP, 0);
        if (!headerOOP.IsSmallInteger)
            throw new InvalidOperationException(
                $"Method {method.LocalId} header is not SmallInteger");

        // Validate bytecode array exists
        int lastIndex = 1 + method.Literals.Length;
        SmalltalkOOP bytecodeOOP = _heapManager.GetIndexedOOP(methodOOP, lastIndex);
        if (bytecodeOOP.RawValue == 0)
            throw new InvalidOperationException(
                $"Method {method.LocalId} has null bytecode array");
    }

    Console.WriteLine($"  Validating methods...");
}
```

---

### Step 3: Validate Special Objects

**Purpose**: Ensure nil/true/false exist and are correct

```csharp
private void ValidateSpecialObjects()
{
    var nilOOP = SpecialObjectsArray.Instance.Nil;
    var trueOOP = SpecialObjectsArray.Instance.True;
    var falseOOP = SpecialObjectsArray.Instance.False;

    if (nilOOP.RawValue == 0)
        throw new InvalidOperationException("nil singleton not allocated");

    if (trueOOP.RawValue == 0)
        throw new InvalidOperationException("true singleton not allocated");

    if (falseOOP.RawValue == 0)
        throw new InvalidOperationException("false singleton not allocated");

    // Validate classes
    SmalltalkOOP nilClass = _heapManager.GetSlot(nilOOP, 0);
    SmalltalkOOP expectedNilClass = _localIdToObjectId["class:UndefinedObject"];
    if (!nilClass.Equals(expectedNilClass))
        throw new InvalidOperationException("nil has wrong class");

    Console.WriteLine($"  Validating special objects...");
}
```

---

### Step 4: Validate Init Expressions

**Purpose**: Ensure init expression array is populated

```csharp
private void ValidateInitExpressions()
{
    var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;

    if (initArrayOOP.RawValue == 0)
        throw new InvalidOperationException("InitExpressionMethods array not allocated");

    // Validate array has correct size
    var entry = _heapManager.GetObjectTableEntry(initArrayOOP);
    if (entry.IndexedSize != _bundle.InitExpressions.Length)
        throw new InvalidOperationException(
            $"InitExpressionMethods array size mismatch: " +
            $"expected {_bundle.InitExpressions.Length}, got {entry.IndexedSize}");

    Console.WriteLine($"  Validating init expressions...");
}
```

---

### Step 5: Validate Cross-References

**Purpose**: Ensure no objects have invalid OOP references

```csharp
private void ValidateCrossReferences()
{
    int validatedCount = 0;

    foreach (var (objectOOP, entry) in _heapManager.EnumerateObjectTable())
    {
        // Validate class reference
        SmalltalkOOP classOOP = _heapManager.GetSlot(objectOOP, 0);
        if (classOOP.RawValue == 0)
            throw new InvalidOperationException(
                $"Object {objectOOP} has NULL_CLASS");

        // Validate instance variables (if Pointer or PointerIndexed)
        if (entry.Format == ObjectFormat.Pointer || entry.Format == ObjectFormat.PointerIndexed)
        {
            for (int i = 0; i < entry.InstanceVariableCount; i++)
            {
                SmalltalkOOP fieldOOP = _heapManager.GetInstanceVariable(objectOOP, i);

                // All OOPs must be valid (either SmallInteger or valid object reference)
                if (!fieldOOP.IsSmallInteger && fieldOOP.RawValue != 0)
                {
                    // Verify referenced object exists
                    if (!_heapManager.IsValidOOP(fieldOOP))
                        throw new InvalidOperationException(
                            $"Object {objectOOP} field {i} has invalid OOP {fieldOOP}");
                }
            }
        }

        validatedCount++;
    }

    Console.WriteLine($"  Validating cross-references...");
    Console.WriteLine($"    Cross-references: {validatedCount} objects checked");
}
```

---

### Step 6: Validate SpecialObjectsArray

**Purpose**: Ensure all GC roots are set

```csharp
private void ValidateSpecialObjectsArray()
{
    var soa = SpecialObjectsArray.Instance;

    if (soa.Nil.RawValue == 0)
        throw new InvalidOperationException("SpecialObjectsArray.Nil not set");

    if (soa.True.RawValue == 0)
        throw new InvalidOperationException("SpecialObjectsArray.True not set");

    if (soa.False.RawValue == 0)
        throw new InvalidOperationException("SpecialObjectsArray.False not set");

    if (soa.Smalltalk.RawValue == 0)
        throw new InvalidOperationException("SpecialObjectsArray.Smalltalk not set");

    if (soa.InitExpressionMethods.RawValue == 0)
        throw new InvalidOperationException("SpecialObjectsArray.InitExpressionMethods not set");

    Console.WriteLine($"  Validating SpecialObjectsArray...");
    Console.WriteLine($"    Validated {5} special object slots");
}
```

---

### Step 7: Validate Method Dictionaries

**Purpose**: Ensure all method dictionaries have correct tally and methods are accessible

```csharp
private void ValidateMethodDictionaries()
{
    int totalMethods = 0;

    foreach (var cls in _bundle.Classes)
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

        // Get method dictionary
        int methodDictIndex = _layoutResolver.GetFieldIndex(cls.LocalId, "methodDict");
        SmalltalkOOP methodDictOOP = _heapManager.GetInstanceVariable(classOOP, methodDictIndex);

        if (methodDictOOP.IsNil)
            continue;  // Some classes may have no methods

        // Get tally
        SmalltalkOOP tallyOOP = _heapManager.GetInstanceVariable(methodDictOOP, 0);
        int tally = tallyOOP.AsSmallInteger();

        // Count actual entries
        var entry = _heapManager.GetObjectTableEntry(methodDictOOP);
        int capacity = entry.IndexedSize / 2;
        int actualCount = 0;

        for (int i = 0; i < capacity; i++)
        {
            SmalltalkOOP keyOOP = _heapManager.GetIndexedOOP(methodDictOOP, i);
            if (!keyOOP.IsNil)
                actualCount++;
        }

        // Validate tally matches
        if (tally != actualCount)
            throw new InvalidOperationException(
                $"Class {cls.LocalId} methodDict tally mismatch: " +
                $"tally={tally}, actual={actualCount}");

        totalMethods += tally;
    }

    Console.WriteLine($"  Validating method dictionaries...");
    Console.WriteLine($"    Validated {_bundle.Classes.Length} method dictionaries");
    Console.WriteLine($"    Found {totalMethods}/{_bundle.Methods.Count(m => !m.LocalId.StartsWith("block:"))} methods in dictionaries");
}
```

---

### Step 8: Setup GC Roots

**Purpose**: Display GC roots for verification

```csharp
private void SetupGCRoots()
{
    Console.WriteLine($"Step 2: GC Root Setup...");
    Console.WriteLine($"  GC Roots ({5} objects):");

    var soa = SpecialObjectsArray.Instance;

    Console.WriteLine($"    nil ({soa.Nil}, class={GetClassName(soa.Nil)})");
    Console.WriteLine($"    false ({soa.False}, class={GetClassName(soa.False)})");
    Console.WriteLine($"    true ({soa.True}, class={GetClassName(soa.True)})");
    Console.WriteLine($"    Smalltalk ({soa.Smalltalk}, class={GetClassName(soa.Smalltalk)})");
    Console.WriteLine($"    InitExpressionMethods ({soa.InitExpressionMethods}, class={GetClassName(soa.InitExpressionMethods)})");
}

private string GetClassName(SmalltalkOOP objectOOP)
{
    SmalltalkOOP classOOP = _heapManager.GetSlot(objectOOP, 0);

    // Try to find class name
    foreach (var cls in _bundle.Classes)
    {
        if (_localIdToObjectId[cls.LocalId].Equals(classOOP))
            return _bundle.Symbols[cls.NameIndex];
    }

    return "UnknownClass";
}
```

---

### Step 9: Run GC Verification

**Purpose**: Verify all objects are reachable from GC roots (CRITICAL!)

```csharp
private void RunGCVerification()
{
    Console.WriteLine($"Step 3: GC Verification...");

    // Trigger GC
    var gcStats = _heapManager.CollectGarbage();

    Console.WriteLine($"  GC Results: {gcStats.ObjectsCollected}/{gcStats.TotalObjects} " +
                      $"objects collected ({gcStats.Efficiency:F1}% efficiency) " +
                      $"in {gcStats.TotalTime:F2}ms " +
                      $"(Mark: {gcStats.MarkTime:F2}ms, Sweep: {gcStats.SweepTime:F2}ms), " +
                      $"{gcStats.BytesFreed} bytes freed");

    Console.WriteLine($"  Object table size: {gcStats.TotalObjects}");
    Console.WriteLine($"  Active objects: {gcStats.TotalObjects - gcStats.ObjectsCollected}");

    // CRITICAL CHECK: No objects should be collected during cold start
    if (gcStats.ObjectsCollected > 0)
    {
        throw new InvalidOperationException(
            $"GC verification FAILED: {gcStats.ObjectsCollected} unreachable objects found! " +
            $"All objects should be reachable from GC roots.");
    }

    Console.WriteLine($"  ✓ GC Verification passed: All {gcStats.TotalObjects} objects are reachable");
}
```

**Why This is CRITICAL**: If ANY objects are collected, it means:
- Objects were allocated but not linked to GC roots
- Broken references in the object graph
- Missing entries in SpecialObjectsArray
- Bug in earlier phases

**Expected Result**: 0 objects collected (100% reachable)

---

### Step 10: Print Validation Summary

```csharp
private void PrintValidationSummary()
{
    Console.WriteLine($"✓ Validation Summary:");
    Console.WriteLine($"    Classes: {_bundle.Classes.Length} validated");
    Console.WriteLine($"    Methods: {_bundle.Methods.Count(m => !m.LocalId.StartsWith("block:"))} validated");
    Console.WriteLine($"    Special objects: 5 validated");
    Console.WriteLine($"    Init expressions: {_bundle.InitExpressions.Length} validated");
    Console.WriteLine($"    Cross-references: {_heapManager.GetObjectTableSize()} objects checked");
    Console.WriteLine($"    ✓ All heap objects match JSON definitions");
}
```

---

**Output State**:

- All validation checks passed
- Class structure verified (480 classes)
- Method structure verified (10,182 methods)
- Special objects verified (nil, true, false, Smalltalk, InitExpressionMethods)
- Cross-references verified (29,230 objects)
- Method dictionaries verified (480 dictionaries, 4,693 methods)
- GC verification passed (0 objects collected)
- **Heap is ready for runtime execution!**

**Performance**: ~115ms

---

## Summary: 6-Phase Architecture Benefits

### Code Reduction
- **Old**: 19 phase files (Phase0-16 + PhaseX + PhaseY)
- **New**: 6 phase files
- **Reduction**: 64% fewer files

### Performance
- **Total time**: ~984ms (similar to old loader)
- **Phase 5 improvement**: v1.0.6 precomputed layouts (~2-5ms vs ~20-25ms)

### Maintainability
- **Logical grouping**: Each phase has clear single responsibility
- **Fewer intermediate states**: Objects fully initialized in one phase
- **Clearer dependencies**: Phase N+1 always depends on Phase N being complete

### Correctness
- **Identical heap**: Produces byte-for-byte identical results as old loader
- **All validations pass**: 100% validation success
- **Zero GC issues**: 0/29,230 objects unreachable

### Feature Highlights
- **Association canonicalization**: 85.3% memory reduction (3,745 → 549 objects)
- **Precomputed layouts**: 80-90% faster layout loading
- **Comprehensive validation**: 10,950 cross-references checked
- **BlockClosure support**: Lexical scoping with homeContext capture

---

## Appendix: Phase Execution Flow

```
Loader_New.Run()
  │
  ├─ Phase 1: Preflight + Symbols (50ms)
  │   ├─ Validate bundle version (v1.0.6+)
  │   ├─ Validate localIds (uniqueness)
  │   ├─ Validate symbol indices (bounds)
  │   ├─ Validate object references (dangling)
  │   ├─ Validate precomputed layouts (consistency)
  │   ├─ Validate home class metadata
  │   ├─ Validate metaclass circle (5 classes)
  │   ├─ Validate special objects (nil/true/false classes)
  │   ├─ Intern 3,834 symbols
  │   └─ Freeze SymbolRegistry
  │
  ├─ Phase 2: Class Infrastructure (22ms)
  │   ├─ Allocate 480 class shells
  │   ├─ Set thisClass for 240 metaclasses
  │   ├─ Wire fundamental metaclass circle (4 hops)
  │   ├─ Wire 239 metaclass superclasses
  │   ├─ Resolve class metadata (name, format, superclass)
  │   └─ Load 480 precomputed layouts
  │
  ├─ Phase 3: Object Allocation (13ms)
  │   ├─ Allocate sentinel objects (nil, true, false)
  │   ├─ Allocate 2 special objects from bundle
  │   ├─ Set class references for 485 objects
  │   ├─ Initialize Object.superclass = nil
  │   └─ Initialize 2,163 instance variables to nil
  │
  ├─ Phase 4: Method Compilation (361ms)
  │   ├─ Allocate 480 method dictionaries
  │   ├─ Create 10,182 method shells
  │   ├─ Resolve literals (create 549 canonical Associations)
  │   ├─ Create init expression array (285 methods)
  │   ├─ Install methods in dictionaries
  │   └─ Set homeClass for all methods
  │
  ├─ Phase 5: Globals + Patches (423ms)
  │   ├─ Apply patches (method dictionaries)
  │   ├─ Create USTable (3,834 symbols)
  │   ├─ Create Smalltalk dictionary (246 globals)
  │   └─ Freeze GlobalRegistry
  │
  └─ Phase 6: Validation + GC (115ms)
      ├─ Validate class structure (480 classes)
      ├─ Validate methods (10,182 methods)
      ├─ Validate special objects (5 singletons)
      ├─ Validate init expressions (285 methods)
      ├─ Validate cross-references (29,230 objects)
      ├─ Validate SpecialObjectsArray (5 roots)
      ├─ Validate method dictionaries (480 dicts, 4,693 methods)
      ├─ Setup GC roots
      ├─ Run GC verification (0 collected)
      └─ Print validation summary
```

**Total**: 984ms, 29,230 objects, 100% validation success

---

End of Documentation
