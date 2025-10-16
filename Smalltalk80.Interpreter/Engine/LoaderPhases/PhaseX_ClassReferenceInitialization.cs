using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Multi-Pass Phase X: Class Reference Initialization
/// 
/// Sets class references (slot 0) for all allocated objects after all classes have been created.
/// This is the first initialization pass of the multi-pass architecture, resolving the circular 
/// dependency where objects need class references but classes don't exist during allocation.
/// 
/// For each allocated object:
/// 1. Resolve its class name from JSON bundle data
/// 2. Find the corresponding class object ID
/// 3. Set slot 0 to point to the class object
/// </summary>
public class PhaseX_ClassReferenceInitialization
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly ISmalltalkHeapManager _heapManager;

    public PhaseX_ClassReferenceInitialization(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        ISmalltalkHeapManager heapManager)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
        _heapManager = heapManager;
    }

    public void Run()
    {
        int classReferencesSet = 0;

        // Step 1: Set class references for all class objects themselves
        // Each class object's slot 0 should point to its metaclass
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;
            
            if (cls.IsMetaclass)
            {
                // Metaclass objects point to the Metaclass class
                int metaclassClassId = GlobalRegistry.LookupClassObjectId("Metaclass");
                var metaclassClassOOP = SmalltalkOOP.FromObjectTableIndex(metaclassClassId);
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
            int classObjectId = GlobalRegistry.LookupClassObjectId(className);
            
            var classOOP = SmalltalkOOP.FromObjectTableIndex(classObjectId);
            _allocator.SetSlot(objectOOP, 0, classOOP);
            
            classReferencesSet++;
        }

        // Step 4: Set class references for symbols, strings, and other runtime objects
        // These were allocated during symbol interning and other phases
        SetRuntimeObjectClassReferences();
        
        // Step 5: Fix any remaining objects with NULL_CLASS (classOOP=0)
        // This catches dynamically allocated objects like Associations that weren't tracked
        FixRemainingNullClassReferences();

    }

    private void SetSingletonClassReference(string globalName, string className)
    {
        if (GlobalRegistry.TryGet(globalName, out int objectId))
        {
            int classObjectId = GlobalRegistry.LookupClassObjectId(className);
            var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
            var classOOP = SmalltalkOOP.FromObjectTableIndex(classObjectId);
            _allocator.SetSlot(objectOOP, 0, classOOP);
        }
    }

    private void SetRuntimeObjectClassReferences()
    {
        // Set class references for symbols (created during symbol interning)
        var allSymbols = SymbolRegistry.AllSymbols();
        int symbolClassId = GlobalRegistry.LookupClassObjectId("Symbol");
        var symbolClassOOP = SmalltalkOOP.FromObjectTableIndex(symbolClassId);

        foreach (var (index, symbolObjectId) in allSymbols)
        {
            var symbolOOP = SmalltalkOOP.FromObjectTableIndex(symbolObjectId);
            _allocator.SetSlot(symbolOOP, 0, symbolClassOOP);
        }

        // Set class reference for SystemDictionary
        if (GlobalRegistry.TryGet("Smalltalk", out int systemDictId))
        {
            int systemDictClassId = GlobalRegistry.LookupClassObjectId("SystemDictionary");
            var systemDictOOP = SmalltalkOOP.FromObjectTableIndex(systemDictId);
            var systemDictClassOOP = SmalltalkOOP.FromObjectTableIndex(systemDictClassId);
            _allocator.SetSlot(systemDictOOP, 0, systemDictClassOOP);
        }

        // Set class reference for USTable (Array)
        if (GlobalRegistry.TryGet("USTable", out int ustableId))
        {
            int arrayClassId = GlobalRegistry.LookupClassObjectId("Array");
            var ustableOOP = SmalltalkOOP.FromObjectTableIndex(ustableId);
            var arrayClassOOP = SmalltalkOOP.FromObjectTableIndex(arrayClassId);
            _allocator.SetSlot(ustableOOP, 0, arrayClassOOP);
        }
    }

    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }
    
    private void FixRemainingNullClassReferences()
    {
        // Association objects created in Phase 6 are not tracked in _localIdToObjectId
        // These have format=Pointer, 2 instance variables, and classOOP=0
        // We need to find them and set their class reference to Association
        
        int associationClassId = GlobalRegistry.LookupClassObjectId("Association");
        var associationClassOOP = SmalltalkOOP.FromObjectTableIndex(associationClassId);
        
        // Scan all allocated objects looking for ones with NULL_CLASS (classOOP=0)
        int maxObjectId = _heapManager.GetObjectTableSize();
        int fixedCount = 0;
        var nullClassObjects = new List<(int objectId, ObjectFormat format, int instVars, int indexed)>();
        
        for (int objectId = 0; objectId <= maxObjectId; objectId++)
        {
            var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
            
            // Skip if object doesn't exist
            if (!_heapManager.IsValidObjectOOP(objectOOP))
                continue;
            
            // Check if this object has NULL_CLASS
            var classOOP = _heapManager.GetSlot(objectOOP, 0);
            if (classOOP.RawValue != 0)
                continue; // Already has a class
            
            // Log all NULL_CLASS objects for debugging
            int instVarCount = _heapManager.GetInstanceVariableCount(objectOOP);
            int indexedSize = _heapManager.GetIndexedSize(objectOOP);
            var format = _heapManager.GetObjectFormat(objectOOP);
            nullClassObjects.Add((objectId, format, instVarCount, indexedSize));
            
            // Check if this is an Association (2 instance variables, Pointer format, no indexed data)
            if (format == ObjectFormat.Pointer && instVarCount == 2 && indexedSize == 0)
            {
                // This is an Association object - set its class reference
                _heapManager.SetSlot(objectOOP, 0, associationClassOOP);
                fixedCount++;
            }
        }
        
    }
}