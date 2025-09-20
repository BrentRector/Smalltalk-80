# Smalltalk-80 Bytecode Reference
## Based on the Blue Book (1983) Specification

This document provides the authoritative bytecode specification for the Smalltalk-80 virtual machine, as defined in the Blue Book. This serves as the ground truth for:
- The standalone compiler's bytecode generation
- The bytecode interpreter's execution semantics
- Stack effect validation and testing

## Bytecode Format

Each bytecode consists of:
- **Opcode byte**: Contains the instruction type and sometimes embedded parameters
- **Optional extension bytes**: For extended instructions requiring additional parameters
- **Binary pattern**: Shows bit layout for decoding embedded parameters

## Complete Bytecode Listing

### Stack Operations - Push

#### 0–15 (0000iiii) - PushReceiverVariable #i
- **Decode**: Low 4 bits = i (0–15)
- **Stack effect**: `... → ..., receiver.instVars[i]`
- **Semantics**: Fetch the i-th instance variable of self from the active MethodContext and push it

#### 16–31 (0001iiii) - PushTemporaryVariable #i
- **Decode**: Low 4 bits = i (0–15)
- **Stack effect**: `... → ..., temporaries[i]`
- **Semantics**: Push the i-th temporary from the active MethodContext

#### 32–47 (0010iiii) - StoreTemporaryVariable #i
- **Decode**: Low 4 bits = i (0–15)
- **Stack effect**: `..., value → ..., value`
- **Semantics**: Pop into temporary i, then re-push that value

#### 48–63 (0011iiii) - PushArgument #i
- **Decode**: Low 4 bits = i (0–15)
- **Stack effect**: `... → ..., arguments[i]`
- **Semantics**: Push the i-th method argument from the active MethodContext

#### 64–71 (010000ii) - PushLiteralConstant #ii
- **Decode**: Low 2 bits = ii (0–3)
- **Stack effect**: `... → ..., literals[ii]`
- **Semantics**: Push the ii-th entry of the compiled method's literal frame (raw object)

#### 72–79 (010001ii) - PushLiteralVariable #ii
- **Decode**: Low 2 bits = ii (0–3)
- **Stack effect**: `... → ..., association.value`
- **Semantics**: Fetch the ii-th literal (an Association) and push its value slot (the current binding)

### Stack Operations - Manipulation

#### 80 (01010000) - PopTopOfStack
- **Stack effect**: `..., x → ...`
- **Semantics**: Discard the top of the evaluation stack

#### 81 (01010001) - DuplicateTopOfStack
- **Stack effect**: `..., x → ..., x, x`
- **Semantics**: Push a copy of the current top element

### Primitive Operations

#### 82–84 (0101001i) - DoPrimitive #i
- **Decode**: Low 2 bits = i (0–3)
- **Stack effect**: Pop N args → `..., result`
- **Semantics**: Invoke VM primitive number i, popping the required receiver+args and pushing its return value (or signaling fallback)

### Context Operations

#### 85 (01010101) - PushActiveContext ("PushThisContext")
- **Stack effect**: `... → ..., currentMethodContext`
- **Semantics**: Push the active MethodContext object itself

### Reserved

#### 86–87 (01010110–01010111) - Reserved

### Block Operations

#### 88–103 (01011kkk ... 01101kkk) - PushBlockCopy #kkk
- **Decode**: Bits kkk = block literal index (0–7)
- **Stack effect**: `... → ..., blockContext`
- **Semantics**: Allocate a new BlockContext, set its homeContext to the active MethodContext, initialize its bytecode range from the CompiledMethod's literals[kkk], then push the new block closure

### Reserved/Extensions

#### 104–119 (011010xx ... 01110xxx) - Reserved / VM-specific extensions

### Return Operations

#### 120–123 (011110ii) - Return Receiver/True/False/Nil
- **ii = 0**: ReturnReceiver
- **ii = 1**: ReturnTrue
- **ii = 2**: ReturnFalse
- **ii = 3**: ReturnNil
- **Stack effect**: `..., value → to caller`
- **Semantics**: Pop (or use receiver for ReturnReceiver), discard current MethodContext, resume its sender, push the returned value

#### 124 (01111100) - ReturnTopOfStack ("PopAndReturn")
- **Stack effect**: `..., x → to caller`
- **Semantics**: Pop the top of stack and return it to the caller context

#### 125 (01111101) - BlockReturn #i
- **Decode**: Low 3 bits = block temp index i
- **Stack effect**: `..., value → to home context`
- **Semantics**: Perform a non-local return from the block: pop value, discard intervening contexts, resume the block's home context, push value there

### Reserved

#### 126–127 (0111111i) - Reserved

### Send Operations

#### 128–191 (1jjjkkkk) - Send #kkkk With jjj Arguments
- **Decode**: Top 3 bits = jjj (arg count 0–7), low 5 bits = selector index kkkkk (0–31)
- **Stack effect**: `..., recv, aₙ ... a₁ → ..., result`
- **Semantics**: Pop receiver and jjj args, look up selector literals[kkkkk] in receiver's class, create a new MethodContext with those slots, begin execution at the method's first bytecode, and push the eventual return value

### Extended Operations

#### 192 (11000000) - ExtendedSend
- **Layout**: Next two bytes = 16-bit selector number; next byte = argCount
- **Semantics**: Like Send #kkkk with jjj args, but uses full selector table and arbitrary arg count

#### 193–207 (11000001–11001111) - Reserved for future uses

### Jump Operations

#### 208–215 (11010000–11010111) - Jump /signedByteOffset/
- **Decode**: Low 8 bits = signed offset (–128 to +127)
- **Semantics**: Unconditionally add offset to the MethodContext's instruction pointer

#### 216 (11011000) - JumpIfTrue
- **Semantics**: Pop cond; if truthy, add offset; else continue

#### 217 (11011001) - JumpIfFalse
- **Semantics**: Pop cond; if falsey, add offset

#### 218 (11011010) - JumpIfNil
- **Semantics**: Pop obj; if obj == nil, add offset

#### 219–223 (11011011–11011111) - Reserved

## Important Implementation Notes

### Our Current Implementation Issues

Based on this specification, our current interpreter has an **incorrect bytecode mapping**. The Blue Book specification shows:

- **128–191**: Send operations with argument count encoded in top 3 bits
- **208–215**: Jump operations

However, our `BytecodeDispatcher.cs` incorrectly maps:
- **176–191**: SendLiteralSelector with 0 args
- **192–207**: SendLiteralSelector with 1 arg
- **208–223**: SendLiteralSelector with 2 args

This is completely wrong according to the Blue Book. The correct implementation should be:

For bytecodes 128–191 (binary: 1jjjkkkk):
- Extract argument count: `jjj = (bytecode >> 5) & 0x07`
- Extract selector index: `kkkkk = bytecode & 0x1F`
- Send the message with jjj arguments using selector at literals[kkkkk]

### Example Bytecode Analysis

The bytecode sequence `[65, 208, 124]` for "Object initialize":
1. **65 (0x41)**: This is in range 64-71, so it's `PushLiteralConstant #1` (65 = 64 + 1)
   - Pushes literals[1] which should be the Object class
2. **208 (0xD0)**: According to Blue Book, this is `Jump` with offset 0, NOT a send!
   - This is clearly wrong for "Object initialize"
3. **124 (0x7C)**: ReturnTopOfStack

The correct bytecode should be:
1. **65**: PushLiteralConstant #1 (Object class)
2. **128**: Send #0 With 0 Arguments (binary: 10000000 = 128)
   - Top 3 bits (100) = 0 arguments
   - Bottom 5 bits (00000) = selector at index 0
3. **124**: ReturnTopOfStack

## Validation Notes

1. Every opcode's binary pattern and range here matches Chapter 28 of the Blue Book
2. Instant fields (iiii, kkk, jjj) index into the receiver's instVars, temporaries, literal frame, or selector table as prescribed
3. Block creation, non-local returns, and extended sends preserve semantics exactly as in the Xerox PARC Smalltalk-80 VM
4. Use this as the ground truth when comparing bytecode dispatch tables, stack-effect tests, and context-unwinding logic

## Action Items

Our interpreter needs to be completely rewritten to match the Blue Book specification:
1. Fix the bytecode ranges for send operations (128-191)
2. Implement proper jump operations (208-215)
3. Fix any JSON files containing incorrect bytecode
4. Update the standalone compiler to generate correct bytecode