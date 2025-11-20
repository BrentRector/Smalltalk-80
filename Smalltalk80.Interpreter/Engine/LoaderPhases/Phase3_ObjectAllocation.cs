using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Engine.LoaderPhases.Phase3;
using Smalltalk80.Interpreter.Engine.Services;
using System.Diagnostics;

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
public class Phase3_ObjectAllocation : IPhase
{
    private readonly ColdStartBundle _bundle;
    private readonly ObjectGraphAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly LayoutResolver _layoutResolver;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly IReadOnlyList<string> _symbols;
    private readonly ColdstartReport _report;
    private readonly IDiagnosticHub? _diagnostics;

    // Helper classes for decomposed functionality
    private readonly DictionaryInstaller _dictionaryInstaller;
    private readonly FieldValueResolver _fieldValueResolver;

    /// <summary>
    /// Gets the phase name for diagnostics and logging.
    /// </summary>
    public string Name => "Phase 3: Object Allocation and Initialization";

    /// <summary>
    /// Gets the phase number (1-6).
    /// </summary>
    public int PhaseNumber => 3;

    public Phase3_ObjectAllocation(
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
        _layoutResolver = layoutResolver;
        _localIdToObjectId = localIdToObjectId;
        _initializedFields = initializedFields;
        _symbols = bundle.Symbols;
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _diagnostics = diagnostics;

        // Initialize services and helper classes
        var symbolService = new SymbolService(bundle);
        var fieldValueService = new FieldValueService(bundle, symbolService);
        var dictionaryService = new DictionaryService(allocator, bundle);

        _fieldValueResolver = new FieldValueResolver(allocator, bundle, localIdToObjectId, diagnostics, fieldValueService);
        _dictionaryInstaller = new DictionaryInstaller(
            localIdToObjectId, initializedFields,
            _fieldValueResolver.InternSymbol, dictionaryService);
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

    public void Run()
    {
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
    }

    private void LogAllocationCheckpoint()
    {
        int objectCount = _localIdToObjectId.Count + 3;  // +3 for nil/true/false in GlobalRegistry

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

        // Record Phase3 metrics
        if (_report.Metrics.Phase3Metrics == null)
        {
            _report.Metrics.Phase3Metrics = new ColdstartReport.Phase3Metrics();
        }
        _report.Metrics.Phase3Metrics.ClassReferencesSet = classRefCount;
    }

    private void LogInitializationCheckpoint()
    {
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

        // Initialize SystemDictionary slots with nil (created in Phase 2 but couldn't be initialized until now)
        InitializeSystemDictionaryWithNil(nilOOP);

        // Create ProcessorScheduler singleton
        var processorSchedulerOOP = _allocator.AllocateFixedObject("ProcessorScheduler");

        // CRITICAL: Set the class reference - AllocateFixedObject doesn't set it!
        var processorSchedulerClassOOP = GlobalRegistry.LookupClassOOP("ProcessorScheduler");
        Console.WriteLine($"[Phase3] ProcessorScheduler CLASS OOP = {processorSchedulerClassOOP}");
        _allocator.SetObjectClass(processorSchedulerOOP, processorSchedulerClassOOP);

        // Verify class was set
        var verifyClass = _allocator.GetClassOOP(processorSchedulerOOP);
        Console.WriteLine($"[Phase3] After SetObjectClass, class = {verifyClass}");

        Console.WriteLine($"[Phase3] Created ProcessorScheduler singleton: {processorSchedulerOOP}");
        GlobalRegistry.Register("ProcessorScheduler", processorSchedulerOOP);

        // Initialize ProcessorScheduler fields that should NOT be nil
        InitializeProcessorSchedulerFields(processorSchedulerOOP, nilOOP);

        // Create Undeclared Dictionary (v1.0.7)
        // The Undeclared dictionary holds Association objects for variables that were referenced
        // but not yet defined during compilation. SystemDictionary>>at:put: will migrate these
        // Associations when the variables become defined.
        CreateUndeclaredDictionary(nilOOP);
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
    /// ProcessorScheduler has: quiescentProcessLists (Array of 8 LinkedLists), activeProcess (Process)
    /// </summary>
    private void InitializeProcessorSchedulerFields(SmalltalkOOP processorSchedulerOOP, SmalltalkOOP nilOOP)
    {
        // 1. Create Array of 8 LinkedLists for quiescentProcessLists (one per priority level)
        var linkedListClassOOP = GlobalRegistry.LookupClassOOP("LinkedList");
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        var arrayOOP = _allocator.AllocateArray(arrayClassOOP, 8);

        for (int i = 0; i < 8; i++)
        {
            var linkedListOOP = _allocator.AllocateFixedObject("LinkedList");
            _allocator.SetObjectClass(linkedListOOP, linkedListClassOOP);
            // LinkedList has: firstLink (index 0), lastLink (index 1)
            _allocator.SetInstanceVariable(linkedListOOP, 0, nilOOP); // firstLink = nil
            _allocator.SetInstanceVariable(linkedListOOP, 1, nilOOP); // lastLink = nil
            _allocator.SetIndexedOOP(arrayOOP, i, linkedListOOP);
        }

        // Set quiescentProcessLists (index 0)
        _allocator.SetInstanceVariable(processorSchedulerOOP, 0, arrayOOP);
        _initializedFields.Add((processorSchedulerOOP, 0));
        Console.WriteLine($"[Phase3] Set quiescentProcessLists = {arrayOOP}");

        // 2. Create initial/main Process for activeProcess
        // Process has: suspendedContext (index 0), priority (index 1), myList (index 2)
        var processClassOOP = GlobalRegistry.LookupClassOOP("Process");
        var processOOP = _allocator.AllocateFixedObject("Process");
        _allocator.SetObjectClass(processOOP, processClassOOP);
        _allocator.SetInstanceVariable(processOOP, 0, nilOOP);  // suspendedContext = nil (main process isn't suspended)
        _allocator.SetInstanceVariable(processOOP, 1, SmalltalkOOP.FromSmallInteger(4));  // priority = 4 (UserSchedulingPriority)
        _allocator.SetInstanceVariable(processOOP, 2, nilOOP);  // myList = nil (active process not on any list)

        // Set activeProcess (index 1)
        _allocator.SetInstanceVariable(processorSchedulerOOP, 1, processOOP);
        _initializedFields.Add((processorSchedulerOOP, 1));
        Console.WriteLine($"[Phase3] Set activeProcess = {processOOP}");

        // Verify fields were set
        var verifyField0 = _allocator.GetInstanceVariable(processorSchedulerOOP, 0);
        var verifyField1 = _allocator.GetInstanceVariable(processorSchedulerOOP, 1);
        Console.WriteLine($"[Phase3] Verify: quiescentProcessLists = {verifyField0}, activeProcess = {verifyField1}");
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
        int dictInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("Dictionary", _bundle);
        for (int i = 0; i < dictInstVarCount; i++)
        {
            _allocator.SetInstanceVariable(undeclaredOOP, i, nilOOP);
            _initializedFields.Add((undeclaredOOP, i));
        }

        // Set tally to 0 (will be incremented as Associations are added in Phase 5)
        int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
        if (tallyIndex >= 0)
        {
            var zeroOOP = SmalltalkOOP.FromSmallInteger(0);
            _allocator.SetInstanceVariable(undeclaredOOP, tallyIndex, zeroOOP);
        }

        // Register in GlobalRegistry (Association will be created in Phase 3.5)
        GlobalRegistry.Register("Undeclared", undeclaredOOP);

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Created Undeclared dictionary (OOP={undeclaredOOP}, capacity={initialCapacity}, undeclared count={undeclaredCount})",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = "Registered in GlobalRegistry - Association will be created in Phase 3.5",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
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
        int strikeFontInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("StrikeFont", _bundle);
        for (int i = 0; i < strikeFontInstVarCount; i++)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
            _initializedFields.Add((strikeFontOOP, i));
        }

        // Set minimal StrikeFont properties to prevent errors
        // name: "DefaultFont"
        int nameIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("StrikeFont", "name", _bundle);
        if (nameIndex >= 0)
        {
            var fontNameOOP = _fieldValueResolver.InternString("DefaultFont");
            _allocator.SetInstanceVariable(strikeFontOOP, nameIndex, fontNameOOP);
        }

        // ascent: 12
        int ascentIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("StrikeFont", "ascent", _bundle);
        if (ascentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, ascentIndex, SmalltalkOOP.FromSmallInteger(12));
        }

        // descent: 4
        int descentIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("StrikeFont", "descent", _bundle);
        if (descentIndex >= 0)
        {
            _allocator.SetInstanceVariable(strikeFontOOP, descentIndex, SmalltalkOOP.FromSmallInteger(4));
        }

        // maxWidth: 8
        int maxWidthIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("StrikeFont", "maxWidth", _bundle);
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
        int fontArrayIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("TextStyle", "fontArray", _bundle);
        int lineGridIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("TextStyle", "lineGrid", _bundle);
        int baselineIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("TextStyle", "baseline", _bundle);
        int alignmentIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("TextStyle", "alignment", _bundle);

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
        int textStyleInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("TextStyle", _bundle);
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
        bool foundAndUpdated = _dictionaryInstaller.FindAndUpdateAssociationValue("DefaultTextStyle", textStyleOOP);

        if (!foundAndUpdated)
        {
            // Association doesn't exist - create one and add to Undeclared
            var defaultTextStyleSymbol = _fieldValueResolver.InternSymbol("DefaultTextStyle");
            var defaultTextStyleAssoc = _dictionaryInstaller.AllocateAssociation(defaultTextStyleSymbol, textStyleOOP);

            // Add to Undeclared dictionary (will be migrated to proper location by runtime)
            var undeclaredOOP = GlobalRegistry.Get("Undeclared");
            _dictionaryInstaller.InstallInDictionary(undeclaredOOP, defaultTextStyleAssoc, "DefaultTextStyle");
        }
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
    }

    /// <summary>
    /// Allocates an object using ObjectGraphAllocator based on format hint.
    /// </summary>
    private SmalltalkOOP AllocateObjectWithSemanticAllocator(string className, string formatHint, int fixedSize, int indexedSize)
    {
        // Map format hint to appropriate ObjectGraphAllocator method
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
                    // DEBUG: Check if this is ProcessorScheduler being incorrectly treated as Association
                    if (GlobalRegistry.TryGet("ProcessorScheduler", out var processorOOP) && objectOOP == processorOOP)
                    {
                        Console.WriteLine($"[Phase3] WARNING: About to overwrite ProcessorScheduler class!");
                        Console.WriteLine($"  objectOOP = {objectOOP}");
                        Console.WriteLine($"  currentClassOOP = {currentClassOOP}");
                        Console.WriteLine($"  default(SmalltalkOOP) = {default(SmalltalkOOP)}");
                        Console.WriteLine($"  nil = {SpecialObjectsArray.Instance.Nil}");
                    }
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

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Fixed {associationsFixed} Association + {dictionariesFixed} Dictionary class references",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
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
            int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(classOfClassName, _bundle);

            // Initialize ONLY uninitialized instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                if (!_initializedFields.Contains((classOOP, i)))
                {
                    _allocator.SetInstanceVariable(classOOP, i, nilOOP);
                    instanceVariablesInitialized++;
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
            int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(className, _bundle);

            // First: Initialize ALL instance variables to nil
            for (int i = 0; i < instVarCount; i++)
            {
                _allocator.SetInstanceVariable(objectOOP, i, nilOOP);
                instanceVariablesInitialized++;
            }

            // Second: Override with JSON field values
            for (int i = 0; i < obj.Fields.Length; i++)
            {
                var valueOOP = _fieldValueResolver.ResolveFieldValue(obj.Fields[i]);
                _allocator.SetInstanceVariable(objectOOP, i, valueOOP);
            }

            // Third: Set indexed fields if any
            for (int i = 0; i < obj.IndexedFields.Length; i++)
            {
                var valueOOP = _fieldValueResolver.ResolveFieldValue(obj.IndexedFields[i]);
                _allocator.SetIndexedOOP(objectOOP, i, valueOOP);
            }
        }

        // Step 4: Initialize SystemDictionary and other special objects
        InitializeSpecialObjectInstanceVariables(nilOOP);

        // Step 5: Initialize Dictionary indexed slots to nil
        InitializeDictionaryIndexedSlots(nilOOP);
    }

    private void InitializeSingletonInstanceVariables(string globalName, string className, SmalltalkOOP nilOOP)
    {
        if (GlobalRegistry.TryGet(globalName, out SmalltalkOOP objectId))
        {
            int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(className, _bundle);
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
            int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("SystemDictionary", _bundle);
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
            int classPoolIndex = ClassHierarchyTraversal.FindInstanceVariableIndex(templateClassName, "classPool", _bundle);
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
            _diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "Phase3",
                Message = "No undeclared variables in bundle - skipping Association pre-creation",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
            return;
        }

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = "Pre-creating canonical Associations for undeclared variables...",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int createdCount = 0;
        int classNameCount = 0;
        int withPreCreatedValues = 0;

        // Build set of class names for reporting (we CREATE Associations for all, including classes)
        var classNames = new HashSet<string>(_bundle.Classes.Select(c => _symbols[c.NameIndex]));

        foreach (var undeclaredVar in _bundle.Undeclared)
        {
            // CRITICAL: Create Associations for ALL undeclared variables (including class names!)
            // Phase 4 method literals MUST reference the SAME Associations that Phase 5 installs in SystemDictionary.
            // If we skip classes here, Phase 4 creates Association #1 (value=nil) and Phase 5 creates Association #2 (value=class),
            // causing method bytecode to reference the wrong Association.

            // Track class names for reporting
            if (classNames.Contains(undeclaredVar.Key))
            {
                classNameCount++;
            }

            // Get symbol OOP for the key
            SmalltalkOOP keySymbolOOP = _fieldValueResolver.InternSymbol(undeclaredVar.Key);

            // Use AssociationRegistry to create canonical Association
            // This ensures Phase 4 and Phase 5 will get the SAME Association object
            var resultAssocOOP = AssociationRegistry.GetOrCreate(undeclaredVar.Key, () =>
            {
                // Check if this global has a pre-created object in GlobalRegistry
                // (e.g., Undeclared, Smalltalk, ProcessorScheduler created in Phase 3)
                SmalltalkOOP valueOOP;
                if (GlobalRegistry.TryGet(undeclaredVar.Key, out var preCreatedValue))
                {
                    valueOOP = preCreatedValue;  // Use the pre-created object
                    withPreCreatedValues++;
                }
                else
                {
                    valueOOP = nilOOP;  // Default to nil for truly undeclared variables
                }

                var assocOOP = _dictionaryInstaller.AllocateAssociation(keySymbolOOP, valueOOP);

                // DEBUG: Track specific globals
                if (undeclaredVar.Key == "Processor" || undeclaredVar.Key == "ProcessorScheduler")
                {
                    Console.WriteLine($"[Phase3] Created canonical Association for '{undeclaredVar.Key}':");
                    Console.WriteLine($"  Association OOP = {assocOOP}");
                    Console.WriteLine($"  Value OOP = {valueOOP}");
                }

                return assocOOP;
            });

            // DEBUG: Log if we got an existing Association
            if (undeclaredVar.Key == "Processor" || undeclaredVar.Key == "ProcessorScheduler")
            {
                Console.WriteLine($"[Phase3] Canonical Association for '{undeclaredVar.Key}' = {resultAssocOOP}");
            }

            createdCount++;
        }

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Pre-created {createdCount} canonical Associations",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Includes {classNameCount} class names (Phase 5 will set their values)",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Used pre-created values from GlobalRegistry for {withPreCreatedValues} globals",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"AssociationRegistry now contains {AssociationRegistry.Count} canonical Associations",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // CRITICAL: Install all Associations in Undeclared dictionary
        // This matches runtime semantics: undefined variables start in Undeclared,
        // then migrate to SystemDictionary when defined (e.g., class installation, init expressions)
        var undeclaredOOP = GlobalRegistry.Get("Undeclared");
        int installedInUndeclared = 0;

        foreach (var undeclaredVar in _bundle.Undeclared)
        {
            // Get the canonical Association from AssociationRegistry (already created in first loop)
            var associationOOP = AssociationRegistry.GetAll()[undeclaredVar.Key];

            // Install in Undeclared dictionary using hash-based insertion
            _dictionaryInstaller.InstallInDictionary(undeclaredOOP, associationOOP, undeclaredVar.Key);
            installedInUndeclared++;
        }

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Installed {installedInUndeclared} Associations in Undeclared dictionary",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = "Phase 5 will migrate defined globals to SystemDictionary",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    #endregion
}
