using Core.Execution;
using Core.Memory;

namespace Core.Abstractions.Execution;

/// <summary>
/// Manages method and block contexts according to the Blue Book specification.
/// As defined in Interpreter Architecture.md section 2.
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Creates a new method context for method activation.
    /// </summary>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <param name="sender">The sending context (null for top-level).</param>
    /// <returns>A new method context ready for execution.</returns>
    MethodContext CreateMethodContext(
        OOP receiver,
        OOP method,
        OOP[] arguments,
        MethodContext? sender = null);
        
    /// <summary>
    /// Creates a new block context for block activation.
    /// </summary>
    /// <param name="home">The home method context.</param>
    /// <param name="argumentCount">Number of block arguments.</param>
    /// <returns>A new block context.</returns>
    BlockContext CreateBlockContext(
        MethodContext home,
        int argumentCount);
        
    /// <summary>
    /// Activates a context, making it the active context.
    /// </summary>
    /// <param name="context">The context to activate.</param>
    void ActivateContext(MethodContext context);
    
    /// <summary>
    /// Returns from a context with the given value.
    /// </summary>
    /// <param name="context">The context to return from.</param>
    /// <param name="value">The return value.</param>
    void ReturnFromContext(MethodContext context, OOP value);
    
    /// <summary>
    /// Gets the currently active context.
    /// </summary>
    MethodContext? ActiveContext { get; }
    
    /// <summary>
    /// Gets the home context of the current execution.
    /// </summary>
    MethodContext? HomeContext { get; }
}