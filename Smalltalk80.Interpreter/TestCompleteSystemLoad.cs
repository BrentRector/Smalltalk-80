using System;
using System.IO;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// [DEPRECATED] Test program for loading the complete Smalltalk-80 system image.
    /// The main Program.cs now loads the complete system by default.
    /// This test is kept for backward compatibility only.
    /// </summary>
    [Obsolete("Use the main program which now loads the complete system by default")]
    public static class TestCompleteSystemLoad
    {
        public static void Run(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("Testing Complete System Image Load");
            Console.WriteLine("==================================================\n");

            try
            {
                // Create VM with larger memory for the complete system
                var vm = new VirtualMachine(initialMemorySize: 262144); // 256K objects
                
                // Find the complete system JSON - try multiple locations
                string? completePath = null;
                var searchPaths = new[]
                {
                    @"D:\Users\brent\OneDrive\Src\Smalltalk-80\complete_system_final.json",
                    Path.Combine("..", "complete_system_final.json"),
                    Path.Combine("..", "..", "complete_system_final.json"),
                    "complete_system_final.json"
                };
                
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        completePath = Path.GetFullPath(path);
                        break;
                    }
                }
                
                if (completePath == null || !File.Exists(completePath))
                {
                    Console.WriteLine("Error: Could not find complete_system_final.json");
                    Console.WriteLine("Searched locations:");
                    foreach (var path in searchPaths)
                        Console.WriteLine($"  - {path}");
                    return;
                }
                
                Console.WriteLine($"Loading complete system from: {completePath}");
                Console.WriteLine($"File size: {new FileInfo(completePath).Length / 1024}KB");
                
                var startTime = DateTime.Now;
                
                // Try to load it as a bootstrap (which will process all classes)
                vm.LoadBootstrapFromJson(completePath);
                
                var loadTime = DateTime.Now - startTime;
                Console.WriteLine($"\nLoad completed in {loadTime.TotalSeconds:F2} seconds");
                
                // Print statistics
                var memory = vm.Memory;
                var stats = memory.GetStatistics();
                Console.WriteLine($"\nMemory Statistics:");
                Console.WriteLine($"  Total Objects: {stats.TotalObjects}");
                Console.WriteLine($"  Total Capacity: {stats.TotalCapacity}");
                Console.WriteLine($"  Next Index: {stats.NextIndex}");
                
                // Run GC to see if everything is reachable
                Console.WriteLine("\nRunning garbage collection on complete system...");
                vm.RunGarbageCollection();
                
                Console.WriteLine("\n==================================================");
                Console.WriteLine("Complete system load test finished successfully!");
                Console.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n!!! ERROR during complete system load !!!");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"\nStack trace:");
                Console.WriteLine(ex.StackTrace);
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\nInner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}