using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 4: Allocate Method Dictionaries
/// Creates empty method dictionaries for all classes and sets the methodDictionary slot.
/// </summary>
public class Phase4_MethodDictionaries
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase4_MethodDictionaries(ColdStartBundle bundle, ISmalltalkHeap heap, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        // OBSOLETE: Method dictionaries now pre-allocated in Phase6_MethodInstallation
        // This phase is no longer used but kept for compatibility
    }
}
