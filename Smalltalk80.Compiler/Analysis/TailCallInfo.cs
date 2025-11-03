using System.Collections.Generic;
using Antlr4.Runtime.Tree;

namespace Smalltalk80.Compiler.Analysis;

/// <summary>
/// Information about a detected tail-recursive call.
/// Represents a method that calls itself recursively in tail position,
/// enabling bytecode optimization (jump instead of send).
/// </summary>
/// <remarks>
/// A tail call is a recursive call where:
/// - The receiver is 'self' (the current object)
/// - The selector matches the current method's selector
/// - The call is in tail position (last statement in execution path)
/// - The call is NOT inside a block literal [...]
/// - The result is returned directly without further computation
/// </remarks>
public sealed record TailCallInfo
{
    /// <summary>
    /// Parse tree node of the message send expression.
    /// This allows the bytecode generator to locate the call site for optimization.
    /// </summary>
    public required IParseTree MessageSendNode { get; init; }

    /// <summary>
    /// Selector of the recursive call.
    /// Must match the containing method's selector for tail recursion optimization.
    /// </summary>
    public required string Selector { get; init; }

    /// <summary>
    /// Number of arguments in the recursive call.
    /// Must match the containing method's argument count for safe optimization.
    /// </summary>
    public required int ArgumentCount { get; init; }

    /// <summary>
    /// Argument expression nodes (for bytecode generation).
    /// These expressions must be evaluated before the tail call jump.
    /// </summary>
    public required List<IParseTree> ArgumentNodes { get; init; }

    /// <summary>
    /// True if this tail call is provably safe to optimize.
    /// False if there are any doubts about correctness (conservative analysis).
    /// </summary>
    public required bool IsSafeToOptimize { get; init; }

    /// <summary>
    /// Reason if not safe to optimize (for diagnostics and debugging).
    /// Null if IsSafeToOptimize is true.
    /// </summary>
    public string? UnsafeReason { get; init; }
}
