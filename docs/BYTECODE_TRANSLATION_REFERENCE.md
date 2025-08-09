# Smalltalk-80 Bytecode Translation Reference

## Overview

This document provides a complete reference for translating Smalltalk-80 method AST nodes to Blue Book bytecode. The process is deterministic, auditable, and aligns with the canonical Smalltalk-80 bytecode model.

## Implementation Status

**Current Implementation Files:**
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Main compiler implementation
- `Smalltalk80/ByteCodes/BytecodeTable.cs` - VM bytecode operation definitions

**Key Classes:**
- `BytecodeCompiler` - Main compiler class with full AST traversal
- `BytecodeEmitter` - Bytecode generation and literal management
- `CompilationEnvironment` - Variable scope and index management
- `CompiledMethodData` - Output data structure

## Table of Contents

1. [Input/Output Specification](#1-inputoutput-specification)
2. [Environment and Symbol Tables](#2-environment-and-symbol-tables)
3. [Literal Frame Construction](#3-literal-frame-construction)
4. [Bytecode Instruction Set](#4-bytecode-instruction-set)
5. [Code Generation Process](#5-code-generation-process)
6. [Variable Handling](#6-variable-handling)
7. [Message Sends](#7-message-sends)
8. [Cascades](#8-cascades)
9. [Blocks](#9-blocks)
10. [Opcode Mapping](#10-opcode-mapping)
11. [Examples](#11-examples)

## 1. Input/Output Specification

### Input: methodDef Parse Node

```
methodDef
  ├── methodHeader
  │   ├── unarySelector | binarySelector + formalArg | keywords + formalArgs
  ├── stringConst?         // optional selector comment
  ├── NEW_LINE?           // optional
  ├── temporaries?        // | t1 t2 ... tn |
  ├── primitiveDecl?      // <primitive: N>
  └── methodBody          // statements, ^expr, or empty
```

### Output: CompiledMethod

```json
{
  "header": {
    "selector": "at:put:",
    "numArgs": 2,
    "numTemps": 3,        // includes hidden temps
    "numLits": 5,
    "primitive": null,    // or primitive number
    "flags": 0
  },
  "literals": ["+", "Array", "Transcript", 42, #symbol],
  "bytecodes": [0x10, 0x11, 0xB0, 0x20, 0x7C]
}
```

**Actual Implementation (CompiledMethodData class):**
```csharp
public class CompiledMethodData
{
    public string Selector { get; set; }
    public int ArgumentCount { get; set; }
    public int TemporaryCount { get; set; }  // Includes hidden temps
    public bool IsPrimitive { get; set; }
    public int PrimitiveNumber { get; set; }
    public object[] Literals { get; set; }   // Symbol, string, int, etc.
    public byte[] Bytecode { get; set; }     // Raw bytecode array
}
```

### Intermediate Representation (IR)

```json
{
  "selector": "at:put:",
  "numArgs": 2,
  "numTemps": 1,
  "primitive": null,
  "literals": ["+", "at:put:"],
  "bytecodes": [
    {"ip":0, "op":"PUSH_TEMP", "arg":0, "comment":"i"},
    {"ip":1, "op":"PUSH_TEMP", "arg":1, "comment":"v"},
    {"ip":2, "op":"SEND_BINARY", "arg":{"lit":0}, "comment":"+ operator"},
    {"ip":5, "op":"RETURN_TOP_FROM_METHOD"}
  ]
}
```

## 2. Environment and Symbol Tables

### Selector Resolution

| Pattern | Arity | Selector |
|---------|-------|----------|
| `unarySelector` | 0 | IDENTIFIER |
| `binarySelector formalArg` | 1 | binary symbol |
| `(identifierColon formalArg)+` | count | concatenated keywords |

### Variable Indexing

```json
{
  "scopeDepth": 0,
  "args": {
    "index": 0,
    "value": 1
  },
  "temps": {
    "t": 2,
    "result": 3
  },
  "hiddenTemps": {
    "cascade1": 4,
    "scratch": 5
  },
  "instVars": {
    "x": 0,
    "y": 1
  }
}
```

**Index Allocation:**
- Arguments: `[0..numArgs-1]`
- Temporaries: `[numArgs..numArgs+numTemps-1]`
- Hidden temps: counted in numTemps
- Instance variables: separate index space

**Actual Implementation (CompilationEnvironment class):**
```csharp
public class CompilationEnvironment
{
    public int ScopeDepth { get; set; }
    public Dictionary<string, int> Arguments { get; }
    public Dictionary<string, int> Temporaries { get; }
    public Dictionary<string, int> InstanceVariables { get; }
    public Dictionary<string, int> HiddenTemporaries { get; }
    
    public int AllocateHiddenTemp(string name)
    {
        // Allocates at ArgumentCount + TemporaryCount
        // Increments TemporaryCount
    }
}
```

## 3. Literal Frame Construction

### Literal Types

1. **Selector Symbols**: All message selectors used in sends
2. **Constants**: Numbers, strings, symbols, characters
3. **Arrays**: Literal arrays `#(1 2 3)`
4. **Byte Arrays**: `#[1 2 3]`
5. **Associations**: Global/class variables (stored as symbols, resolved at load time)

### Collection Process

```
1. Walk AST in first pass
2. Collect literals in first-use order
3. Deduplicate by value:
   - Symbols: by name
   - Numbers: by numeric value
   - Strings: by contents
   - Associations: by variable name
4. Build index map
```

**Actual Implementation:**
- Uses `LiteralCollector` visitor class (inner class of BytecodeCompiler)
- `BytecodeEmitter.AddLiteral()` handles deduplication via `LiteralsEqual()` method
- Symbol class provides value equality semantics
- NilObject class represents nil in literal frame

### Example Literal Frame

```json
{
  "literals": [
    "+",                    // 0: arithmetic selector
    "at:put:",             // 1: keyword selector
    "Transcript",          // 2: global variable
    42,                    // 3: integer constant
    "hello",               // 4: string constant
    #computeValue          // 5: symbol constant
  ],
  "litIndex": {
    "+": 0,
    "at:put:": 1,
    "Transcript": 2,
    "42": 3,
    "hello": 4,
    "computeValue": 5
  }
}
```

## 4. Bytecode Instruction Set

**Implementation Note:** The `BytecodeEmitter` class provides methods for each operation type. The actual bytecode values are generated according to the Blue Book specification.

### Stack Operations

**BytecodeEmitter Methods:**
- `PushReceiverVariable(int index)` - Handles extended push for index > 15
- `PushTemporary(int index)` - Handles extended push for index > 15  
- `PushLiteral(object literal)` - Adds to literal frame, handles index > 31
- `PushLiteralVariable(string name)` - For globals, creates Symbol in literals
- `PushSelf()` - Emits bytecode 112
- `PushNil/True/False()` - Push via literal frame (not special bytecodes)
- `Dup()` - Bytecode 136
- `Pop()` - Bytecode 135

| Mnemonic | Description | Stack Effect |
|----------|-------------|--------------|
| `PUSH_RECEIVER_VAR i` | Push instance variable i | → value |
| `PUSH_TEMP i` | Push temporary/argument i | → value |
| `PUSH_LITERAL i` | Push literal constant i | → value |
| `PUSH_LITERAL_VAR i` | Push value of Association i | → value |
| `PUSH_SELF` | Push receiver | → self |
| `PUSH_TRUE` | Push true | → true |
| `PUSH_FALSE` | Push false | → false |
| `PUSH_NIL` | Push nil | → nil |
| `DUP` | Duplicate top | value → value, value |
| `POP` | Pop stack | value → |

### Store Operations

**BytecodeEmitter Methods:**
- `StoreReceiverVariable(int index)` - Uses 96-103 for index 0-7, extended otherwise
- `StoreTemporary(int index)` - Uses 104-111 for index 0-7, extended otherwise
- `StoreLiteralVariable(string name)` - Always uses extended store (129)
- Note: All store methods emit POP variants, caller must DUP if value needed

| Mnemonic | Description | Stack Effect |
|----------|-------------|--------------|
| `STORE_RECEIVER_VAR i` | Store in instance var i | value → value |
| `STORE_RECEIVER_VAR_POP i` | Store and pop | value → |
| `STORE_TEMP i` | Store in temp i | value → value |
| `STORE_TEMP_POP i` | Store and pop | value → |
| `STORE_LITERAL_VAR i` | Store in Association i | value → value |
| `STORE_LITERAL_VAR_POP i` | Store and pop | value → |

### Send Operations

**BytecodeEmitter Method: `SendMessage(string selector, int argCount)`**
- Checks special arithmetic selectors first (bytecodes 176-191)
- Then common selectors (bytecodes 192-207)
- Falls back to literal frame:
  - 208-223: 0 args, selector index 0-15
  - 224-239: 1 arg, selector index 0-15
  - 240-255: 2 args, selector index 0-15
  - Extended sends (131/132) for index > 15 or args > 2

| Mnemonic | Description | Stack Effect |
|----------|-------------|--------------|
| `SEND_UNARY litIndex` | Send unary message | receiver → result |
| `SEND_BINARY litIndex` | Send binary message | receiver, arg → result |
| `SEND_KEYWORD litIndex argc` | Send keyword message | receiver, args... → result |
| `SEND_SUPER_*` | Super send variants | (same as above) |

### Control Flow

| Mnemonic | Description | Stack Effect |
|----------|-------------|--------------|
| `JUMP offset` | Unconditional jump | |
| `JUMP_IF_FALSE offset` | Jump if false | bool → |
| `JUMP_IF_TRUE offset` | Jump if true | bool → |
| `BLOCK_COPY argc size` | Create block | → block |
| `RETURN_TOP_FROM_METHOD` | Return top | value → (return) |
| `RETURN_SELF_FROM_METHOD` | Return self | → (return) |
| `RETURN_TOP_FROM_BLOCK` | Non-local return | value → (return) |

## 5. Code Generation Process

### Overall Algorithm

```pseudocode
compileMethod(methodDef):
  env = buildEnvironment(methodHeader, temporaries)
  primitive = parsePrimitive(methodDef.primitiveDecl)
  literals = collectLiterals(methodDef.methodBody)
  emitter = new Emitter(literals)
  
  compileMethodBody(methodDef.methodBody, env, emitter)
  
  if !hasExplicitReturn:
    emitter.emit(RETURN_TOP_FROM_METHOD)
  
  return CompiledMethod(header, literals, emitter.bytes())
```

**Actual Implementation (BytecodeCompiler.CompileMethod):**
```csharp
public CompiledMethodData CompileMethod(
    string methodSource,
    List<string> instanceVars,
    Dictionary<string, object>? classVars = null,
    Dictionary<string, object>? poolVars = null)
{
    // 1. Parse with ANTLR
    var parser = new Smalltalk80Parser(tokenStream);
    var methodDef = parser.methodDef();
    
    // 2. Process header and temps
    ProcessMethodHeader(methodDef.methodHeader());
    ProcessTemporaries(methodDef.temporaries());
    
    // 3. Two-pass compilation
    CollectLiterals(methodDef.methodBody());  // First pass
    CompileMethodBody(methodDef.methodBody()); // Second pass
    
    // 4. Ensure return
    EnsureMethodReturn();
}
```

### Statement Compilation

```pseudocode
compileStatement(stmt, isFinal):
  if stmt is returnStatement:
    compileExpression(stmt.expr)
    emit(RETURN_TOP_FROM_METHOD or RETURN_TOP_FROM_BLOCK)
  else:
    compileExpression(stmt.expr)
    if !isFinal:
      spillResult()  // STORE_TEMP_POP scratchTemp
```

### Expression Compilation

```pseudocode
compileExpression(expr):
  switch expr.type:
    case assignment:
      compileExpression(expr.rhs)
      storeVariable(expr.lhs, leaveOnStack=true)
    
    case cascade:
      compileCascade(expr)
    
    case message:
      compileMessageExpr(expr)
    
    case variable:
      pushVariable(expr.name)
    
    case literal:
      pushLiteral(expr.value)
    
    case block:
      compileBlock(expr)
```

## 6. Variable Handling

### Variable Resolution Order

1. **Arguments**: Index `[0..numArgs-1]`
2. **Temporaries**: Index `[numArgs..numArgs+numTemps-1]`
3. **Instance Variables**: Separate index via class layout
4. **Global/Class/Pool Variables**: Via literal frame as Associations

### Push Variable

```pseudocode
pushVariable(name):
  if name == "self": PUSH_SELF
  else if name == "super": PUSH_SELF  // super uses self as receiver
  else if name == "true": PUSH_TRUE
  else if name == "false": PUSH_FALSE
  else if name == "nil": PUSH_NIL
  else if isArgument(name): PUSH_TEMP argIndex
  else if isTemporary(name): PUSH_TEMP tempIndex
  else if isInstanceVar(name): PUSH_RECEIVER_VAR instIndex
  else: PUSH_LITERAL_VAR litIndex  // global
```

**Actual Implementation Notes:**
- `PushVariable()` method in BytecodeCompiler handles all cases
- Special literals (true/false/nil) are pushed via literal frame, not special bytecodes
- `BytecodeEmitter.PushLiteral()` handles literal indices > 31 with extended bytecodes

### Store Variable (with value left on stack)

```pseudocode
storeVariable(name, leaveOnStack):
  if leaveOnStack && !hasNonPopStore:
    DUP
    STORE_*_POP varIndex
  else:
    STORE_* varIndex  // leaves value on stack
```

**Actual Implementation:**
- `StoreVariable()` uses DUP when `leaveOnStack=true`
- Blue Book only has STORE_POP variants for indices 0-7
- Extended store (129) used for higher indices, followed by explicit POP

## 7. Message Sends

### Precedence Rules

1. **Unary** (highest): `receiver unaryMessage`
2. **Binary**: `receiver binaryOp argument`
3. **Keyword** (lowest): `receiver keyword: arg1 keyword: arg2`

### Compilation Algorithm

```pseudocode
compileMessageExpr(expr):
  compileMessageBase(expr.receiver)
  
  // Apply unary messages
  for each unaryMsg in expr.unaryMessages:
    SEND_UNARY litIndex(unaryMsg.selector)
  
  // Apply binary messages (left-to-right)
  for each binaryMsg in expr.binaryMessages:
    compileMessageBase(binaryMsg.argument)
    SEND_BINARY litIndex(binaryMsg.selector)
  
  // Apply keyword message
  if expr.keywordMessage:
    for each keywordPart in expr.keywordMessage.parts:
      compileNonKeywordExpr(keywordPart.argument)
    SEND_KEYWORD litIndex(selector) argc
```

### Special Selectors

Certain selectors have optimized bytecodes:

**Arithmetic**: `+ - < > <= >= = ~= * / \\ @ bitShift: // bitAnd: bitOr:`

**Common**: `at: at:put: size next nextPut: atEnd == class blockCopy: value value: do: new new: x y`

**Implementation:** `BytecodeEmitter.SendMessage()` checks dictionaries for special/common selectors:
- Arithmetic selectors → bytecodes 176-191
- Common selectors → bytecodes 192-207
- Others use literal frame with bytecodes 208-255

## 8. Cascades

### Cascade Structure

```smalltalk
Transcript
  show: 'Hello';     "first message"
  cr;                "cascaded message"
  show: 'World'      "last cascaded message"
```

### Compilation Strategy

```pseudocode
compileCascade(expr):
  // Compile receiver and cache
  compileMessageBase(expr.receiver)
  cascadeTemp = allocateHiddenTemp()
  DUP
  STORE_TEMP_POP cascadeTemp
  
  // First message (already has receiver on stack)
  compileFirstMessage(expr.firstMessage)
  
  // Cascaded messages
  for each msg in expr.cascadedMessages[0..-2]:
    spillResult()  // discard previous result
    PUSH_TEMP cascadeTemp
    compileMessage(msg)
  
  // Last cascaded message (keep result)
  spillResult()
  PUSH_TEMP cascadeTemp
  compileMessage(expr.cascadedMessages.last)
```

**Actual Implementation (CompileCascadeExpr):**
- Allocates hidden temp via `environment.AllocateHiddenTemp()`
- Uses `nextCascadeTemp` counter for unique names
- `SpillResult()` stores to scratch temp to discard intermediate values
- Handles case where no cascade parts exist (just regular message)

## 9. Blocks

### Block Structure

```smalltalk
[:x :y | | temp | temp := x + y. ^temp]
```

### Compilation Process

```pseudocode
compileBlock(block):
  savedScope = env.scopeDepth
  env.scopeDepth++
  
  argCount = block.arguments.count
  BLOCK_COPY argCount sizePlaceholder
  blockStartIP = currentIP
  
  // Compile block body
  if block.statements:
    compileStatements(block.statements)
  else:
    PUSH_NIL
  
  RETURN_TOP_FROM_BLOCK
  
  // Patch block size
  blockSize = currentIP - blockStartIP
  patchBlockSize(blockCopyIP, blockSize)
  
  env.scopeDepth = savedScope
```

**Actual Implementation:**
- `CompileBlock()` uses bytecode 137 (Push Active Context) as placeholder
- Block size stored as 2 bytes after the BLOCK_COPY bytecode
- Block arguments NOT currently handled (simplified implementation)
- Always emits RETURN_TOP_FROM_BLOCK at end

### Variable Access in Blocks

- **Block arguments/temps**: Local to block context
- **Method temps/args**: Via captured home context
- **Instance variables**: Via captured receiver

## 10. Opcode Mapping

### Canonical Blue Book Encoding

**Implementation:** See `Smalltalk80/ByteCodes/BytecodeTable.cs` for actual VM operations.

| Range | Bytecode | Description |
|-------|----------|-------------|
| 0x00-0x0F | 0-15 | Push Receiver Variable 0-15 |
| 0x10-0x1F | 16-31 | Push Temporary Location 0-15 |
| 0x20-0x3F | 32-63 | Push Literal Constant 0-31 |
| 0x40-0x5F | 64-95 | Push Literal Variable 0-31 |
| 0x60-0x67 | 96-103 | Pop and Store Receiver Variable 0-7 |
| 0x68-0x6F | 104-111 | Pop and Store Temporary Location 0-7 |
| 0x70 | 112 | Push Receiver (self) |
| 0x71-0x77 | 113-119 | (unused in canonical - marked as NoOp) |
| 0x78 | 120 | Return Receiver From Message |
| 0x79-0x7B | 121-123 | (unused in canonical - marked as NoOp) |
| 0x7C | 124 | Return Top From Message |
| 0x7D | 125 | Return Top From Block |
| 0x7E-0x7F | 126-127 | (unused) |
| 0x80 | 128 | Extended Push |
| 0x81 | 129 | Extended Store |
| 0x82 | 130 | Extended Store and Pop |
| 0x83 | 131 | Single Extended Send |
| 0x84 | 132 | Double Extended Send |
| 0x85 | 133 | Single Extended Super |
| 0x86 | 134 | Double Extended Super |
| 0x87 | 135 | Pop Stack Top |
| 0x88 | 136 | Duplicate Stack Top |
| 0x89 | 137 | Push Active Context |
| 0x90-0x97 | 144-151 | Jump 1-8 |
| 0x98-0x9F | 152-159 | Pop Jump On False 1-8 |
| 0xA0-0xA7 | 160-167 | Long Jump (0-7)*256 + next |
| 0xA8-0xAB | 168-171 | Pop Jump On True (0-3)*256 + next |
| 0xAC-0xAF | 172-175 | Pop Jump On False (0-3)*256 + next |
| 0xB0-0xBF | 176-191 | Send Arithmetic Selector |
| 0xC0-0xCF | 192-207 | Send Common Selector |
| 0xD0-0xDF | 208-223 | Send Literal Selector 0-15 With 0 Args |
| 0xE0-0xEF | 224-239 | Send Literal Selector 0-15 With 1 Arg |
| 0xF0-0xFF | 240-255 | Send Literal Selector 0-15 With 2 Args |

### Extended Bytecodes

**Extended Push (128)**
```
byte1: 128
byte2: vvvvviii
  - iii: variable type (0=receiver, 1=temp, 2=literal, 3=literal var)
  - vvvvv: variable index
```

**Single Extended Send (131)**
```
byte1: 131
byte2: aaaiiiiii
  - aaaaa: argument count
  - iiiii: selector literal index
```

## 11. Examples

### Example 1: Simple Method

**Source:**
```smalltalk
at: i put: v
  | t |
  t := i + v.
  ^t
```

**Actual Compilation Process:**
1. `ProcessMethodHeader()` sets selector="at:put:", args={i:0, v:1}
2. `ProcessTemporaries()` sets temps={t:2}
3. `CollectLiterals()` adds "+" to literal frame at index 0
4. `CompileAssignmentExpr()` compiles "i + v", then DUPs and stores to t
5. `CompileExpression()` pushes t
6. `CompileReturnStatement()` emits RETURN_TOP_FROM_METHOD

**Bytecode Generation:**
```json
{
  "selector": "at:put:",
  "numArgs": 2,
  "numTemps": 1,
  "literals": ["+"],
  "bytecodes": [
    {"op":"PUSH_TEMP", "arg":0, "comment":"i"},
    {"op":"PUSH_TEMP", "arg":1, "comment":"v"},
    {"op":"SEND_BINARY", "arg":0, "comment":"send +"},
    {"op":"STORE_TEMP", "arg":2, "comment":"t :="},
    {"op":"PUSH_TEMP", "arg":2, "comment":"push t"},
    {"op":"RETURN_TOP_FROM_METHOD"}
  ]
}
```

**Final Bytecodes:**
```
0x10  // Push temp 0 (i) - PUSH_TEMP
0x11  // Push temp 1 (v) - PUSH_TEMP
0xB0  // Send arithmetic + (special selector index 0)
0x88  // DUP (for assignment that leaves value)
0x6A  // Pop store temp 2 (t) - STORE_TEMP_POP
0x12  // Push temp 2 (t) - PUSH_TEMP
0x7C  // Return top - RETURN_TOP_FROM_METHOD
```

**Note:** The actual implementation uses DUP to handle assignment value on stack.

### Example 2: Cascade

**Source:**
```smalltalk
displayOn: aStream
  aStream
    nextPutAll: 'Value: ';
    print: value;
    cr
```

**Actual Compilation with BytecodeCompiler:**
1. `CompileCascadeExpr()` detects cascade (SEMICOLON tokens)
2. Allocates hidden temp "cascade0" at index 1
3. Compiles receiver (aStream), DUPs, stores to cascade0
4. For each cascade part: pushes cascade0, compiles message
5. Uses `SpillResult()` to discard intermediate results

**Bytecode Generation:**
```json
{
  "literals": ["nextPutAll:", "print:", "cr", "Value: "],
  "hiddenTemps": {"cascade1": 1},
  "bytecodes": [
    {"op":"PUSH_TEMP", "arg":0, "comment":"aStream"},
    {"op":"DUP"},
    {"op":"STORE_TEMP_POP", "arg":1, "comment":"cache receiver"},
    {"op":"PUSH_LITERAL", "arg":3, "comment":"'Value: '"},
    {"op":"SEND_KEYWORD", "arg":0, "argc":1, "comment":"nextPutAll:"},
    {"op":"POP", "comment":"discard result"},
    {"op":"PUSH_TEMP", "arg":1, "comment":"cached receiver"},
    {"op":"PUSH_RECEIVER_VAR", "arg":0, "comment":"value"},
    {"op":"SEND_KEYWORD", "arg":1, "argc":1, "comment":"print:"},
    {"op":"POP", "comment":"discard result"},
    {"op":"PUSH_TEMP", "arg":1, "comment":"cached receiver"},
    {"op":"SEND_UNARY", "arg":2, "comment":"cr"},
    {"op":"RETURN_TOP_FROM_METHOD"}
  ]
}
```

### Example 3: Block

**Source:**
```smalltalk
collect: aBlock
  | result |
  result := OrderedCollection new.
  self do: [:each | result add: (aBlock value: each)].
  ^result
```

**Block Bytecodes:**
```json
{
  "blockStart": {"op":"BLOCK_COPY", "argc":1, "size":12},
  "blockBody": [
    {"op":"PUSH_TEMP", "arg":1, "comment":"result (from method)"},
    {"op":"PUSH_TEMP", "arg":2, "comment":"aBlock (from method)"},
    {"op":"PUSH_TEMP", "arg":0, "comment":"each (block arg)"},
    {"op":"SEND_KEYWORD", "arg":"value:", "argc":1},
    {"op":"SEND_KEYWORD", "arg":"add:", "argc":1},
    {"op":"RETURN_TOP_FROM_BLOCK"}
  ]
}
```

## Validation Checklist

### Pre-Compilation
- [ ] Method header parsed correctly
- [ ] Arguments indexed 0..numArgs-1
- [ ] Temporaries indexed numArgs..numArgs+numTemps-1
- [ ] Instance variables mapped to class layout
- [ ] Primitive number extracted if present

### Literal Frame
- [ ] All selectors collected
- [ ] All constants collected
- [ ] No duplicate literals
- [ ] Associations for globals created
- [ ] Literal indices sequential from 0

### Bytecode Generation
- [ ] Stack depth never negative
- [ ] All variables resolved
- [ ] Hidden temps counted in numTemps
- [ ] Cascade temps allocated
- [ ] Scratch temp allocated if needed

### Post-Compilation
- [ ] All paths end with return
- [ ] Block sizes patched correctly
- [ ] Jump offsets calculated correctly
- [ ] Bytecode array complete
- [ ] Header fields accurate

## Notes

1. **True/False/Nil**: In canonical Blue Book, these are pushed via the literal frame, not special bytecodes
2. **Assignment Value**: Assignments must leave their value on the stack for chained assignments
3. **Cascade Optimization**: DUP can be used to avoid extra temp if available
4. **Block Variables**: Blocks capture their home context at creation time
5. **Super Sends**: Use self as receiver but different method lookup

## Implementation-Specific Notes

### Current Limitations
- Block arguments and temporaries not fully implemented
- Remote temp access for blocks not implemented
- Array and byte array literals stored as text (need proper parsing)
- Class/pool variables treated as globals

### Key Features
- Two-pass compilation (literal collection then code generation)
- Proper hidden temporary allocation
- Correct cascade handling with receiver caching
- Full AST traversal with visitor pattern
- Comprehensive special selector optimization

### Testing Considerations
- Bytecode output validated against Blue Book specification
- Literal frame deduplication verified
- Hidden temps properly counted in method header
- All code paths end with appropriate return bytecode

## References

- Goldberg & Robson: "Smalltalk-80: The Language and its Implementation" (Blue Book)
- ANSI Smalltalk Standard
- Original Xerox Smalltalk-80 Virtual Image Format Specification

## Implementation Summary

### Key Components Interaction

```
methodSource → Smalltalk80Parser → methodDef AST
                                        ↓
                    BytecodeCompiler.CompileMethod()
                           ↓                ↓
                    CollectLiterals    CompileMethodBody
                    (LiteralCollector)  (recursive descent)
                           ↓                ↓
                    BytecodeEmitter ← compilation methods
                           ↓
                    CompiledMethodData
                    {Selector, Args, Temps, Literals[], Bytecode[]}
```

### Critical Implementation Details

1. **Two-Pass Compilation**: First pass collects all literals via visitor pattern, second pass generates bytecode
2. **Hidden Temporaries**: Cascade and scratch temps allocated via `AllocateHiddenTemp()`, counted in method header
3. **Literal Management**: `BytecodeEmitter.AddLiteral()` deduplicates using value equality
4. **Variable Resolution**: Strict order - args → temps → instance vars → globals
5. **Assignment Handling**: Uses DUP to leave value on stack after store
6. **Extended Bytecodes**: Automatic for indices > standard range
7. **Special Selector Optimization**: Dictionaries in `SendMessage()` for arithmetic/common selectors

### Usage Example

```csharp
var compiler = new BytecodeCompiler();
var methodData = compiler.CompileMethod(
    methodSource: "at: index put: value | temp | temp := index + value. ^temp",
    instanceVars: new List<string> { "x", "y", "z" }
);

// Result contains:
// - Selector: "at:put:"
// - ArgumentCount: 2
// - TemporaryCount: 1
// - Literals: [Symbol("+")]
// - Bytecode: [0x10, 0x11, 0xB0, 0x88, 0x6A, 0x12, 0x7C]
```