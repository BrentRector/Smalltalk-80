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
            
        Console.WriteLine($"[BOOTSTRAP] Executing {bootstrap.Operations?.Count ?? 0} operations from {bootstrap.Description}");
        
        if (bootstrap.Operations == null)
            return;
            
        foreach (var operation in bootstrap.Operations)
        {
            ExecuteOperation(operation);
        }
        
        Console.WriteLine($"[BOOTSTRAP] Phase 1 created {namedObjects.Count} named objects");
    }
    
    /// <summary>
    /// Execute a single bootstrap operation.
    /// </summary>
    private void ExecuteOperation(BootstrapOperation operation)
    {
        if (!string.IsNullOrEmpty(operation.Comment))
        {
            Console.WriteLine($"[BOOTSTRAP] {operation.Comment}");
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
                
            default:
                if (!string.IsNullOrEmpty(operation.Op))
                    Console.WriteLine($"[BOOTSTRAP] Warning: Unknown operation '{operation.Op}'");
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
        if (op.Result.EndsWith("Class") || op.Result == "Metaclass")
        {
            // It's a metaclass
            oop = vm.PrimitiveNewMetaclass();
            Console.WriteLine($"[BOOTSTRAP] Created metaclass '{op.Result}' -> OOP {oop}");
        }
        else if (op.Result == "nil")
        {
            // Special case for nil
            oop = vm.PrimitiveNil();
            Console.WriteLine($"[BOOTSTRAP] Created nil -> OOP {oop}");
        }
        else
        {
            // It's a regular class object
            oop = vm.PrimitiveNewClass();
            Console.WriteLine($"[BOOTSTRAP] Created class '{op.Result}' -> OOP {oop}");
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
        if (!string.IsNullOrEmpty(op.Value))
        {
            if (!namedObjects.TryGetValue(op.Value, out valueOop))
                throw new InvalidOperationException($"Value object '{op.Value}' not found");
        }
        
        // Handle special slot names
        if (op.Slot == "class")
        {
            vm.PrimitiveSetClass(targetOop, valueOop);
            Console.WriteLine($"[BOOTSTRAP] Set class of '{op.Target}' to '{op.Value}'");
        }
        else if (int.TryParse(op.Slot, out var slotIndex))
        {
            vm.PrimitiveSetSlot(targetOop, slotIndex, valueOop);
            Console.WriteLine($"[BOOTSTRAP] Set slot {slotIndex} of '{op.Target}' to '{op.Value}'");
        }
        else
        {
            Console.WriteLine($"[BOOTSTRAP] Warning: Unknown slot type '{op.Slot}'");
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
        if (!string.IsNullOrEmpty(op.Value) && op.Value != "null")
        {
            if (!namedObjects.TryGetValue(op.Value, out superclassOop))
                throw new InvalidOperationException($"Superclass '{op.Value}' not found");
        }
        
        // Superclass is at slot 0
        vm.PrimitiveSetSlot(targetOop, 0, superclassOop);
        Console.WriteLine($"[BOOTSTRAP] Set superclass of '{op.Target}' to '{op.Value ?? "nil"}'");
    }
    
    /// <summary>
    /// Execute registerGlobal operation.
    /// </summary>
    private void ExecuteRegisterGlobal(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Name) || string.IsNullOrEmpty(op.Value))
            throw new InvalidOperationException("registerGlobal requires name and value");
            
        if (!namedObjects.TryGetValue(op.Value, out var valueOop))
            throw new InvalidOperationException($"Value object '{op.Value}' not found");
            
        vm.PrimitiveRegisterGlobal(op.Name, valueOop);
    }
    
    /// <summary>
    /// Execute createSymbol operation.
    /// </summary>
    private void ExecuteCreateSymbol(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result) || string.IsNullOrEmpty(op.Value))
            throw new InvalidOperationException("createSymbol requires result and value");
            
        var oop = vm.PrimitiveInternSymbol(op.Value);
        namedObjects[op.Result] = oop;
        Console.WriteLine($"[BOOTSTRAP] Created symbol '{op.Value}' -> OOP {oop}");
    }
    
    /// <summary>
    /// Execute createString operation.
    /// </summary>
    private void ExecuteCreateString(BootstrapOperation op)
    {
        if (string.IsNullOrEmpty(op.Result) || string.IsNullOrEmpty(op.Value))
            throw new InvalidOperationException("createString requires result and value");
            
        var oop = vm.PrimitiveInternString(op.Value);
        namedObjects[op.Result] = oop;
        Console.WriteLine($"[BOOTSTRAP] Created string '{op.Value}' -> OOP {oop}");
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
        Console.WriteLine($"[BOOTSTRAP] Created array of size {size} -> OOP {oop}");
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
        Console.WriteLine($"[BOOTSTRAP] Created dictionary -> OOP {oop}");
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
/// A single bootstrap operation.
/// </summary>
public class BootstrapOperation
{
    public string? Op { get; set; }
    public string? Result { get; set; }
    public string? Target { get; set; }
    public string? Slot { get; set; }
    public string? Value { get; set; }
    public string? Name { get; set; }
    public string? Selector { get; set; }
    public string? Source { get; set; }
    public int? Size { get; set; }
    public string? Comment { get; set; }
}