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

| Range | Operation | Description |
|-------|-----------|-------------|
| 192-223 | `sendMessage: n` | Send message with selector at literal index n |
| 224-255 | `sendSuperMessage: n` | Send super message with selector at literal index n |

## Important Notes

1. **Store vs Pop**: The Blue Book store operations (96-175) do NOT pop the stack. They leave the value on the stack after storing.

2. **No Special Push Constants**: Unlike many modern implementations, the Blue Book does NOT have dedicated bytecodes for pushing special constants like self, true, false, nil, -1, 0, 1, 2. These must be handled differently:
   - `self` is pushed using instance variable access or other means
   - `true`, `false`, `nil` are returned directly via return bytecodes or stored as literals
   - Small integers are stored as literal constants

3. **Dense Encoding**: The Blue Book uses all 256 bytecode values with no gaps (except 181-191 which are explicitly marked as unused).

4. **Send Operations**: The send operations (192-255) reference selectors stored in the method's literal frame. The bytecode value minus the base (192 or 224) gives the literal index.

## Migration from Non-Canonical Implementations

Many modern Smalltalk implementations have diverged from this specification:
- Squeak/Pharo use different encodings with special push bytecodes
- Some implementations use "pop and store" operations instead of separate store operations
- Extended bytecode sets go beyond 256 values

To implement a Blue Book compatible interpreter:
1. Use this exact bytecode mapping
2. Implement store operations that don't pop
3. Handle special constants through literals or return bytecodes
4. Ensure send operations correctly index into the literal frame