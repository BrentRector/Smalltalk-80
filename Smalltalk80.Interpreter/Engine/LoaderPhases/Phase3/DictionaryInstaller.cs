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
        return dictionaryService.AllocateAssociation(keyOOP, valueOOP);
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
        var result = dictionaryService.FindAndUpdateAssociationValue(
            keyName, newValueOOP, localIdToObjectId, symbolInternerFunc);

        if (result.HasValue)
        {
            initializedFields.Add(result.Value);
            return true;
        }

        return false;
    }
}
