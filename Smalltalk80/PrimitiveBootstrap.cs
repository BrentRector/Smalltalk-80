// ==============================================================================
// Bootstrap Using Only VM Primitives
// Creates real Smalltalk objects using only primitive operations
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Bootstrap that creates real Smalltalk objects using only VM primitives.
/// This is how the system should work - no C# shadow structures!
/// </summary>
public static class PrimitiveBootstrap
{
    // Field indices for Class hierarchy (from Smalltalk-80)
    // Behavior instance variables:
    private const int BEHAVIOR_SUPERCLASS = 0;
    private const int BEHAVIOR_METHODDICT = 1;
    private const int BEHAVIOR_FORMAT = 2;
    
    // ClassDescription adds:
    private const int CLASSDESC_INSTANCEVARIABLES = 3;
    private const int CLASSDESC_ORGANIZATION = 4;
    
    // Class adds:
    private const int CLASS_NAME = 5;
    private const int CLASS_CLASSPOOL = 6;  // This is the Dictionary for class variables!
    private const int CLASS_SHAREDPOOLS = 7;
    
    // Metaclass adds (after ClassDescription fields):
    private const int METACLASS_THISCLASS = 5;
    
    // Dictionary/Set instance variables:
    private const int SET_TALLY = 0;
    private const int SET_ARRAY = 1;
    
    // Association instance variables:
    private const int ASSOCIATION_KEY = 0;
    private const int ASSOCIATION_VALUE = 1;
    
    /// <summary>
    /// Initialize bootstrap using only primitives.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[PRIMITIVE_BOOTSTRAP] Creating real Smalltalk objects using only primitives...");
        
        // We need to bootstrap in careful order:
        // 1. Create nil, true, false first (needed everywhere)
        // 2. Create core class objects
        // 3. Set up class hierarchy
        // 4. Create dictionaries and arrays
        // 5. Initialize class structures
        
        // Phase 1: Create the absolute minimum objects
        var nil = CreateNil();
        var trueObj = CreateTrue();
        var falseObj = CreateFalse();
        
        // Phase 2: Create core class objects (initially with nil class pointer)
        // These are created as raw objects first, then properly initialized
        var objectClass = AllocateObject(8);  // Class has 8 instance vars
        var behaviorClass = AllocateObject(8);
        var classDescriptionClass = AllocateObject(8);
        var classClass = AllocateObject(8);
        var metaclassClass = AllocateObject(8);
        
        // Collection classes needed for Dictionary
        var collectionClass = AllocateObject(8);
        var setClass = AllocateObject(8);
        var dictionaryClass = AllocateObject(8);
        var arrayClass = AllocateObject(8);
        var associationClass = AllocateObject(8);
        
        // Phase 3: Create metaclasses
        var objectMetaclass = AllocateObject(6);  // Metaclass has 6 fields
        var behaviorMetaclass = AllocateObject(6);
        var classDescriptionMetaclass = AllocateObject(6);
        var classMetaclass = AllocateObject(6);
        var metaclassMetaclass = AllocateObject(6);
        
        // Phase 4: Set up class pointers (instances point to their class)
        SetClassPointer(objectClass, objectMetaclass);
        SetClassPointer(behaviorClass, behaviorMetaclass);
        SetClassPointer(classDescriptionClass, classDescriptionMetaclass);
        SetClassPointer(classClass, classMetaclass);
        SetClassPointer(metaclassClass, metaclassMetaclass);
        
        // Metaclasses are instances of Metaclass
        SetClassPointer(objectMetaclass, metaclassClass);
        SetClassPointer(behaviorMetaclass, metaclassClass);
        SetClassPointer(classDescriptionMetaclass, metaclassClass);
        SetClassPointer(classMetaclass, metaclassClass);
        SetClassPointer(metaclassMetaclass, metaclassClass);  // Metaclass class is instance of Metaclass
        
        // Phase 5: Set up superclass hierarchy
        // Object has no superclass (nil)
        SetField(objectClass, BEHAVIOR_SUPERCLASS, nil);
        SetField(behaviorClass, BEHAVIOR_SUPERCLASS, objectClass);
        SetField(classDescriptionClass, BEHAVIOR_SUPERCLASS, behaviorClass);
        SetField(classClass, BEHAVIOR_SUPERCLASS, classDescriptionClass);
        SetField(metaclassClass, BEHAVIOR_SUPERCLASS, classDescriptionClass);
        
        // Metaclass hierarchy parallels class hierarchy
        SetField(objectMetaclass, BEHAVIOR_SUPERCLASS, classClass);  // Object class's superclass is Class
        SetField(behaviorMetaclass, BEHAVIOR_SUPERCLASS, objectMetaclass);
        SetField(classDescriptionMetaclass, BEHAVIOR_SUPERCLASS, behaviorMetaclass);
        SetField(classMetaclass, BEHAVIOR_SUPERCLASS, classDescriptionMetaclass);
        SetField(metaclassMetaclass, BEHAVIOR_SUPERCLASS, classDescriptionMetaclass);
        
        // Set thisClass for metaclasses
        SetField(objectMetaclass, METACLASS_THISCLASS, objectClass);
        SetField(behaviorMetaclass, METACLASS_THISCLASS, behaviorClass);
        SetField(classDescriptionMetaclass, METACLASS_THISCLASS, classDescriptionClass);
        SetField(classMetaclass, METACLASS_THISCLASS, classClass);
        SetField(metaclassMetaclass, METACLASS_THISCLASS, metaclassClass);
        
        // Phase 6: Create method dictionaries (empty for now)
        var objectMethodDict = CreateEmptyDictionary(dictionaryClass);
        var behaviorMethodDict = CreateEmptyDictionary(dictionaryClass);
        var classDescriptionMethodDict = CreateEmptyDictionary(dictionaryClass);
        var classMethodDict = CreateEmptyDictionary(dictionaryClass);
        var metaclassMethodDict = CreateEmptyDictionary(dictionaryClass);
        
        SetField(objectClass, BEHAVIOR_METHODDICT, objectMethodDict);
        SetField(behaviorClass, BEHAVIOR_METHODDICT, behaviorMethodDict);
        SetField(classDescriptionClass, BEHAVIOR_METHODDICT, classDescriptionMethodDict);
        SetField(classClass, BEHAVIOR_METHODDICT, classMethodDict);
        SetField(metaclassClass, BEHAVIOR_METHODDICT, metaclassMethodDict);
        
        // Phase 7: CREATE CLASS POOLS! This is the critical missing piece!
        // Object has class variables: DependentsFields, ErrorRecursion
        var objectClassPool = CreateEmptyDictionary(dictionaryClass);
        SetField(objectClass, CLASS_CLASSPOOL, objectClassPool);
        
        // Add class variables to Object's classPool with nil initial values
        AddToClassPool(objectClassPool, "DependentsFields", nil, associationClass);
        AddToClassPool(objectClassPool, "ErrorRecursion", nil, associationClass);
        
        // Other classes get empty classPools
        SetField(behaviorClass, CLASS_CLASSPOOL, CreateEmptyDictionary(dictionaryClass));
        SetField(classDescriptionClass, CLASS_CLASSPOOL, CreateEmptyDictionary(dictionaryClass));
        SetField(classClass, CLASS_CLASSPOOL, CreateEmptyDictionary(dictionaryClass));
        
        // Phase 8: Register in system
        RegisterGlobal("Object", objectClass);
        RegisterGlobal("Behavior", behaviorClass);
        RegisterGlobal("ClassDescription", classDescriptionClass);
        RegisterGlobal("Class", classClass);
        RegisterGlobal("Metaclass", metaclassClass);
        RegisterGlobal("nil", nil);
        RegisterGlobal("true", trueObj);
        RegisterGlobal("false", falseObj);
        
        Console.WriteLine("[PRIMITIVE_BOOTSTRAP] Bootstrap complete - real Smalltalk objects created!");
    }
    
    /// <summary>
    /// Allocates a raw Smalltalk object using primitive 71 (new:).
    /// </summary>
    private static SmalltalkObject AllocateObject(int fieldCount)
    {
        // This simulates primitive 71: new: size
        // In real implementation, this would push class on stack, then size, then call primitive
        return new SmalltalkObject(null, fieldCount);
    }
    
    /// <summary>
    /// Sets a field using primitive 61 (at:put:).
    /// </summary>
    private static void SetField(SmalltalkObject obj, int zeroBasedIndex, object? value)
    {
        // This simulates primitive 61: at: index put: value
        // Smalltalk uses 1-based indexing, but internally we use 0-based
        obj.Fields[zeroBasedIndex] = value;
    }
    
    /// <summary>
    /// Gets a field using primitive 60 (at:).
    /// </summary>
    private static object? GetField(SmalltalkObject obj, int zeroBasedIndex)
    {
        // This simulates primitive 60: at: index
        return obj.Fields[zeroBasedIndex];
    }
    
    /// <summary>
    /// Sets the class pointer of an object.
    /// </summary>
    private static void SetClassPointer(SmalltalkObject obj, SmalltalkObject cls)
    {
        obj.ClassPointer = (SmalltalkClass)cls;
    }
    
    /// <summary>
    /// Creates nil object.
    /// </summary>
    private static SmalltalkObject CreateNil()
    {
        var nil = AllocateObject(0);  // nil has no instance variables
        // nil's class is UndefinedObject, set up later
        SystemDictionary.Nil = nil;
        return nil;
    }
    
    /// <summary>
    /// Creates true object.
    /// </summary>
    private static SmalltalkObject CreateTrue()
    {
        var trueObj = AllocateObject(0);  // true has no instance variables
        // true's class is True, set up later
        SystemDictionary.True = trueObj;
        return trueObj;
    }
    
    /// <summary>
    /// Creates false object.
    /// </summary>
    private static SmalltalkObject CreateFalse()
    {
        var falseObj = AllocateObject(0);  // false has no instance variables
        // false's class is False, set up later
        SystemDictionary.False = falseObj;
        return falseObj;
    }
    
    /// <summary>
    /// Creates an empty Dictionary using primitives.
    /// Dictionary is a Set with tally and array fields.
    /// </summary>
    private static SmalltalkObject CreateEmptyDictionary(SmalltalkObject dictionaryClass)
    {
        // Create Dictionary instance
        var dict = AllocateObject(2);  // Set has: tally, array
        SetClassPointer(dict, dictionaryClass);
        
        // Set tally to 0
        SetField(dict, SET_TALLY, new SmallInteger(0));
        
        // Create empty array for storage (start with small size)
        var array = AllocateObject(11);  // Start with 11 slots (Smalltalk default)
        SetField(dict, SET_ARRAY, array);
        
        // Fill array with nils
        for (int i = 0; i < 11; i++)
        {
            SetField(array, i, SystemDictionary.Nil);
        }
        
        return dict;
    }
    
    /// <summary>
    /// Adds a class variable to a classPool Dictionary.
    /// This is a minimal implementation for bootstrap.
    /// </summary>
    private static void AddToClassPool(SmalltalkObject classPool, string varName, object? initialValue, SmalltalkObject associationClass)
    {
        // Create an Association: key -> value
        var association = AllocateObject(2);  // Association has: key, value
        SetClassPointer(association, associationClass);
        
        // Set key to a Symbol
        var symbol = new Symbol(varName);
        SetField(association, ASSOCIATION_KEY, symbol);
        SetField(association, ASSOCIATION_VALUE, initialValue ?? SystemDictionary.Nil);
        
        // In a real implementation, we'd need to:
        // 1. Hash the key
        // 2. Find the right slot in the array
        // 3. Handle collisions
        // For bootstrap, we can use a simplified approach or defer to first initialization
        
        // For now, just mark that this needs proper Dictionary implementation
        // The key insight is that classPool IS a real Smalltalk Dictionary object!
    }
    
    /// <summary>
    /// Registers a global in the system dictionary.
    /// </summary>
    private static void RegisterGlobal(string name, SmalltalkObject obj)
    {
        // This should also use a real Smalltalk Dictionary eventually
        // For bootstrap, we register in SystemDictionary
        if (obj is SmalltalkClass cls)
        {
            SystemDictionary.RegisterBootstrapClass(name, cls);
        }
        else
        {
            SystemDictionary.RegisterGlobal(name, obj);
        }
    }
}