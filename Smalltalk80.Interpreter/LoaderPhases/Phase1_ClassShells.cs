using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 1: Allocate Class Shells
/// Creates empty class objects for all classes in the bundle.
/// These are placeholder objects that will be populated in subsequent phases.
/// </summary>
public class Phase1_ClassShells
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase1_ClassShells(ColdStartBundle bundle, ISmalltalkHeap heap, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        // First, create the well-known singleton objects: nil, true, false
        int nilId = _heap.AllocateObject("UndefinedObject", "fixed", fixedSize: 0, indexedSize: 0);
        int trueId = _heap.AllocateObject("True", "fixed", fixedSize: 0, indexedSize: 0);
        int falseId = _heap.AllocateObject("False", "fixed", fixedSize: 0, indexedSize: 0);

        GlobalRegistry.Register("nil", nilId);
        GlobalRegistry.Register("true", trueId);
        GlobalRegistry.Register("false", falseId);

        // Count EXACTLY what goes in SystemDictionary - NO TEMPORARY OBJECTS!
        // Contents: All instance classes + pool dictionaries
        int instanceClassCount = _bundle.Classes.Count(c => !c.IsMetaclass);
        int poolCount = _bundle.Objects.Count(o => o.LocalId.StartsWith("pool:"));
        int totalEntries = instanceClassCount + poolCount; // 237 classes + 2 pools = 239

        // SystemDictionary is a Dictionary with indexed Association array
        // Pre-allocate to EXACT size - associations will be added during class creation
        int smalltalkId = _heap.AllocateObject("SystemDictionary", "pointers", fixedSize: 2, indexedSize: totalEntries);

        GlobalRegistry.Register("Smalltalk", smalltalkId);

        // Track next index for SystemDictionary
        int nextSmalltalkIndex = 0;

        // Now allocate all class shells
        foreach (var cls in _bundle.Classes)
        {
            // Allocate a placeholder Class object with standard structure
            // fixedSize: 10 covers the standard Class instance variables
            int objectId = _heap.AllocateObject("Class", "pointers", fixedSize: 10, indexedSize: 0);
            _localIdToObjectId[cls.LocalId] = objectId;

            // Register class in GlobalRegistry
            // For instance classes (class:Name), register as "Name"
            // For metaclasses (meta:Name), DON'T register (only instance classes need global lookup)
            if (!cls.IsMetaclass)
            {
                string className = GetClassNameFromLocalId(cls.LocalId);
                GlobalRegistry.RegisterClass(className, objectId);

                // Add the class to Smalltalk dictionary as Association
                int classNameSymbol = _heap.InternSymbol(className);
                int association = _heap.AllocateAssociation(classNameSymbol, objectId);
                _heap.SetIndexedField(smalltalkId, nextSmalltalkIndex++, association);
            }
        }

        // Store the next available index for other phases to use
        _localIdToObjectId["__smalltalk_next_index__"] = nextSmalltalkIndex;
    }

    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }
}
