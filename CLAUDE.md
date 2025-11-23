# Claude Development Guidelines

**Last Updated**: 2025-11-23

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 46/47 succeed (98% success rate)
- **Comment Expressions**: Skipped (234 total - WriteStream bug)
- **BlockContext Tests**: ✅ **120/120 tests PASS** (returns 1191)
- **VM Core**: Fully functional, verified operational

## Known Issues

**WriteStream>>nextPut: Infinite Loop**: 210/234 comment expressions fail with infinite loop in WriteStream. All comment expressions skipped until bug is fixed.

**Parser Token Recognition**: The `--execute` feature currently fails with Parser error "Nothing more expected" at position 1. The Scanner is not properly tokenizing number literals ("4" produces `tokenType: #xDigit` instead of `#number`, and `hereType` remains nil). Root cause under investigation.

## Migration Status

**System Startup Migration** - Migrating from single-threaded cold start to full interactive system with fiber-based process scheduling.

**Phase 1: Program Refactoring** ✅ **COMPLETE**
- ✅ SmalltalkThreadContext class created
- ✅ SmalltalkEventQueue and SmalltalkEvent classes created
- ✅ SmalltalkInterpreterThread class created
- ✅ Program.cs refactored: UI thread (WinForms) + Smalltalk thread separation
- ✅ Window remains responsive during init expression execution

**Phase 2: Window Sizing** ✅ **COMPLETE**
- ✅ WinFormsDisplayProvider accepts width/height parameters (no heap access - thread safety)
- ✅ DisplayResized() method added to IPlatformBridge interface
- ✅ BeDisplay primitive (102) detects size changes and calls DisplayResized()
- ✅ Window dimensions dynamically match Smalltalk Display object via resize flow
- ✅ Supports runtime resizing: `DisplayScreen displayExtent: 800@600`

**Testing Window Resize Flow:**
- Window initially created at **800x600** (hardcoded in Program.cs)
- Smalltalk Display object created at **640x480** (in RuntimeObjectCreator.cs)
- When `DisplayScreen initialize` init expression runs, it calls `Display beDisplay`
- BeDisplay primitive (102) reads Display dimensions (640x480) and calls `DisplayResized(640, 480)`
- Window automatically resizes from 800x600 → 640x480
- Console output shows: `[Display] Resizing from 800x600 to 640x480`
- This validates the complete resize communication flow: Smalltalk thread → UI thread

**Phase 3: Fiber Infrastructure** 📋 **ON HOLD**
- Fiber work postponed pending other priorities

See `docs/PROGRAM-REFACTORING-PLAN.md`, `docs/WINDOW-SIZING-ARCHITECTURE.md`, and `docs/SYSTEM-STARTUP-IMPLEMENTATION-PLAN.md` for complete migration documentation.

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

**Default Behavior**: The interpreter automatically loads `coldstart.json` from the same directory as the executable when no `--coldstart` parameter is provided.

### Standard Execution

```bash
cd Smalltalk80.Interpreter
dotnet run
```

Executes cold start phases 1-6 and init expressions from `coldstart.json` in the executable directory.

### With Diagnostics

```bash
# Verbose cold start
dotnet run -- --diagnostics detail

# Method tracing
dotnet run -- --diagnostics method

# Multiple categories
dotnet run -- --diagnostics detail,method,gc
```

### Execute User Expressions (--execute)

**NEW**: Runtime expression evaluation using the built-in Smalltalk Compiler.

```bash
# Simple arithmetic
dotnet run -- --execute "3 + 4"

# Message sends
dotnet run -- --execute "Array new: 10"

# Method calls
dotnet run -- --execute "BlockContextTest testAllCombinations"

# Multiple messages
dotnet run -- --execute "'Hello, World!' size"
```

**How it works**:
1. Creates Smalltalk String from C# expression
2. Sends `compile:in:` message to Smalltalk Compiler class
3. Executes resulting CompiledMethod with nil receiver
4. Displays result with type-aware formatting

**Architecture**:
- Uses `MessageSender` API for runtime message sending from C#
- Uses `StringHelper` for C# ↔ Smalltalk String conversion
- Works with runtime Smalltalk objects (not ColdStartBundle structures)
- Leverages built-in Smalltalk Compiler (no third compiler needed)

**See**: `docs/EXECUTE-FEATURE-DESIGN.md` for complete design documentation.

**Limitations**:
- Requires Smalltalk Compiler class to be loaded in the image
- Expression must be valid Smalltalk syntax
- No access to workspace variables (future enhancement)

## Building and Testing

### Quick Build

```bash
cd Smalltalk80.Interpreter
dotnet build
dotnet run
```

### 🔄 Regenerating Cold Start Bundle

**When to regenerate**: After ANY changes to `Smalltalk-80 Source/Smalltalk-80.st`

Changes to the Smalltalk source (including adding init expressions like `DisplayScreen initialize`) require regenerating the coldstart bundle:

```bash
# Navigate to source directory
cd "Smalltalk-80 Source"

# Compile Smalltalk source to generate new coldstart.json
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"

# Copy to interpreter bin directory (Windows)
copy ..\Smalltalk80.Compiler\bin\Debug\net9.0\coldstart.json ..\Smalltalk80.Interpreter\bin\Debug\net9.0-windows\coldstart.json

# Copy to interpreter bin directory (Linux/Mac)
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/coldstart.json
```

**Important**: The `coldstart.json` file in the Interpreter's bin directory is now tracked in git to ensure init expressions are always in sync with the source code.

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

### C# Unit Tests

**Status**: ✅ **388/388 tests passing (100%)**

Run the comprehensive C# unit test suite:

```bash
cd Smalltalk80.Interpreter.Tests
dotnet test
```

**Test Coverage**:
- **Primitives** (263 tests): All primitive operations including arithmetic, comparison, control flow, strings, floats, objects, and system operations
- **HeapManager** (125 tests): Object allocation, garbage collection, memory management, and heap operations

**Run tests by category**:
```bash
# Primitives only
dotnet test --filter "Category=Primitives"

# HeapManager only
dotnet test --filter "Category=HeapManager"
```

See `docs/Testing-Guide.md` for complete testing documentation.

## Init Expression Status

**✅ Passing (46)**: Character, Float, SmallInteger, Text, Symbol, Date, File, Scanner, Debugger, InputSensor (including install and initSensor), Cursor, Object, Form, TextList, BitEditor, and 30 more

**⏭️ Skipped (1)**:
- FormMenuView initialize - Requires file I/O primitives (98, 92, 99, 146 - not implemented)

**Recent Fixes**:
- **Primitive 65 (Stream>>next)**: Fixed to return Character objects (not SmallIntegers) when reading from Strings/Symbols, enabling Scanner and --execute feature to work correctly. Uses isKindOf: semantics to handle subclasses properly.
- Added InputSensor install and InputSensor initSensor init expressions to properly initialize Sensor global
- Fixed Cursor initialize to set CurrentCursor (was nil, causing cursorPoint failures)
- Modified Object initialize to comment out GUI confirmation dialog while preserving DependentsFields initialization
- Fixed Object initialize to initialize DependentsFields before calling confirm: (chicken-egg problem)

## Development Methodology

- **Never perform workarounds** - Always find and fix root cause
- **Trust source code over analysis** - When agent analysis contradicts source, trust source
- **Use instruction tracing** - 5K+ instructions indicates infinite loop
- **Validate with tests** - BlockContextTest suite validates core functionality

## Model Selection

Always use model: **claude-sonnet-4-5**
