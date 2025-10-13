using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 13: Global Variable Installation
/// 
/// Installs all global variables into SystemDictionary after classes are fully initialized.
/// This includes:
/// - Smalltalk -> SystemDictionary (self-reference)
/// - USTable -> Universal Symbol Table
/// - Any other system globals
/// 
/// This phase runs after all classes and methods are complete to ensure proper object references.
/// </summary>
public class Phase13_GlobalVariableInstallation
{
    private readonly ColdStartBundle _bundle;
    private readonly SemanticAllocator _allocator;
    private readonly Dictionary<string, int> _localIdToObjectId;

    public Phase13_GlobalVariableInstallation(
        ColdStartBundle bundle,
        SemanticAllocator allocator,
        Dictionary<string, int> localIdToObjectId)
    {
        _bundle = bundle;
        _allocator = allocator;
        _localIdToObjectId = localIdToObjectId;
    }

    public void Run()
    {
        Console.WriteLine($"DEBUG: Installing global variables in SystemDictionary...");

        int installedGlobals = 0;

        // Install Smalltalk -> SystemDictionary (self-reference)
        InstallSmalltalkGlobal();
        installedGlobals++;

        // Install USTable -> Universal Symbol Table
        if (GlobalRegistry.TryGet("USTable", out int ustableId))
        {
            InstallUSTableGlobal(ustableId);
            installedGlobals++;
        }

        // TODO: Install other system globals as needed:
        // - ProcessorScheduler (when process system is implemented)
        // - Character table, etc.

        Console.WriteLine($"DEBUG: Global variable installation complete: {installedGlobals} globals installed");
    }

    /// <summary>
    /// Installs "Smalltalk" as a global variable that references the SystemDictionary itself.
    /// This creates the self-reference: Smalltalk at: #Smalltalk -> SystemDictionary
    /// </summary>
    private void InstallSmalltalkGlobal()
    {
        // Create symbol for #Smalltalk
        int smalltalkSymbolId = GetSymbolObjectId("Smalltalk");
        
        // Create Association (#Smalltalk -> SystemDictionary)
        int systemDictionaryId = GlobalRegistry.SystemDictionaryId;
        int associationId = AllocateAssociation(smalltalkSymbolId, systemDictionaryId);
        
        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationId, "Smalltalk");
    }

    /// <summary>
    /// Installs "USTable" as a global variable that references the Universal Symbol Table.
    /// This enables: Smalltalk at: #USTable -> Array of all symbols
    /// </summary>
    private void InstallUSTableGlobal(int ustableId)
    {
        // Create symbol for #USTable
        int ustableSymbolId = GetSymbolObjectId("USTable");
        
        // Create Association (#USTable -> USTable)
        int associationId = AllocateAssociation(ustableSymbolId, ustableId);
        
        // Install in SystemDictionary
        InstallAssociationInSystemDictionary(associationId, "USTable");
    }

    /// <summary>
    /// Installs an association in the first available slot in SystemDictionary.
    /// </summary>
    private void InstallAssociationInSystemDictionary(int associationId, string globalName)
    {
        var systemDictOOP = SmalltalkOOP.FromObjectTableIndex(GlobalRegistry.SystemDictionaryId);
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        
        Console.WriteLine($"DEBUG: Searching SystemDictionary (size {systemDictSize}) for empty slot to install {globalName}");
        
        int usedSlots = 0;
        int nilObjectId = GlobalRegistry.NilObjectId;
        Console.WriteLine($"DEBUG: Looking for nil object ID {nilObjectId} in SystemDictionary slots");
        
        // Find first nil slot in SystemDictionary
        for (int i = 0; i < systemDictSize; i++)
        {
            var slotOOP = _allocator.GetIndexedOOP(systemDictOOP, i);
            int slotObjectId = slotOOP.AsObjectTableIndex();
            
            if (i < 5) // Debug first few slots
            {
                Console.WriteLine($"DEBUG: Slot {i} contains object ID {slotObjectId} (nil={nilObjectId})");
            }
            
            if (slotObjectId == nilObjectId || slotObjectId == 0)
            {
                // Found empty slot - handle both nil and any uninitialized cases during architectural transition
                var associationOOP = SmalltalkOOP.FromObjectTableIndex(associationId);
                _allocator.SetIndexedOOP(systemDictOOP, i, associationOOP);
                Console.WriteLine($"DEBUG: Installed {globalName} in SystemDictionary at slot {i} (out of {systemDictSize} slots, {usedSlots} were used)");
                return;
            }
            else
            {
                usedSlots++;
            }
        }
        
        Console.WriteLine($"DEBUG: SystemDictionary full - all {systemDictSize} slots occupied");
        throw new InvalidOperationException($"SystemDictionary is full - cannot install {globalName} global variable (all {systemDictSize} slots occupied)");
    }

    /// <summary>
    /// Gets the symbol object ID for a given symbol name.
    /// </summary>
    private int GetSymbolObjectId(string symbolName)
    {
        for (int i = 0; i < _bundle.Symbols.Length; i++)
        {
            if (_bundle.Symbols[i] == symbolName)
            {
                return SymbolRegistry.GetObjectId(i);
            }
        }
        
        throw new InvalidOperationException($"Symbol '{symbolName}' not found in symbol table");
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    private int AllocateAssociation(int keyId, int valueId)
    {
        // Find Association class definition
        var associationClassDefinition = FindClassDefinition("Association");
        if (associationClassDefinition == null)
        {
            throw new InvalidOperationException("Association class not found in bundle");
        }
        
        var associationOOP = _allocator.AllocateFixedObject("Association");
        
        // Set instance variables: [0] = key, [1] = value
        _allocator.SetInstanceVariable(associationOOP, 0, SmalltalkOOP.FromObjectTableIndex(keyId));
        _allocator.SetInstanceVariable(associationOOP, 1, SmalltalkOOP.FromObjectTableIndex(valueId));
        
        return associationOOP.AsObjectTableIndex();
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
}