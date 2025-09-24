# Interactive Smalltalk Grammar Entry Points Guide

## Overview

While the file-out format is essential for code persistence, interactive Smalltalk development requires multiple grammar entry points to handle various code entry contexts. This guide documents the different ways users enter Smalltalk code interactively and the corresponding grammar entry points needed.

## Interactive Code Entry Scenarios

### 1. Workspace/Transcript Expressions (DoIt, PrintIt, InspectIt)

**User Context**: Workspace window, evaluating arbitrary expressions

**What Users Type**:
```smalltalk
| temp result |
temp := 100.
result := temp sqrt.
Transcript show: 'Result is ', result printString
```

**Grammar Entry Point**: ✅ Already exists
```antlr
statementsEntry: statements EOF;
```

**Key Features**:
- Supports temporaries at the top level
- Multiple statements with periods
- Optional return (^) statement
- No method or class context needed

### 2. Browser Method Entry (Without Bangs)

**User Context**: Class Browser, defining a method for selected class/category

**What Users Type**:
```smalltalk
factorial
    "Answer the factorial of the receiver."
    self = 0 ifTrue: [^1].
    ^self * (self - 1) factorial
```

**Grammar Entry Point**: ✅ Already exists
```antlr
methodEntry: methodHeader temporaries? primitiveDecl? methodBodyNoBang EOF;
```

**Key Features**:
- No class specification (implicit from browser selection)
- No bangs or category markers
- Full method syntax with primitives, temps, etc.

### 3. Single Expression Evaluation

**User Context**: Debugger, Inspector, or Workspace for quick evaluation

**What Users Type**:
```smalltalk
self class allInstances size
```

**Grammar Entry Point**: ✅ Already exists
```antlr
expressionEntry: expression EOF;
```

**Key Features**:
- Single expression only
- No temporaries or statements
- Used for PrintIt and InspectIt operations

### 4. Class Definition in Browser

**User Context**: Class Browser, defining a new class

**What Users Type**:
```smalltalk
Object subclass: #MyClass
    instanceVariableNames: 'x y z'
    classVariableNames: 'DefaultSize'
    poolDictionaries: ''
    category: 'My-Category'
```

**Grammar Entry Point**: ✅ Already exists
```antlr
classEntry: classHeader classBody EOF;
```

**Key Features**:
- No bang delimiters
- Complete class specification
- Category included as part of definition

## Recommended Additional Entry Points

### 5. Class Comment Entry

**User Context**: Class comment pane in browser

**What Users Type**: Plain documentation text
```
This class represents a 2D point.
Instance variables:
  x <Number> - the x coordinate
  y <Number> - the y coordinate
```

**Recommended Grammar Entry Point**: ⚠️ Needs addition
```antlr
commentEntry: COMMENT EOF;
// or for raw text:
commentTextEntry: (~EOF)* EOF;
```

### 6. Selector Entry

**User Context**: Method finder, selector browser, search dialogs

**What Users Type**: Just a selector
```smalltalk
at:put:
factorial
+
```

**Recommended Grammar Entry Point**: ⚠️ Needs addition
```antlr
selectorEntry: selector EOF;

selector: unarySelector
        | binarySelector  
        | keywordSelector
        ;

keywordSelector: (IDENTIFIER COLON)+;
```

### 7. Block Entry

**User Context**: Block editor, specialized block input

**What Users Type**: Just a block
```smalltalk
[:x :y | x + y > 100]
```

**Recommended Grammar Entry Point**: ⚠️ Needs addition
```antlr
blockEntry: block EOF;
```

### 8. Method Signature Entry

**User Context**: Method signature display, interface documentation

**What Users Type**: Just the method header
```smalltalk
at: index put: anObject
```

**Recommended Grammar Entry Point**: ⚠️ Needs addition
```antlr
methodSignatureEntry: methodHeader EOF;
```

### 9. Temporaries Declaration Entry

**User Context**: Code generation tools, refactoring tools

**What Users Type**: Just temporaries
```smalltalk
| x y temp |
```

**Recommended Grammar Entry Point**: ⚠️ Needs addition
```antlr
temporariesEntry: temporaries EOF;
```

## Complete Set of Entry Points for Interactive System

```antlr
// File-based entry
fileOut: chunk* EOF;                                    // File-out format

// Interactive entries (existing)
expressionEntry: expression EOF;                        // Single expressions
statementsEntry: statements EOF;                        // DoIt scenarios
methodEntry: methodHeader temporaries?                  // Browser methods
             primitiveDecl? methodBodyNoBang EOF;
classEntry: classHeader classBody EOF;                  // Class definitions

// Interactive entries (recommended additions)
commentEntry: COMMENT EOF;                             // Class comments
selectorEntry: selector EOF;                           // Selector search
blockEntry: block EOF;                                 // Block editing
methodSignatureEntry: methodHeader EOF;                // Signatures only
temporariesEntry: temporaries EOF;                     // Temp declarations
```

## Context Handling in Interactive Scenarios

### Implicit Context by Tool

| Tool | Implicit Context | Entry Point |
|------|-----------------|-------------|
| **Workspace** | None (UndefinedObject) | statementsEntry |
| **Class Browser (method)** | Selected class & category | methodEntry |
| **Class Browser (class)** | Selected category | classEntry |
| **Debugger** | Current method context | expressionEntry/statementsEntry |
| **Inspector** | Inspected object as self | expressionEntry |
| **Method Finder** | None | selectorEntry |
| **Class Comment** | Selected class | commentEntry |

### Variable Resolution by Context

```smalltalk
// In Workspace (no context):
x := 100  "Creates workspace temporary"

// In Inspector (object context):
x := 100  "Could be instVar or error"

// In Debugger (method context):  
x := 100  "Could be temp, arg, instVar, or error"

// In Browser method entry:
x := 100  "Must be temp, arg, or instVar"
```

## Implementation Strategy

### Phase 1: Core Interactive Support
Use existing entry points:
- expressionEntry
- statementsEntry  
- methodEntry
- classEntry

### Phase 2: Enhanced Tool Support
Add specialized entry points:
- selectorEntry (for search/navigation)
- commentEntry (for documentation)
- methodSignatureEntry (for interfaces)

### Phase 3: Advanced Tool Integration
Add remaining entry points:
- blockEntry (for block editors)
- temporariesEntry (for refactoring tools)

## Usage Examples

### Example 1: Workspace Evaluation
```csharp
// Parse workspace code
var input = "| x | x := 100. x sqrt";
var parser = CreateParser(input);
var tree = parser.statementsEntry();
```

### Example 2: Browser Method Entry
```csharp
// Parse method without file-out format
var input = @"initialize
    super initialize.
    count := 0";
var parser = CreateParser(input);
var tree = parser.methodEntry();
```

### Example 3: Selector Search
```csharp
// Parse just a selector
var input = "at:put:";
var parser = CreateParser(input);
var tree = parser.selectorEntry(); // Needs to be added
```

## Benefits of Multiple Entry Points

1. **Tool-Specific Parsing**: Each tool can use the appropriate entry point
2. **Better Error Messages**: Context-specific parsing gives clearer errors
3. **Performance**: Skip unnecessary parsing for simple inputs
4. **Cleaner AST**: Get exactly the tree structure needed
5. **IDE Integration**: Better support for incremental parsing

## Conclusion

A complete Smalltalk grammar needs multiple entry points to support the various interactive development scenarios. While the file-out format is crucial for code exchange, interactive development is where Smalltalk shines, and the grammar should reflect all these usage patterns.

The current grammar already provides good coverage with 5 entry points, but adding 5 more specialized entry points would make it comprehensive for all interactive Smalltalk development scenarios.