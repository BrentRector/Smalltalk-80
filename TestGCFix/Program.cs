using System;

class Program
{
    static void Main()
    {
        Console.WriteLine("[TestGCFix] Testing GC fix for OOP 0 handling");
        
        // Reset object memory
        ObjectMemory.Reset();
        
        // Create some test objects (they auto-register with ObjectMemory)
        var obj1 = new SmalltalkObject(null, 1);
        var obj2 = new SmalltalkObject(null, 1);
        var obj3 = new SmalltalkObject(null, 1);
        
        Console.WriteLine($"obj1 OOP: {obj1.Oop} (expected: 0)");
        Console.WriteLine($"obj2 OOP: {obj2.Oop} (expected: 2)");
        Console.WriteLine($"obj3 OOP: {obj3.Oop} (expected: 4)");
        
        // Create a dictionary-like structure where obj3 references obj1 (OOP 0)
        obj3.Fields[0] = 0; // Store OOP 0 as an integer reference
        
        // Make obj3 a permanent root (simulating SystemDictionary)
        ObjectMemory.AddPermanentRoot(obj3);
        
        // Mark Special Objects Array as complete to enable GC
        ObjectMemory.MarkSpecialObjectsArrayComplete();
        
        Console.WriteLine("\n[TestGCFix] Before GC:");
        Console.WriteLine($"  Allocated objects: {ObjectMemory.AllocatedObjectCount}");
        Console.WriteLine($"  obj1 exists: {ObjectMemory.HasObject(0)}");
        Console.WriteLine($"  obj2 exists: {ObjectMemory.HasObject(2)}");
        Console.WriteLine($"  obj3 exists: {ObjectMemory.HasObject(4)}");
        
        // Force GC - obj1 should NOT be collected because obj3 references it
        // obj2 should be collected because nothing references it
        int freed = ObjectMemory.ManualGC();
        
        Console.WriteLine($"\n[TestGCFix] After GC (freed {freed} objects):");
        Console.WriteLine($"  Allocated objects: {ObjectMemory.AllocatedObjectCount}");
        Console.WriteLine($"  obj1 exists: {ObjectMemory.HasObject(0)} (should be true - referenced by obj3)");
        Console.WriteLine($"  obj2 exists: {ObjectMemory.HasObject(2)} (should be false - unreferenced)");
        Console.WriteLine($"  obj3 exists: {ObjectMemory.HasObject(4)} (should be true - permanent root)");
        
        // Verify the fix worked
        if (ObjectMemory.HasObject(0) && ObjectMemory.HasObject(4) && !ObjectMemory.HasObject(2))
        {
            Console.WriteLine("\n✅ SUCCESS: GC correctly preserved OOP 0 reference!");
        }
        else
        {
            Console.WriteLine("\n❌ FAILURE: GC did not handle OOP 0 correctly!");
            Environment.Exit(1);
        }
    }
}