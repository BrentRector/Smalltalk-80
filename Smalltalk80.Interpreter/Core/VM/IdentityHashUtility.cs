using System;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Shared VM utility functions used by both cold start and runtime primitives.
/// </summary>
public static class IdentityHashUtility
{
    /// <summary>
    /// Computes the identity hash for a Symbol (or any object).
    /// Implements Primitive 75 (Symbol>>hash) semantics.
    ///
    /// Blue Book: "Answer with a SmallInteger whose value is half of the receiver's object pointer"
    ///
    /// This is used by:
    /// - Primitive 75 (Object>>asOop / Symbol>>hash)
    /// - Cold start dictionary insertion (Phase 3, Phase 5)
    /// - Runtime dictionary lookups
    /// </summary>
    /// <param name="objectOOP">The object to compute hash for</param>
    /// <returns>Identity hash as SmallInteger value</returns>
    public static int ComputeIdentityHash(SmalltalkOOP objectOOP)
    {
        // For SmallIntegers, there is no object pointer, so this would fail
        // However, Symbol is never a SmallInteger, so this is safe for Symbol>>hash
        if (objectOOP.IsSmallInteger)
            throw new InvalidOperationException($"Cannot compute identity hash for SmallInteger: {objectOOP.AsSmallInteger()}");

        // Blue Book: "Answer with a SmallInteger whose value is half of the receiver's object pointer"
        // OOP encoding: object pointer = (objectTableIndex << 1) | 0
        // Therefore: half of object pointer = objectTableIndex
        // The object table index IS the identity hash
        return objectOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Computes the 0-indexed hash slot for a key in an IdentityDictionary of given size.
    /// Matches Smalltalk's `key asOop \\ length` (converted from 1-indexed to 0-indexed).
    /// Use this for IdentityDictionary and MethodDictionary lookups.
    ///
    /// This is the canonical hash index computation used by:
    /// - Cold start dictionary installation (Phase 3, 4, 5)
    /// - Runtime dictionary lookups and installations
    /// </summary>
    /// <param name="keyOOP">The key OOP to hash (must be object pointer, not SmallInteger).</param>
    /// <param name="dictionarySize">The size of the dictionary's indexed storage.</param>
    /// <returns>The 0-indexed slot position.</returns>
    public static int ComputeHashIndex(SmalltalkOOP keyOOP, int dictionarySize)
    {
        int hash = ComputeIdentityHash(keyOOP);
        return (hash % dictionarySize + dictionarySize) % dictionarySize;
    }

    /// <summary>
    /// Computes the 0-indexed hash slot for a key in a regular Dictionary of given size.
    /// Matches Smalltalk's `key hash \\ length` (converted from 1-indexed to 0-indexed).
    /// Use this for Dictionary lookups where keys might be SmallIntegers.
    /// </summary>
    /// <param name="keyOOP">The key OOP to hash (can be SmallInteger or object pointer).</param>
    /// <param name="dictionarySize">The size of the dictionary's indexed storage.</param>
    /// <returns>The 0-indexed slot position.</returns>
    public static int ComputeDictionaryHashIndex(SmalltalkOOP keyOOP, int dictionarySize)
    {
        // For SmallIntegers: hash returns self (masked to ensure positive)
        // For objects: hash returns half the OOP (object table index)
        int hash = keyOOP.IsSmallInteger
            ? keyOOP.AsSmallInteger() & 0x3FFFFFFF
            : keyOOP.ToObjectTableIndex();
        return (hash % dictionarySize + dictionarySize) % dictionarySize;
    }
}
