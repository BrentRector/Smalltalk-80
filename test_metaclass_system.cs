using System;

class TestMetaclassSystem
{
    static void Main()
    {
        // Initialize the system
        var vm = new VirtualMachine();
        
        Console.WriteLine("=== Metaclass System Test ===\n");
        
        // Test 1: Verify Object is the root
        if (SystemDictionary.TryGetClass("Object", out var objectClass))
        {
            Console.WriteLine($"✓ Object class found");
            Console.WriteLine($"  - Superclass: {(objectClass.Superclass == null ? "nil" : "not nil")}");
            Console.WriteLine($"  - Has Metaclass: {objectClass.Metaclass != null}");
            if (objectClass.Metaclass != null)
            {
                Console.WriteLine($"  - Metaclass has ThisClass: {objectClass.Metaclass.ThisClass == objectClass}");
                Console.WriteLine($"  - Metaclass has 'thisClass' ivar: {objectClass.Metaclass.InstanceVariables.Contains("thisClass")}");
            }
        }
        else
        {
            Console.WriteLine("✗ Object class not found!");
        }
        
        Console.WriteLine();
        
        // Test 2: Verify metaclass hierarchy is parallel
        if (SystemDictionary.TryGetClass("Integer", out var integerClass) &&
            SystemDictionary.TryGetClass("Number", out var numberClass))
        {
            Console.WriteLine($"✓ Integer and Number classes found");
            Console.WriteLine($"  - Integer -> Number: {integerClass.Superclass == numberClass}");
            
            if (integerClass.Metaclass != null && numberClass.Metaclass != null)
            {
                var parallel = integerClass.Metaclass.Superclass == numberClass.Metaclass;
                Console.WriteLine($"  - Integer class -> Number class: {parallel}");
                if (!parallel)
                {
                    Console.WriteLine($"    Integer metaclass super: {integerClass.Metaclass.Superclass?.ThisClass?.GetType().Name ?? "unknown"}");
                }
            }
        }
        
        Console.WriteLine();
        
        // Test 3: Verify Metaclass class setup
        if (SystemDictionary.TryGetClass("Metaclass", out var metaclassClass))
        {
            Console.WriteLine($"✓ Metaclass class found");
            Console.WriteLine($"  - Has instance variable 'thisClass': {metaclassClass.InstanceVariables.Contains("thisClass")}");
            Console.WriteLine($"  - Has own metaclass: {metaclassClass.Metaclass != null}");
            if (metaclassClass.Metaclass != null)
            {
                Console.WriteLine($"  - Metaclass's metaclass has ThisClass: {metaclassClass.Metaclass.ThisClass == metaclassClass}");
            }
        }
        
        Console.WriteLine();
        
        // Test 4: Check all classes have proper metaclasses
        int classCount = 0;
        int metaclassCount = 0;
        int thisClassCount = 0;
        
        foreach (var (name, cls) in SystemDictionary.AllClasses)
        {
            classCount++;
            if (cls.Metaclass != null)
            {
                metaclassCount++;
                if (cls.Metaclass.ThisClass == cls)
                {
                    thisClassCount++;
                }
                else
                {
                    Console.WriteLine($"  ! Class {name} metaclass doesn't point back via ThisClass");
                }
            }
            else
            {
                Console.WriteLine($"  ! Class {name} has no metaclass");
            }
        }
        
        Console.WriteLine($"✓ Classes checked: {classCount}");
        Console.WriteLine($"  - Have metaclass: {metaclassCount}/{classCount}");
        Console.WriteLine($"  - Metaclass has correct ThisClass: {thisClassCount}/{classCount}");
        
        Console.WriteLine("\n=== Test Complete ===");
    }
}