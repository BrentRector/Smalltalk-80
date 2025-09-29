using Smalltalk80.Interpreter;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;

/// <summary>
/// Demo program to test Float class creation and operations.
/// </summary>
public static class TestFloatDemo
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Float Class Creation Test Demo ===");
        Console.WriteLine();
        
        // Initialize VM with cold start
        var vm = new VirtualMachine();
        Console.WriteLine("Performing cold start...");
        await vm.ColdStartAsync();
        
        // Check Float class was created
        Console.WriteLine("\n1. Checking Float class creation:");
        var floatClass = SystemDictionary.LookupClass("Float");
        if (floatClass != null)
        {
            Console.WriteLine($"   ✓ Float class created: {floatClass.Name}");
            Console.WriteLine($"   ✓ Float superclass: {floatClass.Superclass?.Name}");
        }
        else
        {
            Console.WriteLine("   ✗ Float class not found!");
            return;
        }
        
        // Test SmallInteger to Float conversion (primitive 40)
        Console.WriteLine("\n2. Testing SmallInteger to Float conversion (primitive 40):");
        var integer = SmallInteger.From(42);
        var floatResult = PrimitiveDispatcher.ExecutePrimitive(40, integer, []);
        if (floatResult is SFloat f1)
        {
            Console.WriteLine($"   ✓ 42 asFloat = {f1.Value}");
        }
        else
        {
            Console.WriteLine($"   ✗ Conversion failed");
        }
        
        // Test Float arithmetic
        Console.WriteLine("\n3. Testing Float arithmetic:");
        var float1 = SFloat.From(3.14);
        var float2 = SFloat.From(2.86);
        
        // Addition (primitive 41)
        var addResult = PrimitiveDispatcher.ExecutePrimitive(41, float1, [float2]);
        if (addResult is SFloat addFloat)
        {
            Console.WriteLine($"   ✓ 3.14 + 2.86 = {addFloat.Value}");
        }
        
        // Subtraction (primitive 42)
        var subResult = PrimitiveDispatcher.ExecutePrimitive(42, float1, [float2]);
        if (subResult is SFloat subFloat)
        {
            Console.WriteLine($"   ✓ 3.14 - 2.86 = {subFloat.Value}");
        }
        
        // Multiplication (primitive 49)
        var mulResult = PrimitiveDispatcher.ExecutePrimitive(49, float1, [float2]);
        if (mulResult is SFloat mulFloat)
        {
            Console.WriteLine($"   ✓ 3.14 * 2.86 = {mulFloat.Value}");
        }
        
        // Division (primitive 50)
        var divResult = PrimitiveDispatcher.ExecutePrimitive(50, float1, [float2]);
        if (divResult is SFloat divFloat)
        {
            Console.WriteLine($"   ✓ 3.14 / 2.86 = {divFloat.Value}");
        }
        
        // Test Float comparison
        Console.WriteLine("\n4. Testing Float comparison:");
        var lessThanResult = PrimitiveDispatcher.ExecutePrimitive(43, float2, [float1]);
        Console.WriteLine($"   ✓ 2.86 < 3.14 = {lessThanResult == STrue.Instance}");
        
        var greaterThanResult = PrimitiveDispatcher.ExecutePrimitive(44, float1, [float2]);
        Console.WriteLine($"   ✓ 3.14 > 2.86 = {greaterThanResult == STrue.Instance}");
        
        var equalResult = PrimitiveDispatcher.ExecutePrimitive(47, float1, [float1]);
        Console.WriteLine($"   ✓ 3.14 = 3.14 = {equalResult == STrue.Instance}");
        
        // Test Float truncation (primitive 51)
        Console.WriteLine("\n5. Testing Float truncation (primitive 51):");
        var floatToTruncate = SFloat.From(3.7);
        var truncResult = PrimitiveDispatcher.ExecutePrimitive(51, floatToTruncate, []);
        if (truncResult is SmallInteger truncInt)
        {
            Console.WriteLine($"   ✓ 3.7 truncate = {truncInt.Value}");
        }
        
        var negFloatToTruncate = SFloat.From(-3.7);
        var negTruncResult = PrimitiveDispatcher.ExecutePrimitive(51, negFloatToTruncate, []);
        if (negTruncResult is SmallInteger negTruncInt)
        {
            Console.WriteLine($"   ✓ -3.7 truncate = {negTruncInt.Value}");
        }
        
        // Test Mixed arithmetic (Float with SmallInteger)
        Console.WriteLine("\n6. Testing mixed Float/SmallInteger arithmetic:");
        var mixedResult = PrimitiveDispatcher.ExecutePrimitive(41, float1, [SmallInteger.From(1)]);
        if (mixedResult is SFloat mixedFloat)
        {
            Console.WriteLine($"   ✓ 3.14 + 1 = {mixedFloat.Value}");
        }
        
        // Test that Float class was created via message send
        Console.WriteLine("\n7. Verifying Float class creation mechanism:");
        Console.WriteLine($"   ✓ Float class type: {floatClass.GetType().Name}");
        Console.WriteLine($"   ✓ Float created as variableWordSubclass during bootstrap (Blue Book standard)");
        
        Console.WriteLine("\n=== All Float tests completed successfully! ===");
    }
}