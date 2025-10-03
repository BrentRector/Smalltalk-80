using System.CommandLine;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Core.Objects;
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
        
        // GC validation option
        var validateGCOption = new Option<bool>(
            "--validate-gc",
            description: "Validate that all objects are reachable from SOA after cold start");
        
        // Class inspection options
        var printClassOption = new Option<string?>(
            "--print-class",
            description: "Print a specific class and its methods");
        
        var listColdStartClassesOption = new Option<bool>(
            "--list-cold-start-classes",
            description: "List all cold start classes");
        
        var listClassesOption = new Option<bool>(
            "--list-classes",
            description: "List all classes in the system");
        
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

        var testPrecompiledOption = new Option<bool>(
            "--test-precompiled",
            description: "Test execution of precompiled methods in TestClass");

        var validateBytecodeOption = new Option<bool>(
            "--validate-bytecode",
            description: "Validate bytecode execution through method contexts");
        
        var coldStartOption = new Option<FileInfo?>(
            "--cold-start",
            description: "Cold start from specific file");

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
        rootCommand.AddGlobalOption(validateGCOption);
        rootCommand.AddGlobalOption(printClassOption);
        rootCommand.AddGlobalOption(listColdStartClassesOption);
        rootCommand.AddGlobalOption(listClassesOption);
        rootCommand.AddGlobalOption(inspectClassOption);
        rootCommand.AddGlobalOption(enhancedPrintOption);
        rootCommand.AddGlobalOption(testCriticalMethodsOption);
        rootCommand.AddGlobalOption(testClassCreationOption);
        rootCommand.AddGlobalOption(testOOPBugOption);
        rootCommand.AddGlobalOption(testPrecompiledOption);
        rootCommand.AddGlobalOption(validateBytecodeOption);
        rootCommand.AddGlobalOption(coldStartOption);

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
        var tokens = parseResult.Tokens.ToList();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            // Handle options with values
            if (token.Value == "--print-class" && i + 1 < tokens.Count)
            {
                RuntimeOptions.ClassToPrint = tokens[i + 1].Value;
                i++; // Skip the next token since we consumed it
            }
            else if (token.Value == "--inspect-class" && i + 1 < tokens.Count)
            {
                RuntimeOptions.ClassToInspect = tokens[i + 1].Value;
                i++; // Skip the next token since we consumed it
            }
            else if (token.Value == "--cold-start-log")
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
            else if (token.Value == "--validate-bytecode")
            {
                RuntimeOptions.ValidateBytecodeExecution = true;
            }
            else if (token.Value == "--validate-gc")
            {
                RuntimeOptions.ValidateGC = true;
            }
            else if (token.Value == "--list-classes" || token.Value == "--list-cold-start-classes")
            {
                RuntimeOptions.ListColdStartClasses = true;
            }
            else if (token.Value == "--test-critical-methods")
            {
                RuntimeOptions.TestCriticalMethods = true;
            }
            else if (token.Value == "--test-class-creation-fixed")
            {
                RuntimeOptions.TestClassCreation = true;
            }
            else if (token.Value == "--test-oop-bug")
            {
                RuntimeOptions.TestOOPBug = true;
            }
            else if (token.Value == "--test-precompiled")
            {
                RuntimeOptions.TestPrecompiled = true;
            }
            else if (token.Value == "--trace-methods")
            {
                DebugOptions.EnableMethodTrace = true;
            }
            else if (token.Value == "--bytecode-trace")
            {
                DebugOptions.EnableBytecodeTrace = true;
            }
            else if (token.Value == "--show-bytecode")
            {
                DebugOptions.EnableBytecodeTrace = true;
            }
            else if (token.Value == "--show-mnemonics")
            {
                DebugOptions.ShowBytecodeWithMnemonics = true;
            }
            else if (token.Value?.StartsWith("--debug") == true)
            {
                // Handle --debug=mode or following value
                var debugMode = token.Value.Contains('=')
                    ? token.Value.Split('=')[1]
                    : "all"; // Default to all if no specific mode
                DebugOptions.SetDebugMode(debugMode);
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

            // Handle test precompiled option
            if (RuntimeOptions.TestPrecompiled)
            {
                RunPrecompiledTests(vm);
                return;
            }

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

    /// <summary>
    /// Runs precompiled method tests on TestClass.
    /// </summary>
    private static void RunPrecompiledTests(VirtualMachine vm)
    {
        Console.WriteLine("\n" + "=".PadRight(70, '='));
        Console.WriteLine("PRECOMPILED METHOD EXECUTION TESTS");
        Console.WriteLine("=".PadRight(70, '=') + "\n");

        int passed = 0;
        int failed = 0;

        // Test 1: testReturnConstant - should return 42
        try
        {
            Console.WriteLine("[TEST 1] TestClass>>testReturnConstant");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testReturnConstant");
            if (result.IsSmallInteger && result.AsSmallInteger() == 42)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 42");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED - Expected: 42, Got: {result}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            failed++;
        }

        Console.WriteLine();

        // Test 2: testAddition - should return 7 (3 + 4)
        try
        {
            Console.WriteLine("[TEST 2] TestClass>>testAddition");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testAddition");
            if (result.IsSmallInteger && result.AsSmallInteger() == 7)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 7");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED - Expected: 7, Got: {result}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine($"     {ex.StackTrace?.Split('\n')[0]}");
            failed++;
        }

        Console.WriteLine();

        // Test 3: testGetValue - should return 100 (value instance variable)
        try
        {
            Console.WriteLine("[TEST 3] TestClass>>testGetValue");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testGetValue");
            if (result.IsSmallInteger && result.AsSmallInteger() == 100)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 100");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED - Expected: 100, Got: {result}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine($"     {ex.StackTrace?.Split('\n')[0]}");
            failed++;
        }

        Console.WriteLine();

        // Test 4: testMultiplication - should return 42 (6 * 7)
        try
        {
            Console.WriteLine("[TEST 4] TestClass>>testMultiplication");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testMultiplication");
            if (result.IsSmallInteger && result.AsSmallInteger() == 42)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 42");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED - Expected: 42, Got: {result}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine($"     {ex.StackTrace?.Split('\n')[0]}");
            failed++;
        }

        Console.WriteLine();

        // Test 5: testTemporary - temporary variable store/load
        try
        {
            Console.WriteLine("[TEST 5] TestClass>>testTemporary");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testTemporary");
            if (result.IsSmallInteger && result.AsSmallInteger() == 99)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 99");
                passed++;
            }
            else
            {
                Console.WriteLine($"  ✗ FAILED - Expected: 99, Got: {result}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            failed++;
        }

        Console.WriteLine();

        // Test 6: testNestedLevel3 - nested sends with GC at deepest level
        try
        {
            Console.WriteLine("[TEST 6] TestClass>>testNestedLevel3 (4 nested calls with GC)");
            var result = vm.ExecutePrecompiledMethod("TestClass", "testNestedLevel3");
            if (result.IsSmallInteger && result.AsSmallInteger() == 88)
            {
                Console.WriteLine($"  ✓ PASSED - Result: 88 (GC triggered at deepest level)");
                passed++;
            }
            else
            {
                var resultStr = result.IsSmallInteger ? result.AsSmallInteger().ToString() : $"OOP({result.AsObjectPointer()})";
                Console.WriteLine($"  ✗ FAILED - Expected: 88, Got: {resultStr}");
                failed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ FAILED - Exception: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            failed++;
        }

        Console.WriteLine();

        // Test 7: Remove TestClass from SystemDictionary using Smalltalk removeKey:ifAbsent:
        Console.WriteLine("[POST-TEST] Deregistering TestClass from SystemDictionary...");
        try
        {
            // Before removal - verify TestClass exists
            var testClassBefore = SystemDictionary.LookupGlobalRef("TestClass");
            if (testClassBefore.HasValue && !testClassBefore.Value.IsNil)
            {
                Console.WriteLine($"  TestClass found before removal: OOP({testClassBefore.Value.AsObjectPointer()})");
            }

            // Remove TestClass from Smalltalk dictionary
            SystemDictionary.UnregisterGlobalRef("TestClass");

            // After removal - verify TestClass is gone
            var testClassAfter = SystemDictionary.LookupGlobalRef("TestClass");
            if (!testClassAfter.HasValue || testClassAfter.Value.IsNil)
            {
                Console.WriteLine("  ✓ TestClass successfully removed from SystemDictionary");
            }
            else
            {
                Console.WriteLine("  ✗ TestClass still exists after removal!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Failed to remove TestClass: {ex.Message}");
        }

        Console.WriteLine();

        // Post-test GC validation
        Console.WriteLine("[POST-TEST] Running final garbage collection...");
        var beforeGC = ObjectMemoryNew.ObjectTable.Count;
        var gcStats = vm.GarbageCollector.ForceCollection();
        Console.WriteLine($"  Final GC: {gcStats.ObjectsCollected} objects collected");
        Console.WriteLine($"  Objects before GC: {beforeGC}, after GC: {ObjectMemoryNew.ObjectTable.Count}");

        // Dump all remaining classes
        Console.WriteLine();
        Console.WriteLine("[POST-TEST] Remaining classes in SystemDictionary:");
        var remainingClasses = SystemDictionary.GetAllClassNames().OrderBy(n => n).ToList();
        Console.WriteLine($"  Total classes: {remainingClasses.Count}");
        foreach (var className in remainingClasses.Take(10))
        {
            Console.WriteLine($"    - {className}");
        }
        if (remainingClasses.Count > 10)
        {
            Console.WriteLine($"    ... and {remainingClasses.Count - 10} more");
        }

        // Verify TestClass is not in the list
        if (remainingClasses.Contains("TestClass"))
        {
            Console.WriteLine("  ✗ WARNING: TestClass still in class list after removal!");
        }
        else
        {
            Console.WriteLine("  ✓ Confirmed: TestClass not in class list");
        }

        Console.WriteLine();
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine($"RESULTS: {passed} passed, {failed} failed");
        Console.WriteLine("=".PadRight(70, '='));

        if (failed > 0)
        {
            Environment.ExitCode = 1;
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

            // Handle test precompiled option
            if (RuntimeOptions.TestPrecompiled)
            {
                RunPrecompiledTests(vm);
                return;
            }

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
        Console.Error.WriteLine($"\nFull exception details:\n{ex}");
        Environment.Exit(1);
    }
}