using System;
using System.IO;
using Smalltalk80;
using Smalltalk80.Bootstrap;

public class TestOOPBug
{
    public static void Main()
    {
        Console.WriteLine("=== Testing Object/ObjectClass OOP Assignment ===\n");
        
        // Clear ObjectMemory
        ObjectMemory.Reset();
        
        // Create VM adapter 
        var vm = new Smalltalk80.VirtualMachine();
        
        // Load bootstrap
        var bootstrapPath = Path.Combine("Bootstrap", "bootstrap.json");
        Console.WriteLine($"Loading bootstrap from: {bootstrapPath}");
        
        try
        {
            vm.LoadBootstrapFrom(bootstrapPath);
            
            // Now check what OOPs Object and ObjectClass have
            var adapter = vm.Adapter;
            if (adapter != null)
            {
                int objectOop = adapter.PrimitiveLookupGlobal("Object");
                int objectClassOop = adapter.PrimitiveLookupGlobal("ObjectClass");
                
                Console.WriteLine($"\nAfter bootstrap:");
                Console.WriteLine($"  Object OOP: {objectOop}");
                Console.WriteLine($"  ObjectClass OOP: {objectClassOop}");
                
                if (objectOop == objectClassOop)
                {
                    Console.WriteLine("\n*** BUG CONFIRMED: Object and ObjectClass have the same OOP! ***");
                    
                    // Try to understand why
                    var objectObj = ObjectMemory.FromOop(objectOop);
                    if (objectObj != null)
                    {
                        Console.WriteLine($"  Object at OOP {objectOop} has {objectObj.Fields.Length} fields");
                        Console.WriteLine($"  Object's slot 0 (superclass): {objectObj.Fields[0]}");
                    }
                }
                else
                {
                    Console.WriteLine("\n✓ Object and ObjectClass have different OOPs (no bug)");
                    
                    // Check Association's superclass chain
                    int associationOop = adapter.PrimitiveLookupGlobal("Association");
                    if (associationOop > 0)
                    {
                        Console.WriteLine($"\nChecking Association's superclass chain:");
                        var current = ObjectMemory.FromOop(associationOop);
                        int depth = 0;
                        
                        while (current != null && depth < 10)
                        {
                            Console.WriteLine($"  Depth {depth}: OOP {current.Oop}");
                            
                            // Get superclass from slot 0
                            if (current.Fields[0] is SmalltalkObject super)
                            {
                                current = super;
                            }
                            else
                            {
                                Console.WriteLine($"    Superclass is null or not an object");
                                break;
                            }
                            depth++;
                        }
                        
                        if (depth >= 10)
                        {
                            Console.WriteLine("  *** Infinite loop detected in superclass chain! ***");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}