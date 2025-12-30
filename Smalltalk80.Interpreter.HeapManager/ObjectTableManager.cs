using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Manages the object table blocks and index allocation.
/// Provides low-level entry access for the heap manager.
/// </summary>
internal class ObjectTableManager : IObjectTableManager
{
    private readonly List<ObjectTableBlock> _objectTableBlocks = new();
    private readonly Stack<int> _freeObjectTableIndices = new();
    private int _nextObjectTableIndex = 1; // Start at 1, reserve 0 for null/special cases

    public ObjectTableManager()
    {
        // Allocate initial ObjectTable block
        _objectTableBlocks.Add(new ObjectTableBlock());
    }

    public int AllocateObjectTableIndex()
    {
        // Reuse free index if available
        if (_freeObjectTableIndices.Count > 0)
            return _freeObjectTableIndices.Pop();

        // Canonical mapping: index >= 1, so (index - 1) gives 0-based block/entry
        int blockIndex = (_nextObjectTableIndex - 1) / ObjectTableBlock.BlockSize;
        if (blockIndex >= _objectTableBlocks.Count)
        {
            _objectTableBlocks.Add(new ObjectTableBlock());
        }

        return _nextObjectTableIndex++;
    }

    public ObjectTableEntry GetObjectTableEntry(SmalltalkOOP objectOOP)
    {
        int tableIndex = objectOOP.ToObjectTableIndex();

        // Canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlock.BlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlock.BlockSize;

        if (blockIndex >= _objectTableBlocks.Count)
            throw new ArgumentException($"Invalid ObjectTable index: {tableIndex}");

        var entry = _objectTableBlocks[blockIndex].Entries[entryIndex];
        if (entry == null)
            throw new ArgumentException($"ObjectTable entry {tableIndex} is null");

        return entry;
    }

    public void SetObjectTableEntry(int tableIndex, ObjectTableEntry entry)
    {
        // Canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlock.BlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlock.BlockSize;

        if (blockIndex >= _objectTableBlocks.Count)
            throw new ArgumentException($"Invalid ObjectTable index: {tableIndex}");

        var block = _objectTableBlocks[blockIndex];
        bool wasNull = block.Entries[entryIndex] == null;

        block.Entries[entryIndex] = entry;

        if (wasNull && entry != null)
            block.ActiveCount++;
        else if (!wasNull && entry == null)
            block.ActiveCount--;
    }

    public void FreeObjectTableIndex(int tableIndex)
    {
        _freeObjectTableIndices.Push(tableIndex);
    }

    public int GetObjectTableSize()
    {
        return _nextObjectTableIndex - 1;
    }

    public int GetActiveObjectCount()
    {
        return _objectTableBlocks.Sum(block => block.ActiveCount);
    }

    public int GetFreeIndexCount()
    {
        return _freeObjectTableIndices.Count;
    }

    /// <summary>
    /// Ensures the object table has enough blocks to accommodate the specified maximum object ID.
    /// Used during snapshot restore to pre-allocate blocks before populating entries.
    /// </summary>
    /// <param name="maxObjectId">The highest object table index that will be used.</param>
    public void EnsureCapacity(int maxObjectId)
    {
        // Canonical mapping: index >= 1, so (index - 1) gives 0-based block/entry
        int requiredBlocks = ((maxObjectId - 1) / ObjectTableBlock.BlockSize) + 1;

        while (_objectTableBlocks.Count < requiredBlocks)
        {
            _objectTableBlocks.Add(new ObjectTableBlock());
        }

        // Update _nextObjectTableIndex if needed
        if (maxObjectId >= _nextObjectTableIndex)
        {
            _nextObjectTableIndex = maxObjectId + 1;
        }
    }

    /// <summary>
    /// Clears all object table entries and resets state.
    /// Used during snapshot restore to start with empty object table.
    /// </summary>
    public void Clear()
    {
        _objectTableBlocks.Clear();
        _freeObjectTableIndices.Clear();
        _nextObjectTableIndex = 1;

        // Allocate initial block
        _objectTableBlocks.Add(new ObjectTableBlock());
    }

    /// <summary>
    /// Rebuilds the free list by scanning the object table for null entries.
    /// Used after snapshot restore to enable OOP reuse for new allocations.
    /// </summary>
    public void RebuildFreeList()
    {
        _freeObjectTableIndices.Clear();

        // Scan all indices from 1 to _nextObjectTableIndex - 1
        // Add null entries to free list for reuse
        for (int index = 1; index < _nextObjectTableIndex; index++)
        {
            int blockIndex = (index - 1) / ObjectTableBlock.BlockSize;
            int entryIndex = (index - 1) % ObjectTableBlock.BlockSize;

            if (blockIndex < _objectTableBlocks.Count &&
                _objectTableBlocks[blockIndex].Entries[entryIndex] == null)
            {
                _freeObjectTableIndices.Push(index);
            }
        }
    }

    public bool IsValidObjectOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return false; // SmallIntegers are not object pointers

        int tableIndex = objectOOP.ToObjectTableIndex();
        if (tableIndex <= 0 || tableIndex >= _nextObjectTableIndex)
            return false;

        // Use canonical mapping: (index - 1) for 1-based indices
        int blockIndex = (tableIndex - 1) / ObjectTableBlock.BlockSize;
        int entryIndex = (tableIndex - 1) % ObjectTableBlock.BlockSize;

        return blockIndex < _objectTableBlocks.Count &&
               _objectTableBlocks[blockIndex].Entries[entryIndex] != null;
    }

    public bool IsValidOOP(SmalltalkOOP oop)
    {
        // SmallIntegers are always valid
        if (oop.IsSmallInteger)
            return true;

        // Check if object ID is in valid range
        int objectId = oop.ToObjectTableIndex();
        if (objectId <= 0)
            return false;

        // Calculate block and index
        int blockIndex = (objectId - 1) / ObjectTableBlock.BlockSize;
        int index = (objectId - 1) % ObjectTableBlock.BlockSize;

        // Check bounds
        if (blockIndex >= _objectTableBlocks.Count)
            return false;

        // Check if entry exists
        var block = _objectTableBlocks[blockIndex];
        return block.Entries[index] != null;
    }

    public IEnumerable<SmalltalkOOP> EnumerateObjectTable()
    {
        for (int blockIndex = 0; blockIndex < _objectTableBlocks.Count; blockIndex++)
        {
            var block = _objectTableBlocks[blockIndex];
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                if (block.Entries[i] != null)
                {
                    // Use canonical mapping: objectId = blockIndex * BlockSize + i + 1
                    int objectId = blockIndex * ObjectTableBlock.BlockSize + i + 1;
                    yield return SmalltalkOOP.FromObjectTableIndex(objectId);
                }
            }
        }
    }

    public IReadOnlyList<ObjectTableBlock> GetBlocks()
    {
        return _objectTableBlocks;
    }
}
