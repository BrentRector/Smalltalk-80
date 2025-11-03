# ANTLR4 Grammar Analysis for Tail Recursion Detection

**Document Version:** 1.0
**Date:** 2025-11-03
**Purpose:** Guide implementation of tail call optimization in Smalltalk-80 compiler
**Grammar File:** `Smalltalk80.Compiler/Grammar/Smalltalk80.g4`

---

## Executive Summary

This document analyzes the Smalltalk-80 ANTLR4 grammar to identify how to detect tail-recursive patterns for optimization. The grammar provides clean separation between method bodies, statement sequences, and message sends, making tail call detection straightforward through parse tree analysis.

**Key Finding:** Tail recursion detection can be implemented purely in semantic analysis (no grammar changes needed) by analyzing the parse tree structure during bytecode compilation.

---

## 1. Grammar Analysis: Method Body Structure

### 1.1 Method Definition Rules

**Rule Hierarchy:**
```
methodDef
├── methodHeader (selector + parameters)
├── temporaries? (local variables)
├── primitiveDecl? (e.g., <primitive: 60>)
└── methodBody / methodBodyNoBang (statements)
```

**Method Body Rules (Lines 271-282):**
```antlr
// Method body variants
methodBody
    : statements PERIOD* BANG    // Standard method
    | returnStatement BANG        // Short return form
    | BANG                       // Empty method
    ;

// Method body for browser entry (no bang)
methodBodyNoBang
    : statements PERIOD*         // Standard statements
    | returnStatement           // Return statement
    ;
```

**Key Observations:**
- Method bodies contain either `statements` or a single `returnStatement`
- `statements` rule handles sequences of statements (see section 1.2)
- Empty methods implicitly return `self` (handled by compiler, not grammar)

### 1.2 Statement Sequences

**Statement Rules (Lines 330-348):**
```antlr
// Statement sequences
statements
    : PERIOD* statement (PERIOD+ statement)* PERIOD*
    ;

// Individual statements
statement
    : returnStatement      // ^expr
    | expressionStatement  // expr
    ;

// Return statement
returnStatement
    : CARET expression PERIOD?
    ;

// Expression as statement
expressionStatement
    : expression
    ;
```

**Parse Tree Structure:**
```
statements
├── statement[0]              // First statement
├── PERIOD+
├── statement[1]              // Second statement
├── PERIOD+
├── ...
└── statement[N-1]            // LAST STATEMENT (tail position!)
```

**Critical Insight:** The LAST element in `statements.statement()` array is the tail position. This is where we detect tail calls.

---

## 2. Grammar Analysis: Message Send Structure

### 2.1 Expression Hierarchy (Lines 350-382)

**Precedence Chain:**
```
expression
└── assignmentExpr         (x := y)
    └── cascadeExpr        (obj msg1; msg2; msg3)
        └── messageExpr    (obj keyword: arg)
            └── binaryExpr (obj + 1)
                └── unaryExpr (obj size)
                    └── messageBase (receiver)
```

**Key Rules:**
```antlr
// Top-level expression with assignment
expression
    : assignmentExpr
    ;

// Cascade expression (multiple messages to same receiver)
cascadeExpr
    : messageExpr (SEMICOLON messagePart)*
    ;

// Complete message expression (may include keyword message)
messageExpr
    : binaryExpr keywordMessage?
    ;
```

### 2.2 Message Types (Lines 416-447)

**Three Types of Messages:**
```antlr
// Unary message: size, isEmpty
unaryMessage
    : unarySelector
    ;

// Binary message: + 5, = true
binaryMessage
    : binarySelector unaryExpr
    ;

// Keyword message: at: 5 put: 'hello'
keywordMessage
    : keywordPart+
    ;

// Keyword part with argument
keywordPart
    : IDENTIFIER COLON binaryExpr
    ;
```

### 2.3 Message Base (Receiver) - Lines 395-402

**Possible Receivers:**
```antlr
messageBase
    : literal              // 42, 'hello', #symbol
    | parenthesizedExpr    // (x + y)
    | block                // [:x | x + 1]
    | dynamicArray         // {1. 2. 3}
    | variable             // self, temp, instVar
    | negativeNumber       // -5
    ;
```

**For Tail Recursion Detection:**
- We care about `variable` → specifically when `variable.IDENTIFIER.GetText() == "self"`
- Must reject all other receiver types (literals, blocks, etc.)

---

## 3. Grammar Analysis: Block Structure

### 3.1 Block Rules (Lines 450-468)

**Block Definition:**
```antlr
block
    : LEFT_BRACKET blockArgs? blockTemps? statements? RIGHT_BRACKET
    ;

blockArgs
    : (COLON argumentName)+ PIPE?
    ;

blockTemps
    : PIPE temporaryName+ PIPE
    ;
```

**CRITICAL SAFETY RULE:**

**Tail calls inside blocks are NOT safe to optimize!**

**Reason:** Blocks can perform non-local returns (`^expr`) that escape to the method context. Optimizing tail calls inside blocks would break non-local return semantics.

**Example of UNSAFE pattern:**
```smalltalk
whileTrue: aBlock
    self value ifTrue: [
        aBlock value.
        self whileTrue: aBlock  ← Inside block - NOT tail recursive!
    ]
```

Even though this looks like a tail call, the `ifTrue:` block can have `^` returns that bypass the recursive call.

---

## 4. Detection Algorithm: Visitor Pattern Implementation

### 4.1 Current Compiler Architecture

**Relevant Files:**
- `BytecodeCompiler.cs` - Main compilation logic (lines 1225-1255)
- `FileInFormatAstBuilder.cs` - AST builder using visitor pattern
- Generated visitor: `Smalltalk80BaseVisitor<T>`, `Smalltalk80Visitor<T>`

**Current Compilation Flow:**
```
CompileMethodBody()
└── CompileStatements(statementsContext)
    └── for each statement[i] in statements.statement()
        └── CompileStatement(statement, isLast=(i==length-1))
            ├── if returnStatement → CompileReturnStatement()
            └── else → CompileExpression() + optional pop
```

### 4.2 Tail Call Detection Strategy

**Where to Insert Detection:**

In `CompileStatement()` method (line 1236), when `isLast == true`:

```csharp
private void CompileStatement(
    Smalltalk80Parser.StatementContext stmt,
    bool isLast,
    bool inBlock)
{
    // Existing code handles explicit returns
    var returnStmt = stmt.returnStatement();
    if (returnStmt != null)
    {
        CompileReturnStatement(returnStmt);
        return;
    }

    // NEW: Check for tail recursion if this is the last statement
    if (isLast && !inBlock)  // NOT inside block!
    {
        var exprStmt = stmt.expressionStatement();
        if (exprStmt != null)
        {
            var tailCallInfo = AnalyzeTailCall(exprStmt.expression());
            if (tailCallInfo != null)
            {
                // Emit optimized tail call bytecode
                EmitTailCall(tailCallInfo);
                return;
            }
        }
    }

    // Existing code for normal statement compilation
    CompileExpression(stmt.expressionStatement().expression());
    if (!isLast)
    {
        emitter.Pop(); // Discard intermediate values
    }
}
```

### 4.3 Tail Call Analysis Algorithm

**Pseudo-code for `AnalyzeTailCall()`:**

```csharp
class TailCallInfo
{
    public string Selector { get; set; }      // e.g., "whileTrue:"
    public List<Expression> Arguments { get; set; }
}

TailCallInfo? AnalyzeTailCall(ExpressionContext expr)
{
    // Step 1: Strip assignments (x := self foo is NOT tail recursive)
    if (expr is AssignmentExprContext)
        return null;  // Assignment before call - NOT tail recursive

    // Step 2: Handle cascades (only last message in cascade can be tail)
    var cascadeExpr = expr.cascadeExpr();
    if (cascadeExpr.SEMICOLON().Length > 0)
        return null;  // Multiple messages - complex case, skip for now

    // Step 3: Get the message expression
    var messageExpr = cascadeExpr.messageExpr();

    // Step 4: Check if receiver is 'self'
    var receiver = GetReceiver(messageExpr);
    if (receiver == null || !IsSelfVariable(receiver))
        return null;  // Not sent to self

    // Step 5: Extract selector and arguments
    var selector = GetSelector(messageExpr);
    var arguments = GetArguments(messageExpr);

    // Step 6: Verify selector matches current method
    if (selector != _currentMethodSelector)
        return null;  // Different method - not recursive

    // Success! This is a tail-recursive call
    return new TailCallInfo
    {
        Selector = selector,
        Arguments = arguments
    };
}
```

### 4.4 Helper Functions

**Get Receiver from Message Expression:**

```csharp
MessageBaseContext? GetReceiver(MessageExprContext messageExpr)
{
    // Navigate: messageExpr → binaryExpr → unaryExpr → messageBase
    var binaryExpr = messageExpr.binaryExpr();
    if (binaryExpr == null) return null;

    var unaryExpr = binaryExpr.unaryExpr(0);  // First unary expr is receiver
    if (unaryExpr == null) return null;

    return unaryExpr.messageBase();
}

bool IsSelfVariable(MessageBaseContext messageBase)
{
    var variable = messageBase.variable();
    if (variable == null) return false;

    var identifier = variable.IDENTIFIER();
    return identifier?.GetText() == "self";
}
```

**Extract Selector:**

```csharp
string GetSelector(MessageExprContext messageExpr)
{
    // Check for keyword message: whileTrue:
    var keywordMsg = messageExpr.keywordMessage();
    if (keywordMsg != null)
    {
        var parts = keywordMsg.keywordPart();
        return string.Join("", parts.Select(p =>
            p.IDENTIFIER().GetText() + ":"));
    }

    // Check for binary message: +, -, etc.
    var binaryExpr = messageExpr.binaryExpr();
    var binaryMsgs = binaryExpr.binaryMessage();
    if (binaryMsgs.Length > 0)
    {
        var lastBinary = binaryMsgs[^1];  // Last binary in chain
        return lastBinary.binarySelector().GetText();
    }

    // Check for unary message: size, isEmpty
    var unaryExpr = binaryExpr.unaryExpr(0);
    var unaryMsgs = unaryExpr.unaryMessage();
    if (unaryMsgs.Length > 0)
    {
        var lastUnary = unaryMsgs[^1];  // Last unary in chain
        return lastUnary.unarySelector().GetText();
    }

    return null;  // No message found
}
```

**Extract Arguments:**

```csharp
List<Expression> GetArguments(MessageExprContext messageExpr)
{
    var arguments = new List<Expression>();

    // Keyword message arguments
    var keywordMsg = messageExpr.keywordMessage();
    if (keywordMsg != null)
    {
        foreach (var part in keywordMsg.keywordPart())
        {
            arguments.Add(part.binaryExpr());
        }
        return arguments;
    }

    // Binary message argument
    var binaryExpr = messageExpr.binaryExpr();
    var binaryMsgs = binaryExpr.binaryMessage();
    if (binaryMsgs.Length > 0)
    {
        var lastBinary = binaryMsgs[^1];
        arguments.Add(lastBinary.unaryExpr());
        return arguments;
    }

    // Unary message has no arguments
    return arguments;
}
```

---

## 5. Parse Tree Examples

### 5.1 Safe Tail Recursive Pattern

**Smalltalk Code:**
```smalltalk
whileTrue: aBlock
    self value ifTrue: [
        aBlock value.
        self whileTrue: aBlock
    ]
```

**Parse Tree Structure:**
```
methodDef
├── methodHeader: "whileTrue:"
│   └── keywordPart: "whileTrue:" "aBlock"
└── methodBody
    └── statements
        └── statement (LAST, isLast=true)
            └── expressionStatement
                └── expression
                    └── cascadeExpr
                        └── messageExpr (self value ifTrue: [...])
                            ├── receiver: self
                            └── keywordMessage: "ifTrue:"
                                └── argument: block[...]
```

**Analysis Result:**
- Last statement: `self value ifTrue: [...]`
- Receiver: `self`
- Selector: `ifTrue:`
- **NOT tail recursive** (selector != "whileTrue:")
- **Deep inside block:** The recursive call `self whileTrue: aBlock` is inside the block argument to `ifTrue:`
- **Conclusion:** This is NOT a tail call at the method level

### 5.2 True Tail Recursive Pattern

**Smalltalk Code:**
```smalltalk
factorial: n
    n = 0 ifTrue: [^1].
    self factorial: (n - 1)
```

**Parse Tree Structure:**
```
methodBody
└── statements
    ├── statement[0]: n = 0 ifTrue: [^1]
    │   └── expressionStatement
    └── statement[1]: self factorial: (n - 1)  ← LAST (isLast=true)
        └── expressionStatement
            └── expression
                └── cascadeExpr
                    └── messageExpr
                        ├── binaryExpr
                        │   └── unaryExpr
                        │       └── messageBase
                        │           └── variable: "self" ✓
                        └── keywordMessage: "factorial:"
                            └── keywordPart
                                ├── IDENTIFIER: "factorial"
                                ├── COLON: ":"
                                └── binaryExpr: (n - 1)
```

**Analysis Result:**
- Last statement: `self factorial: (n - 1)`
- Receiver: `self` ✓
- Selector: `factorial:` ✓
- Current method: `factorial:` ✓
- NOT inside block ✓
- **Conclusion:** Valid tail call!

### 5.3 Unsafe Pattern: Assignment After Call

**Smalltalk Code:**
```smalltalk
whileTrue: aBlock
    | result |
    result := self value.
    self whileTrue: aBlock
```

**Parse Tree Structure:**
```
statements
├── statement[0]: result := self value
│   └── expressionStatement
│       └── expression
│           └── assignmentExpr ← Assignment!
└── statement[1]: self whileTrue: aBlock ← LAST
    └── expressionStatement
        └── expression (NOT assignmentExpr) ✓
```

**Analysis Result:**
- Last statement: `self whileTrue: aBlock`
- Receiver: `self` ✓
- Selector: `whileTrue:` ✓
- **BUT:** There's a prior statement (assignment)
- **Conclusion:** Valid tail call (prior assignments are OK, they completed before the call)

### 5.4 Unsafe Pattern: Call Inside Block

**Smalltalk Code:**
```smalltalk
whileTrue: aBlock
    [self whileTrue: aBlock] value
```

**Parse Tree Structure:**
```
statements
└── statement (LAST, isLast=true, inBlock=false)
    └── expressionStatement
        └── expression
            └── messageExpr: [...] value
                ├── receiver: block[...]
                │   └── block (inBlock=true!) ← Recursive call here
                │       └── statements
                │           └── statement
                │               └── expressionStatement
                │                   └── expression: self whileTrue: aBlock
                └── unaryMessage: "value"
```

**Analysis Result:**
- Last statement at method level: `[...] value`
- Receiver: Block (not self) ✗
- **Recursive call is INSIDE the block**
- **Conclusion:** NOT a tail call (call is nested inside block)

### 5.5 Unsafe Pattern: Code After Call

**Smalltalk Code:**
```smalltalk
whileTrue: aBlock
    self value ifTrue: [
        aBlock value.
        self whileTrue: aBlock.
        1 + 2  ← Code after call!
    ]
```

**Parse Tree Structure:**
```
methodBody
└── statements
    └── statement (LAST)
        └── expression: self value ifTrue: [...]
            └── argument: block
                └── statements
                    ├── statement[0]: aBlock value
                    ├── statement[1]: self whileTrue: aBlock
                    └── statement[2]: 1 + 2 ← NOT LAST in block!
```

**Analysis Result:**
- The recursive call `self whileTrue: aBlock` is statement[1] in the block
- There's a statement[2] after it: `1 + 2`
- **Conclusion:** NOT a tail call (code executes after the call)

---

## 6. Implementation Strategy

### 6.1 Where to Implement

**File:** `Smalltalk80.Compiler/BytecodeCompiler.cs`

**Integration Point:** `CompileStatement()` method (line 1236)

**New Class:** `TailRecursionAnalyzer`

**Architecture:**
```
BytecodeCompiler
├── CompileMethod()
│   ├── Set _currentMethodSelector
│   └── CompileStatements()
│       └── CompileStatement(stmt, isLast, inBlock)
│           └── if isLast && !inBlock
│               └── TailRecursionAnalyzer.Analyze(expr, _currentMethodSelector)
│                   └── returns TailCallInfo? or null
└── TailRecursionAnalyzer (new class)
    ├── Analyze(expression, currentSelector) → TailCallInfo?
    ├── GetReceiver(messageExpr) → MessageBaseContext?
    ├── IsSelfVariable(messageBase) → bool
    ├── GetSelector(messageExpr) → string?
    └── GetArguments(messageExpr) → List<ExpressionContext>
```

### 6.2 Context Information Needed

**Required State:**
1. **Current method selector** - To compare against recursive call
2. **Block nesting depth** - To reject calls inside blocks
3. **Current statement index** - Already tracked via `isLast` parameter

**Already Available:**
- `isLast` parameter in `CompileStatement()` (line 1236)
- `inBlock` parameter in `CompileStatement()` (line 1236)

**Need to Add:**
- `_currentMethodSelector` field in `BytecodeCompiler` class
- Set this field at the start of `CompileMethod()` based on method header

### 6.3 Bytecode Emission Changes

**Current Bytecode for Message Send:**
```
PUSH_RECEIVER
PUSH_ARG_0
PUSH_ARG_1
SEND_SELECTOR #whileTrue: (2 args)
RETURN_STACK_TOP
```

**Optimized Tail Call Bytecode:**
```
PUSH_ARG_0
PUSH_ARG_1
POP_INTO_ARG_0   ← Overwrite current arguments
POP_INTO_ARG_1
JUMP_TO_START    ← Jump to method start (offset 0)
```

**Alternative (if JUMP not supported):**
Keep existing approach but add metadata for future VM optimization:
```
// Add flag to CompiledMethod header
header |= TAIL_RECURSIVE_FLAG

// VM can detect this and optimize at runtime
```

### 6.4 Validation and Testing

**Test Cases to Implement:**

1. **Basic tail recursion:**
   ```smalltalk
   factorial: n
       n = 0 ifTrue: [^1].
       self factorial: (n - 1)
   ```
   Expected: Tail call detected ✓

2. **Non-tail recursion (value used):**
   ```smalltalk
   factorial: n
       n = 0 ifTrue: [^1].
       n * (self factorial: (n - 1))
   ```
   Expected: Tail call NOT detected ✗ (result is multiplied)

3. **Tail call inside block (unsafe):**
   ```smalltalk
   whileTrue: aBlock
       self value ifTrue: [self whileTrue: aBlock]
   ```
   Expected: Tail call NOT detected ✗ (inside block)

4. **Different selector (not recursive):**
   ```smalltalk
   foo
       self bar  ← Different selector
   ```
   Expected: Tail call NOT detected ✗ (not recursive)

5. **Receiver not self:**
   ```smalltalk
   foo
       other whileTrue: []  ← Receiver is 'other', not 'self'
   ```
   Expected: Tail call NOT detected ✗ (wrong receiver)

---

## 7. Grammar Change Assessment

### 7.1 Required Changes: NONE

The existing grammar provides sufficient structure for tail call detection:

✓ **Statement sequences:** `statements` rule clearly identifies last statement
✓ **Message structure:** `messageExpr` rule provides access to receiver and selector
✓ **Block detection:** `block` rule allows tracking nesting depth
✓ **Assignment detection:** `assignmentExpr` rule distinguishes assignments

### 7.2 Optional Enhancements (Future)

**If we want explicit tail call annotations:**

```antlr
// Add tail call annotation syntax
statement
    : TAIL_CALL_ANNOTATION? returnStatement
    | TAIL_CALL_ANNOTATION? expressionStatement
    ;

TAIL_CALL_ANNOTATION: '@tailcall';
```

**Example usage:**
```smalltalk
factorial: n
    n = 0 ifTrue: [^1].
    @tailcall self factorial: (n - 1)  ← Explicit annotation
```

**Benefits:**
- Programmer intent is explicit
- Compiler can warn if annotation is invalid
- Makes code self-documenting

**Drawbacks:**
- Non-standard Smalltalk syntax
- Requires education/documentation
- May confuse developers familiar with standard Smalltalk

**Recommendation:** Implement automatic detection first, consider annotations as Phase 2 feature.

---

## 8. Edge Cases and Limitations

### 8.1 Multiple Message Sends (Cascade)

**Code:**
```smalltalk
foo
    self msg1; msg2; msg3  ← Which is tail call?
```

**Analysis:**
- Only the LAST message (`msg3`) can be a tail call
- Intermediate messages (`msg1`, `msg2`) have side effects
- Detection must ignore cascades or only check the final message

**Recommendation:** For Phase 1, reject cascades entirely (return `null` from analyzer).

### 8.2 Conditional Returns

**Code:**
```smalltalk
factorial: n
    n = 0 ifTrue: [^1].
    self factorial: (n - 1)  ← Tail call after conditional
```

**Analysis:**
- The `ifTrue:` block contains a return that might execute
- If the condition is false, control reaches the tail call
- This IS a valid tail call (the prior statement is independent)

**Recommendation:** This works correctly with the detection algorithm (last statement is still the tail call).

### 8.3 Nested Blocks with Non-Local Returns

**Code:**
```smalltalk
whileTrue: aBlock
    [^self whileTrue: aBlock] value  ← Non-local return!
```

**Analysis:**
- The `^` causes return from METHOD, not block
- The `value` message never executes
- This is a tail call at the semantic level, but not syntactically

**Recommendation:** Reject this pattern (too complex for Phase 1). The `inBlock` check already prevents this.

### 8.4 Super Sends

**Code:**
```smalltalk
foo: arg
    super foo: arg  ← Super send, not self send
```

**Analysis:**
- Receiver is `super`, not `self`
- Selector matches current method
- This IS semantically tail recursive (goes to superclass implementation)

**Recommendation:** For Phase 1, only handle `self` sends. Super sends can be added in Phase 2.

### 8.5 Implicit Returns

**Code:**
```smalltalk
factorial: n
    n = 0 ifTrue: [^1]
    ← Implicit: ^(self factorial: (n - 1))
```

**Analysis:**
- Missing explicit `^` on last statement
- Compiler automatically inserts `^` for last statement value
- This IS a tail call (the implicit return wraps the recursive call)

**Recommendation:** This works correctly with the detection algorithm. The `isLast` flag identifies this statement as tail position.

---

## 9. Recommendations for Implementation

### Phase 1: Basic Detection (THIS DOCUMENT)

**Goals:**
- Detect simple tail recursion: `self selector: args` as last statement
- Reject all edge cases (blocks, cascades, super sends, etc.)
- No bytecode optimization yet - just flag methods as tail-recursive

**Deliverables:**
1. `TailRecursionAnalyzer` class with detection logic
2. Integration into `CompileStatement()` method
3. Add `IsTailRecursive` flag to `CompiledMethod` metadata
4. Unit tests for 10 test cases (5 valid, 5 invalid)

**Success Criteria:**
- Correctly identifies `factorial: n` as tail recursive
- Correctly rejects all unsafe patterns
- No false positives (flags non-tail calls as tail calls)
- No false negatives (misses valid tail calls)

### Phase 2: Bytecode Optimization

**Goals:**
- Emit optimized bytecode for detected tail calls
- Replace `SEND + RETURN` with `OVERWRITE_ARGS + JUMP`
- Measure performance improvement on `factorial`, `fibonacci`, `whileTrue:`

**Deliverables:**
1. New bytecode instructions: `POP_INTO_ARG`, `JUMP_TO_OFFSET`
2. Modified bytecode emitter for tail calls
3. VM support for new instructions
4. Performance benchmarks

### Phase 3: Advanced Patterns

**Goals:**
- Support super sends: `super foo: arg`
- Support cascades: `self msg1; msg2; msg3` (optimize last)
- Support mutual recursion: `foo` → `bar` → `foo`

**Deliverables:**
1. Enhanced detection algorithm
2. Call graph analysis for mutual recursion
3. Extended test suite

---

## 10. Code Snippets for Implementation

### 10.1 TailRecursionAnalyzer Class (Skeleton)

```csharp
namespace Smalltalk80.Compiler.Analysis
{
    /// <summary>
    /// Analyzes parse trees to detect tail-recursive patterns.
    /// </summary>
    public class TailRecursionAnalyzer
    {
        /// <summary>
        /// Information about a detected tail call.
        /// </summary>
        public class TailCallInfo
        {
            public string Selector { get; set; }
            public List<Smalltalk80Parser.BinaryExprContext> Arguments { get; set; }
            public Smalltalk80Parser.MessageExprContext MessageExpr { get; set; }
        }

        /// <summary>
        /// Analyzes an expression to determine if it's a tail-recursive call.
        /// </summary>
        /// <param name="expr">Expression in tail position (last statement)</param>
        /// <param name="currentMethodSelector">Selector of the enclosing method</param>
        /// <returns>TailCallInfo if tail recursive, null otherwise</returns>
        public static TailCallInfo? Analyze(
            Smalltalk80Parser.ExpressionContext expr,
            string currentMethodSelector)
        {
            // Step 1: Reject assignments
            var assignExpr = expr as Smalltalk80Parser.AssignmentExprContext;
            if (assignExpr?.ASSIGN() != null)
                return null;

            // Step 2: Get cascade expression
            var cascadeExpr = GetCascadeExpr(expr);
            if (cascadeExpr == null)
                return null;

            // Step 3: Reject multi-message cascades (for Phase 1)
            if (cascadeExpr.SEMICOLON().Length > 0)
                return null;

            // Step 4: Get message expression
            var messageExpr = cascadeExpr.messageExpr();
            if (messageExpr == null)
                return null;

            // Step 5: Check receiver is 'self'
            if (!IsSelfReceiver(messageExpr))
                return null;

            // Step 6: Extract selector
            var selector = GetSelector(messageExpr);
            if (selector == null || selector != currentMethodSelector)
                return null;

            // Step 7: Extract arguments
            var arguments = GetArguments(messageExpr);

            // Success!
            return new TailCallInfo
            {
                Selector = selector,
                Arguments = arguments,
                MessageExpr = messageExpr
            };
        }

        private static Smalltalk80Parser.CascadeExprContext? GetCascadeExpr(
            Smalltalk80Parser.ExpressionContext expr)
        {
            // Navigate: expression → assignmentExpr → cascadeExpr
            if (expr is Smalltalk80Parser.AssignmentExprContext assignExpr)
            {
                return assignExpr.cascadeExpr();
            }
            return null;
        }

        private static bool IsSelfReceiver(
            Smalltalk80Parser.MessageExprContext messageExpr)
        {
            // Navigate: messageExpr → binaryExpr → unaryExpr → messageBase
            var binaryExpr = messageExpr.binaryExpr();
            if (binaryExpr == null) return false;

            var unaryExprs = binaryExpr.unaryExpr();
            if (unaryExprs.Length == 0) return false;

            var messageBase = unaryExprs[0].messageBase();
            if (messageBase == null) return false;

            var variable = messageBase.variable();
            if (variable == null) return false;

            return variable.IDENTIFIER()?.GetText() == "self";
        }

        private static string? GetSelector(
            Smalltalk80Parser.MessageExprContext messageExpr)
        {
            // Check for keyword message
            var keywordMsg = messageExpr.keywordMessage();
            if (keywordMsg != null)
            {
                var parts = keywordMsg.keywordPart();
                return string.Join("", parts.Select(p =>
                    p.IDENTIFIER().GetText() + ":"));
            }

            // Check for binary message
            var binaryExpr = messageExpr.binaryExpr();
            var binaryMsgs = binaryExpr.binaryMessage();
            if (binaryMsgs.Length > 0)
            {
                return binaryMsgs[^1].binarySelector().GetText();
            }

            // Check for unary message
            var unaryExprs = binaryExpr.unaryExpr();
            if (unaryExprs.Length > 0)
            {
                var unaryMsgs = unaryExprs[0].unaryMessage();
                if (unaryMsgs.Length > 0)
                {
                    return unaryMsgs[^1].unarySelector().GetText();
                }
            }

            return null;
        }

        private static List<Smalltalk80Parser.BinaryExprContext> GetArguments(
            Smalltalk80Parser.MessageExprContext messageExpr)
        {
            var arguments = new List<Smalltalk80Parser.BinaryExprContext>();

            // Keyword message arguments
            var keywordMsg = messageExpr.keywordMessage();
            if (keywordMsg != null)
            {
                foreach (var part in keywordMsg.keywordPart())
                {
                    arguments.Add(part.binaryExpr());
                }
            }

            // Binary/unary messages have arguments embedded in binaryExpr chain
            // (handled by bytecode compiler, not extracted here)

            return arguments;
        }
    }
}
```

### 10.2 Integration into BytecodeCompiler

```csharp
// In BytecodeCompiler class

private string _currentMethodSelector;  // Add this field

public MethodNode CompileMethod(/* ... */)
{
    // Extract selector from method header
    _currentMethodSelector = ExtractSelector(header);

    // ... existing method compilation code ...

    CompileMethodBody(body);

    // ... rest of method ...
}

private void CompileStatement(
    Smalltalk80Parser.StatementContext stmt,
    bool isLast,
    bool inBlock)
{
    // Existing return statement handling
    var returnStmt = stmt.returnStatement();
    if (returnStmt != null)
    {
        CompileReturnStatement(returnStmt);
        return;
    }

    // NEW: Tail call detection
    if (isLast && !inBlock)
    {
        var exprStmt = stmt.expressionStatement();
        if (exprStmt != null)
        {
            var tailCallInfo = TailRecursionAnalyzer.Analyze(
                exprStmt.expression(),
                _currentMethodSelector);

            if (tailCallInfo != null)
            {
                Console.WriteLine(
                    $"[TAIL CALL] Detected in {_currentMethodSelector}: " +
                    $"{tailCallInfo.Selector}");

                // For Phase 1: Just set a flag, don't optimize yet
                _currentMethodNode.IsTailRecursive = true;

                // Still compile normally for now
                CompileExpression(exprStmt.expression());
                return;
            }
        }
    }

    // Existing expression statement handling
    var expr = stmt.expressionStatement();
    if (expr != null)
    {
        CompileExpression(expr.expression());
        if (!isLast)
        {
            emitter.Pop(); // Discard intermediate values
        }
    }
}

private string ExtractSelector(Smalltalk80Parser.MethodHeaderContext header)
{
    // Unary selector
    if (header.unarySelector() != null)
        return header.unarySelector().GetText();

    // Binary selector
    if (header.binarySelector() != null)
        return header.binarySelector().GetText();

    // Keyword selector
    var keywordParts = header.identifierColon();
    if (keywordParts != null && keywordParts.Length > 0)
    {
        return string.Join("", keywordParts.Select(p => p.GetText()));
    }

    return "<unknown>";
}
```

---

## 11. Conclusion

The Smalltalk-80 ANTLR4 grammar provides clean separation of method bodies, statement sequences, and message sends, making tail recursion detection straightforward:

**Key Findings:**
1. **No grammar changes needed** - Detection can be implemented in semantic analysis
2. **Clear tail position** - Last element in `statements.statement()` array
3. **Easy receiver check** - Navigate `messageExpr → binaryExpr → unaryExpr → messageBase → variable`
4. **Selector extraction** - Handle three message types (unary, binary, keyword)
5. **Block safety** - `inBlock` parameter already tracks nesting

**Implementation Path:**
1. **Phase 1:** Detection only - flag methods as tail-recursive
2. **Phase 2:** Optimization - emit `JUMP` bytecode instead of `SEND`
3. **Phase 3:** Advanced patterns - super sends, cascades, mutual recursion

**Next Steps:**
1. Implement `TailRecursionAnalyzer` class (Section 10.1)
2. Integrate into `BytecodeCompiler.CompileStatement()` (Section 10.2)
3. Add unit tests for edge cases (Section 8)
4. Measure baseline performance before optimization

---

**Document Status:** Complete
**Ready for Implementation:** Yes
**Estimated Implementation Time:** 4-6 hours for Phase 1
