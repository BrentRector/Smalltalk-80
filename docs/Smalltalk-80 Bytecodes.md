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
| 152–159   | 10011iii      | Pop and Short Jump On False iii+1 (1 through 8)                         |
| 160–167   | 10100iii      | Long Jump Forward (iii+4)*256 + next byte                                |
| 168–171   | 1010100i      | Long Jump On True ii*256 + next byte                                    |
| 172–175   | 1010110i      | Long Jump On False ii*256 + next byte                                   |
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