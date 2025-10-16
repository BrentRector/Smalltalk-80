using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.InitExecution;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Entry point for the Smalltalk-80 interpreter.
/// Loads a cold start bundle and initializes the virtual machine.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Interpreter");
        Console.WriteLine("========================\n");

        try
        {
            // Parse command line arguments
            var (coldStartFile, dumpClassName) = ParseCommandLine(args);

            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Cold start file not found: {coldStartFile}");
                Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
                Console.Error.WriteLine("Options:");
                Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
                Console.Error.WriteLine("                         (default: coldstart.json in executable directory)");
                Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
                Console.Error.WriteLine("                         after cold start completes");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Loading cold start bundle: {coldStartFile}");

            // Load the cold start bundle from JSON
            var bundle = ColdStartBundle.LoadFromFile(coldStartFile);

            Console.WriteLine($"Bundle loaded: {bundle.Metadata.Version}");
            Console.WriteLine($"  Symbols: {bundle.Symbols.Length}");
            Console.WriteLine($"  Classes: {bundle.Classes.Length}");
            Console.WriteLine($"  Objects: {bundle.Objects.Length}");
            Console.WriteLine($"  Methods: {bundle.Methods.Length}");
            Console.WriteLine();

            // Create the heap manager
            Console.WriteLine("Initializing heap manager...");
            var heapManager = new SmalltalkHeapManager();

            // Create and run the loader
            Console.WriteLine("Starting cold start loader...");
            var loader = new Loader(bundle, heapManager);
            loader.Run();

            // Execute init expressions
            if (bundle.InitExpressions.Length > 0)
            {
                Console.WriteLine("\n=== Init Expression Execution ===");
                Console.WriteLine($"  Found {bundle.InitExpressions.Length} init expression(s) in bundle");
                
                if (loader.LayoutResolver == null)
                {
                    Console.WriteLine("  ⚠️  Execution skipped - LayoutResolver not available");
                    return;
                }
                
                var vm = new VirtualMachine(heapManager, loader.LayoutResolver, bundle.Symbols);
                
                // Initialize selector caches after cold start completes
                vm.InitializeSelectorCaches();
                
                var executor = new InitExpressionExecutor(
                    heapManager,
                    vm,
                    loader.LayoutResolver,
                    loader.LocalIdToObjectId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    InitPolicy.AbortOnFirstError,
                    defaultTimeoutMs: 5000,
                    symbols: bundle.Symbols,
                    strings: bundle.Strings,
                    allocator: loader.Allocator
                );
                
                var report = executor.ExecuteFromBundle(bundle, loader.Allocator);
                
                Console.WriteLine($"  Executed: {report.Succeeded}/{bundle.InitExpressions.Length}");
                if (report.Failed > 0)
                {
                    Console.WriteLine($"  ⚠️  Failed: {report.Failed}");
                    foreach (var result in report.Results.Where(r => r.Status != InitStatus.Success))
                    {
                        Console.WriteLine($"    - {result.InitLocalId}: {result.Status} - {result.Diagnostic?.Message}");
                    }
                }
            }

            Console.WriteLine("\n✓ All systems ready!");
            Console.WriteLine("Virtual machine initialized successfully.");

            // TODO: Implement --dump-class after ClassPrettyPrinter is migrated to new heap manager
            if (dumpClassName != null)
            {
                Console.WriteLine($"\n⚠️  --dump-class not yet implemented (ClassPrettyPrinter not migrated to new heap manager)");
            }

            // TODO: Start REPL or execute user code
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFatal error: {ex.Message}");
            Console.Error.WriteLine($"\nStack trace:\n{ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parses command line arguments and returns the cold start file path and optional class name to dump.
    /// Supports: --coldstart path, --dump-class ClassName
    /// Default: coldstart.json in the same directory as the executable
    /// </summary>
    private static (string coldStartFile, string? dumpClassName) ParseCommandLine(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--coldstart" && i + 1 < args.Length)
            {
                coldStartFile = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i] == "--dump-class" && i + 1 < args.Length)
            {
                dumpClassName = args[i + 1];
                i++; // Skip the next argument
            }
        }

        // Default: coldstart.json in the executable directory
        if (coldStartFile == null)
        {
            string exeDir = AppContext.BaseDirectory;
            coldStartFile = Path.Combine(exeDir, "coldstart.json");
        }

        return (coldStartFile, dumpClassName);
    }
}
