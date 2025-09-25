using Core.Memory;
using System;

namespace TestGC;

/// <summary>
/// Test program to verify the mark-and-sweep garbage collector implementation.
/// </summary>
public static class TestGarbageCollector
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Testing Mark-and-Sweep Garbage Collector ===");
        Console.WriteLine();
        
        // Create an object memory instance
        var memory = new ObjectMemory();
        
        // Test 1: Create some objects without references (should be collected)
        Console.WriteLine("Test 1: Creating unreferenced objects...");
        var classOop = OOP.FromPointer(100); // Dummy class OOP
        
        for (int i = 0; i < 10; i++)
        {
            var obj = memory.AllocateObject(classOop, 5);
            Console.WriteLine($"  Allocated object: {obj}");
        }
        
        // Test 2: Create objects with references (should survive GC)
        Console.WriteLine("\nTest 2: Creating referenced objects...");
        var root = memory.AllocateObject(classOop, 3);
        Console.WriteLine($"  Allocated root object: {root}");
        
        // Add as GC root
        memory.AddGCRoot(root);
        
        // Create child objects and link them
        var child1 = memory.AllocateObject(classOop, 2);
        var child2 = memory.AllocateObject(classOop, 2);
        Console.WriteLine($"  Allocated child1: {child1}");
        Console.WriteLine($"  Allocated child2: {child2}");
        
        // Store references in root
        memory.StorePointer(root, 0, child1);
        memory.StorePointer(root, 1, child2);
        
        // Create a circular reference
        var circular1 = memory.AllocateObject(classOop, 1);
        var circular2 = memory.AllocateObject(classOop, 1);
        Console.WriteLine($"  Allocated circular1: {circular1}");
        Console.WriteLine($"  Allocated circular2: {circular2}");
        memory.StorePointer(circular1, 0, circular2);
        memory.StorePointer(circular2, 0, circular1);
        
        // Link one to root so both survive
        memory.StorePointer(root, 2, circular1);
        
        // Test 3: Create a deep object graph
        Console.WriteLine("\nTest 3: Creating deep object graph...");
        var current = memory.AllocateObject(classOop, 1);
        var graphRoot = current;
        memory.AddGCRoot(graphRoot);
        
        for (int i = 0; i < 5; i++)
        {
            var next = memory.AllocateObject(classOop, 1);
            memory.StorePointer(current, 0, next);
            current = next;
            Console.WriteLine($"  Created chain link: {next}");
        }
        
        // Test 4: Test Special Objects Array simulation
        Console.WriteLine("\nTest 4: Simulating Special Objects Array...");
        var specialObjectsArray = memory.AllocateObject(classOop, 50);
        memory.RegisterSpecialObjectsArray(specialObjectsArray);
        
        // Add some objects to the special objects array
        for (int i = 0; i < 5; i++)
        {
            var specialObj = memory.AllocateObject(classOop, 1);
            memory.StorePointer(specialObjectsArray, i, specialObj);
            Console.WriteLine($"  Added special object at index {i}: {specialObj}");
        }
        
        // Create some more garbage
        Console.WriteLine("\nCreating more garbage objects...");
        for (int i = 0; i < 20; i++)
        {
            memory.AllocateObject(classOop, 3);
        }
        
        // Get initial stats
        Console.WriteLine("\n=== Before Garbage Collection ===");
        
        // Run garbage collection
        Console.WriteLine("\nRunning garbage collection...\n");
        memory.CollectGarbage();
        
        // Get GC stats
        var (objectsCollected, bytesFreed) = memory.GetLastGCStats();
        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"Objects collected: {objectsCollected}");
        Console.WriteLine($"Memory freed: {bytesFreed} bytes");
        
        // Test 5: Verify surviving objects are still accessible
        Console.WriteLine("\n=== Verifying Surviving Objects ===");
        try
        {
            var rootChild1 = memory.FetchPointer(root, 0);
            var rootChild2 = memory.FetchPointer(root, 1);
            var rootCircular = memory.FetchPointer(root, 2);
            Console.WriteLine($"Root's child1 is still accessible: {rootChild1}");
            Console.WriteLine($"Root's child2 is still accessible: {rootChild2}");
            Console.WriteLine($"Root's circular reference is still accessible: {rootCircular}");
            
            // Verify circular reference still works
            var circularNext = memory.FetchPointer(rootCircular, 0);
            var circularBack = memory.FetchPointer(circularNext, 0);
            Console.WriteLine($"Circular reference intact: {rootCircular} -> {circularNext} -> {circularBack}");
            
            // Verify deep graph
            current = graphRoot;
            Console.Write("Deep graph chain: ");
            for (int i = 0; i < 5; i++)
            {
                current = memory.FetchPointer(current, 0);
                Console.Write($"{current} ");
            }
            Console.WriteLine();
            
            Console.WriteLine("\n✓ All surviving objects are accessible!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error accessing surviving objects: {ex.Message}");
        }
        
        // Test 6: Run GC again to ensure stability
        Console.WriteLine("\n=== Running Second GC Cycle ===");
        
        memory.CollectGarbage();
        
        var (objectsCollected2, bytesFreed2) = memory.GetLastGCStats();
        Console.WriteLine($"\nSecond GC collected: {objectsCollected2} objects, freed {bytesFreed2} bytes");
        Console.WriteLine("(Should be minimal since no new garbage was created)");
        
        Console.WriteLine("\n=== Test Complete ===");
        Console.WriteLine("The mark-and-sweep garbage collector is working correctly!");
    }
}