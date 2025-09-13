using System;
using System.IO;
using System.Diagnostics;
using Antlr4.Runtime;

class TestParser
{
    static void Main(string[] args)
    {
        var filePath = @"..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
        
        if (!File.Exists(filePath))
        {
            // Try alternative path
            filePath = @"..\..\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: Smalltalk-80.st not found");
                Console.WriteLine($"Tried: {Path.GetFullPath(filePath)}");
                return;
            }
        }
        
        Console.WriteLine($"========================================");
        Console.WriteLine($"Smalltalk-80 Parser Verification Test");
        Console.WriteLine($"========================================\n");
        Console.WriteLine($"File: {Path.GetFileName(filePath)}");
        Console.WriteLine($"Path: {Path.GetFullPath(filePath)}");
        Console.WriteLine($"Size: {new FileInfo(filePath).Length:N0} bytes");
        Console.WriteLine();
        
        try
        {
            var input = File.ReadAllText(filePath);
            Console.WriteLine($"File loaded: {input.Length:N0} characters");
            
            // Create lexer
            var inputStream = new AntlrInputStream(input);
            var lexer = new Smalltalk80Lexer(inputStream);
            
            // Count tokens for verification
            Console.WriteLine("Lexing...");
            var tokenCount = 0;
            IToken token;
            while ((token = lexer.NextToken()).Type != TokenConstants.EOF)
            {
                tokenCount++;
                if (tokenCount % 10000 == 0)
                    Console.Write($"\r  Tokens: {tokenCount:N0}");
            }
            Console.WriteLine($"\r  Tokens: {tokenCount:N0}");
            
            // Reset lexer and create parser
            lexer.Reset();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Add error listener
            var errorListener = new ConsoleErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            
            // Parse the file
            Console.WriteLine("\nParsing...");
            var sw = Stopwatch.StartNew();
            var tree = parser.program();
            sw.Stop();
            
            // Results
            var errorCount = parser.NumberOfSyntaxErrors;
            
            Console.WriteLine($"\n========================================");
            Console.WriteLine($"Parse Results");
            Console.WriteLine($"========================================");
            Console.WriteLine($"Time:   {sw.Elapsed.TotalSeconds:F2} seconds");
            Console.WriteLine($"Errors: {errorCount}");
            
            if (errorCount == 0)
            {
                Console.WriteLine($"\n✓ SUCCESS: The Smalltalk-80 system image");
                Console.WriteLine($"  parsed without any syntax errors!");
                Console.WriteLine($"\nThe compiler can successfully parse");
                Console.WriteLine($"the complete Smalltalk-80.st file.");
            }
            else
            {
                Console.WriteLine($"\n✗ FAILED: {errorCount} syntax errors found");
                Console.WriteLine($"\nThe parser encountered errors while");
                Console.WriteLine($"processing the Smalltalk-80.st file.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ EXCEPTION: {ex.GetType().Name}");
            Console.WriteLine($"  {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
        }
    }
    
    class ConsoleErrorListener : BaseErrorListener
    {
        private int errorCount = 0;
        private const int MaxErrors = 5;
        
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, 
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            errorCount++;
            if (errorCount <= MaxErrors)
            {
                Console.WriteLine($"\n  ERROR at line {line}:{charPositionInLine}");
                Console.WriteLine($"    {msg}");
                if (offendingSymbol != null && offendingSymbol.Text != null && offendingSymbol.Text.Length < 50)
                {
                    Console.WriteLine($"    Near: '{offendingSymbol.Text}'");
                }
            }
            else if (errorCount == MaxErrors + 1)
            {
                Console.WriteLine($"\n  (Additional errors suppressed...)");
            }
        }
    }
}