using System;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

/// <summary>
/// Test program to verify that:
/// 1. ProcessList is created in special objects array
/// 2. Processes are added to ProcessList
/// 3. GC traverses ProcessList and all process contexts
/// </summary>
class TestProcessTracking
{
    static void Main()
    {
        Console.WriteLine("=== Testing Process Tracking and GC ===\n");
        
        // Enable debug output
        DebugOptions.EnableDebugOutput = true;
        
        // Create VM instance
        var memory = new ObjectMemory(1024 * 1024); // 1MB heap
        var vm = new VirtualMachine(memory);
        
        // Initialize the VM (this should create ProcessList)
        Console.WriteLine("Initializing VM...");
        vm.Initialize();
        
        // Test 1: Verify ProcessList exists in special objects
        TestProcessListExists(vm);
        
        // Test 2: Create initial process and verify it's added to ProcessList
        TestInitialProcess(vm);
        
        // Test 3: Run GC and verify processes are preserved
        TestGCPreservesProcesses(vm, memory);
        
        Console.WriteLine("\n=== All Process Tracking Tests Complete ===");
    }
    
    static void TestProcessListExists(VirtualMachine vm)
    {
        Console.WriteLine("\nTest 1: ProcessList in Special Objects");
        Console.WriteLine("--------------------------------------");
        
        var processList = vm.GetSpecialObject(SpecialObjects.ProcessList);
        if (!processList.IsNil)
        {
            Console.WriteLine($"✓ ProcessList found at index {SpecialObjects.ProcessList}: {processList}");
        }
        else
        {
            Console.WriteLine($"✗ ProcessList not found at index {SpecialObjects.ProcessList}");
        }
    }
    
    static void TestInitialProcess(VirtualMachine vm)
    {
        Console.WriteLine("\nTest 2: Initial Process Creation");
        Console.WriteLine("--------------------------------");
        
        // The VM should create an initial process during startup
        var processList = vm.GetSpecialObject(SpecialObjects.ProcessList);
        if (processList.IsNil)
        {
            Console.WriteLine("✗ ProcessList not found");
            return;
        }
        
        var memory = vm.GetMemory();
        var processListObj = memory.GetObject(processList);
        if (processListObj == null)
        {
            Console.WriteLine("✗ ProcessList object not found in memory");
            return;
        }
        
        // Check if ProcessList has any processes
        var firstLink = processListObj.Fields[0]; // firstLink field
        if (!firstLink.IsNil)
        {
            Console.WriteLine($"✓ ProcessList contains process: {firstLink}");
            
            // Verify the process structure
            var processObj = memory.GetObject(firstLink);
            if (processObj != null)
            {
                Console.WriteLine($"  Process fields:");
                Console.WriteLine($"    nextLink: {processObj.Fields[0]}");
                Console.WriteLine($"    suspendedContext: {processObj.Fields[1]}");
                Console.WriteLine($"    priority: {processObj.Fields[2]}");
                Console.WriteLine($"    myList: {processObj.Fields[3]}");
                
                // Verify myList points back to ProcessList
                if (processObj.Fields[3].Equals(processList))
                {
                    Console.WriteLine("  ✓ Process.myList correctly points to ProcessList");
                }
            }
        }
        else
        {
            Console.WriteLine("✗ ProcessList is empty");
        }
    }
    
    static void TestGCPreservesProcesses(VirtualMachine vm, ObjectMemory memory)
    {
        Console.WriteLine("\nTest 3: GC Preserves Processes");
        Console.WriteLine("------------------------------");
        
        // Get initial process from ProcessList
        var processList = vm.GetSpecialObject(SpecialObjects.ProcessList);
        var processListObj = memory.GetObject(processList);
        
        if (processListObj == null)
        {
            Console.WriteLine("✗ ProcessList not accessible");
            return;
        }
        
        var firstProcess = processListObj.Fields[0];
        if (firstProcess.IsNil)
        {
            Console.WriteLine("✗ No processes to test");
            return;
        }
        
        Console.WriteLine($"Process before GC: {firstProcess}");
        
        // Run garbage collection
        var gc = new GarbageCollector(memory);
        var stats = gc.Collect(vm.GetSpecialObjectsArray());
        
        Console.WriteLine($"GC completed: {stats.ObjectsMarked} objects marked, {stats.ObjectsCollected} collected");
        
        // Verify process still exists
        var processListAfterGC = vm.GetSpecialObject(SpecialObjects.ProcessList);
        var processListObjAfterGC = memory.GetObject(processListAfterGC);
        
        if (processListObjAfterGC != null)
        {
            var firstProcessAfterGC = processListObjAfterGC.Fields[0];
            if (firstProcessAfterGC.Equals(firstProcess))
            {
                Console.WriteLine($"✓ Process preserved after GC: {firstProcessAfterGC}");
                
                // Verify the process's context is also preserved
                var processObjAfterGC = memory.GetObject(firstProcessAfterGC);
                if (processObjAfterGC != null)
                {
                    var context = processObjAfterGC.Fields[1]; // suspendedContext
                    if (!context.IsNil)
                    {
                        var contextObj = memory.GetObject(context);
                        if (contextObj != null)
                        {
                            Console.WriteLine($"✓ Process context also preserved: {context}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("✗ Process was not preserved by GC");
            }
        }
        else
        {
            Console.WriteLine("✗ ProcessList was collected by GC!");
        }
    }
}