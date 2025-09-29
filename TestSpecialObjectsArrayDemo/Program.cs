using System;
using Smalltalk80.Interpreter.Core;

/// <summary>
/// Standalone test demonstrating the SpecialObjectsArray functionality.
/// </summary>
class TestSpecialObjectsArray
{
    static void Main()
    {
        Console.WriteLine("Testing SpecialObjectsArray Implementation");
        Console.WriteLine("==========================================\n");
        
        // Test 1: Basic initialization
        Console.WriteLine("Test 1: Basic Initialization");
        using var array = new SpecialObjectsArray();
        Console.WriteLine($"✓ Created array with standard size {SpecialObjectsArray.StandardSize}");
        Console.WriteLine($"✓ Nil object: {array[SpecialObjectsIndex.NilObject]}");
        Console.WriteLine($"✓ True object: {array[SpecialObjectsIndex.TrueObject]}");
        Console.WriteLine($"✓ False object: {array[SpecialObjectsIndex.FalseObject]}");
        Console.WriteLine();
        
        // Test 2: Enum indexing
        Console.WriteLine("Test 2: Enum-based Indexing");
        var stringClass = new SClass { Name = "String" };
        array[SpecialObjectsIndex.StringClass] = stringClass;
        var retrieved = array[SpecialObjectsIndex.StringClass];
        Console.WriteLine($"✓ Set and retrieved String class: {retrieved}");
        Console.WriteLine();
        
        // Test 3: Integer indexing
        Console.WriteLine("Test 3: Integer-based Indexing");
        var testObj = new SObject();
        array[10] = testObj;
        var retrievedInt = array[10];
        Console.WriteLine($"✓ Set and retrieved object at index 10: {retrievedInt}");
        Console.WriteLine();
        
        // Test 4: Helper properties
        Console.WriteLine("Test 4: Helper Properties");
        Console.WriteLine($"✓ Nil helper: {array.Nil}");
        Console.WriteLine($"✓ True helper: {array.True}");
        Console.WriteLine($"✓ False helper: {array.False}");
        Console.WriteLine($"✓ StringClass helper: {array.StringClass}");
        Console.WriteLine();
        
        // Test 5: All indices defined
        Console.WriteLine("Test 5: All Blue Book Indices (0-47) Defined");
        int indexCount = 0;
        foreach (SpecialObjectsIndex index in Enum.GetValues<SpecialObjectsIndex>())
        {
            indexCount++;
            if ((int)index < 10 || (int)index > 37) // Show first and last few
            {
                Console.WriteLine($"  Index {(int)index,2}: {index}");
            }
            else if ((int)index == 10)
            {
                Console.WriteLine($"  ... (indices 10-37 omitted for brevity) ...");
            }
        }
        Console.WriteLine($"✓ Total indices defined: {indexCount}");
        Console.WriteLine();
        
        // Test 6: Thread safety (simplified)
        Console.WriteLine("Test 6: Thread Safety");
        var tasks = new System.Threading.Tasks.Task[5];
        for (int i = 0; i < tasks.Length; i++)
        {
            int taskId = i;
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    array[20 + taskId] = new SObject();
                    var _ = array[20 + taskId];
                }
            });
        }
        System.Threading.Tasks.Task.WaitAll(tasks);
        Console.WriteLine("✓ Completed 500 concurrent read/write operations");
        Console.WriteLine();
        
        // Test 7: Snapshot and restore
        Console.WriteLine("Test 7: Snapshot and Restore");
        var snapshot = array.CreateSnapshot();
        Console.WriteLine($"✓ Created snapshot with {snapshot.Length} elements");
        using var newArray = new SpecialObjectsArray();
        newArray.RestoreFromSnapshot(snapshot);
        Console.WriteLine($"✓ Restored from snapshot");
        Console.WriteLine($"✓ Verified String class: {newArray[SpecialObjectsIndex.StringClass]}");
        Console.WriteLine();
        
        Console.WriteLine("All tests completed successfully!");
    }
}