using System.Text.Json;
using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 8: Prepare Init Expressions
/// Creates method objects for initialization expressions but does NOT add them to SystemDictionary.
/// Instead, returns an array of init expression methods that will be:
/// 1. Temporarily stored as a GC root during verification
/// 2. Executed as DoIts after GC verification passes
/// 3. Discarded after execution (allowing garbage collection)
/// </summary>
public class Phase8_InitExecution
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase8_InitExecution(ColdStartBundle bundle, ISmalltalkHeap heap, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdMap;
    }

    /// <summary>
    /// Creates init expression method objects and returns the array ID for temporary GC root storage.
    /// Returns 0 if there are no init expressions.
    /// </summary>
    public int Run()
    {
        // Create array to hold all init expression method objects
        var initExprMethods = new List<int>();

        foreach (var expr in _bundle.InitExpressions)
        {
            var literalIds = expr.Literals.Select(ResolveLiteral).ToArray();

            // Create a method object for the init expression to keep its literals alive
            // This will be executed after GC verification
            var bytecodes = DecodeBytecodes(expr.Bytecodes);
            int initMethodId = _heap.CreateCompiledMethod(
                new MethodDescriptor
                {
                    LocalId = expr.LocalId,
                    HomeClassLocalId = "class:UndefinedObject", // placeholder
                    SelectorIndex = 0,
                    Header = expr.Header, // Use the actual header from init expression
                    Literals = Array.Empty<LiteralValue>(), // already resolved
                    Bytecodes = expr.Bytecodes
                },
                literalIds,
                bytecodes);

            initExprMethods.Add(initMethodId);
            _localIdToObjectId[expr.LocalId] = initMethodId;
        }

        // Return the array (or 0 if no init expressions)
        // This will be stored temporarily as a GC root, then executed and discarded
        if (initExprMethods.Count > 0)
        {
            return _heap.AllocateArray(initExprMethods.ToArray());
        }

        return 0;
    }

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

    /// <summary>
    /// Resolves an init expression literal to an object ID.
    /// Init expressions use the same LiteralValue type as methods.
    /// v1.0.3: Supports "compiledMethod" for nested blocks.
    /// </summary>
    private int ResolveLiteral(LiteralValue literal)
    {
        return literal.Type switch
        {
            "symbol" => _heap.InternSymbol(_bundle.Symbols[literal.Index!.Value]),
            "string" => _heap.InternString(_bundle.Strings[literal.Index!.Value]),
            "smallInt" => _heap.EncodeSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => GlobalRegistry.NilObjectId,
            "true" => GlobalRegistry.TrueObjectId,
            "false" => GlobalRegistry.FalseObjectId,
            "objectRef" => _localIdToObjectId[literal.LocalId!],
            "compiledMethod" => _localIdToObjectId[literal.LocalId!],  // v1.0.3: Blocks reference their method via localId
            "character" => _heap.AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => _heap.AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveAssociation(literal.Association!),
            "array" => _heap.AllocateArray(literal.Elements!.Select(ResolveLiteral).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    private int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

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

    private int AllocateFloat(double value)
    {
        return _heap.AllocateFloat(value);
    }

    /// <summary>
    /// Resolves an association literal (key/value pair).
    /// CRITICAL: Reuse existing SystemDictionary associations instead of creating duplicates with nil value.
    /// </summary>
    private int ResolveAssociation(AssociationLiteral assoc)
    {
        int keyId = _heap.InternSymbol(_bundle.Symbols[assoc.KeyIndex]);
        string keyName = _bundle.Symbols[assoc.KeyIndex];

        // CRITICAL FIX: If this is a class association and valueRef is None,
        // look up the existing association from SystemDictionary instead of creating new one with nil
        if (assoc.ValueRef?.LocalId == null)
        {
            // Try to find existing class association in SystemDictionary
            if (GlobalRegistry.TryGet($"class:{keyName}", out int classOop))
            {
                // This is a known class - find its existing association in SystemDictionary
                var heap = (SmalltalkHeap)_heap;
                int sysDictId = GlobalRegistry.SystemDictionaryId;
                int sysDictSize = heap.GetIndexedSize(sysDictId);

                for (int i = 0; i < sysDictSize; i++)
                {
                    int assocId = heap.GetIndexedField(sysDictId, i);
                    if (assocId == 0) continue;

                    int assocKeyId = heap.GetField(assocId, 0);
                    if (assocKeyId == keyId)
                    {
                        // Found the existing association - reuse it!
                        if (keyName == "ActionMenu")
                        {
                            int assocValue = heap.GetField(assocId, 1);
                            Console.WriteLine($"DEBUG Phase9: Reusing ActionMenu assoc OOP={assocId}, value={assocValue}");
                        }
                        return assocId;
                    }
                }
            }
        }

        // Fall back to creating new association
        int valueId = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilObjectId;

        return _heap.AllocateAssociation(keyId, valueId);
    }
}
