using System;
using System.IO;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Test program to verify minimal Phase 1 bootstrap.
/// </summary>
public class TestMinimalBootstrap
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Testing Minimal Phase 1 Bootstrap ===");
        Console.WriteLine();
        
        try
        {
            // Create VM and bootstrap executor
            var vm = new VirtualMachine();
            var adapter = new VMPrimitiveAdapter(vm);
            var executor = new JsonBootstrapExecutor(adapter);
            
            // Load Phase 1 minimal
            var phase1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bootstrap_phase1_minimal.json");
            if (!File.Exists(phase1Path))
            {
                Console.WriteLine($"ERROR: Cannot find {phase1Path}");
                return;
            }
            
            Console.WriteLine("Loading Phase 1 minimal bootstrap...");
            var phase1Json = File.ReadAllText(phase1Path);
            executor.ExecutePhase1(phase1Json);
            
            Console.WriteLine("\n=== Phase 1 Complete ===");
            Console.WriteLine("Testing message-based class creation...\n");
            
            // Test creating a class via messages
            TestClassCreation(adapter);
            
            Console.WriteLine("\n=== All Tests Passed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    private static void TestClassCreation(VMPrimitiveAdapter vm)
    {
        Console.WriteLine("Test 1: Creating a metaclass via Metaclass new");
        
        // Get Metaclass
        var metaclassOop = vm.PrimitiveLookupClass("Metaclass");
        if (metaclassOop < 0)
        {
            throw new Exception("Metaclass not found!");
        }
        
        // Send 'new' to Metaclass
        var newSelector = vm.PrimitiveInternSymbol("new");
        var pointClassOop = vm.PrimitiveSendMessage(metaclassOop, newSelector, new int[0]);
        
        if (pointClassOop < 0)
        {
            throw new Exception("Failed to create metaclass!");
        }
        
        Console.WriteLine($"✓ Created metaclass OOP {pointClassOop}");
        
        // Test 2: Create MethodDictionary
        Console.WriteLine("\nTest 2: Creating method dictionary via MethodDictionary new");
        
        var methodDictClassOop = vm.PrimitiveLookupClass("MethodDictionary");
        if (methodDictClassOop < 0)
        {
            throw new Exception("MethodDictionary class not found!");
        }
        
        var dictOop = vm.PrimitiveSendMessage(methodDictClassOop, newSelector, new int[0]);
        
        if (dictOop < 0)
        {
            throw new Exception("Failed to create method dictionary!");
        }
        
        Console.WriteLine($"✓ Created method dictionary OOP {dictOop}");
        
        // Test 3: Set metaclass properties
        Console.WriteLine("\nTest 3: Setting metaclass properties via messages");
        
        var superclassSelector = vm.PrimitiveInternSymbol("superclass:");
        var objectClassOop = vm.PrimitiveLookupClass("ObjectClass");
        
        if (objectClassOop < 0)
        {
            throw new Exception("Object class metaclass not found!");
        }
        
        var result = vm.PrimitiveSendMessage(pointClassOop, superclassSelector, new[] { objectClassOop });
        Console.WriteLine($"✓ Set superclass to Object class");
        
        var methodDictSelector = vm.PrimitiveInternSymbol("methodDictionary:");
        result = vm.PrimitiveSendMessage(pointClassOop, methodDictSelector, new[] { dictOop });
        Console.WriteLine($"✓ Set method dictionary");
        
        var formatSelector = vm.PrimitiveInternSymbol("format:");
        var formatValue = vm.PrimitiveNewSmallInteger(2); // 2 instance variables
        result = vm.PrimitiveSendMessage(pointClassOop, formatSelector, new[] { formatValue });
        Console.WriteLine($"✓ Set format to 2");
        
        // Test 4: Create the class itself
        Console.WriteLine("\nTest 4: Creating class from metaclass");
        
        var pointOop = vm.PrimitiveSendMessage(pointClassOop, newSelector, new int[0]);
        
        if (pointOop < 0)
        {
            throw new Exception("Failed to create Point class!");
        }
        
        Console.WriteLine($"✓ Created Point class OOP {pointOop}");
        
        // Test 5: Create an instance
        Console.WriteLine("\nTest 5: Creating instance of Point");
        
        var instanceOop = vm.PrimitiveSendMessage(pointOop, newSelector, new int[0]);
        
        if (instanceOop < 0)
        {
            throw new Exception("Failed to create Point instance!");
        }
        
        Console.WriteLine($"✓ Created Point instance OOP {instanceOop}");
        
        Console.WriteLine("\n✅ All class creation tests passed!");
    }
}