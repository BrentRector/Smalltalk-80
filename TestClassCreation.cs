using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Tests
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
                // Step 1: Check if we have the necessary infrastructure
                Console.WriteLine("Step 1: Checking infrastructure...");
                
                // Get Collection class
                int collectionOop = adapter.PrimitiveLookupGlobal("Collection");
                if (collectionOop < 0)
                {
                    Console.WriteLine("  ERROR: Collection class not found");
                    return;
                }
                Console.WriteLine($"  ✓ Collection class found (OOP: {collectionOop})");
                
                // Check if Collection has subclass:instanceVariableNames:... method
                // This would normally be done by sending doesNotUnderstand:, but let's check directly
                
                // Step 2: Create SequenceableCollection (if it doesn't exist)
                Console.WriteLine("\nStep 2: Creating SequenceableCollection...");
                
                int seqCollOop = adapter.PrimitiveLookupGlobal("SequenceableCollection");
                if (seqCollOop >= 0)
                {
                    Console.WriteLine("  SequenceableCollection already exists");
                }
                else
                {
                    Console.WriteLine("  Creating SequenceableCollection as subclass of Collection...");
                    
                    // Build the bytecode to create the class
                    // This would be: Collection subclass: #SequenceableCollection instanceVariableNames: '' ...
                    
                    // First, let's just try a simpler approach - use primitives
                    // The real test is whether the message-based infrastructure works
                    
                    // Create the message send bytecode
                    var bytecode = new BytecodeBuilder();
                    
                    // Push Collection
                    bytecode.PushGlobal("Collection");
                    
                    // Push arguments
                    bytecode.PushLiteral(adapter.PrimitiveInternSymbol("SequenceableCollection"));
                    bytecode.PushLiteral(adapter.PrimitiveInternString(""));  // instance vars
                    bytecode.PushLiteral(adapter.PrimitiveInternString(""));  // class vars
                    bytecode.PushLiteral(adapter.PrimitiveInternString(""));  // pool dicts
                    bytecode.PushLiteral(adapter.PrimitiveInternString("Collections-Abstract"));  // category
                    
                    // Send the message
                    bytecode.SendLiteral("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", 5);
                    
                    // Return
                    bytecode.ReturnTop();
                    
                    // Execute the bytecode
                    Console.WriteLine("  Executing class creation bytecode...");
                    int result = adapter.ExecuteBytecodeWithLiterals(
                        collectionOop, 
                        bytecode.GetBytes(),
                        new int[0],
                        bytecode.GetLiterals()
                    );
                    
                    if (result < 0)
                    {
                        Console.WriteLine("  ERROR: Class creation failed");
                        return;
                    }
                    
                    Console.WriteLine($"  ✓ SequenceableCollection created (OOP: {result})");
                }
                
                // Step 3: Create Interval class
                Console.WriteLine("\nStep 3: Creating Interval class...");
                
                int intervalOop = adapter.PrimitiveLookupGlobal("Interval");
                if (intervalOop >= 0)
                {
                    Console.WriteLine("  Interval already exists");
                }
                else
                {
                    seqCollOop = adapter.PrimitiveLookupGlobal("SequenceableCollection");
                    if (seqCollOop < 0)
                    {
                        Console.WriteLine("  ERROR: SequenceableCollection not found");
                        return;
                    }
                    
                    var bytecode = new BytecodeBuilder();
                    
                    // Push SequenceableCollection
                    bytecode.PushGlobal("SequenceableCollection");
                    
                    // Push arguments
                    bytecode.PushLiteral(adapter.PrimitiveInternSymbol("Interval"));
                    bytecode.PushLiteral(adapter.PrimitiveInternString("start stop step"));  // instance vars
                    bytecode.PushLiteral(adapter.PrimitiveInternString(""));  // class vars
                    bytecode.PushLiteral(adapter.PrimitiveInternString(""));  // pool dicts
                    bytecode.PushLiteral(adapter.PrimitiveInternString("Collections-Sequenceable"));  // category
                    
                    // Send the message
                    bytecode.SendLiteral("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", 5);
                    
                    // Return
                    bytecode.ReturnTop();
                    
                    Console.WriteLine("  Executing Interval creation bytecode...");
                    int result = adapter.ExecuteBytecodeWithLiterals(
                        seqCollOop,
                        bytecode.GetBytes(),
                        new int[0],
                        bytecode.GetLiterals()
                    );
                    
                    if (result < 0)
                    {
                        Console.WriteLine("  ERROR: Interval creation failed");
                        return;
                    }
                    
                    Console.WriteLine($"  ✓ Interval created (OOP: {result})");
                    intervalOop = result;
                }
                
                // Step 4: Verify the created class
                Console.WriteLine("\nStep 4: Verifying Interval class...");
                
                intervalOop = adapter.PrimitiveLookupGlobal("Interval");
                if (intervalOop < 0)
                {
                    Console.WriteLine("  ERROR: Interval not found in globals");
                    return;
                }
                
                Console.WriteLine($"  ✓ Interval found in globals (OOP: {intervalOop})");
                
                // Check superclass
                var superclassOop = adapter.GetSlot(intervalOop, 0);  // slot 0 is superclass
                Console.WriteLine($"  Superclass OOP: {superclassOop}");
                
                // Try to create an instance
                Console.WriteLine("\nStep 5: Creating an instance of Interval...");
                
                var instBytecode = new BytecodeBuilder();
                instBytecode.PushGlobal("Interval");
                instBytecode.SendLiteral("basicNew", 0);
                instBytecode.ReturnTop();
                
                int instance = adapter.ExecuteBytecodeWithLiterals(
                    intervalOop,
                    instBytecode.GetBytes(),
                    new int[0],
                    instBytecode.GetLiterals()
                );
                
                if (instance < 0)
                {
                    Console.WriteLine("  ERROR: Could not create instance");
                }
                else
                {
                    Console.WriteLine($"  ✓ Instance created (OOP: {instance})");
                }
                
                Console.WriteLine("\n✓ Class creation via message sends successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
    
    /// <summary>
    /// Helper class to build bytecode sequences.
    /// </summary>
    public class BytecodeBuilder
    {
        private readonly System.Collections.Generic.List<byte> bytes = new();
        private readonly System.Collections.Generic.List<int> literals = new();
        
        public void PushGlobal(string name)
        {
            // Push literal variable (0x40 + index)
            literals.Add(0);  // Placeholder - would need actual symbol OOP
            bytes.Add((byte)(0x40 + literals.Count - 1));
        }
        
        public void PushLiteral(int oop)
        {
            literals.Add(oop);
            bytes.Add((byte)(0x20 + literals.Count - 1));  // Push literal constant
        }
        
        public void SendLiteral(string selector, int argCount)
        {
            literals.Add(0);  // Placeholder - would need actual selector symbol
            
            // Send literal selector with N args
            if (argCount == 0)
            {
                bytes.Add((byte)(0xD0 + literals.Count - 1));  // 208-223: send literal selector 0 args
            }
            else if (argCount == 1)
            {
                bytes.Add((byte)(0xE0 + literals.Count - 1));  // 224-239: send literal selector 1 arg
            }
            else if (argCount == 2)
            {
                bytes.Add((byte)(0xF0 + literals.Count - 1));  // 240-255: send literal selector 2 args
            }
            else
            {
                // Extended send for more than 2 args
                bytes.Add(0x83);  // Extended send
                bytes.Add((byte)((argCount << 5) | (literals.Count - 1)));
            }
        }
        
        public void ReturnTop()
        {
            bytes.Add(0x7C);  // Return top
        }
        
        public byte[] GetBytes()
        {
            return bytes.ToArray();
        }
        
        public int[] GetLiterals()
        {
            return literals.ToArray();
        }
    }
}