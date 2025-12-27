namespace Smalltalk80.Interpreter;

/// <summary>
/// Constants for the Smalltalk-80 interpreter configuration.
/// Centralizes magic numbers to improve maintainability and documentation.
/// </summary>
public static class InterpreterConstants
{
    /// <summary>
    /// Default width for the Smalltalk display window in pixels.
    /// Matches the Blue Book default display dimensions.
    /// </summary>
    public const int DefaultDisplayWidth = 800;

    /// <summary>
    /// Default height for the Smalltalk display window in pixels.
    /// Matches the Blue Book default display dimensions.
    /// </summary>
    public const int DefaultDisplayHeight = 600;

    /// <summary>
    /// Default timeout in milliseconds for init expression execution.
    /// Allows complex initializations while preventing infinite loops.
    /// </summary>
    public const int DefaultInitExpressionTimeoutMs = 5000;

    /// <summary>
    /// Time to wait in milliseconds for the window to be fully shown before
    /// starting Smalltalk execution. This gives the UI thread time to complete
    /// window creation and initial paint.
    /// </summary>
    public const int WindowReadyWaitMs = 1000;

    /// <summary>
    /// Maximum inheritance chain depth for method lookup.
    /// Prevents infinite loops in circular class hierarchies (which shouldn't exist).
    /// </summary>
    public const int MaxInheritanceDepth = 20;

    /// <summary>
    /// Maximum context chain depth for debugging/diagnostics.
    /// Prevents runaway output when dumping deep call stacks.
    /// </summary>
    public const int MaxContextChainDepth = 100;

    /// <summary>
    /// Minimum process priority (lowest scheduling priority).
    /// </summary>
    public const int MinProcessPriority = 1;

    /// <summary>
    /// Maximum process priority (highest scheduling priority).
    /// Blue Book defines 8 priority levels (1-8).
    /// </summary>
    public const int MaxProcessPriority = 8;

    /// <summary>
    /// Maximum instructions to execute before assuming infinite loop.
    /// Used by ExecutionTracer for debugging runaway expressions.
    /// </summary>
    public const long MaxInstructionsPerExpression = 5_000_000;
}
