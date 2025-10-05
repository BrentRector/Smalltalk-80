namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Defines the format of an object's indexed data per Blue Book specification.
/// The VM must understand these format types to provide generic access.
/// </summary>
public enum ObjectFormat : byte
{
    /// <summary>
    /// Object has only named instance variables, no indexed data.
    /// Example: Point, Character, most user-defined classes
    /// </summary>
    FixedPointer = 0,

    /// <summary>
    /// Object has named instance variables + indexed pointer fields.
    /// Indexed fields contain SObjectRef values (objects or SmallIntegers).
    /// Example: Array, OrderedCollection, Dictionary
    /// Blue Book formats 0-4 (varying number of fixed fields)
    /// </summary>
    PointerIndexed = 1,

    /// <summary>
    /// Object has named instance variables + indexed 16-bit word fields.
    /// Indexed fields contain unsigned 16-bit integers.
    /// Example: WordArray
    /// Blue Book format 5
    /// </summary>
    WordIndexed = 2,

    /// <summary>
    /// Object has named instance variables + indexed byte fields.
    /// Indexed fields contain unsigned 8-bit bytes.
    /// Example: String, ByteArray, Symbol
    /// Blue Book formats 6-7
    /// </summary>
    ByteIndexed = 3,

    /// <summary>
    /// Special format for CompiledMethod objects.
    /// Contains both literal pointers and bytecode bytes.
    /// Blue Book formats 8-11 (varying number of literal fields)
    /// </summary>
    CompiledMethod = 4
}