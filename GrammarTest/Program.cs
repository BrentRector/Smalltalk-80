using System;
using System.Diagnostics;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace GrammarTest;

/// <summary>
/// Test program to validate the new Smalltalk80.g4 grammar
/// by parsing the complete Smalltalk-80.st file.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Smalltalk-80 Grammar Test Program");
        Console.WriteLine("===========================================\n");

        // Path to the Smalltalk-80.st file
        string sourceFile = args.Length > 0 ? args[0] : "../Smalltalk-80 Source/Smalltalk-80.st";
        
        if (!File.Exists(sourceFile))
        {
            Console.Error.WriteLine($"ERROR: Source file not found: {sourceFile}");
            return 1;
        }

        Console.WriteLine($"Source file: {sourceFile}");
        var fileInfo = new FileInfo(sourceFile);
        Console.WriteLine($"File size: {fileInfo.Length:N0} bytes");
        
        // Read the source code
        Console.Write("Reading source file... ");
        var sw = Stopwatch.StartNew();
        string sourceCode = File.ReadAllText(sourceFile);
        sw.Stop();
        Console.WriteLine($"Done ({sw.ElapsedMilliseconds}ms)");
        
        // Count some basic statistics
        int lineCount = sourceCode.Split('\n').Length;
        int bangCount = sourceCode.Count(c => c == '!');
        Console.WriteLine($"Lines: {lineCount:N0}");
        Console.WriteLine($"Bang delimiters: {bangCount:N0}");
        Console.WriteLine();

        // Test each entry point
        Console.WriteLine("Testing Grammar Entry Points:");
        Console.WriteLine("---------------------------------------------");
        
        bool allTestsPassed = true;

        // 1. Test main fileIn entry point (most important)
        allTestsPassed &= TestEntryPoint("fileIn (complete file)", sourceCode, 
            parser => parser.fileIn());

        // 2. Test expression entry with simple expression
        allTestsPassed &= TestEntryPoint("expressionEntry", "3 + 4", 
            parser => parser.expressionEntry());

        // 3. Test statements entry
        allTestsPassed &= TestEntryPoint("statementsEntry", "| x | x := 10. x + 5", 
            parser => parser.statementsEntry());

        // 4. Test method entry
        allTestsPassed &= TestEntryPoint("methodEntry", "factorial\n\t^self = 0 ifTrue: [1] ifFalse: [self * (self - 1) factorial]", 
            parser => parser.methodEntry());

        // 5. Test class entry
        allTestsPassed &= TestEntryPoint("classEntry", 
            "Object subclass: #TestClass instanceVariableNames: 'x y' classVariableNames: '' poolDictionaries: '' category: 'Test'", 
            parser => parser.classEntry());

        // 6. Test block entry
        allTestsPassed &= TestEntryPoint("blockEntry", "[:x :y | x + y]", 
            parser => parser.blockEntry());

        // 7. Test selector entry
        allTestsPassed &= TestEntryPoint("selectorEntry", "at:put:", 
            parser => parser.selectorEntry());

        // 8. Test cascade entry
        allTestsPassed &= TestEntryPoint("cascadeEntry", "Transcript cr; show: 'Hello'; cr", 
            parser => parser.cascadeEntry());

        // 9. Test literal entry
        allTestsPassed &= TestEntryPoint("literalEntry", "#(1 2 3)", 
            parser => parser.literalEntry());

        // 10. Test temporaries entry
        allTestsPassed &= TestEntryPoint("temporariesEntry", "| a b | a := 5. b := 10. a * b", 
            parser => parser.temporariesEntry());

        Console.WriteLine("\n=============================================");
        if (allTestsPassed)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ ALL TESTS PASSED!");
            Console.ResetColor();
            Console.WriteLine("The grammar successfully parses the complete");
            Console.WriteLine("Smalltalk-80.st file and all entry points work.");
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ SOME TESTS FAILED");
            Console.ResetColor();
            Console.WriteLine("The grammar needs adjustments.");
            return 1;
        }
    }

    static bool TestEntryPoint(string testName, string input, Func<Smalltalk80Parser, IParseTree> parseAction)
    {
        Console.Write($"  Testing {testName}... ");
        
        try
        {
            var sw = Stopwatch.StartNew();
            
            // Create lexer and parser
            var inputStream = new AntlrInputStream(input);
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new Smalltalk80Parser(tokenStream);
            
            // Set up error handling
            var errorListener = new ErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            
            // Parse with the specified entry point
            var tree = parseAction(parser);
            sw.Stop();
            
            // Check for errors
            if (errorListener.HasErrors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED ({sw.ElapsedMilliseconds}ms)");
                Console.ResetColor();
                Console.WriteLine($"    Syntax errors: {errorListener.ErrorCount}");
                
                // Show first few errors
                int errorsToShow = Math.Min(3, errorListener.Errors.Count);
                for (int i = 0; i < errorsToShow; i++)
                {
                    Console.WriteLine($"    - {errorListener.Errors[i]}");
                }
                if (errorListener.Errors.Count > errorsToShow)
                {
                    Console.WriteLine($"    ... and {errorListener.Errors.Count - errorsToShow} more errors");
                }
                
                return false;
            }
            
            // Success!
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"PASSED");
            Console.ResetColor();
            
            // For the main file, show more statistics
            if (testName.Contains("complete file"))
            {
                var visitor = new StatisticsVisitor();
                visitor.Visit(tree);
                Console.WriteLine($" ({sw.ElapsedMilliseconds}ms)");
                Console.WriteLine($"    Chunks: {visitor.ChunkCount:N0}");
                Console.WriteLine($"    Classes: {visitor.ClassCount:N0}");
                Console.WriteLine($"    Methods: {visitor.MethodCount:N0}");
                Console.WriteLine($"    Parse tree nodes: {visitor.NodeCount:N0}");
            }
            else
            {
                Console.WriteLine($" ({sw.ElapsedMilliseconds}ms)");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"EXCEPTION");
            Console.ResetColor();
            Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Custom error listener to capture parsing errors.
/// </summary>
class ErrorListener : BaseErrorListener
{
    public List<string> Errors { get; } = new List<string>();
    public bool HasErrors => Errors.Count > 0;
    public int ErrorCount => Errors.Count;
    
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

/// <summary>
/// Visitor to collect statistics about the parsed tree.
/// </summary>
class StatisticsVisitor : Smalltalk80BaseVisitor<object?>
{
    public int NodeCount { get; private set; }
    public int ChunkCount { get; private set; }
    public int ClassCount { get; private set; }
    public int MethodCount { get; private set; }
    
    public override object? Visit(IParseTree tree)
    {
        NodeCount++;
        return base.Visit(tree);
    }
    
    public override object? VisitChunk(Smalltalk80Parser.ChunkContext context)
    {
        ChunkCount++;
        return base.VisitChunk(context);
    }
    
    public override object? VisitClassDefChunk(Smalltalk80Parser.ClassDefChunkContext context)
    {
        ClassCount++;
        return base.VisitClassDefChunk(context);
    }
    
    public override object? VisitMethodDef(Smalltalk80Parser.MethodDefContext context)
    {
        MethodCount++;
        return base.VisitMethodDef(context);
    }
    
    protected override object? DefaultResult => null;
    
    protected override object? AggregateResult(object? aggregate, object? nextResult)
    {
        return nextResult ?? aggregate;
    }
}