using System.Text.Json;
using Smalltalk80.Interpreter.Core.Memory;

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
    
    private readonly Dictionary<string, SmalltalkOOP> _canonicalAssociations = new();

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
        var objectIdToLocalId = new Dictionary<SmalltalkOOP, string>();


        // Method dictionaries should already exist from Phase6 object allocation
        // Phase8 creates CompiledMethod objects in TWO PASSES to handle block references
        
        // PASS 1: Create all CompiledMethod shells with nil placeholder literals
        // This ensures all blocks exist in _localIdToObjectId before we resolve literals
        int methodsCreated = 0;
        foreach (var method in _bundle.Methods)
        {
            var bytecodes = ClassHierarchyUtils.DecodeBytecodes(method.Bytecodes);
            var nilOOP = SpecialObjectsArray.Instance.Nil;
            var placeholderLiterals = new SmalltalkOOP[method.Literals.Length];
            for (int i = 0; i < placeholderLiterals.Length; i++)
            {
                placeholderLiterals[i] = nilOOP;
            }
            
            var methodOOP = CreateCompiledMethod(method, placeholderLiterals, bytecodes, compiledMethodClassOOP, byteArrayClassOOP);
            _localIdToObjectId[method.LocalId] = methodOOP;
            objectIdToLocalId[methodOOP] = method.LocalId;
            methodsCreated++;
        }
        
        // PASS 2: Resolve and patch all literals now that all blocks exist
        foreach (var method in _bundle.Methods)
        {
            if (method.Literals.Length == 0) continue;

            var methodOOP = _localIdToObjectId[method.LocalId];
            for (int i = 0; i < method.Literals.Length; i++)
            {
                try
                {
                    var literalOOP = ResolveLiteralValueAsOOP(method.Literals[i]);
                    // Patch literal in CompiledMethod: literals start at indexed slot 1 (slot 0 is header)
                    _allocator.SetIndexedOOP(methodOOP, i + 1, literalOOP);
                }
                catch (Exception)
                {
                    Console.WriteLine($"ERROR resolving literal {i} in method {method.LocalId}");
                    Console.WriteLine($"  Literal type: {method.Literals[i].Type}");
                    var index = method.Literals[i].Index;
                    if (index.HasValue)
                        Console.WriteLine($"  Index: {index.Value}");
                    throw;
                }
            }
        }

        // Store the reverse mapping in GlobalRegistry for runtime access
        GlobalRegistry.Set("objectIdToLocalId", objectIdToLocalId);
        
        // VALIDATION PASS: Verify Association canonicalization
        ValidateAssociationCanonicalization();
    }
    
    /// <summary>
    /// Validates that all Association literals are properly canonicalized.
    /// Ensures no duplicate Association objects exist for the same variable name.
    /// This is a critical correctness check for the lazy class variable initialization system.
    /// </summary>
    private void ValidateAssociationCanonicalization()
    {
        var associationsByKey = new Dictionary<string, List<(string methodId, SmalltalkOOP assocOOP)>>();
        int totalAssociations = 0;
        int nonNilValueCount = 0;
        
        // Scan all methods and collect Association literals
        foreach (var method in _bundle.Methods)
        {
            if (method.Literals.Length == 0) continue;
            
            var methodOOP = _localIdToObjectId[method.LocalId];
            
            for (int i = 0; i < method.Literals.Length; i++)
            {
                var literal = method.Literals[i];
                if (literal.Type != "association") continue;
                
                totalAssociations++;
                
                // Get the resolved Association OOP from the method
                var assocOOP = _allocator.GetIndexedOOP(methodOOP, i + 1); // +1 to skip header
                
                // Validate Association structure
                ValidateAssociationStructure(assocOOP, method.LocalId);
                
                // VALUE MIGRATION VALIDATION: Check if Association has non-nil value
                var valueOOP = _allocator.GetInstanceVariable(assocOOP, 1);
                if (valueOOP != SpecialObjectsArray.Instance.Nil)
                {
                    nonNilValueCount++;
                }
                
                // Get the key Symbol name
                string keyName = _symbols[literal.Association!.KeyIndex];
                
                // Track this Association by key
                if (!associationsByKey.ContainsKey(keyName))
                {
                    associationsByKey[keyName] = new List<(string, SmalltalkOOP)>();
                }
                associationsByKey[keyName].Add((method.LocalId, assocOOP));
            }
        }
        
        // Validate: All Associations with same key must have identical OOP
        var violations = new List<string>();
        int canonicalAssociations = 0;
        
        foreach (var (keyName, associations) in associationsByKey)
        {
            canonicalAssociations++;
            
            if (associations.Count == 1)
            {
                // Only one method uses this variable - automatically canonical
                continue;
            }
            
            // Check that all OOPs are identical
            var firstOOP = associations[0].assocOOP;
            for (int i = 1; i < associations.Count; i++)
            {
                if (associations[i].assocOOP != firstOOP)
                {
                    violations.Add(
                        $"Association canonicalization violation for key '{keyName}':\n" +
                        $"  Method {associations[0].methodId}: OOP={associations[0].assocOOP}\n" +
                        $"  Method {associations[i].methodId}: OOP={associations[i].assocOOP}\n" +
                        $"  Expected all methods to share the same canonical Association OOP."
                    );
                }
            }
        }
        
        // Report results
        if (violations.Count > 0)
        {
            var errorMessage = $"Association canonicalization validation FAILED:\n" +
                              $"  Total Association literals: {totalAssociations}\n" +
                              $"  Unique Association keys: {canonicalAssociations}\n" +
                              $"  Canonicalization violations: {violations.Count}\n\n" +
                              string.Join("\n\n", violations);
            throw new InvalidOperationException(errorMessage);
        }
        
        // Calculate deduplication statistics
        int duplicatesAvoided = totalAssociations - canonicalAssociations;
        double dedupRatio = totalAssociations > 0 ? (double)canonicalAssociations / totalAssociations : 1.0;
        double memoryReduction = totalAssociations > 0 ? (1.0 - dedupRatio) : 0.0;
        
        Console.WriteLine($"[Phase8] Association canonicalization validation PASSED:");
        Console.WriteLine($"  Total Association literals in methods: {totalAssociations}");
        Console.WriteLine($"  Canonical Associations created: {canonicalAssociations}");
        Console.WriteLine($"  Duplicate Associations avoided: {duplicatesAvoided}");
        Console.WriteLine($"  Canonicalization ratio: {dedupRatio:P1}");
        Console.WriteLine($"  Memory reduction: {memoryReduction:P1}");
        Console.WriteLine($"  Associations with non-nil values: {nonNilValueCount}");
        
        // VALUE MIGRATION POLICY WARNING
        if (nonNilValueCount > 0)
        {
            Console.WriteLine($"\n[Phase8] WARNING: {nonNilValueCount} Association(s) have non-nil values!");
            Console.WriteLine($"  Association literals should have nil values at compile time.");
            Console.WriteLine($"  Values should only be set at runtime via bytecode 0x09.");
            Console.WriteLine($"  This may indicate a compiler anomaly or special cold-start initialization.");
        }
        
        // STRICT REACHABILITY VALIDATION
        // Verify all Associations are reachable from canonical sources
        ValidateAssociationReachability(associationsByKey);
    }
    
    /// <summary>
    /// Validates that all Association OOPs are reachable from canonical sources:
    /// - Canonical cache (_canonicalAssociations)
    /// - SystemDictionary
    /// - classPool dictionaries
    /// 
    /// This ensures no "orphan" Associations exist that bypassed canonicalization.
    /// </summary>
    private void ValidateAssociationReachability(Dictionary<string, List<(string methodId, SmalltalkOOP assocOOP)>> associationsByKey)
    {
        var reachableAssociations = new HashSet<SmalltalkOOP>();
        
        // Source 1: Canonical cache
        foreach (var assocOOP in _canonicalAssociations.Values)
        {
            reachableAssociations.Add(assocOOP);
        }
        
        // Source 2: SystemDictionary
        if (GlobalRegistry.TryGet("SystemDictionary", out var sysDictOOP) || 
            GlobalRegistry.TryGet("Smalltalk", out sysDictOOP))
        {
            int sysDictSize = _allocator.GetIndexedSize(sysDictOOP);
            for (int i = 0; i < sysDictSize; i++)
            {
                var assocOOP = _allocator.GetIndexedOOP(sysDictOOP, i);
                if (assocOOP != SpecialObjectsArray.Instance.Nil)
                {
                    reachableAssociations.Add(assocOOP);
                }
            }
        }
        
        // Source 3: All classPool dictionaries
        foreach (var cls in _bundle.Classes)
        {
            if (cls.IsMetaclass) continue; // Metaclasses share classPool with their class
            
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out var classOOP))
                continue;
                
            try
            {
                int classPoolIndex = 7; // Standard classPool index
                var classPoolOOP = _allocator.GetInstanceVariable(classOOP, classPoolIndex);
                
                if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
                    continue;
                    
                int poolSize = _allocator.GetIndexedSize(classPoolOOP);
                for (int i = 0; i < poolSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(classPoolOOP, i);
                    if (assocOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        reachableAssociations.Add(assocOOP);
                    }
                }
            }
            catch
            {
                // Class might not have classPool - skip
            }
        }
        
        // Validate: all Associations used in methods are reachable
        var unreachableAssociations = new List<string>();
        
        foreach (var (keyName, associations) in associationsByKey)
        {
            foreach (var (methodId, assocOOP) in associations)
            {
                if (!reachableAssociations.Contains(assocOOP))
                {
                    unreachableAssociations.Add(
                        $"Association '{keyName}' (OOP={assocOOP}) in method {methodId} " +
                        $"is not reachable from any canonical source!");
                }
            }
        }
        
        if (unreachableAssociations.Count > 0)
        {
            var errorMessage = $"Association reachability validation FAILED:\n" +
                              $"  {unreachableAssociations.Count} orphan Associations detected:\n\n" +
                              string.Join("\n", unreachableAssociations);
            throw new InvalidOperationException(errorMessage);
        }
        
        Console.WriteLine($"[Phase8] Association reachability validation PASSED:");
        Console.WriteLine($"  All {associationsByKey.Sum(kvp => kvp.Value.Count)} Association literals are reachable from canonical sources.");
    }
    
    /// <summary>
    /// Validates that an Association object has correct structure:
    /// - Must be a Pointer format object (2 instance variables)
    /// - key (slot 0) must be a Symbol
    /// - value (slot 1) must be a valid OOP (can be nil)
    /// </summary>
    private void ValidateAssociationStructure(SmalltalkOOP assocOOP, string methodId)
    {
        // Check object format
        var format = _allocator.GetObjectFormat(assocOOP);
        if (format != ObjectFormat.Pointer)
        {
            throw new InvalidOperationException(
                $"Association OOP {assocOOP} in method {methodId} has invalid format {format}. " +
                $"Expected ObjectFormat.Pointer for Association objects.");
        }
        
        // Check instance variable count (Association has 2: key and value)
        int instanceVarCount = _allocator.GetInstanceVariableCount(assocOOP);
        if (instanceVarCount != 2)
        {
            throw new InvalidOperationException(
                $"Association OOP {assocOOP} in method {methodId} has {instanceVarCount} instance variables. " +
                $"Expected 2 (key and value) for Association objects.");
        }
        
        // Validate key is a Symbol
        var keyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
        var keyClass = _allocator.GetClassOOP(keyOOP);
        if (GlobalRegistry.TryGet("class:Symbol", out var symbolClassOOP))
        {
            if (keyClass != symbolClassOOP)
            {
                throw new InvalidOperationException(
                    $"Association OOP {assocOOP} in method {methodId} has non-Symbol key. " +
                    $"Key OOP {keyOOP} has class {keyClass}, expected Symbol class {symbolClassOOP}.");
            }
        }
        
        // Validate value is a valid OOP (any object or nil)
        var valueOOP = _allocator.GetInstanceVariable(assocOOP, 1);
        // Just check that we can get its format without error
        try
        {
            _ = _allocator.GetObjectFormat(valueOOP);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Association OOP {assocOOP} in method {methodId} has invalid value OOP {valueOOP}: {ex.Message}");
        }
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
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
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
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
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
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
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
                return SymbolRegistry.GetSymbolOOP(i);
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
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
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
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
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
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
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
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        _allocator.SetObjectClass(associationOOP, associationClassOOP);
        
        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        
        return associationOOP;
    }

    /// <summary>
    /// Resolves an association literal (key/value pair) with canonicalization.
    /// Ensures only one Association object exists per variable name across all methods.
    /// 
    /// Priority order:
    /// 1. Check canonical association cache (class variables and other associations)
    /// 2. For global class references, look up in SystemDictionary
    /// 3. Create new canonical association and cache it
    /// 
    /// This eliminates duplicate Association objects and guarantees identity stability.
    /// </summary>
    private SmalltalkOOP ResolveAssociation(AssociationLiteral assoc)
    {
        var keyOOP = InternSymbol(_symbols[assoc.KeyIndex]);
        string keyName = _symbols[assoc.KeyIndex];

        // VALUE MIGRATION POLICY: Validate that incoming Association has nil value
        // Association literals should ALWAYS have nil values at compile time.
        // Values are set at runtime via bytecode 0x09, not during cold start.
        // Any non-nil valueRef indicates compiler bug or corrupted JSON.
        var incomingValueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : SpecialObjectsArray.Instance.Nil;

        // STEP 1: Check canonical association cache first
        // This ensures all methods share the same Association object for the same variable
        if (_canonicalAssociations.TryGetValue(keyName, out var cachedAssocOOP))
        {
            // CANONICALIZATION CONFLICT: Validate value migration semantics
            // Policy: STRICT NIL ENFORCEMENT - detect any non-nil values and warn/fail
            if (incomingValueOOP != SpecialObjectsArray.Instance.Nil)
            {
                // Read canonical Association's current value for diagnostics
                var canonicalValueOOP = _allocator.GetInstanceVariable(cachedAssocOOP, 1);
                
                if (canonicalValueOOP == SpecialObjectsArray.Instance.Nil)
                {
                    // Canonical has nil, incoming has non-nil - WARNING but not fatal
                    Console.WriteLine(
                        $"WARNING: Association canonicalization value conflict for key '{keyName}':\n" +
                        $"  Canonical Association {cachedAssocOOP} has value: nil\n" +
                        $"  Incoming Association has value: {incomingValueOOP}\n" +
                        $"  Policy: DISCARDING incoming non-nil value (first-writer-wins)\n" +
                        $"  This may indicate compiler bug or corrupted JSON.");
                }
                else if (canonicalValueOOP != incomingValueOOP)
                {
                    // Both have non-nil values but they differ - SERIOUS ERROR
                    throw new InvalidOperationException(
                        $"Association canonicalization value CONFLICT for key '{keyName}':\n" +
                        $"  Canonical Association {cachedAssocOOP} has value: {canonicalValueOOP}\n" +
                        $"  Incoming Association has value: {incomingValueOOP}\n" +
                        $"  Association literals MUST have nil values at compile time.\n" +
                        $"  Values should only be set at runtime via bytecode 0x09.\n" +
                        $"  This indicates a serious compiler bug or corrupted cold start data.");
                }
                // else: both have same non-nil value - tolerate but still unusual
            }
            
            return cachedAssocOOP;
        }

        // STEP 2: For global class references (valueRef is None), look up in SystemDictionary
        if (assoc.ValueRef?.LocalId == null)
        {
            // Try to find existing class association in SystemDictionary
            if (GlobalRegistry.TryGet($"class:{keyName}", out SmalltalkOOP classOop))
            {
                // This is a known class - find its existing association in SystemDictionary
                var sysDictOOP = GlobalRegistry.SystemDictionaryOOP;
                int sysDictSize = _allocator.GetIndexedSize(sysDictOOP);

                for (int i = 0; i < sysDictSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(sysDictOOP, i);
                    if (assocOOP == SpecialObjectsArray.Instance.Nil) continue;

                    var assocKeyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
                    if (assocKeyOOP == keyOOP)
                    {
                        // Found the existing association - cache and return it
                        _canonicalAssociations[keyName] = assocOOP;
                        return assocOOP;
                    }
                }
            }
        }

        // STEP 3: Create new canonical association and cache it
        var newAssocOOP = AllocateAssociation(keyOOP, incomingValueOOP);
        
        // Cache the newly created association to ensure all future references use the same object
        _canonicalAssociations[keyName] = newAssocOOP;
        
        return newAssocOOP;
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
