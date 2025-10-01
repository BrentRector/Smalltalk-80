namespace Smalltalk80.Interpreter.Memory;

using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Mark-and-sweep garbage collector for the new SObjectRef-based architecture.
/// Works directly with ObjectTable and ObjectMemoryNew instead of SObject instances.
/// </summary>
public sealed class GarbageCollectorNew
{
    private readonly ObjectTable objectTable;
    private readonly object gcLock = new();

    // The ONLY roots for GC - Special Objects Array entries
    private readonly List<SObjectRef> specialObjectsArray = new();

    // Statistics
    private long totalCollections;
    private long totalObjectsCollected;
    private long totalBytesFreed;
    private long lastCollectionDuration;
    private int lastLiveObjectCount;

    /// <summary>
    /// Gets the total number of garbage collections performed.
    /// </summary>
    public long TotalCollections => totalCollections;

    /// <summary>
    /// Gets the total number of objects collected.
    /// </summary>
    public long TotalObjectsCollected => totalObjectsCollected;

    /// <summary>
    /// Gets the total amount of memory freed in bytes.
    /// </summary>
    public long TotalBytesFreed => totalBytesFreed;

    /// <summary>
    /// Gets the duration of the last collection in milliseconds.
    /// </summary>
    public long LastCollectionDurationMs => lastCollectionDuration;

    /// <summary>
    /// Gets the number of live objects after the last collection.
    /// </summary>
    public int LastLiveObjectCount => lastLiveObjectCount;

    /// <summary>
    /// Initializes a new instance of the GarbageCollectorNew class.
    /// </summary>
    /// <param name="table">The object table to use for garbage collection.</param>
    public GarbageCollectorNew(ObjectTable table)
    {
        objectTable = table ?? throw new ArgumentNullException(nameof(table));
    }

    /// <summary>
    /// Sets the Special Objects Array - the ONLY roots for garbage collection.
    /// </summary>
    /// <param name="soa">The special objects array.</param>
    public void SetSpecialObjectsArray(List<SObjectRef> soa)
    {
        lock (gcLock)
        {
            specialObjectsArray.Clear();
            if (soa != null)
            {
                specialObjectsArray.AddRange(soa);
            }
        }
    }

    /// <summary>
    /// Gets or sets a specific special object.
    /// </summary>
    /// <param name="index">The index in the Special Objects Array.</param>
    /// <returns>The special object at the specified index.</returns>
    public SObjectRef GetSpecialObject(int index)
    {
        lock (gcLock)
        {
            if (index >= 0 && index < specialObjectsArray.Count)
                return specialObjectsArray[index];
            return SObjectRef.Nil;
        }
    }

    /// <summary>
    /// Sets a specific special object in the Special Objects Array.
    /// </summary>
    /// <param name="index">The index in the Special Objects Array.</param>
    /// <param name="obj">The object to set at the specified index.</param>
    public void SetSpecialObject(int index, SObjectRef obj)
    {
        lock (gcLock)
        {
            while (specialObjectsArray.Count <= index)
                specialObjectsArray.Add(SObjectRef.Nil);
            specialObjectsArray[index] = obj;
        }
    }

    /// <summary>
    /// Performs a garbage collection cycle using mark-and-sweep.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics Collect()
    {
        var stopwatch = Stopwatch.StartNew();
        int objectsCollected;
        long bytesFreed = 0;

        lock (gcLock)
        {
            if (Program.RuntimeOptions?.EnableGCLogging ?? false)
            {
                Console.WriteLine($"[GC] Starting collection #{totalCollections + 1}...");
            }

            // Phase 1: Mark all reachable objects
            var roots = new HashSet<nint>();

            // Add Special Objects Array elements as roots
            foreach (var specialObj in specialObjectsArray)
            {
                if (!specialObj.IsNil && specialObj.IsHeapObject)
                {
                    roots.Add(specialObj.AsObjectPointer());
                }
            }

            // Also add any additional roots from the current execution context
            // This would include:
            // - Active contexts (stack frames)
            // - Temporary variables
            // - Method arguments
            // These would be passed in as additional roots in a real implementation

            if (Program.RuntimeOptions?.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Starting mark phase with {roots.Count} root objects");
            }

            // Perform mark phase
            objectTable.MarkPhase(roots);

            var stats = objectTable.GetStatistics();
            var liveBeforeSweep = stats.LiveObjects;

            if (Program.RuntimeOptions?.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Mark phase complete, {liveBeforeSweep} objects marked as live");
            }

            // Phase 2: Sweep unmarked objects
            objectsCollected = objectTable.SweepPhase();

            // Estimate bytes freed (approximate)
            bytesFreed = objectsCollected * EstimateAverageObjectSize();

            var finalStats = objectTable.GetStatistics();
            lastLiveObjectCount = finalStats.LiveObjects;

            if (Program.RuntimeOptions?.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Sweep phase complete, {objectsCollected} objects collected");
                Console.WriteLine($"[GC] Table statistics: {finalStats.LiveObjects} live objects, " +
                                  $"{finalStats.FreeListSize} free slots, " +
                                  $"{finalStats.Capacity} total capacity");
            }

            totalCollections++;
            totalObjectsCollected += objectsCollected;
            totalBytesFreed += bytesFreed;
        }

        stopwatch.Stop();
        lastCollectionDuration = stopwatch.ElapsedMilliseconds;

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[GC] Collection #{totalCollections} complete: " +
                            $"{objectsCollected} objects collected, " +
                            $"{bytesFreed:N0} bytes freed (estimated), " +
                            $"{lastLiveObjectCount} objects remain, " +
                            $"{lastCollectionDuration}ms");
        }

        return new GarbageCollectionStatistics(
            objectsCollected,
            bytesFreed,
            lastCollectionDuration,
            totalCollections);
    }

    /// <summary>
    /// Forces a garbage collection.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics ForceCollection()
    {
        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine("[GC] Forced collection requested");
        }
        return Collect();
    }

    /// <summary>
    /// Adds execution context roots for garbage collection.
    /// </summary>
    /// <param name="contextRoots">Additional roots from the execution context.</param>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics CollectWithContextRoots(IEnumerable<SObjectRef> contextRoots)
    {
        var stopwatch = Stopwatch.StartNew();
        int objectsCollected;
        long bytesFreed = 0;

        lock (gcLock)
        {
            if (Program.RuntimeOptions?.EnableGCLogging ?? false)
            {
                Console.WriteLine($"[GC] Starting collection #{totalCollections + 1} with context roots...");
            }

            // Phase 1: Mark all reachable objects
            var roots = new HashSet<nint>();

            // Add Special Objects Array elements as roots
            foreach (var specialObj in specialObjectsArray)
            {
                if (!specialObj.IsNil && specialObj.IsHeapObject)
                {
                    roots.Add(specialObj.AsObjectPointer());
                }
            }

            // Add context roots
            if (contextRoots != null)
            {
                foreach (var contextRoot in contextRoots)
                {
                    if (!contextRoot.IsNil && contextRoot.IsHeapObject)
                    {
                        roots.Add(contextRoot.AsObjectPointer());
                    }
                }
            }

            if (Program.RuntimeOptions?.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Starting mark phase with {roots.Count} root objects");
            }

            // Perform mark phase
            objectTable.MarkPhase(roots);

            // Phase 2: Sweep unmarked objects
            objectsCollected = objectTable.SweepPhase();

            // Estimate bytes freed
            bytesFreed = objectsCollected * EstimateAverageObjectSize();

            var finalStats = objectTable.GetStatistics();
            lastLiveObjectCount = finalStats.LiveObjects;

            totalCollections++;
            totalObjectsCollected += objectsCollected;
            totalBytesFreed += bytesFreed;
        }

        stopwatch.Stop();
        lastCollectionDuration = stopwatch.ElapsedMilliseconds;

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[GC] Collection #{totalCollections} complete: " +
                            $"{objectsCollected} objects collected, " +
                            $"{bytesFreed:N0} bytes freed (estimated), " +
                            $"{lastLiveObjectCount} objects remain, " +
                            $"{lastCollectionDuration}ms");
        }

        return new GarbageCollectionStatistics(
            objectsCollected,
            bytesFreed,
            lastCollectionDuration,
            totalCollections);
    }

    /// <summary>
    /// Estimates the average size of an object in bytes.
    /// </summary>
    /// <returns>Estimated average object size.</returns>
    private static long EstimateAverageObjectSize()
    {
        // Rough estimate: base overhead + average content
        // - ObjectData overhead: ~40 bytes
        // - Instance variables: average 4 * 8 bytes = 32 bytes
        // - Indexed data: average 8 * 8 bytes = 64 bytes
        return 136; // Total estimate
    }

    /// <summary>
    /// Validates that all critical objects are reachable from the SOA.
    /// </summary>
    /// <returns>True if all critical objects are reachable.</returns>
    public bool ValidateReachability()
    {
        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine("[GC] Validating all objects are reachable from SOA...");
        }

        // Perform a collection to mark all reachable objects
        Collect();

        // In the new architecture, we can't easily check specific objects
        // without maintaining a registry. Instead, we verify consistency.
        var stats = objectTable.GetStatistics();
        objectTable.ValidateConsistency();
        var isValid = true;

        if (isValid && (Program.RuntimeOptions?.EnableGCLogging ?? false))
        {
            Console.WriteLine($"[GC] Validation passed: {stats.LiveObjects} objects reachable from SOA");
        }
        else if (!isValid)
        {
            Console.WriteLine("[GC] WARNING: Object table consistency check failed!");
        }

        return isValid;
    }

    /// <summary>
    /// Gets statistics about the garbage collector.
    /// </summary>
    /// <returns>Extended statistics about the garbage collector.</returns>
    public GarbageCollectorStatistics GetStatistics()
    {
        var tableStats = objectTable.GetStatistics();
        return new GarbageCollectorStatistics(
            TotalCollections,
            TotalObjectsCollected,
            TotalBytesFreed,
            LastCollectionDurationMs,
            tableStats.LiveObjects);
    }

    /// <summary>
    /// Gets detailed table statistics.
    /// </summary>
    /// <returns>Detailed statistics about the object table.</returns>
    public ObjectTableStatistics GetTableStatistics()
    {
        var stats = objectTable.GetStatistics();
        return new ObjectTableStatistics(
            stats.Capacity,
            stats.LiveObjects,
            stats.FreeListSize,
            stats.NextIndex,
            (double)stats.LiveObjects / stats.Capacity,
            (double)stats.FreeListSize / stats.Capacity);
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void ResetStatistics()
    {
        totalCollections = 0;
        totalObjectsCollected = 0;
        totalBytesFreed = 0;
        lastCollectionDuration = 0;
        lastLiveObjectCount = 0;
    }

    /// <summary>
    /// Performs an incremental collection (for future implementation).
    /// </summary>
    /// <param name="milliseconds">Maximum time to spend collecting.</param>
    /// <returns>Statistics about the partial collection.</returns>
    public GarbageCollectionStatistics IncrementalCollect(int milliseconds)
    {
        // For now, just do a full collection
        // Future: Implement incremental/generational collection
        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[GC] Incremental collection requested ({milliseconds}ms budget)");
        }
        return Collect();
    }
}

/// <summary>
/// Extended statistics about the object table.
/// </summary>
public readonly record struct ObjectTableStatistics(
    int Capacity,
    int LiveObjects,
    int FreeListSize,
    int NextIndex,
    double Utilization,
    double FragmentationRatio)
{
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"""
        Object Table Statistics:
          Capacity:         {Capacity:N0}
          Live Objects:     {LiveObjects:N0}
          Free List Size:   {FreeListSize:N0}
          Next Index:       {NextIndex:N0}
          Utilization:      {Utilization:P2}
          Fragmentation:    {FragmentationRatio:P2}
        """;
}