// ==============================================================================
// Minimal Primitive Bootstrap - Phase 1 & 2 Implementation
// Creates only the 9 essential classes needed for self-hosting
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Minimal bootstrap that creates only the 9 essential classes needed for
/// Smalltalk to define its own classes via "Object subclass:".
/// 
/// Phase 1 Classes:
/// - Object (root of all classes)
/// - Class (metaclass for all classes)
/// - Metaclass (class of Class)
/// - Behavior (superclass of Class and Metaclass)
/// - CompiledMethod (holds bytecode)
/// - Symbol (method names, keys)
/// - Dictionary (method dictionaries)
/// - Array (instance variable storage)
/// - UndefinedObject (singleton nil)
/// </summary>
public static class MinimalPrimitiveBootstrap
{
    // Field indices for Behavior hierarchy (0-based)
    private const int BEHAVIOR_SUPERCLASS = 0;
    private const int BEHAVIOR_METHODDICT = 1;
    private const int BEHAVIOR_FORMAT = 2;
    
    // ClassDescription adds:
    private const int CLASSDESC_INSTANCEVARIABLES = 3;
    private const int CLASSDESC_ORGANIZATION = 4;
    
    // Class adds:
    private const int CLASS_NAME = 5;
    private const int CLASS_CLASSPOOL = 6;
    private const int CLASS_SHAREDPOOLS = 7;
    
    // Metaclass has different layout (extends ClassDescription, not Class):
    private const int METACLASS_THISCLASS = 5;
    
    // Dictionary structure:
    private const int DICT_TALLY = 0;
    private const int DICT_ARRAY = 1;
    
    // References to the 9 essential classes
    private static SmalltalkObject? nil;
    private static SmalltalkObject? objectClass;
    private static SmalltalkObject? behaviorClass;
    private static SmalltalkObject? classClass;
    private static SmalltalkObject? metaclassClass;
    private static SmalltalkObject? symbolClass;
    private static SmalltalkObject? dictionaryClass;
    private static SmalltalkObject? arrayClass;
    private static SmalltalkObject? compiledMethodClass;
    private static SmalltalkObject? undefinedObjectClass;
    
    /// <summary>
    /// Execute the minimal bootstrap using only primitives.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[MINIMAL_BOOTSTRAP] Starting Phase 1 & 2 bootstrap...");
        Console.WriteLine("[MINIMAL_BOOTSTRAP] Creating 9 essential classes using only primitives");
        
        // Clear any existing state
        SystemDictionary.Clear();
        
        // ============================================
        // PHASE 1: Identify the 9 essential classes
        // ============================================
        // Object, Class, Metaclass, Behavior, CompiledMethod,
        // Symbol, Dictionary, Array, UndefinedObject
        
        // ============================================
        // PHASE 2: Manual construction via primitives
        // ============================================
        
        // Step 1: Create nil first (we need it everywhere)
        nil = PrimitiveNew(null, 0);  // nil has no fields
        
        // Step 2: Create raw class objects (8 fields each for Class instances)
        objectClass = PrimitiveNewClass("Object");
        symbolClass = PrimitiveNewClass("Symbol");
        arrayClass = PrimitiveNewClass("Array");
        dictionaryClass = PrimitiveNewClass("Dictionary");
        behaviorClass = PrimitiveNewClass("Behavior");
        classClass = PrimitiveNewClass("Class");
        metaclassClass = PrimitiveNewClass("Metaclass");
        compiledMethodClass = PrimitiveNewClass("CompiledMethod");
        undefinedObjectClass = PrimitiveNewClass("UndefinedObject");
        
        // Step 3: Create metaclasses for each class
        var objectMetaclass = PrimitiveNewMetaclass(objectClass);
        var symbolMetaclass = PrimitiveNewMetaclass(symbolClass);
        var arrayMetaclass = PrimitiveNewMetaclass(arrayClass);
        var dictionaryMetaclass = PrimitiveNewMetaclass(dictionaryClass);
        var behaviorMetaclass = PrimitiveNewMetaclass(behaviorClass);
        var classMetaclass = PrimitiveNewMetaclass(classClass);
        var metaclassMetaclass = PrimitiveNewMetaclass(metaclassClass);
        var compiledMethodMetaclass = PrimitiveNewMetaclass(compiledMethodClass);
        var undefinedObjectMetaclass = PrimitiveNewMetaclass(undefinedObjectClass);
        
        // Step 4: Set class pointers (each class is an instance of its metaclass)
        PrimitiveSetClass(objectClass, objectMetaclass);
        PrimitiveSetClass(symbolClass, symbolMetaclass);
        PrimitiveSetClass(arrayClass, arrayMetaclass);
        PrimitiveSetClass(dictionaryClass, dictionaryMetaclass);
        PrimitiveSetClass(behaviorClass, behaviorMetaclass);
        PrimitiveSetClass(classClass, classMetaclass);
        PrimitiveSetClass(metaclassClass, metaclassMetaclass);
        PrimitiveSetClass(compiledMethodClass, compiledMethodMetaclass);
        PrimitiveSetClass(undefinedObjectClass, undefinedObjectMetaclass);
        
        // All metaclasses are instances of Metaclass
        PrimitiveSetClass(objectMetaclass, metaclassClass);
        PrimitiveSetClass(symbolMetaclass, metaclassClass);
        PrimitiveSetClass(arrayMetaclass, metaclassClass);
        PrimitiveSetClass(dictionaryMetaclass, metaclassClass);
        PrimitiveSetClass(behaviorMetaclass, metaclassClass);
        PrimitiveSetClass(classMetaclass, metaclassClass);
        PrimitiveSetClass(metaclassMetaclass, metaclassClass);  // Metaclass class is instance of Metaclass!
        PrimitiveSetClass(compiledMethodMetaclass, metaclassClass);
        PrimitiveSetClass(undefinedObjectMetaclass, metaclassClass);
        
        // nil is an instance of UndefinedObject
        PrimitiveSetClass(nil, undefinedObjectClass);
        
        // Step 5: Set up superclass hierarchy
        
        // Object is the root (no superclass)
        PrimitiveAtPut(objectClass, BEHAVIOR_SUPERCLASS + 1, nil);
        
        // Direct subclasses of Object
        PrimitiveAtPut(symbolClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        PrimitiveAtPut(arrayClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        PrimitiveAtPut(dictionaryClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        PrimitiveAtPut(compiledMethodClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        PrimitiveAtPut(undefinedObjectClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        
        // Behavior hierarchy
        PrimitiveAtPut(behaviorClass, BEHAVIOR_SUPERCLASS + 1, objectClass);
        PrimitiveAtPut(classClass, BEHAVIOR_SUPERCLASS + 1, behaviorClass);
        PrimitiveAtPut(metaclassClass, BEHAVIOR_SUPERCLASS + 1, behaviorClass);
        
        // Metaclass superclass hierarchy (parallel to class hierarchy)
        PrimitiveAtPut(objectMetaclass, BEHAVIOR_SUPERCLASS + 1, classClass);  // Object class inherits from Class
        PrimitiveAtPut(symbolMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(arrayMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(dictionaryMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(compiledMethodMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(undefinedObjectMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(behaviorMetaclass, BEHAVIOR_SUPERCLASS + 1, objectMetaclass);
        PrimitiveAtPut(classMetaclass, BEHAVIOR_SUPERCLASS + 1, behaviorMetaclass);
        PrimitiveAtPut(metaclassMetaclass, BEHAVIOR_SUPERCLASS + 1, behaviorMetaclass);
        
        // Step 6: Create and set method dictionaries
        SetMethodDictionary(objectClass);
        SetMethodDictionary(symbolClass);
        SetMethodDictionary(arrayClass);
        SetMethodDictionary(dictionaryClass);
        SetMethodDictionary(behaviorClass);
        SetMethodDictionary(classClass);
        SetMethodDictionary(metaclassClass);
        SetMethodDictionary(compiledMethodClass);
        SetMethodDictionary(undefinedObjectClass);
        
        // Also for metaclasses
        SetMethodDictionary(objectMetaclass);
        SetMethodDictionary(symbolMetaclass);
        SetMethodDictionary(arrayMetaclass);
        SetMethodDictionary(dictionaryMetaclass);
        SetMethodDictionary(behaviorMetaclass);
        SetMethodDictionary(classMetaclass);
        SetMethodDictionary(metaclassMetaclass);
        SetMethodDictionary(compiledMethodMetaclass);
        SetMethodDictionary(undefinedObjectMetaclass);
        
        // Step 7: Create classPools (for class variables)
        SetClassPool(objectClass);
        SetClassPool(symbolClass);
        SetClassPool(arrayClass);
        SetClassPool(dictionaryClass);
        SetClassPool(behaviorClass);
        SetClassPool(classClass);
        SetClassPool(metaclassClass);
        SetClassPool(compiledMethodClass);
        SetClassPool(undefinedObjectClass);
        
        // Step 8: Register with SystemDictionary
        RegisterClass("Object", objectClass);
        RegisterClass("Symbol", symbolClass);
        RegisterClass("Array", arrayClass);
        RegisterClass("Dictionary", dictionaryClass);
        RegisterClass("Behavior", behaviorClass);
        RegisterClass("Class", classClass);
        RegisterClass("Metaclass", metaclassClass);
        RegisterClass("CompiledMethod", compiledMethodClass);
        RegisterClass("UndefinedObject", undefinedObjectClass);
        
        // Register special objects
        SystemDictionary.Nil = nil;
        SystemDictionary.RegisterGlobal("nil", nil);
        
        Console.WriteLine("[MINIMAL_BOOTSTRAP] Phase 1 & 2 complete!");
        Console.WriteLine("[MINIMAL_BOOTSTRAP] Created: Object, Class, Metaclass, Behavior,");
        Console.WriteLine("[MINIMAL_BOOTSTRAP]          CompiledMethod, Symbol, Dictionary, Array, UndefinedObject");
        Console.WriteLine("[MINIMAL_BOOTSTRAP] System ready for: Object subclass: #NewClass ...");
    }
    
    // ============================================
    // VM PRIMITIVE IMPLEMENTATIONS
    // ============================================
    
    /// <summary>
    /// primitiveNew: size - Allocate object with N fields
    /// </summary>
    private static SmalltalkObject PrimitiveNew(SmalltalkObject? cls, int size)
    {
        return new SmalltalkObject((SmalltalkClass?)cls, size);
    }
    
    /// <summary>
    /// primitiveNewClassNamed: - Create a new class object
    /// </summary>
    private static SmalltalkObject PrimitiveNewClass(string name)
    {
        var cls = PrimitiveNew(null, 8);  // Class has 8 fields
        PrimitiveAtPut(cls, CLASS_NAME + 1, new Symbol(name));
        return cls;
    }
    
    /// <summary>
    /// primitiveNewMetaclass: - Create a metaclass for a class
    /// </summary>
    private static SmalltalkObject PrimitiveNewMetaclass(SmalltalkObject cls)
    {
        var metaclass = PrimitiveNew(null, 6);  // Metaclass has 6 fields
        PrimitiveAtPut(metaclass, METACLASS_THISCLASS + 1, cls);
        return metaclass;
    }
    
    /// <summary>
    /// primitiveAt:put: - Set field at index (1-based)
    /// </summary>
    private static void PrimitiveAtPut(SmalltalkObject obj, int oneBasedIndex, object? value)
    {
        if (oneBasedIndex < 1 || oneBasedIndex > obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        obj.Fields[oneBasedIndex - 1] = value;
    }
    
    /// <summary>
    /// primitiveSetClass: - Set the class pointer of an object
    /// </summary>
    private static void PrimitiveSetClass(SmalltalkObject obj, SmalltalkObject cls)
    {
        obj.ClassPointer = (SmalltalkClass)cls;
    }
    
    /// <summary>
    /// primitiveNewDictionary - Create an empty dictionary
    /// </summary>
    private static SmalltalkObject PrimitiveNewDictionary()
    {
        var dict = PrimitiveNew(dictionaryClass, 2);
        PrimitiveAtPut(dict, DICT_TALLY + 1, new SmallInteger(0));
        
        // Create small array for storage
        var array = PrimitiveNew(arrayClass, 11);
        for (int i = 1; i <= 11; i++)
        {
            PrimitiveAtPut(array, i, nil);
        }
        PrimitiveAtPut(dict, DICT_ARRAY + 1, array);
        
        return dict;
    }
    
    /// <summary>
    /// Set the method dictionary for a class
    /// </summary>
    private static void SetMethodDictionary(SmalltalkObject cls)
    {
        var methodDict = PrimitiveNewDictionary();
        PrimitiveAtPut(cls, BEHAVIOR_METHODDICT + 1, methodDict);
    }
    
    /// <summary>
    /// Set the classPool for a class
    /// </summary>
    private static void SetClassPool(SmalltalkObject cls)
    {
        // Only Class instances have classPool (not Metaclass instances)
        if (cls.Fields.Length > CLASS_CLASSPOOL)
        {
            var classPool = PrimitiveNewDictionary();
            PrimitiveAtPut(cls, CLASS_CLASSPOOL + 1, classPool);
        }
    }
    
    /// <summary>
    /// Register a class with SystemDictionary
    /// </summary>
    private static void RegisterClass(string name, SmalltalkObject cls)
    {
        SystemDictionary.RegisterBootstrapClass(name, (SmalltalkClass)cls);
    }
}