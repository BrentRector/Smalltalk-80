using System;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace GrammarTest;

/// <summary>
/// Simple test to debug grammar issues
/// </summary>
class SimpleTest
{
    static void MainSimple()
    {
        // Test temporaries parsing
        TestParse("Temporaries test", "| x | x := 10. x + 5", 
            parser => parser.temporariesEntry());
        
        // Test simple class
        TestParse("Simple class", @"Object subclass: #Test
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!", 
            parser => parser.fileIn());
        
        // Test method with temporaries  
        TestParse("Method with temps", @"test
	| temp |
	temp := 5.
	^temp + 1", 
            parser => parser.methodEntry());
    }
    
    static void TestParse(string name, string input, Func<Smalltalk80Parser, IParseTree> parse)
    {
        Console.WriteLine($"\n=== {name} ===");
        Console.WriteLine($"Input: {input.Replace('\n', '\\').Replace('\r', '\\').Replace('\t', '.')}");
        
        var inputStream = new AntlrInputStream(input);
        var lexer = new Smalltalk80Lexer(inputStream);
        
        // Show tokens
        Console.WriteLine("\nTokens:");
        var tokens = new CommonTokenStream(lexer);
        tokens.Fill();
        foreach (var token in tokens.GetTokens())
        {
            if (token.Type != Smalltalk80Lexer.Eof)
            {
                var typeName = lexer.Vocabulary.GetSymbolicName(token.Type) ?? token.Type.ToString();
                Console.WriteLine($"  [{token.Type}] {typeName}: '{token.Text}'");
            }
        }
        
        // Reset and parse
        inputStream = new AntlrInputStream(input);
        lexer = new Smalltalk80Lexer(inputStream);
        tokens = new CommonTokenStream(lexer);
        var parser = new Smalltalk80Parser(tokens);
        
        var errorListener = new TestErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        
        var tree = parse(parser);
        
        if (errorListener.Errors.Count > 0)
        {
            Console.WriteLine("\nERRORS:");
            foreach (var error in errorListener.Errors)
                Console.WriteLine($"  {error}");
        }
        else
        {
            Console.WriteLine("\nSUCCESS - Parsed correctly");
        }
    }
}

class TestErrorListener : BaseErrorListener
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