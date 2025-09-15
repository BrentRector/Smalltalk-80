using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Executes bootstrap manifests by sending messages through the minimal Smalltalk system.
/// </summary>
public class Phase2Executor
{
    private readonly ISmalltalkVM vm;
    
    /// <summary>
    /// No-op logging method (debug logging removed).
    /// </summary>
    public static void Phase2Log(string message)
    {
        // Debug logging removed
    }
    
    public Phase2Executor(ISmalltalkVM vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Class definition from manifest.
    /// </summary>
    public class ClassDef
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "Object";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unclassified";
    }
    
    /// <summary>
    /// Method definition from manifest.
    /// </summary>
    public class MethodDef
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("selector")]
        public string Selector { get; set; } = "";
        
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        
        [JsonPropertyName("bytecodes")]
        public List<byte> Bytecodes { get; set; } = new();
        
        [JsonPropertyName("literals")]
        public List<string> Literals { get; set; } = new();
        
        [JsonPropertyName("numArgs")]
        public int NumArgs { get; set; }
        
        [JsonPropertyName("primitive")]
        public int Primitive { get; set; } = 0;
    }
    
    
    /// <summary>
    /// bootstrap manifest structure (legacy multi-class format).
    /// </summary>
    public class Phase2Manifest
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "3";
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        
        [JsonPropertyName("classes")]
        public List<ClassDef> Classes { get; set; } = new();
        
        [JsonPropertyName("methods")]
        public List<MethodDef> Methods { get; set; } = new();
        
    }
    
    /// <summary>
    /// bootstrap single class manifest structure.
    /// </summary>
    public class Phase2ClassManifest
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = "3";
        
        [JsonPropertyName("className")]
        public string ClassName { get; set; } = "";
        
        [JsonPropertyName("superclass")]
        public string Superclass { get; set; } = "Object";
        
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "";
        
        [JsonPropertyName("instanceVariables")]
        public List<string> InstanceVariables { get; set; } = new();
        
        [JsonPropertyName("classVariables")]
        public List<string> ClassVariables { get; set; } = new();
        
        [JsonPropertyName("poolDictionaries")]
        public List<string> PoolDictionaries { get; set; } = new();
        
        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unclassified";
        
        [JsonPropertyName("methods")]
        public List<MethodDef> Methods { get; set; } = new();
        
        [JsonPropertyName("classMethods")]
        public List<MethodDef> ClassMethods { get; set; } = new();
    }
    
    /// <summary>
    /// Execute a bootstrap manifest.
    /// </summary>
    public void ExecuteManifest(string jsonContent)
    {
        // Check if this is a sequence-based manifest (like phase3_test_counter.json)
        if (jsonContent.Contains("\"sequence\""))
        {
            ExecuteSequenceManifest(jsonContent);
            return;
        }
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var manifest = JsonSerializer.Deserialize<Phase2Manifest>(jsonContent, options);
        if (manifest == null)
        {
            throw new InvalidOperationException("Failed to parse bootstrap manifest");
        }
        
        Phase2Log($"[BOOTSTRAP] Executing: {manifest.Description}");
        
        // Step 1: Create classes
        foreach (var classDef in manifest.Classes)
        {
            CreateClass(classDef);
        }
        
        // Step 2: Install methods
        foreach (var methodDef in manifest.Methods)
        {
            InstallMethod(methodDef);
        }
        
        
        Phase2Log("[BOOTSTRAP] Manifest execution complete");
    }
    
    /// <summary>
    /// Execute a sequence-based bootstrap manifest (like phase3_test_counter.json).
    /// This format uses individual message sends to build classes step by step.
    /// </summary>
    private void ExecuteSequenceManifest(string jsonContent)
    {
        Console.WriteLine("[BOOTSTRAP] Executing sequence-based manifest via bytecode...");
        
        // Sequence-based manifest execution not implemented
    }
    
    
    /// <summary>
    /// Get or create a Symbol object.
    /// </summary>
    private Symbol GetOrCreateSymbol(string value)
    {
        // Create new symbol directly - we need the actual Symbol object for literals
        return new Symbol(value);
    }
    
    /// <summary>
    /// Create a class from definition.
    /// </summary>
    private void CreateClass(ClassDef classDef)
    {
        Phase2Log($"[BOOTSTRAP] Creating class {classDef.Name} < {classDef.Superclass}");
        
        // Look up superclass
        var superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
        if (superclassOop < 0)
        {
            Phase2Log($"[BOOTSTRAP] Warning: Superclass {classDef.Superclass} not found");
            return;
        }
        
        // Create the class
        var classOop = vm.PrimitiveNewClass();
        
        // Create the metaclass
        var metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set up class relationships
        vm.PrimitiveSetClass(classOop, metaclassOop);
        
        // The metaclass itself needs to have Metaclass as its class
        var metaclassClassOop = vm.PrimitiveLookupClass("Metaclass");
        if (metaclassClassOop > 0)
        {
            vm.PrimitiveSetClass(metaclassOop, metaclassClassOop);
        }
        
        // Set superclass (slot 0 in Behavior)
        vm.PrimitiveSetSlot(classOop, 0, superclassOop);
        
        // Create and set instance variables array
        if (classDef.InstanceVariables.Count > 0)
        {
            var instVarsArray = vm.PrimitiveNewArray(classDef.InstanceVariables.Count);
            for (int i = 0; i < classDef.InstanceVariables.Count; i++)
            {
                var symbolOop = vm.PrimitiveInternSymbol(classDef.InstanceVariables[i]);
                vm.PrimitiveArrayAtPut(instVarsArray, i, symbolOop);
            }
            // Instance variables are in slot 3 of ClassDescription
            vm.PrimitiveSetSlot(classOop, 3, instVarsArray);
        }
        
        // Set class name (slot 5 in Class)
        var nameSymbol = vm.PrimitiveInternSymbol(classDef.Name);
        vm.PrimitiveSetSlot(classOop, 5, nameSymbol);
        
        // Register the class globally
        vm.PrimitiveRegisterGlobal(classDef.Name, classOop);
        
        Phase2Log($"[BOOTSTRAP] Created class {classDef.Name} -> OOP {classOop}");
    }
    
    /// <summary>
    /// Install a method in a class following the 5-step protocol.
    /// </summary>
    private void InstallMethod(MethodDef methodDef)
    {
        Phase2Log($"[BOOTSTRAP] Installing {methodDef.ClassName}>>{methodDef.Selector}");
        
        // Step 1: Look up the target class
        var classOop = vm.PrimitiveLookupClass(methodDef.ClassName);
        if (classOop < 0)
        {
            Phase2Log($"[BOOTSTRAP] Warning: Class {methodDef.ClassName} not found");
            return;
        }
        
        // Step 2: Ensure the selector is properly interned as a Symbol
        var selectorSymbol = vm.PrimitiveInternSymbol(methodDef.Selector);
        Phase2Log($"[BOOTSTRAP]   - Selector '{methodDef.Selector}' interned as Symbol OOP {selectorSymbol}");
        
        // Step 3: Create the CompiledMethod object with proper setup
        var compiledMethodClass = vm.PrimitiveLookupClass("CompiledMethod");
        if (compiledMethodClass < 0)
        {
            Phase2Log("[BOOTSTRAP] Warning: CompiledMethod class not found, cannot install method");
            return;
        }
        
        var methodOop = vm.PrimitiveNewInstance(compiledMethodClass);
        Phase2Log($"[BOOTSTRAP]   - Created CompiledMethod instance OOP {methodOop}");
        
        // CompiledMethod has 4 fields: selector, bytecode, literals, header
        // Set selector (slot 0)
        vm.PrimitiveSetSlot(methodOop, 0, selectorSymbol);
        
        // Set bytecodes (slot 1) - create ByteArray or regular array with bytes
        if (methodDef.Bytecodes.Count > 0)
        {
            // Store bytecodes as an array where each element contains the byte value
            var bytecodeArray = vm.PrimitiveNewArray(methodDef.Bytecodes.Count);
            for (int i = 0; i < methodDef.Bytecodes.Count; i++)
            {
                // Create a small integer object for each byte
                var byteObj = vm.PrimitiveNewSmallInteger(methodDef.Bytecodes[i]);
                vm.PrimitiveArrayAtPut(bytecodeArray, i, byteObj);
            }
            vm.PrimitiveSetSlot(methodOop, 1, bytecodeArray);
            Phase2Log($"[BOOTSTRAP]   - Set bytecodes ({methodDef.Bytecodes.Count} bytes): [{string.Join(", ", methodDef.Bytecodes)}]");
        }
        else
        {
            // No bytecodes - set empty array
            var emptyArray = vm.PrimitiveNewArray(0);
            vm.PrimitiveSetSlot(methodOop, 1, emptyArray);
        }
        
        // Set literals (slot 2)
        var literalsArray = vm.PrimitiveNewArray(methodDef.Literals.Count);
        for (int i = 0; i < methodDef.Literals.Count; i++)
        {
            var literal = methodDef.Literals[i];
            int literalOop;
            
            if (literal.StartsWith("#"))
            {
                // Symbol literal
                literalOop = vm.PrimitiveInternSymbol(literal.Substring(1));
            }
            else if (int.TryParse(literal, out int intValue))
            {
                // Integer literal - store as string for now
                // TODO: Create proper SmallInteger objects
                literalOop = vm.PrimitiveInternString(literal);
            }
            else
            {
                // String literal
                literalOop = vm.PrimitiveInternString(literal);
            }
            
            vm.PrimitiveArrayAtPut(literalsArray, i, literalOop);
        }
        vm.PrimitiveSetSlot(methodOop, 2, literalsArray);
        Phase2Log($"[BOOTSTRAP]   - Set literals ({methodDef.Literals.Count} literals)");
        
        // Set header (slot 3) - Blue Book encoding (16-bit word):
        // Bits 0-8: Primitive number (0 if no primitive)
        // Bit 9: Large context flag (1 if > 16 temps)
        // Bits 10-15: Number of temporaries/stack size
        
        int primitiveNum = methodDef.Primitive & 0x1FF; // 9 bits for primitive
        int largeContext = 0; // We don't have large contexts yet
        int tempCount = methodDef.NumArgs; // For now, temps = args
        
        // Blue Book header encoding
        int headerValue = primitiveNum | (largeContext << 9) | (tempCount << 10);
        
        var headerOop = vm.PrimitiveNewSmallInteger(headerValue);
        vm.PrimitiveSetSlot(methodOop, 3, headerOop);
        
        // Format the header for display
        var headerDesc = FormatBlueBookHeader(headerValue);
        Phase2Log($"[BOOTSTRAP]   - Set header: {headerDesc}");
        
        // Step 4: Retrieve or create the class's method dictionary using message sends
        var methodDictOop = vm.GetSlot(classOop, 1);
        
        if (methodDictOop < 0)
        {
            // Create a new method dictionary via "MethodDictionary new"
            var methodDictClass = vm.PrimitiveLookupClass("MethodDictionary");
            if (methodDictClass > 0)
            {
                var newSelector = vm.PrimitiveInternSymbol("new");
                methodDictOop = vm.PrimitiveSendMessage(methodDictClass, newSelector, new int[0]);
                Phase2Log($"[BOOTSTRAP]   - Created MethodDictionary via message send: MethodDictionary new -> OOP {methodDictOop}");
            }
            else
            {
                // Fallback: use simple dictionary
                methodDictOop = vm.PrimitiveNewDictionary();
                Phase2Log($"[BOOTSTRAP]   - Created simple dictionary OOP {methodDictOop}");
            }
            
            // Install the dictionary in the class (slot 1 of Behavior)
            vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
            Phase2Log($"[BOOTSTRAP]   - Installed method dictionary in class slot 1");
        }
        else
        {
            Phase2Log($"[BOOTSTRAP]   - Using existing method dictionary OOP {methodDictOop}");
        }
        
        // Step 5: Install the method directly using primitives (bypassing broken at:put: method)
        // Since Phase 1 now creates properly sized dictionaries, we can use direct primitive storage
        try
        {
            vm.PrimitiveDictionaryAtPut(methodDictOop, selectorSymbol, methodOop);
            Phase2Log($"[BOOTSTRAP]   - Installed method via primitive: dict[#{methodDef.Selector}] = method OOP {methodOop}");
        }
        catch (Exception ex)
        {
            // Fallback to message send if primitive fails
            Phase2Log($"[BOOTSTRAP]   - Primitive failed ({ex.Message}), falling back to message send");
            var atPutSelector = vm.PrimitiveInternSymbol("at:put:");
            var result = vm.PrimitiveSendMessage(methodDictOop, atPutSelector, new int[] { selectorSymbol, methodOop });
            Phase2Log($"[BOOTSTRAP]   - Fallback to message send: dict at: #{methodDef.Selector} put: method -> result OOP {result}");
        }
        
        Phase2Log($"[BOOTSTRAP] ✓ Method {methodDef.ClassName}>>{methodDef.Selector} successfully installed");
    }
    
    
    
    /// <summary>
    /// Formats a Blue Book method header for display.
    /// </summary>
    private string FormatBlueBookHeader(int headerValue)
    {
        // Decode Blue Book header format (16-bit word):
        // Bits 0-8: Primitive number (0 if no primitive)
        // Bit 9: Large context flag
        // Bits 10-15: Number of temporaries
        
        int primitiveNum = headerValue & 0x1FF;  // Lower 9 bits
        bool largeContext = (headerValue & 0x200) != 0;  // Bit 9
        int tempCount = (headerValue >> 10) & 0x3F;  // Bits 10-15
        
        var parts = new List<string>();
        
        if (primitiveNum > 0)
        {
            parts.Add($"primitive={primitiveNum}");
        }
        
        parts.Add($"temps={tempCount}");
        
        if (largeContext)
        {
            parts.Add("largeContext");
        }
        
        return $"0x{headerValue:X4} ({string.Join(", ", parts)})";
    }
    
    
    /// <summary>
    /// Execute a bootstrap single class manifest.
    /// </summary>
    public void ExecuteClassManifest(string jsonContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var manifest = JsonSerializer.Deserialize<Phase2ClassManifest>(jsonContent, options);
        if (manifest == null)
        {
            throw new InvalidOperationException("Failed to parse bootstrap class manifest");
        }
        
        Phase2Log($"[BOOTSTRAP] Loading class: {manifest.ClassName}");
        
        // Create the class definition
        var classDef = new ClassDef
        {
            Name = manifest.ClassName,
            Superclass = manifest.Superclass,
            InstanceVariables = manifest.InstanceVariables,
            ClassVariables = manifest.ClassVariables,
            PoolDictionaries = manifest.PoolDictionaries,
            Category = manifest.Category
        };
        
        // Step 1: Create the class (only if it doesn't already exist)
        var existingClass = vm.PrimitiveLookupClass(manifest.ClassName);
        if (existingClass < 0)
        {
            CreateClass(classDef);
        }
        else
        {
            Phase2Log($"[BOOTSTRAP] Class {manifest.ClassName} already exists, adding methods only");
        }
        
        // Step 1.5: Method dictionary sizing is now handled in Phase 1
        // No longer needed - Phase 1 creates properly sized dictionaries
        // Workaround disabled since Phase 1 now pre-sizes dictionaries correctly
        
        // Step 2: Install instance methods
        foreach (var methodDef in manifest.Methods)
        {
            methodDef.ClassName = manifest.ClassName;
            InstallMethod(methodDef);
        }
        
        // Step 3: Install class methods (on the metaclass)
        foreach (var methodDef in manifest.ClassMethods)
        {
            methodDef.ClassName = manifest.ClassName + " class";
            InstallMethod(methodDef);
        }
        
        Phase2Log($"[BOOTSTRAP] Class {manifest.ClassName} loading complete");
    }
    
    /// <summary>
    /// Workaround: Ensures a class has a large enough method dictionary to hold new methods.
    /// Replaces small/full dictionaries with appropriately sized new ones.
    /// </summary>
    private void EnsureLargeMethodDictionary(int classOop, string className, int expectedMethodCount)
    {
        Phase2Log($"[BOOTSTRAP] Ensuring large method dictionary for {className} (expecting {expectedMethodCount} methods)");
        
        // Get the current method dictionary (slot 1 in Behavior)
        var currentMethodDict = vm.GetSlot(classOop, 1);
        
        // Calculate needed capacity: existing methods + new methods + 50% growth buffer
        int neededCapacity = expectedMethodCount + 10; // Add buffer for growth
        
        // Always create a new, larger dictionary to avoid the broken at:put: issue
        Phase2Log($"[BOOTSTRAP]   - Creating new method dictionary with capacity {neededCapacity}");
        
        // Create a new method dictionary using primitives (avoiding the broken MethodDictionary new)
        var newMethodDict = vm.PrimitiveNewDictionary();
        
        // If there was an existing method dictionary, copy its methods to the new one
        if (currentMethodDict > 0)
        {
            Phase2Log($"[BOOTSTRAP]   - Found existing method dictionary OOP {currentMethodDict}, copying methods");
            CopyMethodsToNewDictionary(currentMethodDict, newMethodDict, className);
        }
        else
        {
            Phase2Log($"[BOOTSTRAP]   - No existing method dictionary found, starting with empty dictionary");
        }
        
        // Install the new dictionary in the class
        vm.PrimitiveSetSlot(classOop, 1, newMethodDict);
        Phase2Log($"[BOOTSTRAP]   - Installed new method dictionary OOP {newMethodDict} in class {className}");
    }
    
    /// <summary>
    /// Copies methods from an old method dictionary to a new one.
    /// Uses primitive operations to avoid the broken at:put: implementation.
    /// </summary>
    private void CopyMethodsToNewDictionary(int oldDict, int newDict, string className)
    {
        // This is a simplified copy - in a full implementation we'd need to properly
        // traverse the dictionary structure and extract key-value pairs.
        // For now, we'll just note that copying is happening
        Phase2Log($"[BOOTSTRAP]     - Method copying from old dictionary not yet implemented");
        Phase2Log($"[BOOTSTRAP]     - Existing methods in {className} may need to be re-added manually");
        
        // TODO: Implement proper method dictionary traversal and copying
        // This would require understanding the exact dictionary format used by bootstrap
        // and extracting selector->method mappings to re-install them
    }
}