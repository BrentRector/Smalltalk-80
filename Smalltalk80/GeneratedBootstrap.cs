// ==============================================================================
// Generated Bootstrap Code
// This would normally be auto-generated from Bootstrap.st
// For now, this is a handwritten version that creates the minimal bootstrap
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Creates the minimal set of classes required to execute any Smalltalk code.
/// This code represents the "baked in" bootstrap that is compiled into the interpreter.
/// </summary>
public static class GeneratedBootstrap
{
    /// <summary>
    /// Initializes the bootstrap classes in memory.
    /// This method should be called once at interpreter startup before any Smalltalk code executes.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[BOOTSTRAP] Initializing generated bootstrap...");
        
        // Clear any existing classes
        SystemDictionary.Clear();
        
        // Create all class objects
        var classes = new Dictionary<string, SmalltalkClass>();
        
        // ============================================
        // Core Object System
        // ============================================
        
        // Create Object - the root of the class hierarchy
        classes["Object"] = SystemDictionary.DefineClass(
            "Object",
            null, // No superclass - it's the root
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects");
        
        // Create UndefinedObject for nil
        classes["UndefinedObject"] = SystemDictionary.DefineClass(
            "UndefinedObject",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects");
        
        // ============================================
        // Metaclass System
        // ============================================
        
        classes["Behavior"] = SystemDictionary.DefineClass(
            "Behavior",
            null, // Superclass set later
            new List<string> { "superclass", "methodDict", "format" },
            new List<string>(),
            new List<string>(),
            "Kernel-Classes");
        
        classes["ClassDescription"] = SystemDictionary.DefineClass(
            "ClassDescription",
            null, // Superclass set later
            new List<string> { "instanceVariables", "organization" },
            new List<string>(),
            new List<string>(),
            "Kernel-Classes");
        
        classes["Class"] = SystemDictionary.DefineClass(
            "Class",
            null, // Superclass set later
            new List<string> { "name", "classPool", "sharedPools" },
            new List<string>(),
            new List<string>(),
            "Kernel-Classes");
        
        classes["Metaclass"] = SystemDictionary.DefineClass(
            "Metaclass",
            null, // Superclass set later
            new List<string> { "thisClass" },
            new List<string>(),
            new List<string>(),
            "Kernel-Classes");
        
        // ============================================
        // Booleans
        // ============================================
        
        classes["Boolean"] = SystemDictionary.DefineClass(
            "Boolean",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects");
        
        classes["True"] = SystemDictionary.DefineClass(
            "True",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects");
        
        classes["False"] = SystemDictionary.DefineClass(
            "False",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Objects");
        
        // ============================================
        // Numbers
        // ============================================
        
        classes["Magnitude"] = SystemDictionary.DefineClass(
            "Magnitude",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Numbers");
        
        classes["Number"] = SystemDictionary.DefineClass(
            "Number",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Numbers");
        
        classes["Integer"] = SystemDictionary.DefineClass(
            "Integer",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Numbers");
        
        classes["SmallInteger"] = SystemDictionary.DefineClass(
            "SmallInteger",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Numbers");
        
        // ============================================
        // Symbols and Strings
        // ============================================
        
        classes["Symbol"] = SystemDictionary.DefineClass(
            "Symbol",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Strings");
        
        // ============================================
        // Collections
        // ============================================
        
        classes["Collection"] = SystemDictionary.DefineClass(
            "Collection",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Abstract");
        
        classes["SequenceableCollection"] = SystemDictionary.DefineClass(
            "SequenceableCollection",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Abstract");
        
        classes["ArrayedCollection"] = SystemDictionary.DefineClass(
            "ArrayedCollection",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Abstract");
        
        classes["Array"] = SystemDictionary.DefineClass(
            "Array",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Arrayed");
        
        classes["String"] = SystemDictionary.DefineClass(
            "String",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Strings");
        
        classes["Set"] = SystemDictionary.DefineClass(
            "Set",
            null, // Superclass set later
            new List<string> { "tally", "array" },
            new List<string>(),
            new List<string>(),
            "Collections-Unordered");
        
        classes["Dictionary"] = SystemDictionary.DefineClass(
            "Dictionary",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Unordered");
        
        classes["SystemDictionary"] = SystemDictionary.DefineClass(
            "SystemDictionary",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "System-Support");
        
        classes["IdentityDictionary"] = SystemDictionary.DefineClass(
            "IdentityDictionary",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Collections-Unordered");
        
        classes["MethodDictionary"] = SystemDictionary.DefineClass(
            "MethodDictionary",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Methods");
        
        // ============================================
        // Supporting Classes
        // ============================================
        
        classes["LookupKey"] = SystemDictionary.DefineClass(
            "LookupKey",
            null, // Superclass set later
            new List<string> { "key" },
            new List<string>(),
            new List<string>(),
            "Collections-Support");
        
        classes["Association"] = SystemDictionary.DefineClass(
            "Association",
            null, // Superclass set later
            new List<string> { "value" },
            new List<string>(),
            new List<string>(),
            "Collections-Support");
        
        // ============================================
        // Execution Machinery
        // ============================================
        
        classes["BlockContext"] = SystemDictionary.DefineClass(
            "BlockContext",
            null, // Superclass set later
            new List<string> { "nargs", "startpc", "home" },
            new List<string>(),
            new List<string>(),
            "Kernel-Methods");
        
        classes["CompiledMethod"] = SystemDictionary.DefineClass(
            "CompiledMethod",
            null, // Superclass set later
            new List<string>(),
            new List<string>(),
            new List<string>(),
            "Kernel-Methods");
        
        // ============================================
        // Organization Classes
        // ============================================
        
        classes["ClassOrganizer"] = SystemDictionary.DefineClass(
            "ClassOrganizer",
            null, // Superclass set later
            new List<string> { "categories" },
            new List<string>(),
            new List<string>(),
            "Kernel-Support");
        
        classes["SystemOrganizer"] = SystemDictionary.DefineClass(
            "SystemOrganizer",
            null, // Superclass set later
            new List<string> { "categories" },
            new List<string>(),
            new List<string>(),
            "System-Support");
        
        // ============================================
        // Set up superclass relationships
        // ============================================
        
        // Object hierarchy
        classes["UndefinedObject"].Superclass = classes["Object"];
        
        // Behavior hierarchy
        classes["Behavior"].Superclass = classes["Object"];
        classes["ClassDescription"].Superclass = classes["Behavior"];
        classes["Class"].Superclass = classes["ClassDescription"];
        classes["Metaclass"].Superclass = classes["ClassDescription"];
        
        // Boolean hierarchy
        classes["Boolean"].Superclass = classes["Object"];
        classes["True"].Superclass = classes["Boolean"];
        classes["False"].Superclass = classes["Boolean"];
        
        // Number hierarchy
        classes["Magnitude"].Superclass = classes["Object"];
        classes["Number"].Superclass = classes["Magnitude"];
        classes["Integer"].Superclass = classes["Number"];
        classes["SmallInteger"].Superclass = classes["Integer"];
        
        // Symbol
        classes["Symbol"].Superclass = classes["Object"];
        
        // Collection hierarchy
        classes["Collection"].Superclass = classes["Object"];
        classes["SequenceableCollection"].Superclass = classes["Collection"];
        classes["ArrayedCollection"].Superclass = classes["SequenceableCollection"];
        classes["Array"].Superclass = classes["ArrayedCollection"];
        classes["String"].Superclass = classes["ArrayedCollection"];
        classes["Set"].Superclass = classes["Collection"];
        classes["Dictionary"].Superclass = classes["Set"];
        classes["SystemDictionary"].Superclass = classes["Dictionary"];
        classes["IdentityDictionary"].Superclass = classes["Dictionary"];
        classes["MethodDictionary"].Superclass = classes["IdentityDictionary"];
        
        // LookupKey hierarchy
        classes["LookupKey"].Superclass = classes["Magnitude"];
        classes["Association"].Superclass = classes["LookupKey"];
        
        // Execution classes
        classes["BlockContext"].Superclass = classes["Object"];
        classes["CompiledMethod"].Superclass = classes["Object"];
        
        // Organization classes
        classes["ClassOrganizer"].Superclass = classes["Object"];
        classes["SystemOrganizer"].Superclass = classes["Object"];
        
        // Set up metaclass relationships
        SetupMetaclassHierarchy(classes);
        
        // Install essential methods
        InstallBootstrapMethods(classes);
        
        // Initialize global variables
        InitializeGlobals(classes);
        
        Console.WriteLine($"[BOOTSTRAP] Initialized {classes.Count} bootstrap classes");
    }
    
    /// <summary>
    /// Sets up the metaclass hierarchy.
    /// </summary>
    private static void SetupMetaclassHierarchy(Dictionary<string, SmalltalkClass> classes)
    {
        // Create metaclasses for each class
        foreach (var kvp in classes)
        {
            var cls = kvp.Value;
            var className = kvp.Key; // Use the key as the class name
            var metaclass = new SmalltalkClass();
            metaclass.ThisClass = cls;
            cls.Metaclass = metaclass;
            cls.ClassPointer = metaclass;
        }
        
        // Set up metaclass superclass relationships
        foreach (var kvp in classes)
        {
            var cls = kvp.Value;
            if (cls.Superclass != null && cls.Superclass.Metaclass != null)
            {
                cls.Metaclass!.Superclass = cls.Superclass.Metaclass;
            }
        }
        
        // Special case: Class's metaclass is Metaclass
        classes["Class"].Metaclass = classes["Metaclass"];
        classes["Metaclass"].Metaclass = classes["Metaclass"];
    }
    
    /// <summary>
    /// Installs the essential bootstrap methods.
    /// </summary>
    private static void InstallBootstrapMethods(Dictionary<string, SmalltalkClass> classes)
    {
        // Object methods
        InstallMethod(classes["Object"], "==",
            new byte[] { 0x70, 0x7C }, // primitive 110, returnTop
            new object[0], 0, true, 110);
        
        InstallMethod(classes["Object"], "class",
            new byte[] { 0x71, 0x7C }, // primitive 111, returnTop
            new object[0], 0, true, 111);
        
        InstallMethod(classes["Object"], "isNil",
            new byte[] { 0x79 }, // returnFalse
            new object[0], 0, false, 0);
        
        InstallMethod(classes["Object"], "notNil",
            new byte[] { 0x78 }, // returnTrue
            new object[0], 0, false, 0);
        
        // UndefinedObject methods
        InstallMethod(classes["UndefinedObject"], "isNil",
            new byte[] { 0x78 }, // returnTrue
            new object[0], 0, false, 0);
        
        InstallMethod(classes["UndefinedObject"], "notNil",
            new byte[] { 0x79 }, // returnFalse
            new object[0], 0, false, 0);
        
        // Behavior methods
        InstallMethod(classes["Behavior"], "basicNew",
            new byte[] { 0x46, 0x7C }, // primitive 70, returnTop
            new object[0], 0, true, 70);
        
        InstallMethod(classes["Behavior"], "basicNew:",
            new byte[] { 0x47, 0x7C }, // primitive 71, returnTop
            new object[0], 0, true, 71);
        
        InstallMethod(classes["Behavior"], "new",
            new byte[] { 0x70, 0xCE }, // pushReceiver, send basicNew
            new object[] { new Symbol("basicNew") }, 0, false, 0);
        
        // Boolean methods - simplified for bootstrap
        InstallMethod(classes["True"], "ifTrue:",
            new byte[] { 0x10, 0xCA }, // pushTemp0, send value
            new object[] { new Symbol("value") }, 0, false, 0);
        
        InstallMethod(classes["True"], "ifFalse:",
            new byte[] { 0x7B }, // returnNil
            new object[0], 0, false, 0);
        
        InstallMethod(classes["False"], "ifTrue:",
            new byte[] { 0x7B }, // returnNil
            new object[0], 0, false, 0);
        
        InstallMethod(classes["False"], "ifFalse:",
            new byte[] { 0x10, 0xCA }, // pushTemp0, send value
            new object[] { new Symbol("value") }, 0, false, 0);
        
        // SmallInteger arithmetic
        InstallMethod(classes["SmallInteger"], "+",
            new byte[] { 0x01, 0x7C }, // primitive 1, returnTop
            new object[0], 0, true, 1);
        
        InstallMethod(classes["SmallInteger"], "-",
            new byte[] { 0x02, 0x7C }, // primitive 2, returnTop
            new object[0], 0, true, 2);
        
        InstallMethod(classes["SmallInteger"], "*",
            new byte[] { 0x09, 0x7C }, // primitive 9, returnTop
            new object[0], 0, true, 9);
        
        InstallMethod(classes["SmallInteger"], "<",
            new byte[] { 0x03, 0x7C }, // primitive 3, returnTop
            new object[0], 0, true, 3);
        
        InstallMethod(classes["SmallInteger"], "=",
            new byte[] { 0x07, 0x7C }, // primitive 7, returnTop
            new object[0], 0, true, 7);
        
        // Array accessing
        InstallMethod(classes["Array"], "at:",
            new byte[] { 0x3C, 0x7C }, // primitive 60, returnTop
            new object[0], 0, true, 60);
        
        InstallMethod(classes["Array"], "at:put:",
            new byte[] { 0x3D, 0x7C }, // primitive 61, returnTop
            new object[0], 0, true, 61);
        
        InstallMethod(classes["Array"], "size",
            new byte[] { 0x3E, 0x7C }, // primitive 62, returnTop
            new object[0], 0, true, 62);
        
        // BlockContext evaluation
        InstallMethod(classes["BlockContext"], "value",
            new byte[] { 0x51, 0x7C }, // primitive 81, returnTop
            new object[0], 0, true, 81);
    }
    
    /// <summary>
    /// Installs a compiled method in a class.
    /// </summary>
    private static void InstallMethod(SmalltalkClass cls, string selector,
        byte[] bytecodes, object[] literals, int tempCount,
        bool hasPrimitive, int primitiveIndex)
    {
        if (cls == null) return;
        
        var method = new CompiledMethod
        {
            Bytecodes = bytecodes,
            Literals = literals,
            TemporariesCount = tempCount,
            HasPrimitive = hasPrimitive,
            PrimitiveIndex = primitiveIndex
        };
        
        cls.MethodDictionary[new Symbol(selector)] = method;
    }
    
    /// <summary>
    /// Initializes global variables.
    /// </summary>
    private static void InitializeGlobals(Dictionary<string, SmalltalkClass> classes)
    {
        // Get or create Smalltalk system dictionary
        var smalltalk = SystemDictionary.GetSmalltalk();
        
        // Create nil, true, false
        var nil = new SmalltalkObject(classes["UndefinedObject"], 0);
        var trueObj = new SmalltalkObject(classes["True"], 0);
        var falseObj = new SmalltalkObject(classes["False"], 0);
        
        SystemDictionary.SetGlobal("nil", nil);
        SystemDictionary.SetGlobal("true", trueObj);
        SystemDictionary.SetGlobal("false", falseObj);
        
        // Create other essential globals
        if (classes.TryGetValue("SystemOrganizer", out var orgClass))
        {
            SystemDictionary.SetGlobal("SystemOrganization", new SmalltalkObject(orgClass, 0));
        }
        
        if (classes.TryGetValue("Dictionary", out var dictClass))
        {
            SystemDictionary.SetGlobal("Undeclared", new SmalltalkObject(dictClass, 0));
        }
        
        // Create Transcript placeholder
        if (classes.TryGetValue("Object", out var objectClass))
        {
            SystemDictionary.SetGlobal("Transcript", new SmalltalkObject(objectClass, 0));
        }
    }
}