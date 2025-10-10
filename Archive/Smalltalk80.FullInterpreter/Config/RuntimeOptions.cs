namespace Smalltalk80.Interpreter.Config;

/// <summary>
/// Runtime options for controlling various aspects of the Smalltalk-80 interpreter.
/// These options can be set via command-line flags and environment variables.
/// </summary>
public sealed class RuntimeOptions
{
    /// <summary>
    /// Gets or sets whether to enable [COLD START] logging messages.
    /// Controlled by the --cold-start-log command-line flag.
    /// </summary>
    public bool EnableColdStartLogging { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable [GC] garbage collection logging messages.
    /// Controlled by the --gc-log command-line flag.
    /// </summary>
    public bool EnableGCLogging { get; set; }
    
    /// <summary>
    /// Gets or sets whether to validate GC reachability from SOA after cold start.
    /// Controlled by the --validate-gc command-line flag.
    /// </summary>
    public bool ValidateGC { get; set; }
    
    /// <summary>
    /// Gets or sets whether to enable [SEND] message send logging.
    /// Controlled by the --log-sends command-line flag.
    /// Logs all message sends with receiver class, selector, and arguments.
    /// </summary>
    public bool EnableSendLogging { get; set; }
    
    /// <summary>
    /// Gets or sets whether verbose logging is enabled.
    /// Controlled by the -v or --verbose command-line flag.
    /// </summary>
    public bool VerboseLogging { get; set; }
    
    /// <summary>
    /// Gets or sets the GC trace level.
    /// Controlled by SMALLTALK_GC_TRACE environment variable.
    /// </summary>
    public GCTraceLevel GCTraceLevel { get; set; } = GCTraceLevel.Off;
    
    /// <summary>
    /// Gets or sets the path to the system image file.
    /// </summary>
    public string? ImagePath { get; set; }
    
    /// <summary>
    /// Gets or sets the path for saving snapshots.
    /// </summary>
    public string? SnapshotPath { get; set; }
    
    /// <summary>
    /// Gets or sets the path to the cold start file.
    /// </summary>
    public string? ColdStartPath { get; set; }
    
    /// <summary>
    /// Gets or sets the class name to print or inspect.
    /// </summary>
    public string? ClassToPrint { get; set; }

    /// <summary>
    /// Gets or sets the class name to inspect comprehensively.
    /// </summary>
    public string? ClassToInspect { get; set; }

    /// <summary>
    /// Gets or sets whether to list all cold start classes.
    /// </summary>
    public bool ListColdStartClasses { get; set; }
    
    /// <summary>
    /// Gets or sets whether to list all classes.
    /// </summary>
    public bool ListClasses { get; set; }
    
    /// <summary>
    /// Gets or sets whether to test critical methods.
    /// </summary>
    public bool TestCriticalMethods { get; set; }
    
    /// <summary>
    /// Gets or sets whether to test class creation.
    /// </summary>
    public bool TestClassCreation { get; set; }
    
    /// <summary>
    /// Gets or sets whether to test OOP bug.
    /// </summary>
    public bool TestOOPBug { get; set; }

    /// <summary>
    /// Gets or sets whether to test precompiled method execution.
    /// </summary>
    public bool TestPrecompiled { get; set; }

    /// <summary>
    /// Gets or sets whether to validate bytecode execution through method contexts.
    /// </summary>
    public bool ValidateBytecodeExecution { get; set; }
    
    /// <summary>
    /// Creates a new instance with default options.
    /// </summary>
    public static RuntimeOptions Default => new()
    {
        EnableColdStartLogging = false,
        EnableGCLogging = false,
        ValidateGC = false,
        EnableSendLogging = false,
        VerboseLogging = false,
        GCTraceLevel = GCTraceLevel.Off
    };
    
    /// <summary>
    /// Creates runtime options from environment variables.
    /// </summary>
    public static RuntimeOptions FromEnvironment()
    {
        var options = Default;
        
        // Check SMALLTALK_GC_TRACE
        var gcTrace = Environment.GetEnvironmentVariable("SMALLTALK_GC_TRACE");
        if (!string.IsNullOrEmpty(gcTrace))
        {
            options.GCTraceLevel = gcTrace.ToLowerInvariant() switch
            {
                "off" => GCTraceLevel.Off,
                "basic" => GCTraceLevel.Basic,
                "deep" => GCTraceLevel.Deep,
                _ => GCTraceLevel.Off
            };
            options.EnableGCLogging = options.GCTraceLevel != GCTraceLevel.Off;
        }
        
        // Check LOGGING__LOGLEVEL__DEFAULT
        var logLevel = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT");
        if (!string.IsNullOrEmpty(logLevel))
        {
            options.VerboseLogging = logLevel.ToLowerInvariant() switch
            {
                "debug" => true,
                "trace" => true,
                "verbose" => true,
                _ => false
            };
        }
        
        return options;
    }
    
    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new RuntimeOptions instance with the same settings.</returns>
    public RuntimeOptions Clone() => new()
    {
        EnableColdStartLogging = EnableColdStartLogging,
        EnableGCLogging = EnableGCLogging,
        ValidateGC = ValidateGC,
        EnableSendLogging = EnableSendLogging,
        VerboseLogging = VerboseLogging,
        GCTraceLevel = GCTraceLevel,
        ImagePath = ImagePath,
        SnapshotPath = SnapshotPath,
        ColdStartPath = ColdStartPath,
        ClassToPrint = ClassToPrint,
        ClassToInspect = ClassToInspect,
        ListColdStartClasses = ListColdStartClasses,
        ListClasses = ListClasses,
        TestCriticalMethods = TestCriticalMethods,
        TestClassCreation = TestClassCreation,
        TestOOPBug = TestOOPBug,
        TestPrecompiled = TestPrecompiled,
        ValidateBytecodeExecution = ValidateBytecodeExecution
    };
}

/// <summary>
/// Garbage collection trace levels.
/// </summary>
public enum GCTraceLevel
{
    /// <summary>
    /// No GC tracing.
    /// </summary>
    Off,
    
    /// <summary>
    /// Basic GC tracing (start/end, object counts).
    /// </summary>
    Basic,
    
    /// <summary>
    /// Deep GC tracing (includes object traversal details).
    /// </summary>
    Deep
}