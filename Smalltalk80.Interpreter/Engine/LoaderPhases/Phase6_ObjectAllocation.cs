using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 6: Object Allocation
/// Reads from the JSON bundle and allocates real Smalltalk heap objects.
/// Handles:
/// - Resolving classNameIndex → Symbol → Class
/// - Decoding fieldValue discriminated unions
/// - Allocating fixed and indexed fields
/// - Mapping localId → objectId for patching and references
/// </summary>
public class Phase6_ObjectAllocation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase6_ObjectAllocation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
    }

    public void Run()
    {
        // Create minimal dependency singletons: nil, true, false 
        // These must be created AFTER all classes are fully wired so they get correct classIds
        var nilOOP = _allocator.AllocateFixedObject("UndefinedObject");
        var trueOOP = _allocator.AllocateFixedObject("True");
        var falseOOP = _allocator.AllocateFixedObject("False");
        
        int nilId = nilOOP.ToObjectTableIndex();
        int trueId = trueOOP.ToObjectTableIndex();
        int falseId = falseOOP.ToObjectTableIndex();

        GlobalRegistry.Register("nil", nilId);
        GlobalRegistry.Register("true", trueId);
        GlobalRegistry.Register("false", falseId);

        // Initialize SystemDictionary slots with nil (created in Phase 2 but couldn't be initialized until now)
        InitializeSystemDictionaryWithNil(nilId);

        // Create ProcessorScheduler singleton
        // This is the unique process scheduler instance that manages all processes in the system
        var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
        int processorSchedulerId = processorSchedulerOOP.ToObjectTableIndex();
        GlobalRegistry.Register("ProcessorScheduler", processorSchedulerId);
        
        // Initialize ProcessorScheduler fields that should NOT be nil
        // Phase Y will later fill any remaining uninitialized fields with nil
        InitializeProcessorSchedulerFields(processorSchedulerOOP, nilOOP);

        // Note: smalltalkIndex no longer needed - was part of object ID sequencing

        // Create other bundle objects
        foreach (var obj in _bundle.Objects)
        {
            string className = _symbols[obj.ClassNameIndex];
            int classId = ResolveClassObjectId(className);
            int fixedSize = obj.Fields.Length;
            int indexedSize = obj.IndexedFields.Length;

            int objectId = AllocateObjectWithSemanticAllocator(className, obj.FormatHint, fixedSize, indexedSize);
            _localIdToObjectId[obj.LocalId] = SmalltalkOOP.FromObjectTableIndex(objectId);

            // Note: Field initialization moved to Phase Y after nil exists

            // Add pool dictionaries to SystemDictionary using semantic lookup (no sequential indexing!)
            if (obj.LocalId.StartsWith("pool:"))
            {
                string poolName = obj.LocalId.Substring(5); // Remove "pool:" prefix
                int poolNameSymbol = InternSymbol(poolName);
                int association = AllocateAssociationWithSemanticAllocator(poolNameSymbol, objectId);
                InstallAssociationInSystemDictionary(association, poolName);
            }
        }

        // Note: Special key "__smalltalk_next_index__" removed - no longer needed with SmalltalkOOP
    }

    /// <summary>
    /// Resolves a class name to its object ID.
    /// Assumes class objects were already allocated and mapped by name.
    /// </summary>
    private int ResolveClassObjectId(string className)
    {
        return GlobalRegistry.LookupClassObjectId(className);
    }

    /// <summary>
    /// Resolves a field value from the discriminated union to an object ID.
    /// Handles JsonElement types from JSON deserialization.
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
            "symbolRef" => InternSymbol(_symbols[value.Index!.Value]),
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
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    private double GetFloatValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    // All duplicate methods removed - now using ClassHierarchyUtils

    /// <summary>
    /// Allocates an object using SemanticAllocator based on format hint.
    /// </summary>
    private int AllocateObjectWithSemanticAllocator(string className, string formatHint, int fixedSize, int indexedSize)
    {
        // Map format hint to appropriate SemanticAllocator method
        var objectOOP = formatHint.ToLower() switch
        {
            "pointers" or "pointer" or "fixed" => _allocator.AllocateFixedObject(className),
            "pointerindexed" => _allocator.AllocateArray(className, indexedSize),
            "bytes" or "byte" or "byteindexed" => _allocator.AllocateByteArray(className, indexedSize),
            "words" or "word" or "wordindexed" => _allocator.AllocateObject(className, ObjectFormat.WordIndexed, indexedSize),
            "compiledmethod" => _allocator.AllocateArray(className, indexedSize),
            _ => _allocator.AllocateFixedObject(className) // Default fallback
        };

        return objectOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Sets an instance variable in an object.
    /// </summary>
    private void SetInstanceVariable(int objectId, int fieldIndex, int valueId)
    {
        var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
        var valueOOP = SmalltalkOOP.FromObjectTableIndex(valueId);
        _allocator.SetInstanceVariable(objectOOP, fieldIndex, valueOOP);
    }

    /// <summary>
    /// Sets an indexed OOP in an object.
    /// </summary>
    private void SetIndexedOOP(int objectId, int index, int valueId)
    {
        var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
        var valueOOP = SmalltalkOOP.FromObjectTableIndex(valueId);
        _allocator.SetIndexedOOP(objectOOP, index, valueOOP);
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private int InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetObjectId(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    private int InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);
        
        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }
        
        return stringOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates an Association object (key-value pair) using SemanticAllocator.
    /// </summary>
    private int AllocateAssociationWithSemanticAllocator(int keyId, int valueId)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");
        int objectId = associationOOP.ToObjectTableIndex();
        
        // Set instance variables: [0] = key, [1] = value (assuming standard Association layout)
        _allocator.SetInstanceVariable(associationOOP, 0, SmalltalkOOP.FromObjectTableIndex(keyId));
        _allocator.SetInstanceVariable(associationOOP, 1, SmalltalkOOP.FromObjectTableIndex(valueId));
        
        return objectId;
    }

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP.
    /// </summary>
    private int EncodeSmallInteger(int value)
    {
        var smallIntOOP = SmalltalkOOP.FromSmallInteger(value);
        return smallIntOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private int AllocateCharacter(int codePoint)
    {
        var characterOOP = _allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        return characterOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private int AllocateByteArray(string encoding, string data)
    {
        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes.Length);

        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _allocator.SetIndexedByte(byteArrayOOP, i, bytes[i]);
        }

        return byteArrayOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private int AllocateFloat(double value)
    {
        var floatOOP = _allocator.AllocateByteArray("Float", 8); // 8 bytes for IEEE 754 double

        // Store the double as 8 bytes
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            _allocator.SetIndexedByte(floatOOP, i, bytes[i]);
        }

        return floatOOP.ToObjectTableIndex();
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    private byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove any whitespace or separators
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");
        
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even number of characters");
            
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Creates SystemDictionary instance with proper dependency ordering.
    /// SystemDictionary is created AFTER nil exists so it can be properly initialized.
    /// </summary>
    private int CreateSystemDictionary(int nilId)
    {
        // Calculate SystemDictionary size requirements
        int instanceClassCount = _bundle.Classes.Count(c => !c.IsMetaclass);
        int poolCount = _bundle.Objects.Count(o => o.LocalId.StartsWith("pool:"));
        int systemGlobalCount = 2; // Smalltalk self-reference + USTable global
        int totalEntries = instanceClassCount + poolCount + systemGlobalCount; // 237 classes + 2 pools + 2 globals = 241
        
        // Add generous buffer for hash table efficiency
        totalEntries = Math.Max(totalEntries * 2, 500); // Ensure at least 500 slots
        
        
        // Allocate SystemDictionary instance using SemanticAllocator
        var systemDictOOP = _allocator.AllocateArray("SystemDictionary", totalEntries);
        int systemDictId = systemDictOOP.ToObjectTableIndex();
        
        // Initialize all indexed slots with nil (proper dependency ordering!)
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(nilId);
        for (int i = 0; i < totalEntries; i++)
        {
            _allocator.SetIndexedOOP(systemDictOOP, i, nilOOP);
        }
        
        return systemDictId;
    }

    /// <summary>
    /// Installs an association in SystemDictionary using semantic slot finding (no sequential indexing).
    /// Finds the first available nil slot rather than relying on brittle object ID sequencing.
    /// </summary>
    private void InstallAssociationInSystemDictionary(int associationId, string name)
    {
        var systemDictOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.SystemDictionaryId);
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        int nilId = GlobalRegistry.NilObjectId;
        
        // Find first available nil slot
        for (int i = 0; i < systemDictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(systemDictOOP, i);
            if (slotOOP.ToObjectTableIndex() == nilId)
            {
                // Found empty slot - install association
                var associationOOP = SmalltalkOOP.FromObjectTableIndex(associationId);
                _allocator.SetIndexedOOP(systemDictOOP, i, associationOOP);
                return;
            }
        }
        
        throw new InvalidOperationException($"SystemDictionary is full - cannot install {name} association");
    }

    /// <summary>
    /// Initializes SystemDictionary indexed slots with nil.
    /// SystemDictionary was created in Phase 2 but couldn't be initialized until nil object exists.
    /// </summary>
    private void InitializeSystemDictionaryWithNil(int nilId)
    {
        var systemDictOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.SystemDictionaryId);
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        var nilOOP = SmalltalkOOP.FromObjectTableIndex(nilId);
        
        for (int i = 0; i < systemDictSize; i++)
        {
            _allocator.SetIndexedOOP(systemDictOOP, i, nilOOP);
        }
    }

    /// <summary>
    /// Initializes ProcessorScheduler instance variables with their proper initial values.
    /// Sets activeProcess and quiescentProcessLists to nil (no processes scheduled yet).
    /// Phase Y will skip these fields since they're already initialized (not 0).
    /// </summary>
    private void InitializeProcessorSchedulerFields(SmalltalkOOP processorSchedulerOOP, SmalltalkOOP nilOOP)
    {
        // ProcessorScheduler instance variables (from Smalltalk-80 Blue Book):
        // - quiescentProcessLists: Array of LinkedLists (one per priority level)
        // - activeProcess: Currently executing Process (or nil)
        
        // For now, set both to nil (simplified scheduler for init expressions)
        // Later, quiescentProcessLists can be initialized as Array new: 8 with LinkedList instances
        
        // Get instance variable count for ProcessorScheduler
        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("ProcessorScheduler", _bundle);
        
        // Initialize all instance variables to nil
        // This prevents Phase Y from overwriting with nil (it only sets uninitialized fields)
        for (int i = 0; i < instVarCount; i++)
        {
            _allocator.SetInstanceVariable(processorSchedulerOOP, i, nilOOP);
        }
        
        // Note: activeProcess and quiescentProcessLists are both set to nil
        // This is correct for the initial state (no processes scheduled)
        // When we execute init expressions, we'll create a Process and set activeProcess
    }

}
