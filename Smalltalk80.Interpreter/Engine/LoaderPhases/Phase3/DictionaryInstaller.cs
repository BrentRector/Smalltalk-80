using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Engine.Services;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase3;

/// <summary>
/// Handles hash-based dictionary operations for Phase 3 object allocation.
/// Implements Association installation in SystemDictionary and other dictionaries
/// using the same algorithms as Smalltalk-80 Dictionary>>findKeyOrNil:.
/// </summary>
internal class DictionaryInstaller(
    ObjectGraphAllocator allocator,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
    Func<string, SmalltalkOOP> symbolInternerFunc,
    DictionaryService dictionaryService)
{
    /// <summary>
    /// Allocates an Association object (key-value pair) using ObjectGraphAllocator.
    /// </summary>
    /// <param name="keyOOP">The symbol key for the Association.</param>
    /// <param name="valueOOP">The value for the Association.</param>
    /// <returns>The newly allocated Association OOP.</returns>
    public SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = allocator.AllocateFixedObject("Association");

        // Set instance variables: [0] = key, [1] = value (assuming standard Association layout)
        allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Installs an association in SystemDictionary using hash-based slot finding.
    /// CRITICAL: Must use same algorithm as Dictionary>>findKeyOrNil: so runtime lookups work!
    /// </summary>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        dictionaryService.InstallInSystemDictionary(associationOOP, name);
    }

    /// <summary>
    /// Gets the identity hash of a symbol using Primitive 75 semantics.
    /// Symbol>>hash returns half the object pointer - an IDENTITY hash based on OOP.
    /// </summary>
    /// <param name="symbolOOP">The symbol OOP to hash.</param>
    /// <returns>The identity hash value.</returns>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        return dictionaryService.GetSymbolHash(symbolOOP);
    }

    /// <summary>
    /// Installs an Association in any Dictionary object (including pool dictionaries and Undeclared).
    /// Uses hash-based lookup to match Dictionary>>findKeyOrNil: algorithm.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to install into.</param>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string name)
    {
        dictionaryService.InstallInDictionary(dictionaryOOP, associationOOP, name);
    }

    /// <summary>
    /// Finds an Association by key in all allocated objects and updates its value.
    /// Searches through all objects looking for Association instances with matching key.
    /// </summary>
    /// <param name="keyName">The key name to search for.</param>
    /// <param name="newValueOOP">The new value to set.</param>
    /// <returns>True if Association was found and updated, false otherwise.</returns>
    public bool FindAndUpdateAssociationValue(string keyName, SmalltalkOOP newValueOOP)
    {
        var keySymbol = symbolInternerFunc(keyName);
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");

        // Search through all allocated objects (both bundle objects and sentinel objects)
        foreach (var kvp in localIdToObjectId)
        {
            var objectOOP = kvp.Value;

            try
            {
                // Check if this is an Association object
                var classOOP = allocator.GetSlot(objectOOP, 0);
                if (classOOP == associationClassOOP)
                {
                    // Get the key (instance variable 0)
                    var assocKeyOOP = allocator.GetInstanceVariable(objectOOP, 0);

                    // Check if key matches
                    if (assocKeyOOP == keySymbol)
                    {
                        // Update the value (instance variable 1)
                        allocator.SetInstanceVariable(objectOOP, 1, newValueOOP);
                        initializedFields.Add((objectOOP, 1));
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
