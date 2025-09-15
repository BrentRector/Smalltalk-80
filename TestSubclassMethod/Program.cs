using System;
using Smalltalk80.Bootstrap;

namespace TestSubclassMethod
{
    /// <summary>
    /// Test program to verify the subclass:instanceVariableNames: method is working
    /// and determine which class it's installed on (Class vs ClassDescription)
    /// </summary>
    public class TestSubclassMethod
    {
        static void Main()
        {
            Console.WriteLine("Testing subclass:instanceVariableNames: Method Availability");
            Console.WriteLine("==========================================================\n");
            
            try
            {
                // Initialize VM with bootstrap
                Console.WriteLine("1. Initializing VM and running bootstrap...");
                var vm = new VirtualMachine();
                var vmAdapter = vm.Adapter;
                
                if (vmAdapter == null)
                {
                    Console.WriteLine("   ERROR: VM adapter not available");
                    Environment.Exit(1);
                }
                
                Console.WriteLine("   Bootstrap complete\n");
                
                // Get Object class
                var objectClassOop = vmAdapter.PrimitiveLookupGlobal("Object");
                if (objectClassOop < 0)
                {
                    Console.WriteLine("   ERROR: Object class not found");
                    Environment.Exit(1);
                }
                Console.WriteLine($"   Object class found at OOP {objectClassOop}");
                
                // Test 1: Check if Object responds to subclass:instanceVariableNames:...
                Console.WriteLine("\n2. Testing if Object responds to subclass message...");
                
                var subclassSelector = vmAdapter.PrimitiveInternSymbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:");
                Console.WriteLine($"   Subclass selector OOP: {subclassSelector}");
                
                // Create arguments for the message
                var testPointSymbol = vmAdapter.PrimitiveInternSymbol("TestPoint");
                var instVarsString = vmAdapter.PrimitiveInternSymbol("x y");
                var classVarsString = vmAdapter.PrimitiveInternSymbol("");
                var poolsString = vmAdapter.PrimitiveInternSymbol("");
                var categoryString = vmAdapter.PrimitiveInternSymbol("Test");
                
                var args = new int[] { testPointSymbol, instVarsString, classVarsString, poolsString, categoryString };
                
                Console.WriteLine("   Attempting to send subclass message to Object...");
                
                // Try to send the message
                var resultOop = vmAdapter.PrimitiveSendMessage(objectClassOop, subclassSelector, args);
                
                if (resultOop < 0)
                {
                    Console.WriteLine("   ERROR: subclass message failed - method not found or error occurred");
                    
                    // Let's check what methods Object's class actually has
                    Console.WriteLine("\n3. Checking what class Object belongs to...");
                    var objectClass = ObjectMemory.FromOop(objectClassOop);
                    if (objectClass?.ClassPointer != null)
                    {
                        Console.WriteLine($"   Object's class is at OOP {objectClass.ClassPointer.Oop}");
                        
                        // Try to see if the method exists in the method dictionary
                        Console.WriteLine("   Checking if method exists in Object's class method dictionary...");
                        // This would require deeper introspection...
                    }
                }
                else
                {
                    Console.WriteLine($"   ✓ SUCCESS: subclass message returned OOP {resultOop}");
                    
                    // Verify the result is a class
                    var testPointClass = ObjectMemory.FromOop(resultOop);
                    if (testPointClass != null)
                    {
                        Console.WriteLine($"   ✓ Created class object with {testPointClass.Fields.Length} fields");
                        
                        // Check if it was registered in Smalltalk dictionary
                        var lookupResult = vmAdapter.PrimitiveLookupGlobal("TestPoint");
                        if (lookupResult == resultOop)
                        {
                            Console.WriteLine("   ✓ Class was automatically registered in Smalltalk dictionary");
                        }
                        else
                        {
                            Console.WriteLine("   - Class was not automatically registered (this may be expected)");
                        }
                    }
                }
                
                Console.WriteLine("\n" + new string('=', 60));
                if (resultOop >= 0)
                {
                    Console.WriteLine("SUCCESS: subclass:instanceVariableNames: method is working!");
                    Console.WriteLine("The system supports bytecode-driven class creation.");
                }
                else
                {
                    Console.WriteLine("ISSUE: subclass:instanceVariableNames: method not working");
                    Console.WriteLine("Need to investigate method installation or lookup.");
                }
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