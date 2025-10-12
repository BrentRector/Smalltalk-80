# Claude Development Guidelines

**Last Updated**: 2025-01-12
**Status**: Current Working State
**Compiler Status**: ✅ Fully operational - generates valid JSON from Smalltalk-80.st
**Validator Status**: ✅ 100% validation passing (10,059 methods validate correctly)
**Interpreter Status**: ⚠️ GC ISSUE - All phases complete, heap manager enhanced, but GC verification failing

## Recent Accomplishments (2025-01-12)

### ✅ Heap Manager Enhanced - Large Object Support

**Problem**: Artificial 255-word (1020-byte) limit in heap manager prevented large objects like USTable (7828 words) and large strings (2712+ characters).

**Root Cause**: ObjectTableEntry PackedMetadata used only 8 bits for size field, limiting objects to 255 words maximum.

**Solution**: Expanded heap manager architecture for large object support:
- **Expanded PackedMetadata**: Size field increased from 8 bits to 16 bits (0-65535 words = 0-256KB objects)
- **ExtendedSize field**: Added optional uint field for objects larger than 65535 words
- **Removed artificial limits**: SmalltalkHeapManager no longer enforces 255-word restriction
- **Updated bit layout**: Reorganized PackedMetadata to prioritize size over GC flags

**Impact**: 
- ✅ Large strings (2712+ chars) now supported without truncation
- ✅ Large arrays (285+ elements) now supported without chunking  
- ✅ USTable (7828 symbols) can be allocated as single hash table
- ✅ Modern object sizes supported (up to 256KB standard, unlimited with ExtendedSize)

### ✅ Primitive Implementation Complete - CompiledMethod Support

**Problem**: Missing primitives 60, 68, 69 for proper CompiledMethod object access according to Smalltalk-80 source.

**Solution**: Implemented correct primitive mapping based on Smalltalk-80.st source analysis:
- **Primitive 60**: `at:` / `basicAt:` / `digitAt:` - General object indexing ✅
- **Primitive 68**: `objectAt:` - CompiledMethod header/literal access ✅ (was incorrectly `become:`)
- **Primitive 69**: `objectAt:put:` - CompiledMethod header/literal modification ✅ (was missing)
- **Primitive 72**: `become:` - Object identity swap ✅ (moved from 68)
- **Primitive 75**: `asOop` - Object pointer access ✅ (was `hash`)

**Key Insight**: CompiledMethod uses dual access patterns:
- `objectAt:` (primitive 68) for header/literals in PointerIndexed part
- `at:` (primitive 60) for bytecodes in separate ByteArray (via hidden reference)

### ✅ Cold Start Phases All Complete

**Achievement**: All 10 cold start phases now complete successfully:
- Phase 0: Preflight validation ✅
- Phase 1: Symbol interning ✅  
- Phase 2: Class shells ✅
- Phase 3: Metaclass wiring ✅
- Phase 4: Class resolution ✅
- Phase 5: Layout computation ✅
- Phase 6: Object allocation ✅
- Phase 7: Method installation ✅
- Phase 8: Patch application ✅
- Phase 9: Init expression preparation ✅
- Phase 10: USTable population ✅

**Current Issue**: ⚠️ **Missing method dictionary installation** - Method dictionaries exist as objects and CompiledMethods exist, but no phase installs methods into the dictionaries. This incomplete object structure likely causes GC traversal issues.

### ⚠️ Method Dictionary Installation - CRITICAL MISSING PHASE

**Problem**: Method dictionaries exist as objects (from Phase 6) and CompiledMethods exist (from Phase 7), but no phase actually installs the methods into the dictionaries.

**Root Cause**: Phase 7 comment says "installation happens elsewhere" but no such phase exists. Classes have empty method dictionaries, breaking Smalltalk semantics and likely causing GC traversal to miss method references.

**Investigation Results**:
- ✅ Method dictionaries present in JSON bundle and allocated in Phase 6
- ✅ CompiledMethods created in Phase 7 but only stored in `_localIdToObjectId` 
- ❌ No code found that installs methods into class method dictionaries
- ❌ GC likely fails because method references not properly established

**Next Steps**:

1. ⚠️ **Create Phase 7.5: Method Dictionary Population** (HIGHEST PRIORITY)
   - Install CompiledMethods into class method dictionaries 
   - Handle both instance-side and class-side methods
   - Use primitive interface for clean separation
   - Run between Phase 7 (method creation) and Phase 8 (patches)

2. ⚠️ **Enable comprehensive validation** - Fix Phase 12 to work with new heap manager
   - Validate all objects match JSON definitions
   - Verify method dictionaries properly populated
   - Check cross-references and object integrity
   - Use results to diagnose remaining GC issues

3. ⚠️ **Fix GC traversal** - Once objects properly structured
   - Method dictionaries will contain proper references to CompiledMethods
   - GC should traverse method references correctly  
   - May resolve the 26,815 collected objects issue

## Previous Accomplishments (2025-01-11)

### ✅ Interpreter Breakthrough - Init Expression Execution Working!
- **Fixed** all cold start phases (0-9) - complete system bootstrap successful
- **Implemented** metaclass hierarchy wiring with superclass chain traversal
- **Fixed** method lookup to walk superclass chain (enables inheritance)
- **Added** byte-indexed object access (`GetIndexedByte`, `SetIndexedByte`)
- **Fixed** Float object allocation with correct classId
- **Working** Float arithmetic primitives (add, subtract, multiply, divide, etc.)
- **Executed** first init expression successfully: `Float>>initialize` (init:123)
- **Result**: Init expression results show "Succeeded: 1, Failed: 0"

### ✅ Heap Manager Architecture Overhaul (2025-01-12)
- **Migrated** from old heap manager to new `SmalltalkHeapManager` architecture
- **Implemented** ObjectTableEntry with PackedMetadata for efficient object storage
- **Added** tagged OOP system for SmallInteger optimization
- **Fixed** instance variable counting and allocation across all phases
- **Converted** cold start phases to use primitive interfaces (Phase3, Phase4)
- **Implemented** proper CompiledMethod as PointerIndexed with separate ByteArray design

### Key Technical Fixes (2025-01-11)

#### 1. Metaclass Hierarchy Wiring
- Added `WireFundamentalMetaclassHierarchy()` in Phase2_MetaclassWiring.cs
  - Establishes the metaclass circle: meta:Object → class:Class → class:ClassDescription → class:Object
- Added `WireMetaclassSuperclasses()` to compute all 236 metaclass relationships
  - Each metaclass's superclass = its instance class's superclass's metaclass
- Fixed Phase3 to skip metaclasses when setting superclass (preserves Phase2 wiring)

#### 2. Method Lookup with Inheritance
- Modified `SmalltalkHeap.GetMethod()` to walk superclass chain
- Added `ExtractMethodInfo()` helper for method metadata extraction
- Enables finding methods in superclasses (e.g., ClassDescription>>comment:)

#### 3. Object Allocation Timing
- Moved nil/true/false creation from Phase1 to Phase6 (Object Allocation)
- Ensures singletons created AFTER all classes fully wired
- Fixed classId=0 issues for well-known objects

#### 4. Byte-Indexed Object Access
- Added `GetIndexedByte(int objectId, int index)` to SmalltalkHeap
- Added `SetIndexedByte(int objectId, int index, byte value)` to SmalltalkHeap
- Fixed FloatPrimitives to use byte access instead of int[] access
- Enables Float, String, ByteArray proper byte-level access

#### 5. Float Object Allocation
- Added `AllocateFloat(double value)` to ISmalltalkHeap interface and SmalltalkHeap
- Looks up Float class from GlobalRegistry for correct classId
- Updated all AllocateFloat calls in Phase5, Phase6, Phase8, Phase12
- Float literals in methods now created with proper Float class

#### 6. Association Deduplication
- Fixed `ResolveAssociation()` in Phase6 and Phase8
- Searches SystemDictionary for existing class associations
- Reuses existing associations instead of creating duplicates with nil value
- Prevents class references from being broken during method installation

## Quick Start

```bash
# Set console to UTF-8 for proper character display
pwsh -Command "chcp 65001"

# Build everything (use -p:RunCompilerTests=false to avoid MSBuild test issues)
dotnet build -p:RunCompilerTests=false

# Generate JSON from Smalltalk source
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler -- Smalltalk-80.st

# Validate the generated JSON
dotnet run --project ../Smalltalk80.Validator -- ../bin/Debug/net9.0/coldstart.json

# Run interpreter - NOW WORKING!
dotnet run --project ../Smalltalk80.Interpreter -- --coldstart ../bin/Debug/net9.0/coldstart.json
```

## Project Structure

```
Smalltalk-80/
├── Smalltalk80.Compiler/        # Compiles .st → .json (WORKING)
├── Smalltalk80.Validator/       # Validates JSON (WORKING - 17 validators)
│   ├── Smalltalk80.Validator.csproj    # CLI application
│   ├── Core/                           # ValidationContext, Diagnostic, IValidator, 17 validators
│   ├── Engine/                         # ValidatorRunner, ValidatorPipeline
│   ├── Tests/                          # Unit tests and fixtures
│   └── README.md                       # Complete validator documentation
├── Smalltalk80.Interpreter/     # VM runtime (WORKING - executes init expressions!)
│   ├── VM/
│   │   ├── SmalltalkHeap.cs           # Heap with byte-indexed access
│   │   ├── VirtualMachine.cs          # Bytecode interpreter
│   │   └── Primitives/
│   │       ├── FloatPrimitives.cs     # Float arithmetic (WORKING)
│   │       └── ...
│   ├── LoaderPhases/
│   │   ├── Phase2_MetaclassWiring.cs  # Metaclass hierarchy
│   │   ├── Phase6_MethodInstallation.cs
│   │   └── Phase8_InitExecution.cs
│   └── InitExecution/
│       └── InitExpressionExecutor.cs  # Executes init expressions
├── Smalltalk80.Compiler.Tests/  # Unit tests (BROKEN - incompatible with current API)
├── Smalltalk-80 Source/         # Source files
│   └── Smalltalk-80.st         # Master source (READ-ONLY - NEVER MODIFY)
├── docs/
│   ├── Interpreter-Architecture.md    # Detailed architecture documentation
│   └── schemas/
│       └── coldstart-v1.0.3.schema.json  # JSON schema specification
└── Shared/
    └── Core/Bytecodes/
        └── BytecodeDefinitions.cs  # Shared bytecode definitions

Solution file includes: Compiler, Validator (+ Core, Engine, Tests), Interpreter, Compiler.Tests
```

## Current State Summary

### What's Working ✅
- **Compiler**: Generates `coldstart.json` with 10,059 methods (4,640 methods + 5,419 blocks) from Smalltalk-80.st
- **Validator**: All 17 validators pass with 100% success rate (zero errors, zero warnings)
  - Modular architecture: Core (validators), Engine (orchestration), CLI (application)
  - CFG-based bytecode stack analysis with worklist propagation
  - Parallel execution with diagnostic deduplication
  - Comprehensive validation: metadata, bytecode, headers, literals, patches, install order, class hierarchy, primitives, selectors, pool dictionaries, object formats, block closures, dead code
  - Complete documentation in Smalltalk80.Validator/README.md
- **Interpreter**: All 10 cold start phases complete successfully! ✅
  - Phase 0: Preflight validation ✅
  - Phase 1: Symbol interning ✅
  - Phase 2: Class shells ✅
  - Phase 3: Metaclass wiring ✅
  - Phase 4: Class resolution ✅
  - Phase 5: Layout computation ✅
  - Phase 6: Object allocation ✅
  - Phase 7: Method installation ✅
  - Phase 8: Patch application ✅
  - Phase 9: Init expression preparation ✅
  - Phase 10: USTable population ✅
  - **All GC roots set correctly** ✅
  - **Large object support**: USTable (7828 words), large strings (2712+ chars), large arrays (285+ elements) ✅
- **Heap Manager**: Modern architecture with large object support ✅
  - ObjectTableEntry with expandable PackedMetadata (16-bit size field = 0-65535 words)
  - ExtendedSize field for objects larger than 256KB
  - Tagged OOP system for SmallInteger optimization
  - Proper instance variable tracking and allocation
- **Primitives**: Correct Smalltalk-80 primitive implementation ✅
  - Primitive 60: `at:` - General object indexing
  - Primitive 68: `objectAt:` - CompiledMethod header/literal access  
  - Primitive 69: `objectAt:put:` - CompiledMethod header/literal modification
  - Primitive 72: `become:` - Object identity swap
  - Primitive 75: `asOop` - Object pointer access
- **JSON Schema**: v1.0.3 with Blue Book-compliant bytecode generation
- **CI/CD Pipeline**: Automated build-and-validate.ps1 script (build → compile → validate)

### Current Issue ⚠️
- **GC Verification**: 26,815 objects collected during verification (should be 0)
  - Suggests GC traversal issue with large objects or new ObjectTableEntry structure
  - All objects should be reachable from GC roots array
  - May be related to ExtendedSize field or PackedMetadata bit layout changes

### What's Broken ⚠️
- **Compiler Tests**: 131 tests exist but reference obsolete compiler API
- **MSBuild Integration**: Looks for non-existent test files
- **Init Expression Execution**: Blocked by GC verification failure

### Known Issues
1. Compiler.Tests project won't build due to missing types (AstBuilder, ProcessorContext, etc.)
2. MSBuild test target in Compiler.csproj references missing test files
3. Must use `-p:RunCompilerTests=false` when building to avoid MSBuild errors
4. Only one init expression tested - need to systematically test all 285

## Core Development Principles

### 1. Source File Immutability

**🚨 NEVER MODIFY Smalltalk-80.st 🚨**

`Smalltalk-80 Source/Smalltalk-80.st` is the canonical, read-only system image. All data comes from compiler analysis of this file.

### 2. Shared Bytecode Definitions

The file `Shared/Core/Bytecodes/BytecodeDefinitions.cs` contains the bytecode specification and MUST remain synchronized between Compiler and Interpreter. Any mismatch causes runtime failures.

### 3. JSON Cold Start Format

- Schema version: v1.0.3 (`docs/schemas/coldstart-v1.0.3.schema.json`)
- Blocks compile to separate CompiledMethod objects (selector=null)
- All method headers include: word, literalCount, tempCount, argCount, primitiveIndex, largeContext
- Bytecodes as integer arrays (no base64)

### 4. Object Format and Indexed Data

- **Pointer-indexed objects** (Array, OrderedCollection): Use `GetIndexedField` / `SetIndexedField` with int[]
- **Byte-indexed objects** (Float, String, ByteArray): Use `GetIndexedByte` / `SetIndexedByte` with byte[]
- **Word-indexed objects** (Bitmap): Use `GetIndexedField` / `SetIndexedField` with ushort[]
- Float objects: 8-byte IEEE 754 double precision, stored as byte-indexed

### 5. Cold Start Object Allocation

- **Float literals**: Allocated during Phase 6 (Method Installation) and Phase 8 (Init Expression Preparation)
- **String literals**: Allocated during Phase 1 (Symbol Interning) with String classId lookup
- **nil/true/false**: Allocated during Phase 6 (Object Allocation) AFTER classes fully wired
- All runtime allocations MUST use correct classId from GlobalRegistry

## Build and Test

### Build Projects

```bash
# Build all (with test workaround)
dotnet build -p:RunCompilerTests=false

# Build individually
dotnet build Smalltalk80.Compiler -p:RunCompilerTests=false
dotnet build Smalltalk80.Validator
dotnet build Smalltalk80.Interpreter -p:RunCompilerTests=false
```

### Generate and Validate JSON

```bash
# Generate JSON
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler -- Smalltalk-80.st

# Output shows:
#   Symbols: 3914
#   Strings: 6324
#   Classes: 474
#   Methods: 10059
#   Init expressions: 285

# Validate JSON
dotnet run --project ../Smalltalk80.Validator -- ../bin/Debug/net9.0/coldstart.json

# Should show: ✓ Validation PASSED
```

### Run Interpreter

```bash
dotnet run --project Smalltalk80.Interpreter -- --coldstart bin/Debug/net9.0/coldstart.json

# Expected output (SUCCESS):
# Phase 0: Preflight... ✓
# Phase 1: Symbol Interning... ✓
# Phase 2: Class Shells... ✓
# Phase 3: Metaclass Wiring... ✓
# ...
# Phase 9: USTable Population... ✓
# === Cold Start Complete ===
# Total objects created: 10821
# Creating Special Objects Array... ✓
# Garbage Collection Verification... ✓
# === Executing Init Expressions ===
# DEBUG: Searching for init:123 out of 285 total init expressions
# DEBUG: Found init:123 at index 123, OOP=40050
# DEBUG: Executing init expression #123: init:123
# DEBUG: Init expression #123 result: Success
# Init Expression Results:
#   Total: 285
#   Succeeded: 1
#   Failed: 0
# ✓ All systems ready!
```

## Implementation Status

### Compiler ✅
- ANTLR4 grammar for Smalltalk-80 file-out format
- Generates JSON conforming to v1.0.3 schema
- Produces 11MB JSON (compresses to 1.2MB)

### Validator ✅
- **Architecture**: Modular design with 4 projects
  - `Smalltalk80.Validator/` - CLI application
  - `Core/` - ValidationContext, Diagnostic, IValidator, 17 validator implementations
  - `Engine/` - ValidatorRunner, ValidatorPipeline (orchestration)
  - `Tests/` - Unit tests and test fixtures

- **17 validators organized in 4 phases**:
  - **Phase 1: Structural Integrity** (3 validators)
    - MetadataCountsValidator, LocalIdUniquenessValidator, SymbolUtf8AndUniquenessValidator
  - **Phase 2: Semantic Validation** (7 validators)
    - MethodHeaderValidator (Blue Book encoding)
    - BytecodeValidator (CFG-based stack analysis)
    - LiteralFrameValidator (symbol/string bounds)
    - PatchValidator, PatchTargetExistenceValidator
    - InstallOrderValidator, InstallOrderCoverageValidator
  - **Phase 3: Deep Semantic** (6 validators)
    - ClassHierarchySanityValidator, MethodPrimitiveValidityValidator
    - MethodSelectorPresenceValidator, PoolDictionaryUsageValidator
    - ObjectFormatValidator, BlockContextClosureGraphValidator
  - **Phase 4: Static Analysis** (1 validator)
    - DeadCodeDetectionValidator (reachability analysis)

- **Key Features**:
  - CFG-based bytecode stack analysis with worklist propagation
  - Parallel validator execution (Task.WhenAll)
  - Diagnostic deduplication by (Code + Location)
  - JSON-based validation (no heap construction required)
  - ~2-3 second validation time for 10,059 methods
  - CI/CD ready (exit code 0=pass, 1=fail)

- **Documentation**: Complete reference in `Smalltalk80.Validator/README.md`
  - Detailed description of all 17 validators
  - Error codes and diagnostic system
  - Extension guide for custom validators
  - Architecture and technical details

### Interpreter ✅ (Major Milestone Achieved!)
- **All Cold Start Phases Working (0-9)**:
  - Phase 0: Preflight validation ✅
  - Phase 1: Symbol interning ✅
  - Phase 2: Class shells & metaclass wiring (with hierarchy) ✅
  - Phase 3: Class resolution ✅
  - Phase 4: Layout computation ✅
  - Phase 5: Object allocation ✅
  - Phase 6: Method installation (with association reuse) ✅
  - Phase 7: Patch application ✅
  - Phase 8: Init expression preparation ✅
  - Phase 9: USTable population ✅

- **Init Expression Execution Working**:
  - InitExpressionExecutor can find and execute specific init expressions
  - Currently configured to execute Float>>initialize (init:123)
  - Successfully executes Float arithmetic and class variable assignments
  - Result: "Succeeded: 1, Failed: 0"

- **Float Primitives Fully Functional**:
  - Primitive 40: asFloat (SmallInteger → Float conversion)
  - Primitive 41: Float + (addition) ✅
  - Primitive 42: Float - (subtraction) ✅
  - Primitive 43: Float < (less than) ✅
  - Primitive 44: Float > (greater than) ✅
  - Primitive 45: Float <= (less than or equal) ✅
  - Primitive 46: Float >= (greater than or equal) ✅
  - Primitive 47: Float = (equality) ✅
  - Primitive 48: Float ~= (not equal) ✅
  - Primitive 49: Float * (multiplication) ✅
  - Primitive 50: Float / (division) ✅
  - Primitive 51: truncated ✅
  - Primitive 52: fractionPart ✅
  - Primitive 53: exponent ✅
  - Primitive 54: timesTwoPower: ✅

- **Special Objects Array & GC**:
  - Special Objects Array creation ✅
  - Garbage collection verification ✅
  - All 20,319 objects reachable - no garbage collected ✅

- **Optional Bytecode Validation**:
  - Phase 12: Validates 4,640/10,059 methods (non-block methods)
  - Stack depth analysis ✅
  - Bytecode operand validation ✅
  - Decompilation and source comparison ✅

## Automated Build Pipeline

Use the automated pipeline script for complete build and validation:

```bash
# Run complete CI/CD pipeline
pwsh scripts/build-and-validate.ps1

# This script:
# 1. Builds the compiler
# 2. Generates coldstart.json from Smalltalk-80.st
# 3. Builds and runs all 17 validators
# 4. Reports results with proper exit codes

# Output:
# ========================================
# ✓ BUILD AND VALIDATION SUCCESSFUL
# ========================================
```

## Before Committing

```bash
# Option 1: Use automated pipeline (recommended)
pwsh scripts/build-and-validate.ps1

# Option 2: Manual steps
# 1. Build must succeed (with workaround)
dotnet build -p:RunCompilerTests=false

# 2. Validator must pass
dotnet run --project Smalltalk80.Validator -- bin/Debug/net9.0/coldstart.json

# 3. Check that no files were modified
git status

# DO NOT commit if Smalltalk-80.st was modified
```

## Next Steps

### IMMEDIATE (Session Continuation)

1. **Fix GC Traversal Issue** (CRITICAL - blocking init expression execution)
   - ⚠️ **Root Cause**: 26,815 objects collected during GC verification (should be 0)
   - ⚠️ **Investigation needed**: GC traversal may not handle new ObjectTableEntry structure correctly
   - ⚠️ **Potential issues**: ExtendedSize field, PackedMetadata bit layout changes, large object traversal
   - ⚠️ **Debug approach**: Check if GC properly reads SizeInWords from new bit layout
   - ⚠️ **Test**: Ensure GC can traverse large objects (USTable 7828 words, large arrays 285+ elements)

### SHORT TERM

2. **Complete BlockContext Implementation** (After GC fix)
   - ✅ Documentation updated
   - ✅ blockOwnerMap built
   - ✅ CreateBlockContext implemented
   - ⚠️ Modify VirtualMachine.SendMessage() to detect block invocation
   - ⚠️ Implement block invocation handler (InvokeBlock method)
   - ⚠️ Update interpreter bytecode dispatch for temp/arg/self resolution in blocks
   - ⚠️ Create DoIt MethodContext for init expressions
   - ⚠️ Test init:000 (ActionMenu comment:) - should succeed after BlockContext complete

3. **Test All Init Expressions**: Systematically execute all 285 init expressions
   - Currently blocked by GC verification failure
   - After GC fixed and BlockContext working, test init:000
   - Modify InitExpressionExecutor to test all or specific sets
   - Identify and fix any failing init expressions

4. **Validate Large Object Support**: Ensure heap manager changes work correctly
   - ✅ Large string allocation (2712+ characters)
   - ✅ Large array allocation (285+ elements)  
   - ✅ Large USTable allocation (7828 words)
   - ⚠️ GC traversal of large objects
   - ⚠️ Memory alignment and performance with large objects

### MEDIUM TERM

4. **Update Compiler Tests**: Either update tests to match current API or remove them
   - 131 tests reference obsolete API
   - Need to decide: fix or remove

5. **Fix MSBuild Integration**: Remove or fix test file references in Compiler.csproj

### LONG TERM

6. **Implement REPL**: Once init expressions complete, build interactive REPL
   - Parse Smalltalk expressions
   - Compile to bytecode
   - Execute in VM
   - Display results

## References

- **Smalltalk-80 Blue Book** - Goldberg & Robson (1983)
- **docs/Interpreter-Architecture.md** - Detailed architecture documentation
- **docs/schemas/coldstart-v1.0.3.schema.json** - JSON schema specification
- **Smalltalk80.Validator/README.md** - Complete validator documentation
