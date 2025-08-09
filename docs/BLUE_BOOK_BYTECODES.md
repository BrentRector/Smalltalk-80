# Blue Book Canonical Bytecode Specification

This document contains the **canonical Smalltalk-80 bytecode specification** from the Blue Book (Goldberg & Robson, 1983).

## Complete Bytecode Table

### Push Operations (0-95)

| Range | Operation | Description |
|-------|-----------|-------------|
| 0-15 | `pushReceiverVariable: n` | Push instance variable n of receiver |
| 16-31 | `pushTemporaryVariable: n` | Push temporary variable n |
| 32-63 | `pushLiteralConstant: n` | Push literal constant at index n |
| 64-95 | `pushLiteralVariable: n` | Push value of literal variable (Association) at index n |

### Store Operations (96-175)

| Range | Operation | Description |
|-------|-----------|-------------|
| 96-127 | `storeIntoReceiverVariable: n` | Store top of stack into instance variable n (no pop) |
| 128-159 | `storeIntoTemporaryVariable: n` | Store top of stack into temporary variable n (no pop) |
| 160-175 | `storeIntoLiteralVariable: n` | Store top of stack into literal variable n (no pop) |

### Return Operations (176-180)

| Value | Operation | Description |
|-------|-----------|-------------|
| 176 | `returnReceiver` | Return self |
| 177 | `returnTrue` | Return true |
| 178 | `returnFalse` | Return false |
| 179 | `returnNil` | Return nil |
| 180 | `returnTop` | Return top of stack |

### Unused (181-191)

| Range | Operation | Description |
|-------|-----------|-------------|
| 181-191 | unused | Reserved for future use |

### Send Operations (192-255)

**🔹 Format: 11xx_xxxx — All send bytecodes start with 11 in the high bits.**

#### Literal Sends (192-255)
These bytecodes reference selectors stored in the literal frame.

| Bytecode Name | Range | Binary Format | Arguments | Selector Source |
|---------------|-------|---------------|-----------|-----------------|
| `sendLiteralSelector0Args` | 192-207 | 11000000-11001111 | 0 | Literal frame |
| `sendLiteralSelector1Arg` | 208-223 | 11010000-11011111 | 1 | Literal frame |  
| `sendLiteralSelector2Args` | 224-239 | 11100000-11101111 | 2 | Literal frame |
| `sendLiteralSelectorNArgs` | 240-255 | 11110000-11111111 | N | Literal frame |

**Encoding:**
- **Bits 6-7**: Argument count (00=0, 01=1, 10=2, 11=N args)
- **Bits 2-5**: Literal frame index (0-15)
- **Bits 0-1**: Unused/reserved

**Examples:**
- `192` (11000000): Send selector at literal[0] with 0 arguments
- `208` (11010000): Send selector at literal[0] with 1 argument
- `209` (11010001): Send selector at literal[1] with 1 argument
- `224` (11100000): Send selector at literal[0] with 2 arguments

## Important Notes

1. **Store vs Pop**: The Blue Book store operations (96-175) do NOT pop the stack. They leave the value on the stack after storing.

2. **No Special Push Constants**: Unlike many modern implementations, the Blue Book does NOT have dedicated bytecodes for pushing special constants like self, true, false, nil, -1, 0, 1, 2. These must be handled differently:
   - `self` is pushed using instance variable access or other means
   - `true`, `false`, `nil` are returned directly via return bytecodes or stored as literals
   - Small integers are stored as literal constants

3. **Dense Encoding**: The Blue Book uses all 256 bytecode values with no gaps (except 181-191 which are explicitly marked as unused).

4. **Send Operations**: The send operations (192-255) encode both the argument count and selector index directly in the bytecode value. There are no separate "super send" bytecodes - super sends are handled through extended bytecodes or compiler-generated code.

5. **Literal Frame Limitation**: Send bytecodes can only reference selectors in the first 16 positions (0-15) of the literal frame due to 4-bit encoding.

## Migration from Non-Canonical Implementations

Many modern Smalltalk implementations have diverged from this specification:
- Squeak/Pharo use different encodings with special push bytecodes
- Some implementations use "pop and store" operations instead of separate store operations
- Extended bytecode sets go beyond 256 values
- Some add dedicated "super send" bytecode ranges

To implement a Blue Book compatible interpreter:
1. Use this exact bytecode mapping
2. Implement store operations that don't pop
3. Handle special constants through literals or return bytecodes
4. Ensure send operations correctly decode argument count and selector index from the bytecode value
5. Limit literal frame selector references to indices 0-15 for send bytecodes