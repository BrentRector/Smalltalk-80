# Smalltalk-80 Self-Hosting Roadmap

## Overview
This document tracks the progress toward making the Smalltalk-80 interpreter self-hosting - capable of loading and compiling the entire Smalltalk-80 system image from source code.

**Current Status: ~40% Complete** - Core interpreter working, compilation pipeline missing

---

## Phase 1: Complete Core Runtime ✅ 100% Complete

### Object System
- [x] Object memory with proper OOP management
- [x] Core class hierarchy (Object, Behavior, Class, Metaclass)
- [x] Method dictionaries with proper structure
- [x] Instance variable inheritance
- [x] Association objects with correct 2-field structure
- [x] Symbol table and interning
- [x] SmallInteger immediate values

### Bytecode Interpreter
- [x] Complete bytecode dispatcher (256 opcodes)
- [x] Stack operations (push/pop/dup)
- [x] Variable access (receiver/temporary/literal)
- [x] Method sends (0-2 arguments)
- [x] Return operations (receiver/top/block)
- [x] Jump operations (conditional/unconditional)
- [x] Special selector sends
- [x] Extended bytecodes (single/double)

### Method System
- [x] Method lookup through class hierarchy
- [x] Method cache for performance
- [x] `doesNotUnderstand:` message handling
- [x] Super sends
- [x] Primitive method dispatch
- [x] Method tracing for debugging

### Critical Primitives
- [x] Arithmetic (SmallInteger/LargeInteger/Float)
- [x] Comparison operations
- [x] Object creation (`new`/`new:`/`basicNew`/`basicNew:`)
- [x] Array/Collection access (`at:`/`at:put:`/`size`)
- [x] Storage management (`become:`/`instVarAt:`/`instVarAtPut:`)
- [x] Control primitives (`perform:`/`perform:with:`)
- [x] String comparison primitives (#158, #159)
- [x] Character creation primitive (#183 - moved from #170)
- [x] String at:/at:put: primitives (#63, #64)
- [x] Stream primitives stubbed (next/nextPut:/atEnd)

### Block Contexts
- [x] BlockContext class exists
- [x] Block copy primitive (#80)
- [x] Block value primitives (#81, #82, #201)
- [x] Block activation with proper context chain
- [x] Block return handling
- [x] Block temporaries and arguments
- [x] Non-local returns from blocks

---

## Phase 2: Compilation Pipeline ⬜ 0% Complete

### Parser Integration
- [ ] Connect ANTLR parser to VM
- [ ] Parse method source to AST
- [ ] Parse class definitions
- [ ] Parse doIt expressions
- [ ] Handle syntax errors gracefully
- [ ] Support all Smalltalk-80 syntax constructs

### AST to Bytecode Compiler
- [ ] Create Compiler class hierarchy
- [ ] Implement AST visitor for code generation
- [ ] Variable binding resolution
  - [ ] Instance variables
  - [ ] Temporary variables
  - [ ] Class variables
  - [ ] Pool variables
  - [ ] Global variables
- [ ] Literal frame construction
  - [ ] Literal constants
  - [ ] Literal variables (Associations)
  - [ ] Selector symbols
  - [ ] Block literals
- [ ] Message send compilation
  - [ ] Unary messages
  - [ ] Binary messages
  - [ ] Keyword messages
  - [ ] Cascade messages
  - [ ] Super sends
- [ ] Control structure compilation
  - [ ] ifTrue:/ifFalse: optimization
  - [ ] whileTrue:/whileFalse: optimization
  - [ ] to:do: optimization
  - [ ] and:/or: optimization
- [ ] Block compilation
  - [ ] Block arguments
  - [ ] Block temporaries
  - [ ] Block returns
  - [ ] Nested blocks
- [ ] Primitive method compilation

### Method Installation
- [ ] Create CompiledMethod from bytecode
- [ ] Set method header (temps/args/primitive)
- [ ] Install method in class method dictionary
- [ ] Flush method cache on installation
- [ ] Handle method categories
- [ ] Support class-side methods

### Compiler Primitives
- [ ] Primitive #170: `createSubclass`
- [ ] Primitive #171: `compileMethod`
- [ ] Primitive #172: `compileMethodClassified`
- [ ] Primitive #173: `createClassFull`
- [ ] Primitive #174: `installMethodInClass`
- [ ] Primitive #175: `newMethodFromSource`

---

## Phase 3: Dynamic Class Creation ⬜ 0% Complete

### Class Building Protocol
- [ ] Implement `Class>>#subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:`
- [ ] Implement `Metaclass>>#subclass:`
- [ ] Support class redefinition
- [ ] Handle class variable initialization
- [ ] Pool dictionary support
- [ ] Class categories

### Class Modification
- [ ] Add instance variables to existing class
- [ ] Remove instance variables
- [ ] Recompile affected methods
- [ ] Migrate existing instances
- [ ] Update subclasses

### Method Categories
- [ ] ClassOrganizer implementation
- [ ] Method categorization
- [ ] Category browsing support
- [ ] Default category handling

---

## Phase 4: File-In Support ⬜ 0% Complete

### Chunk Format Parser
- [ ] Parse bang-delimited chunks
- [ ] Handle embedded quotes
- [ ] Support comment chunks
- [ ] Parse method temporaries
- [ ] Handle primitive declarations

### File-In Protocol
- [ ] Implement `fileIn` mechanism
- [ ] Parse class definition chunks
- [ ] Parse `methodsFor:` chunks
- [ ] Parse `methodsFor:stamp:` chunks (optional)
- [ ] Parse class comment chunks
- [ ] Parse class initialization chunks
- [ ] Handle doit chunks
- [ ] Support reorganize chunks

### Dependency Management
- [ ] Track undefined globals
- [ ] Forward reference resolution
- [ ] Class initialization order
- [ ] Handle circular dependencies

### Error Recovery
- [ ] Syntax error reporting with line numbers
- [ ] Undefined variable warnings
- [ ] Method compilation error handling
- [ ] Partial file-in recovery
- [ ] Transaction/rollback support

---

## Phase 5: System Image Loading ⬜ 0% Complete

### Minimal Kernel
- [ ] Define minimal class set for bootstrap
- [ ] Create kernel loading order
- [ ] Bootstrap compiler from bytecode
- [ ] Load Symbol/String/Array classes
- [ ] Initialize global Smalltalk dictionary

### Compiler Bootstrap
- [ ] Load Scanner class
- [ ] Load Parser class  
- [ ] Load Compiler class
- [ ] Load CompiledMethod support
- [ ] Test compilation of simple methods
- [ ] Enable recursive compilation

### Core System Classes
- [ ] Load Collection hierarchy
- [ ] Load Number hierarchy
- [ ] Load Stream classes
- [ ] Load Exception handling (optional)
- [ ] Load Process/Semaphore (optional)

### Full System Image
- [ ] Parse Smalltalk-80.st file
- [ ] Load classes in dependency order
- [ ] Compile all methods
- [ ] Initialize class variables
- [ ] Run class initialization
- [ ] Verify against reference image

---

## Phase 6: Validation & Testing ⬜ 0% Complete

### Unit Tests
- [ ] Test each primitive operation
- [ ] Test bytecode execution
- [ ] Test method lookup
- [ ] Test block contexts
- [ ] Test non-local returns
- [ ] Test exception handling

### Compilation Tests
- [ ] Test expression compilation
- [ ] Test method compilation
- [ ] Test block compilation
- [ ] Test optimization patterns
- [ ] Test error cases

### System Tests
- [ ] Run Smalltalk-80 test suite
- [ ] Verify core class behavior
- [ ] Test collection operations
- [ ] Test stream operations
- [ ] Test number arithmetic
- [ ] Test string operations

### Performance Tests
- [ ] Benchmark method sends
- [ ] Benchmark arithmetic
- [ ] Benchmark collection operations
- [ ] Benchmark compilation speed
- [ ] Memory usage analysis

---

## Known Issues & Blockers

### Critical Issues
- [ ] String/Character primitives not implemented
- [ ] Block activation incomplete
- [ ] No source compilation pathway
- [ ] File I/O primitives disabled

### Important Issues  
- [ ] Method categories not supported
- [ ] Class variables not fully implemented
- [ ] Pool dictionaries not supported
- [ ] Process scheduler incomplete

### Nice to Have
- [ ] Debugger support
- [ ] Image snapshot/resume
- [ ] Graphics primitives
- [ ] Sound primitives
- [ ] Network primitives

---

## Next Immediate Actions

1. **Week 1-2: Complete String/Character Primitives**
   - [ ] Implement primitive #158 (string equals)
   - [ ] Implement primitive #159 (string less than)
   - [ ] Implement primitive #170 (character from value)
   - [ ] Test with string operations

2. **Week 3-4: Fix Block Contexts**
   - [ ] Implement block value primitives
   - [ ] Fix block activation
   - [ ] Test with iteration methods
   - [ ] Verify non-local returns

3. **Week 5-6: Basic Compiler Integration**
   - [ ] Create Compiler facade class
   - [ ] Connect parser to VM
   - [ ] Implement simple expression compilation
   - [ ] Test with doIt expressions

4. **Week 7-8: Method Compilation**
   - [ ] Compile simple methods
   - [ ] Install in method dictionary
   - [ ] Test method execution
   - [ ] Handle compilation errors

5. **Week 9-10: File-In Support**
   - [ ] Implement chunk parser
   - [ ] Support methodsFor: protocol
   - [ ] Test with single class file-in
   - [ ] Handle compilation errors

---

## Success Criteria

The interpreter will be considered self-hosting when it can:

1. ✅ Execute bytecode for all Smalltalk-80 operations
2. ⬜ Compile Smalltalk source to bytecode
3. ⬜ Create new classes from Smalltalk expressions
4. ⬜ File in the complete Smalltalk-80.st system image
5. ⬜ Pass the Smalltalk-80 test suite
6. ⬜ Recompile itself from source

---

## Progress Tracking

| Phase | Status | Completion | Target Date |
|-------|--------|------------|-------------|
| Phase 1: Core Runtime | ✅ Complete | 100% | Completed |
| Phase 2: Compilation | ⬜ Not Started | 0% | End of Week 8 |
| Phase 3: Class Creation | ⬜ Not Started | 0% | End of Week 10 |
| Phase 4: File-In | ⬜ Not Started | 0% | End of Week 12 |
| Phase 5: System Load | ⬜ Not Started | 0% | End of Week 14 |
| Phase 6: Validation | ⬜ Not Started | 0% | End of Week 16 |

**Overall Progress: █████░░░░░░░░░░ 40%**

---

*Last Updated: January 2025*
*Document Version: 1.0*