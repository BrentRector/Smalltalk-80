using System;
using System.Collections.Generic;

namespace Smalltalk80.Bootstrap;

/// <summary>
/// Adapter that implements ISmalltalkVM by delegating to VirtualMachine and ObjectMemory.
/// Provides primitive operations for bootstrap to create real Smalltalk objects.
/// </summary>
public class VMPrimitiveAdapter : ISmalltalkVM
{
    private readonly VirtualMachine vm;
    private readonly Dictionary<string, int> globals = new();
    private readonly Dictionary<string, int> classes = new();
    
    public VMPrimitiveAdapter(VirtualMachine vm)
    {
        this.vm = vm;
    }
    
    /// <summary>
    /// Create a new class object (8 slots for full class).
    /// </summary>
    public int PrimitiveNewClass()
    {
        // Classes have 8 slots:
        // [0] superclass, [1] methodDict, [2] format
        // [3] instanceVariables, [4] organization, [5] name
        // [6] classPool, [7] sharedPools
        var classObj = new SmalltalkObject(null, 8);
        return classObj.Oop;
    }
    
    /// <summary>
    /// Create a new metaclass object (6 slots).
    /// </summary>
    public int PrimitiveNewMetaclass()
    {
        // Metaclasses have 6 slots:
        // [0] superclass, [1] methodDict, [2] format
        // [3] instanceVariables, [4] organization, [5] thisClass
        var metaclassObj = new SmalltalkObject(null, 6);
        return metaclassObj.Oop;
    }
    
    /// <summary>
    /// Create a new instance of a class.
    /// </summary>
    public int PrimitiveNewInstance(int classId)
    {
        var classObj = ObjectMemory.FromOop(classId);
        if (classObj == null)
            throw new InvalidOperationException($"Class with OOP {classId} not found");
            
        // For now, create a basic instance with no fields
        // In a full implementation, we'd look at the class's instance size
        var instance = new SmalltalkObject(classObj, 0);
        return instance.Oop;
    }
    
    /// <summary>
    /// Set slot value at a specific index.
    /// </summary>
    public void PrimitiveSetSlot(int objectId, int slotIndex, int valueId)
    {
        var obj = ObjectMemory.FromOop(objectId);
        if (obj == null)
            throw new InvalidOperationException($"Object with OOP {objectId} not found");
            
        if (slotIndex < 0 || slotIndex >= obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Slot index {slotIndex} out of range for object with {obj.Fields.Length} slots");
            
        var value = valueId == 0 ? null : ObjectMemory.FromOop(valueId);
        obj.SetField(slotIndex, value);
    }
    
    /// <summary>
    /// Set the class pointer of an object.
    /// </summary>
    public void PrimitiveSetClass(int objectId, int classId)
    {
        var obj = ObjectMemory.FromOop(objectId);
        if (obj == null)
            throw new InvalidOperationException($"Object with OOP {objectId} not found");
            
        var classObj = ObjectMemory.FromOop(classId);
        obj.ClassPointer = classObj;
    }
    
    /// <summary>
    /// Intern a symbol and return its OOP.
    /// </summary>
    public int PrimitiveInternSymbol(string name)
    {
        // For now, create a simple string object to represent the symbol
        // In a full implementation, symbols would be properly interned
        var symbol = new SmalltalkObject(null, 1);
        symbol.SetField(0, name);
        return symbol.Oop;
    }
    
    /// <summary>
    /// Create a new array.
    /// </summary>
    public int PrimitiveNewArray(int size)
    {
        var array = new SmalltalkObject(null, size);
        return array.Oop;
    }
    
    /// <summary>
    /// Set array element at index.
    /// </summary>
    public void PrimitiveArrayAtPut(int arrayId, int index, int valueId)
    {
        PrimitiveSetSlot(arrayId, index, valueId);
    }
    
    /// <summary>
    /// Create a new dictionary.
    /// </summary>
    public int PrimitiveNewDictionary()
    {
        // A simple dictionary representation with capacity for entries
        var dict = new SmalltalkObject(null, 32);
        return dict.Oop;
    }
    
    /// <summary>
    /// Add a key-value pair to a dictionary.
    /// </summary>
    public void PrimitiveDictionaryAtPut(int dictId, int keyId, int valueId)
    {
        // Simplified: just store in slots for now
        // A real implementation would handle hash tables properly
        var dict = ObjectMemory.FromOop(dictId);
        if (dict == null)
            throw new InvalidOperationException($"Dictionary with OOP {dictId} not found");
            
        // Find an empty slot or replace existing key
        // This is a very simplified implementation
        for (int i = 0; i < dict.Fields.Length; i += 2)
        {
            if (dict.Fields[i] == null || (dict.Fields[i] is SmalltalkObject key && key.Oop == keyId))
            {
                dict.SetField(i, ObjectMemory.FromOop(keyId));
                dict.SetField(i + 1, ObjectMemory.FromOop(valueId));
                break;
            }
        }
    }
    
    /// <summary>
    /// Compile and install a method in a class.
    /// </summary>
    public void PrimitiveInstallMethod(int classId, string selector, string sourceCode)
    {
        // For bootstrap, we'll skip actual compilation
        // Methods will be compiled later when the compiler is available
        Console.WriteLine($"[BOOTSTRAP] Method {selector} queued for installation in class {classId}");
    }
    
    /// <summary>
    /// Get the nil object OOP.
    /// </summary>
    public int PrimitiveNil()
    {
        // Return 0 as the nil OOP
        return 0;
    }
    
    /// <summary>
    /// Register a global variable.
    /// </summary>
    public void PrimitiveRegisterGlobal(string name, int valueId)
    {
        globals[name] = valueId;
        
        // Also track classes separately for easy lookup
        if (name.Length > 0 && char.IsUpper(name[0]))
        {
            classes[name] = valueId;
        }
        
        Console.WriteLine($"[BOOTSTRAP] Registered global '{name}' -> OOP {valueId}");
    }
    
    /// <summary>
    /// Get a registered class by name.
    /// </summary>
    public int PrimitiveLookupClass(string className)
    {
        return classes.TryGetValue(className, out var oop) ? oop : -1;
    }
    
    /// <summary>
    /// Look up a global variable by name.
    /// </summary>
    public int PrimitiveLookupGlobal(string name)
    {
        return globals.TryGetValue(name, out var oop) ? oop : -1;
    }
    
    /// <summary>
    /// Intern a string and return its OOP.
    /// </summary>
    public int PrimitiveInternString(string value)
    {
        // Create a string object
        var str = new SmalltalkObject(null, 1);
        str.SetField(0, value);
        return str.Oop;
    }
    
    /// <summary>
    /// Execute bytecode (not used in Phase 1).
    /// </summary>
    public int ExecuteBytecode(int receiverId, byte[] bytecode, int[] argumentIds)
    {
        throw new NotImplementedException("ExecuteBytecode is not used in Phase 1 bootstrap");
    }
}