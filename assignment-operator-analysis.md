# Smalltalk-80 Runtime Compiler Assignment Operator Analysis

**Date**: 2025-12-31
**Source File**: `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st`

## Executive Summary

The Smalltalk-80 runtime compiler **currently supports ONLY the underscore (`_`) operator** for assignment, which it tokenizes as `#leftArrow`. The `:=` (colon-equals) operator is **NOT supported** in the current implementation.

---

## Current Implementation

### 1. Scanner Class - Token Type Table (Line 34022-34047)

The Scanner maintains a `TypeTable` that maps ASCII characters to token types:

```smalltalk
!Scanner class methodsFor: 'class initialization'!
initialize
	"Scanner initialize."

	| newTable |
	newTable _ Array new: 256 withAll: #xBinary. "default"
	newTable atAll: #(9 10 12 13 32 ) put: #xDelimiter. "tab lf ff cr space"
	newTable atAll: ($0 asciiValue to: $9 asciiValue) put: #xDigit.
	newTable atAll: ($A asciiValue to: $Z asciiValue) put: #xLetter.
	newTable atAll: ($a asciiValue to: $z asciiValue) put: #xLetter.
	newTable at: 30 put: #doIt.
	newTable at: $" asciiValue put: #xDoubleQuote.
	newTable at: $# asciiValue put: #xLitQuote.
	newTable at: $$ asciiValue put: #xDollar.
	newTable at: $' asciiValue put: #xSingleQuote.
	newTable at: $( asciiValue put: #leftParenthesis.
	newTable at: $) asciiValue put: #rightParenthesis.
	newTable at: $. asciiValue put: #period.
	newTable at: $: asciiValue put: #colon.           "LINE 34039"
	newTable at: $; asciiValue put: #semicolon.
	newTable at: $[ asciiValue put: #leftBracket.
	newTable at: $] asciiValue put: #rightBracket.
	newTable at: $^ asciiValue put: #upArrow.
	newTable at: $_ asciiValue put: #leftArrow.      "LINE 34044"
	newTable at: $| asciiValue put: #verticalBar.
	EndChar _ 30 asCharacter.
	TypeTable _ newTable "bon voyage!!"! !
```

**Key Points**:
- **Line 34044**: `$_` (underscore) → `#leftArrow` token type
- **Line 34039**: `$:` (colon) → `#colon` token type
- `:=` is NOT recognized as a multi-character token

### 2. Scanner Instance Variables (Line 33807)

```smalltalk
Object subclass: #Scanner
	instanceVariableNames: 'source mark hereChar aheadChar token tokenType currentComment buffer typeTable '
```

The Scanner has **lookahead capability** via `hereChar` and `aheadChar`, which is used for:
- Multi-character binary operators (line 33904-33908)
- Keywords with trailing colons (line 33967-33970)

### 3. Parser Assignment Detection (Line 30702-30716)

```smalltalk
assignment
	"variable '_' expression => AssignmentNode"

	| varName var |
	varName _ self advance.
	(encoder cantStoreInto: varName)
		ifTrue: [^self notify: 'Cannot store into'].
	var _ encoder encodeVariable: varName.
	self advance.
	self expression ifFalse: [^self expected: 'Expression'].
	var isArg ifTrue: [^self notify: 'Cannot store into argument'].
	parseNode _ AssignmentNode new
				variable: var
				value: parseNode
				from: encoder!
```

### 4. Parser Expression Method (Line 30749-30756)

```smalltalk
expression
	(hereType == #word and: [tokenType == #leftArrow])   "LINE 30750"
		ifTrue: [self assignment.  ^true].               "LINE 30751"
	self primaryExpression ifFalse: [^false].
	(self messagePart: 3 repeat: true)
		ifTrue:
			[hereType == #semicolon ifTrue: [self cascade]].
	^true!
```

**Key Logic**:
- **Line 30750**: Checks if current token (`hereType`) is a word (variable name)
- **Line 30750**: AND next token (`tokenType`) is `#leftArrow`
- **Line 30751**: If both true, parse as assignment statement

---

## Why `:=` Doesn't Work

The current Scanner processes `:` and `=` as **separate tokens**:

1. `:` → `#colon` token (used for keywords like `ifTrue:`)
2. `=` → `#xBinary` token (binary message selector for equality test)

There is **no lookahead logic** in Scanner to combine `:` + `=` into a single `#leftArrow` or `#assignment` token.

---

## Changes Required to Support `:=`

### Option A: Support BOTH `_` and `:=` as Assignment Operators

This is the recommended approach for maximum compatibility.

#### Change 1: Add Scanner Method to Handle `:=` (Insert after line 33973)

Add a new Scanner method to detect and handle `:=` as an assignment token:

```smalltalk
!Scanner methodsFor: 'multi-character scans'!
xColon
	"Handle colon - check for := assignment or plain colon"

	self step. "consume the colon"
	hereChar == $=
		ifTrue:
			[self step. "consume the equals"
			token _ #'_'. "same token as underscore assignment"
			tokenType _ #leftArrow]
		ifFalse:
			[token _ $: asSymbol.
			tokenType _ #colon]! !
```

**Location**: Insert after line 33973 (after `xLetter` method)

**Rationale**: This method:
- Consumes the `:` character
- Looks ahead at `hereChar` (the next character)
- If it's `=`, consumes it and produces `#leftArrow` token (same as `_`)
- Otherwise, produces `#colon` token (for keywords)

#### Change 2: Modify TypeTable Initialization (Line 34039)

**IMPORTANT**: Change the TypeTable mapping for `:` from simple `#colon` to `#xColon` (with 'x' prefix):

**OLD** (Line 34039):
```smalltalk
	newTable at: $: asciiValue put: #colon.
```

**NEW**:
```smalltalk
	newTable at: $: asciiValue put: #xColon.
```

**Rationale**: The Scanner's `scanToken` method (line 33887-33894) checks if the token type starts with 'x' (line 33891). If it does, Scanner performs (calls) that token type as a method name, allowing multi-character token processing. Without the 'x' prefix, the new `xColon` method won't be invoked.

#### Change 3: Update xLetter Method (Line 33967-33970)

The `xLetter` method currently has special handling for colons to form keywords. This needs to be updated to call the new `xColon` method:

**OLD** (Lines 33967-33970):
```smalltalk
	type == #colon
		ifTrue:
			[buffer nextPut: self step.
			tokenType _ #keyword]
		ifFalse:
			[tokenType _ #word].
```

**NEW**:
```smalltalk
	type == #xColon
		ifTrue:
			[self xColon.
			tokenType == #colon
				ifTrue:
					[buffer nextPut: token.
					tokenType _ #keyword]]
		ifFalse:
			[tokenType _ #word].
```

**Rationale**:
- Now that colon uses `#xColon`, we need to check for that type
- Call `xColon` method to process `:` with lookahead for `=`
- If `xColon` returns `#colon` (not `:=`), append it to buffer for keyword
- If `xColon` returns `#leftArrow` (was `:=`), we're already done (leave as assignment)

#### Summary of Changes for Option A

| Line | Change | Description |
|------|--------|-------------|
| After 33973 | **INSERT** new `xColon` method | Handle `:=` assignment with lookahead |
| 34039 | **MODIFY** `#colon` → `#xColon` | Enable method-based token processing |
| 33967-33970 | **MODIFY** colon handling in `xLetter` | Call `xColon` method for proper tokenization |

**Parser**: No changes needed - already handles `#leftArrow` token type

---

### Option B: Support ONLY `:=` (Remove `_` Support)

This approach modernizes the syntax to match later Smalltalk variants.

#### Change 1: Add Scanner Method (Same as Option A)

Insert the `xColon` method after line 33973 (same as Option A).

#### Change 2: Modify TypeTable for `:` (Same as Option A)

Change line 34039 from `#colon` to `#xColon` (same as Option A).

#### Change 3: Update xLetter Method (Same as Option A)

Modify lines 33967-33970 to call `xColon` method (same as Option A).

#### Change 4: Remove Underscore Assignment Support (Line 34044)

**DELETE** Line 34044:
```smalltalk
	newTable at: $_ asciiValue put: #leftArrow.
```

**REPLACE** with:
```smalltalk
	newTable at: $_ asciiValue put: #xBinary.
```

**Rationale**: Treat underscore as a regular binary selector (like `+`, `-`, etc.) instead of assignment.

**CRITICAL WARNING**: This is a **breaking change** that will cause all existing Smalltalk-80 source code using `_` for assignment to fail compilation. You would need to rewrite ALL source code to use `:=` instead.

---

## Detailed Technical Analysis

### Scanner Tokenization Flow

1. **scanToken** (line 33887-33894):
   - Gets token type from `TypeTable` using character ASCII value
   - If type starts with 'x' (like `#xLetter`, `#xBinary`, `#xColon`), calls that method
   - Otherwise, returns the character as a single-character symbol

2. **xBinary** (line 33904-33908):
   - Handles multi-character binary operators like `<=`, `>=`, `==`, `~~`
   - Consumes first character, checks if next is also binary (and not `-`)
   - If so, combines into 2-character symbol

3. **xLetter** (line 33944-33973):
   - Accumulates letters and digits into a word
   - If followed by `:`, marks as keyword
   - If not, marks as word (identifier)

### Parser Token Lookahead

The Parser maintains both current token (`here`/`hereType`) and next token (`token`/`tokenType`):

- **hereType**: Type of current token being processed
- **tokenType**: Type of next token (lookahead)
- **advance** (line 30974-30986): Shifts tokens (next becomes current, scan next)

For assignment detection (line 30750):
```smalltalk
(hereType == #word and: [tokenType == #leftArrow])
```

This means:
- **Current token**: word (variable name like `x`)
- **Next token**: leftArrow (assignment operator `_` or proposed `:=`)

### Why the 'x' Prefix Matters

From `scanToken` (line 33891):
```smalltalk
(tokenType at: 1) = $x "x as first letter"
	ifTrue: [self perform: tokenType "means perform to compute token & type"]
	ifFalse: [token _ self step asSymbol "else just unique the first char"].
```

Token types starting with 'x' trigger method dispatch:
- `#xLetter` → calls `xLetter` method
- `#xBinary` → calls `xBinary` method
- `#xDigit` → calls `xDigit` method
- `#xColon` → would call `xColon` method (NEW)

Without the 'x' prefix, Scanner just returns `:` as a single-character symbol without any lookahead processing.

---

## Testing Recommendations

After implementing either option, test with:

1. **Basic Assignment**:
   ```smalltalk
   x := 42
   ```

2. **Multiple Assignments**:
   ```smalltalk
   x := y := 10
   ```

3. **Assignment in Expressions**:
   ```smalltalk
   arr at: 1 put: (x := 5)
   ```

4. **Keywords Still Work**:
   ```smalltalk
   arr at: 1 put: value
   dict at: key ifAbsent: [nil]
   ```

5. **Colon in Selectors**:
   ```smalltalk
   #at:put:
   #do:
   ```

6. **If supporting both operators**:
   ```smalltalk
   x _ 10.    "underscore"
   y := 20.   "colon-equals"
   ```

---

## Recommendations

**For maximum compatibility**: Implement **Option A** (support both `_` and `:=`). This:
- Maintains backward compatibility with existing Smalltalk-80 source code
- Adds support for modern `:=` syntax
- Allows gradual migration of source code
- Requires minimal changes (3 modifications)

**For modernization**: Implement **Option B** (only `:=`). This:
- Matches modern Smalltalk variants (Squeak, Pharo, VisualWorks)
- Eliminates the ambiguous underscore character
- **Requires rewriting ALL existing source code**
- Should only be done with a automated source transformation tool

---

## Files Requiring Changes

| File | Lines | Changes |
|------|-------|---------|
| `Smalltalk-80.st` | After 33973 | INSERT `xColon` method |
| `Smalltalk-80.st` | 34039 | MODIFY `#colon` to `#xColon` |
| `Smalltalk-80.st` | 33967-33970 | MODIFY colon handling in `xLetter` |
| `Smalltalk-80.st` | 34044 | (Option B only) REMOVE or change underscore mapping |

---

## Additional Considerations

### 1. Cold Start Bundle Regeneration

After modifying `Smalltalk-80.st`, you MUST regenerate the cold start bundle:

```bash
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/* ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/
```

### 2. Compiler Bootstrapping Issue

**CRITICAL**: The Scanner source code itself uses `_` for assignment! When modifying Scanner:

1. First compile the source WITH existing `_` operator
2. Then modify Scanner to support `:=`
3. Then compile again (now both `_` and `:=` work)
4. Then optionally rewrite Scanner source to use `:=`
5. Then compile again (now only `:=` works, if Option B)

### 3. Impact on Decompiler

If the system has a decompiler (for System Browser source viewing), it may need updates to:
- Recognize `:=` in bytecode comments
- Pretty-print assignments with `:=` instead of `_`

However, bytecode generation is unchanged - only source-level syntax changes.

---

## Conclusion

The Smalltalk-80 runtime compiler currently supports **only `_` (underscore)** for assignment. Adding support for `:=` requires:

1. **3 modifications** to Scanner class (Option A - both operators)
2. **4 modifications** to Scanner class (Option B - only `:=`)
3. **0 modifications** to Parser (already token-type agnostic)

The changes are **localized to Scanner tokenization** and leverage existing lookahead infrastructure. The Parser's assignment detection logic is already abstracted to work with any token producing `#leftArrow` type.

**Recommended**: Implement Option A to maintain compatibility while adding modern syntax support.
