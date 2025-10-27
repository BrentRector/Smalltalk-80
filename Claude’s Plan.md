Claude’s Plan
6-Phase Cold Start Loader Refactoring Plan
Executive Summary
Consolidate the current 17-phase loader (Phase0 through Phase16, plus PhaseX and PhaseY) into a streamlined 6-phase architecture. This simplification is now possible because the compiler emits blocks in post-order (forward references only), enabling single-pass method compilation. Primary Goal: Code simplification (reduce complexity, improve maintainability) Approach: Big-bang rewrite with comprehensive validation Timeline: 10-13 hours of focused work
Current State Analysis
Current 17 Phases (Too Complex!)
Phase 0:  Preflight Validation
Phase 1:  Symbol Interning → SymbolRegistry.Freeze()
Phase 2:  Class Shells
Phase 3:  Metaclass Wiring
Phase 4:  Class Resolution
Phase 5:  Layout Loading → LayoutResolver.Freeze()
Phase 6:  Object Allocation (nil/true/false singletons)
Phase X:  Class Reference Initialization
Phase Y:  Instance Variable Initialization
Phase 7:  Method Dictionary Allocation
Phase 8:  Method Installation (2-pass: shells → literals)
Phase 8b: Init Method Creation
Phase 9:  Method Dictionary Installation
Phase 10: Patch Application
Phase 11: USTable Population
Phase 12: Global Variable Installation
Phase 13: Comprehensive Validation
[Phase 14-15: GC setup/verification - inline in Loader.cs]
Problems:
Too many phases (cognitive load)
Multi-pass patterns (Phase 8, PhaseX/PhaseY)
Unclear dependencies (why PhaseX before Phase 7?)
Scattered responsibilities (Phase 8 + Phase 8b)
New 6-Phase Architecture
Phase 1: Preflight & Symbols
Validate bundle and allocate all symbols. Consolidates: Phase 0 + Phase 1 Responsibilities:
Bundle validation (version, schema, consistency)
Symbol allocation (ByteIndexed objects)
SymbolRegistry population and freeze
Output: 3,834 symbols allocated and registered Validation Checkpoint: All symbols allocated, SymbolRegistry frozen
Phase 2: Class Infrastructure
Allocate classes, metaclasses, singletons, and load precomputed layouts. Consolidates: Phase 2 + Phase 3 + Phase 4 + Phase 5 + Phase 6 Responsibilities:
Allocate all class shells (480 classes)
Allocate all metaclass shells (480 metaclasses)
Wire metaclass.thisClass relationships
Wire metaclass circle (meta:Object.superclass = class:Class)
Allocate sentinels: nil, true, false (class slot = 0 initially - relaxed invariant)
Register sentinels in SpecialObjectsArray
Populate class metadata (name, format, superclass)
Load precomputed layouts from bundle into LayoutResolver
Freeze LayoutResolver
Output: 960 class/metaclass objects, 3 sentinels, layouts ready Validation Checkpoint: All classes allocated, metaclass circle wired, layouts loaded, sentinels exist Relaxed Invariant: Sentinels (nil/true/false) have class slot = 0 until Phase 3 sets them
Phase 3: Superclass Wiring & Sentinel Finalization
Complete class hierarchy and finalize sentinels. Consolidates: Part of old Phase 4 + Phase X partial + Phase Y partial Responsibilities:
Resolve and set all superclass references (except Object.superclass - deferred to Phase 6)
Set class slots for sentinels (nil.class = UndefinedObject, true.class = True, false.class = False)
Mark sentinel fields in _initializedFields
Output: Complete superclass chains, sentinels fully initialized Validation Checkpoint: Superclass chains correct, sentinel class slots set
Phase 4: Method Compilation (SINGLE-PASS! 🎉)
Allocate and compile all methods and their bytecode arrays. Consolidates: Phase 7 + Phase 8 + Phase 8b Responsibilities:
Allocate method dictionaries for all classes (using precomputed capacities from bundle)
SINGLE-PASS method compilation (enabled by post-order blocks):
Iterate all 10,182 methods in bundle order
For each method:
Decode bytecodes
Resolve literals (symbols, classes, blocks - all forward refs!, associations)
Canonicalize associations ( _canonicalAssociations.GetOrAdd())
Allocate ByteArray for bytecodes
Allocate CompiledMethod with resolved literals + bytecode reference
Set homeClass field (fixed slot 0)
Register in _localIdToObjectId
Build blockOwnerMap for runtime
Create init expression methods array
Output: 10,182 CompiledMethod objects, 480 MethodDictionaries (empty), init methods array Validation Checkpoint:
All methods compiled
All block references resolved (forward-only)
Association canonicalization validated
blockOwnerMap complete
Key Innovation: No more two-pass! The compiler's post-order emission means all block references point to already-processed methods.
Phase 5: Method Dictionaries & Globals
Install methods and populate global variables. Consolidates: Phase 9 + Phase 10 + Phase 11 + Phase 12 Responsibilities:
Install CompiledMethods into class MethodDictionaries (open addressing with linear probing)
Set class.methodDict references
Apply patches (circular reference fixes)
Populate USTable with all symbols (Smalltalk-80 canonical hash algorithm)
Create and populate SystemDictionary (Smalltalk) with:
All class globals
Canonical associations
Self-reference (Smalltalk at: #Smalltalk → SystemDictionary)
Populate class pools
Register InitExpressionMethods in SpecialObjectsArray
Output: All methods findable via lookup, globals accessible, USTable complete Validation Checkpoint: Dictionary integrity, global accessibility, patch application successful
Phase 6: Finalization & Validation
Finalize all references, initialize unset fields, validate heap. Consolidates: Phase X + Phase Y + Phase 13 + Loader.cs inline code (Phase 14-15) Responsibilities: Step 1: Final Reference Initialization
Set Object.superclass = nil (final superclass link)
Set all class slots (slot 0) for non-class objects:
Symbols → class:Symbol
CompiledMethods → class:CompiledMethod
MethodDictionaries → class:MethodDictionary
ByteArrays → class:ByteArray
Pool dictionaries → class:Dictionary
Step 2: Instance Variable Initialization
Iterate all objects
For each instance variable slot not in _initializedFields:
Set to nilOOP
Discard _initializedFields (no longer needed)
Step 3: Comprehensive Validation
Non-zero class slots: All objects have valid class references
Metaclass circle integrity: meta:Object.superclass = class:Class → ... → class:Object
Superclass chain termination: Object.superclass = nil
Class layouts: Match precomputed bundle layouts
Method dictionary integrity:
Tally matches actual method count
All methods findable via lookup
4693/4693 methods accessible
SpecialObjectsArray integrity:
nil has class UndefinedObject
true has class True
false has class False
Smalltalk global exists
InitExpressionMethods is Array
Association canonicalization: All method literals share canonical instances
Step 4: GC Root Setup
Register GC roots: nil, true, false, Smalltalk, InitExpressionMethods
Step 5: GC Verification
Run full garbage collection
CRITICAL: 0 objects collected (all objects reachable)
If any objects collected → FAIL (heap corruption)
Output: Fully initialized, validated heap ready for runtime Validation Checkpoint: ALL invariants satisfied, 0 objects collected by GC Error Reporting: On failure, report:
Exact OOP address
LocalId (if available)
Expected vs actual values
Which invariant violated
Detailed Phase Mapping
Old Phase → New Phase Mapping
Old Phase	Responsibility	New Phase	Notes
Phase 0	Preflight Validation	Phase 1	Bundle consistency checks
Phase 1	Symbol Interning	Phase 1	Freeze after interning
Phase 2	Class Shells	Phase 2	Allocate 960 class/metaclass objects
Phase 3	Metaclass Wiring	Phase 2	Wire thisClass and metaclass circle
Phase 4	Class Resolution	Phase 2	Populate class metadata
Phase 5	Layout Loading	Phase 2	Load precomputed layouts, freeze
Phase 6	Object Allocation	Phase 2	Allocate nil/true/false singletons
Phase X	Class References	Phase 3 + Phase 6	Split: sentinels in Phase 3, rest in Phase 6
Phase Y	Instance Variable Init	Phase 6	Initialize unset slots to nil
Phase 7	MethodDict Allocation	Phase 4	Allocate empty dictionaries
Phase 8	Method Installation	Phase 4	SINGLE-PASS compilation
Phase 8b	Init Method Creation	Phase 4	Create init expression methods
Phase 9	MethodDict Installation	Phase 5	Install methods into dictionaries
Phase 10	Patch Application	Phase 5	Fix circular references
Phase 11	USTable Population	Phase 5	Populate universal symbol table
Phase 12	Global Variables	Phase 5	Install globals into Smalltalk dict
Phase 13	Validation	Phase 6	Comprehensive heap validation
(inline)	GC Root Setup	Phase 6	Register GC roots
(inline)	GC Verification	Phase 6	Final GC test (0 collected)
File Changes
Files to CREATE (6 new phase files)
Phase1_PreflightAndSymbols.cs
Merges: Phase0_Preflight + Phase1_SymbolInterning
~300 lines
Phase2_ClassInfrastructure.cs
Merges: Phase2_ClassShells + Phase3_MetaclassWiring + Phase4_ClassResolution + (Phase5 layout loading) + Phase6_ObjectAllocation (singletons only)
~600 lines (largest phase)
Phase3_SuperclassAndSentinels.cs
Merges: Superclass wiring from old Phase4 + Sentinel finalization from PhaseX
~200 lines
Phase4_MethodCompilation.cs
Merges: Phase7_MethodDictionaryAllocation + Phase8_MethodInstallation + Phase8b_InitMethodCreation
~500 lines
KEY CHANGE: Single-pass compilation (no shell allocation pass)
Phase5_DictionariesAndGlobals.cs
Merges: Phase9_MethodDictionaryInstallation + Phase10_PatchApplication + Phase11_USTablePopulation + Phase12_GlobalVariableInstallation
~400 lines
Phase6_FinalizationAndValidation.cs
Merges: PhaseX_ClassReferenceInitialization + PhaseY_InstanceVariableInitialization + Phase13_ComprehensiveValidation + GC setup/verification
~700 lines (second largest)
Files to DELETE (17 old phase files)
Phase0_Preflight.cs
Phase1_SymbolInterning.cs
Phase2_ClassShells.cs
Phase3_MetaclassWiring.cs
Phase4_ClassResolution.cs
Phase6_ObjectAllocation.cs
Phase7_MethodDictionaryAllocation.cs
Phase8_MethodInstallation.cs
Phase8b_InitMethodCreation.cs
Phase9_MethodDictionaryInstallation.cs
Phase10_PatchApplication.cs
Phase11_USTablePopulation.cs
Phase12_GlobalVariableInstallation.cs
Phase13_ComprehensiveValidation.cs
Phase16_InitExecution.cs
PhaseX_ClassReferenceInitialization.cs
PhaseY_InstanceVariableInitialization.cs
Total reduction: 17 files → 6 files (65% reduction)
Files to MODIFY
Loader.cs
Rewrite Run() method to call 6 new phases
Remove Phase 14-15 inline code (moves to Phase 6)
Update comments and documentation
~100 lines modified
ClassHierarchyUtils.cs
Keep utility methods (may need minor updates)
LayoutResolver.cs
No changes needed (API stays same)
SemanticAllocator.cs
No changes needed (API stays same)
Validation Strategy
Relaxed Invariants During Cold Start
Track "Bootstrap Mode" to defer some validations: Phases 1-2 (BootstrapMode = true):
Allow class slot = 0 for sentinels (nil/true/false)
Allow uninitialized instance variable slots
Phase 3:
Set sentinel class slots
BootstrapMode still true (other objects still have class slot = 0)
Phase 6 (BootstrapMode = false before validation):
Set all remaining class slots
Initialize all remaining instance variables
THEN enforce ALL invariants
Validation Checkpoints
After each phase, validate phase-specific postconditions: Phase 1: All symbols allocated, SymbolRegistry frozen Phase 2: All classes allocated, layouts loaded, sentinels exist Phase 3: Superclasses wired, sentinel class slots set Phase 4: All methods compiled, associations canonical, blockOwnerMap built Phase 5: All methods installed, dictionaries correct, globals accessible Phase 6: ALL invariants satisfied, heap ready for runtime
Error Handling
Fail-fast with detailed context:
Cold start failed at Phase 4: Method Compilation
  Error: Failed to resolve block literal
  Method: method:Point>>= (localId: method:Point:=)
  Literal index: 2
  Literal type: compiledMethod
  Block localId: block:abc123...
  Expected: Block at position AFTER current method
  Actual: Block not found in _localIdToObjectId
  
Diagnostic context:
  - Method position in bundle: 1234
  - Total methods processed: 1234/10182
  - Blocks resolved so far: 1500
Implementation Steps
Step 1: Create New Phase Files
Write all 6 new phase classes:
Phase1_PreflightAndSymbols.cs
Phase2_ClassInfrastructure.cs
Phase3_SuperclassAndSentinels.cs
Phase4_MethodCompilation.cs
Phase5_DictionariesAndGlobals.cs
Phase6_FinalizationAndValidation.cs
Each file includes:
Comprehensive class-level documentation
Clear method-level comments
Inline explanations for non-obvious logic
Validation checkpoints after major steps
Step 2: Update Loader.cs
Rewrite Run() method:
public void Run()
{
    Console.WriteLine("\n=== Cold Start Loader (6-Phase) ===\n");

    // Phase 1: Preflight & Symbols
    RunPhase("Phase 1: Preflight & Symbols", () =>
    {
        var phase = new Phase1_PreflightAndSymbols(_bundle, _allocator);
        phase.Run();
        SymbolRegistry.Freeze();
    });

    // Phase 2: Class Infrastructure
    RunPhase("Phase 2: Class Infrastructure", () =>
    {
        var phase = new Phase2_ClassInfrastructure(_bundle, _allocator, _localIdToObjectId, _heapManager, _initializedFields);
        phase.Run();
        _layoutResolver = phase.LayoutResolver;  // Capture layout resolver
    });

    // Phase 3: Superclass Wiring & Sentinel Finalization
    RunPhase("Phase 3: Superclass Wiring & Sentinel Finalization", () =>
    {
        var phase = new Phase3_SuperclassAndSentinels(_bundle, _allocator, _localIdToObjectId, _heapManager, _initializedFields);
        phase.Run();
    });

    // Phase 4: Method Compilation (SINGLE-PASS!)
    RunPhase("Phase 4: Method Compilation", () =>
    {
        var phase = new Phase4_MethodCompilation(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols, _bundle.Strings);
        phase.Run();
    });

    // Phase 5: Method Dictionaries & Globals
    RunPhase("Phase 5: Method Dictionaries & Globals", () =>
    {
        var phase = new Phase5_DictionariesAndGlobals(_bundle, _allocator, _localIdToObjectId, _bundle.Symbols);
        phase.Run();
    });

    // Phase 6: Finalization & Validation
    RunPhase("Phase 6: Finalization & Validation", () =>
    {
        var phase = new Phase6_FinalizationAndValidation(_bundle, _allocator, _localIdToObjectId, _heapManager, _initializedFields, _bundle.Symbols);
        phase.Run();
        // Phase 6 includes GC verification - heap is fully validated
    });

    Console.WriteLine("\n=== Cold Start Complete! ===\n");
}
Step 3: Delete Old Phase Files
cd Smalltalk80.Interpreter/Engine/LoaderPhases
rm Phase0_Preflight.cs \
   Phase1_SymbolInterning.cs \
   Phase2_ClassShells.cs \
   Phase3_MetaclassWiring.cs \
   Phase4_ClassResolution.cs \
   Phase6_ObjectAllocation.cs \
   Phase7_MethodDictionaryAllocation.cs \
   Phase8_MethodInstallation.cs \
   Phase8b_InitMethodCreation.cs \
   Phase9_MethodDictionaryInstallation.cs \
   Phase10_PatchApplication.cs \
   Phase11_USTablePopulation.cs \
   Phase12_GlobalVariableInstallation.cs \
   Phase13_ComprehensiveValidation.cs \
   Phase16_InitExecution.cs \
   PhaseX_ClassReferenceInitialization.cs \
   PhaseY_InstanceVariableInitialization.cs
Step 4: Update Documentation
Update CLAUDE.md with new phase structure:
## Cold Start Phases (6-Phase Architecture)

1. **Phase 1: Preflight & Symbols** - Validate bundle, allocate symbols
2. **Phase 2: Class Infrastructure** - Classes, metaclasses, layouts, sentinels
3. **Phase 3: Superclass Wiring & Sentinel Finalization** - Complete class hierarchy
4. **Phase 4: Method Compilation** - Single-pass method compilation (post-order blocks)
5. **Phase 5: Method Dictionaries & Globals** - Install methods, populate globals
6. **Phase 6: Finalization & Validation** - Finalize references, validate heap, GC test
Step 5: Test & Validate
Build:
cd Smalltalk80.Interpreter
dotnet build
Run Cold Start:
cd bin/Debug/net9.0
./Smalltalk80.Interpreter.exe
Expected Output:
=== Cold Start Loader (6-Phase) ===

Phase 1: Preflight & Symbols... ✓
Phase 2: Class Infrastructure... ✓
Phase 3: Superclass Wiring & Sentinel Finalization... ✓
Phase 4: Method Compilation... ✓
Phase 5: Method Dictionaries & Globals... ✓
Phase 6: Finalization & Validation...
  Validating heap integrity...
  GC Verification: 0/29513 objects collected
✓

=== Cold Start Complete! ===
Validation Criteria: ✅ All 6 phases complete successfully
✅ Same object count as before (29,513 objects)
✅ 0 objects collected by GC
✅ All validations pass
✅ Method dictionary integrity: 4693/4693 methods findable
✅ Association canonicalization: Same statistics as before
Feature Branch Strategy
Branch Name: refactor/6-phase-loader Commit Strategy:
git checkout -b refactor/6-phase-loader

# Commit 1: Create new phase files
git add Smalltalk80.Interpreter/Engine/LoaderPhases/Phase1_PreflightAndSymbols.cs
git add Smalltalk80.Interpreter/Engine/LoaderPhases/Phase2_ClassInfrastructure.cs
# ... (all 6 new files)
git commit -m "feat: Create 6 new consolidated loader phases"

# Commit 2: Update Loader.cs
git add Smalltalk80.Interpreter/Engine/Loader.cs
git commit -m "feat: Update Loader.cs to use 6-phase architecture"

# Commit 3: Delete old files
git rm Smalltalk80.Interpreter/Engine/LoaderPhases/Phase0_Preflight.cs
# ... (all 17 old files)
git commit -m "refactor: Remove old 17-phase files"

# Commit 4: Update documentation
git add CLAUDE.md
git commit -m "docs: Update CLAUDE.md with 6-phase architecture"

# Commit 5: Test results
git add test_results.md
git commit -m "test: Add validation results for 6-phase loader"
Merge Strategy:
Direct merge to main after all validations pass
Tag as v1.1.0-loader-refactor after merge
Architecture Diagrams
Before: 17-Phase Pipeline (Too Complex)
Phase 0: Preflight
    ↓
Phase 1: Symbols → SymbolRegistry.Freeze
    ↓
Phase 2: Class Shells
    ↓
Phase 3: Metaclass Wiring
    ↓
Phase 4: Class Resolution
    ↓
Phase 5: Layout Loading → LayoutResolver.Freeze
    ↓
Phase 6: Singleton Allocation
    ↓
Phase X: Class References (multi-pass 1)
    ↓
Phase Y: Instance Variable Init (multi-pass 2)
    ↓
Phase 7: MethodDict Allocation
    ↓
Phase 8: Method Installation (2-pass: shells + literals)
    ↓
Phase 8b: Init Method Creation
    ↓
Phase 9: MethodDict Installation
    ↓
Phase 10: Patch Application
    ↓
Phase 11: USTable Population
    ↓
Phase 12: Global Variables
    ↓
Phase 13: Validation
    ↓
(inline) GC Setup + Verification
Problems:
17 separate phases
Multi-pass patterns (Phase 8, PhaseX/Y)
Scattered responsibilities
Unclear dependencies
After: 6-Phase Pipeline (Clean & Simple)
Phase 1: Preflight & Symbols
  ├─ Bundle validation
  ├─ Symbol allocation
  └─ SymbolRegistry.Freeze ✓

Phase 2: Class Infrastructure
  ├─ Class/metaclass allocation (960 objects)
  ├─ Metaclass wiring (thisClass + circle)
  ├─ Sentinel allocation (nil/true/false)
  ├─ Class metadata population
  ├─ Layout loading (precomputed)
  └─ LayoutResolver.Freeze ✓

Phase 3: Superclass & Sentinels
  ├─ Superclass wiring
  └─ Sentinel class slots ✓

Phase 4: Method Compilation
  ├─ MethodDict allocation
  ├─ SINGLE-PASS method compilation
  │   ├─ Decode bytecodes
  │   ├─ Resolve literals (forward blocks!)
  │   ├─ Canonicalize associations
  │   └─ Allocate CompiledMethod + ByteArray
  ├─ BlockOwnerMap creation
  └─ Init method creation ✓

Phase 5: Dictionaries & Globals
  ├─ Method installation
  ├─ Patch application
  ├─ USTable population
  ├─ SystemDictionary (Smalltalk) creation
  └─ Global variable installation ✓

Phase 6: Finalization & Validation
  ├─ Set all class slots
  ├─ Initialize instance variables to nil
  ├─ Comprehensive validation:
  │   ├─ Non-zero class slots
  │   ├─ Metaclass circle
  │   ├─ Superclass chains
  │   ├─ Method dictionary integrity
  │   ├─ SpecialObjectsArray
  │   └─ Association canonicalization
  ├─ GC root setup
  └─ GC verification (0 collected) ✓
Benefits:
6 clear phases (65% reduction)
Single-pass per phase
Clear phase boundaries
Explicit dependencies
Validation at end (simpler reasoning)
Success Criteria
✅ Code Simplification: 17 files → 6 files (65% reduction)
✅ Semantic Equivalence: All init expressions pass, all tests pass (deferred runtime issues)
✅ Validation Passing: All 8 invariants + 13 guarantees satisfied
✅ Performance: No regression (cold start time ≤ current time)
✅ Maintainability: Clear phase boundaries, documented dependencies
✅ Single-Pass Method Compilation: Enabled by post-order block emission
Timeline Estimate
Phase 1 file creation: 1 hour (simple merge)
Phase 2 file creation: 2 hours (largest, most complex)
Phase 3 file creation: 1 hour (straightforward)
Phase 4 file creation: 2 hours (single-pass rewrite)
Phase 5 file creation: 1.5 hours (multiple merges)
Phase 6 file creation: 2 hours (validation consolidation)
Loader.cs update: 30 minutes
Testing & validation: 2 hours
Documentation: 1 hour
Total: 13 hours of focused work
Risk Mitigation
Risk: New phase code has subtle bugs
Mitigation: Comprehensive validation in Phase 6, fail-fast error reporting Risk: Performance regression
Mitigation: Measure cold start time before/after Risk: GC verification fails (objects collected)
Mitigation: Detailed diagnostic output showing which objects are unreachable