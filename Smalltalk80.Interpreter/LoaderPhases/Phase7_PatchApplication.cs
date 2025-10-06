using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 7: Apply Patches
/// Applies post-allocation patches to fix circular references and late-bound values.
/// Supports multiple patch operations: setValue, setAssociationValue, addToSystemDictionary.
/// </summary>
public class Phase7_PatchApplication
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase7_PatchApplication(
        ColdStartBundle bundle,
        ISmalltalkHeap heap,
        Dictionary<string, int> localIdMap,
        IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
        _symbols = symbols;
    }

    public void Run()
    {
        foreach (var patch in _bundle.Patches)
        {
            // Skip metaclass wiring patches - already handled in Phase 3
            if (patch.Target.Kind == "metaclassWiring")
                continue;

            int targetId = ResolveTarget(patch.Target);
            int valueId = patch.Value != null ? ResolveValue(patch.Value) : GlobalRegistry.NilObjectId;

            switch (patch.Operation)
            {
                case "setValue":
                    _heap.SetField(targetId, patch.Target.FieldIndex!.Value, valueId);
                    break;

                case "setAssociationValue":
                    _heap.SetNamedSlot(targetId, "value", valueId);
                    break;

                case "addToSystemDictionary":
                    string key = _symbols[patch.Value!.SymbolIndex!.Value];
                    _heap.SetNamedSlot(GlobalRegistry.SystemDictionaryId, key, valueId);
                    break;

                case "wireMetaclass":
                    // Skip - already handled in Phase 3
                    break;

                default:
                    throw new InvalidOperationException($"Unknown patch operation: {patch.Operation}");
            }
        }
    }

    /// <summary>
    /// Resolves the target of a patch to an object ID.
    /// </summary>
    private int ResolveTarget(PatchTarget target)
    {
        return target.Kind switch
        {
            "objectField" => _localIdToObjectId[target.ObjectLocalId!],
            "methodLiteral" => _localIdToObjectId[target.MethodLocalId!],
            "classSlot" => _localIdToObjectId[target.ClassLocalId!],
            "metaclassWiring" => 0, // Should be skipped in Run(), but return dummy value
            _ => throw new NotSupportedException($"Unsupported patch target: {target.Kind}")
        };
    }

    /// <summary>
    /// Resolves a patch value to an object ID.
    /// </summary>
    private int ResolveValue(PatchValue value)
    {
        return value.Type switch
        {
            "objectRef" => _localIdToObjectId[value.LocalId!],
            "symbolRef" => _heap.InternSymbol(_symbols[value.SymbolIndex!.Value]),
            "smallInt" => _heap.EncodeSmallInteger(value.IntValue!.Value),
            "nil" => GlobalRegistry.NilObjectId,
            _ => throw new InvalidOperationException($"Unknown patch value type: {value.Type}")
        };
    }
}
