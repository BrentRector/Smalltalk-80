# Assignment Operator Implementation - Code Changes

This document shows the **exact code changes** needed to add `:=` support to the Smalltalk-80 runtime compiler.

---

## Option A: Support BOTH `_` and `:=` (Recommended)

### Change 1: Add xColon Method (Insert after line 33973)

**Location**: In Scanner class, after the `xLetter` method ends at line 33973

**INSERT**:
```smalltalk
xColon
	"Handle colon - check for := assignment or plain colon for keywords"

	self step. "consume the colon"
	hereChar == $=
		ifTrue:
			[self step. "consume the equals"
			token _ #'_'. "produce same token as underscore"
			tokenType _ #leftArrow]
		ifFalse:
			[token _ $: asSymbol.
			tokenType _ #colon]!
```

---

### Change 2: Modify TypeTable Entry for Colon (Line 34039)

**Location**: Scanner class >> initialize method, line 34039

**BEFORE**:
```smalltalk
	newTable at: $: asciiValue put: #colon.
```

**AFTER**:
```smalltalk
	newTable at: $: asciiValue put: #xColon.
```

**Critical**: The 'x' prefix triggers method dispatch in `scanToken` (line 33891)

---

### Change 3: Update xLetter Method's Colon Handling (Lines 33967-33970)

**Location**: Scanner class >> xLetter method, lines 33967-33970

**BEFORE**:
```smalltalk
	type == #colon
		ifTrue:
			[buffer nextPut: self step.
			tokenType _ #keyword]
		ifFalse:
			[tokenType _ #word].
	token _ buffer contents!
```

**AFTER**:
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
	token _ buffer contents!
```

**Explanation**:
- Check for `#xColon` instead of `#colon` (now the TypeTable value)
- Call `xColon` method to process `:` with lookahead for `=`
- If result is `#colon` (plain colon), append to buffer for keyword
- If result is `#leftArrow` (`:=` assignment), skip keyword processing

---

## Option B: Support ONLY `:=` (Breaking Change)

All changes from Option A, PLUS:

### Additional Change: Remove Underscore Support (Line 34044)

**Location**: Scanner class >> initialize method, line 34044

**BEFORE**:
```smalltalk
	newTable at: $_ asciiValue put: #leftArrow.
```

**AFTER**:
```smalltalk
	newTable at: $_ asciiValue put: #xBinary.
```

**Warning**: This breaks ALL existing Smalltalk-80 source code using `_` for assignment!

---

## Complete Modified Methods

### Scanner >> xColon (NEW METHOD)

```smalltalk
!Scanner methodsFor: 'multi-character scans'!
xColon
	"Handle colon - check for := assignment or plain colon for keywords"

	self step. "consume the colon"
	hereChar == $=
		ifTrue:
			[self step. "consume the equals"
			token _ #'_'. "produce same token as underscore"
			tokenType _ #leftArrow]
		ifFalse:
			[token _ $: asSymbol.
			tokenType _ #colon]! !
```

---

### Scanner class >> initialize (MODIFIED)

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
	newTable at: $: asciiValue put: #xColon.        "CHANGED from #colon"
	newTable at: $; asciiValue put: #semicolon.
	newTable at: $[ asciiValue put: #leftBracket.
	newTable at: $] asciiValue put: #rightBracket.
	newTable at: $^ asciiValue put: #upArrow.
	newTable at: $_ asciiValue put: #leftArrow.     "Option B: change to #xBinary"
	newTable at: $| asciiValue put: #verticalBar.
	EndChar _ 30 asCharacter.
	TypeTable _ newTable "bon voyage!!"! !
```

---

### Scanner >> xLetter (MODIFIED)

```smalltalk
!Scanner methodsFor: 'multi-character scans'!
xLetter
	"form a word or keyword"
	| type prefix |
	buffer reset.
	[type _ typeTable at: hereChar asciiValue.
	type == #xLetter or: [type == #xDigit]]
		whileTrue:
			["open code step for speed"
			buffer nextPut: hereChar.
			hereChar _ aheadChar.
			source atEnd
				ifTrue: [aheadChar _ EndChar "doit"]
				ifFalse: [aheadChar _ source next]].
	(type == #period and:
			[tokenType _ typeTable at: aheadChar asciiValue.
			(tokenType == #xLetter) | (tokenType == #xBinary)])
		ifTrue:  "Allow embedded period in selectors"
			[buffer nextPut: self step.
			prefix _ buffer contents.
			self perform: tokenType.
			token _ prefix , token.
			tokenType == #binary ifTrue: [token _ token asSymbol].
			^self].
	type == #xColon                                  "CHANGED from #colon"
		ifTrue:
			[self xColon.                            "CHANGED: call method"
			tokenType == #colon                      "CHANGED: check result"
				ifTrue:                              "CHANGED: only if plain colon"
					[buffer nextPut: token.          "CHANGED: append token"
					tokenType _ #keyword]]           "CHANGED: mark as keyword"
		ifFalse:
			[tokenType _ #word].
	token _ buffer contents! !
```

---

## Test Cases

After making changes, test with these expressions:

### Basic Assignment
```smalltalk
x := 42
```
Should tokenize as:
- `x` (word)
- `_` (leftArrow)
- `42` (number)

### Keywords Still Work
```smalltalk
dict at: key put: value
```
Should tokenize as:
- `dict` (word)
- `at:` (keyword)
- `key` (word)
- `put:` (keyword)
- `value` (word)

### Mixed Assignment and Keywords
```smalltalk
result := dict at: key ifAbsent: [nil]
```
Should tokenize as:
- `result` (word)
- `_` (leftArrow)
- `dict` (word)
- `at:` (keyword)
- `key` (word)
- `ifAbsent:` (keyword)
- `[` (leftBracket)
- `nil` (word)
- `]` (rightBracket)

### Both Operators (Option A only)
```smalltalk
x _ 10.
y := 20
```
Both should compile successfully.

---

## Implementation Steps

1. **Make the 3 changes** to `Smalltalk-80.st` (Option A) or 4 changes (Option B)

2. **Regenerate cold start bundle**:
   ```bash
   cd "Smalltalk-80 Source"
   dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
   ```

3. **Copy to interpreter**:
   ```bash
   cp ../Smalltalk80.Compiler/bin/Debug/net9.0/Data/* \
      ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/
   ```

4. **Test the interpreter**:
   ```bash
   cd Smalltalk80.Interpreter
   dotnet run -- --execute "x := 42"
   ```

5. **Run regression tests**:
   ```bash
   dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
   ```
   Should return `1191` (120 tests pass)

---

## Rationale for Design Decisions

### Why `xColon` instead of modifying scanToken?

The Scanner uses a **dispatch pattern** where token types starting with 'x' trigger method calls. This:
- Keeps tokenization logic modular
- Follows existing pattern (`xBinary`, `xLetter`, `xDigit`)
- Allows lookahead processing (checking `hereChar` for `=`)

### Why produce same token as underscore?

The `xColon` method produces `token _ #'_'` and `tokenType _ #leftArrow` for `:=` to:
- Maintain compatibility with Parser (already handles `#leftArrow`)
- Ensure decompiler/pretty-printer works correctly
- Allow both operators to be semantically identical

### Why modify xLetter?

The `xLetter` method processes words (identifiers) and checks if they're followed by `:` to form keywords:
- `at:` → keyword
- `put:` → keyword
- `ifAbsent:` → keyword

After our change, it needs to:
1. Call `xColon` to check for `:=` vs plain `:`
2. Only process as keyword if result is plain `:`
3. Leave as-is if result is `:=` (assignment, not keyword)

Without this change, `:=` would incorrectly tokenize as keyword followed by equals.

---

## Edge Cases Handled

1. **`:=` at end of line**: Works (assigns, then continues)
2. **`:==` (assignment with equality test)**: Tokenizes as `:=` then `=`
3. **`::` (double colon)**: Tokenizes as two separate colons
4. **`:` in symbols**: `#:` still works (literal symbol)
5. **Whitespace**: `x : = 10` tokenizes as `x`, `:`, `=`, `10` (syntax error, as expected)

The lookahead only combines `:=` when they're **immediately adjacent** with no whitespace.

---

## Performance Impact

**Negligible**. The changes add:
- 1 additional character lookahead check (`:` → check if next is `=`)
- 1 additional method call (`xColon`)

This is identical to existing multi-character operators like:
- `<=`, `>=`, `~=`, `~~` (handled by `xBinary`)
- Embedded periods in selectors (handled by `xLetter`)

The Scanner already uses lookahead (`hereChar`, `aheadChar`) for all tokenization.
