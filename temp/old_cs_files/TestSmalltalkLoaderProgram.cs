using System;
using Smalltalk80;

class TestSmalltalkLoaderProgram
{
    static void Main()
    {
        Console.WriteLine("Testing SmalltalkLoader with generated bootstrap...");
        
        try
        {
            // Create VM with generated bootstrap
            var vm = new VirtualMachine(skipStandardLibrary: true, useGeneratedBootstrap: true);
            
            Console.WriteLine("\nBootstrap classes loaded:");
            var bootstrapClasses = new[] { "Object", "Class", "Behavior", "SmallInteger", "Array", "Dictionary" };
            foreach (var className in bootstrapClasses)
            {
                if (SystemDictionary.TryGetClass(className, out var cls))
                {
                    Console.WriteLine($"  ✓ {className}");
                }
                else
                {
                    Console.WriteLine($"  ✗ {className} NOT FOUND");
                }
            }
            
            // Now test the SmalltalkLoader
            Console.WriteLine("\nTesting SmalltalkLoader...");
            var loader = new SmalltalkLoader(vm.Interpreter);
            
            // Try to load a simple test file
            var testFile = "test_smalltalk_loader.st";
            if (System.IO.File.Exists(testFile))
            {
                Console.WriteLine($"Loading {testFile}...");
                var result = loader.LoadFile(testFile);
                
                Console.WriteLine($"\nLoad Result:");
                Console.WriteLine($"  Success: {result.Success}");
                Console.WriteLine($"  Classes loaded: {result.ClassesLoaded}");
                Console.WriteLine($"  Methods loaded: {result.MethodsLoaded}");
                
                if (result.Errors.Count > 0)
                {
                    Console.WriteLine($"  Errors ({result.Errors.Count}):");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"    - {error}");
                    }
                }
                
                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine($"  Warnings ({result.Warnings.Count}):");
                    foreach (var warning in result.Warnings)
                    {
                        Console.WriteLine($"    - {warning}");
                    }
                }
                
                // Check if the test class was created
                if (SystemDictionary.TryGetClass("TestClassFromLoader", out var testClass))
                {
                    Console.WriteLine("\n✓ TestClassFromLoader was successfully created!");
                    Console.WriteLine($"  Instance variables: {string.Join(", ", testClass.InstanceVariables)}");
                    Console.WriteLine($"  Methods: {testClass.MethodDictionary.Count}");
                }
                else
                {
                    Console.WriteLine("\n✗ TestClassFromLoader was NOT created");
                }
            }
            else
            {
                Console.WriteLine($"Test file {testFile} not found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}