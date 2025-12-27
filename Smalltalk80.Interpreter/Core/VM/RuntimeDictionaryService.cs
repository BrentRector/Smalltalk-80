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
