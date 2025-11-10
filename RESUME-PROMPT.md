# Smalltalk-80 Interpreter - Resume Context

**Last Updated**: 2025-01-10
**Build Status**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ All 6 phases succeed
**Latest Commit**: `2df0090` - Fix critical Float/Character bugs and refactor temp frame handling

---

## Recent Accomplishments (2025-01-10)

### Critical Bug Fixes Completed ✅

#### 1. Float Object Format Decoding Bug
**Problem**: Float initialize was crashing with "nil class" error. Float objects were incorrectly decoded as PointerIndexed instead of WordIndexed.

**Root Cause**: `VirtualMachine.AllocateObject()` format decoder wasn't checking bit 3 (pointer flag) to distinguish between PointerIndexed and WordIndexed objects.

**Fix Applied**: [VirtualMachine.cs:3651](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs#L3651)
```csharp
bool hasPointerIndexedSlots = (formatValue & 0x08) != 0;  // Bit 3
2 => hasPointerIndexedSlots ? ObjectFormat.PointerIndexed : ObjectFormat.WordIndexed
```

**Result**: Float initialize (#118) now passes ✅

#### 2. Block Temporary Frame Corruption Bug
**Problem**: Character initialize was failing because block temps corrupted method temp frame, causing wrong bytecode indices.

**Root Causes**:
1. `TemporaryCount` was incremented during block compilation but never restored
2. Block temp base index didn't account for block arguments, causing index collisions

**Fixes Applied**: [BytecodeCompiler.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\BytecodeCompiler.cs)
- Line 2085: Save `TemporaryCount` before block compilation
- Line 2121: Fixed temp base: `tempBase = ArgumentCount + TemporaryCount`
- Line 2188: Restore `TemporaryCount` after block compilation

**Result**: Character initialize (#52) now passes ✅

#### 3. Float Allocation Consistency
**Problem**: Multiple `AllocateFloat()` implementations were inconsistent (some used ByteIndexed).

**Fix**: Standardized all Float allocation to use WordIndexed format with 4 words:
- [SemanticAllocator.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\SemanticAllocator.cs)
- [Phase3_ObjectAllocation.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Engine\LoaderPhases\Phase3_ObjectAllocation.cs)
- [Phase4_MethodCompilation.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Engine\LoaderPhases\Phase4_MethodCompilation.cs)

### Refactoring: Semantic Clarity ✅

**Rename**: `TemporaryCount` → `TemporariesAllocatedInFrame`

**Motivation**: Clarify that this tracks total temp slots allocated (named + hidden), not just named temps from `| temp |` declarations.

**Files Modified**:
- [BytecodeCompiler.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\BytecodeCompiler.cs): 12 locations updated
- [VariableResolver.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\VariableResolver.cs): 3 locations updated

**Preserved**: `CompiledMethodData.TemporaryCount` (different semantic - output metadata, not internal state)

**Distinction Now Clear**:
- `TemporariesAllocatedInFrame`: Total frame size (named + hidden temps)
- `Temporaries.Count`: Only named temps from `| temp |`
- `HiddenTemporaries.Count`: Only hidden temps (cascades, etc.)

---

## Current Project Status

### Init Expression Execution Status

**Passing** (as of latest test):
- ✅ SmallInteger initialize (#207)
- ✅ Float initialize (#118) - NEWLY FIXED
- ✅ Character initialize (#52) - NEWLY FIXED

**Next Failure** (not yet investigated):
- ❌ BinaryChoiceView initialize (#30) - Next item to debug

### Test Files Available

Located in `Smalltalk-80 Source/Tests/`:
- `BlockArgTest.st` - Tests block argument corruption bug
- `BlockArgTempTest.st` - Tests block arg/temp index collision (validates fix #2)
- `FloatTest.st` - Tests Float object format and arithmetic (validates fix #1)
- `TempTest7.st` - Tests nested blocks with outer temp access
- `ContextAccessTest.st` - Context access validation tests
- `SimpleNestedBlockTest.st` - Nested block tests
- `BlockContextTest.st` - Comprehensive 120-test suite (all passing)

### Running Tests

**Full Init Expression Run**:
```bash
cd Smalltalk80.Interpreter
dotnet run
```

**Run Specific Test**:
```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json \
              --skip-init-expressions \
              --execute "TestClassName run"
```

**Flags**:
- `--skip-init-expressions`: Skip init expression execution (avoids known bugs)
- `--execute <expr>`: Execute specific Smalltalk expression after cold start

**Important**: Use `dotnet run` instead of running .exe directly on Windows (output buffering issues)

---

## Architecture Overview

### Cold Start Process (6 Phases)

1. **Phase 1**: Preflight + Symbol Interning (3,834 symbols)
2. **Phase 2**: Class Infrastructure (470 classes)
3. **Phase 3**: Object Allocation (pre-allocated objects from JSON)
4. **Phase 4**: Method Compilation (4,623 methods, creates Float literals)
5. **Phase 5**: Globals + Patches (246 globals)
6. **Phase 6**: Validation + GC Verification

All phases currently passing ✅

### Object Format Encoding

**Format Field** (16 bits):
- **Bits 13-12**: Instance Specification
  - `00` = no indexed fields
  - `01` = pointer fields only (subclass:)
  - `10` = word or pointer indexed (variableSubclass: or variableWordSubclass:)
  - `11` = byte indexed (variableByteSubclass:)
- **Bits 11-6**: Fixed field count (0-63)
- **Bit 4**: Indexable flag (1 = has indexed portion)
- **Bit 3**: Pointer flag ⚠️ **CRITICAL FOR BIT 10 CASE**
  - `1` = indexed slots contain object pointers (variableSubclass:)
  - `0` = indexed slots contain 16-bit words (variableWordSubclass:)

**Critical Distinction** (instanceSpec = 10b):
- `variableSubclass:` (bit 3=1) → PointerIndexed (Array, OrderedCollection)
- `variableWordSubclass:` (bit 3=0) → WordIndexed (Float, Bitmap, WordArray)

**Reference**: [ColdStartJsonGenerator.CalculateFormatWord()](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\CodeGen\ColdStartJsonGenerator.cs#L468)

### Float Storage Format

**Blue Book Specification**: Float is `Number variableWordSubclass: #Float`

**Storage**: 64-bit IEEE 754 double stored as **4x 16-bit words** (not 2x 32-bit)
- Preserves little-endian byte order
- Matches Blue Book Word-based architecture
- WordIndexed object format, 0 fixed fields, 4 indexed words

### Block Compilation Architecture

**Inline Block Approach**: Blocks share parent method's temp frame during compilation

**Key Compilation Rules**:
1. Block args are added temporarily to environment during compilation
2. Block temps extend method temp frame **during** compilation only
3. `TemporariesAllocatedInFrame` **MUST be saved and restored** after block ends
4. Block temp base index = `ArgumentCount + TemporariesAllocatedInFrame`
   - **Must include block args** to avoid collision!
   - Example: `[:i | | char | ...]` → i=0, char=1 (NOT char=0)

### Temp Frame Structure

Three distinct concepts (now clearly named):

1. **Named temporaries**: Visible in source `| temp |`, tracked in `Temporaries` dictionary
2. **Hidden temporaries**: Implementation details (cascades), tracked in `HiddenTemporaries`
3. **TemporariesAllocatedInFrame**: Total slots allocated = named + hidden

---

## Key Source Files

### Compiler

- **[BytecodeCompiler.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\BytecodeCompiler.cs)**: Main bytecode compiler (2700+ lines)
  - Line 71: `TemporariesAllocatedInFrame` property definition with documentation
  - Lines 102-104: `AllocateHiddenTemp()` method
  - Lines 2078-2189: Block compilation logic ⚠️ **CRITICAL BUG FIXES HERE**
    - Line 2085: Save TemporariesAllocatedInFrame before block
    - Line 2121: Block temp base includes ArgumentCount
    - Line 2188: Restore TemporariesAllocatedInFrame after block
  - Line 2658: Quick-return optimization check

- **[VariableResolver.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\VariableResolver.cs)**: Variable resolution (9-step priority)
  - Line 259: Parent variable offset calculation uses TemporariesAllocatedInFrame
  - Line 294: Parent local accumulation uses TemporariesAllocatedInFrame
  - Line 322: Block local offset calculation uses TemporariesAllocatedInFrame

- **[CompilerDataTypes.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\CompilerDataTypes.cs)**: Data structures
  - Line 40: `CompiledMethodData.TemporaryCount` ⚠️ **NOT renamed** - represents output metadata

### Interpreter

- **[VirtualMachine.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs)**: VM execution (5200+ lines)
  - Line 2068: Special selector 'class' with error handling
  - Line 3651: Object format decoding fix ⚠️ **CRITICAL FIX**
    - Added bit 3 check to distinguish WordIndexed from PointerIndexed

- **[SemanticAllocator.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\SemanticAllocator.cs)**:
  - `AllocateFloat()`: Creates Float with WordIndexed format, 4 words

- **[Phase3_ObjectAllocation.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Engine\LoaderPhases\Phase3_ObjectAllocation.cs)**:
  - Pre-allocated object creation (calls SemanticAllocator)

- **[Phase4_MethodCompilation.cs](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Engine\LoaderPhases\Phase4_MethodCompilation.cs)**:
  - Method compilation during cold start (creates Float literals, calls SemanticAllocator)

### Blue Book Source

- **[Smalltalk-80.st](D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk-80 Source\Smalltalk-80.st)**: Complete Blue Book source
  - Line 18658: `Number variableWordSubclass: #Float` - Float class definition
  - Line 7573: Character initialize (now working ✅)
  - Line 52: Character class definition with CharacterTable class variable

---

## Important Patterns & Lessons Learned

### 1. Always Check Bit 3 for Object Format

When decoding object format from format field with instanceSpec=10b:

```csharp
// WRONG - assumes all 10b are PointerIndexed
2 => ObjectFormat.PointerIndexed

// CORRECT - check bit 3 to distinguish
bool hasPointerIndexedSlots = (formatValue & 0x08) != 0;
2 => hasPointerIndexedSlots ? ObjectFormat.PointerIndexed : ObjectFormat.WordIndexed
```

**Why**: Both variableSubclass: and variableWordSubclass: use instanceSpec=10b. Only bit 3 differentiates them.

### 2. Block Temp Frame Must Be Restored

When compiling blocks in inline-block architecture:

```csharp
// REQUIRED pattern:
int savedTemporariesAllocatedInFrame = environment.TemporariesAllocatedInFrame;
// ... compile block, extend temp frame ...
environment.TemporariesAllocatedInFrame = savedTemporariesAllocatedInFrame;  // RESTORE!
```

**Why**: Block temps extend the frame during compilation, but shouldn't persist in method header's tempCount.

### 3. Block Temp Base Includes Block Args

When calculating block temp base index:

```csharp
// WRONG - temps collide with args
int tempBase = environment.TemporariesAllocatedInFrame;

// CORRECT - account for block args
int tempBase = environment.ArgumentCount + environment.TemporariesAllocatedInFrame;
```

**Why**: Example `[:i | | char | ...]` needs i=0, char=1, not i=0, char=0 (collision).

### 4. Float Storage: 4 Words, Not 2

IEEE 754 double (64-bit) stored as **4x 16-bit words**, not 2x 32-bit words.

**Why**:
- Preserves little-endian byte order
- Matches Blue Book Word-based architecture
- Consistent with variableWordSubclass: semantics

### 5. Semantic Naming Matters

`TemporaryCount` was ambiguous. Renamed to `TemporariesAllocatedInFrame` to clearly distinguish:
- Total frame size (what we renamed)
- vs. Count of named temps only (`Temporaries.Count`)
- vs. Count of hidden temps (`HiddenTemporaries.Count`)

---

## Next Steps / Known Issues

### Immediate Next Task

**Debug BinaryChoiceView initialize (#30)** - First failing init expression after Float/Character fixes

**Command**:
```bash
cd Smalltalk80.Interpreter
dotnet run
```

Watch output for first failure after Float and Character pass.

### Investigation Approach

1. Identify failure type (method not found, nil receiver, infinite recursion, etc.)
2. If method not found: Check method compilation in Phase 4
3. If nil receiver: Check object allocation in Phase 3
4. If infinite recursion: Check for circular dependencies in init expressions
5. Add targeted test case in `Smalltalk-80 Source/Tests/` if reproducible
6. Fix root cause (no workarounds!)
7. Verify with runtime test

---

## Development Guidelines

### Methodology

- **Never perform workarounds** - Always find and fix root cause
- Use Blue Book (Goldberg & Robson, 1983) as authoritative source
- Verify fixes with actual runtime tests, not just compilation
- Add test cases for all bugs fixed

### Build Commands

```bash
# Compiler
cd Smalltalk80.Compiler
dotnet build --no-incremental

# Interpreter
cd Smalltalk80.Interpreter
dotnet build --no-incremental

# Run interpreter
cd Smalltalk80.Interpreter
dotnet run
```

### Git Workflow

```bash
git status
git add <files>
git commit -m "Detailed message with problem/solution"
git push
```

**Commit Message Format**:
- Detailed explanation of problem and solution
- Include file/line references for key changes
- End with: `🤖 Generated with [Claude Code](https://claude.com/claude-code)`
- Add: `Co-Authored-By: Claude <noreply@anthropic.com>`

---

## Critical Principles (from CLAUDE.md)

### No Hardcoded OOP Values

```csharp
// WRONG
return new SmalltalkOOP(0);
var nilOOP = new SmalltalkOOP(0);

// CORRECT
return SpecialObjectsArray.Instance.Nil;
var nilOOP = SpecialObjectsArray.Instance.Nil;
```

### Array Initialization Rule

C# arrays default to `SmalltalkOOP(0)` which is NOT nil:

```csharp
var slots = new SmalltalkOOP[size];
var nilOOP = SpecialObjectsArray.Instance.Nil;
for (int i = 0; i < size; i++) {
    slots[i] = nilOOP;  // REQUIRED
}
```

### Cold Start vs Runtime Separation

**Cold Start Phase** (Phases 1-6):
- Can use `ColdStartBundle`, registries (write), frozen data
- Marked with `[ColdStartOnly]` attribute

**Runtime Phase** (Init expressions onward):
- ONLY `ISmalltalkHeapManager` and `LayoutResolver`
- Marked with `[RuntimeOnly]` attribute
- Registries are frozen (read-only after Phase 5)

### No Hardcoded Indices at Runtime

```csharp
// WRONG
var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 6, stackSize);

// CORRECT
int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");
var contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
```

---

## References

### Key Documentation

- **[CLAUDE.md](D:\Users\brent\OneDrive\Src\Smalltalk-80\CLAUDE.md)**: Complete development history and guidelines ⚠️ **READ THIS FIRST**
- **[Variable-Indexing-Architecture.md](D:\Users\brent\OneDrive\Src\Smalltalk-80\docs\Variable-Indexing-Architecture.md)**: Variable resolution specification
- **[Interpreter-Architecture.md](D:\Users\brent\OneDrive\Src\Smalltalk-80\docs\Interpreter-Architecture.md)**: VM architecture and BlockClosure support
- **[COLDSTART-PHASES-DETAILED.md](D:\Users\brent\OneDrive\Src\Smalltalk-80\docs\COLDSTART-PHASES-DETAILED.md)**: Cold start loader architecture

### Blue Book Specification

**Goldberg & Robson, "Smalltalk-80: The Language and its Implementation", 1983**

Key chapters for current work:
- **Chapter 26**: Object Memory (object format encoding - critical for Float bug)
- **Chapter 28**: Contexts (MethodContext/BlockContext structure)
- **Chapter 30**: Primitives (primitive method implementations)

---

## Session Continuation Checklist

When resuming work:

1. ✅ Read [CLAUDE.md](D:\Users\brent\OneDrive\Src\Smalltalk-80\CLAUDE.md) for latest development history
2. ✅ Verify build status: `dotnet build --no-incremental` (should be 0 errors)
3. ✅ Check git status: `git status` (should be clean)
4. ✅ Review this RESUME-PROMPT.md for session context
5. ✅ Run cold start to confirm all 6 phases pass
6. ✅ Identify next failing init expression (currently: BinaryChoiceView #30)
7. 🔍 Debug root cause (no workarounds!)
8. ✅ Add test case if reproducible
9. ✅ Verify fix with runtime test
10. ✅ Commit with detailed message
11. ✅ Update CLAUDE.md with findings

---

## Current Status Summary

**Build**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ All 6 phases succeed
**Fixed This Session**:
- ✅ Float initialize (#118) - object format decoding bug
- ✅ Character initialize (#52) - block temp frame corruption
- ✅ Float allocation consistency across all code paths

**Next Target**: ❌ BinaryChoiceView initialize (#30)

**Latest Commit**: `2df0090` - Fix critical Float/Character bugs and refactor temp frame handling

---

**Remember**: The rename `TemporaryCount` → `TemporariesAllocatedInFrame` clarifies that this is the total temp frame size (named + hidden), not just the count of named temps. This distinction is critical for understanding block compilation.
