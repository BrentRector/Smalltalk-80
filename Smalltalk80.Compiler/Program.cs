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
        if (args.Length == 0)
        {
            ShowHelp();
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "compile" => ExecuteCompileCommand(args[1..]),
            "generate-coldstart" => ExecuteGenerateColdStartCommand(args[1..]),
            "--help" or "-h" or "/?" => ShowHelp(),
            _ => HandleUnknownCommand(args[0])
        };
    }

    /// <summary>
    /// Displays help information for all available commands.
    /// </summary>
    /// <returns>Always returns 0.</returns>
    private static int ShowHelp()
    {
        Console.WriteLine("Smalltalk-80 Compiler");
        Console.WriteLine("=====================");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  Smalltalk80.Compiler <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compile <input.st> <output.json> [--verbose]");
        Console.WriteLine("    Compiles a Smalltalk-80 file-out format source file to JSON.");
        Console.WriteLine("    Options:");
        Console.WriteLine("      --verbose    Show detailed error information");
        Console.WriteLine();
        Console.WriteLine("  generate-coldstart <input.st> <output-dir> --classes Class1,Class2,...");
        Console.WriteLine("    Generates complete cold start artifacts for specified classes (and their superclasses):");
        Console.WriteLine("      - Memory layouts (Layouts.cs)");
        Console.WriteLine("      - Initialization data with exact sizes (ColdStartData.cs)");
        Console.WriteLine("      - Pre-intern symbol list (ColdStartLiterals.cs)");
        Console.WriteLine("    Options:");
        Console.WriteLine("      --classes    REQUIRED: Comma-separated list of cold start classes,");
        Console.WriteLine("                   or 'ALL' to include all classes from the source file");
        Console.WriteLine();
        Console.WriteLine("  --help, -h, /?");
        Console.WriteLine("    Shows this help message.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Smalltalk80.Compiler compile SystemClasses.st output.json");
        Console.WriteLine("  Smalltalk80.Compiler generate-coldstart Smalltalk-80.st ./Generated \\");
        Console.WriteLine("      --classes Object,Behavior,Class,Metaclass,CompiledMethod,Array,Dictionary");

        return 0;
    }

    /// <summary>
    /// Handles unknown commands by displaying an error message.
    /// </summary>
    /// <param name="command">The unknown command.</param>
    /// <returns>Always returns 1.</returns>
    private static int HandleUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Error: Unknown command '{command}'");
        Console.Error.WriteLine("Run 'Smalltalk80.Compiler --help' for usage information.");
        return 1;
    }

    /// <summary>
    /// Executes the compile command to generate JSON from Smalltalk source.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static int ExecuteCompileCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: compile command requires <input.st> and <output.json> arguments");
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler compile <input.st> <output.json> [--verbose]");
            return 1;
        }

        string inputFile = args[0];
        string outputFile = args[1];
        bool verbose = args.Length > 2 && args[2] == "--verbose";

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
            return 1;
        }

        try
        {
            Console.WriteLine($"Compiling {inputFile}...");

            // Parse the Smalltalk source
            var result = ParseSmalltalkSource(inputFile);

            // Serialize to JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(result, jsonOptions);

            // Write output file
            File.WriteAllText(outputFile, json);

            // Display statistics
            Console.WriteLine($"Successfully compiled to {outputFile}");
            Console.WriteLine($"  Classes: {result.Classes.Count}");

            int totalMethods = result.Classes.Sum(c => c.Methods.Count);
            int totalClassMethods = result.Classes.Sum(c => c.ClassMethods.Count);

            Console.WriteLine($"  Instance Methods: {totalMethods}");
            Console.WriteLine($"  Class Methods: {totalClassMethods}");
            Console.WriteLine($"  Initialization Expressions: {result.InitializationExpressions.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    /// <summary>
    /// Executes the unified generate-coldstart command.
    /// Generates layouts, initialization data, and literals for specified cold start classes.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static int ExecuteGenerateColdStartCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: generate-coldstart command requires <input.st> and <output-dir> arguments");
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler generate-coldstart <input.st> <output-dir> --classes Class1,Class2,...");
            return 1;
        }

        string inputFile = args[0];
        string outputDir = args[1];
        HashSet<string>? classFilter = null;

        // Parse required --classes argument
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--classes", StringComparison.OrdinalIgnoreCase))
            {
                string classesArg;
                if (args[i].Contains('='))
                {
                    // Format: --classes=Class1,Class2
                    classesArg = args[i][(args[i].IndexOf('=') + 1)..];
                }
                else if (i + 1 < args.Length)
                {
                    // Format: --classes Class1,Class2
                    classesArg = args[++i];
                }
                else
                {
                    Console.Error.WriteLine("Error: --classes option requires a value");
                    return 1;
                }

                classFilter = [.. classesArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            }
        }

        if (classFilter == null || classFilter.Count == 0)
        {
            Console.Error.WriteLine("Error: --classes option is required");
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler generate-coldstart <input.st> <output-dir> --classes Class1,Class2,...");
            Console.Error.WriteLine("       Use --classes ALL to generate cold start for all classes in the source file");
            return 1;
        }

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
            return 1;
        }

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(outputDir);

        try
        {
            Console.WriteLine($"Generating cold start artifacts from {inputFile}...");
            Console.WriteLine($"  Output directory: {outputDir}");
            Console.WriteLine($"  Requested classes: {string.Join(", ", classFilter)}");
            Console.WriteLine();

            // Parse the Smalltalk source
            var result = ParseSmalltalkSource(inputFile);

            // Check if "ALL" was specified
            if (classFilter.Count == 1 && classFilter.First().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Generating cold start for ALL classes in source file");
                classFilter = result.Classes.Select(c => c.Name).ToHashSet();
            }

            // Expand the filter to include all superclasses
            var expandedClasses = ExpandClassFilterWithSuperclasses(classFilter, result.Classes);

            if (expandedClasses.MissingClasses.Count > 0)
            {
                Console.Error.WriteLine($"Error: The following requested classes were not found in the input file:");
                Console.Error.WriteLine($"  Missing: {string.Join(", ", expandedClasses.MissingClasses)}");
                return 1;
            }

            var classesToGenerate = expandedClasses.Classes;
            Console.WriteLine($"Generating for {classesToGenerate.Count} classes (including superclasses):");
            Console.WriteLine($"  {string.Join(", ", classesToGenerate.Select(c => c.Name))}");
            Console.WriteLine();

            // Generate 1: Memory Layouts
            Console.WriteLine("[1/3] Generating memory layouts...");
            var layoutGenerator = new LayoutGenerator();
            string layoutsCode = layoutGenerator.Generate(classesToGenerate);
            string layoutsFile = Path.Combine(outputDir, "Layouts.cs");
            File.WriteAllText(layoutsFile, layoutsCode);
            Console.WriteLine($"      Written: {layoutsFile} ({new FileInfo(layoutsFile).Length:N0} bytes)");

            // Generate 2: Initialization Data
            Console.WriteLine("[2/3] Generating initialization data...");
            var dataGenerator = new ColdStartDataGenerator();

            // Create a filtered compilation result with only the cold start classes
            var filteredResult = new CompilationResult
            {
                Classes = classesToGenerate,
                InitializationExpressions = result.InitializationExpressions
            };

            dataGenerator.Analyze(filteredResult);
            var (classCount, methodCount, bytecodeSize, literalCount) = dataGenerator.GetStatistics();
            Console.WriteLine($"      Classes: {classCount}, Methods: {methodCount}");
            Console.WriteLine($"      Bytecode: {bytecodeSize:N0} bytes, Literals: {literalCount:N0}");

            string dataCode = dataGenerator.GenerateCSharpData();
            string dataFile = Path.Combine(outputDir, "ColdStartData.cs");
            File.WriteAllText(dataFile, dataCode);
            Console.WriteLine($"      Written: {dataFile} ({new FileInfo(dataFile).Length:N0} bytes)");

            // Generate 3: Symbol Literals
            Console.WriteLine("[3/3] Generating symbol literals...");
            var literalExtractor = new LiteralExtractor();
            literalExtractor.Extract(filteredResult);
            var (symbolCount, stringCount) = literalExtractor.GetStatistics();
            Console.WriteLine($"      Symbols: {symbolCount:N0}, Strings: {stringCount:N0}");

            string literalsCode = literalExtractor.GenerateCSharpCode();
            string literalsFile = Path.Combine(outputDir, "ColdStartLiterals.cs");
            File.WriteAllText(literalsFile, literalsCode);
            Console.WriteLine($"      Written: {literalsFile} ({new FileInfo(literalsFile).Length:N0} bytes)");

            Console.WriteLine();
            Console.WriteLine($"Successfully generated all cold start artifacts to {outputDir}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    /// <summary>
    /// Executes the generate-literals command to extract all symbols and string literals.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static int ExecuteGenerateLiteralsCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: generate-literals command requires <input.st> and <output.cs> arguments");
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler generate-literals <input.st> <output.cs>");
            return 1;
        }

        string inputFile = args[0];
        string outputFile = args[1];

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
            return 1;
        }

        try
        {
            Console.WriteLine($"Extracting literals from {inputFile}...");

            // Parse the Smalltalk source
            var result = ParseSmalltalkSource(inputFile);

            // Extract literals
            var extractor = new LiteralExtractor();
            extractor.Extract(result);

            var (symbolCount, stringCount) = extractor.GetStatistics();
            Console.WriteLine($"  Extracted {symbolCount} unique symbols");
            Console.WriteLine($"  Extracted {stringCount} unique string literals");

            // Generate C# code
            string generatedCode = extractor.GenerateCSharpCode();
            File.WriteAllText(outputFile, generatedCode);

            Console.WriteLine($"Successfully generated literals file to {outputFile}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Executes the generate-coldstart-data command to create complete initialization data.
    /// </summary>
    /// <param name="args">Command arguments.</param>
    /// <returns>Exit code: 0 for success, non-zero for failure.</returns>
    private static int ExecuteGenerateColdStartDataCommand(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: generate-coldstart-data command requires <input.st> and <output-prefix> arguments");
            Console.Error.WriteLine("Usage: Smalltalk80.Compiler generate-coldstart-data <input.st> <output-prefix> [--format json|csharp]");
            return 1;
        }

        string inputFile = args[0];
        string outputPrefix = args[1];
        string format = "csharp"; // Default format

        // Parse optional --format argument
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].StartsWith("--format", StringComparison.OrdinalIgnoreCase))
            {
                if (args[i].Contains('='))
                {
                    format = args[i][(args[i].IndexOf('=') + 1)..].ToLowerInvariant();
                }
                else if (i + 1 < args.Length)
                {
                    format = args[++i].ToLowerInvariant();
                }
            }
        }

        if (format != "json" && format != "csharp")
        {
            Console.Error.WriteLine($"Error: Invalid format '{format}'. Must be 'json' or 'csharp'.");
            return 1;
        }

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile}' not found.");
            return 1;
        }

        try
        {
            Console.WriteLine($"Analyzing cold start data from {inputFile}...");

            // Parse the Smalltalk source
            var result = ParseSmalltalkSource(inputFile);

            // Analyze and generate cold start data
            var generator = new ColdStartDataGenerator();
            generator.Analyze(result);

            var (classCount, methodCount, bytecodeSize, literalCount) = generator.GetStatistics();
            Console.WriteLine($"  Classes: {classCount}");
            Console.WriteLine($"  Methods: {methodCount}");
            Console.WriteLine($"  Total bytecode size: {bytecodeSize} bytes");
            Console.WriteLine($"  Total literals: {literalCount}");
            Console.WriteLine();

            // Generate output based on format
            string outputFile;
            string generatedData;

            if (format == "json")
            {
                outputFile = outputPrefix.EndsWith(".json") ? outputPrefix : outputPrefix + ".json";
                generatedData = generator.GenerateJsonData();
                Console.WriteLine($"Generating JSON data...");
            }
            else // csharp
            {
                outputFile = outputPrefix.EndsWith(".cs") ? outputPrefix : outputPrefix + ".cs";
                generatedData = generator.GenerateCSharpData();
                Console.WriteLine($"Generating C# data structures...");
            }

            File.WriteAllText(outputFile, generatedData);

            Console.WriteLine($"Successfully generated cold start data to {outputFile}");
            Console.WriteLine($"  File size: {new FileInfo(outputFile).Length:N0} bytes");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }

    /// <summary>
    /// Expands a set of seed class names to include all their superclasses.
    /// </summary>
    /// <param name="seedClassNames">The initial set of class names requested by the user.</param>
    /// <param name="availableClasses">All available class definitions from the parsed source.</param>
    /// <returns>A tuple containing the expanded list of classes in dependency order and any missing seed classes.</returns>
    private static (List<ClassDefinitionNode> Classes, HashSet<string> MissingClasses) ExpandClassFilterWithSuperclasses(
        HashSet<string> seedClassNames,
        List<ClassDefinitionNode> availableClasses)
    {
        // Create a lookup dictionary for quick class retrieval
        var classLookup = availableClasses.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // Track which seed classes are missing
        var missingClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Use a HashSet to track all classes we need (including superclasses)
        var requiredClassNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Process each seed class
        foreach (var seedClassName in seedClassNames)
        {
            if (!classLookup.TryGetValue(seedClassName, out var seedClass))
            {
                missingClasses.Add(seedClassName);
                continue;
            }

            // Walk up the superclass chain for this seed class
            var currentClass = seedClass;
            var visitedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Prevent circular references

            while (currentClass is not null)
            {
                // Add current class to required set
                requiredClassNames.Add(currentClass.Name);

                // Check for circular reference (defensive programming)
                if (!visitedClasses.Add(currentClass.Name))
                {
                    Console.WriteLine($"  Warning: Circular reference detected in class hierarchy for {currentClass.Name}");
                    break;
                }

                // Check if we've reached the top of the hierarchy
                if (string.IsNullOrEmpty(currentClass.Superclass) ||
                    currentClass.Superclass.Equals("nil", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Move to the superclass
                if (!classLookup.TryGetValue(currentClass.Superclass, out var superclass))
                {
                    // Superclass not found in available classes - this is normal for system classes like Object
                    // that may be defined elsewhere
                    break;
                }

                currentClass = superclass;
            }
        }

        // If any seed classes are missing, return early with error information
        if (missingClasses.Count > 0)
        {
            return ([], missingClasses);
        }

        // Return the filtered list in the original order (preserves dependency order from the source)
        var expandedClasses = availableClasses
            .Where(c => requiredClassNames.Contains(c.Name))
            .ToList();

        return (expandedClasses, missingClasses);
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