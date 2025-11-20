using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine;

/// <summary>
/// Interface exposing VM primitives for use during cold start phases.
/// Provides the same object access semantics that Smalltalk code uses, eliminating
/// layout duplication between cold start and runtime VM operations.
/// </summary>
public interface IColdStartPrimitives
{
    /// <summary>
    /// Instance variable access (like Smalltalk instVarAt:)
    /// Gets an instance variable by 1-based index.
    /// </summary>
    /// <param name="objectOOP">Object to read from</param>
    /// <param name="index">1-based instance variable index</param>
    /// <returns>OOP value of the instance variable</returns>
    SmalltalkOOP InstVarAt(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Instance variable access (like Smalltalk instVarAt:put:)
    /// Sets an instance variable by 1-based index.
    /// </summary>
    /// <param name="objectOOP">Object to modify</param>
    /// <param name="index">1-based instance variable index</param>
    /// <param name="value">OOP value to store</param>
    void InstVarAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    /// <summary>
    /// Indexed content access (Primitive 60: at:)
    /// Gets indexed content by 1-based index. Format-aware:
    /// - PointerIndexed: returns OOP
    /// - ByteIndexed: returns SmallInteger (0-255)
    /// - CompiledMethod: delegates to bytecode array
    /// </summary>
    /// <param name="objectOOP">Object to read from</param>
    /// <param name="index">1-based index</param>
    /// <returns>Content at the specified index</returns>
    SmalltalkOOP At(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Indexed content access (Primitive 61: at:put:)
    /// Sets indexed content by 1-based index. Format-aware.
    /// </summary>
    /// <param name="objectOOP">Object to modify</param>
    /// <param name="index">1-based index</param>
    /// <param name="value">Value to store</param>
    void AtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    /// <summary>
    /// Object content access (Primitive 68: objectAt:)
    /// Gets object content by 1-based index. CompiledMethod-specific:
    /// - Index 1: method header
    /// - Index 2+: literals
    /// </summary>
    /// <param name="objectOOP">Object to read from (must be CompiledMethod)</param>
    /// <param name="index">1-based index</param>
    /// <returns>OOP at the specified index</returns>
    SmalltalkOOP ObjectAt(SmalltalkOOP objectOOP, int index);

    /// <summary>
    /// Object content access (Primitive 69: objectAt:put:)
    /// Sets object content by 1-based index. CompiledMethod-specific.
    /// </summary>
    /// <param name="objectOOP">Object to modify (must be CompiledMethod)</param>
    /// <param name="index">1-based index</param>
    /// <param name="value">OOP value to store</param>
    void ObjectAtPut(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);
}