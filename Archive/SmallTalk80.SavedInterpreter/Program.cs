using System.Text.Json;
using Core;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Abstractions.Primitives;
using Core.Execution;
using Core.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Compiler;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            ShowHelp();
            return 0;
        }

        // Parse global flags first
        var runtimeOptions = RuntimeOptions.Default;
        var filteredArgs = new List<string>();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cold-start-log":
                    runtimeOptions.EnableColdStartLogging = true;
                    break;
                case "--gc-log":
                    runtimeOptions.EnableGCLogging = true;
                    break;
                case "-v":
                case "--verbose":
                    runtimeOptions.VerboseLogging = true;
                    break;
                default:
                    filteredArgs.Add(args[i]);
                    break;
            }
        }
        
        if (filteredArgs.Count == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = filteredArgs[0].ToLowerInvariant();
        var remainingArgs = filteredArgs.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "run" => await ExecuteRun(remainingArgs, runtimeOptions),
                "repl" => await StartRepl(remainingArgs, runtimeOptions),
                "compile" => await CompileSource(remainingArgs, runtimeOptions),
                "inspect" => await InspectFile(remainingArgs),
                "cold-start" => await ExecuteColdStart(remainingArgs, runtimeOptions),
                "eval" => await ExecuteEval(remainingArgs, runtimeOptions),
                _ => ShowHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ShowHelp()
    {
        Console.WriteLine("Smalltalk-80 Interpreter - A faithful implementation of the Blue Book specification");
        Console.WriteLine();
        Console.WriteLine("Usage: Smalltalk80.Interpreter <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run [file] [-e expression] [-i image]   Execute a Smalltalk file or expression");
        Console.WriteLine("  repl [-i image]                          Start interactive REPL");
        Console.WriteLine("  compile <file> [-o output]               Compile Smalltalk source to bytecode");
        Console.WriteLine("  inspect <file>                           Inspect a system image or compiled method");
        Console.WriteLine("  cold-start [-s source] [--print-classes] Bootstrap from minimal kernel");
        Console.WriteLine("  eval <expression>                        Evaluate a Smalltalk expression after cold start");
        Console.WriteLine();
        Console.WriteLine("Global Flags (can be used with any command):");
        Console.WriteLine("  --cold-start-log    Enable [COLD START] logging messages");
        Console.WriteLine("  --gc-log            Enable [GC] garbage collection logging messages");
        Console.WriteLine("  -v, --verbose       Enable verbose output");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help          Show this help message");
        Console.WriteLine("  -i, --image         Path to system image file");
        Console.WriteLine("  -e, --expr          Smalltalk expression to evaluate");
        Console.WriteLine("  -o, --output        Output file path");
        Console.WriteLine("  -s, --source        Path to Smalltalk-80.st source file");
        Console.WriteLine("  --print-classes     Pretty print all classes and methods after cold start");
        return 0;
    }

    private static async Task<int> ExecuteRun(string[] args, RuntimeOptions runtimeOptions)
    {
        string? filePath = null;
        string? expression = null;
        string? imagePath = null;
        bool verbose = runtimeOptions.VerboseLogging;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-e":
                case "--expr":
                    if (i + 1 < args.Length)
                        expression = args[++i];
                    break;
                case "-i":
                case "--image":
                    if (i + 1 < args.Length)
                        imagePath = args[++i];
                    break;
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    if (!args[i].StartsWith("-"))
                        filePath = args[i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(filePath) && string.IsNullOrEmpty(expression))
        {
            Console.Error.WriteLine("Error: No input specified. Use a file or -e option.");
            return 1;
        }

        // Set up dependency injection
        var services = ConfigureServices(verbose, runtimeOptions);
        await using var serviceProvider = services.BuildServiceProvider();

        var vm = serviceProvider.GetRequiredService<Smalltalk80.Interpreter.VM.VirtualMachine>();
        var logger = serviceProvider.GetRequiredService<ILogger<Smalltalk80.Interpreter.VM.VirtualMachine>>();

        // Load system image if provided
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            logger.LogInformation("Loading system image from {Path}", imagePath);
            await LoadSystemImage(vm, imagePath, serviceProvider);
        }

        // Determine what to execute
        string sourceCode;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            logger.LogInformation("Reading source from {Path}", filePath);
            sourceCode = await File.ReadAllTextAsync(filePath);
        }
        else if (!string.IsNullOrEmpty(expression))
        {
            sourceCode = expression;
        }
        else
        {
            Console.Error.WriteLine("Error: Input file not found.");
            return 1;
        }

        // Compile and execute
        logger.LogInformation("Compiling expression...");
        var method = CompileExpression(sourceCode);

        logger.LogInformation("Executing...");
        // TODO: Execute the compiled method
        // var result = vm.Execute(OOP.Nil, "doIt", Array.Empty<OOP>());

        Console.WriteLine($"Result: Not yet implemented");

        if (verbose)
        {
            // TODO: Get statistics from VM
            // var stats = vm.GetStatistics();
            Console.WriteLine($"\nExecution Statistics:");
            Console.WriteLine($"  Not yet implemented");
        }

        return 0;
    }

    private static async Task<int> StartRepl(string[] args, RuntimeOptions runtimeOptions)
    {
        string? imagePath = null;
        bool verbose = runtimeOptions.VerboseLogging;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-i":
                case "--image":
                    if (i + 1 < args.Length)
                        imagePath = args[++i];
                    break;
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
            }
        }

        // Set up dependency injection
        var services = ConfigureServices(verbose, runtimeOptions);
        await using var serviceProvider = services.BuildServiceProvider();

        var vm = serviceProvider.GetRequiredService<Smalltalk80.Interpreter.VM.VirtualMachine>();
        var logger = serviceProvider.GetRequiredService<ILogger<Smalltalk80.Interpreter.VM.VirtualMachine>>();

        // Load system image if provided
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            logger.LogInformation("Loading system image from {Path}", imagePath);
            await LoadSystemImage(vm, imagePath, serviceProvider);
        }

        Console.WriteLine("Smalltalk-80 Interactive REPL");
        Console.WriteLine("Type 'exit' or 'quit' to leave, 'help' for commands");
        Console.WriteLine();

        while (true)
        {
            Console.Write("st> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            input = input.Trim();

            // Check for commands
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowReplHelp();
                continue;
            }

            if (input.Equals("gc", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Implement garbage collection
                // vm.CollectGarbage();
                Console.WriteLine("Garbage collection not yet implemented.");
                continue;
            }

            if (input.Equals("stats", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Get statistics from VM
                // var stats = vm.GetStatistics();
                Console.WriteLine($"Statistics not yet implemented");
                continue;
            }

            // Execute Smalltalk expression
            try
            {
                // TODO: Implement expression execution
                // var method = CompileExpression(input);
                // var result = vm.Execute(OOP.Nil, "doIt", Array.Empty<OOP>());
                Console.WriteLine($"Expression execution not yet implemented");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (verbose)
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
            }
        }

        Console.WriteLine("Goodbye!");
        return 0;
    }

    private static async Task<int> CompileSource(string[] args, RuntimeOptions runtimeOptions)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: No input file specified.");
            return 1;
        }

        string inputPath = args[0];
        string? outputPath = null;
        bool verbose = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                        outputPath = args[++i];
                    break;
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
            }
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return 1;
        }

        var sourceCode = await File.ReadAllTextAsync(inputPath);
        var method = CompileExpression(sourceCode);

        // Determine output file
        outputPath ??= Path.ChangeExtension(inputPath, ".json");

        // Write output as JSON
        var json = JsonSerializer.Serialize(new
        {
            Header = method.Header,
            ArgumentCount = method.ArgumentCount,
            TemporaryCount = method.TemporaryCount,
            PrimitiveIndex = method.PrimitiveIndex,
            Literals = method.Literals.Select(l => l.ToString()),
            Bytecodes = method.Bytecodes.Select(b => $"0x{b:X2}")
        }, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine($"Compiled to: {outputPath}");

        if (verbose)
        {
            Console.WriteLine($"Method info:");
            Console.WriteLine($"  Arguments: {method.ArgumentCount}");
            Console.WriteLine($"  Temporaries: {method.TemporaryCount}");
            Console.WriteLine($"  Literals: {method.LiteralCount}");
            Console.WriteLine($"  Bytecodes: {method.Bytecodes.Length} bytes");
        }

        return 0;
    }

    private static async Task<int> ExecuteEval(string[] args, RuntimeOptions runtimeOptions)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: No expression provided.");
            return 1;
        }

        // Join all arguments as the expression (in case it contains spaces)
        string expression = string.Join(" ", args);
        bool verbose = runtimeOptions.VerboseLogging || args.Contains("-v") || args.Contains("--verbose");

        // Set up dependency injection
        var services = ConfigureServices(verbose, runtimeOptions);
        await using var serviceProvider = services.BuildServiceProvider();

        // For eval command, we need to manage memory carefully
        // Create Smalltalk80.Interpreter.Memory.ObjectMemory for the VM
        var interpreterMemory = new Smalltalk80.Interpreter.Memory.ObjectMemory(262144);
        
        // But ColdStartBootstrap needs Core.IObjectMemory, so get it from DI
        var coreMemory = serviceProvider.GetRequiredService<IObjectMemory>();
        var logger = serviceProvider.GetRequiredService<ILogger<ColdStartBootstrap>>();

        try
        {
            // First perform cold start to bootstrap the system using Core memory
            logger.LogInformation("Performing cold start before expression evaluation...");
            
            // Find Smalltalk-80.st source file
            string? sourcePath = null;
            var possiblePaths = new[]
            {
                "Smalltalk-80 Source/Smalltalk-80.st",
                "../Smalltalk-80 Source/Smalltalk-80.st",
                "../../Smalltalk-80 Source/Smalltalk-80.st",
                "../../../Smalltalk-80 Source/Smalltalk-80.st",
                "D:\\Users\\brent\\OneDrive\\Src\\Smalltalk-80\\Smalltalk-80 Source\\Smalltalk-80.st"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    sourcePath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(sourcePath))
            {
                Console.Error.WriteLine("Error: Could not find Smalltalk-80.st source file.");
                return 1;
            }

            logger.LogInformation("Using source file: {Path}", sourcePath);
            
            // The eval command should use a minimal VM cold start, not the full system image
            // The architecture issue remains: ColdStartBootstrap uses Core.IObjectMemory 
            // while VM uses Interpreter.Memory.ObjectMemory
            
            // For now, create a VM with minimal cold start
            var vm = new Smalltalk80.Interpreter.VM.VirtualMachine(
                logger: serviceProvider.GetRequiredService<ILogger<Smalltalk80.Interpreter.VM.VirtualMachine>>()
            );
            
            // Use the minimal cold start that creates core classes and installs primitives
            logger.LogInformation("Performing minimal cold start for expression evaluation");
            vm.ColdStart();
            
            // Execute the expression through the VM
            // The VM's ExecuteExpression method handles:
            // 1. Compiling the expression to bytecode
            // 2. Creating a CompiledMethod object
            // 3. Setting up a proper execution context
            // 4. Executing through the bytecode dispatcher
            // 5. Returning the actual result
            var result = vm.ExecuteExpression(expression);
            
            // Format and display the result
            Console.WriteLine();
            Console.WriteLine("=== RESULT ===");
            if (result.IsSmallInteger)
            {
                Console.WriteLine($"SmallInteger: {result.SmallIntegerValue}");
            }
            else if (result.IsNil)
            {
                Console.WriteLine("nil");
            }
            else
            {
                // Get object class name for better output
                var classOop = vm.GetClassOfObject(result);
                if (!classOop.IsNil)
                {
                    var className = vm.GetSymbolString(vm.GetField(classOop, "Behavior", "name"));
                    
                    // Special handling for booleans
                    if (className == "True")
                    {
                        Console.WriteLine("true");
                    }
                    else if (className == "False")
                    {
                        Console.WriteLine("false");
                    }
                    else
                    {
                        Console.WriteLine($"{className} instance: {result}");
                    }
                }
                else
                {
                    Console.WriteLine($"Object: {result}");
                }
            }
            Console.WriteLine("==============");
            Console.WriteLine();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error evaluating expression: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static async Task<int> ExecuteColdStart(string[] args, RuntimeOptions runtimeOptions)
    {
        string? sourcePath = null;
        bool verbose = runtimeOptions.VerboseLogging;
        bool printClasses = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-s":
                case "--source":
                    if (i + 1 < args.Length)
                        sourcePath = args[++i];
                    break;
                case "-v":
                case "--verbose":
                    verbose = true;
                    break;
                case "--print-classes":
                    printClasses = true;
                    break;
                default:
                    if (!args[i].StartsWith("-"))
                        sourcePath = args[i];
                    break;
            }
        }

        // Default to looking for Smalltalk-80.st in known locations
        if (string.IsNullOrEmpty(sourcePath))
        {
            var possiblePaths = new[]
            {
                "Smalltalk-80 Source/Smalltalk-80.st",
                "../Smalltalk-80 Source/Smalltalk-80.st",
                "../../Smalltalk-80 Source/Smalltalk-80.st",
                "../../../Smalltalk-80 Source/Smalltalk-80.st"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    sourcePath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(sourcePath))
            {
                Console.Error.WriteLine("Error: Could not find Smalltalk-80.st source file.");
                Console.Error.WriteLine("Please specify the path with -s option.");
                return 1;
            }
        }

        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Error: Source file not found: {sourcePath}");
            return 1;
        }

        if (runtimeOptions.EnableColdStartLogging)
        {
            Console.WriteLine($"[COLD START] Using source file: {sourcePath}");
        }

        // Set up dependency injection
        var services = ConfigureServices(verbose, runtimeOptions);
        await using var serviceProvider = services.BuildServiceProvider();

        var memory = serviceProvider.GetRequiredService<IObjectMemory>();
        var logger = serviceProvider.GetRequiredService<ILogger<ColdStartBootstrap>>();

        var bootstrap = new ColdStartBootstrap(memory, logger, runtimeOptions);

        try
        {
            await bootstrap.BootstrapAsync(sourcePath);
            if (runtimeOptions.EnableColdStartLogging)
            {
                Console.WriteLine("[COLD START] Bootstrap completed successfully!");
            }
            
            // Print all classes if requested
            if (printClasses)
            {
                var classPrinterLogger = serviceProvider.GetRequiredService<ILogger<Diagnostics.ClassPrinter>>();
                var classPrinter = new Diagnostics.ClassPrinter(memory, classPrinterLogger);
                classPrinter.PrintAllClasses(bootstrap.Classes);
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            if (runtimeOptions.EnableColdStartLogging)
            {
                Console.Error.WriteLine($"[COLD START] Bootstrap failed: {ex.Message}");
            }
            else
            {
                Console.Error.WriteLine($"Bootstrap failed: {ex.Message}");
            }
            if (verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static async Task<int> InspectFile(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: No file specified.");
            return 1;
        }

        string filePath = args[0];
        
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        var fileInfo = new FileInfo(filePath);
        var content = await File.ReadAllTextAsync(filePath);

        // Try to parse as JSON
        try
        {
            var json = JsonDocument.Parse(content);
            Console.WriteLine($"File: {fileInfo.Name}");
            Console.WriteLine($"Type: JSON");
            Console.WriteLine($"Size: {fileInfo.Length:N0} bytes");
            
            // Show some basic info about the JSON structure
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                Console.WriteLine($"Properties: {string.Join(", ", root.EnumerateObject().Select(p => p.Name))}");
            }
        }
        catch (JsonException)
        {
            Console.WriteLine($"File: {fileInfo.Name}");
            Console.WriteLine($"Type: Unknown/Binary");
            Console.WriteLine($"Size: {fileInfo.Length:N0} bytes");
        }

        return 0;
    }

    private static IServiceCollection ConfigureServices(bool verbose, RuntimeOptions? runtimeOptions = null)
    {
        runtimeOptions ??= RuntimeOptions.Default;
        var services = new ServiceCollection();
        
        // Register runtime options as singleton so all components can access it
        services.AddSingleton(runtimeOptions);

        // Add logging with modernized configuration
        services.AddLogging(builder => LoggingConfiguration.ConfigureLogging(builder, verbose));

        // Register core services
        // ColdStartBootstrap needs Core.Memory.ObjectMemory via IObjectMemory interface
        services.AddSingleton<IObjectMemory, Core.Memory.ObjectMemory>();
        services.AddSingleton<IMethodLookup, MethodLookup>();
        services.AddSingleton<IPrimitiveTable, PrimitiveTable>();
        
        // Don't create BytecodeDispatcher here - it's created internally by VirtualMachine
        // services.AddSingleton<IBytecodeDispatcher>(provider =>
        // {
        //     var methodLookup = provider.GetRequiredService<IMethodLookup>();
        //     return new BytecodeDispatcher(methodLookup);
        // });
        
        // Don't register VirtualMachine as singleton here - we need to create it with the bootstrapped memory
        // services.AddSingleton<Smalltalk80.Interpreter.VM.VirtualMachine>();
        
        services.AddSingleton<SystemImageLoader>();

        return services;
    }

    private static CompiledMethod CompileExpression(string sourceCode)
    {
        // This is a placeholder - integrate with actual compiler
        // For now, return a simple method that returns receiver
        return CompiledMethod.Create(
            argumentCount: 0,
            temporaryCount: 0,
            primitiveIndex: 0,
            literals: Array.Empty<Core.Memory.OOP>(),
            bytecodes: new byte[] { 0x78 }, // returnReceiver
            useLargeContext: false
        );
    }

    private static async Task LoadSystemImage(Smalltalk80.Interpreter.VM.VirtualMachine vm, string imagePath, IServiceProvider serviceProvider)
    {
        var loader = serviceProvider.GetRequiredService<SystemImageLoader>();
        var systemImage = await loader.LoadFromFileAsync(imagePath);
        
        // Load classes and methods into VM
        // This would normally populate the ObjectMemory and MethodLookup
        var logger = serviceProvider.GetRequiredService<ILogger<Smalltalk80.Interpreter.VM.VirtualMachine>>();
        logger.LogInformation("Loaded {ClassCount} classes and {MethodCount} methods",
            systemImage.Classes.Count, systemImage.Methods.Count);
    }

    private static void ShowReplHelp()
    {
        Console.WriteLine("\nREPL Commands:");
        Console.WriteLine("  exit, quit  - Exit the REPL");
        Console.WriteLine("  help        - Show this help");
        Console.WriteLine("  gc          - Run garbage collection");
        Console.WriteLine("  stats       - Show VM statistics");
        Console.WriteLine("\nEnter any Smalltalk expression to evaluate it.");
        Console.WriteLine();
    }
}