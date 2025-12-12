# Conditional Compilation Implementation Guide

**Date**: 2025-12-12
**Prerequisite**: Read CONDITIONAL-COMPILATION-FOR-VALUE-VS-EFFECT-ANALYSIS.md first

## Implementation Overview

This guide provides detailed code changes to implement "for value" vs "for effect" bytecode generation in the C# compiler.

## File to Modify

**Location**: `/home/user/Smalltalk-80/Smalltalk80.Compiler/BytecodeCompiler.cs`

## Phase 1: Add forValue Parameter to Signatures

### Step 1.1: Modify Inlining Method Signatures

**Change TryInlineIfTrue** (line ~1523):
```csharp
// BEFORE:
private bool TryInlineIfTrue(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineIfTrue(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

**Change TryInlineIfFalse** (line ~1569):
```csharp
// BEFORE:
private bool TryInlineIfFalse(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineIfFalse(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

**Change TryInlineIfTrueIfFalse** (line ~1615):
```csharp
// BEFORE:
private bool TryInlineIfTrueIfFalse(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineIfTrueIfFalse(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

**Change TryInlineIfFalseIfTrue** (line ~1657):
```csharp
// BEFORE:
private bool TryInlineIfFalseIfTrue(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineIfFalseIfTrue(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

**Change TryInlineAnd** (line ~1699):
```csharp
// BEFORE:
private bool TryInlineAnd(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineAnd(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

**Change TryInlineOr** (line ~1733):
```csharp
// BEFORE:
private bool TryInlineOr(Smalltalk80Parser.KeywordPartContext[] keywordParts)

// AFTER:
private bool TryInlineOr(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
```

### Step 1.2: Modify TryInlineConditional

**Location**: Line ~1340

**BEFORE**:
```csharp
private bool TryInlineConditional(string selector, Smalltalk80Parser.KeywordPartContext[] keywordParts)
{
    switch (selector)
    {
        case "ifTrue:":
            return TryInlineIfTrue(keywordParts);
        case "ifFalse:":
            return TryInlineIfFalse(keywordParts);
        case "ifTrue:ifFalse:":
            return TryInlineIfTrueIfFalse(keywordParts);
        case "ifFalse:ifTrue:":
            return TryInlineIfFalseIfTrue(keywordParts);
        case "and:":
            return TryInlineAnd(keywordParts);
        case "or:":
            return TryInlineOr(keywordParts);
        case "whileTrue:":
            return TryInlineWhileTrue(keywordParts);
        case "whileFalse:":
            return TryInlineWhileFalse(keywordParts);
        default:
            return false;
    }
}
```

**AFTER**:
```csharp
/// <summary>
/// Attempts to inline a conditional message (ifTrue:, ifFalse:, etc.).
/// </summary>
/// <param name="selector">The message selector</param>
/// <param name="keywordParts">The keyword parts containing the block arguments</param>
/// <param name="forValue">True if result is needed, false if for effect only</param>
/// <returns>True if successfully inlined, false otherwise</returns>
private bool TryInlineConditional(
    string selector,
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    switch (selector)
    {
        case "ifTrue:":
            return TryInlineIfTrue(keywordParts, forValue);
        case "ifFalse:":
            return TryInlineIfFalse(keywordParts, forValue);
        case "ifTrue:ifFalse:":
            return TryInlineIfTrueIfFalse(keywordParts, forValue);
        case "ifFalse:ifTrue:":
            return TryInlineIfFalseIfTrue(keywordParts, forValue);
        case "and:":
            return TryInlineAnd(keywordParts, forValue);
        case "or:":
            return TryInlineOr(keywordParts, forValue);
        case "whileTrue:":
            return TryInlineWhileTrue(keywordParts);  // No forValue (always returns nil)
        case "whileFalse:":
            return TryInlineWhileFalse(keywordParts);  // No forValue (always returns nil)
        default:
            return false;
    }
}
```

### Step 1.3: Modify CompileKeywordMessage

**Location**: Line ~1252

**BEFORE**:
```csharp
private void CompileKeywordMessage(Smalltalk80Parser.KeywordMessageContext msg, bool isSuper = false)
{
    var keywordParts = msg.keywordPart();
    var keywords = new List<string>();

    // Build selector first to check for conditional patterns
    foreach (var part in keywordParts)
    {
        var keyword = part.IDENTIFIER().GetText() + ":";
        keywords.Add(keyword);
    }
    var selector = string.Join("", keywords);

    // Check if this is a conditional message that can be inlined
    if (!isSuper && TryInlineConditional(selector, keywordParts))
    {
        return; // Successfully inlined
    }

    // ... rest of method ...
}
```

**AFTER**:
```csharp
/// <summary>
/// Compiles a keyword message (e.g., at:put:, ifTrue:ifFalse:).
/// </summary>
/// <param name="msg">The keyword message context</param>
/// <param name="isSuper">True if this is a super send</param>
/// <param name="forValue">True if result is needed (default true)</param>
/// <returns>True if message was inlined and handled forValue internally, false otherwise</returns>
private bool CompileKeywordMessage(
    Smalltalk80Parser.KeywordMessageContext msg,
    bool isSuper = false,
    bool forValue = true)
{
    var keywordParts = msg.keywordPart();
    var keywords = new List<string>();

    // Build selector first to check for conditional patterns
    foreach (var part in keywordParts)
    {
        var keyword = part.IDENTIFIER().GetText() + ":";
        keywords.Add(keyword);
    }
    var selector = string.Join("", keywords);

    // Check if this is a conditional message that can be inlined
    if (!isSuper && TryInlineConditional(selector, keywordParts, forValue))
    {
        return true; // Successfully inlined - forValue handled internally
    }

    // ... rest of method (unchanged) ...

    // Not inlined - normal send always leaves value on stack
    return false;
}
```

**IMPORTANT**: Don't forget to update the tail-recursive call site and normal send emission - they should still return `false` at the end.

## Phase 2: Thread forValue Through Call Chain

### Step 2.1: Modify CompileBinaryExpr

**Location**: Line ~1097

**BEFORE**:
```csharp
private void CompileBinaryExpr(Smalltalk80Parser.BinaryExprContext expr, bool receiverIsSuper)
```

**AFTER**:
```csharp
/// <summary>
/// Compiles a binary expression (no cascade logic - cascade is handled by CompileCascadeExpr).
/// </summary>
/// <param name="expr">The binary expression context</param>
/// <param name="receiverIsSuper">True if receiver is super</param>
/// <param name="forValue">True if result is needed (default true)</param>
/// <returns>True if last message was inlined, false otherwise</returns>
private bool CompileBinaryExpr(
    Smalltalk80Parser.BinaryExprContext expr,
    bool receiverIsSuper,
    bool forValue = true)
```

**Update Implementation**:
```csharp
private bool CompileBinaryExpr(
    Smalltalk80Parser.BinaryExprContext expr,
    bool receiverIsSuper,
    bool forValue = true)
{
    // Grammar: unaryExpr (binaryMessage)*
    var unaryExpr = expr.unaryExpr();
    bool receiverIsSuperForUnary = IsReceiverSuper(unaryExpr?.messageBase());
    CompileUnaryExpr(unaryExpr!, receiverIsSuperForUnary);

    var binaryMessages = expr.binaryMessage();
    if (binaryMessages != null && binaryMessages.Length > 0)
    {
        bool wasInlined = false;
        for (int i = 0; i < binaryMessages.Length; i++)
        {
            var binMsg = binaryMessages[i];
            bool isLast = (i == binaryMessages.Length - 1);
            bool useForValue = isLast ? forValue : true;  // Only last message gets forValue

            wasInlined = CompileBinaryMessage(binMsg, useForValue);
        }
        return wasInlined;  // Return whether last message was inlined
    }

    return false;  // No binary messages
}
```

**Note**: Binary messages (like `+`, `-`) are never inlined, so this will always return `false`. But we need to thread the parameter for consistency.

### Step 2.2: Modify CompileMessageExpr

**Location**: Line ~1061

**BEFORE**:
```csharp
private void CompileMessageExpr(Smalltalk80Parser.MessageExprContext expr)
```

**AFTER**:
```csharp
/// <summary>
/// Compiles a message expression (no cascade logic - cascade is handled by CompileCascadeExpr).
/// </summary>
/// <param name="expr">The message expression context</param>
/// <param name="forValue">True if result is needed (default true)</param>
/// <returns>True if message was inlined, false otherwise</returns>
private bool CompileMessageExpr(
    Smalltalk80Parser.MessageExprContext expr,
    bool forValue = true)
{
    // Grammar: messageExpr = binaryExpr keywordMessage?
    var binaryExpr = expr.binaryExpr();
    var keywordMessage = expr.keywordMessage();

    // EARLY DETECTION: Check for whileTrue:/whileFalse: with block receiver
    if (binaryExpr != null && keywordMessage != null)
    {
        if (TryInlineWhileLoop(binaryExpr, keywordMessage))
        {
            return true; // Successfully inlined the whole expression
        }
    }

    // NORMAL FLOW: Compile receiver then keyword message
    bool wasInlined = false;
    if (binaryExpr != null)
    {
        var unaryExpr = binaryExpr.unaryExpr();
        bool receiverIsSuper = IsReceiverSuper(unaryExpr?.messageBase());

        // Only pass forValue if there's no keyword message
        bool binaryForValue = (keywordMessage == null) ? forValue : true;
        wasInlined = CompileBinaryExpr(binaryExpr, receiverIsSuper, binaryForValue);
    }

    if (keywordMessage != null)
    {
        // Super send only if: receiver is super AND no unary/binary messages
        bool receiverIsSuper = DetermineIfKeywordReceiverIsSuper(binaryExpr);
        wasInlined = CompileKeywordMessage(keywordMessage, receiverIsSuper, forValue);
    }

    return wasInlined;
}
```

### Step 2.3: Modify CompileCascadeExpr

**Location**: Line ~708

**BEFORE**:
```csharp
private void CompileCascadeExpr(Smalltalk80Parser.CascadeExprContext expr)
```

**AFTER**:
```csharp
/// <summary>
/// Compiles a cascade expression.
/// </summary>
/// <param name="expr">The cascade expression context</param>
/// <param name="forValue">True if final result is needed (default true)</param>
/// <returns>True if last message was inlined, false otherwise</returns>
private bool CompileCascadeExpr(
    Smalltalk80Parser.CascadeExprContext expr,
    bool forValue = true)
{
    var messageExpr = expr.messageExpr();
    var messageParts = expr.messagePart();

    // No cascade - compile normally
    if (messageParts == null || messageParts.Length == 0)
    {
        if (messageExpr != null)
        {
            return CompileMessageExpr(messageExpr, forValue);
        }
        return false;
    }

    // Simple case: "receiver; msg1; msg2"
    if (messageExpr != null && IsJustReceiver(messageExpr))
    {
        CompileMessageExpr(messageExpr);  // Receiver always for value

        bool wasInlined = false;
        for (int i = 0; i < messageParts.Length; i++)
        {
            bool isLast = (i == messageParts.Length - 1);

            if (!isLast)
            {
                emitter.Dup();
            }

            // Only last message gets forValue
            wasInlined = CompileMessagePart(messageParts[i], isLast ? forValue : false);

            if (!isLast)
            {
                emitter.Pop();
            }
        }
        return wasInlined;
    }

    // Complex case: "receiver msg1; msg2"
    // ... rest of implementation (update similarly) ...

    return false;  // Update based on last message
}
```

### Step 2.4: Modify CompileAssignmentExpr

**Location**: Line ~658

**BEFORE**:
```csharp
private void CompileAssignmentExpr(Smalltalk80Parser.AssignmentExprContext expr, bool leaveOnStack = true)
{
    var variable = expr.variable();

    if (variable != null && expr.ASSIGN() != null)
    {
        // ... assignment code ...
    }
    else
    {
        // No assignment, compile cascade expression
        var cascadeExpr = expr.cascadeExpr();
        if (cascadeExpr != null)
        {
            CompileCascadeExpr(cascadeExpr);
            if (!leaveOnStack)
            {
                emitter.Pop();  // ← PROBLEM: Always pops, even if inlined
            }
        }
    }
}
```

**AFTER**:
```csharp
private void CompileAssignmentExpr(Smalltalk80Parser.AssignmentExprContext expr, bool leaveOnStack = true)
{
    var variable = expr.variable();

    if (variable != null && expr.ASSIGN() != null)
    {
        // ... assignment code (unchanged) ...
    }
    else
    {
        // No assignment, compile cascade expression
        var cascadeExpr = expr.cascadeExpr();
        if (cascadeExpr != null)
        {
            bool wasInlined = CompileCascadeExpr(cascadeExpr, forValue: leaveOnStack);

            // Only pop if:
            // 1. Result not needed (!leaveOnStack), AND
            // 2. Message was NOT inlined (non-inlined messages always leave value)
            //
            // Inlined conditionals handle forValue internally:
            //   - forValue=true: push nil on false branch
            //   - forValue=false: don't push nil (shortcut pattern)
            //
            // Non-inlined messages always leave value, so we must pop if not needed
            if (!leaveOnStack && !wasInlined)
            {
                emitter.Pop();
            }
        }
    }
}
```

## Phase 3: Implement "For Effect" Patterns

### Step 3.1: Modify TryInlineIfTrue

**Location**: Line ~1523

**BEFORE**:
```csharp
private bool TryInlineIfTrue(Smalltalk80Parser.KeywordPartContext[] keywordParts)
{
    if (keywordParts.Length != 1) return false;
    if (!CanInlineBlock(keywordParts[0], out var trueBlock)) return false;

    // Check if block ends with a return (affects code generation)
    bool blockReturns = BlockEndsWithReturn(trueBlock!);

    // PASS 1: Calculate sizes
    int trueBlockSize = CalculateBlockBodySize(trueBlock!);

    // Calculate jump offsets
    int skipNilJumpSize = blockReturns ? 0 : BytecodeEmitter.SizeForwardJump(1);
    int bfpOffset = trueBlockSize + skipNilJumpSize;

    // PASS 2: Emit with optimal jumps
    emitter.EmitJumpOnFalse(bfpOffset);

    // Compile true block body inline
    CompileInlineBlockBody(trueBlock!);

    // Emit: unconditional jump over nil (for false case)
    if (!blockReturns)
    {
        emitter.EmitForwardJump(1); // Skip 1 byte (pushNil)
    }

    // False case: push nil
    emitter.PushNil();

    return true;
}
```

**AFTER**:
```csharp
/// <summary>
/// Inlines: condition ifTrue: [trueBlock]
/// </summary>
/// <param name="keywordParts">The keyword parts (should be 1)</param>
/// <param name="forValue">True if result needed, false for effect only</param>
/// <returns>True if successfully inlined</returns>
/// <remarks>
/// FOR VALUE pattern:
///   <condition>
///   BFP +offset
///   <trueBlock>
///   JMP +1
///   PUSH_NIL
///
/// FOR EFFECT pattern (shortcut):
///   <condition>
///   BFP +offset
///   <trueBlock>
///   (no jump, no nil)
/// </remarks>
private bool TryInlineIfTrue(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    if (keywordParts.Length != 1) return false;
    if (!CanInlineBlock(keywordParts[0], out var trueBlock)) return false;

    // Check if block ends with a return (affects code generation)
    bool blockReturns = BlockEndsWithReturn(trueBlock!);

    // PASS 1: Calculate sizes
    int trueBlockSize = CalculateBlockBodySize(trueBlock!);

    if (forValue)
    {
        // FOR VALUE: Full pattern with nil
        // Pattern: BFP <trueBlock> JMP PUSH_NIL
        int skipNilJumpSize = blockReturns ? 0 : BytecodeEmitter.SizeForwardJump(1);
        int bfpOffset = trueBlockSize + skipNilJumpSize;

        // PASS 2: Emit with optimal jumps
        emitter.EmitJumpOnFalse(bfpOffset);

        // Compile true block body inline
        CompileInlineBlockBody(trueBlock!);

        // Emit: unconditional jump over nil (for false case)
        if (!blockReturns)
        {
            emitter.EmitForwardJump(1); // Skip 1 byte (pushNil)
        }

        // False case: push nil
        emitter.PushNil();
    }
    else
    {
        // FOR EFFECT: Shortcut pattern (no nil, no jump)
        // Pattern: BFP <trueBlock>
        // When condition is false, execution just continues past the block

        emitter.EmitJumpOnFalse(trueBlockSize);

        // Compile true block body inline
        CompileInlineBlockBody(trueBlock!);

        // Done - no nil push, no jump needed
        // Result: When true, block executes. When false, block is skipped.
        // No value left on stack either way.
    }

    return true;
}
```

### Step 3.2: Modify TryInlineIfFalse

**Location**: Line ~1569

**AFTER**:
```csharp
/// <summary>
/// Inlines: condition ifFalse: [falseBlock]
/// </summary>
/// <param name="keywordParts">The keyword parts (should be 1)</param>
/// <param name="forValue">True if result needed, false for effect only</param>
/// <returns>True if successfully inlined</returns>
/// <remarks>
/// FOR VALUE pattern:
///   <condition>
///   BTP +offset
///   <falseBlock>
///   JMP +1
///   PUSH_NIL
///
/// FOR EFFECT pattern (shortcut):
///   <condition>
///   BTP +offset
///   <falseBlock>
///   (no jump, no nil)
/// </remarks>
private bool TryInlineIfFalse(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    if (keywordParts.Length != 1) return false;
    if (!CanInlineBlock(keywordParts[0], out var falseBlock)) return false;

    bool blockReturns = BlockEndsWithReturn(falseBlock!);
    int falseBlockSize = CalculateBlockBodySize(falseBlock!);

    if (forValue)
    {
        // FOR VALUE: Full pattern
        int skipNilJumpSize = blockReturns ? 0 : BytecodeEmitter.SizeForwardJump(1);
        int btpOffset = falseBlockSize + skipNilJumpSize;

        emitter.EmitJumpOnTrue(btpOffset);
        CompileInlineBlockBody(falseBlock!);

        if (!blockReturns)
        {
            emitter.EmitForwardJump(1);
        }

        emitter.PushNil();
    }
    else
    {
        // FOR EFFECT: Shortcut pattern
        emitter.EmitJumpOnTrue(falseBlockSize);
        CompileInlineBlockBody(falseBlock!);
        // No nil, no jump
    }

    return true;
}
```

### Step 3.3: Modify TryInlineIfTrueIfFalse

**Location**: Line ~1615

**NOTE**: For both-branch conditionals, the pattern is SAME whether forValue=true or false.
When both branches exist, we always generate the full pattern and let the caller pop if needed.

**AFTER**:
```csharp
/// <summary>
/// Inlines: condition ifTrue: [trueBlock] ifFalse: [falseBlock]
/// </summary>
/// <param name="keywordParts">The keyword parts (should be 2)</param>
/// <param name="forValue">True if result needed (NOTE: pattern same either way)</param>
/// <returns>True if successfully inlined</returns>
/// <remarks>
/// Pattern (same for both forValue=true and forValue=false):
///   <condition>
///   BFP +offset
///   <trueBlock>
///   JMP +offset2
///   <falseBlock>
///
/// Both branches present, so no nil needed. Result (last value from either branch)
/// left on stack. Caller pops if forValue=false.
/// </remarks>
private bool TryInlineIfTrueIfFalse(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    if (keywordParts.Length != 2) return false;
    if (!CanInlineBlock(keywordParts[0], out var trueBlock)) return false;
    if (!CanInlineBlock(keywordParts[1], out var falseBlock)) return false;

    bool trueBlockReturns = BlockEndsWithReturn(trueBlock!);
    bool falseBlockReturns = BlockEndsWithReturn(falseBlock!);

    // PASS 1: Calculate sizes
    int trueBlockSize = CalculateBlockBodySize(trueBlock!);
    int falseBlockSize = CalculateBlockBodySize(falseBlock!);

    // Calculate jump offset
    int skipFalseJumpSize = trueBlockReturns ? 0 : BytecodeEmitter.SizeForwardJump(falseBlockSize);
    int bfpOffset = trueBlockSize + skipFalseJumpSize;

    // PASS 2: Emit
    emitter.EmitJumpOnFalse(bfpOffset);

    // True block
    CompileInlineBlockBody(trueBlock!);

    // Jump over false block (unless true block returns)
    if (!trueBlockReturns)
    {
        emitter.EmitForwardJump(falseBlockSize);
    }

    // False block
    CompileInlineBlockBody(falseBlock!);

    // Note: Both branches leave their result on stack
    // If forValue=false, caller will pop the result

    return true;
}
```

### Step 3.4: Modify TryInlineIfFalseIfTrue

**Location**: Line ~1657

Similar to ifTrueIfFalse - pattern is same regardless of forValue.

### Step 3.5: Modify TryInlineAnd and TryInlineOr

**Location**: Lines ~1699 and ~1733

**NOTE**: `and:` and `or:` always leave a boolean value (true/false) on the stack.
They don't have a "for effect" shortcut pattern because they must return a value.

Just add the `forValue` parameter for consistency, but ignore it:

```csharp
private bool TryInlineAnd(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    // NOTE: and: always leaves value (true or false) on stack
    // forValue parameter ignored - no shortcut pattern exists

    // ... existing implementation ...
    return true;
}

private bool TryInlineOr(
    Smalltalk80Parser.KeywordPartContext[] keywordParts,
    bool forValue)
{
    // NOTE: or: always leaves value (true or false) on stack
    // forValue parameter ignored - no shortcut pattern exists

    // ... existing implementation ...
    return true;
}
```

## Phase 4: Update Helper Methods

### Step 4.1: Update CompileBinaryMessage

If this method exists, update signature:

```csharp
private bool CompileBinaryMessage(
    Smalltalk80Parser.BinaryMessageContext msg,
    bool forValue = true)
{
    // Binary operators are never inlined
    // ... existing implementation ...
    return false;  // Not inlined
}
```

### Step 4.2: Update CompileMessagePart

**Location**: Around line 750 (in CompileCascadeExpr)

```csharp
private bool CompileMessagePart(
    Smalltalk80Parser.MessagePartContext part,
    bool forValue = true)
{
    // ... implementation ...
    // Return whether message was inlined
}
```

## Phase 5: Update Call Sites

### Step 5.1: Find All Call Sites

Search for:
- `CompileKeywordMessage(`
- `CompileMessageExpr(`
- `CompileBinaryExpr(`
- `CompileCascadeExpr(`

Update each call site to pass `forValue` appropriately.

### Step 5.2: Example Call Site Updates

**In CompileUnaryExpr** (if it calls keyword messages):
```csharp
// If last unary in chain and result not needed
bool isLast = ...;
bool useForValue = isLast ? forValue : true;
CompileKeywordMessage(msg, isSuper, useForValue);
```

## Testing Plan

### Test 1: Simple Statement (for effect)

**Smalltalk**:
```smalltalk
testIfTrueEffect
    | x y |
    x _ true.
    y _ 42.
    x ifTrue: [y].
    ^nil
```

**Expected bytecode**:
```
PUSH true
STORE x, leave=false
PUSH 42
STORE y, leave=false
PUSH x
BFP +2          ; if false, skip block
PUSH y          ; true block
                ; (no JMP, no PUSH_NIL, no POP)
PUSH nil
RETURN
```

### Test 2: Assignment (for value)

**Smalltalk**:
```smalltalk
testIfTrueValue
    | x result |
    x _ true.
    result _ x ifTrue: [42].
    ^result
```

**Expected bytecode**:
```
PUSH true
STORE x, leave=false
PUSH x
BFP +3          ; if false, jump to nil
PUSH 42         ; true block
JMP +1          ; skip nil
PUSH_NIL        ; false returns nil
STORE result, leave=false
PUSH result
RETURN
```

### Test 3: Both Branches (statement)

**Smalltalk**:
```smalltalk
testIfTrueIfFalseEffect
    | x |
    x _ true.
    x ifTrue: [1] ifFalse: [2].
    ^nil
```

**Expected bytecode**:
```
PUSH true
STORE x, leave=false
PUSH x
BFP +3          ; if false, jump to false block
PUSH 1          ; true block
JMP +2          ; skip false block
PUSH 2          ; false block
POP             ; pop result (both branches leave value, not needed)
PUSH nil
RETURN
```

**Note**: For both-branch conditionals, we MUST pop because both branches leave a value.

### Test 4: Run Regression Suite

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
```

**Expected**: Returns `1191` (120 tests pass)

## Common Pitfalls

### Pitfall 1: Forgetting to Return wasInlined

**Wrong**:
```csharp
private void CompileKeywordMessage(..., bool forValue)
{
    if (TryInlineConditional(..., forValue))
    {
        return;  // ← WRONG: void return, caller doesn't know it was inlined
    }
}
```

**Right**:
```csharp
private bool CompileKeywordMessage(..., bool forValue)
{
    if (TryInlineConditional(..., forValue))
    {
        return true;  // ← RIGHT: caller knows it was inlined
    }
    // ... normal send ...
    return false;  // Not inlined
}
```

### Pitfall 2: Popping Both-Branch Conditionals Incorrectly

**Wrong**:
```csharp
// In TryInlineIfTrueIfFalse
if (!forValue)
{
    emitter.Pop();  // ← WRONG: Inlining method shouldn't pop
}
```

**Right**:
```csharp
// In CompileAssignmentExpr
bool wasInlined = CompileCascadeExpr(cascadeExpr, forValue: leaveOnStack);

// For ifTrue:ifFalse:, wasInlined=true but we still need to pop if !leaveOnStack
// because both branches leave a value

// Actually, this is tricky - see next section
```

### Pitfall 3: Both-Branch Conditionals Need Special Handling

**The Problem**:
- `x ifTrue: [1] ifFalse: [2]` always leaves a value (either 1 or 2)
- If used as statement, we need to POP
- But wasInlined=true, so our logic says "don't pop"

**Solution**:
Two options:

**Option A**: Have `TryInlineIfTrueIfFalse` return false (not inlined) so caller pops
```csharp
// This is semantically wrong but works
return false;  // Pretend not inlined so caller pops
```

**Option B**: Have inlining methods return WHAT KIND of inlining:
```csharp
enum InlineResult
{
    NotInlined,           // Normal send - leaves value, caller should pop
    InlinedForValue,      // Inlined with value - caller should pop if !leaveOnStack
    InlinedForEffect      // Inlined for effect - no value left, no pop needed
}
```

**Recommended**: Use Option B for correctness.

Update return types:
```csharp
private InlineResult TryInlineIfTrue(..., bool forValue)
{
    // ...
    if (forValue)
    {
        // Full pattern with nil
        return InlineResult.InlinedForValue;
    }
    else
    {
        // Shortcut pattern
        return InlineResult.InlinedForEffect;
    }
}

private InlineResult TryInlineIfTrueIfFalse(..., bool forValue)
{
    // Both branches - always leaves value
    return InlineResult.InlinedForValue;
}
```

Then in caller:
```csharp
InlineResult result = CompileCascadeExpr(cascadeExpr, forValue: leaveOnStack);

if (!leaveOnStack && result != InlineResult.InlinedForEffect)
{
    emitter.Pop();  // Pop if: not inlined OR inlined for value
}
```

## Summary

**Key Changes**:
1. Add `forValue` parameter to all inlining methods
2. Thread `forValue` from `CompileStatement` through entire call chain
3. Generate shortcut patterns when `forValue=false` for single-branch conditionals
4. Return `InlineResult` enum instead of bool to indicate what kind of value handling
5. Update `CompileAssignmentExpr` to only pop when needed

**Bytecode Patterns**:
- **ifTrue: (for value)**: `<cond> BFP <block> JMP PUSH_NIL`
- **ifTrue: (for effect)**: `<cond> BFP <block>`
- **ifTrue:ifFalse: (either)**: `<cond> BFP <true> JMP <false>` + POP if for effect
- **and:/or: (always)**: `<cond> BFP/BTP <block> JMP PUSH_TRUE/FALSE`

**Testing**:
1. BlockContext regression suite must pass (1191)
2. Test decompiler on various methods
3. Verify bytecode patterns match Smalltalk compiler

**Critical**: Use the `InlineResult` enum approach to correctly handle both-branch conditionals!
