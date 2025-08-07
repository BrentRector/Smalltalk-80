// ==============================================================================
// Phase 3: Transition to Message-Based Bootstrapping
// Install essential methods to enable self-hosting
// ==============================================================================

using System;
using System.Collections.Generic;

namespace Smalltalk80;

/// <summary>
/// Phase 3 of bootstrap: Install essential methods that enable Smalltalk
/// to define its own classes via message sends like "Object subclass: #MyClass"
/// </summary>
public static class Phase3Bootstrap
{
    // Field indices (0-based)
    private const int BEHAVIOR_SUPERCLASS = 0;
    private const int BEHAVIOR_METHODDICT = 1;
    private const int BEHAVIOR_FORMAT = 2;
    private const int CLASSDESC_INSTANCEVARIABLES = 3;
    private const int CLASS_NAME = 5;
    private const int CLASS_CLASSPOOL = 6;
    private const int DICT_ARRAY = 1;
    private const int ASSOC_KEY = 0;
    private const int ASSOC_VALUE = 1;
    
    /// <summary>
    /// Install the essential methods needed for self-hosting.
    /// This must be called after MinimalPrimitiveBootstrap.
    /// </summary>
    public static void InstallEssentialMethods()
    {
        Console.WriteLine("[PHASE3] Installing essential methods for self-hosting...");
        
        // Get references to the bootstrap classes
        var objectClass = SystemDictionary.TryGetClass("Object", out var obj) ? obj : throw new Exception("Object not found");
        var classClass = SystemDictionary.TryGetClass("Class", out var cls) ? cls : throw new Exception("Class not found");
        var behaviorClass = SystemDictionary.TryGetClass("Behavior", out var beh) ? beh : throw new Exception("Behavior not found");
        var dictionaryClass = SystemDictionary.TryGetClass("Dictionary", out var dict) ? dict : throw new Exception("Dictionary not found");
        var symbolClass = SystemDictionary.TryGetClass("Symbol", out var sym) ? sym : throw new Exception("Symbol not found");
        var arrayClass = SystemDictionary.TryGetClass("Array", out var arr) ? arr : throw new Exception("Array not found");
        
        // Install critical methods
        InstallObjectMethods(objectClass);
        InstallClassMethods(classClass);
        InstallBehaviorMethods(behaviorClass);
        InstallDictionaryMethods(dictionaryClass);
        InstallSymbolMethods(symbolClass);
        
        // Create the global Smalltalk dictionary
        CreateSmalltalkGlobal(dictionaryClass);
        
        Console.WriteLine("[PHASE3] Essential methods installed!");
        Console.WriteLine("[PHASE3] System ready for: Object subclass: #MyClass ...");
    }
    
    /// <summary>
    /// Install essential Object methods.
    /// </summary>
    private static void InstallObjectMethods(SmalltalkClass objectClass)
    {
        Console.WriteLine("[PHASE3] Installing Object methods...");
        
        // Object>>class - returns the class of the receiver
        var classMethod = CreatePrimitiveMethod(111); // Primitive 111: answer receiver's class
        InstallMethod(objectClass, "class", classMethod);
        
        // Object>>isNil - returns false (overridden in UndefinedObject)
        var isNilMethod = CreateReturnFalseMethod();
        InstallMethod(objectClass, "isNil", isNilMethod);
        
        // Object>>notNil - returns true (overridden in UndefinedObject)
        var notNilMethod = CreateReturnTrueMethod();
        InstallMethod(objectClass, "notNil", notNilMethod);
        
        // Object>>yourself - returns self
        var yourselfMethod = CreateReturnSelfMethod();
        InstallMethod(objectClass, "yourself", yourselfMethod);
    }
    
    /// <summary>
    /// Install essential Class methods for creating subclasses.
    /// </summary>
    private static void InstallClassMethods(SmalltalkClass classClass)
    {
        Console.WriteLine("[PHASE3] Installing Class methods...");
        
        // Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
        // This is THE critical method that enables self-hosting!
        var subclassMethod = CreateSubclassMethod();
        InstallMethod(classClass.Metaclass!, "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", 
                     subclassMethod);
        
        // Shorter convenience methods that delegate to the full version
        var subclassSimpleMethod = CreateSubclassSimpleMethod();
        InstallMethod(classClass.Metaclass!, "subclass:", subclassSimpleMethod);
    }
    
    /// <summary>
    /// Install essential Behavior methods for compiling.
    /// </summary>
    private static void InstallBehaviorMethods(SmalltalkClass behaviorClass)
    {
        Console.WriteLine("[PHASE3] Installing Behavior methods...");
        
        // Behavior>>compile: - compile and install a method
        var compileMethod = CreateCompileMethod();
        InstallMethod(behaviorClass, "compile:", compileMethod);
        
        // Behavior>>compile:classified: - compile with category
        var compileClassifiedMethod = CreateCompileClassifiedMethod();
        InstallMethod(behaviorClass, "compile:classified:", compileClassifiedMethod);
        
        // Behavior>>new - create an instance
        var newMethod = CreatePrimitiveMethod(70); // Primitive 70: basicNew
        InstallMethod(behaviorClass, "new", newMethod);
        
        // Behavior>>new: - create an instance with indexed variables
        var newWithSizeMethod = CreatePrimitiveMethod(71); // Primitive 71: basicNew:
        InstallMethod(behaviorClass, "new:", newWithSizeMethod);
    }
    
    /// <summary>
    /// Install essential Dictionary methods.
    /// </summary>
    private static void InstallDictionaryMethods(SmalltalkClass dictionaryClass)
    {
        Console.WriteLine("[PHASE3] Installing Dictionary methods...");
        
        // Dictionary>>at:put: - store a key-value pair
        var atPutMethod = CreateDictionaryAtPutMethod();
        InstallMethod(dictionaryClass, "at:put:", atPutMethod);
        
        // Dictionary>>at: - retrieve value for key
        var atMethod = CreateDictionaryAtMethod();
        InstallMethod(dictionaryClass, "at:", atMethod);
        
        // Dictionary>>at:ifAbsent: - retrieve with default block
        var atIfAbsentMethod = CreateDictionaryAtIfAbsentMethod();
        InstallMethod(dictionaryClass, "at:ifAbsent:", atIfAbsentMethod);
    }
    
    /// <summary>
    /// Install essential Symbol methods.
    /// </summary>
    private static void InstallSymbolMethods(SmalltalkClass symbolClass)
    {
        Console.WriteLine("[PHASE3] Installing Symbol methods...");
        
        // Symbol>>asSymbol - returns self (symbols are already symbols)
        var asSymbolMethod = CreateReturnSelfMethod();
        InstallMethod(symbolClass, "asSymbol", asSymbolMethod);
    }
    
    /// <summary>
    /// Create the global Smalltalk dictionary.
    /// </summary>
    private static void CreateSmalltalkGlobal(SmalltalkClass dictionaryClass)
    {
        Console.WriteLine("[PHASE3] Creating Smalltalk global dictionary...");
        
        // Create SystemDictionary class (subclass of Dictionary)
        var systemDictClass = CreateSystemDictionaryClass(dictionaryClass);
        
        // Create the Smalltalk instance
        var smalltalk = new SmalltalkObject(systemDictClass, 2); // Dictionary has 2 fields
        smalltalk.Fields[0] = new SmallInteger(0); // tally
        
        // Create array for storage
        var array = new SmalltalkObject(
            SystemDictionary.TryGetClass("Array", out var arr) ? arr : null, 
            101); // Start with 101 slots
        for (int i = 0; i < 101; i++)
        {
            array.Fields[i] = SystemDictionary.Nil;
        }
        smalltalk.Fields[1] = array;
        
        // Add all bootstrap classes to Smalltalk
        AddToSmalltalk(smalltalk, "Object");
        AddToSmalltalk(smalltalk, "Class");
        AddToSmalltalk(smalltalk, "Metaclass");
        AddToSmalltalk(smalltalk, "Behavior");
        AddToSmalltalk(smalltalk, "Symbol");
        AddToSmalltalk(smalltalk, "Dictionary");
        AddToSmalltalk(smalltalk, "Array");
        AddToSmalltalk(smalltalk, "CompiledMethod");
        AddToSmalltalk(smalltalk, "UndefinedObject");
        AddToSmalltalk(smalltalk, "SystemDictionary");
        
        // Add special objects
        AddToSmalltalkObject(smalltalk, "nil", SystemDictionary.Nil);
        AddToSmalltalkObject(smalltalk, "true", SystemDictionary.True);
        AddToSmalltalkObject(smalltalk, "false", SystemDictionary.False);
        AddToSmalltalkObject(smalltalk, "Smalltalk", smalltalk); // Self-reference!
        
        // Register the global
        SystemDictionary.RegisterGlobal("Smalltalk", smalltalk);
        
        Console.WriteLine("[PHASE3] Smalltalk global dictionary created with all bootstrap classes");
    }
    
    // ============================================
    // METHOD CREATION HELPERS
    // ============================================
    
    /// <summary>
    /// Create the critical Object>>subclass:instanceVariableNames:... method.
    /// This enables: Object subclass: #Point instanceVariableNames: 'x y' ...
    /// </summary>
    private static CompiledMethod CreateSubclassMethod()
    {
        // This would normally be compiled from Smalltalk source:
        // subclass: className instanceVariableNames: ivNames classVariableNames: cvNames 
        //     poolDictionaries: pools category: category
        //     "Create a new subclass of the receiver"
        //     | newClass |
        //     newClass := self createSubclass: className.
        //     newClass instanceVariableNames: ivNames.
        //     newClass classVariableNames: cvNames.
        //     newClass poolDictionaries: pools.
        //     newClass category: category.
        //     Smalltalk at: className put: newClass.
        //     ^newClass
        
        // For bootstrap, we use a primitive implementation
        var header = new MethodHeader(5, 0); // 5 arguments
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.PrimitiveBytecode, 254, // Extended primitive 254: create subclass
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Create a new subclass of the receiver");
    }
    
    /// <summary>
    /// Simple subclass: method that provides defaults.
    /// </summary>
    private static CompiledMethod CreateSubclassSimpleMethod()
    {
        // subclass: className
        //     ^self subclass: className 
        //           instanceVariableNames: '' 
        //           classVariableNames: '' 
        //           poolDictionaries: '' 
        //           category: 'Unclassified'
        
        var header = new MethodHeader(1, 0); // 1 argument
        var literals = new object[] {
            new Symbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"),
            "", "", "", "Unclassified"
        };
        var bytecodes = new byte[] {
            (byte)Bytecode.PushSelf,
            (byte)Bytecode.PushTemporaryVariable0, // className
            (byte)Bytecode.PushLiteralConstant1,    // ''
            (byte)Bytecode.PushLiteralConstant2,    // ''
            (byte)Bytecode.PushLiteralConstant3,    // ''
            (byte)Bytecode.PushLiteralConstant4,    // 'Unclassified'
            (byte)Bytecode.SingleExtendedSend, (byte)(5 << 5 | 0), // Send with 5 args
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Create a subclass with default settings");
    }
    
    /// <summary>
    /// Create Behavior>>compile: method.
    /// </summary>
    private static CompiledMethod CreateCompileMethod()
    {
        // compile: sourceString
        //     "Compile and install the method"
        //     ^self compile: sourceString classified: 'unclassified'
        
        var header = new MethodHeader(1, 0);
        var literals = new object[] {
            new Symbol("compile:classified:"),
            "unclassified"
        };
        var bytecodes = new byte[] {
            (byte)Bytecode.PushSelf,
            (byte)Bytecode.PushTemporaryVariable0,  // sourceString
            (byte)Bytecode.PushLiteralConstant1,    // 'unclassified'
            (byte)Bytecode.SingleExtendedSend, (byte)(2 << 5 | 0), // Send with 2 args
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Compile and install a method");
    }
    
    /// <summary>
    /// Create Behavior>>compile:classified: method.
    /// </summary>
    private static CompiledMethod CreateCompileClassifiedMethod()
    {
        // For bootstrap, use primitive that calls into C# compiler
        var header = new MethodHeader(2, 0); // 2 arguments
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.PrimitiveBytecode, 255, // Extended primitive 255: compile method
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Compile and install a method with category");
    }
    
    /// <summary>
    /// Create Dictionary>>at:put: method.
    /// </summary>
    private static CompiledMethod CreateDictionaryAtPutMethod()
    {
        // Simplified for bootstrap - uses primitive
        var header = new MethodHeader(2, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.PrimitiveBytecode, 251, // Extended primitive 251: dictionary at:put:
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Store value at key in dictionary");
    }
    
    /// <summary>
    /// Create Dictionary>>at: method.
    /// </summary>
    private static CompiledMethod CreateDictionaryAtMethod()
    {
        var header = new MethodHeader(1, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.PrimitiveBytecode, 250, // Extended primitive 250: dictionary at:
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Retrieve value for key from dictionary");
    }
    
    /// <summary>
    /// Create Dictionary>>at:ifAbsent: method.
    /// </summary>
    private static CompiledMethod CreateDictionaryAtIfAbsentMethod()
    {
        var header = new MethodHeader(2, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.PrimitiveBytecode, 252, // Extended primitive 252: dictionary at:ifAbsent:
            (byte)Bytecode.ReturnTop
        };
        
        return new CompiledMethod(header, literals, bytecodes, 
            "Retrieve value for key, or evaluate block if absent");
    }
    
    /// <summary>
    /// Create a method that uses a primitive.
    /// </summary>
    private static CompiledMethod CreatePrimitiveMethod(int primitiveIndex)
    {
        var header = new MethodHeader(0, 0, true, primitiveIndex);
        var literals = new object[] { };
        var bytecodes = new byte[] {
            (byte)Bytecode.ReturnSelf // Fallback if primitive fails
        };
        
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    /// <summary>
    /// Create a method that returns self.
    /// </summary>
    private static CompiledMethod CreateReturnSelfMethod()
    {
        var header = new MethodHeader(0, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] { (byte)Bytecode.ReturnSelf };
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    /// <summary>
    /// Create a method that returns true.
    /// </summary>
    private static CompiledMethod CreateReturnTrueMethod()
    {
        var header = new MethodHeader(0, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] { (byte)Bytecode.ReturnTrue };
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    /// <summary>
    /// Create a method that returns false.
    /// </summary>
    private static CompiledMethod CreateReturnFalseMethod()
    {
        var header = new MethodHeader(0, 0);
        var literals = new object[] { };
        var bytecodes = new byte[] { (byte)Bytecode.ReturnFalse };
        return new CompiledMethod(header, literals, bytecodes, null);
    }
    
    /// <summary>
    /// Create SystemDictionary class.
    /// </summary>
    private static SmalltalkClass CreateSystemDictionaryClass(SmalltalkClass dictionaryClass)
    {
        // Manually create SystemDictionary as subclass of Dictionary
        var systemDictClass = new SmalltalkClass();
        systemDictClass.Superclass = dictionaryClass;
        systemDictClass.MethodDictionary = new Dictionary<Symbol, CompiledMethod>();
        
        // Create its metaclass
        var systemDictMetaclass = new SmalltalkClass();
        systemDictMetaclass.Superclass = dictionaryClass.Metaclass;
        systemDictMetaclass.ThisClass = systemDictClass;
        systemDictMetaclass.ClassPointer = SystemDictionary.TryGetClass("Metaclass", out var mc) ? mc : null;
        
        systemDictClass.Metaclass = systemDictMetaclass;
        systemDictClass.ClassPointer = systemDictMetaclass;
        
        SystemDictionary.RegisterBootstrapClass("SystemDictionary", systemDictClass);
        
        return systemDictClass;
    }
    
    // ============================================
    // HELPERS
    // ============================================
    
    /// <summary>
    /// Install a method in a class.
    /// </summary>
    private static void InstallMethod(SmalltalkClass cls, string selector, CompiledMethod method)
    {
        cls.MethodDictionary[new Symbol(selector)] = method;
    }
    
    /// <summary>
    /// Add a class to the Smalltalk dictionary.
    /// </summary>
    private static void AddToSmalltalk(SmalltalkObject smalltalk, string className)
    {
        if (SystemDictionary.TryGetClass(className, out var cls))
        {
            AddToSmalltalkObject(smalltalk, className, cls);
        }
    }
    
    /// <summary>
    /// Add an object to the Smalltalk dictionary.
    /// </summary>
    private static void AddToSmalltalkObject(SmalltalkObject smalltalk, string key, SmalltalkObject value)
    {
        // Simplified dictionary add for bootstrap
        var array = (SmalltalkObject)smalltalk.Fields[DICT_ARRAY];
        var assocClass = SystemDictionary.TryGetClass("Association", out var ac) ? ac : null;
        
        // Create association
        var assoc = new SmalltalkObject(assocClass, 2);
        assoc.Fields[ASSOC_KEY] = new Symbol(key);
        assoc.Fields[ASSOC_VALUE] = value;
        
        // Find empty slot and add (simplified - no hashing)
        for (int i = 0; i < array.Fields.Length; i++)
        {
            if (array.Fields[i] == SystemDictionary.Nil)
            {
                array.Fields[i] = assoc;
                
                // Update tally
                var tally = (SmallInteger)smalltalk.Fields[0];
                smalltalk.Fields[0] = new SmallInteger(tally.Value + 1);
                break;
            }
        }
    }
}