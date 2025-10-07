using System.Text.Json;
using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 6: Method Installation
/// Reads each method descriptor, resolves its selector and literals, decodes bytecodes,
/// and installs the resulting CompiledMethod into the owning class's method dictionary.
///
/// Literal value types supported:
/// - symbol, string, smallInt, objectRef, character, bytes
/// - association (with optional patching)
/// - array (nested literals)
///
/// Note: Bytecode validation (stack analysis, operand validation, decompilation)
/// has been moved to Phase 12 (optional).
/// </summary>
public class Phase6_MethodInstallation
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _strings;

    public Phase6_MethodInstallation(
        ColdStartBundle bundle,
        ISmalltalkHeap heap,
        Dictionary<string, int> localIdToObjectId,
        IReadOnlyList<string> symbols,
        IReadOnlyList<string> strings)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
        _strings = strings;
    }

    public void Run()
    {
        // FIRST: Count methods per class to pre-allocate method dictionaries to EXACT size
        var methodsPerClass = new Dictionary<string, List<(int selectorId, int methodId)>>();

        // Pre-count methods per class
        foreach (var method in _bundle.Methods)
        {
            if (!methodsPerClass.ContainsKey(method.HomeClassLocalId))
            {
                methodsPerClass[method.HomeClassLocalId] = new List<(int, int)>();
            }
        }

        // Pre-allocate all method dictionaries to EXACT final size (no resizing!)
        foreach (var kvp in methodsPerClass)
        {
            int classId = _localIdToObjectId[kvp.Key];
            int methodCount = _bundle.Methods.Count(m => m.HomeClassLocalId == kvp.Key);
            int methodDictId = _heap.CreateMethodDictionary(methodCount);
            _heap.SetNamedSlot(classId, "methodDictionary", methodDictId);
        }

        // Create and install all methods
        int totalMethods = 0;
        foreach (var method in _bundle.Methods)
        {
            totalMethods++;
            int selectorId = _heap.InternSymbol(_symbols[method.SelectorIndex]);
            int homeClassId = _localIdToObjectId[method.HomeClassLocalId];

            var literalIds = method.Literals.Select(ResolveLiteralValue).ToArray();
            var bytecodes = DecodeBytecodes(method.Bytecodes);

            int methodId = _heap.CreateCompiledMethod(method, literalIds, bytecodes);
            _heap.InstallMethod(homeClassId, selectorId, methodId);

            _localIdToObjectId[method.LocalId] = methodId;
        }

        Console.WriteLine($"  Methods installed: {totalMethods}");
    }


    /// <summary>
    /// Resolves a literal value from the discriminated union to an object ID.
    /// </summary>
    private int ResolveLiteralValue(LiteralValue literal)
    {
        return literal.Type switch
        {
            "symbol" => _heap.InternSymbol(_symbols[literal.Index!.Value]),
            "string" => _heap.InternString(_strings[literal.Index!.Value]),
            "smallInt" => _heap.EncodeSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => GlobalRegistry.NilObjectId,
            "true" => GlobalRegistry.TrueObjectId,
            "false" => GlobalRegistry.FalseObjectId,
            "objectRef" => _localIdToObjectId[literal.LocalId!],
            "character" => _heap.AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => _heap.AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveAssociation(literal.Association!),
            "array" => _heap.AllocateArray(literal.Elements!.Select(ResolveLiteralValue).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    private int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    /// <summary>
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    private double GetFloatValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private int AllocateFloat(double value)
    {
        // Float objects in Smalltalk-80 are typically stored as a word-indexed object
        // containing the IEEE 754 representation
        byte[] bytes = BitConverter.GetBytes(value);
        return _heap.AllocateByteArray("base64", Convert.ToBase64String(bytes));
    }

    /// <summary>
    /// Resolves an association literal (key/value pair).
    /// </summary>
    private int ResolveAssociation(AssociationLiteral assoc)
    {
        int keyId = _heap.InternSymbol(_symbols[assoc.KeyIndex]);
        int valueId = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilObjectId;

        return _heap.AllocateAssociation(keyId, valueId);
    }

    /// <summary>
    /// Decodes bytecodes from either array or base64 string format.
    /// </summary>
    private byte[] DecodeBytecodes(object bytecodes)
    {
        return bytecodes switch
        {
            JsonElement e when e.ValueKind == JsonValueKind.Array =>
                e.EnumerateArray().Select(b => (byte)b.GetInt32()).ToArray(),
            JsonElement e when e.ValueKind == JsonValueKind.String =>
                Convert.FromBase64String(e.GetString()!),
            _ => throw new InvalidOperationException("Invalid bytecode format")
        };
    }
}
