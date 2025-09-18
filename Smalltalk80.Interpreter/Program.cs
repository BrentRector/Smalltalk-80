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
            // Check if we want to run the complete system load test
            if (args.Length > 0 && args[0] == "--test-complete-load")
            {
                TestCompleteSystemLoad.Run(args);
                return;
            }
            
            try
            {
                // Create VM with default memory size
                var vm = new VirtualMachine();
                
                // Check for bootstrap.json in various locations
                var bootstrapPath = FindBootstrapFile();
                
                if (bootstrapPath != null && File.Exists(bootstrapPath))
                {
                    Console.WriteLine($"Found bootstrap file: {bootstrapPath}");
                    Console.WriteLine("Loading from JSON bootstrap...");
                    
                    // Load bootstrap from JSON
                    vm.LoadBootstrapFromJson(bootstrapPath);
                    
                    // Optionally load extended manifests
                    var manifestDir = Path.GetDirectoryName(bootstrapPath);
                    if (manifestDir != null)
                    {
                        var extendedDir = Path.Combine(manifestDir, "extended");
                        if (Directory.Exists(extendedDir))
                        {
                            Console.WriteLine($"Loading extended manifests from: {extendedDir}");
                            vm.LoadExtendedManifests(extendedDir);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No bootstrap.json found, using built-in bootstrap...");
                    // Fall back to programmatic bootstrap
                    vm.Bootstrap();
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