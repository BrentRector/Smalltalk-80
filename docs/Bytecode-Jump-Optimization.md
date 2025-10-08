# Bytecode Jump Optimization System

**Created**: 2025-10-07
**Status**: New Implementation - Replaces Address-Shifting Bug Fix

## Problem Statement

The original bytecode compiler had a critical bug when compiling nested blocks in `ifTrue:/ifFalse:` expressions:

1. Compiler emits 2-byte jump placeholders
2. Compiles block body (which may contain nested blocks)
3. Nested blocks emit their own jumps
4. When patching jumps to short form (1 byte), uses `RemoveAt()` to delete placeholder byte
5. **This shifts all subsequent addresses**, breaking jump distance calculations
6. Results in invalid bytecode with stray jump opcodes (e.g., 0x97 appearing where it shouldn't)

### Example Failure

```smalltalk
arguments size = 0
    ifTrue: [view indicatorOnDuring: [model perform: selector]]
    ifFalse: [...]
```

The nested block `[model perform: selector]` would emit a BlockCopy jump. When optimized to short form, `RemoveAt()` shifts addresses, causing the outer `ifTrue:` jump to calculate wrong distance.

## Solution: AST-Based Symbolic Jumps

Instead of numeric offsets, use **AST node references** as jump targets. This allows:

1. **No address shifting during emission** - all jumps start in long form
2. **Post-compilation optimization** - iteratively optimize jumps after all code generated
3. **Automatic target tracking** - AST nodes move with bytecode changes

### Architecture

#### 1. BytecodeFragment (Base Class)

Every piece of bytecode is a fragment associated with an AST node:

```csharp
public abstract class BytecodeFragment
{
    public required IParseTree AstNode { get; init; }  // AST association
    public int Position { get; set; } = -1;            // Final position
    public abstract int Size { get; }                  // Current size
    public abstract byte[] EmitBytes();                // Generate bytes
}
```

#### 2. Fragment Types

**SimpleInstruction**: Fixed bytecode (most instructions)
```csharp
public class SimpleInstruction : BytecodeFragment
{
    public required byte[] Bytes { get; init; }
    public override int Size => Bytes.Length;
    public override byte[] EmitBytes() => Bytes;
}
```

**JumpInstruction**: Symbolic jump with AST target
```csharp
public class JumpInstruction : BytecodeFragment
{
    public required JumpType Type { get; init; }       // Unconditional/Conditional
    public required IParseTree TargetNode { get; init; } // AST target (symbolic!)

    // Optimize based on distance (returns true if encoding changed)
    public bool Optimize(int targetPosition) { ... }
}
```

**JumpTarget**: Zero-byte marker for jump destinations
```csharp
public class JumpTarget : BytecodeFragment
{
    public override int Size => 0;  // Just marks position
    public override byte[] EmitBytes() => [];
}
```

#### 3. Iterative Optimizer

```csharp
public class BytecodeAssembler
{
    public byte[] Assemble()
    {
        // Build AST -> fragment map
        var nodeToFragment = new Dictionary<IParseTree, BytecodeFragment>();

        bool changed;
        do {
            changed = false;

            // Update all positions
            int position = 0;
            foreach (var frag in _fragments) {
                frag.Position = position;
                position += frag.Size;
            }

            // Optimize all jumps
            foreach (var jump in _fragments.OfType<JumpInstruction>()) {
                var target = nodeToFragment[jump.TargetNode];
                if (jump.Optimize(target.Position))
                    changed = true;  // Encoding changed, repeat
            }
        } while (changed);

        // Emit final bytecode
        return _fragments.SelectMany(f => f.EmitBytes()).ToArray();
    }
}
```

### Optimization Strategy

**Key Principle**: ALL jumps are emitted in **LONGEST form first** (3-4 bytes). This ensures:
- No address shifting during initial emission
- All positions are stable for first distance calculations
- Optimization only makes bytecode **shorter**, never longer
- Convergence is guaranteed (monotonic decrease in size)

**Iterative Optimization Process**:

1. **Initial State**: All jumps in long form (maximum bytes)
   - Unconditional: 4 bytes (0xA1-0xA7 + multi-byte offset)
   - Conditional: 4 bytes (0x80 + subop + 2-byte offset)

2. **Iteration N**:
   - Calculate all fragment positions (accumulate sizes)
   - For each jump: calculate distance to target
   - Try shorter encoding if distance allows:
     - Unconditional: Long (4 bytes) → Medium (2 bytes) → Short (1 byte)
     - Conditional: Long (4 bytes) → Medium (4 bytes, smaller offset)
   - If ANY jump encoding changed, set `changed = true`

3. **Repeat** until no changes (convergence guaranteed)
   - Each iteration can only make jumps shorter
   - Shorter jumps reduce subsequent distances
   - This may enable other jumps to become shorter
   - Eventually all jumps reach optimal encoding

4. **Convergence**: When `changed = false`, all jumps are optimal

**Example Convergence**:
```
Iteration 1: [Long jump: 4 bytes to offset 300] → stays Long
             [Long jump: 4 bytes to offset 50]  → becomes Medium (2 bytes)
Iteration 2: [Long jump: 4 bytes to offset 298] → becomes Medium (2 bytes saved!)
             [Medium jump: 2 bytes to offset 50] → becomes Short (1 byte)
Iteration 3: [Medium jump: 2 bytes to offset 297] → stays Medium
             [Short jump: 1 byte to offset 50]   → stays Short
Iteration 4: No changes → CONVERGED
```

**Why This Works**:
- Reducing a jump's size decreases the distance to all jumps AFTER it
- This cascade effect requires iteration to stabilize
- The process always terminates (sizes can only decrease, minimum is 1 byte)

### Usage in Compiler

```csharp
var emitter = new FragmentEmitter(methodNode);

// Emit conditional jump with AST target
emitter.SetContext(testExpr);
emitter.EmitJump(falseBlockNode, JumpInstruction.JumpType.ConditionalFalse);

// Compile true block
CompileBlock(trueBlock);

// Emit jump over false block
emitter.EmitJump(afterFalseNode, JumpInstruction.JumpType.Unconditional);

// Mark false block start
emitter.EmitJumpTarget(falseBlockNode);
CompileBlock(falseBlock);

// Mark end
emitter.EmitJumpTarget(afterFalseNode);

// Assemble with optimization
byte[] bytecode = emitter.Assemble();
```

### Benefits

1. **No address shifting bugs** - positions only calculated during optimization, not during emission
2. **Optimal bytecode** - iteratively finds smallest encoding
3. **Strongly typed** - AST nodes as targets prevent errors
4. **Testable** - can verify fragment structure before assembly
5. **Maintainable** - separation of emission and optimization

### Integration Plan

1. ✅ Create `BytecodeFragment` hierarchy
2. ✅ Create `FragmentEmitter` with symbolic jumps
3. ✅ Implement `BytecodeAssembler` with iterative optimizer
4. ⏳ Refactor `BytecodeCompiler` to use `FragmentEmitter`
5. ⏳ Update block compilation to use AST-targeted jumps
6. ⏳ Test all scenarios (nested blocks, ifTrue:/ifFalse:, loops)

### Related Files

- `Smalltalk80.Compiler/BytecodeGeneration/BytecodeFragment.cs` - Fragment base and types
- `Smalltalk80.Compiler/BytecodeGeneration/FragmentEmitter.cs` - Fragment-based emitter
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Will be refactored to use new system
- `Shared/Core/Bytecodes/BytecodeDefinitions.cs` - Bytecode enum definitions
- `docs/Interpreter-Architecture.md` - Bytecode specification

### Testing Strategy

1. **Unit test fragments**: Verify each fragment type emits correct bytes
2. **Test optimizer**: Verify convergence, optimal encodings
3. **Integration tests**: Compile methods with all jump scenarios
4. **Regression tests**: Ensure all 4,640 methods compile correctly
5. **Validation**: Run bytecode stack/operand validation on all methods
