using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 3: Resolve Class Fields
/// Populates class objects with their name, superclass, comment, and pool dictionaries.
/// </summary>
public class Phase3_ClassResolution
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _strings;

    public Phase3_ClassResolution(
        ColdStartBundle bundle,
        ISmalltalkHeap heap,
        Dictionary<string, int> localIdMap,
        IReadOnlyList<string> symbols,
        IReadOnlyList<string> strings)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
        _symbols = symbols;
        _strings = strings;
    }

    public void Run()
    {
        foreach (var cls in _bundle.Classes)
        {
            int classId = _localIdToObjectId[cls.LocalId];
            string name = _symbols[cls.NameIndex];
            _heap.SetNamedSlot(classId, "name", _heap.InternSymbol(name));

            // Set superclass reference
            if (cls.SuperNameIndex is int superIdx)
            {
                string superName = _symbols[superIdx];
                int superId = GlobalRegistry.LookupClassObjectId(superName);
                _heap.SetNamedSlot(classId, "superclass", superId);
            }

            // Set class comment
            if (cls.CommentStringIndex is int commentIdx)
            {
                string comment = _strings[commentIdx];
                _heap.SetNamedSlot(classId, "comment", _heap.InternString(comment));
            }

            // Set pool dictionaries (skip if not yet allocated - will be patched later)
            foreach (var poolId in cls.PoolLocalIds)
            {
                if (_localIdToObjectId.TryGetValue(poolId, out int poolObjId))
                {
                    _heap.SetNamedSlot(classId, "pool", poolObjId); // or append to pool list
                }
                // else: pool not yet allocated, will be wired in patch phase
            }
        }
    }
}
