using System;
using System.Collections.Generic;

/// <summary>
///     Global repository mapping class names to <see cref="SmalltalkClass"/> instances.
/// </summary>
public static class SystemDictionary
{
    private static readonly Dictionary<string, SmalltalkClass> classes = new(StringComparer.Ordinal);
    private static SmalltalkObject? smalltalkInstance;
    private static Dictionary<object, object>? smalltalkDictionary;

    /// <summary>
    ///     Registers a new class object under the specified name. If a class with the
    ///     name already exists it is returned instead.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="superclass">The optional superclass reference.</param>
    /// <param name="instanceVariables">Names of instance variables for the class.</param>
    /// <param name="classVariables">Names of class variables shared across all instances.</param>
    /// <param name="poolDictionaries">Names of shared pool dictionaries referenced by the class.</param>
    /// <param name="category">Organizational category of the class.</param>
    /// <returns>The created or existing <see cref="SmalltalkClass"/>.</returns>
    public static SmalltalkClass DefineClass(
        string name,
        SmalltalkClass? superclass = null,
        IReadOnlyList<string>? instanceVariables = null,
        IReadOnlyList<string>? classVariables = null,
        IReadOnlyList<string>? poolDictionaries = null,
        string? category = null)
    {
        if (classes.TryGetValue(name, out var existing))
        {
            if (superclass is not null && existing.Superclass is null)
                existing.Superclass = superclass;
            if (instanceVariables is not null)
            {
                foreach (var ivar in instanceVariables)
                {
                    if (!existing.InstanceVariables.Contains(ivar))
                        existing.InstanceVariables.Add(ivar);
                }
            }
            if (classVariables is not null)
            {
                foreach (var cvar in classVariables)
                {
                    if (!existing.ClassVariables.Contains(cvar))
                        existing.ClassVariables.Add(cvar);
                }
            }
            if (poolDictionaries is not null)
            {
                foreach (var pool in poolDictionaries)
                {
                    if (!existing.PoolDictionaries.Contains(pool))
                        existing.PoolDictionaries.Add(pool);
                }
            }
            if (category is not null)
                existing.Category = category;
            return existing;
        }

        var cls = CreateClassWithMetaclass(name, superclass, instanceVariables, classVariables, poolDictionaries, category);
        classes[name] = cls;
        
        // Also add to Smalltalk dictionary if it exists
        if (smalltalkDictionary != null)
        {
            smalltalkDictionary[new Symbol(name)] = cls;
        }
        
        return cls;
    }

    /// <summary>
    ///     Creates a class and its associated metaclass with proper relationships.
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
            cls.InstanceVariables.AddRange(instanceVariables);
        if (classVariables is not null)
            cls.ClassVariables.AddRange(classVariables);
        if (poolDictionaries is not null)
            cls.PoolDictionaries.AddRange(poolDictionaries);
        if (category is not null)
            cls.Category = category;

        // Create the metaclass for this class
        var metaclass = CreateMetaclassFor(cls, name);
        cls.Metaclass = metaclass;
        cls.ClassPointer = metaclass; // The class is an instance of its metaclass

        return cls;
    }

    /// <summary>
    ///     Creates a metaclass for the given class.
    /// </summary>
    private static SmalltalkClass CreateMetaclassFor(SmalltalkClass cls, string className)
    {
        var metaclass = new SmalltalkClass();
        
        // CRITICAL: Add the 'thisClass' instance variable to every metaclass
        // This is inherited from the Metaclass class definition
        metaclass.InstanceVariables.Add("thisClass");
        
        // Set the thisClass property to point to the class this metaclass describes
        metaclass.ThisClass = cls;
        
        // The metaclass inherits from the superclass's metaclass (parallel hierarchy)
        SmalltalkClass? superMetaclass = null;
        if (cls.Superclass?.Metaclass is not null)
        {
            superMetaclass = cls.Superclass.Metaclass;
        }
        else if (className == "Object")
        {
            // Object's metaclass inherits from Class
            if (TryGetClass("Class", out var classClass))
                superMetaclass = classClass;
        }
        else if (className != "Object" && TryGetClass("Class", out var classClass))
        {
            // Orphan classes get Class as their metaclass's superclass
            superMetaclass = classClass;
        }
        
        metaclass.Superclass = superMetaclass;

        // All metaclasses are instances of Metaclass
        if (TryGetClass("Metaclass", out var metaclassClass))
        {
            metaclass.ClassPointer = metaclassClass;
        }

        return metaclass;
    }

    /// <summary>
    ///     Initializes the fundamental metaclass system with Object, Class, and Metaclass.
    /// </summary>
    public static void InitializeMetaclassSystem()
    {
        if (classes.Count > 0)
        {
            // System already initialized, but we need to ensure all existing classes have proper metaclass setup
            SetupMetaclassRelationshipsForExistingClasses();
            return;
        }

        // Bootstrap the fundamental classes with circular references

        // Create Object as the root (classic Smalltalk-80)
        var objectClass = new SmalltalkClass { Superclass = null };
        objectClass.ClassVariables.Add("DependentsFields");
        objectClass.ClassVariables.Add("ErrorRecursion");
        classes["Object"] = objectClass;

        // Create Behavior
        var behavior = new SmalltalkClass { Superclass = objectClass };
        behavior.InstanceVariables.Add("superclass");
        behavior.InstanceVariables.Add("methodDict");
        behavior.InstanceVariables.Add("format");
        behavior.InstanceVariables.Add("subclasses");
        classes["Behavior"] = behavior;

        // Create ClassDescription
        var classDescription = new SmalltalkClass { Superclass = behavior };
        classDescription.InstanceVariables.Add("instanceVariables");
        classDescription.InstanceVariables.Add("organization");
        classes["ClassDescription"] = classDescription;

        // Create Class
        var classClass = new SmalltalkClass { Superclass = classDescription };
        classClass.InstanceVariables.Add("name");
        classClass.InstanceVariables.Add("classPool");
        classClass.InstanceVariables.Add("sharedPools");
        classes["Class"] = classClass;

        // Create Metaclass
        var metaclassClass = new SmalltalkClass { Superclass = classDescription };
        metaclassClass.InstanceVariables.Add("thisClass");
        classes["Metaclass"] = metaclassClass;

        // Now set up the metaclass relationships with proper parallel hierarchy

        // Object class -> Class
        var objectMetaclass = new SmalltalkClass { Superclass = classClass };
        objectMetaclass.InstanceVariables.Add("thisClass");
        objectMetaclass.ClassPointer = metaclassClass;
        objectMetaclass.ThisClass = objectClass;
        objectClass.Metaclass = objectMetaclass;
        objectClass.ClassPointer = objectMetaclass;

        // Behavior class -> Object class (parallel hierarchy)
        var behaviorMetaclass = new SmalltalkClass { Superclass = objectMetaclass };
        behaviorMetaclass.InstanceVariables.Add("thisClass");
        behaviorMetaclass.ClassPointer = metaclassClass;
        behaviorMetaclass.ThisClass = behavior;
        behavior.Metaclass = behaviorMetaclass;
        behavior.ClassPointer = behaviorMetaclass;

        // ClassDescription class -> Behavior class (parallel hierarchy)
        var classDescriptionMetaclass = new SmalltalkClass { Superclass = behaviorMetaclass };
        classDescriptionMetaclass.InstanceVariables.Add("thisClass");
        classDescriptionMetaclass.ClassPointer = metaclassClass;
        classDescriptionMetaclass.ThisClass = classDescription;
        classDescription.Metaclass = classDescriptionMetaclass;
        classDescription.ClassPointer = classDescriptionMetaclass;

        // Class class -> ClassDescription class (parallel hierarchy)
        var classMetaclass = new SmalltalkClass { Superclass = classDescriptionMetaclass };
        classMetaclass.InstanceVariables.Add("thisClass");
        classMetaclass.ClassPointer = metaclassClass;
        classMetaclass.ThisClass = classClass;
        classClass.Metaclass = classMetaclass;
        classClass.ClassPointer = classMetaclass;

        // Metaclass class -> ClassDescription class (parallel hierarchy)
        var metaclassMetaclass = new SmalltalkClass { Superclass = classDescriptionMetaclass };
        metaclassMetaclass.InstanceVariables.Add("thisClass");
        metaclassMetaclass.ClassPointer = metaclassClass;
        metaclassMetaclass.ThisClass = metaclassClass;
        metaclassClass.Metaclass = metaclassMetaclass;
        metaclassClass.ClassPointer = metaclassMetaclass;
    }

    /// <summary>
    ///     Sets up metaclass relationships for classes that were created before the metaclass system was initialized.
    /// </summary>
    private static void SetupMetaclassRelationshipsForExistingClasses()
    {
        // Get the fundamental classes
        if (!TryGetClass("Class", out var classClass) ||
            !TryGetClass("Metaclass", out var metaclassClass))
        {
            throw new InvalidOperationException("Fundamental classes Class and Metaclass must exist before setting up relationships");
        }

        // Set up metaclass relationships for all existing classes
        foreach (var (className, cls) in classes.ToList())
        {
            if (cls.Metaclass == null)
            {
                var metaclass = CreateMetaclassFor(cls, className);
                cls.Metaclass = metaclass;
                cls.ClassPointer = metaclass;

                Console.WriteLine($"[METACLASS] Set up metaclass relationship for class: {className}");
            }
        }
    }

    /// <summary>
    ///     Gets the class object registered under the provided name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <returns>The <see cref="SmalltalkClass"/> instance.</returns>
    public static SmalltalkClass GetClass(string name) => classes[name];

    /// <summary>
    ///     Attempts to retrieve a class with the given name.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="cls">When this method returns, contains the class object if found.</param>
    /// <returns><c>true</c> if the class exists; otherwise <c>false</c>.</returns>
    public static bool TryGetClass(string name, out SmalltalkClass cls) => classes.TryGetValue(name, out cls!);

    /// <summary>
    ///     Gets a read-only view of all classes currently registered in the
    ///     system dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, SmalltalkClass> AllClasses => classes;
    
    /// <summary>
    ///     Clears all classes and globals (used for bootstrap).
    /// </summary>
    public static void Clear()
    {
        classes.Clear();
        smalltalkDictionary?.Clear();
        smalltalkInstance = null;
        Console.WriteLine("[SYSTEM] Cleared all classes and globals");
    }
    
    /// <summary>
    ///     Registers a bootstrap class directly without going through Smalltalk.
    ///     This should ONLY be used for the minimal bootstrap classes.
    /// </summary>
    public static void RegisterBootstrapClass(string name, SmalltalkClass cls)
    {
        classes[name] = cls;
        
        // Also register in Smalltalk dictionary if it exists
        if (smalltalkDictionary != null)
        {
            smalltalkDictionary[new Symbol(name)] = cls;
        }
        
        Console.WriteLine($"[BOOTSTRAP] Registered bootstrap class: {name}");
    }

    /// <summary>
    ///     Gets or creates the global Smalltalk instance (the system dictionary).
    /// </summary>
    public static SmalltalkObject GetSmalltalk()
    {
        if (smalltalkInstance == null)
        {
            InitializeSmalltalkGlobal();
        }
        return smalltalkInstance!;
    }
    
    /// <summary>
    ///     Initializes the Smalltalk global as a SystemDictionary instance.
    /// </summary>
    private static void InitializeSmalltalkGlobal()
    {
        // Create the backing dictionary for our C# bridge
        smalltalkDictionary = new Dictionary<object, object>();
        
        // Create the Smalltalk global as an instance of SystemDictionary
        if (TryGetClass("SystemDictionary", out var systemDictClass))
        {
            // SystemDictionary is a variableSubclass of Dictionary, which is a variableSubclass of Set
            // Set has 'tally' instance variable, Dictionary adds no instance variables
            // So we need: instance variables (1 for tally) + indexed fields for storing Associations
            
            // Get the number of instance variables from the class
            var instSize = systemDictClass.InstanceVariables.Count;
            if (systemDictClass.Superclass != null)
                instSize += systemDictClass.Superclass.InstanceVariables.Count;
            
            // Create with enough indexed slots for associations
            smalltalkInstance = new SmalltalkObject(systemDictClass, instSize + 32); // instVars + 32 indexed slots
            
            // Initialize tally to 0 (number of elements in dictionary)
            smalltalkInstance.SetField(0, 0); // tally is the first instance variable from Set
            
            // Now populate the dictionary with existing classes as Associations
            int tally = 0;
            foreach (var (className, cls) in classes)
            {
                var key = new Symbol(className);
                var association = new Association(key, cls);
                
                // Store in our C# backing dictionary
                smalltalkDictionary[key] = cls;
                
                // Also store the Association in the Smalltalk dictionary's indexed fields
                // This is simplified - real Dictionary uses hashing
                if (tally < 32)
                {
                    smalltalkInstance.SetField(instSize + tally, association);
                    tally++;
                }
            }
            
            // Add Smalltalk itself
            var smalltalkKey = new Symbol("Smalltalk");
            var smalltalkAssoc = new Association(smalltalkKey, smalltalkInstance);
            smalltalkDictionary[smalltalkKey] = smalltalkInstance;
            if (tally < 32)
            {
                smalltalkInstance.SetField(instSize + tally, smalltalkAssoc);
                tally++;
            }
            
            // Update tally
            smalltalkInstance.SetField(0, tally);
            
            Console.WriteLine($"[SYSTEM] Created Smalltalk global instance with {tally} entries");
        }
        else if (TryGetClass("Dictionary", out var dictClass))
        {
            Console.WriteLine("[SYSTEM] Warning: SystemDictionary class not found, using Dictionary");
            
            // Dictionary is variableSubclass of Set, so has tally instance variable
            var instSize = 1; // tally from Set
            smalltalkInstance = new SmalltalkObject(dictClass, instSize + 32);
            smalltalkInstance.SetField(0, 0); // Initialize tally
            
            // Populate as above
            int tally = 0;
            foreach (var (className, cls) in classes)
            {
                var key = new Symbol(className);
                smalltalkDictionary[key] = cls;
                
                if (tally < 32)
                {
                    var association = new Association(key, cls);
                    smalltalkInstance.SetField(instSize + tally, association);
                    tally++;
                }
            }
            
            // Add Smalltalk itself
            smalltalkDictionary[new Symbol("Smalltalk")] = smalltalkInstance;
            smalltalkInstance.SetField(0, tally);
        }
        else
        {
            throw new InvalidOperationException("Cannot create Smalltalk global: no suitable dictionary class found");
        }
    }

    /// <summary>
    ///     Registers a global variable in the Smalltalk dictionary.
    /// </summary>
    /// <param name="name">The global variable name.</param>
    /// <param name="value">The value to store.</param>
    public static void SetGlobal(string name, object value)
    {
        if (smalltalkDictionary == null)
        {
            InitializeSmalltalkGlobal();
        }
        
        var symbol = new Symbol(name);
        smalltalkDictionary![symbol] = value;
        Console.WriteLine($"[SYSTEM] Registered global: {name}");
    }

    /// <summary>
    ///     Gets a global variable value from the Smalltalk dictionary.
    /// </summary>
    /// <param name="name">The global variable name.</param>
    /// <param name="value">When this method returns, contains the value if found.</param>
    /// <returns><c>true</c> if the global exists; otherwise <c>false</c>.</returns>
    public static bool TryGetGlobal(string name, out object? value)
    {
        if (smalltalkDictionary == null)
        {
            InitializeSmalltalkGlobal();
        }
        
        var symbol = new Symbol(name);
        if (smalltalkDictionary!.TryGetValue(symbol, out var globalValue))
        {
            value = globalValue;
            return true;
        }
        
        // Fallback to checking classes directly (for bootstrap)
        if (classes.TryGetValue(name, out var cls))
        {
            value = cls;
            // Also add it to Smalltalk dictionary
            smalltalkDictionary[symbol] = cls;
            return true;
        }
        
        value = null;
        return false;
    }
    
    /// <summary>
    ///     Gets the internal dictionary backing the Smalltalk global.
    ///     This is used by primitive operations for at: and at:put:
    /// </summary>
    public static Dictionary<object, object> GetSmalltalkDictionary()
    {
        if (smalltalkDictionary == null)
        {
            InitializeSmalltalkGlobal();
        }
        return smalltalkDictionary!;
    }

    /// <summary>
    ///     Initializes standard global variables.
    /// </summary>
    public static void InitializeGlobals()
    {
        // Ensure Smalltalk global exists
        GetSmalltalk();
        
        // Create Transcript global if TextCollector exists
        if (TryGetClass("TextCollector", out var textCollectorClass))
        {
            var transcript = new SmalltalkObject(textCollectorClass, 0);
            SetGlobal("Transcript", transcript);
        }
        
        // Create SystemOrganization placeholder
        if (TryGetClass("Object", out var objectClass))
        {
            var systemOrg = new SmalltalkObject(objectClass, 0);
            SetGlobal("SystemOrganization", systemOrg);
        }
        
        Console.WriteLine($"[SYSTEM] Smalltalk dictionary initialized with {smalltalkDictionary!.Count} globals");
    }
}
