using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antlr4.Runtime;
using Smalltalk80.Compiler.AST;
using Smalltalk80.Compiler.CodeGen;
using Smalltalk80.Compiler.Generated;

namespace Smalltalk80.Compiler;

/// <summary>
/// Main entry point for the Smalltalk-80 compiler command-line tool.
/// Compiles Smalltalk file-out format to JSON cold-start image.
/// </summary>
file sealed class Program
{
    /// <summary>
    /// Main entry point for the compiler.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    static int Main(string[] args)
    {
        // Default input: Smalltalk-80 Source/Smalltalk-80.st relative to solution directory
        // AppContext.BaseDirectory is typically: Smalltalk-80/bin/Debug/net9.0/
        // So we need to go up to the solution root
        string solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        string defaultInputFile = Path.Combine(solutionRoot, "Smalltalk-80 Source", "Smalltalk-80.st");

        // Parse arguments
        string inputFile = args.Length > 0 ? args[0] : defaultInputFile;

        // Default output: coldstart.json in the executable's output directory
        string outputFile = args.Length > 1
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "coldstart.json");

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputFile}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler [input.st] [output.json]");
            Console.Error.WriteLine("  Compiles Smalltalk file-out format to JSON cold-start image.");
            Console.Error.WriteLine($"  Default input:  {defaultInputFile}");
            Console.Error.WriteLine($"  Default output: coldstart.json in executable directory");
            return 1;
        }

        try
        {
            Console.WriteLine("Smalltalk-80 Compiler");
            Console.WriteLine("====================");
            Console.WriteLine($"Input:  {inputFile}");
            Console.WriteLine($"Output: {outputFile}");
            Console.WriteLine();

            Console.WriteLine("Parsing Smalltalk source...");
            var result = ParseSmalltalkSource(inputFile);

            Console.WriteLine("Generating cold start JSON...");
            var generator = new ColdStartJsonGenerator(inputFile);
            string json = generator.Generate(result);

            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputFile, json);

            Console.WriteLine();
            Console.WriteLine($"Successfully generated: {outputFile}");
            var stats = generator.GetStatistics();
            Console.WriteLine($"  Symbols: {stats.SymbolCount}");
            Console.WriteLine($"  Strings: {stats.StringCount}");
            Console.WriteLine($"  Classes: {stats.ClassCount}");
            Console.WriteLine($"  Methods: {stats.MethodCount}");
            Console.WriteLine($"  Init expressions: {stats.InitExpressionCount}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (args.Length > 1 && args[1] == "--verbose")
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }


    /// <summary>
    /// Parses a Smalltalk source file and returns the compilation result.
    /// </summary>
    /// <param name="inputFile">Path to the Smalltalk source file.</param>
    /// <returns>The parsed compilation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when parsing fails.</exception>
    private static CompilationResult ParseSmalltalkSource(string inputFile)
    {
        // Read the input file
        string input = File.ReadAllText(inputFile);

        // Create ANTLR input stream
        var inputStream = new AntlrInputStream(input);

        // Create lexer
        var lexer = new Smalltalk80Lexer(inputStream);

        // Create token stream
        var tokenStream = new CommonTokenStream(lexer);

        // Create parser
        var parser = new Smalltalk80Parser(tokenStream);

        // Add error listener
        var errorListener = new ConsoleErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        // Parse the file
        var tree = parser.fileIn();

        // Check for syntax errors
        if (parser.NumberOfSyntaxErrors > 0)
        {
            throw new InvalidOperationException($"Parsing failed with {parser.NumberOfSyntaxErrors} syntax errors.");
        }

        // Build the AST
        var astBuilder = new FileInFormatAstBuilder(tokenStream);
        astBuilder.Visit(tree);

        // Get and return the compilation result
        return astBuilder.GetResult();
    }
}

/// <summary>
/// Custom error listener for ANTLR parser errors.
/// </summary>
file sealed class ConsoleErrorListener : BaseErrorListener
{
    /// <summary>
    /// Handles syntax errors from the ANTLR parser.
    /// </summary>
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Console.Error.WriteLine($"Line {line}:{charPositionInLine} - {msg}");
    }
}