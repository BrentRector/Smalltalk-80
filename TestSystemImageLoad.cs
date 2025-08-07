using System;
using System.IO;
using System.Linq;
using Smalltalk80;

class TestSystemImageLoad
{
    static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("Smalltalk-80 Full System Image Loader Test");
        Console.WriteLine("==============================================\n");
        
        try
        {
            // Create VM with generated bootstrap
            Console.WriteLine("Phase 1: Initializing VM with generated bootstrap...");
            var vm = new VirtualMachine(skipStandardLibrary: true, useGeneratedBootstrap: true);
            
            // Verify bootstrap classes
            Console.WriteLine("\nBootstrap classes loaded:");
            var bootstrapClasses = new[] { "Object", "Class", "Behavior", "SmallInteger", "Array", "Dictionary", "BlockContext" };
            foreach (var className in bootstrapClasses)
            {
                if (SystemDictionary.TryGetClass(className, out var cls))
                {
                    Console.WriteLine($"  ✓ {className} ({cls.MethodDictionary.Count} methods)");
                }
                else
                {
                    Console.WriteLine($"  ✗ {className} NOT FOUND");
                }
            }
            
            // Load second stage bootstrap for class creation infrastructure
            Console.WriteLine("\nPhase 2: Loading second stage bootstrap...");
            var secondStageBootstrapPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "SecondStageBootstrap.st");
                
            if (File.Exists(secondStageBootstrapPath))
            {
                var result = SystemImageLoader.LoadSystemImage(secondStageBootstrapPath);
                Console.WriteLine($"  Second stage bootstrap: {result.MethodsLoaded} methods loaded");
            }
            
            // Now load the full system image
            Console.WriteLine("\nPhase 3: Loading full system image...");
            var systemImagePath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "Smalltalk80", "Smalltalk-80 Source", "Smalltalk-80.st");
            
            if (!File.Exists(systemImagePath))
            {
                Console.WriteLine($"System image not found at: {systemImagePath}");
                return;
            }
            
            Console.WriteLine($"Loading from: {systemImagePath}");
            var fileInfo = new FileInfo(systemImagePath);
            Console.WriteLine($"File size: {fileInfo.Length / 1024} KB");
            Console.WriteLine($"Lines: ~41,400");
            
            // Create the system image loader
            var loader = new SystemImageLoader(vm);
            
            // Add any additional exclusions for things we know won't work
            loader.ExcludeCategory("Graphics-Display Objects");
            loader.ExcludeCategory("Graphics-Editors");
            loader.ExcludeCategory("Interface-Browser");
            loader.ExcludeCategory("Interface-Debugger");
            
            Console.WriteLine("\nStarting load (this may take a while)...\n");
            
            var startTime = DateTime.Now;
            var loadResult = loader.LoadSystemImage(systemImagePath);
            var duration = DateTime.Now - startTime;
            
            // Display results
            Console.WriteLine("\n==============================================");
            Console.WriteLine("Load Results");
            Console.WriteLine("==============================================");
            Console.WriteLine($"Success: {loadResult.Success}");
            Console.WriteLine($"Duration: {duration.TotalSeconds:F2} seconds");
            Console.WriteLine($"Classes loaded: {loadResult.ClassesLoaded}");
            Console.WriteLine($"Classes skipped: {loadResult.ClassesSkipped}");
            Console.WriteLine($"Methods loaded: {loadResult.MethodsLoaded}");
            
            if (loadResult.Errors.Count > 0)
            {
                Console.WriteLine($"\nErrors ({loadResult.Errors.Count}):");
                foreach (var error in loadResult.Errors.Take(10))
                {
                    Console.WriteLine($"  - {error}");
                }
                if (loadResult.Errors.Count > 10)
                {
                    Console.WriteLine($"  ... and {loadResult.Errors.Count - 10} more");
                }
            }
            
            if (loadResult.Warnings.Count > 0)
            {
                Console.WriteLine($"\nWarnings ({loadResult.Warnings.Count}):");
                foreach (var warning in loadResult.Warnings.Take(10))
                {
                    Console.WriteLine($"  - {warning}");
                }
                if (loadResult.Warnings.Count > 10)
                {
                    Console.WriteLine($"  ... and {loadResult.Warnings.Count - 10} more");
                }
            }
            
            // Test some loaded classes
            Console.WriteLine("\n==============================================");
            Console.WriteLine("Testing Loaded Classes");
            Console.WriteLine("==============================================");
            
            var testClasses = new[]
            {
                "Object", "Boolean", "SmallInteger", "Float", "Fraction",
                "Character", "String", "Symbol", "Array", "OrderedCollection",
                "Dictionary", "Set", "Association", "Date", "Time",
                "Stream", "FileStream", "Compiler", "Parser", "Scanner",
                "Process", "Semaphore", "MethodContext", "BlockContext",
                "CompiledMethod", "Text", "Interval", "Random"
            };
            
            int found = 0;
            foreach (var className in testClasses)
            {
                if (SystemDictionary.TryGetClass(className, out var cls))
                {
                    Console.WriteLine($"  ✓ {className} ({cls.MethodDictionary.Count} methods)");
                    found++;
                }
                else
                {
                    Console.WriteLine($"  ✗ {className} NOT FOUND");
                }
            }
            
            Console.WriteLine($"\nFound {found} of {testClasses.Length} test classes");
            
            // Display total system statistics
            Console.WriteLine("\n==============================================");
            Console.WriteLine("System Statistics");
            Console.WriteLine("==============================================");
            Console.WriteLine($"Total classes in system: {SystemDictionary.AllClasses.Count}");
            Console.WriteLine($"Total globals: {SystemDictionary.AllGlobals.Count}");
            
            // Try to run a simple expression to test the system
            Console.WriteLine("\n==============================================");
            Console.WriteLine("Testing System Functionality");
            Console.WriteLine("==============================================");
            
            try
            {
                Console.WriteLine("Testing: 3 + 4");
                var result = vm.EvaluateSource("3 + 4");
                Console.WriteLine($"  Result: {result}");
                
                Console.WriteLine("Testing: 'hello' , ' world'");
                result = vm.EvaluateSource("'hello' , ' world'");
                Console.WriteLine($"  Result: {result}");
                
                Console.WriteLine("Testing: (1 to: 10) collect: [:x | x * x]");
                result = vm.EvaluateSource("(1 to: 10) collect: [:x | x * x]");
                Console.WriteLine($"  Result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
            
            Console.WriteLine("\n==============================================");
            Console.WriteLine("System image load test complete!");
            Console.WriteLine("==============================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}