using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

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
        foreach (var patch in _bundle.Patches)
        {
            // Skip metaclass wiring patches - already handled in Phase 3
            if (patch.Target.Kind == "metaclassWiring")
                continue;

            SmalltalkOOP targetOOP = ResolveTarget(patch.Target);
            SmalltalkOOP valueOOP = patch.Value != null ? ResolveValue(patch.Value) : GlobalRegistry.NilOOP;

            // Find the localId for debug purposes
            string? targetLocalId = patch.Target.ObjectLocalId ?? patch.Target.MethodLocalId ?? patch.Target.ClassLocalId ?? "unknown";

            switch (patch.Operation)
            {
                case "setValue":
                    SetField(targetOOP, patch.Target.FieldIndex!.Value, valueOOP);
                    break;

                case "setAssociationValue":
                    SetNamedSlot(targetOOP, "value", valueOOP);
                    break;

                case "addToSystemDictionary":
                    string key = _symbols[patch.Value!.SymbolIndex!.Value];
                    SetNamedSlot(GlobalRegistry.SystemDictionaryOOP, key, valueOOP);
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
    /// Resolves the target of a patch to a SmalltalkOOP.
    /// </summary>
    private SmalltalkOOP ResolveTarget(PatchTarget target)
    {
        return target.Kind switch
        {
            "objectField" => _localIdToObjectId[target.ObjectLocalId!],
            "methodLiteral" => GetMethodLiteral(target.MethodLocalId!, target.LiteralIndex!.Value),
            "classSlot" => _localIdToObjectId[target.ClassLocalId!],
            "metaclassWiring" => SmalltalkOOP.FromObjectTableIndex(0), // Should be skipped in Run(), but return dummy value
            _ => throw new NotSupportedException($"Unsupported patch target: {target.Kind}")
        };
    }

    private SmalltalkOOP GetMethodLiteral(string methodLocalId, int literalIndex)
    {
        SmalltalkOOP methodOOP = _localIdToObjectId[methodLocalId];
        
        // CompiledMethod structure: [header, literal1, literal2, ..., literalN, bytecodeArrayOOP]
        // Literals start at indexed slot 1 (slot 0 is header)
        var literalOOP = _allocator.GetIndexedOOP(methodOOP, literalIndex + 1);
        return literalOOP;
    }

    /// <summary>
    /// Resolves a patch value to a SmalltalkOOP.
    /// </summary>
    private SmalltalkOOP ResolveValue(PatchValue value)
    {
        return value.Type switch
        {
            "objectRef" => _localIdToObjectId[value.LocalId!],
            "symbolRef" => InternSymbol(_symbols[value.SymbolIndex!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(value.IntValue!.Value),
            "nil" => GlobalRegistry.NilOOP,
            _ => throw new InvalidOperationException($"Unknown patch value type: {value.Type}")
        };
    }

    /// <summary>
    /// Sets a field (instance variable) in an object.
    /// </summary>
    private void SetField(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP valueOOP)
    {
        _allocator.SetInstanceVariable(objectOOP, fieldIndex, valueOOP);
    }

    /// <summary>
    /// Sets a named slot in an object by looking up the slot name in the class's instance variable names.
    /// Handles objects created dynamically (like Associations) that don't have localId mappings.
    /// </summary>
    private void SetNamedSlot(SmalltalkOOP objectOOP, string slotName, SmalltalkOOP valueOOP)
    {
        // Try to find the class definition for this object
        var classDefinition = FindClassDefinitionForObject(objectOOP);
        
        if (classDefinition == null)
        {
            int instVarCount = _allocator.GetInstanceVariableCount(objectOOP);
            
            // Handle dynamically created objects (like Associations) that don't have localId mappings
            if (slotName == "value")
            {
                if (instVarCount < 2)
                {
                    throw new InvalidOperationException($"Object {objectOOP} claims to be Association but only has {instVarCount} instance variables (expected 2 for key+value)");
                }
                // For Association objects: key = index 0, value = index 1
                // This is the standard Association layout in Smalltalk-80
                _allocator.SetInstanceVariable(objectOOP, 1, valueOOP);
                return;
            }
            else if (slotName == "key")
            {
                if (instVarCount < 1)
                {
                    throw new InvalidOperationException($"Object {objectOOP} claims to be Association but only has {instVarCount} instance variables (expected at least 1 for key)");
                }
                // For Association objects: key = index 0, value = index 1
                _allocator.SetInstanceVariable(objectOOP, 0, valueOOP);
                return;
            }
            else
            {
                throw new InvalidOperationException($"Cannot find class definition for object {objectOOP} and unknown slot '{slotName}'");
            }
        }
        
        // Look up the slot name in the class's instance variable names
        int index = FindInstanceVariableIndex(classDefinition, slotName);
        if (index == -1)
        {
            throw new ArgumentException($"Instance variable '{slotName}' not found in class {GetClassName(classDefinition)}");
        }
        
        _allocator.SetInstanceVariable(objectOOP, index, valueOOP);
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    private SmalltalkOOP InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Encodes a SmallInteger as a tagged OOP.
    /// </summary>
    private SmalltalkOOP EncodeSmallInteger(int value)
    {
        return SmalltalkOOP.FromSmallInteger(value);
    }

    /// <summary>
    /// Finds the class definition for an object by looking up its localId.
    /// </summary>
    private ClassDescriptor? FindClassDefinitionForObject(SmalltalkOOP objectOOP)
    {
        // Find the localId for this object
        var localId = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectOOP).Key;
        if (localId == null) return null;
        
        // Find the class definition in the bundle
        return _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
    }

    /// <summary>
    /// Finds the index of an instance variable by name in a class definition.
    /// </summary>
    private int FindInstanceVariableIndex(ClassDescriptor classDefinition, string variableName)
    {
        for (int i = 0; i < classDefinition.InstVarNames.Length; i++)
        {
            int symbolIndex = classDefinition.InstVarNames[i];
            string varName = _bundle.Symbols[symbolIndex];
            if (varName == variableName)
            {
                return i;
            }
        }
        return -1; // Not found
    }

    /// <summary>
    /// Gets the class name for debugging purposes.
    /// </summary>
    private string GetClassName(ClassDescriptor classDefinition)
    {
        return _bundle.Symbols[classDefinition.NameIndex];
    }
}
