# Claude Development Guidelines

**Last Updated**: 2025-01-10
**Status**: Current Working State
**Compiler Status**: ✅ Fully operational - generates valid JSON from Smalltalk-80.st
**Validator Status**: ✅ 100% validation passing (10,059 methods validate correctly)
**Interpreter Status**: ⚠️ Builds but crashes at Phase 6 (Object Allocation) - JSON deserialization bug

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
├── Smalltalk80.Validator/       # Validates JSON (WORKING) 
├── Smalltalk80.Interpreter/     # VM runtime (PARTIALLY WORKING - Phase 6 bug)
├── Smalltalk80.Compiler.Tests/  # Unit tests (BROKEN - incompatible with current API)
├── Smalltalk-80 Source/         # Source files
│   └── Smalltalk-80.st         # Master source (READ-ONLY - NEVER MODIFY)
└── Shared/
    └── Core/Bytecodes/
        └── BytecodeDefinitions.cs  # Shared bytecode definitions

Solution file includes: Compiler, Validator, Interpreter, Compiler.Tests (tests don't build)
```

## Current State Summary

### What's Working
- **Compiler**: Generates `coldstart.json` with 10,059 methods from Smalltalk-80.st
- **Validator**: All 6 validators pass with 100% success rate
- **JSON Schema**: v1.0.3 with Blue Book-compliant bytecode generation

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
- 6 validators all passing:
  - BytecodeValidator (CFG-based stack analysis)
  - OperandValidator (index validation)
  - MethodHeaderValidator (Blue Book encoding)
  - LiteralFrameValidator (symbol/string bounds)
  - PatchValidator (patch structure)
  - InstallOrderValidator (completeness)

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

## Before Committing

```bash
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