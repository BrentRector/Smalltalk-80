# Smalltalk-80 Compilation Support

## Overview

This document outlines the minimum set of classes and selectors required to enable the Smalltalk-80 interpreter to compile source code dynamically. Once these components are implemented, the system becomes self-hosting - able to read Smalltalk source, parse it, compile it to bytecode, and install new methods and classes into the running system.

## Goal

Enable the interpreter to execute Smalltalk code that can:
1. Read in a Smalltalk class definition
2. Parse the source code
3. Compile it to bytecode
4. Call the appropriate selectors to add it to the system

## Architecture Overview

The Smalltalk-80 compilation system consists of five main layers:

```
Source Code → Scanner → Parser → Parse Tree → Code Generator → Bytecode
                ↓         ↓          ↓             ↓              ↓
            [Tokens] [AST Tree] [Encoder]  [MethodNode]  [CompiledMethod]
```

1. **Scanner** - Tokenizes source code into symbols, keywords, literals, etc.
2. **Parser** - Builds parse trees from tokens using recursive descent parsing
3. **Encoder** - Manages variable scoping, literal pools, and temporary variables
4. **ParseNode hierarchy** - Represents the abstract syntax tree
5. **Compiler** - Orchestrates the compilation process and interfaces with the class system

## Minimum Required Classes (15 Total)

### 1. Scanner Infrastructure

#### **Scanner** (Object subclass)
- **Purpose**: Tokenizes Smalltalk source code
- **Instance Variables**:
  - `source` - Input stream
  - `hereChar`, `aheadChar` - Character lookahead
  - `token`, `tokenType` - Current token
  - `buffer` - Token accumulation buffer
  - `typeTable` - Character type lookup table
- **Critical Methods**:
  - `scan:` - Initialize and scan input
  - `scanToken` - Get next token
  - `xLetter`, `xDigit`, `xBinary` - Token type handlers
  - `xSingleQuote`, `xDoubleQuote` - String/comment parsing
  - `scanFieldNames:` - Parse variable declarations
- **Class Variables**:
  - `TypeTable` - Character classification table (must be initialized)

### 2. Parse Tree Base Classes

#### **ParseNode** (Object subclass)
- **Purpose**: Abstract base class for all parse tree nodes
- **Critical Methods**:
  - `emitEffect:on:` - Generate effect bytecode
  - `emitValue:on:` - Generate value bytecode
  - `ensureCanCascade:` - Cascade validation
  - `sizeEffect:`, `sizeValue:` - Bytecode size calculation

#### **LeafNode** (ParseNode subclass)
- **Purpose**: Base class for terminal nodes
- **Instance Variables**:
  - `key` - The value or name
  - `code` - Associated bytecode

### 3. Parse Tree Node Classes

#### **VariableNode** (LeafNode subclass)
- **Purpose**: Represents variable access
- **Instance Variables**:
  - `name` - Variable name
  - `index` - Variable offset
  - `code` - Access bytecode
- **Critical Methods**:
  - `name:index:type:` - Initialize variable
  - `emitCodeForValue:on:` - Generate access bytecode
  - `emitCodeForStore:on:` - Generate store bytecode

#### **LiteralNode** (LeafNode subclass)
- **Purpose**: Represents literal values
- **Critical Methods**:
  - `emitCodeForValue:on:` - Generate literal push bytecode

#### **MessageNode** (ParseNode subclass)
- **Purpose**: Represents message sends
- **Instance Variables**:
  - `receiver` - Receiver expression
  - `selector` - Message selector
  - `arguments` - Argument expressions
  - `precedence` - Message precedence
- **Critical Methods**:
  - `receiver:selector:arguments:precedence:from:` - Initialize
  - `emitForValue:on:` - Generate send bytecode

#### **AssignmentNode** (ParseNode subclass)
- **Purpose**: Represents variable assignment
- **Instance Variables**:
  - `variable` - Target variable
  - `value` - Value expression
- **Critical Methods**:
  - `variable:value:from:` - Initialize assignment
  - `emitCodeForValue:on:` - Generate assignment bytecode

#### **ReturnNode** (ParseNode subclass)
- **Purpose**: Represents return statements
- **Instance Variables**:
  - `expr` - Expression to return
- **Critical Methods**:
  - `expr:` - Set return expression
  - `emitCodeForValue:on:` - Generate return bytecode

#### **BlockNode** (ParseNode subclass)
- **Purpose**: Represents block closures
- **Instance Variables**:
  - `arguments` - Block arguments
  - `statements` - Block statements
  - `returns` - Return flag
- **Critical Methods**:
  - `arguments:statements:returns:from:` - Initialize block
  - `emitForValue:on:` - Generate block bytecode

#### **CascadeNode** (ParseNode subclass)
- **Purpose**: Represents message cascades
- **Instance Variables**:
  - `receiver` - Shared receiver
  - `messages` - Cascaded messages

#### **MethodNode** (ParseNode subclass)
- **Purpose**: Represents a complete method
- **Instance Variables**:
  - `selector` - Method selector
  - `arguments` - Argument nodes
  - `block` - Method body block
  - `encoder` - Associated encoder
  - `primitive` - Primitive number
  - `literals` - Literal pool
- **Critical Methods**:
  - `generate:` - Generate CompiledMethod
  - `selector:arguments:precedence:temporaries:block:encoder:primitive:` - Full initialization

### 4. Encoding Support

#### **Encoder** (ParseNode subclass)
- **Purpose**: Manages variables and literals during compilation
- **Instance Variables**:
  - `scopeTable` - Variable name to node mapping
  - `nTemps` - Temporary variable count
  - `literalStream` - Literal collection
  - `selectorSet`, `litSet`, `litIndSet` - Literal pools
- **Critical Methods**:
  - `init:context:notifying:` - Initialize encoding context
  - `encodeVariable:` - Encode variable access
  - `encodeLiteral:` - Encode literal value
  - `bindTemp:` - Bind temporary variable
  - `litIndex:` - Add literal to stream
  - `cantStoreInto:` - Check assignability

### 5. Parser and Compiler

#### **Parser** (Scanner subclass)
- **Purpose**: Parses Smalltalk syntax into parse tree
- **Instance Variables**:
  - `here`, `hereType`, `hereMark` - Current token info
  - `encoder` - Variable/literal encoder
  - `parseNode` - Current parse tree node
  - `failBlock` - Error handling block
- **Critical Methods**:
  - `parse:class:noPattern:context:notifying:ifFail:` - Main parse method
  - `method:context:` - Parse a complete method
  - `pattern:inContext:` - Parse method pattern
  - `temporaries` - Parse temporary variables
  - `statements:innerBlock:` - Parse statement sequences
  - `expression` - Parse expressions
  - `primaryExpression` - Parse primary expressions
  - `messagePart:repeat:` - Parse message sends

#### **Compiler** (Object subclass)
- **Purpose**: Orchestrates the compilation process
- **Instance Variables**:
  - `sourceStream` - Input stream for source code
  - `requestor` - Error notification handler
  - `class` - Context class for compilation
  - `context` - Debugging context if any
- **Critical Methods**:
  - `compile:in:notifying:ifFail:` - Main compilation entry point
  - `evaluate:in:to:notifying:ifFail:` - Evaluate expressions (DoIt)
  - `translate:noPattern:ifFail:` - Translate source to parse tree
  - `from:class:context:notifying:` - Initialize compilation context

## Critical Selectors That Must Be Implemented

### On Compiler Classes

```smalltalk
"Main compilation flow"
Compiler>>compile:in:notifying:ifFail:
Compiler>>translate:noPattern:ifFail:

"Parsing"
Parser>>parse:class:noPattern:context:notifying:ifFail:
Parser>>method:context:
Parser>>expression
Parser>>primaryExpression

"Code generation"
MethodNode>>generate:
BlockNode>>emitForValue:on:
MessageNode>>emitForValue:on:
VariableNode>>emitCodeForValue:on:

"Encoding"
Encoder>>init:context:notifying:
Encoder>>encodeVariable:
Encoder>>encodeLiteral:
```

### On Existing Classes (Behavior Hierarchy)

```smalltalk
"Method compilation and installation"
Behavior>>compile:notifying:trailer:ifFail:
Behavior>>addSelector:withMethod:
Behavior>>compilerClass

"Class-specific compilation"
Class>>compile:classified:
Class>>compile:classified:notifying:
ClassDescription>>compile:classified:notifying:

"Method dictionary access"
Behavior>>methodDictionary
Behavior>>methodDictionary:
```

## Implementation Plan

### Phase 1: Foundation (Week 1)
**Goal**: Create the basic infrastructure for tokenization

1. **Add Scanner class to cold start**
   - Initialize TypeTable with character classifications
   - Implement basic tokenization methods
   - Test with simple expressions

2. **Add ParseNode base class**
   - Implement abstract methods for code generation
   - Add size calculation methods

3. **Add LeafNode and basic node types**
   - VariableNode for variable access
   - LiteralNode for constants

### Phase 2: Parse Tree (Week 2)
**Goal**: Build complete AST representation

1. **Add remaining parse nodes**
   - MessageNode for message sends
   - AssignmentNode for assignments
   - ReturnNode for returns
   - BlockNode for blocks
   - CascadeNode for cascades
   - MethodNode for complete methods

2. **Add Encoder class**
   - Variable scope management
   - Literal pool management
   - Temporary variable tracking

### Phase 3: Parser (Week 3)
**Goal**: Parse Smalltalk syntax into AST

1. **Add Parser class**
   - Extend Scanner
   - Implement recursive descent parsing
   - Handle all Smalltalk syntax constructs

2. **Test parsing**
   - Parse simple methods
   - Parse complex expressions
   - Verify AST structure

### Phase 4: Code Generation (Week 4)
**Goal**: Generate bytecode from AST

1. **Implement code generation in parse nodes**
   - Each node type generates appropriate bytecode
   - Handle special cases (super sends, primitives)

2. **Implement MethodNode>>generate:**
   - Combine all bytecode
   - Create CompiledMethod
   - Set up literal frame

### Phase 5: Compiler Integration (Week 5)
**Goal**: Complete compilation pipeline

1. **Add Compiler class**
   - Orchestrate parsing and code generation
   - Handle error reporting
   - Support DoIt evaluation

2. **Implement installation methods**
   - Behavior>>compile:notifying:trailer:ifFail:
   - Behavior>>addSelector:withMethod:
   - Class>>compile:classified:

### Phase 6: Testing and Validation (Week 6)
**Goal**: Ensure self-hosting capability

1. **Basic compilation test**:
   ```smalltalk
   Object compile: 'test ^42' classified: 'testing'
   ```

2. **Complex method test**:
   ```smalltalk
   Object compile: 'add: x to: y ^x + y' classified: 'arithmetic'
   ```

3. **Block compilation test**:
   ```smalltalk
   Object compile: 'do: aBlock ^aBlock value' classified: 'blocks'
   ```

4. **Self-modification test**:
   ```smalltalk
   Compiler compile: 'enhanced ^self' classified: 'self-test'
   ```

## Validation Criteria

The implementation is complete when:

1. **Scanner can tokenize** any valid Smalltalk source
2. **Parser can build AST** for all Smalltalk constructs
3. **Code generator produces** correct bytecode
4. **Methods can be installed** and executed
5. **Compiler can compile itself** (true self-hosting)

## Dependencies

### Required Primitives
- Object allocation (for creating nodes)
- Array/Dictionary access (for method dictionaries)
- String operations (for source manipulation)
- Stream operations (for reading source)

### Required Existing Classes
- Object, Behavior, ClassDescription, Class, Metaclass
- CompiledMethod (already in cold start)
- Array, Dictionary, String, Symbol
- Stream classes for source input

## Testing Strategy

### Unit Tests
1. Scanner tokenization tests
2. Parser AST generation tests
3. Code generation tests
4. Method installation tests

### Integration Tests
1. Compile and execute simple methods
2. Compile methods with blocks
3. Compile methods with complex expressions
4. Compile class-side methods

### Self-Hosting Test
The ultimate test: Use the compiled compiler to compile an enhanced version of itself:

```smalltalk
"Bootstrap test - compiler compiling compiler"
Compiler compile: 
  'compile: sourceString in: aClass
    ^self new compile: sourceString 
              in: aClass 
              notifying: nil 
              ifFail: [nil]'
  classified: 'compiling'
```

## Benefits of Implementation

Once these ~15 classes are implemented:

1. **Dynamic Extension**: Add new methods and classes at runtime
2. **Self-Modification**: System can enhance itself
3. **Development Tools**: Build browsers, debuggers, inspectors
4. **Complete Smalltalk**: Full language available for use
5. **True Object System**: Everything becomes truly dynamic

## Summary

The Smalltalk-80 compilation system requires a relatively small set of classes (~15) to achieve self-hosting capability. The implementation follows a clear architectural pattern from tokenization through code generation. Once complete, the system can compile any Smalltalk code, including enhancements to the compiler itself, making it a truly reflective and self-modifying system.

This represents the "DNA" of Smalltalk - the minimal genetic code that allows the system to grow and evolve through its own mechanisms.