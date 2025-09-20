using System;
using System.IO;
using System.Linq;
using Smalltalk80.Interpreter.Config;
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
                // Parse command line arguments (will exit if invalid)
                ParseArguments(args);
                
                // Create VM with larger memory size for complete system
                // The complete system has 223 classes and ~4500 methods
                var vm = new VirtualMachine(initialMemorySize: 262144); // 256K objects
                
                // Look for the system image JSON file in multiple locations
                string? systemImagePath = null;
                var searchPaths = new[]
                {
                    "system_image.json",                                    // Current directory
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system_image.json"), // Exe directory
                    Path.Combine("..", "system_image.json"),                // Parent directory
                    Path.Combine("bin", "Debug", "net9.0", "system_image.json"), // Debug build output
                    Path.Combine("bin", "Release", "net9.0", "system_image.json") // Release build output
                };
                
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        systemImagePath = path;
                        break;
                    }
                }
                
                if (systemImagePath == null)
                {
                    Console.Error.WriteLine("Error: System image file 'system_image.json' not found.");
                    Console.Error.WriteLine("Searched in:");
                    foreach (var path in searchPaths)
                    {
                        Console.Error.WriteLine($"  - {Path.GetFullPath(path)}");
                    }
                    Console.Error.WriteLine("Please run the StandaloneCompiler to generate system_image.json first.");
                    Environment.Exit(1);
                }
                
                // Print the correct banner
                Console.WriteLine();
                Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║     Smalltalk-80 Virtual Machine - Clean Slate Edition     ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                
                Console.WriteLine($"Loading system image: {Path.GetFullPath(systemImagePath)}");
                
                var startTime = DateTime.Now;
                
                // Load the system image
                vm.LoadBootstrapFromJson(systemImagePath);
                
                var loadTime = DateTime.Now - startTime;
                Console.WriteLine($"System loaded in {loadTime.TotalSeconds:F2} seconds");
                Console.WriteLine();
                
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
        /// Parses command line arguments.
        /// </summary>
        private static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                
                if (arg == "--help" || arg == "-h")
                {
                    PrintUsage();
                    Environment.Exit(0);
                }
                else if (arg == "--version" || arg == "-v")
                {
                    Console.WriteLine("Smalltalk-80 Interpreter v1.0");
                    Environment.Exit(0);
                }
                else if (arg == "--debug" || arg == "-d")
                {
                    // Check if there's a value after --debug
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        var debugOptions = args[++i].ToLower().Split(',');
                        foreach (var option in debugOptions)
                        {
                            switch (option.Trim())
                            {
                                case "all":
                                    DebugOptions.EnableAll();
                                    Console.WriteLine("All debug output enabled.");
                                    break;
                                case "debug":
                                    DebugOptions.EnableDebugOutput = true;
                                    Console.WriteLine("[DEBUG] output enabled.");
                                    break;
                                case "method":
                                case "methodinstaller":
                                    DebugOptions.EnableMethodInstallerOutput = true;
                                    Console.WriteLine("[MethodInstaller] output enabled.");
                                    break;
                                case "class":
                                case "registration":
                                    DebugOptions.EnableClassRegistrationOutput = true;
                                    Console.WriteLine("Class registration output enabled.");
                                    break;
                                case "detail":
                                case "phasedetail":
                                    DebugOptions.EnablePhaseDetailOutput = true;
                                    Console.WriteLine("Phase detail output enabled.");
                                    break;
                                case "trace":
                                case "methodtrace":
                                    DebugOptions.EnableMethodTrace = true;
                                    Console.WriteLine("Method invocation trace enabled.");
                                    break;
                                case "bytecode":
                                case "bytecodetrace":
                                    DebugOptions.EnableBytecodeTrace = true;
                                    Console.WriteLine("Bytecode execution trace enabled.");
                                    break;
                                default:
                                    Console.WriteLine($"Unknown debug option: {option}");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // No specific options, enable all
                        DebugOptions.EnableAll();
                        Console.WriteLine("All debug output enabled.");
                    }
                }
                else
                {
                    // Unrecognized option
                    Console.Error.WriteLine($"Error: Unrecognized option '{arg}'");
                    Console.Error.WriteLine();
                    PrintUsage();
                    Environment.Exit(1);
                }
            }
        }
        
        /// <summary>
        /// Prints usage information.
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("Smalltalk-80 Interpreter");
            Console.WriteLine("Usage: Smalltalk80.Interpreter [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --debug [options], -d [options]");
            Console.WriteLine("      Enable debug output. Options can be:");
            Console.WriteLine("        all              - Enable all debug output");
            Console.WriteLine("        debug            - Enable [DEBUG] output");
            Console.WriteLine("        method           - Enable [MethodInstaller] output");
            Console.WriteLine("        class            - Enable class registration output");
            Console.WriteLine("        detail           - Enable phase detail output (Set name, etc.)");
            Console.WriteLine("        trace            - Enable method invocation trace");
            Console.WriteLine("        bytecode         - Enable bytecode execution trace");
            Console.WriteLine("      Multiple options can be comma-separated: --debug debug,method");
            Console.WriteLine("      If no options specified, enables all debug output");
            Console.WriteLine();
            Console.WriteLine("  --help, -h            Show this help message");
            Console.WriteLine("  --version, -v         Show version information");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Smalltalk80.Interpreter --debug all");
            Console.WriteLine("  Smalltalk80.Interpreter -d debug,method");
            Console.WriteLine("  Smalltalk80.Interpreter --debug class");
        }
    }
}