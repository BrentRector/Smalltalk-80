# Syntax Highlighting Implementation Plan

**Created**: 2026-01-02
**Status**: Planning
**Updated**: 2026-01-02 - Analysis of existing Scanner infrastructure

## Executive Summary

This plan originally proposed creating a new `SyntaxTokenizer` class. **Analysis reveals the existing Scanner class already provides complete tokenization with position tracking.** We should leverage Scanner instead of duplicating its functionality.

**Key Insight**: Scanner already:
- Tokenizes all Smalltalk syntax elements
- Provides token types (#word, #keyword, #binary, #string, #number, #literal, #comment, etc.)
- Tracks token positions via `mark` instance variable
- Is proven robust (used by the compiler)

**Recommended Approach**: Extend Scanner with a `scanForHighlighting:` method that yields (tokenType, start, end) tuples, then apply TextAttributes as originally planned.

---

## Analysis of Existing Infrastructure

### Scanner Class (Blue Book Scanner)

**Location**: Line 34379 in Smalltalk-80.st

**Instance Variables**:
```smalltalk
source      "ReadStream on input text"
mark        "Position where current token began"
hereChar    "Current character"
aheadChar   "Lookahead character"
token       "Current token value"
tokenType   "Symbol indicating token type"
currentComment  "Comments collected during scan"
buffer      "WriteStream for building tokens"
typeTable   "Character type lookup table"
```

**Token Types Already Provided**:
- `#word` - identifiers (variables, unary selectors)
- `#keyword` - keyword selectors (with colons)
- `#binary` - binary selectors (+, -, *, etc.)
- `#string` - single-quoted strings
- `#number` - integer and float literals
- `#literal` - symbols (#symbol)
- `#leftParenthesis`, `#rightParenthesis`
- `#leftBracket`, `#rightBracket`
- `#period`, `#semicolon`, `#colon`, `#verticalBar`
- `#upArrow` - return (^)
- `#leftArrow` - assignment (_)
- `#doIt` - end of input

**Key Methods**:
- `scanToken` - advances to next token, sets `mark`, `token`, `tokenType`
- `scan: inputStream` - initializes scanner on stream
- Multi-character scan methods: `xLetter`, `xBinary`, `xDigit`, `xDoubleQuote`, `xSingleQuote`, `xLitQuote`

**Comment Handling**:
Scanner collects comments in `currentComment` instance variable as an OrderedCollection of Strings.

### Parser Class (extends Scanner)

**Location**: Line 30950 in Smalltalk-80.st

**Additional Position Tracking**:
- `hereMark` - position of current token
- `prevMark` - position of previous token
- `prevToken` - size of previous token

**Position Methods**:
```smalltalk
startOfNextToken
    "Return starting position in source of next token"
    hereType == #doIt ifTrue: [^source position + 1 + correctionDelta].
    ^hereMark + correctionDelta

endOfLastToken
    "Return ending position of last consumed token"
    hereType == #doIt ifTrue: [^ prevMark + prevToken + 1 + correctionDelta].
    tokenType == #doIt ifTrue: [^ prevMark + prevToken + correctionDelta].
    ^ prevMark + prevToken - 1 + correctionDelta
```

**Existing Syntax Styling**:
`Text>>makeSelectorBoldIn: aClass` already demonstrates syntax-aware styling:
```smalltalk
makeSelectorBoldIn: aClass
    "For formatting Smalltalk source code, set the emphasis of that portion of
    the receiver's string that parses as a message selector to be bold."
    | parser |
    string size = 0 ifTrue: [^self].
    (parser _ aClass parserClass new) parseSelector: string.
    self emphasizeFrom: 1
        to: (parser endOfLastToken min: string size)
        with: 2
```

This proves the pattern: **Parse with Scanner/Parser → get positions → apply emphasis to Text**.

### Text Class (Already Has RunArray Support)

**Location**: Line 40011 in Smalltalk-80.st

**Instance Variables**:
```smalltalk
string   "A String of Characters"
runs     "A RunArray of emphasis codes"
```

**Key Insight**: Text already uses RunArray for efficient storage of styling information! The `runs` variable stores integer emphasis codes, not TextAttribute objects.

**Current Emphasis System**:
- Emphasis codes are integers (1=normal, 2=bold, 3=bold+italic, etc.)
- TextStyle maps codes to fonts via `fontAt: emphasisCode`
- Font objects have `emphasis` bitfield (1=bold, 2=italic, 4=underlined, 8=strikethrough)

**Existing Methods**:
```smalltalk
emphasizeFrom: start to: stop with: emphasis
    "Set the emphasis for characters in the interval start-stop."
    runs _ runs copyReplaceFrom: start to: stop
        with: (RunArray new: stop - start + 1 withAll: emphasis)

emphasisAt: characterIndex
    "Answer the code for characters in the run beginning at characterIndex."
    self size = 0 ifTrue: [^1].  "null text tolerates access"
    ^runs at: characterIndex

runLengthFor: characterIndex
    "Answer the count of characters remaining in run beginning with characterIndex."
    ^runs runLengthAt: characterIndex
```

### RunArray Class (Already Exists)

**Location**: Line 34206 in Smalltalk-80.st

RunArray provides run-length encoding exactly as specified in the original plan. No need to implement it!

**Instance Variables**:
```smalltalk
runs     "Array of run lengths"
values   "Array of values (one per run)"
```

**Example**: `#(a a a b b c)` becomes `runs: #(3 2 1)`, `values: #(a b c)`

**Key Methods**:
- `at:` - get value at index (expands runs)
- `copyFrom:to:` - extract subrange preserving runs
- `copyReplaceFrom:to:with:` - splice replacement into array
- `addLast:` - append value (merges if same as last)
- `,` - concatenate (merges middle runs if values match)

### CharacterScanner (Text Rendering)

**Location**: Line 9568 in Smalltalk-80.st

**How Emphasis Currently Works**:
```smalltalk
"In CharacterScanner>>scanCharactersFrom:to:in:rightX:stopConditions:displaying:"
font _ textStyle fontAt: (text emphasisAt: lastIndex).
```

CharacterScanner gets the emphasis code from Text, looks up the font from TextStyle, and renders with that font.

**Current Limitation**: Emphasis maps to **fonts**, not colors. To support syntax highlighting with colors, we need to either:
1. Extend the emphasis system to support color (breaks existing integer model)
2. Create parallel TextAttribute system (cleaner, more extensible)

---

## Revised Architecture

### Keep From Original Plan

**Phase 1: TextAttribute Foundation** - Keep as-is
- Create TextAttribute, TextColor, TextEmphasis classes
- These provide a cleaner model than overloading integer emphasis codes

**Phase 2: Text with Attributes** - Simplify
- ~~Create RunArray~~ - **Already exists!**
- Modify Text to support TextAttribute objects in runs (currently stores integers)
- Keep backward compatibility with integer emphasis codes

**Phase 3: CharacterScanner Color Support** - Keep as-is
- Add textColor instance variable
- Modify rendering to use color

**Phase 5: SyntaxStyler** - Keep as-is
- Maps token types to TextAttributes

**Phase 6: Browser Integration** - Keep as-is

### Replace With Scanner-Based Approach

**Phase 4: DELETE SyntaxTokenizer, CREATE ScannerHighlighter**

Instead of writing a new tokenizer, create a **ScannerHighlighter** that:
1. Uses Scanner to tokenize
2. Tracks positions using Scanner's `mark`
3. Categorizes tokens for highlighting
4. Returns collection of (type, start, end) tuples

```smalltalk
Object subclass: #ScannerHighlighter
    instanceVariableNames: 'scanner tokens'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Browser-Syntax'

"ScannerHighlighter uses Scanner to tokenize Smalltalk source and
 produce position-tagged tokens for syntax highlighting."

!ScannerHighlighter methodsFor: 'scanning'!

tokenize: sourceString
    "Answer a collection of (type, start, end) tuples for highlighting."
    | stream result tokenStart |
    stream _ ReadStream on: sourceString.
    scanner _ Scanner new on: stream.
    result _ OrderedCollection new.

    "Scan all tokens"
    [scanner tokenType ~~ #doIt] whileTrue: [
        tokenStart _ scanner mark.
        self addToken: scanner tokenType
             start: tokenStart
             end: scanner mark + scanner token size - 1
             to: result.
        scanner scanToken].

    "Add comments (tracked separately by Scanner)"
    scanner currentComment ifNotNil: [
        self addCommentTokensTo: result fromSource: sourceString].

    ^result

addToken: type start: start end: end to: collection
    "Categorize token type for highlighting and add to collection."
    | highlightType |

    "Map Scanner token types to highlight categories"
    highlightType _ self highlightTypeFor: type.

    collection add: (Array with: highlightType with: start with: end)

highlightTypeFor: scannerTokenType
    "Map Scanner's token types to our highlighting categories."

    "Literals"
    scannerTokenType = #string ifTrue: [^#string].
    scannerTokenType = #number ifTrue: [^#number].
    scannerTokenType = #literal ifTrue: [^#symbol].

    "Selectors"
    scannerTokenType = #keyword ifTrue: [^#keyword].
    scannerTokenType = #binary ifTrue: [^#binarySelector].

    "Special syntax"
    scannerTokenType = #upArrow ifTrue: [^#return].
    scannerTokenType = #leftArrow ifTrue: [^#assignment].
    scannerTokenType = #leftBracket ifTrue: [^#blockStart].
    scannerTokenType = #rightBracket ifTrue: [^#blockEnd].
    scannerTokenType = #leftParenthesis ifTrue: [^#parenStart].
    scannerTokenType = #rightParenthesis ifTrue: [^#parenEnd].
    scannerTokenType = #period ifTrue: [^#statementSeparator].
    scannerTokenType = #semicolon ifTrue: [^#cascadeSeparator].
    scannerTokenType = #verticalBar ifTrue: [^#verticalBar].
    scannerTokenType = #colon ifTrue: [
        "Could be block arg (:arg) or keyword separator (key:)"
        "Need lookahead to distinguish - for now treat as keyword"
        ^#colon].

    "Identifiers - need semantic analysis to distinguish"
    scannerTokenType = #word ifTrue: [
        ^self categorizeIdentifier: scanner token].

    ^#identifier  "default"

categorizeIdentifier: word
    "Distinguish pseudo-variables, globals, and local identifiers."

    "Pseudo-variables"
    (#('self' 'super' 'true' 'false' 'nil' 'thisContext') includes: word)
        ifTrue: [^word asSymbol].

    "Globals (start with uppercase)"
    word first isUppercase ifTrue: [^#globalVar].

    "Local variable, temp, or argument"
    ^#identifier

addCommentTokensTo: collection fromSource: sourceString
    "Scanner tracks comments separately in currentComment.
     We need to find their positions in the source."
    "This is tricky - Scanner consumes comments during scanning.
     Need to re-scan just for comments or track positions during scan."
    "TODO: Enhance Scanner to track comment positions"
    ^self  "Skip for now"
! !
```

**Key Advantages**:
1. **Reuses proven Scanner code** - no duplicate tokenization logic
2. **Automatic bug fixes** - if Scanner is fixed, highlighting benefits
3. **Complete syntax coverage** - Scanner handles all edge cases (escaped quotes, radix numbers, etc.)
4. **Smaller code** - ~100 lines vs ~400 lines for new tokenizer

**Limitation**:
Scanner tracks comments separately in `currentComment` but doesn't record their positions. We need to either:
- Enhance Scanner to track comment positions
- Re-scan for comments separately
- Parse comment positions from source using simple search

### Recommended Scanner Enhancement

Add to Scanner class:

```smalltalk
Scanner subclass: #PositionTrackingScanner
    instanceVariableNames: 'source mark hereChar aheadChar token tokenType
                           currentComment buffer typeTable commentPositions'
    ...

"Track comment positions as we scan them"
xDoubleQuote
    "Collect comment - MODIFIED to track position"
    | aStream commentStart |
    commentStart _ mark.  "NEW: Save start position"
    aStream _ WriteStream on: (String new: 32).
    self step.
    [aStream nextPut: self step. hereChar == $"]
        whileFalse:
            [(hereChar = EndChar and: [source atEnd])
                ifTrue: [^self offEnd: 'Unmatched comment quote']].
    self step.
    currentComment == nil
        ifTrue: [currentComment _ OrderedCollection with: aStream contents.
                 commentPositions _ OrderedCollection with: (commentStart to: mark)]  "NEW"
        ifFalse: [currentComment add: aStream contents.
                  commentPositions add: (commentStart to: mark)].  "NEW"
    self scanToken
```

---

## Revised Phase 4: Scanner-Based Highlighting

### Phase 4A: Enhance Scanner (Optional)

Create `PositionTrackingScanner` subclass that records comment positions.

**Alternative**: Keep Scanner unchanged and use simple regex to find comments:
```smalltalk
findComments: sourceString
    "Return collection of (start, end) for all comments."
    | result pos |
    result _ OrderedCollection new.
    pos _ 1.
    [pos _ sourceString indexOf: $" startingAt: pos.
     pos > 0] whileTrue: [
        | startPos endPos |
        startPos _ pos.
        pos _ sourceString indexOf: $" startingAt: pos + 1.
        pos = 0 ifTrue: [^result].  "Unterminated comment"
        endPos _ pos.
        result add: (startPos to: endPos).
        pos _ pos + 1].
    ^result
```

### Phase 4B: Create ScannerHighlighter

Implement `ScannerHighlighter` class as shown above.

### Phase 4C: Testing

```smalltalk
"Test 1: Basic tokenization"
| tokens |
tokens _ ScannerHighlighter new tokenize: 'self foo: 42'.
tokens size.  "Should be 4"
tokens first.  "Should be #(#self 1 4)"
tokens second. "Should be #(#keyword 6 9)"
tokens third.  "Should be #(#number 11 12)"

"Test 2: Verify positions"
| source tokens |
source _ 'self foo: 42'.
tokens _ ScannerHighlighter new tokenize: source.
tokens do: [:token |
    | type start end substring |
    type _ token at: 1.
    start _ token at: 2.
    end _ token at: 3.
    substring _ source copyFrom: start to: end.
    Transcript show: type; show: ': '; show: substring; cr]

"Test 3: Comments"
| tokens |
tokens _ ScannerHighlighter new tokenize: 'self "a comment" foo'.
"Should include comment token at positions 6-17"
```

---

## Implementation Advantages

### Using Scanner vs New Tokenizer

| Aspect | Scanner Approach | New Tokenizer |
|--------|------------------|---------------|
| **Code size** | ~150 lines (wrapper + categorization) | ~400 lines (full tokenizer) |
| **Correctness** | Proven by compiler | Needs extensive testing |
| **Edge cases** | Handles all (radix, escapes, etc.) | Must implement each |
| **Maintenance** | Inherits Scanner fixes | Independent maintenance |
| **Comments** | Needs enhancement or workaround | Must implement |
| **Performance** | Same (both use Scanner primitives) | Same |

### Existing Infrastructure to Leverage

1. **RunArray** - Already exists, no implementation needed
2. **Text emphasis** - Already has methods, just extend value type
3. **Scanner tokenization** - Complete, robust, proven
4. **Parser position tracking** - Shows the pattern to follow

---

## Updated Implementation Order

| Phase | Description | Estimated Effort | Uses Scanner? |
|-------|-------------|------------------|---------------|
| 1 | TextAttribute classes | Small | No |
| 2 | Text + RunArray modifications | Small (RunArray exists!) | No |
| 3 | CharacterScanner color support | Medium | No |
| **4 (NEW)** | **ScannerHighlighter** | **Small** | **Yes** |
| 5 | SyntaxStyler | Small | No |
| 6 | Browser integration | Small | No |
| 7 | Advanced features | Large | No |

**Total Reduction**: ~250 lines of code saved by using Scanner.

---

## Open Questions

### Q1: How to handle comment positions?

**Options**:
1. **Extend Scanner** to track comment positions in `commentPositions` instance variable
2. **Re-scan for comments** using simple string search (regex-like)
3. **Parse during tokenization** by checking for `$"` characters

**Recommendation**: Option 2 (re-scan) for simplicity. Comments are rare enough that double-scanning isn't expensive.

### Q2: How to distinguish variable types?

Scanner returns `#word` for all identifiers. To distinguish instance variables, class variables, temps, and arguments, we need semantic analysis.

**Options**:
1. **Phase 1**: Use simple heuristics (uppercase = global)
2. **Phase 2**: Add class context to `styleMethod:inClass:` (like `makeSelectorBoldIn:`)
3. **Future**: Full semantic analysis with variable scope tracking

**Recommendation**: Start with Phase 1 (simple), add Phase 2 when integrating with Browser.

### Q3: Should we modify Scanner or subclass it?

**Options**:
1. **Modify Scanner** - adds comment position tracking
2. **Subclass Scanner** - keeps original unchanged
3. **Wrapper approach** - ScannerHighlighter wraps Scanner

**Recommendation**: Option 3 (wrapper). Less invasive, maintains separation of concerns.

### Q4: Text runs: integers or objects?

Current Text uses integer emphasis codes. Plan proposes TextAttribute objects.

**Options**:
1. **Keep integers** - map syntax types to emphasis codes (0-255)
2. **Use objects** - more flexible, cleaner, but changes Text internals
3. **Hybrid** - support both (polymorphic runs)

**Recommendation**: Option 2 (objects) for long-term flexibility. Modern systems need more than 256 styles.

---

## Migration Strategy

### Backward Compatibility

Existing code uses integer emphasis:
```smalltalk
text emphasizeFrom: 1 to: 5 with: 2  "bold"
```

New code uses TextAttribute:
```smalltalk
text addAttribute: TextColor blue from: 1 to: 5
```

**Solution**: RunArray stores polymorphic values - can hold both integers (old) and TextAttribute objects (new).

### Gradual Migration

1. **Phase 1-2**: Implement TextAttribute, modify Text to accept both integers and objects
2. **Phase 3**: Add color support to CharacterScanner
3. **Phase 4**: Implement ScannerHighlighter
4. **Phase 5**: Implement SyntaxStyler
5. **Phase 6**: Integrate with Browser (new code uses TextAttribute)
6. **Future**: Gradually migrate old emphasis code to TextAttribute

---

## Files to Create/Modify (Updated)

### New Smalltalk Classes
- `TextAttribute` (new) - as in original plan
- `TextColor` (new) - as in original plan
- `TextEmphasis` (new) - as in original plan
- ~~`SyntaxToken` (DELETED)~~ - use Scanner output
- ~~`SyntaxTokenizer` (DELETED)~~ - use Scanner
- `ScannerHighlighter` (new) - wraps Scanner for highlighting
- `SyntaxStyler` (new) - as in original plan
- `Color` (new or extend existing) - as in original plan

### Modified Smalltalk Classes
- `Text` - modify runs to support objects (was: add runs variable - but it already exists!)
- `String` - add asText, withColor: convenience methods
- `CharacterScanner` - add textColor support
- `DisplayScanner` - pass color to BitBlt
- `CodeView` - integrate syntax styling
- `Paragraph` - ensure Text attribute support
- ~~`RunArray` (NO CHANGES NEEDED)~~ - already exists!

### C# Changes (if needed)
- `BitBlt32Engine` - ensure Paint mode uses mask color correctly
- Verify 32bpp color rendering pipeline works with arbitrary colors

---

## Conclusion

**Major Findings**:
1. **Scanner already tokenizes** - don't reimplement
2. **RunArray already exists** - don't reimplement
3. **Text already has runs** - just extend value type
4. **makeSelectorBoldIn:** proves the pattern works

**Recommended Changes to Plan**:
1. ✅ Keep TextAttribute hierarchy (cleaner than integer codes)
2. ✅ Keep CharacterScanner color support
3. ✅ Keep SyntaxStyler
4. ❌ DELETE SyntaxTokenizer
5. ✅ ADD ScannerHighlighter (Scanner wrapper)
6. ⚠️ Phase 2: Simplify (RunArray exists, just extend Text.runs value type)

**Estimated Savings**: ~250 lines of code, plus inheriting Scanner's correctness and maintenance.

**Next Steps**:
1. Implement TextAttribute hierarchy (Phase 1)
2. Extend Text to support TextAttribute objects in runs (Phase 2)
3. Implement ScannerHighlighter using Scanner (Phase 4, moved up)
4. Add color support to CharacterScanner (Phase 3)
5. Implement SyntaxStyler (Phase 5)
6. Integrate with Browser (Phase 6)
