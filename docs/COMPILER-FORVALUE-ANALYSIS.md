# Blue Book Compiler: forValue vs forEffect Bytecode Generation

**Created**: 2025-12-12
**Status**: CANONICAL REFERENCE
**Source**: Smalltalk-80.st (Blue Book compiler implementation)

---

## Executive Summary

The Blue Book compiler uses a `forValue` flag to determine whether an expression should leave a value on the stack (`forValue: true`) or be compiled purely for its side effects (`forValue: false`). This distinction is critical for optimizing conditional expressions and avoiding unnecessary stack operations.

**Key Principle**: Code compiled "for effect" does not leave a value on the stack; code compiled "for value" always leaves exactly one value on the stack.

---

## 1. Core Methods in Parse Nodes

### ParseNode Protocol

Every parse node implements these four methods:

```smalltalk
emitForValue: stack on: stream
  "Emit bytecodes that leave a value on the stack"

emitForEffect: stack on: stream
  "Emit bytecodes that do not leave a value on the stack"

emitForEvaluatedValue: stack on: stream
  "Like emitForValue, but assumes arguments/receiver already evaluated"

emitForEvaluatedEffect: stack on: stream
  "Like emitForEffect, but assumes arguments/receiver already evaluated"
```

### BlockNode Implementation

**From Smalltalk-80.st:**

```smalltalk
emitForEvaluatedValue: stack on: aStream
	self emitExceptLast: stack on: aStream.
	statements last emitForValue: stack on: aStream!

emitForEvaluatedEffect: stack on: aStream
	self returns
		ifTrue:
			[self emitForEvaluatedValue: stack on: aStream.
			stack pop: 1]
		ifFalse:
			[self emitExceptLast: stack on: aStream.
			statements last emitForEffect: stack on: aStream]!
```

**Key Insight**: When a block ends with a return (`^`), even `emitForEvaluatedEffect` calls `emitForEvaluatedValue` and then pops because the return exits the method anyway (the pop never executes).

---

## 2. MessageNode>>emitIf:on:value: - Two-Arm Conditional

**Source from Smalltalk-80.st:**

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
					super emitForEffect: stack on: strm]]!
```

---

## 3. Bytecode Patterns by Conditional Type

### 3.1 Two-Arm Conditional: `ifTrue:ifFalse:`

#### Compiled FOR VALUE (`forValue: true`)

```
Pattern: <condition> BFP <thenBlock> [JMP] <elseBlock>
```

**Example:** `x < 5 ifTrue: [^10] ifFalse: [20]`

```
PUSH x
PUSH 5
SEND <
BFP 2          ; skip to elseBlock if false
  PUSH 10
  RETURN       ; exits method
               ; (no JMP needed - unreachable)
  PUSH 20      ; leaves result on stack
```

**Key:**
- Both blocks use `emitForEvaluatedValue`
- If `thenBlock` ends with `^`, no JMP is emitted (dead code elimination)
- Otherwise, JMP skips over `elseBlock`
- Result from either branch left on stack

#### Compiled FOR EFFECT (`forValue: false`)

**Case 1: Both blocks non-empty**
```
Compiles as: compile for value + POP
```

**Example:** `x < 5 ifTrue: [y _ 10] ifFalse: [z _ 20]`

```
PUSH x
PUSH 5
SEND <
BFP 4
  PUSH 10
  STORE y
  PUSH 20
  STORE z
POP            ; discard the result
```

**Case 2: One block empty (single-arm conditional)**
```
Uses shortcut - see section 3.2
```

**Case 3: Both blocks empty (null conditional)**
```
Compiles as: <condition> emitForEffect
```

---

### 3.2 Single-Arm Conditional: `ifTrue:`

#### Compiled FOR VALUE

```
Pattern: <condition> BFP [blockBody] JMP PUSH_NIL
```

**Example:** `x < 5 ifTrue: [y _ 10]` (value needed)

```
PUSH x
PUSH 5
SEND <
BFP 3          ; jump to nil if false
  PUSH 10
  STORE y      ; leaves 10 on stack
  JMP 1        ; skip over nil
  PUSH_NIL     ; false case returns nil
```

**Key:** Always leaves a value on stack (block result or nil)

#### Compiled FOR EFFECT

```
Pattern: <condition> BFP [blockBody]
```

**Example:** `x < 5 ifTrue: [y _ 10].` (statement, not expression)

```
PUSH x
PUSH 5
SEND <
BFP 2          ; jump to end if false
  PUSH 10
  STORE y      ; leaves 10 on stack
               ; (no JMP, no PUSH_NIL)
```

**Key:**
- Uses `emitForEvaluatedEffect` for block
- No JMP, no PUSH_NIL
- May leave value on stack but it's discarded by caller

---

### 3.3 Single-Arm Conditional: `ifFalse:`

#### Compiled FOR VALUE

```
Pattern: <condition> BTP [blockBody] JMP PUSH_NIL
```

**Example:** `x < 5 ifFalse: [y _ 10]` (value needed)

```
PUSH x
PUSH 5
SEND <
BTP 3          ; jump to block if true (inverted!)
  PUSH 10
  STORE y
  JMP 1
  PUSH_NIL
```

#### Compiled FOR EFFECT

```
Pattern: <condition> BTP [blockBody]
```

**Example:** `x < 5 ifFalse: [y _ 10].`

```
PUSH x
PUSH 5
SEND <
BTP 2
  PUSH 10
  STORE y
```

---

### 3.4 Short-Circuit Boolean: `and:`

#### Compiled FOR VALUE

```
Pattern: <receiver> BFP PUSH_FALSE [blockBody] JMP PUSH_FALSE
```

**Example:** `x < 5 and: [y > 3]`

```
PUSH x
PUSH 5
SEND <
BFP 5          ; if false, skip block and push false
  PUSH y
  PUSH 3
  SEND >       ; block result on stack
  JMP 1        ; skip final false
  PUSH_FALSE
```

**Key:** Returns receiver if false, else returns block result

#### Compiled FOR EFFECT

```
Pattern: <receiver> BFP [blockBody]
```

---

### 3.5 Short-Circuit Boolean: `or:`

#### Compiled FOR VALUE

```
Pattern: <receiver> BTP PUSH_TRUE [blockBody] JMP PUSH_TRUE
```

**Example:** `x < 5 or: [y > 3]`

```
PUSH x
PUSH 5
SEND <
BTP 5          ; if true, skip block and push true
  PUSH y
  PUSH 3
  SEND >
  JMP 1
  PUSH_TRUE
```

---

## 4. Nested Conditionals and forValue Propagation

### Example from User's Question

```smalltalk
meta = 0 ifTrue: [aKeyboardEvent hasLock ifTrue: [meta _ 1024]].
```

**Analysis:**

1. **Outer `ifTrue:`** - Used as statement (followed by period) → compiled FOR EFFECT
2. **Inner `ifTrue:`** - Inside block, last statement → compiled FOR VALUE!

**Why?** BlockNode>>emitForEvaluatedEffect says:
- If block returns (`^`), emit for value
- Otherwise, emit last statement `emitForValue`

### Bytecode Pattern

```
PUSH meta                          ; outer condition
PUSH 0
SEND =
BFP end                            ; outer ifTrue: (for effect)
  PUSH aKeyboardEvent              ; inner condition (block body)
  SEND hasLock
  BFP skip                         ; inner ifTrue: (for value!)
    PUSH 1024
    STORE meta
    JMP pushNil
  skip:
    PUSH_NIL                       ; inner ifTrue: returns nil when false
  pushNil:
                                   ; (no JMP, no PUSH_NIL for outer)
end:
```

**Key Insight**: Inner `ifTrue:` is compiled FOR VALUE even though outer is FOR EFFECT, because it's the last statement in a block that doesn't return.

---

## 5. Method-Level forValue Propagation

### Top-Level Statement Compilation

```smalltalk
MethodNode>>emitOn: stream
  statements do: [:stmt |
    stmt == statements last
      ifTrue: [stmt emitForValue: stack on: stream]
      ifFalse: [stmt emitForEffect: stack on: stream]
  ]
```

**Rule:**
- All statements except last → `emitForEffect`
- Last statement → `emitForValue` (becomes method return value)

### Block-Level Statement Compilation

```smalltalk
BlockNode>>emitForEvaluatedValue: stack on: aStream
	self emitExceptLast: stack on: aStream.
	statements last emitForValue: stack on: aStream!
```

**Rule:**
- All statements except last → `emitForEffect`
- Last statement → `emitForValue` (becomes block value)

---

## 6. Complete Example Walkthrough

### Source Code

```smalltalk
example
  | x |
  x = 0 ifTrue: [^1].
  x < 5 ifTrue: [x _ 10] ifFalse: [x _ 20].
  ^x
```

### Bytecode Analysis

```
Statement 1: x = 0 ifTrue: [^1].
  - Used for effect (not last statement)
  - Block ends with return
  - Pattern: <cond> BFP <block-for-value> [no JMP]

  PUSH_TEMP 0 (x)
  PUSH 0
  SEND =
  BFP 2           ; skip to statement 2 if false
    PUSH 1
    RETURN        ; exits method

Statement 2: x < 5 ifTrue: [x _ 10] ifFalse: [x _ 20].
  - Used for effect (not last statement)
  - Both blocks non-empty
  - Compiles as: for-value + POP

  PUSH_TEMP 0 (x)
  PUSH 5
  SEND <
  BFP 3           ; skip to else block
    PUSH 10
    STORE_TEMP 0  ; leaves 10 on stack
    JMP 2
    PUSH 20
    STORE_TEMP 0  ; leaves 20 on stack
  POP             ; discard result (for effect)

Statement 3: ^x
  - Last statement (always for value)

  PUSH_TEMP 0 (x)
  RETURN
```

---

## 7. Compiler Optimizations Summary

### Dead Code Elimination

When a block ends with `^`, the compiler skips the JMP after it:

```smalltalk
thenExpr returns ifFalse: [self emitJump: elseSize on: strm]
```

### Shortcut Detection

Single-arm conditionals compiled for effect use simplified pattern:

```smalltalk
"Shortcut for ifTrue: only:  <A> bfp-end <B>"
self emitBranch: thenSize pop: stack on: strm.
thenExpr emitForEvaluatedEffect: stack on: strm
```

### Null Conditional Elimination

When both blocks are empty and compiled for effect:

```smalltalk
(thenSize=0) & (elseSize=0)  "null conditional"
  ifTrue: [^ receiver emitForEffect: stack on: strm]
```

Just emits the condition for its side effects.

---

## 8. Differences from Current C# Compiler

### Current C# Implementation Issues

**File:** `/home/user/Smalltalk-80/Smalltalk80.Compiler/BytecodeCompiler.cs`

#### TryInlineIfTrue (Lines 1514-1557)

```csharp
// ALWAYS emits PUSH_NIL at end
emitter.PushNil();
```

**Problem:** This makes it "for value" always. Should check if parent context needs value.

#### TryInlineIfFalse (Lines 1560-1603)

```csharp
// ALWAYS emits PUSH_NIL at end
emitter.PushNil();
```

**Problem:** Same issue - always "for value".

#### TryInlineIfTrueIfFalse (Lines 1606-1649)

```csharp
// No PUSH_NIL at end
CompileInlineBlockBody(falseBlock!);
```

**Correct:** Two-arm conditionals don't need trailing PUSH_NIL.

### Required C# Compiler Changes

1. **Add forValue parameter** to all TryInline methods
2. **Propagate forValue** from statement context
3. **Conditional PUSH_NIL emission**:
   ```csharp
   if (forValue && !blockReturns) {
       emitter.EmitForwardJump(1);
       emitter.PushNil();
   }
   ```
4. **Block body compilation** should use:
   - `CompileInlineBlockBodyForValue()` when forValue=true
   - `CompileInlineBlockBodyForEffect()` when forValue=false

---

## 9. forValue Decision Table

| Context | forValue | Reason |
|---------|----------|--------|
| Last statement in method | true | Becomes method return value |
| Non-last statement in method | false | Result discarded |
| Last statement in block (no `^`) | true | Becomes block value |
| Non-last statement in block | false | Result discarded |
| Last statement in block (with `^`) | true | Returns from method |
| Assignment RHS | true | Value needed for assignment |
| Message argument | true | Value needed for send |
| Message receiver | true | Value needed for send |
| Standalone statement | false | No value needed |

---

## 10. Key Takeaways

1. **forValue is contextual**: Determined by where the expression appears in the AST
2. **Blocks inherit last-statement rule**: Last statement always compiled for value
3. **Returns force for-value**: Even in for-effect context, `^expr` needs value
4. **Single-arm conditionals differ**: FOR EFFECT skips JMP and PUSH_NIL
5. **Two-arm conditionals**: Both branches must produce same stack effect
6. **Short-circuit booleans**: Similar to single-arm (receiver value or block value)
7. **Optimization matters**: Dead JMP elimination when block ends with return

---

## References

- **Source File**: `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st`
- **MessageNode>>emitIf:on:value:**: Lines 25663-25693
- **BlockNode>>emitForEvaluatedValue**: Line ~5810
- **BlockNode>>emitForEvaluatedEffect**: Line ~5820
- **Blue Book Chapter 26**: The Compiler (overview)
- **Blue Book Chapter 28**: Bytecode specification
