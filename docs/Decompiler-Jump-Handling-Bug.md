# BytecodeDecompiler Jump Handling Bug

**Date**: 2025-10-07
**Status**: Root Cause Identified
**Severity**: Low (affects only 1/4640 methods, non-runtime-critical validation tool)

## Summary

The BytecodeDecompiler fails to decompile `SmallInteger>>digitLength` with error "Stack empty" because it **does not follow jump targets** during linear decompilation. This causes the stack simulation to diverge from actual execution when processing code with conditional jumps.

## Error Output

```
Methods decompiled: 4639/4640 (100.0%)
⚠ Decompilation failures: 1
Showing first 10 failures:
  SmallInteger>>digitLength: Stack empty.
```

## Root Cause

**File**: `Smalltalk80.Interpreter/Core/Bytecodes/BytecodeDecompiler.cs`

**Problem**: Lines 697-722 handle jump bytecodes by outputting `<jump +offset>` as a statement but **continuing linearly** through the bytecode array instead of following the jump target.

```csharp
// Line 697-702: Short unconditional forward jump
if (b >= 0x90 && b <= 0x97)
{
    int offset = (b & 0x07) + 1;
    statements.Add($"<jump +{offset}>");  // ❌ Doesn't follow jump
    continue;                              // ❌ Continues to next PC
}
```

**Consequence**: When an unconditional jump is encountered:
1. Decompiler outputs `<jump +N>` statement
2. Decompiler continues to PC+1 (dead code or alternate branch)
3. Stack simulation diverges from actual execution
4. Eventually hits a bytecode that pops from empty stack

**Example**: After `A0 01` (long jump forward +1 byte):
```
PC 6:  A0 01    LONG_JUMP_UNCOND +1    ; Should jump to PC=9
PC 8:  ??       <dead code>             ; Decompiler processes this!
PC 9:  70       PUSH_SELF               ; Actual jump target
```

The decompiler processes PC=8 (dead code) instead of jumping to PC=9, corrupting stack state.

## SmallInteger>>digitLength Bytecode Analysis

**Source** (Smalltalk-80.st lines 35269-35285):
```smalltalk
digitLength
    "Answer with the number of indexable fields in the receiver."
    | maxSize minValue size |
    (self < 16r100 and: [self > -16r100]) ifTrue: [^1].
    maxSize _ SmallInteger maxBytes.
    maxSize = 2 ifTrue: [^2].
    minValue _ -16r100.
    size _ 2.
    [size < maxSize]
        whileTrue:
            [minValue _ minValue bitShift: 8.
            (self <= (-1 - minValue) and: [self > minValue]) ifTrue: [^size].
            size _ size + 1].
    ^maxSize
```

**Bytecode** (92 bytes with complex control flow):
```
Hex: 70 20 B2 89 75 C8 A0 01 70 B3 E0 89 75 C8 A0 01 76 7E E1 80 12 ...
```

**Key Issue**: Multiple `A0` (long jump) and `98-9F` (conditional jump) bytecodes create branches. Linear decompilation processes dead code between branches, causing stack underflow.

## Affected Code Locations

**BytecodeDecompiler.cs**:

| Line | Bytecode | Issue |
|------|----------|-------|
| 192-193 | 0xB0-0xBF | ❌ Unguarded `stack.Pop()` calls for arithmetic sends |
| 100, 110 | 0x60-0x6F | ❌ Unguarded `stack.Pop()` for pop-and-store |
| 162 | 0x78-0x7C | ❌ Unguarded `stack.Pop()` for RETURN_TOP |
| 224-226 | 0xC0-0xCF | ❌ Unguarded `stack.Pop()` for special sends |
| 697-722 | 0x90-0xA7 | ❌ Jump handling doesn't follow targets |

**Note**: Lines 244, 260-261, 278-279 (literal sends 0xD0-0xFF) correctly use defensive `stack.Count > 0 ? stack.Pop() : "<??>"` pattern.

## Solution Options

### Option 1: Add Stack Guards (Quick Fix)

Add defensive checks to all `stack.Pop()` calls:

```csharp
// Before (Line 192-193)
string arg = stack.Pop();
string receiver = stack.Pop();

// After
if (stack.Count < 2)
{
    throw new InvalidOperationException(
        $"Stack underflow at PC {pc-1}: bytecode 0x{b:X2} requires 2 stack items, but only {stack.Count} available. " +
        $"This likely indicates dead code or incorrect jump handling.");
}
string arg = stack.Pop();
string receiver = stack.Pop();
```

**Pros**:
- Simple, low-risk fix
- Provides better error messages for debugging
- Doesn't change decompilation logic

**Cons**:
- Doesn't fix underlying jump-following problem
- Still fails to decompile methods with complex control flow

### Option 2: Implement Control Flow Graph (Proper Fix)

Rewrite decompiler to:
1. Build control flow graph from jump targets
2. Track separate stack states per basic block
3. Merge stack states at join points
4. Only decompile reachable code

**Pros**:
- Correctly handles all jump patterns
- Produces accurate decompilation
- Enables proper structured control flow reconstruction

**Cons**:
- Complex implementation (100+ lines)
- Requires basic block analysis
- Higher risk of introducing bugs

### Option 3: Skip Dead Code Detection (Middle Ground)

Track which PCs are jump targets, skip unreachable code:

```csharp
// Phase 1: Scan for jump targets
HashSet<int> jumpTargets = new HashSet<int>();
for (int i = 0; i < bytecodes.Length; i++)
{
    byte b = bytecodes[i];
    if (b >= 0x90 && b <= 0x9F) // Short jump
    {
        int offset = (b & 0x07) + 1;
        jumpTargets.Add(i + offset);
    }
    else if (b >= 0xA0 && b <= 0xA7) // Long jump
    {
        int offset = ((b & 0x07) << 8) | bytecodes[i + 1];
        jumpTargets.Add(i + 2 + offset);
    }
}

// Phase 2: Linear decompilation with dead code skipping
// When hitting unconditional jump, skip to next jump target
```

**Pros**:
- Moderate complexity (~50 lines)
- Fixes most common cases
- Preserves linear decompilation strategy

**Cons**:
- Still doesn't handle conditional jumps perfectly
- May miss edge cases

## Recommendation

**Option 1 (Add Stack Guards)** is recommended because:

1. **Decompilation is non-critical**: Only used for validation/debugging, not runtime execution
2. **99.98% success rate is acceptable**: Only 1/4640 methods fail
3. **Low risk**: Doesn't change core logic
4. **Better diagnostics**: Clear error messages help debug compiler bugs

If more methods start failing decompilation in the future, revisit Option 2 or 3.

## Related Files

- `Smalltalk80.Interpreter/Core/Bytecodes/BytecodeDecompiler.cs` - Decompiler implementation
- `Smalltalk80.Interpreter/LoaderPhases/Phase12_BytecodeValidation.cs` - Calls decompiler
- `Smalltalk-80 Source/Smalltalk-80.st` - Line 35269-35285 (digitLength source)
- `docs/Interpreter-Architecture.md` - Section 2.13 "Schema-Driven Round-Tripping"

## Testing Strategy

After implementing Option 1:

1. Run decompilation validation: `dotnet run --project Smalltalk80.Interpreter`
2. Verify error message includes:
   - PC location
   - Bytecode value
   - Stack depth expected vs actual
   - Hint about dead code/jumps
3. Confirm 4639/4640 methods still decompile successfully
4. Document `SmallInteger>>digitLength` as known limitation

## Future Work

If decompilation becomes runtime-critical (e.g., for IDE integration):
- Implement Option 2 (CFG-based decompilation)
- Add structured control flow reconstruction (`ifTrue:`, `whileTrue:`, etc.)
- Emit proper Smalltalk syntax instead of linearized statements
