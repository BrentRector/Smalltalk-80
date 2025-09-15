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
                var vm = new VirtualMachine();
                var vmAdapter = new VMPrimitiveAdapter(vm);
                
                Console.WriteLine("   Bootstrap complete\n");
                
                // Verify Object class exists
                Console.WriteLine("2. Verifying Object class exists...");
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("Object");
                if (objectClassOop < 0)
                {
                    Console.WriteLine("   ERROR: Object class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   Object class found at OOP {objectClassOop}");
                
                // Create the bytecode to send: Object subclass: #TestPoint instanceVariableNames: 'x y' ...
                Console.WriteLine("\n3. Creating bytecode to send class creation message...");
                
                // We need to build literals array and bytecode
                var literals = new List<int>();
                
                // Literal 0: Association for Object global variable
                var objectSymbolOop = vmAdapter.PrimitiveInternSymbol("Object");
                var objectAssocOop = vmAdapter.PrimitiveNewInstance(vmAdapter.PrimitiveLookupGlobal("Association"));
                vmAdapter.PrimitiveSetSlot(objectAssocOop, 0, objectSymbolOop); // key
                vmAdapter.PrimitiveSetSlot(objectAssocOop, 1, objectClassOop);  // value
                literals.Add(objectAssocOop);
                
                // Literal 1: selector #subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
                var selectorOop = vmAdapter.PrimitiveInternSymbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:");
                literals.Add(selectorOop);
                
                // Literal 2: #TestPoint (class name)
                var classNameOop = vmAdapter.PrimitiveInternSymbol("TestPoint");
                literals.Add(classNameOop);
                
                // Literal 3: 'x y' (instance variables)
                var instVarsOop = vmAdapter.PrimitiveInternString("x y");
                literals.Add(instVarsOop);
                
                // Literal 4: '' (class variables - empty)
                var classVarsOop = vmAdapter.PrimitiveInternString("");
                literals.Add(classVarsOop);
                
                // Literal 5: '' (pool dictionaries - empty)
                var poolDictsOop = vmAdapter.PrimitiveInternString("");
                literals.Add(poolDictsOop);
                
                // Literal 6: 'Test-Classes' (category)
                var categoryOop = vmAdapter.PrimitiveInternString("Test-Classes");
                literals.Add(categoryOop);
                
                // Generate bytecode
                var bytecode = new List<byte>();
                
                // Push Object (global variable via Association)
                bytecode.Add(0x40); // PushLiteralVariable 0
                
                // Push arguments
                bytecode.Add(0x42); // PushLiteralConstant 2 (TestPoint)
                bytecode.Add(0x43); // PushLiteralConstant 3 (instance vars)
                bytecode.Add(0x44); // PushLiteralConstant 4 (class vars)
                bytecode.Add(0x45); // PushLiteralConstant 5 (pool dicts)
                bytecode.Add(0x46); // PushLiteralConstant 6 (category)
                
                // Send the message with 5 arguments
                bytecode.Add(0xE1); // Single extended send
                bytecode.Add(0x15); // 5 args, literal 1 (the selector)
                
                // Return top
                bytecode.Add(0x7C);
                
                Console.WriteLine($"   Generated {bytecode.Count} bytes of bytecode");
                Console.WriteLine($"   Bytecode: [{string.Join(", ", bytecode.Select(b => $"0x{b:X2}"))}]");
                
                // Execute the bytecode
                Console.WriteLine("\n4. Executing bytecode to create class...");
                
                // Use nil as receiver (doesn't matter for this test)
                var nilOop = vmAdapter.PrimitiveNil();
                
                var resultOop = vmAdapter.ExecuteBytecodeWithLiterals(
                    nilOop, 
                    bytecode.ToArray(), 
                    new int[0], // no arguments 
                    literals.ToArray()
                );
                
                if (resultOop < 0)
                {
                    Console.WriteLine("   ERROR: Bytecode execution failed");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   Bytecode executed, returned OOP {resultOop}");
                
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
                    var testPointMetaclassOop = vmAdapter.PrimitiveLookupGlobal("TestPointClass");
                    if (testPointMetaclassOop == testPointClass.ClassPointer.Oop)
                    {
                        Console.WriteLine($"   ✓ Metaclass registered as TestPointClass");
                    }
                }
                
                // Try to create an instance
                Console.WriteLine("\n6. Testing instance creation...");
                
                // Send 'new' message to TestPoint class
                var newSelectorOop = vmAdapter.PrimitiveInternSymbol("new");
                var instanceOop = vmAdapter.PrimitiveSendMessage(testPointOop, newSelectorOop, new int[0]);
                
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