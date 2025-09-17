using System;
using Smalltalk80.Bootstrap;

/// <summary>
/// Test program to verify class-side method installation with the Boolean new method.
/// </summary>
class TestClassSideMethodInstallation
{
    static void Main(string[] args)
    {
        Console.WriteLine("Testing Class-Side Method Installation");
        Console.WriteLine("=====================================");
        
        try
        {
            // Create the VM and bootstrap
            var vm = new VMPrimitiveAdapter();
            
            Console.WriteLine("Loading bootstrap phases...");
            
            // Load Phase 1 (core classes)
            if (!vm.LoadBootstrapPhase1())
            {
                Console.WriteLine("ERROR: Failed to load Phase 1");
                return;
            }
            
            // Load Phase 2 (methods)
            if (!vm.LoadBootstrapPhase2())
            {
                Console.WriteLine("ERROR: Failed to load Phase 2");
                return;
            }
            
            Console.WriteLine("Bootstrap phases loaded successfully");
            Console.WriteLine();
            
            // Test: Install Boolean class new method
            Console.WriteLine("Installing Boolean class new method with 10-send sequence...");
            
            // Boolean class new method bytecodes and literals as specified by user
            byte[] bytecodes = { 112, 33, 224, 124 };  // self error: 'message' ^self
            object[] literals = { "error:", "You may not create any more Booleans - this is two-valued logic" };
            
            vm.InstallClassSideMethodAuthentic("Boolean", "new", bytecodes, literals, 0);
            
            Console.WriteLine();
            Console.WriteLine("Method installation completed");
            
            // Verify the method was installed
            Console.WriteLine("Verifying method installation...");
            
            var booleanClassOop = vm.PrimitiveLookupClass("Boolean");
            if (booleanClassOop > 0)
            {
                var metaclassOop = vm.GetClassOfClass(booleanClassOop);
                if (metaclassOop > 0)
                {
                    var methodDictOop = vm.GetSlot(metaclassOop, 1);
                    if (methodDictOop > 0)
                    {
                        var selectorOop = vm.PrimitiveInternSymbol("new");
                        var methodOop = vm.PrimitiveDictionaryAt(methodDictOop, selectorOop);
                        
                        if (methodOop > 0)
                        {
                            Console.WriteLine($"SUCCESS: new method found in Boolean class methodDict (OOP {methodOop})");
                            
                            // Try to extract and display the method
                            var methodObj = vm.ObjectMemory.FromOop(methodOop);
                            if (methodObj != null)
                            {
                                Console.WriteLine($"Method object has {methodObj.Fields.Length} fields");
                                
                                // Display method structure for verification
                                for (int i = 0; i < methodObj.Fields.Length; i++)
                                {
                                    var field = methodObj.Fields[i];
                                    string fieldDesc = field?.ToString() ?? "null";
                                    if (field is object[] arr)
                                    {
                                        fieldDesc = $"Array[{arr.Length}]: [{string.Join(", ", arr)}]";
                                    }
                                    Console.WriteLine($"  Field[{i}]: {fieldDesc}");
                                }
                            }
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
        Console.WriteLine("Test completed. Press any key to exit.");
        Console.ReadKey();
    }
}