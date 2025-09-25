using System;
using System.Diagnostics;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for the garbage collector implementation.
/// </summary>
public static class GarbageCollectorTests
{
    /// <summary>
    /// Runs all garbage collector tests.
    /// </summary>
    public static void RunAll()
    {
        Console.WriteLine("\n=== Garbage Collector Tests ===\n");
        
        TestBasicCollection();
        TestCyclicReferences();
        TestExplicitRoots();
        TestPermanentObjects();
        TestStatistics();
        TestPerformance();
        
        Console.WriteLine("\n=== All GC Tests Passed ===\n");
    }
    
    /// <summary>
    /// Tests basic garbage collection functionality.
    /// </summary>
    private static void TestBasicCollection()
    {
        Console.WriteLine("Test: Basic Collection");
        
        var memory = new ObjectMemory(1000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create some objects
        var obj1 = memory.AllocateObject(OOP.Nil, 2);
        var obj2 = memory.AllocateObject(OOP.Nil, 3);
        var obj3 = memory.AllocateObject(OOP.Nil, 1);
        
        // Make obj1 reachable from special objects
        specialObjects[0] = obj1;
        
        // Make obj2 reachable from obj1
        memory.SetField(obj1, 0, obj2);
        
        // obj3 is unreachable and should be collected
        
        var stats = gc.Collect(specialObjects);
        
        Console.WriteLine($"  Collected: {stats.CollectedCount} objects");
        Console.WriteLine($"  Surviving: {stats.SurvivingCount} objects");
        Debug.Assert(stats.CollectedCount == 1, "Should collect 1 unreachable object");
        Debug.Assert(memory.GetObject(obj3) == null, "obj3 should be collected");
        Debug.Assert(memory.GetObject(obj1) != null, "obj1 should survive");
        Debug.Assert(memory.GetObject(obj2) != null, "obj2 should survive");
        
        Console.WriteLine("  ✓ Basic collection working\n");
    }
    
    /// <summary>
    /// Tests collection of cyclic references.
    /// </summary>
    private static void TestCyclicReferences()
    {
        Console.WriteLine("Test: Cyclic References");
        
        var memory = new ObjectMemory(1000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create a cycle: obj1 -> obj2 -> obj3 -> obj1
        var obj1 = memory.AllocateObject(OOP.Nil, 1);
        var obj2 = memory.AllocateObject(OOP.Nil, 1);
        var obj3 = memory.AllocateObject(OOP.Nil, 1);
        
        memory.SetField(obj1, 0, obj2);
        memory.SetField(obj2, 0, obj3);
        memory.SetField(obj3, 0, obj1); // Create cycle
        
        // Create another object reachable from roots
        var obj4 = memory.AllocateObject(OOP.Nil, 1);
        specialObjects[0] = obj4;
        
        // The cycle is unreachable and should be collected
        var stats = gc.Collect(specialObjects);
        
        Console.WriteLine($"  Collected: {stats.CollectedCount} objects");
        Debug.Assert(stats.CollectedCount == 3, "Should collect entire unreachable cycle");
        Debug.Assert(memory.GetObject(obj1) == null, "Cycle should be collected");
        Debug.Assert(memory.GetObject(obj2) == null, "Cycle should be collected");
        Debug.Assert(memory.GetObject(obj3) == null, "Cycle should be collected");
        Debug.Assert(memory.GetObject(obj4) != null, "Rooted object should survive");
        
        Console.WriteLine("  ✓ Cyclic references handled correctly\n");
    }
    
    /// <summary>
    /// Tests explicit GC roots.
    /// </summary>
    private static void TestExplicitRoots()
    {
        Console.WriteLine("Test: Explicit GC Roots");
        
        var memory = new ObjectMemory(1000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create objects
        var obj1 = memory.AllocateObject(OOP.Nil, 1);
        var obj2 = memory.AllocateObject(OOP.Nil, 1);
        
        // Add obj1 as explicit root (temporary root for GC only)
        gc.AddRoot(obj1);
        
        // Run GC - obj1 should survive, obj2 should be collected
        var stats = gc.Collect(specialObjects);
        
        Console.WriteLine($"  Collected: {stats.CollectedCount} objects");
        Debug.Assert(stats.CollectedCount == 1, "Should collect 1 object");
        Debug.Assert(memory.GetObject(obj1) != null, "Explicitly rooted object should survive");
        Debug.Assert(memory.GetObject(obj2) == null, "Non-rooted object should be collected");
        
        // Remove explicit root and collect again
        gc.RemoveRoot(obj1);
        
        stats = gc.Collect(specialObjects);
        Debug.Assert(memory.GetObject(obj1) == null, "Object should be collected after root removal");
        
        Console.WriteLine("  ✓ Explicit roots working\n");
    }
    
    /// <summary>
    /// Tests permanent objects.
    /// </summary>
    private static void TestPermanentObjects()
    {
        Console.WriteLine("Test: Permanent Objects");
        
        var memory = new ObjectMemory(1000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create objects
        var obj1 = memory.AllocateObject(OOP.Nil, 1);
        var obj2 = memory.AllocateObject(OOP.Nil, 1);
        
        // Mark obj1 as permanent
        memory.MarkPermanent(obj1);
        
        // Run GC - obj1 should survive (permanent), obj2 should be collected
        var stats = gc.Collect(specialObjects);
        
        Console.WriteLine($"  Collected: {stats.CollectedCount} objects");
        Debug.Assert(memory.GetObject(obj1) != null, "Permanent object should never be collected");
        Debug.Assert(memory.GetObject(obj2) == null, "Non-permanent object should be collected");
        
        // Run GC again - permanent object should still survive
        stats = gc.Collect(specialObjects);
        Debug.Assert(memory.GetObject(obj1) != null, "Permanent object should never be collected");
        
        Console.WriteLine("  ✓ Permanent objects preserved\n");
    }
    
    /// <summary>
    /// Tests GC statistics tracking.
    /// </summary>
    private static void TestStatistics()
    {
        Console.WriteLine("Test: GC Statistics");
        
        var memory = new ObjectMemory(1000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create many objects
        var roots = new OOP[5];
        for (int i = 0; i < 5; i++)
        {
            roots[i] = memory.AllocateObject(OOP.Nil, 10);
            specialObjects[i] = roots[i];
        }
        
        // Create garbage
        for (int i = 0; i < 20; i++)
        {
            memory.AllocateObject(OOP.Nil, 5);
        }
        
        // Run GC with verbose statistics
        var options = new GCOptions
        {
            Verbose = true,
            Generation = 2
        };
        
        var stats = gc.Collect(specialObjects, options);
        
        Console.WriteLine($"  Collection #{stats.CollectionNumber}");
        Console.WriteLine($"  Duration: {stats.Duration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"  Collected: {stats.CollectedCount} objects");
        Console.WriteLine($"  Freed: {stats.BytesFreed:N0} bytes");
        Console.WriteLine($"  Reclaimed: {stats.ReclaimedPercentage:F1}%");
        
        Debug.Assert(stats.CollectedCount == 20, "Should collect 20 garbage objects");
        Debug.Assert(stats.MarkedCount == 5, "Should mark 5 root objects");
        Debug.Assert(gc.TotalCollections == 1, "Should have 1 collection");
        
        // Run multiple collections and check cumulative stats
        for (int i = 0; i < 10; i++)
        {
            memory.AllocateObject(OOP.Nil, 3);
        }
        
        gc.Collect(specialObjects);
        
        Console.WriteLine($"  Total Collections: {gc.TotalCollections}");
        Console.WriteLine($"  Total Objects Collected: {gc.TotalObjectsCollected}");
        Console.WriteLine($"  Total Bytes Freed: {gc.TotalBytesFreed:N0}");
        
        Debug.Assert(gc.TotalCollections == 2, "Should have 2 total collections");
        Debug.Assert(gc.TotalObjectsCollected == 30, "Should have collected 30 objects total");
        
        Console.WriteLine("  ✓ Statistics tracking correctly\n");
    }
    
    /// <summary>
    /// Tests GC performance with large object graphs.
    /// </summary>
    private static void TestPerformance()
    {
        Console.WriteLine("Test: GC Performance");
        
        var memory = new ObjectMemory(100000);
        var gc = new GarbageCollector(memory);
        var specialObjects = new OOP[10];
        Array.Fill(specialObjects, OOP.Nil);
        
        // Create a large linked list
        var head = memory.AllocateObject(OOP.Nil, 1);
        specialObjects[0] = head;
        
        var current = head;
        for (int i = 0; i < 1000; i++)
        {
            var next = memory.AllocateObject(OOP.Nil, 1);
            memory.SetField(current, 0, next);
            current = next;
        }
        
        // Create lots of garbage
        for (int i = 0; i < 5000; i++)
        {
            memory.AllocateObject(OOP.Nil, 3);
        }
        
        var stopwatch = Stopwatch.StartNew();
        var stats = gc.Collect(specialObjects);
        stopwatch.Stop();
        
        Console.WriteLine($"  Large collection: {stats.CollectedCount} objects in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"  Objects per ms: {stats.CollectedCount / Math.Max(1, stopwatch.ElapsedMilliseconds):F0}");
        
        Debug.Assert(stats.CollectedCount == 5000, "Should collect all garbage");
        Debug.Assert(stats.MarkedCount == 1001, "Should mark linked list");
        Debug.Assert(stopwatch.ElapsedMilliseconds < 100, "Should complete in reasonable time");
        
        Console.WriteLine("  ✓ Performance acceptable\n");
    }
}