# Claude Development Guidelines

**Last Updated**: 2025-12-03

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 51/51 succeed (100% success rate)
- **Comment Expressions**: ✅ **234/234 succeed** (100% success rate)
- **BlockContext Tests**: ✅ **120/120 tests PASS** (returns 1191)
- **VM Core**: Fully functional, verified operational
- **Process Primitives**: ✅ **85-88 working** (suspend, resume, terminate, yield)
- **System Browser**: ✅ **FULLY FUNCTIONAL** - source retrieval working
- **Timer/Event System**: ✅ **FULLY OPERATIONAL** - 0% idle CPU via dual-semaphore architecture

## Known Issues

**✅ Control Structure Optimization - IMPLEMENTED**
The C# compiler now inlines `ifTrue:`, `ifFalse:`, `ifTrue:ifFalse:`, `ifFalse:ifTrue:`, `and:`, and `or:` using Blue Book conditional jump bytecodes. No BlockContext is created for zero-argument blocks in these control structures.

**Remaining**: `whileTrue:` and `whileFalse:` are NOT yet inlined (receiver is a block, requires special handling).

**Parser Token Recognition**: The `--execute` feature currently fails with Parser error "Nothing more expected" at position 1. The Scanner is not properly tokenizing number literals ("4" produces `tokenType: #xDigit` instead of `#number`, and `hereType` remains nil). Root cause under investigation.

**✅ Idle CPU Usage - RESOLVED**: The original Blue Book `Processor yield` created a forked process that signaled immediately, keeping priority 4 always occupied and preventing true idle. **Fix**: Dual-semaphore architecture with mouse event queue:
- **InputSemaphore**: VM signals on raw input; InputProcess waits
- **ControllerSemaphore**: InputProcess signals after enqueuing mouse event; controllers wait
- **MouseEventQueue**: FIFO queue of discrete mouse events (mirrors keyboard queue design)
- **Key**: Controllers consume events from queue, ensuring no transitions are missed

This guarantees controllers see EVERY mouse event, enables 0% idle CPU, and eliminates missed button transitions. See `docs/Architecture/MOUSE-EVENT-QUEUE.md` for complete design.

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
- ✅ Compiler generates `.sources` file with chunk-formatted source
- ✅ Source positions encoded in CompiledMethod bytecodes (last 3 bytes)
- ✅ `SourceFiles` global initialized with WindowsFileReference objects
- ✅ UTF-8 BOM handling in file primitives (transparent offset compensation)
- ✅ `CompiledMethod>>getSource` retrieves source via `RemoteString`

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
- ✅ **CPU Efficiency**: 0% CPU during idle via mouse event queue architecture

**Key Implementation Details**:

**Mouse Event Queue Architecture**: Queue-based mouse handling that mirrors the keyboard queue design:

1. **InputSemaphore** - VM signals when raw input arrives; InputProcess (priority 6) waits
2. **ControllerSemaphore** - InputProcess signals after enqueuing mouse event; controllers wait
3. **MouseEventQueue** - FIFO SharedQueue of discrete MouseEvent objects

```smalltalk
ProcessorScheduler>>yield
    "Give other Processes at the current priority a chance to run.
     Modern integration: block on ControllerSemaphore for true idle suspension."
    Sensor waitForEvent

InputSensor>>nextMouseEvent
    "Answer next mouse event or nil if none."
    ^InputState mouseEventQueue isEmpty
        ifTrue: [nil]
        ifFalse: [InputState mouseEventQueue next]

InputSensor>>waitForControllerEvent
    "Block until a mouse event is available."
    (self anyButtonPressed or: [self keyboardPressed]) ifTrue: [^self].
    InputState controllerSemaphore wait
```

**Key Design Decision**: Controllers consume discrete MouseEvent objects from a FIFO queue instead of polling `bitState`. This guarantees EVERY mouse transition is seen - no events are lost even during rapid clicking or when controllers are busy.

**Queue Protocol**:
1. InputProcess dequeues OS event, updates bitState for compatibility
2. InputProcess enqueues MouseEvent to queue
3. InputProcess signals ControllerSemaphore (no handshake needed)
4. Controller wakes, dequeues events from MouseEventQueue

This achieves 0% idle CPU with reliable event delivery and no deadlocks.

**Composite Event Model**: Primitive 95 (`primInputWord`) returns a composite event Array instead of packed integer words:
```smalltalk
event _ self primInputWord.  "Returns #(eventType x y buttonMask hasTransition keyCode isKeyDown) or nil"
event == nil ifFalse:
    [eventType _ event at: 1.
    eventType = 1 ifTrue:  "Mouse"
        [x _ event at: 2. y _ event at: 3. newButtons _ event at: 4].
    eventType = 2 ifTrue:  "Keyboard"
        [keyCode _ event at: 6. isKeyDown _ event at: 7]]
```
This eliminates the inner loop and sentinel parsing from the old word-based format.

**ManualResetEventSlim Pattern**: The original AutoResetEvent caused event loss because it auto-resets when WaitAny returns. The fix uses ManualResetEventSlim with explicit reset:

```csharp
// After WaitAny returns, reset signal only if queue is empty
public void ResetSignalIfEmpty()
{
    if (!HasPendingEvents)
    {
        _eventSignal.Reset();
        // Double-check: producer might have added after check but before reset
        if (HasPendingEvents)
            _eventSignal.Set();
    }
}
```

**Key Files**:
- `Smalltalk-80.st` - `ProcessorScheduler>>yield` calls `Sensor waitForEvent`
- `Smalltalk-80.st` - `InputState` class with `InputSemaphore`, `ControllerSemaphore`, `MouseEventQueue`
- `Smalltalk-80.st` - `InputState>>run` enqueues MouseEvents to queue
- `Smalltalk-80.st` - `InputState>>primInputWord` returns composite event Array (primitive 95)
- `Smalltalk-80.st` - `InputSensor>>nextMouseEvent` dequeues from MouseEventQueue
- `Smalltalk-80.st` - `InputSensor>>waitForControllerEvent` waits on ControllerSemaphore
- `SmalltalkEventQueue.cs` - `DequeueCompositeEvent()` returns event data as tuple
- `InputPrimitives.cs` - Primitive 95 builds composite Array from dequeued event
- `VirtualMachine.cs` - InterpreterLoop uses `WaitHandle.WaitAny([eventHandle, timerHandle])`
- `VirtualMachine.ProcessScheduling.cs` - Extracted process scheduling methods (~330 lines)
- `SmalltalkEventQueue.cs` - ManualResetEventSlim with ResetSignalIfEmpty()
- `TimerManager.cs` - Interrupt-driven timer with TimerExpiredHandle
- `docs/Architecture/MOUSE-EVENT-QUEUE.md` - **Complete queue-based architecture documentation**

See `docs/Architecture/PROPOSED-TIMER-OPTIMIZATION.md` and `docs/Architecture/PROCESS-MANAGEMENT.md` for complete documentation.

See `docs/PROGRAM-REFACTORING-PLAN.md`, `docs/WINDOW-SIZING-ARCHITECTURE.md`, `docs/SYSTEM-STARTUP-IMPLEMENTATION-PLAN.md`, and `docs/SOURCE-LOOKUP-ANALYSIS.md` for complete documentation.

## Architecture Documentation

**See `docs/Architecture/` for complete architecture documentation:**

- **[PHYSICAL-ARCHITECTURE.md](docs/Architecture/PHYSICAL-ARCHITECTURE.md)** - File system organization, subsystems, dependencies
- **[IMPLEMENTATION-ARCHITECTURE.md](docs/Architecture/IMPLEMENTATION-ARCHITECTURE.md)** - Bytecode execution, contexts, primitives, message sending
- **[DEVELOPMENT-GUIDE.md](docs/Architecture/DEVELOPMENT-GUIDE.md)** - How to extend the interpreter (adding primitives, phases, components)
- **[PROCESS-MANAGEMENT.md](docs/Architecture/PROCESS-MANAGEMENT.md)** - Process scheduling, semaphores, priority queues
- **[PROPOSED-TIMER-OPTIMIZATION.md](docs/Architecture/PROPOSED-TIMER-OPTIMIZATION.md)** - Timer/event system, WaitHandle.WaitAny implementation

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

# Copy Data folder contents to interpreter bin directory (Windows)
copy ..\Smalltalk80.Compiler\bin\Debug\net9.0\Data\* ..\Smalltalk80.Interpreter\bin\Debug\net9.0-windows\Data\

# Copy Data folder contents to interpreter bin directory (Linux/Mac)
cp -r ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/* ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/
```

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
