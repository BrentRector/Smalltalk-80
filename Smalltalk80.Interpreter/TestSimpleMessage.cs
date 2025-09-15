using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter
{
    /// <summary>
    /// Test simple message sending to verify infrastructure works.
    /// </summary>
    public class TestSimpleMessage
    {
        public static void RunTest(VirtualMachine vm)
        {
            Console.WriteLine("\n=== Testing Simple Message Send ===\n");
            
            if (vm.Adapter == null)
            {
                Console.WriteLine("ERROR: VM adapter not available");
                return;
            }
            
            var adapter = vm.Adapter;
            
            try
            {
                // Get Collection class
                Console.WriteLine("1. Getting Collection class...");
                int collectionOop = adapter.PrimitiveLookupGlobal("Collection");
                if (collectionOop < 0)
                {
                    Console.WriteLine("  ERROR: Collection class not found");
                    return;
                }
                Console.WriteLine($"  Collection OOP: {collectionOop}");
                
                // Test 1: Send 'superclass' message
                Console.WriteLine("\n2. Sending 'superclass' to Collection...");
                
                var bytecode = new System.Collections.Generic.List<byte>();
                var literals = new System.Collections.Generic.List<int>();
                
                // Push receiver (Collection) - it's already the receiver
                bytecode.Add(0x70); // Push receiver
                
                // Send superclass (0 args)
                int superclassSymbol = adapter.PrimitiveInternSymbol("superclass");
                literals.Add(superclassSymbol);
                bytecode.Add((byte)(0xD0 + 0)); // Send literal selector 0 with 0 args
                
                // Return top
                bytecode.Add(0x7C);
                
                Console.WriteLine($"  Bytecode: {string.Join(" ", bytecode)}");
                
                int result = adapter.ExecuteBytecodeWithLiterals(
                    collectionOop, // receiver
                    bytecode.ToArray(),
                    new int[0],
                    literals.ToArray()
                );
                
                Console.WriteLine($"  Result OOP: {result}");
                
                // Test 2: Send 'name' message
                Console.WriteLine("\n3. Sending 'name' to Collection...");
                
                bytecode.Clear();
                literals.Clear();
                
                bytecode.Add(0x70); // Push receiver
                
                int nameSymbol = adapter.PrimitiveInternSymbol("name");
                literals.Add(nameSymbol);
                bytecode.Add(0xD0); // Send literal selector 0 with 0 args
                
                bytecode.Add(0x7C); // Return top
                
                result = adapter.ExecuteBytecodeWithLiterals(
                    collectionOop,
                    bytecode.ToArray(),
                    new int[0],
                    literals.ToArray()
                );
                
                Console.WriteLine($"  Result OOP: {result}");
                
                // Test 3: Try to call subclass:... with just one argument to see what happens
                Console.WriteLine("\n4. Testing subclass: with single argument...");
                
                bytecode.Clear();
                literals.Clear();
                
                // Push receiver
                bytecode.Add(0x70);
                
                // Push argument (a symbol)
                int testSymbol = adapter.PrimitiveInternSymbol("TestClass");
                literals.Add(testSymbol);
                bytecode.Add(0x20); // Push literal constant 0
                
                // Send subclass:
                int subclassSymbol = adapter.PrimitiveInternSymbol("subclass:");
                literals.Add(subclassSymbol);
                bytecode.Add((byte)(0xE0 + 1)); // Send literal selector 1 with 1 arg
                
                bytecode.Add(0x7C); // Return
                
                result = adapter.ExecuteBytecodeWithLiterals(
                    collectionOop,
                    bytecode.ToArray(),
                    new int[0],
                    literals.ToArray()
                );
                
                Console.WriteLine($"  Result OOP: {result}");
                
                Console.WriteLine("\n=== Test Complete ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            }
        }
    }
}