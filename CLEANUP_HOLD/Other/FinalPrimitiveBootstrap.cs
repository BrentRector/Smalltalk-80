// ==============================================================================
// Final Primitive Bootstrap - Exact Implementation of Bootstrap Script
// ==============================================================================
// This follows the exact primitive-level script with proper creation order:
// 1. Metaclass (self-referential)
// 2. Class
// 3. Behavior  
// 4. ProtoObject (root)
// 5. Object
// 6. Symbol
// 7. Dictionary
// 8. Smalltalk global
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Smalltalk80;

/// <summary>
/// Final bootstrap implementation using only primitive operations.
/// Creates the minimal Smalltalk class hierarchy in the correct order.
/// </summary>
public static class FinalPrimitiveBootstrap
{
    // Field indices - matching Smalltalk-80 specification
    private const int SUPERCLASS = 0;        // Behavior slot 0
    private const int METHODDICT = 1;        // Behavior slot 1
    private const int FORMAT = 2;            // Behavior slot 2
    private const int INSTANCEVARS = 3;      // ClassDescription slot 3
    private const int ORGANIZATION = 4;      // ClassDescription slot 4
    private const int NAME = 5;              // Class slot 5
    private const int CLASSPOOL = 6;         // Class slot 6 - CRITICAL!
    private const int SHAREDPOOLS = 7;       // Class slot 7
    private const int THISCLASS = 5;         // Metaclass slot 5
    
    // Dictionary structure
    private const int DICT_TALLY = 0;
    private const int DICT_ARRAY = 1;
    
    // Association structure
    private const int ASSOC_KEY = 0;
    private const int ASSOC_VALUE = 1;
    
    // Core objects
    private static SmalltalkObject? nil;
    private static SmalltalkObject? metaclass;
    private static SmalltalkObject? classObj;
    private static SmalltalkObject? behavior;
    private static SmalltalkObject? protoObject;
    private static SmalltalkObject? objectObj;
    private static SmalltalkObject? symbol;
    private static SmalltalkObject? dictionary;
    private static SmalltalkObject? smalltalk;
    
    // Symbol table for interning
    private static readonly Dictionary<string, SmalltalkObject> symbolTable = new();
    
    /// <summary>
    /// Initialize the bootstrap following the exact primitive script.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[FINAL_BOOTSTRAP] Starting primitive-level bootstrap...");
        
        // Clear any existing state
        SystemDictionary.Clear();
        symbolTable.Clear();
        
        // Create nil first
        nil = PrimitiveNew(null, 0);
        
        // 🔹 Step 1: Create Metaclass (Self-referential)
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 1: Creating Metaclass (self-referential)...");
        metaclass = PrimitiveNewClass();
        PrimitiveSetSlot(metaclass, NAME, PrimitiveInternSymbol("Metaclass"));
        PrimitiveSetSlot(metaclass, SUPERCLASS, nil); // Temporary
        PrimitiveSetSlot(metaclass, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(metaclass, INSTANCEVARS, PrimitiveNewArray(1)); // thisClass
        PrimitiveSetClass(metaclass, metaclass); // SELF-REFERENTIAL!
        
        // 🔹 Step 2: Create Class
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 2: Creating Class...");
        classObj = PrimitiveNewClass();
        PrimitiveSetSlot(classObj, NAME, PrimitiveInternSymbol("Class"));
        PrimitiveSetSlot(classObj, SUPERCLASS, nil); // Will be Behavior
        PrimitiveSetSlot(classObj, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(classObj, INSTANCEVARS, PrimitiveNewArray(3)); // name, classPool, sharedPools
        PrimitiveSetClass(classObj, metaclass);
        
        // 🔹 Step 3: Create Behavior
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 3: Creating Behavior...");
        behavior = PrimitiveNewClass();
        PrimitiveSetSlot(behavior, NAME, PrimitiveInternSymbol("Behavior"));
        PrimitiveSetSlot(behavior, SUPERCLASS, nil); // Will be ProtoObject
        PrimitiveSetSlot(behavior, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(behavior, INSTANCEVARS, PrimitiveNewArray(3)); // superclass, methodDict, format
        PrimitiveSetClass(behavior, classObj);
        
        // 🔹 Step 4: Link Class to Behavior
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 4: Linking Class -> Behavior...");
        PrimitiveSetSlot(classObj, SUPERCLASS, behavior);
        
        // 🔹 Step 5: Create ProtoObject
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 5: Creating ProtoObject (root)...");
        protoObject = PrimitiveNewClass();
        PrimitiveSetSlot(protoObject, NAME, PrimitiveInternSymbol("ProtoObject"));
        PrimitiveSetSlot(protoObject, SUPERCLASS, nil); // ROOT!
        PrimitiveSetSlot(protoObject, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(protoObject, INSTANCEVARS, PrimitiveNewArray(0));
        PrimitiveSetSlot(protoObject, CLASSPOOL, PrimitiveNewDictionary()); // Empty class pool
        PrimitiveSetClass(protoObject, classObj);
        
        // 🔹 Step 6: Link Behavior to ProtoObject
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 6: Linking Behavior -> ProtoObject...");
        PrimitiveSetSlot(behavior, SUPERCLASS, protoObject);
        
        // Now fix Metaclass superclass (was temporarily nil)
        Console.WriteLine("[FINAL_BOOTSTRAP] Fixing Metaclass -> Class link...");
        PrimitiveSetSlot(metaclass, SUPERCLASS, classObj);
        
        // 🔹 Step 7: Create Object
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 7: Creating Object...");
        objectObj = PrimitiveNewClass();
        PrimitiveSetSlot(objectObj, NAME, PrimitiveInternSymbol("Object"));
        PrimitiveSetSlot(objectObj, SUPERCLASS, protoObject);
        PrimitiveSetSlot(objectObj, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(objectObj, INSTANCEVARS, PrimitiveNewArray(0));
        
        // Object has DependentsFields class variable
        var objectClassPool = PrimitiveNewDictionary();
        AddToDictionary(objectClassPool, "DependentsFields", nil);
        PrimitiveSetSlot(objectObj, CLASSPOOL, objectClassPool); // CRITICAL at slot 6!
        
        PrimitiveSetClass(objectObj, classObj);
        
        // 🔹 Step 8: Create Symbol
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 8: Creating Symbol...");
        symbol = PrimitiveNewClass();
        PrimitiveSetSlot(symbol, NAME, PrimitiveInternSymbol("Symbol"));
        PrimitiveSetSlot(symbol, SUPERCLASS, objectObj);
        PrimitiveSetSlot(symbol, METHODDICT, PrimitiveNewDictionary());
        PrimitiveSetSlot(symbol, INSTANCEVARS, PrimitiveNewArray(0));
        
        // Symbol has SymbolTable class variable
        var symbolClassPool = PrimitiveNewDictionary();
        var symbolTableArray = PrimitiveNewArray(256); // Initial symbol table
        AddToDictionary(symbolClassPool, "SymbolTable", symbolTableArray);
        PrimitiveSetSlot(symbol, CLASSPOOL, symbolClassPool);
        
        PrimitiveSetClass(symbol, classObj);
        
        // 🔹 Step 9: Create Dictionary
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 9: Creating Dictionary...");
        dictionary = PrimitiveNewClass();
        PrimitiveSetSlot(dictionary, NAME, PrimitiveInternSymbol("Dictionary"));
        PrimitiveSetSlot(dictionary, SUPERCLASS, objectObj);
        PrimitiveSetSlot(dictionary, METHODDICT, PrimitiveNewDictionary());
        
        // Dictionary has instance variables: tally, array
        var dictIvars = PrimitiveNewArray(2);
        PrimitiveSetSlot(dictIvars, 0, PrimitiveInternSymbol("tally"));
        PrimitiveSetSlot(dictIvars, 1, PrimitiveInternSymbol("array"));
        PrimitiveSetSlot(dictionary, INSTANCEVARS, dictIvars);
        
        PrimitiveSetSlot(dictionary, CLASSPOOL, PrimitiveNewDictionary()); // Empty class pool
        PrimitiveSetClass(dictionary, classObj);
        
        // 🔹 Step 10: Create Smalltalk Global Dictionary
        Console.WriteLine("[FINAL_BOOTSTRAP] Step 10: Creating Smalltalk global dictionary...");
        smalltalk = PrimitiveNewInstanceOf(dictionary);
        AddToDictionary(smalltalk, "ProtoObject", protoObject);
        AddToDictionary(smalltalk, "Object", objectObj);
        AddToDictionary(smalltalk, "Class", classObj);
        AddToDictionary(smalltalk, "Metaclass", metaclass);
        AddToDictionary(smalltalk, "Behavior", behavior);
        AddToDictionary(smalltalk, "Symbol", symbol);
        AddToDictionary(smalltalk, "Dictionary", dictionary);
        AddToDictionary(smalltalk, "Smalltalk", smalltalk); // Self-reference
        AddToDictionary(smalltalk, "nil", nil);
        
        // Register with SystemDictionary for C# interop
        RegisterWithSystemDictionary();
        
        // Validate the bootstrap
        ValidateBootstrap();
        
        Console.WriteLine("[FINAL_BOOTSTRAP] Bootstrap complete!");
    }
    
    // ============================================
    // PRIMITIVE OPERATIONS
    // ============================================
    
    /// <summary>
    /// Primitive: Create new object with given class and size.
    /// </summary>
    private static SmalltalkObject PrimitiveNew(SmalltalkObject? cls, int size)
    {
        return new SmalltalkObject((SmalltalkClass?)cls, size);
    }
    
    /// <summary>
    /// Primitive: Create new class object (8 slots).
    /// </summary>
    private static SmalltalkObject PrimitiveNewClass()
    {
        return PrimitiveNew(null, 8); // 8 slots for Class
    }
    
    /// <summary>
    /// Primitive: Set slot value (0-based indexing).
    /// </summary>
    private static void PrimitiveSetSlot(SmalltalkObject obj, int slot, object? value)
    {
        if (slot < 0 || slot >= obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));
        obj.Fields[slot] = value;
    }
    
    /// <summary>
    /// Primitive: Set class pointer.
    /// </summary>
    private static void PrimitiveSetClass(SmalltalkObject obj, SmalltalkObject cls)
    {
        obj.ClassPointer = (SmalltalkClass)cls;
    }
    
    /// <summary>
    /// Primitive: Intern a symbol.
    /// </summary>
    private static SmalltalkObject PrimitiveInternSymbol(string str)
    {
        if (symbolTable.TryGetValue(str, out var existing))
            return existing;
        
        var sym = new Symbol(str);
        symbolTable[str] = sym;
        return sym;
    }
    
    /// <summary>
    /// Primitive: Create new dictionary.
    /// </summary>
    private static SmalltalkObject PrimitiveNewDictionary()
    {
        var dict = PrimitiveNew(null, 2); // tally, array
        PrimitiveSetSlot(dict, DICT_TALLY, 0);
        PrimitiveSetSlot(dict, DICT_ARRAY, PrimitiveNewArray(11)); // Initial capacity
        return dict;
    }
    
    /// <summary>
    /// Primitive: Create new array.
    /// </summary>
    private static SmalltalkObject PrimitiveNewArray(int size)
    {
        var array = PrimitiveNew(null, size);
        // Initialize all slots to nil
        for (int i = 0; i < size; i++)
        {
            PrimitiveSetSlot(array, i, nil);
        }
        return array;
    }
    
    /// <summary>
    /// Primitive: Create new instance of class.
    /// </summary>
    private static SmalltalkObject PrimitiveNewInstanceOf(SmalltalkObject cls)
    {
        // For Dictionary: 2 instance variables (tally, array)
        return PrimitiveNew(cls, 2);
    }
    
    /// <summary>
    /// Helper: Add key-value to dictionary.
    /// </summary>
    private static void AddToDictionary(SmalltalkObject dict, string key, object? value)
    {
        var array = dict.Fields[DICT_ARRAY] as SmalltalkObject;
        if (array == null) return;
        
        // Create association
        var assoc = PrimitiveNew(null, 2);
        PrimitiveSetSlot(assoc, ASSOC_KEY, PrimitiveInternSymbol(key));
        PrimitiveSetSlot(assoc, ASSOC_VALUE, value);
        
        // Find empty slot (simplified - no hashing)
        var tally = Convert.ToInt32(dict.Fields[DICT_TALLY] ?? 0);
        for (int i = 0; i < array.Fields.Length; i++)
        {
            if (array.Fields[i] == nil)
            {
                array.Fields[i] = assoc;
                dict.Fields[DICT_TALLY] = tally + 1;
                break;
            }
        }
    }
    
    /// <summary>
    /// Register bootstrap classes with SystemDictionary.
    /// </summary>
    private static void RegisterWithSystemDictionary()
    {
        Console.WriteLine("[FINAL_BOOTSTRAP] Registering with SystemDictionary...");
        
        SystemDictionary.RegisterBootstrapClass("ProtoObject", (SmalltalkClass?)protoObject);
        SystemDictionary.RegisterBootstrapClass("Object", (SmalltalkClass?)objectObj);
        SystemDictionary.RegisterBootstrapClass("Class", (SmalltalkClass?)classObj);
        SystemDictionary.RegisterBootstrapClass("Metaclass", (SmalltalkClass?)metaclass);
        SystemDictionary.RegisterBootstrapClass("Behavior", (SmalltalkClass?)behavior);
        SystemDictionary.RegisterBootstrapClass("Symbol", (SmalltalkClass?)symbol);
        SystemDictionary.RegisterBootstrapClass("Dictionary", (SmalltalkClass?)dictionary);
        
        SystemDictionary.SetGlobal("nil", nil!);
        SystemDictionary.SetGlobal("Smalltalk", smalltalk!);
    }
    
    /// <summary>
    /// Validate critical bootstrap relationships.
    /// </summary>
    private static void ValidateBootstrap()
    {
        Console.WriteLine("[FINAL_BOOTSTRAP] Validating bootstrap integrity...");
        
        // Check Metaclass self-reference
        Debug.Assert(metaclass!.ClassPointer == metaclass, 
            "FAIL: Metaclass must be self-referential");
        Console.WriteLine("  ✓ Metaclass is self-referential");
        
        // Check ProtoObject is root
        Debug.Assert(protoObject!.Fields[SUPERCLASS] == nil, 
            "FAIL: ProtoObject must have nil superclass");
        Console.WriteLine("  ✓ ProtoObject is root (superclass = nil)");
        
        // Check Object extends ProtoObject
        Debug.Assert(objectObj!.Fields[SUPERCLASS] == protoObject, 
            "FAIL: Object must extend ProtoObject");
        Console.WriteLine("  ✓ Object extends ProtoObject");
        
        // Check Class extends Behavior
        Debug.Assert(classObj!.Fields[SUPERCLASS] == behavior, 
            "FAIL: Class must extend Behavior");
        Console.WriteLine("  ✓ Class extends Behavior");
        
        // Check Behavior extends ProtoObject
        Debug.Assert(behavior!.Fields[SUPERCLASS] == protoObject, 
            "FAIL: Behavior must extend ProtoObject");
        Console.WriteLine("  ✓ Behavior extends ProtoObject");
        
        // Check Metaclass extends Class
        Debug.Assert(metaclass!.Fields[SUPERCLASS] == classObj, 
            "FAIL: Metaclass must extend Class");
        Console.WriteLine("  ✓ Metaclass extends Class");
        
        // Check all classes are instances of Class
        Debug.Assert(protoObject!.ClassPointer == classObj, 
            "FAIL: ProtoObject must be instance of Class");
        Debug.Assert(objectObj!.ClassPointer == classObj, 
            "FAIL: Object must be instance of Class");
        Debug.Assert(behavior!.ClassPointer == classObj, 
            "FAIL: Behavior must be instance of Class");
        Console.WriteLine("  ✓ All classes are instances of Class");
        
        // Check classPool at slot 6
        Debug.Assert(objectObj!.Fields[CLASSPOOL] != null, 
            "FAIL: Object must have classPool at slot 6");
        Debug.Assert(objectObj!.Fields[CLASSPOOL] is SmalltalkObject, 
            "FAIL: classPool must be a Dictionary");
        Console.WriteLine("  ✓ Class pool at slot 6 (fixes class variables!)");
        
        // Check Smalltalk global
        Debug.Assert(smalltalk != null, 
            "FAIL: Smalltalk global must exist");
        Debug.Assert(smalltalk!.ClassPointer == dictionary, 
            "FAIL: Smalltalk must be instance of Dictionary");
        Console.WriteLine("  ✓ Smalltalk global dictionary created");
        
        Console.WriteLine("[FINAL_BOOTSTRAP] All validations passed!");
    }
    
    /// <summary>
    /// Get the bootstrap results for testing.
    /// </summary>
    public static (
        SmalltalkObject Nil,
        SmalltalkObject ProtoObject,
        SmalltalkObject Object,
        SmalltalkObject Class,
        SmalltalkObject Metaclass,
        SmalltalkObject Behavior,
        SmalltalkObject Symbol,
        SmalltalkObject Dictionary,
        SmalltalkObject Smalltalk
    ) GetBootstrapObjects()
    {
        return (nil!, protoObject!, objectObj!, classObj!, 
                metaclass!, behavior!, symbol!, dictionary!, smalltalk!);
    }
}