namespace Smalltalk80.Interpreter;

/// <summary>
/// Configuration options parsed from command line arguments.
/// </summary>
public record CommandLineConfig
{
    public required string ColdStartFile { get; init; }
    public string? DumpClassName { get; init; }
    public bool VerboseDebug { get; init; }
    public string? TestFilter { get; init; }
    public string? ReportOutput { get; init; }
    public string? ExecuteExpression { get; init; }
    public string? DiagnosticCategories { get; init; }
}

/// <summary>
/// Parses command line arguments for the Smalltalk-80 interpreter.
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parses command line arguments and returns configuration.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Parsed configuration or null if help was requested</returns>
    public static CommandLineConfig? Parse(string[] args)
    {
        string? coldStartFile = null;
        string? dumpClassName = null;
        bool verboseDebug = false;
        string? testFilter = null;
        string? reportOutput = null;
        string? executeExpression = null;
        string? diagnosticCategories = null;

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
                i++;
            }
            else if (args[i] == "--test-filter" && i + 1 < args.Length)
            {
                testFilter = args[i + 1];
                i++;
            }
            else if (args[i] == "--execute" && i + 1 < args.Length)
            {
                executeExpression = args[i + 1];
                i++;
            }
            else if (args[i] == "--verbose" || args[i] == "-v")
            {
                verboseDebug = true;
            }
            else if (args[i].StartsWith("--coldstart-report="))
            {
                reportOutput = args[i].Substring("--coldstart-report=".Length);
            }
            else if (args[i] == "--diagnostics" && i + 1 < args.Length)
            {
                diagnosticCategories = args[i + 1];
                i++; // Skip the next argument
            }
            else if (args[i].StartsWith("--diagnostics="))
            {
                diagnosticCategories = args[i].Substring("--diagnostics=".Length);
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                PrintUsage();
                return null;
            }
        }

        // Default: coldstart.json in the Data subfolder relative to the executable
        if (coldStartFile == null)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            coldStartFile = Path.Combine(exeDir, "Data", "coldstart.json");
        }

        return new CommandLineConfig
        {
            ColdStartFile = coldStartFile,
            DumpClassName = dumpClassName,
            VerboseDebug = verboseDebug,
            TestFilter = testFilter,
            ReportOutput = reportOutput,
            ExecuteExpression = executeExpression,
            DiagnosticCategories = diagnosticCategories
        };
    }

    /// <summary>
    /// Prints usage information to console.
    /// </summary>
    public static void PrintUsage()
    {
        Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --coldstart <file>     Specify the cold start bundle file to load");
        Console.Error.WriteLine("                         (default: Data/coldstart.json in executable directory)");
        Console.Error.WriteLine("  --dump-class <name>    Dump comprehensive information about a class");
        Console.Error.WriteLine("                         after cold start completes");
        Console.Error.WriteLine("  --test-filter <pattern> Run only init expressions matching pattern");
        Console.Error.WriteLine("                         (e.g., 'BlockValidator' or 'Test')");
        Console.Error.WriteLine("  --execute <expression>  Execute specific Smalltalk expression after cold start");
        Console.Error.WriteLine("                         (e.g., 'BlockContextTest testAllCombinations')");
        Console.Error.WriteLine("  --verbose, -v          Enable verbose debugging (logs all method calls)");
        Console.Error.WriteLine("  --coldstart-report=<path>  Write coldstart metrics to JSON file");
        Console.Error.WriteLine("  --diagnostics <cats>   Enable diagnostic categories (comma-separated)");
        Console.Error.WriteLine("                         Categories: detail,method,gc,bytecode,all");
        Console.Error.WriteLine("                         detail: verbose cold start (symbols, classes, methods)");
        Console.Error.WriteLine("                         method: runtime method entry/exit tracing");
        Console.Error.WriteLine("                         gc: garbage collection events");
        Console.Error.WriteLine("                         bytecode: low-level bytecode execution");
        Console.Error.WriteLine("  --help, -h             Show this help message");
    }
}
