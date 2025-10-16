using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Multi-Pass Phase Y: Instance Variable Initialization
/// 
/// Initializes all instance variables to nil after the nil singleton has been created.
/// This is the final initialization pass of the multi-pass architecture.
/// 
/// For each allocated object:
/// 1. Get the number of instance variables from its class definition
/// 2. Initialize all instance variables (slots 1..N) to nil
/// 3. Skip objects that already have their instance variables set (like pre-initialized objects)
/// </summary>
public class PhaseY_InstanceVariableInitialization
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;

    public PhaseY_InstanceVariableInitialization(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
    }

    public void Run()
    {
        int instanceVariablesInitialized = 0;
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);

        // Step 1: Initialize class object instance variables
        // All class objects should have their instance variables initialized to nil
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;
            
            // Get the number of instance variables for this type of class object
            string classOfClassName = cls.IsMetaclass ? "Metaclass" : "Class";
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(classOfClassName, _bundle);
            
            // Initialize ONLY uninitialized instance variables to nil (slots 1..N, slot 0 is class reference)
            // Don't overwrite instance variables that were already set in Phase 4 (like the name field)
            for (int i = 0; i < instVarCount; i++)
            {
                var currentOOP = _allocator.GetInstanceVariable(classOOP, i);
                if (currentOOP.RawValue == 0) // Only set if still uninitialized
                {
                    _allocator.SetInstanceVariable(classOOP, i, nilOOP);
                    instanceVariablesInitialized++;
                }
            }
        }

        // Step 2: Initialize instance variables for singleton objects
        InitializeSingletonInstanceVariables("nil", "UndefinedObject", nilOOP);
        InitializeSingletonInstanceVariables("true", "True", nilOOP);
        InitializeSingletonInstanceVariables("false", "False", nilOOP);
        InitializeSingletonInstanceVariables("ProcessorScheduler", "ProcessorScheduler", nilOOP);

        // Step 3: Initialize instance variables for other allocated objects and set JSON values
        // These objects were allocated with class-specific instance variable counts
        // but their instance variables haven't been initialized to nil yet
        foreach (var obj in _bundle.Objects)
        {
            if (!_localIdToObjectId.TryGetValue(obj.LocalId, out SmalltalkOOP objectOOP))
                continue;
                

            string className = _bundle.Symbols[obj.ClassNameIndex];
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);
            
            // First: Initialize ALL instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                instanceVariablesInitialized++;
            }
            
            // Second: Override with JSON field values
            for (int i = 0; i < obj.Fields.Length; i++)
            {
                var valueOOP = ResolveFieldValueAsOOP(obj.Fields[i]);
                _allocator.SetInstanceVariable(objectOOP, i, valueOOP);
            }
            
            // Third: Set indexed fields if any
            for (int i = 0; i < obj.IndexedFields.Length; i++)
            {
                var valueOOP = ResolveFieldValueAsOOP(obj.IndexedFields[i]);
                _allocator.SetIndexedOOP(objectOOP, i, valueOOP);
            }
        }

        // Step 4: Initialize SystemDictionary and other special objects
        InitializeSpecialObjectInstanceVariables(nilOOP);

    }

    private void InitializeSingletonInstanceVariables(string globalName, string className, SmalltalkOOP nilOOP)
    {
        if (GlobalRegistry.TryGet(globalName, out int objectId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);
            var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
            
            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                var currentOOP = _allocator.GetInstanceVariable(objectOOP, i);
                if (currentOOP.RawValue == 0) // Only set if still uninitialized
                {
                    _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                }
            }
        }
    }

    private void InitializeSpecialObjectInstanceVariables(SmalltalkOOP nilOOP)
    {
        // Initialize SystemDictionary instance variables
        if (GlobalRegistry.TryGet("Smalltalk", out int systemDictId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("SystemDictionary", _bundle);
            var systemDictOOP = SmalltalkOOP.FromObjectTableIndex(systemDictId);
            
            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                var currentOOP = _allocator.GetInstanceVariable(systemDictOOP, i);
                if (currentOOP.RawValue == 0) // Only set if still uninitialized
                {
                    _allocator.SetInstanceVariable(systemDictOOP, i, nilOOP);
                }
            }
        }

        // Note: USTable (Array) has 0 instance variables, so no initialization needed
        // Symbols also have 0 instance variables in the standard Smalltalk-80 implementation
    }

    /// <summary>
    /// Resolves a field value from JSON to a SmalltalkOOP.
    /// </summary>
    private SmalltalkOOP ResolveFieldValueAsOOP(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
            "true" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.TrueObjectId),
            "false" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.FalseObjectId),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => SmalltalkOOP.FromObjectTableIndex(AllocateFloat(GetFloatValue(value.Value!))),
            "symbolRef" => SmalltalkOOP.FromObjectTableIndex(InternSymbol(_bundle.Symbols[value.Index!.Value])),
            "stringRef" => SmalltalkOOP.FromObjectTableIndex(InternString(_bundle.Strings[value.Index!.Value])),
            "objectRef" => _localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP) ? objOOP : SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
            "character" => SmalltalkOOP.FromObjectTableIndex(AllocateCharacter(value.CodePoint!.Value)),
            "bytes" => SmalltalkOOP.FromObjectTableIndex(AllocateByteArray(value.Encoding!, value.Data!)),
            _ => throw new InvalidOperationException($"Unknown field type: {value.Type}")
        };
    }

    /// <summary>
    /// Resolves a field value from JSON to an object ID.
    /// </summary>
    private int ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => GlobalRegistry.NilObjectId,
            "true" => GlobalRegistry.TrueObjectId,
            "false" => GlobalRegistry.FalseObjectId,
            "smallInt" => EncodeSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => AllocateFloat(GetFloatValue(value.Value!)),
            "symbolRef" => InternSymbol(_bundle.Symbols[value.Index!.Value]),
            "stringRef" => InternString(_bundle.Strings[value.Index!.Value]),
            "objectRef" => _localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP) ? objOOP.ToObjectTableIndex() : GlobalRegistry.NilObjectId,  // Forward reference: use nil as placeholder, will be patched later
            "character" => AllocateCharacter(value.CodePoint!.Value),
            "bytes" => AllocateByteArray(value.Encoding!, value.Data!),
            _ => throw new InvalidOperationException($"Unknown field type: {value.Type}")
        };
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    private int GetIntValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    /// <summary>
    /// Extracts a float value from a JsonElement or double.
    /// </summary>
    private double GetFloatValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetDouble(),
            double d => d,
            float f => (double)f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Encodes a SmallInteger using SObjectRef format.
    /// </summary>
    private int EncodeSmallInteger(int value)
    {
        return SmalltalkOOP.FromSmallInteger(value).RawValue;
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private int AllocateFloat(double value)
    {
        var floatOOP = _allocator.AllocateByteArray("Float", 8);
        // Set Float class reference (since Phase Y runs after Phase X)
        int floatClassId = GlobalRegistry.LookupClassObjectId("Float");
        var floatClassOOP = SmalltalkOOP.FromObjectTableIndex(floatClassId);
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            _allocator.SetIndexedByte(floatOOP, i, bytes[i]);
        }
        return floatOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Interns a symbol by finding it in the symbol table.
    /// </summary>
    private int InternSymbol(string name)
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetObjectId(i);
            }
        }
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table");
    }

    /// <summary>
    /// Interns a string by allocating a String object.
    /// </summary>
    private int InternString(string value)
    {
        var stringOOP = _allocator.AllocateString(value);
        // Set String class reference (since Phase Y runs after Phase X)
        int stringClassId = GlobalRegistry.LookupClassObjectId("String");
        var stringClassOOP = SmalltalkOOP.FromObjectTableIndex(stringClassId);
        _allocator.SetObjectClass(stringOOP, stringClassOOP);
        
        int objectId = stringOOP.ToObjectTableIndex();
        
        
        return objectId;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private int AllocateCharacter(int codePoint)
    {
        var characterOOP = _allocator.AllocateFixedObject("Character");
        // Set Character class reference (since Phase Y runs after Phase X)
        int characterClassId = GlobalRegistry.LookupClassObjectId("Character");
        var characterClassOOP = SmalltalkOOP.FromObjectTableIndex(characterClassId);
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);
        return characterOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private int AllocateByteArray(string encoding, string data)
    {
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes);
        // Set ByteArray class reference (since Phase Y runs after Phase X)
        int byteArrayClassId = GlobalRegistry.LookupClassObjectId("ByteArray");
        var byteArrayClassOOP = SmalltalkOOP.FromObjectTableIndex(byteArrayClassId);
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);
        return byteArrayOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Converts a hex string to bytes.
    /// </summary>
    private byte[] ConvertHexStringToBytes(string hex)
    {
        int length = hex.Length / 2;
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}