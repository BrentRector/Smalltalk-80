using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

class TestAssociation
{
    static void Main()
    {
        Console.WriteLine("=== Testing Association Creation ===");
        Console.WriteLine();
        
        // Enable debug output
        VMPrimitiveAdapter.EnableVMDebugOutput = true;
        
        // Initialize VM
        var vm = new VirtualMachine();
        
        // Get Association class
        var adapter = vm.Adapter!;
        var assocClassOop = adapter.PrimitiveLookupClass("Association");
        Console.WriteLine($"Association class OOP: {assocClassOop}");
        
        if (assocClassOop > 0)
        {
            // Create an Association instance using PrimitiveNewInstance
            Console.WriteLine();
            Console.WriteLine("Creating Association instance via PrimitiveNewInstance...");
            var assocOop = adapter.PrimitiveNewInstance(assocClassOop);
            Console.WriteLine($"Created Association instance OOP: {assocOop}");
            
            // Check its fields
            var assocObj = ObjectMemory.FromOop(assocOop);
            if (assocObj != null)
            {
                Console.WriteLine($"Association has {assocObj.Fields.Length} fields");
                if (assocObj.Fields.Length != 2)
                {
                    Console.WriteLine("ERROR: Association should have 2 fields (key from LookupKey, value from Association)!");
                    Console.WriteLine();
                    
                    // Check LookupKey
                    var lookupKeyOop = adapter.PrimitiveLookupClass("LookupKey");
                    Console.WriteLine($"LookupKey class OOP: {lookupKeyOop}");
                    if (lookupKeyOop > 0)
                    {
                        var lookupKeyClass = ObjectMemory.FromOop(lookupKeyOop);
                        if (lookupKeyClass != null && lookupKeyClass.Fields.Length > 3)
                        {
                            var instVars = lookupKeyClass.Fields[3];
                            Console.WriteLine($"LookupKey instanceVariables (slot 3): {instVars}");
                        }
                    }
                    
                    // Check Association class structure
                    var assocClass = ObjectMemory.FromOop(assocClassOop);
                    if (assocClass != null && assocClass.Fields.Length > 3)
                    {
                        var instVars = assocClass.Fields[3];
                        Console.WriteLine($"Association instanceVariables (slot 3): {instVars}");
                    }
                }
                else
                {
                    Console.WriteLine("SUCCESS: Association correctly has 2 fields!");
                }
            }
        }
        else
        {
            Console.WriteLine("Association class not found!");
        }
    }
}