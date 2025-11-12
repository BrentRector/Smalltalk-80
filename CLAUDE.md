# Claude Development Guidelines

**Last Updated**: 2025-11-11

## Recent Work (2025-11-11)

**VM Extension: Block Temporary Count Tracking**

Implemented compiler and VM support to track and emit block temporary counts, enabling proper population of BlockContext.ntemps field (slot 4).

**Build Status**: ✅ 0 errors, 1 warning (undeclared variables)
**Cold Start**: ✅ Phase 1-6 all succeeded
**Cold Start (--skip-init-expressions)**: ✅ All 3 init expressions succeeded

**Files Modified**:
1. `Smalltalk80.Compiler\BytecodeCompiler.cs` (lines 2113-2134, 2247-2251)
   - Parse block temporaries BEFORE emitting blockCopy bytecode
   - Emit tempCount as second argument to blockCopy primitive
   - Avoid duplicate `blockTemps` variable declaration

2. `Smalltalk80.Interpreter\Core\VM\Primitives\ControlPrimitives.cs` (lines 12-98, 154-158)
   - Modified `BlockCopy` primitive to accept 2 arguments: (nargs, ntemps)
   - Added validation for tempCount parameter
   - Store tempCount in BlockContext.ntemps field instead of hard-coded 0

3. `Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs` (line 1837)
   - Updated `ExecuteSendSpecial()` to recognize blockCopy as 2-argument primitive

**Key Implementation**:
```csharp
// COMPILER (BytecodeCompiler.cs)
var blockTemps = block.blockTemps();
int tempCount = 0;
if (blockTemps != null)
{
    tempCount = blockTemps.children
        .OfType<Smalltalk80Parser.TemporaryNameContext>()
        .Count();
}

emitter.EmitByte(0x89); // PUSH_ACTIVE_CONTEXT
emitter.PushSmallInteger(argCount); // First arg
emitter.PushSmallInteger(tempCount); // Second arg (VM extension)
emitter.EmitByte(0xC8); // SEND_SPECIAL_BLOCKCOPY

// PRIMITIVE (ControlPrimitives.cs)
public static SmalltalkOOP BlockCopy(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 2) { ... }

    SmalltalkOOP blockArgumentCount = args[0];
    SmalltalkOOP blockTemporaryCount = args[1]; // VM extension

    ctx.HeapManager.SetInstanceVariable(newBlockContext,
        VMConstants.BLOCKCONTEXT_NTEMPS_INDEX,
        blockTemporaryCount); // Was hard-coded to 0
}
```

**Benefits**:
- BlockContext.ntemps field now accurately reflects block's temporary count
- Enables future optimizations and debugging tools
- Maintains compatibility with 5-field BlockContext structure

**Note**: This is a VM extension beyond Blue Book specification. The Blue Book's primitive 80 (blockCopy:) takes 1 argument (nargs), but we now pass 2 (nargs, ntemps).

---

## Previous Work (2025-11-11)

**CRITICAL FIX: VM Variable Access Architecture Mismatch**:

Fixed a fundamental architecture mismatch between compiler and VM for block temporary variable access:

### The Bug

The compiler was emitting **DIRECT indices** into target contexts' temp frames (correct), but the VM was using **HIERARCHICAL indexing** with offset adjustments (incorrect).

**Example**: Test `ma1` from BlockContextTestSimple.st
```smalltalk
ma1 "Method 1 arg, 2-level block accesses it"
    ^[:arg | [[arg] value] value] value: 42
```

**Compiler behavior** (correct):
- Outer block: `arg` is argument 0
- Inner block accessing `arg`: Emits index=0 (direct offset into outer block's arg frame)

**VM behavior** (broken):
- Inner BlockContext (nargs=0) sees index=0
- Checked if 0 < 0 (false), so traversed to home
- Subtracted nargs: `index - 0 = 0` (seemed to work by accident!)
- But for blocks with args, this would compute WRONG indices

### The Fix

**Files Modified**:
1. `d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs`
   - Line 4625: Changed `GetTemporaryVariable(homeOOP, index - nargs)` to `GetTemporaryVariable(homeOOP, index)`
   - Line 4669: Changed `SetTemporaryVariable(homeOOP, index - nargs, valueOOP)` to `SetTemporaryVariable(homeOOP, index, valueOOP)`
   - Added XML doc comments explaining direct indexing semantics

**Key Insight**: When traversing the home chain from BlockContext to parent, the VM must **NOT** adjust the index. The compiler already emitted the correct direct index into the target context's temp frame.

### Test Results

**BlockContextTestSimple.st**: All 3 tests PASS
```
ma1: 42   (2-level nested block accessing outer arg)
ma2: 42   (3-level nested block accessing two outer args)
t0001: 10 (2-level nested block accessing method temp)
Total: 94
```

**Verification**: VM diagnostic output shows correct traversal:
```
[VM.GetTemporaryVariable] index=0, isBlock=True
  Traversing to home=oop=64680 with SAME index=0 (direct indexing)
  Returning local block arg[0]: SmallInteger(42)
```

### Architecture Validation

This fix aligns with the compiler's variable resolution architecture:

**Compiler** (`Smalltalk80.Compiler/VariableResolver.cs` lines 92-121):
- Steps 5-6: Method arguments and temps use **DIRECT indices**
- Comments explicitly state: "NO hierarchical offset! VM will traverse home chain automatically"

**VM** (now fixed):
- `GetTemporaryVariable`: Traverses home chain WITHOUT index adjustment
- `SetTemporaryVariable`: Traverses home chain WITHOUT index adjustment

**Build Status**: ✅ 0 errors, 0 warnings
**Test Status**: ✅ BlockContextTestSimple: 94 (all tests pass)

---

## Previous Work (2025-01-10)

**File Initialize Debugging - Multiple Compiler & VM Bugs Fixed**:

**Current Status**: File initialize is now the FIRST failure (was 0th before fixes). Multiple critical bugs found and fixed, one compiler bug remains.

### Bugs Fixed (Commit 304f6ff)

1. **Source Files: Duplicate File Initialize Method**
   - **Files**: `Smalltalk-80 Source/Smalltalk-80.{original,st}`
   - **Problem**: Second (broken) File initialize lacked `FilePool _ Dictionary new`, causing nil FilePool → infinite recursion
   - **Fix**: Removed duplicate at line 57147, added initialization at line 16889

2. **Compiler: Symbol Literal Ordering Bug**
   - **File**: [Smalltalk80.Compiler/BytecodeCompiler.cs](Smalltalk80.Compiler/BytecodeCompiler.cs)
   - **Problem**: Symbol literals (#Read, #Write, #Shorten) added to selectors list instead of otherLiterals, shifting Association indices
   - **Fix**: Created `AddNonSelectorLiteral` method, modified `PushLiteralConstant` to route Symbol literals correctly
   - **Result**: Associations now at correct indices 0-1, Symbols at indices 2-4

3. **VM: BlockContext Home Traversal for Nested Blocks**
   - **File**: [Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs](Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs#L2168-L2314)
   - **Problem**: Assumed BlockContext.home always points to MethodContext, but nested blocks have BlockContext → BlockContext → MethodContext chain
   - **Symptom**: Reading slot 3 from BlockContext got nargs (SmallInteger) instead of method OOP
   - **Fix**: Use `FindMethodContext()` helper in 3 locations (lines 2168-2314) to properly traverse chain

4. **Compiler: Method Argument Resolution from Nested Blocks (Partial)**
   - **File**: [Smalltalk80.Compiler/VariableResolver.cs](Smalltalk80.Compiler/VariableResolver.cs#L84-L330)
   - **Problem**: Nested block [:index | aBlock value: ...] couldn't find outer method parameter aBlock
   - **Fix**: Added traversal to method level (ScopeDepth==0) in Step 5, fixed CalculateBlockLocalOffset to start with parent
   - **Status**: ✅ VariableResolver returns MethodArgument type correctly, ❌ BytecodeCompiler ignores it (still generates pushReceiver)

### Remaining Bug

**Problem**: Compiler generates `pushReceiver 0` (bytecode 0x00) instead of `pushTemporary 0` (bytecode 0x10) when nested block accesses outer method parameter.

**Location**: `Set>>do: aBlock` nested block `[:index | aBlock value: ...]`
- Bytecode[22] = 0x00 (pushReceiver 0) → pushes tally (SmallInteger)
- Should be 0x10 (pushTemporary 0) → would push aBlock (BlockContext)

**Next Step**: Fix `BytecodeCompiler.PushVariable` to honor VariableResolver result type and generate correct instruction.

**See**: [RESUME-PROMPT.md](RESUME-PROMPT.md) for detailed debugging notes and next steps.

**Build Status**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ Phase 1-6 succeeded
**Init Expressions**: ⚠️ File initialize now FIRST failure (progress from 0 expressions succeeding)

---

## Previous Work (2025-11-10)

**Init Expression Dependency Analysis & Execution**:
- Analyzed all 68 class-side initialize methods in Smalltalk-80 system
- Created [InitExpression-DependencyAnalysis.md](InitExpression-DependencyAnalysis.md) with complete dependency graph (3 levels, no circular deps)
- Implemented dependency-ordered execution in [Program.cs](Smalltalk80.Interpreter/Program.cs#L257-L273)
- **Fixed critical VM bug**: Super sends from within blocks now correctly walk BlockContext → home MethodContext

**Super Send Fix** ([VirtualMachine.cs:2142-2174](Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs#L2142-L2174)):
- **Problem**: Super send failed with "Cannot safely read slot 3" when executing inside a block
- **Root Cause**: BlockContext slot 3 = nargs, MethodContext slot 3 = method (different fields!)
- **Solution**: Check if _activeContextOOP is BlockContext; if so, walk to home MethodContext before reading method slot
- **Example**: `Cursor class>>extent:fromArray:offset:` has `ifTrue: [^super ...]` - super send inside block closure

**Instance Variable Initialization Fix** ([SmalltalkHeapManager.cs:81-91](Smalltalk80.Interpreter/Core/HeapManager/SmalltalkHeapManager.cs#L81-L91)):
- **Problem**: C# arrays default to all-zero, leaving instance variables as OOP(0) instead of nil
- **Solution**: After allocating OOP arrays, initialize all slots (except class pointer) to nil
- **Blue Book Requirement**: Uninitialized instance variables MUST be nil, not arbitrary values
- **Impact**: Prevents garbage OOP values in newly allocated objects

**Build Status**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ Phase 1-6 all succeeded
**Init Expressions**: ⚠️ 52 expressions execute in dependency order
- ✅ Super send from blocks now works (BinaryChoiceView progresses past super send)
- ⚠️ Remaining issue: Point>>at: subscript bounds error (investigation needed)

**Files Modified (2025-11-10)**:
- `Smalltalk80.Interpreter/Program.cs` - Added dependency-ordered init execution (lines 257-273, 1686-1810)
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` - Fixed super send from blocks (lines 2142-2174)
- `Smalltalk80.Interpreter/Core/HeapManager/SmalltalkHeapManager.cs` - Initialize instance vars to nil (lines 81-91)
- `InitExpression-DependencyAnalysis.md` - NEW: Complete 68-class dependency analysis
- `init_order.json` - NEW: Machine-readable dependency order

**Known Issues**:
- Point>>at: error in BinaryChoiceView initialize - needs further investigation (receiver oop=64012 is a Point being sent `at:` message)

---

## Previous Work (2025-11-09)

**FIXED: BlockContext Execution Now Working** ✅:

1. **Issue #1 - Fixed: isBlockContext Check Used Class Name Comparison**
   - **Problem**: GetClassName() returned "UnknownClass" for BlockContext due to class name field index not being set
   - **Solution**: Changed to OOP comparison: `contextClassOOP == _metadata.BlockContextClassOOP`
   - **File**: [VirtualMachine.cs](Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs) lines 818, 2765

2. **Issue #2 - Fixed: Primitive 80 (blockCopy:) Sets Wrong startpc**
   - **Problem**: startpc was set to current PC, which pointed to LONG_JUMP, not block body
   - **Root Cause**: When primitive executes, PC has already been incremented past blockCopy: bytecode
   - **Bytecode Layout**: `blockCopy: (0xC8) | LONG_JUMP (3 bytes) | <block body>`
   - **Solution**: Changed from `currentPC + 4` to `currentPC + 3` (PC already points past 0xC8)
   - **File**: [ControlPrimitives.cs](Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs) line 116

3. **Test Results**:
   - ✅ MinimalBlockTest: `[42] value` → **PASSES**
   - ✅ SingleTest: `[| ot | ot _ 4. [:ia | | it | it _ 3. ot + it] value: 999] value` → **PASSES**
   - ⚠️ BlockContextTest testAllCombinations: Executes blocks successfully but fails on arithmetic coercion (unrelated to BlockContext)

4. **Architecture Validated**:
   - ✅ BlockContext creation via Primitive 80 works correctly
   - ✅ BlockContext activation via Primitive 81 works correctly
   - ✅ Nested blocks with lexical variable access work correctly
   - ✅ Home chain traversal for variable access works correctly
   - ✅ Blue Book BlockContext architecture is fully operational

**Build Status**: ✅ 0 errors, 0 warnings
**Basic Block Tests**: ✅ All passing
**Nested Block Tests**: ✅ All passing

---

## Recent Work (2025-11-09)

**CRITICAL CORRECTION: Reverted BlockContext Mistake - Using Correct BlockContext (2025-11-09)**:

1. **Error Discovered**
   - **Mistake**: I incorrectly believed Smalltalk-80.st used BlockContext class
   - **Reality**: BlockContext was mistakenly added in a prior revision and has been removed
   - **Truth**: Original Smalltalk-80 uses **BlockContext** with primitives 81 and 82
   - **Blue Book Reference**: BlockContext is documented in Blue Book Chapter 29

2. **Corrections Made**
   - **VMConstants.cs**: Removed [Obsolete] attributes from BLOCKCONTEXT_* constants
   - **ControlPrimitives.cs**: Reverted Primitive 80 to create BlockContext (not BlockContext)
   - **VMMetadata.cs**: Changed BlockContextClassOOP → BlockContextClassOOP
   - **Loader.cs**: Changed to load BlockContext class instead of BlockContext
   - **VirtualMachine.cs**: GetBlockContextClassOOP() returns BlockContextClassOOP from VMMetadata

3. **Correct BlockContext Structure (Blue Book)**
   - **6 Fixed Fields**: sender, pc, stackp, nargs, startpc, home
   - **Indexed Slots**: Variable size for temps and stack
   - **Home Field**: Points to lexical parent (enables nested block variable access)
   - **Primitives**: 80 (blockCopy:), 81 (value*), 82 (valueWithArguments:)

4. **Primitive 80 (blockCopy:) - Correct Implementation**
   - **File**: `ControlPrimitives.cs` lines 82-139
   - **Creates**: BlockContext with 6 fixed fields + 32 indexed slots
   - **Home Chain**: Nested blocks have home pointing to active BlockContext
   - **Fields Initialized**:
     - sender/caller ← nil (set by primitiveValue)
     - pc ← nil (set by primitiveValue)
     - stackp ← nil (set by primitiveValue)
     - nargs ← block argument count
     - startpc ← current instruction pointer
     - home ← active context (creates lexical parent chain)

**Build Status**: ✅ 0 errors, 0 warnings
**Architecture**: ✅ Correct Blue Book BlockContext implementation
**Lesson**: Always verify against actual source code, not assumptions

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/VMConstants.cs`: Removed [Obsolete] from BlockContext constants
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`: Primitive 80 creates BlockContext
- `Smalltalk80.Interpreter/Core/VM/VMMetadata.cs`: BlockContextClassOOP (not BlockContext)
- `Smalltalk80.Interpreter/Engine/Loader.cs`: Loads BlockContext class
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`: Returns BlockContextClassOOP
- `Smalltalk80.Interpreter/Program.cs`: Uses BlockContextClassOOP

---

**Fixed Nested Block Argument Access - Blue Book Lexical Parent Chain**:

1. **Critical Discovery: Blue Book Expert Agent Was Wrong!**
   - **Agent Claim**: "Nested blocks CANNOT access outer block arguments in Blue Book Smalltalk-80"
   - **Reality**: Smalltalk-80.st source code contains extensive tests demonstrating nested block argument access
   - **Evidence**: BlockContextTest_full.st has 81+ tests with patterns like `[:oa | [oa] value] value: 10`
   - **Lesson**: Always verify agent analysis against actual source code!

2. **Blue Book Architecture for Nested Blocks**
   - **Home Chain**: Inner Block → Outer Block → MethodContext (NOT Inner Block → MethodContext!)
   - **Reference**: docs/BlueBook-Context-Architecture.md lines 452, 486-487
   - **Quote**: "block1 executes blockCopy: → creates nested BlockContext with home = block1"
   - **Variable Access**: GetTemporaryVariable traverses home chain recursively

3. **Implementation Fix in Primitive 80 (blockCopy:)**
   - **File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` line 159
   - **Change**: Store ACTIVE CONTEXT as home (NOT methodContext)
   - **Before**: `home ← methodContext` (would skip intermediate blocks)
   - **After**: `home ← context` (creates proper lexical chain)
   - **Result**: Nested blocks can now access outer block arguments AND temps

4. **Test Results**
   - **t1000 Test**: `[:oa | [oa] value] value: 10` ✅ PASSES (returns 10)
   - **Suite**: 94+ BlockContextTest methods executed successfully
   - **Patterns Verified**:
     - `[:oa | [oa] value] value: 10` - inner accesses outer arg
     - `[:oa | [:ia | oa] value: 999] value: 10` - inner has arg but accesses outer
     - `[:oa | | ot | ot _ 5. [oa + ot] value] value: 5` - inner accesses outer arg AND temp

5. **Architecture Validation**
   - **Confirmed**: Blue Book DOES support nested block argument access
   - **Mechanism**: Lexical parent chain via home field
   - **Home Points To**: Active context at blockCopy: time (BlockContext OR MethodContext)
   - **Documentation**: BlueBook-Context-Architecture.md is CORRECT

**Build Status**: ✅ 0 errors, 0 warnings
**Test Results**: ✅ 134+ BlockContextTest methods pass (up from 94 after bytecode fix)
**Architecture**: ✅ Blue Book compliant lexical parent chain via home field

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`: Fixed blockCopy: to set home=context (line 159)
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`: Updated FindMethodContext() to traverse home chain
- `Shared/Core/Bytecodes/BytecodeDefinitions.cs`: Fixed IsPushTemp() range (line 268)

**Key Takeaway**: When agent analysis contradicts source code evidence, trust the source code!

---

**Fixed PUSH_TEMP Bytecode Range Bug (2025-11-09 continued)**:

1. **Bug Discovery**
   - **Symptom**: Tests failed with "Bytecode 0x14 not yet implemented"
   - **Root Cause**: Helper method `IsPushTemp()` incorrectly limited range to 0x10-0x13
   - **Impact**: Bytecodes 0x14-0x1F (PUSH_TEMP_4 through PUSH_TEMP_15) were unreachable

2. **Investigation**
   - Blue Book Chapter 28 clearly states: "16-31 | 0001iiii | Push Temporary Location #iiii"
   - Our BytecodeDefinitions.cs had CORRECT definitions for PUSH_TEMP_0..15 (0x10-0x1F)
   - VM dispatcher had CORRECT handler checking `IsPushTemp()` at line 985
   - But `IsPushTemp()` had WRONG range check based on incorrect comment

3. **The Fix**
   - **File**: `Shared/Core/Bytecodes/BytecodeDefinitions.cs` line 268
   - **Before**: `op >= 0x10 && op <= 0x13` (only 4 bytecodes)
   - **After**: `op >= 0x10 && op <= 0x1F` (all 16 bytecodes)
   - **Comment Removed**: False claim that 0x14-0x19 are "control flow opcodes"

4. **Test Results**
   - **Before Fix**: 94 BlockContextTest methods pass, fail on bytecode 0x14
   - **After Fix**: 134 BlockContextTest methods pass (+40 methods!)
   - **Verification**: Bytecodes 0x10-0x1F now work correctly

5. **Architecture Clarity**
   - PUSH_TEMP range is 0x10-0x1F (Blue Book standard)
   - Extended bytecodes (0x80 0x10, 0x80 0x14, etc.) are DIFFERENT - handled separately
   - VM dispatcher checks `IsPushTemp()` BEFORE switch statement, so 0x10-0x1F never reach extended handlers

**Build Status**: ✅ 0 errors, 0 warnings
**Test Results**: ✅ 134 BlockContextTest methods pass
**Blue Book Compliance**: ✅ PUSH_TEMP range now matches Blue Book Chapter 28 specification

---

## Previous Work (2025-11-08)

**Critical Primitive and Compiler Fixes**:

1. **Fixed Primitive 82 (BlockContext>>valueWithArguments:) Class Name Validation**
   - **Bug**: Primitive 82 was checking `className != "class:Array"` but `GetClassName()` returns just "Array"
   - **Impact**: Primitive ALWAYS failed, causing infinite recursion in fallback Smalltalk code
   - **Fix**: Changed validation to `className != "Array"` in ControlPrimitives.cs:338
   - **Result**: BlockContext execution via valueWithArguments: now succeeds

2. **Fixed BytecodeCompiler.PushSmallInteger() to Use Special Bytecodes**
   - **Bug**: `PushSmallInteger()` always called `PushLiteralConstant()`, creating method literals for 0, 1, 2, -1
   - **Impact**: blockCopy: received Symbol literals instead of SmallInteger argument counts
   - **Symptom**: BlockContext created with OOP instead of SmallInteger in nargs field
   - **Fix**: Added switch statement to emit special bytecodes:
     - 0 → 0x75 (PUSH_ZERO)
     - 1 → 0x76 (PUSH_ONE)
     - 2 → 0x77 (PUSH_TWO)
     - -1 → 0x74 (PUSH_MINUS_ONE)
   - **Result**: blockCopy: now receives correct SmallInteger argument counts

**Build Status**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ Phases 1-6 all pass with regenerated bundle
**Block Primitives**: ✅ Primitive 82 now succeeds, blockCopy: receives SmallIntegers

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`: Fixed class name comparison (line 338)
- `Smalltalk80.Compiler/BytecodeCompiler.cs`: Fixed PushSmallInteger to use special bytecodes (lines 185-208)
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`: Added BlockContext field validation with diagnostics

**Bundle Regeneration**: Cold start bundle regenerated with correct bytecode. All methods now use special bytecodes for 0, 1, 2, -1 instead of literals.

---

## Previous Work (2025-11-08)

**Block Implementation Fixes - Blue Book Conformance**:

Fixed critical bugs in BlockContext implementation to achieve full Blue Book conformance:

1. **Fixed BlockContext Format Field Corruption**
   - **Bug**: Phase2_ClassInfrastructure.cs had incorrect "WORKAROUND" that changed instanceSpec from 2→1
   - **Impact**: BlockContext allocated as Pointer (no indexed slots) instead of PointerIndexed
   - **Fix**: Removed incorrect workaround; BlockContext now correctly uses instanceSpec=2 (PointerIndexed)

2. **Fixed Block Return Bytecodes**
   - **Bug**: RETURN_SELF_POP (0x7D) and RETURN_TOP_POP (0x7E) were reading wrong field
   - **Impact**: Non-local returns from blocks were broken (reading startpc instead of home)
   - **Fix**: Changed to use BLOCKCONTEXT_HOME_INDEX (slot 5) instead of CONTEXT_LEXICALPARENT_INDEX (slot 4)

3. **Added PUSH_ACTIVE_CONTEXT Bytecode**
   - Added bytecode definition for 0x89 (PUSH_ACTIVE_CONTEXT)
   - Updated handler to use enum constant instead of magic number

**Build Status**: ✅ 0 errors, 0 warnings
**Cold Start**: ✅ Phases 1-6 all pass
**Architecture**: ✅ Blue Book two-context model (MethodContext + BlockContext)

**Files Modified**:
- `Shared/Core/Bytecodes/BytecodeDefinitions.cs`: Added PUSH_ACTIVE_CONTEXT (0x89)
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`: Fixed RETURN_SELF_POP and RETURN_TOP_POP to use correct field
- `Smalltalk80.Interpreter/Engine/LoaderPhases/Phase2_ClassInfrastructure.cs`: Removed incorrect format workaround
- `Smalltalk80.Interpreter/Core/Memory/SpecialObjectsArray.cs`: Fixed ActiveProcess accessor
- `Smalltalk80.Interpreter/Core/HeapManager/SmalltalkHeapManager.cs`: Cleaned up debug code

---

## Previous Work (2025-11-06)

---

## Previous Work (2025-11-05)


## Running Tests (2025-11-05)

### Test Execution with --skip-init-expressions Flag

When running comprehensive test suites like BlockContextTest, use the `--skip-init-expressions` flag to avoid triggering known bugs in init expressions that would prevent the tests from running.

**Command Pattern**:
```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json \
              --skip-init-expressions \
              --execute "testAllCombinations"
```

**Flags**:
- `--coldstart <file>`: Specify the cold start bundle containing test classes
- `--skip-init-expressions`: Skip all init expression execution (avoids infinite recursion bugs)
- `--execute <expression>`: Execute a specific Smalltalk expression after cold start

**Important**: Use `dotnet run` instead of running the .exe directly on Windows, as the standalone exe may have issues with output buffering.

**Expected Results**:
- Tests execute without hitting init expression bugs
- Test suite detects compiler bugs (e.g., infinite recursion in whileTrue:)
- Error output shows specific failure (e.g., "Context depth exceeded")

**Files Modified**:
- `Smalltalk80.Interpreter/Program.cs`: Added --skip-init-expressions and --execute flag support

**Usage**:
```csharp
// Enable optimization during compilation
var options = new CompilerOptions { OptimizeTailRecursion = true };
// Or use preset
var options = CompilerOptions.Optimized;
```


### Cold Start Phase (Phases 1-6)

**Components**: `Loader`, `SemanticAllocator`, Phase classes
**Data Access**: Can use `ColdStartBundle`, registries (write), `_symbols[]`
**Marked with**: `[ColdStartOnly]` attribute

**Example**:
```csharp
[ColdStartOnly(Reason = "Uses ColdStartBundle which is stale after cold start")]
public class SemanticAllocator
{
    private readonly ColdStartBundle _bundle;  // OK during cold start
}
```

### Runtime Phase (Init Expressions Onward)

**Components**: `VirtualMachine`, primitives
**Data Access**: ONLY `ISmalltalkHeapManager` and `LayoutResolver`
**Marked with**: `[RuntimeOnly]` attribute

**Example**:
```csharp
[RuntimeOnly(Reason = "Executes Smalltalk bytecode at runtime, must use heap-only access")]
public partial class VirtualMachine : IPrimitiveContext
{
    // NO _symbols field!
    // NO _bundle field!
    // ONLY heap access via _heapManager
}
```

### Enforcement Mechanisms

1. **Compile-Time**: Type system prevents access to removed fields
   - `_symbols` field removed → compile error if accessed
   - Constructor parameter removed → compile error if provided

2. **Runtime**: Registries throw exceptions if modified after freeze
   ```csharp
   // After Loader.Run() completes:
   SymbolRegistry.Freeze();   // After Phase 1
   GlobalRegistry.Freeze();   // After Phase 15

   // Later attempts to modify:
   GlobalRegistry.Register("test", oop);  // THROWS InvalidOperationException
   ```

3. **Documentation**: Custom attributes mark architectural boundaries
   - `[ColdStartOnly]` - Used during heap construction
   - `[RuntimeOnly]` - Used during bytecode execution

### Registry Freezing

After `Loader.Run()` completes:
- **SymbolRegistry.Freeze()** called after Phase 1 (3,834 symbols)
- **GlobalRegistry.Freeze()** called after Phase 5 (246 globals)

Any attempts to modify frozen registries throw `InvalidOperationException` with descriptive message:
```
GlobalRegistry.Register() called after freeze.
Runtime code must access heap directly. Attempted to register: 'ClassName'
```

**Read-only access remains allowed**: `TryGet()`, `Get()` operations work at runtime (safe for OOP lookups during VM initialization like selector caches).

### Why This Matters

**Before (Dangerous)**:
```csharp
// Could accidentally use stale data
private readonly string[] _symbols;
var symbolName = _symbols[index];  // STALE if new symbols created!
```

**After (Safe)**:
```csharp
// Must use heap - always current
SmalltalkOOP InternSymbol(string symbolName)
{
    var ustableOOP = GlobalRegistry.Get("USTable");
    // Search through heap Array, compare bytes...
}
```

**Key Benefits**:
- ✅ **Impossible to access stale data** - removed from VM entirely
- ✅ **Runtime safety** - frozen registries throw on modifications
- ✅ **Self-documenting** - attributes mark cold-start vs runtime code
- ✅ **Future-proof** - pattern established for all new code

### Methodology and Problem-Solving

- Never perform "workaround". Always find and fix the root cause of any error.

### Model Selection Guidelines

- Always use model claude-sonnet-4-5
```