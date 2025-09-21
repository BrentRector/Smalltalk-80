using System;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.ObjectModel;

class TestArithmetic
{
    static void Main()
    {
        Console.WriteLine("Testing arithmetic primitives...");
        
        // Create VM
        var vm = new VirtualMachine();
        
        // Load the system
        var bootstrapPath = "../../../bin/Debug/net9.0/system_image.json";
        vm.LoadBootstrapFromJson(bootstrapPath);
        
        Console.WriteLine("\nTesting 2 + 3...");
        
        // Create SmallIntegers
        var two = OOP.FromSmallInteger(2);
        var three = OOP.FromSmallInteger(3);
        
        // Find the + selector
        var plusSelector = vm.Intern("+");
        
        Console.WriteLine($"two = {two}");
        Console.WriteLine($"three = {three}");
        Console.WriteLine($"plusSelector = {plusSelector}");
        
        // Send the message 2 + 3
        var result = vm.SendWithResult(two, plusSelector, new[] { three });
        
        Console.WriteLine($"Result of 2 + 3 = {result}");
        
        if (result.IsSmallInteger)
        {
            Console.WriteLine($"Result value: {result.SmallIntegerValue}");
        }
        else if (result.IsNil)
        {
            Console.WriteLine("ERROR: Result is nil!");
        }
        else
        {
            Console.WriteLine($"ERROR: Result is not a SmallInteger: {result}");
        }
    }
}