using System;
using System.IO;
using Smalltalk80.Interpreter.VM;

namespace TestFieldLookup
{
    /// <summary>
    /// Tests the complete bootstrap process with Phase -1 field mapping.
    /// </summary>
    public static class TestBootstrapWithPhaseMinusOne
    {
        public static void Main2(string[] args)
        {
            Console.WriteLine("=== Testing Bootstrap with Phase -1 Field Mapping ===\n");
            
            try
            {
                // Find bootstrap.json file
                var bootstrapPath = FindBootstrapFile();
                if (bootstrapPath == null)
                {
                    Console.WriteLine("✗ Could not find bootstrap.json file");
                    return;
                }
                
                Console.WriteLine($"Using bootstrap file: {bootstrapPath}\n");
                
                // Create VM and load bootstrap
                Console.WriteLine("Creating VirtualMachine...");
                var vm = new VirtualMachine(262144);
                
                Console.WriteLine("Loading bootstrap from JSON...");
                vm.LoadBootstrapFromJson(bootstrapPath);
                
                // Check that some key classes were loaded
                Console.WriteLine("\nVerifying loaded classes:");
                
                var classNames = new[] { "Object", "Behavior", "ClassDescription", "Class", "Metaclass", 
                                         "CompiledMethod", "MethodDictionary", "Symbol", "String" };
                
                foreach (var className in classNames)
                {
                    var classOop = vm.GetClassByName(className);
                    if (!classOop.IsNil)
                    {
                        Console.WriteLine($"  ✓ {className} loaded: {classOop}");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ {className} not found");
                    }
                }
                
                Console.WriteLine("\n=== Bootstrap with Phase -1 Completed Successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Bootstrap failed with error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
        
        private static string? FindBootstrapFile()
        {
            // Try various possible locations
            var paths = new[]
            {
                "Bootstrap/bootstrap.json",
                "../Bootstrap/bootstrap.json",
                "../../Bootstrap/bootstrap.json",
                "../../../Bootstrap/bootstrap.json",
                "D:/Users/brent/OneDrive/Src/Smalltalk-80/Bootstrap/bootstrap.json"
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
            
            return null;
        }
    }
}