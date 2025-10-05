namespace Smalltalk80.Interpreter.Memory;

using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Extension of ObjectMemory that replaces ConcurrentDictionary with ObjectTable
/// for efficient O(1) allocation and improved garbage collection.
/// </summary>
public static partial class ObjectMemory
{
    // Replace the concurrent dictionary with the ObjectTable
    private static readonly ObjectTable objectTableNew = new();

    // Replace the old garbage collector with the new one
    private static readonly GarbageCollector garbageCollector = new(objectTableNew);

    // Constants for memory management
    private const int MinimumFreeSpaceThreshold = 1000; // Trigger GC if less than this many free slots
    private const double MaximumUtilizationThreshold = 0.9; // Trigger GC if table is 90% full

    /// <summary>
    /// Allocates a new object using the ObjectTable and returns its OOP.
    /// This method replaces the original AllocateObject implementation.
    /// </summary>
    public static nint AllocateObjectWithTable(
        SObjectRef classRef,
        ObjectFormat format,
        int instanceVarCount,
        int indexedSize)
    {
        // Check if we need to trigger garbage collection
        var stats = objectTableNew.GetStatistics();
        if (stats.FreeListSize < MinimumFreeSpaceThreshold ||
            (double)stats.LiveObjects / stats.Capacity > MaximumUtilizationThreshold)
        {
            if (Program.RuntimeOptions?.EnableGCLogging ?? false)
            {
                Console.WriteLine("[ObjectMemory] Triggering automatic GC due to memory pressure");
            }
            garbageCollector.Collect();
        }

        // Create object data
        var data = new ObjectData(classRef, format, instanceVarCount);

        // Initialize indexed data based on format
        if (indexedSize > 0)
        {
            data.IndexedData = format switch
            {
                ObjectFormat.PointerIndexed => InitializePointerIndexed(indexedSize),
                ObjectFormat.ByteIndexed => new byte[indexedSize],
                ObjectFormat.WordIndexed => new ushort[indexedSize],
                ObjectFormat.CompiledMethod => new byte[indexedSize],
                _ => null
            };
        }

        // Allocate in object table
        int index = objectTableNew.Allocate(data);

        // Convert index to OOP
        var oop = ObjectTable.OopFromIndex(index);

        return oop;
    }

    /// <summary>
    /// Gets object data for the given OOP using the ObjectTable.
    /// This method replaces the original GetObjectData implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectData? GetObjectDataFromTable(nint oop)
    {
        if (oop == 0)
            return null; // nil

        // Check bootstrap map first (for backward compatibility)
        if (bootstrapObjectMap.TryGetValue(oop, out var bootstrapObj))
        {
            // Convert bootstrap object to ObjectData (temporary compatibility layer)
            // This would need proper implementation based on your bootstrap objects
            return null; // Bootstrap objects handled separately
        }

        int index = ObjectTable.IndexFromOop(oop);
        return objectTableNew.Get(index);
    }

    /// <summary>
    /// Checks if an OOP is valid using the ObjectTable.
    /// </summary>
    public static bool IsValidOopInTable(nint oop)
    {
        if (oop == 0)
            return true; // nil is valid

        int index = ObjectTable.IndexFromOop(oop);
        return objectTableNew.Get(index) != null;
    }

    /// <summary>
    /// Performs garbage collection using the new GarbageCollector.
    /// </summary>
    public static void CollectGarbageWithTable(SObjectRef[] roots)
    {
        // Pass roots array directly to the GC
        garbageCollector.SetSpecialObjectsArray(roots);

        // Perform collection
        var stats = garbageCollector.Collect();

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] GC completed: {stats}");
        }
    }

    /// <summary>
    /// Performs garbage collection with additional context roots.
    /// </summary>
    public static void CollectGarbageWithContext(SObjectRef[] specialObjects, IEnumerable<SObjectRef> contextRoots)
    {
        garbageCollector.SetSpecialObjectsArray(specialObjects);

        var stats = garbageCollector.CollectWithContextRoots(contextRoots);

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] GC with context completed: {stats}");
        }
    }

    /// <summary>
    /// Implements the become: primitive using the ObjectTable.
    /// </summary>
    public static void BecomeWithTable(nint oop1, nint oop2)
    {
        if (oop1 == 0 || oop2 == 0)
            throw new ArgumentException("Cannot become: with nil");

        int index1 = ObjectTable.IndexFromOop(oop1);
        int index2 = ObjectTable.IndexFromOop(oop2);

        var data1 = objectTableNew.Get(index1);
        var data2 = objectTableNew.Get(index2);

        if (data1 == null || data2 == null)
            throw new InvalidOperationException("Invalid OOP in become:");

        // Swap the object data
        objectTableNew.Set(index1, data2);
        objectTableNew.Set(index2, data1);

        // All existing SObjectRef instances pointing to oop1 now resolve to data2
        // and vice versa. This is the magic of the indirection!
    }

    /// <summary>
    /// Gets memory statistics including ObjectTable information.
    /// </summary>
    public static MemoryStatistics GetMemoryStatistics()
    {
        var tableStats = objectTableNew.GetStatistics();
        var gcStats = garbageCollector.GetStatistics();

        return new MemoryStatistics(
            tableStats.Capacity,
            tableStats.LiveObjects,
            tableStats.FreeListSize,
            tableStats.NextIndex,
            gcStats.TotalCollections,
            gcStats.TotalObjectsCollected,
            gcStats.TotalBytesFreed,
            gcStats.LastCollectionDurationMs,
            (double)tableStats.LiveObjects / tableStats.Capacity,
            (double)tableStats.FreeListSize / tableStats.Capacity);
    }

    /// <summary>
    /// Validates the consistency of the memory system.
    /// </summary>
    public static bool ValidateMemoryConsistency()
    {
        objectTableNew.ValidateConsistency();
        bool tableConsistent = true;
        bool gcReachable = garbageCollector.ValidateReachability();

        if (!tableConsistent)
        {
            Console.WriteLine("[ObjectMemory] ERROR: Object table consistency check failed");
        }

        if (!gcReachable)
        {
            Console.WriteLine("[ObjectMemory] ERROR: GC reachability check failed");
        }

        return tableConsistent && gcReachable;
    }

    /// <summary>
    /// Forces a garbage collection.
    /// </summary>
    public static GarbageCollectionStatistics ForceGarbageCollection()
    {
        return garbageCollector.ForceCollection();
    }

    /// <summary>
    /// Performs an incremental garbage collection.
    /// </summary>
    /// <param name="milliseconds">Maximum time to spend collecting.</param>
    /// <returns>Statistics about the collection.</returns>
    public static GarbageCollectionStatistics IncrementalGarbageCollection(int milliseconds)
    {
        return garbageCollector.IncrementalCollect(milliseconds);
    }

    /// <summary>
    /// Enumerates all live objects in memory.
    /// </summary>
    /// <returns>An enumerable of live object OOPs.</returns>
    public static IEnumerable<nint> EnumerateLiveObjects()
    {
        foreach (var (index, data) in objectTableNew.EnumerateLiveObjects())
        {
            yield return ObjectTable.OopFromIndex(index);
        }
    }

    /// <summary>
    /// Gets detailed object table statistics.
    /// </summary>
    public static ObjectTableStatistics GetObjectTableStatistics()
    {
        return garbageCollector.GetTableStatistics();
    }

    /// <summary>
    /// Clears all object mappings (for testing/reset only).
    /// This method extends the original Clear method.
    /// </summary>
    internal static void ClearWithTable()
    {
        objectTableNew.Clear();
        garbageCollector.ResetStatistics();
        // Symbol table is now in Smalltalk heap (Symbol class -> classPool -> #USTable)
        // bootstrapSymbolTable in ObjectMemory.cs is only for early bootstrap
        methodCache.Clear();
        bootstrapObjectMap.Clear();
    }
}

/// <summary>
/// Comprehensive memory statistics.
/// </summary>
public readonly record struct MemoryStatistics(
    int TableCapacity,
    int LiveObjects,
    int FreeListSize,
    int NextIndex,
    long TotalCollections,
    long TotalObjectsCollected,
    long TotalBytesFreed,
    long LastGCDurationMs,
    double Utilization,
    double FragmentationRatio)
{
    /// <summary>
    /// Returns a string representation of the memory statistics.
    /// </summary>
    public override string ToString() =>
        $"""
        Memory Statistics:
          Object Table:
            Capacity:         {TableCapacity:N0}
            Live Objects:     {LiveObjects:N0}
            Free List:        {FreeListSize:N0}
            Next Index:       {NextIndex:N0}
            Utilization:      {Utilization:P2}
            Fragmentation:    {FragmentationRatio:P2}

          Garbage Collector:
            Total Collections:    {TotalCollections:N0}
            Objects Collected:    {TotalObjectsCollected:N0}
            Memory Freed:         {TotalBytesFreed:N0} bytes
            Last GC Duration:     {LastGCDurationMs}ms
        """
    ;
}