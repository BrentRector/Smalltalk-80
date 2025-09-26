namespace Smalltalk80.Interpreter.Config;

/// <summary>
/// Runtime options for controlling various aspects of the Smalltalk-80 interpreter.
/// These options can be set via command-line flags and are passed to components during initialization.
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
    /// Gets or sets whether verbose logging is enabled.
    /// Controlled by the -v or --verbose command-line flag.
    /// </summary>
    public bool VerboseLogging { get; set; }
    
    /// <summary>
    /// Creates a new instance with default options.
    /// </summary>
    public static RuntimeOptions Default => new()
    {
        EnableColdStartLogging = false,
        EnableGCLogging = false,
        VerboseLogging = false
    };
    
    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new RuntimeOptions instance with the same settings.</returns>
    public RuntimeOptions Clone() => new()
    {
        EnableColdStartLogging = EnableColdStartLogging,
        EnableGCLogging = EnableGCLogging,
        VerboseLogging = VerboseLogging
    };
}