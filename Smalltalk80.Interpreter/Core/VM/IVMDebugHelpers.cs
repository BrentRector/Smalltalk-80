using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Helper methods needed by VMDebugger for accessing VM state.
/// Provides access to symbol/class names, method literals, and local IDs.
/// </summary>
internal interface IVMDebugHelpers
{
    /// <summary>
    /// Gets the class OOP of an object.
    /// </summary>
    /// <param name="objectOOP">Object to get class of.</param>
    /// <returns>Class OOP.</returns>
    SmalltalkOOP GetClass(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets the class name as a string.
    /// </summary>
    /// <param name="classOOP">Class OOP to get name of.</param>
    /// <returns>Class name string.</returns>
    string GetClassName(SmalltalkOOP classOOP);

    /// <summary>
    /// Gets the name of a symbol as a string.
    /// </summary>
    /// <param name="symbolOOP">Symbol OOP to get name of.</param>
    /// <returns>Symbol name, or null if not a symbol.</returns>
    string? GetSymbolName(SmalltalkOOP symbolOOP);

    /// <summary>
    /// Gets the local ID for an object (used for debugging/diagnostics).
    /// </summary>
    /// <param name="objectOOP">Object OOP to get local ID for.</param>
    /// <returns>Local ID string, or null if not found.</returns>
    string? GetLocalId(SmalltalkOOP objectOOP);

    /// <summary>
    /// Gets a literal from a CompiledMethod's literal frame.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod OOP.</param>
    /// <param name="index">Literal index (0 = header, 1+ = literals).</param>
    /// <returns>Literal OOP.</returns>
    SmalltalkOOP GetMethodLiteral(SmalltalkOOP methodOOP, int index);

    /// <summary>
    /// Gets the bytecode array from a CompiledMethod.
    /// </summary>
    /// <param name="methodOOP">CompiledMethod OOP.</param>
    /// <returns>Bytecode array.</returns>
    byte[] GetMethodBytecodes(SmalltalkOOP methodOOP);
}
