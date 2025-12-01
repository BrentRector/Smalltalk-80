# Smalltalk-80 Compiler Bytecodes Documentation

## Summary Table

| Range | Category | Description |
|-------|----------|-------------|
| 0-15 | Push Instance Variable | Push receiver's instance variable 0-15 |
| 16-31 | Push Temporary Variable | Push temporary variable 0-15 |
| 32-63 | Push Literal Constant | Push literal constant 0-31 |
| 64-95 | Push Literal Variable | Push literal variable 0-31 |
| 96-103 | Store and Pop Instance Variable | Pop stack, store into instance variable 0-7 |
| 104-111 | Store and Pop Temporary Variable | Pop stack, store into temp variable 0-7 |
| 112-119 | Push Special Constants | Push receiver, true, false, nil, -1, 0, 1, 2 |
| 120-123 | Return Special Constants | Return receiver, true, false, nil |
| 124 | Return Top of Stack | Return top of stack from method |
| 125 | Return Top from Block | Return top of stack from block to caller |
| 128-130 | Extended Push/Store | Extended bytecodes for variables |
| 131 | Send Selector (Long) | Long form of send |
| 133 | Push Super | Push current receiver as super |
| 135 | Pop Stack | Pop and discard top of stack |
| 136 | Duplicate Top | Duplicate top of stack |
| 137 | Push Active Context | Push thisContext |
| 144-151 | Short Jump | Jump forward 1-8 bytes |
| 152-159 | Short Jump on False | Pop and jump forward 1-8 if false |
| 160-167 | Long Jump | Long unconditional jump |
| 168-175 | Long Jump on True/False | Long conditional jumps |
| 176-207 | Send Arithmetic/Special | Send +, -, <, >, <=, >=, =, ~=, *, /, \\, @, bitShift:, //, bitAnd:, bitOr: |
| 208-223 | Send Common Selector | Send common selectors with 0 args |
| 224-239 | Send Common Selector | Send common selectors with 1 arg |
| 240-255 | Send Common Selector | Send common selectors with 2 args |

## Detailed Bytecode Documentation

### Push Bytecodes

#### **0-15: Push Receiver Variable**
- **Mnemonic**: pushRcvr:0 to pushRcvr:15
- **Semantics**: Push the value of the receiver's instance variable at the given index onto the stack
- **Encoding**: Direct bytecode 0-15
- **Usage**: Accessing instance variables of self
- **Code Generation**: `VariableNode>>emitForValue:` when code is 0-15

#### **16-31: Push Temporary Variable**  
- **Mnemonic**: pushTemp:0 to pushTemp:15
- **Semantics**: Push the value of the temporary variable at the given index onto the stack
- **Encoding**: Direct bytecode 16-31
- **Usage**: Accessing method arguments and temporary variables
- **Code Generation**: `VariableNode>>emitForValue:` when code is 16-31

#### **32-63: Push Literal Constant**
- **Mnemonic**: pushConstant:0 to pushConstant:31
- **Semantics**: Push literal constant at the given index in the method's literal frame
- **Encoding**: Direct bytecode 32-63
- **Usage**: Pushing literal objects (numbers, strings, symbols, etc.)
- **Code Generation**: `LiteralNode>>emitForValue:` when code is 32-63

#### **64-95: Push Literal Variable**
- **Mnemonic**: pushLit:0 to pushLit:31
- **Semantics**: Push the value of literal variable (global, class var, pool var) at given index
- **Encoding**: Direct bytecode 64-95
- **Usage**: Accessing global variables, class variables, pool variables
- **Code Generation**: `VariableNode>>emitForValue:` when code is 64-95

#### **112-119: Push Special Constants**
- **112 (LdSelf)**: Push receiver (self)
- **113 (LdTrue)**: Push true
- **114 (LdFalse)**: Push false
- **115 (LdNil)**: Push nil
- **116 (LdMinus1)**: Push -1
- **117**: Push 0
- **118**: Push 1
- **119**: Push 2
- **Code Generation**: `VariableNode>>emitForValue:` for special variables; `LiteralNode>>emitForValue:` for special literals

### Store Bytecodes

#### **96-103: Store and Pop Instance Variable**
- **Mnemonic**: storeAndPopRcvr:0 to storeAndPopRcvr:7
- **Semantics**: Pop top of stack and store into receiver's instance variable 0-7
- **Encoding**: Direct bytecode 96-103 (ShortStoP + index)
- **Usage**: Assignment to instance variables
- **Code Generation**: `VariableNode>>emitStorePop:` when code is 0-7

#### **104-111: Store and Pop Temporary Variable**
- **Mnemonic**: storeAndPopTemp:0 to storeAndPopTemp:7
- **Semantics**: Pop top of stack and store into temporary variable 0-7
- **Encoding**: Direct bytecode 104-111 (ShortStoP + 8 + index)
- **Usage**: Assignment to temporary variables
- **Code Generation**: `VariableNode>>emitStorePop:` when code is 16-23

### Extended Variable Access

#### **128 (LdInstLong): Extended Push Instance Variable**
- **Format**: [128] [type:index]
- **Semantics**: Push variable using extended indexing
- **Operands**: 
  - type (high 2 bits): 0=instance, 1=temp, 2=literal const, 3=literal var
  - index (low 6 bits): variable index
- **Code Generation**: `VariableNode>>emitForValue:` for variables with index >= 16

#### **129 (Store): Extended Store**
- **Format**: [129] [type:index]
- **Semantics**: Store top of stack into variable (don't pop)
- **Operands**: Same as LdInstLong
- **Code Generation**: `VariableNode>>emitStore:`

#### **130 (StorePop): Extended Store and Pop**
- **Format**: [130] [type:index]
- **Semantics**: Pop stack and store into variable
- **Operands**: Same as LdInstLong
- **Code Generation**: `VariableNode>>emitStorePop:` for variables with index >= 8

### Control Flow Bytecodes

#### **144-151 (Jmp): Short Unconditional Jump**
- **Format**: Single byte 144-151
- **Semantics**: Jump forward by 1-8 bytes
- **Offset**: bytecode - 143 (i.e., 144 jumps 1 byte, 151 jumps 8 bytes)
- **Code Generation**: `ParseNode>>emitJump:` for short distances

#### **152-159 (Bfp): Short Jump on False**
- **Format**: Single byte 152-159
- **Semantics**: Pop stack, if false jump forward by 1-8 bytes
- **Offset**: bytecode - 151
- **Usage**: Implementing ifTrue:, ifFalse:, whileTrue:, etc.
- **Code Generation**: `ParseNode>>emitBranch:pop:on:` for short distances

#### **160-167 (JmpLong): Long Unconditional Jump**
- **Format**: [160-167] [low-byte]
- **Semantics**: Jump by extended distance
- **Distance Calculation**:
  - For 160-163: Jump forward (bytecode-160)*256 + low-byte
  - For 164-167: Jump backward 1024 - ((bytecode-164)*256 + low-byte)
- **Code Generation**: `ParseNode>>emitLongJump:` or `ParseNode>>emitJump:` for long distances

#### **168-175: Long Jump on True**
- **Format**: [168-175] [low-byte]
- **Semantics**: Pop stack, if true jump by distance
- **Distance**: Calculated same as JmpLong
- **Usage**: Implementing or:, ifFalse:
- **Code Generation**: `ParseNode>>emitTrueBranch:pop:on:`

### Stack Operations

#### **135 (Pop): Pop Stack**
- **Semantics**: Pop and discard top of stack
- **Usage**: Discarding expression values, optimization for dist=0 branches
- **Code Generation**: `ParseNode>>emitForEffect:`, `ParseNode>>emitBranch:pop:on:` when dist=0

#### **136 (Dup): Duplicate Top**
- **Semantics**: Duplicate top of stack
- **Usage**: Cascaded messages
- **Code Generation**: `CascadeNode>>emitForValue:`

#### **137 (LdThisContext): Push Active Context**
- **Semantics**: Push the active context (thisContext)
- **Usage**: Block creation, debugging, reflection
- **Code Generation**: `BlockNode>>emitForValue:`, `VariableNode>>emitForValue:` for thisContext

### Send Bytecodes

#### **176-207: Send Special Selector**
- **Format**: Single byte 176-207
- **Semantics**: Send special selector to receiver
- **Selector Mapping** (from Smalltalk>>specialSelectorAt:):
  - 176: + (1 arg)
  - 177: - (1 arg)
  - 178: < (1 arg)
  - 179: > (1 arg)
  - 180: <= (1 arg)
  - 181: >= (1 arg)
  - 182: = (1 arg)
  - 183: ~= (1 arg)
  - 184: * (1 arg)
  - 185: / (1 arg)
  - 186: \\ (1 arg)
  - 187: @ (1 arg)
  - 188: bitShift: (1 arg)
  - 189: // (1 arg)
  - 190: bitAnd: (1 arg)
  - 191: bitOr: (1 arg)
  - 192: at: (1 arg)
  - 193: at:put: (2 args)
  - 194: size (0 args)
  - 195: next (0 args)
  - 196: nextPut: (1 arg)
  - 197: atEnd (0 args)
  - 198: == (1 arg)
  - 199: class (0 args)
  - 200: blockCopy: (1 arg)
  - 201: value (0 args)
  - 202: value: (1 arg)
  - 203: do: (1 arg)
  - 204: new (0 args)
  - 205: new: (1 arg)
  - 206: x (0 args)
  - 207: y (0 args)
- **Code Generation**: `SelectorNode>>emit:args:on:super:`

#### **208-223: Send Literal Selector (0 args)**
- **Format**: Single byte 208-223
- **Semantics**: Send literal selector at index 0-15 with 0 arguments
- **Usage**: Common unary messages
- **Code Generation**: `SelectorNode>>emit:args:on:super:` for 0-arg sends

#### **224-239: Send Literal Selector (1 arg)**
- **Format**: Single byte 224-239
- **Semantics**: Send literal selector at index 0-15 with 1 argument
- **Usage**: Common binary and keyword messages with 1 arg
- **Code Generation**: `SelectorNode>>emit:args:on:super:` for 1-arg sends

#### **240-255: Send Literal Selector (2 args)**
- **Format**: Single byte 240-255
- **Semantics**: Send literal selector at index 0-15 with 2 arguments
- **Usage**: Common keyword messages with 2 args
- **Code Generation**: `SelectorNode>>emit:args:on:super:` for 2-arg sends

#### **131 (SendLong): Extended Send**
- **Format**: [131+flags] [nArgs] [selector-index]
- **Flags**:
  - +0: Normal send
  - +1: Extended format (3 bytes)
  - +2: Super send
- **Medium Send Format**: [131+flags] [nArgs*32 + index]
  - When selector index < 32 and nArgs <= 7
- **Long Send Format**: [131+flags+1] [nArgs] [selector-index]
  - When selector index >= 32 or nArgs > 7
- **Code Generation**: `SelectorNode>>emit:args:on:super:`

#### **133 (LdSuper): Push Super**
- **Semantics**: Push receiver for super send
- **Usage**: Before super sends
- **Note**: Actually pushes self; the super flag on send determines lookup class
- **Code Generation**: `VariableNode>>emitForValue:` for super (but emits LdSelf)

### Return Bytecodes

#### **120-123: Return Special Constants**
- **120**: Return receiver (self)
- **121**: Return true
- **122**: Return false
- **123**: Return nil
- **Usage**: Quick returns from methods
- **Code Generation**: `VariableNode>>emitForReturn:` for special constants

#### **124 (EndMethod): Return Top of Stack**
- **Semantics**: Return top of stack from method to sender
- **Usage**: Normal method return (^expr)
- **Code Generation**: `ParseNode>>emitForReturn:`

#### **125 (EndRemote): Return from Block**
- **Semantics**: Return top of stack from block closure
- **Usage**: Block value return (no explicit ^)
- **Code Generation**: `BlockNode>>emitForValue:` when block doesn't have explicit return

## Bytecode Type System

The compiler uses a type system for encoding variables:
- **Type 1 (LdInstType)**: Instance variables
- **Type 2 (LdTempType)**: Temporary variables and arguments
- **Type 3 (LdLitType)**: Literal constants
- **Type 4 (LdLitIndType)**: Literal variables (indirect through Association)
- **Type 5 (SendType)**: Message selectors

Each type has:
- **CodeBase**: Starting bytecode for the type
- **CodeLimit**: Maximum number of short bytecodes for the type

CodeBases: [0, 16, 32, 64, 208]
CodeLimits: [16, 16, 32, 32, 16]

## Special Optimizations

### Control Structure Optimizations
The compiler recognizes and optimizes certain message patterns:
- **ifTrue:, ifFalse:, ifTrue:ifFalse:**: Compiled to conditional jumps
- **and:, or:**: Compiled with short-circuit evaluation
- **whileTrue:, whileFalse:**: Compiled to loops with backward jumps
- **to:do:, to:by:do:**: Not currently optimized (sent as regular messages)

### Block Context Creation
- Block creation uses bytecode **137 (LdThisContext)** followed by **200 (blockCopy:)**
- The blockCopy: primitive creates the BlockContext
- Block arguments are stored using **StorePop** bytecodes at block entry

### Quick Methods
Methods that simply return a constant or instance variable are optimized:
- Return self: Single bytecode 120
- Return instance variable: Encoded in method header
- Return literal constant: Encoded in method header

## Implementation Notes

1. **Jump Distances**: 
   - Positive jumps are forward (increasing PC)
   - Negative jumps encoded using two's complement in long form
   - Maximum jump distance: ±1023 bytes

2. **Variable Indexing**:
   - Instance variables indexed from receiver
   - Temporaries indexed from method context
   - Arguments are first temporaries (indices 0 to nArgs-1)

3. **Literal Frame**:
   - Method literals stored after bytecodes
   - Maximum 63 literals per method
   - Includes constants, variable bindings, selectors, block methods

4. **Stack Management**:
   - Each bytecode's stack effect is predictable
   - Compiler tracks stack depth for verification
   - Stack underflow/overflow caught at compile time

5. **Super Sends**:
   - Use same bytecode as regular sends but with super flag
   - Receiver is still self (not a special super object)
   - Method lookup starts in superclass of method's class
