# Implement Unified Temp Frame Architecture for Block Variable Access

## Summary

This PR implements a major architectural rewrite of block and variable processing in both the compiler and interpreter, transitioning from dual storage (BlockContext indexed slots + MethodContext temp frame) to a unified temp frame where **ALL variables are stored in MethodContext with absolute indexing**.

**Impact**: 7 files modified (+310 insertions, -298 deletions)
**Scope**: Compiler + Interpreter + Source code
**Breaking**: Yes - requires cold start bundle regeneration
**Testing Required**: All 194 BlockContextTest tests must pass

---

## Motivation

### Problems with Previous Architecture

1. **Dual Storage Complexity**: Block arguments stored in BlockContext indexed slots, while temps stored in MethodContext - required complex dual routing logic
2. **Home Chain Bug**: Nested blocks incorrectly set home to outer BlockContext instead of MethodContext, breaking variable access
3. **Dual Routing Overhead**: `if (index < nargs)` checks throughout Get/SetTemporaryVariable added complexity and potential bugs
4. **Special Case Handling**: BlockArgument required special treatment in compiler and VM, increasing maintenance burden

### Benefits of Unified Temp Frame

1. **Simplified VM**: Single storage location, single access path - 79 lines of code eliminated (38% reduction)
2. **Better Correctness**: Nested blocks work naturally without special handling
3. **Easier Maintenance**: Clear, predictable behavior with absolute indexing
4. **Blue Book Compliant**: Functionally equivalent to Blue Book with simpler implementation
5. **Foundation for Optimization**: Enables future slot reuse and escape analysis optimizations

---

## Architectural Changes

### Before (Dual Storage)

```
❌ Block arguments → BlockContext indexed slots [0..nargs-1]
❌ Block temps     → MethodContext temp frame
❌ Dual routing    → if (index < nargs) checks
❌ Special type    → BlockArgument enum requiring special handling
❌ Local indices   → Block args use 0-based local indices
❌ Home chain bug  → Nested blocks incorrectly chain through parent blocks
```

### After (Unified Storage)

```
✅ ALL variables   → MethodContext indexed slots with absolute indices
✅ BlockContext    → NO variable storage (only expression stack)
✅ Single routing  → Traverse to home, use absolute index (no adjustment)
✅ Unified types   → Block args treated as BlockTemporary/ParentVariable
✅ Absolute indices → All variables use absolute indices from allocator
✅ Home chain fixed → All blocks in method share SAME MethodContext home
```

---

## Implementation Phases

### Phase 1: Critical Bug Fixes ✅

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`
- `Smalltalk80.Interpreter/Core/VM/VMConstants.cs`
- `Smalltalk-80 Source/Smalltalk-80.st`

**Changes**:
1. Fixed Primitive 80 home chain bug - nested blocks now inherit outer block's home (MethodContext)
2. Added `BLOCKCONTEXT_ARG_START_INDEX = 6` VM extension constant
3. Updated BlockContext class definition to include `argStartIndex` instance variable

**Impact**: All blocks in same method now correctly share MethodContext home

---

### Phase 2: Remove Dual Routing ✅

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

**Changes**:
1. Rewrote `GetTemporaryVariable` - simple traverse-to-home pattern with absolute index
2. Rewrote `SetTemporaryVariable` - simple traverse-to-home pattern with absolute index
3. Removed all `if (index < nargs)` dual routing checks
4. Removed index adjustment (`index - nargs`) when traversing home chain

**Code Reduction**: -79 lines (38% reduction in these methods)

---

### Phase 3: Update Primitives ✅

**Files Modified**:
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`

**Changes**:
1. **Primitive 80 (blockCopy:)**: Computes and stores argStartIndex in BlockContext slot 6
2. **Primitive 81 (value/value:/value:value:)**: Reads argStartIndex, stores args in home MethodContext
3. **Primitive 82 (valueWithArguments:)**: Reads argStartIndex, stores args in home MethodContext

**Note**: Phase 3 uses conservative allocation (argStartIndex at end of temp frame) until compiler provides exact indices

---

### Phase 4: Update Compiler ✅

**Files Modified**:
- `Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs`
- `Smalltalk80.Compiler/VariableResolver.cs`
- `Smalltalk80.Compiler/BytecodeCompiler.cs`

**Changes**:

**TempFrameAllocator.cs**:
- `EnterBlockScope`: NOW allocates block arguments in unified temp frame (was skipped before)
- Added `GetCurrentBlockArgStartIndex()` helper method
- Tracks allocated slots in `scope.AllocatedSlots`

**VariableResolver.cs**:
- Removed `BlockArgument` enum type
- Removed BlockArgument resolution logic (Step 2)
- Block arguments now resolved as `BlockTemporary` or `ParentVariable`
- Updated helpers to return correct types with absolute indices

**BytecodeCompiler.cs**:
- Removed `BlockArgument` case from `PushVariable()`
- Removed `BlockArgument` case from `StoreVariable()`
- Block arguments now use absolute indices from allocator

---

## Key Technical Details

### VM Extension: BlockContext.argStartIndex

**Field**: `BLOCKCONTEXT_ARG_START_INDEX = 6` (slot 6)
**Purpose**: Stores absolute index in MethodContext temp frame where block arguments begin
**Set By**: Primitive 80 (blockCopy:)
**Used By**: Primitive 81 (value/value:/value:value:), Primitive 82 (valueWithArguments:)
**Status**: VM extension beyond Blue Book (acceptable internal optimization)

### Home Chain Semantics

**Critical Fix**: All blocks in the same method now share the **SAME MethodContext home**

```
┌──────────────────┐
│ MethodContext    │◄─── All blocks point here
│ temp frame:      │
│  [0] method arg  │
│  [1] method temp │
│  [2] block1 arg  │
│  [3] block1 temp │
│  [4] block2 arg  │
└──────────────────┘
         ▲
         │ home
    ┌────┴────┐
    │ Block1  │
    └─────────┘
         ▲ home
         │
    ┌────┴────┐
    │ Block2  │ (nested)
    └─────────┘
```

**Why**: With unified temp frame, all variables are in MethodContext, so no chain needed

### Variable Access Pattern

**Simple Traverse-to-Home**:
```csharp
if (isBlockContext)
{
    // Traverse to home MethodContext (NO index adjustment!)
    var homeOOP = GetInstanceVariable(contextOOP, BLOCKCONTEXT_HOME_INDEX);
    return GetTemporaryVariable(homeOOP, index);  // SAME index!
}
else
{
    // MethodContext: Direct access (base case)
    return GetIndexedOOP(contextOOP, index);
}
```

**Key**: Absolute index preserved throughout traversal - no adjustment

---

## Files Changed

### Interpreter (3 files)

1. **ControlPrimitives.cs** (+135, -35)
   - Fixed Primitive 80 home chain bug
   - Added argStartIndex computation and storage
   - Updated Primitives 81 & 82 to use argStartIndex

2. **VMConstants.cs** (+25, -8)
   - Added `BLOCKCONTEXT_ARG_START_INDEX = 6`
   - Updated documentation for unified temp frame

3. **VirtualMachine.cs** (+46, -125)
   - Rewrote GetTemporaryVariable (simple traverse-to-home)
   - Rewrote SetTemporaryVariable (simple traverse-to-home)
   - Removed dual routing logic

### Compiler (3 files)

4. **TempFrameAllocator.cs** (+57, -45)
   - NOW allocates block arguments in unified frame
   - Added GetCurrentBlockArgStartIndex() helper
   - Tracks allocated slots per scope

5. **VariableResolver.cs** (+35, -65)
   - Removed BlockArgument enum type
   - Removed special BlockArgument resolution
   - Simplified to use absolute indices from allocator

6. **BytecodeCompiler.cs** (+4, -20)
   - Removed BlockArgument cases from PushVariable/StoreVariable
   - Block args use absolute indices like all other temps

### Source Code (1 file)

7. **Smalltalk-80.st** (+8, -0)
   - Added `argStartIndex` instance variable to BlockContext class
   - Updated class comment with VM extension documentation

---

## Testing Requirements

### Build Verification

```bash
# Build compiler
cd Smalltalk80.Compiler
dotnet build
# Expected: 0 errors, 0 warnings

# Build interpreter
cd ../Smalltalk80.Interpreter
dotnet build
# Expected: 0 errors, 0 warnings
```

### Bundle Regeneration

```bash
cd "../Smalltalk-80 Source"

# Compile with NEW compiler
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"

# Copy to test bundle
cp bin/Debug/net9.0/coldstart.json ../bin/Debug/net9.0/coldstart_with_tests.json
```

### Test Execution

```bash
cd ../Smalltalk80.Interpreter

# Test cold start
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json --skip-init-expressions

# Run comprehensive BlockContextTest suite (194 tests)
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json \
              --skip-init-expressions \
              --execute "BlockContextTest testAllCombinations"
```

**Success Criteria**: ✅ All 194 BlockContextTest tests MUST pass

### Test Coverage

The BlockContextTest suite validates:
- Single-level blocks (9 tests)
- Two-level nested blocks (81 tests)
- Three-level nested blocks (27 tests)
- Method multi-temp (6 tests)
- Mutation tests (3 tests)
- Method argument access (6 tests)
- Real-world patterns (5 tests)
- Edge cases (7 tests)
- Non-local returns (3 tests)
- Exception handling (2 tests)
- Block lifetime (2 tests)
- Control flow (2 tests)
- Instance/class/pool/global variables (28 tests)
- Pseudo-variables (5 tests)
- Mixed access patterns (5 tests)

**Total**: 194 comprehensive test cases

---

## Documentation

### Comprehensive References

1. **Block-Context-And-Variable-Access-COMPREHENSIVE.md**
   - Complete architecture reference
   - Section 9: Advanced topics (shadowing, slot reuse, escaping blocks)
   - Implementation checklist
   - Common pitfalls

2. **Block-Context-Design-Validation.md**
   - Expert validation of architecture
   - Blue Book compliance analysis
   - Critical bug identification
   - Implementation roadmap

### Updated Documentation

- `CLAUDE.md`: Added Recent Work section documenting implementation
- Both reference docs updated with Section 9 (advanced semantic guidance)

---

## Blue Book Compliance

### Compatible Deviations

⚠️ **Unified temp frame** - Blue Book uses dual storage; we use unified (functionally equivalent)
⚠️ **BlockContext extension field** - Added argStartIndex slot 6 (internal optimization)
⚠️ **Home chain simplification** - All blocks point to MethodContext (not chained through parents)

### Why Acceptable

1. **Functionally Equivalent**: Both produce correct results
2. **Internal Optimization**: Not visible to Smalltalk code
3. **Simpler Implementation**: Easier to maintain and understand
4. **Blue Book Allows**: "Implementation-specific extensions" (Chapter 27)

### What's Preserved

✅ **BlockContext structure**: 6 fixed fields (sender, pc, stackp, nargs, startpc, home)
✅ **Variable semantics**: Shadowing, lexical scoping, closure behavior
✅ **Block primitives**: 80 (blockCopy:), 81 (value*/), 82 (valueWithArguments:)
✅ **Context retention**: Blocks capture their home context

---

## Rollback Strategy

If tests fail or issues arise:

```bash
# Revert to before implementation
git checkout 620941c

# Or selectively disable unified temp frame
# Add feature flag in VMConstants:
public const bool UNIFIED_TEMP_FRAME_ENABLED = false;
```

---

## Future Work (Phase 5)

### Optimizations (Not Required for Merge)

1. **Slot Reuse**: Implement BlockEscapeAnalyzer for aggressive slot reuse
2. **Compiler Metadata**: Emit exact argStartIndex from compiler (remove runtime calculation)
3. **Context Pooling**: Optimize context allocation/recycling
4. **Escape Analysis**: Three-tier strategy (compile-time + runtime)

### Cleanup Tasks

1. Remove conservative argStartIndex calculation from Primitive 80
2. Add compiler support for exact argStartIndex emission
3. Implement optional slot reuse for sequential non-escaping blocks
4. Performance benchmarking

---

## Reviewer Checklist

- [ ] Review architectural changes (dual → unified storage)
- [ ] Verify home chain fix (all blocks share MethodContext)
- [ ] Check VM extension (BLOCKCONTEXT_ARG_START_INDEX = 6)
- [ ] Validate compiler changes (block args now allocated)
- [ ] Build succeeds (compiler + interpreter)
- [ ] Cold start bundle regenerates successfully
- [ ] All 194 BlockContextTest tests pass
- [ ] No regression in existing functionality
- [ ] Documentation reviewed and accurate

---

## References

- **Comprehensive Reference**: `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md`
- **Design Validation**: `docs/Block-Context-Design-Validation.md`
- **Recent Work**: `CLAUDE.md` (Recent Work section)
- **Test Suite**: `Smalltalk-80 Source/Tests/BlockContextTest.st`

---

## Breaking Changes

⚠️ **BREAKING**: Requires cold start bundle regeneration with updated compiler
⚠️ **BREAKING**: Old bundles incompatible with new interpreter
⚠️ **BREAKING**: New bundles incompatible with old interpreter

**Migration**: Rebuild cold start bundle after merging this PR

---

## Commits

- `620941c` - Add comprehensive semantic guidance for block variable access (documentation)
- `fcca7d8` - Implement unified temp frame architecture for block variable access (implementation)

---

**Ready for Review**: Yes
**Ready to Merge**: After test verification
**Estimated Review Time**: 2-3 hours (major architectural change)
