using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;
using System;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

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

        // Step 2b: Populate Undeclared dictionary (v1.0.7)
        PopulateUndeclaredDictionary();

        // Step 3: Create and populate SystemDictionary (from Phase12)
        CreateAndPopulateSystemDictionary();

        // Step 4: Create DefaultTextStyle (after AssociationRegistry is populated)
        CreateDefaultTextStyle();

        // Freeze AssociationRegistry after all globals installed
        AssociationRegistry.Freeze();
        Console.WriteLine($"[AssociationRegistry] Frozen with {AssociationRegistry.Count} associations");

        // DEBUG: Check ALL Associations in the heap, not just those in AssociationRegistry
        if (_debugOptions.ShouldDebugAssociations)
        {
            Console.WriteLine($"[Phase5] Scanning entire heap for ALL Associations...");
            var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
            int totalAssociations = 0;
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
            // DEBUG: Check if String class is being resolved correctly
            if (_debugOptions.ShouldDebugPatches && value.LocalId == "class:String")
            {
                Console.WriteLine($"      [DEBUG] Resolving class:String -> {resolvedOOP}");
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
    /// Uses two-pass approach to avoid creating temporary arrays:
    /// Pass 1: Count symbols per bucket
    /// Pass 2: Allocate buckets with correct capacity and insert symbols
    /// </summary>
    private void CreateAndPopulateUSTable()
    {
        var symbolObjects = SymbolRegistry.AllSymbols().ToList();
        int ustableSize = ComputeUSTableSize(symbolObjects.Count);

        Console.WriteLine($"  [Phase5] Creating USTable: {symbolObjects.Count} symbols, table size {ustableSize}");

        // Pass 1: Count how many symbols will go in each bucket
        var bucketCounts = new int[ustableSize];
        foreach (var (index, _) in symbolObjects)
        {
            string symbolName = _bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            int bucketIndex = hash % ustableSize;
            bucketCounts[bucketIndex]++;
        }

        // Create USTable with pre-sized buckets
        SmalltalkOOP ustableOOP = CreateUSTableWithBuckets(ustableSize, bucketCounts);

        // Pass 2: Insert symbols into their buckets (no array reallocation needed)
        var bucketPositions = new int[ustableSize]; // Track next insertion index per bucket
        foreach (var (index, symbolOOP) in symbolObjects)
        {
            string symbolName = _bundle.Symbols[index];
            int hash = SymbolHasher.ComputeHash(symbolName);
            int bucketIndex = hash % ustableSize;

            var bucketOOP = _allocator.GetIndexedOOP(ustableOOP, bucketIndex);
            int position = bucketPositions[bucketIndex]++;

            // DIAGNOSTIC: Verify we're inserting Symbol OOPs, not SmallIntegers
            if (symbolOOP.IsSmallInteger)
            {
                Console.WriteLine($"  ERROR: Inserting SmallInteger {symbolOOP.AsSmallInteger()} into USTable bucket {bucketIndex} position {position}!");
                Console.WriteLine($"    Symbol name: \"{symbolName}\"");
            }

            // DIAGNOSTIC: Log bucket 3240 population
            if (bucketIndex == 3240)
            {
                Console.WriteLine($"  [Phase5] Inserting symbol \"{symbolName}\" ({symbolOOP}) into bucket 3240 position {position}");
            }

            _allocator.SetIndexedOOP(bucketOOP, position, symbolOOP);
        }

        GlobalRegistry.Register("USTable", ustableOOP);

        // Update report metric
        _report.Metrics.USTableCapacity = ustableSize;

        // Note: USTable will be installed as a global variable in Step 3
    }

    private int ComputeUSTableSize(int count) => count * 2; // Load factor heuristic

    /// <summary>
    /// Creates a USTable (Universal Symbol Table) as an Array of Arrays.
    /// Each bucket is pre-allocated with the exact capacity needed (from bucketCounts).
    /// This avoids creating temporary arrays during insertion (zero garbage).
    /// Final structure matches Symbol class>>rehash: Array of Arrays for collision chaining.
    /// </summary>
    private SmalltalkOOP CreateUSTableWithBuckets(int size, int[] bucketCounts)
    {
        var ustableOOP = _allocator.AllocateArray("Array", size);

        // Set class reference (Phase 5 runs after Phase 3, so class references are set)
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _allocator.SetSlot(ustableOOP, 0, arrayClassOOP);

        // Create bucket arrays with exact capacity (no reallocation needed later)
        // CRITICAL: ALL buckets must be Arrays, even empty ones (not nil!)
        // Symbol class>>rehash uses: USTable _ USTable collect: [:sym | Array new: 0]
        // Symbol>>intern: does: USTable at: index put: ((USTable at: index) copyWith: sym)
        // If bucket is nil, copyWith: fails → infinite recursion in error handling
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < size; i++)
        {
            // Allocate array for this bucket (size 0 for empty buckets)
            var bucketOOP = _allocator.AllocateArray("Array", bucketCounts[i]);
            _allocator.SetSlot(bucketOOP, 0, arrayClassOOP);

            // DIAGNOSTIC: Log bucket 68364 creation
            if (bucketOOP.RawValue == 68364)
            {
                Console.WriteLine($"  [Phase5] Created USTable bucket {bucketOOP} (index {i}, size {bucketCounts[i]})");
            }

            // Initialize all indexed slots to nil (C# arrays default to OOP(0))
            for (int j = 0; j < bucketCounts[i]; j++)
            {
                _allocator.SetIndexedOOP(bucketOOP, j, nilOOP);
            }

            _allocator.SetIndexedOOP(ustableOOP, i, bucketOOP);
        }

        return ustableOOP;
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

        // Initialize SingleCharSymbols class variable in Symbol's classPool
        // Pre-populate with 128 single-character symbols so Symbol initialize works correctly
        InitializeSingleCharSymbols();

        // Initialize FilePool shared pool dictionary
        // File class and related classes (FileDirectory, FilePage, FileStream) use this pool
        InitializeFilePool();

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
    /// Installs "USTable" as a class variable in Symbol's classPool.
    /// USTable is defined as: Symbol classVariableNames: 'SingleCharSymbols USTable'
    /// This enables: Symbol USTable -> Array of symbol buckets
    /// </summary>
    private void InstallUSTableGlobal(SmalltalkOOP ustableOOP)
    {
        // CRITICAL: USTable is a CLASS VARIABLE of Symbol, not a global!
        // Symbol class definition (line 37241 of Smalltalk-80.st):
        //   classVariableNames: 'SingleCharSymbols USTable '
        //
        // We need to install USTable in Symbol's classPool (Dictionary)

        // Look up Symbol class
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");
        if (symbolClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during USTable installation");
        }

        // Get Symbol's classPool (instance variable index 7 for Class)
        var classPoolOOP = _allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol classPool is nil during USTable installation");
        }

        // Create Association for USTable
        var associationOOP = GetOrCreateCanonicalAssociation("USTable");
        _allocator.SetInstanceVariable(associationOOP, 1, ustableOOP);

        // Install Association in Symbol's classPool
        // classPool is a Dictionary, search for existing USTable association first, then add if not found
        int classPoolSize = _allocator.GetIndexedSize(classPoolOOP);
        bool installed = false;
        int emptySlotIndex = -1;

        // Pass 1: Search for existing USTable association AND track first empty slot
        for (int i = 0; i < classPoolSize; i++)
        {
            var entryOOP = _allocator.GetIndexedOOP(classPoolOOP, i);

            if (entryOOP == SpecialObjectsArray.Instance.Nil)
            {
                // Track first empty slot (don't install yet)
                if (emptySlotIndex == -1)
                {
                    emptySlotIndex = i;
                }
                continue;
            }

            // Check if this is the USTable association
            var keyOOP = _allocator.GetInstanceVariable(entryOOP, 0);
            string keyName = GetSymbolName(keyOOP);
            if (keyName == "USTable")
            {
                // Update existing association's value
                _allocator.SetInstanceVariable(entryOOP, 1, ustableOOP);
                installed = true;
                Console.WriteLine($"  [Phase5] Updated existing USTable association at index {i}");
                break;
            }
        }

        // Pass 2: If no existing association found, create new one in empty slot
        if (!installed)
        {
            if (emptySlotIndex == -1)
            {
                throw new InvalidOperationException($"Failed to install USTable - no empty slots in Symbol classPool (size {classPoolSize})");
            }

            _allocator.SetIndexedOOP(classPoolOOP, emptySlotIndex, associationOOP);
            installed = true;
            Console.WriteLine($"  [Phase5] Installed USTable in Symbol classPool: array size {_allocator.GetIndexedSize(ustableOOP)} buckets");
        }
    }

    /// <summary>
    /// Initializes the "SingleCharSymbols" class variable in Symbol's classPool.
    /// SingleCharSymbols is a class variable: Symbol classVariableNames: 'SingleCharSymbols USTable'
    /// Pre-populates it with an Array of 128 single-character symbols (ASCII 0-127) so that
    /// Symbol initialize can run without errors. The hasInterned:ifTrue: method expects this
    /// to exist and be populated before Symbol initialize executes.
    /// </summary>
    private void InitializeSingleCharSymbols()
    {
        // Look up Symbol class
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");
        if (symbolClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during SingleCharSymbols installation");
        }

        // Get Symbol's classPool (instance variable index 7 for Class)
        var classPoolOOP = _allocator.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol classPool is nil during SingleCharSymbols installation");
        }

        // Create Array with 128 nil elements - Symbol initialize will populate it
        // hasInterned:ifTrue: will access SingleCharSymbols[ascii+1] and get nil,
        // which it treats as "not found" - allowing Symbol initialize to proceed
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        if (arrayClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Array class not found");
        }
        var arrayOOP = _allocator.AllocateArray(arrayClassOOP, 128);

        // Initialize all elements to nil
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < 128; i++)
        {
            _allocator.SetIndexedOOP(arrayOOP, i, nilOOP);
        }

        // Create Association for SingleCharSymbols with the array as value
        var associationOOP = GetOrCreateCanonicalAssociation("SingleCharSymbols");
        _allocator.SetInstanceVariable(associationOOP, 1, arrayOOP);

        // Install Association in Symbol's classPool
        int classPoolSize = _allocator.GetIndexedSize(classPoolOOP);
        bool installed = false;
        int emptySlotIndex = -1;

        // Pass 1: Search for existing SingleCharSymbols association AND track first empty slot
        for (int i = 0; i < classPoolSize; i++)
        {
            var entryOOP = _allocator.GetIndexedOOP(classPoolOOP, i);

            if (entryOOP == SpecialObjectsArray.Instance.Nil)
            {
                if (emptySlotIndex == -1)
                {
                    emptySlotIndex = i;
                }
                continue;
            }

            // Check if this is the SingleCharSymbols association
            var keyOOP = _allocator.GetInstanceVariable(entryOOP, 0);
            string? keyName = GetSymbolName(keyOOP);
            if (keyName == "SingleCharSymbols")
            {
                // Update existing association
                _allocator.SetInstanceVariable(entryOOP, 1, arrayOOP);
                installed = true;
                Console.WriteLine($"  [Phase5] Updated SingleCharSymbols class variable in Symbol classPool (128 nil elements)");
                break;
            }
        }

        // Pass 2: If not found, install in first empty slot
        if (!installed)
        {
            if (emptySlotIndex == -1)
            {
                throw new InvalidOperationException($"Failed to install SingleCharSymbols - no empty slots in Symbol classPool (size {classPoolSize})");
            }

            _allocator.SetIndexedOOP(classPoolOOP, emptySlotIndex, associationOOP);
            installed = true;
            Console.WriteLine($"  [Phase5] Initialized SingleCharSymbols class variable in Symbol classPool at slot {emptySlotIndex} (128 nil elements)");
        }
    }

    /// <summary>
    /// Initializes the "FilePool" shared pool dictionary.
    /// FilePool is a pool dictionary used by File, FileDirectory, FilePage, and FileStream classes.
    /// Pre-creates an empty Dictionary so File initialize can populate it with constants:
    ///   FilePool at: #Read put: 1.
    ///   FilePool at: #Write put: 2.
    ///   FilePool at: #Shorten put: 3.
    /// </summary>
    private void InitializeFilePool()
    {
        // Look up Dictionary class
        if (!GlobalRegistry.TryGet("class:Dictionary", out SmalltalkOOP dictionaryClassOOP))
        {
            throw new InvalidOperationException("Dictionary class not found during FilePool initialization");
        }

        // Create an empty Dictionary with capacity for the 3 constants File initialize will add
        // Dictionary layout: instance variables + indexed array slots
        // Use initial size of 8 slots (3 constants with ~75% load factor)
        int initialSize = 8;

        // Allocate Dictionary as PointerIndexed object
        var dictionaryOOP = _allocator.AllocateObject("Dictionary", ObjectFormat.PointerIndexed, initialSize);

        // Set class reference
        _allocator.SetObjectClass(dictionaryOOP, dictionaryClassOOP);

        // Initialize tally to SmallInteger 0
        // Find 'tally' instance variable index in Dictionary hierarchy (inherited from Set)
        int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
        if (tallyIndex >= 0)
        {
            _allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(0));
        }

        // Initialize all indexed array elements to nil
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        for (int i = 0; i < initialSize; i++)
        {
            _allocator.SetIndexedOOP(dictionaryOOP, i, nilOOP);
        }

        // Create Association for FilePool
        var associationOOP = GetOrCreateCanonicalAssociation("FilePool");
        _allocator.SetInstanceVariable(associationOOP, 1, dictionaryOOP);

        // Install FilePool as a global in Smalltalk dictionary
        InstallAssociationInSystemDictionary(associationOOP, "FilePool");

        // Register in GlobalRegistry for runtime access
        GlobalRegistry.Register("FilePool", dictionaryOOP);

        Console.WriteLine($"  [Phase5] Initialized FilePool global as empty Dictionary");
    }

    /// <summary>
    /// Extracts the string name from a Symbol OOP by reading its indexed bytes.
    /// </summary>
    private string GetSymbolName(SmalltalkOOP symbolOOP)
    {
        int size = _allocator.GetIndexedSize(symbolOOP);
        var bytes = new byte[size];
        for (int i = 0; i < size; i++)
        {
            bytes[i] = _allocator.GetIndexedByte(symbolOOP, i);
        }
        return System.Text.Encoding.ASCII.GetString(bytes);
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

    // ================================================================================
    // SECTION 2b: UNDECLARED DICTIONARY POPULATION (v1.0.7)
    // ================================================================================

    /// <summary>
    /// Populates the Undeclared dictionary with Association objects for each variable
    /// found in bundle.Undeclared[]. Each Association is created with key=variableName
    /// and value=nil. When the variable becomes defined (via init expressions or runtime),
    /// SystemDictionary>>at:put: will migrate the Association to Smalltalk dictionary.
    /// </summary>
    private void PopulateUndeclaredDictionary()
    {
        if (_bundle.Undeclared == null || _bundle.Undeclared.Length == 0)
        {
            Console.WriteLine("[Phase5] No undeclared variables in bundle - skipping Undeclared population");
            return;
        }

        Console.WriteLine($"[Phase5] Populating Undeclared dictionary with {_bundle.Undeclared.Length} variables...");

        var undeclaredOOP = GlobalRegistry.Get("Undeclared");
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int addedCount = 0;

        foreach (var undeclaredVar in _bundle.Undeclared)
        {
            // Get or create canonical Association for this variable
            var association = GetOrCreateCanonicalAssociation(undeclaredVar.Key);

            // Add Association to Undeclared dictionary using Dictionary>>at:put: semantics
            AddAssociationToUndeclaredDictionary(undeclaredOOP, association);
            addedCount++;
        }

        // Update tally
        int tallyIndex = _layoutResolver.GetFieldIndex("class:Dictionary", "tally");
        if (tallyIndex >= 0)
        {
            var tallyOOP = SmalltalkOOP.FromSmallInteger(addedCount);
            _allocator.SetInstanceVariable(undeclaredOOP, tallyIndex, tallyOOP);
        }

        Console.WriteLine($"  ✓ Added {addedCount} Associations to Undeclared dictionary");

        // Show sample of undeclared variables
        if (addedCount > 0)
        {
            var sampleCount = Math.Min(5, _bundle.Undeclared.Length);
            Console.WriteLine($"  Sample undeclared variables:");
            for (int i = 0; i < sampleCount; i++)
            {
                var varName = _bundle.Undeclared[i].Key;
                var refCount = _bundle.Undeclared[i].References.Length;
                Console.WriteLine($"    - {varName} ({refCount} reference(s))");
            }
        }
    }

    /// <summary>
    /// Adds an Association to the Undeclared dictionary using open-addressing hash table.
    /// This matches the Dictionary>>at:put: implementation in Smalltalk-80.
    /// </summary>
    private void AddAssociationToUndeclaredDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP)
    {
        int capacity = _allocator.GetIndexedSize(dictionaryOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key from the Association (slot 0 = key, slot 1 = value)
        var keyOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Compute hash for the key (simple identity hash using OOP value)
        int hash = Math.Abs(keyOOP.RawValue) % capacity;

        // Linear probe to find empty slot
        for (int probe = 0; probe < capacity; probe++)
        {
            int slotIndex = (hash + probe) % capacity;
            var slotOOP = _allocator.GetIndexedOOP(dictionaryOOP, slotIndex);

            if (slotOOP == nilOOP)
            {
                // Found empty slot - install Association
                _allocator.SetIndexedOOP(dictionaryOOP, slotIndex, associationOOP);
                return;
            }
        }

        throw new InvalidOperationException("Undeclared dictionary is full - cannot add Association");
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

    #region DefaultTextStyle Creation

    /// <summary>
    /// Creates a minimal DefaultTextStyle object to support PopUpMenu and other UI features.
    /// DefaultTextStyle is referenced by TextStyle class>>default which returns 'DefaultTextStyle copy'.
    /// PopUpMenu initialization calls 'TextStyle default fontAt: 1', so we need at least one font.
    ///
    /// This MUST be called in Phase 5 after AssociationRegistry is populated (during method compilation).
    /// The compiler creates Association objects as method literals, and we need to find and update the
    /// DefaultTextStyle Association's value.
    /// </summary>
    private void CreateDefaultTextStyle()
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Step 1: Create a minimal stub StrikeFont
        // StrikeFont has many instance variables, but most can be nil for our stub purposes
        var strikeFontOOP = _allocator.AllocateFixedObject("StrikeFont");

        // Set class reference for StrikeFont
        var strikeFontClassOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
        _allocator.SetObjectClass(strikeFontOOP, strikeFontClassOOP);

        // Initialize all StrikeFont fields to nil
        int strikeFontInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("StrikeFont", _bundle);
        for (int i = 0; i < strikeFontInstVarCount; i++)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
            _initializedFields.Add((strikeFontOOP, i));
        }

        // Set minimal StrikeFont properties to prevent errors
        // name: "SystemFont" (maps to Windows font later)
        int nameIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "name", _bundle);
        if (nameIndex >= 0)
        {
            var fontNameOOP = InternString("SystemFont");
            _allocator.SetInstanceVariable(strikeFontOOP, nameIndex, fontNameOOP);
        }

        // ascent: 10 (matches typical Windows system font metrics)
        int ascentIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "ascent", _bundle);
        if (ascentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, ascentIndex, SmalltalkOOP.FromSmallInteger(10));
        }

        // descent: 3
        int descentIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "descent", _bundle);
        if (descentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, descentIndex, SmalltalkOOP.FromSmallInteger(3));
        }

        // maxWidth: 8 (monospace estimate)
        int maxWidthIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "maxWidth", _bundle);
        if (maxWidthIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, maxWidthIndex, SmalltalkOOP.FromSmallInteger(8));
        }

        // Step 2: Create fontArray with one stub font
        var fontArrayOOP = _allocator.AllocateArray("Array", 1);
        _allocator.SetIndexedOOP(fontArrayOOP, 0, strikeFontOOP);

        // Set class reference for Array
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _allocator.SetObjectClass(fontArrayOOP, arrayClassOOP);

        // Step 3: Create TextStyle object
        var textStyleOOP = _allocator.AllocateFixedObject("TextStyle");

        // Set class reference for TextStyle
        var textStyleClassOOP = GlobalRegistry.LookupClassOOP("TextStyle");
        _allocator.SetObjectClass(textStyleOOP, textStyleClassOOP);

        // Get TextStyle instance variable indices
        // TextStyle has: fontArray, lineGrid, baseline, alignment, firstIndent, restIndent, rightIndent, tabsArray, marginTabsArray, outputMedium
        int fontArrayIndex = ClassHierarchyUtils.FindInstanceVariableIndex("TextStyle", "fontArray", _bundle);
        int lineGridIndex = ClassHierarchyUtils.FindInstanceVariableIndex("TextStyle", "lineGrid", _bundle);
        int baselineIndex = ClassHierarchyUtils.FindInstanceVariableIndex("TextStyle", "baseline", _bundle);
        int alignmentIndex = ClassHierarchyUtils.FindInstanceVariableIndex("TextStyle", "alignment", _bundle);

        // Initialize critical TextStyle fields
        if (fontArrayIndex >= 0)
        {
            _allocator.SetInstanceVariable(textStyleOOP, fontArrayIndex, fontArrayOOP);
            _initializedFields.Add((textStyleOOP, fontArrayIndex));
        }

        if (lineGridIndex >= 0)
        {
            _allocator.SetInstanceVariable(textStyleOOP, lineGridIndex, SmalltalkOOP.FromSmallInteger(14)); // ascent + descent + 1
            _initializedFields.Add((textStyleOOP, lineGridIndex));
        }

        if (baselineIndex >= 0)
        {
            _allocator.SetInstanceVariable(textStyleOOP, baselineIndex, SmalltalkOOP.FromSmallInteger(10)); // matches ascent
            _initializedFields.Add((textStyleOOP, baselineIndex));
        }

        if (alignmentIndex >= 0)
        {
            _allocator.SetInstanceVariable(textStyleOOP, alignmentIndex, SmalltalkOOP.FromSmallInteger(0)); // 0 = left flush
            _initializedFields.Add((textStyleOOP, alignmentIndex));
        }

        // Initialize remaining TextStyle fields to nil
        int textStyleInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("TextStyle", _bundle);
        for (int i = 0; i < textStyleInstVarCount; i++)
        {
            if (!_initializedFields.Contains((textStyleOOP, i)))
            {
                _allocator.SetInstanceVariable(textStyleOOP, i, nilOOP);
                _initializedFields.Add((textStyleOOP, i));
            }
        }

        // Step 4: Find and update the DefaultTextStyle Association
        // The Association was created as a method literal during Phase 4 (method compilation)
        // and is now registered in AssociationRegistry
        var allAssociations = AssociationRegistry.GetAll();
        if (allAssociations.TryGetValue("DefaultTextStyle", out SmalltalkOOP defaultTextStyleAssoc))
        {
            // Update the Association's value (instance variable 1) to point to our TextStyle
            _allocator.SetInstanceVariable(defaultTextStyleAssoc, 1, textStyleOOP);
            _initializedFields.Add((defaultTextStyleAssoc, 1));
            Console.WriteLine($"  ✓ Created DefaultTextStyle (oop={textStyleOOP.RawValue}) with stub StrikeFont");
            Console.WriteLine($"    Updated existing Association (oop={defaultTextStyleAssoc.RawValue})");
        }
        else
        {
            // Association doesn't exist - this means no code references DefaultTextStyle
            // This is unexpected, so log a warning
            Console.WriteLine($"  ⚠ WARNING: DefaultTextStyle Association not found in AssociationRegistry");
            Console.WriteLine($"    This suggests no Smalltalk code references DefaultTextStyle");
            Console.WriteLine($"    Created TextStyle object anyway (oop={textStyleOOP.RawValue})");

            // Register in GlobalRegistry for manual access if needed
            GlobalRegistry.Register("DefaultTextStyle", textStyleOOP);
        }
    }

    /// <summary>
    /// Interns a string by creating a String object with UTF-8 encoded bytes.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        // Set String class reference
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);

        return stringOOP;
    }

    #endregion
}