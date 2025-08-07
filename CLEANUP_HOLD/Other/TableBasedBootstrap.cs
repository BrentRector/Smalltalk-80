// ==============================================================================
// Table-Based Minimal Bootstrap
// Implements the exact slot structure from the reference table
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Bootstrap implementation following the exact slot structure from the reference table.
/// Creates the 9 essential classes with their precise field layouts.
/// </summary>
public static class TableBasedBootstrap
{
    // ============================================
    // SLOT INDICES (from the reference table)
    // ============================================
    
    // Behavior slots (5 fields)
    private const int BEHAVIOR_METHODDICT = 0;
    private const int BEHAVIOR_SUPERCLASS = 1;
    private const int BEHAVIOR_FORMAT = 2;
    private const int BEHAVIOR_INSTANCEVARIABLES = 3;
    private const int BEHAVIOR_NAME = 4;
    
    // Symbol slots (2 fields)
    private const int SYMBOL_CHARACTERS = 0;
    private const int SYMBOL_HASH = 1;
    
    // Dictionary slots (3 fields)
    private const int DICT_ARRAY = 0;
    private const int DICT_SIZE = 1;
    private const int DICT_TALLY = 2;
    
    // CompiledMethod slots (3 fields + bytecodes)
    private const int METHOD_BYTECODES = 0;
    private const int METHOD_LITERALS = 1;
    private const int METHOD_HEADER = 2;
    
    // Association slots (for Dictionary entries)
    private const int ASSOC_KEY = 0;
    private const int ASSOC_VALUE = 1;
    
    // References to essential objects
    private static SmalltalkObject? nil;
    private static Dictionary<string, SmalltalkObject> symbolTable = new();
    
    /// <summary>
    /// Execute the table-based bootstrap following the exact specification.
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("🧱 Minimal Class Bootstrap Table Implementation");
        Console.WriteLine("================================================");
        
        // Clear state
        SystemDictionary.Clear();
        symbolTable.Clear();
        
        // Create nil first
        nil = PrimitiveNew(null, 0);
        Console.WriteLine("✓ Created nil (UndefinedObject singleton)");
        
        // ============================================
        // CREATE THE 9 ESSENTIAL CLASSES
        // ============================================
        
        Console.WriteLine("\n📦 Creating essential classes:");
        
        // 1. Symbol - Variable size (characters, hash)
        var symbolClass = CreateBehaviorObject("Symbol");
        Console.WriteLine("  ✓ Symbol (variable: characters, hash)");
        
        // 2. Array - Variable size (size, elements[])
        var arrayClass = CreateBehaviorObject("Array");
        Console.WriteLine("  ✓ Array (variable: size, elements[])");
        
        // 3. Dictionary - Fixed size (array, size, tally)
        var dictionaryClass = CreateBehaviorObject("Dictionary");
        Console.WriteLine("  ✓ Dictionary (fixed: array, size, tally)");
        
        // 4. CompiledMethod - Variable size (bytecodes, literals, header)
        var compiledMethodClass = CreateBehaviorObject("CompiledMethod");
        Console.WriteLine("  ✓ CompiledMethod (variable: bytecodes, literals, header)");
        
        // 5. Object - Fixed size (no fields initially)
        var objectClass = CreateBehaviorObject("Object");
        Console.WriteLine("  ✓ Object (fixed: none)");
        
        // 6. Behavior - Fixed size (methodDict, superclass, format, instanceVariables, name)
        var behaviorClass = CreateBehaviorObject("Behavior");
        Console.WriteLine("  ✓ Behavior (fixed: 5 slots)");
        
        // 7. Class - Fixed size (inherits from Behavior)
        var classClass = CreateBehaviorObject("Class");
        Console.WriteLine("  ✓ Class (inherits from Behavior)");
        
        // 8. Metaclass - Fixed size (inherits from Class)
        var metaclassClass = CreateBehaviorObject("Metaclass");
        Console.WriteLine("  ✓ Metaclass (inherits from Class)");
        
        // 9. UndefinedObject - Fixed size (none)
        var undefinedObjectClass = CreateBehaviorObject("UndefinedObject");
        Console.WriteLine("  ✓ UndefinedObject (fixed: none)");
        
        // ============================================
        // INITIALIZE SLOTS AS PER TABLE
        // ============================================
        
        Console.WriteLine("\n🧩 Slot Initialization:");
        
        // Initialize Behavior
        Console.WriteLine("\n🔹 Behavior:");
        PrimitiveSetSlot(behaviorClass, BEHAVIOR_METHODDICT, PrimitiveNewInstanceOf(dictionaryClass));
        PrimitiveSetSlot(behaviorClass, BEHAVIOR_SUPERCLASS, objectClass);
        PrimitiveSetSlot(behaviorClass, BEHAVIOR_FORMAT, new SmallInteger(0));
        PrimitiveSetSlot(behaviorClass, BEHAVIOR_INSTANCEVARIABLES, PrimitiveNewArrayOfSize(0));
        PrimitiveSetSlot(behaviorClass, BEHAVIOR_NAME, PrimitiveInternSymbol("Behavior"));
        Console.WriteLine("  methodDict ← new Dictionary");
        Console.WriteLine("  superclass ← Object");
        Console.WriteLine("  format ← 0");
        Console.WriteLine("  instanceVariables ← #()");
        Console.WriteLine("  name ← #Behavior");
        
        // Initialize Class
        Console.WriteLine("\n🔹 Class:");
        PrimitiveSetSlot(classClass, BEHAVIOR_SUPERCLASS, behaviorClass);
        PrimitiveSetSlot(classClass, BEHAVIOR_NAME, PrimitiveInternSymbol("Class"));
        PrimitiveSetSlot(classClass, BEHAVIOR_METHODDICT, PrimitiveNewInstanceOf(dictionaryClass));
        Console.WriteLine("  superclass ← Behavior");
        Console.WriteLine("  name ← #Class");
        
        // Initialize Metaclass
        Console.WriteLine("\n🔹 Metaclass:");
        PrimitiveSetSlot(metaclassClass, BEHAVIOR_SUPERCLASS, classClass);
        PrimitiveSetSlot(metaclassClass, BEHAVIOR_NAME, PrimitiveInternSymbol("Metaclass"));
        PrimitiveSetSlot(metaclassClass, BEHAVIOR_METHODDICT, PrimitiveNewInstanceOf(dictionaryClass));
        Console.WriteLine("  superclass ← Class");
        Console.WriteLine("  name ← #Metaclass");
        
        // Initialize Object
        Console.WriteLine("\n🔹 Object:");
        PrimitiveSetSlot(objectClass, BEHAVIOR_SUPERCLASS, nil);
        PrimitiveSetSlot(objectClass, BEHAVIOR_METHODDICT, PrimitiveNewInstanceOf(dictionaryClass));
        PrimitiveSetSlot(objectClass, BEHAVIOR_NAME, PrimitiveInternSymbol("Object"));
        Console.WriteLine("  superclass ← nil");
        Console.WriteLine("  methodDict ← new Dictionary");
        Console.WriteLine("  name ← #Object");
        
        // Initialize other classes
        InitializeClass(symbolClass, "Symbol", objectClass, dictionaryClass);
        InitializeClass(arrayClass, "Array", objectClass, dictionaryClass);
        InitializeClass(dictionaryClass, "Dictionary", objectClass, dictionaryClass);
        InitializeClass(compiledMethodClass, "CompiledMethod", objectClass, dictionaryClass);
        InitializeClass(undefinedObjectClass, "UndefinedObject", objectClass, dictionaryClass);
        
        // ============================================
        // CREATE METACLASSES
        // ============================================
        
        Console.WriteLine("\n🔗 Setting up metaclass relationships:");
        SetupMetaclasses(objectClass, classClass, metaclassClass);
        SetupMetaclasses(behaviorClass, classClass, metaclassClass);
        SetupMetaclasses(classClass, classClass, metaclassClass);
        SetupMetaclasses(metaclassClass, classClass, metaclassClass);
        SetupMetaclasses(symbolClass, classClass, metaclassClass);
        SetupMetaclasses(arrayClass, classClass, metaclassClass);
        SetupMetaclasses(dictionaryClass, classClass, metaclassClass);
        SetupMetaclasses(compiledMethodClass, classClass, metaclassClass);
        SetupMetaclasses(undefinedObjectClass, classClass, metaclassClass);
        
        // Set nil's class
        PrimitiveSetClass(nil, undefinedObjectClass);
        
        // ============================================
        // REGISTER WITH SYSTEM
        // ============================================
        
        RegisterClass("Object", objectClass);
        RegisterClass("Behavior", behaviorClass);
        RegisterClass("Class", classClass);
        RegisterClass("Metaclass", metaclassClass);
        RegisterClass("Symbol", symbolClass);
        RegisterClass("Array", arrayClass);
        RegisterClass("Dictionary", dictionaryClass);
        RegisterClass("CompiledMethod", compiledMethodClass);
        RegisterClass("UndefinedObject", undefinedObjectClass);
        
        SystemDictionary.Nil = nil;
        
        // ============================================
        // EXAMPLE: Creating a Method
        // ============================================
        
        Console.WriteLine("\n🧪 Example: Creating Object>>initialize method");
        CreateExampleMethod(objectClass, dictionaryClass, compiledMethodClass);
        
        Console.WriteLine("\n✅ Bootstrap complete!");
        Console.WriteLine("📊 Memory Format Notes:");
        Console.WriteLine("  • Fixed-size objects have header + fixed slots");
        Console.WriteLine("  • Variable-size objects have header + indexed slots");
        Console.WriteLine("  • Symbols are interned in symbol table");
        Console.WriteLine("  • Methods stored in class methodDict with Symbol keys");
    }
    
    // ============================================
    // PRIMITIVE OPERATIONS
    // ============================================
    
    /// <summary>
    /// primitiveNew - Create an object with specified number of slots
    /// </summary>
    private static SmalltalkObject PrimitiveNew(SmalltalkObject? cls, int slotCount)
    {
        return new SmalltalkObject((SmalltalkClass?)cls, slotCount);
    }
    
    /// <summary>
    /// primitiveNewInstanceOf - Create instance of a class
    /// </summary>
    private static SmalltalkObject PrimitiveNewInstanceOf(SmalltalkObject cls)
    {
        // Determine size based on class type
        var className = GetClassName(cls);
        int size = className switch
        {
            "Dictionary" => 3,  // array, size, tally
            "Symbol" => 2,      // characters, hash
            "Array" => 0,       // Variable size, start empty
            "CompiledMethod" => 3, // bytecodes, literals, header
            _ => 0
        };
        
        return PrimitiveNew(cls, size);
    }
    
    /// <summary>
    /// primitiveNewArrayOfSize - Create an array of specified size
    /// </summary>
    private static SmalltalkObject PrimitiveNewArrayOfSize(int size)
    {
        var array = PrimitiveNew(null, size);
        for (int i = 0; i < size; i++)
        {
            array.Fields[i] = nil;
        }
        return array;
    }
    
    /// <summary>
    /// primitiveInternSymbol - Get or create an interned symbol
    /// </summary>
    private static SmalltalkObject PrimitiveInternSymbol(string str)
    {
        if (symbolTable.TryGetValue(str, out var existing))
        {
            return existing;
        }
        
        var symbol = PrimitiveNew(null, 2);
        symbol.Fields[SYMBOL_CHARACTERS] = str;
        symbol.Fields[SYMBOL_HASH] = new SmallInteger(str.GetHashCode());
        symbolTable[str] = symbol;
        return symbol;
    }
    
    /// <summary>
    /// primitiveSetSlot - Set a named slot in an object
    /// </summary>
    private static void PrimitiveSetSlot(SmalltalkObject obj, int slotIndex, object? value)
    {
        if (slotIndex >= 0 && slotIndex < obj.Fields.Length)
        {
            obj.Fields[slotIndex] = value;
        }
    }
    
    /// <summary>
    /// primitiveGetSlot - Get a named slot from an object
    /// </summary>
    private static object? PrimitiveGetSlot(SmalltalkObject obj, int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < obj.Fields.Length)
        {
            return obj.Fields[slotIndex];
        }
        return nil;
    }
    
    /// <summary>
    /// primitiveSetClass - Set an object's class pointer
    /// </summary>
    private static void PrimitiveSetClass(SmalltalkObject obj, SmalltalkObject cls)
    {
        obj.ClassPointer = (SmalltalkClass)cls;
    }
    
    /// <summary>
    /// primitiveNewCompiledMethod - Create a compiled method
    /// </summary>
    private static SmalltalkObject PrimitiveNewCompiledMethod(
        byte[] bytecodes, 
        object[] literals, 
        MethodHeader header)
    {
        var method = PrimitiveNew(null, 3);
        method.Fields[METHOD_BYTECODES] = bytecodes;
        method.Fields[METHOD_LITERALS] = literals;
        method.Fields[METHOD_HEADER] = header;
        return method;
    }
    
    // ============================================
    // HELPER METHODS
    // ============================================
    
    /// <summary>
    /// Create a Behavior-like object (Class, Metaclass, etc.)
    /// </summary>
    private static SmalltalkObject CreateBehaviorObject(string name)
    {
        var obj = PrimitiveNew(null, 5); // Behavior has 5 slots
        PrimitiveSetSlot(obj, BEHAVIOR_NAME, PrimitiveInternSymbol(name));
        return obj;
    }
    
    /// <summary>
    /// Initialize a class with standard slots
    /// </summary>
    private static void InitializeClass(
        SmalltalkObject cls, 
        string name, 
        SmalltalkObject superclass,
        SmalltalkObject dictionaryClass)
    {
        PrimitiveSetSlot(cls, BEHAVIOR_SUPERCLASS, superclass);
        PrimitiveSetSlot(cls, BEHAVIOR_METHODDICT, PrimitiveNewInstanceOf(dictionaryClass));
        PrimitiveSetSlot(cls, BEHAVIOR_NAME, PrimitiveInternSymbol(name));
        PrimitiveSetSlot(cls, BEHAVIOR_FORMAT, new SmallInteger(0));
        PrimitiveSetSlot(cls, BEHAVIOR_INSTANCEVARIABLES, PrimitiveNewArrayOfSize(0));
    }
    
    /// <summary>
    /// Set up metaclass for a class
    /// </summary>
    private static void SetupMetaclasses(
        SmalltalkObject cls,
        SmalltalkObject classClass,
        SmalltalkObject metaclassClass)
    {
        // Create metaclass
        var metaclass = CreateBehaviorObject(GetClassName(cls) + " class");
        
        // Set metaclass superclass
        var clsSuperclass = PrimitiveGetSlot(cls, BEHAVIOR_SUPERCLASS);
        if (clsSuperclass == nil)
        {
            // Object's metaclass inherits from Class
            PrimitiveSetSlot(metaclass, BEHAVIOR_SUPERCLASS, classClass);
        }
        else
        {
            // Other metaclasses inherit from their superclass's metaclass
            PrimitiveSetSlot(metaclass, BEHAVIOR_SUPERCLASS, clsSuperclass);
        }
        
        // Set class pointers
        PrimitiveSetClass(cls, metaclass);
        PrimitiveSetClass(metaclass, metaclassClass);
    }
    
    /// <summary>
    /// Get class name from a class object
    /// </summary>
    private static string GetClassName(SmalltalkObject cls)
    {
        var nameSymbol = PrimitiveGetSlot(cls, BEHAVIOR_NAME);
        if (nameSymbol != null && nameSymbol is SmalltalkObject sym)
        {
            var chars = sym.Fields[SYMBOL_CHARACTERS];
            if (chars is string str) return str;
        }
        return "Unknown";
    }
    
    /// <summary>
    /// Register a class with SystemDictionary
    /// </summary>
    private static void RegisterClass(string name, SmalltalkObject cls)
    {
        SystemDictionary.RegisterBootstrapClass(name, (SmalltalkClass)cls);
    }
    
    /// <summary>
    /// Example: Create and install a method
    /// </summary>
    private static void CreateExampleMethod(
        SmalltalkObject objectClass,
        SmalltalkObject dictionaryClass,
        SmalltalkObject compiledMethodClass)
    {
        // Create Object>>initialize method
        var bytecodes = new byte[] { 
            (byte)Bytecode.ReturnSelf  // Simple method that returns self
        };
        var literals = new object[] { };
        var header = new MethodHeader(0, 0);
        
        var method = PrimitiveNewCompiledMethod(bytecodes, literals, header);
        PrimitiveSetClass(method, compiledMethodClass);
        
        // Get Object's method dictionary
        var objectMethodDict = PrimitiveGetSlot(objectClass, BEHAVIOR_METHODDICT);
        
        // Store method in dictionary with Symbol key
        var initializeSymbol = PrimitiveInternSymbol("initialize");
        DictionaryAtPut(objectMethodDict!, initializeSymbol, method);
        
        Console.WriteLine("  method := primitiveNewCompiledMethod: bytecodes literals: #() header: 0");
        Console.WriteLine("  objectMethodDict := primitiveGetSlot: objectClass slot: #methodDict");
        Console.WriteLine("  objectMethodDict at: #initialize put: method");
    }
    
    /// <summary>
    /// Simplified dictionary at:put: for bootstrap
    /// </summary>
    private static void DictionaryAtPut(SmalltalkObject dict, SmalltalkObject key, SmalltalkObject value)
    {
        var array = PrimitiveGetSlot(dict, DICT_ARRAY) as SmalltalkObject;
        if (array == null)
        {
            // Initialize array if needed
            array = PrimitiveNewArrayOfSize(11);
            PrimitiveSetSlot(dict, DICT_ARRAY, array);
            PrimitiveSetSlot(dict, DICT_SIZE, new SmallInteger(11));
            PrimitiveSetSlot(dict, DICT_TALLY, new SmallInteger(0));
        }
        
        // Create association
        var assoc = PrimitiveNew(null, 2);
        assoc.Fields[ASSOC_KEY] = key;
        assoc.Fields[ASSOC_VALUE] = value;
        
        // Find empty slot (simplified - no hashing)
        for (int i = 0; i < array.Fields.Length; i++)
        {
            if (array.Fields[i] == nil)
            {
                array.Fields[i] = assoc;
                
                // Update tally
                var tally = (SmallInteger)PrimitiveGetSlot(dict, DICT_TALLY)!;
                PrimitiveSetSlot(dict, DICT_TALLY, new SmallInteger(tally.Value + 1));
                break;
            }
        }
    }
}