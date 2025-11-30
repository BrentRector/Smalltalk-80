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

        // Parse arguments - support multiple input files
        List<string> inputFiles = new List<string>();
        string? outputFile = null;

        if (args.Length == 0)
        {
            // No args: use default input file
            inputFiles.Add(defaultInputFile);
        }
        else
        {
            // Parse args: all .st files are inputs, first .json file is output
            foreach (var arg in args)
            {
                if (arg.EndsWith(".st", StringComparison.OrdinalIgnoreCase))
                {
                    inputFiles.Add(arg);
                }
                else if (arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && outputFile == null)
                {
                    outputFile = arg;
                }
            }

            // If no input files specified, use default
            if (inputFiles.Count == 0)
            {
                inputFiles.Add(defaultInputFile);
            }
        }

        // Default output: coldstart.json in the Data subfolder of the executable's output directory
        outputFile ??= Path.Combine(AppContext.BaseDirectory, "Data", "coldstart.json");

        // Verify all input files exist
        foreach (var inputFile in inputFiles)
        {
            if (!File.Exists(inputFile))
            {
                Console.Error.WriteLine($"Error: Input file not found: {inputFile}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Usage: Smalltalk80.Compiler [input1.st] [input2.st] ... [output.json]");
                Console.Error.WriteLine("  Compiles Smalltalk file-out format to JSON cold-start image.");
                Console.Error.WriteLine("  Supports multiple input .st files (merged into single bundle).");
                Console.Error.WriteLine($"  Default input:  {defaultInputFile}");
                Console.Error.WriteLine($"  Default output: Data/coldstart.json in executable directory");
                return 1;
            }
        }

        try
        {
            Console.WriteLine("Smalltalk-80 Compiler");
            Console.WriteLine("====================");
            Console.WriteLine($"Input files ({inputFiles.Count}):");
            foreach (var file in inputFiles)
            {
                Console.WriteLine($"  - {file}");
            }
            Console.WriteLine($"Output: {outputFile}");
            Console.WriteLine();

            // Clear undeclared variables registry before compilation
            BytecodeCompiler.ClearUndeclaredVariables();

            // Parse all input files and merge the ASTs
            Console.WriteLine("Parsing Smalltalk source files...");
            CompilationResult mergedResult = null!;

            for (int i = 0; i < inputFiles.Count; i++)
            {
                var inputFile = inputFiles[i];
                Console.WriteLine($"  Parsing {Path.GetFileName(inputFile)}...");
                var result = ParseSmalltalkSource(inputFile);

                if (i == 0)
                {
                    // First file: use as base
                    mergedResult = result;
                }
                else
                {
                    // Subsequent files: merge classes and init expressions
                    foreach (var classNode in result.Classes)
                    {
                        mergedResult.Classes.Add(classNode);
                    }
                    foreach (var initExpr in result.InitializationExpressions)
                    {
                        mergedResult.InitializationExpressions.Add(initExpr);
                    }
                }
            }

            Console.WriteLine($"  Total classes: {mergedResult.Classes.Count}");
            Console.WriteLine($"  Total init expressions: {mergedResult.InitializationExpressions.Count}");
            Console.WriteLine();

            Console.WriteLine("Generating cold start JSON...");
            var generator = new ColdStartJsonGenerator(string.Join(", ", inputFiles.Select(Path.GetFileName)));
            string json = generator.Generate(mergedResult);

            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputFile, json);

            // Write the .sources file alongside the JSON
            string sourcesFileContent = generator.GetSourcesFileContent();
            if (!string.IsNullOrEmpty(sourcesFileContent))
            {
                string sourcesFile = Path.Combine(outputDir ?? ".", "Smalltalk-80.sources");
                File.WriteAllText(sourcesFile, sourcesFileContent, System.Text.Encoding.UTF8);
                Console.WriteLine($"Generated sources file: {sourcesFile}");
                Console.WriteLine($"  Size: {sourcesFileContent.Length:N0} bytes");
            }

            // Create empty .changes file (index 2) for runtime modifications
            string changesFile = Path.Combine(outputDir ?? ".", "Smalltalk-80.changes");
            File.WriteAllText(changesFile, "", System.Text.Encoding.UTF8);
            Console.WriteLine($"Generated changes file: {changesFile} (empty)");

            Console.WriteLine();
            Console.WriteLine($"Successfully generated: {outputFile}");
            var stats = generator.GetStatistics();
            Console.WriteLine($"  Symbols: {stats.SymbolCount}");
            Console.WriteLine($"  Strings: {stats.StringCount}");
            Console.WriteLine($"  Classes: {stats.ClassCount}");
            Console.WriteLine($"  Methods: {stats.MethodCount}");
            Console.WriteLine($"  Init expressions: {stats.InitExpressionCount}");

            // Report undeclared variables (suppressed - this is expected during bootstrap)
            // 353 undeclared variables are known/documented in CLAUDE.md
            // These are references to GUI primitives, system objects not available at bootstrap
            var undeclared = BytecodeCompiler.GetUndeclaredVariables();
            if (undeclared.Count > 0 && args.Any(a => a == "--verbose" || a == "--show-undeclared"))
            {
                Console.WriteLine();
                Console.WriteLine($"Info: {undeclared.Count} undeclared variable(s) found during compilation:");
                foreach (var kvp in undeclared.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  - {kvp.Key} (referenced in {kvp.Value.Count} method(s))");
                }
            }

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
        // Read the input file (preserve original for error reporting)
        string input = File.ReadAllText(inputFile);

        // Create ANTLR input stream
        var inputStream = new AntlrInputStream(input);

        // Create lexer
        var lexer = new Smalltalk80Lexer(inputStream);

        // Create token stream
        var tokenStream = new CommonTokenStream(lexer);

        // Create parser
        var parser = new Smalltalk80Parser(tokenStream);

        // Add enhanced error listener with source context
        var errorListener = new ConsoleErrorListener(inputFile, input);
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
/// Custom error listener for ANTLR parser errors with enhanced source context display.
/// Shows the actual source line(s) with proper whitespace and a caret pointing to the error.
/// </summary>
file sealed class ConsoleErrorListener : BaseErrorListener
{
    private readonly string _fileName;
    private readonly string[] _sourceLines;
    private const int ContextLinesBefore = 1;
    private const int ContextLinesAfter = 1;

    /// <summary>
    /// Creates a new error listener with access to the original source.
    /// </summary>
    /// <param name="fileName">Name of the file being parsed (for display purposes).</param>
    /// <param name="sourceText">Original source text with all whitespace intact.</param>
    public ConsoleErrorListener(string fileName, string sourceText)
    {
        _fileName = Path.GetFileName(fileName);
        _sourceLines = sourceText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    /// <summary>
    /// Handles syntax errors from the ANTLR parser.
    /// Displays error with source context, line numbers, and caret position.
    /// </summary>
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        // Write to stdout instead of stderr so errors are visible in MSBuild output
        Console.WriteLine();
        Console.WriteLine($"=== PARSE ERROR in {_fileName} ===");
        Console.WriteLine();

        // Show context lines
        int startLine = Math.Max(1, line - ContextLinesBefore);
        int endLine = Math.Min(_sourceLines.Length, line + ContextLinesAfter);

        // Calculate line number width for alignment
        int lineNumWidth = endLine.ToString().Length;

        for (int i = startLine; i <= endLine; i++)
        {
            // Get the source line (adjust for 0-based indexing)
            string sourceLine = i <= _sourceLines.Length ? _sourceLines[i - 1] : "";

            // Format line number with padding
            string lineNumPrefix = i.ToString().PadLeft(lineNumWidth);

            // Mark the error line with an arrow
            string marker = (i == line) ? " -> " : "    ";

            Console.WriteLine($"{marker}{lineNumPrefix}: {sourceLine}");

            // If this is the error line, show the caret
            if (i == line)
            {
                // Calculate caret position (account for line number prefix)
                int caretOffset = marker.Length + lineNumWidth + 2 + charPositionInLine; // +2 for ": "
                string caretLine = new string(' ', caretOffset) + "^";

                Console.WriteLine(caretLine);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Error: {msg}");

        // Show the offending token with original text from source
        if (offendingSymbol != null)
        {
            // Get the actual text from the source using token's start/stop indices
            string tokenText = GetTokenSourceText(offendingSymbol);
            Console.WriteLine($"Token: '{tokenText}' at position {charPositionInLine}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Extracts the original source text for a token, preserving all whitespace.
    /// Uses the token's character stream interval to get the exact original text.
    /// </summary>
    private string GetTokenSourceText(IToken token)
    {
        if (token.TokenSource == null)
            return token.Text;

        ICharStream inputStream = token.TokenSource.InputStream;
        if (inputStream == null)
            return token.Text;

        // Use ANTLR's Interval to get exact source text
        int start = token.StartIndex;
        int stop = token.StopIndex;

        if (start < 0 || stop < 0 || stop >= inputStream.Size)
            return token.Text;

        // Get text directly from the character stream (preserves original formatting)
        return inputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop));
    }
}