namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Debug output configuration for VirtualMachine.
/// All options default to false for clean production output.
/// </summary>
public class RuntimeDebugOptions
{
    /// <summary>
    /// Enable bytecode execution trace output (shows each bytecode as it executes)
    /// </summary>
    public bool ShouldDebugBytecode { get; set; } = false;

    /// <summary>
    /// Enable message send trace output (shows method lookups and sends)
    /// </summary>
    public bool ShouldDebugMessages { get; set; } = false;

    /// <summary>
    /// Enable method lookup trace output (shows class hierarchy traversal)
    /// </summary>
    public bool ShouldDebugMethodLookup { get; set; } = false;

    /// <summary>
    /// Default instance with all debugging disabled
    /// </summary>
    public static readonly RuntimeDebugOptions Default = new();
}
