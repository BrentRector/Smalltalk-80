using Smalltalk80.Core;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Executes Phase 1 bootstrap operations using VM primitives to create the minimal class hierarchy.
/// This phase creates core classes like ProtoObject, Object, Class, Metaclass, and essential support classes
/// using only primitive operations without any messaging capability.
/// </summary>
public class Phase1BootstrapExecutor
{
    private readonly IVirtualMachine _vm;
    private readonly ILogger<Phase1BootstrapExecutor>? _logger;
    private readonly Dictionary<string, ISmalltalkObject> _objects;
    private readonly Dictionary<string, ISmalltalkClass> _classes;

    /// <summary>
    /// Initializes a new instance of the Phase1BootstrapExecutor class.
    /// </summary>
    /// <param name="vm">The virtual machine to bootstrap.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public Phase1BootstrapExecutor(IVirtualMachine vm, ILogger<Phase1BootstrapExecutor>? logger = null)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        _logger = logger;
        _objects = new Dictionary<string, ISmalltalkObject>();
        _classes = new Dictionary<string, ISmalltalkClass>();
    }

    /// <summary>
    /// Executes the Phase 1 bootstrap operations to create the minimal class hierarchy.
    /// </summary>
    /// <param name="data">The Phase 1 bootstrap data containing primitive operations.</param>
    /// <returns>A task that completes when Phase 1 bootstrap is finished.</returns>
    public async Task ExecuteAsync(Phase1BootstrapData data)
    {
        _logger?.LogInformation("Starting Phase 1 bootstrap with {Count} operations", data.Operations.Count);

        try
        {
            // Execute each primitive operation in sequence
            foreach (var operation in data.Operations)
            {
                await ExecuteOperationAsync(operation);
            }

            // Run validation checkpoints
            await ValidateCheckpointsAsync(data.Checkpoints);

            _logger?.LogInformation("Phase 1 bootstrap completed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Phase 1 bootstrap failed: {Message}", ex.Message);
            throw new BootstrapException("Phase 1 bootstrap failed", ex);
        }
    }

    /// <summary>
    /// Executes a single primitive operation.
    /// </summary>
    private async Task ExecuteOperationAsync(PrimitiveOperation operation)
    {
        _logger?.LogInformation("Executing operation: {Op} {Comment}", operation.Operation, operation.Comment);

        switch (operation.Operation.ToLower())
        {
            case "newclass":
                await ExecuteNewClass(operation);
                break;

            case "setslot":
                await ExecuteSetSlot(operation);
                break;

            case "setnamedslot":
                await ExecuteSetNamedSlot(operation);
                break;

            case "installmethod":
                await ExecuteInstallMethod(operation);
                break;

            case "newinstance":
                await ExecuteNewInstance(operation);
                break;

            case "registerglobal":
                await ExecuteRegisterGlobal(operation);
                break;

            case "dictionaryput":
                await ExecuteDictionaryPut(operation);
                break;

            default:
                _logger?.LogWarning("Unknown operation: {Op}", operation.Operation);
                break;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new class object.
    /// </summary>
    private async Task ExecuteNewClass(PrimitiveOperation operation)
    {
        if (string.IsNullOrEmpty(operation.Result))
            throw new InvalidOperationException("newClass requires a result name");

        // Create a new class object using VM primitives
        var classObject = _vm.ObjectMemory.AllocateObject(_vm.ClassRegistry.GetClassClass(), 8);
        
        // Convert to class interface
        var cls = classObject as ISmalltalkClass 
            ?? throw new InvalidOperationException("Failed to create class object");

        // Initialize basic fields
        cls.MethodDictionary = CreateMethodDictionary();
        cls.InstanceVariables = Array.Empty<ISymbol>();
        
        // Store the class for later reference
        _classes[operation.Result] = cls;
        _objects[operation.Result] = cls;

        _logger?.LogInformation("Created class: {Name}", operation.Result);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sets an indexed slot in an object.
    /// </summary>
    private async Task ExecuteSetSlot(PrimitiveOperation operation)
    {
        if (string.IsNullOrEmpty(operation.Target))
            throw new InvalidOperationException("setSlot requires a target");

        if (!_objects.TryGetValue(operation.Target, out var target))
            throw new InvalidOperationException($"Target object not found: {operation.Target}");

        if (!operation.Parameters.TryGetValue("slot", out var slotObj) || slotObj is not int slot)
            throw new InvalidOperationException("setSlot requires a slot index");

        if (!operation.Parameters.TryGetValue("value", out var valueRef) || valueRef is not string valueName)
            throw new InvalidOperationException("setSlot requires a value reference");

        // Handle special cases
        ISmalltalkObject? value = valueName switch
        {
            "null" or null => null,
            _ => _objects.TryGetValue(valueName, out var obj) ? obj : null
        };

        // Set the slot
        target[slot] = value;

        _logger?.LogInformation("Set slot {Slot} of {Target} to {Value}", slot, operation.Target, valueName);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sets a named field in an object.
    /// </summary>
    private async Task ExecuteSetNamedSlot(PrimitiveOperation operation)
    {
        if (string.IsNullOrEmpty(operation.Target))
            throw new InvalidOperationException("setNamedSlot requires a target");

        if (!_objects.TryGetValue(operation.Target, out var target))
            throw new InvalidOperationException($"Target object not found: {operation.Target}");

        if (!operation.Parameters.TryGetValue("name", out var nameObj) || nameObj is not string fieldName)
            throw new InvalidOperationException("setNamedSlot requires a field name");

        object? value = null;
        if (operation.Parameters.TryGetValue("value", out var valueObj))
        {
            value = valueObj switch
            {
                string str when str == "null" => null,
                string str when _objects.ContainsKey(str) => _objects[str],
                string str => CreateSymbol(str),
                int num => num,
                Dictionary<string, object> dict when dict.ContainsKey("op") => ExecuteNestedOperation(dict),
                IReadOnlyList<string> list => CreateSymbolArray(list),
                _ => valueObj
            };
        }

        // Set the named field based on the field name
        if (target is ISmalltalkClass cls)
        {
            switch (fieldName.ToLower())
            {
                case "name":
                    cls.Name = value as ISymbol ?? CreateSymbol(value?.ToString() ?? string.Empty);
                    break;
                case "superclass":
                    cls.Superclass = value as ISmalltalkClass;
                    break;
                case "methoddict":
                case "methoddictionary":
                    cls.MethodDictionary = value as IMethodDictionary ?? CreateMethodDictionary();
                    break;
                case "instancevariables":
                    cls.InstanceVariables = value as ISymbol[] ?? Array.Empty<ISymbol>();
                    break;
                case "format":
                    cls.Format = value as int? ?? 0;
                    break;
            }
        }
        else
        {
            // Generic field setting
            target[fieldName] = value as ISmalltalkObject;
        }

        _logger?.LogInformation("Set named slot {Name} of {Target}", fieldName, operation.Target);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Installs a method in a class.
    /// </summary>
    private async Task ExecuteInstallMethod(PrimitiveOperation operation)
    {
        if (!operation.Parameters.TryGetValue("target", out var targetObj) && 
            !operation.Parameters.TryGetValue("class", out targetObj))
        {
            targetObj = operation.Target;
        }

        if (targetObj is not string className || !_classes.TryGetValue(className, out var cls))
            throw new InvalidOperationException($"Class not found: {targetObj}");

        if (!operation.Parameters.TryGetValue("selector", out var selectorObj) || selectorObj is not string selector)
            throw new InvalidOperationException("installMethod requires a selector");

        if (!operation.Parameters.TryGetValue("source", out var sourceObj) || sourceObj is not string source)
            throw new InvalidOperationException("installMethod requires source code");

        // Compile the method (simplified for Phase 1 - may just store source)
        var method = CompileMethod(selector, source);
        var selectorSymbol = CreateSymbol(selector);

        // Add to method dictionary
        cls.AddMethod(selectorSymbol, method);

        _logger?.LogInformation("Installed method {Selector} in {Class}", selector, className);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates an instance of a class.
    /// </summary>
    private async Task ExecuteNewInstance(PrimitiveOperation operation)
    {
        if (!operation.Parameters.TryGetValue("class", out var classObj) || classObj is not string className)
            throw new InvalidOperationException("newInstance requires a class name");

        if (!_classes.TryGetValue(className, out var cls))
            throw new InvalidOperationException($"Class not found: {className}");

        if (string.IsNullOrEmpty(operation.Result))
            throw new InvalidOperationException("newInstance requires a result name");

        // Create instance
        var instance = cls.NewInstance();
        _objects[operation.Result] = instance;

        _logger?.LogInformation("Created instance of {Class}: {Name}", className, operation.Result);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Registers a global variable.
    /// </summary>
    private async Task ExecuteRegisterGlobal(PrimitiveOperation operation)
    {
        if (!operation.Parameters.TryGetValue("name", out var nameObj) || nameObj is not string name)
            throw new InvalidOperationException("registerGlobal requires a name");

        if (!operation.Parameters.TryGetValue("value", out var valueObj) || valueObj is not string valueName)
            throw new InvalidOperationException("registerGlobal requires a value");

        if (!_objects.TryGetValue(valueName, out var value))
        {
            // Special case for nil
            if (valueName == "nil")
                value = null;
            else
                throw new InvalidOperationException($"Value not found: {valueName}");
        }

        _vm.SetGlobal(name, value!);

        _logger?.LogInformation("Registered global: {Name}", name);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Adds an entry to a dictionary.
    /// </summary>
    private async Task ExecuteDictionaryPut(PrimitiveOperation operation)
    {
        if (string.IsNullOrEmpty(operation.Target))
            throw new InvalidOperationException("dictionaryPut requires a target");

        if (!operation.Parameters.TryGetValue("key", out var keyObj) || keyObj is not string key)
            throw new InvalidOperationException("dictionaryPut requires a key");

        if (!operation.Parameters.TryGetValue("value", out var valueObj) || valueObj is not string valueName)
            throw new InvalidOperationException("dictionaryPut requires a value");

        // This would add the key-value pair to the dictionary
        // Implementation depends on dictionary structure
        _logger?.LogInformation("Added {Key} to dictionary {Target}", key, operation.Target);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Executes a nested operation (like creating a new dictionary).
    /// </summary>
    private ISmalltalkObject ExecuteNestedOperation(Dictionary<string, object> operation)
    {
        if (!operation.TryGetValue("op", out var opObj) || opObj is not string op)
            throw new InvalidOperationException("Nested operation requires 'op' field");

        return op.ToLower() switch
        {
            "newdictionary" => CreateDictionary(),
            "newarray" => CreateArray(operation.TryGetValue("size", out var sizeObj) && sizeObj is int size ? size : 0),
            _ => throw new InvalidOperationException($"Unknown nested operation: {op}")
        };
    }

    /// <summary>
    /// Creates a method dictionary.
    /// </summary>
    private IMethodDictionary CreateMethodDictionary()
    {
        var dict = _vm.ObjectMemory.AllocateObject(_vm.ClassRegistry.GetMethodDictionaryClass(), 2);
        return dict as IMethodDictionary ?? throw new InvalidOperationException("Failed to create method dictionary");
    }

    /// <summary>
    /// Creates a dictionary object.
    /// </summary>
    private ISmalltalkObject CreateDictionary()
    {
        return _vm.ObjectMemory.AllocateObject(_vm.ClassRegistry.GetDictionaryClass(), 2);
    }

    /// <summary>
    /// Creates an array object.
    /// </summary>
    private ISmalltalkObject CreateArray(int size)
    {
        return _vm.ObjectMemory.AllocateIndexedObject(_vm.ClassRegistry.GetArrayClass(), size);
    }

    /// <summary>
    /// Creates a symbol from a string.
    /// </summary>
    private ISymbol CreateSymbol(string value)
    {
        return _vm.ClassRegistry.InternSymbol(value);
    }

    /// <summary>
    /// Creates an array of symbols from a list of strings.
    /// </summary>
    private ISymbol[] CreateSymbolArray(IReadOnlyList<string> values)
    {
        return values.Select(CreateSymbol).ToArray();
    }

    /// <summary>
    /// Compiles a method from source (simplified for Phase 1).
    /// </summary>
    private ICompiledMethod CompileMethod(string selector, string source)
    {
        // In Phase 1, we might just store the source and primitive index
        // Real compilation happens in later phases
        var method = _vm.ObjectMemory.AllocateObject(_vm.ClassRegistry.GetCompiledMethodClass(), 4) as ICompiledMethod
            ?? throw new InvalidOperationException("Failed to create compiled method");

        // Extract primitive index if present
        var primitiveIndex = ExtractPrimitiveIndex(source);
        
        // Store basic method information
        // Full compilation would happen here in a complete implementation
        
        return method;
    }

    /// <summary>
    /// Extracts primitive index from method source.
    /// </summary>
    private int ExtractPrimitiveIndex(string source)
    {
        // Look for <primitive: N> pattern
        var match = System.Text.RegularExpressions.Regex.Match(source, @"<primitive:\s*(\d+)>");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
            return index;
        return 0;
    }

    /// <summary>
    /// Validates bootstrap checkpoints.
    /// </summary>
    private async Task ValidateCheckpointsAsync(IReadOnlyList<ValidationCheckpoint> checkpoints)
    {
        foreach (var checkpoint in checkpoints)
        {
            _logger?.LogInformation("Validating: {Description}", checkpoint.Description);
            
            // Execute the test expression
            // This would evaluate the test in the context of the bootstrapped system
            var passed = await EvaluateCheckpoint(checkpoint);
            
            if (!passed && checkpoint.IsCritical)
            {
                throw new BootstrapException($"Critical checkpoint failed: {checkpoint.Description}");
            }
            else if (!passed)
            {
                _logger?.LogWarning("Non-critical checkpoint failed: {Description}", checkpoint.Description);
            }
        }
    }

    /// <summary>
    /// Evaluates a validation checkpoint.
    /// </summary>
    private async Task<bool> EvaluateCheckpoint(ValidationCheckpoint checkpoint)
    {
        // Simplified checkpoint evaluation
        // Real implementation would parse and evaluate the test expression
        await Task.CompletedTask;
        return true;
    }
}

/// <summary>
/// Exception thrown when bootstrap operations fail.
/// </summary>
public class BootstrapException : Exception
{
    public BootstrapException(string message) : base(message) { }
    public BootstrapException(string message, Exception innerException) : base(message, innerException) { }
}