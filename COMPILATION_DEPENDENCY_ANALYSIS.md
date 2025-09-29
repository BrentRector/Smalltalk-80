# Smalltalk-80 Compilation System Dependency Analysis

## Executive Summary

This document analyzes the minimum set of classes and selectors required for the Smalltalk-80 interpreter to bootstrap its compilation system. Once these core components are implemented, the system can compile Smalltalk source code and dynamically add new classes and methods.

## Core Compilation Classes

### 1. **Compiler** (System-Compiler)
The main entry point for compilation.

**Key Instance Variables:**
- `sourceStream` - input stream for source code
- `requestor` - error notification handler
- `class` - context class for compilation
- `context` - debugging context if any

**Critical Methods:**
- `compile:in:notifying:ifFail:` - Main compilation entry point
- `evaluate:in:to:notifying:ifFail:` - Evaluate expressions (DoIt)
- `translate:noPattern:ifFail:` - Translate source to parse tree
- `from:class:context:notifying:` - Initialize compilation context

**Class Methods:**
- `evaluate:for:logged:` - Class-side evaluation
- `preferredParserClass` - Returns Parser class

### 2. **Parser** (System-Compiler)
Parses Smalltalk syntax into a parse tree.

**Superclass:** Scanner

**Key Instance Variables:**
- `here`, `hereType`, `hereMark` - Current token info
- `encoder` - Variable/literal encoder
- `parseNode` - Current parse tree node
- `failBlock` - Error handling block

**Critical Methods:**
- `parse:class:noPattern:context:notifying:ifFail:` - Main parse method
- `method:context:` - Parse a complete method
- `pattern:inContext:` - Parse method pattern
- `temporaries` - Parse temporary variables
- `statements:innerBlock:` - Parse statement sequences
- `expression` - Parse expressions
- `primaryExpression` - Parse primary expressions
- `messagePart:repeat:` - Parse message sends

### 3. **Scanner** (System-Compiler)
Tokenizes Smalltalk source code.

**Key Instance Variables:**
- `source` - Input stream
- `hereChar`, `aheadChar` - Character lookahead
- `token`, `tokenType` - Current token
- `buffer` - Token accumulation buffer
- `typeTable` - Character type lookup table

**Critical Methods:**
- `scan:` - Initialize and scan input
- `scanToken` - Get next token
- `xLetter`, `xDigit`, `xBinary` - Token type handlers
- `xSingleQuote`, `xDoubleQuote` - String/comment parsing
- `scanFieldNames:` - Parse variable declarations

**Class Variables:**
- `TypeTable` - Character classification table
- `EndChar` - End of stream marker

### 4. **Encoder** (System-Compiler)
Encodes variables and literals for bytecode generation.

**Superclass:** ParseNode

**Key Instance Variables:**
- `scopeTable` - Variable name to node mapping
- `nTemps` - Temporary variable count
- `literalStream` - Literal collection
- `selectorSet`, `litSet`, `litIndSet` - Literal pools

**Critical Methods:**
- `init:context:notifying:` - Initialize encoding context
- `encodeVariable:` - Encode variable access
- `encodeLiteral:` - Encode literal value
- `bindTemp:` - Bind temporary variable
- `litIndex:` - Add literal to stream
- `cantStoreInto:` - Check assignability

## Parse Tree Node Classes

### 5. **ParseNode** (System-Compiler)
Abstract base class for all parse tree nodes.

**Critical Methods:**
- `emitEffect:on:` - Generate effect bytecode
- `emitValue:on:` - Generate value bytecode
- `ensureCanCascade:` - Cascade validation
- `sizeEffect:`, `sizeValue:` - Bytecode size calculation

### 6. **MethodNode** (System-Compiler)
Represents a compiled method.

**Critical Instance Variables:**
- `selector` - Method selector
- `arguments` - Argument nodes
- `block` - Method body block
- `encoder` - Associated encoder
- `primitive` - Primitive number

**Critical Methods:**
- `generate:` - Generate CompiledMethod
- `selector:arguments:precedence:temporaries:block:encoder:primitive:` - Full initialization

### 7. **BlockNode** (System-Compiler)
Represents block closures.

**Critical Instance Variables:**
- `arguments` - Block arguments
- `statements` - Block statements
- `returns` - Return flag
- `nArgsNode`, `startpc` - Runtime info

**Critical Methods:**
- `arguments:statements:returns:from:` - Initialize block
- `emitForValue:on:` - Generate block bytecode

### 8. **MessageNode** (System-Compiler)
Represents message sends.

**Critical Instance Variables:**
- `receiver` - Receiver expression
- `selector` - Message selector
- `arguments` - Argument expressions
- `precedence` - Message precedence

**Critical Methods:**
- `receiver:selector:arguments:precedence:from:` - Initialize
- `emitForValue:on:` - Generate send bytecode

### 9. **VariableNode** (System-Compiler)
Represents variable access.

**Critical Instance Variables:**
- `name` - Variable name
- `index` - Variable offset
- `code` - Access bytecode

**Critical Methods:**
- `name:index:type:` - Initialize variable
- `emitCodeForValue:on:` - Generate access bytecode
- `emitCodeForStore:on:` - Generate store bytecode

### 10. **AssignmentNode** (System-Compiler)
Represents variable assignment.

**Critical Instance Variables:**
- `variable` - Target variable
- `value` - Value expression

**Critical Methods:**
- `variable:value:from:` - Initialize assignment
- `emitCodeForValue:on:` - Generate assignment bytecode

## Code Generation Classes

### 11. **CompiledMethod**
The bytecode representation of a method.

**Critical Methods:**
- `headerValue` - Method header encoding
- `primitive` - Primitive number
- `numLiterals`, `numArgs`, `numTemps` - Method metadata
- `at:put:` - Bytecode writing

### 12. **MethodContext**
Runtime activation record.

**Critical for Compilation:**
- Used by Compiler for `DoItIn:` evaluation in debug contexts

## Behavior Hierarchy Compilation Support

### 13. **Behavior** (Kernel-Classes)
Base class providing compilation infrastructure.

**Critical Methods:**
- `compile:notifying:trailer:ifFail:` - Compile source code
- `addSelector:withMethod:` - Install compiled method
- `compilerClass` - Return Compiler class
- `parserClass` - Return Parser class

### 14. **ClassDescription** (Kernel-Classes)
Adds category support to compilation.

**Superclass:** Behavior

**Critical Methods:**
- `compile:classified:notifying:` - Compile with category
- `organization` - Method categorization

### 15. **Class** (Kernel-Classes)
Full class support including class variables.

**Superclass:** ClassDescription

**Critical Methods:**
- `classPool` - Class variable dictionary
- `sharedPools` - Shared pool dictionaries

### 16. **Metaclass** (Kernel-Classes)
Class-side behavior support.

## Supporting Classes

### 17. **ByteCodeStream**
Helper for bytecode generation.

**Critical Methods:**
- `nextPut:` - Write bytecode
- `nextPutAll:` - Write bytecode sequence

### 18. **LiteralNode**, **SelectorNode**, **SpecialLiteralNode**
Specialized parse nodes for different literal types.

## Minimum Bootstrap Requirements

To enable the interpreter to compile Smalltalk source code, the following **minimum set** must be implemented:

### Phase 1: Core Parsing (Required First)
1. **Scanner** - Tokenization
   - `scan:`, `scanToken`, character handlers
   - TypeTable initialization
2. **Parser** - Syntax analysis
   - `parse:class:noPattern:context:notifying:ifFail:`
   - Expression parsing methods
3. **ParseNode** and subclasses - Parse tree
   - MethodNode, BlockNode, MessageNode, VariableNode
   - AssignmentNode, LiteralNode

### Phase 2: Encoding and Code Generation
4. **Encoder** - Variable/literal encoding
   - `init:context:notifying:`
   - Variable binding methods
5. **Compiler** - Orchestration
   - `compile:in:notifying:ifFail:`
   - `evaluate:in:to:notifying:ifFail:`
6. **CompiledMethod** - Bytecode storage
   - Basic structure and accessors

### Phase 3: Installation Infrastructure
7. **Behavior>>compile:notifying:trailer:ifFail:**
8. **Behavior>>addSelector:withMethod:**
9. **ClassDescription>>compile:classified:notifying:**

### Phase 4: Execution Support (for DoIt)
10. **MethodContext** - For evaluation in debug contexts

## Critical Selectors

The following selectors MUST be implemented for compilation to work:

### Compiler Protocol
- `#compile:in:notifying:ifFail:`
- `#evaluate:in:to:notifying:ifFail:`
- `#parse:class:noPattern:context:notifying:ifFail:` (Parser)
- `#generate:` (MethodNode)

### Installation Protocol
- `#addSelector:withMethod:` (Behavior)
- `#at:put:` (CompiledMethod)
- `#methodDictionary:` (Behavior)

### Variable Resolution
- `#encodeVariable:` (Encoder)
- `#bindTemp:` (Encoder)
- `#scopeHas:ifTrue:` (Class)

## Bootstrap Strategy

1. **Implement Scanner/Parser** - These can work without full Smalltalk
2. **Implement minimal ParseNode tree** - Just enough for simple methods
3. **Implement Encoder** - Start with simple variable lookup
4. **Implement Compiler>>compile:** - Basic compilation
5. **Implement Behavior>>addSelector:withMethod:** - Method installation
6. **Test with simple methods** - Start with accessor methods
7. **Gradually add complexity** - Blocks, cascades, etc.

## Dependencies on Primitives

The compilation system requires these primitives to function:
- **Object allocation** - For creating parse nodes
- **Array/Dictionary access** - For literal pools and method dictionaries
- **String operations** - For source code manipulation
- **Stream operations** - For reading source
- **Method installation** - May require primitive support

## Validation Test

A minimal compilation test would be:
```smalltalk
Compiler evaluate: 'Object compile: ''test ^42'' classified: ''testing'''
```

This requires:
- Scanner to tokenize the string
- Parser to build parse tree
- Encoder to encode literals (42)
- MethodNode to generate bytecode
- Behavior to install the method

## Conclusion

The Smalltalk-80 compilation system is elegantly layered, with clear separation between:
1. **Lexical analysis** (Scanner)
2. **Syntax analysis** (Parser)
3. **Semantic analysis** (Encoder)
4. **Code generation** (ParseNode hierarchy)
5. **Method installation** (Behavior hierarchy)

The minimum bootstrap implementation requires approximately 15-20 classes with their core methods. Once this foundation is in place, the system becomes self-extending, able to compile and install any Smalltalk code, including enhancements to the compiler itself.