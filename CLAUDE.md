# Claude Development Guidelines

**Last Updated**: 2025-11-18

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 31/46 succeed (67% success rate)
- **BlockContext Tests**: ✅ **120/120 tests PASS** (returns 1191)
- **VM Core**: Fully functional, verified operational

## Architecture Documentation

**See `docs/Architecture/` for complete architecture documentation:**

- **[PHYSICAL-ARCHITECTURE.md](docs/Architecture/PHYSICAL-ARCHITECTURE.md)** - File system organization, subsystems, dependencies
- **[IMPLEMENTATION-ARCHITECTURE.md](docs/Architecture/IMPLEMENTATION-ARCHITECTURE.md)** - Bytecode execution, contexts, primitives, message sending
- **[DEVELOPMENT-GUIDE.md](docs/Architecture/DEVELOPMENT-GUIDE.md)** - How to extend the interpreter (adding primitives, phases, components)

## Block Context Architecture

⚠️ **CANONICAL REFERENCE**: `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md`

**Core Principle**: All method and block variables are stored in the **MethodContext's unified temp frame** using absolute indices assigned at compile time.

**Key Points**:
1. **Single Storage**: ALL variables stored in MethodContext indexed slots
2. **Absolute Indexing**: Compiler assigns unique absolute index (0, 1, 2...) to each variable
3. **BlockContext**: Contains execution state + `home` pointer to MethodContext, NO variable storage
4. **Variable Access**: Get/SetTemporaryVariable traverse to MethodContext via `home` pointer

## Running the Interpreter

### Standard Execution

```bash
cd Smalltalk80.Interpreter
dotnet run
```

Executes cold start phases 1-6 and 31 init expressions.

### With Diagnostics

```bash
# Verbose cold start
dotnet run -- --diagnostics detail

# Method tracing
dotnet run -- --diagnostics method

# Multiple categories
dotnet run -- --diagnostics detail,method,gc
```

### Skip Init Expressions

```bash
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart.json --skip-init-expressions
```

## Building and Testing

### Quick Build

```bash
cd Smalltalk80.Interpreter
dotnet build
dotnet run
```

### 🔴 MANDATORY: BlockContext Regression Test

**Run after EVERY compiler or interpreter change**:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected**: Returns `1191` (120 tests pass)

### Regenerate Test Bundle

```bash
cd "Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st Tests/BlockContextTestRunner.st > Smalltalk-80-with-runner.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-runner.st"
cp ../bin/Debug/net9.0/coldstart.json ../bin/Debug/net9.0/coldstart_with_runner.json
```

### Critical Files Requiring Test

Changes to these files MUST pass the regression test:
- `BytecodeCompiler.cs` - bytecode generation
- `VariableResolver.cs` - variable resolution
- `TempFrameAllocator.cs` - variable allocation
- `VirtualMachine.cs` - execution
- `ControlPrimitives.cs` - BlockCopy, BlockValue primitives

## Init Expression Status

**✅ Passing (31)**: Character, Float, SmallInteger, Text, Symbol, Date, File, Scanner, Debugger, and 22 more

**⏭️ Skipped (15)**: Require GUI/process/file I/O primitives not available at bootstrap

## Development Methodology

- **Never perform workarounds** - Always find and fix root cause
- **Trust source code over analysis** - When agent analysis contradicts source, trust source
- **Use instruction tracing** - 5K+ instructions indicates infinite loop
- **Validate with tests** - BlockContextTest suite validates core functionality

## Model Selection

Always use model: **claude-sonnet-4-5**
