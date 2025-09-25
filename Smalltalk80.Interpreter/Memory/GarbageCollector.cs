using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Memory;

/// <summary>
/// Mark-and-sweep garbage collector for Smalltalk-80 objects.
/// Uses the Special Objects Array and explicit roots for tracing.
/// </summary>
public sealed class GarbageCollector
{
    private readonly ObjectMemory objectMemory;
    private readonly HashSet<OOP> marked = [];
    private readonly HashSet<OOP> explicitRoots = [];
    private readonly List<GCStatistics> history = [];
    
    // GC performance metrics
    private int totalCollections;
    private long totalBytesFreed;
    private long totalObjectsCollected;
    private TimeSpan totalGCTime;
    
    /// <summary>
    /// Creates a new garbage collector for the specified object memory.
    /// </summary>
    /// <param name="objectMemory">The object memory to manage.</param>
    public GarbageCollector(ObjectMemory objectMemory)
    {
        this.objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
    }
    
    /// <summary>
    /// Gets the total number of collections performed.
    /// </summary>
    public int TotalCollections => totalCollections;
    
    /// <summary>
    /// Gets the total bytes freed across all collections.
    /// </summary>
    public long TotalBytesFreed => totalBytesFreed;
    
    /// <summary>
    /// Gets the total objects collected across all collections.
    /// </summary>
    public long TotalObjectsCollected => totalObjectsCollected;
    
    /// <summary>
    /// Gets the total time spent in garbage collection.
    /// </summary>
    public TimeSpan TotalGCTime => totalGCTime;
    
    /// <summary>
    /// Gets the collection history (last 10 collections).
    /// </summary>
    public IReadOnlyList<GCStatistics> History => history.AsReadOnly();
    
    /// <summary>
    /// Adds an explicit GC root that will be traced during collection.
    /// </summary>
    /// <param name="root">The root object to add.</param>
    public void AddRoot(OOP root)
    {
        if (!root.IsNil && !root.IsSmallInteger)
        {
            explicitRoots.Add(root);
        }
    }
    
    /// <summary>
    /// Removes an explicit GC root.
    /// </summary>
    /// <param name="root">The root object to remove.</param>
    public void RemoveRoot(OOP root)
    {
        explicitRoots.Remove(root);
    }

    /// <summary>
    /// Performs a full garbage collection cycle.
    /// </summary>
    /// <param name="specialObjectsArray">The Special Objects Array as primary GC root.</param>
    /// <param name="options">Optional collection options.</param>
    /// <returns>Statistics about the collection.</returns>
    public GCStatistics Collect(OOP[] specialObjectsArray, GCOptions? options = null)
    {
        options ??= GCOptions.Default;
        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;
        
        // Get memory stats before collection
        var memoryBefore = CalculateMemoryUsage();
        var objectsBefore = CountObjects();
        
        // Clear previous marks
        marked.Clear();
        ClearAllMarks();
        
        // Mark phase - trace from all roots
        MarkPhase(specialObjectsArray, options);
        var markedCount = marked.Count;
        
        // Sweep phase - collect unmarked objects
        var collectedCount = SweepPhase(options);
        
        // Optional compaction
        if (options.Compact)
        {
            objectMemory.Compact();
        }
        
        // Calculate statistics
        stopwatch.Stop();
        var memoryAfter = CalculateMemoryUsage();
        var objectsAfter = CountObjects();
        
        var stats = new GCStatistics
        {
            CollectionNumber = ++totalCollections,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            Duration = stopwatch.Elapsed,
            MarkedCount = markedCount,
            CollectedCount = collectedCount,
            SurvivingCount = objectsAfter,
            BytesFreed = memoryBefore - memoryAfter,
            MemoryBefore = memoryBefore,
            MemoryAfter = memoryAfter,
            RootsCount = explicitRoots.Count + specialObjectsArray.Length,
            Generation = options.Generation
        };
        
        // Update totals
        totalBytesFreed += stats.BytesFreed;
        totalObjectsCollected += collectedCount;
        totalGCTime += stopwatch.Elapsed;
        
        // Keep history (last 10 collections)
        history.Add(stats);
        if (history.Count > 10)
        {
            history.RemoveAt(0);
        }
        
        // Log if verbose
        if (options.Verbose)
        {
            Console.WriteLine($"[GC #{stats.CollectionNumber}] {stats}");
        }
        
        return stats;
    }

    /// <summary>
    /// Clears all mark flags from objects.
    /// </summary>
    private void ClearAllMarks()
    {
        foreach (var (_, layout) in objectMemory.EnumerateObjects())
        {
            layout.Header.IsMarked = false;
        }
    }
    
    /// <summary>
    /// Performs the mark phase of garbage collection.
    /// </summary>
    private void MarkPhase(OOP[] specialObjectsArray, GCOptions options)
    {
        var markStack = new Stack<OOP>(1024); // Use explicit stack to avoid recursion
        
        // Mark from Special Objects Array (primary root)
        foreach (var root in specialObjectsArray)
        {
            if (!root.IsNil && !root.IsSmallInteger)
            {
                markStack.Push(root);
            }
        }
        
        // Mark from explicit roots
        foreach (var root in explicitRoots)
        {
            if (!root.IsNil && !root.IsSmallInteger)
            {
                markStack.Push(root);
            }
        }
        
        // Process mark stack iteratively to avoid stack overflow
        while (markStack.Count > 0)
        {
            var oop = markStack.Pop();
            
            // Skip if already marked or not an object
            if (oop.IsSmallInteger || oop.IsNil || marked.Contains(oop))
                continue;
            
            // Get the object layout
            var layout = objectMemory.GetObject(oop);
            if (layout == null)
                continue;
            
            // Skip if permanent (never collected)
            if (layout.Header.IsPermanent)
                continue;
            
            // Mark this object
            marked.Add(oop);
            layout.Header.IsMarked = true;
            
            // Push class for marking
            if (!layout.Header.ClassOOP.IsNil && !layout.Header.ClassOOP.IsSmallInteger)
            {
                markStack.Push(layout.Header.ClassOOP);
            }
            
            // If object has pointers, push all referenced objects
            if (layout.HasPointers)
            {
                foreach (var field in layout.Fields)
                {
                    if (!field.IsSmallInteger && !field.IsNil)
                    {
                        markStack.Push(field);
                    }
                }
            }
        }
        
        if (options.Verbose)
        {
            Console.WriteLine($"[GC] Mark phase completed: {marked.Count} objects marked");
        }
    }

    /// <summary>
    /// Performs the sweep phase of garbage collection.
    /// </summary>
    /// <returns>Number of objects collected.</returns>
    private int SweepPhase(GCOptions options)
    {
        var toRemove = new List<uint>();
        var collectedBytes = 0L;
        
        // Find all unmarked objects
        foreach (var (oop, layout) in objectMemory.EnumerateObjects())
        {
            if (!layout.Header.IsMarked && !layout.Header.IsPermanent)
            {
                // Calculate memory being freed
                collectedBytes += CalculateObjectSize(layout);
                
                // Log first few collected objects in verbose mode
                if (options.Verbose && toRemove.Count < 5)
                {
                    LogCollectedObject(oop, layout);
                }
                
                toRemove.Add(oop.ObjectIndex);
            }
        }
        
        // Remove unmarked objects
        foreach (var index in toRemove)
        {
            objectMemory.RemoveObject(index);
        }
        
        if (options.Verbose)
        {
            Console.WriteLine($"[GC] Sweep phase completed: {toRemove.Count} objects collected, {collectedBytes:N0} bytes freed");
        }
        
        return toRemove.Count;
    }

    /// <summary>
    /// Logs details about a collected object for debugging.
    /// </summary>
    private void LogCollectedObject(OOP oop, ObjectLayout layout)
    {
        Console.WriteLine($"  Collecting: OOP={oop}, Class={layout.Header.ClassOOP}, Size={layout.Fields.Length}");
        
        // Try to identify the class name
        if (!layout.Header.ClassOOP.IsNil)
        {
            var classObj = objectMemory.GetObject(layout.Header.ClassOOP);
            if (classObj != null && classObj.Fields.Length > 6)
            {
                var nameOop = classObj.Fields[6]; // name field at index 6
                var className = ExtractString(nameOop);
                if (!string.IsNullOrEmpty(className))
                {
                    Console.WriteLine($"    Class: {className}");
                }
            }
        }
    }
    
    /// <summary>
    /// Extracts a string from a byte array object.
    /// </summary>
    private string? ExtractString(OOP stringOop)
    {
        if (stringOop.IsNil || stringOop.IsSmallInteger)
            return null;
        
        var obj = objectMemory.GetObject(stringOop);
        if (obj == null || !obj.IsBytes)
            return null;
        
        var bytes = new List<byte>();
        foreach (var field in obj.Fields)
        {
            if (field.IsSmallInteger)
                bytes.Add((byte)field.SmallIntegerValue);
        }
        
        return bytes.Count > 0 ? System.Text.Encoding.UTF8.GetString(bytes.ToArray()) : null;
    }
    
    /// <summary>
    /// Calculates the memory size of an object in bytes.
    /// </summary>
    private long CalculateObjectSize(ObjectLayout layout)
    {
        // Header size + fields size
        const int headerSize = 16; // Estimated header overhead
        var fieldsSize = layout.Fields.Length * sizeof(uint); // Each OOP is 4 bytes
        return headerSize + fieldsSize;
    }
    
    /// <summary>
    /// Calculates total memory usage.
    /// </summary>
    private long CalculateMemoryUsage()
    {
        var totalBytes = 0L;
        foreach (var (_, layout) in objectMemory.EnumerateObjects())
        {
            totalBytes += CalculateObjectSize(layout);
        }
        return totalBytes;
    }
    
    /// <summary>
    /// Counts the total number of objects.
    /// </summary>
    private int CountObjects()
    {
        return objectMemory.EnumerateObjects().Count();
    }

    /// <summary>
    /// Forces a full garbage collection.
    /// </summary>
    public void ForceFullCollection(OOP[] specialObjectsArray)
    {
        Collect(specialObjectsArray, new GCOptions { Generation = 2, Verbose = true, Compact = true });
    }
    
    /// <summary>
    /// Checks if an object would be collected in the next GC.
    /// </summary>
    public bool WouldBeCollected(OOP oop, OOP[] specialObjectsArray)
    {
        if (oop.IsSmallInteger || oop.IsNil)
            return false;
        
        var layout = objectMemory.GetObject(oop);
        if (layout == null || layout.Header.IsPermanent)
            return false;
        
        // Perform a marking simulation
        var tempMarked = new HashSet<OOP>();
        var markStack = new Stack<OOP>();
        
        // Add roots
        foreach (var root in specialObjectsArray.Concat(explicitRoots))
        {
            if (!root.IsNil && !root.IsSmallInteger)
                markStack.Push(root);
        }
        
        // Mark reachable
        while (markStack.Count > 0)
        {
            var current = markStack.Pop();
            if (current.IsSmallInteger || current.IsNil || tempMarked.Contains(current))
                continue;
            
            tempMarked.Add(current);
            
            var currentLayout = objectMemory.GetObject(current);
            if (currentLayout == null)
                continue;
            
            if (!currentLayout.Header.ClassOOP.IsNil)
                markStack.Push(currentLayout.Header.ClassOOP);
            
            if (currentLayout.HasPointers)
            {
                foreach (var field in currentLayout.Fields)
                {
                    if (!field.IsSmallInteger && !field.IsNil)
                        markStack.Push(field);
                }
            }
        }
        
        return !tempMarked.Contains(oop);
    }
}

/// <summary>
/// Options for garbage collection.
/// </summary>
public sealed record GCOptions
{
    /// <summary>
    /// Generation to collect (0, 1, or 2 for full).
    /// </summary>
    public int Generation { get; init; } = 2;
    
    /// <summary>
    /// Whether to compact memory after collection.
    /// </summary>
    public bool Compact { get; init; }
    
    /// <summary>
    /// Whether to output verbose logging.
    /// </summary>
    public bool Verbose { get; init; }
    
    /// <summary>
    /// Default options for standard collection.
    /// </summary>
    public static GCOptions Default { get; } = new();
}

/// <summary>
/// Statistics from a garbage collection cycle.
/// </summary>
public sealed record GCStatistics
{
    /// <summary>
    /// Collection sequence number.
    /// </summary>
    public required int CollectionNumber { get; init; }
    
    /// <summary>
    /// When the collection started.
    /// </summary>
    public required DateTime StartTime { get; init; }
    
    /// <summary>
    /// When the collection ended.
    /// </summary>
    public required DateTime EndTime { get; init; }
    
    /// <summary>
    /// Time spent in collection.
    /// </summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Number of objects marked as reachable.
    /// </summary>
    public required int MarkedCount { get; init; }
    
    /// <summary>
    /// Number of objects collected.
    /// </summary>
    public required int CollectedCount { get; init; }
    
    /// <summary>
    /// Number of objects surviving after collection.
    /// </summary>
    public required int SurvivingCount { get; init; }
    
    /// <summary>
    /// Bytes freed by collection.
    /// </summary>
    public required long BytesFreed { get; init; }
    
    /// <summary>
    /// Memory usage before collection.
    /// </summary>
    public required long MemoryBefore { get; init; }
    
    /// <summary>
    /// Memory usage after collection.
    /// </summary>
    public required long MemoryAfter { get; init; }
    
    /// <summary>
    /// Number of GC roots traced.
    /// </summary>
    public required int RootsCount { get; init; }
    
    /// <summary>
    /// Generation collected (0, 1, or 2).
    /// </summary>
    public required int Generation { get; init; }
    
    /// <summary>
    /// Percentage of memory reclaimed.
    /// </summary>
    public double ReclaimedPercentage => MemoryBefore > 0 
        ? (BytesFreed * 100.0) / MemoryBefore 
        : 0;
    
    public override string ToString() =>
        $"GC #{CollectionNumber} Gen{Generation}: " +
        $"Collected={CollectedCount:N0}, Freed={BytesFreed:N0} bytes ({ReclaimedPercentage:F1}%), " +
        $"Surviving={SurvivingCount:N0}, Duration={Duration.TotalMilliseconds:F1}ms";
}