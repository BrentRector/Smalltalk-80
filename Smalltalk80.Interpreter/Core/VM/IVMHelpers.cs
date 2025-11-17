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
    /// </summary>
    MethodHeaderInfo ParseMethodHeader(SmalltalkOOP methodOOP);
}

/// <summary>
/// Parsed information from a CompiledMethod header.
/// Smalltalk-80 method headers encode metadata in a 15-bit field:
///   Bits 0-5   (6 bits): numLiterals (0-63)
///   Bit  6     (1 bit):  needsLargeFrame flag
///   Bits 7-11  (5 bits): numTemps (0-31)
///   Bits 12-14 (3 bits): flags field
/// </summary>
internal record MethodHeaderInfo(
    int NumLiterals,
    bool NeedsLargeFrame,
    int NumTemps,
    int Flags,
    int NumArgs,
    int Primitive,
    bool IsQuickReturnSelf,
    bool IsQuickReturnField,
    int QuickReturnFieldIndex);
