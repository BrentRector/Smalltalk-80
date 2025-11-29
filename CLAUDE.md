# Claude Development Guidelines

**Last Updated**: 2025-11-28

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 50/51 succeed (98% success rate) - includes process scheduling
- **Comment Expressions**: ✅ **234/234 succeed** (100% success rate)
- **BlockContext Tests**: ✅ **120/120 tests PASS** (returns 1191)
- **VM Core**: Fully functional, verified operational
- **Process Primitives**: ✅ **85-88 working** (suspend, resume, terminate, yield)

## Known Issues

**🟡 Control Structure Optimization Disabled**
The C# compiler has `ifTrue:`/`ifFalse:` optimization **incorrectly disabled**. Blue Book Chapter 28 confirms these SHOULD be optimized to conditional jumps (no BlockContext created). Current behavior creates BlockContexts, causing `thisContext` to return BlockContext instead of MethodContext.

**Fix Applied**: Added `BlockContext>>removeSelf` method that delegates to `home removeSelf`. This follows the existing BlockContext pattern (like `receiver` and `method`) and automatically handles nested blocks via the chain of `home` references. The proper long-term fix still requires enabling compiler optimization.

**Parser Token Recognition**: The `--execute` feature currently fails with Parser error "Nothing more expected" at position 1. The Scanner is not properly tokenizing number literals ("4" produces `tokenType: #xDigit` instead of `#number`, and `hereType` remains nil). Root cause under investigation.

**FormMenuView .form File Loading**: `WindowsFile>>asFileStream:` calls `self size` before the file is open. Primitive 144 (GetFileSize) fails because `fileHandle` is nil. Fix needed: add `self reopen` before querying size, or handle nil handle in primitive by getting path from receiver's `fileName` instance variable.

## Migration Status

**System Startup Migration** - Migrating from single-threaded cold start to full interactive system with MVC and process scheduling.

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

**Display Configuration:**
- Window created at **800x600** (hardcoded in Program.cs)
- Smalltalk Display object created at **800x600** (in RuntimeObjectCreator.cs)
- When `DisplayScreen initialize` init expression runs, it calls `Display beDisplay`
- BeDisplay primitive (102) reads Display dimensions (800x600), no resize needed
- Window size matches Display size by default
- Resize flow tested dynamically: `DisplayScreen displayExtent: width@height`

**DisplayScreen Initialize Enhancement:**
- Tests all 16 BitBlt combination rules with visual 4×4 grid
- Each tile shows different BitBlt operation (rules 0-15)
- Uses checkerboard source pattern (8×8 checks) over gray halftone background
- Comprehensive validation of BitBlt operations during system initialization

**Graphics Optimization** ✅ **COMPLETE**
- ✅ **Primitive 104 (drawLoopX:Y:)**: Implemented Bresenham line drawing in C#
- ✅ **Display Invalidation**: Fixed missing `InvalidateDisplayRegion()` call
- ✅ **Performance**: Dragon drawing - 4.6s (primitive 104) vs 16.6s (Smalltalk fallback) = **3.6x speedup**
- ✅ **Paint Mode**: Correctly converts rule 16 to Or (rule 7) for Form black halftone
- ✅ **BitBlt Engine**: Verified working correctly - all 16 combination rules operational

**Key Lesson**: Display refresh architecture requires `InvalidateDisplayRegion()` after modifying Display bitmap. Without it, WinForms display shows stale cached data even though heap is correctly updated.

**Phase 3: MVC and Process Scheduling** 🔄 **IN PROGRESS**
- ✅ Process primitives 85-88 implemented and working
- ✅ `Processor hiddenBackgroundProcess` creates idle process at priority 1
- ✅ `ControlManager new` creates controller manager
- ✅ `ScheduledControllers` global installed using canonical Association
- ✅ `BlockContext>>removeSelf` delegates to home MethodContext (Smalltalk-level fix)
- ⏳ Controller event loop (`searchForActiveController`) - testing in progress
- ⏳ Integration with InputSensor for mouse/keyboard events

**Key Files Modified**:
- `VirtualMachine.cs` - HandleReturn logic for process termination
- `Smalltalk-80.st` - Added `BlockContext>>removeSelf` for process termination support
- `VMConstants.cs` - LinkedList/Link constants for process queues
- `SmalltalkInterpreterThread.cs` - StartControllerLoop, InstallScheduledControllersGlobal

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

**✅ Passing (47)**: Character, Float, SmallInteger, Text, Symbol, Date, File, Scanner, Debugger, InputSensor (including install and initSensor), Cursor, Object, Form, FormMenuView, TextList, BitEditor, and 30 more

**⏭️ Skipped (0)**: None - all init expressions enabled

**Recent Fixes**:
- **CharacterTable Implementation**: Phase 3 creates CharacterTable Array with 256 Character objects. Phase 5 installs it as CLASS VARIABLE in Character's classPool (following USTable pattern, NOT as a global).
- **Primitive 121 (FileOpen)**: Fixed to support both 2-arg (path, mode string) and 4-arg (path, access, share, create integers) calling conventions for WindowsFile compatibility.
- **Primitive 110 (Character literal identity)**: Fixed comparison bug with runtime canonicalization using CharacterRegistry.
- **WindowsFile>>asFileStream:**: Added method for FileStream compatibility. Currently fails because file not open before size query.
- **Primitive 65 (Stream>>next)**: Fixed to return Character objects (not SmallIntegers) when reading from Strings/Symbols, enabling Scanner and --execute feature to work correctly. Uses isKindOf: semantics to handle subclasses properly.
- Added InputSensor install and InputSensor initSensor init expressions to properly initialize Sensor global
- Fixed Cursor initialize to set CurrentCursor (was nil, causing cursorPoint failures)
- Modified Object initialize to comment out GUI confirmation dialog while preserving DependentsFields initialization

## Development Methodology

- **Never perform workarounds** - Always find and fix root cause
- **Trust source code over analysis** - When agent analysis contradicts source, trust source
- **Use instruction tracing** - 5K+ instructions indicates infinite loop
- **Validate with tests** - BlockContextTest suite validates core functionality

## Model Selection

Always use model: **claude-sonnet-4-5**
