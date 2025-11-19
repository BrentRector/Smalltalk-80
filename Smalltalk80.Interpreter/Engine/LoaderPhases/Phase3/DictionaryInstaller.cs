using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase3;

/// <summary>
/// Handles hash-based dictionary operations for Phase 3 object allocation.
/// Implements Association installation in SystemDictionary and other dictionaries
/// using the same algorithms as Smalltalk-80 Dictionary>>findKeyOrNil:.
/// </summary>
internal class DictionaryInstaller(
    ObjectGraphAllocator allocator,
    ISmalltalkHeapManager heapManager,
    ColdStartBundle bundle,
    Dictionary<string, SmalltalkOOP> localIdToObjectId,
    HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
    Func<string, SmalltalkOOP> symbolInternerFunc)
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
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = allocator.GetIndexedSize(systemDictOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup to find correct slot (same algorithm as Dictionary>>findKeyOrNil:)
        // Smalltalk: location _ key hash \\ length + 1 (then convert to 0-based index)
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % systemDictSize + systemDictSize) % systemDictSize; // Handle negative hash

        // Probe for nil slot or matching key (linear probing)
        int pass = 1;
        int startLocation = location;
        while (true)
        {
            var probe = allocator.GetIndexedOOP(systemDictOOP, location);

            if (probe == nilOOP)
            {
                // Found empty slot - install association
                allocator.SetIndexedOOP(systemDictOOP, location, associationOOP);
                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Slot already contains Association for this key - update it
                allocator.SetIndexedOOP(systemDictOOP, location, associationOOP);
                return;
            }

            // Linear probe to next slot
            location = (location + 1) % systemDictSize;

            // Check if we've wrapped around
            if (location == startLocation)
            {
                pass++;
                if (pass > 2)
                {
                    throw new InvalidOperationException($"SystemDictionary is full - cannot install {name} association after 2 passes");
                }
            }
        }
    }

    /// <summary>
    /// Gets the identity hash of a symbol using Primitive 75 semantics.
    /// Symbol>>hash returns half the object pointer - an IDENTITY hash based on OOP.
    /// </summary>
    /// <param name="symbolOOP">The symbol OOP to hash.</param>
    /// <returns>The identity hash value.</returns>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        // Symbol>>hash uses Primitive 75: returns half the object pointer
        // Blue Book: "Answer with a SmallInteger whose value is half of the receiver's object pointer"
        // This is an IDENTITY hash based on OOP, NOT a string content hash!
        return IdentityHashUtility.ComputeIdentityHash(symbolOOP);
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
        int dictSize = allocator.GetIndexedSize(dictionaryOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup (same algorithm as Dictionary>>findKeyOrNil:)
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

        // Linear probing
        int pass = 1;
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Found empty slot - install association
                allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                // Increment tally field
                if (incrementTally)
                {
                    int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", bundle);
                    if (tallyIndex >= 0)
                    {
                        var currentTallyOOP = allocator.GetInstanceVariable(dictionaryOOP, tallyIndex);
                        int currentTally = currentTallyOOP.IsSmallInteger ? currentTallyOOP.AsSmallInteger() : 0;
                        allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(currentTally + 1));
                    }
                }

                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Update existing Association
                allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                incrementTally = false;
                return;
            }

            // Linear probe
            location = (location + 1) % dictSize;

            if (location == startLocation)
            {
                pass++;
                if (pass > 2)
                {
                    throw new InvalidOperationException($"Dictionary is full - cannot install {name} association after 2 passes");
                }
            }
        }
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
