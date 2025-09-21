using System;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Debug;

class TestBinaryChoiceInit
{
    static void Main()
    {
        Console.WriteLine("Testing BinaryChoiceView>>initialize...");
        
        // Create VM
        var vm = new VirtualMachine();
        
        // Load the system
        var bootstrapPath = "../../../bin/Debug/net9.0/system_image.json";
        vm.LoadBootstrapFromJson(bootstrapPath);
        
        Console.WriteLine("\n=== Testing BinaryChoiceView initialize ===");
        
        // First check what Cursor is
        var cursorSymbol = vm.Intern("Cursor");
        Console.WriteLine($"Cursor symbol: {cursorSymbol}");
        
        // Look up Cursor class
        var cursorClass = vm.GetClass("Cursor");
        Console.WriteLine($"Cursor class: {cursorClass}");
        
        if (!cursorClass.IsNil)
        {
            var cursorMetaClass = vm.GetClassOfObject(cursorClass);
            var cursorMetaClassName = vm.GetClassName(cursorMetaClass);
            Console.WriteLine($"Cursor class is instance of: {cursorMetaClassName}");
        }
        
        // Get BinaryChoiceView class
        var binaryChoiceClass = vm.GetClass("BinaryChoiceView");
        Console.WriteLine($"\nBinaryChoiceView class: {binaryChoiceClass}");
        
        // Find the initialize selector
        var initSelector = vm.Intern("initialize");
        Console.WriteLine($"initialize selector: {initSelector}");
        
        // Enable debug output
        DebugOptions.EnableMethodTrace = true;
        
        // Call BinaryChoiceView class>>initialize
        Console.WriteLine("\nCalling BinaryChoiceView class>>initialize...");
        try
        {
            var result = vm.SendWithResult(binaryChoiceClass, initSelector, new OOP[0]);
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }
}