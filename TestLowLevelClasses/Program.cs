using System;
using System.Collections.Generic;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace TestLowLevelClasses;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing Low-Level Class Loading");
        Console.WriteLine("===============================");
        
        try
        {
            // Create VM with skip standard library to test manual loading
            var vm = new VirtualMachine(skipExtendedSystem: true);
            Console.WriteLine("✓ VM created successfully");
            
            if (vm.Adapter == null)
            {
                Console.WriteLine("✗ VM adapter not available");
                return;
            }
            
            // Test low-level class loading
            Console.WriteLine("\nTesting low-level class loading...");
            var executor = new JsonBootstrapExecutor(vm.Adapter);
            
            // Define a small set of test classes
            var testClasses = new List<string>
            {
                "Set", "OrderedCollection", "Stream", "Character", "Point"
            };
            
            string systemImagePath = "../complete_system_final.json";
            if (!System.IO.File.Exists(systemImagePath))
            {
                // Try alternate path
                systemImagePath = "../../complete_system_final.json";
                if (!System.IO.File.Exists(systemImagePath))
                {
                    Console.WriteLine($"✗ System image not found");
                    return;
                }
            }
            
            executor.ExecuteLowLevelClasses(systemImagePath, testClasses);
            Console.WriteLine($"✓ Attempted to load {testClasses.Count} classes via low-level mechanism");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}