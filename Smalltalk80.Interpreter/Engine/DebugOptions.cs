namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Holds debug flags that control verbose diagnostic output during cold start.
/// These options are intended for debugging loader phases, not for runtime execution.
/// </summary>
public class DebugOptions
{
    /// <summary>
    /// Enable patch application debug output (SetInstanceVariable, ReplaceLiteral, etc.)
    /// Command line: --debug-patches
    /// </summary>
    public bool DebugPatches { get; set; }

    /// <summary>
    /// Enable association scanning and validation debug output
    /// Command line: --debug-associations
    /// </summary>
    public bool DebugAssociations { get; set; }

    /// <summary>
    /// Enable class resolution debug output
    /// Command line: --debug-classes
    /// </summary>
    public bool DebugClasses { get; set; }

    /// <summary>
    /// Enable method installation debug output
    /// Command line: --debug-methods
    /// </summary>
    public bool DebugMethods { get; set; }

    /// <summary>
    /// Enable global variable installation debug output
    /// Command line: --debug-globals
    /// </summary>
    public bool DebugGlobals { get; set; }

    /// <summary>
    /// Enable all debug output (equivalent to specifying all --debug-* flags)
    /// Command line: --debug-all
    /// </summary>
    public bool DebugAll { get; set; }

    /// <summary>
    /// Returns a default DebugOptions instance with all flags disabled.
    /// </summary>
    public static DebugOptions None => new DebugOptions();

    /// <summary>
    /// Returns a DebugOptions instance with all flags enabled.
    /// </summary>
    public static DebugOptions All => new DebugOptions
    {
        DebugAll = true,
        DebugPatches = true,
        DebugAssociations = true,
        DebugClasses = true,
        DebugMethods = true,
        DebugGlobals = true
    };

    /// <summary>
    /// Checks if patches debug output should be enabled.
    /// </summary>
    public bool ShouldDebugPatches => DebugAll || DebugPatches;

    /// <summary>
    /// Checks if associations debug output should be enabled.
    /// </summary>
    public bool ShouldDebugAssociations => DebugAll || DebugAssociations;

    /// <summary>
    /// Checks if classes debug output should be enabled.
    /// </summary>
    public bool ShouldDebugClasses => DebugAll || DebugClasses;

    /// <summary>
    /// Checks if methods debug output should be enabled.
    /// </summary>
    public bool ShouldDebugMethods => DebugAll || DebugMethods;

    /// <summary>
    /// Checks if globals debug output should be enabled.
    /// </summary>
    public bool ShouldDebugGlobals => DebugAll || DebugGlobals;
}
