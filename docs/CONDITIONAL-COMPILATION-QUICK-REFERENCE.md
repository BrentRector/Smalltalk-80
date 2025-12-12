# Conditional Compilation Quick Reference

**Date**: 2025-12-12
**Related Docs**:
- CONDITIONAL-COMPILATION-FOR-VALUE-VS-EFFECT-ANALYSIS.md (detailed analysis)
- CONDITIONAL-COMPILATION-IMPLEMENTATION-GUIDE.md (step-by-step implementation)

## TL;DR

**Problem**: C# compiler generates `<cond> BFP <block> JMP PUSH_NIL POP` for statements.
**Expected**: C# compiler should generate `<cond> BFP <block>` for statements (no nil, no jump, no pop).
**Solution**: Thread `forValue` parameter through compiler, generate different patterns based on whether result is needed.

## Bytecode Patterns

### ifTrue: Patterns

| Context | Pattern | Stack After |
|---------|---------|-------------|
| **For Value**<br/>`x _ cond ifTrue: [val]` | `<cond> BFP+3 <val> JMP+1 PUSH_NIL` | value or nil |
| **For Effect**<br/>`cond ifTrue: [val].` | `<cond> BFP+offset <val>` | empty |

### ifFalse: Patterns

| Context | Pattern | Stack After |
|---------|---------|-------------|
| **For Value**<br/>`x _ cond ifFalse: [val]` | `<cond> BTP+3 <val> JMP+1 PUSH_NIL` | value or nil |
| **For Effect**<br/>`cond ifFalse: [val].` | `<cond> BTP+offset <val>` | empty |

### ifTrue:ifFalse: Patterns

| Context | Pattern | Stack After |
|---------|---------|-------------|
| **For Value**<br/>`x _ cond ifTrue: [a] ifFalse: [b]` | `<cond> BFP+3 <a> JMP+2 <b>` | a or b |
| **For Effect**<br/>`cond ifTrue: [a] ifFalse: [b].` | `<cond> BFP+3 <a> JMP+2 <b> POP` | empty |

**Key Difference**: Both-branch conditionals ALWAYS leave a value, so POP needed for effect.

### and: / or: Patterns

| Selector | Pattern | Stack After |
|----------|---------|-------------|
| `and:` | `<cond> BFP+3 <block> JMP+1 PUSH_FALSE` | true or false |
| `or:` | `<cond> BTP+3 <block> JMP+1 PUSH_TRUE` | true or false |

**Note**: These ALWAYS leave a value (no "for effect" shortcut).

## Key Code Locations

| Method | Line | Purpose |
|--------|------|---------|
| `CompileStatement` | ~510 | Decides `leaveOnStack = inBlock && isLast` |
| `CompileAssignmentExpr` | ~658 | Calls cascade, **pops if !leaveOnStack** |
| `CompileCascadeExpr` | ~708 | Dispatches to message compilation |
| `CompileMessageExpr` | ~1061 | Handles keyword messages |
| `CompileKeywordMessage` | ~1252 | Checks for inlining |
| `TryInlineConditional` | ~1340 | Dispatches to specific inliner |
| `TryInlineIfTrue` | ~1523 | **Generates ifTrue: pattern** |
| `TryInlineIfFalse` | ~1569 | **Generates ifFalse: pattern** |
| `TryInlineIfTrueIfFalse` | ~1615 | **Generates ifTrue:ifFalse: pattern** |

## Call Chain

```
CompileStatement(isLast, inBlock)
  leaveOnStack = inBlock && isLast
    ↓
CompileExpression(leaveOnStack)
    ↓
CompileAssignmentExpr(leaveOnStack)
    ↓
CompileCascadeExpr()  ← ADD: forValue parameter
    ↓
CompileMessageExpr()  ← ADD: forValue parameter
    ↓
CompileKeywordMessage()  ← ADD: forValue parameter, return bool
    ↓
TryInlineConditional(forValue)
    ↓
TryInlineIfTrue/IfFalse/etc(forValue)
    ↓
  Generate pattern based on forValue
```

## Required Changes

### 1. Add forValue Parameter

**Methods to Update**:
- `TryInlineIfTrue(parts, **forValue**)`
- `TryInlineIfFalse(parts, **forValue**)`
- `TryInlineIfTrueIfFalse(parts, **forValue**)`
- `TryInlineIfFalseIfTrue(parts, **forValue**)`
- `TryInlineAnd(parts, **forValue**)`
- `TryInlineOr(parts, **forValue**)`
- `TryInlineConditional(selector, parts, **forValue**)`
- `CompileKeywordMessage(msg, isSuper, **forValue**)`
- `CompileMessageExpr(expr, **forValue**)`
- `CompileBinaryExpr(expr, isSuper, **forValue**)`
- `CompileCascadeExpr(expr, **forValue**)`

### 2. Change Return Types

**From**:
```csharp
private void CompileKeywordMessage(...)
```

**To**:
```csharp
private bool CompileKeywordMessage(...)
// Returns: true if inlined, false if normal send
```

### 3. Fix CompileAssignmentExpr

**From**:
```csharp
CompileCascadeExpr(cascadeExpr);
if (!leaveOnStack)
{
    emitter.Pop();  // ALWAYS pops
}
```

**To**:
```csharp
bool wasInlined = CompileCascadeExpr(cascadeExpr, forValue: leaveOnStack);
if (!leaveOnStack && !wasInlined)
{
    emitter.Pop();  // Only pop non-inlined messages
}
```

**Wait, this is wrong for both-branch conditionals!**

### 4. Use InlineResult Enum (RECOMMENDED)

```csharp
enum InlineResult
{
    NotInlined,           // Normal send - always leaves value
    InlinedForValue,      // Inlined - leaves value
    InlinedForEffect      // Inlined - no value left
}
```

**Then**:
```csharp
InlineResult result = CompileCascadeExpr(expr, forValue: leaveOnStack);

if (!leaveOnStack && result != InlineResult.InlinedForEffect)
{
    emitter.Pop();  // Pop if not inlined OR inlined for value
}
```

**Return Values**:
- `ifTrue:` with `forValue=true` → `InlinedForValue`
- `ifTrue:` with `forValue=false` → `InlinedForEffect`
- `ifTrue:ifFalse:` with either → `InlinedForValue` (both branches leave value)
- `and:` / `or:` with either → `InlinedForValue` (always leave value)
- Normal sends → `NotInlined` (always leave value)

## Implementation Checklist

- [ ] Define `InlineResult` enum in BytecodeCompiler.cs
- [ ] Add `forValue` parameter to all inlining methods
- [ ] Change return types from `bool` to `InlineResult`
- [ ] Update `TryInlineIfTrue` to generate shortcut pattern when `forValue=false`
- [ ] Update `TryInlineIfFalse` to generate shortcut pattern when `forValue=false`
- [ ] Keep `TryInlineIfTrueIfFalse` same (always returns `InlinedForValue`)
- [ ] Keep `TryInlineAnd/Or` same (always returns `InlinedForValue`)
- [ ] Thread `forValue` from `CompileStatement` through entire call chain
- [ ] Update `CompileAssignmentExpr` to use `InlineResult`
- [ ] Test with BlockContext regression suite (must return 1191)
- [ ] Test with Decompiler on various methods

## Testing

### Unit Test Example

```csharp
[Fact]
public void IfTrue_ForEffect_GeneratesShortcutPattern()
{
    var source = @"
testMethod
    true ifTrue: [42].
    ^nil
";
    var bytecode = Compile(source);

    // Should be: PUSH_TRUE BFP+2 PUSH_42 PUSH_NIL RETURN
    // NOT: PUSH_TRUE BFP+5 PUSH_42 JMP+1 PUSH_NIL POP PUSH_NIL RETURN

    Assert.Equal(new byte[] {
        0x71,        // PUSH_TRUE
        0xA8, 0x02,  // BFP +2
        0x21,        // PUSH 42
        0x78,        // PUSH_NIL
        0x7C         // RETURN
    }, bytecode);
}
```

### Regression Test

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart coldstart_with_runner.json
# Expected: 1191
```

## Common Mistakes

### Mistake 1: Not Handling Both-Branch Case

**Wrong**:
```csharp
bool wasInlined = CompileCascadeExpr(...);
if (!leaveOnStack && !wasInlined)
    emitter.Pop();

// FAILS for: cond ifTrue: [a] ifFalse: [b].
// wasInlined=true but we still need to POP!
```

**Right**:
```csharp
InlineResult result = CompileCascadeExpr(...);
if (!leaveOnStack && result != InlinedForEffect)
    emitter.Pop();

// WORKS: ifTrue:ifFalse: returns InlinedForValue, so we POP
```

### Mistake 2: Popping Inside Inlining Method

**Wrong**:
```csharp
private InlineResult TryInlineIfTrueIfFalse(..., bool forValue)
{
    // ... compile both branches ...
    if (!forValue)
        emitter.Pop();  // NO! Caller's responsibility
}
```

**Right**:
```csharp
private InlineResult TryInlineIfTrueIfFalse(..., bool forValue)
{
    // ... compile both branches ...
    return InlineResult.InlinedForValue;  // Caller will pop if needed
}
```

### Mistake 3: Forgetting to Thread Parameter

**Wrong**:
```csharp
CompileKeywordMessage(msg, isSuper);  // Missing forValue!
```

**Right**:
```csharp
CompileKeywordMessage(msg, isSuper, forValue);
```

## Smalltalk Compiler Reference

**From Smalltalk-80.st, MessageNode class:**

```smalltalk
emitIf: stack on: strm value: forValue
    forValue
        ifTrue:
            ["Full pattern: <A> bfp-C <B> jmp-end <C>"
            receiver emitForValue: stack on: strm.
            self emitBranch: thenSize pop: stack on: strm.
            thenExpr emitForEvaluatedValue: stack on: strm.
            thenExpr returns ifFalse: [self emitJump: elseSize on: strm].
            elseExpr emitForEvaluatedValue: stack on: strm]
        ifFalse:
            ["Shortcut for ifTrue: only: <A> bfp-end <B>"
            receiver emitForValue: stack on: strm.
            self emitBranch: thenSize pop: stack on: strm.
            thenExpr emitForEvaluatedEffect: stack on: strm]
```

**Key Insight**: Smalltalk compiler has TWO code paths based on `forValue` parameter.

## Decision Matrix

| Conditional | forValue | Pattern | Returns |
|-------------|----------|---------|---------|
| `ifTrue:` | true | `<c> BFP <t> JMP NIL` | `InlinedForValue` |
| `ifTrue:` | false | `<c> BFP <t>` | `InlinedForEffect` |
| `ifFalse:` | true | `<c> BTP <f> JMP NIL` | `InlinedForValue` |
| `ifFalse:` | false | `<c> BTP <f>` | `InlinedForEffect` |
| `ifTrue:ifFalse:` | true | `<c> BFP <t> JMP <f>` | `InlinedForValue` |
| `ifTrue:ifFalse:` | false | `<c> BFP <t> JMP <f>` | `InlinedForValue` |
| `and:` | either | `<c> BFP <b> JMP FALSE` | `InlinedForValue` |
| `or:` | either | `<c> BTP <b> JMP TRUE` | `InlinedForValue` |
| Normal send | either | `<r> <args> SEND` | `NotInlined` |

**Then in caller**:
```csharp
InlineResult result = CompileMessage(..., forValue: leaveOnStack);

if (!leaveOnStack && result != InlinedForEffect)
{
    emitter.Pop();
}
```

## File Locations

**All changes in**: `/home/user/Smalltalk-80/Smalltalk80.Compiler/BytecodeCompiler.cs`

**Related tests**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter.Tests/` (if we add unit tests)

**Regression test**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json`

## Summary

1. **Root Cause**: C# compiler doesn't have `forValue` parameter, always generates "for value" pattern
2. **Fix**: Thread `forValue` through compiler, generate shortcut patterns for single-branch conditionals
3. **Key Insight**: Use `InlineResult` enum to distinguish between "no value" and "has value" cases
4. **Critical**: Both-branch conditionals always leave value, need POP even when inlined
5. **Testing**: BlockContext regression suite must pass (1191)
