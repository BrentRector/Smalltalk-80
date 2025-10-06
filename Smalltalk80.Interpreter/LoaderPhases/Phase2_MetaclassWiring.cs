using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 2: Wire Metaclass Relationships
/// Establishes bidirectional links between classes and their metaclasses.
/// Sets the 'class' slot on instance-side classes and 'metaclassOf' on metaclasses.
/// </summary>
public class Phase2_MetaclassWiring
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase2_MetaclassWiring(ColdStartBundle bundle, ISmalltalkHeap heap, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (cls.MetaclassOfLocalId is string instanceId)
            {
                int metaId = _localIdToObjectId[cls.LocalId];
                int instId = _localIdToObjectId[instanceId];

                // Wire bidirectional metaclass relationship
                _heap.SetNamedSlot(instId, "class", metaId);
                _heap.SetNamedSlot(metaId, "metaclassOf", instId);
            }
        }
    }
}
