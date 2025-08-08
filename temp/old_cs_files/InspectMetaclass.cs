using System;

public class InspectMetaclass
{
    public static void Main()
    {
        // Load standard library
        SystemDictionary.InitializeMetaclassSystem();
        
        Console.WriteLine("=== Metaclass Inspection After Bootstrap ===\n");
        
        // Check fundamental classes
        string[] fundamentalClasses = { "Object", "Behavior", "ClassDescription", "Class", "Metaclass" };
        
        foreach (var className in fundamentalClasses)
        {
            if (SystemDictionary.TryGetClass(className, out var cls))
            {
                Console.WriteLine($"Class: {className}");
                Console.WriteLine($"  Superclass: {(cls.Superclass?.ThisClass != null ? GetClassName(cls.Superclass) : cls.Superclass != null ? "?" : "nil")}");
                Console.WriteLine($"  Has Metaclass: {cls.Metaclass != null}");
                
                if (cls.Metaclass != null)
                {
                    Console.WriteLine($"  Metaclass superclass: {(cls.Metaclass.Superclass?.ThisClass != null ? GetClassName(cls.Metaclass.Superclass) : "?")}");
                    Console.WriteLine($"  Metaclass.ThisClass == cls: {cls.Metaclass.ThisClass == cls}");
                    Console.WriteLine($"  Metaclass has 'thisClass' ivar: {cls.Metaclass.InstanceVariables.Contains("thisClass")}");
                }
                Console.WriteLine();
            }
        }
    }
    
    static string GetClassName(SmalltalkClass cls)
    {
        // Try to find the name by looking up in SystemDictionary
        foreach (var (name, registeredClass) in SystemDictionary.AllClasses)
        {
            if (registeredClass == cls)
                return name;
        }
        
        // For metaclasses, check ThisClass
        if (cls.ThisClass != null)
        {
            foreach (var (name, registeredClass) in SystemDictionary.AllClasses)
            {
                if (registeredClass == cls.ThisClass)
                    return name + " class";
            }
        }
        
        return "Unknown";
    }
}