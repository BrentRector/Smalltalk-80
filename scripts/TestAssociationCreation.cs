using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

class TestAssociation
{
    static void Main()
    {
        Console.WriteLine("=== Testing Association Creation ===");
        
        // Initialize VM
        var vm = new VirtualMachine();
        vm.Initialize();
        
        // Enable debug output
        VMPrimitiveAdapter.EnableVMDebugOutput = true;
        
        // Get Association class
        var adapter = vm.Adapter;
        var assocClassOop = adapter.PrimitiveLookupClass("Association");
        Console.WriteLine($"Association class OOP: {assocClassOop}");
        
        if (assocClassOop > 0)
        {
            // Create an Association instance using PrimitiveNewInstance
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
                    Console.WriteLine("ERROR: Association should have 2 fields!");
                }
            }
        }
        else
        {
            Console.WriteLine("Association class not found!");
        }
    }
}
