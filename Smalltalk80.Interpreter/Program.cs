using System;
using System.IO;
using Smalltalk80.Interpreter.Host;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// Main entry point for the Smalltalk-80 interpreter.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Create VM with larger memory size for complete system
                // The complete system has 223 classes and ~4500 methods
                var vm = new VirtualMachine(initialMemorySize: 262144); // 256K objects
                
                // Look for the complete system image JSON file
                var systemImagePath = FindSystemImageFile();
                
                if (systemImagePath != null && File.Exists(systemImagePath))
                {
                    Console.WriteLine("==================================================");
                    Console.WriteLine("Smalltalk-80 Interpreter - Complete System Load");
                    Console.WriteLine("==================================================\n");
                    Console.WriteLine($"Loading complete system image from: {systemImagePath}");
                    Console.WriteLine($"File size: {new FileInfo(systemImagePath).Length / 1024}KB");
                    
                    var startTime = DateTime.Now;
                    
                    // Load the complete system image
                    vm.LoadBootstrapFromJson(systemImagePath);
                    
                    var loadTime = DateTime.Now - startTime;
                    Console.WriteLine($"\nSystem loaded in {loadTime.TotalSeconds:F2} seconds");
                    
                    // Print memory statistics
                    var memory = vm.Memory;
                    var stats = memory.GetStatistics();
                    Console.WriteLine($"\nMemory Statistics:");
                    Console.WriteLine($"  Total Objects: {stats.TotalObjects}");
                    Console.WriteLine($"  Classes Loaded: {stats.TotalObjects / 100}"); // Rough estimate
                    
                    Console.WriteLine("\nSystem ready.");
                    Console.WriteLine("==================================================\n");
                }
                else
                {
                    // Fall back to bootstrap if complete system not found
                    Console.WriteLine("Complete system image not found, looking for bootstrap.json...");
                    var bootstrapPath = FindBootstrapFile();
                    
                    if (bootstrapPath != null && File.Exists(bootstrapPath))
                    {
                        Console.WriteLine($"Found bootstrap file: {bootstrapPath}");
                        Console.WriteLine("Loading from bootstrap (limited system)...");
                        vm.LoadBootstrapFromJson(bootstrapPath);
                    }
                    else
                    {
                        Console.WriteLine("No bootstrap.json found, using built-in bootstrap...");
                        vm.Bootstrap();
                    }
                }
                
                // Create and start console interface
                var console = new ConsoleInterface(vm);
                console.Start();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
        
        /// <summary>
        /// Searches for the complete system image JSON file.
        /// </summary>
        private static string? FindSystemImageFile()
        {
            // Search paths in order of preference for complete_system_final.json
            var searchPaths = new[]
            {
                // Current directory
                "complete_system_final.json",
                // Parent directory
                Path.Combine("..", "complete_system_final.json"),
                // Development paths
                Path.Combine("..", "..", "complete_system_final.json"),
                Path.Combine("..", "..", "..", "complete_system_final.json"),
                // Absolute path for development
                @"D:\Users\brent\OneDrive\Src\Smalltalk-80\complete_system_final.json"
            };
            
            foreach (var path in searchPaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // Ignore invalid paths
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Searches for the bootstrap.json file in common locations.
        /// </summary>
        private static string? FindBootstrapFile()
        {
            // Search paths in order of preference
            var searchPaths = new[]
            {
                // Current directory
                "bootstrap.json",
                // Bootstrap subdirectory
                Path.Combine("Bootstrap", "bootstrap.json"),
                // Parent directory's Bootstrap folder
                Path.Combine("..", "Bootstrap", "bootstrap.json"),
                // Development paths
                Path.Combine("..", "..", "Bootstrap", "bootstrap.json"),
                Path.Combine("..", "..", "..", "Bootstrap", "bootstrap.json"),
                // Absolute path for development
                @"D:\Users\brent\OneDrive\Src\Smalltalk-80\Bootstrap\bootstrap.json"
            };
            
            foreach (var path in searchPaths)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    // Ignore invalid paths
                }
            }
            
            return null;
        }
    }
}