using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

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
    /// Gets the count of free indices available for reuse (without growing the table).
    /// Used by heap manager to decide when to trigger GC.
    /// </summary>
    int GetFreeIndexCount();

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

    /// <summary>
    /// Ensures the object table has enough blocks to accommodate the specified maximum object ID.
    /// Used during snapshot restore to pre-allocate blocks before populating entries.
    /// </summary>
    /// <param name="maxObjectId">The highest object table index that will be used.</param>
    void EnsureCapacity(int maxObjectId);

    /// <summary>
    /// Clears all object table entries and resets state.
    /// Used during snapshot restore to start with empty object table.
    /// </summary>
    void Clear();

    /// <summary>
    /// Rebuilds the free list by scanning the object table for null entries.
    /// Used after snapshot restore to enable OOP reuse for new allocations.
    /// </summary>
    void RebuildFreeList();
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
