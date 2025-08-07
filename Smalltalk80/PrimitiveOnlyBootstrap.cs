// ==============================================================================
// Primitive-Only Bootstrap Implementation
// Creates real Smalltalk objects using ONLY VM primitives
// No C# shadow structures - everything is a real Smalltalk object
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Bootstrap implementation that uses ONLY VM primitives to create real Smalltalk objects.
/// This is the correct way - no C# shadow structures, everything is a real Smalltalk object.
/// </summary>
public static class PrimitiveOnlyBootstrap
{
    // Field indices for Class hierarchy (0-based)
    // From Behavior class:
    private const int SUPERCLASS = 0;
    private const int METHODDICT = 1;
    private const int FORMAT = 2;
    
    // From ClassDescription:
    private const int INSTANCEVARIABLES = 3;
    private const int ORGANIZATION = 4;
    
    // From Class:
    private const int NAME = 5;
    private const int CLASSPOOL = 6;  // THIS IS THE KEY - Must be a real Dictionary!
    private const int SHAREDPOOLS = 7;
    
    // From Metaclass (extends ClassDescription, not Class):
    private const int METACLASS_THISCLASS = 5;  // Note: different index than Class fields
    
    // Dictionary/Set structure:
    private const int DICT_TALLY = 0;
    private const int DICT_ARRAY = 1;
    
    // Association structure:
    private const int ASSOC_KEY = 0;
    private const int ASSOC_VALUE = 1;
    
    // References to core classes we're building
    private static SmalltalkObject? nilObject;
    private static SmalltalkObject? trueObject;
    private static SmalltalkObject? falseObject;
    private static SmalltalkObject? objectClass;
    private static SmalltalkObject? behaviorClass;
    private static SmalltalkObject? classDescriptionClass;
    private static SmalltalkObject? classClass;
    private static SmalltalkObject? metaclassClass;
    private static SmalltalkObject? dictionaryClass;
    private static SmalltalkObject? arrayClass;
    private static SmalltalkObject? associationClass;
    private static SmalltalkObject? symbolClass;
    private static SmalltalkObject? smallIntegerClass;
    private static SmalltalkObject? setClass;
    private static SmalltalkObject? systemDictionaryClass;
    private static SmalltalkObject? smalltalkGlobal;
    
    /// <summary>
    /// Initialize the bootstrap using only primitives.
    /// This creates the minimal set of classes needed to run Smalltalk code.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[PRIMITIVE_BOOTSTRAP] Starting primitive-only bootstrap...");
        
        // Clear existing state
        SystemDictionary.Clear();
        
        // Phase 1: Create nil, true, false
        CreateSpecialObjects();
        
        // Phase 2: Create core class objects and metaclasses
        CreateCoreClasses();
        
        // Phase 3: Set up class hierarchy
        SetupClassHierarchy();
        
        // Phase 4: Create collection classes needed for Dictionary
        CreateCollectionClasses();
        
        // Phase 5: Create method dictionaries and class pools
        CreateMethodDictionariesAndClassPools();
        
        // Phase 6: Create global Smalltalk dictionary
        CreateGlobalSmalltalk();
        
        // Phase 7: Register everything
        RegisterBootstrapClasses();
        
        Console.WriteLine("[PRIMITIVE_BOOTSTRAP] Bootstrap complete!");
    }
    
    /// <summary>
    /// Phase 1: Create nil, true, false
    /// </summary>
    private static void CreateSpecialObjects()
    {
        // Create nil (instance of UndefinedObject)
        nilObject = Primitive71_New(null, 0);
        
        // Create true (instance of True)
        trueObject = Primitive71_New(null, 0);
        
        // Create false (instance of False)
        falseObject = Primitive71_New(null, 0);
    }
    
    /// <summary>
    /// Phase 2: Create core class objects and their metaclasses
    /// </summary>
    private static void CreateCoreClasses()
    {
        // Allocate Class objects (8 fields each)
        objectClass = Primitive71_New(null, 8);
        behaviorClass = Primitive71_New(null, 8);
        classDescriptionClass = Primitive71_New(null, 8);
        classClass = Primitive71_New(null, 8);
        metaclassClass = Primitive71_New(null, 8);
        
        // Allocate Metaclass objects (6 fields each - Metaclass extends ClassDescription)
        var objectMetaclass = Primitive71_New(null, 6);
        var behaviorMetaclass = Primitive71_New(null, 6);
        var classDescriptionMetaclass = Primitive71_New(null, 6);
        var classMetaclass = Primitive71_New(null, 6);
        var metaclassMetaclass = Primitive71_New(null, 6);
        
        // Set class pointers - classes are instances of their metaclasses
        SetClassPointer(objectClass, objectMetaclass);
        SetClassPointer(behaviorClass, behaviorMetaclass);
        SetClassPointer(classDescriptionClass, classDescriptionMetaclass);
        SetClassPointer(classClass, classMetaclass);
        SetClassPointer(metaclassClass, metaclassMetaclass);
        
        // All metaclasses are instances of Metaclass
        SetClassPointer(objectMetaclass, metaclassClass);
        SetClassPointer(behaviorMetaclass, metaclassClass);
        SetClassPointer(classDescriptionMetaclass, metaclassClass);
        SetClassPointer(classMetaclass, metaclassClass);
        SetClassPointer(metaclassMetaclass, metaclassClass); // Metaclass class is instance of Metaclass!
        
        // Set thisClass for metaclasses
        Primitive61_AtPut(objectMetaclass, METACLASS_THISCLASS + 1, objectClass);
        Primitive61_AtPut(behaviorMetaclass, METACLASS_THISCLASS + 1, behaviorClass);
        Primitive61_AtPut(classDescriptionMetaclass, METACLASS_THISCLASS + 1, classDescriptionClass);
        Primitive61_AtPut(classMetaclass, METACLASS_THISCLASS + 1, classClass);
        Primitive61_AtPut(metaclassMetaclass, METACLASS_THISCLASS + 1, metaclassClass);
    }
    
    /// <summary>
    /// Phase 3: Set up the class hierarchy
    /// </summary>
    private static void SetupClassHierarchy()
    {
        // Class hierarchy
        Primitive61_AtPut(objectClass, SUPERCLASS + 1, nilObject); // Object has no superclass
        Primitive61_AtPut(behaviorClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(classDescriptionClass, SUPERCLASS + 1, behaviorClass);
        Primitive61_AtPut(classClass, SUPERCLASS + 1, classDescriptionClass);
        Primitive61_AtPut(metaclassClass, SUPERCLASS + 1, classDescriptionClass);
        
        // Metaclass hierarchy (parallel to class hierarchy)
        var objectMetaclass = GetClassPointer(objectClass);
        var behaviorMetaclass = GetClassPointer(behaviorClass);
        var classDescriptionMetaclass = GetClassPointer(classDescriptionClass);
        var classMetaclass = GetClassPointer(classClass);
        var metaclassMetaclass = GetClassPointer(metaclassClass);
        
        Primitive61_AtPut(objectMetaclass, SUPERCLASS + 1, classClass); // Object class inherits from Class
        Primitive61_AtPut(behaviorMetaclass, SUPERCLASS + 1, objectMetaclass);
        Primitive61_AtPut(classDescriptionMetaclass, SUPERCLASS + 1, behaviorMetaclass);
        Primitive61_AtPut(classMetaclass, SUPERCLASS + 1, classDescriptionMetaclass);
        Primitive61_AtPut(metaclassMetaclass, SUPERCLASS + 1, classDescriptionMetaclass);
        
        // Set class names
        Primitive61_AtPut(objectClass, NAME + 1, new Symbol("Object"));
        Primitive61_AtPut(behaviorClass, NAME + 1, new Symbol("Behavior"));
        Primitive61_AtPut(classDescriptionClass, NAME + 1, new Symbol("ClassDescription"));
        Primitive61_AtPut(classClass, NAME + 1, new Symbol("Class"));
        Primitive61_AtPut(metaclassClass, NAME + 1, new Symbol("Metaclass"));
    }
    
    /// <summary>
    /// Phase 4: Create collection classes needed for Dictionary
    /// </summary>
    private static void CreateCollectionClasses()
    {
        // We need these to create Dictionary objects for classPool and methodDict
        
        // Array class
        arrayClass = Primitive71_New(null, 8);
        var arrayMetaclass = Primitive71_New(null, 6);
        SetClassPointer(arrayClass, arrayMetaclass);
        SetClassPointer(arrayMetaclass, metaclassClass);
        Primitive61_AtPut(arrayClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(arrayClass, NAME + 1, new Symbol("Array"));
        Primitive61_AtPut(arrayMetaclass, METACLASS_THISCLASS + 1, arrayClass);
        
        // Symbol class
        symbolClass = Primitive71_New(null, 8);
        var symbolMetaclass = Primitive71_New(null, 6);
        SetClassPointer(symbolClass, symbolMetaclass);
        SetClassPointer(symbolMetaclass, metaclassClass);
        Primitive61_AtPut(symbolClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(symbolClass, NAME + 1, new Symbol("Symbol"));
        Primitive61_AtPut(symbolMetaclass, METACLASS_THISCLASS + 1, symbolClass);
        
        // Association class (for key-value pairs in Dictionary)
        associationClass = Primitive71_New(null, 8);
        var associationMetaclass = Primitive71_New(null, 6);
        SetClassPointer(associationClass, associationMetaclass);
        SetClassPointer(associationMetaclass, metaclassClass);
        Primitive61_AtPut(associationClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(associationClass, NAME + 1, new Symbol("Association"));
        Primitive61_AtPut(associationMetaclass, METACLASS_THISCLASS + 1, associationClass);
        
        // Set class (Dictionary's superclass)
        setClass = Primitive71_New(null, 8);
        var setMetaclass = Primitive71_New(null, 6);
        SetClassPointer(setClass, setMetaclass);
        SetClassPointer(setMetaclass, metaclassClass);
        Primitive61_AtPut(setClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(setClass, NAME + 1, new Symbol("Set"));
        Primitive61_AtPut(setMetaclass, METACLASS_THISCLASS + 1, setClass);
        
        // Dictionary class
        dictionaryClass = Primitive71_New(null, 8);
        var dictionaryMetaclass = Primitive71_New(null, 6);
        SetClassPointer(dictionaryClass, dictionaryMetaclass);
        SetClassPointer(dictionaryMetaclass, metaclassClass);
        Primitive61_AtPut(dictionaryClass, SUPERCLASS + 1, setClass);
        Primitive61_AtPut(dictionaryClass, NAME + 1, new Symbol("Dictionary"));
        Primitive61_AtPut(dictionaryMetaclass, METACLASS_THISCLASS + 1, dictionaryClass);
        
        // SystemDictionary class (for global Smalltalk)
        systemDictionaryClass = Primitive71_New(null, 8);
        var systemDictionaryMetaclass = Primitive71_New(null, 6);
        SetClassPointer(systemDictionaryClass, systemDictionaryMetaclass);
        SetClassPointer(systemDictionaryMetaclass, metaclassClass);
        Primitive61_AtPut(systemDictionaryClass, SUPERCLASS + 1, dictionaryClass);
        Primitive61_AtPut(systemDictionaryClass, NAME + 1, new Symbol("SystemDictionary"));
        Primitive61_AtPut(systemDictionaryMetaclass, METACLASS_THISCLASS + 1, systemDictionaryClass);
        
        // SmallInteger class
        smallIntegerClass = Primitive71_New(null, 8);
        var smallIntegerMetaclass = Primitive71_New(null, 6);
        SetClassPointer(smallIntegerClass, smallIntegerMetaclass);
        SetClassPointer(smallIntegerMetaclass, metaclassClass);
        Primitive61_AtPut(smallIntegerClass, SUPERCLASS + 1, objectClass);
        Primitive61_AtPut(smallIntegerClass, NAME + 1, new Symbol("SmallInteger"));
        Primitive61_AtPut(smallIntegerMetaclass, METACLASS_THISCLASS + 1, smallIntegerClass);
    }
    
    /// <summary>
    /// Phase 5: Create method dictionaries and class pools
    /// </summary>
    private static void CreateMethodDictionariesAndClassPools()
    {
        // Create method dictionaries for all classes
        CreateAndSetMethodDict(objectClass);
        CreateAndSetMethodDict(behaviorClass);
        CreateAndSetMethodDict(classDescriptionClass);
        CreateAndSetMethodDict(classClass);
        CreateAndSetMethodDict(metaclassClass);
        CreateAndSetMethodDict(dictionaryClass);
        CreateAndSetMethodDict(arrayClass);
        CreateAndSetMethodDict(associationClass);
        CreateAndSetMethodDict(symbolClass);
        CreateAndSetMethodDict(setClass);
        CreateAndSetMethodDict(systemDictionaryClass);
        CreateAndSetMethodDict(smallIntegerClass);
        
        // CREATE CLASS POOLS - This is the critical fix!
        // Object has class variables: DependentsFields, ErrorRecursion
        var objectClassPool = CreateEmptyDictionary();
        Primitive61_AtPut(objectClass, CLASSPOOL + 1, objectClassPool);
        
        // Add Object's class variables to its classPool
        AddClassVariable(objectClassPool, "DependentsFields", nilObject);
        AddClassVariable(objectClassPool, "ErrorRecursion", nilObject);
        
        // Other classes get empty classPools
        Primitive61_AtPut(behaviorClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(classDescriptionClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(classClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(metaclassClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(dictionaryClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(arrayClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(associationClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(symbolClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(setClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(systemDictionaryClass, CLASSPOOL + 1, CreateEmptyDictionary());
        Primitive61_AtPut(smallIntegerClass, CLASSPOOL + 1, CreateEmptyDictionary());
    }
    
    /// <summary>
    /// Phase 6: Create the global Smalltalk dictionary
    /// </summary>
    private static void CreateGlobalSmalltalk()
    {
        // Create the Smalltalk global as an instance of SystemDictionary
        smalltalkGlobal = Primitive71_New(systemDictionaryClass, 2); // Dictionary has tally, array
        Primitive61_AtPut(smalltalkGlobal, DICT_TALLY + 1, new SmallInteger(0));
        
        // Create larger array for global storage
        var globalArray = Primitive71_New(arrayClass, 101);
        for (int i = 0; i < 101; i++)
        {
            Primitive61_AtPut(globalArray, i + 1, nilObject);
        }
        Primitive61_AtPut(smalltalkGlobal, DICT_ARRAY + 1, globalArray);
        
        // Add all classes to Smalltalk dictionary
        AddToDictionary(smalltalkGlobal, "Object", objectClass);
        AddToDictionary(smalltalkGlobal, "Behavior", behaviorClass);
        AddToDictionary(smalltalkGlobal, "ClassDescription", classDescriptionClass);
        AddToDictionary(smalltalkGlobal, "Class", classClass);
        AddToDictionary(smalltalkGlobal, "Metaclass", metaclassClass);
        AddToDictionary(smalltalkGlobal, "Dictionary", dictionaryClass);
        AddToDictionary(smalltalkGlobal, "Array", arrayClass);
        AddToDictionary(smalltalkGlobal, "Association", associationClass);
        AddToDictionary(smalltalkGlobal, "Symbol", symbolClass);
        AddToDictionary(smalltalkGlobal, "Set", setClass);
        AddToDictionary(smalltalkGlobal, "SystemDictionary", systemDictionaryClass);
        AddToDictionary(smalltalkGlobal, "SmallInteger", smallIntegerClass);
        
        // Add special objects
        AddToDictionary(smalltalkGlobal, "nil", nilObject);
        AddToDictionary(smalltalkGlobal, "true", trueObject);
        AddToDictionary(smalltalkGlobal, "false", falseObject);
        AddToDictionary(smalltalkGlobal, "Smalltalk", smalltalkGlobal); // Self-reference!
    }
    
    /// <summary>
    /// Phase 7: Register everything in SystemDictionary for C# access
    /// </summary>
    private static void RegisterBootstrapClasses()
    {
        // Register with SystemDictionary for C# interop
        SystemDictionary.RegisterBootstrapClass("Object", (SmalltalkClass)objectClass);
        SystemDictionary.RegisterBootstrapClass("Behavior", (SmalltalkClass)behaviorClass);
        SystemDictionary.RegisterBootstrapClass("ClassDescription", (SmalltalkClass)classDescriptionClass);
        SystemDictionary.RegisterBootstrapClass("Class", (SmalltalkClass)classClass);
        SystemDictionary.RegisterBootstrapClass("Metaclass", (SmalltalkClass)metaclassClass);
        SystemDictionary.RegisterBootstrapClass("Dictionary", (SmalltalkClass)dictionaryClass);
        SystemDictionary.RegisterBootstrapClass("Array", (SmalltalkClass)arrayClass);
        SystemDictionary.RegisterBootstrapClass("Association", (SmalltalkClass)associationClass);
        SystemDictionary.RegisterBootstrapClass("Symbol", (SmalltalkClass)symbolClass);
        SystemDictionary.RegisterBootstrapClass("Set", (SmalltalkClass)setClass);
        SystemDictionary.RegisterBootstrapClass("SystemDictionary", (SmalltalkClass)systemDictionaryClass);
        SystemDictionary.RegisterBootstrapClass("SmallInteger", (SmalltalkClass)smallIntegerClass);
        
        // Register special objects
        SystemDictionary.Nil = nilObject;
        SystemDictionary.True = trueObject;
        SystemDictionary.False = falseObject;
        SystemDictionary.RegisterGlobal("Smalltalk", smalltalkGlobal);
    }
    
    // ============================================
    // PRIMITIVE IMPLEMENTATIONS
    // ============================================
    
    /// <summary>
    /// Primitive 71: new: - Create object with N fields
    /// </summary>
    private static SmalltalkObject Primitive71_New(SmalltalkObject? cls, int fieldCount)
    {
        return new SmalltalkObject((SmalltalkClass?)cls, fieldCount);
    }
    
    /// <summary>
    /// Primitive 61: at:put: - Set field at 1-based index
    /// </summary>
    private static void Primitive61_AtPut(SmalltalkObject obj, int oneBasedIndex, object? value)
    {
        if (oneBasedIndex < 1 || oneBasedIndex > obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        obj.Fields[oneBasedIndex - 1] = value;
    }
    
    /// <summary>
    /// Primitive 60: at: - Get field at 1-based index
    /// </summary>
    private static object? Primitive60_At(SmalltalkObject obj, int oneBasedIndex)
    {
        if (oneBasedIndex < 1 || oneBasedIndex > obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        return obj.Fields[oneBasedIndex - 1];
    }
    
    /// <summary>
    /// Set the class pointer of an object
    /// </summary>
    private static void SetClassPointer(SmalltalkObject obj, SmalltalkObject cls)
    {
        obj.ClassPointer = (SmalltalkClass)cls;
    }
    
    /// <summary>
    /// Get the class pointer of an object
    /// </summary>
    private static SmalltalkObject GetClassPointer(SmalltalkObject obj)
    {
        return obj.ClassPointer ?? throw new InvalidOperationException("Object has no class pointer");
    }
    
    // ============================================
    // DICTIONARY HELPERS
    // ============================================
    
    /// <summary>
    /// Create an empty Dictionary object
    /// </summary>
    private static SmalltalkObject CreateEmptyDictionary()
    {
        var dict = Primitive71_New(dictionaryClass, 2);
        Primitive61_AtPut(dict, DICT_TALLY + 1, new SmallInteger(0));
        
        // Create small array for storage
        var array = Primitive71_New(arrayClass, 11);
        for (int i = 0; i < 11; i++)
        {
            Primitive61_AtPut(array, i + 1, nilObject);
        }
        Primitive61_AtPut(dict, DICT_ARRAY + 1, array);
        
        return dict;
    }
    
    /// <summary>
    /// Create and set method dictionary for a class
    /// </summary>
    private static void CreateAndSetMethodDict(SmalltalkObject cls)
    {
        var methodDict = CreateEmptyDictionary();
        Primitive61_AtPut(cls, METHODDICT + 1, methodDict);
    }
    
    /// <summary>
    /// Add a class variable to a classPool Dictionary
    /// </summary>
    private static void AddClassVariable(SmalltalkObject classPool, string name, object? value)
    {
        // Create Association
        var assoc = Primitive71_New(associationClass, 2);
        Primitive61_AtPut(assoc, ASSOC_KEY + 1, new Symbol(name));
        Primitive61_AtPut(assoc, ASSOC_VALUE + 1, value ?? nilObject);
        
        // Simplified: Just store in first empty slot for bootstrap
        // Real implementation would hash and handle collisions
        var array = (SmalltalkObject)Primitive60_At(classPool, DICT_ARRAY + 1)!;
        for (int i = 0; i < array.Fields.Length; i++)
        {
            if (array.Fields[i] == nilObject)
            {
                Primitive61_AtPut(array, i + 1, assoc);
                
                // Update tally
                var tally = (SmallInteger)Primitive60_At(classPool, DICT_TALLY + 1)!;
                Primitive61_AtPut(classPool, DICT_TALLY + 1, new SmallInteger(tally.Value + 1));
                break;
            }
        }
    }
    
    /// <summary>
    /// Add entry to a Dictionary (simplified for bootstrap)
    /// </summary>
    private static void AddToDictionary(SmalltalkObject dict, string key, SmalltalkObject value)
    {
        // Create Association
        var assoc = Primitive71_New(associationClass, 2);
        Primitive61_AtPut(assoc, ASSOC_KEY + 1, new Symbol(key));
        Primitive61_AtPut(assoc, ASSOC_VALUE + 1, value);
        
        // Simplified: Just store in first empty slot
        var array = (SmalltalkObject)Primitive60_At(dict, DICT_ARRAY + 1)!;
        for (int i = 0; i < array.Fields.Length; i++)
        {
            if (array.Fields[i] == nilObject)
            {
                Primitive61_AtPut(array, i + 1, assoc);
                
                // Update tally
                var tally = (SmallInteger)Primitive60_At(dict, DICT_TALLY + 1)!;
                Primitive61_AtPut(dict, DICT_TALLY + 1, new SmallInteger(tally.Value + 1));
                break;
            }
        }
    }
}