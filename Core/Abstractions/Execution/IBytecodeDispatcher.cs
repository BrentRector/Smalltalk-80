namespace Core.Abstractions.Execution;

/// <summary>
/// Interface for bytecode execution.
/// </summary>
public interface IBytecodeDispatcher
{
    /// <summary>
    /// Executes the next bytecode in the context.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>True if execution should continue, false if the context has completed.</returns>
    bool ExecuteNextBytecode(IExecutionContext context);

    /// <summary>
    /// Executes a specific bytecode.
    /// </summary>
    /// <param name="bytecode">The bytecode to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>True if execution should continue, false if the context has completed.</returns>
    bool ExecuteBytecode(byte bytecode, IExecutionContext context);
}