# Claude Development Guidelines

**Last Updated**: 2025-01-10
**Status**: Current Working State
**Compiler Status**: ✅ Fully operational - generates valid JSON from Smalltalk-80.st
**Validator Status**: ✅ 100% validation passing (10,059 methods validate correctly)
**Interpreter Status**: ⚠️ Builds but crashes at Phase 6 (Object Allocation) - JSON deserialization bug

## Recent Accomplishments (2025-01-10)

### ✅ Validator Refactoring Complete
- **Refactored** Smalltalk80.Validator into modular architecture
  - Split into 4 projects: CLI, Core, Engine, Tests
  - Nested folder structure under `Smalltalk80.Validator/`
- **Expanded** from 6 validators to 17 specialized validators
- **Implemented** all validators with full logic (100% complete)
- **Fixed** critical patch handling bug (patchId vs localId)
- **Created** comprehensive documentation (Smalltalk80.Validator/README.md)
- **Validated** 100% passing: 10,059 methods, 474 classes, zero errors

### ✅ Documentation Updates
- **Created** detailed Smalltalk80.Validator/README.md (21,000+ words)
  - Complete validator reference with all 17 validators documented
  - Error codes, diagnostic system, extension guide
- **Updated** docs/Interpreter-Architecture.md
  - Corrected project structure (nested validator folders)
  - Updated schema version references (v1.0.2 → v1.0.3)
  - Added high-level validator descriptions with README pointers

### ✅ Schema Verification
- **Verified** compiler-generated JSON strictly follows v1.0.3 schema
- **Confirmed** all v1.0.3 features present:
  - Blue Book-compliant block compilation (5,419 blocks)
  - Required method header fields
  - Patch handling with patchId
  - Integer bytecode arrays
  - Comprehensive metadata

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

# Run interpreter (currently fails at Phase 6)
dotnet run --project ../Smalltalk80.Interpreter -- repl
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
├── Smalltalk80.Interpreter/     # VM runtime (PARTIALLY WORKING - Phase 6 bug)
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

### What's Working
- **Compiler**: Generates `coldstart.json` with 10,059 methods (4,640 methods + 5,419 blocks) from Smalltalk-80.st
- **Validator**: All 17 validators pass with 100% success rate (zero errors, zero warnings)
  - Modular architecture: Core (validators), Engine (orchestration), CLI (application)
  - CFG-based bytecode stack analysis with worklist propagation
  - Parallel execution with diagnostic deduplication
  - Comprehensive validation: metadata, bytecode, headers, literals, patches, install order, class hierarchy, primitives, selectors, pool dictionaries, object formats, block closures, dead code
  - Complete documentation in Smalltalk80.Validator/README.md
- **JSON Schema**: v1.0.3 with Blue Book-compliant bytecode generation
- **CI/CD Pipeline**: Automated build-and-validate.ps1 script (build → compile → validate)

### What's Broken
- **Interpreter**: Crashes at Phase 6 (Object Allocation) - JSON type casting issue
- **Compiler Tests**: 131 tests exist but reference obsolete compiler API
- **MSBuild Integration**: Looks for non-existent test files

### Known Issues
1. Interpreter has `System.Text.Json.JsonElement` to `System.Int32` cast error in Phase 6
2. Compiler.Tests project won't build due to missing types (AstBuilder, ProcessorContext, etc.)
3. MSBuild test target in Compiler.csproj references missing test files
4. Must use `-p:RunCompilerTests=false` when building to avoid MSBuild errors

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

### Run Interpreter (Currently Broken)

```bash
dotnet run --project Smalltalk80.Interpreter -- repl

# Currently fails at:
# Phase 6: Object Allocation... ✗ FAILED
# Error: Unable to cast object of type 'System.Text.Json.JsonElement' to type 'System.Int32'
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

### Interpreter ⚠️
- **Working Phases (0-5)**:
  - Preflight validation
  - Symbol interning
  - Class shell creation
  - Metaclass wiring
  - Class resolution
  - Layout computation
  
- **Broken Phase (6)**:
  - Object Allocation - JSON deserialization type error
  
- **Not Yet Reached (7-12)**:
  - Method installation
  - Patch application
  - Init execution
  - Special Objects Array creation
  - GC initialization
  - Bytecode validation

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

1. **Fix Interpreter Phase 6**: Resolve JSON deserialization issue in Object Allocation
2. **Update Compiler Tests**: Either update tests to match current API or remove them
3. **Fix MSBuild Integration**: Remove or fix test file references in Compiler.csproj
4. **Complete Interpreter**: Get through all 12 phases to reach REPL

## References

- **Smalltalk-80 Blue Book** - Goldberg & Robson (1983)
- **docs/Interpreter-Architecture.md** - Detailed architecture documentation
- **docs/schemas/coldstart-v1.0.3.schema.json** - JSON schema specification