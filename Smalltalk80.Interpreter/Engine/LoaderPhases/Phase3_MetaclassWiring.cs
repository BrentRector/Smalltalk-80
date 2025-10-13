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
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase3_MetaclassWiring(ColdStartBundle bundle, ISmalltalkHeapManager heapManager, IColdStartPrimitives primitives, Dictionary<string, int> localIdMap)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _primitives = primitives;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        int count = 0;

        foreach (var cls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (cls.MetaclassOfLocalId is string instanceId)
            {
                int metaId = _localIdToObjectId[cls.LocalId];
                int instId = _localIdToObjectId[instanceId];

                // Wire bidirectional metaclass relationship ONLY where JSON defines the variables
                var instClassDef = FindClassDefinitionForObject(instId);
                var metaClassDef = FindClassDefinitionForObject(metaId);
                
                if (instClassDef != null && FindInstanceVariableIndex(instClassDef, "class") != -1)
                {
                    SetInstanceVariableUsingPrimitive(instId, "class", metaId);
                }
                
                if (metaClassDef != null && FindInstanceVariableIndex(metaClassDef, "metaclassOf") != -1)
                {
                    SetInstanceVariableUsingPrimitive(metaId, "metaclassOf", instId);
                }

                count++;
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException("Phase 3: No metaclass relationships were wired! This is a critical bug.");
        }

        // TEMPORARILY DISABLED: Wire the fundamental metaclass hierarchy
        // This must happen BEFORE computing metaclass superclasses
        // WireFundamentalMetaclassHierarchy();

        // Compute and set metaclass superclass chain
        // For each metaclass, if it has no explicit superclass, set it to the metaclass of
        // its instance-side class's superclass
        WireMetaclassSuperclasses();
    }

    /// <summary>
    /// Sets an instance variable using primitive interface to match Smalltalk semantics.
    /// Uses instVarAt:put: primitive (1-based indexing) instead of direct heap manager calls.
    /// </summary>
    private void SetInstanceVariableUsingPrimitive(int objectId, int zeroBasedIndex, int valueId)
    {
        var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
        var valueOOP = SmalltalkOOP.FromObjectTableIndex(valueId);
        
        // Use primitive interface with 1-based indexing (Smalltalk convention)
        int oneBasedIndex = zeroBasedIndex + 1;
        _primitives.InstVarAtPut(objectOOP, oneBasedIndex, valueOOP);
    }

    /// <summary>
    /// Sets an instance variable using primitive interface to match Smalltalk semantics.
    /// Uses instVarAt:put: primitive (1-based indexing) instead of direct heap manager calls.
    /// DEPRECATED: Use index-based version instead of string-based lookup.
    /// </summary>
    private void SetInstanceVariableUsingPrimitive(int objectId, string slotName, int valueId)
    {
        var objectOOP = SmalltalkOOP.FromObjectTableIndex(objectId);
        var valueOOP = SmalltalkOOP.FromObjectTableIndex(valueId);
        
        // Find the class definition for this object to get its instance variable layout
        var classDefinition = FindClassDefinitionForObject(objectId);
        if (classDefinition == null)
        {
            throw new InvalidOperationException($"Cannot find class definition for object {objectId}");
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
    private ClassDescriptor? FindClassDefinitionForObject(int objectId)
    {
        // Find the localId for this object
        var localId = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectId).Key;
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
    /// This creates the "metaclass circle" that allows ClassDescription methods to be
    /// inherited by all metaclasses.
    ///
    /// Hierarchy:
    ///   meta:Object (Object class) → superclass = class:Class
    ///   class:Class → superclass = class:ClassDescription
    ///   class:ClassDescription → superclass = class:Object
    ///
    /// This allows class-side methods like comment: (defined in ClassDescription) to be
    /// available on all classes through inheritance.
    /// </summary>
    private void WireFundamentalMetaclassHierarchy()
    {
        // Get OOPs for fundamental classes
        if (!_localIdToObjectId.TryGetValue("meta:Object", out int metaObjectOop))
        {
            Console.WriteLine("WARNING: meta:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Class", out int classClassOop))
        {
            Console.WriteLine("WARNING: class:Class not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:ClassDescription", out int classDescriptionOop))
        {
            Console.WriteLine("WARNING: class:ClassDescription not found, skipping fundamental hierarchy wiring");
            return;
        }

        if (!_localIdToObjectId.TryGetValue("class:Object", out int classObjectOop))
        {
            Console.WriteLine("WARNING: class:Object not found, skipping fundamental hierarchy wiring");
            return;
        }

        // Wire the metaclass circle using primitive interface
        // All these are metaclass objects, so they follow the Metaclass template
        int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex("Metaclass", "superclass", _bundle);
        if (superclassIndex >= 0)
        {
            SetInstanceVariableUsingPrimitive(metaObjectOop, superclassIndex, classClassOop);
            SetInstanceVariableUsingPrimitive(classClassOop, superclassIndex, classDescriptionOop);
            SetInstanceVariableUsingPrimitive(classDescriptionOop, superclassIndex, classObjectOop);
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
        var symbols = _bundle.Symbols;

        foreach (var metaCls in _bundle.Classes.Where(c => c.IsMetaclass))
        {
            if (metaCls.MetaclassOfLocalId is not string instanceLocalId)
                continue;

            // Find the instance-side class descriptor
            var instanceCls = _bundle.Classes.FirstOrDefault(c => c.LocalId == instanceLocalId);
            if (instanceCls == null)
                continue;

            // If instance class has a superclass, set metaclass's superclass to superclass's metaclass
            if (instanceCls.SuperNameIndex is int superNameIdx)
            {
                string superName = symbols[superNameIdx];

                // Find the superclass's metaclass localId
                string superMetaLocalId = $"meta:{superName}";

                if (_localIdToObjectId.TryGetValue(metaCls.LocalId, out int metaOop) &&
                    _localIdToObjectId.TryGetValue(superMetaLocalId, out int superMetaOop))
                {
                    // Only set superclass if this metaclass has instance variables (at least for superclass slot)
                    var metaOOP = SmalltalkOOP.FromObjectTableIndex(metaOop);
                    int metaInstVarCount = _heapManager.GetInstanceVariableCount(metaOOP);
                    
                    if (metaInstVarCount > 0)
                    {
                        _heapManager.SetInstanceVariable(metaOOP, 0, SmalltalkOOP.FromObjectTableIndex(superMetaOop)); // superclass slot
                        wiredCount++;
                    }
                    else
                    {
                        // Skip metaclasses with 0 instance variables - they don't have superclass slot
                        Console.WriteLine($"  Skipping superclass wiring for {metaCls.LocalId} (0 instance variables)");
                    }

                    // Debug: trace PopUpMenu class hierarchy
                    if (metaCls.LocalId == "meta:PopUpMenu" || metaCls.LocalId == "meta:ActionMenu")
                    {
                        Console.WriteLine($"DEBUG: Wired {metaCls.LocalId} ({metaOop}) → superclass = {superMetaLocalId} ({superMetaOop})");
                    }
                }
            }
        }

        Console.WriteLine($"DEBUG: Wired {wiredCount} metaclass superclass relationships");
    }
}
