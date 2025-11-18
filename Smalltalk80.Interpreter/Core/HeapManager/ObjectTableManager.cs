using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.HeapManager;

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

    public bool IsValidObjectOOP(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return true; // SmallIntegers are always valid

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
