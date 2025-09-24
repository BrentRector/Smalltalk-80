using System;

namespace Smalltalk80.Interpreter.ObjectModel;
/// <summary>
/// Object header containing metadata for every Smalltalk object.
/// This is separate from the object's fields to maintain clean separation.
/// </summary>
public sealed class ObjectHeader
{
    /// <summary>
    /// The class of this object. NEVER null or Nil after bootstrap Phase 2.
    /// During Phase 0-1, may use temporary placeholder values.
    /// </summary>
    public OOP ClassOOP { get; set; }

    /// <summary>
    /// Size in words (number of fields).
    /// </summary>
    public int Size { get; init; }

    /// <summary>
    /// Object format as defined in Blue Book.
    /// Bits 0-3: Instance specification
    /// Bit 4: Has pointers
    /// Bit 5: Indexable
    /// Bit 6: Words/Bytes
    /// </summary>
    public int Format { get; init; }

    /// <summary>
    /// Identity hash for this object.
    /// </summary>
    public int Hash { get; init; }

    /// <summary>
    /// Garbage collection flags.
    /// Bit 0: Mark bit
    /// Bit 1: Dirty bit (modified since last GC)
    /// Bit 2: Permanent (never collect)
    /// </summary>
    public byte GCFlags { get; set; }

    /// <summary>
    /// Gets whether this object is marked for GC.
    /// </summary>
    public bool IsMarked
    {
        get => (GCFlags & 1) != 0;
        set => GCFlags = value ? (byte)(GCFlags | 1) : (byte)(GCFlags & ~1);
    }

    /// <summary>
    /// Gets whether this object is permanent (never collected).
    /// </summary>
    public bool IsPermanent
    {
        get => (GCFlags & 4) != 0;
        set => GCFlags = value ? (byte)(GCFlags | 4) : (byte)(GCFlags & ~4);
    }

    /// <summary>
    /// Creates a new object header.
    /// </summary>
    public ObjectHeader(OOP classOOP, int size, int format = 0)
    {
        ClassOOP = classOOP;
        Size = size;
        Format = format;
        Hash = GetNextHash();
        GCFlags = 0;
    }

    private static int nextHash = 1;
    private static int GetNextHash() => nextHash++;

    public override string ToString() =>
        $"Header[Class={ClassOOP}, Size={Size}, Format={Format:X}, Hash={Hash}, GC={GCFlags:X}]";
}