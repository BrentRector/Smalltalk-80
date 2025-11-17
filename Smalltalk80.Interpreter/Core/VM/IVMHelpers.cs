using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Helper methods needed by extracted VM components.
/// Provides access to common VM operations without circular dependencies.
/// </summary>
internal interface IVMHelpers
{
    /// <summary>
    /// Gets the class OOP of an object.
    /// </summary>
    SmalltalkOOP GetClass(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the class name as a string.
    /// </summary>
    string GetClassName(SmalltalkOOP classOOP);

    /// <summary>
    /// Traverses the home chain to find the MethodContext for a given context.
    /// BlockContexts link to their home MethodContext via the 'home' field.
    /// </summary>
    SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP);

    /// <summary>
    /// Parses a CompiledMethod header to extract argument count, temporary count, etc.
    /// Returns a MethodHeaderInfo record (defined in VirtualMachine.cs).
    /// </summary>
    MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP);
}
