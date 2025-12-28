using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Runtime-safe dictionary operations for the VM.
/// Provides hash-based and linear search dictionary operations using only heap access.
/// This is the runtime equivalent of Phase5.DictionaryOperations.
/// </summary>
public class RuntimeDictionaryService
{
    private readonly ISmalltalkHeap _heap;
    private readonly SmalltalkOOP _nilOOP;

    public RuntimeDictionaryService(ISmalltalkHeap heap)
    {
        _heap = heap;
        _nilOOP = heap.GetNil();
    }

    #region Association Lookup

    /// <summary>
    /// Finds an Association by key name using linear search with byte comparison.
    /// Returns the Association OOP itself (not the value).
    /// CRITICAL: Cannot use hash-based lookup for string names because Symbol>>hash is OOP-based!
    /// </summary>
    public SmalltalkOOP FindAssociationByName(SmalltalkOOP dictOOP, string keyName)
    {
        byte[] searchBytes = Encoding.Latin1.GetBytes(keyName);
        int size = _heap.GetIndexedSize(dictOOP);

        for (int i = 0; i < size; i++)
        {
            var associationOOP = _heap.GetIndexedOOP(dictOOP, i);
            if (associationOOP == _nilOOP || associationOOP.IsSmallInteger)
                continue;

            var keyOOP = _heap.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            if (keyOOP == _nilOOP || keyOOP.IsSmallInteger)
                continue;

            // Compare Symbol bytes directly
            int symbolLength = _heap.GetIndexedSize(keyOOP);
            if (symbolLength != searchBytes.Length)
                continue;

            bool match = true;
            for (int j = 0; j < searchBytes.Length; j++)
            {
                byte symbolByte = _heap.GetIndexedByte(keyOOP, j);
                if (symbolByte != searchBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return associationOOP;
            }
        }

        return _nilOOP;
    }

    /// <summary>
    /// Finds an Association by key Symbol OOP using hash-based lookup.
    /// Returns the Association OOP itself (not the value).
    /// </summary>
    public SmalltalkOOP FindAssociationBySymbol(SmalltalkOOP dictOOP, SmalltalkOOP keySymbolOOP)
    {
        int size = _heap.GetIndexedSize(dictOOP);
        if (size == 0) return _nilOOP;

        int startIndex = IdentityHashUtility.ComputeDictionaryHashIndex(keySymbolOOP, size);
        int index = startIndex;

        do
        {
            var associationOOP = _heap.GetIndexedOOP(dictOOP, index);

            if (associationOOP == _nilOOP)
                return _nilOOP;  // Empty slot - not found

            if (!associationOOP.IsSmallInteger)
            {
                var assocKeyOOP = _heap.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                if (assocKeyOOP == keySymbolOOP)  // Identity comparison
                {
                    return associationOOP;
                }
            }

            index = (index + 1) % size;
        } while (index != startIndex);

        return _nilOOP;
    }

    /// <summary>
    /// Gets the value from an Association in a dictionary by key name.
    /// </summary>
    public SmalltalkOOP LookupValueByName(SmalltalkOOP dictOOP, string keyName)
    {
        var assocOOP = FindAssociationByName(dictOOP, keyName);
        if (assocOOP == _nilOOP)
            return _nilOOP;
        return _heap.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
    }

    /// <summary>
    /// Gets the value from an Association in a dictionary by Symbol OOP.
    /// </summary>
    public SmalltalkOOP LookupValueBySymbol(SmalltalkOOP dictOOP, SmalltalkOOP keySymbolOOP)
    {
        var assocOOP = FindAssociationBySymbol(dictOOP, keySymbolOOP);
        if (assocOOP == _nilOOP)
            return _nilOOP;
        return _heap.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
    }

    #endregion

    #region Association Installation

    /// <summary>
    /// Installs an Association in a dictionary using hash-based slot finding.
    /// Implements Dictionary>>at:put: semantics.
    /// </summary>
    public void InstallAssociation(SmalltalkOOP dictOOP, SmalltalkOOP assocOOP, SmalltalkOOP keySymbolOOP)
    {
        int dictSize = _heap.GetIndexedSize(dictOOP);

        int location = IdentityHashUtility.ComputeHashIndex(keySymbolOOP, dictSize);
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = _heap.GetIndexedOOP(dictOOP, location);

            if (probe == _nilOOP)
            {
                // Found empty slot - install
                _heap.SetIndexedOOP(dictOOP, location, assocOOP);

                // Increment tally
                if (incrementTally)
                {
                    var tallyOOP = _heap.GetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX);
                    if (tallyOOP.IsSmallInteger)
                    {
                        int newTally = tallyOOP.AsSmallInteger() + 1;
                        _heap.SetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX,
                            SmalltalkOOP.FromSmallInteger(newTally));
                    }
                }
                return;
            }

            // Check if same key already exists
            var probeKeyOOP = _heap.GetInstanceVariable(probe, VMConstants.ASSOCIATION_KEY_INDEX);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Replace existing
                _heap.SetIndexedOOP(dictOOP, location, assocOOP);
                return;
            }

            location = (location + 1) % dictSize;
            if (location == startLocation)
            {
                throw new InvalidOperationException("Dictionary is full");
            }
        }
    }

    /// <summary>
    /// Removes an Association from a dictionary.
    /// Implements Dictionary>>removeKey: with fixCollisionsFrom: for chain repair.
    /// </summary>
    public void RemoveAssociation(SmalltalkOOP dictOOP, SmalltalkOOP keySymbolOOP)
    {
        int dictSize = _heap.GetIndexedSize(dictOOP);

        int location = IdentityHashUtility.ComputeHashIndex(keySymbolOOP, dictSize);
        int startLocation = location;

        while (true)
        {
            var probe = _heap.GetIndexedOOP(dictOOP, location);

            if (probe == _nilOOP)
                return;  // Not found

            var probeKeyOOP = _heap.GetInstanceVariable(probe, VMConstants.ASSOCIATION_KEY_INDEX);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Found - remove
                _heap.SetIndexedOOP(dictOOP, location, _nilOOP);

                // Decrement tally
                var tallyOOP = _heap.GetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX);
                if (tallyOOP.IsSmallInteger)
                {
                    int newTally = tallyOOP.AsSmallInteger() - 1;
                    _heap.SetInstanceVariable(dictOOP, VMConstants.DICTIONARY_TALLY_INDEX,
                        SmalltalkOOP.FromSmallInteger(newTally));
                }

                // Fix collision chain
                FixCollisionsFrom(dictOOP, location);
                return;
            }

            location = (location + 1) % dictSize;
            if (location == startLocation)
                return;  // Not found after full scan
        }
    }

    /// <summary>
    /// Installs a key-value pair in an IdentityDictionary using hash-based slot finding.
    /// IdentityDictionary structure: [0] = tally (inherited from Set), [1] = valueArray, indexed fields = keys.
    /// Uses same algorithm as IdentityDictionary>>findKeyOrNil: so runtime lookups work.
    /// </summary>
    /// <param name="dictOOP">The IdentityDictionary to install into.</param>
    /// <param name="keyOOP">The key OOP (stored directly in indexed slots).</param>
    /// <param name="valueOOP">The value OOP (stored in parallel valueArray).</param>
    public void InstallInIdentityDictionary(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        // IdentityDictionary: tally (slot 0, inherited from Set), valueArray (slot 1), indexed fields = keys
        var tallyOOP = _heap.GetInstanceVariable(dictOOP, VMConstants.IDENTITY_DICTIONARY_TALLY_INDEX);
        var valuesArrayOOP = _heap.GetInstanceVariable(dictOOP, VMConstants.IDENTITY_DICTIONARY_VALUE_ARRAY_INDEX);

        if (valuesArrayOOP == _nilOOP)
        {
            throw new InvalidOperationException("IdentityDictionary has nil valueArray");
        }

        int dictionarySize = _heap.GetIndexedSize(dictOOP);
        int valuesArraySize = _heap.GetIndexedSize(valuesArrayOOP);

        if (dictionarySize != valuesArraySize)
        {
            throw new InvalidOperationException($"IdentityDictionary has mismatched sizes: keys={dictionarySize}, values={valuesArraySize}");
        }

        // Hash using identity hash
        int hashIndex = IdentityHashUtility.ComputeHashIndex(keyOOP, dictionarySize);

        // Linear probe from hash position to find empty slot or matching key
        int targetIndex = -1;
        int probeCount = 0;
        int index = hashIndex;

        while (probeCount < dictionarySize)
        {
            var existingKeyOOP = _heap.GetIndexedOOP(dictOOP, index);

            if (existingKeyOOP == _nilOOP)
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
            throw new InvalidOperationException($"IdentityDictionary is full after {probeCount} probes");
        }

        // Check if this is a new entry (for tally update)
        var existingKeyAtTarget = _heap.GetIndexedOOP(dictOOP, targetIndex);
        bool isNewEntry = existingKeyAtTarget == _nilOOP;

        // Install key and value
        _heap.SetIndexedOOP(dictOOP, targetIndex, keyOOP);
        _heap.SetIndexedOOP(valuesArrayOOP, targetIndex, valueOOP);

        // Update tally if we used a new slot
        if (isNewEntry)
        {
            int currentTally = tallyOOP.IsSmallInteger ? tallyOOP.AsSmallInteger() : 0;
            _heap.SetInstanceVariable(dictOOP, VMConstants.IDENTITY_DICTIONARY_TALLY_INDEX,
                SmalltalkOOP.FromSmallInteger(currentTally + 1));
        }
    }

    #endregion

    #region IdentityDictionary Lookup

    /// <summary>
    /// Looks up a value in an IdentityDictionary by key OOP using hash-based lookup.
    /// IdentityDictionary structure: indexed slots = keys, valueArray (slot 1) = values.
    /// Used by MethodDictionary lookup where keys are selector Symbols and values are CompiledMethods.
    /// </summary>
    /// <param name="dictOOP">The IdentityDictionary to search.</param>
    /// <param name="keyOOP">The key OOP to find.</param>
    /// <returns>The value OOP if found, nil otherwise.</returns>
    public SmalltalkOOP LookupInIdentityDictionary(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
    {
        var valuesArrayOOP = _heap.GetInstanceVariable(dictOOP, VMConstants.IDENTITY_DICTIONARY_VALUE_ARRAY_INDEX);
        if (valuesArrayOOP == _nilOOP)
            return _nilOOP;

        int dictSize = _heap.GetIndexedSize(dictOOP);
        if (dictSize == 0)
            return _nilOOP;

        int hashIndex = IdentityHashUtility.ComputeHashIndex(keyOOP, dictSize);
        int index = hashIndex;
        int probeCount = 0;

        while (probeCount < dictSize)
        {
            var currentKeyOOP = _heap.GetIndexedOOP(dictOOP, index);

            if (currentKeyOOP == _nilOOP)
                return _nilOOP;  // Empty slot - not found

            if (currentKeyOOP == keyOOP)
            {
                // Found! Return corresponding value
                return _heap.GetIndexedOOP(valuesArrayOOP, index);
            }

            // Linear probe to next slot
            index = (index + 1) % dictSize;
            probeCount++;
        }

        return _nilOOP;
    }

    #endregion

    #region Collision Chain Repair

    /// <summary>
    /// Repairs the linear probing chain after removing an entry.
    /// Implements Dictionary>>fixCollisionsFrom: semantics.
    /// </summary>
    public void FixCollisionsFrom(SmalltalkOOP dictOOP, int startIndex)
    {
        int dictSize = _heap.GetIndexedSize(dictOOP);
        int oldIndex = startIndex;

        while (true)
        {
            oldIndex = (oldIndex + 1) % dictSize;
            var nextObject = _heap.GetIndexedOOP(dictOOP, oldIndex);

            if (nextObject == _nilOOP)
                break;

            var keyOOP = _heap.GetInstanceVariable(nextObject, VMConstants.ASSOCIATION_KEY_INDEX);
            int nextIndex = IdentityHashUtility.ComputeHashIndex(keyOOP, dictSize);

            // Linear probe to find correct slot
            int probeCount = 0;
            while (true)
            {
                var probe = _heap.GetIndexedOOP(dictOOP, nextIndex);

                if (probe == _nilOOP || probe == nextObject)
                    break;

                var probeKeyOOP = _heap.GetInstanceVariable(probe, VMConstants.ASSOCIATION_KEY_INDEX);
                if (probeKeyOOP == keyOOP)
                    break;

                nextIndex = (nextIndex + 1) % dictSize;
                probeCount++;

                if (probeCount >= dictSize)
                    throw new InvalidOperationException("FixCollisionsFrom: Cannot find slot");
            }

            // Move if displaced
            if (nextIndex != oldIndex)
            {
                _heap.SetIndexedOOP(dictOOP, nextIndex, nextObject);
                _heap.SetIndexedOOP(dictOOP, oldIndex, _nilOOP);
            }
        }
    }

    #endregion
}
