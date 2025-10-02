namespace Smalltalk80.Interpreter.Memory;

using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Mark-and-sweep garbage collector for the Smalltalk-80 interpreter.
///
/// ARCHITECTURE:
/// - GC is a client of ObjectTable - no special integration needed
/// - The only root is the Special Objects Array (SOA), which stores raw OOPs
/// - Mark phase: Starting from SOA, recursively mark all reachable objects
/// - Sweep phase: Walk all ObjectTable entries, call ObjectTable.Free() for unmarked ones
///
/// CRITICAL DESIGN:
/// - The GC works with OOPs (nint), not SObjectRef, for its internal operations
/// - ObjectTable handles all allocation/deallocation details
/// - Clean separation: GC knows nothing about ObjectTable's internal free list, segmentation, etc.
/// </summary>
public sealed class GarbageCollector
{
    private readonly ObjectTable objectTable;
    private readonly Stack<nint> gcStack = new();
    private readonly object gcLock = new();

    // The ONLY root for GC - stores raw OOPs
    private List<nint> specialObjectsArray = new();

    // Statistics
    private long totalCollections;
    private long totalObjectsCollected;
    private long lastCollectionDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarbageCollector"/> class.
    /// </summary>
    /// <param name="objectTable">The object table to manage.</param>
    public GarbageCollector(ObjectTable objectTable)
    {
        this.objectTable = objectTable ?? throw new ArgumentNullException(nameof(objectTable));
    }

    /// <summary>
    /// Gets the total number of garbage collections performed.
    /// </summary>
    public long TotalCollections => totalCollections;

    /// <summary>
    /// Gets the total number of objects collected.
    /// </summary>
    public long TotalObjectsCollected => totalObjectsCollected;

    /// <summary>
    /// Gets the duration of the last collection in milliseconds.
    /// </summary>
    public long LastCollectionDurationMs => lastCollectionDuration;

    /// <summary>
    /// Sets the Special Objects Array - the ONLY root for garbage collection.
    /// </summary>
    /// <param name="soa">The special objects array as OOPs.</param>
    public void SetSpecialObjectsArray(List<nint> soa)
    {
        lock (gcLock)
        {
            specialObjectsArray = soa ?? new List<nint>();
        }
    }

    /// <summary>
    /// Sets the Special Objects Array from SObjectRefs.
    /// </summary>
    /// <param name="refs">The special objects array as SObjectRefs.</param>
    public void SetSpecialObjectsArrayFromRefs(List<SObjectRef> refs)
    {
        lock (gcLock)
        {
            specialObjectsArray = new List<nint>();
            foreach (var objRef in refs)
            {
                if (!objRef.IsNil && objRef.IsHeapObject)
                {
                    specialObjectsArray.Add(objRef.AsObjectPointer());
                }
                else if (!objRef.IsNil && !objRef.IsSmallInteger)
                {
                    // Store immediates as 0 (they don't need GC)
                    specialObjectsArray.Add(0);
                }
                else
                {
                    specialObjectsArray.Add(0); // nil or SmallInteger
                }
            }
        }
    }

    /// <summary>
    /// Performs a garbage collection cycle.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics Collect()
    {
        var stopwatch = Stopwatch.StartNew();

        lock (gcLock)
        {
            // Get total object count by counting all non-free entries
            int totalObjects = CountAllocatedObjects();

            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Console.WriteLine($"\n[GC] Starting collection #{totalCollections + 1}");
                Console.WriteLine($"[GC] Total allocated objects: {totalObjects}");
            }

            // MARK PHASE
            ClearAllMarkBits();
            MarkFromRoots();
            int reachableObjects = CountMarkedObjects();

            // SWEEP PHASE
            int collected = SweepUnmarkedObjects();

            // Update statistics
            totalCollections++;
            totalObjectsCollected += collected;

            stopwatch.Stop();
            lastCollectionDuration = stopwatch.ElapsedMilliseconds;

            // Log results
            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Console.WriteLine($"[GC] Reachable objects: {reachableObjects}");
                Console.WriteLine($"[GC] Objects collected: {collected}");
                Console.WriteLine($"[GC] Collection time: {lastCollectionDuration}ms");

                if (collected == 0)
                {
                    Console.WriteLine("[GC] ✓ All objects reachable - cold start initialization is correct");
                }
                else
                {
                    Console.WriteLine($"[GC] WARNING: Collected {collected} objects after cold start!");
                    Console.WriteLine("[GC] This indicates a cold start initialization error - all objects should be reachable from SOA");
                }
            }

            return new GarbageCollectionStatistics(collected, 0, lastCollectionDuration, totalCollections);
        }
    }

    /// <summary>
    /// Forces a garbage collection.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics ForceCollection()
    {
        return Collect();
    }

    /// <summary>
    /// Clears all mark bits in the object table.
    /// </summary>
    private void ClearAllMarkBits()
    {
        // Iterate through all allocated objects and clear their mark bits
        foreach (var (index, data) in objectTable.EnumerateLiveObjects())
        {
            data.Marked = false;
        }
    }

    /// <summary>
    /// Marks all objects reachable from the roots (Special Objects Array).
    /// </summary>
    private void MarkFromRoots()
    {
        gcStack.Clear();

        // Push all SOA entries onto stack (skip nil and SmallIntegers)
        foreach (var oop in specialObjectsArray)
        {
            if (oop != 0 && (oop & 0x01) == 0) // Not nil and not SmallInteger (LSB = 0 for objects)
            {
                gcStack.Push(oop);
            }
        }

        // Recursively mark
        while (gcStack.Count > 0)
        {
            var oop = gcStack.Pop();
            MarkObject(oop);
        }
    }

    /// <summary>
    /// Marks an object and traces its references.
    /// </summary>
    /// <param name="oop">The OOP of the object to mark.</param>
    private void MarkObject(nint oop)
    {
        // Skip nil and immediates
        if (oop == 0 || (oop & 0x01) == 1) // nil or SmallInteger
            return;

        // Get object data from ObjectTable
        int index = ObjectTable.IndexFromOop(oop);
        var data = objectTable.Get(index);

        if (data == null)
            return; // Invalid OOP

        // If already marked, skip (already visited)
        if (data.Marked)
            return;

        // Mark this object
        data.Marked = true;

        // Trace class reference
        if (!data.ClassRef.IsNil && data.ClassRef.IsHeapObject)
        {
            gcStack.Push(data.ClassRef.AsObjectPointer());
        }

        // Trace instance variables (always SObjectRef[])
        foreach (var fieldRef in data.InstanceVariables)
        {
            if (!fieldRef.IsNil && fieldRef.IsHeapObject)
            {
                gcStack.Push(fieldRef.AsObjectPointer());
            }
        }

        // Trace indexed data based on format
        switch (data.Format)
        {
            case ObjectFormat.PointerIndexed:
                if (data.IndexedData is SObjectRef[] pointers)
                {
                    foreach (var ptr in pointers)
                    {
                        if (!ptr.IsNil && ptr.IsHeapObject)
                        {
                            gcStack.Push(ptr.AsObjectPointer());
                        }
                    }
                }
                break;

            case ObjectFormat.CompiledMethod:
                // CompiledMethod has literals in instance variables (already handled above)
                // Bytecodes are in IndexedData as byte[] which contains no pointers
                break;

            // ByteIndexed, WordIndexed have no pointers in their indexed data
            case ObjectFormat.ByteIndexed:
            case ObjectFormat.WordIndexed:
            case ObjectFormat.FixedPointer:
                // No additional pointers to trace
                break;
        }
    }

    /// <summary>
    /// Sweeps all unmarked objects by freeing them in the object table.
    /// </summary>
    /// <returns>The number of objects collected.</returns>
    private int SweepUnmarkedObjects()
    {
        int collected = 0;

        // Collect indices to free (can't modify while iterating)
        var indicesToFree = new List<int>();

        // Iterate through all allocated objects
        foreach (var (index, data) in objectTable.EnumerateLiveObjects())
        {
            if (!data.Marked)
            {
                indicesToFree.Add(index);
            }
        }

        // Free all unmarked objects
        foreach (var index in indicesToFree)
        {
            objectTable.Free(index); // Let ObjectTable handle the details
            collected++;
        }

        return collected;
    }

    private string GetClassName(SObjectRef classRef)
    {
        try
        {
            // Try to get the class name by reading the Name instance variable
            var nameRef = classRef.GetInstanceVariable(6); // Name field in Class
            if (!nameRef.IsNil && nameRef.IsHeapObject)
            {
                var data = objectTable.Get((int)(nameRef.AsObjectPointer() >> 2));
                if (data != null && data.Format == ObjectFormat.ByteIndexed && data.IndexedData is byte[] bytes)
                {
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
        }
        catch
        {
            // Ignore errors during debugging
        }
        return "?";
    }

    /// <summary>
    /// Counts the total number of allocated objects in the object table.
    /// </summary>
    /// <returns>The count of allocated objects.</returns>
    private int CountAllocatedObjects()
    {
        return objectTable.GetAllocatedCount();
    }

    /// <summary>
    /// Counts the number of marked objects after mark phase.
    /// </summary>
    /// <returns>The count of marked objects.</returns>
    private int CountMarkedObjects()
    {
        int count = 0;
        foreach (var (_, data) in objectTable.EnumerateLiveObjects())
        {
            if (data.Marked)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Validates that all objects are reachable from the SOA.
    /// This should be run after cold start to ensure proper setup.
    /// </summary>
    /// <returns>True if validation passes.</returns>
    public bool ValidateReachability()
    {
        if (Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("\n[GC] Validating all objects are reachable from SOA...");
        }

        // Perform a collection to mark all reachable objects
        var stats = Collect();

        bool isValid = stats.ObjectsCollected == 0;

        if (isValid && Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[GC] ✓ Validation passed: All objects are reachable from SOA");
        }
        else if (!isValid)
        {
            Console.WriteLine($"[GC] ✗ Validation failed: {stats.ObjectsCollected} objects were not reachable from SOA");
        }

        return isValid;
    }

    /// <summary>
    /// Gets statistics about the garbage collector.
    /// </summary>
    public GarbageCollectorStatistics GetStatistics()
    {
        lock (gcLock)
        {
            int liveObjects = CountAllocatedObjects();
            return new GarbageCollectorStatistics(
                TotalCollections,
                TotalObjectsCollected,
                0, // BytesFreed not tracked in new architecture
                LastCollectionDurationMs,
                liveObjects);
        }
    }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void ResetStatistics()
    {
        totalCollections = 0;
        totalObjectsCollected = 0;
        lastCollectionDuration = 0;
    }
}

/// <summary>
/// Statistics about a garbage collection cycle.
/// </summary>
public readonly record struct GarbageCollectionStatistics(
    int ObjectsCollected,
    long BytesFreed,
    long DurationMs,
    long TotalCollections)
{
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"GC[Collection #{TotalCollections}: {ObjectsCollected} objects collected, {DurationMs}ms]";
}

/// <summary>
/// Extended garbage collector statistics.
/// </summary>
public readonly record struct GarbageCollectorStatistics(
    long TotalCollections,
    long TotalObjectsCollected,
    long TotalBytesFreed,
    long LastCollectionDurationMs,
    int LiveObjectCount)
{
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"""
        Garbage Collector Statistics:
          Total Collections:     {TotalCollections:N0}
          Objects Collected:     {TotalObjectsCollected:N0}
          Memory Freed:          {TotalBytesFreed:N0} bytes
          Last Collection Time:  {LastCollectionDurationMs}ms
          Live Objects:          {LiveObjectCount:N0}
        """;
}