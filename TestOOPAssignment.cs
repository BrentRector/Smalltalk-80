using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

public class TestOOPAssignment
{
    public static void Main()
    {
        Console.WriteLine("Testing OOP Assignment during bootstrap...\n");
        
        // Reset ObjectMemory
        ObjectMemory.Reset();
        
        // Create VMPrimitiveAdapter
        var vm = new VMPrimitiveAdapter();
        
        // Create Object class
        Console.WriteLine("Creating Object class...");
        int objectOop = vm.PrimitiveNewClass();
        Console.WriteLine($"  Object OOP: {objectOop}");
        vm.PrimitiveRegisterGlobal("Object", objectOop);
        
        // Create ObjectClass metaclass
        Console.WriteLine("\nCreating ObjectClass metaclass...");
        int objectClassOop = vm.PrimitiveNewMetaclass();
        Console.WriteLine($"  ObjectClass OOP: {objectClassOop}");
        vm.PrimitiveRegisterGlobal("ObjectClass", objectClassOop);
        
        // Check what OOPs were assigned
        Console.WriteLine("\nChecking OOP assignments:");
        var objectObj = ObjectMemory.FromOop(objectOop);
        var objectClassObj = ObjectMemory.FromOop(objectClassOop);
        
        if (objectObj != null)
            Console.WriteLine($"  Object at OOP {objectOop}: Fields.Length = {objectObj.Fields.Length}");
        if (objectClassObj != null)
            Console.WriteLine($"  ObjectClass at OOP {objectClassOop}: Fields.Length = {objectClassObj.Fields.Length}");
            
        // Now try setting ObjectClass's superclass
        Console.WriteLine("\nSetting ObjectClass superclass to nil...");
        vm.PrimitiveSetSlot(objectClassOop, 0, 0); // nil is OOP 0 in our simplified system
        
        // Check if Object's slot 0 was affected
        Console.WriteLine("\nChecking Object's slot 0 after setting ObjectClass superclass:");
        if (objectObj != null && objectObj.Fields[0] != null)
        {
            Console.WriteLine($"  Object slot 0: {objectObj.Fields[0]}");
            if (objectObj.Fields[0] is SmalltalkObject superObj)
            {
                Console.WriteLine($"    Which is a SmalltalkObject with OOP {superObj.Oop}");
            }
        }
        else
        {
            Console.WriteLine($"  Object slot 0 is null (correct!)");
        }
    }
}