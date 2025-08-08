using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Antlr4.Runtime;
using Smalltalk80.Generated;

namespace Smalltalk80.Compiler;

/// <summary>
/// Smalltalk-80 Compiler - Parses and validates Smalltalk source files in file-out format.
/// </summary>
public class Program
{
    private static int _errorCount = 0;
    private static int _warningCount = 0;

    public static int Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Compiler");
        Console.WriteLine("======================");
        Console.WriteLine();

        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        string? inputFile = null;
        bool verbose = false;
        bool showTree = false;
        bool validateOnly = false;
        bool compileClasses = false;
        string? outputFile = null;
        List<string> classesToCompile = new();

        // Parse command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
                case "-t":
                case "--tree":
                    showTree = true;
                    break;
                case "--validate":
                    validateOnly = true;
                    break;
                case "--compile-classes":
                    compileClasses = true;
                    break;
                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        outputFile = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --output requires a file path");
                        return 1;
                    }
                    break;
                case "-c":
                case "--class":
                    if (i + 1 < args.Length)
                    {
                        classesToCompile.Add(args[++i]);
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --class requires a class name");
                        return 1;
                    }
                    break;
                case "-h":
                case "--help":
                    ShowUsage();
                    return 0;
                default:
                    if (inputFile == null && !args[i].StartsWith("-"))
                    {
                        inputFile = args[i];
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown option or multiple input files: {args[i]}");
                        return 1;
                    }
                    break;
            }
        }

        if (inputFile == null)
        {
            Console.Error.WriteLine("Error: No input file specified.");
            ShowUsage();
            return 1;
        }

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile}' does not exist.");
            return 1;
        }

        // Handle compile-classes mode
        if (compileClasses)
        {
            if (classesToCompile.Count == 0)
            {
                Console.Error.WriteLine("Error: --compile-classes requires at least one --class specification");
                return 1;
            }
            if (outputFile == null)
            {
                outputFile = "bootstrap_phase2_extracted.json";
            }
            // Use the simpler class extractor for now
            return ClassExtractorCLI.ExtractClasses(inputFile, outputFile, classesToCompile.ToArray());
        }

        try
        {
            Console.WriteLine($"Parsing: {inputFile}");
            var stopwatch = Stopwatch.StartNew();

            // Read the input file
            string input = File.ReadAllText(inputFile);
            
            // Create ANTLR input stream
            var inputStream = new AntlrInputStream(input);
            
            // Create lexer
            var lexer = new Smalltalk80Lexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            
            // Create parser
            var parser = new Smalltalk80Parser(tokens);
            
            // Add error listener
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new CompilerErrorListener(verbose));
            
            // Parse the file
            var tree = parser.fileIn();
            
            stopwatch.Stop();

            // Report results
            Console.WriteLine();
            Console.WriteLine($"Parsing completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Lines of code: {parser.CurrentToken.Line}");
            Console.WriteLine($"  Errors: {_errorCount}");
            Console.WriteLine($"  Warnings: {_warningCount}");

            if (showTree)
            {
                Console.WriteLine();
                Console.WriteLine("Parse Tree:");
                Console.WriteLine("-----------");
                Console.WriteLine(tree.ToStringTree(parser));
            }

            if (_errorCount > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Compilation failed with {_errorCount} error(s).");
                return 1;
            }

            if (validateOnly)
            {
                Console.WriteLine();
                Console.WriteLine("✓ Validation successful - file is syntactically correct.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("✓ Parsing successful.");
                // TODO: Add code generation or further processing here
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: Smalltalk80.Compiler [options] <input-file>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --verbose         Show detailed parsing information");
        Console.WriteLine("  -t, --tree            Display the parse tree");
        Console.WriteLine("  --validate            Only validate syntax, don't generate output");
        Console.WriteLine("  --compile-classes     Compile classes from system image to bootstrap JSON");
        Console.WriteLine("  -c, --class <name>    Specify a class to compile (use multiple times for multiple classes)");
        Console.WriteLine("  -o, --output <file>   Output file for compiled JSON (default: bootstrap_phase3_compiled.json)");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Validate the system image");
        Console.WriteLine("  Smalltalk80.Compiler --validate \"Smalltalk-80.st\"");
        Console.WriteLine();
        Console.WriteLine("  # Compile Object and Collection classes to JSON");
        Console.WriteLine("  Smalltalk80.Compiler --compile-classes -c Object -c Collection -o phase3.json \"Smalltalk-80.st\"");
        Console.WriteLine();
        Console.WriteLine("  # Parse and show tree structure");
        Console.WriteLine("  Smalltalk80.Compiler -v -t test.st");
    }

    /// <summary>
    /// Custom error listener for the compiler.
    /// </summary>
    private class CompilerErrorListener : BaseErrorListener
    {
        private readonly bool _verbose;

        public CompilerErrorListener(bool verbose)
        {
            _verbose = verbose;
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
            _errorCount++;
            
            Console.Error.WriteLine($"Error at line {line}:{charPositionInLine}: {msg}");
            
            if (_verbose && offendingSymbol != null)
            {
                var text = offendingSymbol.Text;
                if (text.Length > 20)
                {
                    text = text.Substring(0, 20) + "...";
                }
                Console.Error.WriteLine($"  Near: '{text}'");
            }
        }
    }
}