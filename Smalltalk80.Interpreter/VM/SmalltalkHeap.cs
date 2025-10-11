using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Implementation of ISmalltalkHeap that manages the Smalltalk object heap.
/// Uses tagged OOPs for SmallIntegers and object table indices for heap objects.
/// </summary>
public class SmalltalkHeap : ISmalltalkHeap
{
    private readonly Dictionary<int, ObjectData> _objectTable = new();
    private readonly Dictionary<string, int> _symbolTable = new(); // name -> objectId
    private int _nextObjectId = 2; // Start at 2 (0 and 1 reserved for encoding)
    private int[] _specialObjectsArray = Array.Empty<int>(); // GC roots
    private string[] _optimizedSelectorNames = Array.Empty<string>(); // For bytecode disassembly

    /// <summary>
    /// Internal representation of a Smalltalk object.
    /// </summary>
    private class ObjectData
    {
        public int ClassId { get; set; }
        public ObjectFormat Format { get; set; }
        public int[] InstanceVariables { get; set; } // OOPs
        public object? IndexedData { get; set; } // int[], byte[], or null
        public Dictionary<string, int>? NamedSlots { get; set; } // For dynamic slot access
        public bool Marked { get; set; } // GC mark bit

        public ObjectData(int classId, ObjectFormat format, int instanceVarCount)
        {
            ClassId = classId;
            Format = format;
            InstanceVariables = new int[instanceVarCount];
        }
    }

    /// <summary>
    /// Object format types matching Smalltalk-80 specification.
    /// </summary>
    public enum ObjectFormat
    {
        FixedPointer,      // Fixed size, pointer fields only
        PointerIndexed,    // Pointers + indexed pointer fields
        ByteIndexed,       // Pointers + indexed byte fields
        WordIndexed,       // Pointers + indexed word fields
        CompiledMethod     // Special format for compiled methods
    }

    public SmalltalkHeap()
    {
        // Initialize nil, true, false as first objects
        // These will be properly created during Phase 5
    }

    /// <summary>
    /// Interns a symbol, ensuring only one object exists per symbol name.
    /// </summary>
    public int InternSymbol(string name)
    {
        // Check if symbol already exists
        if (_symbolTable.TryGetValue(name, out int existingId))
        {
            return existingId;
        }

        // Allocate new Symbol object
        // Symbol class will be resolved later, use placeholder 0 for now
        int symbolId = AllocateRawObject(0, ObjectFormat.ByteIndexed, 0, System.Text.Encoding.UTF8.GetByteCount(name));

        // Store the symbol's string data
        var symbolData = _objectTable[GetTableIndex(symbolId)];
        symbolData.IndexedData = System.Text.Encoding.UTF8.GetBytes(name);

        // Cache in symbol table
        _symbolTable[name] = symbolId;

        return symbolId;
    }

    /// <summary>
    /// Interns a string as a String object (not a Symbol).
    /// </summary>
    public int InternString(string value)
    {
        // Look up String class ID from GlobalRegistry
        int stringClassId = 0;
        if (GlobalRegistry.TryGet("class:String", out int foundClassId))
        {
            stringClassId = foundClassId;
        }

        int stringId = AllocateRawObject(stringClassId, ObjectFormat.ByteIndexed, 0, System.Text.Encoding.UTF8.GetByteCount(value));

        var stringData = _objectTable[GetTableIndex(stringId)];
        stringData.IndexedData = System.Text.Encoding.UTF8.GetBytes(value);

        return stringId;
    }

    /// <summary>
    /// Allocates an object with the specified class, format, and size.
    /// </summary>
    public int AllocateObject(string className, string formatHint, int fixedSize, int indexedSize)
    {
        // Determine format from hint
        ObjectFormat format = formatHint switch
        {
            "fixed" => ObjectFormat.FixedPointer,
            "pointers" => ObjectFormat.PointerIndexed,
            "bytes" => ObjectFormat.ByteIndexed,
            "words" => ObjectFormat.WordIndexed,
            "compiledMethod" => ObjectFormat.CompiledMethod,
            _ => ObjectFormat.FixedPointer
        };

        // Lookup class ID (will fail if class not yet allocated, that's expected during bootstrap)
        int classId = 0; // Placeholder, will be patched later
        if (GlobalRegistry.TryGet($"class:{className}", out int foundClassId))
        {
            classId = foundClassId;
        }

        return AllocateRawObject(classId, format, fixedSize, indexedSize);
    }

    /// <summary>
    /// Allocates a raw object without class resolution.
    /// </summary>
    public int AllocateRawObject(int classId, ObjectFormat format, int fixedSize, int indexedSize)
    {
        int tableIndex = _nextObjectId++;
        var objectData = new ObjectData(classId, format, fixedSize);

        // Debug: Track specific OOP allocation
        int oop = tableIndex << 1;
        if (oop == 17474)
        {
            Console.WriteLine($"DEBUG ALLOC: tableIdx={tableIndex}, OOP={oop}, classId={classId}, fmt={format}, fix={fixedSize}, idx={indexedSize}");
            Console.WriteLine($"  ^^^ OOP 17474 allocated with classId={classId}! Stack trace:");
            Console.WriteLine(Environment.StackTrace);
        }

        // Allocate indexed data based on format
        if (indexedSize > 0)
        {
            objectData.IndexedData = format switch
            {
                ObjectFormat.PointerIndexed => new int[indexedSize],
                ObjectFormat.ByteIndexed => new byte[indexedSize],
                ObjectFormat.WordIndexed => new ushort[indexedSize],
                ObjectFormat.CompiledMethod => new byte[indexedSize], // Will be filled later
                _ => null
            };
        }

        _objectTable[tableIndex] = objectData;

        // Encode as object pointer (table index << 1, LSB = 0)
        return tableIndex << 1;
    }

    /// <summary>
    /// Gets a fixed instance variable field.
    /// </summary>
    public int GetField(int objectId, int fieldIndex)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (fieldIndex < 0 || fieldIndex >= objectData.InstanceVariables.Length)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex), $"Field index {fieldIndex} out of range for object with {objectData.InstanceVariables.Length} fields");

        return objectData.InstanceVariables[fieldIndex];
    }

    /// <summary>
    /// Sets a fixed instance variable field.
    /// </summary>
    public void SetField(int objectId, int fieldIndex, int valueId)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (fieldIndex < 0 || fieldIndex >= objectData.InstanceVariables.Length)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex), $"Field index {fieldIndex} out of range for object with {objectData.InstanceVariables.Length} fields");

        objectData.InstanceVariables[fieldIndex] = valueId;
    }

    /// <summary>
    /// Gets an indexed field (for arrays, byte arrays, etc.).
    /// For pointer-indexed objects only. Use GetIndexedByte for byte-indexed objects.
    /// </summary>
    public int GetIndexedField(int objectId, int index)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (objectData.IndexedData is int[] pointerArray)
        {
            if (index < 0 || index >= pointerArray.Length)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for indexed object with {pointerArray.Length} elements");
            return pointerArray[index];
        }

        throw new InvalidOperationException($"Object {objectId} does not have integer indexed data");
    }

    /// <summary>
    /// Gets a byte from a byte-indexed object (for Float, String, ByteArray, etc.).
    /// </summary>
    public byte GetIndexedByte(int objectId, int index)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (objectData.IndexedData is byte[] byteArray)
        {
            if (index < 0 || index >= byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for byte-indexed object with {byteArray.Length} elements");
            return byteArray[index];
        }

        throw new InvalidOperationException($"Object {objectId} does not have byte indexed data");
    }

    /// <summary>
    /// Sets a byte in a byte-indexed object (for Float, String, ByteArray, etc.).
    /// Takes a raw byte value, not a SmallInteger OOP.
    /// </summary>
    public void SetIndexedByte(int objectId, int index, byte value)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (objectData.IndexedData is byte[] byteArray)
        {
            if (index < 0 || index >= byteArray.Length)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range for byte-indexed object with {byteArray.Length} elements");
            byteArray[index] = value;
            return;
        }

        throw new InvalidOperationException($"Object {objectId} does not have byte indexed data");
    }

    /// <summary>
    /// Sets an indexed field (for arrays, byte arrays, etc.).
    /// For SmallInteger-encoded byte values, use this. For raw byte values, use SetIndexedByte.
    /// </summary>
    public void SetIndexedField(int objectId, int index, int valueId)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        if (objectData.IndexedData == null)
            throw new InvalidOperationException("Object does not have indexed fields");

        switch (objectData.IndexedData)
        {
            case int[] pointerArray:
                pointerArray[index] = valueId;
                break;
            case byte[] byteArray:
                // Decode SmallInteger to byte
                if (IsSmallInteger(valueId))
                    byteArray[index] = (byte)DecodeSmallInteger(valueId);
                else
                    throw new ArgumentException("Byte array expects SmallInteger values");
                break;
            case ushort[] wordArray:
                // Decode SmallInteger to ushort
                if (IsSmallInteger(valueId))
                    wordArray[index] = (ushort)DecodeSmallInteger(valueId);
                else
                    throw new ArgumentException("Word array expects SmallInteger values");
                break;
            default:
                throw new NotSupportedException($"Unsupported indexed data type: {objectData.IndexedData.GetType()}");
        }
    }

    /// <summary>
    /// Sets a named slot on an object (for dynamic property access).
    /// </summary>
    public void SetNamedSlot(int objectId, string slotName, int valueId)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        objectData.NamedSlots ??= new Dictionary<string, int>();
        objectData.NamedSlots[slotName] = valueId;
    }

    /// <summary>
    /// Creates a compiled method object.
    /// In Smalltalk-80, a CompiledMethod stores the header as the first literal.
    /// Structure: [header, literal1, literal2, ...] + bytecodes
    /// </summary>
    public int CreateCompiledMethod(MethodDescriptor method, int[] literalIds, byte[] bytecodes)
    {
        // CompiledMethod format: [header + literals] in instance variables, bytecodes in indexed data
        int literalCount = literalIds.Length;

        // Encode the method header as a SmallInteger
        int headerOop = EncodeSmallInteger(method.Header.Word);

        // Allocate CompiledMethod object
        int methodId = AllocateRawObject(0, ObjectFormat.CompiledMethod, 0, bytecodes.Length);

        var methodData = _objectTable[GetTableIndex(methodId)];

        // Store header + literals in instance variables
        // Instance variables: [header, literal1, literal2, ...]
        methodData.InstanceVariables = new int[1 + literalCount];
        methodData.InstanceVariables[0] = headerOop; // Header is literal[0]
        Array.Copy(literalIds, 0, methodData.InstanceVariables, 1, literalCount);

        // Store bytecodes
        methodData.IndexedData = bytecodes;

        return methodId;
    }

    /// <summary>
    /// Creates a MethodDictionary with exact capacity - NO TEMPORARY OBJECTS!
    /// </summary>
    public int CreateMethodDictionary(int methodCount)
    {
        // MethodDictionary stores selector-method pairs, so size = methodCount * 2
        // NEVER create undersized - we know exact count upfront!
        return AllocateRawObject(0, ObjectFormat.PointerIndexed, 0, methodCount * 2);
    }

    /// <summary>
    /// Creates a Universal Symbol Table.
    /// </summary>
    public int CreateUSTable(int size)
    {
        // USTable is an Array
        return AllocateRawObject(0, ObjectFormat.PointerIndexed, 0, size);
    }

    /// <summary>
    /// Inserts a symbol into the USTable at its hash position.
    /// </summary>
    public void InsertIntoUSTable(int ustableId, int symbolId, int hash)
    {
        int tableIndex = GetTableIndex(ustableId);

        if (!_objectTable.TryGetValue(tableIndex, out var ustableData))
            throw new ArgumentException($"Invalid USTable ID: {ustableId}");

        if (ustableData.IndexedData is not int[] table)
            throw new InvalidOperationException("USTable must be a pointer array");

        // Linear probing for hash collision resolution
        int index = hash % table.Length;
        while (table[index] != 0 && !IsSmallInteger(table[index]))
        {
            index = (index + 1) % table.Length;
        }

        table[index] = symbolId;
    }

    /// <summary>
    /// Installs a method in a class's method dictionary.
    /// Dictionary MUST be pre-allocated to exact size - NO EXPANSION!
    /// </summary>
    public void InstallMethod(int classId, int selectorId, int methodId)
    {
        int tableIndex = GetTableIndex(classId);

        if (!_objectTable.TryGetValue(tableIndex, out var classData))
            throw new ArgumentException($"Invalid class ID: {classId}");

        // Get the pre-allocated method dictionary
        if (classData.NamedSlots?.TryGetValue("methodDictionary", out int methodDictId) != true)
            throw new InvalidOperationException($"Method dictionary must be pre-allocated before installing methods!");

        // Get the method dictionary's data
        int dictTableIndex = GetTableIndex(methodDictId);
        if (!_objectTable.TryGetValue(dictTableIndex, out var dictData))
            throw new InvalidOperationException("Method dictionary is invalid");

        if (dictData.IndexedData is not int[] methods)
            throw new InvalidOperationException("Method dictionary must have pointer array!");

        // Find the FIRST empty slot - NO EXPANSION allowed!
        int slot = -1;
        for (int i = 0; i < methods.Length; i += 2) // selector, method pairs
        {
            if (methods[i] == 0)
            {
                slot = i;
                break;
            }
        }

        if (slot == -1)
            throw new InvalidOperationException(
                "Method dictionary full - should have been pre-allocated to exact size! " +
                $"NO TEMPORARY OBJECTS during cold start!");

        // Store selector and method
        methods[slot] = selectorId;
        methods[slot + 1] = methodId;
    }

    /// <summary>
    /// Executes a method (stub - requires interpreter).
    /// </summary>
    public void ExecuteMethod(int methodId)
    {
        throw new NotImplementedException("ExecuteMethod requires interpreter implementation");
    }

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP (does NOT allocate on heap).
    /// SmallIntegers use immediate encoding: (value left-shift 1) OR 1
    /// LSB = 1 indicates SmallInteger, LSB = 0 indicates object pointer.
    /// </summary>
    public int EncodeSmallInteger(int value)
    {
        // SmallInteger tagging: left shift 1 bit, set LSB to 1
        return (value << 1) | 1;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    public int AllocateCharacter(int codePoint)
    {
        // Character objects typically store the code point as a SmallInteger
        int charId = AllocateRawObject(0, ObjectFormat.FixedPointer, 1, 0);
        var charData = _objectTable[GetTableIndex(charId)];
        charData.InstanceVariables[0] = EncodeSmallInteger(codePoint);
        return charId;
    }

    /// <summary>
    /// Allocates a ByteArray from encoded string data.
    /// </summary>
    public int AllocateByteArray(string encoding, string data)
    {
        byte[] bytes = encoding switch
        {
            "base64" => Convert.FromBase64String(data),
            "utf8" => System.Text.Encoding.UTF8.GetBytes(data),
            _ => throw new NotSupportedException($"Unsupported encoding: {encoding}")
        };

        int arrayId = AllocateRawObject(0, ObjectFormat.ByteIndexed, 0, bytes.Length);
        var arrayData = _objectTable[GetTableIndex(arrayId)];
        arrayData.IndexedData = bytes;

        return arrayId;
    }

    /// <summary>
    /// Allocates a Float object with the correct Float class.
    /// Float objects in Smalltalk-80 are stored as byte-indexed objects containing IEEE 754 representation.
    /// </summary>
    public int AllocateFloat(double value)
    {
        // Look up Float class ID from GlobalRegistry
        int floatClassId = 0;
        if (GlobalRegistry.TryGet("class:Float", out int foundClassId))
        {
            floatClassId = foundClassId;
        }

        byte[] bytes = BitConverter.GetBytes(value);
        int floatId = AllocateRawObject(floatClassId, ObjectFormat.ByteIndexed, 0, bytes.Length);
        var floatData = _objectTable[GetTableIndex(floatId)];
        floatData.IndexedData = bytes;

        return floatId;
    }

    /// <summary>
    /// Allocates an Association (key-value pair).
    /// </summary>
    public int AllocateAssociation(int keyId, int valueId)
    {
        // Association has two instance variables: key and value
        int assocId = AllocateRawObject(0, ObjectFormat.FixedPointer, 2, 0);
        var assocData = _objectTable[GetTableIndex(assocId)];
        assocData.InstanceVariables[0] = keyId;
        assocData.InstanceVariables[1] = valueId;
        return assocId;
    }

    /// <summary>
    /// Allocates an Array from element OOPs.
    /// </summary>
    public int AllocateArray(int[] elementIds)
    {
        int arrayId = AllocateRawObject(0, ObjectFormat.PointerIndexed, 0, elementIds.Length);
        var arrayData = _objectTable[GetTableIndex(arrayId)];
        arrayData.IndexedData = (int[])elementIds.Clone();
        return arrayId;
    }

    /// <summary>
    /// Executes an initialization expression (stub - requires interpreter).
    /// </summary>
    public void ExecuteInitExpression(object code, int[] literalIds)
    {
        throw new NotImplementedException("ExecuteInitExpression requires interpreter implementation");
    }

    /// <summary>
    /// Gets a listing of all classes in the heap.
    /// Returns tuples of (className, instanceCount, instanceMethodCount, classMethodCount).
    /// </summary>
    public IEnumerable<(string ClassName, int InstanceCount, int InstanceMethodCount, int ClassMethodCount)> GetAllClasses()
    {
        // TODO: Implement class listing
        // This should:
        // 1. Iterate through all class objects in the heap
        // 2. For each class, count instances of that class
        // 3. Count methods in the class's method dictionary (instance side)
        // 4. Count methods in the metaclass's method dictionary (class side)
        // 5. Return the tuple (className, instanceCount, instanceMethodCount, classMethodCount)

        throw new NotImplementedException("GetAllClasses not yet implemented - requires class structure to be populated");
    }

    // Helper methods

    /// <summary>
    /// Extracts the object table index from an OOP.
    /// </summary>
    private int GetTableIndex(int oop)
    {
        if (IsSmallInteger(oop))
            throw new ArgumentException("Cannot get table index of SmallInteger");

        return oop >> 1;
    }

    /// <summary>
    /// Checks if an OOP is a SmallInteger (LSB = 1).
    /// </summary>
    private bool IsSmallInteger(int oop)
    {
        return (oop & 1) == 1;
    }

    /// <summary>
    /// Decodes a SmallInteger OOP to its numeric value.
    /// </summary>
    private int DecodeSmallInteger(int oop)
    {
        if (!IsSmallInteger(oop))
            throw new ArgumentException("OOP is not a SmallInteger");

        return oop >> 1;
    }

    // ========== Garbage Collection ==========

    /// <summary>
    /// Sets the special objects array for garbage collection roots.
    /// </summary>
    public void SetSpecialObjectsArray(int[] specialObjects)
    {
        _specialObjectsArray = specialObjects ?? Array.Empty<int>();
    }

    /// <summary>
    /// Performs a mark-and-sweep garbage collection.
    /// Returns the number of objects collected (should be 0 after cold start).
    /// </summary>
    public int GarbageCollect()
    {
        // Phase 1: Clear all mark bits
        foreach (var obj in _objectTable.Values)
        {
            obj.Marked = false;
        }

        // Phase 2: Mark from roots (special objects array)
        foreach (var rootId in _specialObjectsArray)
        {
            if (!IsSmallInteger(rootId))
            {
                MarkObject(rootId);
            }
        }

        // Phase 3: Sweep unmarked objects
        int collected = 0;
        var toRemove = new List<int>();
        var collectedByFormat = new Dictionary<ObjectFormat, int>();

        foreach (var kvp in _objectTable)
        {
            if (!kvp.Value.Marked)
            {
                toRemove.Add(kvp.Key);
                collected++;

                // Track what's being collected
                var format = kvp.Value.Format;
                collectedByFormat[format] = collectedByFormat.GetValueOrDefault(format) + 1;
            }
        }

        // Log what's being collected (for debugging)
        if (collected > 0)
        {
            Console.WriteLine($"\n  [GC DEBUG] Collecting {collected} unreachable objects:");
            foreach (var kvp in collectedByFormat.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"    - {kvp.Key}: {kvp.Value} objects");
            }
        }

        foreach (var tableIndex in toRemove)
        {
            _objectTable.Remove(tableIndex);
        }

        return collected;
    }

    /// <summary>
    /// Recursively marks an object and all objects it references.
    /// </summary>
    private void MarkObject(int objectId)
    {
        if (IsSmallInteger(objectId))
            return; // SmallIntegers are not heap objects

        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            return; // Object doesn't exist (could be nil encoded as 0)

        if (objectData.Marked)
            return; // Already marked

        objectData.Marked = true;

        // Mark class
        if (objectData.ClassId != 0 && !IsSmallInteger(objectData.ClassId))
        {
            MarkObject(objectData.ClassId);
        }

        // Mark instance variables (includes literals for CompiledMethod)
        foreach (var fieldId in objectData.InstanceVariables)
        {
            if (fieldId != 0 && !IsSmallInteger(fieldId))
            {
                MarkObject(fieldId);
            }
        }

        // Mark indexed data (if it contains object pointers)
        if (objectData.IndexedData is int[] pointerArray)
        {
            foreach (var pointerId in pointerArray)
            {
                if (pointerId != 0 && !IsSmallInteger(pointerId))
                {
                    MarkObject(pointerId);
                }
            }
        }

        // Mark named slots
        if (objectData.NamedSlots != null)
        {
            foreach (var slotValue in objectData.NamedSlots.Values)
            {
                if (slotValue != 0 && !IsSmallInteger(slotValue))
                {
                    MarkObject(slotValue);
                }
            }
        }
    }

    /// <summary>
    /// Gets garbage collection statistics.
    /// </summary>
    public (int TotalObjects, int LiveObjects) GetGCStatistics()
    {
        int total = _objectTable.Count;
        int live = _objectTable.Values.Count(obj => obj.Marked);
        return (total, live);
    }

    // ========== Class Introspection for Pretty Printing ==========

    /// <summary>
    /// Gets comprehensive information about a class by name.
    /// </summary>
    public ClassInfo? GetClassInfo(string className)
    {
        // Try to find the class in GlobalRegistry
        if (!GlobalRegistry.TryGet($"class:{className}", out int classId))
            return null;

        int tableIndex = GetTableIndex(classId);
        if (!_objectTable.TryGetValue(tableIndex, out var classData))
            return null;

        // Extract class information from named slots
        var superclassId = classData.NamedSlots?.GetValueOrDefault("superclass", 0) ?? 0;
        var metaclassId = classData.NamedSlots?.GetValueOrDefault("class", 0) ?? 0;
        var formatId = classData.NamedSlots?.GetValueOrDefault("format", 0) ?? 0;
        var instanceSizeId = classData.NamedSlots?.GetValueOrDefault("instanceSize", 0) ?? 0;
        var instanceVariablesId = classData.NamedSlots?.GetValueOrDefault("instanceVariables", 0) ?? 0;
        var methodDictId = classData.NamedSlots?.GetValueOrDefault("methodDictionary", 0) ?? 0;

        // Get superclass name
        string superclassName = "nil";
        if (superclassId != 0 && !IsSmallInteger(superclassId))
        {
            var superNameId = _objectTable[GetTableIndex(superclassId)].NamedSlots?.GetValueOrDefault("name", 0) ?? 0;
            if (superNameId != 0)
                superclassName = GetSymbolName(superNameId) ?? "???";
        }

        // Get format
        string format = "unknown";
        if (IsSmallInteger(formatId))
        {
            int formatValue = DecodeSmallInteger(formatId);
            format = ((ObjectFormat)formatValue).ToString();
        }

        // Get instance size
        int instanceSize = IsSmallInteger(instanceSizeId) ? DecodeSmallInteger(instanceSizeId) : 0;

        // Get instance variable names
        string[] instVarNames = Array.Empty<string>();
        if (instanceVariablesId != 0 && !IsSmallInteger(instanceVariablesId))
        {
            var instVarData = _objectTable[GetTableIndex(instanceVariablesId)];
            if (instVarData.IndexedData is int[] instVarArray)
            {
                instVarNames = instVarArray
                    .Where(id => id != 0)
                    .Select(id => GetSymbolName(id) ?? "???")
                    .ToArray();
            }
        }

        // Get selectors from method dictionary
        string[] selectors = Array.Empty<string>();
        if (methodDictId != 0 && !IsSmallInteger(methodDictId))
        {
            var methodDictData = _objectTable[GetTableIndex(methodDictId)];
            if (methodDictData.IndexedData is int[] methods)
            {
                var selectorList = new List<string>();
                for (int i = 0; i < methods.Length; i += 2)
                {
                    if (methods[i] != 0)
                    {
                        var selectorName = GetSymbolName(methods[i]);
                        if (selectorName != null)
                            selectorList.Add(selectorName);
                    }
                }
                selectors = selectorList.ToArray();
            }
        }

        return new ClassInfo
        {
            ClassId = classId,
            ClassName = className,
            SuperclassId = superclassId,
            SuperclassName = superclassName,
            MetaclassId = metaclassId,
            Format = format,
            InstanceSize = instanceSize,
            InstanceVariableNames = instVarNames,
            Selectors = selectors
        };
    }

    /// <summary>
    /// Gets detailed information about a method in a class.
    /// </summary>
    public MethodInfo? GetMethod(int classId, string selector)
    {
        // Walk the superclass chain until we find the method
        int currentClassId = classId;
        int selectorId = InternSymbol(selector);
        int depth = 0;

        // Debug: trace method lookup
        if (selector == "comment:")
        {
            Console.WriteLine($"DEBUG GetMethod: Looking for '{selector}' starting in class {classId}");
        }

        while (currentClassId != 0 && !IsSmallInteger(currentClassId))
        {
            int tableIndex = GetTableIndex(currentClassId);
            if (!_objectTable.TryGetValue(tableIndex, out var classData))
            {
                if (selector == "comment:")
                    Console.WriteLine($"DEBUG GetMethod: Class {currentClassId} not in object table!");
                return null;
            }

            // Get class name for debugging
            string? className = null;
            if (selector == "comment:")
            {
                var nameId = classData.NamedSlots?.GetValueOrDefault("name", 0) ?? 0;
                className = nameId != 0 ? GetSymbolName(nameId) : $"<class {currentClassId}>";
                Console.WriteLine($"DEBUG GetMethod: [{depth}] Searching in class '{className}' (OOP={currentClassId})");
            }

            // Get method dictionary
            var methodDictId = classData.NamedSlots?.GetValueOrDefault("methodDictionary", 0) ?? 0;
            if (methodDictId != 0 && !IsSmallInteger(methodDictId))
            {
                var methodDictData = _objectTable[GetTableIndex(methodDictId)];
                if (methodDictData.IndexedData is int[] methods)
                {
                    if (selector == "comment:")
                        Console.WriteLine($"DEBUG GetMethod: [{depth}] Method dict has {methods.Length / 2} entries");

                    // Search for the selector in this class's method dictionary
                    for (int i = 0; i < methods.Length; i += 2)
                    {
                        if (methods[i] == selectorId)
                        {
                            int methodId = methods[i + 1];
                            if (methodId == 0)
                                continue; // Empty slot, keep searching

                            // Found the method! Extract and return method info
                            if (selector == "comment:")
                                Console.WriteLine($"DEBUG GetMethod: [{depth}] FOUND '{selector}' in '{className}', method OOP={methodId}");
                            return ExtractMethodInfo(methodId, selector);
                        }
                    }

                    if (selector == "comment:")
                        Console.WriteLine($"DEBUG GetMethod: [{depth}] '{selector}' NOT in this class's method dict");
                }
            }
            else
            {
                if (selector == "comment:")
                    Console.WriteLine($"DEBUG GetMethod: [{depth}] No method dictionary in '{className}'");
            }

            // Method not found in this class, try superclass
            int nextClassId = classData.NamedSlots?.GetValueOrDefault("superclass", 0) ?? 0;
            if (selector == "comment:")
                Console.WriteLine($"DEBUG GetMethod: [{depth}] Moving to superclass OOP={nextClassId}");

            currentClassId = nextClassId;
            depth++;

            if (depth > 20)
            {
                Console.WriteLine($"DEBUG GetMethod: Too many levels in class hierarchy! Breaking.");
                break;
            }
        }

        // Method not found in entire hierarchy
        if (selector == "comment:")
            Console.WriteLine($"DEBUG GetMethod: '{selector}' NOT FOUND in entire hierarchy starting from class {classId}");
        return null;
    }

    /// <summary>
    /// Extracts MethodInfo from a CompiledMethod object.
    /// </summary>
    private MethodInfo? ExtractMethodInfo(int methodId, string selector)
    {
        if (methodId == 0)
            return null;

        // Extract method data
        var methodData = _objectTable[GetTableIndex(methodId)];

        // Get bytecodes
        byte[] bytecodes = methodData.IndexedData as byte[] ?? Array.Empty<byte>();

        // Get literals (stored in instance variables of CompiledMethod)
        int[] literalIds = methodData.InstanceVariables ?? Array.Empty<int>();

        // Decode method header (first literal if present, otherwise default)
        int headerWord = literalIds.Length > 0 && IsSmallInteger(literalIds[0])
            ? DecodeSmallInteger(literalIds[0])
            : 0;

        // Extract header fields (Blue Book format)
        int argCount = (headerWord >> 4) & 0x1F;
        int tempCount = (headerWord >> 9) & 0x3F;
        bool largeContext = ((headerWord >> 15) & 1) == 1;
        int primitiveIndex = headerWord & 0x1FF;

        return new MethodInfo
        {
            MethodId = methodId,
            Selector = selector,
            HeaderWord = headerWord,
            ArgumentCount = argCount,
            TemporaryCount = tempCount,
            LargeContext = largeContext,
            PrimitiveIndex = primitiveIndex,
            LiteralIds = literalIds,
            Bytecodes = bytecodes
        };
    }

    /// <summary>
    /// Gets the string name of a symbol object.
    /// </summary>
    public string? GetSymbolName(int symbolId)
    {
        if (symbolId == 0 || IsSmallInteger(symbolId))
            return null;

        int tableIndex = GetTableIndex(symbolId);
        if (!_objectTable.TryGetValue(tableIndex, out var symbolData))
            return null;

        if (symbolData.IndexedData is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    /// <summary>
    /// Gets a displayable representation of any object's value.
    /// </summary>
    /// <summary>
    /// Gets the class ID of an object.
    /// </summary>
    public int GetClass(int objectId)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        return objectData.ClassId;
    }

    /// <summary>
    /// Sets the class ID of an object.
    /// Used during bootstrap to wire up class-metaclass relationships.
    /// </summary>
    public void SetClass(int objectId, int classId)
    {
        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        objectData.ClassId = classId;
    }

    /// <summary>
    /// Gets the bytecodes of a CompiledMethod.
    /// </summary>
    public byte[] GetMethodBytecodes(int methodId)
    {
        int tableIndex = GetTableIndex(methodId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid method ID: {methodId}");

        if (objectData.IndexedData is not byte[] bytecodes)
            throw new InvalidOperationException($"Object {methodId} is not a CompiledMethod");

        return bytecodes;
    }

    /// <summary>
    /// Gets a literal from a CompiledMethod's literal frame.
    /// </summary>
    public int GetMethodLiteral(int methodId, int index)
    {
        int tableIndex = GetTableIndex(methodId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid method ID: {methodId}");

        if (index < 0 || index >= objectData.InstanceVariables.Length)
            throw new ArgumentOutOfRangeException(nameof(index), $"Literal index {index} out of range for method with {objectData.InstanceVariables.Length} literals");

        return objectData.InstanceVariables[index];
    }

    public string GetObjectDisplay(int objectId)
    {
        if (objectId == 0)
            return "nil";

        if (IsSmallInteger(objectId))
            return DecodeSmallInteger(objectId).ToString();

        int tableIndex = GetTableIndex(objectId);
        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            return $"#invalid:{objectId}";

        // Handle special cases
        if (objectData.IndexedData is byte[] bytes)
        {
            // Symbol or String
            try
            {
                string str = System.Text.Encoding.UTF8.GetString(bytes);
                return $"#{str}";
            }
            catch
            {
                return $"#<bytes:{bytes.Length}>";
            }
        }

        if (objectData.IndexedData is int[] pointers)
        {
            // Array
            return $"#<Array size:{pointers.Length}>";
        }

        // Generic object pointer
        return $"#<{objectId}>";
    }

    /// <summary>
    /// Sets the optimized selector names for bytecode disassembly.
    /// </summary>
    public void SetOptimizedSelectors(string[] selectorNames)
    {
        _optimizedSelectorNames = selectorNames;
    }

    /// <summary>
    /// Gets the optimized selector names for bytecode disassembly.
    /// </summary>
    public string[] GetOptimizedSelectors()
    {
        return _optimizedSelectorNames;
    }

    // ========== Infrastructure APIs for Primitives ==========

    /// <summary>
    /// Gets a class object ID by class name.
    /// Used by primitives that need to instantiate specific classes (Float, LargeInteger, etc.).
    /// </summary>
    public int GetClassByName(string className)
    {
        if (!GlobalRegistry.TryGet($"class:{className}", out int classId))
        {
            throw new ArgumentException($"Class not found: {className}");
        }
        return classId;
    }

    /// <summary>
    /// Gets the size of the indexed portion of an object.
    /// Returns 0 if the object has no indexed portion.
    /// </summary>
    public int GetIndexedSize(int objectId)
    {
        if (IsSmallInteger(objectId))
            return 0; // SmallIntegers have no indexed portion

        int tableIndex = GetTableIndex(objectId);

        if (!_objectTable.TryGetValue(tableIndex, out var objectData))
            throw new ArgumentException($"Invalid object ID: {objectId}");

        return objectData.IndexedData switch
        {
            int[] pointerArray => pointerArray.Length,
            byte[] byteArray => byteArray.Length,
            ushort[] wordArray => wordArray.Length,
            _ => 0
        };
    }

    /// <summary>
    /// Enumerates all object pointers in the heap (excluding SmallIntegers).
    /// Used by object enumeration primitives (128, 129, 139).
    /// </summary>
    public IEnumerable<int> GetAllObjects()
    {
        // Return all table indices as object pointers (already shifted left by 1, even numbers)
        return _objectTable.Keys.OrderBy(k => k);
    }

    /// <summary>
    /// One-way become: All references to fromOop become references to toOop.
    /// Used by primitive 130 (becomeForward:).
    /// </summary>
    public void BecomeOneWay(int fromOop, int toOop)
    {
        // Cannot become with SmallIntegers
        if (IsSmallInteger(fromOop) || IsSmallInteger(toOop))
            throw new ArgumentException("Cannot become with SmallIntegers");

        int fromIndex = GetTableIndex(fromOop);
        int toIndex = GetTableIndex(toOop);

        // Verify both objects exist
        if (!_objectTable.ContainsKey(fromIndex))
            throw new ArgumentException($"Source object {fromOop} does not exist");
        if (!_objectTable.ContainsKey(toIndex))
            throw new ArgumentException($"Target object {toOop} does not exist");

        // Forward all references from fromOop to toOop
        // Scan all objects and replace references
        foreach (var entry in _objectTable.Values)
        {
            // Update instance variables
            for (int i = 0; i < entry.InstanceVariables.Length; i++)
            {
                if (entry.InstanceVariables[i] == fromOop)
                    entry.InstanceVariables[i] = toOop;
            }

            // Update indexed pointer fields
            if (entry.IndexedData is int[] pointerArray)
            {
                for (int i = 0; i < pointerArray.Length; i++)
                {
                    if (pointerArray[i] == fromOop)
                        pointerArray[i] = toOop;
                }
            }
        }

        // Update special objects array
        for (int i = 0; i < _specialObjectsArray.Length; i++)
        {
            if (_specialObjectsArray[i] == fromOop)
                _specialObjectsArray[i] = toOop;
        }
    }

    /// <summary>
    /// Two-way become: Swaps identities of two objects.
    /// Used by primitive 68 (become:).
    /// </summary>
    public void BecomeTwoWay(int oop1, int oop2)
    {
        // Cannot become with SmallIntegers
        if (IsSmallInteger(oop1) || IsSmallInteger(oop2))
            throw new ArgumentException("Cannot become with SmallIntegers");

        int index1 = GetTableIndex(oop1);
        int index2 = GetTableIndex(oop2);

        // Verify both objects exist
        if (!_objectTable.ContainsKey(index1))
            throw new ArgumentException($"Object {oop1} does not exist");
        if (!_objectTable.ContainsKey(index2))
            throw new ArgumentException($"Object {oop2} does not exist");

        // Swap the object data in the table
        var temp = _objectTable[index1];
        _objectTable[index1] = _objectTable[index2];
        _objectTable[index2] = temp;

        // Note: We swap the data, not the references, so all existing references
        // to oop1 now point to what was oop2's data, and vice versa.
        // This is the correct Blue Book semantics for become:.
    }

    // ========== Runtime Field Resolution (VM Support) ==========

    /// <summary>
    /// Gets the field index for a named instance variable by walking the class hierarchy.
    /// This uses ONLY heap data - NOT LayoutResolver.
    /// During cold start, class metadata is stored in NamedSlots.
    /// Returns -1 if field not found.
    /// </summary>
    public int GetFieldIndex(int classId, string fieldName)
    {
        if (classId == 0 || IsSmallInteger(classId))
            return -1;

        // Build the complete field list by walking up the superclass chain
        var fieldList = new List<string>();
        int currentClassId = classId;

        while (currentClassId != 0 && !IsSmallInteger(currentClassId))
        {
            int tableIndex = GetTableIndex(currentClassId);
            if (!_objectTable.TryGetValue(tableIndex, out var classData))
                break;

            // Get the instanceVariables array from the class object's named slots
            // This is where the cold start loader stores it
            if (classData.NamedSlots?.TryGetValue("instanceVariables", out int instVarsArrayId) == true)
            {
                if (instVarsArrayId != 0 && !IsSmallInteger(instVarsArrayId))
                {
                    var instVarsData = _objectTable[GetTableIndex(instVarsArrayId)];
                    if (instVarsData.IndexedData is int[] instVarSymbols)
                    {
                        // Get this class's instance variable names
                        var classFields = new List<string>();
                        foreach (int symbolId in instVarSymbols)
                        {
                            if (symbolId != 0 && !IsSmallInteger(symbolId))
                            {
                                string? varName = GetSymbolName(symbolId);
                                if (varName != null)
                                    classFields.Add(varName);
                            }
                        }
                        // Prepend (superclass fields come before subclass fields)
                        fieldList.InsertRange(0, classFields);
                    }
                }
            }

            // Move to superclass (also stored in named slots during cold start)
            if (classData.NamedSlots?.TryGetValue("superclass", out int superclassId) == true)
            {
                currentClassId = superclassId;
            }
            else
            {
                break;
            }
        }

        // Now search for the field name in the accumulated list
        return fieldList.IndexOf(fieldName);
    }
}
