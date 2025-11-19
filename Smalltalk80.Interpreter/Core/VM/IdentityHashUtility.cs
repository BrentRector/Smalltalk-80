using System;
using Smalltalk80.Interpreter.Core.HeapManager;

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
}
