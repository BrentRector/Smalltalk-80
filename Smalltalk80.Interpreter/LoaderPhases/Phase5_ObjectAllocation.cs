using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 5: Object Allocation
/// Reads from the JSON bundle and allocates real Smalltalk heap objects.
/// Handles:
/// - Resolving classNameIndex → Symbol → Class
/// - Decoding fieldValue discriminated unions
/// - Allocating fixed and indexed fields
/// - Mapping localId → objectId for patching and references
/// </summary>
public class Phase5_ObjectAllocation
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase5_ObjectAllocation(
        ColdStartBundle bundle,
        ISmalltalkHeap heap,
        Dictionary<string, int> localIdToObjectId,
        IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
    }

    public void Run()
    {
        int smalltalkIndex = _localIdToObjectId["__smalltalk_next_index__"];

        foreach (var obj in _bundle.Objects)
        {
            string className = _symbols[obj.ClassNameIndex];
            int classId = ResolveClassObjectId(className);
            int fixedSize = obj.Fields.Length;
            int indexedSize = obj.IndexedFields.Length;

            int objectId = _heap.AllocateObject(className, obj.FormatHint, fixedSize, indexedSize);
            _localIdToObjectId[obj.LocalId] = objectId;

            for (int i = 0; i < obj.Fields.Length; i++)
            {
                int valueId = ResolveFieldValue(obj.Fields[i]);
                _heap.SetField(objectId, i, valueId);
            }

            for (int i = 0; i < obj.IndexedFields.Length; i++)
            {
                int valueId = ResolveFieldValue(obj.IndexedFields[i]);
                _heap.SetIndexedField(objectId, i, valueId);
            }

            // Add pool dictionaries to Smalltalk dictionary
            if (obj.LocalId.StartsWith("pool:"))
            {
                string poolName = obj.LocalId.Substring(5); // Remove "pool:" prefix
                int poolNameSymbol = _heap.InternSymbol(poolName);
                int association = _heap.AllocateAssociation(poolNameSymbol, objectId);
                _heap.SetIndexedField(GlobalRegistry.SystemDictionaryId, smalltalkIndex++, association);
            }
        }

        // Update next index for Phase8
        _localIdToObjectId["__smalltalk_next_index__"] = smalltalkIndex;
    }

    /// <summary>
    /// Resolves a class name to its object ID.
    /// Assumes class objects were already allocated and mapped by name.
    /// </summary>
    private int ResolveClassObjectId(string className)
    {
        return GlobalRegistry.LookupClassObjectId(className);
    }

    /// <summary>
    /// Resolves a field value from the discriminated union to an object ID.
    /// </summary>
    private int ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => GlobalRegistry.NilObjectId,
            "true" => GlobalRegistry.TrueObjectId,
            "false" => GlobalRegistry.FalseObjectId,
            "smallInt" => _heap.EncodeSmallInteger((int)value.Value!),
            "symbolRef" => _heap.InternSymbol(_symbols[value.Index!.Value]),
            "stringRef" => _heap.InternString(_bundle.Strings[value.Index!.Value]),
            "objectRef" => _localIdToObjectId[value.LocalId!],
            "character" => _heap.AllocateCharacter(value.CodePoint!.Value),
            "bytes" => _heap.AllocateByteArray(value.Encoding!, value.Data!),
            _ => throw new InvalidOperationException($"Unknown field type: {value.Type}")
        };
    }
}
