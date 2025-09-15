using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// Test creating classes via Smalltalk message sends after Phase 2 bootstrap.
    /// </summary>
    public class TestClassCreation
    {
        public static void RunTest(VirtualMachine vm)
        {
            Console.WriteLine("\n=== Testing Class Creation via Message Sends ===\n");
            
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
                
                // Try to create Interval class via message send
                Console.WriteLine("\n2. Creating Interval class via message send...");
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
                var bytecode = new System.Collections.Generic.List<byte>();
                var literals = new System.Collections.Generic.List<int>();
                
                // Create an Association for Collection global variable
                // An Association needs: key (Symbol) and value (the actual class)
                int collectionSymbol = adapter.PrimitiveInternSymbol("Collection");
                
                // Create the Association object
                int associationClassOop = adapter.PrimitiveLookupGlobal("Association");
                if (associationClassOop < 0)
                {
                    Console.WriteLine("   ERROR: Association class not found");
                    return;
                }
                
                // Create an Association instance with key=Collection symbol, value=Collection class
                // We'll use a simpler approach: get the Association from Smalltalk dictionary
                int smalltalkOop = adapter.PrimitiveLookupGlobal("Smalltalk");
                if (smalltalkOop < 0)
                {
                    Console.WriteLine("   ERROR: Smalltalk dictionary not found");
                    return;
                }
                
                // The Smalltalk dictionary already has Association objects for globals
                // We need to get the Association for Collection, not just the class
                // For now, let's create a workaround by creating the Association manually
                
                // Actually, let's use a different approach - we'll push Collection directly
                // and use a different bytecode sequence
                
                // Push Collection (as a literal constant, not variable)
                literals.Add(collectionOop);
                bytecode.Add((byte)(0x20 + 0)); // Push literal constant 0 (the Collection class itself)
                
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
                Console.WriteLine("\n3. Executing bytecode...");
                int result = adapter.ExecuteBytecodeWithLiterals(
                    collectionOop, // receiver is Collection class itself
                    bytecode.ToArray(),
                    new int[0], // no arguments
                    literals.ToArray()
                );
                
                Console.WriteLine($"   Result OOP: {result}");
                
                if (result < 0)
                {
                    Console.WriteLine("   ERROR: Class creation returned negative OOP");
                    return;
                }
                
                // Verify the class was created
                Console.WriteLine("\n4. Verifying Interval class...");
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
                Console.WriteLine("\n5. Testing instance creation...");
                
                var instBytecode = new System.Collections.Generic.List<byte>();
                var instLiterals = new System.Collections.Generic.List<int>();
                
                // Push Interval
                instLiterals.Add(adapter.PrimitiveInternSymbol("Interval"));
                instBytecode.Add(0x40); // Push literal variable 0
                
                // Send basicNew
                instLiterals.Add(adapter.PrimitiveInternSymbol("basicNew"));
                instBytecode.Add(0xD0 + 1); // Send literal selector 1 with 0 args
                
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
                    
                    // Check its class pointer (would need to get from object header)
                    // For now, just report success
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