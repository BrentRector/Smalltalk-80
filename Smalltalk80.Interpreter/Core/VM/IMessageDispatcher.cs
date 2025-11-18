using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Handles method lookup in the Smalltalk-80 class hierarchy.
/// Extracted from VirtualMachine for better separation of concerns.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Looks up a method in a class hierarchy starting from the given class.
    /// Walks up the superclass chain until the method is found or nil is reached.
    /// </summary>
    /// <param name="classOOP">Starting class for method lookup</param>
    /// <param name="selectorName">Name of the selector to find</param>
    /// <returns>MethodLookupResult containing the method OOP, or null if not found</returns>
    MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, string selectorName);

    /// <summary>
    /// Gets the primitive number from a CompiledMethod.
    /// Returns 0 if the method has no primitive.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod to inspect</param>
    /// <returns>Primitive number (0 if no primitive)</returns>
    int GetPrimitiveNumber(SmalltalkOOP methodOOP);
}

/// <summary>
/// Result of a method lookup operation.
/// </summary>
public class MethodLookupResult
{
    /// <summary>
    /// The OOP of the found CompiledMethod.
    /// </summary>
    public SmalltalkOOP MethodOOP { get; init; }
}
