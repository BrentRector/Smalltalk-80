using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;

using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 12: Global Variable Installation
/// 
/// Installs all global variables into SystemDictionary after classes are fully initialized.
/// This includes:
/// - Smalltalk -> SystemDictionary (self-reference)
/// - USTable -> Universal Symbol Table
/// - Any other system globals
/// 
/// This phase runs after all classes and methods are complete to ensure proper object references.
/// </summary>
public class Phase12_GlobalVariableInstallation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;

    public Phase12_GlobalVariableInstallation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, SmalltalkOOP> localIdToObjectId)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
    }

    public void Run()
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
        // Create symbol for #Smalltalk
        SmalltalkOOP smalltalkSymbolOOP = GetSymbolOOP("Smalltalk");

        // First, try to find an existing canonical Association created by Phase 8
        SmalltalkOOP associationOOP = FindCanonicalAssociation("Smalltalk");

        var systemDictionaryOOP = GlobalRegistry.SystemDictionaryOOP;

        if (associationOOP != SmalltalkOOP.Null)
        {
            // Found existing canonical Association - update its value
            _allocator.SetInstanceVariable(associationOOP, 1, systemDictionaryOOP);
        }
        else
        {
            // No existing Association - create new one
            associationOOP = AllocateAssociation(smalltalkSymbolOOP, systemDictionaryOOP);
        }

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, "Smalltalk");
    }

    /// <summary>
    /// Installs "USTable" as a global variable that references the Universal Symbol Table.
    /// This enables: Smalltalk at: #USTable -> Array of all symbols
    /// </summary>
    private void InstallUSTableGlobal(SmalltalkOOP ustableOOP)
    {
        // Create symbol for #USTable
        SmalltalkOOP ustableSymbolOOP = GetSymbolOOP("USTable");

        // First, try to find an existing canonical Association created by Phase 8
        SmalltalkOOP associationOOP = FindCanonicalAssociation("USTable");

        if (associationOOP != SmalltalkOOP.Null)
        {
            // Found existing canonical Association - update its value
            _allocator.SetInstanceVariable(associationOOP, 1, ustableOOP);
        }
        else
        {
            // No existing Association - create new one
            associationOOP = AllocateAssociation(ustableSymbolOOP, ustableOOP);
        }

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
        // Create symbol for #Processor
        SmalltalkOOP processorSymbolOOP = GetSymbolOOP("Processor");

        // First, try to find an existing canonical Association created by Phase 8
        SmalltalkOOP associationOOP = FindCanonicalAssociation("Processor");

        if (associationOOP != SmalltalkOOP.Null)
        {
            // Found existing canonical Association - update its value
            _allocator.SetInstanceVariable(associationOOP, 1, processorSchedulerOOP);
        }
        else
        {
            // No existing Association - create new one
            associationOOP = AllocateAssociation(processorSymbolOOP, processorSchedulerOOP);
        }

        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationOOP, "Processor");
    }

    /// <summary>
    /// Installs a class as a global variable in SystemDictionary.
    /// This enables: Smalltalk at: #ClassName -> Class object
    /// </summary>
    private void InstallClassGlobal(string className, SmalltalkOOP classOOP)
    {
        // Create symbol for the class name
        SmalltalkOOP classSymbolOOP = GetSymbolOOP(className);

        // First, try to find an existing canonical Association created by Phase 8
        SmalltalkOOP associationOOP = FindCanonicalAssociation(className);

        if (associationOOP != SmalltalkOOP.Null)
        {
            // Found existing canonical Association - update its value
            _allocator.SetInstanceVariable(associationOOP, 1, classOOP);
        }
        else
        {
            // No existing Association - create new one
            associationOOP = AllocateAssociation(classSymbolOOP, classOOP);
        }

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
    /// Searches through all compiled methods to find an existing canonical Association
    /// with the given key name. This is critical for ensuring Phase 12 updates the same
    /// Association objects created by Phase 8, rather than creating duplicate Associations.
    ///
    /// The Association canonicalization system ensures only one Association object exists
    /// per variable name across the entire system. Phase 8 creates these canonical Associations
    /// with nil values when compiling methods. Phase 12 must find and update these same
    /// Associations with proper class values.
    ///
    /// Returns SmalltalkOOP.Nil if no canonical Association exists for the key.
    /// </summary>
    private SmalltalkOOP FindCanonicalAssociation(string keyName)
    {
        // Get the symbol OOP for the key we're looking for
        SmalltalkOOP targetKeyOOP = GetSymbolOOP(keyName);

        // Scan all methods to find Association literals with matching key
        foreach (var method in _bundle.Methods)
        {
            if (method.Literals.Length == 0) continue;

            // Get the CompiledMethod OOP
            if (!_localIdToObjectId.TryGetValue(method.LocalId, out SmalltalkOOP methodOOP))
                continue;

            // Check each literal in the method
            for (int i = 0; i < method.Literals.Length; i++)
            {
                var literal = method.Literals[i];
                if (literal.Type != "association") continue;

                // Get the Association OOP from the method's literal array
                // Literals start at indexed slot 1 (slot 0 is header)
                var assocOOP = _allocator.GetIndexedOOP(methodOOP, i + 1);

                // Check if this Association has the key we're looking for
                var assocKeyOOP = _allocator.GetInstanceVariable(assocOOP, 0);
                if (assocKeyOOP == targetKeyOOP)
                {
                    // Found the canonical Association for this key
                    return assocOOP;
                }
            }
        }

        // No canonical Association found for this key
        return new SmalltalkOOP(0);
    }
}