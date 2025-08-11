using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Executes bootstrap operations from JSON configuration files.
/// </summary>
public class JsonBootstrapExecutor
{
    private readonly ISmalltalkVM vm;
    private readonly Dictionary<string, int> namedObjects = new();
    
    /// <summary>
    /// Controls whether bootstrap debug output is displayed.
    /// </summary>
    public static bool EnableBootstrapDebugOutput { get; set; } = false;
    
    /// <summary>
    /// Logs a bootstrap message only if debug output is enabled.
    /// </summary>
    public static void BootstrapLog(string message)
    {
        if (EnableBootstrapDebugOutput)
        {
            Console.WriteLine(message);
        }
    }
    
    public JsonBootstrapExecutor(ISmalltalkVM vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Execute Phase 1 bootstrap from JSON content.
    /// </summary>
    public void ExecutePhase1(string jsonContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var bootstrap = JsonSerializer.Deserialize<BootstrapConfig>(jsonContent, options);
        if (bootstrap == null)
            throw new InvalidOperationException("Failed to parse bootstrap JSON");
            
        BootstrapLog($"[BOOTSTRAP] Executing {bootstrap.Operations?.Count ?? 0} operations from {bootstrap.Description}");
        
        if (bootstrap.Operations == null)
            return;
            
        foreach (var operation in bootstrap.Operations)
        {
            ExecuteOperation(operation);
        }
        
        BootstrapLog($"[BOOTSTRAP] Phase 1 created {namedObjects.Count} named objects");
        
        // Don't migrate yet - wait until after Phase 2/3 when at:put: method is available
        // This avoids the dictionary expansion problem
        BootstrapLog("[BOOTSTRAP] Phase 1 complete - will migrate to Smalltalk dictionary after methods are installed");
    }
    
    /// <summary>
    /// Execute unified bootstrap from JSON content (consolidated Phase 1+2).
    /// </summary>
    public void ExecuteUnifiedBootstrap(string jsonContent)
    {
        // This is the main bootstrap method that handles all classes with methods
        ExecuteBootstrap(jsonContent);
    }
    
    /// <summary>
    /// Execute bootstrap from JSON content (handles classes with methods).
    /// </summary>
    public void ExecuteBootstrap(string jsonContent)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var phase2 = JsonSerializer.Deserialize<BootstrapManifestConfig>(jsonContent, options);
        if (phase2 == null)
            throw new InvalidOperationException("Failed to parse bootstrap JSON");
            
        BootstrapLog($"[BOOTSTRAP] Bootstrap: {phase2.Description}");
        
        // IMPORTANT: Create all classes first WITHOUT installing methods
        // This ensures CompiledMethod class exists before any methods are installed
        if (phase2.Classes != null)
        {
            BootstrapLog($"[BOOTSTRAP] Pass 1: Creating {phase2.Classes.Count} class structures");
            foreach (var classDef in phase2.Classes)
            {
                CreateClassStructure(classDef);
            }
            
            // Now create objects (instances of classes) after classes exist
            if (phase2.Objects != null)
            {
                BootstrapLog($"[BOOTSTRAP] Creating {phase2.Objects.Count} objects");
                foreach (var objDef in phase2.Objects)
                {
                    CreateBootstrapObject(objDef);
                }
            }
            
            // Now install all methods in a second pass
            BootstrapLog($"[BOOTSTRAP] Pass 2: Installing methods for all classes");
            foreach (var classDef in phase2.Classes)
            {
                InstallClassMethods(classDef);
            }
        }
        
        // Finally, apply patches to wire everything together
        if (phase2.Patches != null)
        {
            BootstrapLog($"[BOOTSTRAP] Applying {phase2.Patches.Count} patches");
            foreach (var patch in phase2.Patches)
            {
                ApplyBootstrapPatch(patch);
            }
        }
        
        BootstrapLog($"[BOOTSTRAP] Bootstrap complete: Created messaging infrastructure");
    }
    
    /// <summary>
    /// Execute Phase 3 bootstrap from JSON content.
    /// </summary>
    public void ExecutePhase3(string jsonContent)
    {
        // Phase 3 uses a different executor for message-driven bootstrap
        var phase2Executor = new Phase2Executor(vm);
        phase2Executor.ExecuteManifest(jsonContent);
    }
    
    /// <summary>
    /// Execute a single bootstrap operation.
    /// </summary>
    private void ExecuteOperation(BootstrapOperation operation)
    {
        if (!string.IsNullOrEmpty(operation.Comment))
        {
            BootstrapLog($"[BOOTSTRAP] {operation.Comment}");
            return;
        }
        
        switch (operation.Op?.ToLower())
        {
            case "createobject":
                ExecuteCreateObject(operation);
                break;
                
            case "patchpointer":
                ExecutePatchPointer(operation);
                break;
                
            case "setsuperclass":
                ExecuteSetSuperclass(operation);
                break;
                
            case "registerglobal":
                ExecuteRegisterGlobal(operation);
                break;
                
            case "createsymbol":
                ExecuteCreateSymbol(operation);
                break;
                
            case "createstring":
                ExecuteCreateString(operation);
                break;
                
            case "createarray":
                ExecuteCreateArray(operation);
                break;
                
            case "createdictionary":
                ExecuteCreateDictionary(operation);
                break;
                
            case "installmethod":
                ExecuteInstallMethod(operation);
                break;
                
            case "setinstancevariables":
                ExecuteSetInstanceVariables(operation);
                break;
                
            case "setslot":
                ExecuteSetSlot(operation);
                break;
                
            case "setcategory":
                ExecuteSetCategory(operation);
                break;
                
            case "definemethod":
                ExecuteDefineMethod(operation);
                break;
                
            case "sendmessage":
                ExecuteSendMessage(operation);
                break;
                
            case "assert":
                ExecuteAssert(operation);
                break;
                
            case "createinstance":
                ExecuteCreateInstance(operation);
                break;
                
            case "createlargeinstance":
                ExecuteCreateLargeInstance(operation);
                break;
                
            case "dictionaryatput":
                ExecuteDictionaryAtPut(operation);
                break;
                
            case "createmethoddictionary":
                ExecuteCreateMethodDictionary(operation);
                break;
                
            default:
                if (!string.IsNullOrEmpty(operation.Op))
                    BootstrapLog($"[BOOTSTRAP] Warning: Unknown operation '{operation.Op}'");
                break;
        }
    }
    
    /// <summary>
    /// Execute createObject operation.
    /// </summary>
    private void ExecuteCreateObject(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result))
            throw new InvalidOperationException("createObject requires a result name");
            
        int oop;
        
        // Determine what type of object to create based on the name
        // Naming convention: FooClass is the metaclass for class Foo
        // But "Class" and "Metaclass" themselves are regular classes, not metaclasses!
        if (op.Result.EndsWith("Class") && op.Result != "Class" && op.Result != "Metaclass")
        {
            // It's a metaclass
            oop = vm.PrimitiveNewMetaclass();
            BootstrapLog($"[BOOTSTRAP] Created metaclass '{op.Result}' -> OOP {oop}");
        }
        else if (op.Result == "nil")
        {
            // Special case for nil
            oop = vm.PrimitiveNil();
            BootstrapLog($"[BOOTSTRAP] Created nil -> OOP {oop}");
        }
        else
        {
            // It's a regular class object
            oop = vm.PrimitiveNewClass();
            BootstrapLog($"[BOOTSTRAP] Created class '{op.Result}' -> OOP {oop}");
        }
        
        namedObjects[op.Result] = oop;
        
        // Register as global if it's a class
        if (op.Result != "nil")
        {
            vm.PrimitiveRegisterGlobal(op.Result, oop);
        }
        
    }
    
    /// <summary>
    /// Execute patchPointer operation.
    /// </summary>
    private void ExecutePatchPointer(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || string.IsNullOrEmpty(op.Slot))
            throw new InvalidOperationException("patchPointer requires target and slot");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Target object '{op.Target}' not found");
            
        int valueOop = 0;
        string? valueStr = null;
        
        if (op.Value == null)
        {
            // Value is null, keep valueOop as 0
        }
        else if (op.Value is string str)
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        
        if (!string.IsNullOrEmpty(valueStr))
        {
            if (!namedObjects.TryGetValue(valueStr, out valueOop))
            {
                throw new InvalidOperationException($"Value object '{valueStr}' not found");
            }
        }
        
        // Handle special slot names
        if (op.Slot == "class")
        {
            vm.PrimitiveSetClass(targetOop, valueOop);
            BootstrapLog($"[BOOTSTRAP] Set class of '{op.Target}' (OOP {targetOop}) to '{op.Value}' (OOP {valueOop})");
        }
        else if (int.TryParse(op.Slot, out var slotIndex))
        {
            vm.PrimitiveSetSlot(targetOop, slotIndex, valueOop);
            BootstrapLog($"[BOOTSTRAP] Set slot {slotIndex} of '{op.Target}' to '{op.Value}'");
        }
        else
        {
            BootstrapLog($"[BOOTSTRAP] Warning: Unknown slot type '{op.Slot}'");
        }
    }
    
    /// <summary>
    /// Execute setSuperclass operation.
    /// </summary>
    private void ExecuteSetSuperclass(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target))
            throw new InvalidOperationException("setSuperclass requires a target");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Target object '{op.Target}' not found");
            
        int superclassOop = 0;
        string? valueStr = null;
        
        if (op.Value is string str && str != "null")
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        
        if (!string.IsNullOrEmpty(valueStr) && valueStr != "null")
        {
            if (!namedObjects.TryGetValue(valueStr, out superclassOop))
                throw new InvalidOperationException($"Superclass '{valueStr}' not found");
        }
        else
        {
            // For null/nil superclass, we always want to set actual null
            // (not the nil object which is an instance of UndefinedObject)
            superclassOop = -1;
        }
        
        // Superclass is at slot 0
        if (superclassOop == -1)
        {
            // Special case: actually set null for ProtoObject's superclass
            var obj = ObjectMemory.FromOop(targetOop);
            if (obj != null)
            {
                obj.SetField(0, null);
            }
        }
        else
        {
            vm.PrimitiveSetSlot(targetOop, 0, superclassOop);
        }
        BootstrapLog($"[BOOTSTRAP] Set superclass of '{op.Target}' to '{op.Value ?? "nil"}'");
    }
    
    /// <summary>
    /// Execute registerGlobal operation.
    /// </summary>
    private void ExecuteRegisterGlobal(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Name) || op.Value == null)
            throw new InvalidOperationException("registerGlobal requires name and value");
            
        string? valueStr = null;
        if (op.Value is string str)
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        else
        {
            throw new InvalidOperationException("registerGlobal value must be a string");
        }
            
        if (!namedObjects.TryGetValue(valueStr!, out var valueOop))
            throw new InvalidOperationException($"Value object '{valueStr}' not found");
            
        vm.PrimitiveRegisterGlobal(op.Name, valueOop);
    }
    
    /// <summary>
    /// Execute createSymbol operation.
    /// </summary>
    private void ExecuteCreateSymbol(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result) || op.Value == null)
            throw new InvalidOperationException("createSymbol requires result and value");
            
        string? valueStr = null;
        if (op.Value is string str)
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        else
        {
            throw new InvalidOperationException("createSymbol value must be a string");
        }
            
        var oop = vm.PrimitiveInternSymbol(valueStr!);
        namedObjects[op.Result] = oop;
        BootstrapLog($"[BOOTSTRAP] Created symbol '{valueStr}' -> OOP {oop}");
    }
    
    /// <summary>
    /// Execute createString operation.
    /// </summary>
    private void ExecuteCreateString(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result) || op.Value == null)
            throw new InvalidOperationException("createString requires result and value");
            
        string? valueStr = null;
        if (op.Value is string str)
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        else
        {
            throw new InvalidOperationException("createString value must be a string");
        }
            
        var oop = vm.PrimitiveInternString(valueStr!);
        namedObjects[op.Result] = oop;
        BootstrapLog($"[BOOTSTRAP] Created string '{valueStr}' -> OOP {oop}");
    }
    
    /// <summary>
    /// Execute createArray operation.
    /// </summary>
    private void ExecuteCreateArray(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result))
            throw new InvalidOperationException("createArray requires a result");
            
        var size = op.Size ?? 0;
        var oop = vm.PrimitiveNewArray(size);
        namedObjects[op.Result] = oop;
        BootstrapLog($"[BOOTSTRAP] Created array of size {size} -> OOP {oop}");
    }
    
    /// <summary>
    /// Execute createDictionary operation.
    /// </summary>
    private void ExecuteCreateDictionary(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result))
            throw new InvalidOperationException("createDictionary requires a result");
            
        var oop = vm.PrimitiveNewDictionary();
        namedObjects[op.Result] = oop;
        BootstrapLog($"[BOOTSTRAP] Created dictionary -> OOP {oop}");
    }
    
    /// <summary>
    /// Execute createMethodDictionary operation.
    /// Creates a method dictionary with specified capacity and installs it in the target class.
    /// </summary>
    private void ExecuteCreateMethodDictionary(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Class))
            throw new InvalidOperationException("createMethodDictionary requires a class");
            
        if (!namedObjects.TryGetValue(op.Class, out int classOop))
            throw new InvalidOperationException($"Class '{op.Class}' not found for createMethodDictionary");
            
        var capacity = op.Capacity ?? 32; // Default capacity
        
        // Create a new dictionary with the specified capacity
        // For now, we'll use PrimitiveNewDictionary and hope it creates with sufficient space
        // In a full implementation, this would create a dictionary with exact capacity
        var methodDictOop = vm.PrimitiveNewDictionary();
        
        // Install the method dictionary in slot 1 (methodDict) of the class
        vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
        
        // Optionally name the dictionary for debugging
        if (!string.IsNullOrEmpty(op.Result))
        {
            namedObjects[op.Result] = methodDictOop;
        }
        
        BootstrapLog($"[BOOTSTRAP] Created method dictionary with capacity {capacity} for class '{op.Class}' -> OOP {methodDictOop}");
    }
    
    /// <summary>
    /// Execute installMethod operation.
    /// </summary>
    private void ExecuteInstallMethod(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || string.IsNullOrEmpty(op.Selector) || string.IsNullOrEmpty(op.Source))
            throw new InvalidOperationException("installMethod requires target, selector, and source");
            
        if (!namedObjects.TryGetValue(op.Target, out var classOop))
            throw new InvalidOperationException($"Target class '{op.Target}' not found");
            
        vm.PrimitiveInstallMethod(classOop, op.Selector, op.Source);
    }
    
    /// <summary>
    /// Execute setInstanceVariables operation.
    /// </summary>
    private void ExecuteSetInstanceVariables(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || op.Value == null)
            throw new InvalidOperationException("setInstanceVariables requires target and value");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Target object '{op.Target}' not found");
            
        // Value should be an array of strings
        if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var varNames = new List<string>();
            foreach (var element in jsonElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                    varNames.Add(element.GetString() ?? "");
            }
            
            // Store instance variable names in slot 3 (for classes)
            // This is a simplification - we'd need to create a proper array object
            var namesArray = vm.PrimitiveNewArray(varNames.Count);
            for (int i = 0; i < varNames.Count; i++)
            {
                var symbolOop = vm.PrimitiveInternSymbol(varNames[i]);
                vm.PrimitiveArrayAtPut(namesArray, i, symbolOop);
            }
            vm.PrimitiveSetSlot(targetOop, 3, namesArray);
            
            BootstrapLog($"[BOOTSTRAP] Set instance variables of '{op.Target}' to [{string.Join(", ", varNames)}]");
        }
        else
        {
            BootstrapLog($"[BOOTSTRAP] Warning: setInstanceVariables value must be an array");
        }
    }
    
    /// <summary>
    /// Execute setSlot operation by name.
    /// </summary>
    private void ExecuteSetSlot(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || string.IsNullOrEmpty(op.Slot))
            throw new InvalidOperationException("setSlot requires target and slot");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Target object '{op.Target}' not found");
            
        // Map slot names to indices based on class structure
        int slotIndex = GetSlotIndex(op.Target, op.Slot);
        if (slotIndex < 0)
        {
            BootstrapLog($"[BOOTSTRAP] Warning: Unknown slot '{op.Slot}' for '{op.Target}'");
            return;
        }
        
        // Get the value to store
        int valueOop = 0;
        string? valueStr = null;
        
        if (op.Value is string str)
        {
            valueStr = str;
        }
        else if (op.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            valueStr = jsonElement.GetString();
        }
        
        if (!string.IsNullOrEmpty(valueStr))
        {
            if (namedObjects.TryGetValue(valueStr, out valueOop))
            {
                // It's a reference to another object
            }
            else
            {
                // It's a literal string - create a symbol
                valueOop = vm.PrimitiveInternSymbol(valueStr);
            }
        }
        
        vm.PrimitiveSetSlot(targetOop, slotIndex, valueOop);
        BootstrapLog($"[BOOTSTRAP] Set slot '{op.Slot}' of '{op.Target}' to '{op.Value}'");
    }
    
    /// <summary>
    /// Get the slot index for a named slot.
    /// </summary>
    private int GetSlotIndex(string className, string slotName)
    {
        // Based on the standard Smalltalk-80 class layout
        // Classes (8 slots): [0] superclass, [1] methodDict, [2] format, 
        //                    [3] instanceVariables, [4] organization, [5] name,
        //                    [6] classPool, [7] sharedPools
        // Metaclasses (6 slots): [0] superclass, [1] methodDict, [2] format,
        //                        [3] instanceVariables, [4] organization, [5] thisClass
        
        // Special handling for Class and Metaclass which are themselves metaclasses
        // but need to be treated as classes for slot purposes
        bool isMetaclass = className.EndsWith("Class") && className != "Class" && className != "Metaclass";
        
        return slotName.ToLower() switch
        {
            "superclass" => 0,
            "methoddict" => 1,
            "format" => 2,
            "instancevariables" => 3,
            "organization" => 4,
            "name" when !isMetaclass => 5,
            "thisclass" when isMetaclass => 5,
            "classpool" when !isMetaclass => 6,
            "sharedpools" when !isMetaclass => 7,
            _ => -1
        };
    }
    
    /// <summary>
    /// Execute setCategory operation.
    /// </summary>
    private void ExecuteSetCategory(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || op.Value == null)
            throw new InvalidOperationException("setCategory requires target and value");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Target object '{op.Target}' not found");
            
        // Category is typically stored in the organization slot (4)
        // For now, just log it
        BootstrapLog($"[BOOTSTRAP] Set category of '{op.Target}' to '{op.Value}'");
    }
    
    /// <summary>
    /// Execute defineMethod operation.
    /// </summary>
    private void ExecuteDefineMethod(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target) || string.IsNullOrEmpty(op.Selector))
            throw new InvalidOperationException("defineMethod requires target and selector");
            
        if (!namedObjects.TryGetValue(op.Target, out var classOop))
            throw new InvalidOperationException($"Target class '{op.Target}' not found");
            
        // Check if it's a primitive method
        if (op.Primitive > 0)
        {
            BootstrapLog($"[BOOTSTRAP] Installing primitive method '{op.Selector}' (primitive {op.Primitive}) on '{op.Target}'");
            
            // Create a CompiledMethod for the primitive
            var compiledMethodClassOop = vm.PrimitiveLookupClass("CompiledMethod");
            if (compiledMethodClassOop < 0)
            {
                BootstrapLog($"[BOOTSTRAP] Warning: CompiledMethod class not found, cannot install method");
                return;
            }
            
            var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop);
            
            // Set selector (slot 0)
            var selectorOop = vm.PrimitiveInternSymbol(op.Selector);
            vm.PrimitiveSetSlot(methodOop, 0, selectorOop);
            
            // Set bytecode (slot 1) - empty for primitive-only methods
            var emptyBytecodeOop = vm.PrimitiveNewByteArray(0);
            vm.PrimitiveSetSlot(methodOop, 1, emptyBytecodeOop);
            
            // Set literals (slot 2) - empty array
            var emptyLiteralsOop = vm.PrimitiveNewArray(0);
            vm.PrimitiveSetSlot(methodOop, 2, emptyLiteralsOop);
            
            // Set header (slot 3) with primitive number
            var headerOop = vm.PrimitiveNewSmallInteger(op.Primitive);
            vm.PrimitiveSetSlot(methodOop, 3, headerOop);
            
            // Install in the class's method dictionary
            var methodDictOop = vm.GetSlot(classOop, 1);
            if (methodDictOop < 0)
            {
                // Create a new method dictionary
                methodDictOop = vm.PrimitiveNewDictionary();
                vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
            }
            
            // Add method to dictionary
            vm.PrimitiveDictionaryAtPut(methodDictOop, selectorOop, methodOop);
            BootstrapLog($"[BOOTSTRAP] Installed method '{op.Selector}' in class '{op.Target}'");
        }
        else if (!string.IsNullOrEmpty(op.Source))
        {
            // For Phase 1, we can't actually compile methods yet
            // Just log it for now
            BootstrapLog($"[BOOTSTRAP] Define method '{op.Selector}' on '{op.Target}' (source methods not yet supported)");
        }
        else
        {
            throw new InvalidOperationException("defineMethod requires either source or primitive");
        }
    }
    
    /// <summary>
    /// Create an object with explicit ID from bootstrap manifest.
    /// </summary>
    private void CreateBootstrapObject(BootstrapObjectDefinition objDef)
    {
        if (string.IsNullOrEmpty(objDef.Id))
            throw new InvalidOperationException("Object definition requires an id");
            
        // Accept either "class" or "className" property
        var className = objDef.ClassName ?? objDef.ClassNameAlternate;
        if (string.IsNullOrEmpty(className))
            throw new InvalidOperationException($"Object '{objDef.Id}' requires a class");
            
        int oop = 0;
        
        // Handle different object types
        switch (className.ToLower())
        {
            case "symbol":
                // Symbol with value field
                if (objDef.Fields != null && objDef.Fields.TryGetValue("value", out var symbolValue))
                {
                    oop = vm.PrimitiveInternSymbol(symbolValue?.ToString() ?? "");
                }
                break;
                
            case "string":
                // String with characters field
                if (objDef.Fields != null && objDef.Fields.TryGetValue("characters", out var stringValue))
                {
                    oop = vm.PrimitiveInternString(stringValue?.ToString() ?? "");
                }
                break;
                
            case "smallinteger":
                // SmallInteger with value
                if (objDef.Value != null)
                {
                    if (int.TryParse(objDef.Value.ToString(), out var intValue))
                    {
                        // SmallIntegers are immediate values (tagged pointers)
                        oop = (intValue << 1) | 1;
                    }
                }
                break;
                
            case "bytearray":
                // ByteArray with elements
                if (objDef.Elements != null)
                {
                    oop = CreateByteArray(objDef.Elements);
                }
                break;
                
            case "array":
                // Array with elements (references to other objects)
                if (objDef.Elements != null)
                {
                    oop = CreateArrayWithReferences(objDef.Elements);
                }
                break;
                
            case "compiledmethod":
                // CompiledMethod with selector, bytecode, literals, header
                oop = CreateCompiledMethodFromDefinition(objDef);
                break;
                
            case "methoddictionary":
                // MethodDictionary with keys and values arrays
                oop = CreateMethodDictionary(objDef);
                break;
                
            default:
                // Generic object creation
                BootstrapLog($"[BOOTSTRAP] Creating object '{objDef.Id}' of class '{className}'");
                if (namedObjects.TryGetValue(className, out var classOop))
                {
                    BootstrapLog($"[BOOTSTRAP]   - Found class '{className}' in namedObjects with OOP {classOop}");
                    
                    // Check if this is a Dictionary-like class that needs a specific size
                    if (objDef.Size.HasValue && (className == "Dictionary" || className == "SystemDictionary" || className == "MethodDictionary"))
                    {
                        BootstrapLog($"[BOOTSTRAP]   - Creating dictionary with size {objDef.Size.Value}");
                        oop = vm.PrimitiveNewArray(objDef.Size.Value);
                        vm.PrimitiveSetClass(oop, classOop);
                        
                        // Initialize tally to 0 for dictionaries
                        var zeroOop = vm.PrimitiveNewSmallInteger(0);
                        vm.PrimitiveArrayAtPut(oop, 0, zeroOop);
                        
                        // Initialize remaining slots to nil
                        var nilOop = vm.PrimitiveNil();
                        for (int i = 1; i < objDef.Size.Value; i++)
                        {
                            vm.PrimitiveArrayAtPut(oop, i, nilOop);
                        }
                    }
                    else
                    {
                        oop = vm.PrimitiveNewInstance(classOop);
                    }
                    
                    // Set fields if any
                    if (objDef.Fields != null)
                    {
                        SetObjectFields(oop, objDef.Fields);
                    }
                }
                else
                {
                    // Try looking it up directly
                    classOop = vm.PrimitiveLookupClass(className);
                    if (classOop > 0)
                    {
                        BootstrapLog($"[BOOTSTRAP]   - Found class '{className}' via PrimitiveLookupClass with OOP {classOop}");
                        
                        // Check if this is a Dictionary-like class that needs a specific size
                        if (objDef.Size.HasValue && (className == "Dictionary" || className == "SystemDictionary" || className == "MethodDictionary"))
                        {
                            BootstrapLog($"[BOOTSTRAP]   - Creating dictionary with size {objDef.Size.Value}");
                            oop = vm.PrimitiveNewArray(objDef.Size.Value);
                            vm.PrimitiveSetClass(oop, classOop);
                            
                            // Initialize tally to 0 for dictionaries
                            var zeroOop = vm.PrimitiveNewSmallInteger(0);
                            vm.PrimitiveArrayAtPut(oop, 0, zeroOop);
                            
                            // Initialize remaining slots to nil
                            var nilOop = vm.PrimitiveNil();
                            for (int i = 1; i < objDef.Size.Value; i++)
                            {
                                vm.PrimitiveArrayAtPut(oop, i, nilOop);
                            }
                        }
                        else
                        {
                            oop = vm.PrimitiveNewInstance(classOop);
                        }
                        
                        // Set fields if any
                        if (objDef.Fields != null)
                        {
                            SetObjectFields(oop, objDef.Fields);
                        }
                    }
                    else
                    {
                        BootstrapLog($"[BOOTSTRAP] Warning: Class '{className}' not found for object '{objDef.Id}'");
                    }
                }
                break;
        }
        
        if (oop > 0)
        {
            namedObjects[objDef.Id] = oop;
            BootstrapLog($"[BOOTSTRAP] Created object '{objDef.Id}' of class '{className}' -> OOP {oop}");
            
            // Register singleton objects with ObjectMemory
            // Use ObjectMemory.FromOop to safely get the object
            if (objDef.Id == "nil" && className == "UndefinedObject")
            {
                var nilObj = ObjectMemory.FromOop(oop);
                if (nilObj != null)
                {
                    Console.WriteLine($"[DEBUG] Setting ObjectMemory.NilObject to object with OOP {oop}");
                    ObjectMemory.NilObject = nilObj;
                    vm.PrimitiveRegisterGlobal("nil", oop);
                    BootstrapLog($"[BOOTSTRAP] Registered nil singleton with OOP {oop}");
                }
            }
            else if (objDef.Id == "true" && objDef.ClassName == "True")
            {
                var trueObj = ObjectMemory.FromOop(oop);
                if (trueObj != null)
                {
                    ObjectMemory.TrueObject = trueObj;
                    vm.PrimitiveRegisterGlobal("true", oop);
                    BootstrapLog($"[BOOTSTRAP] Registered true singleton");
                }
            }
            else if (objDef.Id == "false" && objDef.ClassName == "False")
            {
                var falseObj = ObjectMemory.FromOop(oop);
                if (falseObj != null)
                {
                    ObjectMemory.FalseObject = falseObj;
                    vm.PrimitiveRegisterGlobal("false", oop);
                    BootstrapLog($"[BOOTSTRAP] Registered false singleton");
                }
            }
        }
    }
    
    /// <summary>
    /// Create a ByteArray from elements.
    /// </summary>
    private int CreateByteArray(List<object> elements)
    {
        // Get ByteArray class
        var byteArrayClassOop = vm.PrimitiveLookupClass("ByteArray");
        if (byteArrayClassOop < 0)
        {
            // Create as regular array if ByteArray class doesn't exist
            byteArrayClassOop = vm.PrimitiveLookupClass("Array");
        }
        
        var arrayOop = vm.PrimitiveNewArray(elements.Count);
        
        // Set class if found
        if (byteArrayClassOop > 0)
        {
            vm.PrimitiveSetClass(arrayOop, byteArrayClassOop);
        }
        
        // Store bytes as SmallInteger objects
        for (int i = 0; i < elements.Count; i++)
        {
            int value = 0;
            if (elements[i] is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Number)
            {
                value = jsonElem.GetInt32();
            }
            else if (elements[i] is int intValue)
            {
                value = intValue;
            }
            // Create a SmallInteger object for the byte value
            var smallIntOop = vm.PrimitiveNewSmallInteger(value);
            vm.PrimitiveArrayAtPut(arrayOop, i, smallIntOop);
        }
        
        return arrayOop;
    }
    
    /// <summary>
    /// Create an Array with references to other objects.
    /// </summary>
    private int CreateArrayWithReferences(List<object> elements)
    {
        var arrayOop = vm.PrimitiveNewArray(elements.Count);
        
        for (int i = 0; i < elements.Count; i++)
        {
            int elementOop = 0;
            
            if (elements[i] is string refId)
            {
                // It's a reference to another object
                if (namedObjects.TryGetValue(refId, out elementOop))
                {
                    // Found the referenced object
                }
                else
                {
                    BootstrapLog($"[BOOTSTRAP] Warning: Referenced object '{refId}' not found");
                }
            }
            else if (elements[i] is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.String)
            {
                var refIdStr = jsonElem.GetString();
                if (!string.IsNullOrEmpty(refIdStr) && namedObjects.TryGetValue(refIdStr, out elementOop))
                {
                    // Found the referenced object
                }
            }
            
            vm.PrimitiveArrayAtPut(arrayOop, i, elementOop);
        }
        
        return arrayOop;
    }
    
    /// <summary>
    /// Create a CompiledMethod from object definition.
    /// </summary>
    private int CreateCompiledMethodFromDefinition(BootstrapObjectDefinition objDef)
    {
        // Get the CompiledMethod class
        var compiledMethodClassOop = vm.PrimitiveLookupClass("CompiledMethod");
        if (compiledMethodClassOop < 0)
        {
            BootstrapLog($"[BOOTSTRAP] Warning: CompiledMethod class not found");
            return 0;
        }
        
        // Create the method object
        var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop);
        
        if (objDef.Fields != null)
        {
            // Set selector (slot 0)
            if (objDef.Fields.TryGetValue("selector", out var selectorRef))
            {
                var selectorOop = ResolveReference(selectorRef);
                vm.PrimitiveSetSlot(methodOop, 0, selectorOop);
            }
            
            // Set bytecode (slot 1)
            if (objDef.Fields.TryGetValue("bytecode", out var bytecodeRef))
            {
                var bytecodeOop = ResolveReference(bytecodeRef);
                vm.PrimitiveSetSlot(methodOop, 1, bytecodeOop);
            }
            
            // Set literals (slot 2)
            if (objDef.Fields.TryGetValue("literals", out var literalsRef))
            {
                var literalsOop = ResolveReference(literalsRef);
                vm.PrimitiveSetSlot(methodOop, 2, literalsOop);
            }
            
            // Set header (slot 3)
            if (objDef.Fields.TryGetValue("header", out var headerValue))
            {
                int header = 0;
                if (headerValue is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Number)
                {
                    header = jsonElem.GetInt32();
                }
                else if (headerValue is int intValue)
                {
                    header = intValue;
                }
                // Create a SmallInteger object for the header
                var headerOop = vm.PrimitiveNewSmallInteger(header);
                vm.PrimitiveSetSlot(methodOop, 3, headerOop);
            }
        }
        
        return methodOop;
    }
    
    /// <summary>
    /// Create a MethodDictionary from object definition.
    /// </summary>
    private int CreateMethodDictionary(BootstrapObjectDefinition objDef)
    {
        // Get the MethodDictionary class
        var methodDictClassOop = vm.PrimitiveLookupClass("MethodDictionary");
        if (methodDictClassOop < 0)
        {
            BootstrapLog($"[BOOTSTRAP] Warning: MethodDictionary class not found, using generic dictionary");
            return vm.PrimitiveNewDictionary();
        }
        
        // MethodDictionary uses parallel arrays structure:
        // Slot 0: tally
        // Slots 1..n: keys  
        // Last slot: valueArray (for the values)
        
        int keysCount = 0;
        if (objDef.Fields != null && objDef.Fields.TryGetValue("keys", out var keysValue))
        {
            if (keysValue is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Array)
            {
                keysCount = jsonElem.GetArrayLength();
            }
        }
        
        // Size needs to be: 1 (tally) + keysCount + 1 (valueArray)
        int size = Math.Max(16, keysCount + 2); // Minimum size 16 for method dictionary
        
        // Create an array with the proper size, then change its class
        var dictOop = vm.PrimitiveNewArray(size);
        vm.PrimitiveSetClass(dictOop, methodDictClassOop);
        
        // Initialize tally to actual count
        var tallyOop = vm.PrimitiveNewSmallInteger(keysCount);
        vm.PrimitiveSetSlot(dictOop, 0, tallyOop);
        
        // Initialize all slots to nil
        var nilOop = vm.PrimitiveNil();
        for (int i = 1; i < size; i++)
        {
            vm.PrimitiveSetSlot(dictOop, i, nilOop);
        }
        
        if (objDef.Fields != null)
        {
            // Create and populate keys and values arrays
            if (objDef.Fields.TryGetValue("keys", out var keysVal) && 
                objDef.Fields.TryGetValue("values", out var valuesVal))
            {
                var keysArray = CreateArrayFromValue(keysVal);
                var valuesArray = CreateArrayFromValue(valuesVal);
                
                // Copy keys into the base slots (1..keysCount)
                for (int i = 0; i < keysCount; i++)
                {
                    var keyOop = vm.GetSlot(keysArray, i);
                    if (keyOop < 0)
                    {
                        BootstrapLog($"[BOOTSTRAP] Warning: Could not get key at index {i} from keys array");
                        continue;
                    }
                    vm.PrimitiveSetSlot(dictOop, i + 1, keyOop);
                }
                
                // Set the valueArray in the last slot
                vm.PrimitiveSetSlot(dictOop, size - 1, valuesArray);
            }
        }
        
        return dictOop;
    }
    
    /// <summary>
    /// Create an array from a value (could be list of references).
    /// </summary>
    private int CreateArrayFromValue(object value)
    {
        if (value is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Array)
        {
            var items = new List<object>();
            foreach (var element in jsonElem.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    items.Add(element.GetString()!);
                }
                else if (element.ValueKind == JsonValueKind.Number)
                {
                    items.Add(element.GetInt32());
                }
            }
            return CreateArrayWithReferences(items);
        }
        else if (value is List<object> list)
        {
            return CreateArrayWithReferences(list);
        }
        
        // Single value - create array with one element
        return CreateArrayWithReferences(new List<object> { value });
    }
    
    /// <summary>
    /// Resolve a reference to an object by ID.
    /// </summary>
    private int ResolveReference(object reference)
    {
        if (reference is string refId)
        {
            if (namedObjects.TryGetValue(refId, out var oop))
            {
                return oop;
            }
            BootstrapLog($"[BOOTSTRAP] Warning: Reference '{refId}' not found");
        }
        else if (reference is JsonElement jsonElem)
        {
            if (jsonElem.ValueKind == JsonValueKind.String)
            {
                var refIdStr = jsonElem.GetString();
                if (!string.IsNullOrEmpty(refIdStr) && namedObjects.TryGetValue(refIdStr, out var oop))
                {
                    return oop;
                }
            }
            else if (jsonElem.ValueKind == JsonValueKind.Number)
            {
                return jsonElem.GetInt32();
            }
        }
        else if (reference is int intValue)
        {
            return intValue;
        }
        
        return 0;
    }
    
    /// <summary>
    /// Set fields on an object from a dictionary.
    /// </summary>
    private void SetObjectFields(int objectOop, Dictionary<string, object> fields)
    {
        foreach (var (fieldName, fieldValue) in fields)
        {
            // Map field name to slot index based on class structure
            // For now, we'll use numeric indices if provided
            if (int.TryParse(fieldName, out var slotIndex))
            {
                var valueOop = ResolveReference(fieldValue);
                vm.PrimitiveSetSlot(objectOop, slotIndex, valueOop);
            }
        }
    }
    
    /// <summary>
    /// Create the class structure without installing methods.
    /// </summary>
    private void CreateClassStructure(BootstrapClassDefinition classDef)
    {
        if (string.IsNullOrEmpty(classDef.Name))
            throw new InvalidOperationException("Class definition requires a name");
            
        // Check if the class already exists (from Phase 1)
        int classOop = vm.PrimitiveLookupClass(classDef.Name);
        int metaclassOop = 0;
        var metaclassName = $"{classDef.Name}Class";
        bool isNewClass = classOop < 0;
        
        if (isNewClass)
        {
            // Class doesn't exist, create it
            BootstrapLog($"[BOOTSTRAP] Creating class '{classDef.Name}'");
            classOop = vm.PrimitiveNewClass();
            namedObjects[classDef.Name] = classOop;
            vm.PrimitiveRegisterGlobal(classDef.Name, classOop);
            
            // Create the metaclass
            metaclassOop = vm.PrimitiveNewMetaclass();
            namedObjects[metaclassName] = metaclassOop;
            vm.PrimitiveRegisterGlobal(metaclassName, metaclassOop);
            
        }
        else
        {
            // Class exists, enhance it
            BootstrapLog($"[BOOTSTRAP] Enhancing existing class '{classDef.Name}'");
            namedObjects[classDef.Name] = classOop;
            
            // Get the existing metaclass
            metaclassOop = vm.PrimitiveLookupClass(metaclassName);
            if (metaclassOop > 0)
            {
                namedObjects[metaclassName] = metaclassOop;
            }
        }
        
        // Register the class in the Smalltalk SystemDictionary as well
        var smalltalkDictOop = vm.PrimitiveLookupGlobal("Smalltalk");
        if (smalltalkDictOop > 0)
        {
            // Add class to Smalltalk dictionary: Smalltalk at: #ClassName put: ClassObject
            var keySymbol = vm.PrimitiveInternSymbol(classDef.Name);
            vm.PrimitiveDictionaryAtPut(smalltalkDictOop, keySymbol, classOop);
            BootstrapLog($"[BOOTSTRAP]   - Registered {classDef.Name} in Smalltalk dictionary");
        }
        
        // Mark variable-sized classes (Array, ByteArray, etc.)
        if (classDef.VariableSize)
        {
            // Set format field to indicate variable size
            // Format slot (2) encodes instance spec
            vm.PrimitiveSetSlot(classOop, 2, 2); // 2 = words indexable
        }
        
        // Set class pointers only for new classes (existing ones already have them)
        if (isNewClass)
        {
            // Set class pointer for the class
            vm.PrimitiveSetClass(classOop, metaclassOop);
            
            // Set metaclass's class to Metaclass
            // First try namedObjects, then look it up from VM
            int metaclassClassOop = 0;
            if (namedObjects.TryGetValue("Metaclass", out metaclassClassOop))
            {
                // Found in named objects
                vm.PrimitiveSetClass(metaclassOop, metaclassClassOop);
            }
            else
            {
                // Try to get it from the VM's global registry
                metaclassClassOop = vm.PrimitiveLookupClass("Metaclass");
                if (metaclassClassOop > 0)
                {
                    vm.PrimitiveSetClass(metaclassOop, metaclassClassOop);
                }
                else if (classDef.Name != "Metaclass")
                {
                    // Don't warn for Metaclass itself
                    BootstrapLog($"[BOOTSTRAP] Warning: Metaclass not found, metaclass for '{classDef.Name}' will have no class pointer");
                }
            }
        }
        
        // Set superclass if specified
        if (!string.IsNullOrEmpty(classDef.Superclass))
        {
            // For 'nil' superclass (Object), don't set superclass
            if (classDef.Superclass == "nil")
            {
                // Object has no superclass
                return;
            }
            
            // Try namedObjects first, then lookup from VM
            int superclassOop = 0;
            if (namedObjects.TryGetValue(classDef.Superclass, out superclassOop))
            {
                // Found in named objects
            }
            else
            {
                // Try to look up from VM (for classes created earlier)
                superclassOop = vm.PrimitiveLookupClass(classDef.Superclass);
            }
            
            if (superclassOop > 0)
            {
                vm.PrimitiveSetSlot(classOop, 0, superclassOop); // Slot 0 is superclass
                
                // Set metaclass superclass to superclass's metaclass
                var superMetaclassName = $"{classDef.Superclass}Class";
                int superMetaclassOop = 0;
                if (namedObjects.TryGetValue(superMetaclassName, out superMetaclassOop))
                {
                    // Found in named objects
                }
                else
                {
                    // Try to look up from VM
                    superMetaclassOop = vm.PrimitiveLookupClass(superMetaclassName);
                }
                
                if (superMetaclassOop > 0)
                {
                    vm.PrimitiveSetSlot(metaclassOop, 0, superMetaclassOop);
                }
            }
            else
            {
                BootstrapLog($"[BOOTSTRAP] Warning: Superclass '{classDef.Superclass}' not found for '{classDef.Name}'");
            }
        }
        
        // Set instance variables
        if (classDef.InstanceVariables != null && classDef.InstanceVariables.Count > 0)
        {
            var varsArray = vm.PrimitiveNewArray(classDef.InstanceVariables.Count);
            for (int i = 0; i < classDef.InstanceVariables.Count; i++)
            {
                var symbolOop = vm.PrimitiveInternSymbol(classDef.InstanceVariables[i]);
                vm.PrimitiveArrayAtPut(varsArray, i, symbolOop);
            }
            vm.PrimitiveSetSlot(classOop, 3, varsArray); // Slot 3 is instanceVariables
        }
        
        // Set class name
        var nameSymbol = vm.PrimitiveInternSymbol(classDef.Name);
        vm.PrimitiveSetSlot(classOop, 5, nameSymbol); // Slot 5 is name
    }
    
    /// <summary>
    /// Install methods for a class (must be called after all classes are created).
    /// </summary>
    private void InstallClassMethods(BootstrapClassDefinition classDef)
    {
        if (string.IsNullOrEmpty(classDef.Name))
            return;
            
        // Look up the class that was already created
        int classOop = vm.PrimitiveLookupClass(classDef.Name);
        if (classOop < 0)
        {
            BootstrapLog($"[BOOTSTRAP] Warning: Class '{classDef.Name}' not found, cannot install methods");
            return;
        }
        
        var metaclassName = $"{classDef.Name}Class";
        int metaclassOop = vm.PrimitiveLookupClass(metaclassName);
        
        // Install instance methods
        if (classDef.Methods != null && classDef.Methods.Count > 0)
        {
            BootstrapLog($"[BOOTSTRAP] Installing {classDef.Methods.Count} methods on '{classDef.Name}'");
            
            // Get or create the method dictionary
            var methodDictOop = vm.GetSlot(classOop, 1); // Slot 1 is methodDict
            BootstrapLog($"[BOOTSTRAP]   - Class '{classDef.Name}' (OOP {classOop}) has method dict in slot 1: {methodDictOop}");
            if (methodDictOop <= 0)
            {
                // Create a new method dictionary
                var methodDictClassOop = vm.PrimitiveLookupClass("MethodDictionary");
                if (methodDictClassOop > 0)
                {
                    // Create MethodDictionary as a variable-sized object
                    // Need space for: tally (1) + keys (n) + values (n) = 1 + 2*n slots
                    // Add 50% extra space for hash table efficiency
                    int methodCount = classDef.Methods.Count;
                    int dictSize = 1 + (methodCount * 2) + (methodCount); // 1 + 2n + n/2 extra
                    if (dictSize < 32) dictSize = 32; // Minimum size
                    
                    methodDictOop = vm.PrimitiveNewArray(dictSize);
                    vm.PrimitiveSetClass(methodDictOop, methodDictClassOop);
                    
                    // Initialize tally to 0
                    var zeroOop = vm.PrimitiveNewSmallInteger(0);
                    BootstrapLog($"[BOOTSTRAP]   - Initializing method dict tally to 0, SmallInteger OOP: {zeroOop}");
                    vm.PrimitiveArrayAtPut(methodDictOop, 0, zeroOop);
                    
                    // Initialize remaining slots to nil
                    var nilOop = vm.PrimitiveNil();
                    for (int i = 1; i < dictSize; i++)
                    {
                        vm.PrimitiveArrayAtPut(methodDictOop, i, nilOop);
                    }
                    
                    BootstrapLog($"[BOOTSTRAP]   - Created method dictionary with {dictSize} slots for {methodCount} methods");
                }
                else
                {
                    // Fall back to basic dictionary
                    methodDictOop = vm.PrimitiveNewDictionary();
                }
                vm.PrimitiveSetSlot(classOop, 1, methodDictOop);
                BootstrapLog($"[BOOTSTRAP]   - Created new method dictionary (OOP {methodDictOop}) for '{classDef.Name}' and set in slot 1");
                // Verify it was set
                var verifyDict = vm.GetSlot(classOop, 1);
                BootstrapLog($"[BOOTSTRAP]   - Verification: slot 1 now contains OOP {verifyDict}");
            }
            
            // Install each method
            foreach (var methodDef in classDef.Methods)
            {
                if (string.IsNullOrEmpty(methodDef.Selector))
                    continue;
                    
                // Get the CompiledMethod class
                var compiledMethodClassOop = vm.PrimitiveLookupClass("CompiledMethod");
                if (compiledMethodClassOop < 0)
                {
                    BootstrapLog($"[BOOTSTRAP] Warning: CompiledMethod class not found, skipping method '{methodDef.Selector}'");
                    continue;
                }
                
                // Create the method object
                var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop);
                
                // Set selector (slot 0)
                var selectorOop = vm.PrimitiveInternSymbol(methodDef.Selector);
                vm.PrimitiveSetSlot(methodOop, 0, selectorOop);
                
                // Set bytecode (slot 1)
                if (methodDef.Bytecode != null && methodDef.Bytecode.Count > 0)
                {
                    var bytecodeOop = vm.PrimitiveNewByteArray(methodDef.Bytecode.Count);
                    for (int i = 0; i < methodDef.Bytecode.Count; i++)
                    {
                        // Store each byte directly as a raw value, not as a SmallInteger
                        // ByteArrays store raw bytes, not object references
                        vm.PrimitiveByteArrayAtPut(bytecodeOop, i, (byte)methodDef.Bytecode[i]);
                    }
                    vm.PrimitiveSetSlot(methodOop, 1, bytecodeOop);
                }
                else
                {
                    // Empty bytecode for primitive-only methods
                    var emptyBytecodeOop = vm.PrimitiveNewByteArray(0);
                    vm.PrimitiveSetSlot(methodOop, 1, emptyBytecodeOop);
                }
                
                // Set literals (slot 2) - ALWAYS set this slot to maintain CompiledMethod structure
                if (methodDef.Literals != null && methodDef.Literals.Count > 0)
                {
                    var literalsOop = vm.PrimitiveNewArray(methodDef.Literals.Count);
                    for (int i = 0; i < methodDef.Literals.Count; i++)
                    {
                        var literal = methodDef.Literals[i];
                        int literalOop;
                        
                        // Check if this is an object reference (e.g., "symbol_1000")
                        if (literal.StartsWith("symbol_") || literal.StartsWith("int_"))
                        {
                            // It's a reference to a pre-created object
                            if (namedObjects.TryGetValue(literal, out literalOop))
                            {
                                // Found the referenced object
                            }
                            else
                            {
                                // Object not found, create a symbol as fallback
                                BootstrapLog($"[BOOTSTRAP] Warning: Object reference '{literal}' not found, creating symbol");
                                literalOop = vm.PrimitiveInternSymbol(literal);
                            }
                        }
                        else
                        {
                            // Regular string literal - create as symbol
                            literalOop = vm.PrimitiveInternSymbol(literal);
                        }
                        
                        vm.PrimitiveArrayAtPut(literalsOop, i, literalOop);
                    }
                    vm.PrimitiveSetSlot(methodOop, 2, literalsOop);
                }
                else
                {
                    // Always set literals slot, even for primitive-only methods
                    // CompiledMethod expects all 4 slots to be present
                    var emptyLiteralsOop = vm.PrimitiveNewArray(0);
                    vm.PrimitiveSetSlot(methodOop, 2, emptyLiteralsOop);
                }
                
                // Set header (slot 3) - use Blue Book encoding
                // Blue Book header format (16-bit word):
                // Bits 0-8: Primitive number (0-511)
                // Bit 9: Large context flag
                // Bits 10-15: Number of temporaries
                int primitiveNum = (methodDef.Primitive ?? 0) & 0x1FF; // 9 bits for primitive
                int largeContext = 0; // We don't have large contexts yet
                int tempCount = methodDef.TemporaryCount; // Use actual temp count from Phase 2
                
                // Override incorrect primitive numbers for critical methods
                if (methodDef.Selector == "basicNew")
                {
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitive value from JSON: {methodDef.Primitive ?? -1}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitiveNum before override: {primitiveNum}");
                    if (primitiveNum != 70)
                    {
                        BootstrapLog($"[BOOTSTRAP]   - WARNING: Overriding primitive for basicNew from {primitiveNum} to 70");
                        primitiveNum = 70;
                    }
                }
                else if (methodDef.Selector == "basicNew:" && primitiveNum != 71)
                {
                    BootstrapLog($"[BOOTSTRAP]   - WARNING: Overriding primitive for basicNew: from {primitiveNum} to 71");
                    primitiveNum = 71;
                }
                
                // Blue Book header encoding
                int headerValue = primitiveNum | (largeContext << 9) | (tempCount << 10);
                
                if (methodDef.Selector == "basicNew")
                {
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header calculation: primitive={primitiveNum}, temps={tempCount}, header={headerValue}");
                }
                
                var headerOop = vm.PrimitiveNewSmallInteger(headerValue);
                vm.PrimitiveSetSlot(methodOop, 3, headerOop);
                
                // Verify the header was set correctly
                if (methodDef.Selector == "basicNew")
                {
                    // The header is stored as a SmallInteger OOP (immediate value)
                    // We can't use GetSlot for this, we need to check the actual field
                    var methodObj = ObjectMemory.FromOop(methodOop);
                    if (methodObj != null && methodObj.Fields.Length > 3)
                    {
                        var headerField = methodObj.Fields[3];
                        BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header field type: {headerField?.GetType().Name ?? "null"}");
                        if (headerField is int headerInt)
                        {
                            // This is the SmallInteger OOP (encoded as (value << 1) | 1)
                            var decodedValue = headerInt >> 1;
                            BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header value from field: {decodedValue}, expected: {headerValue}");
                            var readPrimitive = decodedValue & 0x1FF;
                            BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitive from field: {readPrimitive}, expected: {primitiveNum}");
                        }
                    }
                }
                
                // Add method to dictionary
                vm.PrimitiveDictionaryAtPut(methodDictOop, selectorOop, methodOop);
                if (methodDef.Selector == "instVarAt:put:")
                {
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: method created with OOP {methodOop}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: primitive from JSON: {methodDef.Primitive ?? 0}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: primitiveNum: {primitiveNum}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: tempCount: {tempCount}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: header value: {headerValue}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: headerOop: {headerOop}");
                    
                    // Check what was actually set
                    var actualMethod = ObjectMemory.FromOop(methodOop);
                    if (actualMethod != null && actualMethod.Fields.Length > 3)
                    {
                        BootstrapLog($"[BOOTSTRAP]   - DEBUG: instVarAt:put: actual field[3]: {actualMethod.Fields[3]}");
                    }
                }
                BootstrapLog($"[BOOTSTRAP]   - Installed method '{methodDef.Selector}' (bytecode length: {methodDef.Bytecode?.Count ?? 0}, primitive: {methodDef.Primitive ?? 0})");
            }
        }
        
        // Install class methods on the metaclass
        if (classDef.ClassMethods != null && classDef.ClassMethods.Count > 0)
        {
            BootstrapLog($"[BOOTSTRAP] Installing {classDef.ClassMethods.Count} class methods on '{classDef.Name}' metaclass");
            
            // Get or create the metaclass method dictionary
            var metaclassMethodDictOop = vm.GetSlot(metaclassOop, 1); // Slot 1 is methodDict
            if (metaclassMethodDictOop <= 0)
            {
                // Create a new method dictionary
                var methodDictClassOop = vm.PrimitiveLookupClass("MethodDictionary");
                if (methodDictClassOop > 0)
                {
                    // Create MethodDictionary as a variable-sized object
                    // Need space for: tally (1) + keys (n) + values (n) = 1 + 2*n slots
                    // Add 50% extra space for hash table efficiency
                    int methodCount = classDef.ClassMethods.Count;
                    int dictSize = 1 + (methodCount * 2) + (methodCount); // 1 + 2n + n/2 extra
                    if (dictSize < 32) dictSize = 32; // Minimum size
                    
                    metaclassMethodDictOop = vm.PrimitiveNewArray(dictSize);
                    vm.PrimitiveSetClass(metaclassMethodDictOop, methodDictClassOop);
                    
                    // Initialize tally to 0
                    var zeroOop = vm.PrimitiveNewSmallInteger(0);
                    vm.PrimitiveArrayAtPut(metaclassMethodDictOop, 0, zeroOop);
                    
                    // Initialize remaining slots to nil
                    var nilOop = vm.PrimitiveNil();
                    for (int i = 1; i < dictSize; i++)
                    {
                        vm.PrimitiveArrayAtPut(metaclassMethodDictOop, i, nilOop);
                    }
                    
                    BootstrapLog($"[BOOTSTRAP]   - Created metaclass method dictionary with {dictSize} slots for {methodCount} methods");
                }
                else
                {
                    // Fall back to basic dictionary
                    metaclassMethodDictOop = vm.PrimitiveNewDictionary();
                }
                vm.PrimitiveSetSlot(metaclassOop, 1, metaclassMethodDictOop);
                BootstrapLog($"[BOOTSTRAP]   - Created new method dictionary for '{classDef.Name}' metaclass");
            }
            
            // Install each class method
            foreach (var methodDef in classDef.ClassMethods)
            {
                if (string.IsNullOrEmpty(methodDef.Selector))
                    continue;
                    
                // Get the CompiledMethod class
                var compiledMethodClassOop = vm.PrimitiveLookupClass("CompiledMethod");
                if (compiledMethodClassOop < 0)
                {
                    BootstrapLog($"[BOOTSTRAP] Warning: CompiledMethod class not found, skipping class method '{methodDef.Selector}'");
                    continue;
                }
                
                // Create the method object
                var methodOop = vm.PrimitiveNewInstance(compiledMethodClassOop);
                
                // Set selector (slot 0)
                var selectorOop = vm.PrimitiveInternSymbol(methodDef.Selector);
                vm.PrimitiveSetSlot(methodOop, 0, selectorOop);
                
                // Set bytecode (slot 1)
                if (methodDef.Bytecode != null && methodDef.Bytecode.Count > 0)
                {
                    var bytecodeOop = vm.PrimitiveNewByteArray(methodDef.Bytecode.Count);
                    for (int i = 0; i < methodDef.Bytecode.Count; i++)
                    {
                        // Store each byte directly as a raw value, not as a SmallInteger
                        // ByteArrays store raw bytes, not object references
                        vm.PrimitiveByteArrayAtPut(bytecodeOop, i, (byte)methodDef.Bytecode[i]);
                    }
                    vm.PrimitiveSetSlot(methodOop, 1, bytecodeOop);
                }
                else
                {
                    // Empty bytecode for primitive-only methods
                    var emptyBytecodeOop = vm.PrimitiveNewByteArray(0);
                    vm.PrimitiveSetSlot(methodOop, 1, emptyBytecodeOop);
                }
                
                // Set literals (slot 2) - ALWAYS set this slot to maintain CompiledMethod structure
                if (methodDef.Literals != null && methodDef.Literals.Count > 0)
                {
                    var literalsOop = vm.PrimitiveNewArray(methodDef.Literals.Count);
                    for (int i = 0; i < methodDef.Literals.Count; i++)
                    {
                        var literal = methodDef.Literals[i];
                        int literalOop;
                        
                        // Check if this is an object reference (e.g., "symbol_1000")
                        if (literal.StartsWith("symbol_") || literal.StartsWith("int_"))
                        {
                            // It's a reference to a pre-created object
                            if (namedObjects.TryGetValue(literal, out literalOop))
                            {
                                // Found the referenced object
                            }
                            else
                            {
                                // Object not found, create a symbol as fallback
                                BootstrapLog($"[BOOTSTRAP] Warning: Object reference '{literal}' not found, creating symbol");
                                literalOop = vm.PrimitiveInternSymbol(literal);
                            }
                        }
                        else
                        {
                            // Regular string literal - create as symbol
                            literalOop = vm.PrimitiveInternSymbol(literal);
                        }
                        
                        vm.PrimitiveArrayAtPut(literalsOop, i, literalOop);
                    }
                    vm.PrimitiveSetSlot(methodOop, 2, literalsOop);
                }
                else
                {
                    // Always set literals slot, even for primitive-only methods
                    // CompiledMethod expects all 4 slots to be present
                    var emptyLiteralsOop = vm.PrimitiveNewArray(0);
                    vm.PrimitiveSetSlot(methodOop, 2, emptyLiteralsOop);
                }
                
                // Set header (slot 3) - use Blue Book encoding
                // Blue Book header format (16-bit word):
                // Bits 0-8: Primitive number (0-511)
                // Bit 9: Large context flag
                // Bits 10-15: Number of temporaries
                int primitiveNum = (methodDef.Primitive ?? 0) & 0x1FF; // 9 bits for primitive
                int largeContext = 0; // We don't have large contexts yet
                int tempCount = methodDef.TemporaryCount; // Use actual temp count from Phase 2
                
                // Override incorrect primitive numbers for critical methods
                if (methodDef.Selector == "basicNew")
                {
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitive value from JSON: {methodDef.Primitive ?? -1}");
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitiveNum before override: {primitiveNum}");
                    if (primitiveNum != 70)
                    {
                        BootstrapLog($"[BOOTSTRAP]   - WARNING: Overriding primitive for basicNew from {primitiveNum} to 70");
                        primitiveNum = 70;
                    }
                }
                else if (methodDef.Selector == "basicNew:" && primitiveNum != 71)
                {
                    BootstrapLog($"[BOOTSTRAP]   - WARNING: Overriding primitive for basicNew: from {primitiveNum} to 71");
                    primitiveNum = 71;
                }
                
                // Blue Book header encoding
                int headerValue = primitiveNum | (largeContext << 9) | (tempCount << 10);
                
                if (methodDef.Selector == "basicNew")
                {
                    BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header calculation: primitive={primitiveNum}, temps={tempCount}, header={headerValue}");
                }
                
                var headerOop = vm.PrimitiveNewSmallInteger(headerValue);
                vm.PrimitiveSetSlot(methodOop, 3, headerOop);
                
                // Verify the header was set correctly
                if (methodDef.Selector == "basicNew")
                {
                    // The header is stored as a SmallInteger OOP (immediate value)
                    // We can't use GetSlot for this, we need to check the actual field
                    var methodObj = ObjectMemory.FromOop(methodOop);
                    if (methodObj != null && methodObj.Fields.Length > 3)
                    {
                        var headerField = methodObj.Fields[3];
                        BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header field type: {headerField?.GetType().Name ?? "null"}");
                        if (headerField is int headerInt)
                        {
                            // This is the SmallInteger OOP (encoded as (value << 1) | 1)
                            var decodedValue = headerInt >> 1;
                            BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew header value from field: {decodedValue}, expected: {headerValue}");
                            var readPrimitive = decodedValue & 0x1FF;
                            BootstrapLog($"[BOOTSTRAP]   - DEBUG: basicNew primitive from field: {readPrimitive}, expected: {primitiveNum}");
                        }
                    }
                }
                
                // Add method to dictionary
                vm.PrimitiveDictionaryAtPut(metaclassMethodDictOop, selectorOop, methodOop);
                BootstrapLog($"[BOOTSTRAP]   - Installed class method '{methodDef.Selector}' (bytecode length: {methodDef.Bytecode?.Count ?? 0}, primitive: {methodDef.Primitive ?? 0})");
            }
        }
    }
    
    /// <summary>
    /// Register all classes in the Smalltalk global dictionary.
    /// </summary>
    private void RegisterAllClassesInSmalltalk()
    {
        var smalltalkOop = vm.PrimitiveLookupGlobal("Smalltalk");
        if (smalltalkOop < 0)
        {
            BootstrapLog("[BOOTSTRAP] Warning: Smalltalk global not found, cannot register classes");
            return;
        }
        
        BootstrapLog("[BOOTSTRAP] Registering all classes in Smalltalk dictionary...");
        int count = 0;
        
        // Register all classes from namedObjects
        foreach (var kvp in namedObjects)
        {
            // Skip non-class objects (like "Smalltalk", "true", "false", "nil")
            if (kvp.Key == "Smalltalk" || kvp.Key == "true" || kvp.Key == "false" || kvp.Key == "nil")
                continue;
                
            // Skip metaclasses (they end with "Class")
            if (kvp.Key.EndsWith("Class") && kvp.Key != "Class" && kvp.Key != "Metaclass")
                continue;
                
            // Register the class in Smalltalk dictionary
            var keySymbol = vm.PrimitiveInternSymbol(kvp.Key);
            vm.PrimitiveDictionaryAtPut(smalltalkOop, keySymbol, kvp.Value);
            count++;
        }
        
        BootstrapLog($"[BOOTSTRAP]   - Registered {count} classes in Smalltalk dictionary");
    }
    
    /// <summary>
    /// Apply a patch operation.
    /// </summary>
    private void ApplyBootstrapPatch(BootstrapPatch patch)
    {
        // Log comment if present
        if (!string.IsNullOrEmpty(patch.Comment))
        {
            BootstrapLog($"[BOOTSTRAP] Patch: {patch.Comment}");
        }
        
        // Handle different operations
        if (!string.IsNullOrEmpty(patch.Operation))
        {
            switch (patch.Operation.ToLower())
            {
                case "registerglobal":
                    if (string.IsNullOrEmpty(patch.Target) || string.IsNullOrEmpty(patch.Name))
                        throw new InvalidOperationException("registerGlobal requires target and name");
                    
                    if (namedObjects.TryGetValue(patch.Target, out var globalOop))
                    {
                        vm.PrimitiveRegisterGlobal(patch.Name, globalOop);
                        BootstrapLog($"[BOOTSTRAP]   - Registered '{patch.Name}' as global -> OOP {globalOop}");
                    }
                    else
                    {
                        BootstrapLog($"[BOOTSTRAP] Warning: Object '{patch.Target}' not found for global registration");
                    }
                    return;
                    
                case "registerallclassesinsmalltalk":
                    RegisterAllClassesInSmalltalk();
                    return;
                    
                default:
                    BootstrapLog($"[BOOTSTRAP] Warning: Unknown patch operation '{patch.Operation}'");
                    return;
            }
        }
        
        // Original slot-based patching
        if (string.IsNullOrEmpty(patch.Target) || string.IsNullOrEmpty(patch.Slot))
            throw new InvalidOperationException("Patch requires target and slot");
            
        if (!namedObjects.TryGetValue(patch.Target, out var targetOop))
        {
            // Try to look up from VM
            targetOop = vm.PrimitiveLookupClass(patch.Target);
            if (targetOop < 0)
            {
                BootstrapLog($"[BOOTSTRAP] Warning: Patch target '{patch.Target}' not found");
                return;
            }
        }
        
        // Determine the slot index
        int slotIndex = GetSlotIndex(patch.Target, patch.Slot);
        if (slotIndex < 0)
        {
            BootstrapLog($"[BOOTSTRAP] Warning: Unknown slot '{patch.Slot}' for '{patch.Target}'");
            return;
        }
        
        // Resolve the value reference
        int valueOop = ResolveReference(patch.Value ?? 0);
        
        // Apply the patch
        vm.PrimitiveSetSlot(targetOop, slotIndex, valueOop);
        BootstrapLog($"[BOOTSTRAP] Patched '{patch.Target}'.{patch.Slot} = {patch.Value}");
    }
    
    /// <summary>
    /// Execute sendMessage operation.
    /// </summary>
    private void ExecuteSendMessage(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Receiver) || string.IsNullOrEmpty(op.Selector))
            throw new InvalidOperationException("sendMessage requires receiver and selector");
            
        // Get receiver OOP
        int receiverOop;
        if (!namedObjects.TryGetValue(op.Receiver, out receiverOop))
        {
            throw new InvalidOperationException($"Receiver '{op.Receiver}' not found");
        }
        
        // Intern the selector
        var selectorOop = vm.PrimitiveInternSymbol(op.Selector);
        
        // Process arguments
        var argOops = new List<int>();
        if (op.Args != null)
        {
            foreach (var arg in op.Args)
            {
                int argOop;
                
                if (arg == null)
                {
                    argOop = vm.PrimitiveNil();
                }
                else if (arg is int intVal)
                {
                    argOop = vm.PrimitiveNewSmallInteger(intVal);
                }
                else if (arg is string strVal)
                {
                    // Check if it's a named object reference
                    if (namedObjects.TryGetValue(strVal, out var namedOop))
                    {
                        argOop = namedOop;
                    }
                    else
                    {
                        // Treat as a literal string
                        argOop = vm.PrimitiveInternString(strVal);
                    }
                }
                else if (arg is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        argOop = vm.PrimitiveNewSmallInteger(jsonElement.GetInt32());
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var str = jsonElement.GetString()!;
                        if (namedObjects.TryGetValue(str, out var namedOop))
                        {
                            argOop = namedOop;
                        }
                        else
                        {
                            argOop = vm.PrimitiveInternString(str);
                        }
                    }
                    else
                    {
                        argOop = vm.PrimitiveNil();
                    }
                }
                else
                {
                    argOop = vm.PrimitiveNil();
                }
                
                argOops.Add(argOop);
            }
        }
        
        // Send the message
        var resultOop = vm.PrimitiveSendMessage(receiverOop, selectorOop, argOops.ToArray());
        
        // Store result if named
        if (!string.IsNullOrEmpty(op.Result))
        {
            namedObjects[op.Result] = resultOop;
            BootstrapLog($"[BOOTSTRAP] Sent {op.Receiver}>>{op.Selector} -> '{op.Result}' (OOP {resultOop})");
        }
        else
        {
            BootstrapLog($"[BOOTSTRAP] Sent {op.Receiver}>>{op.Selector} -> OOP {resultOop}");
        }
    }
    
    /// <summary>
    /// Execute assert operation for testing.
    /// </summary>
    private void ExecuteAssert(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Target))
            throw new InvalidOperationException("assert requires target");
            
        if (!namedObjects.TryGetValue(op.Target, out var targetOop))
            throw new InvalidOperationException($"Assert failed: target '{op.Target}' not found");
            
        var obj = ObjectMemory.FromOop(targetOop);
        if (obj == null)
            throw new InvalidOperationException($"Assert failed: object at OOP {targetOop} is null");
            
        // If type is specified, verify it
        if (!string.IsNullOrEmpty(op.Type))
        {
            // TODO: Verify the object's class matches the expected type
            BootstrapLog($"[BOOTSTRAP] Assert: '{op.Target}' exists (type check for '{op.Type}' not implemented)");
        }
        else
        {
            BootstrapLog($"[BOOTSTRAP] Assert: '{op.Target}' exists at OOP {targetOop}");
        }
    }
    
    /// <summary>
    /// Execute createInstance operation - create an instance of a class.
    /// </summary>
    private void ExecuteCreateInstance(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.ClassName) || string.IsNullOrEmpty(op.Result))
            throw new InvalidOperationException("createInstance requires class and result");
            
        // Look up the class
        int classOop;
        if (!namedObjects.TryGetValue(op.ClassName, out classOop))
        {
            // Try VM lookup
            classOop = vm.PrimitiveLookupClass(op.ClassName);
            if (classOop < 0)
                throw new InvalidOperationException($"Class '{op.ClassName}' not found for createInstance");
        }
        
        // Create instance
        var instanceOop = vm.PrimitiveNewInstance(classOop);
        namedObjects[op.Result] = instanceOop;
        
        BootstrapLog($"[BOOTSTRAP] Created instance '{op.Result}' of class '{op.ClassName}' -> OOP {instanceOop}");
    }
    
    /// <summary>
    /// Execute createLargeInstance operation - create an instance with specific size.
    /// </summary>
    private void ExecuteCreateLargeInstance(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.ClassName) || string.IsNullOrEmpty(op.Result))
            throw new InvalidOperationException("createLargeInstance requires class and result");
            
        int size = op.Size ?? 10; // Default size if not specified
            
        // Look up the class
        int classOop;
        if (!namedObjects.TryGetValue(op.ClassName, out classOop))
        {
            // Try VM lookup
            classOop = vm.PrimitiveLookupClass(op.ClassName);
            if (classOop < 0)
                throw new InvalidOperationException($"Class '{op.ClassName}' not found for createLargeInstance");
        }
        
        // Create instance with specific size
        // Use PrimitiveNewArray to create a properly sized object
        var instanceOop = vm.PrimitiveNewArray(size);
        
        // Set the class pointer
        vm.PrimitiveSetClass(instanceOop, classOop);
        
        // Initialize slots for dictionary format
        // Dictionary format: [tally, key1, value1, key2, value2, ...]
        // Slot 0: tally (initially 0)
        // Slots 1+: keys and values (initially nil)
        if (op.ClassName == "SystemDictionary" || op.ClassName == "Dictionary")
        {
            // For dictionaries, slot 0 is the tally (count of entries)
            var zeroOop = vm.PrimitiveNewSmallInteger(0);
            BootstrapLog($"[BOOTSTRAP] Setting dictionary tally (slot 0) to SmallInteger 0 (OOP {zeroOop})");
            vm.PrimitiveArrayAtPut(instanceOop, 0, zeroOop);
            
            // Initialize remaining slots to nil
            var nilOop = vm.PrimitiveNil();
            BootstrapLog($"[BOOTSTRAP] Initializing remaining {size-1} slots to nil (OOP {nilOop})");
            for (int i = 1; i < size; i++)
            {
                vm.PrimitiveArrayAtPut(instanceOop, i, nilOop);
            }
        }
        else
        {
            // For non-dictionaries, initialize all slots to nil
            var nilOop = vm.PrimitiveNil();
            for (int i = 0; i < size; i++)
            {
                vm.PrimitiveArrayAtPut(instanceOop, i, nilOop);
            }
        }
        
        namedObjects[op.Result] = instanceOop;
        
        BootstrapLog($"[BOOTSTRAP] Created large instance '{op.Result}' of class '{op.ClassName}' with size {size} -> OOP {instanceOop}");
    }
    
    /// <summary>
    /// Execute dictionaryAtPut operation - add entry to a dictionary.
    /// </summary>
    private void ExecuteDictionaryAtPut(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Dictionary) || string.IsNullOrEmpty(op.Key) || string.IsNullOrEmpty(op.Value?.ToString()))
            throw new InvalidOperationException("dictionaryAtPut requires dictionary, key, and value");
            
        // Get dictionary OOP
        int dictOop;
        if (!namedObjects.TryGetValue(op.Dictionary, out dictOop))
        {
            throw new InvalidOperationException($"Dictionary '{op.Dictionary}' not found");
        }
        
        // Intern key as symbol
        var keyOop = vm.PrimitiveInternSymbol(op.Key);
        
        // Get value OOP
        var valueStr = op.Value?.ToString() ?? "";
        if (string.IsNullOrEmpty(valueStr))
            throw new InvalidOperationException("dictionaryAtPut requires a non-empty value");
            
        int valueOop;
        if (!namedObjects.TryGetValue(valueStr, out valueOop))
        {
            // Try VM lookup
            valueOop = vm.PrimitiveLookupClass(valueStr);
            if (valueOop < 0)
                throw new InvalidOperationException($"Value '{valueStr}' not found for dictionaryAtPut");
        }
        
        // Use primitive 461 (Dictionary at:put:)
        vm.PrimitiveDictionaryAtPut(dictOop, keyOop, valueOop);
        
        BootstrapLog($"[BOOTSTRAP] Added {op.Key} -> {valueStr} to dictionary '{op.Dictionary}'");
    }
}

/// <summary>
/// Bootstrap configuration from JSON.
/// </summary>
public class BootstrapConfig
{
    public string? Version { get; set; }
    public string? Phase { get; set; }
    public string? Description { get; set; }
    public List<string>? Comments { get; set; }
    public List<BootstrapOperation>? Operations { get; set; }
}

/// <summary>
/// Phase 2 bootstrap configuration.
/// </summary>
public class Phase2BootstrapConfig
{
    public string? Phase { get; set; }
    public List<BootstrapClassDefinition>? Classes { get; set; }
    public List<BootstrapPatch>? Patches { get; set; }
}

/// <summary>
/// Phase 2 manifest configuration with explicit objects.
/// </summary>
public class BootstrapManifestConfig
{
    public string? Phase { get; set; }
    public string? Description { get; set; }
    public List<BootstrapClassDefinition>? Classes { get; set; }
    public List<BootstrapObjectDefinition>? Objects { get; set; }
    public List<BootstrapPatch>? Patches { get; set; }
}

/// <summary>
/// Object definition for Phase 2 manifest.
/// </summary>
public class BootstrapObjectDefinition
{
    public string? Id { get; set; }
    [JsonPropertyName("class")]
    public string? ClassName { get; set; }
    [JsonPropertyName("className")]
    public string? ClassNameAlternate { get; set; } // Allow both "class" and "className" 
    public Dictionary<string, object>? Fields { get; set; }
    public List<object>? Elements { get; set; } // For arrays
    public object? Value { get; set; } // For SmallInteger and other immediate values
    public int? Size { get; set; } // For variable-sized objects like dictionaries
}

/// <summary>
/// Class definition for Phase 2.
/// </summary>
public class BootstrapClassDefinition
{
    public string? Name { get; set; }
    public string? Superclass { get; set; }
    public List<string>? InstanceVariables { get; set; }
    public List<BootstrapMethodDefinition>? Methods { get; set; }
    public List<BootstrapMethodDefinition>? ClassMethods { get; set; }
    public bool VariableSize { get; set; }
}

/// <summary>
/// Method definition for Phase 2.
/// </summary>
public class BootstrapMethodDefinition
{
    public string? Selector { get; set; }
    public string? Source { get; set; }
    public List<int>? Bytecode { get; set; }
    public List<string>? Literals { get; set; }
    public int? Primitive { get; set; }
    public int ArgumentCount { get; set; }
    public int TemporaryCount { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Patch operation for Phase 2.
/// </summary>
public class BootstrapPatch
{
    public string? Target { get; set; }
    public string? Slot { get; set; }
    public object? Value { get; set; }
    public string? Operation { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// A single bootstrap operation.
/// </summary>
public class BootstrapOperation
{
    public string? Op { get; set; }
    public string? Result { get; set; }
    public string? Target { get; set; }
    public string? Slot { get; set; }
    public object? Value { get; set; }  // Changed to object? to handle null, string, or array
    public string? Name { get; set; }
    public string? Selector { get; set; }
    public string? Source { get; set; }
    public int? Size { get; set; }
    public string? Comment { get; set; }
    public string? Receiver { get; set; }  // For sendMessage
    public List<object>? Args { get; set; }  // For sendMessage
    public string? Type { get; set; }  // For assert
    public string? ClassName { get; set; }  // For createInstance (use ClassName instead of Class to avoid keyword)
    public string? Dictionary { get; set; }  // For dictionaryAtPut
    public string? Key { get; set; }  // For dictionaryAtPut
    public int Primitive { get; set; }  // For defineMethod with primitive
    public string? Class { get; set; }  // For createMethodDictionary
    public int? Capacity { get; set; }  // For createMethodDictionary
}