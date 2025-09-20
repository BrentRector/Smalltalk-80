using System;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Tests
{
    /// <summary>
    /// Demonstrates the UI initialization skipping feature.
    /// </summary>
    public class TestUISkip
    {
        public static void TestUIInitializationSkipping()
        {
            Console.WriteLine("Testing UI Initialization Skipping Feature");
            Console.WriteLine("==========================================");
            Console.WriteLine();

            // Create VM instance
            var vm = new VirtualMachine(initialMemorySize: 262144);

            // Configure to skip UI initializations
            var config = new InitializationConfig
            {
                SkipUIInitializations = true,
                ContinueOnError = true
            };
            
            // Add some custom UI classes to check for
            config.KnownUIClasses.Add("CustomDialogBox");
            config.KnownUIClasses.Add("CustomMenuBar");
            
            vm.InitializationConfig = config;

            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Skip UI Initializations: {config.SkipUIInitializations}");
            Console.WriteLine($"  Continue on Error: {config.ContinueOnError}");
            Console.WriteLine($"  Known UI Classes: {string.Join(", ", config.KnownUIClasses)}");
            Console.WriteLine();

            // Alternative: Use the convenience method
            // vm.SetSkipUIInitializations(true);

            try
            {
                // Load system image - this will now skip UI-related initializations
                Console.WriteLine("Loading system with UI skipping enabled...");
                vm.LoadBootstrapFromJson("system_image.json");
                
                Console.WriteLine("System loaded successfully!");
                Console.WriteLine();
                
                // The initialization executor will have reported:
                // - Which UI classes were missing
                // - Which initializations were skipped
                // - Summary of all initialization errors
                
                Console.WriteLine("Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during test: {ex.Message}");
            }
        }
        
        public static void Main(string[] args)
        {
            TestUIInitializationSkipping();
        }
    }
}