# Conditional Compilation: For Value vs For Effect Analysis

**Date**: 2025-12-12
**Status**: Analysis Complete
**Issue**: C# compiler generates "for value" patterns even when result isn't needed, causing Decompiler crashes

## Executive Summary

The C# compiler currently generates bytecode patterns that always leave values on the stack, then pops them if not needed. The Smalltalk compiler uses a `forValue` parameter that generates different bytecode patterns depending on whether the result is used. The Decompiler expects Smalltalk compiler patterns and crashes when it sees C# compiler patterns.

## Problem Statement

**Current C# Behavior:**
- Always generates "for value" pattern: `<receiver> BFP <trueBlock> JMP <PUSH_NIL>`
- If result not needed, caller pops it
- Non-statement expressions (like `x ifTrue: [y]`) generate full pattern + POP

**Expected Smalltalk Behavior:**
- **For value**: `<receiver> BFP <trueBlock> JMP <PUSH_NIL>` (leaves result on stack)
- **For effect**: `<receiver> BFP <trueBlock>` (no nil, no jump, no pop needed)

**Why This Matters:**
- Decompiler pattern-matches bytecode sequences
- Unexpected POP instructions confuse the decompiler
- Different bytecode patterns require different decompilation logic

## Key Questions Answered

### 1. Where does the C# compiler decide if an expression result is needed?

**Location**: `BytecodeCompiler.CompileStatement()` (line 510)

```csharp
private void CompileStatement(Smalltalk80Parser.StatementContext stmt, bool isLast, bool inBlock)
{
    var exprStmt = stmt.expressionStatement();
    if (exprStmt != null)
    {
        // In blocks: last statement must leave value (for block return)
        // In methods: statements don't leave values (consumed by implicit ReturnSelf)
        bool leaveOnStack = inBlock && isLast;
        CompileExpression(exprStmt.expression(), leaveOnStack);
    }
}
```

**Decision Logic**:
- `leaveOnStack = inBlock && isLast`
- Method statements: `leaveOnStack = false` (except last in block)
- Block statements: Last statement has `leaveOnStack = true`, others `false`

### 2. How does it currently handle statements (expressions followed by period)?

**Flow**:
1. `CompileStatement()` calls `CompileExpression(expr, leaveOnStack)`
2. `CompileExpression()` calls `CompileAssignmentExpr(expr, leaveOnStack)`
3. `CompileAssignmentExpr()` calls `CompileCascadeExpr()` then pops if `!leaveOnStack`
4. `CompileCascadeExpr()` calls `CompileMessageExpr()` (always leaves value)
5. If `!leaveOnStack`, `CompileAssignmentExpr()` emits `POP` (line 698)

**Key Code** (BytecodeCompiler.cs, lines 695-699):
```csharp
CompileCascadeExpr(cascadeExpr);
if (!leaveOnStack)
{
    emitter.Pop();
}
```

**Problem**: The POP happens at the statement level, but inlined conditionals also push nil when they shouldn't.

### 3. What bytecode pattern does it currently generate for `x ifTrue: [y]` as a statement?

**Current C# Pattern**:
```
PUSH_TEMP 0          ; x
BFP +5               ; jump if false
PUSH_TEMP 1          ; y
JMP +1               ; skip nil
PUSH_NIL             ; false case
POP                  ; statement result not needed
```

**Expected Smalltalk Pattern** (for effect):
```
PUSH_TEMP 0          ; x
BFP +2               ; jump if false
PUSH_TEMP 1          ; y
                     ; (end - no nil, no jump, no pop)
```

**Smalltalk Pattern** (for value):
```
PUSH_TEMP 0          ; x
BFP +3               ; jump if false
PUSH_TEMP 1          ; y
JMP +1               ; skip nil
PUSH_NIL             ; false case
                     ; (end - result on stack)
```

### 4. What would need to change to generate the Smalltalk compiler's "for effect" pattern?

**Required Changes:**

#### A. Thread `forValue` Parameter Through Inlining Methods

Currently: Inlining methods don't know if result is needed
```csharp
private bool TryInlineIfTrue(KeywordPartContext[] keywordParts)
```

Should be:
```csharp
private bool TryInlineIfTrue(KeywordPartContext[] keywordParts, bool forValue)
```

#### B. Modify Pattern Generation Based on `forValue`

**TryInlineIfTrue** changes:
```csharp
private bool TryInlineIfTrue(KeywordPartContext[] keywordParts, bool forValue)
{
    // ... existing code ...

    if (forValue)
    {
        // FOR VALUE: Full pattern with nil
        emitter.EmitJumpOnFalse(bfpOffset);
        CompileInlineBlockBody(trueBlock!);
        if (!blockReturns)
        {
            emitter.EmitForwardJump(1); // Skip nil
        }
        emitter.PushNil();
    }
    else
    {
        // FOR EFFECT: Shortcut pattern (no nil, no jump)
        emitter.EmitJumpOnFalse(trueBlockSize);
        CompileInlineBlockBody(trueBlock!);
        // Done - no nil, no jump needed
    }

    return true;
}
```

**TryInlineIfFalse** changes:
```csharp
private bool TryInlineIfFalse(KeywordPartContext[] keywordParts, bool forValue)
{
    if (forValue)
    {
        // FOR VALUE: Full pattern
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
    }

    return true;
}
```

**TryInlineIfTrueIfFalse** changes:
```csharp
private bool TryInlineIfTrueIfFalse(KeywordPartContext[] keywordParts, bool forValue)
{
    // Same for both forValue=true and forValue=false
    // Both branches present, so always need full pattern
    emitter.EmitJumpOnFalse(bfpOffset);
    CompileInlineBlockBody(trueBlock!);

    if (!trueBlockReturns)
    {
        emitter.EmitForwardJump(falseBlockSize);
    }

    CompileInlineBlockBody(falseBlock!);

    // NOTE: No POP needed - caller handles if !leaveOnStack
    return true;
}
```

**TryInlineAnd/Or** changes:
```csharp
// These always leave a value (true/false), so forValue doesn't affect pattern
// Just pass through for consistency
```

#### C. Update Call Sites to Pass `forValue`

**CompileKeywordMessage** needs to know if result is used:
```csharp
private void CompileKeywordMessage(
    KeywordMessageContext msg,
    bool isSuper = false,
    bool forValue = true)  // NEW PARAMETER
{
    // Check if this is a conditional message that can be inlined
    if (!isSuper && TryInlineConditional(selector, keywordParts, forValue))
    {
        return; // Successfully inlined
    }

    // ... rest of method ...
}

private bool TryInlineConditional(
    string selector,
    KeywordPartContext[] keywordParts,
    bool forValue)  // NEW PARAMETER
{
    switch (selector)
    {
        case "ifTrue:":
            return TryInlineIfTrue(keywordParts, forValue);
        case "ifFalse:":
            return TryInlineIfFalse(keywordParts, forValue);
        // ... etc ...
    }
}
```

**Threading the parameter**:
```
CompileStatement(leaveOnStack)
  ↓
CompileExpression(leaveOnStack)
  ↓
CompileAssignmentExpr(leaveOnStack)
  ↓
CompileCascadeExpr(leaveOnStack)  ← ADD PARAMETER
  ↓
CompileMessageExpr(leaveOnStack)  ← ADD PARAMETER
  ↓
CompileBinaryExpr(leaveOnStack)  ← ADD PARAMETER
  ↓
CompileKeywordMessage(forValue = leaveOnStack)
  ↓
TryInlineConditional(forValue)
  ↓
TryInlineIfTrue/IfFalse/etc(forValue)
```

#### D. Remove POP from CompileAssignmentExpr

Currently (lines 695-699):
```csharp
CompileCascadeExpr(cascadeExpr);
if (!leaveOnStack)
{
    emitter.Pop();  // ← REMOVE THIS
}
```

Should be:
```csharp
CompileCascadeExpr(cascadeExpr, leaveOnStack);
// Inlined conditionals now handle forValue internally
// Non-inlined messages still leave value, so POP needed
if (!leaveOnStack && !WasInlined(cascadeExpr))
{
    emitter.Pop();
}
```

**Problem**: How to know if message was inlined?

**Solution**: Return value from `CompileCascadeExpr`:
```csharp
private bool CompileCascadeExpr(CascadeExprContext expr, bool forValue)
{
    // ... compile message ...
    return wasInlined;
}
```

Then:
```csharp
bool wasInlined = CompileCascadeExpr(cascadeExpr, leaveOnStack);
if (!leaveOnStack && !wasInlined)
{
    emitter.Pop();
}
```

## Comparison: C# Compiler vs Smalltalk Compiler

### Smalltalk Compiler Architecture

**From Smalltalk-80.st, MessageNode class:**

```smalltalk
emitForEffect: stack on: strm
    special > 0
        ifTrue:
            [self perform: (MacroEmitters at: special)
                  with: stack
                  with: strm
                  with: false.  "← forValue = false"
             pc _ strm position]
        ifFalse:
            [super emitForEffect: stack on: strm.
             pc _ strm position]

emitForValue: stack on: strm
    special > 0
        ifTrue:
            [self perform: (MacroEmitters at: special)
                  with: stack
                  with: strm
                  with: true.  "← forValue = true"
             pc _ strm position]
        ifFalse:
            [receiver ~~ nil ifTrue: [receiver emitForValue: stack on: strm].
             arguments do: [:argument | argument emitForValue: stack on: strm].
             self emitMessageSend: stack on: strm.
             pc _ strm position]
```

**Key Pattern**:
- Two separate entry points: `emitForEffect:` and `emitForValue:`
- Both call same macro emitter with different `forValue` parameter
- Non-inlined messages always emit for value, caller pops if needed

**emitIf: implementation** (lines 25663-25697):

```smalltalk
emitIf: stack on: strm value: forValue
    "Emit code for <A> ifTrue: <B> ifFalse: <C>, with some optimizations"
    | thenExpr thenSize elseExpr elseSize |
    thenSize _ sizes at: 1.
    elseSize _ sizes at: 2.
    thenExpr _ arguments at: 1.
    elseExpr _ arguments at: 2.
    forValue
        ifTrue:
            ["Everything is straightforward for value:  <A> bfp-C <B> jmp-end <C>"
            receiver emitForValue: stack on: strm.
            self emitBranch: thenSize pop: stack on: strm.
            thenExpr emitForEvaluatedValue: stack on: strm.
            stack pop: 1.
            thenExpr returns ifFalse: [self emitJump: elseSize on: strm].
            elseExpr emitForEvaluatedValue: stack on: strm]
        ifFalse:
            ["If value not needed, check for possible shortcuts"
            (thenSize=0) & (elseSize=0)  "null conditional"
                ifTrue: [^ receiver emitForEffect: stack on: strm].
            (elseSize = 0) | (thenSize = 0)
                ifTrue:
                    [receiver emitForValue: stack on: strm.
                    (elseSize = 0)
                        ifTrue:
                            ["Shortcut for ifTrue: only:  <A> bfp-end <B>"
                            self emitBranch: thenSize pop: stack on: strm.
                            thenExpr emitForEvaluatedEffect: stack on: strm]
                        ifFalse:
                            ["Shortcut for ifFalse: only  <A> btp-end <C>"
                            self emitTrueBranch: elseSize pop: stack on: strm.
                            elseExpr emitForEvaluatedEffect: stack on: strm]]
                ifFalse:
                    ["If no shortcuts, then compile for value followed by pop"
                    super emitForEffect: stack on: strm]]
```

**Key Patterns**:
1. **For value**: Always full pattern `<A> BFP <B> JMP <C>`
2. **For effect with both branches**: Fall back to `super emitForEffect:` (value + pop)
3. **For effect with one branch**: Shortcut pattern (no nil, no jump)
4. **For effect with no branches**: Just emit receiver for effect

### C# Compiler Current Architecture

**Flow**:
```
CompileStatement(isLast, inBlock)
  → leaveOnStack = inBlock && isLast
  → CompileExpression(leaveOnStack)
    → CompileAssignmentExpr(leaveOnStack)
      → CompileCascadeExpr()  ← ALWAYS leaves value
        → CompileMessageExpr()
          → CompileKeywordMessage()
            → TryInlineConditional()  ← No forValue parameter
              → Always generates "for value" pattern
      → if (!leaveOnStack) emit POP
```

**Problem**:
- Inlining happens deep in call stack without knowledge of `leaveOnStack`
- POP is emitted at wrong level (statement level, not expression level)
- Non-inlined messages aren't being popped

## Previous Attempt Analysis

**From user's note:**
> A previous attempt to add `leaveOnStack` threading broke the compiler because non-inlined keyword sends weren't being popped.

**Why It Failed:**

The previous attempt likely:
1. Passed `leaveOnStack` to inlining methods ✓
2. Modified inlined conditionals to not push nil when `!leaveOnStack` ✓
3. **Removed the POP in CompileAssignmentExpr** ✗

This broke non-inlined messages because:
- Non-inlined keyword sends always leave value on stack
- Example: `receiver doSomething. nextStatement`
- Without POP, value stays on stack incorrectly

**Why the pattern is:**
```
PUSH_RECEIVER
SEND doSomething  ← leaves result on stack
POP               ← MUST pop for statement
```

**Solution**: Need to track whether message was inlined:
```csharp
bool wasInlined = CompileKeywordMessage(..., forValue);
if (!leaveOnStack && !wasInlined)
{
    emitter.Pop();  // Only pop non-inlined messages
}
```

## Recommended Solution

### Option 1: Return Value Approach (RECOMMENDED)

**Advantages**:
- Clean separation of concerns
- Inlining methods report their behavior
- Caller decides when to POP

**Implementation**:
```csharp
// Return true if inlined and handled forValue internally
private bool CompileKeywordMessage(
    KeywordMessageContext msg,
    bool isSuper = false,
    bool forValue = true)
{
    if (!isSuper && TryInlineConditional(selector, keywordParts, forValue))
    {
        return true;  // Inlined - forValue handled
    }

    // Not inlined - emit normal send (always leaves value)
    // ... normal send code ...
    return false;  // Not inlined - caller may need to POP
}
```

Then in `CompileAssignmentExpr`:
```csharp
bool wasInlined = CompileCascadeExpr(cascadeExpr, leaveOnStack);
if (!leaveOnStack && !wasInlined)
{
    emitter.Pop();  // Only pop non-inlined messages
}
```

### Option 2: Emit POP in Inlining Methods (NOT RECOMMENDED)

**Idea**: If `!forValue`, emit POP at end of inlined code

**Why This Fails**:
- `ifTrue:ifFalse:` has both branches - which one pops?
- Blocks ending with return don't need POP
- Confuses responsibility (inlining vs statement handling)

### Option 3: Two-Pass with Side Channel (COMPLEX)

**Idea**: Set flag during inlining, check in caller

**Why This Fails**:
- State management complexity
- Cascade messages need per-message tracking
- Not thread-safe (if we ever parallelize)

## Implementation Plan

### Phase 1: Add forValue Parameter
1. Add `forValue` parameter to all inlining methods
2. Pass through but don't change behavior yet
3. Add `forValue` to CompileKeywordMessage, CompileMessageExpr, etc.
4. Thread parameter from CompileStatement down

### Phase 2: Make Inlining Methods Return bool
1. Change signature: `private bool TryInlineIfTrue(..., bool forValue)`
2. Return true if successfully inlined
3. Update all call sites to capture return value

### Phase 3: Implement "For Effect" Patterns
1. Modify TryInlineIfTrue to generate shortcut when `forValue=false`
2. Modify TryInlineIfFalse to generate shortcut when `forValue=false`
3. Keep TryInlineIfTrueIfFalse same (both branches, always full pattern)
4. Keep And/Or same (always leave value)

### Phase 4: Fix POP Emission
1. Capture `wasInlined` from CompileKeywordMessage
2. Only emit POP if `!leaveOnStack && !wasInlined`
3. Remove POP for inlined conditionals

### Phase 5: Test
1. Run BlockContext regression test (must return 1191)
2. Test decompiler on methods with conditionals
3. Verify both "for value" and "for effect" patterns

## Test Cases

### Test 1: Statement (for effect)
```smalltalk
x ifTrue: [y].
z
```

**Current C# bytecode**:
```
PUSH x
BFP +5
PUSH y
JMP +1
PUSH_NIL
POP        ← extra POP
PUSH z
```

**Expected bytecode**:
```
PUSH x
BFP +2
PUSH y
PUSH z
```

### Test 2: Value (for value)
```smalltalk
result _ x ifTrue: [y].
```

**Current C# bytecode**:
```
PUSH x
BFP +5
PUSH y
JMP +1
PUSH_NIL
STORE result, leave=true
```

**Expected bytecode**: (same - this works correctly)

### Test 3: Both Branches (statement)
```smalltalk
x ifTrue: [y] ifFalse: [z].
```

**Current C# bytecode**:
```
PUSH x
BFP +3
PUSH y
JMP +2
PUSH z
POP        ← This is correct for both-branch case!
```

**Expected bytecode**: (same - Smalltalk also does value + pop for this case)

### Test 4: Non-Inlined Message (statement)
```smalltalk
receiver doSomething.
```

**Current C# bytecode**:
```
PUSH receiver
SEND doSomething
POP        ← Correct!
```

**Expected bytecode**: (same)

## Special Cases

### Empty Block
```smalltalk
x ifTrue: [].
```

**For value**: `<x> BFP +3 PUSH_NIL JMP +1 PUSH_NIL`
**For effect**: `<x> BFP +1 PUSH_NIL` (jump over nil)

Actually, for effect with empty block, Smalltalk just emits receiver for effect.

### Block with Return
```smalltalk
x ifTrue: [^y].
```

**Pattern**: No JMP after block (unreachable)
```
PUSH x
BFP +3
PUSH y
RETURN
PUSH_NIL
```

### Cascade with Conditional
```smalltalk
receiver
    doThis;
    ifTrue: [that];
    doOther
```

**Each message** has its own forValue:
- `doThis` - for effect (not last cascade)
- `ifTrue:` - for effect (not last cascade)
- `doOther` - for value (last cascade)

## Decompiler Impact

The Decompiler expects these patterns:

**ifTrue: (for value)**:
```
<receiver>
BFP offset
<trueBlock>
JMP 1
PUSH_NIL
```

**ifTrue: (for effect)**:
```
<receiver>
BFP offset
<trueBlock>
(no jump, no nil)
```

**Current C# pattern breaks decompiler**:
```
<receiver>
BFP offset
<trueBlock>
JMP 1
PUSH_NIL
POP        ← Decompiler doesn't expect this!
```

## Summary

**Root Cause**: C# compiler doesn't thread `forValue` through to inlining methods

**Solution**:
1. Add `forValue` parameter to inlining methods
2. Generate different patterns based on `forValue`
3. Track whether message was inlined
4. Only POP non-inlined messages

**Critical Insight**:
- Inlined conditionals should handle `forValue` internally (no nil when not needed)
- Non-inlined messages always leave value (caller pops if needed)
- Can't just pass `leaveOnStack` through without tracking inlining

**Next Steps**:
1. Implement Option 1 (Return Value Approach)
2. Test with BlockContext regression suite
3. Test with Decompiler on various methods
4. Verify bytecode matches Smalltalk compiler patterns
