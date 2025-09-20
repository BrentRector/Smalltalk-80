using System;
using System.IO;
using System.Collections.Generic;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.Config;

class TestSmallIntInit
{
    static void Main()
    {
        // Enable bytecode tracing only for specific methods
        DebugOptions.EnableBytecodeTrace = true;
        DebugOptions.EnableMethodTrace = true;
        
        // Create list to track methods we want to trace
        var tracedMethods = new HashSet<string>
        {
            "initialize",
            "new:",
            "subtractOrFail:",
            "whileTrue:",
            "notNil",
            "negated",
            "+", 
            "-",
            ">",
            "value"
        };
        
        try
        {
            Console.WriteLine("Testing SmallInteger initialize with targeted bytecode tracing");
            Console.WriteLine("========================================================");
            
            var vm = new VirtualMachine(initialMemorySize: 262144);
            vm.SetSkipUIInitializations(true);
            
            // Load the system image
            string systemImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system_image.json");
            if (!File.Exists(systemImagePath))
            {
                Console.WriteLine("ERROR: system_image.json not found at " + systemImagePath);
                return;
            }
            
            Console.WriteLine("Loading system image...");
            vm.LoadBootstrapFromJson(systemImagePath);
            Console.WriteLine("System loaded successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}