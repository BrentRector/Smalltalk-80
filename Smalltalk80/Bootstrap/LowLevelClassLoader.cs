using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Loads classes from the complete system image JSON using low-level message sends.
/// This replaces the old JSON manifest approach with direct system image lookups.
/// </summary>
public class LowLevelClassLoader
{
    private readonly ISmalltalkVM vm;
    private readonly Dictionary<string, SystemImageClass> systemClasses = new();
    private readonly Dictionary<string, List<SystemImageMethod>> classMethods = new();
    
    /// <summary>
    /// Represents a class definition from the system image JSON.
    /// </summary>
    public class SystemImageClass
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";
    }
    
    /// <summary>
    /// Represents a method definition from the system image JSON.
    /// </summary>
    public class SystemImageMethod
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("bytecode")]
        public List<int> Bytecode { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("primitiveIndex")]
        public int PrimitiveIndex { get; set; } = 0;
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; } = 0;
        
        [JsonPropertyName("numTemps")]
        public int NumTemps { get; set; } = 0;
        
        [JsonPropertyName("isClassMethod")]
        public bool IsClassMethod { get; set; } = false;
    }
    
    /// <summary>
    /// System image data structure.
    /// </summary>
    public class SystemImageData
    {
        [JsonPropertyName("classes")]
        public List<SystemImageClass> Classes { get; set; } = new();
        
        [JsonPropertyName("methods")]
        public List<SystemImageMethod> Methods { get; set; } = new();
    }
    
    public LowLevelClassLoader(ISmalltalkVM vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Load the complete system image JSON and index classes and methods.
    /// </summary>
    public void LoadSystemImage(string jsonFilePath)
    {
        Console.WriteLine($"[LOWLEVEL] Loading system image from {jsonFilePath}");
        
        var jsonContent = File.ReadAllText(jsonFilePath);
        var systemData = JsonSerializer.Deserialize<SystemImageData>(jsonContent);
        
        if (systemData == null)
        {
            throw new InvalidOperationException("Failed to parse system image JSON");
        }
        
        // Index classes by name
        foreach (var classDef in systemData.Classes)
        {
            systemClasses[classDef.Name] = classDef;
        }
        
        // Index methods by class name
        foreach (var method in systemData.Methods)
        {
            if (!classMethods.ContainsKey(method.ClassName))
            {
                classMethods[method.ClassName] = new List<SystemImageMethod>();
            }
            classMethods[method.ClassName].Add(method);
        }
        
        Console.WriteLine($"[LOWLEVEL] Indexed {systemClasses.Count} classes and {systemData.Methods.Count} methods");
    }
    
    /// <summary>
    /// Create specified classes using low-level message sends.
    /// </summary>
    public void CreateClasses(List<string> classNames)
    {
        Console.WriteLine($"[LOWLEVEL] Creating {classNames.Count} classes via low-level message sends");
        
        // First, ensure the subclass: method exists on Class
        EnsureSubclassMethodExists();
        
        foreach (var className in classNames)
        {
            CreateClassFromSystemImage(className);
        }
    }
    
    /// <summary>
    /// Create a single class from the system image using low-level message sends.
    /// </summary>
    private void CreateClassFromSystemImage(string className)
    {
        if (!systemClasses.TryGetValue(className, out var classDef))
        {
            Console.WriteLine($"[LOWLEVEL] ✗ Class {className} not found in system image");
            return;
        }
        
        Console.WriteLine($"[LOWLEVEL] Creating class {className} < {classDef.Superclass}");
        
        // Step 1: Create the class via low-level message send and get the returned class OOP
        var createdClassOop = CreateClassViaMessageSend(classDef);
        Console.WriteLine($"[LOWLEVEL] Message send returned class OOP: {createdClassOop}");
        
        if (createdClassOop <= 0)
        {
            throw new InvalidOperationException($"Class creation failed: {className} - message send returned invalid OOP {createdClassOop}");
        }
        
        // Step 2: Use the returned class object directly for method installation
        Console.WriteLine($"[LOWLEVEL] Using returned class OOP {createdClassOop} for {className}");
        
        // Step 3: Install all methods for this class using the returned class OOP
        if (classMethods.TryGetValue(className, out var methods))
        {
            InstallMethodsForClass(className, methods, createdClassOop);
        }
        
        Console.WriteLine($"[LOWLEVEL] ✓ Successfully created class {className} at OOP {createdClassOop}");
    }
    
    private bool usePrimitiveFallback = false;
    
    /// <summary>
    /// Ensure the subclass: method exists on Class before we try to use it.
    /// </summary>
    private void EnsureSubclassMethodExists()
    {
        Console.WriteLine("[LOWLEVEL] Checking if subclass: method exists on Class...");
        
        // Try to understand the state of the VM
        Console.WriteLine("[LOWLEVEL] DEBUG: Testing VM state...");
        
        // Try getting nil and Smalltalk globals first
        var nilOop = vm.PrimitiveNil();
        Console.WriteLine($"[LOWLEVEL]   nil: OOP {nilOop}");
        
        var smalltalkOop = vm.PrimitiveLookupGlobal("Smalltalk");
        Console.WriteLine($"[LOWLEVEL]   Smalltalk dictionary: OOP {smalltalkOop}");
        
        // Try to debug the VM state more
        if (vm is VMPrimitiveAdapter vmAdapter)
        {
            // Can't access private field directly, but we can try other approaches
            Console.WriteLine($"[LOWLEVEL] DEBUG: VM adapter type confirmed: {vm.GetType().Name}");
        }
        
        // First, try looking up as a global (it should be there)
        var classClassOop = vm.PrimitiveLookupGlobal("Class");
        if (classClassOop < 0)
        {
            // Fallback to class lookup
            classClassOop = vm.PrimitiveLookupClass("Class");
        }
        
        if (classClassOop < 0)
        {
            Console.WriteLine("[LOWLEVEL] ERROR: Class class not found!");
            Console.WriteLine("[LOWLEVEL] Attempting to list available classes for debugging...");
            
            // Try to find what classes ARE available
            var knownClasses = new[] { "Object", "Behavior", "ClassDescription", "Class", "Metaclass" };
            foreach (var className in knownClasses)
            {
                var oop = vm.PrimitiveLookupGlobal(className);
                Console.WriteLine($"[LOWLEVEL]   {className}: OOP {oop}");
            }
            
            throw new InvalidOperationException("Class class not found - bootstrap incomplete");
        }
        
        // Try to load the subclass: method from the system image if available
        if (classMethods.TryGetValue("Class", out var classMethodsList))
        {
            var subclassMethod = classMethodsList.FirstOrDefault(m => 
                m.Selector == "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:");
                
            if (subclassMethod != null)
            {
                Console.WriteLine("[LOWLEVEL] Installing subclass: method on Class from system image");
                
                // Look up the Class class to install the method
                var classOopForMethod = vm.PrimitiveLookupClass("Class");
                if (classOopForMethod < 0)
                {
                    throw new InvalidOperationException("Cannot find Class class to install subclass: method");
                }
                
                InstallMethod("Class", subclassMethod, classOopForMethod);
                return;
            }
        }
        
        // If we can't find the method in the system image, we have a problem
        // The subclass: method is essential for class creation via message sends
        Console.WriteLine("[LOWLEVEL] WARNING: subclass: method not found in system image for Class");
        Console.WriteLine("[LOWLEVEL] Falling back to primitive-based class creation");
        usePrimitiveFallback = true;
    }
    
    /// <summary>
    /// Create a class using primitive operations when message sends are not available.
    /// </summary>
    private void CreateClassViaPrimitives(SystemImageClass classDef)
    {
        Console.WriteLine($"[LOWLEVEL] Creating {classDef.Name} via primitives (fallback mode)");
        
        // Look up superclass
        var superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop < 0)
        {
            throw new InvalidOperationException($"Superclass {classDef.Superclass} not found");
        }
        
        // Create the class object
        var classOop = vm.PrimitiveNewClass();
        
        // Set superclass (slot 0)
        vm.PrimitiveSetSlot(classOop, 0, superclassOop);
        
        // Create and set method dictionary (slot 1)
        var methodDictOop = vm.PrimitiveNewDictionary();
        vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
        
        // Set format (slot 2) - default format
        vm.PrimitiveSetSlot(classOop, 2, vm.PrimitiveNewSmallInteger(0));
        
        // Set instance variables (slot 3)
        var instVarArrayOop = vm.PrimitiveNewArray(classDef.InstanceVariables.Count);
        for (int i = 0; i < classDef.InstanceVariables.Count; i++)
        {
            var symbolOop = vm.PrimitiveInternSymbol(classDef.InstanceVariables[i]);
            vm.PrimitiveArrayAtPut(instVarArrayOop, i, symbolOop);
        }
        vm.PrimitiveSetSlot(classOop, 3, instVarArrayOop);
        
        // Set organization (slot 4) - can be nil for now
        vm.PrimitiveSetSlot(classOop, 4, vm.PrimitiveNil());
        
        // Set name (slot 5)
        var nameOop = vm.PrimitiveInternSymbol(classDef.Name);
        vm.PrimitiveSetSlot(classOop, 5, nameOop);
        
        // Create the metaclass
        var metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set metaclass's thisClass (slot 5) to point to the class
        vm.PrimitiveSetSlot(metaclassOop, 5, classOop);
        
        // Set class's class pointer to the metaclass
        vm.PrimitiveSetClass(classOop, metaclassOop);
        
        // Register the class globally
        vm.PrimitiveRegisterGlobal(classDef.Name, classOop);
        vm.PrimitiveRegisterGlobal(classDef.Name + "Class", metaclassOop);
        
        Console.WriteLine($"[LOWLEVEL] Created {classDef.Name} via primitives at OOP {classOop}");
    }
    
    /// <summary>
    /// Create a class using low-level subclass: message send.
    /// </summary>
    private int CreateClassViaMessageSend(SystemImageClass classDef)
    {
        // If subclass: method is not available, fall back to primitive creation
        if (usePrimitiveFallback)
        {
            CreateClassViaPrimitives(classDef);
            return vm.PrimitiveLookupClass(classDef.Name); // Return the class OOP
        }
        
        return CreateClassViaMessageSendInternal(classDef);
    }
    
    /// <summary>
    /// Internal implementation of class creation via message send.
    /// </summary>
    private int CreateClassViaMessageSendInternal(SystemImageClass classDef)
    {
        // Look up superclass
        var superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop < 0)
        {
            throw new InvalidOperationException($"Superclass {classDef.Superclass} not found");
        }
        
        // Build instance variable names string
        var instVarNames = string.Join(" ", classDef.InstanceVariables);
        
        // Build class variable names string  
        var classVarNames = string.Join(" ", classDef.ClassVariables);
        
        // Build pool dictionaries string
        var poolDictNames = string.Join(" ", classDef.PoolDictionaries);
        
        // Generate bytecode for: Object subclass: #ClassName instanceVariableNames: 'ivar1 ivar2' ...
        var subclassMessageSend = GenerateSubclassMessageSend(
            classDef.Superclass,
            classDef.Name,
            instVarNames,
            classVarNames,
            poolDictNames,
            classDef.Category
        );
        
        // Convert literals to OOPs for execution
        var literalOops = new int[subclassMessageSend.Literals.Length];
        for (int i = 0; i < subclassMessageSend.Literals.Length; i++)
        {
            var literal = subclassMessageSend.Literals[i];
            if (literal is Symbol sym)
            {
                literalOops[i] = vm.PrimitiveInternSymbol(sym.Value ?? sym.Name ?? "");
            }
            else if (literal is string str)
            {
                literalOops[i] = vm.PrimitiveInternString(str);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported literal type: {literal?.GetType()}");
            }
        }
        
        Console.WriteLine($"[LOWLEVEL] Sending subclass: message to {classDef.Superclass} to create {classDef.Name}");
        Console.WriteLine($"[LOWLEVEL] Bytecode: [{string.Join(", ", subclassMessageSend.Bytecodes.Select(b => $"0x{b:X2}"))}]");
        Console.WriteLine($"[LOWLEVEL] Literals: [{string.Join(", ", literalOops)}]");
        
        // Execute the message send with proper literals
        var result = vm.ExecuteBytecodeWithLiterals(superclassOop, subclassMessageSend.Bytecodes, new int[0], literalOops);
        
        if (result <= 0)
        {
            // Check if the class was actually created despite the return value
            var classOop = vm.PrimitiveLookupClass(classDef.Name);
            if (classOop > 0)
            {
                Console.WriteLine($"[LOWLEVEL] Warning: subclass: returned {result} but class {classDef.Name} exists at OOP {classOop}");
                return classOop; // Success despite odd return value
            }
            
            throw new InvalidOperationException($"Failed to create class {classDef.Name} via subclass: message send (returned {result})");
        }
        
        Console.WriteLine($"[LOWLEVEL] Successfully created class {classDef.Name} at OOP {result}");
        
        // CRITICAL: Register the created class in VM's global lookup table
        // This ensures PrimitiveLookupClass can find it for subsequent class creation
        vm.PrimitiveRegisterGlobal(classDef.Name, result);
        Console.WriteLine($"[LOWLEVEL] Registered {classDef.Name} globally for subsequent lookups");
        
        return result;
    }
    
    /// <summary>
    /// Generate bytecode for Object subclass:instanceVariableNames:... message send.
    /// </summary>
    private CompiledMethod GenerateSubclassMessageSend(
        string superclassName,
        string className,
        string instVarNames,
        string classVarNames,
        string poolDictNames,
        string category)
    {
        // Bytecode sequence for sending:
        // SuperClass subclass: #ClassName 
        //     instanceVariableNames: 'instVars'
        //     classVariableNames: 'classVars'
        //     poolDictionaries: 'pools'
        //     category: 'category'
        //
        // The receiver (superclass) is passed separately to ExecuteBytecodeWithLiterals,
        // so we only need to push the 5 arguments and send the message.
        
        var bytecode = new List<int>
        {
            // Push receiver (the superclass object passed to ExecuteBytecodeWithLiterals)
            0x70,     // pushReceiverVariable: 0 (self)
            
            // Push class name symbol
            0x20,     // pushLiteralConstant: 0 (#className)
            
            // Push instance variable names string
            0x21,     // pushLiteralConstant: 1 (instVarNames)
            
            // Push class variable names string  
            0x22,     // pushLiteralConstant: 2 (classVarNames)
            
            // Push pool dictionaries string
            0x23,     // pushLiteralConstant: 3 (poolDictNames)
            
            // Push category string
            0x24,     // pushLiteralConstant: 4 (category)
            
            // Send subclass:instanceVariableNames:classVariableNames:poolDictionaries:category: with 5 args
            // Use single extended send bytecode (0xE1) followed by arg count and literal index
            0xE1,     // singleExtendedSend
            0x15,     // 5 arguments (high 3 bits = 001), literal 5 (low 5 bits = 00101)
            
            0x7C      // returnTop
        };
        
        // Note: Literals array contains the actual values that will be converted to OOPs
        var literals = new object[]
        {
            new Symbol(className),           // 0: Class name symbol
            instVarNames,                    // 1: Instance variable names string
            classVarNames,                   // 2: Class variable names string
            poolDictNames,                   // 3: Pool dictionaries string
            category,                        // 4: Category string
            new Symbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:") // 5: Selector
        };
        
        // Create method header (0 args, 0 temps)
        var rawHeader = 0; // No primitive, no large context, 0 temps
        var header = new MethodHeader(rawHeader, 0);
        
        return new CompiledMethod
        {
            Bytecodes = bytecode.Select(b => (byte)b).ToArray(),
            Literals = literals,
            Header = header,
            TemporariesCount = 0
        };
    }
    
    /// <summary>
    /// Install all methods for a class using CompiledMethod construction.
    /// </summary>
    private void InstallMethodsForClass(string className, List<SystemImageMethod> methods, int classOop)
    {
        Console.WriteLine($"[LOWLEVEL] Installing {methods.Count} methods for {className} (OOP {classOop})");
        
        foreach (var method in methods)
        {
            try
            {
                InstallMethod(className, method, classOop);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"CRITICAL: Failed to install method {className}>>{method.Selector}: {ex.Message}. Low-level installation must be fixed before proceeding.", ex);
            }
        }
    }
    
    /// <summary>
    /// Install a single method using CompiledMethod construction and methodDict at:put:.
    /// </summary>
    private void InstallMethod(string className, SystemImageMethod method, int classOop)
    {
        // Create CompiledMethod from bytecode and literals
        var compiledMethod = CreateCompiledMethod(method);
        
        // Use the provided class OOP directly
        var targetClassOop = classOop;
        if (method.IsClassMethod)
        {
            // For class methods, we need the metaclass
            // TODO: Need to implement metaclass lookup
            targetClassOop = vm.GetSlot(classOop, 0); // Get metaclass from class
        }
            
        if (targetClassOop <= 0)
        {
            throw new InvalidOperationException($"Invalid target class OOP for {className}: {targetClassOop}");
        }
        
        Console.WriteLine($"[LOWLEVEL] Installing method {className}>>{method.Selector} on {(method.IsClassMethod ? "metaclass" : "class")} OOP {targetClassOop}");
        
        // Install method via: targetClass methodDict at: #selector put: compiledMethod
        InstallMethodViaMessageSend(targetClassOop, method.Selector, compiledMethod);
    }
    
    /// <summary>
    /// Create CompiledMethod from system image method data.
    /// </summary>
    private CompiledMethod CreateCompiledMethod(SystemImageMethod method)
    {
        // Convert literals to proper objects
        var literalObjects = new object[method.Literals.Count];
        for (int i = 0; i < method.Literals.Count; i++)
        {
            literalObjects[i] = new Symbol(method.Literals[i]);
        }
        
        // Create method header
        var rawHeader = method.PrimitiveIndex | (0 << 9) | (method.NumTemps << 10); // primitive | largeContext | tempCount
        var header = new MethodHeader(rawHeader, method.NumArgs);
        
        return new CompiledMethod
        {
            Bytecodes = method.Bytecode.Select(b => (byte)b).ToArray(),
            Literals = literalObjects,
            Header = header,
            TemporariesCount = method.NumTemps
        };
    }
    
    /// <summary>
    /// Install method via low-level message send: class methodDict at:put:.
    /// </summary>
    private void InstallMethodViaMessageSend(int classOop, string selector, CompiledMethod compiledMethod)
    {
        // Generate bytecode for: class methodDict at: #selector put: compiledMethod
        var installBytecode = new List<int>
        {
            // Push class
            131, 32,  // pushLiteralVariable: class
            
            // Send methodDict
            208,      // send: #methodDict (unary)
            
            // Push selector symbol
            33,       // pushLiteralConstant: #selector
            
            // Push compiled method
            34,       // pushLiteralConstant: compiledMethod
            
            // Send at:put:
            131, 35,  // send: #at:put:
            
            124       // returnTop
        };
        
        var literals = new object[]
        {
            classOop,
            new Symbol(selector),
            compiledMethod,
            new Symbol("at:put:")
        };
        
        var rawHeader = 0; // No primitive, no large context, 0 temps
        var header = new MethodHeader(rawHeader, 0);
        var installMethod = new CompiledMethod
        {
            Bytecodes = installBytecode.Select(b => (byte)b).ToArray(),
            Literals = literals,
            Header = header,
            TemporariesCount = 0
        };
        
        vm.ExecuteBytecodeWithLiterals(classOop, installMethod.Bytecode, new int[0], new int[0]);
    }
}