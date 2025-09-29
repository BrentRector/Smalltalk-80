using System.CommandLine;
using Smalltalk80.Interpreter.Config;
using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// </summary>
public static class Program
{
    internal static RuntimeOptions RuntimeOptions { get; set; } = RuntimeOptions.FromEnvironment();
    internal static ILoggerFactory? LoggerFactory { get; set; }
    
    // Compatibility properties
    internal static bool VerboseMode => RuntimeOptions.VerboseLogging;
    internal static bool ColdStartLog => RuntimeOptions.EnableColdStartLogging;
    internal static bool GCLog => RuntimeOptions.EnableGCLogging;

    /// <summary>
    /// Main entry point for the Smalltalk-80 interpreter.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // Initialize runtime options from environment
        RuntimeOptions = RuntimeOptions.FromEnvironment();
        
        var rootCommand = new RootCommand("Smalltalk-80 Interpreter - A faithful implementation of the Blue Book specification");

        // Global options
        var verboseOption = new Option<bool>(
            aliases: ["-v", "--verbose"],
            description: "Enable verbose output");
        
        var coldStartLogOption = new Option<bool>(
            "--cold-start-log",
            description: "Enable logging during system initialization (cold start)");
            
        var gcLogOption = new Option<bool>(
            "--gc-log",
            description: "Enable garbage collection logging");
        
        var logSendsOption = new Option<bool>(
            "--log-sends",
            description: "Enable message send logging (shows receiver, selector, arguments)");
        
        // Debug and trace options
        var debugOption = new Option<string?>(
            "--debug",
            description: "Enable debug mode (bytecode, methodtrace, class, trace, all, etc.)");
        
        var traceMethodsOption = new Option<bool>(
            "--trace-methods",
            description: "Trace method calls");
        
        var bytecodeTraceOption = new Option<bool>(
            "--bytecode-trace",
            description: "Trace bytecode execution");
        
        var showBytecodeOption = new Option<bool>(
            "--show-bytecode",
            description: "Show bytecode during execution");
        
        var showMnemonicsOption = new Option<bool>(
            "--show-mnemonics",
            description: "Show bytecode mnemonics");
        
        // Phase control options
        var skipPhase3Option = new Option<bool>(
            "--skip-phase3",
            description: "Skip phase 3 of bootstrap");
        
        var skipExtendedOption = new Option<bool>(
            "--skip-extended",
            description: "Skip extended library loading");
        
        var extendedLibraryOption = new Option<FileInfo?>(
            "--extended-library",
            description: "Load extended library from JSON file");
        
        var phase3Option = new Option<FileInfo?>(
            "--phase3",
            description: "Load phase 3 from specific file");
        
        // Class inspection options
        var printClassOption = new Option<string?>(
            "--print-class",
            description: "Print a specific class and its methods");
        
        var listBootstrapClassesOption = new Option<bool>(
            "--list-bootstrap-classes",
            description: "List all bootstrap classes");
        
        var listPhase2ClassesOption = new Option<bool>(
            "--list-phase2-classes",
            description: "List phase 2 classes");
        
        var inspectClassOption = new Option<string?>(
            "--inspect-class",
            description: "Inspect a class structure");
        
        var enhancedPrintOption = new Option<bool>(
            "--enhanced-print",
            description: "Enhanced pretty printing");
        
        // Test options
        var testCriticalMethodsOption = new Option<bool>(
            "--test-critical-methods",
            description: "Test critical methods");
        
        var testClassCreationOption = new Option<bool>(
            "--test-class-creation-fixed",
            description: "Test class creation");
        
        var testOOPBugOption = new Option<bool>(
            "--test-oop-bug",
            description: "Test OOP bug");
        
        
        var bootstrapOption = new Option<FileInfo?>(
            "--bootstrap",
            description: "Bootstrap from specific file");

        // Add all global options
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(coldStartLogOption);
        rootCommand.AddGlobalOption(gcLogOption);
        rootCommand.AddGlobalOption(logSendsOption);
        rootCommand.AddGlobalOption(debugOption);
        rootCommand.AddGlobalOption(traceMethodsOption);
        rootCommand.AddGlobalOption(bytecodeTraceOption);
        rootCommand.AddGlobalOption(showBytecodeOption);
        rootCommand.AddGlobalOption(showMnemonicsOption);
        rootCommand.AddGlobalOption(skipPhase3Option);
        rootCommand.AddGlobalOption(skipExtendedOption);
        rootCommand.AddGlobalOption(extendedLibraryOption);
        rootCommand.AddGlobalOption(phase3Option);
        rootCommand.AddGlobalOption(printClassOption);
        rootCommand.AddGlobalOption(listBootstrapClassesOption);
        rootCommand.AddGlobalOption(listPhase2ClassesOption);
        rootCommand.AddGlobalOption(inspectClassOption);
        rootCommand.AddGlobalOption(enhancedPrintOption);
        rootCommand.AddGlobalOption(testCriticalMethodsOption);
        rootCommand.AddGlobalOption(testClassCreationOption);
        rootCommand.AddGlobalOption(testOOPBugOption);
        rootCommand.AddGlobalOption(bootstrapOption);

        // Run command
        var runCommand = new Command("run", "Execute a Smalltalk file or expression");
        var fileArgument = new Argument<FileInfo?>("file", () => null, "Smalltalk source file to execute");
        var expressionOption = new Option<string?>(
            aliases: ["-e", "--expression"],
            description: "Smalltalk expression to evaluate");
        runCommand.AddArgument(fileArgument);
        runCommand.AddOption(expressionOption);
        runCommand.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(fileArgument);
            var expression = context.ParseResult.GetValueForOption(expressionOption);
            UpdateRuntimeOptions(context.ParseResult);
            await ExecuteRun(file, expression);
        });
        rootCommand.AddCommand(runCommand);

        // REPL command
        var replCommand = new Command("repl", "Start interactive REPL");
        replCommand.SetHandler(async (context) =>
        {
            UpdateRuntimeOptions(context.ParseResult);
            await StartRepl();
        });
        rootCommand.AddCommand(replCommand);

        // Compile command
        var compileCommand = new Command("compile", "Compile Smalltalk source to bytecode");
        var compileFileArgument = new Argument<FileInfo>("file", "Smalltalk source file to compile");
        var outputOption = new Option<FileInfo?>(
            aliases: ["-o", "--output"],
            description: "Output file for compiled bytecode");
        compileCommand.AddArgument(compileFileArgument);
        compileCommand.AddOption(outputOption);
        compileCommand.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument(compileFileArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            UpdateRuntimeOptions(context.ParseResult);
            await CompileSource(file, output);
        });
        rootCommand.AddCommand(compileCommand);

        // Inspect command
        var inspectCommand = new Command("inspect", "Inspect a system image or compiled method");
        var inspectFileArgument = new Argument<FileInfo>("file", "File to inspect");
        inspectCommand.AddArgument(inspectFileArgument);
        inspectCommand.SetHandler((context) =>
        {
            var file = context.ParseResult.GetValueForArgument(inspectFileArgument);
            UpdateRuntimeOptions(context.ParseResult);
            return InspectFile(file);
        });
        rootCommand.AddCommand(inspectCommand);

        // Note: Cold start is now performed automatically on startup.
        // The --cold-start-log option can be used to enable logging during initialization.

        // Eval command
        var evalCommand = new Command("eval", "Evaluate a Smalltalk expression");
        var evalExpressionArgument = new Argument<string>("expression", "Smalltalk expression to evaluate");
        evalCommand.AddArgument(evalExpressionArgument);
        evalCommand.SetHandler(async (context) =>
        {
            var expression = context.ParseResult.GetValueForArgument(evalExpressionArgument);
            UpdateRuntimeOptions(context.ParseResult);
            await ExecuteEval(expression);
        });
        rootCommand.AddCommand(evalCommand);

        return await rootCommand.InvokeAsync(args);
    }
    
    /// <summary>
    /// Updates runtime options from command line parse results.
    /// </summary>
    private static void UpdateRuntimeOptions(System.CommandLine.Parsing.ParseResult parseResult)
    {
        // Update runtime options from parsed command line arguments
        // Check each token in the parsed result
        foreach (var token in parseResult.Tokens)
        {
            if (token.Value == "--cold-start-log")
            {
                RuntimeOptions.EnableColdStartLogging = true;
            }
            else if (token.Value == "--gc-log")
            {
                RuntimeOptions.EnableGCLogging = true;
            }
            else if (token.Value == "--log-sends")
            {
                RuntimeOptions.EnableSendLogging = true;
            }
            else if (token.Value == "-v" || token.Value == "--verbose")
            {
                RuntimeOptions.VerboseLogging = true;
            }
        }
        
        // Initialize logger factory with the updated options
        LoggerFactory = LoggingConfiguration.CreateLoggerFactory(RuntimeOptions.VerboseLogging);
    }

    private static async Task ExecuteRun(FileInfo? file, string? expression)
    {
        
        try
        {
            var vm = new VirtualMachine();
            await vm.InitializeAsync();

            if (file != null)
            {
                if (!file.Exists)
                {
                    Console.Error.WriteLine($"Error: File not found: {file.FullName}");
                    Environment.Exit(1);
                }
                
                var source = await File.ReadAllTextAsync(file.FullName);
                var result = vm.Evaluate(source);
                Console.WriteLine($"Result: {result}");
            }
            else if (!string.IsNullOrEmpty(expression))
            {
                var result = vm.Evaluate(expression);
                Console.WriteLine($"Result: {result}");
            }
            else
            {
                Console.WriteLine("Error: Must specify either a file or expression (-e)");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private static async Task StartRepl()
    {
        
        try
        {
            var vm = new VirtualMachine();
            await vm.InitializeAsync();
            
            Console.WriteLine("Smalltalk-80 REPL");
            Console.WriteLine("Type 'exit' to quit");
            Console.WriteLine();

            while (true)
            {
                Console.Write("st> ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                    
                if (input.Trim().ToLowerInvariant() == "exit")
                    break;

                try
                {
                    var result = vm.Evaluate(input);
                    Console.WriteLine($"=> {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (VerboseMode)
                    {
                        Console.WriteLine(ex.StackTrace);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private static async Task CompileSource(FileInfo file, FileInfo? output)
    {
        
        try
        {
            if (!file.Exists)
            {
                Console.Error.WriteLine($"Error: File not found: {file.FullName}");
                Environment.Exit(1);
            }

            var source = await File.ReadAllTextAsync(file.FullName);
            var compiler = new Compiler();
            var bytecode = compiler.Compile(source);
            
            var outputFile = output?.FullName ?? Path.ChangeExtension(file.FullName, ".stc");
            await File.WriteAllBytesAsync(outputFile, bytecode);
            
            Console.WriteLine($"Compiled to: {outputFile}");
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private static Task InspectFile(FileInfo file)
    {
        
        try
        {
            if (!file.Exists)
            {
                Console.Error.WriteLine($"Error: File not found: {file.FullName}");
                Environment.Exit(1);
            }

            // TODO: Implement inspection logic
            Console.WriteLine($"Inspecting: {file.FullName}");
            Console.WriteLine("Inspection not yet implemented");
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
        
        return Task.CompletedTask;
    }


    private static async Task ExecuteEval(string expression)
    {
        
        try
        {
            var vm = new VirtualMachine();
            await vm.InitializeAsync();
            
            var result = vm.Evaluate(expression);
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }


    private static void HandleError(Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (VerboseMode)
        {
            Console.Error.WriteLine(ex.StackTrace);
        }
        Environment.Exit(1);
    }
}