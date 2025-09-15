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
        
        foreach (var className in classNames)
        {
            try
            {
                CreateClassFromSystemImage(className);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOWLEVEL] ✗ Failed to create class {className}: {ex.Message}");
            }
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
        
        // Step 1: Create the class via low-level message send
        CreateClassViaMessageSend(classDef);
        
        // Step 2: Install all methods for this class
        if (classMethods.TryGetValue(className, out var methods))
        {
            InstallMethodsForClass(className, methods);
        }
        
        Console.WriteLine($"[LOWLEVEL] ✓ Successfully created class {className}");
    }
    
    /// <summary>
    /// Create a class using low-level subclass: message send.
    /// </summary>
    private void CreateClassViaMessageSend(SystemImageClass classDef)
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
        
        // Execute the message send
        var result = vm.ExecuteBytecodeWithLiterals(superclassOop, subclassMessageSend.Bytecode, new int[0], new int[0]);
        
        if (result <= 0)
        {
            throw new InvalidOperationException($"Failed to create class {classDef.Name}");
        }
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
        // This follows the pattern from Phase2Executor.CreateTestCounterViaBytecode()
        
        // Bytecode sequence for:
        // Object subclass: #ClassName 
        //     instanceVariableNames: 'instVars'
        //     classVariableNames: 'classVars'
        //     poolDictionaries: 'pools'
        //     category: 'category'
        
        var bytecode = new List<int>
        {
            // Push superclass (global lookup)
            131, 32,  // pushLiteralVariable: superclassName
            
            // Push class name symbol
            32,       // pushLiteralConstant: #className
            
            // Push instance variable names string
            33,       // pushLiteralConstant: instVarNames
            
            // Push class variable names string  
            34,       // pushLiteralConstant: classVarNames
            
            // Push pool dictionaries string
            35,       // pushLiteralConstant: poolDictNames
            
            // Push category string
            36,       // pushLiteralConstant: category
            
            // Send subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
            131, 37,  // send: #subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
            
            124       // returnTop
        };
        
        var literals = new object[]
        {
            new Symbol(superclassName),
            new Symbol(className),
            instVarNames,
            classVarNames,
            poolDictNames,
            category,
            new Symbol("subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:")
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
    private void InstallMethodsForClass(string className, List<SystemImageMethod> methods)
    {
        Console.WriteLine($"[LOWLEVEL] Installing {methods.Count} methods for {className}");
        
        foreach (var method in methods)
        {
            try
            {
                InstallMethod(className, method);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOWLEVEL] ✗ Failed to install method {className}>>{method.Selector}: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Install a single method using CompiledMethod construction and methodDict at:put:.
    /// </summary>
    private void InstallMethod(string className, SystemImageMethod method)
    {
        // Create CompiledMethod from bytecode and literals
        var compiledMethod = CreateCompiledMethod(method);
        
        // Get target class or metaclass
        var targetClassOop = vm.PrimitiveLookupClass(className);
        if (method.IsClassMethod)
        {
            // For class methods, we need the metaclass
            // TODO: Need to implement metaclass lookup
            targetClassOop = vm.GetSlot(targetClassOop, 0); // Temporary workaround
        }
            
        if (targetClassOop < 0)
        {
            throw new InvalidOperationException($"Target class not found: {className}");
        }
        
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