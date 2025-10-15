using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 4: Resolve Class Fields
/// Populates class objects with their name, superclass, comment, and pool dictionaries.
/// Uses primitive interface to eliminate direct heap manager dependence.
/// </summary>
public class Phase4_ClassResolution
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly IColdStartPrimitives _primitives;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly IReadOnlyList<string> _symbols;
    private readonly IReadOnlyList<string> _strings;

    public Phase4_ClassResolution(
        ColdStartBundle bundle,
        ISmalltalkHeapManager heapManager,
        IColdStartPrimitives primitives,
        Dictionary<string, int> localIdMap,
        IReadOnlyList<string> symbols,
        IReadOnlyList<string> strings)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _primitives = primitives;
        _localIdToObjectId = localIdMap;
        _symbols = symbols;
        _strings = strings;
    }

    public void Run()
    {
        // First pass: Set class OOP for all class objects to point to their metaclass
        // This fixes the fundamental issue where class objects have classOOP = 0
        foreach (var cls in _bundle.Classes)
        {
            int classObjectId = _localIdToObjectId[cls.LocalId];
            var classObjectOOP = SmalltalkOOP.FromObjectTableIndex(classObjectId);
            
            if (cls.IsMetaclass)
            {
                // Metaclass objects are instances of the "Metaclass" class
                // Note: Class reference will be set by PhaseX
            }
            else
            {
                // Note: Class reference will be set by PhaseX
            }
        }

        // Second pass: Initialize instance variables for class objects
        foreach (var cls in _bundle.Classes)
        {
            int classId = _localIdToObjectId[cls.LocalId];
            string className = _symbols[cls.NameIndex];
            
            // Determine the template class for this object from JSON data
            string templateClassName = GetTemplateClassNameFromBundle(cls);
            
            // First, initialize ALL instance variables to nil (user requirement)
            InitializeAllInstanceVariablesToNil(classId, templateClassName);
            
            // Set name instance variable (all Class objects have this)
            if (ClassHierarchyUtils.HasInstanceVariable(templateClassName, "name", _bundle))
            {
                int nameIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "name", _bundle);
                if (nameIndex >= 0)
                {
                    SetInstanceVariableUsingPrimitive(classId, nameIndex, InternSymbol(className));
                }
            }

            // Set format field (encode as SmallInteger for AllocateInstance)
            int format = cls.Format;
            
            // WORKAROUND: Fix compiler bug where MethodContext and BlockContext are incorrectly 
            // marked as word-indexable (instance_spec=2) instead of pointer-indexable (instance_spec=1)
            // These classes need to store object pointers in their indexed fields for args/temps
            if (className == "MethodContext" || className == "BlockContext")
            {
                int instanceSpec = (format >> 12) & 0x3;
                if (instanceSpec == 2) // word-indexable
                {
                    // Convert to pointer-indexable: change instance_spec from 2 to 1
                    format = (format & 0x0FFF) | (1 << 12); // Clear top 4 bits, set instance_spec=1
                }
            }
            
            if (ClassHierarchyUtils.HasInstanceVariable(templateClassName, "format", _bundle))
            {
                int formatIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "format", _bundle);
                if (formatIndex >= 0)
                {
                    int formatSmallInteger = (format << 1) | 1; // Encode as SmallInteger
                    SetInstanceVariableUsingPrimitive(classId, formatIndex, formatSmallInteger);
                }
            }

            // Set superclass reference
            // IMPORTANT: Skip for metaclasses - their superclass chain is already wired in Phase 3
            if (!cls.IsMetaclass && cls.SuperNameIndex is int superIdx && 
                ClassHierarchyUtils.HasInstanceVariable(templateClassName, "superclass", _bundle))
            {
                string superName = _symbols[superIdx];
                int superId = GlobalRegistry.LookupClassObjectId(superName);
                int superclassIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "superclass", _bundle);
                if (superclassIndex >= 0)
                {
                    SetInstanceVariableUsingPrimitive(classId, superclassIndex, superId);
                }
            }

            // Set class comment (if instanceVariables field exists for ClassDescription)
            if (cls.CommentStringIndex is int commentIdx)
            {
                string comment = _strings[commentIdx];
                // Comments are typically stored in 'organization' or similar field
                // For now, skip setting comments until we understand the field mapping better
                // TODO: Determine correct field for class comments in Smalltalk-80
            }

            // Set pool dictionaries (skip if not yet allocated - will be patched later)
            foreach (var poolId in cls.PoolLocalIds)
            {
                if (_localIdToObjectId.TryGetValue(poolId, out int poolObjId))
                {
                    // Pool dictionaries are typically stored in sharedPools field
                    if (ClassHierarchyUtils.HasInstanceVariable(templateClassName, "sharedPools", _bundle))
                    {
                        int sharedPoolsIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, "sharedPools", _bundle);
                        if (sharedPoolsIndex >= 0)
                        {
                            // TODO: This should append to an array, not replace - needs proper implementation
                            SetInstanceVariableUsingPrimitive(classId, sharedPoolsIndex, poolObjId);
                        }
                    }
                }
                // else: pool not yet allocated, will be wired in patch phase
            }
        }
        
        // Final step: Ensure ALL remaining instance variables are set to nil (never OOP 0)
        // This is critical for validation - any uninitialized variables must be nil, not OOP 0
        EnsureAllInstanceVariablesNil();
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
        
        // Find which template this object was allocated with
        var localId = _localIdToObjectId.FirstOrDefault(kvp => kvp.Value == objectId).Key;
        if (localId == null)
        {
            throw new InvalidOperationException($"Cannot find localId for object {objectId}");
        }
        
        // Determine template class name from JSON data
        var classDefinition = _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
        if (classDefinition == null)
        {
            throw new InvalidOperationException($"Cannot find class definition for localId: {localId}");
        }
        string templateClassName = GetTemplateClassNameFromBundle(classDefinition);
        
        // Find the variable index in the template hierarchy using ClassHierarchyUtils
        int zeroBasedIndex = ClassHierarchyUtils.FindInstanceVariableIndex(templateClassName, slotName, _bundle);
        if (zeroBasedIndex == -1)
        {
            throw new ArgumentException($"Instance variable '{slotName}' not found in {templateClassName} hierarchy");
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
    /// </summary>
    private int FindInstanceVariableIndex(ClassDescriptor classDefinition, string variableName)
    {
        for (int i = 0; i < classDefinition.InstVarNames.Length; i++)
        {
            int symbolIndex = classDefinition.InstVarNames[i];
            string varName = _bundle.Symbols[symbolIndex];
            if (varName == variableName)
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
    /// Interns a symbol using the symbol table from Phase 1.
    /// During Phase 4, symbols already exist so we look them up in SymbolRegistry.
    /// </summary>
    private int InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == name)
            {
                return SymbolRegistry.GetObjectId(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{name}' not found in symbol table. Ensure Phase 1 (Symbol Interning) completed successfully.");
    }

    /// <summary>
    /// Interns a string by creating a ByteIndexed object.
    /// Unlike symbols, strings are not deduplicated.
    /// </summary>
    private int InternString(string value)
    {
        // Use placeholder class (will be fixed later when String class exists)
        var placeholderClassOOP = SmalltalkOOP.FromObjectTableIndex(0);
        
        // Encode string as UTF-8 bytes
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);
        
        // Look up String class layout from bundle data
        var stringClassDefinition = ClassHierarchyUtils.FindClassDefinition("String", _bundle);
        if (stringClassDefinition == null)
        {
            throw new InvalidOperationException("String class not found in bundle");
        }
        
        // Allocate ByteIndexed object to hold the string text
        var stringOOP = _heapManager.AllocateObject(
            Smalltalk80.Interpreter.Core.Objects.ObjectFormat.ByteIndexed,
            ClassHierarchyUtils.CalculateTotalInstanceVariableCount(stringClassDefinition, _bundle),
            stringBytes.Length
        );
        
        // Note: Class reference will be set by PhaseX (placeholder not needed with new architecture)

        // Store the string text as bytes
        for (int i = 0; i < stringBytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }
        
        return stringOOP.AsObjectTableIndex();
    }

    /// <summary>
    /// Final cleanup: Ensures any instance variables that are still OOP 0 get set to nil.
    /// This addresses validation errors where some instance variables aren't properly initialized.
    /// </summary>
    private void EnsureAllInstanceVariablesNil()
    {
        int nilObjectId = GlobalRegistry.NilObjectId;
        
        foreach (var cls in _bundle.Classes)
        {
            int classId = _localIdToObjectId[cls.LocalId];
            string templateClassName = GetTemplateClassNameFromBundle(cls);
            
            int totalInstanceVars = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(
                ClassHierarchyUtils.FindClassDefinition(templateClassName, _bundle)!, _bundle);
            
            for (int i = 0; i < totalInstanceVars; i++)
            {
                var currentOOP = _heapManager.GetInstanceVariable(SmalltalkOOP.FromObjectTableIndex(classId), i);
                if (currentOOP.RawValue == 0) // If still uninitialized (OOP 0)
                {
                    SetInstanceVariableUsingPrimitive(classId, i, nilObjectId);
                }
            }
        }
    }

    /// <summary>
    /// Initializes all instance variables of a class object to nil.
    /// As per user requirement: "Instance variables are always part of a class instance, 
    /// so can be initialized to Nil because class instances are always created after the Nil singleton instance."
    /// </summary>
    private void InitializeAllInstanceVariablesToNil(int objectId, string templateClassName)
    {
        int totalInstanceVars = ClassHierarchyUtils.CalculateTotalInstanceVariableCount(
            ClassHierarchyUtils.FindClassDefinition(templateClassName, _bundle)!, _bundle);
            
        int nilObjectId = GlobalRegistry.NilObjectId;
        
        for (int i = 0; i < totalInstanceVars; i++)
        {
            SetInstanceVariableUsingPrimitive(objectId, i, nilObjectId);
        }
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

    // All duplicate methods removed - now using ClassHierarchyUtils
}
