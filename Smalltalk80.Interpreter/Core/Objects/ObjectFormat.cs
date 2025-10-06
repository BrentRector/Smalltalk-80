namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Object format enumeration matching Smalltalk-80 Blue Book specification.
/// Determines how an object's indexed portion is stored and accessed.
/// </summary>
public enum ObjectFormat
{
    /// <summary>
    /// Object with named instance variables only (no indexed portion).
    /// </summary>
    Pointer = 0,

    /// <summary>
    /// Object with both named instance variables and indexed pointer fields.
    /// Used for Array, OrderedCollection, etc.
    /// </summary>
    PointerIndexed = 1,

    /// <summary>
    /// Object with indexed byte fields (8-bit).
    /// Used for String, ByteArray, etc.
    /// </summary>
    ByteIndexed = 2,

    /// <summary>
    /// Object with indexed word fields (16-bit).
    /// Used for WordArray, etc.
    /// </summary>
    WordIndexed = 3,

    /// <summary>
    /// CompiledMethod object with special format.
    /// Header, literals (pointers), and bytecode (bytes).
    /// </summary>
    CompiledMethod = 4
}
