using Core.Abstractions.Execution;
using Core.Abstractions.Memory;

namespace Core.Abstractions.Primitives;

/// <summary>
/// Interface for the primitive method table.
/// </summary>
public interface IPrimitiveTable
{
    /// <summary>
    /// Executes a primitive method.
    /// </summary>
    /// <param name="primitiveIndex">The primitive index.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>True if the primitive succeeded, false if it failed.</returns>
    bool ExecutePrimitive(int primitiveIndex, IExecutionContext context);

    /// <summary>
    /// Registers a primitive handler.
    /// </summary>
    /// <param name="index">The primitive index.</param>
    /// <param name="handler">The primitive handler.</param>
    void RegisterPrimitive(int index, Func<IExecutionContext, IObjectMemory, bool> handler);

    /// <summary>
    /// Gets the name of a primitive.
    /// </summary>
    /// <param name="index">The primitive index.</param>
    /// <returns>The primitive name, or null if not found.</returns>
    string? GetPrimitiveName(int index);
}