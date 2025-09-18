using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives;
/// <summary>
/// Registry of primitive methods indexed by number (0-255).
/// Based on Blue Book primitive numbering.
/// </summary>
public sealed class PrimitiveTable
{
    /// <summary>
    /// Delegate for primitive method implementations.
    /// </summary>
    /// <param name="receiver">The receiver of the primitive message.</param>
    /// <param name="arguments">Arguments to the primitive.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Result of the primitive operation.</returns>
    public delegate PrimitiveResult PrimitiveMethod(OOP receiver, OOP[] arguments, IPrimitiveContext context);

    private readonly PrimitiveMethod?[] primitives = new PrimitiveMethod?[256];
    private readonly Dictionary<int, string> primitiveNames = [];

    /// <summary>
    /// Registers a primitive method.
    /// </summary>
    public void Register(int index, string name, PrimitiveMethod method)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 256);
        ArgumentNullException.ThrowIfNull(method);
        
        primitives[index] = method;
        primitiveNames[index] = name;
    }

    /// <summary>
    /// Executes a primitive by index.
    /// </summary>
    public PrimitiveResult Execute(int index, OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if ((uint)index >= 256)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.UnknownPrimitive);
        
        var method = primitives[index];
        if (method == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.UnknownPrimitive);
        
        try
        {
            return method(receiver, arguments, context);
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.Error.WriteLine($"Primitive {index} ({GetName(index)}) failed: {ex.Message}");
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Gets the name of a primitive by index.
    /// </summary>
    public string GetName(int index) =>
        primitiveNames.TryGetValue(index, out var name) 
            ? name 
            : $"Primitive{index}";

    /// <summary>
    /// Checks if a primitive is registered.
    /// </summary>
    public bool IsRegistered(int index) =>
        (uint)index < 256 && primitives[index] != null;

    /// <summary>
    /// Gets all registered primitive indices.
    /// </summary>
    public IEnumerable<int> GetRegisteredIndices()
    {
        for (int i = 0; i < 256; i++)
        {
            if (primitives[i] != null)
                yield return i;
        }
    }
}

/// <summary>
/// Context provided to primitive methods for accessing VM services.
/// </summary>
public interface IPrimitiveContext
{
    /// <summary>
    /// Gets the object memory for allocation and access.
    /// </summary>
    Memory.ObjectMemory Memory { get; }

    /// <summary>
    /// Gets a special object by index.
    /// </summary>
    OOP GetSpecialObject(int index);

    /// <summary>
    /// Gets the OOP for a class by name.
    /// </summary>
    OOP GetClass(string className);

    /// <summary>
    /// Creates a new instance of a class.
    /// </summary>
    OOP NewInstance(OOP classOOP, int size);

    /// <summary>
    /// Performs a message send (for primitives that need to invoke methods).
    /// </summary>
    OOP Send(OOP receiver, OOP selector, OOP[] arguments);
}