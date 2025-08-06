using System;
using System.Collections.Generic;

/// <summary>
///     Global repository mapping class names to <see cref="SmalltalkClass"/> instances.
/// </summary>
public static class SystemDictionary
{
    private static readonly Dictionary<string, SmalltalkClass> classes = new(StringComparer.Ordinal);

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
        // The metaclass inherits from the superclass's metaclass
        SmalltalkClass? superMetaclass = null;
        if (cls.Superclass?.Metaclass is not null)
        {
            superMetaclass = cls.Superclass.Metaclass;
        }
        else if (className != "Object" && TryGetClass("Class", out var classClass))
        {
            // If no superclass metaclass, inherit from Class
            superMetaclass = classClass;
        }

        var metaclass = new SmalltalkClass { Superclass = superMetaclass };

        // Set up the metaclass's class pointer
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

        // Create Object without metaclass first
        var objectClass = new SmalltalkClass();
        classes["Object"] = objectClass;

        // Create Class without metaclass
        var classClass = new SmalltalkClass { Superclass = objectClass };
        classes["Class"] = classClass;

        // Create Metaclass without metaclass
        var metaclassClass = new SmalltalkClass { Superclass = classClass };
        classes["Metaclass"] = metaclassClass;

        // Now set up the circular metaclass relationships

        // Object's metaclass is a special instance of Metaclass
        var objectMetaclass = new SmalltalkClass { Superclass = classClass };
        objectMetaclass.ClassPointer = metaclassClass;
        objectClass.Metaclass = objectMetaclass;
        objectClass.ClassPointer = objectMetaclass;

        // Class's metaclass is a special instance of Metaclass  
        var classMetaclass = new SmalltalkClass { Superclass = objectMetaclass };
        classMetaclass.ClassPointer = metaclassClass;
        classClass.Metaclass = classMetaclass;
        classClass.ClassPointer = classMetaclass;

        // Metaclass's metaclass is itself (circular)
        metaclassClass.Metaclass = metaclassClass;
        metaclassClass.ClassPointer = metaclassClass;
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
}
