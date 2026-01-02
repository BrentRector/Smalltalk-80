# Claude Development Guidelines

**Last Updated**: 2026-01-02

## Project Status

**Interpreter Status**: ✅ **FUNCTIONAL** (with one intermittent race condition)

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 286/286 succeed (100% success rate)
- **Classes**: 238 (MouseEvent class removed as dead code)
- **Methods**: 4747 compiled
- **BlockContext Tests**: ✅ **120/120 tests PASS** (returns 1191)
- **VM Core**: ✅ Functional
- **Process Primitives**: ✅ **85-88 working** (suspend, resume, terminate, yield)
- **InputProcess**: ✅ Working - processes mouse/keyboard events correctly
- **System Browser**: ✅ Functional - opens and displays source code
- **Transcript**: ✅ Functional - opens without crash
- **Timer/Event System**: ✅ Working
- **Mouse Wheel Scrolling**: ✅ Working - list panes move selection, text panes scroll content

## ⚠️ Known Issue: Race Condition on Early Mouse Movement

**Symptom**: Interpreter crashes if mouse is moved into window **immediately** after it appears. Waiting a few seconds before moving mouse avoids the crash.

**Error**: `Instance variable index 15 out of range [0, -1] for object oop=2 (UndefinedObject)`

**Analysis**: A MethodContext has nil as its receiver. The ivar[15] access is `emphasis` in StrikeFont. This suggests something isn't fully initialized when InputProcess first handles mouse events during early startup.

**Workaround**: Wait 2-3 seconds after window appears before moving mouse into it.

**Status**: Under investigation. Difficult to reproduce reliably due to timing-dependent nature.

## Known Issues

**✅ Control Structure Optimization - IMPLEMENTED**
The C# compiler now inlines `ifTrue:`, `ifFalse:`, `ifTrue:ifFalse:`, `ifFalse:ifTrue:`, `and:`, and `or:` using Blue Book conditional jump bytecodes. No BlockContext is created for zero-argument blocks in these control structures.

**"For Value" vs "For Effect" Compilation** (2025-12-12):
Single-arm conditionals (`ifTrue:`, `ifFalse:`) support two bytecode patterns:
- **For value** (result needed): `<cond> BFP <block> JMP PUSH_NIL` - always leaves value on stack
- **For effect** (statement position): `<cond> BFP <block>` - shortcut pattern, nothing on stack

This matches the Smalltalk runtime compiler's behavior (`MessageNode>>emitIf:on:value:`) and enables proper decompilation of methods with nested conditionals.

**Remaining**: `whileTrue:` and `whileFalse:` are NOT yet inlined (receiver is a block, requires special handling).

**Parser Token Recognition**: The `--execute` feature currently fails with Parser error "Nothing more expected" at position 1. The Scanner is not properly tokenizing number literals ("4" produces `tokenType: #xDigit` instead of `#number`, and `hereType` remains nil). Root cause under investigation.

**✅ Idle CPU Usage - RESOLVED**: Uses `WaitHandle.WaitAny` for efficient multi-source waiting on both event queue and timer handles.

**✅ Atomic Mouse State - IMPLEMENTED**: Primitive 202 (`bitState`) provides atomic button state polling for tight loops (PopUpMenu, etc.). Uses packed `long` with `Interlocked.Read/Exchange` to emulate Blue Book hardware registers. See plan file `wild-tumbling-rabbit.md`.

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

**Phase 3: MVC and Process Scheduling** ✅ **COMPLETE**
- ✅ Process primitives 85-88 implemented and working
- ✅ `Processor hiddenBackgroundProcess` creates idle process at priority 1
- ✅ `ControlManager new` creates controller manager
- ✅ `ScheduledControllers` global installed using canonical Association
- ✅ `BlockContext>>removeSelf` delegates to home MethodContext (Smalltalk-level fix)
- ✅ Controller event loop (`searchForActiveController`) - working
- ✅ System Browser fully functional with source code retrieval

**Source Code Retrieval** ✅ **WORKING**
- ✅ Compiler generates `.sources` and `.changes` files in Interpreter project folder
- ✅ Source positions encoded in CompiledMethod bytecodes (last 3 bytes)
- ✅ `SourceFiles` global initialized via `SystemDictionary>>initialize` (pure Smalltalk)
- ✅ `WindowsFileStream` uses `logicalSize` tracking to avoid stale filesystem queries
- ✅ UTF-8 BOM handling in file primitives (transparent offset compensation)
- ✅ `CompiledMethod>>getSource` retrieves source via `RemoteString`
- ✅ Class comments written to `.changes` file with proper `!` chunk terminators

**Key Files Modified**:
- `VirtualMachine.cs` - HandleReturn logic for process termination
- `Smalltalk-80.st` - Added `BlockContext>>removeSelf` for process termination support
- `VMConstants.cs` - LinkedList/Link constants for process queues
- `SmalltalkInterpreterThread.cs` - StartControllerLoop, InstallScheduledControllersGlobal
- `FilePrimitives.cs` - BOM offset handling for source file reading
- `StackManager.cs` - Blue Book block argument handling

**Phase 4: Timer and Event System Optimization** ✅ **COMPLETE**

Efficient idle detection using `WaitHandle.WaitAny` for unified multi-source waiting:

- ✅ **Interrupt-Driven Timer**: `System.Threading.Timer` signals immediately when delay expires
- ✅ **WaitHandle.WaitAny**: Waits on both event queue and timer handles in single kernel call
- ✅ **ManualResetEventSlim**: Replaced AutoResetEvent to prevent signal consumption
- ✅ **Double-Check Reset Pattern**: Safe reset after WaitAny to handle race conditions
- ✅ **CPU Efficiency**: 0% CPU during idle

**Atomic Mouse State (Simulated Hardware Registers)**:

The original Blue Book hardware had memory-mapped registers for mouse state. We emulate this with atomic operations:

```csharp
// Packed into single long for atomic read/write
// Bits 0-15: X, Bits 16-31: Y, Bits 32-34: buttons (red=4, yellow=2, blue=1)
private long _mouseState;

// UI thread writes atomically
Interlocked.Exchange(ref _mouseState, PackMouseState(x, y, buttons));

// Smalltalk thread reads atomically (primitive 202)
int buttons = (int)((Interlocked.Read(ref _mouseState) >> 32) & 0x7);
```

**Primitive 202 (bitState)**: Returns button state atomically for tight polling loops (PopUpMenu, etc.). Call chain: `redButtonPressed` → `buttons` → `mouseButtons` → `bitState` → primitive 202.

**Blue Book Input Format**: Primitive 95 returns packed integer words (type << 12 | param):
- Type 1/2: Mouse X/Y coordinates
- Type 3: Key down (param = keycode)
- Type 4: Key up (param = keycode)
- Type 6: Mouse wheel (param = delta + 2048, offset-encoded signed value)

**Key Files**:
- `WinFormsDisplayProvider.cs` - Atomic mouse state via packed `long`, ring buffer for events
- `InputPrimitives.cs` - Primitives 90-95, 202 (bitState)
- `InputWordRingBuffer.cs` - Thread-safe ring buffer for Blue Book input words
- `VirtualMachine.cs` - InterpreterLoop uses `WaitHandle.WaitAny([eventHandle, timerHandle])`
- `VirtualMachine.ProcessScheduling.cs` - Process scheduling methods (~330 lines)
- `TimerManager.cs` - Interrupt-driven timer with TimerExpiredHandle

See `docs/Architecture/PROPOSED-TIMER-OPTIMIZATION.md` and `docs/Architecture/PROCESS-MANAGEMENT.md` for complete documentation.

**Phase 5: UI Polish and Mouse Wheel** ✅ **COMPLETE**

Mouse wheel scrolling support with platform-appropriate behavior:

- ✅ **Input Type 6 (MouseWheel)**: Added to Blue Book input word format with offset-encoded signed delta
- ✅ **InputState/InputSensor**: `wheelDelta` instance variable, `mouseWheelDelta` method
- ✅ **ScrollController>>wheelActivity:**: Scrolls view content (text panes)
- ✅ **ListController>>wheelActivity:**: Moves selection up/down with auto-scroll (list panes)
- ✅ **ParagraphEditor**: Handles wheel events in controlActivity
- ✅ **Scroll Limiting**: Text panes stop scrolling when last line reaches bottom
- ✅ **Browser Initial Selection**: System Browser opens with first category selected
- ✅ **Direction Convention**: Matches Windows 11 (wheel up = scroll/move up)

**Key Files**:
- `InputWordRingBuffer.cs` - TypeMouseWheel constant, EnqueueMouseWheel method
- `WinFormsDisplay.cs` - OnMouseWheel handler
- `Smalltalk-80.st` - InputState, InputSensor, ScrollController, ListController, ParagraphEditor, Browser, ListView

See `docs/PROGRAM-REFACTORING-PLAN.md`, `docs/WINDOW-SIZING-ARCHITECTURE.md`, `docs/SYSTEM-STARTUP-IMPLEMENTATION-PLAN.md`, and `docs/SOURCE-LOOKUP-ANALYSIS.md` for complete documentation.

**Phase 6: 32bpp Graphics Pipeline** ✅ **COMPLETE**

Full 32bpp color support with anti-aliased text rendering:

- ✅ **BitBlt32Engine Overlap Handling**: Fixed scroll duplication by detecting `destY > sourceY` for copy direction
- ✅ **XOR Caret Fix**: Inverted source in XOR operations (`~source ^ dest`) to preserve 1bpp semantics in 32bpp
- ✅ **Fixed-Pitch Font Alignment**: Uniform cell width for all characters using `GetCellWidth()` method
- ✅ **Paint Mode (Rule 16)**: Implemented multiply blend (`src * dest / 255`) for anti-aliased text
- ✅ **32bpp Font Rendering**: Explicit `Format32bppArgb` bitmap with `TextRenderingHint.AntiAlias`
- ✅ **Color Preservation**: Verified end-to-end with blue text test (colors flow through entire pipeline)
- ✅ **Window Title**: Updated to "Smalltalk-2026"

**Key Algorithm - Multiply Blend for Paint Mode**:
```csharp
// Rule 16: Anti-aliased text blending (white=transparent, black=opaque)
// result = src * dest / 255 (per channel)
// - Gray source (128,128,128) on white dest = gray (128,128,128)
// - Gray source on blue dest (0,0,255) = dark blue (0,0,128)
```

**Key Files**:
- `BitBlt32Engine.cs` - Overlap handling for scroll, multiply blend for paint mode
- `WinFormsFontBridge.cs` - 32bpp font bitmap creation with anti-aliasing
- `GraphicsPrimitives.cs` - Accept rule 16 in primitive (was failing to Smalltalk fallback)
- `CombinationRule.cs` - Added `Paint = 16` enum value

**Remaining Work**:
- Font is currently rendered in blue for testing - change back to black for production
- Anti-aliasing visible in zoomed view but subtle at normal size
- Selection highlight appearance needs polish (multiply blend with inverted selection)

## Architecture Documentation

**See `docs/Architecture/` for complete architecture documentation:**

- **[PHYSICAL-ARCHITECTURE.md](docs/Architecture/PHYSICAL-ARCHITECTURE.md)** - File system organization, subsystems, dependencies
- **[IMPLEMENTATION-ARCHITECTURE.md](docs/Architecture/IMPLEMENTATION-ARCHITECTURE.md)** - Bytecode execution, contexts, primitives, message sending
- **[DEVELOPMENT-GUIDE.md](docs/Architecture/DEVELOPMENT-GUIDE.md)** - How to extend the interpreter (adding primitives, phases, components)
- **[PROCESS-MANAGEMENT.md](docs/Architecture/PROCESS-MANAGEMENT.md)** - Process scheduling, semaphores, priority queues
- **[PROPOSED-TIMER-OPTIMIZATION.md](docs/Architecture/PROPOSED-TIMER-OPTIMIZATION.md)** - Timer/event system, WaitHandle.WaitAny implementation

## 32-bit Migration & Graphics Documentation Map

**Status**: 32bpp graphics pipeline complete. 32-bit word migration planned but not yet started.

### Document Hierarchy

| Document | Purpose | When to Reference |
|----------|---------|-------------------|
| **`docs/32-BIT-IMPLEMENTATION-PLAN.md`** | **MASTER PLAN** - Phased roadmap with 4 milestones | Starting implementation, checking phase order, understanding dependencies |
| **`docs/SESSION-HANDOFF.md`** | Current session state, what works, next steps | Beginning a new session, checking current status |
| **`docs/Migration-to-32-bit-Words.md`** | Technical reference for word migration | Implementing HeapManager changes, understanding 16↔32 bit conversions |
| **`docs/Graphics-Architecture.md`** | Graphics system design, BitBlt, 32bpp design | Understanding graphics primitives, implementing BitBltEngine32 |
| **`docs/Form-File-Format.md`** | Legacy .form file format specification | Reading legacy files, understanding column-major layout |

### Quick Reference: Which Document for What Task

**"I need to start implementing..."** → `docs/32-BIT-IMPLEMENTATION-PLAN.md`
- Contains phased implementation order
- Lists files to modify for each phase
- Shows dependencies between phases

**"What's the current state?"** → `docs/SESSION-HANDOFF.md`
- Shows what works (build, tests, features)
- Lists open questions needing decisions
- Contains build/test commands

**"How do I change HeapManager word size?"** → `docs/Migration-to-32-bit-Words.md`
- Specific code locations to change
- Before/after code examples
- File I/O compatibility layer code

**"How does BitBlt work? How to add 32bpp?"** → `docs/Graphics-Architecture.md`
- BitBlt combination rules
- Primitive 96 implementation
- Color class and TranslucentColor design
- Depth dispatch pattern

**"How are Form files structured?"** → `docs/Form-File-Format.md`
- Binary format specification
- Column-major layout explanation
- Reading/writing algorithms

### Implementation Milestones

```
MILESTONE 1: 32-bit Words (prerequisite for everything)
    └── Phases 1.1-1.7 in 32-BIT-IMPLEMENTATION-PLAN.md

MILESTONE 2: 32bpp Graphics        MILESTONE 3: PNG I/O
    └── Phases 2.1-2.5      ←parallel→    └── Phases 3.1-3.4

MILESTONE 4: Integration
    └── Phase 4.1
```

### Key Design Decisions (Already Made)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Internal word size | 32-bit | Aligns with OOP/SmallInteger, natural for 32bpp |
| External file format | 16-bit (legacy) | Backward compatibility |
| Image format | PNG (pure Smalltalk) | Industry standard, no C# dependencies |
| Primitive acceleration | Optional | Design with clean method boundaries for later optimization |
| Supported depths | 1bpp and 32bpp only | Simplicity, no indexed colors |

## Block Context Architecture

⚠️ **CANONICAL REFERENCE**: `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md`

**Core Principle**: All method and block variables are stored in the **MethodContext's unified temp frame** using absolute indices assigned at compile time.

**Key Points**:
1. **Single Storage**: ALL variables stored in MethodContext indexed slots
2. **Absolute Indexing**: Compiler assigns unique absolute index (0, 1, 2...) to each variable
3. **BlockContext**: Contains execution state + `home` pointer to MethodContext, NO variable storage
4. **Variable Access**: Get/SetTemporaryVariable traverse to MethodContext via `home` pointer

## Running the Interpreter

**Default Behavior**: The interpreter automatically loads `coldstart.json` from the `Data` subfolder relative to the executable when no `--coldstart` parameter is provided.

### Standard Execution

```bash
cd Smalltalk80.Interpreter
dotnet run
```

Executes cold start phases 1-6 and init expressions from `Data/coldstart.json` in the executable directory.

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

# Compile Smalltalk source to generate new coldstart.json (outputs to Data subfolder by default)
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"

# Copy coldstart.json to interpreter bin directory (Windows)
copy ..\Smalltalk80.Compiler\bin\Debug\net9.0\Data\coldstart.json ..\Smalltalk80.Interpreter\bin\Debug\net9.0-windows\Data\

# Copy coldstart.json to interpreter bin directory (Linux/Mac)
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/
```

**Source File Locations**:
- **coldstart.json**: Copied to `Smalltalk80.Interpreter/bin/.../Data/` (runtime location)
- **.sources/.changes**: Written directly to `Smalltalk80.Interpreter/` project folder (opened by Smalltalk via `SystemDictionary>>initialize`)

**Important**: The `Data` folder contents in the Interpreter's bin directory are now tracked in git to ensure init expressions are always in sync with the source code.

### 🔴 MANDATORY: BlockContext Regression Test

**Run after EVERY compiler or interpreter change**:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
```

**Expected**: Returns `1191` (120 tests pass)

### Regenerate Test Bundle

```bash
cd "Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st Tests/BlockContextTestRunner.st > Smalltalk-80-with-runner.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-runner.st"
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/coldstart.json ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
```

### Critical Files Requiring Test

Changes to these files MUST pass the regression test:
- `BytecodeCompiler.cs` - bytecode generation
- `VariableResolver.cs` - variable resolution
- `TempFrameAllocator.cs` - variable allocation
- `VirtualMachine.cs` - execution
- `ControlPrimitives.cs` - BlockCopy, BlockValue primitives

### C# Unit Tests

**Status**: ✅ **385/385 tests passing (100%)**

Run the comprehensive C# unit test suite:

```bash
cd Smalltalk80.Interpreter.Tests
dotnet test
```

**Test Coverage**:
- **Primitives**: All primitive operations including arithmetic, comparison, control flow, strings, floats, objects, and system operations
- **HeapManager**: Object allocation, garbage collection, memory management, and heap operations

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
- **WindowsFileStream logicalSize Tracking (2026-01-01)**: Class comments in .changes file were missing `!` terminators. Root cause: `WindowsFileStream>>size` queried filesystem which doesn't reflect buffered writes. When `setToEnd` called `self position: self size`, it got stale size, positioning before the just-written `!`, and next write overwrote it. Fix: Added `logicalSize` instance variable to track writes locally (like original Alto code). `SourceFiles` initialization moved from C# cold start to pure Smalltalk in `SystemDictionary>>initialize`.
- **SingleCharSymbols Initialization (2025-12-01)**: System Browser crashed with "nil ," error when viewing methods like `Cursor>>changed:`. Root cause: `Symbol internCharacter:` returned nil because SingleCharSymbols wasn't properly populated during cold start. Fix: (1) Compiler now includes Blue Book canonical binary selectors (`+ - * / < > = ~ & | @ , \ ? ! %`) in symbol table, (2) Phase 5 cold start populates SingleCharSymbols array with these Symbol OOPs from the bundle.
- **BOM Double-Offset Bug (2025-11-30)**: Primitive 124 (FileSeek) returned physical position instead of logical, causing primitive 132 (FileReadByte) to add BOM offset twice. Fixed to return logical position.
- **BlockContext Stack Underflow (2025-11-30)**: Blue Book block arg handling pushes args onto stack, but StackManager used old `initialSP=nargs` model. Fixed to use `initialSP=0` for BlockContexts.
- **CharacterTable Implementation**: Phase 3 creates CharacterTable Array with 256 Character objects. Phase 5 installs it as CLASS VARIABLE in Character's classPool (following USTable pattern, NOT as a global).
- **Primitive 121 (FileOpen)**: Fixed to support both 2-arg (path, mode string) and 4-arg (path, access, share, create integers) calling conventions for WindowsFile compatibility.
- **Primitive 110 (Character literal identity)**: Fixed comparison bug with runtime canonicalization using CharacterRegistry.
- **Primitive 65 (Stream>>next)**: Fixed to return Character objects (not SmallIntegers) when reading from Strings/Symbols, enabling Scanner and --execute feature to work correctly. Uses isKindOf: semantics to handle subclasses properly.
- Added InputSensor install and InputSensor initSensor init expressions to properly initialize Sensor global
- Fixed Cursor initialize to set CurrentCursor (was nil, causing cursorPoint failures)
- Modified Object initialize to comment out GUI confirmation dialog while preserving DependentsFields initialization

## Development Methodology

- **Never perform workarounds** - Always find and fix root cause
- **Trust source code over analysis** - When agent analysis contradicts source, trust source
- **Use instruction tracing** - 5K+ instructions indicates infinite loop
- **Validate with tests** - BlockContextTest suite validates core functionality

## Threading and Synchronization Guidelines

**Two-Thread Architecture**:
- **UI Thread (WinForms)**: Runs message loop, handles display, enqueues input events
- **Smalltalk Thread**: Executes bytecodes, processes events from queue

**Event Queue Pattern**:
- Producer (UI thread) calls `Enqueue()` then `Set()` on signal
- Consumer (Smalltalk thread) uses `WaitHandle.WaitAny` then `ResetSignalIfEmpty()`
- Always use `ManualResetEventSlim` (not AutoResetEvent) with WaitAny
- Double-check pattern required: reset, then check if producer added, re-signal if needed

**Timer Pattern**:
- Use `System.Threading.Timer` for interrupt-driven precision
- Expose `TimerExpiredHandle` (ManualResetEventSlim.WaitHandle) for WaitAny
- Timer callback only sets signal; VM thread checks expiration and signals semaphore

**Key Insight**: AutoResetEvent auto-resets when WaitAny returns, consuming the signal before the event can be processed. ManualResetEventSlim requires explicit reset but allows proper event handling.

## Model Selection

Always use model: **claude-sonnet-4-5**
