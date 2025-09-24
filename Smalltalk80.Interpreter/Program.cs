using System.Text.Json;
using Core;
using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Abstractions.Primitives;
using Core.Execution;
using Core.Memory;
using Core.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Config;

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

        var command = args[0].ToLowerInvariant();
        var remainingArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "run" => await ExecuteRun(remainingArgs),
                "repl" => await StartRepl(remainingArgs),
                "compile" => await CompileSource(remainingArgs),
                "inspect" => await InspectFile(remainingArgs),
                "cold-start" => await ExecuteColdStart(remainingArgs),
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
        Console.WriteLine("  cold-start [-s source]                   Bootstrap from minimal kernel");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help      Show this help message");
        Console.WriteLine("  -v, --verbose   Enable verbose output");
        Console.WriteLine("  -i, --image     Path to system image file");
        Console.WriteLine("  -e, --expr      Smalltalk expression to evaluate");
        Console.WriteLine("  -o, --output    Output file path");
        Console.WriteLine("  -s, --source    Path to Smalltalk-80.st source file");
        return 0;
    }

    private static async Task<int> ExecuteRun(string[] args)
    {
        string? filePath = null;
        string? expression = null;
        string? imagePath = null;
        bool verbose = false;

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
        var services = ConfigureServices(verbose);
        await using var serviceProvider = services.BuildServiceProvider();

        var vm = serviceProvider.GetRequiredService<VirtualMachine>();
        var logger = serviceProvider.GetRequiredService<ILogger<VirtualMachine>>();

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
        var result = vm.Execute(OOP.Nil, "doIt", Array.Empty<OOP>());

        Console.WriteLine($"Result: {result}");

        if (verbose)
        {
            var stats = vm.GetStatistics();
            Console.WriteLine($"\nExecution Statistics:");
            Console.WriteLine($"  Methods executed: {stats.MethodsExecuted}");
            Console.WriteLine($"  Memory used: {stats.MemoryUsed:N0} bytes");
        }

        return 0;
    }

    private static async Task<int> StartRepl(string[] args)
    {
        string? imagePath = null;
        bool verbose = false;

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
        var services = ConfigureServices(verbose);
        await using var serviceProvider = services.BuildServiceProvider();

        var vm = serviceProvider.GetRequiredService<VirtualMachine>();
        var logger = serviceProvider.GetRequiredService<ILogger<VirtualMachine>>();

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
                vm.CollectGarbage();
                Console.WriteLine("Garbage collection performed.");
                continue;
            }

            if (input.Equals("stats", StringComparison.OrdinalIgnoreCase))
            {
                var stats = vm.GetStatistics();
                Console.WriteLine($"Methods executed: {stats.MethodsExecuted}");
                Console.WriteLine($"Messages sent: {stats.MessagesSent}");
                Console.WriteLine($"Memory used: {stats.MemoryUsed:N0} bytes");
                continue;
            }

            // Execute Smalltalk expression
            try
            {
                var method = CompileExpression(input);
                var result = vm.Execute(OOP.Nil, "doIt", Array.Empty<OOP>());
                Console.WriteLine($"=> {result}");
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

    private static async Task<int> CompileSource(string[] args)
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

    private static async Task<int> ExecuteColdStart(string[] args)
    {
        string? sourcePath = null;
        bool verbose = false;

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

        Console.WriteLine($"[COLD START] Using source file: {sourcePath}");

        // Set up dependency injection
        var services = ConfigureServices(verbose);
        await using var serviceProvider = services.BuildServiceProvider();

        var memory = serviceProvider.GetRequiredService<IObjectMemory>();
        var logger = serviceProvider.GetRequiredService<ILogger<ColdStartBootstrap>>();

        var bootstrap = new ColdStartBootstrap(memory, logger);

        try
        {
            await bootstrap.BootstrapAsync(sourcePath);
            Console.WriteLine("[COLD START] Bootstrap completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[COLD START] Bootstrap failed: {ex.Message}");
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

    private static IServiceCollection ConfigureServices(bool verbose)
    {
        var services = new ServiceCollection();

        // Add logging with modernized configuration
        services.AddLogging(builder => LoggingConfiguration.ConfigureLogging(builder, verbose));

        // Register core services
        services.AddSingleton<IObjectMemory, ObjectMemory>();
        services.AddSingleton<IMethodLookup, MethodLookup>();
        services.AddSingleton<IPrimitiveTable, PrimitiveTable>();
        services.AddSingleton<IBytecodeDispatcher>(provider =>
        {
            var methodLookup = provider.GetRequiredService<IMethodLookup>();
            return new BytecodeDispatcher(methodLookup);
        });
        services.AddSingleton<VirtualMachine>();
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
            literals: Array.Empty<OOP>(),
            bytecodes: new byte[] { 0x78 }, // returnReceiver
            useLargeContext: false
        );
    }

    private static async Task LoadSystemImage(VirtualMachine vm, string imagePath, IServiceProvider serviceProvider)
    {
        var loader = serviceProvider.GetRequiredService<SystemImageLoader>();
        var systemImage = await loader.LoadFromFileAsync(imagePath);
        
        // Load classes and methods into VM
        // This would normally populate the ObjectMemory and MethodLookup
        var logger = serviceProvider.GetRequiredService<ILogger<VirtualMachine>>();
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