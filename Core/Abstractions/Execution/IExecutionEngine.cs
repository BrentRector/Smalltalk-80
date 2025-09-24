using System.Threading;
using System.Threading.Tasks;
using Core.Execution;
using Core.Memory;

namespace Core.Abstractions.Execution;

/// <summary>
/// Manages bytecode execution flow according to the Smalltalk-80 Blue Book specification.
/// This is the main execution coordinator as defined in Interpreter Architecture.md.
/// </summary>
public interface IExecutionEngine
{
    /// <summary>
    /// Executes a method with the given receiver and arguments.
    /// </summary>
    /// <param name="receiver">The receiver object.</param>
    /// <param name="method">The compiled method to execute.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The execution result.</returns>
    Task<ExecutionResult> ExecuteMethodAsync(
        OOP receiver, 
        OOP method, 
        OOP[] arguments,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Handles a normal method return.
    /// </summary>
    /// <param name="context">The returning context.</param>
    /// <param name="value">The return value.</param>
    void HandleReturn(MethodContext context, OOP value);
    
    /// <summary>
    /// Handles a block return.
    /// </summary>
    /// <param name="context">The block context.</param>
    /// <param name="value">The return value.</param>
    void HandleBlockReturn(BlockContext context, OOP value);
}

/// <summary>
/// Represents the result of method execution.
/// </summary>
public record ExecutionResult(
    OOP Value,
    ExecutionStatus Status,
    string? ErrorMessage = null);

/// <summary>
/// Execution status codes.
/// </summary>
public enum ExecutionStatus
{
    Success,
    PrimitiveFailed,
    DoesNotUnderstand,
    StackOverflow,
    StackUnderflow,
    Error
}

// BlockContext is defined in MethodContext.cs