namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Holds debug flags that control verbose diagnostic output during cold start.
/// These options are intended for debugging loader phases, not for runtime execution.
/// </summary>
public class ColdStartDebugOptions
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
    /// Enable bytecode execution trace output
    /// Command line: --debug-bytecode
    /// </summary>
    public bool DebugBytecode { get; set; }

    /// <summary>
    /// Enable message send and method lookup debug output
    /// Command line: --debug-messages
    /// </summary>
    public bool DebugMessages { get; set; }

    /// <summary>
    /// Enable diagnostic reports (ClassDescription inspection, BuildFieldMap test)
    /// Command line: --debug-diagnostics
    /// </summary>
    public bool DebugDiagnostics { get; set; }

    /// <summary>
    /// Enable all debug output (equivalent to specifying all --debug-* flags)
    /// Command line: --debug-all
    /// </summary>
    public bool DebugAll { get; set; }

    /// <summary>
    /// Returns a default ColdStartDebugOptions instance with all flags disabled.
    /// </summary>
    public static ColdStartDebugOptions None => new ColdStartDebugOptions();

    /// <summary>
    /// Returns a ColdStartDebugOptions instance with all flags enabled.
    /// </summary>
    public static ColdStartDebugOptions All => new ColdStartDebugOptions
    {
        DebugAll = true,
        DebugPatches = true,
        DebugAssociations = true,
        DebugClasses = true,
        DebugMethods = true,
        DebugGlobals = true,
        DebugBytecode = true,
        DebugMessages = true,
        DebugDiagnostics = true
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

    /// <summary>
    /// Checks if bytecode execution trace output should be enabled.
    /// </summary>
    public bool ShouldDebugBytecode => DebugAll || DebugBytecode;

    /// <summary>
    /// Checks if message send and method lookup debug output should be enabled.
    /// </summary>
    public bool ShouldDebugMessages => DebugAll || DebugMessages;

    /// <summary>
    /// Checks if diagnostic reports should be enabled.
    /// </summary>
    public bool ShouldDebugDiagnostics => DebugAll || DebugDiagnostics;
}
