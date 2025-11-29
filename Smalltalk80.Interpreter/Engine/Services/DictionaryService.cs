using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides dictionary operations for cold start phases.
/// Implements hash-based installation using the same algorithm as Smalltalk-80 Dictionary>>findKeyOrNil:.
/// </summary>
/// <remarks>
/// This service handles Association allocation and dictionary installation operations.
/// </remarks>
public class DictionaryService
{
    private readonly ObjectGraphAllocator _allocator;
    private readonly ColdStartBundle _bundle;

    /// <summary>
    /// Initializes a new instance of the DictionaryService class.
    /// </summary>
    /// <param name="allocator">The object graph allocator for memory operations.</param>
    /// <param name="bundle">The cold start bundle for class hierarchy lookups.</param>
    public DictionaryService(ObjectGraphAllocator allocator, ColdStartBundle bundle)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
    }

    /// <summary>
    /// Gets the identity hash of a symbol using Primitive 75 semantics.
    /// Symbol>>hash returns half the object pointer - an IDENTITY hash based on OOP.
    /// </summary>
    /// <param name="symbolOOP">The symbol OOP to hash.</param>
    /// <returns>The identity hash value.</returns>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        return IdentityHashUtility.ComputeIdentityHash(symbolOOP);
    }

    /// <summary>
    /// Computes the 0-indexed hash slot for a key in a dictionary of given size.
    /// Delegates to IdentityHashUtility for the canonical implementation.
    /// </summary>
    /// <param name="keyOOP">The key OOP to hash.</param>
    /// <param name="dictionarySize">The size of the dictionary's indexed storage.</param>
    /// <returns>The 0-indexed slot position.</returns>
    public int ComputeHashIndex(SmalltalkOOP keyOOP, int dictionarySize)
    {
        return IdentityHashUtility.ComputeHashIndex(keyOOP, dictionarySize);
    }

    /// <summary>
    /// Allocates an Association object (key-value pair) using ObjectGraphAllocator.
    /// </summary>
    /// <param name="keyOOP">The symbol key for the Association.</param>
    /// <param name="valueOOP">The value for the Association.</param>
    /// <returns>The newly allocated Association OOP.</returns>
    public SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");

        // Set Association class reference
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");
        _allocator.SetClassOOP(associationOOP, associationClassOOP);

        // Set instance variables: [0] = key, [1] = value (assuming standard Association layout)
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Finds an Association by key in all allocated objects and updates its value.
    /// Searches through all objects looking for Association instances with matching key.
    /// </summary>
    /// <param name="keyName">The key name to search for.</param>
    /// <param name="newValueOOP">The new value to set.</param>
    /// <param name="localIdToObjectId">The mapping from local IDs to allocated OOPs.</param>
    /// <param name="symbolInternerFunc">Function to intern symbols.</param>
    /// <returns>The (oop, slot) tuple if found and updated, or null if not found.</returns>
    public (SmalltalkOOP oop, int slot)? FindAndUpdateAssociationValue(
        string keyName,
        SmalltalkOOP newValueOOP,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        Func<string, SmalltalkOOP> symbolInternerFunc)
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
                var classOOP = _allocator.GetClassOOP(objectOOP);
                if (classOOP == associationClassOOP)
                {
                    // Get the key (instance variable 0)
                    var assocKeyOOP = _allocator.GetInstanceVariable(objectOOP, 0);

                    // Check if key matches
                    if (assocKeyOOP == keySymbol)
                    {
                        // Update the value (instance variable 1)
                        _allocator.SetInstanceVariable(objectOOP, 1, newValueOOP);
                        return (objectOOP, 1);
                    }
                }
            }
            catch
            {
                // Skip objects that don't have the expected structure
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Installs an Association in SystemDictionary using hash-based slot finding.
    /// CRITICAL: Uses same algorithm as Dictionary>>findKeyOrNil: so runtime lookups work!
    /// </summary>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        SmalltalkOOP nilOOP = _allocator.GetNil();

        // Get the key symbol from the Association (instance variable 0)
        var keySymbolOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup (same algorithm as Dictionary>>findKeyOrNil:)
        int location = ComputeHashIndex(keySymbolOOP, systemDictSize);

        // Linear probing
        int pass = 1;
        int startLocation = location;
        while (true)
        {
            var probe = _allocator.GetIndexedOOP(systemDictOOP, location);

            if (probe == nilOOP)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(systemDictOOP, location, associationOOP);
                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = _allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Update existing
                _allocator.SetIndexedOOP(systemDictOOP, location, associationOOP);
                return;
            }

            // Linear probe
            location = (location + 1) % systemDictSize;

            if (location == startLocation)
            {
                pass++;
                if (pass > 2)
                {
                    throw new InvalidOperationException($"SystemDictionary is full - cannot install {name} association");
                }
            }
        }
    }

    /// <summary>
    /// Installs a key-value pair in an IdentityDictionary using hash-based slot finding.
    /// IdentityDictionary structure: [0] = valueArray, [1] = tally, indexed fields = keys.
    /// Uses same algorithm as IdentityDictionary>>findKeyOrNil: so runtime lookups work!
    /// </summary>
    /// <param name="dictionaryOOP">The IdentityDictionary to install into.</param>
    /// <param name="keyOOP">The key OOP (stored directly in indexed slots).</param>
    /// <param name="valueOOP">The value OOP (stored in parallel valueArray).</param>
    /// <param name="name">The key name for diagnostics.</param>
    /// <exception cref="InvalidOperationException">Thrown when dictionary is full or has invalid structure.</exception>
    public void InstallInIdentityDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP keyOOP, SmalltalkOOP valueOOP, string name)
    {
        // IdentityDictionary: [0] = valueArray, [1] = tally, indexed fields = keys
        var valuesArrayOOP = _allocator.GetInstanceVariable(dictionaryOOP, 0);
        var tallyOOP = _allocator.GetInstanceVariable(dictionaryOOP, 1);
        SmalltalkOOP nilOOP = _allocator.GetNil();

        if (valuesArrayOOP == nilOOP)
        {
            throw new InvalidOperationException($"IdentityDictionary has nil valueArray - cannot install {name}");
        }

        int dictionarySize = _allocator.GetIndexedSize(dictionaryOOP);
        int valuesArraySize = _allocator.GetIndexedSize(valuesArrayOOP);

        if (dictionarySize != valuesArraySize)
        {
            throw new InvalidOperationException($"IdentityDictionary has mismatched sizes: keys={dictionarySize}, values={valuesArraySize}");
        }

        // Hash using identity hash: index = key asOop \\ length (0-indexed in C#)
        int hashIndex = ComputeHashIndex(keyOOP, dictionarySize);

        // Linear probe from hash position to find empty slot or matching key
        int targetIndex = -1;
        int probeCount = 0;
        int index = hashIndex;

        while (probeCount < dictionarySize)
        {
            var existingKeyOOP = _allocator.GetIndexedOOP(dictionaryOOP, index);

            if (existingKeyOOP == nilOOP)
            {
                // Empty slot found
                targetIndex = index;
                break;
            }
            else if (existingKeyOOP == keyOOP)
            {
                // Existing entry for this key - replace it
                targetIndex = index;
                break;
            }

            // Linear probe: move to next slot, wrap around
            index = (index + 1) % dictionarySize;
            probeCount++;
        }

        if (targetIndex == -1)
        {
            throw new InvalidOperationException($"IdentityDictionary is full - cannot install {name} after {probeCount} probes");
        }

        // Check if this is a new entry (for tally update)
        var existingKeyAtTarget = _allocator.GetIndexedOOP(dictionaryOOP, targetIndex);
        bool isNewEntry = existingKeyAtTarget == nilOOP;

        // Install key and value
        _allocator.SetIndexedOOP(dictionaryOOP, targetIndex, keyOOP);
        _allocator.SetIndexedOOP(valuesArrayOOP, targetIndex, valueOOP);

        // Update tally if we used a new slot
        if (isNewEntry)
        {
            int currentTally = tallyOOP.IsSmallInteger ? tallyOOP.AsSmallInteger() : 0;
            _allocator.SetInstanceVariable(dictionaryOOP, 1, SmalltalkOOP.FromSmallInteger(currentTally + 1));
        }
    }

    /// <summary>
    /// Installs an Association in any Dictionary object.
    /// Uses hash-based lookup to match Dictionary>>findKeyOrNil: algorithm.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to install into.</param>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string name)
    {
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);
        SmalltalkOOP nilOOP = _allocator.GetNil();

        // Get the key symbol from the Association (instance variable 0)
        var keySymbolOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup
        int location = ComputeHashIndex(keySymbolOOP, dictSize);

        // Linear probing
        int pass = 1;
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = _allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                // Increment tally field
                if (incrementTally)
                {
                    int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
                    if (tallyIndex >= 0)
                    {
                        var currentTallyOOP = _allocator.GetInstanceVariable(dictionaryOOP, tallyIndex);
                        int currentTally = currentTallyOOP.IsSmallInteger ? currentTallyOOP.AsSmallInteger() : 0;
                        _allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(currentTally + 1));
                    }
                }

                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = _allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Update existing
                _allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);
                return;
            }

            // Linear probe
            location = (location + 1) % dictSize;

            if (location == startLocation)
            {
                pass++;
                if (pass > 2)
                {
                    throw new InvalidOperationException($"Dictionary is full - cannot install {name} association");
                }
            }
        }
    }
}
