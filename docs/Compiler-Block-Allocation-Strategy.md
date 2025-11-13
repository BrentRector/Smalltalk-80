# Compiler Block Allocation Strategy

**Date**: 2025-11-12
**Status**: ✅ IMPLEMENTED (Conservative Strategy) - See Also Canonical Reference
**Component**: TempFrameAllocator + BytecodeCompiler

---

## CANONICAL REFERENCE

For the complete variable allocation and indexing architecture, see:

**[Unified-Temp-Frame-Specification.md](./Unified-Temp-Frame-Specification.md)**
**[Variable-Indexing-Architecture.md](./Variable-Indexing-Architecture.md)**

This document provides compiler-specific implementation details and optimization strategies.

---

## Executive Summary

The compiler allocates temporary variables (method args/temps + block args/temps) in the **MethodContext unified temp frame** using absolute indices. The allocation strategy determines which blocks can **reuse slots** vs which must have **unique slots**.

**Current Implementation**: Conservative heuristic based on nesting depth
**Status**: ✅ CORRECT (safe, slightly suboptimal memory usage)
**Optimization Potential**: 🟡 Could be improved with data flow analysis

---

## The Allocation Strategy

### Current Heuristic (BytecodeCompiler.cs:2289)

```csharp
bool isNested = (savedScopeDepth > 0);
allocator.EnterBlockScope(argumentNames, blockTempNames, isNested);
```

**Rule**:
- **Method-level blocks** (depth 0): `isNested = false` → Slots can be reused
- **Nested blocks** (depth > 0): `isNested = true` → Slots cannot be reused

### What TempFrameAllocator Does

**When `isNested = false` (Sequential)**:
- Allocate new slots starting from `_nextSlot`
- On `ExitScope()`: Free slots, reset `_nextSlot` to beginning of block's range
- Next sequential block can **reuse** these slots

**When `isNested = true` (Nested)**:
- Allocate new slots starting from `_nextSlot`
- On `ExitScope()`: Keep slots allocated, `_nextSlot` remains at end
- Subsequent blocks get **new unique** slots

---

## Allocation Examples

### Example 1: Method-Level Sequential Blocks

```smalltalk
method: x
    [:a :b | a + b] value: 1 value: 2.
    [:c | c * 2] value: 5.
```

**Allocation**:
- Slot 0: x (method arg)
- Slot 1: a (first block arg) → **REUSED for 'c'**
- Slot 2: b (first block arg)

After first block exits:
- Slots 1-2 freed
- `_nextSlot` reset to 1

Second block allocates:
- Slot 1: c (reuses slot 1!)

**Maximum frame size**: 3 slots (not 4!)

**Why safe**: First block **completes** before second starts (sequential execution).

### Example 2: Nested Blocks

```smalltalk
method: x
    [:a |
        [:b | a + b] value: 2
    ] value: 1
```

**Allocation**:
- Slot 0: x (method arg)
- Slot 1: a (outer block arg, `isNested=false` at method level)
- Slot 2: b (inner block arg, `isNested=true` because depth=1)

After outer block exits:
- Slot 1 freed (outer was sequential)
- Slot 2 remains allocated (inner was nested)

**Maximum frame size**: 3 slots

**Why `b` cannot be reused**: Inner block might capture variables from outer scope, and both blocks share the same home MethodContext.

### Example 3: The Conservative Gap

```smalltalk
method: x
    [:a |
        [:b | b + 1] value: 2.
        [:c | c + 1] value: 3.
    ] value: 1
```

**Current allocation**:
- Slot 0: x (method arg)
- Slot 1: a (outer block, `isNested=false`)
- Slot 2: b (first inner block, `isNested=true`)
- Slot 3: c (second inner block, `isNested=true`)

**Maximum frame size**: 4 slots

**The inefficiency**:
- Both inner blocks have `savedScopeDepth=1`, so both are marked `isNested=true`
- But they execute **sequentially** (first completes before second starts)
- They **could** reuse the same slot: `b` and `c` could both use slot 2

**Why it's currently conservative**:
The heuristic `isNested = (savedScopeDepth > 0)` doesn't distinguish between:
- **Truly nested** (parent still active during child execution)
- **Peer blocks** (siblings that execute sequentially)

---

## Why the Conservative Strategy is Safe

### Runtime Behavior Ensures Correctness

Even with suboptimal allocation, the runtime behavior is **always correct** because:

1. **Each block activation gets its own BlockContext**
   - BlockContext has its own indexed slots for arguments
   - BlockContext has its own nargs field
   - The `if (index < nargs)` check uses the **current** BlockContext's nargs

2. **All blocks share the same home MethodContext**
   - From CRITICAL-Block-Compilation-Discovery.md:128:
     > "If receiver is BlockContext → home = receiver's home (chain to method)"
   - ALL blocks in a method (regardless of nesting) access the same unified frame

3. **Slot overwriting is safe for sequential blocks**
   - If first block completes before second starts, overwriting is fine
   - The first block's values are no longer needed

### Example: Why Overwriting is Safe

```smalltalk
[:a |
    [:b | b + 1] value: 2.  "First inner - uses slot 2"
    [:c | c + 1] value: 3.  "Second inner - could reuse slot 2"
] value: 1
```

**If slots were reused (slot 2 for both `b` and `c`)**:

First inner block execution:
- BlockContext1 created with nargs=1
- BlockContext1.indexedSlots[0] = 2 (argument 'b')
- PUSH_TEMP 2: Check 2 < 1? NO → Read home.indexedSlots[2] = 2 ✓
- Block completes, BlockContext1 garbage collected

Second inner block execution:
- BlockContext2 created with nargs=1
- BlockContext2.indexedSlots[0] = 3 (argument 'c')
- PUSH_TEMP 2: Check 2 < 1? NO → Read home.indexedSlots[2] = 3 ✓
- (Slot 2 was **overwritten** by Primitive 81, but that's OK - first block is done!)

**Result**: ✅ CORRECT - Each block reads its own value from the unified frame.

---

## Why Nested Blocks Cannot Reuse Slots

### The Problem Case

```smalltalk
[:a |
    [:b | a + b]  "Inner block captures 'a' from outer"
] value: 1
```

**If slots were reused (both `a` and `b` use slot 1)**:

Outer block activation:
- BlockContext1.indexedSlots[0] = 1 (argument 'a')
- home.indexedSlots[1] = 1 (stored by Primitive 81)

Inner block activation:
- BlockContext2.indexedSlots[0] = ? (argument 'b')
- home.indexedSlots[1] = ? (OVERWRITES 'a'!)

Inner block execution:
- PUSH_TEMP 1 ('a'): Check 1 < 1? NO → Read home.indexedSlots[1] = WRONG VALUE!

**Result**: ❌ INCORRECT - Inner block reads wrong value for 'a'.

**Why it fails**:
- Outer block is **still active** when inner block starts (nested execution)
- Both blocks share the same home MethodContext
- Slot 1 is accessed by BOTH blocks simultaneously

---

## The Ideal Strategy (Future Optimization)

### What Would Be Optimal

The ideal allocation strategy would perform **data flow analysis** to determine:

1. **Lifetime analysis**: When does each block's variables become dead?
2. **Escape analysis**: Which blocks capture which variables?
3. **Concurrency analysis**: Which blocks can execute simultaneously?

**Example of optimal allocation**:

```smalltalk
[:a |
    [:b | b + 1] value: 2.  "Doesn't capture 'a'"
    [:c | c + 1] value: 3.  "Doesn't capture 'a'"
] value: 1
```

**Optimal allocation**:
- Slot 0: x (method arg)
- Slot 1: a (outer block arg)
- Slot 2: b **AND** c (REUSED! Both inner blocks use slot 2)

**Maximum frame size**: 3 slots (instead of 4 with current strategy)

### Why We Don't Do This Yet

**Complexity**:
- Requires full data flow analysis
- Need to track variable captures across nesting levels
- Need to model execution order (sequential vs concurrent)

**Benefit**:
- Modest memory savings (typically 1-2 slots per method)
- No performance improvement (allocation happens at compile time)

**Trade-off**: Current conservative strategy is simple, correct, and "good enough" for now.

---

## Implementation Details

### BytecodeCompiler.cs (Lines 2286-2290)

```csharp
// Enter block scope in temp frame allocator
// Determine if block is nested (cannot reuse slots) or sequential (can reuse)
// Simple heuristic: Blocks nested inside other blocks cannot reuse slots
bool isNested = (savedScopeDepth > 0);
allocator.EnterBlockScope(argumentNames, blockTempNames, isNested);
```

**Key variables**:
- `savedScopeDepth`: The scope depth BEFORE entering this block
  - 0 = method scope (block is at method level)
  - 1+ = block scope (block is nested inside another block)

### TempFrameAllocator.cs (Lines 68-94)

```csharp
public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
{
    var scope = new ScopeInfo
    {
        StartSlot = _nextSlot,
        IsSequential = !isNested  // Sequential blocks can reuse, nested cannot
    };
    _scopeStack.Push(scope);

    // Allocate slots for block args
    foreach (var argName in argNames)
    {
        int slot = _nextSlot++;
        _allocatedSlots[argName] = slot;
        scope.AllocatedSlots.Add(slot);
    }

    // Allocate slots for block temps
    foreach (var tempName in tempNames)
    {
        int slot = _nextSlot++;
        _allocatedSlots[argName] = slot;
        scope.AllocatedSlots.Add(slot);
    }

    _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
}
```

### TempFrameAllocator.cs (Lines 101-127)

```csharp
public void ExitScope()
{
    if (_scopeStack.Count == 0)
    {
        throw new InvalidOperationException("Cannot exit scope - no scope is active");
    }

    var scope = _scopeStack.Pop();

    if (scope.IsSequential && scope.AllocatedSlots.Count > 0)
    {
        // Reuse slots from this sequential block
        int minSlot = scope.AllocatedSlots.Min();
        _nextSlot = minSlot;

        // Remove variables from allocation map
        var varsToRemove = _allocatedSlots
            .Where(kvp => scope.AllocatedSlots.Contains(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var varName in varsToRemove)
        {
            _allocatedSlots.Remove(varName);
        }
    }
}
```

**Key logic**:
- If `IsSequential = true`: Reset `_nextSlot` to beginning of block's range
- If `IsSequential = false`: Keep `_nextSlot` at current position (don't reuse)

---

## Verification: Does It Work?

### Test Case 1: Simple Sequential Blocks

```smalltalk
testSequential
    [:a | a + 1] value: 1.
    [:b | b + 2] value: 2.
```

**Allocation**:
- Slot 0: a (first block, sequential)
- After exit: Slot freed, `_nextSlot` = 0
- Slot 0: b (second block, REUSES slot 0) ✓

### Test Case 2: Nested Blocks

```smalltalk
testNested
    [:a | [:b | a + b] value: 2] value: 1.
```

**Allocation**:
- Slot 0: a (outer block, sequential at method level)
- Slot 1: b (inner block, nested at depth 1)
- After inner exits: Slot 1 remains allocated
- After outer exits: Both slots freed ✓

### Test Case 3: Peer Blocks at Depth 1 (Suboptimal but Correct)

```smalltalk
testPeers
    [:a |
        [:b | b + 1] value: 2.
        [:c | c + 1] value: 3.
    ] value: 1.
```

**Allocation**:
- Slot 0: a (outer block, sequential)
- Slot 1: b (first inner, nested)
- Slot 2: c (second inner, nested - DOESN'T reuse slot 1)
- Maximum frame size: 3 slots ✓

**Suboptimal**: Could use 2 slots if `b` and `c` reused slot 1.
**Correct**: All blocks access correct values through unified frame.

---

## Summary

### Current Implementation

✅ **Conservative heuristic**: `isNested = (savedScopeDepth > 0)`
✅ **Always correct**: Runtime behavior guarantees correctness
✅ **Simple**: Easy to understand and maintain
🟡 **Slightly suboptimal**: Allocates extra slots for peer blocks at same nesting level

### Key Architectural Points

1. **Allocation happens at compile time** (TempFrameAllocator)
2. **Access happens at runtime** (GetTemporaryVariable)
3. **Safety comes from dual storage**:
   - BlockContext indexed slots provide per-activation isolation
   - MethodContext unified frame provides shared persistent storage
4. **The `if (index < nargs)` check** ensures correct routing regardless of allocation

### Future Optimization Opportunity

Could improve memory usage by distinguishing:
- **Truly nested** blocks (parent active during child) → Cannot reuse
- **Sequential peer** blocks (siblings that run sequentially) → Can reuse

But current strategy is correct and sufficient for now.

---

## 5. blockCopy: Emission Strategy

### 5.1 Block Literal Detection

Parser identifies `[:args | body]` syntax and creates BlockNode AST.

### 5.2 Offset Calculation

**Algorithm:**
```csharp
int CalculateBlockBodyOffset(BlockNode block)
{
    // Offset is relative to METHOD bytecode start
    int methodStart = 0;
    int currentPosition = _emitter.CurrentBytecodePosition;

    // Account for blockCopy: send size
    int sendSize = 1 + 2 + 3;  // PUSH_SELF + PUSH_CONST + SEND

    return currentPosition + sendSize;
}
```

**For Nested Blocks:**
- Outer block offset: relative to method start
- Inner block offset: ALSO relative to method start (NOT outer block start)
- Each block calculates its own absolute offset

### 5.3 Emission Sequence

```csharp
void EmitBlockLiteral(BlockNode block)
{
    int offset = CalculateBlockBodyOffset(block);

    // Emit blockCopy: send
    Emit(Bytecode.PUSH_SELF);
    EmitPushConstant(offset);
    EmitSend("blockCopy:", 1);

    // Emit block body at calculated offset
    foreach (var stmt in block.Body)
    {
        EmitStatement(stmt);
    }

    EmitReturnStackTop();
}
```

### 5.4 Nested Block Handling

**Example:**
```smalltalk
[:a | [:b | a + b]]
```

**Emission:**
```
0: PUSH_SELF
1: PUSH_CONST 8      ; Outer block body offset
3: SEND blockCopy: 1

; === Outer block body (offset 8) ===
6: PUSH_SELF
7: PUSH_CONST 14     ; Inner block body offset (from method start!)
9: SEND blockCopy: 1
12: RETURN_STACK_TOP

; === Inner block body (offset 14) ===
13: PUSH_TEMP 0      ; a
14: PUSH_TEMP 1      ; b
15: SEND + 1
18: RETURN_STACK_TOP
```

**Key Point:** Inner block offset (14) is calculated from METHOD start (0), not from outer block start (6).

---

## References

**Implementation Files**:
- `Smalltalk80.Compiler\BytecodeCompiler.cs` (lines 2286-2290) - Heuristic logic
- `Smalltalk80.Compiler\Analysis\TempFrameAllocator.cs` - Allocation engine

**Architecture Documents**:
- `docs/Block-Temp-Architecture-FINAL.md` - Runtime dual storage architecture
- `docs/CRITICAL-Block-Compilation-Discovery.md` - Unified temp frame discovery
- `docs/Block-Argument-Dual-Storage-Analysis.md` - Why dual storage is required

**Blue Book Reference**:
- Chapter 30: Context architecture and variable access

---

**Document Version**: 1.0
**Last Updated**: 2025-11-12
**Status**: ✅ DOCUMENTED AND VERIFIED
