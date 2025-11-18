using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.HeapManager;

/// <summary>
/// Interface for low-level object table management.
/// Handles block allocation, index management, and entry access.
/// </summary>
internal interface IObjectTableManager
{
    /// <summary>
    /// Allocates a new ObjectTable index, growing blocks as needed.
    /// </summary>
    int AllocateObjectTableIndex();

    /// <summary>
    /// Gets ObjectTableEntry for a given OOP.
    /// </summary>
    ObjectTableEntry GetObjectTableEntry(SmalltalkOOP objectOOP);

    /// <summary>
    /// Sets ObjectTableEntry for a given index.
    /// </summary>
    void SetObjectTableEntry(int tableIndex, ObjectTableEntry entry);

    /// <summary>
    /// Returns an ObjectTable index to the free list for reuse.
    /// </summary>
    void FreeObjectTableIndex(int tableIndex);

    /// <summary>
    /// Gets the total size of the object table (highest allocated index).
    /// </summary>
    int GetObjectTableSize();

    /// <summary>
    /// Gets the count of active (non-null) objects in the table.
    /// </summary>
    int GetActiveObjectCount();

    /// <summary>
    /// Checks if an OOP refers to a valid object in the heap.
    /// </summary>
    bool IsValidObjectOOP(SmalltalkOOP objectOOP);

    /// <summary>
    /// Checks if an OOP is valid (either SmallInteger or valid heap object).
    /// Used by primitive 76 (SmallInteger asObject).
    /// </summary>
    bool IsValidOOP(SmalltalkOOP oop);

    /// <summary>
    /// Enumerates all valid objects in the object table in canonical order.
    /// Yields only non-null entries, skipping freed slots.
    /// </summary>
    IEnumerable<SmalltalkOOP> EnumerateObjectTable();

    /// <summary>
    /// Gets all object table blocks for iteration (used by GC).
    /// </summary>
    IReadOnlyList<ObjectTableBlock> GetBlocks();
}

/// <summary>
/// Represents a block of ObjectTable entries for scalable growth.
/// </summary>
public sealed class ObjectTableBlock
{
    public const int BlockSize = 1024;
    public readonly ObjectTableEntry?[] Entries = new ObjectTableEntry[BlockSize];
    public int ActiveCount = 0;
}
