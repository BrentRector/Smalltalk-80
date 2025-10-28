using System.Text.Json;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 4: Method Compilation
/// Consolidates Phases 7, 8, 8b, and 9 from the original loader:
/// 1. Allocates method dictionaries for all classes
/// 2. Compiles and creates CompiledMethod objects for all methods and blocks
/// 3. Creates CompiledMethod objects for init expressions
/// 4. Installs all methods into their class method dictionaries
///
/// This is the most complex phase, handling:
/// - Symbol, string, class, array, block (CompiledMethod refs), association, SmallInteger literals
/// - Association canonicalization to prevent duplicates
/// - BlockOwnerMap tracking for lexical scoping
/// - Linear probing for method dictionary insertion
/// </summary>
public class Phase4_MethodCompilation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _strings;
    private readonly ColdstartReport _report;

    public Phase4_MethodCompilation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _initializedFields = initializedFields;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _symbols = bundle.Symbols;
        _strings = bundle.Strings;
    }

    public void Run()
    {
        // ====================================================================
        // STEP 1: ALLOCATE METHOD DICTIONARIES (from Phase7)
        // ====================================================================
        AllocateMethodDictionaries();

        // ====================================================================
        // STEP 2: COMPILE AND INSTALL METHODS (from Phase8)
        // ====================================================================
        CompileAndInstallMethods();

        // ====================================================================
        // STEP 3: CREATE INIT EXPRESSION METHODS (from Phase8b)
        // ====================================================================
        CreateInitExpressionMethods();

        // Update report metrics
        _report.Metrics.InitMethodCount = _bundle.InitExpressions.Length;
        _report.Metrics.CanonicalAssociationCount = AssociationRegistry.Count;

        // ====================================================================
        // STEP 4: INSTALL METHODS INTO DICTIONARIES (from Phase9)
        // ====================================================================
        InstallMethodsIntoDictionaries();
    }

    // ====================================================================
    // STEP 1: METHOD DICTIONARY ALLOCATION (from Phase7)
    // ====================================================================

    private void AllocateMethodDictionaries()
    {
        // Group methods by home class to count them
        var methodCounts = new Dictionary<string, int>();
        foreach (var method in _bundle.Methods)
        {
            if (method.HomeClassLocalId != null)
            {
                methodCounts[method.HomeClassLocalId] = methodCounts.GetValueOrDefault(method.HomeClassLocalId, 0) + 1;
            }
        }

        int allocatedDictionaries = 0;

        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                Console.WriteLine($"WARNING: Class not found in localIdToObjectId: {cls.LocalId}");
                continue;
            }

            // Count methods for this class
            int methodCount = methodCounts.GetValueOrDefault(cls.LocalId, 0);

            // Allocate method dictionary with room for growth (next power of 2, min 8)
            int dictionarySize = Math.Max(8, NextPowerOfTwo(methodCount * 2)); // 50% load factor

            SmalltalkOOP methodDictOOP = AllocateMethodDictionary(dictionarySize);

            // Set the method dictionary as instance variable 1 in the class
            _allocator.SetInstanceVariable(classOOP, 1, methodDictOOP);

            allocatedDictionaries++;
        }
    }

    /// <summary>
    /// Allocates a MethodDictionary object with the specified capacity.
    /// MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
    /// </summary>
    private SmalltalkOOP AllocateMethodDictionary(int capacity)
    {
        // Find MethodDictionary class definition
        var methodDictClassDefinition = FindClassDefinition("MethodDictionary");
        if (methodDictClassDefinition == null)
        {
            throw new InvalidOperationException("MethodDictionary class not found in bundle");
        }

        // Allocate the MethodDictionary object (PointerIndexed with capacity for keys)
        var methodDictClassOOP = GlobalRegistry.LookupClassOOP("MethodDictionary");
        var methodDictOOP = _allocator.AllocateArray(methodDictClassOOP, capacity);

        // Allocate valueArray (Array of CompiledMethod)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var valuesArrayOOP = _allocator.AllocateArray(arrayClassOOP, capacity);
        _allocator.SetInstanceVariable(methodDictOOP, 0, valuesArrayOOP);

        // Set tally to 0 (no methods installed yet)
        var tallyOOP = SmalltalkOOP.FromSmallInteger(0);
        _allocator.SetInstanceVariable(methodDictOOP, 1, tallyOOP);

        // Initialize all indexed fields (keys) to nil
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < capacity; i++)
        {
            _allocator.SetIndexedOOP(methodDictOOP, i, nilOOP);
        }

        return methodDictOOP;
    }

    // ====================================================================
    // STEP 2: METHOD COMPILATION (from Phase8)
    // ====================================================================

    private void CompileAndInstallMethods()
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
    /// Creates a CompiledMethod object with homeClass fixed slot and separate ByteArray for bytecodes.
    /// Layout: Fixed slot 0 = homeClass, Indexed slots = [header] [literal1] [literal2] ... [literalN] [bytecodeArrayOOP]
    /// </summary>
    private SmalltalkOOP CreateCompiledMethod(
        MethodDescriptor method,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // Blocks are code literals embedded in methods, not standalone methods
        // They use nil as homeClass since they inherit context from their enclosing method
        SmalltalkOOP homeClassOOP;
        if (method.LocalId.StartsWith("block:"))
        {
            homeClassOOP = SpecialObjectsArray.Instance.Nil;
        }
        else
        {
            // Resolve homeClass from HomeClassLocalId for regular methods
            if (!_localIdToObjectId.TryGetValue(method.HomeClassLocalId, out homeClassOOP))
            {
                throw new InvalidOperationException($"Cannot find home class '{method.HomeClassLocalId}' for method '{method.LocalId}'");
            }
        }

        // Use AllocateCompiledMethod which handles all the setup including homeClass fixed slot
        return _allocator.AllocateCompiledMethod(
            compiledMethodClassOOP,
            byteArrayClassOOP,
            homeClassOOP,
            method.Header.Word,
            literalOOPs,
            bytecodes);
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
    /// Resolves an association literal (key/value pair) with canonicalization.
    /// Ensures only one Association object exists per variable name across all methods.
    /// </summary>
    private SmalltalkOOP ResolveAssociation(AssociationLiteral assoc)
    {
        var keyOOP = InternSymbol(_symbols[assoc.KeyIndex]);
        string keyName = _symbols[assoc.KeyIndex];

        // VALUE MIGRATION POLICY: Validate that incoming Association has nil value
        var incomingValueOOP = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : SpecialObjectsArray.Instance.Nil;

        // Warn if incoming value is non-nil (policy violation)
        if (incomingValueOOP != SpecialObjectsArray.Instance.Nil)
        {
            Console.WriteLine(
                $"WARNING: Association literal for '{keyName}' has non-nil value: {incomingValueOOP}\n" +
                $"  Policy: Association literals should have nil values at compile time.\n" +
                $"  Creating Association with nil value (ignoring incoming value).");
        }

        // Use AssociationRegistry for canonicalization
        return AssociationRegistry.GetOrCreate(keyName, () =>
        {
            // Create new Association: key=Symbol, value=nil
            // Always use nil value per VALUE MIGRATION POLICY
            var assocOOP = AllocateAssociation(keyOOP, SpecialObjectsArray.Instance.Nil);
            return assocOOP;
        });
    }

    /// <summary>
    /// Validates that all Association literals are properly canonicalized.
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

        Console.WriteLine($"[Phase4] Association canonicalization validation PASSED:");
        Console.WriteLine($"  Total Association literals in methods: {totalAssociations}");
        Console.WriteLine($"  Canonical Associations created: {AssociationRegistry.Count}");
        Console.WriteLine($"  Duplicate Associations avoided: {duplicatesAvoided}");
        Console.WriteLine($"  Canonicalization ratio: {dedupRatio:P1}");
        Console.WriteLine($"  Memory reduction: {memoryReduction:P1}");
        Console.WriteLine($"  Associations with non-nil values: {nonNilValueCount}");

        // VALUE MIGRATION POLICY WARNING
        if (nonNilValueCount > 0)
        {
            Console.WriteLine($"\n[Phase4] WARNING: {nonNilValueCount} Association(s) have non-nil values!");
            Console.WriteLine($"  Association literals should have nil values at compile time.");
            Console.WriteLine($"  Values should only be set at runtime via bytecode 0x09.");
            Console.WriteLine($"  This may indicate a compiler anomaly or special cold-start initialization.");
        }

        // STRICT REACHABILITY VALIDATION
        ValidateAssociationReachability(associationsByKey);
    }

    /// <summary>
    /// Validates that all Association OOPs are reachable from canonical sources.
    /// </summary>
    private void ValidateAssociationReachability(Dictionary<string, List<(string methodId, SmalltalkOOP assocOOP)>> associationsByKey)
    {
        var reachableAssociations = new HashSet<SmalltalkOOP>();

        // Source 1: Canonical cache from AssociationRegistry
        foreach (var assocOOP in AssociationRegistry.GetAll().Values)
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

        Console.WriteLine($"[Phase4] Association reachability validation PASSED:");
        Console.WriteLine($"  All {associationsByKey.Sum(kvp => kvp.Value.Count)} Association literals are reachable from canonical sources.");

        // DEBUG: Check if any Association has value=10776
        Console.WriteLine($"[Phase4] Checking for corrupted Associations...");
        int corruptedCount = 0;
        foreach (var assocOOP in AssociationRegistry.GetAll().Values)
        {
            var valueOOP = _heapManager.GetInstanceVariable(assocOOP, 1);
            if (valueOOP.RawValue == 10776)
            {
                var keyOOP = _heapManager.GetInstanceVariable(assocOOP, 0);
                Console.WriteLine($"  [ERROR] Found Association with value=10776: assocOOP={assocOOP}, keyOOP={keyOOP}");
                corruptedCount++;
            }
        }
        if (corruptedCount == 0)
        {
            Console.WriteLine($"  No corrupted Associations found at end of Phase 4 (good!)");
        }
        else
        {
            Console.WriteLine($"  [ERROR] Found {corruptedCount} corrupted Associations at end of Phase 4!");
        }
    }

    /// <summary>
    /// Validates that an Association object has correct structure.
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

    // ====================================================================
    // STEP 3: INIT EXPRESSION METHODS (from Phase8b)
    // ====================================================================

    private void CreateInitExpressionMethods()
    {
        if (_bundle.InitExpressions.Length == 0)
        {
            // No init expressions - store nil
            SpecialObjectsArray.SetInitExpressionMethods(SpecialObjectsArray.Instance.Nil);
            return;
        }

        // Look up CompiledMethod and ByteArray classes
        if (!_localIdToObjectId.TryGetValue("class:CompiledMethod", out var compiledMethodClassOOP))
        {
            throw new InvalidOperationException("CompiledMethod class not found in _localIdToObjectId");
        }

        if (!_localIdToObjectId.TryGetValue("class:ByteArray", out var byteArrayClassOOP))
        {
            throw new InvalidOperationException("ByteArray class not found in _localIdToObjectId");
        }

        var initExprMethods = new List<SmalltalkOOP>();

        foreach (var expr in _bundle.InitExpressions)
        {
            // Resolve literals using the SAME canonicalization mechanism as regular methods
            var literalOOPs = expr.Literals.Select(ResolveLiteralValueAsOOP).ToArray();

            // Create a CompiledMethod object for the init expression
            var bytecodes = ClassHierarchyUtils.DecodeBytecodes(expr.Bytecodes);
            SmalltalkOOP initMethodOOP = CreateCompiledMethod(
                new MethodDescriptor
                {
                    LocalId = expr.LocalId,
                    HomeClassLocalId = "class:UndefinedObject", // placeholder
                    SelectorIndex = 0,
                    Header = expr.Header,
                    Literals = Array.Empty<LiteralValue>(), // already resolved
                    Bytecodes = expr.Bytecodes
                },
                literalOOPs,
                bytecodes,
                compiledMethodClassOOP,
                byteArrayClassOOP);

            initExprMethods.Add(initMethodOOP);
            _localIdToObjectId[expr.LocalId] = initMethodOOP;
        }

        // Create array to hold all init expression methods
        var arrayClassOOP = _localIdToObjectId["class:Array"];
        var initArrayOOP = _allocator.AllocateArray(arrayClassOOP, initExprMethods.Count);

        for (int i = 0; i < initExprMethods.Count; i++)
        {
            _allocator.SetIndexedOOP(initArrayOOP, i, initExprMethods[i]);
        }

        // Store in SpecialObjectsArray for runtime access
        SpecialObjectsArray.SetInitExpressionMethods(initArrayOOP);
    }

    // ====================================================================
    // STEP 4: METHOD DICTIONARY INSTALLATION (from Phase9)
    // ====================================================================

    private void InstallMethodsIntoDictionaries()
    {
        int installedMethods = 0;
        int installedBlocks = 0;
        int skippedMethods = 0;

        foreach (var method in _bundle.Methods)
        {
            // Skip blocks - they are not installed in method dictionaries
            if (string.IsNullOrEmpty(method.HomeClassLocalId))
            {
                installedBlocks++;
                continue;
            }

            // Get the CompiledMethod object created earlier
            if (!_localIdToObjectId.TryGetValue(method.LocalId, out SmalltalkOOP methodOOP))
            {
                Console.WriteLine($"WARNING: Method not found in localIdToObjectId: {method.LocalId}");
                skippedMethods++;
                continue;
            }

            // Resolve the home class object
            if (!_localIdToObjectId.TryGetValue(method.HomeClassLocalId, out SmalltalkOOP classOOP))
            {
                Console.WriteLine($"WARNING: Home class not found: {method.HomeClassLocalId}");
                skippedMethods++;
                continue;
            }

            // Get the class's method dictionary (instance variable 1 in Behavior)
            var methodDictOOP = _allocator.GetInstanceVariable(classOOP, 1); // methodDict is at index 1

            if (methodDictOOP == SpecialObjectsArray.Instance.Nil)
            {
                Console.WriteLine($"WARNING: Class {method.HomeClassLocalId} has nil methodDict");
                skippedMethods++;
                continue;
            }

            // Get the selector symbol
            string selectorName = _symbols[method.SelectorIndex];
            SmalltalkOOP selectorSymbolOOP = GetSymbolOOP(selectorName);

            // Install the method in the method dictionary
            InstallMethodInDictionary(methodDictOOP, selectorSymbolOOP, methodOOP);
            installedMethods++;
        }
    }

    /// <summary>
    /// Gets the OOP for a symbol that was interned in Phase 1.
    /// </summary>
    private SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        // Find the symbol in the symbol table from Phase 1
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == symbolName)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table.");
    }

    /// <summary>
    /// Installs a method into a method dictionary using IdentityDictionary structure.
    /// MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
    /// </summary>
    private void InstallMethodInDictionary(SmalltalkOOP methodDictOOP, SmalltalkOOP selectorSymbolOOP, SmalltalkOOP methodOOP)
    {
        // MethodDictionary (IdentityDictionary): [0] = valueArray, [1] = tally, indexed fields = keys
        var valuesArrayOOP = _allocator.GetInstanceVariable(methodDictOOP, 0);
        var tallyOOP = _allocator.GetInstanceVariable(methodDictOOP, 1);

        if (valuesArrayOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} has nil valueArray");
        }

        int dictionarySize = _allocator.GetIndexedSize(methodDictOOP);
        int valuesArraySize = _allocator.GetIndexedSize(valuesArrayOOP);

        if (dictionarySize != valuesArraySize)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} has mismatched sizes: keys={dictionarySize}, values={valuesArraySize}");
        }

        // Find an empty slot or existing entry for this selector
        int targetIndex = -1;
        for (int i = 0; i < dictionarySize; i++)
        {
            var keyOOP = _allocator.GetIndexedOOP(methodDictOOP, i);

            if (keyOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Empty slot found
                targetIndex = i;
                break;
            }
            else if (keyOOP == selectorSymbolOOP)
            {
                // Existing entry for this selector - replace it
                targetIndex = i;
                break;
            }
        }

        if (targetIndex == -1)
        {
            throw new InvalidOperationException($"MethodDictionary {methodDictOOP} is full - no empty slots available");
        }

        // Check if this is a new entry (for tally update)
        var existingKeyOOP = _allocator.GetIndexedOOP(methodDictOOP, targetIndex);
        bool isNewEntry = existingKeyOOP == SpecialObjectsArray.Instance.Nil;

        // Install the method
        _allocator.SetIndexedOOP(methodDictOOP, targetIndex, selectorSymbolOOP);
        _allocator.SetIndexedOOP(valuesArrayOOP, targetIndex, methodOOP);

        // Update tally if we used a new slot
        if (isNewEntry)
        {
            int currentTally = tallyOOP.IsSmallInteger ? tallyOOP.AsSmallInteger() : 0;
            var newTallyOOP = SmalltalkOOP.FromSmallInteger(currentTally + 1);
            _allocator.SetInstanceVariable(methodDictOOP, 1, newTallyOOP);
        }
    }

    // ====================================================================
    // HELPER METHODS (used across multiple steps)
    // ====================================================================

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

        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table.");
    }

    /// <summary>
    /// Allocates a String object (NOT interned - each call creates a new String).
    /// In Smalltalk-80, Strings are mutable and NOT interned.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        // Allocate string object (AllocateString handles the bytes internally)
        var stringOOP = _allocator.AllocateString(value);
        // Set String class reference (coldstart phases can use GlobalRegistry)
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);
        return stringOOP;
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
        // Set Float class reference
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
        // Set ByteArray class reference
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
        // Set Association class reference
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        _allocator.SetObjectClass(associationOOP, associationClassOOP);

        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        // DEBUG: Check if value is the problematic OOP 10776
        if (valueOOP.RawValue == 10776)
        {
            Console.WriteLine($"[ERROR] AllocateAssociation: Creating Association with value=OOP 10776!");
            Console.WriteLine($"  keyOOP={keyOOP}, This is wrong - valueOOP should be nil!");
        }

        return associationOOP;
    }

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

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// </summary>
    private ClassDescriptor? FindClassDefinition(string className)
    {
        return _bundle.Classes.FirstOrDefault(c =>
            !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Returns the next power of 2 greater than or equal to the given value.
    /// </summary>
    private int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;

        int power = 1;
        while (power < value)
        {
            power *= 2;
        }
        return power;
    }
}