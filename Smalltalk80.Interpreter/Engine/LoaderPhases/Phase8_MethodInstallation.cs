using System.Text.Json;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 8: Method Installation
/// Reads each method descriptor, resolves its selector and literals, decodes bytecodes,
/// and installs the resulting CompiledMethod into the owning class's method dictionary.
///
/// Literal value types supported:
/// - symbol, string, smallInt, objectRef, character, bytes
/// - association (with optional patching)
/// - array (nested literals)
///
/// Note: Bytecode validation (stack analysis, operand validation, decompilation)
/// has been moved to Phase 14 (optional).
/// </summary>
public class Phase8_MethodInstallation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _strings;

    public Phase8_MethodInstallation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        IReadOnlyList<string> symbols,
        IReadOnlyList<string> strings)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
        _strings = strings;
    }

    public void Run()
    {
        // Look up CompiledMethod and ByteArray classes - they were created in Phase2
        if (!_localIdToObjectId.TryGetValue("class:CompiledMethod", out var compiledMethodClassOOP))
        {
            throw new InvalidOperationException("CompiledMethod class not found in _localIdToObjectId - Phase 2 must run first");
        }
        
        if (!_localIdToObjectId.TryGetValue("class:ByteArray", out var byteArrayClassOOP))
        {
            throw new InvalidOperationException("ByteArray class not found in _localIdToObjectId - Phase 2 must run first");
        }
        
        // STEP 0: Build blockOwnerMap by scanning all method literals for block references
        // This map is needed at runtime to determine block home contexts
        var blockOwnerMap = new Dictionary<string, string>(); // blockLocalId -> ownerMethodOrBlockLocalId

        foreach (var method in _bundle.Methods)
        {
            string ownerLocalId = method.LocalId;

            foreach (var literal in method.Literals)
            {
                if (literal.Type == "compiledMethod" && !string.IsNullOrEmpty(literal.LocalId))
                {
                    // This method contains a block as a literal - record ownership
                    blockOwnerMap[literal.LocalId] = ownerLocalId;
                }
            }
        }

        // Store blockOwnerMap in GlobalRegistry for runtime access
        GlobalRegistry.Set("blockOwnerMap", blockOwnerMap);

        // Build reverse mapping for runtime access: objectId -> localId
        var objectIdToLocalId = new Dictionary<int, string>();


        // Method dictionaries should already exist from Phase6 object allocation
        // Phase8 creates CompiledMethod objects in TWO PASSES to handle block references
        
        // PASS 1: Create all CompiledMethod shells with nil placeholder literals
        // This ensures all blocks exist in _localIdToObjectId before we resolve literals
        int methodsCreated = 0;
        foreach (var method in _bundle.Methods)
        {
            var bytecodes = ClassHierarchyUtils.DecodeBytecodes(method.Bytecodes);
            var nilOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);
            var placeholderLiterals = new SmalltalkOOP[method.Literals.Length];
            for (int i = 0; i < placeholderLiterals.Length; i++)
            {
                placeholderLiterals[i] = nilOOP;
            }
            
            var methodOOP = CreateCompiledMethod(method, placeholderLiterals, bytecodes, compiledMethodClassOOP, byteArrayClassOOP);
            _localIdToObjectId[method.LocalId] = methodOOP;
            objectIdToLocalId[methodOOP.ToObjectTableIndex()] = method.LocalId;
            methodsCreated++;
        }
        
        // PASS 2: Resolve and patch all literals now that all blocks exist
        foreach (var method in _bundle.Methods)
        {
            if (method.Literals.Length == 0) continue;
            
            var methodOOP = _localIdToObjectId[method.LocalId];
            for (int i = 0; i < method.Literals.Length; i++)
            {
                var literalOOP = ResolveLiteralValueAsOOP(method.Literals[i]);
                // Patch literal in CompiledMethod: literals start at indexed slot 1 (slot 0 is header)
                _allocator.SetIndexedOOP(methodOOP, i + 1, literalOOP);
            }
        }

        // Store the reverse mapping in GlobalRegistry for runtime access
        GlobalRegistry.Set("objectIdToLocalId", objectIdToLocalId);
    }


    /// <summary>
    /// Resolves a literal value from the discriminated union to a SmalltalkOOP.
    /// v1.0.3: Supports "compiledMethod" for nested blocks.
    /// </summary>
    private SmalltalkOOP ResolveLiteralValueAsOOP(LiteralValue literal)
    {
        
        var result = literal.Type switch
        {
            "symbol" => InternSymbol(_symbols[literal.Index!.Value]),
            "string" => InternString(_strings[literal.Index!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId),
            "true" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.TrueObjectId),
            "false" => SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.FalseObjectId),
            "objectRef" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP1) ? objOOP1 : throw new InvalidOperationException($"objectRef not found: '{literal.LocalId}'"),
            "compiledMethod" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP2) ? objOOP2 : throw new InvalidOperationException($"compiledMethod not found: '{literal.LocalId}'"),
            "character" => AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveAssociation(literal.Association!),
            "array" => AllocateArray(literal.Elements!.Select(l => ResolveLiteralValueAsOOP(l)).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
        
        return result;
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
    /// Creates a CompiledMethod object using PointerIndexed layout with separate ByteArray for bytecodes.
    /// Layout: [header] [literal1] [literal2] ... [literalN] [bytecodeArrayOOP]
    /// </summary>
    private SmalltalkOOP CreateCompiledMethod(
        MethodDescriptor method, 
        SmalltalkOOP[] literalOOPs, 
        byte[] bytecodes,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // Use AllocateCompiledMethod which handles all the setup including class reference
        return _allocator.AllocateCompiledMethod(
            compiledMethodClassOOP, 
            byteArrayClassOOP,
            method.Header.Word, 
            literalOOPs, 
            bytecodes);
    }

    /// <summary>
    /// Creates a ByteArray object to hold method bytecodes.
    /// </summary>
    private SmalltalkOOP AllocateByteArrayForBytecodes(byte[] bytecodes)
    {
        // Look up ByteArray class layout
        var byteArrayClassDefinition = ClassHierarchyUtils.FindClassDefinition("ByteArray", _bundle);
        if (byteArrayClassDefinition == null)
        {
            throw new InvalidOperationException("ByteArray class not found in bundle");
        }

        var bytecodeArrayOOP = _allocator.AllocateByteArray("ByteArray", bytecodes.Length);
        // Set ByteArray class reference (since Phase 8 runs after Phase Y)
        int byteArrayClassId = GlobalRegistry.LookupClassObjectId("ByteArray");
        var byteArrayClassOOP = SmalltalkOOP.FromObjectTableIndex(byteArrayClassId);
        _allocator.SetObjectClass(bytecodeArrayOOP, byteArrayClassOOP);

        // Store the bytecodes
        for (int i = 0; i < bytecodes.Length; i++)
        {
            _allocator.SetIndexedByte(bytecodeArrayOOP, i, bytecodes[i]);
        }

        return bytecodeArrayOOP;
    }



    /// <summary>
    /// Allocates a Float object.
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value)
    {
        // Look up Float class layout
        var floatClassDefinition = ClassHierarchyUtils.FindClassDefinition("Float", _bundle);
        if (floatClassDefinition == null)
        {
            throw new InvalidOperationException("Float class not found in bundle");
        }

        var floatOOP = _allocator.AllocateByteArray("Float", 8);
        // Set Float class reference (since Phase 8 runs after Phase Y)
        int floatClassId = GlobalRegistry.LookupClassObjectId("Float");
        var floatClassOOP = SmalltalkOOP.FromObjectTableIndex(floatClassId);
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        // Store the double as 8 bytes
        byte[] bytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 8; i++)
        {
            _allocator.SetIndexedByte(floatOOP, i, bytes[i]);
        }

        return floatOOP;
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
                return SmalltalkOOP.FromObjectTableIndex(SymbolRegistry.GetObjectId(i));
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Allocates a String object (NOT interned - each call creates a new String).
    /// In Smalltalk-80, Strings are mutable and NOT interned.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        // Look up String class layout from bundle data
        var stringClassDefinition = ClassHierarchyUtils.FindClassDefinition("String", _bundle);
        if (stringClassDefinition == null)
        {
            throw new InvalidOperationException("String class not found in bundle");
        }
        
        // Allocate string object (AllocateString handles the bytes internally)
        var stringOOP = _allocator.AllocateString(value);
        // Set String class reference (since Phase 8 runs after Phase Y)
        int stringClassId = GlobalRegistry.LookupClassObjectId("String");
        var stringClassOOP = SmalltalkOOP.FromObjectTableIndex(stringClassId);
        _allocator.SetObjectClass(stringOOP, stringClassOOP);
        return stringOOP;
    }


    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private SmalltalkOOP AllocateCharacter(int codePoint)
    {
        // Look up Character class layout
        var characterClassDefinition = ClassHierarchyUtils.FindClassDefinition("Character", _bundle);
        if (characterClassDefinition == null)
        {
            throw new InvalidOperationException("Character class not found in bundle");
        }

        var characterOOP = _allocator.AllocateFixedObject("Character");
        // Set Character class reference (since Phase 8 runs after Phase Y)
        int characterClassId = GlobalRegistry.LookupClassObjectId("Character");
        var characterClassOOP = SmalltalkOOP.FromObjectTableIndex(characterClassId);
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Look up ByteArray class layout
        var byteArrayClassDefinition = ClassHierarchyUtils.FindClassDefinition("ByteArray", _bundle);
        if (byteArrayClassDefinition == null)
        {
            throw new InvalidOperationException("ByteArray class not found in bundle");
        }

        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes);
        // Set ByteArray class reference (since Phase 8 runs after Phase Y)
        int byteArrayClassId = GlobalRegistry.LookupClassObjectId("ByteArray");
        var byteArrayClassOOP = SmalltalkOOP.FromObjectTableIndex(byteArrayClassId);
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);

        // Bytes already stored during allocation

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates an Array object.
    /// </summary>
    private SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs)
    {
        // Look up Array class layout
        var arrayClassDefinition = ClassHierarchyUtils.FindClassDefinition("Array", _bundle);
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        int arrayClassId = GlobalRegistry.LookupClassObjectId("Array");
        var arrayClassOOP = SmalltalkOOP.FromObjectTableIndex(arrayClassId);
        var arrayOOP = _allocator.AllocateArray(arrayClassOOP, elementOOPs.Length);
        // Class reference automatically set by new AllocateArray API

        // Store the elements
        for (int i = 0; i < elementOOPs.Length; i++)
        {
            _allocator.SetIndexedOOP(arrayOOP, i, elementOOPs[i]);
        }

        return arrayOOP;
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        // Look up Association class layout from bundle data
        var associationClassDefinition = ClassHierarchyUtils.FindClassDefinition("Association", _bundle);
        if (associationClassDefinition == null)
        {
            throw new InvalidOperationException("Association class not found in bundle");
        }
        
        var associationOOP = _allocator.AllocateFixedObject("Association");
        // Set Association class reference (since Phase 8 runs after Phase Y)
        int associationClassId = GlobalRegistry.LookupClassObjectId("Association");
        var associationClassOOP = SmalltalkOOP.FromObjectTableIndex(associationClassId);
        _allocator.SetObjectClass(associationOOP, associationClassOOP);
        
        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        
        return associationOOP;
    }

    /// <summary>
    /// Resolves an association literal (key/value pair).
    /// If the association already exists in SystemDictionary (for global class names),
    /// reuse it instead of creating a new one with nil value.
    /// </summary>
    private SmalltalkOOP ResolveAssociation(AssociationLiteral assoc)
    {
        var keyOOP = InternSymbol(_symbols[assoc.KeyIndex]);
        string keyName = _symbols[assoc.KeyIndex];

        // CRITICAL FIX: If this is a class association and valueRef is None,
        // look up the existing association from SystemDictionary instead of creating new one with nil
        if (assoc.ValueRef?.LocalId == null)
        {
            // Try to find existing class association in SystemDictionary
            if (GlobalRegistry.TryGet($"class:{keyName}", out int classOop))
            {
                // This is a known class - find its existing association in SystemDictionary
                int sysDictId = GlobalRegistry.SystemDictionaryId;
                var sysDictOOP = SmalltalkOOP.FromObjectTableIndex(sysDictId);
                int sysDictSize = _allocator.GetIndexedSize(sysDictOOP);

                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP.ToObjectTableIndex() == GlobalRegistry.NilObjectId) continue;

                    var assocKeyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP == keyOOP)
                    {
                        // Found the existing association - reuse it!
                        return assocOOP;
                    }
                }
            }
        }

        // Fall back to creating new association
        var valueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.NilObjectId);

        return AllocateAssociation(keyOOP, valueOOP);
    }

    // DecodeBytecodes method moved to ClassHierarchyUtils to eliminate duplication

    // All duplicate methods removed - now using ClassHierarchyUtils

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    private byte[] ConvertHexStringToBytes(string hex)
    {
        // Remove any whitespace or separators
        hex = hex.Replace(" ", "").Replace("-", "").Replace(":", "");
        
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even number of characters");
            
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
