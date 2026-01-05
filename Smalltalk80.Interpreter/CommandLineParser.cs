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
    public bool DisableGC { get; init; }

    /// <summary>
    /// Path to image file to restore from. If set, cold start is skipped.
    /// </summary>
    public string? RestoreImageFile { get; init; }

    /// <summary>
    /// Whether the coldstart file was explicitly specified on command line.
    /// Used to provide better error messages.
    /// </summary>
    public bool ColdStartExplicitlySpecified { get; init; }

    /// <summary>
    /// Whether the restore file was explicitly specified on command line.
    /// Used to provide better error messages.
    /// </summary>
    public bool RestoreExplicitlySpecified { get; init; }
}

/// <summary>
/// Parses command line arguments for the Smalltalk-80 interpreter.
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Application name used for well-known folder paths.
    /// </summary>
    private const string AppName = "Smalltalk-80";

    /// <summary>
    /// Default coldstart bundle filename.
    /// </summary>
    private const string DefaultColdstartFilename = "coldstart.json";

    /// <summary>
    /// Default image filename.
    /// </summary>
    private const string DefaultImageFilename = "snapshot.im";

    /// <summary>
    /// Parses command line arguments and returns configuration.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Parsed configuration or null if help was requested</returns>
    public static CommandLineConfig? Parse(string[] args)
    {
        string? coldStartFile = null;
        bool coldStartExplicit = false;
        string? dumpClassName = null;
        bool verboseDebug = false;
        string? testFilter = null;
        string? reportOutput = null;
        string? executeExpression = null;
        string? diagnosticCategories = null;
        bool disableGC = false;
        string? restoreImageFile = null;
        bool restoreExplicit = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--coldstart")
            {
                // Check if next arg exists and is not another option
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    coldStartFile = args[i + 1];
                    coldStartExplicit = true;
                    i++;
                }
                // else: --coldstart without path, will use well-known search
            }
            else if (args[i] == "--restore")
            {
                // --restore with optional filename
                // Check if next arg exists and is not another option
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    restoreImageFile = args[i + 1];
                    restoreExplicit = true;
                    i++;
                }
                else
                {
                    // --restore without explicit path - will search well-known locations
                    restoreImageFile = ""; // Marker to indicate restore mode without explicit path
                    restoreExplicit = false;
                }
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
            else if (args[i] == "--no-gc")
            {
                disableGC = true;
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                PrintUsage();
                return null;
            }
        }

        // Resolve coldstart file path
        if (coldStartExplicit)
        {
            // Explicit path provided - verify it exists
            if (!File.Exists(coldStartFile))
            {
                Console.Error.WriteLine($"Error: Specified coldstart file not found: {coldStartFile}");
                Environment.Exit(1);
                return null;
            }
        }
        else
        {
            // No explicit path - search well-known locations
            coldStartFile = ResolveColdstartFile();
            if (coldStartFile == null)
            {
                Console.Error.WriteLine("Error: Coldstart file not found.");
                Console.Error.WriteLine("Searched locations:");
                Console.Error.WriteLine($"  1. {GetCommonAppDataPath()}\\{DefaultColdstartFilename}");
                Console.Error.WriteLine($"  2. {Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", DefaultColdstartFilename)}");
                Console.Error.WriteLine("\nUse --coldstart <path> to specify a coldstart file.");
                Environment.Exit(1);
                return null;
            }
        }

        // Resolve restore image file if --restore was used
        if (restoreImageFile != null)
        {
            if (restoreExplicit)
            {
                // Explicit path provided - verify it exists
                if (!File.Exists(restoreImageFile))
                {
                    Console.Error.WriteLine($"Error: Specified image file not found: {restoreImageFile}");
                    Environment.Exit(1);
                    return null;
                }
            }
            else
            {
                // No explicit path (empty string marker) - search well-known locations
                restoreImageFile = ResolveImageFile();
                if (restoreImageFile == null)
                {
                    Console.Error.WriteLine("Error: Image file not found.");
                    Console.Error.WriteLine("Searched locations:");
                    Console.Error.WriteLine($"  1. {GetAppDataPath()}\\{DefaultImageFilename}");
                    Console.Error.WriteLine($"  2. {Path.Combine(Environment.CurrentDirectory, DefaultImageFilename)}");
                    Console.Error.WriteLine("\nUse --restore <path> to specify an image file.");
                    Environment.Exit(1);
                    return null;
                }
            }
        }

        return new CommandLineConfig
        {
            ColdStartFile = coldStartFile,
            DumpClassName = dumpClassName,
            VerboseDebug = verboseDebug,
            TestFilter = testFilter,
            ReportOutput = reportOutput,
            ExecuteExpression = executeExpression,
            DiagnosticCategories = diagnosticCategories,
            DisableGC = disableGC,
            RestoreImageFile = restoreImageFile,
            ColdStartExplicitlySpecified = coldStartExplicit,
            RestoreExplicitlySpecified = restoreExplicit
        };
    }

    /// <summary>
    /// Resolves coldstart file from well-known locations.
    /// Search order:
    /// 1. %COMMONAPPDATA%\Smalltalk-80\coldstart.json (shared system data)
    /// 2. Data\coldstart.json relative to executable (development/portable)
    /// </summary>
    /// <returns>Path to coldstart file, or null if not found</returns>
    private static string? ResolveColdstartFile()
    {
        // First: Check common application data (ProgramData on Windows)
        string commonAppDataPath = GetCommonAppDataPath();
        if (commonAppDataPath != null)
        {
            string systemPath = Path.Combine(commonAppDataPath, DefaultColdstartFilename);
            if (File.Exists(systemPath))
            {
                Console.WriteLine($"Found coldstart at: {systemPath}");
                return systemPath;
            }
        }

        // Second: Check Data subfolder relative to executable (development fallback)
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string localPath = Path.Combine(exeDir, "Data", DefaultColdstartFilename);
        if (File.Exists(localPath))
        {
            Console.WriteLine($"Found coldstart at: {localPath}");
            return localPath;
        }

        return null;
    }

    /// <summary>
    /// Resolves image file from well-known locations.
    /// Search order:
    /// 1. %APPDATA%\Smalltalk-80\snapshot.im (user data)
    /// 2. snapshot.im in current working directory (portable)
    /// </summary>
    /// <returns>Path to image file, or null if not found</returns>
    private static string? ResolveImageFile()
    {
        // First: Check user application data (AppData\Roaming on Windows)
        string appDataPath = GetAppDataPath();
        if (appDataPath != null)
        {
            string userPath = Path.Combine(appDataPath, DefaultImageFilename);
            if (File.Exists(userPath))
            {
                Console.WriteLine($"Found image at: {userPath}");
                return userPath;
            }
        }

        // Second: Check current working directory (portable/development)
        string localPath = Path.Combine(Environment.CurrentDirectory, DefaultImageFilename);
        if (File.Exists(localPath))
        {
            Console.WriteLine($"Found image at: {localPath}");
            return localPath;
        }

        return null;
    }

    /// <summary>
    /// Gets the application-specific path under common application data.
    /// On Windows: C:\ProgramData\Smalltalk-80
    /// </summary>
    private static string GetCommonAppDataPath()
    {
        string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(commonAppData, AppName);
    }

    /// <summary>
    /// Gets the application-specific path under user application data.
    /// On Windows: C:\Users\{user}\AppData\Roaming\Smalltalk-80
    /// </summary>
    private static string GetAppDataPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppName);
    }

    /// <summary>
    /// Prints usage information to console.
    /// </summary>
    public static void PrintUsage()
    {
        Console.Error.WriteLine("\nUsage: Smalltalk80.Interpreter [options]");
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --coldstart [file]     Specify the cold start bundle file to load");
        Console.Error.WriteLine("                         With explicit path: fails if file not found");
        Console.Error.WriteLine("                         Without path: searches well-known locations:");
        Console.Error.WriteLine($"                           1. %COMMONAPPDATA%\\{AppName}\\{DefaultColdstartFilename}");
        Console.Error.WriteLine($"                           2. Data\\{DefaultColdstartFilename} (relative to exe)");
        Console.Error.WriteLine("  --restore [file]       Restore from saved image instead of cold start");
        Console.Error.WriteLine("                         With explicit path: fails if file not found");
        Console.Error.WriteLine("                         Without path: searches well-known locations:");
        Console.Error.WriteLine($"                           1. %APPDATA%\\{AppName}\\{DefaultImageFilename}");
        Console.Error.WriteLine($"                           2. {DefaultImageFilename} (current directory)");
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
        Console.Error.WriteLine("  --no-gc                Disable automatic garbage collection");
        Console.Error.WriteLine("                         (useful for benchmarking to avoid GC interference)");
        Console.Error.WriteLine("  --help, -h             Show this help message");
        Console.Error.WriteLine();
        Console.Error.WriteLine("File Locations:");
        Console.Error.WriteLine($"  Coldstart:   %COMMONAPPDATA%\\{AppName}\\ (system-wide)");
        Console.Error.WriteLine($"  Images:      %APPDATA%\\{AppName}\\ (per-user)");
        Console.Error.WriteLine($"  Source:      %APPDATA%\\{AppName}\\ (per-user, .sources/.changes)");
    }
}
