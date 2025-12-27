using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase3;

/// <summary>
/// Handles hash-based dictionary operations for Phase 3 object allocation.
/// Uses RuntimeDictionaryService for hash-based operations and ObjectGraphAllocator for object creation.
/// </summary>
internal class DictionaryInstaller(
    ObjectGraphAllocator allocator,
    RuntimeDictionaryService dictService,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
    Func<string, SmalltalkOOP> symbolInternerFunc)
{
    /// <summary>
    /// Allocates an Association object (key-value pair) using ObjectGraphAllocator.
    /// </summary>
    public SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = allocator.AllocateFixedObject("Association");

        // Set Association class reference
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        allocator.SetClassOOP(associationOOP, associationClassOOP);

        // Set instance variables using VMConstants
        allocator.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX, keyOOP);
        allocator.SetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Installs an association in SystemDictionary using hash-based slot finding.
    /// Delegates to RuntimeDictionaryService.
    /// </summary>
    public void InstallInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        var keySymbolOOP = allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
        dictService.InstallAssociation(systemDictOOP, associationOOP, keySymbolOOP);
    }

    /// <summary>
    /// Gets the identity hash of a symbol using Primitive 75 semantics.
    /// </summary>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        return IdentityHashUtility.ComputeIdentityHash(symbolOOP);
    }

    /// <summary>
    /// Installs an Association in any Dictionary object.
    /// Delegates to RuntimeDictionaryService.
    /// </summary>
    public void InstallInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string name)
    {
        var keySymbolOOP = allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
        dictService.InstallAssociation(dictionaryOOP, associationOOP, keySymbolOOP);
    }

    /// <summary>
    /// Finds an Association by key in all allocated objects and updates its value.
    /// This is a cold-start specific operation that searches through all objects.
    /// </summary>
    public bool FindAndUpdateAssociationValue(string keyName, SmalltalkOOP newValueOOP)
    {
        var keySymbol = symbolInternerFunc(keyName);
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");

        // Search through all allocated objects
        foreach (var kvp in localIdToObjectId)
        {
            var objectOOP = kvp.Value;

            try
            {
                // Check if this is an Association object
                var classOOP = allocator.GetClassOOP(objectOOP);
                if (classOOP == associationClassOOP)
                {
                    // Get the key
                    var assocKeyOOP = allocator.GetInstanceVariable(objectOOP, VMConstants.ASSOCIATION_KEY_INDEX);

                    // Check if key matches
                    if (assocKeyOOP == keySymbol)
                    {
                        // Update the value
                        allocator.SetInstanceVariable(objectOOP, VMConstants.ASSOCIATION_VALUE_INDEX, newValueOOP);
                        initializedFields.Add((objectOOP, VMConstants.ASSOCIATION_VALUE_INDEX));
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
}
