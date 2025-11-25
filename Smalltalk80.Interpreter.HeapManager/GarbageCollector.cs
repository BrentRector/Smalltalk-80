using System.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.HeapManager;

/// <summary>
/// Mark-and-sweep garbage collector for the Smalltalk heap.
/// Uses GC roots as starting points for the mark phase.
/// </summary>
internal class GarbageCollector : IGarbageCollector
{
    private readonly IObjectTableManager _tableManager;
    private readonly ISmalltalkHeap _heap;
    private readonly IDiagnosticHub? _diagnostics;
    private SmalltalkOOP[] _gcRoots = Array.Empty<SmalltalkOOP>();

    // Cumulative statistics (thread-safe accumulation with overflow protection)
    private readonly object _cumulativeStatsLock = new();
    private long _cumulativeGCCount;
    private long _cumulativeCollectedObjects;
    private long _cumulativeBytesFreed;
    private double _cumulativeMarkTimeMs;
    private double _cumulativeSweepTimeMs;
    private double _cumulativeTotalTimeMs;
    private long _cumulativeObjectsExamined;

    public GarbageCollector(
        IObjectTableManager tableManager,
        ISmalltalkHeap heap,
        IDiagnosticHub? diagnostics = null)
    {
        _tableManager = tableManager;
        _heap = heap;
        _diagnostics = diagnostics;
    }

    public void AddGCRoot(SmalltalkOOP oop)
    {
        // Check if already in roots
        if (_gcRoots.Contains(oop))
            return;

        var newRoots = new SmalltalkOOP[_gcRoots.Length + 1];
        _gcRoots.CopyTo(newRoots, 0);
        newRoots[_gcRoots.Length] = oop;
        _gcRoots = newRoots;
    }

    public void RemoveGCRoot(SmalltalkOOP oop)
    {
        int index = Array.IndexOf(_gcRoots, oop);
        if (index < 0)
            return; // Not found

        var newRoots = new SmalltalkOOP[_gcRoots.Length - 1];
        Array.Copy(_gcRoots, 0, newRoots, 0, index);
        Array.Copy(_gcRoots, index + 1, newRoots, index, _gcRoots.Length - index - 1);
        _gcRoots = newRoots;
    }

    public SmalltalkOOP[] GetGCRoots()
    {
        return _gcRoots.ToArray();
    }

    public void ClearGCRoots()
    {
        _gcRoots = Array.Empty<SmalltalkOOP>();
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

        // Count objects before collection
        int objectsBeforeCollection = _tableManager.GetActiveObjectCount();

        // Emit GCStart event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCStart) ?? false)
        {
            _diagnostics.Emit(new GCStartEvent
            {
                TotalObjects = objectsBeforeCollection,
                EstimatedLiveObjects = objectsBeforeCollection,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        // Emit GCMarkPhaseStart event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCMarkPhaseStart) ?? false)
        {
            _diagnostics.Emit(new GCMarkPhaseStartEvent
            {
                RootCount = _gcRoots.Length,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        // Steps 1-3: Clear marks → Mark reachable → Find unreachable
        double markStartTime = stopwatch.Elapsed.TotalMilliseconds;
        var unreachableObjects = GetUnreachableObjects();
        double markEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double markTime = markEndTime - markStartTime;

        // Emit GCMarkPhaseEnd event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCMarkPhaseEnd) ?? false)
        {
            _diagnostics.Emit(new GCMarkPhaseEndEvent
            {
                MarkedObjects = objectsBeforeCollection - unreachableObjects.Length,
                UnreachableObjects = unreachableObjects.Length,
                MarkTimeMs = markTime,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        // Emit GCSweepPhaseStart event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCSweepPhaseStart) ?? false)
        {
            _diagnostics.Emit(new GCSweepPhaseStartEvent
            {
                ObjectsToCollect = unreachableObjects.Length,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        // Step 4: Collect unreachable objects
        var collectedByFormat = new Dictionary<ObjectFormat, int>();
        long estimatedBytesFreed = 0;
        var collectedObjectDetails = new List<(int oop, ObjectFormat format, int size, string? className)>();

        // Check if per-object collection events are enabled
        bool emitCollectedEvents = _diagnostics?.IsEnabled(DiagnosticEventTypes.GCObjectCollected) ?? false;

        foreach (var oop in unreachableObjects)
        {
            var entry = _tableManager.GetObjectTableEntry(oop);
            int objectId = oop.ToObjectTableIndex();

            // Track collection statistics
            collectedByFormat[entry.FormatExplicit] = collectedByFormat.GetValueOrDefault(entry.FormatExplicit) + 1;
            estimatedBytesFreed += entry.SizeInWordsExplicit * 4;

            // Track detailed info about collected objects (no limit - use events instead)
            string? className = null;
            try
            {
                className = _heap.GetClassName(oop);
            }
            catch
            {
                className = "<unknown>";
            }

            // IMPORTANT: Report OOP raw value (external API), not internal object table index
            collectedObjectDetails.Add((oop.RawValue, entry.FormatExplicit, entry.SizeInWordsExplicit, className));

            // Emit per-object collection event if listener registered
            if (emitCollectedEvents)
            {
                _diagnostics!.Emit(new GCObjectCollectedEvent
                {
                    ObjectOOP = oop,
                    ClassName = className,
                    Format = entry.FormatExplicit,
                    SizeInWords = entry.SizeInWordsExplicit,
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
            }

            // Clear managed storage (allows .NET GC to reclaim memory)
            entry.ManagedStorage = null;
            entry.InstanceVariableStorage = null;

            // Remove entry from ObjectTable (prevents double-free on next GC)
            _tableManager.SetObjectTableEntry(objectId, null!);

            // Free the object table index for reuse
            _tableManager.FreeObjectTableIndex(objectId);
        }

        double sweepEndTime = stopwatch.Elapsed.TotalMilliseconds;
        double sweepTime = sweepEndTime - markEndTime;

        // Emit GCSweepPhaseEnd event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCSweepPhaseEnd) ?? false)
        {
            _diagnostics.Emit(new GCSweepPhaseEndEvent
            {
                CollectedObjects = unreachableObjects.Length,
                BytesFreed = estimatedBytesFreed,
                SweepTimeMs = sweepTime,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        int objectsAfterCollection = _tableManager.GetActiveObjectCount();
        int freeEntries = _tableManager.GetObjectTableSize() - objectsAfterCollection;

        // Emit GCEnd event
        if (_diagnostics?.IsEnabled(DiagnosticEventTypes.GCEnd) ?? false)
        {
            _diagnostics.Emit(new GCEndEvent
            {
                ObjectsBefore = objectsBeforeCollection,
                ObjectsAfter = objectsAfterCollection,
                ObjectsCollected = unreachableObjects.Length,
                MarkTimeMs = markTime,
                SweepTimeMs = sweepTime,
                TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                BytesFreed = estimatedBytesFreed,
                CollectionEfficiency = objectsBeforeCollection > 0
                    ? (double)unreachableObjects.Length / objectsBeforeCollection * 100.0
                    : 0,
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }

        // Accumulate cumulative statistics (thread-safe, overflow-protected)
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        AccumulateCumulativeStats(
            objectsBeforeCollection,
            unreachableObjects.Length,
            estimatedBytesFreed,
            markTime,
            sweepTime,
            totalTime);

        return new GCStatistics
        {
            TotalObjects = objectsBeforeCollection,
            LiveObjects = objectsAfterCollection,
            CollectedObjects = unreachableObjects.Length,
            MarkTimeMs = markTime,
            SweepTimeMs = sweepTime,
            TotalTimeMs = totalTime,
            EstimatedBytesFreed = estimatedBytesFreed,
            CollectedByFormat = collectedByFormat,
            FreeObjectTableEntries = freeEntries,
            CollectedObjectDetails = collectedObjectDetails
        };
    }

    /// <summary>
    /// Accumulates statistics from a GC cycle with overflow protection.
    /// </summary>
    private void AccumulateCumulativeStats(
        int objectsExamined,
        int collectedObjects,
        long bytesFreed,
        double markTimeMs,
        double sweepTimeMs,
        double totalTimeMs)
    {
        lock (_cumulativeStatsLock)
        {
            // Increment GC count with overflow protection
            if (_cumulativeGCCount < long.MaxValue)
                _cumulativeGCCount++;

            // Accumulate with overflow protection using checked arithmetic
            _cumulativeObjectsExamined = SafeAddLong(_cumulativeObjectsExamined, objectsExamined);
            _cumulativeCollectedObjects = SafeAddLong(_cumulativeCollectedObjects, collectedObjects);
            _cumulativeBytesFreed = SafeAddLong(_cumulativeBytesFreed, bytesFreed);

            // Accumulate time (double has enough precision for practical use)
            _cumulativeMarkTimeMs += markTimeMs;
            _cumulativeSweepTimeMs += sweepTimeMs;
            _cumulativeTotalTimeMs += totalTimeMs;
        }
    }

    /// <summary>
    /// Safely adds two long values, capping at long.MaxValue on overflow.
    /// </summary>
    private static long SafeAddLong(long a, long b)
    {
        try
        {
            return checked(a + b);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    public GCStatistics GetStatistics()
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

    public GCCumulativeStatistics GetAndResetCumulativeStatistics()
    {
        lock (_cumulativeStatsLock)
        {
            var stats = new GCCumulativeStatistics
            {
                GCCount = _cumulativeGCCount,
                TotalCollectedObjects = _cumulativeCollectedObjects,
                TotalBytesFreed = _cumulativeBytesFreed,
                TotalMarkTimeMs = _cumulativeMarkTimeMs,
                TotalSweepTimeMs = _cumulativeSweepTimeMs,
                TotalGCTimeMs = _cumulativeTotalTimeMs,
                TotalObjectsExamined = _cumulativeObjectsExamined
            };

            // Reset all counters
            _cumulativeGCCount = 0;
            _cumulativeCollectedObjects = 0;
            _cumulativeBytesFreed = 0;
            _cumulativeMarkTimeMs = 0;
            _cumulativeSweepTimeMs = 0;
            _cumulativeTotalTimeMs = 0;
            _cumulativeObjectsExamined = 0;

            return stats;
        }
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
