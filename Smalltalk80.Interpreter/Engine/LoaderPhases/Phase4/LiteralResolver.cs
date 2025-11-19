using System.Text.Json;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase4;

/// <summary>
/// Resolves literal values from the cold start bundle to Smalltalk object pointers.
/// Handles all literal types: symbols, strings, numbers, arrays, associations, etc.
/// </summary>
/// <param name="bundle">The cold start bundle containing symbols and strings.</param>
/// <param name="allocator">The object graph allocator for creating objects.</param>
/// <param name="localIdToObjectId">Mapping from local IDs to allocated object OOPs.</param>
/// <param name="symbolService">The symbol service for symbol lookups.</param>
/// <param name="fieldValueService">The field value service for value conversion.</param>
internal class LiteralResolver(
    ColdStartBundle bundle,
    ObjectGraphAllocator allocator,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    SymbolService symbolService,
    FieldValueService fieldValueService)
{
    private readonly ColdStartBundle _bundle = bundle;
    private readonly ObjectGraphAllocator _allocator = allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId = localIdToObjectId;
    private readonly SymbolService _symbolService = symbolService;
    private readonly FieldValueService _fieldValueService = fieldValueService;
    private readonly IReadOnlyList<string> _symbols = bundle.Symbols;
    private readonly IReadOnlyList<string> _strings = bundle.Strings;

    // Pool variable binding Array deduplication registry
    // Key: "PoolDictionaryName:KeySymbol", Value: 2-element Array OOP (#PoolName, #Key)
    // These are NOT Associations - they are binding metadata for VM runtime resolution
    private readonly Dictionary<string, SmalltalkOOP> _poolVariableBindings = new();

    /// <summary>
    /// Resolves a literal value from the discriminated union to a SmalltalkOOP.
    /// v1.0.3: Supports "compiledMethod" for nested blocks.
    /// </summary>
    /// <param name="literal">The literal value descriptor from the bundle.</param>
    /// <returns>The resolved Smalltalk object pointer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when literal type is unknown or reference not found.</exception>
    public SmalltalkOOP ResolveLiteralValueAsOOP(LiteralValue literal)
    {
        var result = literal.Type switch
        {
            "symbol" => InternSymbol(_symbols[literal.Index!.Value]),
            "string" => InternString(_strings[literal.Index!.Value]),
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "objectRef" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP1) ? objOOP1 : throw new InvalidOperationException($"objectRef not found: '{literal.LocalId}'"),
            "compiledMethod" => _localIdToObjectId.TryGetValue(literal.LocalId ?? "", out SmalltalkOOP objOOP2) ? objOOP2 : throw new InvalidOperationException($"compiledMethod not found: '{literal.LocalId}'"),
            "character" => AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveVariableLiteral(literal.Association!),
            "array" => AllocateArray(literal.Elements!.Select(l => ResolveLiteralValueAsOOP(l)).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };

        return result;
    }

    /// <summary>
    /// Resolves a variable literal - either a pool variable Array or global Association.
    ///
    /// POOL VARIABLES: Creates a 2-element Array (#PoolDictionaryName, #Key).
    /// These are NOT Associations - they are binding metadata for runtime resolution.
    /// At runtime, the VM will:
    /// 1. Resolve pool dictionary name to actual dictionary object
    /// 2. Look up key in that dictionary
    ///
    /// GLOBAL VARIABLES: Creates an Association with key and nil value.
    /// The AssociationRegistry ensures canonicalization.
    /// </summary>
    /// <param name="assoc">The association literal descriptor.</param>
    /// <returns>The resolved object pointer (Array for pool variables, Association for globals).</returns>
    public SmalltalkOOP ResolveVariableLiteral(AssociationLiteral assoc)
    {
        var keySymbol = InternSymbol(_symbols[assoc.KeyIndex]);
        string keyName = _symbols[assoc.KeyIndex];

        // POOL VARIABLES: Create 2-element Array (#PoolName, #Key) - NOT an Association
        if (assoc.PoolDictionaryName != null)
        {
            // Deduplication key: "PoolDictionaryName:KeySymbol"
            string dedupKey = $"{assoc.PoolDictionaryName}:{keyName}";

            if (_poolVariableBindings.TryGetValue(dedupKey, out var existingArrayOOP))
            {
                // Reuse existing pool variable binding Array
                return existingArrayOOP;
            }

            // Create new pool variable binding Array: (#PoolDictionaryName, #Key)
            var poolNameSymbol = InternSymbol(assoc.PoolDictionaryName);
            var poolBindingArray = AllocateArray([poolNameSymbol, keySymbol]);

            // Register for deduplication
            _poolVariableBindings[dedupKey] = poolBindingArray;

            return poolBindingArray;
        }

        // GLOBAL VARIABLES: Use AssociationRegistry for canonicalization
        // VALUE MIGRATION POLICY: Validate that incoming Association has nil value
        var incomingValueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : SpecialObjectsArray.Instance.Nil;

        // Warn if incoming value is non-nil (policy violation - silent)
        // Policy: Association literals should have nil values at compile time

        // Use AssociationRegistry for canonicalization
        var result = AssociationRegistry.GetOrCreate(keyName, () =>
        {
            // Create new Association: key=Symbol, value=nil
            // Always use nil value per VALUE MIGRATION POLICY
            var assocOOP = AllocateAssociation(keySymbol, SpecialObjectsArray.Instance.Nil);

            return assocOOP;
        });

        return result;
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The extracted integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when value cannot be converted to int.</exception>
    public int GetIntValue(object value)
    {
        return _fieldValueService.GetIntValue(value);
    }

    /// <summary>
    /// Extracts a double value from a JsonElement or double.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The extracted double value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when value cannot be converted to double.</exception>
    public double GetFloatValue(object value)
    {
        return _fieldValueService.GetFloatValue(value);
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// </summary>
    /// <param name="name">The symbol name to intern.</param>
    /// <returns>The interned symbol OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when symbol not found in symbol table.</exception>
    public SmalltalkOOP InternSymbol(string name)
    {
        return _symbolService.GetSymbolOOP(name);
    }

    /// <summary>
    /// Allocates a String object (NOT interned - each call creates a new String).
    /// In Smalltalk-80, Strings are mutable and NOT interned.
    /// </summary>
    /// <param name="value">The string value to allocate.</param>
    /// <returns>The allocated String object OOP.</returns>
    public SmalltalkOOP InternString(string value)
    {
        // Allocate string object (AllocateString handles the bytes internally)
        var stringOOP = _allocator.AllocateString(value);
        // Set String class reference (coldstart phases can use GlobalRegistry)
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);
        return stringOOP;
    }

    /// <summary>
    /// Allocates a Float object using ObjectGraphAllocator.
    /// Float must be WordIndexed with 4 words (16-bit each) per Blue Book specification.
    /// </summary>
    /// <param name="value">The double value to store in the Float.</param>
    /// <returns>The allocated Float object OOP.</returns>
    public SmalltalkOOP AllocateFloat(double value)
    {
        // Use ObjectGraphAllocator's AllocateFloat which correctly creates WordIndexed Float with 4 words
        var floatOOP = _allocator.AllocateFloat(value);

        // Set Float class reference
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        return floatOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    /// <param name="codePoint">The Unicode code point for the character.</param>
    /// <returns>The allocated Character object OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Character class not found.</exception>
    public SmalltalkOOP AllocateCharacter(int codePoint)
    {
        // Look up Character class layout
        var characterClassDefinition = ClassHierarchyTraversal.FindClassDefinition("Character", _bundle);
        if (characterClassDefinition == null)
        {
            throw new InvalidOperationException("Character class not found in bundle");
        }

        var characterOOP = _allocator.AllocateFixedObject("Character");
        // Set Character class reference
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        // Store the code point in the Character object
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    /// <param name="encoding">The encoding of the data ("base64" or "hex").</param>
    /// <param name="data">The encoded byte data.</param>
    /// <returns>The allocated ByteArray object OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when ByteArray class not found or encoding unsupported.</exception>
    public SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Look up ByteArray class layout
        var byteArrayClassDefinition = ClassHierarchyTraversal.FindClassDefinition("ByteArray", _bundle);
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
        // Set ByteArray class reference
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);

        // Bytes already stored during allocation

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates an Array object.
    /// </summary>
    /// <param name="elementOOPs">The array of element OOPs to store.</param>
    /// <returns>The allocated Array object OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Array class not found.</exception>
    public SmalltalkOOP AllocateArray(SmalltalkOOP[] elementOOPs)
    {
        // Look up Array class layout
        var arrayClassDefinition = ClassHierarchyTraversal.FindClassDefinition("Array", _bundle);
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
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
    /// <param name="keyOOP">The key Symbol OOP.</param>
    /// <param name="valueOOP">The value OOP.</param>
    /// <returns>The allocated Association object OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Association class not found.</exception>
    public SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        // Look up Association class layout from bundle data
        var associationClassDefinition = ClassHierarchyTraversal.FindClassDefinition("Association", _bundle);
        if (associationClassDefinition == null)
        {
            throw new InvalidOperationException("Association class not found in bundle");
        }

        var associationOOP = _allocator.AllocateFixedObject("Association");
        // Set Association class reference
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        _allocator.SetObjectClass(associationOOP, associationClassOOP);

        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Converts a hex string to byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>The converted byte array.</returns>
    /// <exception cref="ArgumentException">Thrown when hex string has odd number of characters.</exception>
    public byte[] ConvertHexStringToBytes(string hex)
    {
        return _fieldValueService.ConvertHexStringToBytes(hex);
    }
}
