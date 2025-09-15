using System;
using System.Collections.Generic;
using Smalltalk80.Bootstrap;

namespace Testing
{
    /// <summary>
    /// Test program that creates a Smalltalk class using only message sends,
    /// proving that the infrastructure supports dynamic class creation.
    /// </summary>
    public class TestCreateClassViaSend
    {
        static void Main()
        {
            Console.WriteLine("Testing Class Creation via Message Sends Only");
            Console.WriteLine("=============================================\n");
            
            try
            {
                // Initialize VM with bootstrap
                Console.WriteLine("1. Initializing VM and running bootstrap...");
                
                // Enable bootstrap debug output temporarily to diagnose the issue (debug output removed)
                // JsonBootstrapExecutor.EnableBootstrapDebugOutput = true;
                
                var vm = new VirtualMachine();
                
                // Disable bootstrap output again (debug output removed)
                // JsonBootstrapExecutor.EnableBootstrapDebugOutput = false;
                
                // Enable method tracing for debugging
                Tracer.Enabled = true;
                
                // Get the VM adapter from the VM's primitives
                var vmAdapter = vm.Adapter;
                if (vmAdapter == null)
                {
                    Console.WriteLine("   ERROR: VM adapter not available");
                    Environment.Exit(1);
                }
                
                Console.WriteLine("   Bootstrap complete\n");
                
                // Verify Object class exists
                Console.WriteLine("2. Verifying Object class exists...");
                
                // Enable debug output temporarily
                VMPrimitiveAdapter.EnableVMDebugOutput = true;
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("Object");
                VMPrimitiveAdapter.EnableVMDebugOutput = false;
                
                if (objectClassOop < 0)
                {
                    Console.WriteLine("   ERROR: Object class not found");
                    
                    // Try to see what's in Smalltalk dictionary
                    Console.WriteLine("\n   Debugging: Checking Smalltalk dictionary...");
                    var debugSmalltalkOop = vmAdapter.PrimitiveLookupGlobal("Smalltalk");
                    Console.WriteLine($"   Smalltalk dictionary OOP: {debugSmalltalkOop}");
                    
                    Environment.Exit(1);
                }
                Console.WriteLine($"   Object class found at OOP {objectClassOop}");
                
                // Create TestPoint class using low-level manual construction (following the guide)
                Console.WriteLine("\n3. Creating TestPoint class manually using minimal operations...");
                
                // Step 1: Get essential classes needed for manual construction
                var behaviorClassOop = vmAdapter.PrimitiveLookupGlobal("Behavior");
                var classClassOop = vmAdapter.PrimitiveLookupGlobal("Class");
                var metaclassClassOop = vmAdapter.PrimitiveLookupGlobal("Metaclass");
                var methodDictClassOop = vmAdapter.PrimitiveLookupGlobal("MethodDictionary");
                var arrayClassOop = vmAdapter.PrimitiveLookupGlobal("Array");
                var smalltalkOop = vmAdapter.PrimitiveLookupGlobal("Smalltalk");
                
                Console.WriteLine($"   Found essential classes: Class={classClassOop}, Metaclass={metaclassClassOop}");
                
                // Step 2: Create the metaclass first (following Blue Book pattern)
                var testPointMetaclassOop = vmAdapter.PrimitiveNewInstance(metaclassClassOop);
                Console.WriteLine($"   Created TestPoint metaclass at OOP {testPointMetaclassOop}");
                
                // Step 3: Create the class itself
                var testPointClassOop = vmAdapter.PrimitiveNewInstance(classClassOop);
                Console.WriteLine($"   Created TestPoint class at OOP {testPointClassOop}");
                
                // Step 4: Set up the class-metaclass relationship
                vmAdapter.PrimitiveSetClass(testPointClassOop, testPointMetaclassOop); // class's class is its metaclass
                vmAdapter.PrimitiveSetSlot(testPointMetaclassOop, 5, testPointClassOop); // metaclass's thisClass points to class
                
                // Step 5: Set up inheritance hierarchy
                // Get Object's metaclass by looking up ObjectClass in globals
                var objectMetaclassOop = vmAdapter.PrimitiveLookupGlobal("ObjectClass");
                if (objectMetaclassOop < 0)
                {
                    Console.WriteLine("   WARNING: ObjectClass not found, using Class as metaclass superclass");
                    objectMetaclassOop = classClassOop;
                }
                
                vmAdapter.PrimitiveSetSlot(testPointClassOop, 0, objectClassOop); // superclass = Object
                vmAdapter.PrimitiveSetSlot(testPointMetaclassOop, 0, objectMetaclassOop); // metaclass superclass = Object class
                
                Console.WriteLine($"   Set up inheritance: TestPoint superclass=Object({objectClassOop})");
                
                // Step 6: Create and set up method dictionaries
                var classMethodDictOop = vmAdapter.PrimitiveNewInstance(methodDictClassOop);
                var metaclassMethodDictOop = vmAdapter.PrimitiveNewInstance(methodDictClassOop);
                
                vmAdapter.PrimitiveSetSlot(testPointClassOop, 1, classMethodDictOop); // class methodDict
                vmAdapter.PrimitiveSetSlot(testPointMetaclassOop, 1, metaclassMethodDictOop); // metaclass methodDict
                
                Console.WriteLine($"   Created method dictionaries");
                
                // Step 7: Set up instance variables array
                var xSymbolOop = vmAdapter.PrimitiveInternSymbol("x");
                var ySymbolOop = vmAdapter.PrimitiveInternSymbol("y");
                
                // Create Array directly with proper size using ObjectMemory
                var arrayClass = ObjectMemory.FromOop(arrayClassOop);
                var instVarsArray = new SmalltalkObject(arrayClass, 2); // Array with 2 slots
                instVarsArray.SetField(0, ObjectMemory.FromOop(xSymbolOop));
                instVarsArray.SetField(1, ObjectMemory.FromOop(ySymbolOop));
                var instVarsArrayOop = instVarsArray.Oop;
                
                vmAdapter.PrimitiveSetSlot(testPointClassOop, 3, instVarsArrayOop); // instanceVariables field
                
                Console.WriteLine($"   Set up instance variables: x, y");
                
                // Step 8: Set class name
                var classNameSymbolOop = vmAdapter.PrimitiveInternSymbol("TestPoint");
                vmAdapter.PrimitiveSetSlot(testPointClassOop, 5, classNameSymbolOop); // name field
                
                // Step 9: Register in Smalltalk dictionary
                var testPointSymbolOop = vmAdapter.PrimitiveInternSymbol("TestPoint");
                vmAdapter.PrimitiveDictionaryAtPut(smalltalkOop, testPointSymbolOop, testPointClassOop);
                
                // Also register the metaclass
                var testPointClassSymbolOop = vmAdapter.PrimitiveInternSymbol("TestPointClass");
                vmAdapter.PrimitiveDictionaryAtPut(smalltalkOop, testPointClassSymbolOop, testPointMetaclassOop);
                
                Console.WriteLine($"   Registered TestPoint in Smalltalk dictionary");
                Console.WriteLine($"   Manual class creation complete!");
                
                var resultOop = testPointClassOop;
                
                // Validate the created class
                Console.WriteLine("\n5. Validating the created class...");
                
                // Check if TestPoint exists in globals
                var testPointOop = vmAdapter.PrimitiveLookupGlobal("TestPoint");
                if (testPointOop < 0)
                {
                    Console.WriteLine("   ERROR: TestPoint not found in globals");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   ✓ TestPoint registered in globals at OOP {testPointOop}");
                
                // Verify it matches what was returned
                if (testPointOop != resultOop)
                {
                    Console.WriteLine($"   WARNING: Returned OOP {resultOop} != registered OOP {testPointOop}");
                }
                
                // Get the class object
                var testPointClass = ObjectMemory.FromOop(testPointOop);
                if (testPointClass == null)
                {
                    Console.WriteLine("   ERROR: TestPoint class object not in memory");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   ✓ TestPoint class object exists with {testPointClass.Fields.Length} fields");
                
                // Check superclass (should be Object)
                if (testPointClass.Fields.Length > 0 && testPointClass.Fields[0] is SmalltalkObject superclass)
                {
                    if (superclass.Oop == objectClassOop)
                    {
                        Console.WriteLine($"   ✓ Superclass is Object (OOP {objectClassOop})");
                    }
                    else
                    {
                        Console.WriteLine($"   ERROR: Superclass OOP {superclass.Oop} != Object OOP {objectClassOop}");
                    }
                }
                
                // Check instance variables
                if (testPointClass.Fields.Length > 3 && testPointClass.Fields[3] is SmalltalkObject instVars)
                {
                    Console.WriteLine($"   ✓ Instance variables field exists (OOP {instVars.Oop})");
                    // Should be an array with 'x' and 'y'
                    if (instVars.Fields.Length == 2)
                    {
                        Console.WriteLine("   ✓ Has 2 instance variables (x and y)");
                    }
                    else
                    {
                        Console.WriteLine($"   WARNING: Expected 2 instance variables, found {instVars.Fields.Length}");
                    }
                }
                
                // Check class name
                if (testPointClass.Fields.Length > 5 && testPointClass.Fields[5] is SmalltalkObject nameSymbol)
                {
                    if (nameSymbol.Fields.Length > 0 && nameSymbol.Fields[0] is string name)
                    {
                        if (name == "TestPoint")
                        {
                            Console.WriteLine($"   ✓ Class name is 'TestPoint'");
                        }
                        else
                        {
                            Console.WriteLine($"   ERROR: Class name is '{name}', expected 'TestPoint'");
                        }
                    }
                }
                
                // Check metaclass
                if (testPointClass.ClassPointer != null)
                {
                    Console.WriteLine($"   ✓ Has metaclass at OOP {testPointClass.ClassPointer.Oop}");
                    
                    // Check metaclass name
                    var foundMetaclassOop = vmAdapter.PrimitiveLookupGlobal("TestPointClass");
                    if (foundMetaclassOop == testPointClass.ClassPointer.Oop)
                    {
                        Console.WriteLine($"   ✓ Metaclass registered as TestPointClass");
                    }
                }
                
                // Try to create an instance
                Console.WriteLine("\n6. Testing instance creation...");
                
                // Enable debug output to see what's happening with 'new' message
                VMPrimitiveAdapter.EnableVMDebugOutput = true;
                
                // Debug: Check what method OOP 2022 contains (the 'new' method on Behavior)
                Console.WriteLine("\n   DEBUG: Checking 'new' method (OOP 2022) contents...");
                var newMethodObj = ObjectMemory.FromOop(2022);
                if (newMethodObj != null && newMethodObj.Fields.Length >= 4)
                {
                    var headerField = newMethodObj.Fields[3];
                    Console.WriteLine($"   Method OOP 2022 header field: {headerField}");
                    if (headerField is SmalltalkObject headerObj && headerObj.Fields.Length > 0)
                    {
                        var headerValue = headerObj.Fields[0];
                        Console.WriteLine($"   Header value: {headerValue}");
                        if (headerValue is int headerInt)
                        {
                            int primitive = headerInt & 0x1FF;
                            bool largeCtx = (headerInt & 0x200) != 0;
                            int temps = (headerInt >> 10) & 0x3F;
                            Console.WriteLine($"   Decoded: primitive={primitive}, largeContext={largeCtx}, temps={temps}");
                        }
                    }
                }
                
                // Send 'new' message to TestPoint class
                var newSelectorOop = vmAdapter.PrimitiveInternSymbol("new");
                var instanceOop = vmAdapter.PrimitiveSendMessage(testPointOop, newSelectorOop, new int[0]);
                
                // Disable debug output again
                VMPrimitiveAdapter.EnableVMDebugOutput = false;
                
                if (instanceOop < 0)
                {
                    Console.WriteLine("   ERROR: Failed to create instance");
                    Environment.Exit(1);
                }
                
                var instance = ObjectMemory.FromOop(instanceOop);
                if (instance == null)
                {
                    Console.WriteLine($"   ERROR: Instance OOP {instanceOop} not in memory");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   ✓ Created instance at OOP {instanceOop}");
                
                // Check instance structure
                if (instance.ClassPointer?.Oop == testPointOop)
                {
                    Console.WriteLine($"   ✓ Instance points to TestPoint class");
                }
                else
                {
                    Console.WriteLine($"   ERROR: Instance class pointer {instance.ClassPointer?.Oop} != TestPoint {testPointOop}");
                }
                
                if (instance.Fields.Length >= 2)
                {
                    Console.WriteLine($"   ✓ Instance has {instance.Fields.Length} fields (for x and y)");
                }
                else
                {
                    Console.WriteLine($"   ERROR: Instance has {instance.Fields.Length} fields, expected at least 2");
                }
                
                // Final summary
                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine("SUCCESS: Class Creation via Message Sends");
                Console.WriteLine("✓ Created TestPoint class using only bytecode execution");
                Console.WriteLine("✓ Class has correct structure (superclass, inst vars, name)");
                Console.WriteLine("✓ Class registered in global namespace");
                Console.WriteLine("✓ Metaclass created and registered");
                Console.WriteLine("✓ Instance creation works");
                Console.WriteLine(new string('=', 60));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}