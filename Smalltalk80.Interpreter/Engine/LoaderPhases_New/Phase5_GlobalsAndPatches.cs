using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;
using System;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases_New;

/// <summary>
/// Phase 5: Globals and Patches
/// Consolidates patch application, USTable creation, and global variable installation.
/// This phase handles:
/// 1. Applying patches to fix forward references and circular structures
/// 2. Creating and populating the Universal Symbol Table (USTable)
/// 3. Creating SystemDictionary and installing all global variables
/// </summary>
public class Phase5_GlobalsAndPatches
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly IReadOnlyList<string> _symbols;
    private readonly ColdstartReport _report;
    private readonly DebugOptions _debugOptions;

    public Phase5_GlobalsAndPatches(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        ISmalltalkHeapManager heapManager,
        LayoutResolver layoutResolver,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report,
        DebugOptions? debugOptions = null)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _initializedFields = initializedFields;
        _symbols = bundle.Symbols;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _debugOptions = debugOptions ?? DebugOptions.None;
    }

    public void Run()
    {
        // Step 1: Apply patches (from Phase10)
        ApplyPatches();

        // Step 2: Create and populate USTable (from Phase11)
        CreateAndPopulateUSTable();

        // Step 3: Create and populate SystemDictionary (from Phase12)
        CreateAndPopulateSystemDictionary();

        // Freeze AssociationRegistry after all globals installed
        AssociationRegistry.Freeze();
        Console.WriteLine($"[AssociationRegistry] Frozen with {AssociationRegistry.Count} associations");

        // DEBUG: Check ALL Associations in the heap, not just those in AssociationRegistry
        if (_debugOptions.ShouldDebugAssociations)
        {
            Console.WriteLine($"[Phase5] Scanning entire heap for ALL Associations...");
            var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
            int totalAssociations = 0;
            int corruptedCount = 0;
            int registeredCount = AssociationRegistry.GetAll().Count;

            // Scan entire heap for Association objects
            for (int oop = 0; oop < 100000; oop += 2) // Scan first 100000 OOPs (even numbers only for object pointers)
            {
                try
                {
                    var objOOP = new SmalltalkOOP(oop);
                    var classOOP = _heapManager.GetSlot(objOOP, 0);
                    if (classOOP == associationClassOOP)
                    {
                        totalAssociations++;
                        var valueOOP = _heapManager.GetInstanceVariable(objOOP, 1);
                        if (valueOOP.RawValue == 10776)
                        {
                            var keyOOP = _heapManager.GetInstanceVariable(objOOP, 0);
                            Console.WriteLine($"  [ERROR] Found corrupted Association: OOP={objOOP}, keyOOP={keyOOP}, value=10776");
                            corruptedCount++;
                        }
                    }
                }
                catch
                {
                    // Skip invalid OOPs
                }
            }

            Console.WriteLine($"  Total Associations in heap: {totalAssociations}");
            Console.WriteLine($"  Associations in AssociationRegistry: {registeredCount}");
            Console.WriteLine($"  Unregistered Associations: {totalAssociations - registeredCount}");
            if (corruptedCount == 0)
            {
                Console.WriteLine($"  No corrupted Associations found in entire heap (good!)");
            }
            else
            {
                Console.WriteLine($"  [ERROR] Found {corruptedCount} corrupted Associations in heap!");
            }
        }
    }

    // ================================================================================
    // SECTION 1: PATCH APPLICATION (from Phase10_PatchApplication)
    // ================================================================================

    /// <summary>
    /// Applies post-allocation patches to fix circular references and late-bound values.
    /// </summary>
    private void ApplyPatches()
    {
        if (_bundle.Patches == null || _bundle.Patches.Length == 0)
        {
            Console.WriteLine($"  No patches to apply");
            return;
        }

        Console.WriteLine($"  Applying {_bundle.Patches.Length} patches...");

        foreach (var patch in _bundle.Patches)
        {
            switch (patch.Type)
            {
                case "SetInstanceVariable":
                    ApplySetInstanceVariablePatch(patch);
                    break;

                case "InstallMethodDictionary":
                    ApplyInstallMethodDictionaryPatch(patch);
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
        int fieldIndex = _layoutResolver.GetFieldIndex(patch.TargetLocalId!, patch.FieldName!);

        // Get before value for logging (if debug enabled)
        SmalltalkOOP? beforeOOP = null;
        if (_debugOptions.ShouldDebugPatches)
        {
            beforeOOP = _heapManager.GetInstanceVariable(targetOOP, fieldIndex);
        }

        // Resolve new value
        var valueOOP = ResolveFieldValue(patch.Value);

        // Apply patch
        _heapManager.SetInstanceVariable(targetOOP, fieldIndex, valueOOP);

        // Debug output only if enabled
        if (_debugOptions.ShouldDebugPatches)
        {
            Console.WriteLine($"    [PATCH] SetInstanceVariable: {patch.TargetLocalId}.{patch.FieldName} = {valueOOP} (before: {beforeOOP})");
        }
    }

    private void ApplyInstallMethodDictionaryPatch(PatchDescriptor patch)
    {
        var classOOP = _localIdToObjectId[patch.ClassLocalId!];

        // Method dictionary should already exist from Phase 4
        // This patch type is for forward compatibility

        if (_debugOptions.ShouldDebugPatches)
        {
            Console.WriteLine($"    [PATCH] InstallMethodDictionary: {patch.ClassLocalId} (already installed in Phase 4)");
        }
    }

    private void ApplyReplaceLiteralPatch(PatchDescriptor patch)
    {
        var methodOOP = _localIdToObjectId[patch.MethodLocalId!];
        var literalOOP = _heapManager.GetIndexedOOP(methodOOP, patch.LiteralIndex!.Value + 1);

        var newValueOOP = ResolveFieldValue(patch.Value);

        // Check if the literal is an Association
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        var literalClassOOP = _heapManager.GetSlot(literalOOP, 0);

        if (literalClassOOP == associationClassOOP)
        {
            // It's an Association - set its VALUE field (index 1), don't replace the Association itself
            SmalltalkOOP? beforeValue = null;
            if (_debugOptions.ShouldDebugPatches)
            {
                beforeValue = _heapManager.GetInstanceVariable(literalOOP, 1);
            }

            _heapManager.SetInstanceVariable(literalOOP, 1, newValueOOP);

            if (_debugOptions.ShouldDebugPatches)
            {
                Console.WriteLine($"    [PATCH] ReplaceLiteral: {patch.MethodLocalId}[{patch.LiteralIndex}] Association.value = {newValueOOP} (before: {beforeValue})");
            }
        }
        else
        {
            // Not an Association - replace the literal itself
            _heapManager.SetIndexedOOP(methodOOP, patch.LiteralIndex.Value + 1, newValueOOP);

            if (_debugOptions.ShouldDebugPatches)
            {
                Console.WriteLine($"    [PATCH] ReplaceLiteral: {patch.MethodLocalId}[{patch.LiteralIndex}] = {newValueOOP} (before: {literalOOP})");
            }
        }
    }

    private SmalltalkOOP ResolveFieldValue(FieldValue? value)
    {
        if (value == null)
            return SpecialObjectsArray.Instance.Nil;

        if (value.Type == "objectRef")
        {
            var resolvedOOP = _localIdToObjectId[value.LocalId!];
            // DEBUG: Check if String class is being resolved to wrong OOP
            if (_debugOptions.ShouldDebugPatches && value.LocalId == "class:String")
            {
                Console.WriteLine($"      [DEBUG] Resolving class:String -> {resolvedOOP}");
                if (resolvedOOP.RawValue == 10776)
                {
                    Console.WriteLine($"      [ERROR] class:String mapped to OOP 10776 (should be String class)!");
                }
            }
            return resolvedOOP;
        }

        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger((int)value.Value!),
            "stringRef" => _allocator.AllocateString(_bundle.Strings[value.Index!.Value]),
            "symbolRef" => SymbolRegistry.GetSymbolOOP(value.Index!.Value),
            _ => throw new InvalidOperationException($"Unknown field value type: {value.Type}")
        };
    }


    // ================================================================================
    // SECTION 2: USTABLE CREATION AND POPULATION (from Phase11_USTablePopulation)
    // ================================================================================

    /// <summary>
    /// Creates and populates the USTable with all interned symbols.
    /// </summary>
    private void CreateAndPopulateUSTable()
    {
        var symbolObjects = SymbolRegistry.AllSymbols();
        int ustableSize = ComputeUSTableSize(symbolObjects.Count());

        SmalltalkOOP ustableOOP = CreateUSTable(ustableSize);

        foreach (var (index, symbolOOP) in symbolObjects)
        {
            string symbolName = _bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            InsertIntoUSTable(ustableOOP, symbolOOP, hash);
        }

        GlobalRegistry.Register("USTable", ustableOOP);

        // Update report metric
        _report.Metrics.USTableCapacity = ustableSize;

        // Note: USTable will be installed as a global variable in Step 3
    }

    private int ComputeUSTableSize(int count) => count * 2; // Load factor heuristic

    /// <summary>
    /// Creates a USTable (Universal Symbol Table) as an Array.
    /// </summary>
    private SmalltalkOOP CreateUSTable(int size)
    {
        // Look up Array class layout
        var arrayClassDefinition = FindClassDefinition("Array");
        if (arrayClassDefinition == null)
        {
            throw new InvalidOperationException("Array class not found in bundle");
        }

        var ustableOOP = _allocator.AllocateArray("Array", size);

        // Set class reference (Phase 11 runs after Phase X, so we must set it manually)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _allocator.SetSlot(ustableOOP, 0, arrayClassOOP);

        // Initialize all slots to nil
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < size; i++)
        {
            _allocator.SetIndexedOOP(ustableOOP, i, nilOOP);
        }

        return ustableOOP;
    }

    /// <summary>
    /// Inserts a symbol into the USTable using open addressing.
    /// </summary>
    private void InsertIntoUSTable(SmalltalkOOP ustableOOP, SmalltalkOOP symbolOOP, int hash)
    {
        int size = _allocator.GetIndexedSize(ustableOOP);
        int index = hash % size;

        // Find first empty slot using open addressing
        while (true)
        {
            var slotOOP = _allocator.GetIndexedOOP(ustableOOP, index);
            if (slotOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Found empty slot
                _allocator.SetIndexedOOP(ustableOOP, index, symbolOOP);
                break;
            }

            // Move to next slot (wrap around)
            index = (index + 1) % size;
        }
    }

    // ================================================================================
    // SECTION 3: SYSTEM DICTIONARY AND GLOBAL VARIABLE INSTALLATION (from Phase12)
    // ================================================================================

    /// <summary>
    /// Creates SystemDictionary and installs all global variables.
    /// </summary>
    private void CreateAndPopulateSystemDictionary()
    {
        int installedGlobals = 0;

        // Install Smalltalk -> SystemDictionary (self-reference)
        InstallSmalltalkGlobal();
        installedGlobals++;

        // Install USTable -> Universal Symbol Table
        if (GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableOOP))
        {
            InstallUSTableGlobal(ustableOOP);
            installedGlobals++;
        }

        // Install Processor -> ProcessorScheduler singleton
        if (GlobalRegistry.TryGet("ProcessorScheduler", out SmalltalkOOP processorSchedulerOOP))
        {
            InstallProcessorGlobal(processorSchedulerOOP);
            installedGlobals++;
        }

        // Install all classes as global variables in SystemDictionary
        // Each class becomes accessible as: Smalltalk at: #ClassName
        foreach (var cls in _bundle.Classes)
        {
            // Only install instance classes (not metaclasses) as globals
            if (!cls.IsMetaclass && _localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
            {
                string className = _bundle.Symbols[cls.NameIndex];
                InstallClassGlobal(className, classOOP);
                installedGlobals++;
            }
        }
    }

    /// <summary>
    /// Installs "Smalltalk" as a global variable that references the SystemDictionary itself.
    /// This creates the self-reference: Smalltalk at: #Smalltalk -> SystemDictionary
    /// </summary>
    private void InstallSmalltalkGlobal()
    {
        var systemDictionaryOOP = GlobalRegistry.SystemDictionaryOOP;

        // Get or create canonical Association
        var associationOOP = GetOrCreateCanonicalAssociation("Smalltalk");

        // Set Association value to SystemDictionary
        _allocator.SetInstanceVariable(associationOOP, 1, systemDictionaryOOP);

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, "Smalltalk");
    }

    /// <summary>
    /// Installs "USTable" as a global variable that references the Universal Symbol Table.
    /// This enables: Smalltalk at: #USTable -> Array of all symbols
    /// </summary>
    private void InstallUSTableGlobal(SmalltalkOOP ustableOOP)
    {
        // Get or create canonical Association
        var associationOOP = GetOrCreateCanonicalAssociation("USTable");

        // Set Association value to USTable
        _allocator.SetInstanceVariable(associationOOP, 1, ustableOOP);

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, "USTable");
    }

    /// <summary>
    /// Installs "Processor" as a global variable that references the ProcessorScheduler singleton.
    /// This enables: Smalltalk at: #Processor -> ProcessorScheduler instance
    /// Note: By convention, the global is named "Processor" not "ProcessorScheduler"
    /// </summary>
    private void InstallProcessorGlobal(SmalltalkOOP processorSchedulerOOP)
    {
        // Get or create canonical Association
        var associationOOP = GetOrCreateCanonicalAssociation("Processor");

        // Set Association value to ProcessorScheduler
        _allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, "Processor");
    }

    /// <summary>
    /// Installs a class as a global variable in SystemDictionary.
    /// This enables: Smalltalk at: #ClassName -> Class object
    /// </summary>
    private void InstallClassGlobal(string className, SmalltalkOOP classOOP)
    {
        // Get or create canonical Association
        var associationOOP = GetOrCreateCanonicalAssociation(className);

        // Set Association value to the class object
        _allocator.SetInstanceVariable(associationOOP, 1, classOOP);

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, className);
    }

    /// <summary>
    /// Installs an association in the first available slot in SystemDictionary.
    /// </summary>
    private void InstallAssociationInSystemDictionary(SmalltalkOOP associationOOP, string globalName)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);

        int usedSlots = 0;
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Find first nil slot in SystemDictionary
        for (int i = 0; i < systemDictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(systemDictOOP, i);

            if (slotOOP == nilOOP || slotOOP == SmalltalkOOP.Null)
            {
                // Found empty slot - handle both nil and any uninitialized cases during architectural transition
                _allocator.SetIndexedOOP(systemDictOOP, i, associationOOP);
                return;
            }
            else
            {
                usedSlots++;
            }
        }

        throw new InvalidOperationException($"SystemDictionary is full - cannot install {globalName} global variable (all {systemDictSize} slots occupied)");
    }

    /// <summary>
    /// Gets the symbol OOP for a given symbol name.
    /// </summary>
    private SmalltalkOOP GetSymbolOOP(string symbolName)
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == symbolName)
            {
                return SymbolRegistry.GetSymbolOOP(i);
            }
        }

        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table");
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        // Find Association class definition
        var associationClassDefinition = FindClassDefinition("Association");
        if (associationClassDefinition == null)
        {
            throw new InvalidOperationException("Association class not found in bundle");
        }

        var associationOOP = _allocator.AllocateFixedObject("Association");

        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
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
    /// Calculates the total instance variable count for a class including inherited ones.
    /// </summary>
    private int CalculateTotalInstanceVariableCount(ClassDescriptor classDefinition)
    {
        int totalCount = 0;
        var currentClass = classDefinition;

        while (currentClass != null)
        {
            totalCount += currentClass.InstVarNames.Length;

            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = _bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = _bundle.Classes.FirstOrDefault(c =>
                    !c.IsMetaclass && _bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }

        return totalCount;
    }

    /// <summary>
    /// Gets or creates a canonical Association for the given key name.
    /// Uses AssociationRegistry to ensure only one Association per global variable name.
    /// </summary>
    private SmalltalkOOP GetOrCreateCanonicalAssociation(string keyName)
    {
        // Get symbol OOP for the key
        SmalltalkOOP keySymbolOOP = GetSymbolOOP(keyName);

        // Use AssociationRegistry to get or create
        return AssociationRegistry.GetOrCreate(keyName, () =>
        {
            // Create new Association with key and nil value
            return AllocateAssociation(keySymbolOOP, SpecialObjectsArray.Instance.Nil);
        });
    }

}