# Missing Classes for Self-Hosted Smalltalk Class Compilation

## Current Status

**Currently bootstrapped: 34 classes** including Object, Behavior, Class, Metaclass, CompiledMethod, String, Symbol, Array, etc.

## Missing Classes for Self-Hosted Smalltalk Class Compilation

### **Critical Missing Classes for Compilation Pipeline:**

**1. Compiler Infrastructure:**
- `Scanner` - Lexical analysis (tokenizes source code)
- `Parser` - Syntactic analysis (creates parse trees) 
- `Compiler` - Main compilation coordinator
- `Encoder` - Bytecode generation from parse trees
- `Decompiler` - Reverse engineering bytecode to source

**2. Parse Tree Classes:**
- `ParseNode` - Base class for all parse tree nodes
- `MethodNode` - Method definition nodes
- `MessageNode` - Message send nodes  
- `BlockNode` - Block/closure nodes
- `AssignmentNode` - Variable assignment nodes
- `ReturnNode` - Return statement nodes
- `CascadeNode` - Cascaded message sends
- `LeafNode` - Base for leaf nodes
- `LiteralNode` - Literal values (numbers, strings, symbols)
- `VariableNode` - Variable references
- `SelectorNode` - Message selector nodes

**3. Essential Missing Collections/Streams:**
- `WriteStream` - ✅ Already present
- `ReadStream` - ✅ Already present  
- `OrderedCollection` - **Missing** - Dynamic arrays needed by compiler
- `Interval` - **Missing** - Number ranges used in compilation
- `SortedCollection` - **Missing** - Used for organizing symbols/methods

**4. Class Building Infrastructure:**
- Methods on `Behavior` for `subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:`
- Methods on `Class` for method compilation and installation
- `ClassDescription` - ✅ Already present but needs compilation methods

**5. Support Classes:**
- `SyntaxError` - **Missing** - Compilation error handling
- `Undeclared` - **Missing** - Undefined variable tracking during compilation

### **Key Methods Needed on Existing Classes:**

**On `Behavior`:**
- `compile:notifying:` - Main method compilation entry point
- `subclass:instanceVariableNames:...` - Class creation
- `addSelector:withMethod:` - Method installation

**On `String`:**
- Stream creation methods for compilation input

**On `Class`:**  
- Class organization and method compilation support

### **Minimum Additional Classes for Basic Self-Hosting:**

```
OrderedCollection, Scanner, Parser, Compiler, Encoder, ParseNode, 
MethodNode, MessageNode, BlockNode, LeafNode, LiteralNode, VariableNode
```

**Total: ~12 additional classes** would provide basic self-hosted compilation capability, allowing Smalltalk code to compile and create new classes within the VM.

### **Essential Workflow for Self-Hosted Compilation:**

```
Smalltalk Source Code
        ↓
    Scanner (tokenize)
        ↓
    Parser (parse tree)
        ↓
    Compiler (coordinate)
        ↓
    Encoder (bytecode)
        ↓
    CompiledMethod
        ↓
Class Installation (via Behavior methods)
```

This would enable the essential workflow: `source → Scanner → Parser → Compiler → Encoder → CompiledMethod → class installation`, allowing Smalltalk to compile and install new classes entirely within the VM environment.

## Analysis Notes

- **Current bootstrap** provides the core object model (classes, metaclasses, method dictionaries) and basic collections
- **Missing compilation infrastructure** prevents self-hosted class creation via source code compilation  
- **Adding these 12 classes** would enable a Smalltalk system to parse, compile, and install new classes from source code
- **Full Blue Book compliance** would require additional classes, but this minimal set enables basic self-hosting capabilities

Generated: 2025-09-16