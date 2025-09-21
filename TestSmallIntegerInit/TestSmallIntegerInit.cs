using System;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.ObjectModel;

class TestSmallIntegerInit
{
    static void Main()
    {
        Console.WriteLine("Testing SmallInteger>>initialize...");
        
        // Create VM
        var vm = new VirtualMachine();
        
        // Load the system
        var bootstrapPath = "../../../bin/Debug/net9.0/system_image.json";
        vm.LoadBootstrapFromJson(bootstrapPath);
        
        Console.WriteLine("\n=== Testing SmallInteger initialize ===");
        
        // Get SmallInteger class
        var smallIntClass = vm.GetClass("SmallInteger");
        Console.WriteLine($"SmallInteger class: {smallIntClass}");
        
        // Find the initialize selector
        var initSelector = vm.Intern("initialize");
        Console.WriteLine($"initialize selector: {initSelector}");
        
        // Call SmallInteger class>>initialize
        Console.WriteLine("\nCalling SmallInteger class>>initialize...");
        try
        {
            var result = vm.SendWithResult(smallIntClass, initSelector, new OOP[0]);
            Console.WriteLine($"Result: {result}");
            
            // Test arithmetic after initialization
            Console.WriteLine("\n=== Testing arithmetic after SmallInteger initialize ===");
            var two = OOP.FromSmallInteger(2);
            var three = OOP.FromSmallInteger(3);
            var plusSelector = vm.Intern("+");
            
            var sum = vm.SendWithResult(two, plusSelector, new[] { three });
            Console.WriteLine($"2 + 3 = {sum}");
            
            if (sum.IsSmallInteger)
            {
                Console.WriteLine($"Result value: {sum.SmallIntegerValue}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during SmallInteger initialize: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }
}