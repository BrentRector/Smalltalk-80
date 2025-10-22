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
        // IMPORTANT: For class objects, there are TWO different "slot 0" concepts:
        //   - Header slot 0 (memory offset 0): The class reference (set here)
        //   - Instance variable 0 (memory offset 4): The superclass field (set in Phase 3)
        //
        // SetSlot(obj, 0) sets the class reference header (memory offset 0)
        // SetInstanceVariable(obj, 0) sets the first instance variable (memory offset 4)
        //
        // These are DIFFERENT slots and both need to be set!
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            if (cls.IsMetaclass)
            {
                // Debug: Check superclass BEFORE setting class reference
                if (classOOP.RawValue == 8530)
                {
                    var superclassBefore = _heapManager.GetInstanceVariable(classOOP, 0);
                    Console.WriteLine($"  PhaseX: BEFORE SetSlot: meta:Point superclass = {superclassBefore}");
                }

                // Metaclass objects point to the Metaclass class
                var metaclassClassOOP = GlobalRegistry.LookupClassOOP("Metaclass");
                _allocator.SetSlot(classOOP, 0, metaclassClassOOP); // Sets header, NOT instance variable

                // Debug: Check superclass AFTER setting class reference
                if (classOOP.RawValue == 8530)
                {
                    var superclassAfter = _heapManager.GetInstanceVariable(classOOP, 0);
                    Console.WriteLine($"  PhaseX: AFTER SetSlot: meta:Point superclass = {superclassAfter}");
                }

                // Debug: Show first few and OOP 8530 specifically
                if (classReferencesSet < 5 || classOOP.RawValue == 8530)
                {
                    Console.WriteLine($"  PhaseX: {cls.LocalId} (OOP={classOOP}).class = meta:Metaclass (OOP={metaclassClassOOP})");
                }
            }
            else
            {
                // Instance class objects point to their metaclass
                string instanceClassName = GetClassNameFromLocalId(cls.LocalId);
                string metaclassLocalId = $"meta:{instanceClassName}";

                if (_localIdToObjectId.TryGetValue(metaclassLocalId, out SmalltalkOOP metaclassOOP))
                {
                    _allocator.SetSlot(classOOP, 0, metaclassOOP); // Sets header, NOT instance variable

                    // Debug: Show first few and class:Point specifically
                    if (classReferencesSet < 5 || instanceClassName == "Point")
                    {
                        Console.WriteLine($"  PhaseX: {cls.LocalId} (OOP={classOOP}).class = {metaclassLocalId} (OOP={metaclassOOP})");
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
        // These were allocated during symbol interning and other phases
        SetRuntimeObjectClassReferences();
        
        // Step 5: Fix any remaining objects with NULL_CLASS (classOOP=0)
        // This catches dynamically allocated objects like Associations that weren't tracked
        FixRemainingNullClassReferences();

        // Debug: Verify superclass field is still intact for meta:Point after Phase X
        if (_localIdToObjectId.TryGetValue("meta:Point", out SmalltalkOOP metaPointOOP))
        {
            var superclassOOP = _heapManager.GetInstanceVariable(metaPointOOP, 0);
            Console.WriteLine($"  PhaseX Complete: Verified meta:Point (OOP={metaPointOOP}) superclass = {superclassOOP}");
        }
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
    
    private void FixRemainingNullClassReferences()
    {
        // NOTE: This method should NOT iterate through raw object table indices.
        // All objects MUST be properly tracked in _localIdToObjectId during allocation.
        // If objects are missing class references, that indicates a bug in earlier phases
        // that must be fixed at the source, not patched here.
        
        // This method is intentionally left as a no-op.
        // If NULL_CLASS objects exist, they will be caught by Phase 14 validation.
    }
}