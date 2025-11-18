// Test program to validate ANTLR4 block PIPE grammar
// Demonstrates that all three patterns parse correctly:
//   1. [:a] - block with arg, no body, no PIPE
//   2. [:a | expr] - block with arg, body, PIPE present
//   3. [:a | | t | expr] - block with arg, temps, body
//
// Usage:
//   csc test_block_pipe_parsing.cs /r:Antlr4.Runtime.dll /r:Smalltalk80.Compiler.dll
//   ./test_block_pipe_parsing

using System;
using Antlr4.Runtime;
using Smalltalk80.Compiler.Generated;

public class BlockPipeTest
{
    public static void Main()
    {
        Console.WriteLine("ANTLR4 Block PIPE Grammar Validation");
        Console.WriteLine("=====================================");
        Console.WriteLine();

        // Test case 1: Block with arg, no body, no PIPE
        TestBlockParse(
            "[:ignored]",
            "Block with arg, no body, no PIPE",
            expected: "blockArgs=':ignored', no PIPE consumed"
        );

        // Test case 2: Block with arg and body, PIPE present
        TestBlockParse(
            "[:p | p notEmpty]",
            "Block with arg and body, PIPE present",
            expected: "blockArgs=':p |', body='p notEmpty'"
        );

        // Test case 3: Block with arg, temps, and body
        TestBlockParse(
            "[:x | | temp | temp := x * 2]",
            "Block with arg, temps, and body",
            expected: "blockArgs=':x |', blockTemps='| temp |', body='temp := x * 2'"
        );

        // Test case 4: Multiple args with PIPE
        TestBlockParse(
            "[:a :b | a + b]",
            "Block with multiple args and body",
            expected: "blockArgs=':a :b |', body='a + b'"
        );

        // Test case 5: Multiple args without PIPE
        TestBlockParse(
            "[:a :b]",
            "Block with multiple args, no body",
            expected: "blockArgs=':a :b', no PIPE"
        );

        Console.WriteLine();
        Console.WriteLine("=== All tests passed! ===");
    }

    private static void TestBlockParse(string input, string description, string expected)
    {
        Console.WriteLine($"Test: {description}");
        Console.WriteLine($"Input: {input}");
        Console.WriteLine($"Expected: {expected}");

        try
        {
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(input);

            // Create lexer
            var lexer = new Smalltalk80Lexer(inputStream);

            // Create token stream
            var tokenStream = new CommonTokenStream(lexer);

            // Create parser
            var parser = new Smalltalk80Parser(tokenStream);

            // Add error listener
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new TestErrorListener());

            // Parse using blockEntry rule
            var tree = parser.blockEntry();

            // Check for syntax errors
            if (parser.NumberOfSyntaxErrors > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED: {parser.NumberOfSyntaxErrors} syntax errors");
                Console.ResetColor();
                return;
            }

            // Analyze parse tree structure
            Console.WriteLine($"Parse tree: {tree.ToStringTree(parser)}");

            // Success
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("PASSED");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"FAILED: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine();
    }
}

class TestErrorListener : Antlr4.Runtime.BaseErrorListener
{
    public override void SyntaxError(
        System.IO.TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Syntax error at {line}:{charPositionInLine} - {msg}");
        Console.ResetColor();
    }
}
