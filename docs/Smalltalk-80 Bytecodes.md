# Smalltalk-80 Bytecode Specification

This is the canonical bytecode reference for the Smalltalk-80 interpreter. The compiler MUST generate bytecode that matches these specifications, and the interpreter implementation MUST follow these formats exactly.

## Bytecode Table

| Bytecode  | Binary        | Description                                                               |
|-----------|---------------|---------------------------------------------------------------------------|
| 0–15      | 0000iiii      | Push Receiver Variable #iiii                                             |
| 16–31     | 0001iiii      | Push Temporary Location #iiii                                            |
| 32–63     | 001iiiii      | Push Literal Constant #iiiii                                             |
| 64–95     | 010iiiii      | Push Literal Variable #iiiii (Association value)                         |
| 96–103    | 01100iii      | Pop and Store Receiver Variable #iii                                     |
| 104–111   | 01101iii      | Pop and Store Temporary Location #iii                                    |
| 112       | 01110000      | Push Receiver (self)                                                     |
| 113       | 01110001      | Push True                                                                |
| 114       | 01110010      | Push False                                                               |
| 115       | 01110011      | Push Nil                                                                 |
| 116       | 01110100      | Push -1                                                                  |
| 117       | 01110101      | Push 0                                                                   |
| 118       | 01110110      | Push 1                                                                   |
| 119       | 01110111      | Push 2                                                                   |
| 120       | 01111000      | Return Receiver (self) From Message                                      |
| 121       | 01111001      | Return True From Message                                                 |
| 122       | 01111010      | Return False From Message                                                |
| 123       | 01111011      | Return Nil From Message                                                  |
| 124       | 01111100      | Return Stack Top From Message                                            |
| 125       | 01111101      | Return Stack Top From Block                                              |
| 126–127   | 0111111x      | unused                                                                   |
| 128       | 10000000      | Extended Push (see format below)                                         |
| 129       | 10000001      | Extended Store (see format below)                                        |
| 130       | 10000010      | Extended Pop and Store (see format below)                               |
| 131       | 10000011      | Single Extended Send (see format below)                                  |
| 132       | 10000100      | Double Extended Send (see format below)                                  |
| 133       | 10000101      | Single Extended Super Send (see format below)                            |
| 134       | 10000110      | Double Extended Super Send (see format below)                            |
| 135       | 10000111      | Pop Stack Top                                                            |
| 136       | 10001000      | Duplicate Stack Top                                                      |
| 137       | 10001001      | Push Active Context                                                      |
| 138–143   | 100010xx      | unused                                                                   |
| 144–151   | 10010iii      | Short Jump Forward iii+1 (1 through 8)                                   |
| 152–159   | 10011iii      | Short Conditional Jump On False (iii-152)+1 (1 through 8)               |
| 160       | 10100000      | Medium Unconditional Jump Forward NN+1 (next byte NN, jumps 1-256)      |
| 161–167   | 10100iii      | Long Unconditional Jump: N=(iii-160)+1 operand bytes, offset=NN+1       |
| 168       | 10101000      | Medium Conditional Jump On False NN+1 (next byte NN, jumps 1-256)       |
| 169–175   | 101010ii      | Long Conditional Jump On False: N=(ii-168)+1 operand bytes, offset=NN+1 |
| 176–191   | 1011iiii      | Send Arithmetic Selector #iiii                                           |
| 192–207   | 1100iiii      | Send Special Selector #iiii                                              |
| 208–223   | 1101iiii      | Send Literal Selector #iiii With No Arguments                            |
| 224–239   | 1110iiii      | Send Literal Selector #iiii With 1 Argument                              |
| 240–255   | 1111iiii      | Send Literal Selector #iiii With 2 Arguments                             |

## Extended Bytecode Formats

### Bytecode 128: Extended Push
**Format:** First byte = 128, Second byte = `vvkkkkkk`
- `vv` (bits 6-7): Variable type
  - 00: Receiver variable (instance variable)
  - 01: Temporary variable
  - 10: Literal constant
  - 11: Literal variable (Association)
- `kkkkkk` (bits 0-5): Variable index (0-63)

### Bytecode 129: Extended Store
**Format:** First byte = 129, Second byte = `vvkkkkkk`
- `vv` (bits 6-7): Variable type
  - 00: Receiver variable
  - 01: Temporary variable
  - 10: (illegal)
  - 11: Literal variable (Association)
- `kkkkkk` (bits 0-5): Variable index (0-63)
- Note: Stores but leaves value on stack

### Bytecode 130: Extended Pop and Store
**Format:** First byte = 130, Second byte = `vvkkkkkk`
- Same format as 129, but pops value from stack after storing

### Bytecode 131: Single Extended Send
**Format:** First byte = 131, Second byte = `jjjkkkkk`
- `jjj` (bits 5-7): Argument count (0-7)
- `kkkkk` (bits 0-4): Selector index in literal frame (0-31)

**Example:** `131 97` sends selector at literal #1 with 3 arguments
- 97 = 01100001 binary = (3 << 5) | 1

### Bytecode 132: Double Extended Send
**Format:** First byte = 132, Second byte = selector index, Third byte = argument count
- Second byte: Selector index in literal frame (0-255)
- Third byte: Argument count (0-255)

**Example:** `132 1 3` sends selector at literal #1 with 3 arguments

### Bytecode 133: Single Extended Super Send
**Format:** First byte = 133, Second byte = `jjjkkkkk`
- Same format as bytecode 131, but performs super send

### Bytecode 134: Double Extended Super Send
**Format:** First byte = 134, Second byte = selector index, Third byte = argument count
- Same format as bytecode 132, but performs super send

## Special Selectors

### Arithmetic Selectors (176-191)
Used with bytecodes 176-191. Each takes 1 argument.

| Index | Selector      | Bytecode |
|-------|---------------|----------|
| 0     | +             | 176      |
| 1     | -             | 177      |
| 2     | <             | 178      |
| 3     | >             | 179      |
| 4     | <=            | 180      |
| 5     | >=            | 181      |
| 6     | =             | 182      |
| 7     | ~=            | 183      |
| 8     | *             | 184      |
| 9     | /             | 185      |
| 10    | \\            | 186      |
| 11    | @             | 187      |
| 12    | bitShift:     | 188      |
| 13    | //            | 189      |
| 14    | bitAnd:       | 190      |
| 15    | bitOr:        | 191      |

### Special Selectors (192-207)
Used with bytecodes 192-207. Argument count varies.

| Index | Selector      | Args | Bytecode |
|-------|---------------|------|----------|
| 0     | at:           | 1    | 192      |
| 1     | at:put:       | 2    | 193      |
| 2     | size          | 0    | 194      |
| 3     | next          | 0    | 195      |
| 4     | nextPut:      | 1    | 196      |
| 5     | atEnd         | 0    | 197      |
| 6     | ==            | 1    | 198      |
| 7     | class         | 0    | 199      |
| 8     | blockCopy:    | 1    | 200      |
| 9     | value         | 0    | 201      |
| 10    | value:        | 1    | 202      |
| 11    | do:           | 1    | 203      |
| 12    | new           | 0    | 204      |
| 13    | new:          | 1    | 205      |
| 14    | x             | 0    | 206      |
| 15    | y             | 0    | 207      |

## Jump Bytecode Specifications

### Understanding Smalltalk-80 Control Flow

While Smalltalk-80 is primarily message-based, the Blue Book does define conditional jump bytecodes as compiler optimizations. When the compiler recognizes patterns like `expr ifTrue: [block]`, it can inline them using conditional jumps rather than creating block contexts and sending messages.

### Complete Jump Bytecode Table

| Jump Category | Opcode Range | Operand Bytes | Offset Formula | Condition Tested |
|--------------|--------------|---------------|----------------|-----------------|
| **Unconditional short** | 144-151 | 0 | (opcode - 144) + 1 ⇒ 1-8 | Always jump |
| **Unconditional medium** | 160 | 1 | nextByte + 1 ⇒ 1-256 | Always jump |
| **Unconditional long** | 161-167 | 2-8 | bigEndianUInt(N bytes) + 1 | Always jump |
| **Conditional short** | 152-159 | 0 | (opcode - 152) + 1 ⇒ 1-8 | Pop; if FALSE jump |
| **Conditional medium** | 168 | 1 | nextByte + 1 ⇒ 1-256 | Pop; if FALSE jump |
| **Conditional long** | 169-175 | 2-8 | bigEndianUInt(N bytes) + 1 | Pop; if FALSE jump |

**Key Insight**: All conditional jumps test for FALSE. There are no "jump if true" opcodes in the Smalltalk-80 VM.

### Unconditional Jumps
The Smalltalk-80 Blue Book divides unconditional forward branches into three classes:
- **Short jumps**: 1-8 bytes (bytecodes 144-151)
- **Medium jumps**: 9-256 bytes (bytecode 160 + 1-byte operand)
- **Long jumps**: ≥257 bytes (bytecodes 161-167 + multi-byte operands)

### Short Jumps (Bytecodes 144-151)
- **Encoding**: Single byte, opcode = 144 + (distance - 1)
- **Jump range**: 1 to 8 bytes forward
- **Examples**:
  - Jump 1 byte → bytecode 144 (0x90)
  - Jump 8 bytes → bytecode 151 (0x97)
- **Interpreter**: `offset = (bytecode - 144) + 1; PC += offset`

### Medium Jumps (Bytecode 160)
- **Encoding**: Two bytes [160] [NN] where NN = distance - 1
- **Jump range**: 1 to 256 bytes forward (NN + 1)
- **Examples**:
  - Jump 9 bytes → [160, 8]
  - Jump 256 bytes → [160, 255]
- **Interpreter**: `NN = fetchNextByte(); offset = NN + 1; PC += offset`
- **Note**: Compilers must use short jumps for distances ≤8

### Long Jumps (Bytecodes 161-167)
- **Encoding**: Opcode + N operand bytes (big-endian unsigned)
- **Opcode selection**:
  - 161: 2-byte operand (jumps up to 65,536)
  - 162: 3-byte operand (jumps up to 16,777,216)
  - 163: 4-byte operand
  - ...
  - 167: 8-byte operand
- **Formula**: opcode = 159 + N where N is operand byte count (2-8)
- **Operand**: Encodes (distance - 1) as N-byte big-endian unsigned integer
- **Interpreter**: 
  ```
  N = bytecode - 159;  // operand byte count
  NN = decodeBigEndianUInt(fetchNextNBytes(N));
  offset = NN + 1;
  PC += offset;
  ```

### Compiler Jump Selection Algorithm (Unconditional)
```
delta = targetIP - currentIP;
if (delta >= 1 && delta <= 8)
    emit short jump (144 + delta - 1)
else if (delta <= 256)
    emit medium jump [160, delta - 1]
else
    N = minimum bytes needed for (delta - 1)
    emit long jump [159 + N, encodeBigEndian(delta - 1, N)]
```

## Conditional Jump Specifications

**Critical Point**: ALL conditional jumps in Smalltalk-80 test for FALSE. There are no "jump if true" opcodes. The compiler generates code that tests for false and jumps over the consequent block.

### Short Conditional Jump on False (Bytecodes 152-159)
- **Pop and Jump on False**: Skip forward if top of stack is false
- **Encoding**: Single byte, no operands
- **Jump range**: (opcode - 152) + 1 = 1 to 8 bytes forward
- **Interpreter semantics**:
  ```
  value = pop();
  if (value == false) {
      PC += (bytecode - 152) + 1;
  }
  ```

### Medium Conditional Jump on False (Bytecode 168)
- **Pop and Jump on False**: Skip forward if top of stack is false
- **Encoding**: Two bytes [168] [NN]
- **Jump range**: NN + 1 = 1 to 256 bytes forward
- **Interpreter semantics**:
  ```
  value = pop();
  NN = fetchNextByte();
  if (value == false) {
      PC += NN + 1;
  }
  ```

### Long Conditional Jump on False (Bytecodes 169-175)
- **Pop and Jump on False**: Skip forward if top of stack is false
- **Encoding**: Opcode + N operand bytes where N = opcode - 168
  - Bytecode 169: 2 operand bytes (jumps up to 65,536)
  - Bytecode 170: 3 operand bytes (jumps up to 16,777,216)
  - Bytecode 171: 4 operand bytes
  - ...
  - Bytecode 175: 8 operand bytes
- **Formula**: offset = decodeBigEndian(N operand bytes) + 1
- **Interpreter semantics**:
  ```
  value = pop();
  N = bytecode - 168;  // gives 1-7 for bytecodes 169-175
  NN = decodeBigEndianUInt(fetchNextNBytes(N));
  if (value == false) {
      PC += NN + 1;
  }
  ```

### Compiler Pattern Matching

Since all conditional jumps test for FALSE, the compiler inverts logic as needed:

1. **Simple ifTrue:**
   ```smalltalk
   expr ifTrue: [statements]
   ```
   Becomes:
   - Evaluate expr (leaves boolean on stack)
   - **Jump on False** past the statements (skip if condition is false)
   - Execute statements inline
   - Continue...
   
2. **Simple ifFalse:**
   ```smalltalk
   expr ifFalse: [statements]  
   ```
   Becomes:
   - Evaluate expr (leaves boolean on stack)
   - Negate the boolean (true→false, false→true)
   - **Jump on False** past the statements (skip if original was true)
   - Execute statements inline
   - Continue...

3. **Complex conditionals**: For non-optimizable patterns, the compiler falls back to creating actual blocks and sending messages.

### Why Both Messages and Jumps?

- **Performance**: Conditional jumps avoid block context creation overhead
- **Flexibility**: Message sends remain available for complex cases
- **Optimization**: Common patterns get inlined automatically
- **Simplicity**: VM only needs to check boolean and branch

## Compiler Conditional Inlining Rules

The compiler must carefully decide when to inline conditionals as jump bytecodes versus using the normal block/message approach.

### Selectors Eligible for Inlining

Only these three selectors can be inlined:
- `ifTrue:` (one-argument)
- `ifFalse:` (one-argument)
- `ifTrue:ifFalse:` (two-argument)

### Requirements for Inlining

#### 1. Block Requirements
All block arguments must be:
- **Literal blocks**: Defined directly in the method's literal frame
- **Zero-argument blocks**: No parameters like `[:x | ...]`
- **Locally scoped**: No non-local returns (`^`)
- **Simple evaluation**: No `value:`, `value:value:`, etc.
- **Compile-time sized**: Block size must be determinable at compile time

Reject inlining if:
- Block is stored in a temp or computed at runtime
- Block declares parameters
- Block contains non-local return
- Block sends multi-argument value selectors
- Block size cannot be determined at compile time

#### 2. Test Expression Requirements
The test expression must be:
- **Side-effect free**: No writes to temps or instance variables
- **Simple control flow**: No nested blocks that mutate control flow

### Inlining Implementation

When all requirements are met, compile as follows:

#### For `expr ifTrue: [thenBlock]`:
```
1. Compile expr (leaves boolean on stack)
2. Emit jump-if-false over thenBlock (choose short/medium/long)
3. Inline thenBlock bytecodes
4. Continue...
```

#### For `expr ifFalse: [elseBlock]`:
```
1. Compile expr (leaves boolean on stack)
2. Negate (optional: or just jump-if-true equivalent)
3. Emit jump-if-false over elseBlock
4. Inline elseBlock bytecodes
5. Continue...
```

#### For `expr ifTrue: [thenBlock] ifFalse: [elseBlock]`:
```
1. Compile expr (leaves boolean on stack)
2. Emit jump-if-false to elseBlock
3. Inline thenBlock bytecodes
4. Emit unconditional jump over elseBlock
5. Inline elseBlock bytecodes
6. Continue...
```

### Fallback to Message Send

When inlining requirements are NOT met:
1. Push block literals onto stack
2. Issue normal send bytecode for `ifTrue:`, `ifFalse:`, or `ifTrue:ifFalse:`
3. Runtime creates BlockContext objects
4. Runtime dispatches to `True>>ifTrue:`, `False>>ifFalse:`, etc.

### Summary

- **Inline only when**: Blocks are literal, zero-argument, no non-local returns, compile-time sized
- **Fall back when**: Blocks are dynamic, have parameters, contain `^`, call `value:`, or have unpredictable size
- **Single VM primitive**: Only `jump-if-false` exists; compiler must arrange logic accordingly

## Implementation References

### Interpreter (BytecodeDispatcher.cs)
The interpreter's bytecode dispatcher must handle each bytecode exactly as specified above.

### Compiler (BytecodeCompiler.cs)
The compiler must generate bytecode that matches these specifications exactly:
- Use bytecodes 208-223 for 0-argument sends with selector index 0-15
- Use bytecodes 224-239 for 1-argument sends with selector index 0-15
- Use bytecodes 240-255 for 2-argument sends with selector index 0-15
- Use bytecode 131 for sends with 3-7 arguments or selector index 16-31
- Use bytecode 132 for sends with 8+ arguments or selector index 32+

## Common Bytecode Patterns

### Method Call: `object message: arg1 with: arg2 and: arg3`
1. Push receiver (object)
2. Push arg1
3. Push arg2
4. Push arg3
5. `131 xxx` where xxx encodes 3 arguments and selector index

### Assignment to Class Variable: `ClassVar _ expression`
1. Compile expression (leaves value on stack)
2. `130 xxx` where xxx = 0xC0 | index (literal variable type + index)

### Instance Variable Access: `^instVar`
1. `0-15` to push instance variable
2. `124` to return stack top

## Validation Requirements

All three components must be in sync:
1. This specification document
2. The compiler implementation (BytecodeCompiler.cs)
3. The interpreter implementation (BytecodeDispatcher.cs)

Any discrepancy between these three is a bug that must be fixed immediately.