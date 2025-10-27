using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 10: Apply Patches
/// Applies post-allocation patches to fix circular references and late-bound values.
/// Supports multiple patch operations: setValue, setAssociationValue, addToSystemDictionary.
/// </summary>
public class Phase10_PatchApplication
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;

    public Phase10_PatchApplication(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdMap,
        IReadOnlyList<string> symbols)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdMap;
        _symbols = symbols;
    }

    public void Run()
    {
        if (_bundle.Patches == null || _bundle.Patches.Length == 0)
        {
            return; // No patches to apply
        }

        foreach (var patch in _bundle.Patches)
        {
            switch (patch.Type)
            {
                case "SetInstanceVariable":
                    ApplySetInstanceVariablePatch(patch);
                    break;

                case "InstallMethodDictionary":
                    // Method dictionary should already exist from earlier phases
                    // This patch type is for forward compatibility
                    break;

                case "ReplaceLiteral":
                    ApplyReplaceLiteralPatch(patch);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown patch type: {patch.Type}");
            }
        }
    }

    private void ApplySetInstanceVariablePatch(PatchDescriptor patch)
    {
        var targetOOP = _localIdToObjectId[patch.TargetLocalId!];

        // Find field index by name
        int fieldIndex = FindFieldIndex(patch.TargetLocalId!, patch.FieldName!);

        // Resolve new value
        var valueOOP = ResolveFieldValue(patch.Value);

        // Apply patch
        _allocator.SetInstanceVariable(targetOOP, fieldIndex, valueOOP);
    }

    private void ApplyReplaceLiteralPatch(PatchDescriptor patch)
    {
        var methodOOP = _localIdToObjectId[patch.MethodLocalId!];
        var literalOOP = _allocator.GetIndexedOOP(methodOOP, patch.LiteralIndex!.Value + 1);

        var newValueOOP = ResolveFieldValue(patch.Value);

        // Check if the literal is an Association
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        var literalClassOOP = _allocator.GetSlot(literalOOP, 0);

        if (literalClassOOP == associationClassOOP)
        {
            // It's an Association - set its VALUE field (index 1), don't replace the Association itself
            _allocator.SetInstanceVariable(literalOOP, 1, newValueOOP);
        }
        else
        {
            // Not an Association - replace the literal itself
            _allocator.SetIndexedOOP(methodOOP, patch.LiteralIndex.Value + 1, newValueOOP);
        }
    }

    private int FindFieldIndex(string localId, string fieldName)
    {
        // Find the class descriptor for this object
        var classDescriptor = _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
        if (classDescriptor != null)
        {
            // Look through instance variable names
            for (int i = 0; i < classDescriptor.InstVarNames.Length; i++)
            {
                if (_symbols[classDescriptor.InstVarNames[i]] == fieldName)
                    return i;
            }
        }
        throw new InvalidOperationException($"Field '{fieldName}' not found for object '{localId}'");
    }

    private SmalltalkOOP ResolveFieldValue(FieldValue? value)
    {
        if (value == null)
            return GlobalRegistry.NilOOP;

        return value.Type switch
        {
            "nil" => GlobalRegistry.NilOOP,
            "true" => GlobalRegistry.TrueOOP,
            "false" => GlobalRegistry.FalseOOP,
            "smallInt" => SmalltalkOOP.FromSmallInteger((int)value.Value!),
            "objectRef" => _localIdToObjectId[value.LocalId!],
            "stringRef" => _allocator.AllocateString(_bundle.Strings[value.Index!.Value]),
            "symbolRef" => SymbolRegistry.GetSymbolOOP(value.Index!.Value),
            _ => throw new InvalidOperationException($"Unknown field value type: {value.Type}")
        };
    }
}
