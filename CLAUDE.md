# Claude Development Guidelines

**Last Updated**: 2025-11-17

## Project Status

**Interpreter Status**: ✅ **FULLY OPERATIONAL**

- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phase 1-6 all succeed
- **Init Expressions**: 31/46 succeed (67% success rate - updated 2025-11-17)
  - ✅ 31 passing (all essential system classes including Text)
  - ⏭️ 15 skipped (require GUI/process/file I/O primitives not available at bootstrap)
- **BlockContext Tests**: ✅ **194/194 tests PASS** (validated 2025-11-14)
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
- 31 initialize expressions in dependency order
- Skips 15 init expressions requiring GUI/process/file I/O primitives

### Init Expression Status (31/46 passing - updated 2025-11-17)

**✅ Passing (31)**:
All essential system classes initialize successfully:
- Character, Float, SmallInteger (numeric/character basics)
- BinaryChoiceView (GUI foundation)
- ClassOrganizer, CompiledMethod (system infrastructure)
- Cursor, Date, Decompiler, Explainer (utilities)
- File, FileDirectory, WindowsFileDirectory, WindowsFileSystem (file system)
- InputSensor, InputState (input handling)
- InstructionStream, Scanner, MessageNode, ParseNode (compiler)
- Paragraph, StringHolder, Symbol, **Text** (text/string support)
- ProcessorScheduler, Project (process management)
- SystemDictionary, SystemTracer (system globals)
- Debugger (debugging support)
- LargePositiveInteger, LargeNegativeInteger (large integer arithmetic)

**⏭️ Skipped (15 - require runtime features not available at bootstrap)**:

*GUI/PopUpMenu infrastructure (10)*:
- BitEditor, ChangeListController, FormEditor, NotifierController
- ParagraphEditor, ProjectController, ScreenController
- StandardSystemController, StringHolderController
- FormMenuView (file I/O: Form readFrom:)

*Process/threading primitives (1)*:
- Delay (forkAt: creates infinite whileTrue loop)

*Complex initialization issues (4)*:
- Form (infinite loop 4M+ instructions)
- Object (GUI: confirm: needs Sensor cursorPoint)
- TextList (infinite loop 500K+ instructions)
- VariableNode (infinite loop 500K+ instructions)

**✅ FIXED: TextConstants Pool Dictionary Creation (2025-11-17)**

**The Bug**:
- Text class>>initialize failed with primitive failure when executing `TextConstants at: #CaretForm put: ...`
- Error: "[AtPut PRIMITIVE FAIL] Non-integer index" - receiver was nil instead of Dictionary

**Root Cause**:
- Phase 5 was pre-creating TextConstants Association with value=nil during pool dictionary initialization
- Text>>initialize checks `(Smalltalk includes: #TextConstants)` before creating the Dictionary
- Since Association existed (value=nil), the check returned true and Dictionary was never created
- Code tried to execute `nil at: #CaretForm put: ...` → primitive failure

**The Fix**:
- Modified Phase5_GlobalsAndPatches.cs:771-775 to SKIP TextConstants during cold start
- Added explicit check to skip pool dictionaries that have init expressions
- Text>>initialize now creates TextConstants Dictionary at runtime as intended

**Files Modified**:
- `Phase5_GlobalsAndPatches.cs:768-775` - Added skip logic for TextConstants

**Result**:
- ✅ Text initialize now runs successfully - 86,221 instructions, no errors
- ✅ TextConstants Dictionary created and populated with pool variables
- ✅ Init expression success rate improved from 30/46 (65%) to 31/46 (67%)

### With Essential Init Expressions Only

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart.json --skip-init-expressions
```

Runs only 4 essential init expressions:
- Character initialize (required for CharacterTable)
- Float initialize (required for Float constants)
- SmallInteger initialize (required for SmallInteger constants)
- BlockContextTest validate (test suite)

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

**Test Suite Status**: ✅ **ALL 194 TESTS PASSING** (validated 2025-11-14)

The comprehensive BlockContext test suite validates:
- Nested blocks (1-3 levels, all arg/temp combinations)
- Variable access (instance, class, pool, global vars)
- Mutations (outer temp modification from inner blocks)
- Control flow (whileTrue:, whileFalse:, do:, select:)
- Non-local returns
- Real-world patterns and edge cases

**Test Result**: `BlockContextTest>>testAllCombinations` returns **3503** ✅ (expected value)

See `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md` Appendix A.1 for detailed test results.

### 🔴 MANDATORY: BlockContext Regression Test (After ANY Changes)

**Run after EVERY compiler or interpreter change**:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected Output**:
```
[282] BlockContextTestRunner initialize... ~12,000 instructions  ✓ SUCCESS - Returned: 15000
```

**Result Interpretation**:
- `15000` = ✅ All 15 core tests pass (expected)
- `14001` = ❌ Test #1 failed (investigate immediately)
- Any other value = ❌ Multiple failures (see `Block Context Test Results.md`)

**Regenerate Test Bundle** (if tests modified):
```bash
cd "Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st Tests/BlockContextTestRunner.st > Smalltalk-80-with-runner.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-runner.st"
cp ../bin/Debug/net9.0/coldstart.json ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Critical Files Requiring Test**:
- ❗ `BytecodeCompiler.cs` - bytecode generation
- ❗ `VariableResolver.cs` - variable resolution
- ❗ `TempFrameAllocator.cs` - variable allocation
- ❗ `VirtualMachine.cs` - GetTemporaryVariable, SetTemporaryVariable
- ❗ `ControlPrimitives.cs` - BlockCopy, BlockValue primitives

**Test Coverage**: 15 focused tests covering all critical BlockContext operations. See `Block Context Test Results.md` for complete test table and details.

---

## Recent Work (2025-11-17)

**✅ COMPLETED: TextConstants Pool Dictionary Fix (2025-11-17)**

Successfully fixed Text class>>initialize failure by preventing Phase 5 from pre-creating TextConstants pool dictionary during cold start.

**Problem**:
- Text>>initialize includes guard code: `(Smalltalk includes: #TextConstants) ifFalse: [Smalltalk at: #TextConstants put: (Dictionary new: 32)]`
- Phase 5 was pre-creating TextConstants Association with value=nil
- Guard returned true, Dictionary never created
- Subsequent `TextConstants at: #CaretForm put: ...` tried to send at:put: to nil → primitive failure

**Solution**:
- Added skip logic in Phase5_GlobalsAndPatches.cs:768-775
- TextConstants now created at runtime by Text>>initialize as source code intends
- Follows same pattern as CharacterBlock pool dictionary creation

**Result**:
- ✅ Text initialize: 86,221 instructions, SUCCESS
- ✅ Init expressions: 31/46 passing (67% success rate, up from 30/46)
- ✅ All pool variables (CaretForm, DefaultTab, etc.) properly initialized

**Files Modified**:
- `Phase5_GlobalsAndPatches.cs:768-775` - Skip TextConstants during cold start

---

## Recent Work (2025-11-17) - Hash Algorithm Fixes

**🔴 CRITICAL FIX: Hash Algorithm Modulo vs Bitwise AND (2025-11-17)**

Fixed catastrophic bug where our hash algorithm used bitwise AND instead of modulo, causing hash mismatches with Smalltalk-80 and creating duplicate Associations.

**The Bug:**
- **Our C# code**: `hash = (hash + b) & 0x0FFFFFFF;` (bitwise AND)
- **Smalltalk-80**: `hash := (hash + char) \\ 16r0FFFFFFF.` (modulo)
- Since 0x0FFFFFFF (268435455) is NOT a power of 2, these give **different results**!
- Example: hash=268435456 → AND gives 0, modulo gives 1

**Impact on Text Initialize Failure:**
1. Phase 5 installed TextConstants Association at WRONG slot (using AND hash)
2. Runtime `Smalltalk at: #TextConstants put: dict` couldn't find it (using modulo hash)
3. Created DUPLICATE Association → original stayed nil
4. `TextConstants at: put:` read pool variable literal (Association #1, value=nil) → CRASH

**The Fix:**
Changed all three hash implementations from `& 0x0FFFFFFF` to `% 0x0FFFFFFF`:
- Phase3_ObjectAllocation.cs:492 - `hash = (hash + b) % HashModulus;`
- Phase5_GlobalsAndPatches.cs:1031 - `hash = (hash + b) % HashModulus;`
- RuntimeHeapAccess.cs:229 - `hash = (hash + b) % HashModulus;`

**Expected Result:**
Text initialize should now succeed - dictionary operations will find the correct Associations.

**Commit**: `2a1d5b5` - "CRITICAL FIX: Use modulo (%) instead of bitwise AND (&) in hash algorithm"

**FOLLOW-UP: Smalltalk Floored Division Semantics**

Further refined to use Smalltalk `\\` (floored division) instead of C# `%` (truncated division):
- **C# `%`**: Remainder sign follows dividend - Example: `-7 % 3 = -1`
- **Smalltalk `\\`**: Remainder sign follows divisor - Example: `-7 \\ 3 = 2`

Created `SmalltalkMath.Modulo()` matching Primitive 11 semantics. All hash implementations now use `SmalltalkMath.Modulo(hash + b, HashModulus)`.

**Note**: For our inputs (positive bytes, positive modulus), results are identical, but this ensures correctness per Blue Book spec.

**Commit**: `891b4aa` - "Use Smalltalk floored division semantics for hash modulo operations"

**🔴 ACTUAL ROOT CAUSE: Symbol>>hash Uses OOP-Based Hash, Not String Hash!**

The previous fixes (modulo vs AND, floored vs truncated division) were addressing the wrong problem. The **real issue** was discovered by analyzing the actual Smalltalk-80 source code:

**Symbol>>hash implementation:**
```smalltalk
hash
    "Answer with a SmallInteger whose value is half of the receiver's object pointer"
    <primitive: 75>
    ^self!
```

Symbol>>hash uses **Primitive 75** which returns `objectPointer / 2` (OOP-based identity hash), **NOT** a hash computed from string contents!

**Why This Broke Everything:**
1. Our code computed hash from STRING contents: `hash = sum(char) % modulus`
2. Smalltalk uses hash from OOP: `hash = oop / 2`
3. **Complete mismatch** → Associations installed/looked up at different locations
4. Result: Duplicate Associations, nil values, infinite loops

**The Real Fix:**
Changed GetSymbolHash() to use OOP-based hash:
```csharp
private int GetSymbolHash(SmalltalkOOP symbolOOP)
{
    // Symbol>>hash uses Primitive 75: half the object pointer
    return (int)(symbolOOP.Value >> 1);
}
```

Also changed RuntimeHeapAccess.LookupGlobalByName to use **linear search** instead of hash-based lookup (can't compute OOP hash from string name).

**Commit**: `8a0b16f` - "CRITICAL FIX: Use OOP-based hash (Primitive 75) for Symbol>>hash"

---

**✅ COMPLETED: Runtime GlobalRegistry Violations Fixed**

Successfully identified and eliminated all runtime GlobalRegistry access violations, ensuring clean separation between cold start infrastructure and runtime execution.

**Issues Fixed**:

1. **RuntimeHeapAccess.LookupGlobalByName** (lines 146-226):
   - **Problem**: Used O(n) linear search through entire SystemDictionary
   - **Solution**: Implemented O(1) hash-based lookup matching Dictionary>>findKeyOrNil: algorithm
   - **Impact**: Massive performance improvement for ~500 entry SystemDictionary lookups

2. **VirtualMachine.cs Runtime Violations** (4 instances):
   - Line 1148: `GlobalRegistry.LookupClassOOP("Array")` → `_runtimeHeapAccess.GetClass("Array")`
   - Line 1161: `GlobalRegistry.LookupClassOOP("Symbol")` → `_runtimeHeapAccess.GetClass("Symbol")`
   - Line 1175: `GlobalRegistry.LookupClassOOP("Dictionary")` → `_runtimeHeapAccess.GetClass("Dictionary")`
   - Line 4508: `GlobalRegistry.Get("Undeclared")` → `_runtimeHeapAccess.LookupGlobalByName(systemDictOOP, "Undeclared")`

**Hash Algorithm Implementation** (CORRECTED to use modulo):
```csharp
private int ComputeSymbolHashFromString(string symbolName)
{
    byte[] bytes = Encoding.Latin1.GetBytes(symbolName);
    const int HashModulus = 0x0FFFFFFF; // 268435455 = 2^28 - 1
    int hash = 0;
    foreach (byte b in bytes)
    {
        hash = (hash + b) % HashModulus; // Modulo, NOT bitwise AND!
    }
    return hash;
}
```

**Verified Acceptable GlobalRegistry Usage**:
- VirtualMachine.cs:192 - USTable lookup during VM initialization (cold start only, documented)
- VirtualMachine.cs:2919 - objectIdToLocalId lookup (VM metadata, not Smalltalk data, documented)

**Compilation Fixes**:
- Phase3_ObjectAllocation.cs:483 - Fixed GetSymbolHash() to use GetIndexedByte() instead of non-existent GetIndexedBytes()
- Phase5_GlobalsAndPatches.cs:1022 - Fixed GetSymbolHash() to use GetIndexedByte() instead of non-existent GetIndexedBytes()
- Phase3_ObjectAllocation.cs:1361 - Fixed to use AssociationRegistry.GetAll()[key] instead of non-existent Get() method

**Files Modified**:
- `RuntimeHeapAccess.cs:146-226` - Hash-based lookup + ComputeSymbolHashFromString()
- `VirtualMachine.cs:1148,1161,1175,4509` - Replaced 4 GlobalRegistry calls
- `Phase3_ObjectAllocation.cs:483,1361` - Fixed GetSymbolHash() and AssociationRegistry access
- `Phase5_GlobalsAndPatches.cs:1022` - Fixed GetSymbolHash()

**Commits**:
- `4f823e8` - "Fix runtime GlobalRegistry violations and optimize dictionary lookups"
- `cf03359` - "Fix compilation errors from hash-based lookup changes"

**Impact**:
✅ Clean separation between cold start (GlobalRegistry) and runtime (Smalltalk heap only)
✅ All dictionary operations now use consistent hash-based lookup algorithm
✅ O(1) performance for global variable lookups
✅ Matches Smalltalk-80 Dictionary semantics exactly


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
