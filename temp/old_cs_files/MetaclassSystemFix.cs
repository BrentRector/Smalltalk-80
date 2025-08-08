// Proposed fix for SystemDictionary.cs to properly handle metaclasses

using System;
using System.Collections.Generic;

public static partial class SystemDictionary
{
    /// <summary>
    /// Creates a class and its associated metaclass with proper relationships
    /// following the classic Smalltalk-80 metaclass protocol.
    /// </summary>
    private static SmalltalkClass CreateClassWithMetaclass(
        string name,
        SmalltalkClass? superclass,
        IReadOnlyList<string>? instanceVariables,
        IReadOnlyList<string>? classVariables,
        IReadOnlyList<string>? poolDictionaries,
        string? category)
    {
        // Create the class
        var cls = new SmalltalkClass { Superclass = superclass };
        
        if (instanceVariables is not null)
            foreach (var ivar in instanceVariables)
                cls.InstanceVariables.Add(ivar);
        
        if (classVariables is not null)
            foreach (var cvar in classVariables)
                cls.ClassVariables.Add(cvar);
        
        if (poolDictionaries is not null)
            foreach (var pool in poolDictionaries)
                cls.PoolDictionaries.Add(pool);
        
        if (category is not null)
            cls.Category = category;

        // Create the metaclass for this class
        var metaclass = CreateMetaclassFor(cls, name);
        cls.Metaclass = metaclass;
        cls.ClassPointer = metaclass; // The class is an instance of its metaclass
        
        // CRITICAL: Set the thisClass instance variable in the metaclass
        // This represents the sole instance of the metaclass (the class itself)
        SetMetaclassThisClass(metaclass, cls);
        
        return cls;
    }

    /// <summary>
    /// Creates a metaclass for the given class following Smalltalk-80 rules.
    /// </summary>
    private static SmalltalkClass CreateMetaclassFor(SmalltalkClass cls, string className)
    {
        var metaclass = new SmalltalkClass();
        
        // CRITICAL: Add the 'thisClass' instance variable to every metaclass
        // This is defined in the Metaclass class definition
        metaclass.InstanceVariables.Add("thisClass");
        
        // The metaclass inherits from the superclass's metaclass (parallel hierarchy)
        if (cls.Superclass?.Metaclass != null)
        {
            metaclass.Superclass = cls.Superclass.Metaclass;
        }
        else if (className == "Object")
        {
            // Object's metaclass inherits from Class
            if (TryGetClass("Class", out var classClass))
                metaclass.Superclass = classClass;
        }
        else
        {
            // Orphan classes get Class as their metaclass's superclass
            if (TryGetClass("Class", out var classClass))
                metaclass.Superclass = classClass;
        }
        
        // All metaclasses are instances of Metaclass
        if (TryGetClass("Metaclass", out var metaclassClass))
        {
            metaclass.ClassPointer = metaclassClass;
        }
        
        return metaclass;
    }
    
    /// <summary>
    /// Sets the thisClass instance variable in a metaclass to point to its sole instance.
    /// In the VM, this would be done by storing the class reference in the first instance variable slot.
    /// </summary>
    private static void SetMetaclassThisClass(SmalltalkClass metaclass, SmalltalkClass theClass)
    {
        // In a real Smalltalk VM, we would set the instance variable value here.
        // For now, we need to track this relationship somehow.
        // Options:
        // 1. Add a ThisClass property to SmalltalkClass (when it's a metaclass)
        // 2. Store in a separate dictionary
        // 3. Use the first slot of the object's instance variables
        
        // This is a design decision - for now, let's assume we add a property:
        // metaclass.ThisClass = theClass;
        
        // Or we could store it as instance data when the metaclass is instantiated
    }

    /// <summary>
    /// Initializes the fundamental metaclass system with Object, Class, and Metaclass.
    /// This creates the circular dependencies required by Smalltalk-80.
    /// </summary>
    public static void InitializeMetaclassSystem()
    {
        if (classes.Count > 0)
        {
            // System already initialized, set up metaclasses for existing classes
            SetupMetaclassRelationshipsForExistingClasses();
            return;
        }

        // 1. Create Object as the root (classic Smalltalk-80)
        var objectClass = new SmalltalkClass { Superclass = null };
        objectClass.ClassVariables.Add("DependentsFields");
        objectClass.ClassVariables.Add("ErrorRecursion");
        classes["Object"] = objectClass;

        // 2. Create Behavior
        var behavior = new SmalltalkClass { Superclass = objectClass };
        behavior.InstanceVariables.Add("superclass");
        behavior.InstanceVariables.Add("methodDict");
        behavior.InstanceVariables.Add("format");
        behavior.InstanceVariables.Add("subclasses");
        classes["Behavior"] = behavior;

        // 3. Create ClassDescription
        var classDescription = new SmalltalkClass { Superclass = behavior };
        classDescription.InstanceVariables.Add("instanceVariables");
        classDescription.InstanceVariables.Add("organization");
        classes["ClassDescription"] = classDescription;

        // 4. Create Class
        var classClass = new SmalltalkClass { Superclass = classDescription };
        classClass.InstanceVariables.Add("name");
        classClass.InstanceVariables.Add("classPool");
        classClass.InstanceVariables.Add("sharedPools");
        classes["Class"] = classClass;

        // 5. Create Metaclass
        var metaclassClass = new SmalltalkClass { Superclass = classDescription };
        metaclassClass.InstanceVariables.Add("thisClass");
        classes["Metaclass"] = metaclassClass;

        // 6. Now create all the metaclasses with proper relationships
        
        // Object class -> Class
        var objectMeta = new SmalltalkClass { 
            Superclass = classClass,
            ClassPointer = metaclassClass 
        };
        objectMeta.InstanceVariables.Add("thisClass");
        objectClass.Metaclass = objectMeta;
        objectClass.ClassPointer = objectMeta;
        SetMetaclassThisClass(objectMeta, objectClass);

        // Behavior class -> Object class (parallel hierarchy)
        var behaviorMeta = new SmalltalkClass { 
            Superclass = objectMeta,
            ClassPointer = metaclassClass 
        };
        behaviorMeta.InstanceVariables.Add("thisClass");
        behavior.Metaclass = behaviorMeta;
        behavior.ClassPointer = behaviorMeta;
        SetMetaclassThisClass(behaviorMeta, behavior);

        // ClassDescription class -> Behavior class (parallel hierarchy)
        var classDescriptionMeta = new SmalltalkClass { 
            Superclass = behaviorMeta,
            ClassPointer = metaclassClass 
        };
        classDescriptionMeta.InstanceVariables.Add("thisClass");
        classDescription.Metaclass = classDescriptionMeta;
        classDescription.ClassPointer = classDescriptionMeta;
        SetMetaclassThisClass(classDescriptionMeta, classDescription);

        // Class class -> ClassDescription class (parallel hierarchy)
        var classMeta = new SmalltalkClass { 
            Superclass = classDescriptionMeta,
            ClassPointer = metaclassClass 
        };
        classMeta.InstanceVariables.Add("thisClass");
        classClass.Metaclass = classMeta;
        classClass.ClassPointer = classMeta;
        SetMetaclassThisClass(classMeta, classClass);

        // Metaclass class -> ClassDescription class (parallel hierarchy)  
        var metaclassMeta = new SmalltalkClass {
            Superclass = classDescriptionMeta,
            ClassPointer = metaclassClass
        };
        metaclassMeta.InstanceVariables.Add("thisClass");
        metaclassClass.Metaclass = metaclassMeta;
        metaclassClass.ClassPointer = metaclassMeta;
        SetMetaclassThisClass(metaclassMeta, metaclassClass);

        Console.WriteLine("[METACLASS] Initialized fundamental metaclass system");
        Console.WriteLine("[METACLASS] Object -> Behavior -> ClassDescription -> Class");
        Console.WriteLine("[METACLASS] Object -> Behavior -> ClassDescription -> Metaclass");
        Console.WriteLine("[METACLASS] All metaclasses are instances of Metaclass");
        Console.WriteLine("[METACLASS] Metaclass hierarchy parallels class hierarchy");
    }
}