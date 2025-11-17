using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;
using System.Text;
using System.Linq;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 3: Object Allocation and Initialization
///
/// Consolidates three original phases into one cohesive phase:
/// 1. Object Allocation (Phase 6) - Allocates sentinel objects and special objects
/// 2. Class Reference Initialization (Phase X) - Sets class slots (slot 0) for all objects
/// 3. Instance Variable Initialization (Phase Y) - Initializes unset instance variables to nil
///
/// This phase creates the fundamental objects needed by the Smalltalk system and ensures
/// all objects are properly initialized with class references and nil-initialized fields.
/// </summary>
public class Phase3_ObjectAllocation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly IReadOnlyList<string> _symbols;
    private readonly ColdstartReport _report;

    public Phase3_ObjectAllocation(
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
        _symbols = bundle.Symbols;
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void Run()
    {
// Console.WriteLine("Phase 3: Object Allocation and Initialization");

        // Section 1: Allocation (ALL object creation happens here)
        AllocateSentinelObjects();
        AllocateSpecialObjects();

        // Phase 3.5: Pre-create Associations for Undeclared Variables
        // CRITICAL: This runs BEFORE class pointer fix to ensure all Associations
        // are created before SetClassReferencesForAllObjects() runs.
        // This way, the class pointer fix happens in ONE pass for ALL Associations.
        PreCreateUndeclaredAssociations();

        // NOTE: DefaultTextStyle creation moved to Phase 5 (after AssociationRegistry is populated)

        LogAllocationCheckpoint();

        // Section 2: Class Reference Setting (ONE fix for ALL objects including Phase 3.5 Associations)
        SetClassReferencesForAllObjects();
        LogClassReferenceCheckpoint();

        // Section 3: Populate Behavior.subclasses field
        PopulateSubclassesField();

        // Section 4: Nil Initialization
        InitializeUnsetInstanceVariables();
        LogInitializationCheckpoint();

        // DEBUG: Check SystemDictionary tally at END of Phase 3
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        var tallyEnd = _allocator.GetInstanceVariable(systemDictOOP, 0);
        Console.WriteLine($"[Phase3 END] SystemDictionary tally: {tallyEnd}, IsSmallInteger={tallyEnd.IsSmallInteger}");

// Console.WriteLine("  Phase 3 complete: All objects allocated and initialized");
    }

    private void LogAllocationCheckpoint()
    {
        int objectCount = _localIdToObjectId.Count + 3;  // +3 for nil/true/false in GlobalRegistry
// Console.WriteLine($"  ✓ Allocation checkpoint: {objectCount} objects allocated");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.ObjectsAllocated = objectCount;
    }

    private void LogClassReferenceCheckpoint()
    {
        // Count all objects that now have class references
        // This includes classes, metaclasses, symbols, and allocated objects
        int classRefCount = _bundle.Classes.Length + SymbolRegistry.AllSymbols().Count() + _localIdToObjectId.Count;
// Console.WriteLine($"  ✓ Class reference checkpoint: {classRefCount} class references set");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.ClassReferencesSet = classRefCount;
    }

    private void LogInitializationCheckpoint()
    {
        // Initialize unset instance variables completes Phase 3
// Console.WriteLine($"  ✓ Initialization checkpoint: All unset fields initialized to nil");

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.FieldsInitialized = _initializedFields.Count;
    }

    #region Sentinel Object Allocation

    /// <summary>
    /// Allocates the fundamental sentinel objects: nil, true, false.
    /// These must be created AFTER all classes are fully wired so they get correct classIds.
    /// </summary>
    private void AllocateSentinelObjects()
    {
        // Create minimal dependency singletons: nil, true, false
        var nilOOP = _allocator.AllocateFixedObject("UndefinedObject");
        var trueOOP = _allocator.AllocateFixedObject("True");
        var falseOOP = _allocator.AllocateFixedObject("False");

        GlobalRegistry.Register("nil", nilOOP);
        GlobalRegistry.Register("true", trueOOP);
        GlobalRegistry.Register("false", falseOOP);

        Console.WriteLine($"  Allocated sentinel objects: nil={nilOOP}, true={trueOOP}, false={falseOOP}");

        // Initialize SystemDictionary slots with nil (created in Phase 2 but couldn't be initialized until now)
        Console.WriteLine($"  About to call InitializeSystemDictionaryWithNil with nilOOP={nilOOP}");
        
        // DEBUG: Check tally BEFORE InitializeSystemDictionaryWithNil
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        var tallyBefore = _allocator.GetInstanceVariable(systemDictOOP, 0);
        Console.WriteLine($"  SystemDictionary tally BEFORE init: {tallyBefore}");
        
        InitializeSystemDictionaryWithNil(nilOOP);
        
        // DEBUG: Check tally AFTER InitializeSystemDictionaryWithNil
        var tallyAfter = _allocator.GetInstanceVariable(systemDictOOP, 0);
        Console.WriteLine($"  SystemDictionary tally AFTER init: {tallyAfter}");
        Console.WriteLine($"  ✓ SystemDictionary initialized with nil");

        // Create ProcessorScheduler singleton
        var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");
        GlobalRegistry.Register("ProcessorScheduler", processorSchedulerOOP);

        // Initialize ProcessorScheduler fields that should NOT be nil
        InitializeProcessorSchedulerFields(processorSchedulerOOP, nilOOP);

        // Create Undeclared Dictionary (v1.0.7)
        // The Undeclared dictionary holds Association objects for variables that were referenced
        // but not yet defined during compilation. SystemDictionary>>at:put: will migrate these
        // Associations when the variables become defined.
        CreateUndeclaredDictionary(nilOOP);
        
        // DEBUG: Check tally after CreateUndeclaredDictionary
        var tallyAfterUndeclared = _allocator.GetInstanceVariable(systemDictOOP, 0);
        Console.WriteLine($"  SystemDictionary tally after CreateUndeclaredDictionary: {tallyAfterUndeclared}");
    }

    /// <summary>
    /// Initializes SystemDictionary indexed slots with nil.
    /// SystemDictionary was created in Phase 2 but couldn't be initialized until nil object exists.
    /// </summary>
    private void InitializeSystemDictionaryWithNil(SmalltalkOOP nilOOP)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);

        for (int i = 0; i < systemDictSize; i++)
        {
            _allocator.SetIndexedOOP(systemDictOOP, i, nilOOP);
        }
    }

    /// <summary>
    /// Initializes ProcessorScheduler instance variables with their proper initial values.
    /// </summary>
    private void InitializeProcessorSchedulerFields(SmalltalkOOP processorSchedulerOOP, SmalltalkOOP nilOOP)
    {
        // Get instance variable count for ProcessorScheduler
        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("ProcessorScheduler", _bundle);

        // Initialize all instance variables to nil
        // This prevents Phase Y from overwriting with nil (it only sets uninitialized fields)
        for (int i = 0; i < instVarCount; i++)
        {
            _allocator.SetInstanceVariable(processorSchedulerOOP, i, nilOOP);
            _initializedFields.Add((processorSchedulerOOP, i));
        }
    }

    /// <summary>
    /// Creates the Undeclared Dictionary and registers it as a global (v1.0.7).
    /// The Undeclared dictionary is initially empty - it will be populated later in Phase 3 (Section 5)
    /// with canonical Association objects for each variable found in bundle.Undeclared[].
    /// </summary>
    private void CreateUndeclaredDictionary(SmalltalkOOP nilOOP)
    {
        // Determine initial capacity based on bundle undeclared count
        int undeclaredCount = _bundle.Undeclared?.Length ?? 0;
        int initialCapacity = Math.Max(16, undeclaredCount * 2); // Double capacity for hash table efficiency

        // Allocate Dictionary with appropriate capacity
        var undeclaredOOP = _allocator.AllocateArray("Dictionary", initialCapacity);

        // Initialize all indexed slots to nil
        for (int i = 0; i < initialCapacity; i++)
        {
            _allocator.SetIndexedOOP(undeclaredOOP, i, nilOOP);
        }

        // Initialize Dictionary instance variables (tally = 0)
        int dictInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("Dictionary", _bundle);
        for (int i = 0; i < dictInstVarCount; i++)
        {
            _allocator.SetInstanceVariable(undeclaredOOP, i, nilOOP);
            _initializedFields.Add((undeclaredOOP, i));
        }

        // Set tally to 0 (will be incremented as Associations are added in Phase 5)
        int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
        if (tallyIndex >= 0)
        {
            var zeroOOP = SmalltalkOOP.FromSmallInteger(0);
            _allocator.SetInstanceVariable(undeclaredOOP, tallyIndex, zeroOOP);
        }

        // Register in GlobalRegistry
        GlobalRegistry.Register("Undeclared", undeclaredOOP);

        // Add to Smalltalk dictionary
        var undeclaredSymbol = InternSymbol("Undeclared");
        var undeclaredAssoc = AllocateAssociationWithSemanticAllocator(undeclaredSymbol, undeclaredOOP);
        InstallAssociationInSystemDictionary(undeclaredAssoc, "Undeclared");

        // CRITICAL: Register in AssociationRegistry so Phase 4 method literals can reference it
        // This ensures method literals that reference "Undeclared" get the correct Association (not a new one with nil value)
        AssociationRegistry.Register("Undeclared", undeclaredAssoc);

        Console.WriteLine($"  Created Undeclared dictionary (OOP={undeclaredOOP}, capacity={initialCapacity}, undeclared count={undeclaredCount})");
        Console.WriteLine($"  Undeclared Association: key={undeclaredSymbol}, value={undeclaredOOP}, assoc={undeclaredAssoc}");
    }

    /// <summary>
    /// Creates a minimal DefaultTextStyle object to support PopUpMenu and other UI features.
    /// DefaultTextStyle is referenced by TextStyle class>>default which returns 'DefaultTextStyle copy'.
    /// PopUpMenu initialization calls 'TextStyle default fontAt: 1', so we need at least one font.
    /// </summary>
    private void CreateDefaultTextStyle(SmalltalkOOP nilOOP)
    {
        // Step 1: Create a minimal stub StrikeFont
        // StrikeFont has many instance variables, but most can be nil for our stub purposes
        var strikeFontOOP = _allocator.AllocateFixedObject("StrikeFont");

        // Initialize all StrikeFont fields to nil
        int strikeFontInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("StrikeFont", _bundle);
        for (int i = 0; i < strikeFontInstVarCount; i++)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
            _initializedFields.Add((strikeFontOOP, i));
        }

        // Set minimal StrikeFont properties to prevent errors
        // name: "DefaultFont"
        int nameIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "name", _bundle);
        if (nameIndex >= 0)
        {
            var fontNameOOP = InternString("DefaultFont");
            _allocator.SetInstanceVariable(strikeFontOOP, nameIndex, fontNameOOP);
        }

        // ascent: 12
        int ascentIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "ascent", _bundle);
        if (ascentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, ascentIndex, SmalltalkOOP.FromSmallInteger(12));
        }

        // descent: 4
        int descentIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "descent", _bundle);
        if (descentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, descentIndex, SmalltalkOOP.FromSmallInteger(4));
        }

        // maxWidth: 8
        int maxWidthIndex = ClassHierarchyUtils.FindInstanceVariableIndex("StrikeFont", "maxWidth", _bundle);
        if (maxWidthIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, maxWidthIndex, SmalltalkOOP.FromSmallInteger(8));
        }

        // Step 2: Create fontArray with one stub font
        var fontArrayOOP = _allocator.AllocateArray("Array", 1);
        _allocator.SetIndexedOOP(fontArrayOOP, 0, strikeFontOOP);

        // Step 3: Create TextStyle object
        var textStyleOOP = _allocator.AllocateFixedObject("TextStyle");

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
            _allocator.SetInstanceVariable(textStyleOOP, lineGridIndex, SmalltalkOOP.FromSmallInteger(16));
            _initializedFields.Add((textStyleOOP, lineGridIndex));
        }

        if (baselineIndex >= 0)
        {
            _allocator.SetInstanceVariable(textStyleOOP, baselineIndex, SmalltalkOOP.FromSmallInteger(12));
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

        // Step 4: Register DefaultTextStyle globally
        // This makes it accessible via Smalltalk at: #DefaultTextStyle
        GlobalRegistry.Register("DefaultTextStyle", textStyleOOP);

        // Step 5: Find existing DefaultTextStyle Association and update its value
        // The compiler should have created an Association for DefaultTextStyle (either in
        // TextConstants pool or Undeclared), we just need to find it and set the value
        bool foundAndUpdated = FindAndUpdateAssociationValue("DefaultTextStyle", textStyleOOP);

        if (!foundAndUpdated)
        {
            // Association doesn't exist - create one and add to Undeclared
// Console.WriteLine($"  DefaultTextStyle Association not found in bundle, adding to Undeclared");
            var defaultTextStyleSymbol = InternSymbol("DefaultTextStyle");
            var defaultTextStyleAssoc = AllocateAssociationWithSemanticAllocator(defaultTextStyleSymbol, textStyleOOP);

            // Add to Undeclared dictionary (will be migrated to proper location by runtime)
            var undeclaredOOP = GlobalRegistry.Get("Undeclared");
            InstallAssociationInDictionary(undeclaredOOP, defaultTextStyleAssoc, "DefaultTextStyle");
        }

// Console.WriteLine($"  Created DefaultTextStyle with stub StrikeFont for PopUpMenu support");
    }

    #endregion

    #region Special Object Allocation

    /// <summary>
    /// Allocates special objects defined in the bundle.
    /// </summary>
    private void AllocateSpecialObjects()
    {
        int objectsAllocated = 0;

        // Create other bundle objects
        foreach (var obj in _bundle.Objects)
        {
            string className = _symbols[obj.ClassNameIndex];
            int fixedSize = obj.Fields.Length;
            int indexedSize = obj.IndexedFields.Length;

            SmalltalkOOP objectOOP = AllocateObjectWithSemanticAllocator(className, obj.FormatHint, fixedSize, indexedSize);
            _localIdToObjectId[obj.LocalId] = objectOOP;

            // Note: Field initialization moved to Step 4 (PhaseY logic) after nil exists

            // REMOVED OBSOLETE CODE: Pool dictionaries are no longer installed as Associations in SystemDictionary.
            // Pool variables are now resolved via 2-element Arrays (#PoolName, #Key) created in Phase 4.
            // Pool dictionaries themselves are stored in Class>>classPool instance variable.

            objectsAllocated++;
        }

// Console.WriteLine($"  Allocated {objectsAllocated} special objects from bundle");
    }

    /// <summary>
    /// Allocates an object using SemanticAllocator based on format hint.
    /// </summary>
    private SmalltalkOOP AllocateObjectWithSemanticAllocator(string className, string formatHint, int fixedSize, int indexedSize)
    {
        // Map format hint to appropriate SemanticAllocator method
        var objectOOP = formatHint.ToLower() switch
        {
            "pointers" or "pointer" or "fixed" => _allocator.AllocateFixedObject(className),
            "pointerindexed" => _allocator.AllocateArray(className, indexedSize),
            "bytes" or "byte" or "byteindexed" => _allocator.AllocateByteArray(className, indexedSize),
            "words" or "word" or "wordindexed" => _allocator.AllocateObject(className, ObjectFormat.WordIndexed, indexedSize),
            "compiledmethod" => _allocator.AllocateArray(className, indexedSize),
            _ => _allocator.AllocateFixedObject(className) // Default fallback
        };

        return objectOOP;
    }

    /// <summary>
    /// Allocates an Association object (key-value pair) using SemanticAllocator.
    /// </summary>
    private SmalltalkOOP AllocateAssociationWithSemanticAllocator(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");

        // Set instance variables: [0] = key, [1] = value (assuming standard Association layout)
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Installs an association in SystemDictionary using semantic slot finding.
    /// </summary>
    private void InstallAssociationInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Find first available nil slot
        for (int i = 0; i < systemDictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(systemDictOOP, i);
            if (slotOOP == nilOOP)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(systemDictOOP, i, associationOOP);
                return;
            }
        }

        throw new InvalidOperationException($"SystemDictionary is full - cannot install {name} association");
    }

    /// <summary>
    /// Installs an Association in any Dictionary object (including pool dictionaries).
    /// Similar to InstallAssociationInSystemDictionary but works with any dictionary OOP.
    /// </summary>
    private void InstallAssociationInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string name)
    {
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Find first available nil slot
        for (int i = 0; i < dictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(dictionaryOOP, i);
            if (slotOOP == nilOOP)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(dictionaryOOP, i, associationOOP);

                // Increment tally field (field 0 for Dictionary)
                int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
                if (tallyIndex >= 0)
                {
                    var currentTallyOOP = _allocator.GetInstanceVariable(dictionaryOOP, tallyIndex);
                    int currentTally = currentTallyOOP.IsSmallInteger ? currentTallyOOP.AsSmallInteger() : 0;
                    _allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(currentTally + 1));
                }

                return;
            }
        }

        throw new InvalidOperationException($"Dictionary is full - cannot install {name} association");
    }

    /// <summary>
    /// Finds an Association by key in all allocated objects and updates its value.
    /// Searches through all objects looking for Association instances with matching key.
    /// </summary>
    private bool FindAndUpdateAssociationValue(string keyName, SmalltalkOOP newValueOOP)
    {
        var keySymbol = InternSymbol(keyName);
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");

        // Search through all allocated objects (both bundle objects and sentinel objects)
        foreach (var kvp in _localIdToObjectId)
        {
            var objectOOP = kvp.Value;

            try
            {
                // Check if this is an Association object
                var classOOP = _allocator.GetSlot(objectOOP, 0);
                if (classOOP == associationClassOOP)
                {
                    // Get the key (instance variable 0)
                    var assocKeyOOP = _allocator.GetInstanceVariable(objectOOP, 0);

                    // Check if key matches
                    if (assocKeyOOP == keySymbol)
                    {
                        // Update the value (instance variable 1)
                        _allocator.SetInstanceVariable(objectOOP, 1, newValueOOP);
                        _initializedFields.Add((objectOOP, 1));
// Console.WriteLine($"  Updated existing {keyName} Association value to oop={newValueOOP.RawValue}");
                        return true;
                    }
                }
            }
            catch
            {
                // Skip objects that don't have the expected structure
                continue;
            }
        }

        return false;
    }

    #endregion

    #region Class Reference Initialization

    /// <summary>
    /// Sets class references (slot 0) for all allocated objects.
    /// This resolves the circular dependency where objects need class references but classes don't exist during allocation.
    /// </summary>
    private void SetClassReferencesForAllObjects()
    {
        int classReferencesSet = 0;

        // Step 1: Set class references for all class objects themselves
        // IMPORTANT: For class objects, there are TWO different "slot 0" concepts:
        //   - Header slot 0 (memory offset 0): The class reference (set here)
        //   - Instance variable 0 (memory offset 4): The superclass field (set in Phase 3)
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            if (cls.IsMetaclass)
            {
                // Metaclass objects point to the Metaclass class
                var metaclassClassOOP = GlobalRegistry.LookupClassOOP("Metaclass");
                _allocator.SetSlot(classOOP, 0, metaclassClassOOP);
            }
            else
            {
                // Instance class objects point to their metaclass
                string instanceClassName = GetClassNameFromLocalId(cls.LocalId);
                string metaclassLocalId = $"meta:{instanceClassName}";

                if (_localIdToObjectId.TryGetValue(metaclassLocalId, out SmalltalkOOP metaclassOOP))
                {
                    _allocator.SetSlot(classOOP, 0, metaclassOOP);

                    // Debug: Show first few and class:Point and class:MethodDictionary specifically
                    if (classReferencesSet < 5 || instanceClassName == "Point" || instanceClassName == "MethodDictionary")
                    {
// Console.WriteLine($"    Set {cls.LocalId}.class = {metaclassLocalId} (classOOP={classOOP}, metaclassOOP={metaclassOOP})");
                    }
                }
                else
                {
// Console.WriteLine($"WARNING: Could not find metaclass for {instanceClassName}");
                }
            }

            classReferencesSet++;
        }

        // Step 2: Set class references for singleton objects (nil, true, false)
        SetSingletonClassReference("nil", "UndefinedObject");
        SetSingletonClassReference("true", "True");
        SetSingletonClassReference("false", "False");
        classReferencesSet += 3;

        // Step 3: Set class references for all other objects created in phases
        foreach (var obj in _bundle.Objects)
        {
            if (!_localIdToObjectId.TryGetValue(obj.LocalId, out SmalltalkOOP objectOOP))
                continue;

            string className = _bundle.Symbols[obj.ClassNameIndex];
            var classOOP = GlobalRegistry.LookupClassOOP(className);
            _allocator.SetSlot(objectOOP, 0, classOOP);

            classReferencesSet++;
        }

        // Step 4: Set class references for symbols, strings, and other runtime objects
        SetRuntimeObjectClassReferences();

// Console.WriteLine($"  Set {classReferencesSet} class references");
    }

    private void SetSingletonClassReference(string globalName, string className)
    {
        if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
        {
            SmalltalkOOP classObjectId = GlobalRegistry.LookupClassOOP(className);
            var objectOOP = objectId;
            var classOOP = classObjectId;
            _allocator.SetSlot(objectOOP, 0, classOOP);
        }
    }

    private void SetRuntimeObjectClassReferences()
    {
        // Set class references for symbols (created during symbol interning)
        var allSymbols = SymbolRegistry.AllSymbols();
        var symbolClassOOP = GlobalRegistry.LookupClassOOP("Symbol");

        foreach (var (index, symbolOOP) in allSymbols)
        {
            _allocator.SetSlot(symbolOOP, 0, symbolClassOOP);
        }

        // Set class reference for SystemDictionary
        if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictId))
        {
            SmalltalkOOP systemDictClassId = GlobalRegistry.LookupClassOOP("SystemDictionary");
            var systemDictOOP = systemDictId;
            var systemDictClassOOP = systemDictClassId;
            _allocator.SetSlot(systemDictOOP, 0, systemDictClassOOP);
        }

        // Set class reference for USTable (Array)
        if (GlobalRegistry.TryGet("USTable", out SmalltalkOOP ustableId))
        {
            SmalltalkOOP arrayClassId = GlobalRegistry.LookupClassOOP("Array");
            var ustableOOP = ustableId;
            var arrayClassOOP = arrayClassId;
            _allocator.SetSlot(ustableOOP, 0, arrayClassOOP);
        }
        
        // CRITICAL FIX: Set class references for Associations created in Phase 3
        // These include:
        // - Undeclared Association (line 229)
        // - DefaultTextStyle Association (line 348)
        // - Pool dictionary Associations (line 386)
        SetAssociationClassReferences();
    }
    
    /// <summary>
    /// Sets class references for all Association objects allocated in Phase 3.
    /// Associations are created during sentinel object allocation and special object allocation,
    /// but their class pointers are not set until this method runs.
    /// IMPORTANT: This scans the ENTIRE heap, not just _localIdToObjectId, because some Associations
    /// are created at runtime (e.g., Undeclared Association at line 229).
    /// </summary>
    private void SetAssociationClassReferences()
    {
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        var dictionaryClassOOP = GlobalRegistry.LookupClassOOP("Dictionary");
        int associationsFixed = 0;
        int dictionariesFixed = 0;
        
        // Scan through ALL allocated objects in the heap (including runtime-allocated ones)
        foreach (var objectOOP in _heapManager.EnumerateObjectTable())
        {
            try
            {
                // Skip SmallIntegers
                if (objectOOP.IsSmallInteger)
                    continue;
                
                // Check if class pointer is unset (OOP(0) or nil)
                var currentClassOOP = _allocator.GetSlot(objectOOP, 0);
                
                // Skip if class pointer already set to a non-zero/non-nil value
                if (currentClassOOP != default(SmalltalkOOP) && currentClassOOP != SpecialObjectsArray.Instance.Nil)
                    continue;
                
                // Check object format to determine class
                var format = _allocator.GetObjectFormat(objectOOP);
                int instVarCount = _allocator.GetInstanceVariableCount(objectOOP);
                int indexedSize = _allocator.GetIndexedSize(objectOOP);
                
                // Association: 2 instance vars, Pointer format, no indexed data
                if (format == ObjectFormat.Pointer && instVarCount == 2 && indexedSize == 0)
                {
                    _allocator.SetSlot(objectOOP, 0, associationClassOOP);
                    associationsFixed++;
                }
                // Dictionary: 1 instance var (tally), PointerIndexed format, indexed data
                else if (format == ObjectFormat.PointerIndexed && instVarCount == 1 && indexedSize > 0)
                {
                    _allocator.SetSlot(objectOOP, 0, dictionaryClassOOP);
                    dictionariesFixed++;
                }
            }
            catch
            {
                // Skip objects that can't be inspected
                continue;
            }
        }
        
        Console.WriteLine($"  Fixed {associationsFixed} Association + {dictionariesFixed} Dictionary class references");
    }

    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }

    #endregion

    #region Instance Variable Initialization

    /// <summary>
    /// Initializes all unset instance variables to nil.
    /// This is the final initialization pass of the multi-pass architecture.
    /// </summary>
    private void InitializeUnsetInstanceVariables()
    {
        int instanceVariablesInitialized = 0;
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Step 1: Initialize class object instance variables
        foreach (var cls in _bundle.Classes)
        {
            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            // Get the number of instance variables for this type of class object
            string classOfClassName = cls.IsMetaclass ? "Metaclass" : "Class";
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(classOfClassName, _bundle);

            // Initialize ONLY uninitialized instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((classOOP, i)))
                {
                    _allocator.SetInstanceVariable(classOOP, i, nilOOP);
                    instanceVariablesInitialized++;

                    // Verify Object.superclass is being set to nil
                    if (!cls.IsMetaclass && _bundle.Symbols[cls.NameIndex] == "Object")
                    {
                        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);
                        if (i == superclassIndex)
                        {
// Console.WriteLine($"    Initialized class:Object.superclass = nil (field index {i})");
                        }
                    }
                }
            }
        }

        // Step 2: Initialize instance variables for singleton objects
        InitializeSingletonInstanceVariables("nil", "UndefinedObject", nilOOP);
        InitializeSingletonInstanceVariables("true", "True", nilOOP);
        InitializeSingletonInstanceVariables("false", "False", nilOOP);
        InitializeSingletonInstanceVariables("ProcessorScheduler", "ProcessorScheduler", nilOOP);

        // Step 3: Initialize instance variables for other allocated objects and set JSON values
        foreach (var obj in _bundle.Objects)
        {
            if (!_localIdToObjectId.TryGetValue(obj.LocalId, out SmalltalkOOP objectOOP))
                continue;

            string className = _bundle.Symbols[obj.ClassNameIndex];
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);

            // First: Initialize ALL instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                instanceVariablesInitialized++;
            }

            // Second: Override with JSON field values
            for (int i = 0; i < obj.Fields.Length; i++)
            {
                var valueOOP = ResolveFieldValue(obj.Fields[i]);
                _allocator.SetInstanceVariable(objectOOP, i, valueOOP);
            }

            // Third: Set indexed fields if any
            for (int i = 0; i < obj.IndexedFields.Length; i++)
            {
                var valueOOP = ResolveFieldValue(obj.IndexedFields[i]);
                _allocator.SetIndexedOOP(objectOOP, i, valueOOP);
            }
        }

        // Step 4: Initialize SystemDictionary and other special objects
        InitializeSpecialObjectInstanceVariables(nilOOP);

        // Step 5: Initialize Dictionary indexed slots to nil
        InitializeDictionaryIndexedSlots(nilOOP);

// Console.WriteLine($"  Initialized {instanceVariablesInitialized} instance variables to nil");
    }

    private void InitializeSingletonInstanceVariables(string globalName, string className, SmalltalkOOP nilOOP)
    {
        if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(className, _bundle);
            var objectOOP = objectId;

            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((objectOOP, i)))
                {
                    _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                }
            }
        }
    }

    private void InitializeSpecialObjectInstanceVariables(SmalltalkOOP nilOOP)
    {
        // Initialize SystemDictionary instance variables
        if (GlobalRegistry.TryGet("Smalltalk", out SmalltalkOOP systemDictId))
        {
            int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("SystemDictionary", _bundle);
            var systemDictOOP = systemDictId;

            // Only initialize uninitialized instance variables
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((systemDictOOP, i)))
                {
                    _allocator.SetInstanceVariable(systemDictOOP, i, nilOOP);
                }
            }
        }

        // Note: USTable (Array) has 0 instance variables, so no initialization needed
        // Symbols also have 0 instance variables in the standard Smalltalk-80 implementation
    }

    /// <summary>
    /// Initializes Dictionary indexed slots to nil.
    /// </summary>
    private void InitializeDictionaryIndexedSlots(SmalltalkOOP nilOOP)
    {
        // Get Dictionary class OOP to identify which objects are Dictionaries
        if (!GlobalRegistry.TryGet("class:Dictionary", out SmalltalkOOP dictClassOOP))
            return;

        // Iterate through all class objects and initialize their classPool dictionaries
        foreach (var cls in _bundle.Classes)
        {
            if (cls.IsMetaclass)
                continue;

            if (!_localIdToObjectId.TryGetValue(cls.LocalId, out SmalltalkOOP classOOP))
                continue;

            // Find classPool instance variable
            string templateClassName = "Class";
            int classPoolIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "classPool", _bundle);
            if (classPoolIndex < 0)
                continue;

            // Get the classPool Dictionary OOP
            var classPoolOOP = _allocator.GetInstanceVariable(classOOP, classPoolIndex);
            if (classPoolOOP == SmalltalkOOP.Null)
                continue;

            // Check if this object is a Dictionary by checking its class OOP
            var objectClassOOP = _heapManager.GetClassOOP(classPoolOOP);
            if (objectClassOOP != dictClassOOP)
                continue;

            // Initialize all indexed slots to nil
            int indexedSize = _allocator.GetIndexedSize(classPoolOOP);
            for (int i = 0; i < indexedSize; i++)
            {
                _allocator.SetIndexedOOP(classPoolOOP, i, nilOOP);
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Resolves a field value from the discriminated union to an OOP.
    /// </summary>
    private SmalltalkOOP ResolveFieldValue(FieldValue value)
    {
        return value.Type switch
        {
            "nil" => SpecialObjectsArray.Instance.Nil,
            "true" => SpecialObjectsArray.Instance.True,
            "false" => SpecialObjectsArray.Instance.False,
            "smallInt" => SmalltalkOOP.FromSmallInteger(GetIntValue(value.Value!)),
            "largePositiveInteger" => throw new NotImplementedException("LargePositiveInteger not yet supported"),
            "largeNegativeInteger" => throw new NotImplementedException("LargeNegativeInteger not yet supported"),
            "float" => AllocateFloat(GetFloatValue(value.Value!)),
            "symbolRef" => InternSymbol(_symbols[value.Index!.Value]),
            "stringRef" => InternString(_bundle.Strings[value.Index!.Value]),
            "objectRef" => _localIdToObjectId.TryGetValue(value.LocalId!, out SmalltalkOOP objOOP) ? objOOP : SpecialObjectsArray.Instance.Nil,
            "character" => AllocateCharacter(value.CodePoint!.Value),
            "bytes" => AllocateByteArray(value.Encoding!, value.Data!),
            _ => throw new InvalidOperationException($"Unknown field type: {value.Type}")
        };
    }

    /// <summary>
    /// Extracts an integer value from a JsonElement or int.
    /// </summary>
    private int GetIntValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement e => e.GetInt32(),
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
            System.Text.Json.JsonElement e => e.GetDouble(),
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

        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// </summary>
    private SmalltalkOOP InternString(string value)
    {
        // Encode string as UTF-8 bytes
        byte[] stringBytes = Encoding.UTF8.GetBytes(value);

        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _allocator.AllocateString("String", stringBytes.Length);

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        // Set String class reference (since we're after class reference init)
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _allocator.SetObjectClass(stringOOP, stringClassOOP);

        return stringOOP;
    }

    /// <summary>
    /// Allocates a Character object.
    /// </summary>
    private SmalltalkOOP AllocateCharacter(int codePoint)
    {
        var characterOOP = _allocator.AllocateFixedObject("Character");

        // Store the code point in the Character object (assuming it's stored in first instance variable)
        var codePointOOP = SmalltalkOOP.FromSmallInteger(codePoint);
        _allocator.SetInstanceVariable(characterOOP, 0, codePointOOP);

        // Set Character class reference
        var characterClassOOP = GlobalRegistry.LookupClassOOP("Character");
        _allocator.SetObjectClass(characterOOP, characterClassOOP);

        return characterOOP;
    }

    /// <summary>
    /// Allocates a ByteArray object.
    /// </summary>
    private SmalltalkOOP AllocateByteArray(string encoding, string data)
    {
        // Decode the data based on encoding
        byte[] bytes = encoding.ToLower() switch
        {
            "base64" => Convert.FromBase64String(data),
            "hex" => ConvertHexStringToBytes(data),
            _ => throw new InvalidOperationException($"Unsupported byte array encoding: {encoding}")
        };

        var byteArrayOOP = _allocator.AllocateByteArray("ByteArray", bytes.Length);

        // Store the bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _allocator.SetIndexedByte(byteArrayOOP, i, bytes[i]);
        }

        // Set ByteArray class reference
        var byteArrayClassOOP = GlobalRegistry.LookupClassOOP("ByteArray");
        _allocator.SetObjectClass(byteArrayOOP, byteArrayClassOOP);

        return byteArrayOOP;
    }

    /// <summary>
    /// Allocates a Float object using SemanticAllocator.
    /// Float must be WordIndexed with 4 words (16-bit each) per Blue Book specification.
    /// </summary>
    private SmalltalkOOP AllocateFloat(double value)
    {
        Console.WriteLine($"[Phase3.AllocateFloat] Creating Float value={value}");

        // Use SemanticAllocator's AllocateFloat which correctly creates WordIndexed Float with 4 words
        var floatOOP = _allocator.AllocateFloat(value);

        Console.WriteLine($"[Phase3.AllocateFloat] Created floatOOP={floatOOP}, format={_heapManager.GetObjectFormat(floatOOP)}, size={_heapManager.GetIndexedSize(floatOOP)}");

        // Set Float class reference
        var floatClassOOP = GlobalRegistry.LookupClassOOP("Float");
        _allocator.SetObjectClass(floatOOP, floatClassOOP);

        return floatOOP;
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

    #endregion

    #region Behavior.subclasses Field Population

    /// <summary>
    /// Populates Behavior.subclasses field (slot 3) with Set containing class OOP references
    /// for all direct subclasses of each class. This enables runtime reflection and supports
    /// class hierarchy queries.
    ///
    /// Behavior field layout:
    ///   superclass(0), methodDict(1), format(2), subclasses(3)
    ///
    /// Blue Book specification:
    ///   subclasses is a Set containing direct subclass objects (not strings, not names)
    ///   Populated by addSubclass: method at runtime, but we build it during cold-start
    ///
    /// This runs in Phase 3 after nil singleton is created (Section 1) so we can set nil
    /// for classes without subclasses.
    /// </summary>
    private void PopulateSubclassesField()
    {
// Console.WriteLine("  Populating Behavior.subclasses fields...");

        int setCount = 0;
        int totalSubclassRefs = 0;

        // Get Set class OOP for allocating subclass Sets
        if (!_localIdToObjectId.TryGetValue("class:Set", out SmalltalkOOP setClassOOP))
        {
            throw new InvalidOperationException("Set class not found - needed for subclasses field");
        }

        // Get nil singleton (created in Section 1: AllocateSentinelObjects)
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Build subclass map: className -> list of direct subclass OOPs
        var subclassMap = new Dictionary<string, List<SmalltalkOOP>>();

        // Initialize map with empty lists for all classes
        foreach (var cls in _bundle.Classes)
        {
            string className = _symbols[cls.NameIndex];
            subclassMap[className] = new List<SmalltalkOOP>();
        }

        // Populate map by examining each class's superclass
        // IMPORTANT: Only process regular classes, NOT metaclasses
        // Metaclasses should not appear in the subclasses Set
        foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
        {
            // Skip if class has no superclass (e.g., Object in some hierarchies)
            if (cls.SuperNameIndex == null)
            {
                continue;
            }

            string superclassName = _symbols[cls.SuperNameIndex.Value];
            string thisClassName = cls.LocalId; // Use LocalId for OOP lookup

            // Add this class OOP to its superclass's subclass list
            if (!_localIdToObjectId.TryGetValue(thisClassName, out SmalltalkOOP classOOP))
            {
                throw new InvalidOperationException($"Class OOP not found for {thisClassName}");
            }

            subclassMap[superclassName].Add(classOOP);
            totalSubclassRefs++;
        }

        // Now populate slot 3 for each class with its Set of subclasses
        foreach (var cls in _bundle.Classes)
        {
            string className = _symbols[cls.NameIndex];
            SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

            var subclasses = subclassMap[className];

            if (subclasses.Count == 0)
            {
                // No subclasses - set subclasses to nil (not empty Set)
                // This matches Blue Book behavior: subclasses is nil until first subclass added
                SetAndTrackInstanceVariable(classOOP, 3, nilOOP);
            }
            else
            {
                // Allocate Set object to hold subclass OOPs
                // Set is PointerIndexed with 1 instance variable (tally) + indexed slots for elements
                // For simplicity during cold-start, we'll use a direct Array-based representation
                // Real Set uses open-addressing hash table, but we'll use a simple Array for now
                var setOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 1, subclasses.Count);
                _heapManager.SetSlot(setOOP, 0, setClassOOP);  // Set class reference

                // Set tally (instance variable 0) to SmallInteger with subclass count
                var tallyOOP = SmalltalkOOP.FromSmallInteger(subclasses.Count);
                _heapManager.SetInstanceVariable(setOOP, 0, tallyOOP);

                // Populate indexed slots with subclass OOPs
                for (int i = 0; i < subclasses.Count; i++)
                {
                    _heapManager.SetIndexedOOP(setOOP, i, subclasses[i]);
                }

                // Set Behavior.subclasses field (slot 3)
                SetAndTrackInstanceVariable(classOOP, 3, setOOP);
                setCount++;
            }
        }

// Console.WriteLine($"    Populated subclasses for {setCount} classes with children");
// Console.WriteLine($"    Total subclass references: {totalSubclassRefs}");
        _report.AddValidation("Phase 3", "Behavior.subclasses populated", true);
    }

    /// <summary>
    /// Helper method to set an instance variable and track it as initialized.
    /// This prevents Phase Y (InitializeUnsetInstanceVariables) from overwriting with nil.
    /// </summary>
    private void SetAndTrackInstanceVariable(SmalltalkOOP oop, int slot, SmalltalkOOP value)
    {
        _allocator.SetInstanceVariable(oop, slot, value);
        _initializedFields.Add((oop, slot));
    }

    #endregion

    #region Pre-create Undeclared Associations (Phase 3.5)

    /// <summary>
    /// Pre-creates canonical Association objects for undeclared variables (Phase 3.5).
    /// CRITICAL: This runs BEFORE Phase 4 (method compilation).
    ///
    /// Architecture:
    /// 1. Creates canonical Associations via AssociationRegistry for each undeclared variable
    /// 2. Does NOT add them to Undeclared dictionary yet
    /// 3. Phase 4 will use AssociationRegistry.GetOrCreate() to get the SAME Associations for method literals
    /// 4. Phase 5 will use AssociationRegistry.GetOrCreate() to get the SAME Associations for global installation
    ///
    /// This ensures ONE Association object per global variable name, enabling the
    /// Undeclared → SystemDictionary migration pattern specified in the Blue Book.
    /// </summary>
    private void PreCreateUndeclaredAssociations()
    {
        if (_bundle.Undeclared == null || _bundle.Undeclared.Length == 0)
        {
            Console.WriteLine("[Phase3.5] No undeclared variables in bundle - skipping Association pre-creation");
            return;
        }

        Console.WriteLine($"[Phase3.5] Pre-creating canonical Associations for undeclared variables...");

        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int createdCount = 0;
        int skippedClasses = 0;
        int skippedSpecialGlobals = 0;

        // Build set of class names to exclude (these will be installed in SystemDictionary by Phase 5)
        var classNames = new HashSet<string>(_bundle.Classes.Select(c => _symbols[c.NameIndex]));

        // Special globals that are created explicitly in Phase 3 (with correct values)
        // Skip these to avoid creating duplicate Associations with nil values
        var specialGlobals = new HashSet<string> { "Undeclared", "Smalltalk" };

        foreach (var undeclaredVar in _bundle.Undeclared)
        {
            // CRITICAL: Skip class names - they will be installed directly in SystemDictionary by Phase 5
            // We only pre-create Associations for non-class undeclared variables (like pool dictionaries)
            if (classNames.Contains(undeclaredVar.Key))
            {
                skippedClasses++;
                continue;
            }

            // CRITICAL: Skip special globals that are created explicitly in Phase 3 with correct values
            // Creating them here would create duplicate Associations with nil values
            if (specialGlobals.Contains(undeclaredVar.Key))
            {
                skippedSpecialGlobals++;
                continue;
            }

            // Get symbol OOP for the key
            SmalltalkOOP keySymbolOOP = InternSymbol(undeclaredVar.Key);

            // Use AssociationRegistry to create canonical Association
            // This ensures Phase 4 and Phase 5 will get the SAME Association object
            AssociationRegistry.GetOrCreate(undeclaredVar.Key, () =>
            {
                // Create new Association with key and nil value
                return AllocateAssociationWithSemanticAllocator(keySymbolOOP, nilOOP);
            });

            createdCount++;
        }

        Console.WriteLine($"  ✓ Pre-created {createdCount} canonical Associations");
        Console.WriteLine($"  ✓ Skipped {skippedClasses} class names (will be installed directly in SystemDictionary)");
        Console.WriteLine($"  ✓ Skipped {skippedSpecialGlobals} special globals (created explicitly in Phase 3)");
        Console.WriteLine($"  ✓ AssociationRegistry now contains {AssociationRegistry.Count} canonical Associations");

        // Show sample
        if (createdCount > 0)
        {
            var actuallyCreated = _bundle.Undeclared
                .Where(u => !classNames.Contains(u.Key) && !specialGlobals.Contains(u.Key))
                .Take(5);
            Console.WriteLine($"  Sample pre-created undeclared Associations:");
            foreach (var uv in actuallyCreated)
            {
                Console.WriteLine($"    - {uv.Key} ({uv.References.Length} reference(s))");
            }
        }
    }

    #endregion
}
