using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

/// <summary>
/// Minimal test program to verify class-side method installation.
/// </summary>
class TestClassSideMethodInstall
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing Class-Side Method Installation");
        Console.WriteLine("=====================================");
        
        try
        {
            // Create the VM and bootstrap
            var vm = new VirtualMachine();
            var vmAdapter = vm.Adapter;
            
            if (vmAdapter == null)
            {
                Console.WriteLine("ERROR: VMPrimitiveAdapter not available");
                return;
            }
            
            Console.WriteLine("VM created successfully, VMPrimitiveAdapter available");
            Console.WriteLine();
            
            // Set up the VMPrimitiveAdapter to use the bootstrapped SystemDictionary
            // From the bootstrap log, we can see SystemDictionary was created at OOP 20
            Console.WriteLine("Connecting VMPrimitiveAdapter to bootstrapped SystemDictionary...");
            
            // Use reflection to set the private smalltalkDictOop field
            var smalltalkDictOopField = vmAdapter.GetType().GetField("smalltalkDictOop", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (smalltalkDictOopField != null)
            {
                smalltalkDictOopField.SetValue(vmAdapter, 20); // SystemDictionary is at OOP 20
                Console.WriteLine("Set smalltalkDictOop to 20 (SystemDictionary)");
            }
            else
            {
                Console.WriteLine("Could not find smalltalkDictOop field for reflection");
            }
            
            // Debug: Test the lookup process step by step
            Console.WriteLine("Debug: Testing SystemDictionary lookup components...");
            
            // Step 1: Check if we can intern a symbol
            Console.WriteLine("Step 1: Testing symbol interning...");
            var booleanSymbolOop = vmAdapter.PrimitiveInternSymbol("Boolean");
            Console.WriteLine($"Boolean symbol OOP: {booleanSymbolOop}");
            
            // Step 2: Check if we can lookup in the dictionary
            Console.WriteLine("Step 2: Testing dictionary lookup...");
            if (booleanSymbolOop > 0)
            {
                var booleanFromDict = vmAdapter.PrimitiveDictionaryAt(20, booleanSymbolOop); // SystemDictionary is at OOP 20
                Console.WriteLine($"Boolean class from dictionary: {booleanFromDict}");
            }
            
            // Step 3: Test the full lookup process
            Console.WriteLine("Step 3: Testing full class lookup...");
            var objectClassOop = vmAdapter.PrimitiveLookupClass("Object");
            var behaviorClassOop = vmAdapter.PrimitiveLookupClass("Behavior");
            var booleanClassOop2 = vmAdapter.PrimitiveLookupClass("Boolean");
            Console.WriteLine($"Object class OOP: {objectClassOop}");
            Console.WriteLine($"Behavior class OOP: {behaviorClassOop}");
            Console.WriteLine($"Boolean class OOP: {booleanClassOop2}");
            Console.WriteLine();
            
            // Test: Install Boolean class new method
            Console.WriteLine("Installing Boolean class new method with 10-send sequence...");
            
            // Boolean class new method bytecodes and literals as specified by user
            byte[] bytecodes = { 112, 33, 224, 124 };  // self error: 'message' ^self
            object[] literals = { "error:", "You may not create any more Booleans - this is two-valued logic" };
            
            vmAdapter.InstallClassSideMethodAuthentic("Boolean", "new", bytecodes, literals, 0);
            
            Console.WriteLine();
            Console.WriteLine("Method installation completed");
            
            // Verify the method was installed
            Console.WriteLine("Verifying method installation...");
            
            var booleanClassOop = vmAdapter.PrimitiveLookupClass("Boolean");
            if (booleanClassOop > 0)
            {
                var metaclassOop = vmAdapter.GetClassOfClass(booleanClassOop);
                if (metaclassOop > 0)
                {
                    var methodDictOop = vmAdapter.GetSlot(metaclassOop, 1);
                    if (methodDictOop > 0)
                    {
                        var selectorOop = vmAdapter.PrimitiveInternSymbol("new");
                        var methodOop = vmAdapter.PrimitiveDictionaryAt(methodDictOop, selectorOop);
                        
                        if (methodOop > 0)
                        {
                            Console.WriteLine($"SUCCESS: new method found in Boolean class methodDict (OOP {methodOop})");
                        }
                        else
                        {
                            Console.WriteLine("ERROR: new method not found in Boolean class methodDict");
                        }
                    }
                    else
                    {
                        Console.WriteLine("ERROR: No method dictionary found in Boolean metaclass");
                    }
                }
                else
                {
                    Console.WriteLine("ERROR: Boolean metaclass not found");
                }
            }
            else
            {
                Console.WriteLine("ERROR: Boolean class not found");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Test completed.");
    }
}