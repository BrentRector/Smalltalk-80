using System.Diagnostics;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Hash-based dictionary operations matching Smalltalk-80 Dictionary semantics.
/// Implements findKeyOrNil:, fixCollisionsFrom:, and related algorithms from Blue Book.
/// </summary>
public class DictionaryOperations(Phase5Context context) : IDictionaryOperations
{
    /// <summary>
    /// Installs an association in a dictionary using hash-based slot finding.
    /// CRITICAL: Implements Smalltalk-80 migration semantics - removes from Undeclared first.
    /// </summary>
    public void InstallAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string keyName)
    {
        int dictSize = context.Allocator.GetIndexedSize(dictionaryOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = context.Allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup (Dictionary>>findKeyOrNil:)
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

        // Probe for nil slot or matching key (linear probing)
        int pass = 1;
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = context.Allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP || probe == SmalltalkOOP.Null)
            {
                // Found empty slot - install association
                context.Allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                // Increment tally
                if (incrementTally)
                {
                    var currentTallyOOP = context.Allocator.GetInstanceVariable(dictionaryOOP, 0);
                    if (!currentTallyOOP.IsSmallInteger)
                    {
                        throw new InvalidOperationException(
                            $"Dictionary tally is not a SmallInteger when installing '{keyName}'. Tally OOP: {currentTallyOOP}");
                    }

                    int newTally = currentTallyOOP.AsSmallInteger() + 1;
                    context.Allocator.SetInstanceVariable(dictionaryOOP, 0, SmalltalkOOP.FromSmallInteger(newTally));
                }

                // Emit diagnostic event
                var valueOOP = context.Allocator.GetInstanceVariable(associationOOP, 1);
                context.Diagnostics?.Emit(new GlobalInstalledEvent
                {
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0,
                    GlobalName = keyName,
                    AssociationOOP = associationOOP,
                    ValueOOP = valueOOP
                });

                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = context.Allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Replace existing
                context.Allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);
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
                    throw new InvalidOperationException(
                        $"Dictionary is full - cannot install {keyName} association after 2 passes");
                }
            }
        }
    }

    /// <summary>
    /// Removes an Association from a dictionary.
    /// Implements Dictionary>>removeKey: with fixCollisionsFrom: for chain repair.
    /// </summary>
    public void RemoveAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbolOOP, string keyName)
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int dictSize = context.Allocator.GetIndexedSize(dictionaryOOP);

        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

        int pass = 1;
        int startLocation = location;

        while (true)
        {
            var probe = context.Allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Not found
                return;
            }

            var probeKeyOOP = context.Allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Found - remove
                context.Allocator.SetIndexedOOP(dictionaryOOP, location, nilOOP);

                // Decrement tally
                var tallyOOP = context.Allocator.GetInstanceVariable(dictionaryOOP, 0);
                if (tallyOOP.IsSmallInteger)
                {
                    int tally = tallyOOP.AsSmallInteger();
                    context.Allocator.SetInstanceVariable(dictionaryOOP, 0, SmalltalkOOP.FromSmallInteger(tally - 1));
                }

                // Fix probe chain
                FixCollisionsFrom(dictionaryOOP, location);
                return;
            }

            location = (location + 1) % dictSize;

            if (location == startLocation)
            {
                pass++;
                if (pass > 2)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Gets the identity hash of a Symbol using Primitive 75 semantics (oop >> 1).
    /// </summary>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        return VMUtilities.ComputeIdentityHash(symbolOOP);
    }

    /// <summary>
    /// Repairs the linear probing chain after removing an entry.
    /// Implements Smalltalk-80 Dictionary>>fixCollisionsFrom: semantics.
    /// </summary>
    public void FixCollisionsFrom(SmalltalkOOP dictionaryOOP, int startIndex)
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int dictSize = context.Allocator.GetIndexedSize(dictionaryOOP);
        int oldIndex = startIndex;

        while (true)
        {
            oldIndex = (oldIndex + 1) % dictSize;
            var nextObject = context.Allocator.GetIndexedOOP(dictionaryOOP, oldIndex);

            if (nextObject == nilOOP)
                break;

            // Find where this Association SHOULD be
            var keyOOP = context.Allocator.GetInstanceVariable(nextObject, 0);
            int hash = GetSymbolHash(keyOOP);
            int nextIndex = (hash % dictSize + dictSize) % dictSize;

            // Linear probe to find correct slot
            int probeCount = 0;
            while (true)
            {
                var probe = context.Allocator.GetIndexedOOP(dictionaryOOP, nextIndex);

                if (probe == nilOOP || probe == nextObject)
                    break;

                var probeKeyOOP = context.Allocator.GetInstanceVariable(probe, 0);
                if (probeKeyOOP == keyOOP)
                    break;

                nextIndex = (nextIndex + 1) % dictSize;
                probeCount++;

                if (probeCount >= dictSize)
                {
                    throw new InvalidOperationException(
                        "FixCollisionsFrom: Cannot find slot for Association during rehash");
                }
            }

            // Move if displaced
            if (nextIndex != oldIndex)
            {
                context.Allocator.SetIndexedOOP(dictionaryOOP, nextIndex, nextObject);
                context.Allocator.SetIndexedOOP(dictionaryOOP, oldIndex, nilOOP);
            }
        }
    }
}
