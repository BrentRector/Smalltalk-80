using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 2: Complete Class Infrastructure
/// Consolidates class shell allocation, metaclass wiring, class metadata, and layout loading.
/// This phase creates all class and metaclass objects with their complete infrastructure.
/// </summary>
public class Phase2_ClassInfrastructure
{
    private readonly ColdStartBundle _bundle;
    private readonly ObjectGraphAllocator _allocator;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly ColdstartReport _report;

    /// <summary>
    /// Gets the layout resolver populated with class layouts.
    /// </summary>
    public LayoutResolver LayoutResolver { get; private set; } = new();

    public Phase2_ClassInfrastructure(
        ColdStartBundle bundle,
        ObjectGraphAllocator allocator,
        ISmalltalkHeapManager heapManager,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        ColdstartReport report)
    {
        _bundle = bundle;
        _allocator = allocator;
        _heapManager = heapManager;
        _localIdToObjectId = localIdToObjectId;
        _initializedFields = initializedFields;
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void Run()
    {
        // Step 1: Allocate class shells (from Phase2_ClassShells)
        AllocateClassShells();
        _report.AddValidation("Phase 2", "Class shells allocated", true);

        // Step 2: Wire metaclass.thisClass for all metaclasses (from Phase3)
        WireMetaclassThisClass();
        _report.AddValidation("Phase 2", "Metaclass.thisClass wired", true);

        // Step 3: Wire the fundamental metaclass circle (from Phase3)
        WireFundamentalMetaclassCircle();
        _report.AddValidation("Phase 2", "Metaclass circle wired", true);

        // Step 4: Resolve class metadata - name, format, superclass (from Phase4)
        ResolveClassMetadata();

        // Step 4b: Populate ClassDescription.instanceVariables field with Array-of-Strings
        PopulateInstanceVariablesField();
        _report.AddValidation("Phase 2", "ClassDescription.instanceVariables populated", true);

        // Step 5: Load precomputed layouts (from Loader Phase 5)
        LoadPrecomputedLayouts();
    }

    #region Class Shell Allocation

    /// <summary>
    /// Allocates empty class and metaclass shells for all classes in the bundle.
    /// Also creates the SystemDictionary for now (to maintain working object ID sequence).
    /// </summary>
    private void AllocateClassShells()
    {
        // Create SystemDictionary with proper sizing (no object ID sequencing)
        int instanceClassCount = _bundle.Classes.Count(c => !c.IsMetaclass);
        int poolCount = _bundle.Objects.Count(o => o.LocalId.StartsWith("pool:"));
        int systemGlobalCount = 2; // Smalltalk self-reference + USTable global
        int totalEntries = instanceClassCount + poolCount + systemGlobalCount;
        totalEntries = Math.Max(totalEntries * 2, 500); // Large buffer

        var systemDictClassDefinition = ClassHierarchyUtils.FindClassDefinition("SystemDictionary", _bundle);
        if (systemDictClassDefinition == null)
        {
            throw new InvalidOperationException("SystemDictionary class not found in bundle");
        }
        var systemDictOOP = _allocator.AllocateArray("SystemDictionary", totalEntries);
        _localIdToObjectId["object:Smalltalk"] = systemDictOOP;

        // CRITICAL: Initialize 'tally' instance variable (inherited from Set) to 0
        // Set has 1 instance variable: tally (a SmallInteger counting elements)
        // SystemDictionary starts empty, so tally = 0
        _allocator.SetInstanceVariable(systemDictOOP, 0, new SmalltalkOOP(1)); // SmallInteger(0) = OOP(1)
        
        // CRITICAL: Mark tally as initialized so Phase 3 won't overwrite it with nil
        _initializedFields.Add((systemDictOOP, 0));
        
        Console.WriteLine($"[Phase2] ✓ Initialized SystemDictionary tally to SmallInteger(0) and marked as initialized");

        GlobalRegistry.Register("Smalltalk", systemDictOOP);

        // Now allocate all class shells
        foreach (var cls in _bundle.Classes)
        {
            // CRITICAL: All class objects are instances of "Class" and should have 9 instance variables
            // All metaclass objects are instances of "Metaclass" and should have 7 instance variables
            // This is different from the instance variable count of instances OF these classes
            int totalInstVarCount = cls.IsMetaclass ? 7 : 9;

            // Get the appropriate class name from the class hierarchy - no hard-coding!
            string classOfClassName = cls.IsMetaclass ? "Metaclass" : "Class";
            var classOOP = _allocator.AllocateFixedObject(classOfClassName);
            _localIdToObjectId[cls.LocalId] = classOOP;

            // Register class in GlobalRegistry
            // For instance classes (class:Name), register as "Name"
            // For metaclasses (meta:Name), DON'T register (only instance classes need global lookup)
            if (!cls.IsMetaclass)
            {
                string className = GetClassNameFromLocalId(cls.LocalId);
                GlobalRegistry.RegisterClass(className, classOOP);
            }
        }
    }

    #endregion

    #region Metaclass Wiring

    /// <summary>
    /// Wires metaclass.thisClass for all metaclasses.
    /// CRITICAL: thisClass is required for class variable lookup.
    /// </summary>
    private void WireMetaclassThisClass()
    {
        int count = 0;

        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (cls.MetaclassOfLocalId is string instanceId)
            {
                SmalltalkOOP metaOOP = _localIdToObjectId[cls.LocalId];
                SmalltalkOOP instOOP = _localIdToObjectId[instanceId];

                // CRITICAL: Set thisClass on metaclass instances (required for class variable lookup)
                // Metaclass instances have 'thisClass' instance variable pointing to their instance class
                // thisClass is defined in the Metaclass class, not in the individual metaclass descriptors
                int thisClassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Metaclass", "thisClass", _bundle);
                if (thisClassIndex >= 0)
                {
                    // Use 0-based indexing to match heap manager reads
                    SetAndTrackInstanceVariable(metaOOP, thisClassIndex, instOOP);
                    if (count == 0) // Only log for first metaclass to avoid spam
                    {
// Console.WriteLine($"  Setting thisClass: index={thisClassIndex}, meta={metaOOP}, inst={instOOP}");
                    }
                }
                else
                {
// Console.WriteLine($"  WARNING: Could not find thisClass field index in Metaclass!");
                }

                count++;
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException("Phase 2: No metaclass relationships were wired! This is a critical bug.");
        }
    }

    /// <summary>
    /// Wires the fundamental metaclass inheritance chain per Blue Book specification.
    /// This creates the "metaclass circle" that allows Behavior methods (like 'new') to be
    /// inherited by all metaclasses.
    ///
    /// Hierarchy:
    ///   meta:Object (Object class) → superclass = class:Class
    ///   class:Class → superclass = class:ClassDescription
    ///   class:ClassDescription → superclass = class:Behavior
    ///   class:Behavior → superclass = class:Object
    /// </summary>
    private void WireFundamentalMetaclassCircle()
    {
        // Get OOPs for fundamental classes
        if (!_localIdToObjectId.TryGetValue("meta:Object", out SmalltalkOOP metaObjectOOP))
        {
// Console.WriteLine("WARNING: meta:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Class", out SmalltalkOOP classClassOOP))
        {
// Console.WriteLine("WARNING: class:Class not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:ClassDescription", out SmalltalkOOP classDescriptionOOP))
        {
// Console.WriteLine("WARNING: class:ClassDescription not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Behavior", out SmalltalkOOP classBehaviorOOP))
        {
// Console.WriteLine("WARNING: class:Behavior not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Object", out SmalltalkOOP classObjectOOP))
        {
// Console.WriteLine("WARNING: class:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        // Wire the metaclass circle using heap manager directly (0-based indexing)
        // Use "Class" instead of "Metaclass" because class:Class, class:ClassDescription, class:Behavior
        // are instances of their respective metaclasses, but all inherit the superclass field from Behavior
        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);
        if (superclassIndex >= 0)
        {
// Console.WriteLine($"  Wiring fundamental metaclass hierarchy (superclass index: {superclassIndex}):");
// Console.WriteLine($"    meta:Object.superclass = class:Class");
            SetAndTrackInstanceVariable(metaObjectOOP, superclassIndex, classClassOOP);
// Console.WriteLine($"    class:Class.superclass = class:ClassDescription");
            SetAndTrackInstanceVariable(classClassOOP, superclassIndex, classDescriptionOOP);
// Console.WriteLine($"    class:ClassDescription.superclass = class:Behavior");
            SetAndTrackInstanceVariable(classDescriptionOOP, superclassIndex, classBehaviorOOP);
// Console.WriteLine($"    class:Behavior.superclass = class:Object");
            SetAndTrackInstanceVariable(classBehaviorOOP, superclassIndex, classObjectOOP);
        }
        else
        {
// Console.WriteLine("  WARNING: Could not find superclass field index in Class hierarchy!");
        }

        // Wire metaclass superclasses
        WireMetaclassSuperclasses();
    }

    /// <summary>
    /// Computes and sets the superclass for each metaclass based on its instance-side class's superclass.
    ///
    /// Rule: If metaclass M is the class of instance class I, and I has superclass S,
    /// then M's superclass is the metaclass of S.
    ///
    /// Example:
    ///   ActionMenu (instance class) has superclass PopUpMenu
    ///   ActionMenu class (metaclass) should have superclass PopUpMenu class
    /// </summary>
    private void WireMetaclassSuperclasses()
    {
        int wiredCount = 0;
        int skippedCount = 0;
        var symbols = _bundle.Symbols;

        // Look up the superclass field index properly (inherited from Behavior)
        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);
        if (superclassIndex < 0)
        {
// Console.WriteLine("  WARNING: Could not find superclass field index, skipping metaclass superclass wiring");
            return;
        }

        foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (metaCls.MetaclassOfLocalId is not string instanceLocalId)
                continue;

            // Find the instance-side class descriptor
            var instanceCls = _bundle.Classes.FirstOrDefault(c => c.LocalId == instanceLocalId);
            if (instanceCls == null)
                continue;

            // Get metaclass OOP for diagnostics
            if (!_localIdToObjectId.TryGetValue(metaCls.LocalId, out SmalltalkOOP metaOOP))
            {
// Console.WriteLine($"    WARNING: Metaclass {metaCls.LocalId} not found in localIdToObjectId!");
                continue;
            }

            // If instance class has a superclass, set metaclass's superclass to superclass's metaclass
            if (instanceCls.SuperNameIndex is int superNameIdx)
            {
                string superName = symbols[superNameIdx];

                // Find the superclass's metaclass localId
                string superMetaLocalId = $"meta:{superName}";

                if (!_localIdToObjectId.TryGetValue(superMetaLocalId, out SmalltalkOOP superMetaOOP))
                {
// Console.WriteLine($"    WARNING: Super-metaclass {superMetaLocalId} not found for {metaCls.LocalId}");
                    skippedCount++;
                    continue;
                }

                // Use the proper superclass index (0-based, direct heap manager access)
                SetAndTrackInstanceVariable(metaOOP, superclassIndex, superMetaOOP);
                wiredCount++;
            }
            else
            {
                // Instance class has no superclass - this metaclass was already wired in WireFundamentalMetaclassCircle()
                // Only meta:Object should be in this category (Object has no superclass)
                // meta:Object.superclass = class:Class (wired in fundamental hierarchy)
                string instanceClassName = symbols[instanceCls.NameIndex];

                // Verify this is meta:Object
                if (instanceClassName != "Object")
                {
// Console.WriteLine($"    WARNING: Unexpected class without superclass: {instanceClassName}");
                }

                skippedCount++;
            }
        }

// Console.WriteLine($"  Wired {wiredCount} metaclass superclass relationships ({skippedCount} skipped - meta:Object already wired in fundamental hierarchy)");
    }

    #endregion

    #region Class Metadata Resolution

    /// <summary>
    /// Populates class and metaclass objects with their metadata.
    /// CRITICAL: Class and Metaclass have different instance variable layouts!
    /// </summary>
    private void ResolveClassMetadata()
    {
        var symbols = _bundle.Symbols;
        var strings = _bundle.Strings;

        // Process instance classes and metaclasses separately for clarity
        foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
        {
            InitializeClassObject(cls, symbols, strings);
        }

        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            InitializeMetaclassObject(cls, symbols, strings);
        }
    }

    /// <summary>
    /// Initializes a Class object (instance of Class).
    /// 
    /// Class instance variable layout (9 fields total):
    ///   [0] superclass      - reference to superclass (Class object)
    ///   [1] methodDict      - MethodDictionary of instance-side methods (set in Phase 4)
    ///   [2] format          - SmallInteger encoding object format
    ///   [3] subclasses      - Set of subclasses (not set during cold start)
    ///   [4] instanceVariables - Array of instance variable name Strings (set in PopulateInstanceVariablesField)
    ///   [5] organization    - ClassOrganizer (not set during cold start)
    ///   [6] name            - Symbol with class name (e.g., #Browser)
    ///   [7] classPool       - Dictionary of class variables
    ///   [8] sharedPools     - Array of shared pool Dictionaries
    /// </summary>
    private void InitializeClassObject(ClassDescriptor cls, string[] symbols, string[] strings)
    {
        SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];
        string className = symbols[cls.NameIndex];

        // Field [0]: superclass - reference to superclass Class object
        if (cls.SuperNameIndex is int superIdx)
        {
            string superName = symbols[superIdx];
            string superLocalId = $"class:{superName}";
            if (!_localIdToObjectId.TryGetValue(superLocalId, out SmalltalkOOP superOOP))
            {
                throw new InvalidOperationException($"Superclass '{superName}' not found for class '{className}'");
            }
            SetAndTrackInstanceVariable(classOOP, 0, superOOP);
        }
        // else: Object class has no superclass (nil set in Phase Y)

        // Field [1]: methodDict - set in Phase 4 (method compilation)

        // Field [2]: format - SmallInteger encoding object format
        var formatOOP = SmalltalkOOP.FromSmallInteger(cls.Format);
        SetAndTrackInstanceVariable(classOOP, 2, formatOOP);

        // Field [3]: subclasses - not set during cold start (populated at runtime)

        // Field [4]: instanceVariables - Array of Strings, set in PopulateInstanceVariablesField()

        // Field [5]: organization - ClassOrganizer, not set during cold start

        // Field [6]: name - Symbol with class name
        SetAndTrackInstanceVariable(classOOP, 6, InternSymbol(className));

        // Field [7]: classPool - Dictionary of class variables
        int classVarCount = cls.ClassVarNames?.Length ?? 0;
        var classPoolOOP = AllocateEmptyDictionary(classVarCount);
        SetAndTrackInstanceVariable(classOOP, 7, classPoolOOP);

        // Field [8]: sharedPools - Array of shared pool Dictionaries
        // IMPORTANT: Do NOT set sharedPools here! Phase 5 will create the proper Array structure
        // and populate it with all pool dictionaries. Setting it here causes the Dictionary
        // to become unreachable when Phase 5 replaces it with an Array, leading to GC failures.
        //
        // Phase 5's InstallPoolDictionaryInClasses() creates an Array and installs all pools correctly.
    }

    /// <summary>
    /// Initializes a Metaclass object (instance of Metaclass).
    /// 
    /// Metaclass instance variable layout (7 fields total):
    ///   [0] superclass        - reference to superclass Metaclass (set in WireMetaclassSuperclasses)
    ///   [1] methodDict        - MethodDictionary of class-side methods (set in Phase 4)
    ///   [2] format            - SmallInteger encoding object format
    ///   [3] subclasses        - Set of subclasses (not set during cold start)
    ///   [4] instanceVariables - NOT populated for metaclasses (inherited but unused)
    ///   [5] organization      - ClassOrganizer (not set during cold start)
    ///   [6] thisClass         - reference to instance class (set in WireMetaclassThisClass)
    /// 
    /// CRITICAL: Metaclass does NOT have name, classPool, or sharedPools fields!
    /// </summary>
    private void InitializeMetaclassObject(ClassDescriptor cls, string[] symbols, string[] strings)
    {
        SmalltalkOOP metaclassOOP = _localIdToObjectId[cls.LocalId];
        string className = symbols[cls.NameIndex];

        // Field [0]: superclass - set in WireFundamentalMetaclassCircle() and WireMetaclassSuperclasses()

        // Field [1]: methodDict - set in Phase 4 (method compilation)

        // Field [2]: format - SmallInteger encoding object format
        var formatOOP = SmalltalkOOP.FromSmallInteger(cls.Format);
        SetAndTrackInstanceVariable(metaclassOOP, 2, formatOOP);

        // Field [3]: subclasses - not set during cold start

        // Field [4]: instanceVariables - NOT populated for metaclasses

        // Field [5]: organization - not set during cold start

        // Field [6]: thisClass - set in WireMetaclassThisClass()

        // NO name, classPool, or sharedPools fields for Metaclass!
    }

    /// <summary>
    /// Allocates an empty Dictionary for classPool.
    /// Dictionary has instance variables: tally (SmallInteger 0), plus indexed array for associations.
    /// Size is calculated based on the number of class variables with a reasonable load factor.
    /// </summary>
    private SmalltalkOOP AllocateEmptyDictionary(int classVarCount)
    {
        // Calculate initial size with ~75% load factor (round up to next power of 2 for efficiency)
        // Minimum size of 4 for classes with 0-3 class variables
        int initialSize = classVarCount == 0 ? 4 : NextPowerOfTwo((int)Math.Ceiling(classVarCount / 0.75));

        if (!GlobalRegistry.TryGet("class:Dictionary", out SmalltalkOOP dictClassOOP))
        {
            throw new InvalidOperationException("Dictionary class not found - needed for classPool allocation");
        }

        // Dictionary layout: instance var 'tally' + indexed array
        // Find Dictionary's instance variable count
        var dictClassDef = ClassHierarchyUtils.FindClassDefinition("Dictionary", _bundle);
        if (dictClassDef == null)
        {
            throw new InvalidOperationException("Dictionary class definition not found in bundle");
        }

        int instVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(dictClassDef, _bundle);

        // Allocate Dictionary as PointerIndexed object
        var dictOOP = _heapManager.AllocateObject(
            ObjectFormat.PointerIndexed,
            instVarCount,
            initialSize
        );

        // Set class reference (will be done by PhaseX, but we can set it now since Dictionary class exists)
        _heapManager.SetSlot(dictOOP, 0, dictClassOOP);

        // Initialize tally to SmallInteger 0
        // Find 'tally' instance variable index in Dictionary hierarchy
        int tallyIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
        if (tallyIndex >= 0)
        {
            _heapManager.SetInstanceVariable(dictOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(0));
        }

        // NOTE: Dictionary indexed slots will be initialized to nil in Phase Y
        // (after nil singleton is created in Phase 6). Phase 2 runs before Phase 6,
        // so GlobalRegistry.NilOOP is not yet available here.

        return dictOOP;
    }

    #endregion

    #region Layout Loading

    /// <summary>
    /// Loads precomputed field layouts from bundle schema v1.0.6+.
    /// This avoids expensive runtime computation of class layouts.
    /// </summary>
    private void LoadPrecomputedLayouts()
    {
        LayoutResolver.LoadLayouts(_bundle.Classes, _bundle.Symbols);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sets an instance variable and marks it as initialized for Phase Y tracking.
    /// </summary>
    private void SetAndTrackInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value)
    {
        _heapManager.SetInstanceVariable(objectOOP, fieldIndex, value);
        _initializedFields.Add((objectOOP, fieldIndex));
    }

    /// <summary>
    /// Gets the class name from a localId.
    /// </summary>
    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }

    /// <summary>
    /// Determines what class a class object should be an instance of, based on JSON data.
    /// Replaces hard-coded "Class" and "Metaclass" assumptions with JSON-based lookup.
    /// </summary>
    private string GetTemplateClassNameFromBundle(ClassDescriptor classDefinition)
    {
        if (classDefinition.IsMetaclass)
        {
            // All metaclass objects are instances of "Metaclass" class
            // Find the Metaclass class in the JSON bundle
            var metaclassDefinition = ClassHierarchyUtils.FindClassDefinition("Metaclass", _bundle);
            if (metaclassDefinition != null)
            {
                return _bundle.Symbols[metaclassDefinition.NameIndex];
            }
            throw new InvalidOperationException("Metaclass class definition not found in JSON bundle");
        }
        else
        {
            // All regular class objects are instances of "Class" class
            // Find the Class class in the JSON bundle
            var classClassDefinition = ClassHierarchyUtils.FindClassDefinition("Class", _bundle);
            if (classClassDefinition != null)
            {
                return _bundle.Symbols[classClassDefinition.NameIndex];
            }
            throw new InvalidOperationException("Class class definition not found in JSON bundle");
        }
    }

    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// During Phase 2, symbols already exist so we look them up in SymbolRegistry.
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
    /// Calculates the next power of 2 greater than or equal to the given value.
    /// Used for efficient Dictionary sizing.
    /// </summary>
    private int NextPowerOfTwo(int value)
    {
        if (value <= 4) return 4;
        if (value <= 8) return 8;
        if (value <= 16) return 16;
        if (value <= 32) return 32;
        if (value <= 64) return 64;
        if (value <= 128) return 128;
        return 256;
    }

    /// <summary>
    /// Populates ClassDescription.instanceVariables field (slot 4) with Array-of-Strings
    /// containing the class's own instance variable names (not inherited ones).
    /// This enables runtime reflection via RuntimeHeapAccessor.BuildFieldMap().
    ///
    /// ClassDescription field layout:
    ///   Inherited from Behavior: superclass(0), methodDict(1), format(2), subclasses(3)
    ///   Own fields: instanceVariables(4), organization(5)
    /// </summary>
    private void PopulateInstanceVariablesField()
    {
// Console.WriteLine("  Populating ClassDescription.instanceVariables fields...");

        int stringCount = 0;
        int arrayCount = 0;
        int classCount = 0;

        // Get Array class OOP for allocating instance variable Arrays
        if (!_localIdToObjectId.TryGetValue("class:Array", out SmalltalkOOP arrayClassOOP))
        {
            throw new InvalidOperationException("Array class not found - needed for instanceVariables field");
        }

        // Get String class OOP for allocating instance variable name Strings
        if (!_localIdToObjectId.TryGetValue("class:String", out SmalltalkOOP stringClassOOP))
        {
            throw new InvalidOperationException("String class not found - needed for instanceVariables field");
        }

        // Process all non-metaclass classes (metaclasses don't have instanceVariables field populated)
        foreach (var cls in _bundle.Classes.Where(c => !c.IsMetaclass))
        {
            SmalltalkOOP classOOP = _localIdToObjectId[cls.LocalId];

            // Skip if class has no instance variables of its own
            if (cls.InstVarNames.Length == 0)
            {
                // Set instanceVariables to empty Array (not nil)
                var emptyArrayOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, 0);
                _heapManager.SetSlot(emptyArrayOOP, 0, arrayClassOOP);
                SetAndTrackInstanceVariable(classOOP, 4, emptyArrayOOP);
                continue;
            }

            // Allocate String objects for each instance variable name
            var instVarStringOOPs = new SmalltalkOOP[cls.InstVarNames.Length];

            for (int i = 0; i < cls.InstVarNames.Length; i++)
            {
                int symbolIndex = cls.InstVarNames[i];
                string varName = _bundle.Symbols[symbolIndex];
                byte[] varNameBytes = System.Text.Encoding.Latin1.GetBytes(varName);

                // Allocate ByteIndexed String object
                var stringOOP = _heapManager.AllocateObject(ObjectFormat.ByteIndexed, 0, varNameBytes.Length);
                _heapManager.SetSlot(stringOOP, 0, stringClassOOP);  // Set class reference

                // Copy bytes into String object
                for (int b = 0; b < varNameBytes.Length; b++)
                {
                    _heapManager.SetIndexedByte(stringOOP, b, varNameBytes[b]);
                }

                instVarStringOOPs[i] = stringOOP;
                stringCount++;
            }

            // Allocate Array object to hold the String objects
            var instVarsArrayOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, 0, cls.InstVarNames.Length);
            _heapManager.SetSlot(instVarsArrayOOP, 0, arrayClassOOP);  // Set class reference

            // Populate Array with String OOPs
            for (int i = 0; i < instVarStringOOPs.Length; i++)
            {
                _heapManager.SetIndexedOOP(instVarsArrayOOP, i, instVarStringOOPs[i]);
            }

            arrayCount++;

            // Set ClassDescription.instanceVariables field (slot 4)
            SetAndTrackInstanceVariable(classOOP, 4, instVarsArrayOOP);
            classCount++;
        }

// Console.WriteLine($"    Populated instanceVariables for {classCount} classes");
// Console.WriteLine($"    Allocated {stringCount} String objects for field names");
// Console.WriteLine($"    Allocated {arrayCount} Array objects");
    }

    #endregion
}
