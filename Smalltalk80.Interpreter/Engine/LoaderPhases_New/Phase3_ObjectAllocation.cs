using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;
using System.Text;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases_New;

/// <summary>
/// Phase 3: Object Allocation and Initialization
///
/// Consolidates three original phases into one cohesive phase:
/// 1. Object Allocation (Phase 6) - Allocates sentinel objects and special objects
/// 2. Class Reference Initialization (Phase X) - Sets class slots (slot 0) for all objects
/// 3. Instance Variable Initialization (Phase Y) - Initializes unset instance variables to nil
///
/// This phase creates the fundamental objects needed by the Smalltalk system and ensures
/// all objects are properly initialized with class references and nil-initialized fields.
/// </summary>
public class Phase3_ObjectAllocation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly IReadOnlyList<string> _symbols;
    private readonly ColdstartReport _report;

    public Phase3_ObjectAllocation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _initializedFields = initializedFields;
        _symbols = bundle.Symbols;
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void Run()
    {
        Console.WriteLine("Phase 3: Object Allocation and Initialization");

        // Section 1: Allocation
        AllocateSentinelObjects();
        AllocateSpecialObjects();
        LogAllocationCheckpoint();

        // Section 2: Class Reference Setting
        SetClassReferencesForAllObjects();
        LogClassReferenceCheckpoint();

        // Section 3: Nil Initialization
        InitializeUnsetInstanceVariables();
        LogInitializationCheckpoint();

        Console.WriteLine("  Phase 3 complete: All objects allocated and initialized");
    }

    private void LogAllocationCheckpoint()
    {
        int objectCount = _localIdToObjectId.Count + 3;  // +3 for nil/true/false in GlobalRegistry
        Console.WriteLine($"  ✓ Allocation checkpoint: {objectCount} objects allocated");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.ObjectsAllocated = objectCount;
    }

    private void LogClassReferenceCheckpoint()
    {
        // Count all objects that now have class references
        // This includes classes, metaclasses, symbols, and allocated objects
        int classRefCount = _bundle.Classes.Length + SymbolRegistry.AllSymbols().Count() + _localIdToObjectId.Count;
        Console.WriteLine($"  ✓ Class reference checkpoint: {classRefCount} class references set");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.ClassReferencesSet = classRefCount;
    }

    private void LogInitializationCheckpoint()
    {
        // Initialize unset instance variables completes Phase 3
        Console.WriteLine($"  ✓ Initialization checkpoint: All unset fields initialized to nil");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.FieldsInitialized = _initializedFields.Count;
    }

    #region Sentinel Object Allocation

    /// <summary>
    /// Allocates the fundamental sentinel objects: nil, true, false.
    /// These must be created AFTER all classes are fully wired so they get correct classIds.
    /// </summary>
    private void AllocateSentinelObjects()
    {
        // Create minimal dependency singletons: nil, true, false
        var nilOOP = _allocator.AllocateFixedObject("UndefinedObject");
        var trueOOP = _allocator.AllocateFixedObject("True");
        var falseOOP = _allocator.AllocateFixedObject("False");

        GlobalRegistry.Register("nil", nilOOP);
        GlobalRegistry.Register("true", trueOOP);
        GlobalRegistry.Register("false", falseOOP);

        Console.WriteLine($"  Allocated sentinel objects: nil={nilOOP}, true={trueOOP}, false={falseOOP}");

        // Initialize SystemDictionary slots with nil (created in Phase 2 but couldn't be initialized until now)
        InitializeSystemDictionaryWithNil(nilOOP);

        // Create ProcessorScheduler singleton
        var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
        GlobalRegistry.Register("ProcessorScheduler", processorSchedulerOOP);

        // Initialize ProcessorScheduler fields that should NOT be nil
        InitializeProcessorSchedulerFields(processorSchedulerOOP, nilOOP);
    }

    /// <summary>
    /// Initializes SystemDictionary indexed slots with nil.
    /// SystemDictionary was created in Phase 2 but couldn't be initialized until nil object exists.
    /// </summary>
    private void InitializeSystemDictionaryWithNil(SmalltalkOOP nilOOP)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);

        for (int i = 0; i < systemDictSize; i++)
        {
            _allocator.SetIndexedOOP(systemDictOOP, i, nilOOP);
        }
    }

    /// <summary>
    /// Initializes ProcessorScheduler instance variables with their proper initial values.
    /// </summary>
    private void InitializeProcessorSchedulerFields(SmalltalkOOP processorSchedulerOOP, SmalltalkOOP nilOOP)
    {
        // Get instance variable count for ProcessorScheduler
        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("ProcessorScheduler", _bundle);

        // Initialize all instance variables to nil
        // This prevents Phase Y from overwriting with nil (it only sets uninitialized fields)
        for (int i = 0; i < instVarCount; i++)
        {
            _allocator.SetInstanceVariable(processorSchedulerOOP, i, nilOOP);
            _initializedFields.Add((processorSchedulerOOP, i));
        }
    }

    #endregion

    #region Special Object Allocation

    /// <summary>
    /// Allocates special objects defined in the bundle.
    /// </summary>
    private void AllocateSpecialObjects()
    {
        int objectsAllocated = 0;

        // Create other bundle objects
        foreach (var obj in _bundle.Objects)
        {
            string className = _symbols[obj.ClassNameIndex];
            int fixedSize = obj.Fields.Length;
            int indexedSize = obj.IndexedFields.Length;

            SmalltalkOOP objectOOP = AllocateObjectWithSemanticAllocator(className, obj.FormatHint, fixedSize, indexedSize);
            _localIdToObjectId[obj.LocalId] = objectOOP;

            // Note: Field initialization moved to Step 4 (PhaseY logic) after nil exists

            // Add pool dictionaries to SystemDictionary using semantic lookup
            if (obj.LocalId.StartsWith("pool:"))
            {
                string poolName = obj.LocalId.Substring(5); // Remove "pool:" prefix
                SmalltalkOOP poolNameSymbol = InternSymbol(poolName);
                SmalltalkOOP association = AllocateAssociationWithSemanticAllocator(poolNameSymbol, objectOOP);
                InstallAssociationInSystemDictionary(association, poolName);
            }

            objectsAllocated++;
        }

        Console.WriteLine($"  Allocated {objectsAllocated} special objects from bundle");
    }

    /// <summary>
    /// Allocates an object using SemanticAllocator based on format hint.
    /// </summary>
    private SmalltalkOOP AllocateObjectWithSemanticAllocator(string className, string formatHint, int fixedSize, int indexedSize)
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

        return objectOOP;
    }

    /// <summary>
    /// Allocates an Association object (key-value pair) using SemanticAllocator.
    /// </summary>
    private SmalltalkOOP AllocateAssociationWithSemanticAllocator(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");

        // Set instance variables: [0] = key, [1] = value (assuming standard Association layout)
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Installs an association in SystemDictionary using semantic slot finding.
    /// </summary>
    private void InstallAssociationInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Find first available nil slot
        for (int i = 0; i < systemDictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(systemDictOOP, i);
            if (slotOOP == nilOOP)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(systemDictOOP, i, associationOOP);
                return;
            }
        }

        throw new InvalidOperationException($"SystemDictionary is full - cannot install {name} association");
    }

    #endregion

    #region Class Reference Initialization

    /// <summary>
    /// Sets class references (slot 0) for all allocated objects.
    /// This resolves the circular dependency where objects need class references but classes don't exist during allocation.
    /// </summary>
    private void SetClassReferencesForAllObjects()
    {
        int classReferencesSet = 0;

        // Step 1: Set class references for all class objects themselves
        // IMPORTANT: For class objects, there are TWO different "slot 0" concepts:
        //   - Header slot 0 (memory offset 0): The class reference (set here)
        //   - Instance variable 0 (memory offset 4): The superclass field (set in Phase 3)
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            if (cls.IsMetaclass)
            {
                // Metaclass objects point to the Metaclass class
                var metaclassClassOOP = GlobalRegistry.LookupClassOOP("Metaclass");
                _allocator.SetSlot(classOOP, 0, metaclassClassOOP);
            }
            else
            {
                // Instance class objects point to their metaclass
                string instanceClassName = GetClassNameFromLocalId(cls.LocalId);
                string metaclassLocalId = $"meta:{instanceClassName}";

                if (_localIdToObjectId.TryGetValue(metaclassLocalId, out SmalltalkOOP metaclassOOP))
                {
                    _allocator.SetSlot(classOOP, 0, metaclassOOP);

                    // Debug: Show first few and class:Point specifically
                    if (classReferencesSet < 5 || instanceClassName == "Point")
                    {
                        Console.WriteLine($"    Set {cls.LocalId}.class = {metaclassLocalId}");
                    }
                }
                else
                {
                    Console.WriteLine($"WARNING: Could not find metaclass for {instanceClassName}");
                }
            }

            classReferencesSet++;
        }

        // Step 2: Set class references for singleton objects (nil, true, false)
        SetSingletonClassReference("nil", "UndefinedObject");
        SetSingletonClassReference("true", "True");
        SetSingletonClassReference("false", "False");
        classReferencesSet += 3;

        // Step 3: Set class references for all other objects created in phases
        foreach (var obj in _bundle.Objects)
        {
            if (!_localIdToObjectId.TryGetValue(obj.LocalId, out SmalltalkOOP objectOOP))
                continue;

            string className = _bundle.Symbols[obj.ClassNameIndex];
            var classOOP = GlobalRegistry.LookupClassOOP(className);
            _allocator.SetSlot(objectOOP, 0, classOOP);

            classReferencesSet++;
        }

        // Step 4: Set class references for symbols, strings, and other runtime objects
        SetRuntimeObjectClassReferences();

        Console.WriteLine($"  Set {classReferencesSet} class references");
    }

    private void SetSingletonClassReference(string globalName, string className)
    {
        if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
        {
            SmalltalkOOP classObjectId = GlobalRegistry.LookupClassOOP(className);
            var objectOOP = objectId;
            var classOOP = classObjectId;
            _allocator.SetSlot(objectOOP, 0, classOOP);
        }
    }

    private void SetRuntimeObjectClassReferences()
    {
        // Set class references for symbols (created during symbol interning)
        var allSymbols = SymbolRegistry.AllSymbols();
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");

        foreach (var (index, symbolOOP) in allSymbols)
        {
            _allocator.SetSlot(symbolOOP, 0, symbolClassOOP);
        }

        // Set class reference for SystemDictionary
        if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictId))
        {
            SmalltalkOOP systemDictClassId = GlobalRegistry.LookupClassOOP("SystemDictionary");
            var systemDictOOP = systemDictId;
            var systemDictClassOOP = systemDictClassId;
            _allocator.SetSlot(systemDictOOP, 0, systemDictClassOOP);
        }

        // Set class reference for USTable (Array)
        if (GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableId))
        {
            SmalltalkOOP arrayClassId = GlobalRegistry.LookupClassOOP("Array");
            var ustableOOP = ustableId;
            var arrayClassOOP = arrayClassId;
            _allocator.SetSlot(ustableOOP, 0, arrayClassOOP);
        }
    }

    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }

    #endregion

    #region Instance Variable Initialization

    /// <summary>
    /// Initializes all unset instance variables to nil.
    /// This is the final initialization pass of the multi-pass architecture.
    /// </summary>
    private void InitializeUnsetInstanceVariables()
    {
        int instanceVariablesInitialized = 0;
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Step 1: Initialize class object instance variables
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            // Get the number of instance variables for this type of class object
            string classOfClassName = cls.IsMetaclass ? "Metaclass" : "Class";
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(classOfClassName, _bundle);

            // Initialize ONLY uninitialized instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((classOOP, i)))
                {
                    _allocator.SetInstanceVariable(classOOP, i, nilOOP);
                    instanceVariablesInitialized++;

                    // Verify Object.superclass is being set to nil
                    if (!cls.IsMetaclass && _bundle.Symbols[cls.NameIndex] == "Object")
                    {
                        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);
                        if (i == superclassIndex)
                        {
                            Console.WriteLine($"    Initialized class:Object.superclass = nil (field index {i})");
                        }
                    }
                }
            }
        }

        // Step 2: Initialize instance variables for singleton objects
        InitializeSingletonInstanceVariables("nil", "UndefinedObject", nilOOP);
        InitializeSingletonInstanceVariables("true", "True", nilOOP);
        InitializeSingletonInstanceVariables("false", "False", nilOOP);
        InitializeSingletonInstanceVariables("ProcessorScheduler", "ProcessorScheduler", nilOOP);

        // Step 3: Initialize instance variables for other allocated objects and set JSON values
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
                var valueOOP = ResolveFieldValue(obj.Fields[i]);
                _allocator.SetInstanceVariable(objectOOP, i, valueOOP);
            }

            // Third: Set indexed fields if any
            for (int i = 0; i < obj.IndexedFields.Length; i++)
            {
                var valueOOP = ResolveFieldValue(obj.IndexedFields[i]);
                _allocator.SetIndexedOOP(objectOOP, i, valueOOP);
            }
        }

        // Step 4: Initialize SystemDictionary and other special objects
        InitializeSpecialObjectInstanceVariables(nilOOP);

        // Step 5: Initialize Dictionary indexed slots to nil
        InitializeDictionaryIndexedSlots(nilOOP);

        Console.WriteLine($"  Initialized {instanceVariablesInitialized} instance variables to nil");
    }

    private void InitializeSingletonInstanceVariables(string globalName, string className, SmalltalkOOP nilOOP)
    {
        if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);
            var objectOOP = objectId;

            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((objectOOP, i)))
                {
                    _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                }
            }
        }
    }

    private void InitializeSpecialObjectInstanceVariables(SmalltalkOOP nilOOP)
    {
        // Initialize SystemDictionary instance variables
        if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("SystemDictionary", _bundle);
            var systemDictOOP = systemDictId;

            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((systemDictOOP, i)))
                {
                    _allocator.SetInstanceVariable(systemDictOOP, i, nilOOP);
                }
            }
        }

        // Note: USTable (Array) has 0 instance variables, so no initialization needed
        // Symbols also have 0 instance variables in the standard Smalltalk-80 implementation
    }

    /// <summary>
    /// Initializes Dictionary indexed slots to nil.
    /// </summary>
    private void InitializeDictionaryIndexedSlots(SmalltalkOOP nilOOP)
    {
        // Get Dictionary class OOP to identify which objects are Dictionaries
        if (!GlobalRegistry.TryGet("class:Dictionary", out SmalltalkOOP dictClassOOP))
            return;

        // Iterate through all class objects and initialize their classPool dictionaries
        foreach (var cls in _bundle.Classes)
        {
            if (cls.IsMetaclass)
                continue;

            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            // Find classPool instance variable
            string templateClassName = "Class";
            int classPoolIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "classPool", _bundle);
            if (classPoolIndex < 0)
                continue;

            // Get the classPool Dictionary OOP
            var classPoolOOP = _allocator.GetInstanceVariable(classOOP, classPoolIndex);
            if (classPoolOOP == SmalltalkOOP.Null)
                continue;

            // Check if this object is a Dictionary by checking its class OOP
            var objectClassOOP = _heapManager.GetClassOOP(classPoolOOP);
            if (objectClassOOP != dictClassOOP)
                continue;

            // Initialize all indexed slots to nil
            int indexedSize = _allocator.GetIndexedSize(classPoolOOP);
            for (int i = 0; i < indexedSize; i++)
            {
                _allocator.SetIndexedOOP(classPoolOOP, i, nilOOP);
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Resolves a field value from the discriminated union to an OOP.
    /// </summary>
    private SmalltalkOOP ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => AllocateFloat(GetFloatValue(value.Value!)),
            "symbolRef" => InternSymbol(_symbols[value.Index!.Value]),
            "stringRef" => InternString(_bundle.Strings[value.Index!.Value]),
            "objectRef" => _localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP) ? objOOP : SpecialObjectsArray.Instance.Nil,
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

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private SmalltalkOOP InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        // Set String class reference (since we're after class reference init)
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);

        return stringOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private SmalltalkOOP AllocateCharacter(int codePoint)
    {
        var characterOOP = _allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        // Set Character class reference
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private SmalltalkOOP AllocateByteArray(string encoding, string data)
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

        // Set ByteArray class reference
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value)
    {
        var floatOOP = _allocator.AllocateByteArray("Float", 8); // 8 bytes for IEEE 754 double

        // Store the double as 8 bytes
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            _allocator.SetIndexedByte(floatOOP, i, bytes[i]);
        }

        // Set Float class reference
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        return floatOOP;
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

    #endregion
}