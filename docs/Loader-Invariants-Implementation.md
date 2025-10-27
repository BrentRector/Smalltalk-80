# Loader Invariants Implementation Guide

**Last Updated**: 2025-01-25
**Status**: ✅ **COMPLETE** - All validations implemented and passing

---

## Test Results

**Cold Start Validation Status** (2025-01-25):
- ✅ Phase 0: Preflight Validation - **PASSING**
  - Metaclass circle structure validated
  - All required fundamental classes present
- ✅ Phase 13: Comprehensive Heap Validation - **PASSING**
  - 5/5 special object slots validated
  - 480/480 method dictionaries validated
  - 4693/4693 methods found in dictionaries (100%)
  - All objects have valid class references
  - All cross-references valid

**New Validation Methods Added**:
1. `Phase0_Preflight.cs::ValidateMetaclassCircle()` - Validates metaclass structure at bundle level
2. `Phase0_Preflight.cs::ValidateSpecialObjectsArrayDefinition()` - Validates required classes exist
3. `Phase13_ComprehensiveValidation.cs::ValidateSpecialObjectsArray()` - Validates runtime special objects
4. `Phase13_ComprehensiveValidation.cs::ValidateMethodDictionaries()` - Validates method dictionary integrity

**Helper Methods Added**:
- `SemanticAllocator.cs::GetClassName()` - Gets class name for debugging/validation
- `SemanticAllocator.cs::GetSymbolName()` - Gets symbol string value

---

## Overview

This document maps each loader invariant and guarantee from [CLAUDE.md](../CLAUDE.md) to its implementation and validation code. Use this as a reference to understand how the cold start loader ensures correctness.

---

## Minimal Hard Invariants

### Invariant #1: Non-Zero Class Slots

**Requirement**: Every non-immediate object has a non-zero class slot (slot 0) that points to the exact class object the file-out expects.

**Implementation**:
- **Phase X** (`PhaseX_ClassReferenceInitialization.cs`): Sets class slot 0 for all objects
  - Classes → metaclasses
  - Symbols → `class:Symbol`
  - Strings → `class:String`
  - Arrays → `class:Array`
  - etc.

**Validation**:
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateAllClasses()`, `ValidateAllMethods()`
  - Verifies every object has non-null class reference
  - Verifies class reference points to expected class

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/LoaderPhases/PhaseX_ClassReferenceInitialization.cs`
- Validation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase13_ComprehensiveValidation.cs:63-115`

---

### Invariant #2: Metaclass Circle and Superclass Chains

**Requirement**:
- The metaclass circle and all class superclass chains terminate exactly as in the file-out
- `Object.superclass == nil`
- The metaclass circle closes at `Behavior → Object → Class → ... → Behavior`

**Implementation**:
- **Phase 3** (`Phase3_MetaclassWiring.cs`): Wires metaclass circle
  - Sets `metaclass.thisClass` for all metaclasses
  - Wires fundamental hierarchy: `meta:Object.superclass = class:Class`
  - Defers `Object.superclass` until nil exists
- **Phase 4** (`Phase4_ClassResolution.cs`): Sets all superclass references
  - `Object.superclass = nil` set explicitly

**Validation**:
- **Phase 0** (`Phase0_Preflight.cs`): `ValidateMetaclassCircle()`
  - Validates `Object.SuperclassLocalId` is empty (will be nil)
  - Validates `meta:Object.SuperclassLocalId == "class:Class"`
  - Validates all metaclasses have `thisClass` field
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateCrossReferences()`
  - Verifies actual runtime superclass chain termination

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_MetaclassWiring.cs`, `Phase4_ClassResolution.cs`
- Validation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase0_Preflight.cs:197-270`

---

### Invariant #3: Class Layouts

**Requirement**: Every class's instance variable count and field name→slot mapping match the file-out exactly.

**Implementation**:
- **Phase 5** (`LayoutResolver.cs`): Loads precomputed layouts from v1.0.6 bundle
  - `TotalInstanceVarCount` loaded from bundle
  - `FieldLayout[]` loaded with name→slot mappings
  - All layout queries use precomputed data (no runtime calculation)

**Validation**:
- **Phase 0** (`Phase0_Preflight.cs`): `ValidatePrecomputedLayouts()`
  - Validates `TotalInstanceVarCount == FieldLayout.Length`
  - Validates field name indices are within symbol table bounds
  - Validates slot indices are sequential (0, 1, 2, ...)
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateAllClasses()`
  - Validates actual allocated instance variable count matches precomputed layout

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Core/LayoutResolver.cs`
- Validation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase0_Preflight.cs:113-152`

---

### Invariant #4: Instance Variable Initialization

**Requirement**: All instance variable slots are either explicitly set to the same object the file-out sets or are initialized to the nil singleton (not left as zero).

**Implementation**:
- **Phase Y** (`PhaseY_InstanceVariableInitialization.cs`): Initializes all ivars to nil
  - Tracks which fields were set by earlier phases in `_initializedFields`
  - Sets all uninitialized fields to `SpecialObjectsArray.Instance.Nil`
  - Validates nil singleton exists before use

**Validation**:
- **Phase Y** (built-in): Logs initialization statistics
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateAllClasses()`, `ValidateAllMethods()`
  - Verifies no instance variables are SmalltalkOOP.Null (uninitialized)

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/LoaderPhases/PhaseY_InstanceVariableInitialization.cs`
- Validation: Built into Phase Y execution

---

### Invariant #5: Canonical Objects

**Requirement**: Symbols, Strings, ByteArrays, CompiledMethods, and Bytecode arrays exist with the same contents and are canonical where the file-out uses canonical objects (e.g., Associations for globals).

**Implementation**:
- **Phase 1** (`Phase1_SymbolInterning.cs`): Symbols canonicalized, registry frozen
- **Phase 8** (`Phase8_MethodInstallation.cs`): Association canonicalization
  - `_canonicalAssociations` dictionary ensures one Association per variable name
  - `FindCanonicalAssociation()` returns existing Association or creates new
  - `ValidateAssociationCanonicalization()` verifies no duplicates
  - `ValidateAssociationReachability()` ensures all Associations traceable to canonical sources

**Validation**:
- **Phase 8** (built-in): `ValidateAssociationCanonicalization()`
  - Reports canonicalization ratio, duplicates avoided
  - Throws exception if same key maps to different Association OOPs
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): Validates object contents

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase8_MethodInstallation.cs:142-410`
- Validation: Built into Phase 8, verified in Phase 13

---

### Invariant #6: Method Dictionaries

**Requirement**: Method dictionaries - capacity, key/value placement (open-addressing invariants), and tally must match (or be provably equivalent for the VM hash scheme) so method lookup yields identical results.

**Implementation**:
- **Phase 7** (`Phase7_MethodDictionaryAllocation.cs`): Allocates method dictionaries with deterministic capacity
- **Phase 9** (`Phase9_MethodDictionaryInstallation.cs`): Installs methods
  - Linear probe search for empty slot or existing selector
  - Updates tally for new entries
  - Deterministic given stable method order from bundle

**Validation**:
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateMethodDictionaries()`
  - Validates tally matches bundle method count
  - Validates all methods are findable via linear search
  - Reports statistics (classes with dicts, methods found)

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase9_MethodDictionaryInstallation.cs`
- Validation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase13_ComprehensiveValidation.cs:539-635`

**Note**: Current implementation uses linear probe which is deterministic given stable bundle order. For absolute guarantees, consider storing placement data in bundle v1.0.7.

---

### Invariant #7: SpecialObjectsArray

**Requirement**: SpecialObjectsArray contains the same singletons in the same slots (nil, true, false, Smalltalk dictionary, USTable, InitMethods, ProcessorScheduler, etc.).

**Implementation**:
- **Phase 6** (`Phase6_ObjectAllocation.cs`): Creates nil/true/false singletons, registers in SpecialObjectsArray
- **Phase 12** (`Phase12_GlobalVariableInstallation.cs`): Populates SystemDictionary
- **Loader** (`Loader.cs`): Registers InitExpressionMethods array

**Validation**:
- **Phase 0** (`Phase0_Preflight.cs`): `ValidateSpecialObjectsArrayDefinition()`
  - Validates bundle defines required singletons (`object:nil`, `object:true`, `object:false`)
  - Validates fundamental classes exist (Object, Behavior, Class, etc.)
- **Phase 13** (`Phase13_ComprehensiveValidation.cs`): `ValidateSpecialObjectsArray()`
  - Validates nil OOP has class UndefinedObject
  - Validates true OOP has class True
  - Validates false OOP has class False
  - Validates SystemDictionary exists and is not null
  - Validates InitExpressionMethods is Array (if present)

**Code Location**:
- Implementation: Distributed across Phase 6, Phase 12, Loader
- Validation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase0_Preflight.cs:272-338`, `Phase13_ComprehensiveValidation.cs:451-537`

---

### Invariant #8: Garbage Collection Reachability

**Requirement**: GC roots and object reachability must match the file-out reachability graph (no unreachable objects that the file-out would have reachable).

**Implementation**:
- **Phase 14** (`Loader.cs`): Sets GC roots
  - nil, true, false
  - SystemDictionary (Smalltalk)
  - InitExpressionMethods array
- **Phase 15** (`Loader.cs`): Runs GC verification
  - Executes full mark-sweep GC
  - **REQUIRES 0 collected objects** for cold start to pass

**Validation**:
- **Phase 15** (built-in): GC verification
  - Throws exception if any objects are collected
  - Reports statistics: allocated vs reachable
  - Ensures perfect reachability graph

**Code Location**:
- Implementation: `Smalltalk80.Interpreter/Engine/Loader.cs:246-302`
- Validation: Built into Phase 15 (lines 279-302)

---

## Deterministic Loader Guarantees

### Guarantee #1: Deterministic Symbol Interning

**Requirement**: Same symbol indices → same symbol OOPs.

**Implementation**:
- **Phase 1** (`Phase1_SymbolInterning.cs`): Deterministic symbol creation
  - Processes symbols in array order (0, 1, 2, ...)
  - Each symbol allocated with sequential OOP
  - SymbolRegistry frozen after phase (no new symbols)

**Validation**:
- **Phase 13**: Validates symbols are reachable and have correct class

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase1_SymbolInterning.cs`

---

### Guarantee #2: Class Shell Allocation Before Wiring

**Requirement**: Allocate class shells for all classes/metaclasses before wiring any cross-class fields.

**Implementation**:
- **Phase 2** (`Phase2_ClassShells.cs`): Allocates all classes first
  - Creates shells with nil instance variables
  - No superclass or thisClass wiring yet
  - Ensures all class OOPs exist before Phase 3

**Validation**:
- **Phase 3**: Validates all classes exist before wiring metaclass circle

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase2_ClassShells.cs`

---

### Guarantee #3: Metaclass Wiring

**Requirement**: Wire metaclass.thisClass and the metaclass circle exactly as in the file-out (deterministic wiring order).

**Implementation**:
- **Phase 3** (`Phase3_MetaclassWiring.cs`): Wires metaclass relationships
  - Processes classes in deterministic order
  - Sets `metaclass.thisClass` before superclass wiring
  - Wires fundamental metaclass hierarchy first

**Validation**:
- **Phase 0**: `ValidateMetaclassCircle()` validates bundle structure
- **Phase 13**: Validates actual wiring matches expectations

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_MetaclassWiring.cs`

---

### Guarantee #4: Class Metadata Population

**Requirement**: Populate class metadata (name, format, superclass references) using the file-out data; defer Object.superclass until nil exists.

**Implementation**:
- **Phase 4** (`Phase4_ClassResolution.cs`): Sets class metadata
  - Defers `Object.superclass` until after Phase 6 (when nil exists)
  - Sets all other superclass references deterministically

**Validation**:
- **Phase 13**: Validates class metadata matches bundle

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase4_ClassResolution.cs`

---

### Guarantee #5: Layout Loading

**Requirement**: Load or compute class layouts deterministically (precomputed layout from file-out preferred).

**Implementation**:
- **Phase 5** (`LayoutResolver.cs`): Loads precomputed layouts from v1.0.6 bundle
  - No runtime computation (eliminates non-determinism)
  - Direct loading from `FieldLayout[]` arrays
  - ~18ms faster than runtime computation

**Validation**:
- **Phase 0**: `ValidatePrecomputedLayouts()` validates layout data structure

**Code Location**: `Smalltalk80.Interpreter/Core/LayoutResolver.cs`

---

### Guarantee #6: Singleton Allocation

**Requirement**: Allocate nil/true/false singletons and register them in SpecialObjectsArray.

**Implementation**:
- **Phase 6** (`Phase6_ObjectAllocation.cs`): Creates singletons
  - Allocates nil (class:UndefinedObject)
  - Allocates true (class:True)
  - Allocates false (class:False)
  - Registers in SpecialObjectsArray

**Validation**:
- **Phase 13**: `ValidateSpecialObjectsArray()` validates singleton integrity

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase6_ObjectAllocation.cs`

---

### Guarantee #7: Class Reference Initialization

**Requirement**: Set every object's class slot (slot 0) deterministically (classes → metaclasses, symbols → class:Symbol, etc.).

**Implementation**:
- **Phase X** (`PhaseX_ClassReferenceInitialization.cs`): Sets all class slots
  - Processes all classes, symbols, strings, etc.
  - Deterministic assignment based on object type

**Validation**:
- **Phase 13**: Validates all objects have correct class references

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/PhaseX_ClassReferenceInitialization.cs`

---

### Guarantee #8: Instance Variable Initialization

**Requirement**: Initialize all remaining instance variable slots to nil only where not explicitly set by earlier phases.

**Implementation**:
- **Phase Y** (`PhaseY_InstanceVariableInitialization.cs`): Initializes unset ivars
  - Uses `_initializedFields` tracking from earlier phases
  - Sets all uninitialized fields to nil
  - Logs initialization statistics

**Validation**:
- **Phase 13**: Validates no fields are uninitialized (SmalltalkOOP.Null)

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/PhaseY_InstanceVariableInitialization.cs`

---

### Guarantee #9: Method Dictionary Allocation

**Requirement**: Allocate method dictionaries with deterministic sizing.

**Implementation**:
- **Phase 7** (`Phase7_MethodDictionaryAllocation.cs`): Allocates dicts
  - Capacity computed from bundle method counts
  - Deterministic sizing algorithm

**Validation**:
- **Phase 13**: `ValidateMethodDictionaries()` validates capacity and structure

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase7_MethodDictionaryAllocation.cs`

---

### Guarantee #10: Method Installation

**Requirement**:
- Compile/allocate CompiledMethod and ByteArray objects exactly as described by the file-out
- Canonicalize Associations used by methods globally
- All CompiledMethods have valid homeClass reference in fixed slot 0 for correct super send semantics

**Implementation**:
- **Phase 8** (`Phase8_MethodInstallation.cs`): Installs methods
  - Creates CompiledMethod objects with homeClass in fixed slot 0
  - Canonicalizes Associations globally via `_canonicalAssociations`
  - Validates canonicalization (no duplicates)
  - Processes init expressions with same canonicalization

**Validation**:
- **Phase 0**: `ValidateHomeClassMetadata()` validates all methods have HomeClassLocalId
- **Phase 8** (built-in): `ValidateAssociationCanonicalization()`, `ValidateAssociationReachability()`
- **Phase 13**: `ValidateAllMethods()` validates CompiledMethod structure

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase8_MethodInstallation.cs`

---

### Guarantee #11: Method Dictionary Installation

**Requirement**: Install methods into method dictionaries deterministically (hash probing must be deterministic or use file-out placement data).

**Implementation**:
- **Phase 9** (`Phase9_MethodDictionaryInstallation.cs`): Installs methods
  - Linear probe search (deterministic given stable bundle order)
  - Updates tally correctly
  - Handles selector replacement

**Validation**:
- **Phase 13**: `ValidateMethodDictionaries()` validates all methods are findable

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase9_MethodDictionaryInstallation.cs`

**Note**: Linear probe is deterministic with stable bundle. For absolute guarantees, bundle v1.0.7 could store exact placement.

---

### Guarantee #12: USTable and Globals Population

**Requirement**: Populate USTable and Smalltalk dictionary using canonical Association objects.

**Implementation**:
- **Phase 11** (`Phase11_USTablePopulation.cs`): Populates USTable
- **Phase 12** (`Phase12_GlobalVariableInstallation.cs`): Populates SystemDictionary
  - Uses canonical Associations from Phase 8

**Validation**:
- **Phase 13**: `ValidateSpecialObjects()` validates SystemDictionary exists

**Code Location**: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase11_USTablePopulation.cs`, `Phase12_GlobalVariableInstallation.cs`

---

### Guarantee #13: Init Expression Execution

**Requirement**: Execute init expressions (if the running image does) in the same order and environment the file-out expects.

**Implementation**:
- **Phase 8** (merged from Phase 8b): Creates init expression CompiledMethods
  - Stores in InitExpressionMethods array in bundle order
- **Runtime** (`InitExpressionExecutor.cs`): Executes in array order
  - Executes init:0, init:1, init:2, ... sequentially
  - Uses same VM execution environment as regular methods

**Validation**:
- **Phase 13**: `ValidateInitExpressions()` validates init expression array structure

**Code Location**:
- Preparation: `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase8_MethodInstallation.cs:146-199`
- Execution: `Smalltalk80.Interpreter/Engine/InitExecution/InitExpressionExecutor.cs`

---

## Validation Summary

| Invariant/Guarantee | Phase 0 Validation | Phase 13 Validation | Built-in Validation |
|---------------------|-------------------|---------------------|---------------------|
| #1: Non-zero class slots | - | ✓ ValidateAllClasses | Phase X logs |
| #2: Metaclass circle | ✓ ValidateMetaclassCircle | ✓ ValidateCrossReferences | - |
| #3: Class layouts | ✓ ValidatePrecomputedLayouts | ✓ ValidateAllClasses | - |
| #4: Ivar initialization | - | ✓ ValidateAllClasses/Methods | Phase Y logs |
| #5: Canonical objects | - | - | Phase 8 canonicalization |
| #6: Method dictionaries | - | ✓ ValidateMethodDictionaries | Phase 9 tally updates |
| #7: SpecialObjectsArray | ✓ ValidateSpecialObjectsArrayDef | ✓ ValidateSpecialObjectsArray | - |
| #8: GC reachability | - | - | ✓ Phase 15 GC (0 collected) |
| G1: Symbol interning | - | - | Phase 1 frozen registry |
| G2: Class shells first | - | - | Phase ordering |
| G3: Metaclass wiring | ✓ ValidateMetaclassCircle | ✓ ValidateCrossReferences | Phase 3 logs |
| G4: Class metadata | - | ✓ ValidateAllClasses | Phase 4 logs |
| G5: Layout loading | ✓ ValidatePrecomputedLayouts | - | Phase 5 logs |
| G6: Singletons | ✓ ValidateSpecialObjectsArrayDef | ✓ ValidateSpecialObjectsArray | Phase 6 logs |
| G7: Class refs | - | ✓ ValidateAllClasses/Methods | Phase X logs |
| G8: Ivar init | - | ✓ ValidateAllClasses/Methods | Phase Y logs |
| G9: MethodDict alloc | - | ✓ ValidateMethodDictionaries | Phase 7 logs |
| G10: Method install | ✓ ValidateHomeClassMetadata | ✓ ValidateAllMethods | Phase 8 canonicalization |
| G11: MethodDict install | - | ✓ ValidateMethodDictionaries | Phase 9 tally |
| G12: USTable/Globals | - | ✓ ValidateSpecialObjects | Phase 11/12 logs |
| G13: Init expressions | - | ✓ ValidateInitExpressions | Execution logs |

---

## Future Enhancements

### Bundle Schema v1.0.7 (Proposed)

To provide absolute determinism guarantees for method dictionary placement:

1. **Add to ClassDescriptor**:
   ```json
   {
     "methodDictPlacement": {
       "type": "array",
       "items": {
         "selectorIndex": "integer",
         "slotIndex": "integer"
       }
     }
   }
   ```

2. **Modify Phase 9**: Use placement data instead of linear probe

3. **Benefits**:
   - Eliminates any non-determinism from hash probe order
   - Allows verification of exact slot placement
   - Enables future optimizations (perfect hashing, etc.)

---

## References

- [CLAUDE.md](../CLAUDE.md) - Developer principles and loader invariants
- [Interpreter-Architecture.md](Interpreter-Architecture.md) - VM architecture overview
- [COLD-START-DESIGN-AND-IMPLEMENTATION.md](../COLD-START-DESIGN-AND-IMPLEMENTATION.md) - Cold start design document
- [Bundle Schema v1.0.6](../Smalltalk80.Compiler/Schema/coldstart-v1.0.6.schema.json) - Current bundle format
