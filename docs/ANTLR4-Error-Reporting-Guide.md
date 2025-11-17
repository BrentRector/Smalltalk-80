# ANTLR4 Error Reporting Guide for C#

## Overview

This guide documents the enhanced error reporting system implemented in the Smalltalk-80 compiler. The system displays parse errors with original source code context, proper whitespace, and clear visual indicators.

## Problem Statement

**Before**: Error messages showed whitespace-stripped tokens, making errors hard to understand:
```
Line 430:85 - no viable alternative at input '^[|ot|ot_4.[:ma|[:ia||it|it_3.ot+it]value:999]value:888]value]'
```

**After**: Error messages show original source with context and visual indicators:
```
=== PARSE ERROR in BlockContextTest.st ===

    429: n011011 "O[0,1] M[1,0] I[1,1]: middle arg, inner accesses outer temp"
 -> 430: ^[| ot | ot _ 4. [:ma | [:ia | | it | it _ 3. ot + it] value: 999] value: 888] value]
                                                                                             ^
    431:

Error: no viable alternative at input '^[| ot | ot _ 4. [:ma | [:ia | | it |'
Token: ']' at position 84
```

## Implementation

### 1. Enhanced Error Listener

The `ConsoleErrorListener` class has been enhanced with:

- **File name tracking**: Shows which file contains the error
- **Source text access**: Stores original source to preserve whitespace
- **Context display**: Shows lines before/after the error
- **Visual indicators**: Arrow marker and caret pointing to error position
- **Original token text**: Extracts token text from `ICharStream` preserving formatting

**Key Features**:

```csharp
public ConsoleErrorListener(string fileName, string sourceText)
{
    _fileName = Path.GetFileName(fileName);
    _sourceLines = sourceText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
}
```

### 2. Source Text Extraction

The error listener uses ANTLR's `ICharStream.GetText()` with `Interval` to extract original source text:

```csharp
private string GetTokenSourceText(IToken token)
{
    ICharStream inputStream = token.TokenSource.InputStream;
    int start = token.StartIndex;
    int stop = token.StopIndex;

    // Get text directly from character stream (preserves original formatting)
    return inputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop));
}
```

**Why this works**:
- `token.Text` contains the processed token text (whitespace stripped)
- `ICharStream.GetText(interval)` returns the **original** source characters
- Token's `StartIndex` and `StopIndex` point to positions in the original stream

### 3. Usage in Parser

The parser initialization passes both filename and source text to the error listener:

```csharp
string input = File.ReadAllText(inputFile);
var inputStream = new AntlrInputStream(input);
var lexer = new Smalltalk80Lexer(inputStream);
var tokenStream = new CommonTokenStream(lexer);
var parser = new Smalltalk80Parser(tokenStream);

// Add enhanced error listener with source context
var errorListener = new ConsoleErrorListener(inputFile, input);
parser.RemoveErrorListeners();
parser.AddErrorListener(errorListener);
```

## Best Practices

### 1. Access Original Source Text

**Always use `ICharStream.GetText()` for displaying source**:

```csharp
// ❌ BAD: Shows processed token text
string text = token.Text;

// ✅ GOOD: Shows original source text with whitespace
ICharStream stream = token.TokenSource.InputStream;
string text = stream.GetText(new Interval(token.StartIndex, token.StopIndex));
```

### 2. Handle Multi-File Compilation

Pass the filename to the error listener so users know which file has the error:

```csharp
var errorListener = new ConsoleErrorListener(inputFile, input);
```

The error listener extracts just the filename for display:

```csharp
_fileName = Path.GetFileName(fileName);
```

### 3. Context Lines

The current implementation shows 1 line before and after the error:

```csharp
private const int ContextLinesBefore = 1;
private const int ContextLinesAfter = 1;
```

For longer methods or complex expressions, consider increasing these values:

```csharp
private const int ContextLinesBefore = 2;  // Show 2 lines before
private const int ContextLinesAfter = 2;   // Show 2 lines after
```

### 4. Line Number Alignment

The implementation calculates line number width dynamically for proper alignment:

```csharp
int lineNumWidth = endLine.ToString().Length;
string lineNumPrefix = i.ToString().PadLeft(lineNumWidth);
```

This ensures clean formatting even with large files:

```
    997: prevLine
    998: currentLine
    999: nextLine
 -> 1000: errorLine
          ^
   1001: followingLine
```

### 5. Caret Position Calculation

The caret position accounts for all prefix elements:

```csharp
int caretOffset = marker.Length  // " -> " or "    " (4 chars)
                + lineNumWidth   // Line number digits
                + 2              // ": " separator
                + charPositionInLine;  // ANTLR's character position
```

## ANTLR4-Specific Utilities

### ICharStream Methods

```csharp
// Get text from interval
string GetText(Interval interval)

// Get size of stream
int Size { get; }

// Get current index
int Index { get; }
```

### IToken Properties

```csharp
int StartIndex      // Character position in stream where token starts
int StopIndex       // Character position in stream where token ends
int Line            // Line number (1-based)
int Column          // Column number (0-based) - same as charPositionInLine
string Text         // Processed token text (may have whitespace stripped)
ITokenSource TokenSource  // Access to lexer and input stream
```

### Interval Class

```csharp
// Create interval from start to stop (inclusive)
var interval = new Antlr4.Runtime.Misc.Interval(startIndex, stopIndex);

// Use with GetText
string originalText = charStream.GetText(interval);
```

## Example Error Output

### Syntax Error

```
=== PARSE ERROR in TestMethods.st ===

     98: "Test method with syntax error"
 -> 99: method: arg1 | temp |
                     ^
    100:     temp := arg1.

Error: mismatched input '|' expecting PIPE
Token: '|' at position 20
```

### Unexpected Input

```
=== PARSE ERROR in ClassDefinitions.st ===

     42:     ^self new
 -> 43: ]
        ^
     44:

Error: extraneous input ']' expecting {'!', IDENTIFIER, KEYWORD_SELECTOR, ...}
Token: ']' at position 0
```

### Token Recognition Error

```
=== PARSE ERROR in InitExpressions.st ===

     15: Float initialize.
 -> 16: @#$InvalidToken here
        ^
     17: Character initialize.

Error: token recognition error at: '@'
Token: '@' at position 0
```

## Advanced Customization

### Custom Error Formatting

You can customize the error format by modifying the `SyntaxError` method:

```csharp
public override void SyntaxError(...)
{
    // Custom header
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"SYNTAX ERROR in {_fileName}:{line}");
    Console.ResetColor();

    // Custom formatting...
}
```

### Error Recovery Suggestions

Add suggested fixes based on error type:

```csharp
if (msg.Contains("expecting '!'"))
{
    Console.WriteLine("Suggestion: Methods must end with '!' character");
}
else if (msg.Contains("expecting PIPE"))
{
    Console.WriteLine("Suggestion: Temporary variables must be enclosed in '| |'");
}
```

### Lexer Error Handling

The error listener also handles lexer errors. Add a lexer error listener if needed:

```csharp
var lexerErrorListener = new ConsoleErrorListener(inputFile, input);
lexer.RemoveErrorListeners();
lexer.AddErrorListener(lexerErrorListener);
```

## Testing

### Creating Test Cases

Create a test file with intentional errors:

```smalltalk
"Test file with errors"

Object subclass: #TestClass | temp |  "Missing instanceVariableNames:"
    ...!

TestClass methodsFor: 'testing'!

badMethod
    | temp1 temp2 |  "Missing PIPE at end"
    temp1 := 42.
    ^temp1!

missingBang
    ^self
" Missing method terminator '!' "

```

### Verifying Output

Run the compiler on the test file and verify:

1. Each error shows the correct file name
2. Source lines are displayed with proper whitespace
3. Caret points to the exact error position
4. Context lines provide helpful surrounding code
5. Error message from ANTLR is clear and actionable

## Performance Considerations

### Memory Usage

The error listener stores the entire source file as a string array:

```csharp
_sourceLines = sourceText.Split(...);
```

For very large files (>100MB), consider:
- Reading lines on-demand from disk
- Caching only lines near errors
- Using memory-mapped files

### Error Count

ANTLR's error recovery may generate multiple errors. Consider limiting output:

```csharp
private int _errorCount = 0;
private const int MaxErrors = 10;

public override void SyntaxError(...)
{
    if (++_errorCount > MaxErrors)
    {
        Console.WriteLine($"Too many errors ({_errorCount}). Stopping after {MaxErrors}.");
        return;
    }
    // ... normal error reporting
}
```

## Troubleshooting

### Caret Position Off by One

If the caret is misaligned:
- Verify `charPositionInLine` is 0-based (ANTLR standard)
- Check for tabs vs spaces in source (tabs expand to 8 spaces by default)
- Ensure line number formatting width is calculated correctly

### Missing Source Lines

If context lines don't show:
- Verify source text splitting handles all line endings (`\r\n`, `\r`, `\n`)
- Check array bounds (use `Math.Max` and `Math.Min`)
- Ensure 1-based line numbers are converted to 0-based array indices

### Token Text Different from Source

If token text doesn't match what you see:
- Confirm you're using `ICharStream.GetText()` with `Interval`
- Verify token's `StartIndex` and `StopIndex` are valid
- Check for whitespace-handling options in lexer grammar

## References

- **ANTLR4 Runtime API**: `Antlr4.Runtime` namespace
- **ICharStream**: `Antlr4.Runtime.ICharStream`
- **IToken**: `Antlr4.Runtime.IToken`
- **Interval**: `Antlr4.Runtime.Misc.Interval`
- **BaseErrorListener**: `Antlr4.Runtime.BaseErrorListener`

## Files Modified

- `/home/user/Smalltalk-80/Smalltalk80.Compiler/Program.cs`
  - Enhanced `ConsoleErrorListener` class (lines 226-328)
  - Updated `ParseSmalltalkSource` to pass source context (lines 180-223)

## Summary

The enhanced error reporting system provides:

✅ Original source code display with whitespace
✅ Visual indicators (arrows, carets) for error location
✅ Context lines showing surrounding code
✅ File name for multi-file compilation
✅ Token text extracted from original source
✅ Clean, readable formatting with line numbers

This makes parse errors much easier to diagnose and fix, especially for complex nested expressions like Smalltalk blocks.
