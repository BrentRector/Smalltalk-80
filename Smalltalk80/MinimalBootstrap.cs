using System;
using System.Collections.Generic;

/// <summary>
/// Provides the absolute minimal bootstrap necessary to execute Smalltalk code.
/// Only creates classes that MUST exist before any Smalltalk code can run.
/// Everything else should be created by executing Smalltalk class definitions.
/// </summary>
public static class MinimalBootstrap
{
    /// <summary>
    /// Creates only the essential classes needed to execute Smalltalk code.
    /// These are the classes that must exist before we can execute:
    /// Object subclass: #SomeClass ...
    /// </summary>
    public static void CreateEssentialClasses()
    {
        Console.WriteLine("[BOOTSTRAP] Creating minimal essential classes...");
        
        // Clear any existing classes
        SystemDictionary.Clear();
        
        // Phase 1: Create the core metaclass hierarchy
        // These MUST exist to have a functioning class system
        CreateCoreMetaclassHierarchy();
        
        // Phase 2: Create essential data types
        // These MUST exist to represent literals and basic operations
        CreateEssentialDataTypes();
        
        // Phase 3: Create collection classes needed for the system itself
        CreateEssentialCollections();
        
        Console.WriteLine("[BOOTSTRAP] Essential classes created");
    }
    
    private static void CreateCoreMetaclassHierarchy()
    {
        Console.WriteLine("[BOOTSTRAP] Creating core metaclass hierarchy...");
        
        // 1. Object - root of all classes
        var objectClass = new SmalltalkClass { Superclass = null };
        objectClass.ClassVariables.Add("DependentsFields");
        objectClass.ClassVariables.Add("ErrorRecursion");
        SystemDictionary.RegisterBootstrapClass("Object", objectClass);
        
        // 2. Behavior - provides method dictionary and superclass
        var behavior = new SmalltalkClass { Superclass = objectClass };
        behavior.InstanceVariables.Add("superclass");
        behavior.InstanceVariables.Add("methodDict");
        behavior.InstanceVariables.Add("format");
        behavior.InstanceVariables.Add("subclasses");
        SystemDictionary.RegisterBootstrapClass("Behavior", behavior);
        
        // 3. ClassDescription - adds organization
        var classDescription = new SmalltalkClass { Superclass = behavior };
        classDescription.InstanceVariables.Add("instanceVariables");
        classDescription.InstanceVariables.Add("organization");
        SystemDictionary.RegisterBootstrapClass("ClassDescription", classDescription);
        
        // 4. Class - for regular classes
        var classClass = new SmalltalkClass { Superclass = classDescription };
        classClass.InstanceVariables.Add("name");
        classClass.InstanceVariables.Add("classPool");
        classClass.InstanceVariables.Add("sharedPools");
        SystemDictionary.RegisterBootstrapClass("Class", classClass);
        
        // 5. Metaclass - for metaclasses
        var metaclassClass = new SmalltalkClass { Superclass = classDescription };
        metaclassClass.InstanceVariables.Add("thisClass");
        SystemDictionary.RegisterBootstrapClass("Metaclass", metaclassClass);
        
        // Now set up the metaclass relationships
        SetupMetaclasses(objectClass, behavior, classDescription, classClass, metaclassClass);
        
        // 6. UndefinedObject - for nil
        var undefinedObject = new SmalltalkClass { Superclass = objectClass };
        SystemDictionary.RegisterBootstrapClass("UndefinedObject", undefinedObject);
        CreateMetaclassFor(undefinedObject, "UndefinedObject", objectClass.Metaclass, metaclassClass);
    }
    
    private static void CreateEssentialDataTypes()
    {
        Console.WriteLine("[BOOTSTRAP] Creating essential data types...");
        
        var objectClass = SystemDictionary.GetClass("Object");
        var metaclassClass = SystemDictionary.GetClass("Metaclass");
        
        // Boolean hierarchy - needed for conditionals
        var booleanClass = new SmalltalkClass { Superclass = objectClass };
        SystemDictionary.RegisterBootstrapClass("Boolean", booleanClass);
        CreateMetaclassFor(booleanClass, "Boolean", objectClass.Metaclass, metaclassClass);
        
        var trueClass = new SmalltalkClass { Superclass = booleanClass };
        SystemDictionary.RegisterBootstrapClass("True", trueClass);
        CreateMetaclassFor(trueClass, "True", booleanClass.Metaclass, metaclassClass);
        
        var falseClass = new SmalltalkClass { Superclass = booleanClass };
        SystemDictionary.RegisterBootstrapClass("False", falseClass);
        CreateMetaclassFor(falseClass, "False", booleanClass.Metaclass, metaclassClass);
        
        // Number hierarchy - needed for arithmetic
        var magnitudeClass = new SmalltalkClass { Superclass = objectClass };
        SystemDictionary.RegisterBootstrapClass("Magnitude", magnitudeClass);
        CreateMetaclassFor(magnitudeClass, "Magnitude", objectClass.Metaclass, metaclassClass);
        
        var numberClass = new SmalltalkClass { Superclass = magnitudeClass };
        SystemDictionary.RegisterBootstrapClass("Number", numberClass);
        CreateMetaclassFor(numberClass, "Number", magnitudeClass.Metaclass, metaclassClass);
        
        var integerClass = new SmalltalkClass { Superclass = numberClass };
        SystemDictionary.RegisterBootstrapClass("Integer", integerClass);
        CreateMetaclassFor(integerClass, "Integer", numberClass.Metaclass, metaclassClass);
        
        var smallIntegerClass = new SmalltalkClass { Superclass = integerClass };
        SystemDictionary.RegisterBootstrapClass("SmallInteger", smallIntegerClass);
        CreateMetaclassFor(smallIntegerClass, "SmallInteger", integerClass.Metaclass, metaclassClass);
        
        // Symbol - needed for selectors and class names
        var symbolClass = new SmalltalkClass { Superclass = objectClass };
        symbolClass.ClassVariables.Add("SymbolTable");
        SystemDictionary.RegisterBootstrapClass("Symbol", symbolClass);
        CreateMetaclassFor(symbolClass, "Symbol", objectClass.Metaclass, metaclassClass);
        
        // String - needed for source code
        var collectionClass = new SmalltalkClass { Superclass = objectClass };
        SystemDictionary.RegisterBootstrapClass("Collection", collectionClass);
        CreateMetaclassFor(collectionClass, "Collection", objectClass.Metaclass, metaclassClass);
        
        var sequenceableClass = new SmalltalkClass { Superclass = collectionClass };
        SystemDictionary.RegisterBootstrapClass("SequenceableCollection", sequenceableClass);
        CreateMetaclassFor(sequenceableClass, "SequenceableCollection", collectionClass.Metaclass, metaclassClass);
        
        var arrayedClass = new SmalltalkClass { Superclass = sequenceableClass };
        SystemDictionary.RegisterBootstrapClass("ArrayedCollection", arrayedClass);
        CreateMetaclassFor(arrayedClass, "ArrayedCollection", sequenceableClass.Metaclass, metaclassClass);
        
        var stringClass = new SmalltalkClass { Superclass = arrayedClass };
        SystemDictionary.RegisterBootstrapClass("String", stringClass);
        CreateMetaclassFor(stringClass, "String", arrayedClass.Metaclass, metaclassClass);
        
        // BlockContext - needed for blocks and conditionals
        var blockContextClass = new SmalltalkClass { Superclass = objectClass };
        blockContextClass.InstanceVariables.Add("nargs");
        blockContextClass.InstanceVariables.Add("startpc");
        blockContextClass.InstanceVariables.Add("home");
        SystemDictionary.RegisterBootstrapClass("BlockContext", blockContextClass);
        CreateMetaclassFor(blockContextClass, "BlockContext", objectClass.Metaclass, metaclassClass);
    }
    
    private static void CreateEssentialCollections()
    {
        Console.WriteLine("[BOOTSTRAP] Creating essential collections...");
        
        var objectClass = SystemDictionary.GetClass("Object");
        var collectionClass = SystemDictionary.GetClass("Collection");
        var metaclassClass = SystemDictionary.GetClass("Metaclass");
        
        // Array - needed for literal arrays
        var arrayClass = new SmalltalkClass { Superclass = SystemDictionary.GetClass("ArrayedCollection") };
        SystemDictionary.RegisterBootstrapClass("Array", arrayClass);
        CreateMetaclassFor(arrayClass, "Array", SystemDictionary.GetClass("ArrayedCollection").Metaclass, metaclassClass);
        
        // Set - base for Dictionary
        var setClass = new SmalltalkClass { Superclass = collectionClass };
        setClass.InstanceVariables.Add("tally");
        SystemDictionary.RegisterBootstrapClass("Set", setClass);
        CreateMetaclassFor(setClass, "Set", collectionClass.Metaclass, metaclassClass);
        
        // Dictionary - needed for method dictionaries and Smalltalk global
        var dictionaryClass = new SmalltalkClass { Superclass = setClass };
        SystemDictionary.RegisterBootstrapClass("Dictionary", dictionaryClass);
        CreateMetaclassFor(dictionaryClass, "Dictionary", setClass.Metaclass, metaclassClass);
        
        // SystemDictionary - for Smalltalk global
        var systemDictClass = new SmalltalkClass { Superclass = dictionaryClass };
        systemDictClass.ClassVariables.Add("CachedClassNames");
        systemDictClass.ClassVariables.Add("SpecialSelectors");
        SystemDictionary.RegisterBootstrapClass("SystemDictionary", systemDictClass);
        CreateMetaclassFor(systemDictClass, "SystemDictionary", dictionaryClass.Metaclass, metaclassClass);
        
        // Association - needed for variable bindings
        var associationClass = new SmalltalkClass { Superclass = objectClass };
        associationClass.InstanceVariables.Add("key");
        associationClass.InstanceVariables.Add("value");
        SystemDictionary.RegisterBootstrapClass("Association", associationClass);
        CreateMetaclassFor(associationClass, "Association", objectClass.Metaclass, metaclassClass);
        
        // CompiledMethod - needed to hold compiled methods
        var compiledMethodClass = new SmalltalkClass { Superclass = objectClass };
        SystemDictionary.RegisterBootstrapClass("CompiledMethod", compiledMethodClass);
        CreateMetaclassFor(compiledMethodClass, "CompiledMethod", objectClass.Metaclass, metaclassClass);
    }
    
    private static void SetupMetaclasses(
        SmalltalkClass objectClass,
        SmalltalkClass behavior,
        SmalltalkClass classDescription,
        SmalltalkClass classClass,
        SmalltalkClass metaclassClass)
    {
        // Object class -> Class (special case)
        var objectMetaclass = new SmalltalkClass { Superclass = classClass };
        objectMetaclass.InstanceVariables.Add("thisClass");
        objectMetaclass.ClassPointer = metaclassClass;
        objectMetaclass.ThisClass = objectClass;
        objectClass.Metaclass = objectMetaclass;
        objectClass.ClassPointer = objectMetaclass;
        
        // Behavior class -> Object class
        var behaviorMetaclass = new SmalltalkClass { Superclass = objectMetaclass };
        behaviorMetaclass.InstanceVariables.Add("thisClass");
        behaviorMetaclass.ClassPointer = metaclassClass;
        behaviorMetaclass.ThisClass = behavior;
        behavior.Metaclass = behaviorMetaclass;
        behavior.ClassPointer = behaviorMetaclass;
        
        // ClassDescription class -> Behavior class
        var classDescriptionMetaclass = new SmalltalkClass { Superclass = behaviorMetaclass };
        classDescriptionMetaclass.InstanceVariables.Add("thisClass");
        classDescriptionMetaclass.ClassPointer = metaclassClass;
        classDescriptionMetaclass.ThisClass = classDescription;
        classDescription.Metaclass = classDescriptionMetaclass;
        classDescription.ClassPointer = classDescriptionMetaclass;
        
        // Class class -> ClassDescription class
        var classMetaclass = new SmalltalkClass { Superclass = classDescriptionMetaclass };
        classMetaclass.InstanceVariables.Add("thisClass");
        classMetaclass.ClassPointer = metaclassClass;
        classMetaclass.ThisClass = classClass;
        classClass.Metaclass = classMetaclass;
        classClass.ClassPointer = classMetaclass;
        
        // Metaclass class -> ClassDescription class
        var metaclassMetaclass = new SmalltalkClass { Superclass = classDescriptionMetaclass };
        metaclassMetaclass.InstanceVariables.Add("thisClass");
        metaclassMetaclass.ClassPointer = metaclassClass;
        metaclassMetaclass.ThisClass = metaclassClass;
        metaclassClass.Metaclass = metaclassMetaclass;
        metaclassClass.ClassPointer = metaclassMetaclass;
    }
    
    private static void CreateMetaclassFor(
        SmalltalkClass cls,
        string className,
        SmalltalkClass? superclassMetaclass,
        SmalltalkClass metaclassClass)
    {
        var metaclass = new SmalltalkClass { Superclass = superclassMetaclass };
        metaclass.InstanceVariables.Add("thisClass");
        metaclass.ClassPointer = metaclassClass;
        metaclass.ThisClass = cls;
        cls.Metaclass = metaclass;
        cls.ClassPointer = metaclass;
    }
}