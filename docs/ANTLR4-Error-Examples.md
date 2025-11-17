# ANTLR4 Enhanced Error Reporting - Examples

## Before and After Comparison

### Example 1: Nested Block Error

**Original Source Code** (BlockContextTest.st, line 430):
```smalltalk
n011011 "O[0,1] M[1,0] I[1,1]: middle arg, inner accesses outer temp"
^[| ot | ot _ 4. [:ma | [:ia | | it | it _ 3. ot + it] value: 999] value: 888] value]
```

**BEFORE** (Old Error Format):
```
Line 430:85 - no viable alternative at input '^[|ot|ot_4.[:ma|[:ia||it|it_3.ot+it]value:999]value:888]value]'
```

Problems:
- All whitespace removed - hard to read
- No context - what comes before/after?
- No visual indicator of exact error position
- No file name shown

**AFTER** (Enhanced Error Format):
```
=== PARSE ERROR in BlockContextTest.st ===

    429: n011011 "O[0,1] M[1,0] I[1,1]: middle arg, inner accesses outer temp"
 -> 430: ^[| ot | ot _ 4. [:ma | [:ia | | it | it _ 3. ot + it] value: 999] value: 888] value]
                                                                                             ^
    431:

Error: no viable alternative at input '^[| ot | ot _ 4. [:ma | [:ia | | it |'
Token: ']' at position 84
```

Benefits:
- Original formatting preserved - easy to read
- Context lines show surrounding code
- Caret points to exact error position
- File name clearly shown
- Arrow marker highlights error line

---

### Example 2: Missing Pipe Character

**Original Source Code** (TestMethods.st, line 99):
```smalltalk
testMethod
    | temp1 temp2 temp3
    temp1 := 42.
    ^temp1
!
```

**BEFORE**:
```
Line 99:18 - mismatched input 'temp1' expecting PIPE
```

**AFTER**:
```
=== PARSE ERROR in TestMethods.st ===

     98: testMethod
 ->  99:     | temp1 temp2 temp3
                               ^
    100:     temp1 := 42.

Error: mismatched input 'temp3' expecting PIPE
Token: 'temp3' at position 18
```

---

### Example 3: Unexpected Token in Expression

**Original Source Code** (Expressions.st, line 42):
```smalltalk
calculate: arg
    | result |
    result := arg + 10 * 5..
    ^result
!
```

**BEFORE**:
```
Line 42:27 - extraneous input '.' expecting {'!', IDENTIFIER, ...}
```

**AFTER**:
```
=== PARSE ERROR in Expressions.st ===

     41:     | result |
 ->  42:     result := arg + 10 * 5..
                                   ^
     43:     ^result

Error: extraneous input '.' expecting {'!', IDENTIFIER, KEYWORD_SELECTOR}
Token: '.' at position 27
```

---

### Example 4: Token Recognition Error

**Original Source Code** (BadTokens.st, line 16):
```smalltalk
Float initialize.
@#$InvalidToken here
Character initialize.
```

**BEFORE**:
```
Line 16:0 - token recognition error at: '@'
```

**AFTER**:
```
=== PARSE ERROR in BadTokens.st ===

     15: Float initialize.
 ->  16: @#$InvalidToken here
         ^
     17: Character initialize.

Error: token recognition error at: '@'
Token: '@' at position 0
```

---

### Example 5: Class Definition Error

**Original Source Code** (ClassDefinitions.st, line 23):
```smalltalk
Object subclass: #TestClass
    instanceVariableNames: 'var1 var2'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests' !  "Extra '!' - should be on next line"
```

**BEFORE**:
```
Line 23:18 - no viable alternative at input 'category:'Tests'!'
```

**AFTER**:
```
=== PARSE ERROR in ClassDefinitions.st ===

     22:     poolDictionaries: ''
 ->  23:     category: 'Tests' !
                                ^
     24:

Error: no viable alternative at input 'category: 'Tests' !'
Token: '!' at position 18
```

---

### Example 6: Missing Method Terminator

**Original Source Code** (MissingBang.st, lines 55-58):
```smalltalk
getValue
    ^42

anotherMethod  "Missing '!' after previous method"
    ^self
!
```

**BEFORE**:
```
Line 57:0 - mismatched input 'anotherMethod' expecting '!'
```

**AFTER**:
```
=== PARSE ERROR in MissingBang.st ===

     56:     ^42
 ->  57:
         ^
     58: anotherMethod

Error: mismatched input 'anotherMethod' expecting '!'
Token: 'anotherMethod' at position 0
```

---

### Example 7: Multi-Line Expression Error

**Original Source Code** (MultiLine.st, lines 102-106):
```smalltalk
complexCalculation
    | temp |
    temp := (self getValue1 +
             self getValue2 *
             self getValue3) +.  "Extra '+' operator"
    ^temp
!
```

**BEFORE**:
```
Line 106:28 - mismatched input '.' expecting {IDENTIFIER, ...}
```

**AFTER**:
```
=== PARSE ERROR in MultiLine.st ===

    105:              self getValue3) +.
                                        ^
    106:     ^temp

Error: mismatched input '.' expecting {IDENTIFIER, KEYWORD_SELECTOR, LITERAL, ...}
Token: '.' at position 28
```

---

## Key Improvements Summary

### 1. Readability
- **Before**: `'^[|ot|ot_4.[:ma|`
- **After**: `^[| ot | ot _ 4. [:ma |`

The enhanced format preserves all whitespace, making complex nested structures readable.

### 2. Context
- **Before**: Single line with no context
- **After**: 1 line before + error line + 1 line after (configurable)

Context helps understand what the code is trying to do and where the error fits in the structure.

### 3. Visual Indicators
- **Before**: Line:column number only
- **After**:
  - Arrow marker `->` on error line
  - Caret `^` pointing to exact character position
  - Line numbers for reference

### 4. File Identification
- **Before**: No file name
- **After**: Clear header `=== PARSE ERROR in filename.st ===`

Critical for multi-file compilation - users know immediately which file to fix.

### 5. Original Token Text
- **Before**: Processed token text only
- **After**: Shows token extracted from original source using `ICharStream`

Ensures what you see in the error matches what's in the file.

---

## Implementation Notes

### Character Position Accuracy

The caret position is calculated to account for all prefix elements:

```
 -> 430: ^[| ot | ot _ 4. [:ma | [:ia | | it | it _ 3. ot + it] value: 999]
^^^^    ^^                                                                    ^
|       |                                                                     |
|       +-- ": " separator (2 chars)                                         |
|                                                                             |
+-- Marker " -> " (4 chars)                                                   |
                                                                              |
    Line number "430" (variable width)                                       |
                                                                              |
                          Character position from ANTLR ---> ----------------+
```

### Line Splitting

The source text is split preserving empty lines:

```csharp
_sourceLines = sourceText.Split(
    new[] { "\r\n", "\r", "\n" },
    StringSplitOptions.None  // Keep empty lines
);
```

This ensures accurate line number mapping.

### Bounds Checking

All array access is bounds-checked:

```csharp
int startLine = Math.Max(1, line - ContextLinesBefore);
int endLine = Math.Min(_sourceLines.Length, line + ContextLinesAfter);
string sourceLine = i <= _sourceLines.Length ? _sourceLines[i - 1] : "";
```

This prevents crashes on edge cases (error at line 1, end of file, etc.).

---

## Testing Recommendations

### Create Error Test Suite

Create a test file with intentional errors to verify formatting:

```smalltalk
"ErrorTestSuite.st - Intentional errors for testing error reporting"

"Error 1: Missing pipe"
Object subclass: #Test1
    instanceVariableNames: 'var1 var2 var3
    ...

"Error 2: Extra bracket"
method1
    ^[| temp | temp := 42. temp]]
!

"Error 3: Missing assignment operator"
method2
    | x |
    x 42.
    ^x
!

"Error 4: Invalid token"
method3
    ^@#$%^
!
```

### Verify Each Error

Run the compiler and verify:
1. File name is shown correctly
2. Line numbers are accurate
3. Source text matches file content
4. Caret points to the right character
5. Context lines are relevant
6. Error message is clear

---

## Configuration Options

### Adjust Context Size

Modify constants for different context amounts:

```csharp
// Show more context for complex methods
private const int ContextLinesBefore = 3;
private const int ContextLinesAfter = 3;

// Minimal context for quick scanning
private const int ContextLinesBefore = 0;
private const int ContextLinesAfter = 0;
```

### Color Coding (Optional)

Add console colors for better visibility:

```csharp
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"=== PARSE ERROR in {_fileName} ===");
Console.ResetColor();

// Error line in yellow
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"{marker}{lineNumPrefix}: {sourceLine}");
Console.ResetColor();

// Caret in red
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine(caretLine);
Console.ResetColor();
```

### HTML Output (Optional)

For IDE integration, generate HTML output:

```csharp
output.WriteLine($"<div class='parse-error'>");
output.WriteLine($"<span class='file'>{_fileName}</span>");
output.WriteLine($"<span class='line'>{line}</span>");
output.WriteLine($"<pre class='code'>{EscapeHtml(sourceLine)}</pre>");
output.WriteLine($"</div>");
```

---

## Conclusion

The enhanced error reporting transforms cryptic one-line errors into clear, actionable messages that show:

- **Where**: File name and line number
- **What**: Original source code with formatting
- **Exactly where**: Visual caret pointing to error
- **Why**: ANTLR's error message with token info
- **Context**: Surrounding code for understanding

This dramatically improves the developer experience when dealing with parse errors in complex Smalltalk code.
