using System;

class TestMinimalBootstrap
{
    static void Main()
    {
        Console.WriteLine("=== Testing Minimal Bootstrap ===\n");
        
        // Create VM with minimal bootstrap
        var vm = new VirtualMachine(skipStandardLibrary: false, useMinimalBootstrap: true);
        
        Console.WriteLine("\n=== Verifying Bootstrap Classes ===");
        
        // Check essential classes exist
        string[] essentialClasses = {
            "Object", "UndefinedObject", "Behavior", "ClassDescription", 
            "Class", "Metaclass", "Boolean", "True", "False",
            "SmallInteger", "Symbol", "String", "Array", "Dictionary",
            "SystemDictionary", "Association", "BlockContext"
        };
        
        foreach (var className in essentialClasses)
        {
            if (SystemDictionary.TryGetClass(className, out var cls))
            {
                Console.WriteLine($"✓ {className} exists");
            }
            else
            {
                Console.WriteLine($"✗ {className} NOT FOUND");
            }
        }
        
        Console.WriteLine("\n=== Testing Simple Expressions ===");
        
        // Test 1: Basic arithmetic
        Console.WriteLine("\nTest 1: 2 + 3");
        try
        {
            var result = vm.EvaluateSource("2 + 3");
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
        }
        
        // Test 2: Boolean logic
        Console.WriteLine("\nTest 2: true ifTrue: [42]");
        try
        {
            var result = vm.EvaluateSource("true ifTrue: [42]");
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
        }
        
        // Test 3: Class creation via Smalltalk
        Console.WriteLine("\nTest 3: Create class via Smalltalk");
        try
        {
            vm.EvaluateSource(@"
                Object subclass: #TestClass
                    instanceVariableNames: 'x y'
                    classVariableNames: ''
                    poolDictionaries: ''
                    category: 'Test'
            ");
            
            if (SystemDictionary.TryGetClass("TestClass", out var testClass))
            {
                Console.WriteLine("✓ TestClass created successfully");
                Console.WriteLine($"  Instance variables: {string.Join(", ", testClass.InstanceVariables)}");
            }
            else
            {
                Console.WriteLine("✗ TestClass not found after creation");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
        }
        
        // Test 4: Check Smalltalk global
        Console.WriteLine("\nTest 4: Smalltalk global");
        try
        {
            var result = vm.EvaluateSource("Smalltalk");
            Console.WriteLine($"Smalltalk global exists: {result != null}");
            
            // Test storing in Smalltalk
            vm.EvaluateSource("Smalltalk at: #MyGlobal put: 123");
            var globalValue = vm.EvaluateSource("Smalltalk at: #MyGlobal");
            Console.WriteLine($"Stored and retrieved global: {globalValue}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
        }
        
        Console.WriteLine("\n=== Test Complete ===");
    }
}