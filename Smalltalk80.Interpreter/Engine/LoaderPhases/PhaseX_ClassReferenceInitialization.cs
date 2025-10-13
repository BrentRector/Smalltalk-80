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
    private readonly Dictionary<string, int> _localIdToObjectId;

    public PhaseX_ClassReferenceInitialization(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, int> localIdToObjectId)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
    }

    public void Run()
    {
        Console.WriteLine("Multi-Pass Phase X: Initializing class references...");
        
        int classReferencesSet = 0;

        // Step 1: Set class references for all class objects themselves
        // Each class object's slot 0 should point to its metaclass
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out int classObjectId))
                continue;

            var classOOP = SmalltalkOOP.FromObjectTableIndex(classObjectId);
            
            if (cls.IsMetaclass)
            {
                // Metaclass objects point to the Metaclass class
                int metaclassClassId = GlobalRegistry.LookupClassObjectId("Metaclass");
                var metaclassClassOOP = SmalltalkOOP.FromObjectTableIndex(metaclassClassId);
                _allocator.SetSlot(classOOP, 0, metaclassClassOOP);
                Console.WriteLine($"DEBUG: Set metaclass {GetClassNameFromLocalId(cls.LocalId)} class reference to Metaclass class");
            }
            else
            {
                // Instance class objects point to their metaclass
                string instanceClassName = GetClassNameFromLocalId(cls.LocalId);
                string metaclassLocalId = $"meta:{instanceClassName}";
                
                if (_localIdToObjectId.TryGetValue(metaclassLocalId, out int metaclassObjectId))
                {
                    var metaclassOOP = SmalltalkOOP.FromObjectTableIndex(metaclassObjectId);
                    _allocator.SetSlot(classOOP, 0, metaclassOOP);
                    Console.WriteLine($"DEBUG: Set class {instanceClassName} class reference to its metaclass");
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
            if (!_localIdToObjectId.TryGetValue(obj.LocalId, out int objectId))
                continue;

            string className = _bundle.Symbols[obj.ClassNameIndex];
            int classObjectId = GlobalRegistry.LookupClassObjectId(className);
            
            var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
            var classOOP = SmalltalkOOP.FromObjectTableIndex(classObjectId);
            _allocator.SetSlot(objectOOP, 0, classOOP);
            
            classReferencesSet++;
        }

        // Step 4: Set class references for symbols, strings, and other runtime objects
        // These were allocated during symbol interning and other phases
        SetRuntimeObjectClassReferences();

        Console.WriteLine($"DEBUG: Set class references for {classReferencesSet} objects");
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
}