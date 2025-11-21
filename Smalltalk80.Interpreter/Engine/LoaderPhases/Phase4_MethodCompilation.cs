using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Engine.LoaderPhases.Phase4;
using Smalltalk80.Interpreter.Engine.Services;

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
public class Phase4_MethodCompilation : IPhase
{
    private readonly ColdStartBundle _bundle;
    private readonly ObjectGraphAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;
    private readonly ColdstartReport _report;
    private readonly IDiagnosticHub? _diagnostics;

    // Helper classes for decomposed functionality
    private readonly LiteralResolver _literalResolver;
    private readonly MethodDictionaryInstaller _dictionaryInstaller;
    private readonly SymbolService _symbolService;

    // Init expression methods array (allocated during Run())
    private SmalltalkOOP _initExpressionMethods;

    /// <summary>
    /// Gets the phase name for diagnostics and logging.
    /// </summary>
    public string Name => "Phase 4: Method Compilation";

    /// <summary>
    /// Gets the InitExpressionMethods array OOP (available after Run() completes).
    /// Returns default(SmalltalkOOP) if no init expressions exist.
    /// </summary>
    public SmalltalkOOP InitExpressionMethods => _initExpressionMethods;

    /// <summary>
    /// Gets the phase number (1-6).
    /// </summary>
    public int PhaseNumber => 4;

    /// <summary>
    /// Initializes a new instance of the Phase4_MethodCompilation class.
    /// </summary>
    /// <param name="bundle">The cold start bundle containing all class and method data.</param>
    /// <param name="allocator">The object graph allocator for creating objects.</param>
    /// <param name="heapManager">The heap manager for memory operations.</param>
    /// <param name="layoutResolver">The layout resolver for class layouts.</param>
    /// <param name="localIdToObjectId">Mapping from local IDs to allocated object OOPs.</param>
    /// <param name="initializedFields">Set of initialized fields to track.</param>
    /// <param name="report">The coldstart report for metrics.</param>
    /// <param name="diagnostics">Optional diagnostic hub for logging.</param>
    public Phase4_MethodCompilation(
        ColdStartBundle bundle,
        ObjectGraphAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report,
        IDiagnosticHub? diagnostics = null)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _localIdToObjectId = localIdToObjectId;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _symbols = bundle.Symbols;
        _diagnostics = diagnostics;

        // Initialize services and helper classes
        _symbolService = new SymbolService(bundle);
        var fieldValueService = new FieldValueService(bundle, _symbolService);
        var dictionaryService = new DictionaryService(allocator, bundle);
        _literalResolver = new LiteralResolver(bundle, allocator, localIdToObjectId, _symbolService, fieldValueService, dictionaryService);
        _dictionaryInstaller = new MethodDictionaryInstaller(bundle, allocator, localIdToObjectId, _symbolService, diagnostics);
    }

    /// <summary>
    /// Validates phase preconditions before execution.
    /// </summary>
    /// <returns>List of precondition failure messages.</returns>
    public IReadOnlyList<string> ValidatePreconditions()
    {
        return new List<string>();
    }

    /// <summary>
    /// Validates phase postconditions after execution.
    /// </summary>
    /// <returns>List of postcondition failure messages.</returns>
    public IReadOnlyList<string> ValidatePostconditions()
    {
        return new List<string>();
    }

    /// <summary>
    /// Executes the phase: allocates dictionaries, compiles methods, creates init expressions, and installs methods.
    /// </summary>
    public void Run()
    {
        // ====================================================================
        // STEP 1: ALLOCATE METHOD DICTIONARIES (from Phase7)
        // ====================================================================
        _dictionaryInstaller.AllocateMethodDictionaries();

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
        _dictionaryInstaller.InstallMethodsIntoDictionaries();
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
        // IMPORTANT: Must include blocks from both regular methods AND init expressions!
        var blockOwnerMap = new Dictionary<string, string>(); // blockLocalId -> ownerMethodOrBlockLocalId

        // Scan regular methods for block literals
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

        // Scan init expressions for block literals
        foreach (var expr in _bundle.InitExpressions)
        {
            string ownerLocalId = expr.LocalId;

            foreach (var literal in expr.Literals)
            {
                if (literal.Type == "compiledMethod" && !string.IsNullOrEmpty(literal.LocalId))
                {
                    // This init expression contains a block as a literal - record ownership
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
            var bytecodes = ClassHierarchyTraversal.DecodeBytecodes(method.Bytecodes);
            var nilOOP = GlobalRegistry.Get("nil");
            var placeholderLiterals = new SmalltalkOOP[method.Literals.Length];
            for (int i = 0; i < placeholderLiterals.Length; i++)
            {
                placeholderLiterals[i] = nilOOP;
            }

            var methodOOP = CreateCompiledMethod(method, placeholderLiterals, bytecodes, compiledMethodClassOOP, byteArrayClassOOP);

            _localIdToObjectId[method.LocalId] = methodOOP;
            objectIdToLocalId[methodOOP] = method.LocalId;
            methodsCreated++;

            // Emit typed event for detail listeners
            // Skip block literals - they don't have selectors
            if (!method.LocalId.StartsWith("block:"))
            {
                string className = method.HomeClassLocalId?.Replace("class:", "").Replace("meta:", "") + (method.HomeClassLocalId?.StartsWith("meta:") == true ? " class" : "") ?? "Unknown";
                string selector = method.SelectorIndex >= 0 && method.SelectorIndex < _bundle.Symbols.Length
                    ? _bundle.Symbols[method.SelectorIndex]
                    : "unknown";

                _diagnostics?.Emit(new MethodCompiledEvent
                {
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0,
                    ClassName = className,
                    Selector = selector,
                    MethodOOP = methodOOP,
                    BytecodeLength = bytecodes.Length,
                    LiteralCount = method.Literals.Length
                });
            }
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
                    var literalOOP = _literalResolver.ResolveLiteralValueAsOOP(method.Literals[i]);
                    // Patch literal in CompiledMethod: literals start at indexed slot 1 (slot 0 is header)
                    _allocator.SetIndexedOOP(methodOOP, i + 1, literalOOP);
                }
                catch (Exception)
                {
                    var index = method.Literals[i].Index;
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
    /// Extracts the homeClass LocalId from an init expression.
    /// Primary approach: Parse from source (reliable and simple).
    /// Fallback: Search literals for Association and extract class name from key.
    /// </summary>
    /// <param name="expr">The init expression descriptor.</param>
    /// <param name="literalOOPs">The resolved literal OOPs.</param>
    /// <returns>The home class local ID.</returns>
    private string ExtractHomeClassFromInitExpression(InitExpression expr, SmalltalkOOP[] literalOOPs)
    {
        // Primary: Parse from source if available
        if (!string.IsNullOrEmpty(expr.Source))
        {
            string className = ParseClassNameFromInitExpression(expr.Source);
            string homeClassLocalId = "meta:" + className;

            // Verify the metaclass exists
            if (_localIdToObjectId.ContainsKey(homeClassLocalId))
            {
                return homeClassLocalId;
            }
        }

        // Fallback: Search literals for an Association
        foreach (var literalOOP in literalOOPs)
        {
            var literalClassOOP = _heapManager.GetSlot(literalOOP, 0);
            var literalClassName = _allocator.GetClassName(literalClassOOP);

            if (literalClassName == "Association")
            {
                // Extract class name from Association's key (Symbol)
                var keySymbolOOP = _heapManager.GetInstanceVariable(literalOOP, 0);
                string className = _allocator.GetSymbolName(keySymbolOOP);
                string homeClassLocalId = "meta:" + className;

                if (_localIdToObjectId.ContainsKey(homeClassLocalId))
                {
                    return homeClassLocalId;
                }
            }
        }

        // Last resort: UndefinedObject
        return "class:UndefinedObject";
    }

    /// <summary>
    /// Parses the class name from an init expression source string.
    /// Examples:
    /// "BinaryChoiceView initialize" -> "BinaryChoiceView"
    /// "Cursor extent:fromArray:offset:" -> "Cursor"
    /// "Float class initialize" -> "Float class" (explicit class-side)
    /// </summary>
    /// <param name="source">The source string of the init expression.</param>
    /// <returns>The parsed class name.</returns>
    /// <exception cref="InvalidOperationException">Thrown when source is null or empty.</exception>
    private string ParseClassNameFromInitExpression(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidOperationException("Init expression source is null or empty");
        }

        // Check for "Foo class selector" pattern (explicit class-side)
        // E.g., "Float class initialize" -> "Float class"
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[1] == "class")
        {
            // Return "Foo class"
            return parts[0] + " class";
        }

        // Normal pattern: "ClassName selector..."
        // E.g., "BinaryChoiceView initialize" -> "BinaryChoiceView"
        if (parts.Length >= 1)
        {
            return parts[0];
        }

        // Fallback (shouldn't happen)
        return source;
    }

    /// <summary>
    /// Creates a CompiledMethod object with homeClass fixed slot and separate ByteArray for bytecodes.
    /// Layout: Fixed slot 0 = homeClass, Indexed slots = [header] [literal1] [literal2] ... [literalN] [bytecodeArrayOOP]
    /// </summary>
    /// <param name="method">The method descriptor from the bundle.</param>
    /// <param name="literalOOPs">The resolved literal OOPs.</param>
    /// <param name="bytecodes">The compiled bytecodes.</param>
    /// <param name="compiledMethodClassOOP">The CompiledMethod class OOP.</param>
    /// <param name="byteArrayClassOOP">The ByteArray class OOP.</param>
    /// <returns>The created CompiledMethod OOP.</returns>
    /// <exception cref="InvalidOperationException">Thrown when home class cannot be resolved.</exception>
    private SmalltalkOOP CreateCompiledMethod(
        MethodDescriptor method,
        SmalltalkOOP[] literalOOPs,
        byte[] bytecodes,
        SmalltalkOOP compiledMethodClassOOP,
        SmalltalkOOP byteArrayClassOOP)
    {
        // CRITICAL: Both regular methods AND blocks need a valid homeClass for super sends
        // Blocks inherit their homeClass from their owning method (via blockOwnerMap)
        SmalltalkOOP homeClassOOP;
        if (method.LocalId.StartsWith("block:"))
        {
            // Get blockOwnerMap to find this block's owning method
            var blockOwnerMap = GlobalRegistry.GetObject<Dictionary<string, string>>("blockOwnerMap");
            if (blockOwnerMap == null)
            {
                throw new InvalidOperationException("blockOwnerMap not found - should be built in CompileAndInstallMethods");
            }

            // Walk ownership chain to find root method (blocks can be nested)
            string currentLocalId = method.LocalId;
            while (blockOwnerMap.TryGetValue(currentLocalId, out string? ownerLocalId))
            {
                currentLocalId = ownerLocalId;
                // If owner is not a block, we've found the root method
                if (!ownerLocalId.StartsWith("block:"))
                    break;
            }

            // Now get the root method's or init expression's homeClass
            // Check if the root is a regular method
            var rootMethod = _bundle.Methods.FirstOrDefault(m => m.LocalId == currentLocalId);
            if (rootMethod != null)
            {
                // Block is owned by a regular method
                if (!_localIdToObjectId.TryGetValue(rootMethod.HomeClassLocalId, out homeClassOOP))
                {
                    throw new InvalidOperationException($"Cannot find home class '{rootMethod.HomeClassLocalId}' for block '{method.LocalId}' via root method '{currentLocalId}'");
                }
            }
            else
            {
                // Root might be an init expression - check if it was already created
                if (_localIdToObjectId.TryGetValue(currentLocalId, out var ownerMethodOOP))
                {
                    // Owner CompiledMethod exists - read its homeClass from slot 0
                    homeClassOOP = _heapManager.GetInstanceVariable(ownerMethodOOP, 0);
                }
                else
                {
                    throw new InvalidOperationException($"Cannot find owner method or init expression '{currentLocalId}' for block '{method.LocalId}' in _localIdToObjectId");
                }
            }
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
        var resultOOP = _allocator.AllocateCompiledMethod(
            compiledMethodClassOOP,
            byteArrayClassOOP,
            homeClassOOP,
            method.Header.Word,
            literalOOPs,
            bytecodes);

        return resultOOP;
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

                // POOL VARIABLES: Skip - these are binding Arrays, NOT Associations
                // Pool variable bindings are 2-element Arrays (#PoolName, #Key)
                if (literal.Association!.PoolDictionaryName != null)
                {
                    continue; // Pool variable binding - not an Association
                }

                totalAssociations++;

                // Get the resolved Association OOP from the method
                var assocOOP = _allocator.GetIndexedOOP(methodOOP, i + 1); // +1 to skip header

                // Validate Association structure
                ValidateAssociationStructure(assocOOP, method.LocalId);

                // VALUE MIGRATION VALIDATION: Check if Association has non-nil value
                var valueOOP = _allocator.GetInstanceVariable(assocOOP, 1);
                if (valueOOP != GlobalRegistry.Get("nil"))
                {
                    nonNilValueCount++;
                }

                // Get the key Symbol name
                string keyName = _symbols[literal.Association!.KeyIndex];

                // Track this Association by key
                if (!associationsByKey.ContainsKey(keyName))
                {
                    associationsByKey[keyName] = [];
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

        // STRICT REACHABILITY VALIDATION
        ValidateAssociationReachability(associationsByKey);
    }

    /// <summary>
    /// Validates that all Association OOPs are reachable from canonical sources.
    /// </summary>
    /// <param name="associationsByKey">Dictionary of associations grouped by key name.</param>
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
                if (assocOOP != GlobalRegistry.Get("nil"))
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

                if (classPoolOOP == GlobalRegistry.Get("nil"))
                    continue;

                int poolSize = _allocator.GetIndexedSize(classPoolOOP);
                for (int i = 0; i < poolSize; i++)
                {
                    var assocOOP = _allocator.GetIndexedOOP(classPoolOOP, i);
                    if (assocOOP != GlobalRegistry.Get("nil"))
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
    }

    /// <summary>
    /// Validates that an Association object has correct structure.
    /// </summary>
    /// <param name="assocOOP">The Association object pointer to validate.</param>
    /// <param name="methodId">The method ID for error reporting.</param>
    /// <exception cref="InvalidOperationException">Thrown when Association structure is invalid.</exception>
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
            // No init expressions - leave as default
            _initExpressionMethods = default;
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

        // NOTE: Store init expressions in ORIGINAL bundle order
        // Program.cs handles the execution ordering (initialize -> comments -> other)
        // We must maintain 1:1 index mapping between bundle.InitExpressions and InitExpressionMethods array

        var initExprMethods = new List<SmalltalkOOP>();

        foreach (var expr in _bundle.InitExpressions)
        {
            // Resolve literals using the SAME canonicalization mechanism as regular methods
            var literalOOPs = expr.Literals.Select(_literalResolver.ResolveLiteralValueAsOOP).ToArray();

            // Determine homeClass from the resolved literals (not by parsing source!)
            // Init expressions like "BinaryChoiceView initialize" have first literal = Association for the class
            // We extract the metaclass from the Association's value
            string homeClassLocalId = ExtractHomeClassFromInitExpression(expr, literalOOPs);

            // Create a CompiledMethod object for the init expression
            var bytecodes = ClassHierarchyTraversal.DecodeBytecodes(expr.Bytecodes);
            SmalltalkOOP initMethodOOP = CreateCompiledMethod(
                new MethodDescriptor
                {
                    LocalId = expr.LocalId,
                    HomeClassLocalId = homeClassLocalId,
                    SelectorIndex = 0,
                    Header = expr.Header,
                    Literals = [],
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

        // Store for ColdStartOrchestrator to pass to BuildVMMetadata
        _initExpressionMethods = initArrayOOP;
    }
}
