namespace Smalltalk80.Compiler;

/// <summary>
/// Compiler options and feature flags for controlling bytecode generation.
/// These options allow fine-grained control over optimizations and diagnostic output.
/// </summary>
public sealed record CompilerOptions
{
    /// <summary>
    /// Enable tail recursion optimization (default: false for safety).
    /// When enabled, converts recursive tail calls to jumps, reusing the MethodContext.
    /// </summary>
    /// <remarks>
    /// Tail recursion optimization transforms:
    /// <code>
    /// whileTrue: aBlock
    ///     self value ifTrue: [
    ///         aBlock value.
    ///         self whileTrue: aBlock  ← Recursive call in tail position
    ///     ]
    /// </code>
    /// Into a loop using a backward jump instead of a message send.
    /// This eliminates stack growth and enables infinite recursion patterns.
    ///
    /// Disabled by default because:
    /// - Requires careful analysis to ensure correctness
    /// - Changes execution semantics (no new context created)
    /// - May interfere with debugging (no stack frame for recursive calls)
    /// </remarks>
    public bool OptimizeTailRecursion { get; init; } = false;

    /// <summary>
    /// Emit diagnostic comments for optimized calls (default: false).
    /// When enabled, prints messages to console showing which tail calls were optimized.
    /// Useful for debugging and verifying optimization behavior.
    /// </summary>
    /// <remarks>
    /// Example output:
    /// <code>
    /// [TailOpt] BlockContext>>whileTrue: 1 tail call(s) detected
    /// [TailOpt]   - Line 3: self whileTrue: aBlock (safe to optimize)
    /// </code>
    /// </remarks>
    public bool EmitOptimizationComments { get; init; } = false;

    /// <summary>
    /// Gets the default compiler options with all optimizations disabled.
    /// Safe for production use - prioritizes correctness over performance.
    /// </summary>
    public static CompilerOptions Default { get; } = new();

    /// <summary>
    /// Gets compiler options with all optimizations enabled.
    /// Use with caution - requires comprehensive testing.
    /// </summary>
    public static CompilerOptions Optimized { get; } = new()
    {
        OptimizeTailRecursion = true,
        EmitOptimizationComments = false
    };

    /// <summary>
    /// Gets compiler options with optimizations enabled and diagnostics on.
    /// Useful for development and debugging optimization behavior.
    /// </summary>
    public static CompilerOptions Debug { get; } = new()
    {
        OptimizeTailRecursion = true,
        EmitOptimizationComments = true
    };
}
