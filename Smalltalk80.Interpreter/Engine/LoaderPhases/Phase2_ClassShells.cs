using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Objects;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 2: Allocate Class Shells (renamed from Phase1 to match loader sequence)
/// Creates empty class objects for all classes in the bundle.
/// These are placeholder objects that will be populated in subsequent phases.
/// Uses the new heap manager with low-level object allocation.
/// </summary>
public class Phase2_ClassShells
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;

    public Phase2_ClassShells(ColdStartBundle bundle, SemanticAllocator allocator, Dictionary<string, SmalltalkOOP> localIdMap)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdMap;
    }

    public void Run()
    {
        // Phase 2: Allocate class/metaclass shells AND SystemDictionary (for now - to maintain working object ID sequence)
        // TODO: Move SystemDictionary to Phase 6 after eliminating all object ID dependencies
        
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

        // Note: Special key "__smalltalk_next_index__" removed - no longer needed with SmalltalkOOP
    }

    /// <summary>
    /// Decodes object format from JSON bundle format field using the project's encoding.
    /// NOT Blue Book encoding - uses the project's format word encoding from ObjectFormatValidator.
    /// </summary>
    private Smalltalk80.Interpreter.Core.Objects.ObjectFormat DecodeObjectFormat(int formatValue)
    {
        // Project-specific format word interpretation (from ObjectFormatValidator)
        var instanceSpec = (formatValue >> 12) & 0x03;
        var isIndexable = (formatValue & 0x10) != 0;
        var hasPointers = (formatValue & 0x08) != 0;
        
        // Decode based on instanceSpec and flags
        return instanceSpec switch
        {
            0x00 => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.Pointer, // Fixed pointer objects
            0x01 => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.Pointer, // Fixed pointer objects
            0x02 when hasPointers && isIndexable => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.PointerIndexed, // Variable pointer objects
            0x02 when hasPointers && !isIndexable => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.Pointer, // Fixed pointer objects
            0x02 when !hasPointers && isIndexable => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.ByteIndexed, // ByteIndexed objects
            0x02 when !hasPointers && !isIndexable => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.WordIndexed, // Word objects
            0x03 => Smalltalk80.Interpreter.Core.Objects.ObjectFormat.ByteIndexed, // Byte objects
            _ => throw new InvalidOperationException($"Unknown format instanceSpec: {instanceSpec:X2}, indexable={isIndexable}, pointers={hasPointers}")
        };
    }


    /// <summary>
    /// Interns a symbol using the symbol table from Phase 1.
    /// During Phase 2, symbols already exist so we look them up in SymbolRegistry.
    /// </summary>
    private SmalltalkOOP InternSymbol(string name)
    {
        // Find the symbol in the SymbolRegistry (created in Phase 1)
        // This is a simplified lookup - in practice we'd need to search through all symbols
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
    /// Allocates an Association object (key-value pair).
    /// Association has 2 instance variables: key and value.
    /// </summary>
    private SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");
        
        // Set instance variables: [0] = key (from LookupKey), [1] = value (from Association)
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);
        
        return associationOOP;
    }

    /// <summary>
    /// Sets an indexed field in an object.
    /// </summary>
    private void SetIndexedField(SmalltalkOOP objectOOP, int index, SmalltalkOOP valueOOP)
    {
        _allocator.SetIndexedOOP(objectOOP, index, valueOOP);
    }

    // FindClassDefinition method removed - now using ClassHierarchyUtils.FindClassDefinition

    // Methods removed - now using ClassHierarchyUtils

    private string GetClassNameFromLocalId(string localId)
    {
        // LocalId format: "class:ClassName" or "meta:ClassName"
        int colonIndex = localId.IndexOf(':');
        return colonIndex >= 0 ? localId[(colonIndex + 1)..] : localId;
    }
}
