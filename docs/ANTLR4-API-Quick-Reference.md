# ANTLR4 C# API Quick Reference for Error Reporting

## Overview

This document provides a quick reference for ANTLR4 C# Runtime APIs commonly used in custom error reporting.

## Core Interfaces and Classes

### ICharStream

Represents the input character stream for the lexer.

```csharp
namespace Antlr4.Runtime;

public interface ICharStream : IIntStream
{
    // Get text from a range in the stream
    string GetText(Interval interval);

    // Total size of the input
    int Size { get; }

    // Current position in the stream
    int Index { get; }

    // Get character at specific index
    int LA(int offset);
}
```

**Usage for Error Reporting**:
```csharp
IToken token = ...;
ICharStream stream = token.TokenSource.InputStream;

// Get original source text for token (preserves whitespace)
string originalText = stream.GetText(
    new Interval(token.StartIndex, token.StopIndex)
);

// Get entire input
string allInput = stream.GetText(new Interval(0, stream.Size - 1));
```

---

### IToken

Represents a single token recognized by the lexer.

```csharp
namespace Antlr4.Runtime;

public interface IToken
{
    // Token type (defined in parser/lexer)
    int Type { get; }

    // Start position in character stream (0-based)
    int StartIndex { get; }

    // End position in character stream (0-based, inclusive)
    int StopIndex { get; }

    // Line number (1-based)
    int Line { get; }

    // Column number (0-based)
    int Column { get; }

    // Processed token text (may have whitespace removed)
    string Text { get; }

    // Access to lexer and input stream
    ITokenSource TokenSource { get; }

    // Channel (default = 0, hidden = 1)
    int Channel { get; }

    // Token index in stream
    int TokenIndex { get; }
}
```

**Key Properties for Error Reporting**:

| Property | Description | Use Case |
|----------|-------------|----------|
| `Line` | 1-based line number | Display "Error on line 42" |
| `Column` | 0-based character position | Position caret in error display |
| `StartIndex` | Character position in stream | Extract original text |
| `StopIndex` | End position in stream | Extract original text |
| `Text` | Processed token text | Quick reference (may lack whitespace) |
| `TokenSource.InputStream` | Original character stream | Access original source |

**Example**:
```csharp
void ShowTokenInfo(IToken token)
{
    Console.WriteLine($"Line: {token.Line}");              // e.g., "Line: 42"
    Console.WriteLine($"Column: {token.Column}");          // e.g., "Column: 18"
    Console.WriteLine($"Processed: {token.Text}");         // e.g., "value:"

    // Get original text from stream
    string original = token.TokenSource.InputStream.GetText(
        new Interval(token.StartIndex, token.StopIndex)
    );
    Console.WriteLine($"Original: {original}");            // e.g., "value:  "
}
```

---

### Interval

Represents a range in the input stream.

```csharp
namespace Antlr4.Runtime.Misc;

public class Interval
{
    // Create interval from start to stop (inclusive)
    public Interval(int start, int stop);

    // Start index (inclusive)
    public int a { get; }

    // Stop index (inclusive)
    public int b { get; }

    // Length of interval
    public int Length { get; }
}
```

**Usage**:
```csharp
// Get text from token
var interval = new Interval(token.StartIndex, token.StopIndex);
string text = charStream.GetText(interval);

// Get text from multiple tokens
var firstToken = ...;
var lastToken = ...;
var interval = new Interval(firstToken.StartIndex, lastToken.StopIndex);
string multiTokenText = charStream.GetText(interval);

// Get entire line
int lineStart = 0;  // Calculate based on line breaks
int lineEnd = 100;  // Calculate based on line breaks
var lineInterval = new Interval(lineStart, lineEnd);
string lineText = charStream.GetText(lineInterval);
```

---

### BaseErrorListener

Base class for custom error listeners.

```csharp
namespace Antlr4.Runtime;

public class BaseErrorListener : IAntlrErrorListener<IToken>, IAntlrErrorListener<int>
{
    // Called on parser syntax errors
    public virtual void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        // Default implementation does nothing
    }

    // Other methods for ambiguity detection, etc.
}
```

**Parameters Explained**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `output` | `TextWriter` | Default output (usually `Console.Error`) |
| `recognizer` | `IRecognizer` | Parser or lexer that detected error |
| `offendingSymbol` | `IToken` | Token where error occurred (may be null) |
| `line` | `int` | Line number (1-based) |
| `charPositionInLine` | `int` | Character position in line (0-based) |
| `msg` | `string` | ANTLR-generated error message |
| `e` | `RecognitionException` | Exception with details (may be null) |

**Custom Implementation**:
```csharp
public class MyErrorListener : BaseErrorListener
{
    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        // Custom error handling
        Console.WriteLine($"Error at {line}:{charPositionInLine}");
        Console.WriteLine($"Message: {msg}");

        if (offendingSymbol != null)
        {
            string tokenText = offendingSymbol.Text;
            Console.WriteLine($"Offending token: {tokenText}");
        }
    }
}
```

---

### IRecognizer

Base interface for lexers and parsers.

```csharp
namespace Antlr4.Runtime;

public interface IRecognizer
{
    // Grammar name
    string GrammarFileName { get; }

    // Rule names (parser only)
    string[] RuleNames { get; }

    // Token names
    string[] TokenNames { get; }

    // Get display name for token type
    string GetTokenDisplayName(int tokenType);

    // Error listeners
    void AddErrorListener(IAntlrErrorListener<IToken> listener);
    void RemoveErrorListeners();
}
```

**Usage in Error Listener**:
```csharp
public override void SyntaxError(
    TextWriter output,
    IRecognizer recognizer,
    IToken offendingSymbol,
    int line,
    int charPositionInLine,
    string msg,
    RecognitionException e)
{
    // Get grammar name
    string grammar = recognizer.GrammarFileName;
    Console.WriteLine($"Error in {grammar}");

    // Get token display name
    if (offendingSymbol != null)
    {
        string tokenName = recognizer.GetTokenDisplayName(offendingSymbol.Type);
        Console.WriteLine($"Unexpected {tokenName}");
    }
}
```

---

### RecognitionException

Exception thrown during parsing.

```csharp
namespace Antlr4.Runtime;

public class RecognitionException : Exception
{
    // Recognizer that detected the error
    public IRecognizer Recognizer { get; }

    // Context where error occurred
    public RuleContext Context { get; }

    // Token where error occurred
    public IToken OffendingToken { get; }

    // Input stream position
    public int OffendingState { get; }
}
```

**Usage**:
```csharp
public override void SyntaxError(..., RecognitionException e)
{
    if (e != null)
    {
        // Get detailed error information
        var context = e.Context;
        var state = e.OffendingState;

        Console.WriteLine($"Error in state {state}");
        Console.WriteLine($"Context: {context?.GetType().Name}");
    }
}
```

---

## Common Patterns

### Pattern 1: Get Original Source Text for Token

```csharp
public string GetOriginalTokenText(IToken token)
{
    if (token?.TokenSource?.InputStream == null)
        return token?.Text ?? "";

    ICharStream stream = token.TokenSource.InputStream;
    return stream.GetText(new Interval(token.StartIndex, token.StopIndex));
}
```

### Pattern 2: Get Entire Line from Stream

```csharp
public string GetLine(ICharStream stream, int lineNumber)
{
    string fullText = stream.GetText(new Interval(0, stream.Size - 1));
    string[] lines = fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    if (lineNumber < 1 || lineNumber > lines.Length)
        return "";

    return lines[lineNumber - 1];
}
```

### Pattern 3: Find Line Start/End Indices

```csharp
public (int start, int end) GetLineIndices(ICharStream stream, int lineNumber)
{
    string fullText = stream.GetText(new Interval(0, stream.Size - 1));

    int currentLine = 1;
    int start = 0;

    for (int i = 0; i < fullText.Length; i++)
    {
        if (fullText[i] == '\n')
        {
            if (currentLine == lineNumber)
                return (start, i - 1);

            currentLine++;
            start = i + 1;
        }
    }

    // Last line
    if (currentLine == lineNumber)
        return (start, fullText.Length - 1);

    return (-1, -1);
}
```

### Pattern 4: Get Multi-Token Context

```csharp
public string GetContext(IToken centerToken, int tokensBefore, int tokensAfter)
{
    if (centerToken?.TokenSource == null)
        return "";

    ITokenSource tokenSource = centerToken.TokenSource;
    ICharStream stream = tokenSource.InputStream;

    // Find surrounding tokens
    // Note: This is simplified - you'd need access to the token stream
    int startIndex = Math.Max(0, centerToken.StartIndex - 100);  // Estimate
    int endIndex = Math.Min(stream.Size - 1, centerToken.StopIndex + 100);  // Estimate

    return stream.GetText(new Interval(startIndex, endIndex));
}
```

### Pattern 5: Custom Error Formatting

```csharp
public class FormattedErrorListener : BaseErrorListener
{
    private readonly string _fileName;
    private readonly ICharStream _stream;

    public FormattedErrorListener(string fileName, ICharStream stream)
    {
        _fileName = fileName;
        _stream = stream;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        // Format: "file:line:column: message"
        Console.WriteLine($"{_fileName}:{line}:{charPositionInLine}: {msg}");

        // Show source line
        string sourceLine = GetLine(_stream, line);
        Console.WriteLine(sourceLine);

        // Show caret
        string caret = new string(' ', charPositionInLine) + "^";
        Console.WriteLine(caret);
    }

    private string GetLine(ICharStream stream, int lineNumber)
    {
        // Implementation from Pattern 2
        ...
    }
}
```

---

## Integration with Parser

### Setup Lexer and Parser

```csharp
// Read source
string sourceText = File.ReadAllText("input.txt");

// Create input stream
var inputStream = new AntlrInputStream(sourceText);

// Create lexer
var lexer = new MyLexer(inputStream);

// Create token stream
var tokenStream = new CommonTokenStream(lexer);

// Create parser
var parser = new MyParser(tokenStream);

// Remove default error listeners
parser.RemoveErrorListeners();
lexer.RemoveErrorListeners();

// Add custom error listener
var errorListener = new MyErrorListener("input.txt", sourceText);
parser.AddErrorListener(errorListener);
lexer.AddErrorListener(errorListener);  // If lexer errors are needed

// Parse
var tree = parser.startRule();
```

### Access Token Stream in Error Listener

```csharp
public class ContextAwareErrorListener : BaseErrorListener
{
    private readonly CommonTokenStream _tokens;

    public ContextAwareErrorListener(CommonTokenStream tokens)
    {
        _tokens = tokens;
    }

    public override void SyntaxError(...)
    {
        if (offendingSymbol != null)
        {
            // Get surrounding tokens
            int index = offendingSymbol.TokenIndex;
            var before = _tokens.Get(Math.Max(0, index - 3));
            var after = _tokens.Get(Math.Min(_tokens.Size - 1, index + 3));

            // Show context
            Console.WriteLine($"Context: {before.Text} ... {after.Text}");
        }
    }
}
```

---

## Best Practices

### 1. Always Preserve Original Source

```csharp
// ✅ GOOD: Keep original source for error reporting
string source = File.ReadAllText(file);
var stream = new AntlrInputStream(source);
var errorListener = new MyListener(file, source);

// ❌ BAD: Don't discard source after creating stream
var stream = new AntlrInputStream(File.ReadAllText(file));
var errorListener = new MyListener();  // No source access
```

### 2. Use ICharStream.GetText() for Original Formatting

```csharp
// ✅ GOOD: Get original text with whitespace
string original = token.TokenSource.InputStream.GetText(
    new Interval(token.StartIndex, token.StopIndex)
);

// ❌ BAD: Token.Text may have whitespace stripped
string text = token.Text;
```

### 3. Handle Null Tokens

```csharp
// ✅ GOOD: Check for null
if (offendingSymbol != null)
{
    string text = offendingSymbol.Text;
}

// ❌ BAD: Assume token is not null
string text = offendingSymbol.Text;  // May throw NullReferenceException
```

### 4. Provide Context

```csharp
// ✅ GOOD: Show surrounding lines
ShowContext(line - 1, line, line + 1);

// ❌ BAD: Show only error line
ShowLine(line);
```

### 5. Calculate Caret Position Correctly

```csharp
// ✅ GOOD: Account for all prefixes
int caretPos = lineNumberWidth + separatorWidth + charPositionInLine;

// ❌ BAD: Use raw position
int caretPos = charPositionInLine;  // Misaligned with line numbers
```

---

## ANTLR4 NuGet Package

```xml
<PackageReference Include="Antlr4.Runtime.Standard" Version="4.13.1" />
```

Or latest version:
```bash
dotnet add package Antlr4.Runtime.Standard
```

---

## Complete Example

```csharp
using System;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

public class EnhancedErrorListener : BaseErrorListener
{
    private readonly string _fileName;
    private readonly string[] _lines;

    public EnhancedErrorListener(string fileName, string source)
    {
        _fileName = Path.GetFileName(fileName);
        _lines = source.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        // Header
        Console.WriteLine($"\n=== PARSE ERROR in {_fileName} ===\n");

        // Context
        for (int i = Math.Max(0, line - 2); i < Math.Min(_lines.Length, line + 1); i++)
        {
            string marker = (i == line - 1) ? "->" : "  ";
            Console.WriteLine($"{marker} {i + 1,4}: {_lines[i]}");

            if (i == line - 1)
            {
                string caret = new string(' ', charPositionInLine + 8) + "^";
                Console.WriteLine(caret);
            }
        }

        // Message
        Console.WriteLine($"\nError: {msg}");

        // Token info
        if (offendingSymbol != null)
        {
            string original = GetOriginalText(offendingSymbol);
            Console.WriteLine($"Token: '{original}' at {charPositionInLine}\n");
        }
    }

    private string GetOriginalText(IToken token)
    {
        var stream = token.TokenSource?.InputStream;
        if (stream == null) return token.Text;

        try
        {
            return stream.GetText(new Interval(token.StartIndex, token.StopIndex));
        }
        catch
        {
            return token.Text;
        }
    }
}

// Usage:
string source = File.ReadAllText("input.st");
var input = new AntlrInputStream(source);
var lexer = new Smalltalk80Lexer(input);
var tokens = new CommonTokenStream(lexer);
var parser = new Smalltalk80Parser(tokens);

parser.RemoveErrorListeners();
parser.AddErrorListener(new EnhancedErrorListener("input.st", source));

var tree = parser.fileIn();
```

---

## Summary

Key ANTLR4 APIs for error reporting:

- **ICharStream.GetText(Interval)** - Get original source text
- **IToken.StartIndex/StopIndex** - Token position in stream
- **IToken.Line/Column** - Position for display
- **Interval** - Specify ranges in input
- **BaseErrorListener.SyntaxError** - Override for custom errors

This enables creating rich, user-friendly error messages with proper source context.
