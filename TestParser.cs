using System;
using System.IO;
using Antlr4.Runtime;
using Smalltalk80;

class TestParser
{
    static void Main(string[] args)
    {
        var filePath = @"Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st";
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found: {filePath}");
            return;
        }
        
        Console.WriteLine($"Parsing Smalltalk-80 system image: {filePath}");
        Console.WriteLine($"File size: {new FileInfo(filePath).Length:N0} bytes");
        Console.WriteLine();
        
        try
        {
            var input = File.ReadAllText(filePath);
            var inputStream = new AntlrInputStream(input);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Add error listener to capture any parsing errors
            var errorListener = new ConsoleErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            
            // Parse the entire file
            var startTime = DateTime.Now;
            var tree = parser.program();
            var parseTime = DateTime.Now - startTime;
            
            // Check for errors
            var errorCount = parser.NumberOfSyntaxErrors;
            
            Console.WriteLine($"Parsing completed in {parseTime.TotalSeconds:F2} seconds");
            Console.WriteLine($"Syntax errors: {errorCount}");
            
            if (errorCount == 0)
            {
                Console.WriteLine("✓ SUCCESS: Smalltalk-80.st parsed without errors!");
                
                // Count some statistics
                var visitor = new SimpleAstBuildingVisitor();
                var program = visitor.Visit(tree) as Smalltalk80.AST.Program;
                
                if (program != null)
                {
                    int methodCount = 0;
                    int classCount = 0;
                    
                    foreach (var elem in program.Elements)
                    {
                        if (elem is Smalltalk80.AST.MethodDefinition)
                            methodCount++;
                        else if (elem is Smalltalk80.AST.ClassDefinition)
                            classCount++;
                    }
                    
                    Console.WriteLine($"  Classes defined: {classCount}");
                    Console.WriteLine($"  Methods defined: {methodCount}");
                }
            }
            else
            {
                Console.WriteLine("✗ FAILED: Parsing errors encountered");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ ERROR: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner: {ex.InnerException.Message}");
            }
        }
    }
    
    class ConsoleErrorListener : BaseErrorListener
    {
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, 
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Console.WriteLine($"  Line {line}:{charPositionInLine} - {msg}");
        }
    }
}