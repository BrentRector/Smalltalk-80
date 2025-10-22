using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 3: Wire Metaclass Relationships
/// Establishes bidirectional links between classes and their metaclasses.
/// Sets the 'class' slot on instance-side classes and 'metaclassOf' on metaclasses.
/// Uses primitive interface to eliminate direct heap manager dependence.
/// </summary>
public class Phase3_MetaclassWiring
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly IColdStartPrimitives _primitives;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(int objectOOP, int fieldIndex)> _initializedFields;

    public Phase3_MetaclassWiring(ColdStartBundle bundle, ISmalltalkHeapManager heapManager, IColdStartPrimitives primitives, Dictionary<string, SmalltalkOOP> localIdMap, HashSet<(int objectOOP, int fieldIndex)> initializedFields)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _primitives = primitives;
        _localIdToObjectId = localIdMap;
        _initializedFields = initializedFields;
    }

    public void Run()
    {
        int count = 0;

        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (cls.MetaclassOfLocalId is string instanceId)
            {
                SmalltalkOOP metaOOP = _localIdToObjectId[cls.LocalId];
                SmalltalkOOP instOOP = _localIdToObjectId[instanceId];

                // Wire bidirectional metaclass relationship ONLY where JSON defines the variables
                var instClassDef = FindClassDefinitionForObject(instOOP);
                var metaClassDef = FindClassDefinitionForObject(metaOOP);
                
                if (instClassDef != null && FindInstanceVariableIndex(instClassDef, "class") != -1)
                {
                    SetInstanceVariableUsingPrimitive(instOOP, "class", metaOOP);
                }
                
                if (metaClassDef != null && FindInstanceVariableIndex(metaClassDef, "metaclassOf") != -1)
                {
                    SetInstanceVariableUsingPrimitive(metaOOP, "metaclassOf", instOOP);
                }
                
                // CRITICAL: Set thisClass on metaclass instances (required for class variable lookup)
                // Metaclass instances have 'thisClass' instance variable pointing to their instance class
                // thisClass is defined in the Metaclass class, not in the individual metaclass descriptors
                int thisClassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Metaclass", "thisClass", _bundle);
                if (thisClassIndex >= 0)
                {
                    // Use 0-based indexing to match heap manager reads
                    SetAndTrackInstanceVariable(metaOOP, thisClassIndex, instOOP);
                    if (count == 1) // Only log for first metaclass to avoid spam
                    {
                        Console.WriteLine($"  Setting thisClass: index={thisClassIndex}, meta={metaOOP}, inst={instOOP}");
                    }
                }
                else
                {
                    Console.WriteLine($"  WARNING: Could not find thisClass field index in Metaclass!");
                }

                count++;
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException("Phase 3: No metaclass relationships were wired! This is a critical bug.");
        }

        // Wire the fundamental metaclass hierarchy
        // This must happen BEFORE computing metaclass superclasses
        WireFundamentalMetaclassHierarchy();

        // Compute and set metaclass superclass chain
        // For each metaclass, if it has no explicit superclass, set it to the metaclass of
        // its instance-side class's superclass
        WireMetaclassSuperclasses();
    }

    /// <summary>
    /// Sets an instance variable using primitive interface to match Smalltalk semantics.
    /// Uses instVarAt:put: primitive (1-based indexing) instead of direct heap manager calls.
    /// </summary>
    private void SetInstanceVariableUsingPrimitive(SmalltalkOOP objectOOP, int zeroBasedIndex, SmalltalkOOP valueOOP)
    {
        
        // Use primitive interface with 1-based indexing (Smalltalk convention)
        int oneBasedIndex = zeroBasedIndex + 1;
        _primitives.InstVarAtPut(objectOOP, oneBasedIndex, valueOOP);
    }

    /// <summary>
    /// Sets an instance variable using primitive interface to match Smalltalk semantics.
    /// Uses instVarAt:put: primitive (1-based indexing) instead of direct heap manager calls.
    /// DEPRECATED: Use index-based version instead of string-based lookup.
    /// </summary>
    private void SetInstanceVariableUsingPrimitive(SmalltalkOOP objectOOP, string slotName, SmalltalkOOP valueOOP)
    {
        
        // Find the class definition for this object to get its instance variable layout
        var classDefinition = FindClassDefinitionForObject(objectOOP);
        if (classDefinition == null)
        {
            throw new InvalidOperationException($"Cannot find class definition for object {objectOOP}");
        }
        
        // Look up the slot name in the class's instance variable names using ClassHierarchyUtils
        string className = GetClassName(classDefinition);
        int zeroBasedIndex = ClassHierarchyUtils.FindInstanceVariableIndex(className, slotName, _bundle);
        if (zeroBasedIndex == -1)
        {
            throw new ArgumentException($"Instance variable '{slotName}' not found in class {className}");
        }
        
        // Use primitive interface with 1-based indexing (Smalltalk convention)
        int oneBasedIndex = zeroBasedIndex + 1;
        _primitives.InstVarAtPut(objectOOP, oneBasedIndex, valueOOP);
    }

    /// <summary>
    /// Finds the class definition for an object by looking up its localId.
    /// </summary>
    private ClassDescriptor? FindClassDefinitionForObject(SmalltalkOOP objectOOP)
    {
        // Find the localId for this object
        var localId = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value.RawValue == objectOOP.RawValue).Key;
        if (localId == null) return null;
        
        // Find the class definition in the bundle
        return _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
    }

    /// <summary>
    /// Finds the index of an instance variable by name in a class definition.
    /// Includes inherited instance variables from superclasses.
    /// </summary>
    private int FindInstanceVariableIndex(ClassDescriptor classDefinition, string variableName)
    {
        // Build complete instance variable list including inherited ones
        var allInstVars = new List<string>();
        var currentClass = classDefinition;
        
        // Traverse superclass chain to collect all instance variables
        var classChain = new List<ClassDescriptor>();
        while (currentClass != null)
        {
            classChain.Add(currentClass);
            
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
        
        // Add instance variables from superclass to subclass order
        classChain.Reverse();
        foreach (var cls in classChain)
        {
            foreach (int varIndex in cls.InstVarNames)
            {
                allInstVars.Add(_bundle.Symbols[varIndex]);
            }
        }
        
        // Find the variable in the complete list
        for (int i = 0; i < allInstVars.Count; i++)
        {
            if (allInstVars[i] == variableName)
            {
                return i;
            }
        }
        
        return -1; // Not found
    }

    /// <summary>
    /// Gets the class name for debugging purposes.
    /// </summary>
    private string GetClassName(ClassDescriptor classDefinition)
    {
        return _bundle.Symbols[classDefinition.NameIndex];
    }

    /// <summary>
    /// REMOVED: SetClass method - the object header class field cannot be changed after allocation.
    /// The metaclass relationship is maintained through instance variables only during cold start.
    /// </summary>

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
    ///
    /// This allows class-side methods like 'new' (defined in Behavior) to be
    /// available on all classes through inheritance.
    /// </summary>
    private void WireFundamentalMetaclassHierarchy()
    {
        // Get OOPs for fundamental classes
        if (!_localIdToObjectId.TryGetValue("meta:Object", out SmalltalkOOP metaObjectOOP))
        {
            Console.WriteLine("WARNING: meta:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Class", out SmalltalkOOP classClassOOP))
        {
            Console.WriteLine("WARNING: class:Class not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:ClassDescription", out SmalltalkOOP classDescriptionOOP))
        {
            Console.WriteLine("WARNING: class:ClassDescription not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Behavior", out SmalltalkOOP classBehaviorOOP))
        {
            Console.WriteLine("WARNING: class:Behavior not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Object", out SmalltalkOOP classObjectOOP))
        {
            Console.WriteLine("WARNING: class:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        // Wire the metaclass circle using heap manager directly (0-based indexing)
        // Use "Class" instead of "Metaclass" because class:Class, class:ClassDescription, class:Behavior
        // are instances of their respective metaclasses, but all inherit the superclass field from Behavior
        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Class", "superclass", _bundle);
        if (superclassIndex >= 0)
        {
            Console.WriteLine($"  Wiring fundamental metaclass hierarchy (superclass index: {superclassIndex}):");
            Console.WriteLine($"    meta:Object.superclass = class:Class");
            SetAndTrackInstanceVariable(metaObjectOOP, superclassIndex, classClassOOP);
            Console.WriteLine($"    class:Class.superclass = class:ClassDescription");
            SetAndTrackInstanceVariable(classClassOOP, superclassIndex, classDescriptionOOP);
            Console.WriteLine($"    class:ClassDescription.superclass = class:Behavior");
            SetAndTrackInstanceVariable(classDescriptionOOP, superclassIndex, classBehaviorOOP);
            Console.WriteLine($"    class:Behavior.superclass = class:Object");
            SetAndTrackInstanceVariable(classBehaviorOOP, superclassIndex, classObjectOOP);
        }
        else
        {
            Console.WriteLine("  WARNING: Could not find superclass field index in Class hierarchy!");
        }

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
            Console.WriteLine("  WARNING: Could not find superclass field index, skipping metaclass superclass wiring");
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
                Console.WriteLine($"    WARNING: Metaclass {metaCls.LocalId} not found in localIdToObjectId!");
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
                    Console.WriteLine($"    WARNING: Super-metaclass {superMetaLocalId} not found for {metaCls.LocalId} (OOP={metaOOP})!");
                    skippedCount++;
                    continue;
                }

                // Use the proper superclass index (0-based, direct heap manager access)
                SetAndTrackInstanceVariable(metaOOP, superclassIndex, superMetaOOP);
                wiredCount++;
            }
            else
            {
                // Instance class has no superclass - this metaclass should remain with nil superclass
                // (Only Object and ProtoObject should be in this category)
                string instanceClassName = symbols[instanceCls.NameIndex];
                if (skippedCount == 0 || metaOOP.RawValue == 8530)
                {
                    Console.WriteLine($"    {metaCls.LocalId} (OOP={metaOOP}) for {instanceClassName}: no superclass (expected for root classes)");
                }
                skippedCount++;
            }
        }

        Console.WriteLine($"  Wired {wiredCount} metaclass superclass relationships ({skippedCount} skipped)");
    }

    /// <summary>
    /// Sets an instance variable and marks it as initialized for Phase Y tracking.
    /// </summary>
    private void SetAndTrackInstanceVariable(SmalltalkOOP objectOOP, int fieldIndex, SmalltalkOOP value)
    {
        _heapManager.SetInstanceVariable(objectOOP, fieldIndex, value);
        _initializedFields.Add((objectOOP.RawValue, fieldIndex));
    }
}
