using System.CommandLine;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Smalltalk80.Bootstrap;
using Smalltalk80.Core;
using Smalltalk80.VM;

namespace Smalltalk80.CLI;

/// <summary>
/// Main entry point for the Smalltalk-80 command-line interface.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Smalltalk-80 Virtual Machine")
        {
            TreatUnmatchedTokensAsErrors = false
        };

        // Global options
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");

        var traceOption = new Option<bool>(
            aliases: new[] { "--trace", "-t" },
            description: "Enable execution tracing");

        var bootstrapDirOption = new Option<string?>(
            aliases: new[] { "--bootstrap-dir", "-b" },
            description: "Path to bootstrap JSON files directory",
            getDefaultValue: () => null);

        var modeOption = new Option<BootstrapMode>(
            aliases: new[] { "--mode", "-m" },
            description: "Bootstrap mode",
            getDefaultValue: () => BootstrapMode.Full);

        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(traceOption);
        rootCommand.AddOption(bootstrapDirOption);
        rootCommand.AddOption(modeOption);

        // Run command (default)
        var runCommand = new Command("run", "Run a Smalltalk file")
        {
            new Argument<FileInfo?>("file", "Smalltalk source file to execute") { Arity = ArgumentArity.ZeroOrOne }
        };

        runCommand.SetHandler(async (context) =>
        {
            var file = context.ParseResult.GetValueForArgument<FileInfo?>(
                context.ParseResult.CommandResult.Command.Arguments.OfType<Argument<FileInfo?>>().First());
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var trace = context.ParseResult.GetValueForOption(traceOption);
            var bootstrapDir = context.ParseResult.GetValueForOption(bootstrapDirOption);
            var mode = context.ParseResult.GetValueForOption(modeOption);

            await RunSmalltalk(file?.FullName, verbose, trace, bootstrapDir, mode);
        });

        // REPL command
        var replCommand = new Command("repl", "Start interactive REPL");
        replCommand.SetHandler(async (context) =>
        {
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var trace = context.ParseResult.GetValueForOption(traceOption);
            var bootstrapDir = context.ParseResult.GetValueForOption(bootstrapDirOption);
            var mode = context.ParseResult.GetValueForOption(modeOption);

            await StartRepl(verbose, trace, bootstrapDir, mode);
        });

        // Validate command
        var validateCommand = new Command("validate", "Validate bootstrap JSON files");
        validateCommand.SetHandler(async (context) =>
        {
            var bootstrapDir = context.ParseResult.GetValueForOption(bootstrapDirOption);
            await ValidateBootstrap(bootstrapDir);
        });

        rootCommand.AddCommand(runCommand);
        rootCommand.AddCommand(replCommand);
        rootCommand.AddCommand(validateCommand);

        // Default behavior when no command specified
        rootCommand.SetHandler(async (context) =>
        {
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var trace = context.ParseResult.GetValueForOption(traceOption);
            var bootstrapDir = context.ParseResult.GetValueForOption(bootstrapDirOption);
            var mode = context.ParseResult.GetValueForOption(modeOption);

            // Check if there's an unnamed argument (file to run)
            var tokens = context.ParseResult.UnmatchedTokens;
            if (tokens.Count > 0 && File.Exists(tokens[0]))
            {
                await RunSmalltalk(tokens[0], verbose, trace, bootstrapDir, mode);
            }
            else
            {
                await StartRepl(verbose, trace, bootstrapDir, mode);
            }
        });

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Runs a Smalltalk source file.
    /// </summary>
    private static async Task RunSmalltalk(
        string? filePath,
        bool verbose,
        bool trace,
        string? bootstrapDir,
        BootstrapMode mode)
    {
        using var logger = CreateLogger(verbose);
        
        logger.LogInformation("Starting Smalltalk-80 VM...");
        
        try
        {
            // Initialize VM
            var vm = await CreateAndBootstrapVM(bootstrapDir, mode, trace, logger);
            
            // Load and execute file if provided
            if (!string.IsNullOrEmpty(filePath))
            {
                logger.LogInformation("Loading file: {FilePath}", filePath);
                await vm.LoadFileAsync(filePath);
            }
            else
            {
                logger.LogInformation("VM ready. No file specified.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run Smalltalk");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Starts the interactive REPL.
    /// </summary>
    private static async Task StartRepl(
        bool verbose,
        bool trace,
        string? bootstrapDir,
        BootstrapMode mode)
    {
        using var logger = CreateLogger(verbose);
        
        Console.WriteLine("Smalltalk-80 Interactive REPL");
        Console.WriteLine("Type 'exit' or 'quit' to leave, 'help' for commands");
        Console.WriteLine();

        try
        {
            // Initialize VM
            var vm = await CreateAndBootstrapVM(bootstrapDir, mode, trace, logger);
            
            // REPL loop
            while (true)
            {
                Console.Write("st> ");
                var input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                    
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;
                    
                if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    ShowHelp();
                    continue;
                }
                
                try
                {
                    // Evaluate the input as Smalltalk code
                    var result = await vm.EvaluateAsync(input);
                    if (result != null)
                    {
                        Console.WriteLine($"=> {result.ToDebugString()}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (verbose)
                    {
                        Console.WriteLine(ex.StackTrace);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start REPL");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Validates bootstrap JSON files.
    /// </summary>
    private static async Task ValidateBootstrap(string? bootstrapDir)
    {
        Console.WriteLine("Validating bootstrap files...");
        
        bootstrapDir = GetBootstrapDirectory(bootstrapDir);
        Console.WriteLine($"Bootstrap directory: {bootstrapDir}");
        
        try
        {
            var provider = new JsonBootstrapProvider(bootstrapDir);
            var result = await provider.ValidateAsync();
            
            if (result.IsValid)
            {
                Console.WriteLine("✓ Bootstrap files are valid");
            }
            else
            {
                Console.WriteLine("✗ Bootstrap validation failed");
                
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  Error: {error}");
                }
            }
            
            if (result.Warnings.Any())
            {
                Console.WriteLine("\nWarnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  Warning: {warning}");
                }
            }
            
            if (result.Details.Any())
            {
                Console.WriteLine("\nDetails:");
                foreach (var (key, value) in result.Details)
                {
                    Console.WriteLine($"  {key}: {value}");
                }
            }
            
            Environment.Exit(result.IsValid ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to validate: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Creates and bootstraps the VM.
    /// </summary>
    private static async Task<IVirtualMachine> CreateAndBootstrapVM(
        string? bootstrapDir,
        BootstrapMode mode,
        bool trace,
        ILogger logger)
    {
        bootstrapDir = GetBootstrapDirectory(bootstrapDir);
        logger.LogInformation("Bootstrap directory: {Directory}", bootstrapDir);
        
        // Create VM configuration
        var config = new VMConfiguration
        {
            EnableTracing = trace,
            EnableMethodCache = true,
            BootstrapMode = mode,
            MaxStackDepth = 1000,
            InitialHeapSize = 10 * 1024 * 1024 // 10MB
        };
        
        // Create VM
        var vm = new VirtualMachine(config, logger);
        
        // Create bootstrap provider
        var bootstrapProvider = new JsonBootstrapProvider(bootstrapDir, logger);
        
        // Execute bootstrap
        logger.LogInformation("Bootstrapping VM in {Mode} mode...", mode);
        await vm.BootstrapAsync(bootstrapProvider);
        
        logger.LogInformation("VM bootstrap completed successfully");
        return vm;
    }

    /// <summary>
    /// Gets the bootstrap directory path.
    /// </summary>
    private static string GetBootstrapDirectory(string? bootstrapDir)
    {
        if (!string.IsNullOrEmpty(bootstrapDir))
            return bootstrapDir;
            
        // Default to Bootstrap subfolder relative to executable
        var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(exeDir))
            exeDir = Environment.CurrentDirectory;
            
        var defaultDir = Path.Combine(exeDir, "Bootstrap");
        
        if (!Directory.Exists(defaultDir))
        {
            // Try development paths
            var devPaths = new[]
            {
                Path.Combine(exeDir, "..", "..", "..", "Bootstrap", "Smalltalk80.Bootstrap.Data"),
                Path.Combine(exeDir, "..", "..", "..", "..", "Bootstrap", "Smalltalk80.Bootstrap.Data"),
                Path.Combine(Environment.CurrentDirectory, "Bootstrap", "Smalltalk80.Bootstrap.Data"),
                Path.Combine(Environment.CurrentDirectory, "Bootstrap")
            };
            
            foreach (var path in devPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        
        return defaultDir;
    }

    /// <summary>
    /// Creates a logger instance.
    /// </summary>
    private static ILogger CreateLogger(bool verbose)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
        });
        
        return new LoggerAdapter(loggerFactory.CreateLogger<Program>());
    }

    /// <summary>
    /// Shows REPL help.
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine(@"
REPL Commands:
  exit, quit    - Exit the REPL
  help          - Show this help

Smalltalk Expressions:
  3 + 4                           - Arithmetic
  'Hello' , ' World'              - String concatenation  
  5 factorial                     - Message send
  [1 + 2] value                   - Block evaluation
  1 to: 10 do: [:i | i print]    - Iteration
  
Special Variables:
  nil, true, false                - Constants
  Smalltalk                       - System dictionary
");
    }
}

/// <summary>
/// Adapter to bridge Microsoft.Extensions.Logging to our ILogger interface.
/// </summary>
internal class LoggerAdapter : Bootstrap.ILogger<Program>
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public LoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }

    public void LogInformation(string message, params object[] args)
        => _logger.LogInformation(message, args);

    public void LogWarning(string message, params object[] args)
        => _logger.LogWarning(message, args);

    public void LogError(string message, params object[] args)
        => _logger.LogError(message, args);
}

// Placeholder VM implementation extensions
public static class VirtualMachineExtensions
{
    public static async Task<ISmalltalkObject?> EvaluateAsync(this IVirtualMachine vm, string source)
    {
        // This would compile and execute the source string
        await Task.CompletedTask;
        throw new NotImplementedException("Evaluate not yet implemented");
    }
}