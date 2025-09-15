using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Tests
{
    /// <summary>
    /// Test to reproduce the Object/ObjectClass OOP collision issue.
    /// </summary>
    public class TestBootstrapOOPIssue
    {
        public static void Main()
        {
            Console.WriteLine("=== Testing Bootstrap OOP Assignment Issue ===\n");
            
            // Reset ObjectMemory
            ObjectMemory.Reset();
            
            // Create bootstrap context
            var bootstrapContext = new BootstrapContext();
            
            // Create VMPrimitiveAdapter with bootstrap context
            var vm = new VMPrimitiveAdapter();
            vm.SetBootstrapContext(bootstrapContext);
            
            // Simulate what happens in the bootstrap
            Console.WriteLine("Step 1: Creating Object class...");
            int objectOop = vm.PrimitiveNewClass();
            Console.WriteLine($"  Object created with OOP: {objectOop}");
            vm.PrimitiveRegisterGlobal("Object", objectOop);
            
            Console.WriteLine("\nStep 2: Creating ObjectClass metaclass...");
            int objectClassOop = vm.PrimitiveNewMetaclass();
            Console.WriteLine($"  ObjectClass created with OOP: {objectClassOop}");
            vm.PrimitiveRegisterGlobal("ObjectClass", objectClassOop);
            
            Console.WriteLine("\nStep 3: Looking up registered globals...");
            int lookupObject = vm.PrimitiveLookupGlobal("Object");
            int lookupObjectClass = vm.PrimitiveLookupGlobal("ObjectClass");
            
            Console.WriteLine($"  Lookup 'Object' returned OOP: {lookupObject}");
            Console.WriteLine($"  Lookup 'ObjectClass' returned OOP: {lookupObjectClass}");
            
            if (lookupObject == lookupObjectClass)
            {
                Console.WriteLine("\n*** ERROR: Object and ObjectClass have the same OOP! ***");
                Console.WriteLine("This is the bug causing the superclass chain issue.");
            }
            else
            {
                Console.WriteLine("\n✓ Object and ObjectClass have different OOPs (correct)");
            }
            
            // Check what's in the bootstrap context
            Console.WriteLine("\nStep 4: Checking bootstrap context globals...");
            var globals = bootstrapContext.GetAllGlobals();
            foreach (var kvp in globals)
            {
                Console.WriteLine($"  {kvp.Key} -> OOP {kvp.Value}");
            }
            
            // Check ObjectMemory
            Console.WriteLine("\nStep 5: Checking ObjectMemory state...");
            Console.WriteLine($"  Total allocated objects: {ObjectMemory.AllocatedObjectCount}");
            
            var obj0 = ObjectMemory.FromOop(0);
            var obj2 = ObjectMemory.FromOop(2);
            var obj4 = ObjectMemory.FromOop(4);
            
            if (obj0 != null)
                Console.WriteLine($"  OOP 0: Object with {obj0.Fields.Length} fields");
            if (obj2 != null)
                Console.WriteLine($"  OOP 2: Object with {obj2.Fields.Length} fields (method dict?)");
            if (obj4 != null)
                Console.WriteLine($"  OOP 4: Object with {obj4.Fields.Length} fields");
        }
    }
}