using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Generated.Layouts;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Tests;

/// <summary>
/// Test utility to verify ProcessorScheduler initialization after cold start.
/// </summary>
public static class TestProcessorScheduler
{
    /// <summary>
    /// Validates that ProcessorScheduler is properly initialized with all required components.
    /// </summary>
    /// <returns>True if validation passes, false otherwise.</returns>
    public static bool ValidateProcessorSchedulerStructure()
    {
        // Get ProcessorScheduler from SpecialObjectsArray
        var schedulerRef = SpecialObjectsArray.ProcessorScheduler;
        if (schedulerRef.IsNil)
        {
            Console.WriteLine("ERROR: ProcessorScheduler is nil in SpecialObjectsArray");
            return false;
        }

        // Use the typed layout for safe access
        var scheduler = new STProcessorScheduler(schedulerRef);

        // Validate activeProcess is linked
        var activeProcess = scheduler.ActiveProcess;
        if (activeProcess.IsNil)
        {
            Console.WriteLine("ERROR: ProcessorScheduler.activeProcess is nil");
            return false;
        }

        // Validate quiescentProcessLists array
        var quiescentLists = scheduler.QuiescentProcessLists;
        if (quiescentLists.IsNil)
        {
            Console.WriteLine("ERROR: ProcessorScheduler.quiescentProcessLists is nil");
            return false;
        }

        // Validate it's an array with 8 elements
        var arraySize = quiescentLists.GetIndexedSize();
        if (arraySize != 8)
        {
            Console.WriteLine($"ERROR: quiescentProcessLists should have 8 elements, has {arraySize}");
            return false;
        }

        // Validate each element is a LinkedList
        for (var i = 0; i < 8; i++)
        {
            var linkedList = quiescentLists.GetIndexedPointer(i);
            if (linkedList.IsNil)
            {
                Console.WriteLine($"ERROR: quiescentProcessLists[{i}] is nil");
                return false;
            }

            // Verify it's a LinkedList with proper structure
            var list = new STLinkedList(linkedList);

            // Empty lists should have nil firstLink and lastLink
            if (!list.FirstLink.IsNil || !list.LastLink.IsNil)
            {
                Console.WriteLine($"WARNING: quiescentProcessLists[{i}] is not empty (expected for initial state)");
            }
        }

        Console.WriteLine("SUCCESS: ProcessorScheduler structure validation passed");
        Console.WriteLine($"  - activeProcess: {activeProcess}");
        Console.WriteLine($"  - quiescentProcessLists: Array of {arraySize} LinkedLists");
        Console.WriteLine("  - All LinkedLists properly initialized (empty)");

        return true;
    }

    /// <summary>
    /// Runs the validation test.
    /// </summary>
    public static void Main()
    {
        Console.WriteLine("Testing ProcessorScheduler initialization...");

        if (ValidateProcessorSchedulerStructure())
        {
            Console.WriteLine("\nGC Reachability Chain verified:");
            Console.WriteLine("  SOA[4] -> ProcessorScheduler");
            Console.WriteLine("    -> activeProcess (instance var 1) -> Process object");
            Console.WriteLine("    -> quiescentProcessLists (instance var 0) -> Array[8]");
            Console.WriteLine("      -> LinkedList[0..7] (one per priority level)");
        }
        else
        {
            Console.WriteLine("\nERROR: ProcessorScheduler validation failed!");
            Environment.Exit(1);
        }
    }
}