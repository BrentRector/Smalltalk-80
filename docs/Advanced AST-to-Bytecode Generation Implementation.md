# Smalltalk-80 Bytecode Compiler Implementation

This document describes the complete implementation of the Smalltalk-80 bytecode compiler that generates bytecode from ANTLR parse trees.

## Architecture Overview

The compiler uses a direct parse tree traversal approach rather than building an intermediate AST. It works directly with ANTLR4-generated parse trees from the Smalltalk-80 grammar.

### Key Components

1. **BytecodeCompiler**: Main compiler class that traverses parse trees and generates bytecode
2. **BytecodeEmitter**: Handles bytecode emission and literal frame management
3. **CompilationEnvironment**: Tracks variable scopes (arguments, temporaries, instance variables, class variables)
4. **LiteralCollector**: First-pass visitor that collects all literals for the literal frame

## Compilation Process

### Two-Pass Compilation

The compiler uses a two-pass approach for each method:

**Pass 1: Literal Collection**
```csharp
var visitor = new LiteralCollector(this, emitter, environment);
visitor.Visit(parseTree);
```
- Collects all literals (strings, symbols, numbers, arrays)
- Builds Associations for global/class variables
- Ensures literals are added to frame before code generation

**Pass 2: Code Generation**
```csharp
CompileMethod(methodContext);
```
- Generates actual bytecode
- References literals by index from pass 1
- Emits optimized bytecode patterns

## Expression Compilation

### Cascade Expressions

Cascades (`receiver msg1; msg2; msg3`) are optimized to evaluate the receiver only once:

```csharp
private void CompileCascadeExpr(CascadeExprContext expr)
{
    var messageExpr = expr.messageExpr();
    var messageParts = expr.messagePart();
    
    if (messageParts != null && messageParts.Length > 0)
    {
        // Compile receiver once
        CompileMessageExpr(messageExpr);
        
        // For each cascade part except the last:
        for (int i = 0; i < messageParts.Length - 1; i++)
        {
            emitter.Dup();                    // Duplicate receiver
            CompileMessagePart(messageParts[i]);
            emitter.Pop();                     // Pop result, keep receiver
        }
        
        // Last message - result remains on stack
        CompileMessagePart(messageParts[messageParts.Length - 1]);
    }
}
```

**Bytecode Pattern**:
- Push receiver
- For each message except last:
  - `136` (Duplicate top)
  - Send message
  - `135` (Pop stack)
- Send final message

### Assignment Expressions

Assignments correctly handle both local and global variables:

```csharp
private void CompileAssignmentExpr(AssignmentExprContext expr, bool leaveOnStack)
{
    var variable = expr.variable();
    if (variable != null && expr.ASSIGN() != null)
    {
        // Compile RHS first (must leave value on stack)
        CompileAssignmentExpr(expr.assignmentExpr(), leaveOnStack: true);
        
        // Store to variable
        StoreVariable(variable.GetText(), leaveOnStack);
    }
}
```

**Important**: The RHS is compiled first, then stored. Assignment targets are NOT pushed onto the stack.

### Block Expressions

Blocks are compiled as closures with proper capture semantics:

```csharp
private void CompileBlock(BlockContext block)
{
    environment.ScopeDepth++;
    
    // Count block arguments
    var blockArgs = block.blockArgList()?.blockArg() ?? Array.Empty<BlockArgContext>();
    
    // Emit blockCopy: with argument count
    emitter.EmitBlockCopy(blockArgs.Length);
    var blockCopyPos = emitter.CurrentIP - 1;
    
    // Jump over block body
    emitter.EmitJump(0); // Placeholder
    var blockStartIP = emitter.CurrentIP;
    
    // Compile block body
    CompileStatements(block.statements());
    
    // Block return
    emitter.ReturnTopFromBlock();
    
    // Patch jump distance
    var blockSize = emitter.CurrentIP - blockStartIP;
    emitter.PatchBlockSize(blockCopyPos, blockSize);
    
    environment.ScopeDepth--;
}
```

## Message Send Compilation

### Optimized Send Patterns

The compiler generates optimized bytecode for common message patterns:

```csharp
public void EmitSend(string selector, int argCount)
{
    // Special selectors (arithmetic, comparison)
    if (SpecialSelectors.TryGetValue(selector, out var special))
    {
        Emit((byte)special.bytecode);
        return;
    }
    
    var selectorIndex = AddLiteral(new Symbol(selector));
    
    // Optimized bytecode ranges
    if (argCount == 0 && selectorIndex <= 15)
        Emit((byte)(208 + selectorIndex));      // 208-223: 0 args
    else if (argCount == 1 && selectorIndex <= 15)
        Emit((byte)(224 + selectorIndex));      // 224-239: 1 arg
    else if (argCount == 2 && selectorIndex <= 15)
        Emit((byte)(240 + selectorIndex));      // 240-255: 2 args
    else if (argCount <= 7 && selectorIndex <= 31)
    {
        Emit(131);  // Single extended send
        Emit((byte)((argCount << 5) | selectorIndex));
    }
    else
    {
        Emit(132);  // Double extended send
        Emit((byte)selectorIndex);
        Emit((byte)argCount);
    }
}
```

### Special Selector Optimization

Common arithmetic and comparison selectors use single-byte bytecodes:

- `176-191`: Arithmetic selectors (+, -, <, >, <=, >=, =, ~=, *, /, \\, @, bitShift:, //, bitAnd:, bitOr:)
- `192-207`: Special selectors (at:, at:put:, size, next, nextPut:, atEnd, ==, class, blockCopy:, value, value:, do:, new, new:, x, y)

## Variable Access

### Variable Resolution Order

Variables are resolved in this order:
1. Special pseudo-variables (self, super, true, false, nil)
2. Arguments (stored in temporary locations 0 through argCount-1)
3. Temporaries (stored in temporary locations starting at argCount)
4. Instance variables
5. Class variables
6. Pool variables
7. Global variables

**Important**: In Smalltalk-80, arguments ARE temporary variables. They occupy the first slots of the temporary variable frame. A method with 3 arguments and 2 temporaries has a temporary frame of size 5, with arguments at indices 0-2 and local temporaries at indices 3-4.

```csharp
private void PushVariable(string name)
{
    // Pseudo-variables
    if (name == "self") { emitter.PushSelf(); return; }
    if (name == "true") { emitter.PushTrue(); return; }
    if (name == "false") { emitter.PushFalse(); return; }
    if (name == "nil") { emitter.PushNil(); return; }
    
    // Arguments are stored in temporary locations starting at index 0
    if (environment.Arguments.TryGetValue(name, out var argIndex))
    {
        // Arguments use temporary location bytecodes (16-31 for indices 0-15)
        emitter.PushTemporary(argIndex);
        return;
    }
    
    // Temporaries
    if (environment.Temporaries.TryGetValue(name, out var tempIndex))
    {
        emitter.PushTemporary(tempIndex);
    }
    
    // Instance variables
    if (environment.InstanceVariables.TryGetValue(name, out var instIndex))
    {
        emitter.PushReceiverVariable(instIndex);
    }
    
    // Class/Pool/Global variables use Associations
    emitter.PushLiteralVariable(name);
}
```

## Literal Frame Management

### Array Literal Handling

Array literals are properly parsed as arrays of elements:

```csharp
private object ProcessArrayElement(ArrayElementContext element)
{
    if (element.literal() != null)
    {
        var lit = element.literal();
        if (lit.numberLiteral() != null)
            return ParseNumber(lit.numberLiteral().GetText());
        if (lit.stringLiteral() != null)
            return ParseString(lit.stringLiteral().GetText());
        // ... handle other literal types
    }
    else if (element.symbolName() != null)
    {
        return new Symbol(element.symbolName().GetText());
    }
    else if (element.arrayLiteral() != null)
    {
        // Recursive array handling
        var elements = new List<object>();
        foreach (var el in element.arrayLiteral().arrayElement())
            elements.Add(ProcessArrayElement(el));
        return elements.ToArray();
    }
}
```

### Association Creation

Global and class variables are stored as Associations in the literal frame:

```csharp
public void PushLiteralVariable(string name)
{
    // Creates Association(name, null) for runtime resolution
    var index = AddLiteral(new Association(name, null));
    if (index <= 31)
        Emit((byte)(64 + index));  // 64-95: Push literal variable
    else
    {
        Emit(128);  // Extended push
        Emit((byte)(0xC0 | (index & 0x3F)));  // Literal variable type
    }
}
```

## Critical Bug Fixes Implemented

### 1. Array Literal Parsing
**Problem**: Arrays like `#(1 2 3)` were concatenated to "#(123)"
**Solution**: Iterate through parse tree elements instead of using GetText()

### 2. Message Send Argument Count
**Problem**: Messages with 3+ arguments used wrong bytecode format
**Solution**: Use extended send bytecode 131/132 for 3+ arguments

### 3. Assignment Compilation
**Problem**: Assignment targets were being pushed onto stack
**Solution**: Only compile RHS, then store to variable

### 4. Double Extended Send Format
**Problem**: Byte order was reversed (argCount, selector)
**Solution**: Corrected to (selector, argCount) per Blue Book spec

### 5. Method Argument Access
**Problem**: Arguments were being compiled to bytecode 48-63 (push literal constant)
**Solution**: Arguments are temporary variables at indices 0 through argCount-1, use bytecode 16-31 (push temporary)

## JSON Output Format

The compiler generates JSON with this structure:

```json
{
  "bytecode": [65, 35, 35, 187, ...],
  "literals": [
    "Association(key: #ThumbsUp, value: null)",
    "#extent:fromArray:offset:",
    16,
    [16, 104, 72, 120, ...]
  ],
  "numArgs": 0,
  "numTemps": 2,
  "stackDepth": 4,
  "primitive": 0,
  "source": "original source code"
}
```

### Literal Conversion

The JSON serializer properly handles different literal types:

```csharp
private object[] ConvertLiterals(object[] literals)
{
    var result = new List<object>();
    foreach (var literal in literals)
    {
        if (literal is Symbol sym)
            result.Add("#" + sym.Name);
        else if (literal is Association assoc)
            result.Add($"Association(key: #{assoc.Key}, value: {assoc.Value ?? "null"})");
        else if (literal is object[] array)
        {
            // Recursively convert array elements
            var arrayElements = new List<object>();
            foreach (var element in array)
            {
                var converted = ConvertLiterals(new[] { element });
                arrayElements.Add(converted[0]);
            }
            result.Add(arrayElements);
        }
        else
            result.Add(literal);
    }
    return result.ToArray();
}
```

## Performance Characteristics

- **Cascade Compilation**: O(1) receiver evaluation, O(n) for n messages
- **Literal Collection**: O(n) parse tree traversal
- **Variable Lookup**: O(1) hash table lookups
- **Send Optimization**: Constant-time special selector detection

## Testing and Validation

The compiler is validated through:
1. Compilation of the complete Smalltalk-80 system (4600+ methods)
2. Bytecode comparison with reference implementation
3. Execution testing in the interpreter
4. Specific test cases for each optimization

## Future Enhancements

Potential improvements to consider:
1. Inline caching hints for frequently called methods
2. Type inference for better optimization
3. Dead code elimination
4. Common subexpression elimination
5. Tail call optimization

## Compliance

The compiler strictly follows the Smalltalk-80 Blue Book specification for:
- Bytecode formats (see docs/Smalltalk-80 Bytecodes.md)
- Method layout and structure
- Variable scoping rules
- Message send semantics
- Block closure implementation