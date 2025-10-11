# Claude Development Guidelines

**Last Updated**: 2025-01-11
**Status**: Current Working State
**Compiler Status**: ✅ Fully operational - generates valid JSON from Smalltalk-80.st
**Validator Status**: ✅ 100% validation passing (10,059 methods validate correctly)
**Interpreter Status**: ✅ WORKING - Successfully executes init expressions!

## Recent Accomplishments (2025-01-11)

### ✅ Interpreter Breakthrough - Init Expression Execution Working!
- **Fixed** all cold start phases (0-10) - complete system bootstrap successful
- **Implemented** metaclass hierarchy wiring with superclass chain traversal
- **Fixed** method lookup to walk superclass chain (enables inheritance)
- **Added** byte-indexed object access (`GetIndexedByte`, `SetIndexedByte`)
- **Fixed** Float object allocation with correct classId
- **Working** Float arithmetic primitives (add, subtract, multiply, divide, etc.)
- **Executed** first init expression successfully: `Float>>initialize` (init:123)
- **Result**: Init expression results show "Succeeded: 1, Failed: 0"

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
- **Interpreter**: All 10 cold start phases complete successfully!
  - Phase 0: Preflight validation ✅
  - Phase 1: Symbol interning ✅
  - Phase 2: Class shells & metaclass wiring ✅
  - Phase 3: Class resolution ✅
  - Phase 4: Layout computation ✅
  - Phase 5: Object allocation ✅
  - Phase 6: Method installation ✅
  - Phase 7: Patch application ✅
  - Phase 8: Init expression preparation ✅
  - Phase 9: USTable population ✅
  - **Init Expression Execution**: Float>>initialize executes successfully! ✅
  - Garbage collection verification ✅
  - Optional bytecode validation ✅
- **JSON Schema**: v1.0.3 with Blue Book-compliant bytecode generation
- **CI/CD Pipeline**: Automated build-and-validate.ps1 script (build → compile → validate)

### What's Broken ⚠️
- **Compiler Tests**: 131 tests exist but reference obsolete compiler API
- **MSBuild Integration**: Looks for non-existent test files
- **Remaining Init Expressions**: Only Float>>initialize tested so far (1 of 285)

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

1. **Test All Init Expressions**: Systematically execute all 285 init expressions
   - Currently only Float>>initialize (init:123) tested
   - Modify InitExpressionExecutor to test all or specific sets
   - Identify and fix any failing init expressions

2. **Implement Missing Primitives**: As init expressions fail, implement required primitives
   - Object instantiation primitives (new, new:, basicNew, basicNew:)
   - Collection primitives (at:, at:put:, size, etc.)
   - Block evaluation primitives (value, value:, etc.)
   - String primitives (comparison, concatenation, etc.)

3. **Update Compiler Tests**: Either update tests to match current API or remove them
   - 131 tests reference obsolete API
   - Need to decide: fix or remove

4. **Fix MSBuild Integration**: Remove or fix test file references in Compiler.csproj

5. **Implement REPL**: Once init expressions complete, build interactive REPL
   - Parse Smalltalk expressions
   - Compile to bytecode
   - Execute in VM
   - Display results

## References

- **Smalltalk-80 Blue Book** - Goldberg & Robson (1983)
- **docs/Interpreter-Architecture.md** - Detailed architecture documentation
- **docs/schemas/coldstart-v1.0.3.schema.json** - JSON schema specification
- **Smalltalk80.Validator/README.md** - Complete validator documentation
