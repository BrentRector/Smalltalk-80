# Block Argument Indexing Bug - Root Cause Analysis

**Date**: 2025-11-12
**Status**: CRITICAL BUG IDENTIFIED

## Executive Summary

The 2025-11-12 "fix" to VirtualMachine.cs was **INCORRECT**. We changed the VM to use absolute indexing, but this broke the `if (index < nargs)` guard that routes block argument access to BlockContext indexed slots.

**The Real Bug**: The **compiler** is emitting absolute indices for block arguments instead of local indices.

**The Real Fix**: The compiler must emit **local indices (0, 1, 2...)** for block arguments, not absolute indices from TempFrameAllocator.

---

## The Dual Storage Architecture (Correct Understanding)

Block arguments ARE stored in TWO locations (this part is correct):

1. **BlockContext.indexedSlots[0..nargs-1]**: For expression stack and shadowing
2. **MethodContext unified temp frame**: For persistence across activations

But the **indexing semantics** are different for each:

### BlockContext Indexed Slots (LOCAL indexing)
- **Indices**: 0-based local to the block (0, 1, 2...)
- **Purpose**: Immediate access during block execution
- **VM Check**: `if (index < nargs)` routes to this storage

### MethodContext Unified Temp Frame (ABSOLUTE indexing)
- **Indices**: Absolute position in unified frame (may be 3, 4, 5...)
- **Purpose**: Persistent storage, slot reuse
- **VM Access**: Only when `index >= nargs`

---

## The Bug Reproduction

### Code
```smalltalk
[:index | bits at: index put: (anArray at: index)]
```

### Compiler Behavior (WRONG)
1. TempFrameAllocator allocates block arg `index` at **absolute slot 3**
2. Compiler emits: `PUSH_TEMP 3` (absolute index)
3. Primitive 81 stores argument at BlockContext.indexedSlots[0]

### VM Execution (WRONG)
```csharp
// VirtualMachine.cs:4170-4177
if (index < nargs)  // 3 < 1? NO!
{
    return _heapManager.GetIndexedOOP(contextOOP, index);
}
else
{
    // Falls through to MethodContext access
    var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    return GetTemporaryVariable(homeOOP, index);  // Reads MethodContext.indexedSlots[3] → nil!
}
```

**Result**: Reads nil from MethodContext unified frame instead of the argument value from BlockContext indexed slots.

---

## The 2025-11-12 "Fix" Was Wrong

### What We Changed
```csharp
// BEFORE (labeled as "WRONG - hierarchical adjustment")
return GetTemporaryVariable(homeOOP, index - nargs);

// AFTER (labeled as "CORRECT - absolute indexing")
return GetTemporaryVariable(homeOOP, index);
```

### Why This Was Wrong
1. The `if (index < nargs)` check **REQUIRES** local indices to work
2. By keeping absolute indices, we broke block argument routing
3. The comment "Block arguments are allocated in MethodContext's temp frame, NOT in BlockContext indexed slots" contradicts the documented dual storage architecture

---

## The Correct Architecture

### Blue Book Specification
Block arguments use **LOCAL indexing** in bytecode:
- First block arg: PUSH_TEMP 0
- Second block arg: PUSH_TEMP 1
- Block temps: PUSH_TEMP nargs, PUSH_TEMP nargs+1, etc.

### VM Implementation Must Support
```csharp
private SmalltalkOOP GetTemporaryVariable(SmalltalkOOP contextOOP, int index)
{
    if (contextClassOOP == _metadata.BlockContextClassOOP)
    {
        int nargs = nargsOOP.AsSmallInteger();

        if (index < nargs)
        {
            // LOCAL index - read from BlockContext indexed slots
            return _heapManager.GetIndexedOOP(contextOOP, index);
        }
        else
        {
            // Block temp or method var - convert to UNIFIED frame index
            // index is still LOCAL (nargs, nargs+1, ...) so we need to convert
            int unifiedIndex = ConvertLocalToUnifiedIndex(contextOOP, index);
            var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            return GetTemporaryVariable(homeOOP, unifiedIndex);
        }
    }
}
```

---

## The Two Valid Architectural Approaches

### Approach 1: Local Indices (Blue Book Standard)
**Compiler emits**: Local indices (0, 1, 2...)
**VM handles**:
- `index < nargs` → BlockContext indexed slots (local index)
- `index >= nargs` → Convert to unified index, traverse to MethodContext

**Pros**:
- Simple bytecode
- Matches Blue Book
- `if (index < nargs)` check works naturally

**Cons**:
- VM needs mapping logic to convert local to unified indices

### Approach 2: Absolute Indices (Unified Frame)
**Compiler emits**: Absolute indices (3, 4, 5...)
**VM handles**:
- Check if index is in block's argument range (needs offset tracking)
- If yes → Calculate local index, read BlockContext indexed slots
- If no → Read MethodContext unified frame

**Pros**:
- Simpler compiler (TempFrameAllocator directly provides index)

**Cons**:
- Complex VM logic (needs to track block's argument offset range)
- `if (index < nargs)` check doesn't work without offset calculation
- More error-prone

---

## The Correct Fix

We should implement **Approach 1 (Local Indices)** because:
1. It matches the Blue Book specification
2. The `if (index < nargs)` guard works naturally
3. Less complex VM logic
4. The compiler already has all the information needed

### Compiler Changes Needed

**File**: `Smalltalk80.Compiler/VariableResolver.cs`

**Current (WRONG)**:
```csharp
if (context.OwnArguments.ContainsKey(varName))
{
    return new ResolutionResult(
        context.ScopeDepth > 0 ? VariableType.BlockArgument : VariableType.MethodArgument,
        slotIndex,  // WRONG: Uses absolute index from TempFrameAllocator
        context.ScopeDepth > 0,
        varName
    );
}
```

**Fixed (CORRECT)**:
```csharp
if (context.OwnArguments.ContainsKey(varName))
{
    if (context.ScopeDepth > 0)
    {
        // Block argument: Use LOCAL index (0-based within block)
        int localIndex = context.OwnArguments[varName];
        return new ResolutionResult(
            VariableType.BlockArgument,
            localIndex,  // CORRECT: Local index for block arguments
            true,
            varName
        );
    }
    else
    {
        // Method argument: Use absolute index from TempFrameAllocator
        return new ResolutionResult(
            VariableType.MethodArgument,
            slotIndex,  // Absolute index for method arguments
            false,
            varName
        );
    }
}
```

### VM Changes Needed

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

**Current (from 2025-11-12 "fix")**:
```csharp
else
{
    // Unified temp frame: ALL other temps (block temps, method args/temps) are in MethodContext
    // Traverse to home MethodContext and use absolute index (NO adjustment!)
    var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    return GetTemporaryVariable(homeOOP, index);  // Keep same index (absolute indexing)
}
```

**Fixed (CORRECT)**:
```csharp
else
{
    // Block temp or method var - index is LOCAL to block
    // Need to convert to UNIFIED frame index before traversing to MethodContext

    // Get block's start offset in unified frame from BlockContext
    var startOffsetOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_TEMP_OFFSET_INDEX);
    int startOffset = startOffsetOOP.AsSmallInteger();

    // Convert local index to unified index
    int unifiedIndex = startOffset + index;

    // Traverse to home MethodContext with unified index
    var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    return GetTemporaryVariable(homeOOP, unifiedIndex);
}
```

**WAIT - This requires BlockContext to store its temp offset!**

Alternative VM approach (no BlockContext changes):
```csharp
else
{
    // Block temp - index is LOCAL (nargs, nargs+1, ...)
    // Calculate unified frame offset by walking parent chain
    int unifiedIndex = CalculateUnifiedIndex(contextOOP, index);

    var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    return GetTemporaryVariable(homeOOP, unifiedIndex);
}
```

---

## Recommended Implementation Path

### Phase 1: Fix Compiler (Minimal Change)
Change VariableResolver to emit local indices for block arguments while continuing to use absolute indices for block temps and method vars.

### Phase 2: Test
Run the comprehensive BlockContext test suite to verify the fix.

### Phase 3: Update VM (If Needed)
If block temps also need adjustment (they probably do), implement unified-to-local index conversion in the VM.

### Phase 4: Update Documentation
Correct Block-Temp-Architecture-FINAL.md to clearly specify:
- Block arguments use LOCAL indices in bytecode
- Block temps use ??? (TBD based on testing)
- Method args/temps use absolute indices

---

## Open Questions

1. **Do block temps also need local indexing?**
   - If yes: More compiler changes needed
   - If no: Current approach works for temps

2. **Should we store temp offset in BlockContext?**
   - Pro: Faster VM lookup
   - Con: Increases object size

3. **What about nested blocks?**
   - Need to verify nested block argument access works correctly
   - May need recursive offset calculation

---

**Next Steps**: Implement Phase 1 compiler fix and run comprehensive tests to validate the approach.
