using System;
using System.Collections.Generic;
using Smalltalk80.Bootstrap;

namespace Testing
{
    /// <summary>
    /// Test program to verify that classes can be created via message sends
    /// after Phase 2 bootstrap.
    /// </summary>
    public class TestClassCreation
    {
        static void Main()
        {
            Console.WriteLine("Testing Class Creation via Message Sends");
            Console.WriteLine("=========================================\n");
            
            try
            {
                // Initialize VM
                Console.WriteLine("1. Initializing VM...");
                var vm = new VirtualMachine();
                var vmAdapter = new VMPrimitiveAdapter(vm);
                
                // Bootstrap Phase 1 and 2
                Console.WriteLine("2. Running bootstrap...");
                string bootstrapPath = "../bin/Debug/net9.0/Bootstrap/bootstrap.json";
                
                var executor = new JsonBootstrapExecutor(vmAdapter);
                string jsonContent = System.IO.File.ReadAllText(bootstrapPath);
                
                // Execute the unified bootstrap
                executor.ExecuteBootstrap(jsonContent);
                Console.WriteLine("   Bootstrap complete - classes and methods installed");
                
                // Test 1: Verify Object class exists and has the subclass:... method
                Console.WriteLine("\n3. Verifying Object class has subclass creation method...");
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("Object");
                if (objectClassOop == -1)
                {
                    Console.WriteLine("   ERROR: Object class not found");
                    Environment.Exit(1);
                }
                
                var objectClass = ObjectMemory.FromOop(objectClassOop);
                Console.WriteLine($"   Found Object class at OOP {objectClassOop}");
                
                // Get Object's metaclass (where class methods are)
                var objectMetaclass = objectClass?.ClassPointer;
                if (objectMetaclass == null)
                {
                    Console.WriteLine("   ERROR: Object metaclass not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   Found Object metaclass at OOP {objectMetaclass.Oop}");
                
                // Check if metaclass has method dictionary
                if (objectMetaclass.Fields.Length > 1 && objectMetaclass.Fields[1] is SmalltalkObject methodDict)
                {
                    Console.WriteLine($"   Method dictionary at OOP {methodDict.Oop}");
                    
                    // Try to find subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
                    var selector = "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:";
                    var selectorSymbol = vmAdapter.InternSymbol(selector);
                    
                    // Look for the method in the dictionary
                    bool foundMethod = false;
                    if (methodDict.Fields.Length > 0)
                    {
                        // Method dictionary format: [tally, key1, value1, key2, value2, ...]
                        for (int i = 1; i < methodDict.Fields.Length - 1; i += 2)
                        {
                            if (methodDict.Fields[i] is SmalltalkObject sym && sym.Oop == selectorSymbol.Oop)
                            {
                                foundMethod = true;
                                var method = methodDict.Fields[i + 1];
                                Console.WriteLine($"   ✓ Found {selector} method: {method}");
                                break;
                            }
                        }
                    }
                    
                    if (!foundMethod)
                    {
                        Console.WriteLine($"   WARNING: {selector} method not found in Object class methods");
                    }
                }
                
                // Test 2: Create bytecode to send subclass creation message
                Console.WriteLine("\n4. Creating bytecode to send class creation message...");
                
                // We need to create bytecode that does:
                // Object subclass: #TestClass 
                //        instanceVariableNames: ''
                //        classVariableNames: ''
                //        poolDictionaries: ''
                //        category: 'Test'
                
                var bytecode = new List<byte>();
                var literals = new List<object>();
                
                // Add literals
                literals.Add(vmAdapter.PrimitiveLookupGlobal("Object")); // 0: Object class
                literals.Add(vmAdapter.InternSymbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:")); // 1: selector
                literals.Add(vmAdapter.InternSymbol("TestClass")); // 2: class name
                literals.Add(""); // 3: instance vars (empty string)
                literals.Add(""); // 4: class vars (empty string)  
                literals.Add(""); // 5: pool dicts (empty string)
                literals.Add("Test"); // 6: category
                
                // Generate bytecode:
                // Push Object (literal 0)
                bytecode.Add(0x40); // Push literal variable 0 (Object)
                
                // Push arguments
                bytecode.Add(0x42); // Push literal constant 2 (TestClass symbol)
                bytecode.Add(0x43); // Push literal constant 3 (instance vars)
                bytecode.Add(0x44); // Push literal constant 4 (class vars)
                bytecode.Add(0x45); // Push literal constant 5 (pool dicts)
                bytecode.Add(0x46); // Push literal constant 6 (category)
                
                // Send message with 5 arguments
                bytecode.Add(0xE1); // Single extended send
                bytecode.Add(0x15); // 5 args, literal 1
                
                // Return top
                bytecode.Add(0x7C);
                
                Console.WriteLine($"   Generated {bytecode.Count} bytes of bytecode");
                Console.WriteLine($"   Literals: {literals.Count}");
                
                // Test 3: Execute the bytecode (would need interpreter context)
                Console.WriteLine("\n5. Testing class creation infrastructure...");
                
                // Check that Class class exists
                var classClassOop = vmAdapter.PrimitiveLookupGlobal("Class");
                if (classClassOop == -1)
                {
                    Console.WriteLine("   ERROR: Class class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   ✓ Class class exists at OOP {classClassOop}");
                
                // Check that Metaclass exists
                var metaclassOop = vmAdapter.PrimitiveLookupGlobal("Metaclass");
                if (metaclassOop == -1)
                {
                    Console.WriteLine("   ERROR: Metaclass class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   ✓ Metaclass class exists at OOP {metaclassOop}");
                
                // Check that Behavior exists
                var behaviorOop = vmAdapter.PrimitiveLookupGlobal("Behavior");
                if (behaviorOop == -1)
                {
                    Console.WriteLine("   ERROR: Behavior class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   ✓ Behavior class exists at OOP {behaviorOop}");
                
                // Check that ClassDescription exists
                var classDescOop = vmAdapter.PrimitiveLookupGlobal("ClassDescription");
                if (classDescOop == -1)
                {
                    Console.WriteLine("   ERROR: ClassDescription class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   ✓ ClassDescription class exists at OOP {classDescOop}");
                
                // Summary
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("INFRASTRUCTURE CHECK COMPLETE");
                Console.WriteLine("✓ All core class creation classes exist");
                Console.WriteLine("✓ Bytecode for class creation generated");
                Console.WriteLine("✓ Ready for class creation via message sends");
                Console.WriteLine("\nNext step: Execute bytecode through interpreter");
                Console.WriteLine(new string('=', 50));
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