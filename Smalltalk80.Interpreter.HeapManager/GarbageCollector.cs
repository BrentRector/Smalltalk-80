using System.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Mark-and-sweep garbage collector for the Smalltalk heap.
/// Uses GC roots as starting points for the mark phase.
/// </summary>
internal class GarbageCollector : IGarbageCollector
{
    private readonly IObjectTableManager _tableManager;
    private readonly ISmalltalkHeapManager _heap;
    private SmalltalkOOP[] _gcRoots = Array.Empty<SmalltalkOOP>();

    public GarbageCollector(IObjectTableManager tableManager, ISmalltalkHeapManager heap)
    {
        _tableManager = tableManager;
        _heap = heap;
    }

    public void SetGCRoots(SmalltalkOOP[] rootObjects)
    {
        _gcRoots = rootObjects?.ToArray() ?? Array.Empty<SmalltalkOOP>();
    }

    public void AddGCRoots(SmalltalkOOP[] additionalRoots)
    {
        if (additionalRoots == null || additionalRoots.Length == 0)
            return;

        var newRoots = new SmalltalkOOP[_gcRoots.Length + additionalRoots.Length];
        _gcRoots.CopyTo(newRoots, 0);
        additionalRoots.CopyTo(newRoots, _gcRoots.Length);
        _gcRoots = newRoots;
    }

    public SmalltalkOOP[] GetGCRoots()
    {
        return _gcRoots.ToArray();
    }

    public void MarkObject(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return; // SmallIntegers don't need marking

        if (!_tableManager.IsValidObjectOOP(objectOOP))
            return; // Invalid OOPs are ignored

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        entry.IsMarked = true;
    }

    public SmalltalkOOP[] GetUnreachableObjects()
    {
        var unreachableObjects = new List<SmalltalkOOP>();
        var blocks = _tableManager.GetBlocks();

        // Phase 1: Clear all mark bits
        foreach (var block in blocks)
        {
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                if (block.Entries[i] != null)
                    block.Entries[i]!.IsMarked = false;
            }
        }

        // Phase 2: Mark from configured roots
        foreach (var rootOOP in _gcRoots)
        {
            MarkObjectRecursively(rootOOP);
        }

        // Phase 3: Find unmarked objects (would be collected)
        for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                var entry = block.Entries[i];
                if (entry != null && !entry.IsMarked)
                {
                    // Use canonical mapping to create OOP
                    int objectId = blockIndex * ObjectTableBlock.BlockSize + i + 1;
                    unreachableObjects.Add(SmalltalkOOP.FromObjectTableIndex(objectId));
                }
            }
        }

        return unreachableObjects.ToArray();
    }

    public GCStatistics GarbageCollect()
    {
        var stopwatch = Stopwatch.StartNew();
        var blocks = _tableManager.GetBlocks();

        // Phase 1: Count objects before collection
        int objectsBeforeCollection = _tableManager.GetActiveObjectCount();

        // Phase 2: Clear all mark bits
        foreach (var block in blocks)
        {
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                if (block.Entries[i] != null)
                    block.Entries[i]!.IsMarked = false;
            }
        }

        double markStartTime = stopwatch.Elapsed.TotalMilliseconds;

        // Phase 3: Mark from configured roots
        foreach (var rootOOP in _gcRoots)
        {
            MarkObjectRecursively(rootOOP);
        }

        double markEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double markTime = markEndTime - markStartTime;

        // Phase 4: Sweep unmarked objects
        var collectedByFormat = new Dictionary<ObjectFormat, int>();
        long estimatedBytesFreed = 0;
        int objectsCollected = 0;

        var firstCollectedIds = new List<int>();
        var collectedObjectDetails = new List<(int oop, ObjectFormat format, int size, string? className)>();

        for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                var entry = block.Entries[i];
                if (entry != null && !entry.IsMarked)
                {
                    // Track collection statistics
                    collectedByFormat[entry.FormatExplicit] = collectedByFormat.GetValueOrDefault(entry.FormatExplicit) + 1;
                    estimatedBytesFreed += entry.SizeInWordsExplicit * 4;
                    objectsCollected++;

                    // Track collected object IDs for debugging (use canonical mapping)
                    int objectId = blockIndex * ObjectTableBlock.BlockSize + i + 1;
                    if (firstCollectedIds.Count < 20)
                    {
                        firstCollectedIds.Add(objectId);
                    }

                    // Track detailed info about collected objects
                    if (collectedObjectDetails.Count < 50)
                    {
                        var oop = SmalltalkOOP.FromObjectTableIndex(objectId);
                        string? className = null;
                        try
                        {
                            className = _heap.GetClassName(oop);
                        }
                        catch
                        {
                            className = "<unknown>";
                        }
                        collectedObjectDetails.Add((objectId, entry.FormatExplicit, entry.SizeInWordsExplicit, className));
                    }

                    // Clear managed storage (allows .NET GC to reclaim memory)
                    entry.ManagedStorage = null;
                    entry.InstanceVariableStorage = null;

                    // Push freed index back to free list for reuse
                    _tableManager.FreeObjectTableIndex(objectId);

                    // Free the ObjectTable entry
                    block.Entries[i] = null;
                    block.ActiveCount--;
                }
            }
        }

        double sweepEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double sweepTime = sweepEndTime - markEndTime;

        int objectsAfterCollection = _tableManager.GetActiveObjectCount();
        int freeEntries = _tableManager.GetObjectTableSize() - objectsAfterCollection;

        return new GCStatistics
        {
            TotalObjects = objectsBeforeCollection,
            LiveObjects = objectsAfterCollection,
            CollectedObjects = objectsCollected,
            MarkTimeMs = markTime,
            SweepTimeMs = sweepTime,
            TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds,
            EstimatedBytesFreed = estimatedBytesFreed,
            CollectedByFormat = collectedByFormat,
            FreeObjectTableEntries = freeEntries,
            CollectedObjectDetails = collectedObjectDetails
        };
    }

    public GCStatistics GetGCStatistics()
    {
        var objectsByFormat = new Dictionary<ObjectFormat, int>();
        long estimatedMemoryUsage = 0;
        int activeObjects = 0;
        var blocks = _tableManager.GetBlocks();

        foreach (var block in blocks)
        {
            for (int i = 0; i < ObjectTableBlock.BlockSize; i++)
            {
                var entry = block.Entries[i];
                if (entry != null)
                {
                    activeObjects++;
                    objectsByFormat[entry.Format] = objectsByFormat.GetValueOrDefault(entry.Format) + 1;
                    estimatedMemoryUsage += entry.SizeInBytes;
                }
            }
        }

        return new GCStatistics
        {
            TotalObjects = activeObjects,
            LiveObjects = activeObjects,
            CollectedObjects = 0, // No collection performed
            MarkTimeMs = 0,
            SweepTimeMs = 0,
            TotalTimeMs = 0,
            EstimatedBytesFreed = 0,
            CollectedByFormat = new Dictionary<ObjectFormat, int>(),
            FreeObjectTableEntries = _tableManager.GetObjectTableSize() - activeObjects
        };
    }

    /// <summary>Recursively marks an object and all objects it references.</summary>
    private void MarkObjectRecursively(SmalltalkOOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return;

        if (!_tableManager.IsValidObjectOOP(objectOOP))
            return;

        var entry = _tableManager.GetObjectTableEntry(objectOOP);
        if (entry.IsMarked)
            return;

        entry.IsMarked = true;

        // Traverse pointer fields based on format
        if (entry.FormatExplicit == ObjectFormat.Pointer || entry.FormatExplicit == ObjectFormat.PointerIndexed)
        {
            var oopArray = entry.AsOopArray();
            if (oopArray != null)
            {
                // Mark all OOPs in the array (header, instance vars, indexed elements)
                // INCLUDES slot 0 (class pointer) for Pointer/PointerIndexed objects
                foreach (var childOOP in oopArray)
                {
                    MarkObjectRecursively(childOOP);
                }
            }
        }
        // ALSO mark the class pointer and instance variables for ByteIndexed/WordIndexed objects
        // (they store class in separate ClassOOP field and instance variables in InstanceVariableStorage)
        else if (entry.FormatExplicit == ObjectFormat.ByteIndexed || entry.FormatExplicit == ObjectFormat.WordIndexed)
        {
            // Mark class pointer
            var classOOP = entry.ClassOOP;
            if (classOOP != _heap.GetNil())
            {
                MarkObjectRecursively(classOOP);
            }

            // Mark instance variables (e.g., Form has bits, width, height, offset)
            if (entry.InstanceVariableStorage != null)
            {
                foreach (var instVarOOP in entry.InstanceVariableStorage)
                {
                    MarkObjectRecursively(instVarOOP);
                }
            }
        }
    }
}
