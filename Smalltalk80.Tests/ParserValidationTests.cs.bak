using System;
using System.IO;
using System.Diagnostics;
using Xunit;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Tests;

/// <summary>
/// Tests to validate that the parser continues to work correctly with the Smalltalk-80 system image.
/// </summary>
public class ParserValidationTests
{
    private const string SystemImagePath = @"Smalltalk-80 Source\Smalltalk-80.st";

    [Fact]
    public void Parser_ShouldSuccessfullyParse_Smalltalk80SystemImage()
    {
        // Arrange
        var fullPath = Path.Combine(GetProjectRoot(), SystemImagePath);
        Skip.If(!File.Exists(fullPath), $"System image file not found at: {fullPath}");
        
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var input = File.ReadAllText(fullPath);
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        // Use a custom error listener to track errors
        var errorListener = new TestErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        
        var tree = parser.fileIn();
        stopwatch.Stop();
        
        // Assert
        Assert.Empty(errorListener.Errors);
        Assert.NotNull(tree);
        
        // Log statistics
        var lines = parser.CurrentToken.Line;
        Console.WriteLine($"Successfully parsed Smalltalk-80.st:");
        Console.WriteLine($"  - Lines: {lines:N0}");
        Console.WriteLine($"  - Time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  - Tokens: {tokens.Size:N0}");
    }

    [Fact]
    public void Parser_ShouldSuccessfullyParse_StandardLibrary()
    {
        // Arrange
        var standardLibraryPath = Path.Combine(GetProjectRoot(), "StandardLibrary.st");
        Skip.If(!File.Exists(standardLibraryPath), $"StandardLibrary.st not found at: {standardLibraryPath}");
        
        // Act
        var input = File.ReadAllText(standardLibraryPath);
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        var errorListener = new TestErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        
        var tree = parser.fileIn();
        
        // Assert
        Assert.Empty(errorListener.Errors);
        Assert.NotNull(tree);
    }

    [Theory]
    [InlineData("Object subclass: #TestClass instanceVariableNames: '' classVariableNames: '' poolDictionaries: '' category: 'Test'!")]
    [InlineData("!Object methodsFor: 'testing'! testMethod ^self! !")]
    [InlineData("3 + 4!")]
    [InlineData("'hello world' size!")]
    [InlineData("[:x | x * 2] value: 5!")]
    public void Parser_ShouldSuccessfullyParse_BasicSmalltalkConstructs(string code)
    {
        // Act
        var inputStream = new AntlrInputStream(code);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        var errorListener = new TestErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        
        var tree = parser.fileIn();
        
        // Assert
        Assert.Empty(errorListener.Errors);
        Assert.NotNull(tree);
    }

    [Fact]
    public void Parser_ShouldReportErrors_ForInvalidSyntax()
    {
        // Arrange
        var invalidCode = "Object subclass #TestClass!"; // Missing colon after subclass
        
        // Act
        var inputStream = new AntlrInputStream(invalidCode);
        var lexer = new Smalltalk80Lexer(inputStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        var errorListener = new TestErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        
        var tree = parser.fileIn();
        
        // Assert
        Assert.NotEmpty(errorListener.Errors); // Should have syntax errors
    }

    private static string GetProjectRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "Smalltalk80.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? Directory.GetCurrentDirectory();
    }

    private class TestErrorListener : BaseErrorListener
    {
        public List<string> Errors { get; } = new List<string>();

        public override void SyntaxError(
            TextWriter output,
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            Errors.Add($"Line {line}:{charPositionInLine} - {msg}");
        }
    }
}