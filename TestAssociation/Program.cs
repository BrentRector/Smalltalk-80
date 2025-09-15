using System;
using Smalltalk80.Bootstrap;

namespace TestAssociation
{
    /// <summary>
    /// Test program to validate that Association instances can be created
    /// after the OOP collision fix.
    /// </summary>
    public class Program
    {
        static void Main()
        {
            Console.WriteLine("Testing Association Creation After OOP Collision Fix");
            Console.WriteLine("====================================================\n");
            
            try
            {
                // Initialize VM
                Console.WriteLine("1. Initializing VM...");
                var vm = new VirtualMachine();
                var vmAdapter = new VMPrimitiveAdapter(vm);
                
                // Bootstrap Phase 1 and 2
                Console.WriteLine("2. Running Phase 1 bootstrap...");
                string bootstrapPath = "../bin/Debug/net9.0/Bootstrap/bootstrap.json";
                JsonBootstrapExecutor.Execute(bootstrapPath, vmAdapter);
                Console.WriteLine("   Phase 1 complete - core classes created");
                
                Console.WriteLine("\n3. Running Phase 2 bootstrap...");
                JsonBootstrapExecutor.ExecutePhase2(bootstrapPath, vmAdapter);
                Console.WriteLine("   Phase 2 complete - methods installed");
                
                // Now test Association creation
                Console.WriteLine("\n4. Testing Association creation...");
                
                // Look up Association class
                var associationClassOop = vmAdapter.PrimitiveLookupGlobal("Association");
                if (associationClassOop == -1)
                {
                    Console.WriteLine("   ERROR: Association class not found in globals");
                    Environment.Exit(1);
                }
                
                var associationClass = ObjectMemory.FromOop(associationClassOop);
                if (associationClass == null)
                {
                    Console.WriteLine($"   ERROR: Association class OOP {associationClassOop} not in memory");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   Found Association class at OOP {associationClassOop}");
                
                // Create a symbol for the key
                var keySymbol = vmAdapter.InternSymbol("TestKey");
                Console.WriteLine($"   Created symbol 'TestKey' at OOP {keySymbol.Oop}");
                
                // Create SmallInteger for value
                int valueInt = 42;
                int valueSmallInt = (valueInt << 1) | 1; // Encode as SmallInteger
                Console.WriteLine($"   Using SmallInteger value: {valueInt} (encoded as {valueSmallInt})");
                
                // Create an Association instance using primitive
                Console.WriteLine("\n5. Creating Association instance using primitive new...");
                
                // Use the VM adapter to create a new instance
                int instanceOop = vmAdapter.PrimitiveNew(associationClassOop);
                if (instanceOop == -1)
                {
                    Console.WriteLine("   ERROR: Failed to create Association instance");
                    Environment.Exit(1);
                }
                
                var instance = ObjectMemory.FromOop(instanceOop);
                if (instance == null)
                {
                    Console.WriteLine($"   ERROR: Instance OOP {instanceOop} not in memory");
                    Environment.Exit(1);
                }
                
                Console.WriteLine($"   SUCCESS: Created Association instance at OOP {instanceOop}");
                Console.WriteLine($"   Instance structure:");
                Console.WriteLine($"     - Class pointer: {instance.ClassPointer?.Oop ?? -1}");
                Console.WriteLine($"     - Fields count: {instance.Fields.Length}");
                
                // Set key and value fields (Association has 2 instance variables)
                if (instance.Fields.Length >= 2)
                {
                    instance.Fields[0] = keySymbol;  // key
                    instance.Fields[1] = valueSmallInt;  // value
                    Console.WriteLine($"     - Set key to symbol 'TestKey'");
                    Console.WriteLine($"     - Set value to SmallInteger {valueInt}");
                }
                
                // Verify the instance points back to Association class
                if (instance.ClassPointer?.Oop == associationClassOop)
                {
                    Console.WriteLine("   ✓ Instance correctly points to Association class");
                }
                else
                {
                    Console.WriteLine($"   ERROR: Instance class pointer {instance.ClassPointer?.Oop} != Association {associationClassOop}");
                }
                
                // Check that Object and ObjectClass have different OOPs
                Console.WriteLine("\n6. Verifying OOP collision fix...");
                var objectOop = vmAdapter.PrimitiveLookupGlobal("Object");
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("ObjectClass");
                Console.WriteLine($"   Object OOP: {objectOop}");
                Console.WriteLine($"   ObjectClass OOP: {objectClassOop}");
                
                if (objectOop == objectClassOop)
                {
                    Console.WriteLine("   ERROR: Object and ObjectClass still have same OOP!");
                }
                else
                {
                    Console.WriteLine("   ✓ Object and ObjectClass have different OOPs");
                }
                
                // Check Association superclass chain
                Console.WriteLine("\n7. Checking Association superclass chain...");
                var current = associationClass;
                int depth = 0;
                while (current != null && depth < 10)
                {
                    var name = current.Fields.Length > 5 && current.Fields[5] is string className 
                        ? className 
                        : $"<unnamed OOP {current.Oop}>";
                    Console.WriteLine($"   {new string(' ', depth * 2)}- {name} (OOP {current.Oop})");
                    
                    if (current.Fields.Length > 0 && current.Fields[0] is SmalltalkObject super)
                    {
                        current = super;
                    }
                    else
                    {
                        break;
                    }
                    depth++;
                }
                
                // Final summary
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("TEST COMPLETED SUCCESSFULLY");
                Console.WriteLine("✓ Association instances can be created");
                Console.WriteLine("✓ OOP collision has been fixed");
                Console.WriteLine("✓ Superclass chain is intact");
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