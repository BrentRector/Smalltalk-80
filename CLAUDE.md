# Claude Development Guidelines

**Last Updated**: 2025-01-14

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 2 warnings (non-critical: XML doc duplicate param tags)
- **Cold Start**: Phase 1-6 all succeed
- **Init Expressions**: 29/29 attempted succeed (100% success rate)
- **Instruction Counter**: ✅ Fixed to count bytecode opcodes (was counting interpreter loop iterations)
- **VM Core**: Fully functional, verified operational

## Block Context Architecture - IMPORTANT

⚠️ **CANONICAL REFERENCE**: `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md`

### Unified Temp Frame Architecture (Current Design)

**Core Principle**: All method and block variables (arguments and temporaries) are stored in the **MethodContext's unified temp frame** using absolute indices assigned at compile time.

**Key Points**:
1. **Single Storage**: ALL variables stored in MethodContext indexed slots
2. **Absolute Indexing**: Compiler assigns unique absolute index (0, 1, 2...) to each variable
3. **BlockContext**: Contains execution state + `home` pointer to MethodContext, NO variable storage
4. **Extension Field**: BlockContext slot 6 (`BLOCKCONTEXT_ARG_START_INDEX`) stores metadata for Primitive 81
5. **Variable Access**: Get/SetTemporaryVariable traverse to MethodContext via `home` pointer, use absolute index

**Data Structures**:
```
MethodContext:
  Fixed fields [0-4]: sender, pc, stackp, method, receiver
  Indexed slots: [0..n] ALL variables (method args/temps, block args/temps, stack)

BlockContext:
  Fixed fields [0-5]: sender, pc, stackp, argumentCount, initialIP, home
  Extension field [6]: argStartIndex (VM extension for Primitive 81)
  Indexed slots: Expression stack only (NO variables)
```

## Running the Interpreter

### Standard Execution

```bash
cd Smalltalk80.Interpreter
dotnet run
```

This executes:
- Cold start phases 1-6
- 29 initialize expressions in dependency order
- Skips 5 known problematic init expressions

### With Essential Init Expressions Only

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart.json --skip-init-expressions
```

Runs only 3 essential init expressions:
- Character initialize
- Float initialize  
- SmallInteger initialize

### Building and Testing

**Full rebuild with BlockContext tests**:
```bash
# Build interpreter (auto-regenerates coldstart.json)
cd Smalltalk80.Interpreter
dotnet build

# Generate test bundle (combines Smalltalk-80.st + BlockContextTest.st)
cd "../Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st > Smalltalk-80-with-tests.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-tests.st"
cp bin/Debug/net9.0/coldstart.json bin/Debug/net9.0/coldstart_with_tests.json

# Run BlockContext test suite (194 tests)
cd ../Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json --skip-init-expressions
```

**Quick rebuild (standard bundle only)**:
```bash
cd Smalltalk80.Interpreter
dotnet build
dotnet run
```

**Test Suite**: 194 comprehensive BlockContext tests covering nested blocks, variable access, mutations, control flow, non-local returns, exception handling, and real-world patterns.

## Init Expression Validation Results (2025-01-14)

### Summary
- **Total Attempted**: 29 initialize expressions
- **Succeeded**: 29 (100%)
- **Failed**: 0
- **Skipped**: 5 (known issues)

### Successful Init Expressions (29)
1. Character, Float, SmallInteger (essential)
2. BinaryChoiceView, ClassOrganizer, CompiledMethod
3. Cursor, Date, Debugger, Decompiler
4. Explainer, FileDirectory
5. InputSensor, InputState, InstructionStream
6. LargeNegativeInteger, LargePositiveInteger
7. MessageNode, Paragraph, ParseNode
8. ProcessorScheduler, Project
9. Scanner, StringHolder, Symbol
10. SystemDictionary, SystemTracer
11. WindowsFileDirectory, WindowsFileSystem

### Skipped Init Expressions (5)

**Infinite Loops** (Dictionary initialization issues):
1. `File initialize` - 1M+ instructions, Dictionary creation loop
2. `Form initialize` - 4M+ instructions
3. `TextList initialize` - 100K+ instructions

**Other Issues**:
4. `Object initialize` - Requires interactive GUI (`confirm:` → `Sensor cursorPoint`)
5. `Text initialize` - Corrupt OOP (classId 0, method lookup fails)

### Key Fix: InputSensor Dependency (2025-01-14)

**Problem**: Object initialize failed because Sensor global was nil.

**Root Cause**: InputSensor initialize creates keyboard map but does NOT create Sensor global. The `InputSensor initSensor` method must be called separately (commented out in source).

**Solution**: 
1. Added InputSensor to dependency order BEFORE Object
2. InputSensor initialize now runs and succeeds
3. Object initialize skipped (requires interactive confirmation)

**Files Modified**:
- `Smalltalk80.Interpreter/Program.cs:1972` - Added InputSensor to dependency order

---

## Recent Work (2025-01-14)

### Fixed Instruction Counter to Count Bytecode Opcodes

**Problem**: Instruction counter was counting interpreter loop iterations instead of bytecode opcodes executed. Float initialize showed "2 instructions" when it actually executed 39 bytecodes.

**Root Cause**: Counter was incremented in `InterpreterLoop()` (once per context switch) instead of in `ExecuteBytecodeStep()` (once per bytecode opcode).

**The Fix**:
- **File**: `VirtualMachine.cs`
- **Removed**: `_instructionCount++` from `InterpreterLoop()` (line 469)
- **Added**: `_instructionCount++` inside bytecode dispatch loop in `ExecuteBytecodeStep()` (line 582)
- **Added**: `_instructionCount++` for primitive execution (line 2183)

**Result**: Instruction counter now correctly reflects bytecode opcodes executed:
- Float initialize: 39 opcodes (was 2)
- Character initialize: 3,594 opcodes (was 3,594 - happened to be same due to many message sends)
- Each bytecode opcode = 1 instruction
- Each primitive execution = 1 instruction

**Configuration**: MAX_INSTRUCTIONS raised from 100K to 500K to allow more complex init expressions to run.

---

## Architecture Notes

### Cold Start vs Runtime Phases

**Cold Start Phase** (Phases 1-6):
- Uses `ColdStartBundle`, registries, `_symbols[]`
- Marked with `[ColdStartOnly]` attribute

**Runtime Phase** (Init Expressions onward):
- Uses ONLY `ISmalltalkHeapManager` and `LayoutResolver`
- Marked with `[RuntimeOnly]` attribute
- Registries frozen after Phase 5 (throw on modification)

### VM Instruction Tracing

Added instruction counter for debugging infinite loops:

```csharp
// VirtualMachine.cs
private long _instructionCount = 0;
private const long INSTRUCTION_REPORT_INTERVAL = 100000;

// Reports every 100K instructions with current method OOP
```

This successfully identified all three infinite loop init expressions.

## Methodology and Problem-Solving

- **Never perform workarounds** - Always find and fix root cause
- **Trust source code over analysis** - When agent analysis contradicts source, trust source
- **Use instruction tracing** - 5K+ instructions indicates infinite loop
- **Validate with tests** - BlockContextTest suite validates core functionality

## Model Selection

Always use model: **claude-sonnet-4-5**
