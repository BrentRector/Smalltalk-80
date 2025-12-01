# Cascade Compilation Refactoring Proposal

## Problem Statement

The current cascade compilation approach passes boolean flags (`isCascadeBase`, `totalCascadeMessages`) through multiple nested method calls (`CompileMessageExpr` → `CompileBinaryExpr` → `CompileUnaryExpr`). This creates tight coupling and makes the code difficult to understand and maintain.

## Current Issues

1. **Flag Passing Pollution**: Flags propagate through 3+ method layers
2. **Complex Conditional Logic**: Methods have special-case logic for cascade vs non-cascade
3. **Maintainability**: Hard to understand where DUP should be inserted
4. **Fragility**: Changes to one message type affect all others

## Proposed Solution: Dedicated Helper Methods

**Core Principle**: Create focused helper methods that decompose message expressions into "receiver" and "final message" components. Each helper method has a single, clear responsibility.

### Architecture Overview

```
CompileCascadeExpr (orchestrates)
    ↓
    ├── CompileCascadeReceiver (compiles all but last message)
    │   └── Delegates to ExceptLast helpers
    │
    └── CompileFinalMessage (compiles only the last message)
        └── Delegates to FinalMessage helpers
```

### Method Decomposition Pattern

For each expression level (MessageExpr, BinaryExpr, UnaryExpr):
- **ExceptLast method**: Compiles everything *except* the final message
- **FinalMessage method**: Compiles *only* the final message

This creates a clean separation that mirrors the cascade semantics:
- **Cascade receiver** = result of all but last message
- **Cascade target** = final message applied after DUP

## Benefits

### 1. No Flag Passing
```csharp
// OLD (messy)
CompileMessageExpr(expr, isCascadeBase: true, totalCascadeMessages: 5);

// NEW (clean)
CompileCascadeReceiver(expr);  // Clear intent
CompileFinalMessage(expr);     // Clear intent
```

### 2. Single Responsibility
Each method does ONE thing:
- `CompileCascadeReceiver`: Generates bytecode for cascade receiver
- `CompileFinalMessage`: Generates bytecode for final message
- `CompileMessageExpr`: Normal message compilation (no cascade logic)

### 3. Easier Testing
Helper methods can be tested independently:
```csharp
[Test]
public void TestCascadeReceiver_UnaryMessages()
{
    // "obj foo bar; baz" → cascade receiver is "obj foo"
    var code = CompileCascadeReceiver(Parse("obj foo bar"));
    Assert.That(code, Is.EqualTo([PUSH obj, SEND foo]));
}
```

### 4. Clear Semantics
Method names express intent:
- `ExceptLast` → "all messages except the final one"
- `FinalMessage` → "only the last message"
- No mental overhead to understand flags

## Implementation Guide

### Phase 1: Add Helper Methods

Create the new helper methods alongside existing code (no breaking changes):

```csharp
private void CompileCascadeReceiver(MessageExprContext expr) { }
private void CompileFinalMessage(MessageExprContext expr) { }
private void CompileBinaryExprExceptLast(BinaryExprContext expr) { }
private void CompileFinalBinaryMessage(BinaryExprContext expr) { }
private void CompileUnaryExprExceptLast(UnaryExprContext expr) { }
private void CompileFinalUnaryMessage(UnaryExprContext expr) { }
```

### Phase 2: Refactor CompileCascadeExpr

Replace flag-passing logic with helper method calls:

```csharp
private void CompileCascadeExpr(CascadeExprContext expr)
{
    var messageExpr = expr.messageExpr();
    var messageParts = expr.messagePart();

    if (messageParts is null || messageParts.Length == 0)
    {
        CompileMessageExpr(messageExpr);  // No cascade
        return;
    }

    // Cascade compilation:
    CompileCascadeReceiver(messageExpr);  // All but last message
    emitter.Dup();                        // Duplicate receiver
    CompileFinalMessage(messageExpr);     // Last message

    // Process cascade parts
    for (int i = 0; i < messageParts.Length; i++)
    {
        bool isLast = i == messageParts.Length - 1;
        emitter.Pop();                    // Pop previous result
        if (!isLast) emitter.Dup();       // Dup for next message
        CompileMessagePart(messageParts[i]);
    }
}
```

### Phase 3: Remove Flags

Remove `isCascadeBase` and `totalCascadeMessages` parameters from:
- `CompileMessageExpr`
- `CompileBinaryExpr`
- `CompileUnaryExpr`

All cascade logic is now isolated in `CompileCascadeExpr` and its helpers.

## Example Walkthrough

### Input: `obj foo bar; baz`

**AST Structure:**
```
CascadeExpr
├── messageExpr: "obj foo bar"
│   └── binaryExpr
│       └── unaryExpr
│           ├── messageBase: "obj"
│           └── unaryMessages: ["foo", "bar"]
└── messageParts: ["baz"]
```

**Compilation Flow:**

1. **CompileCascadeReceiver(messageExpr)**
   - Calls `CompileBinaryExprExceptLast(binaryExpr)`
   - Calls `CompileUnaryExprExceptLast(unaryExpr)`
   - Emits: `PUSH obj, SEND foo`

2. **emitter.Dup()**
   - Emits: `DUP`

3. **CompileFinalMessage(messageExpr)**
   - Calls `CompileFinalBinaryMessage(binaryExpr)`
   - Calls `CompileFinalUnaryMessage(unaryExpr)`
   - Emits: `SEND bar`

4. **Cascade loop for "baz"**
   - Emits: `POP, SEND baz`

**Final Bytecode:**
```
PUSH obj
SEND foo
DUP
SEND bar
POP
SEND baz
```

## Modern C# 13 Features

### 1. Index from End Operator
```csharp
var lastMessage = unaryMessages[^1];  // Last element
```

### 2. Pattern Matching
```csharp
if (keywordMessage is not null)
{
    // Handle keyword case
}
```

### 3. Collection Expressions
```csharp
// Future: Could use for building message lists
ReadOnlySpan<Message> messages = [msg1, msg2, msg3];
```

### 4. Expression-Bodied Members
```csharp
private bool HasKeywordMessage(MessageExprContext expr) =>
    expr.keywordMessage() is not null;
```

### 5. XML Documentation
```csharp
/// <summary>
/// Compiles the cascade receiver (all messages except the last one).
/// </summary>
/// <param name="messageExpr">The message expression to decompose.</param>
/// <remarks>
/// For "obj foo bar; baz", compiles "obj foo" (everything before the last message).
/// </remarks>
private void CompileCascadeReceiver(MessageExprContext messageExpr)
```

## Migration Strategy

### Step 1: Add Tests (Safety Net)
Create tests for all cascade scenarios:
- Simple unary: `obj foo; bar`
- Multiple unary: `obj foo bar; baz`
- Binary: `obj + 1; + 2`
- Keyword: `obj at: 1; at: 2`
- Mixed: `obj foo at: 1; bar`

### Step 2: Implement Helpers
Add new helper methods without modifying existing code.

### Step 3: Switch CompileCascadeExpr
Modify only `CompileCascadeExpr` to use helpers.

### Step 4: Clean Up
Remove flag parameters and dead code from other methods.

### Step 5: Verify
Run all tests (unit tests + BlockContext regression test).

## Conclusion

This refactoring:
- **Eliminates** flag passing through multiple layers
- **Simplifies** each method to have a single responsibility
- **Improves** code readability and maintainability
- **Enables** easier testing and debugging
- **Uses** modern C# 13 patterns

The key insight: **Decompose the problem into two orthogonal operations** (compile receiver vs compile final message) rather than threading conditional logic through the entire compilation stack.
