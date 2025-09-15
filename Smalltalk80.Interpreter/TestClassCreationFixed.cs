using System;
using System.Collections.Generic;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// Fixed test for creating classes via Smalltalk message sends.
    /// Properly handles Association objects for global variables.
    /// </summary>
    public class TestClassCreationFixed
    {
        public static void RunTest(VirtualMachine vm)
        {
            Console.WriteLine("\n=== Testing Class Creation via Message Sends (Fixed) ===\n");
            
            if (vm.Adapter == null)
            {
                Console.WriteLine("ERROR: VM adapter not available");
                return;
            }
            
            var adapter = vm.Adapter;
            
            try
            {
                // First, check if we have Collection class
                Console.WriteLine("1. Checking for Collection class...");
                int collectionOop = adapter.PrimitiveLookupGlobal("Collection");
                if (collectionOop < 0)
                {
                    Console.WriteLine("  ERROR: Collection class not found");
                    return;
                }
                Console.WriteLine($"  Found Collection (OOP: {collectionOop})");
                
                // Get Association class
                Console.WriteLine("\n2. Getting Association class...");
                int associationClassOop = adapter.PrimitiveLookupGlobal("Association");
                if (associationClassOop < 0)
                {
                    Console.WriteLine("  ERROR: Association class not found");
                    return;
                }
                Console.WriteLine($"  Found Association (OOP: {associationClassOop})");
                
                // Try to create Interval class via message send
                Console.WriteLine("\n3. Creating Interval class via message send...");
                Console.WriteLine("   Executing: Collection subclass: #Interval instanceVariableNames: 'start stop step' ...");
                
                // Create symbols and strings we need
                int intervalSymbol = adapter.PrimitiveInternSymbol("Interval");
                int instVarsString = adapter.PrimitiveInternString("start stop step");
                int emptyString = adapter.PrimitiveInternString("");
                int categoryString = adapter.PrimitiveInternString("Collections-Sequenceable");
                
                // Create the selector symbol
                int selectorSymbol = adapter.PrimitiveInternSymbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:");
                
                Console.WriteLine($"   Symbols/strings created:");
                Console.WriteLine($"     Interval symbol: {intervalSymbol}");
                Console.WriteLine($"     Selector symbol: {selectorSymbol}");
                
                // Build bytecode to send the message
                var bytecode = new List<byte>();
                var literals = new List<int>();
                
                // Create an Association for Collection global variable
                // We need to create: Association key: #Collection value: Collection
                Console.WriteLine("\n4. Creating Association for Collection global...");
                
                // Get symbols for creating the Association
                int collectionSymbol = adapter.PrimitiveInternSymbol("Collection");
                int keySymbol = adapter.PrimitiveInternSymbol("key:");
                int valueSymbol = adapter.PrimitiveInternSymbol("value:");
                int keyValueSymbol = adapter.PrimitiveInternSymbol("key:value:");
                
                // Build bytecode to create Association manually
                var assocBytecode = new List<byte>();
                var assocLiterals = new List<int>();
                
                // First create an Association instance
                // Push Association class
                assocLiterals.Add(associationClassOop);
                assocBytecode.Add(0x20); // Push literal constant 0
                
                // Send new message
                int newSymbol = adapter.PrimitiveInternSymbol("new");
                assocLiterals.Add(newSymbol);
                assocBytecode.Add((byte)(176 + 1)); // Send literal selector 1 with 0 args (176-191 range)
                
                // Now the stack has the new Association instance
                // Duplicate it twice (we need it for key: and value: messages)
                assocBytecode.Add(0x88); // Duplicate top
                assocBytecode.Add(0x88); // Duplicate top again
                
                // Set the key
                // Push key (Collection symbol)
                assocLiterals.Add(collectionSymbol);
                assocBytecode.Add(0x22); // Push literal constant 2
                
                // Send key: message
                assocLiterals.Add(keySymbol);
                assocBytecode.Add((byte)(192 + 3)); // Send literal selector 3 with 1 arg (192-207 range)
                
                // Pop the result (key: returns the receiver)
                assocBytecode.Add(0x87); // Pop stack
                
                // Set the value
                // Push value (Collection class)
                assocLiterals.Add(collectionOop);
                assocBytecode.Add(0x24); // Push literal constant 4
                
                // Send value: message
                assocLiterals.Add(valueSymbol);
                assocBytecode.Add((byte)(192 + 5)); // Send literal selector 5 with 1 arg (192-207 range)
                
                // Pop the result (value: returns the receiver)
                assocBytecode.Add(0x87); // Pop stack
                
                // Return
                assocBytecode.Add(0x7C);
                
                Console.WriteLine("   Creating Association via bytecode...");
                int associationOop = adapter.ExecuteBytecodeWithLiterals(
                    associationClassOop, // receiver is Association class
                    assocBytecode.ToArray(),
                    new int[0],
                    assocLiterals.ToArray()
                );
                
                if (associationOop < 0)
                {
                    Console.WriteLine("   ERROR: Failed to create Association");
                    return;
                }
                Console.WriteLine($"   Association created (OOP: {associationOop})");
                
                // Now build the main bytecode using the Association
                Console.WriteLine("\n5. Building class creation bytecode...");
                
                // Push Collection via the Association (PushLiteralVariable)
                literals.Add(associationOop);
                bytecode.Add(0x40); // PushLiteralVariable 0
                
                // Push arguments
                literals.Add(intervalSymbol);
                bytecode.Add((byte)(0x20 + 1)); // Push literal constant 1
                
                literals.Add(instVarsString);
                bytecode.Add((byte)(0x20 + 2)); // Push literal constant 2
                
                literals.Add(emptyString);
                bytecode.Add((byte)(0x20 + 3)); // Push literal constant 3
                
                literals.Add(emptyString);
                bytecode.Add((byte)(0x20 + 4)); // Push literal constant 4
                
                literals.Add(categoryString);
                bytecode.Add((byte)(0x20 + 5)); // Push literal constant 5
                
                // Send the message (5 arguments)
                literals.Add(selectorSymbol);
                // Extended send for 5 arguments
                bytecode.Add(0x83); // Extended send
                bytecode.Add((byte)((5 << 5) | 6)); // 5 args, literal 6
                
                // Return top
                bytecode.Add(0x7C);
                
                Console.WriteLine($"   Bytecode: {string.Join(" ", bytecode)}");
                Console.WriteLine($"   Literals: {literals.Count}");
                
                // Execute the bytecode
                Console.WriteLine("\n6. Executing class creation bytecode...");
                int result = adapter.ExecuteBytecodeWithLiterals(
                    adapter.PrimitiveLookupGlobal("Smalltalk"), // execute in Smalltalk context
                    bytecode.ToArray(),
                    new int[0],
                    literals.ToArray()
                );
                
                Console.WriteLine($"   Result OOP: {result}");
                
                if (result < 0)
                {
                    Console.WriteLine("   ERROR: Class creation returned negative OOP");
                    return;
                }
                
                // Verify the class was created
                Console.WriteLine("\n7. Verifying Interval class...");
                int intervalOop = adapter.PrimitiveLookupGlobal("Interval");
                if (intervalOop < 0)
                {
                    Console.WriteLine("   ERROR: Interval not found in globals");
                    return;
                }
                Console.WriteLine($"   Interval found in globals (OOP: {intervalOop})");
                
                // Check its superclass
                int superclassOop = adapter.GetSlot(intervalOop, 0); // slot 0 is superclass
                Console.WriteLine($"   Superclass OOP: {superclassOop} (should be {collectionOop})");
                
                if (superclassOop == collectionOop)
                {
                    Console.WriteLine("   ✓ Superclass is Collection!");
                }
                else
                {
                    Console.WriteLine("   WARNING: Superclass is not Collection");
                }
                
                // Try to create an instance using basicNew
                Console.WriteLine("\n8. Testing instance creation...");
                
                // Create Association for Interval global
                var instAssocBytecode = new List<byte>();
                var instAssocLiterals = new List<int>();
                
                // Push Association class and create instance
                instAssocLiterals.Add(associationClassOop);
                instAssocBytecode.Add(0x20); // Push literal constant 0
                
                instAssocLiterals.Add(newSymbol); // Reuse new symbol
                instAssocBytecode.Add((byte)(176 + 1)); // Send new with 0 args
                
                // Duplicate twice for key: and value:
                instAssocBytecode.Add(0x88); // Duplicate
                instAssocBytecode.Add(0x88); // Duplicate
                
                // Set key
                instAssocLiterals.Add(intervalSymbol);
                instAssocBytecode.Add(0x22); // Push literal constant 2
                
                instAssocLiterals.Add(keySymbol); // Reuse key: symbol
                instAssocBytecode.Add((byte)(192 + 3)); // Send key: with 1 arg
                
                instAssocBytecode.Add(0x87); // Pop
                
                // Set value
                instAssocLiterals.Add(intervalOop);
                instAssocBytecode.Add(0x24); // Push literal constant 4
                
                instAssocLiterals.Add(valueSymbol); // Reuse value: symbol
                instAssocBytecode.Add((byte)(192 + 5)); // Send value: with 1 arg
                
                instAssocBytecode.Add(0x87); // Pop
                
                // Return
                instAssocBytecode.Add(0x7C);
                
                int intervalAssocOop = adapter.ExecuteBytecodeWithLiterals(
                    associationClassOop,
                    instAssocBytecode.ToArray(),
                    new int[0],
                    instAssocLiterals.ToArray()
                );
                
                if (intervalAssocOop < 0)
                {
                    Console.WriteLine("   ERROR: Failed to create Interval Association");
                    return;
                }
                
                var instBytecode = new List<byte>();
                var instLiterals = new List<int>();
                
                // Push Interval via Association
                instLiterals.Add(intervalAssocOop);
                instBytecode.Add(0x40); // Push literal variable 0
                
                // Send basicNew
                instLiterals.Add(adapter.PrimitiveInternSymbol("basicNew"));
                instBytecode.Add((byte)(0xD0 + 1)); // Send literal selector 1 with 0 args
                
                // Return
                instBytecode.Add(0x7C);
                
                int instance = adapter.ExecuteBytecodeWithLiterals(
                    adapter.PrimitiveLookupGlobal("Smalltalk"),
                    instBytecode.ToArray(),
                    new int[0],
                    instLiterals.ToArray()
                );
                
                Console.WriteLine($"   Instance OOP: {instance}");
                
                if (instance > 0)
                {
                    Console.WriteLine("   ✓ Instance created successfully!");
                    Console.WriteLine($"   Instance appears to be valid Interval instance");
                }
                else
                {
                    Console.WriteLine("   ERROR: Could not create instance");
                }
                
                Console.WriteLine("\n=== Test Complete ===");
                
                if (intervalOop > 0 && instance > 0)
                {
                    Console.WriteLine("✓ SUCCESS: Class creation via message sends works!");
                }
                else
                {
                    Console.WriteLine("✗ FAILURE: Class creation did not work properly");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            }
        }
    }
}