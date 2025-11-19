using System.Diagnostics;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides centralized hash-based dictionary operations for cold start phases.
/// Consolidates duplicated dictionary logic from Phase3/DictionaryInstaller
/// and Phase5/DictionaryOperations.
/// </summary>
/// <remarks>
/// Implements Smalltalk-80 Dictionary semantics including:
/// - Hash-based slot finding using identity hash (Primitive 75)
/// - Linear probing for collision resolution
/// - Tally management
/// - Collision repair during removal (fixCollisionsFrom:)
/// </remarks>
public class DictionaryService
{
    private readonly ObjectGraphAllocator _allocator;
    private readonly ColdStartBundle _bundle;
    private readonly Dictionary<string, SmalltalkOOP> _localIdToObjectId;
    private readonly HashSet<(SmalltalkOOP oop, int slot)> _initializedFields;
    private readonly SymbolService _symbolService;
    private readonly IDiagnosticHub? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the DictionaryService class.
    /// </summary>
    /// <param name="allocator">The object graph allocator for creating objects.</param>
    /// <param name="bundle">The cold start bundle containing class and object data.</param>
    /// <param name="localIdToObjectId">Mapping from local IDs to allocated object OOPs.</param>
    /// <param name="initializedFields">Set tracking which fields have been initialized.</param>
    /// <param name="symbolService">The symbol service for symbol lookups.</param>
    /// <param name="diagnostics">Optional diagnostic hub for event emission.</param>
    public DictionaryService(
        ObjectGraphAllocator allocator,
        ColdStartBundle bundle,
        Dictionary<string, SmalltalkOOP> localIdToObjectId,
        HashSet<(SmalltalkOOP oop, int slot)> initializedFields,
        SymbolService symbolService,
        IDiagnosticHub? diagnostics = null)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
        _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
        _initializedFields = initializedFields ?? throw new ArgumentNullException(nameof(initializedFields));
        _symbolService = symbolService ?? throw new ArgumentNullException(nameof(symbolService));
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    /// </summary>
    /// <param name="keyOOP">The symbol key for the Association.</param>
    /// <param name="valueOOP">The value for the Association.</param>
    /// <returns>The newly allocated Association OOP.</returns>
    public SmalltalkOOP AllocateAssociation(SmalltalkOOP keyOOP, SmalltalkOOP valueOOP)
    {
        var associationOOP = _allocator.AllocateFixedObject("Association");

        // Set instance variables: [0] = key, [1] = value (standard Association layout)
        _allocator.SetInstanceVariable(associationOOP, 0, keyOOP);
        _allocator.SetInstanceVariable(associationOOP, 1, valueOOP);

        return associationOOP;
    }

    /// <summary>
    /// Installs an association in a dictionary using hash-based slot finding.
    /// Uses the same algorithm as Smalltalk-80 Dictionary>>findKeyOrNil:.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to install into.</param>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="keyName">The key name for diagnostics.</param>
    /// <exception cref="InvalidOperationException">Thrown when dictionary is full.</exception>
    public void InstallAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string keyName)
    {
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup (Dictionary>>findKeyOrNil:)
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize; // Handle negative hash

        // Probe for nil slot or matching key (linear probing)
        int pass = 1;
        int startLocation = location;
        bool incrementTally = true;

        while (true)
        {
            var probe = _allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP || probe == SmalltalkOOP.Null)
            {
                // Found empty slot - install association
                _allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);

                // Increment tally field
                if (incrementTally)
                {
                    IncrementDictionaryTally(dictionaryOOP, keyName);
                }

                // Emit diagnostic event
                EmitGlobalInstalledEvent(associationOOP, keyName);
                return;
            }

            // Check if Association with same key already exists
            var probeKeyOOP = _allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Replace existing Association
                _allocator.SetIndexedOOP(dictionaryOOP, location, associationOOP);
                return;
            }

            // Linear probe to next slot
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
    /// Installs an association in SystemDictionary.
    /// </summary>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="keyName">The key name for diagnostics.</param>
    public void InstallInSystemDictionary(SmalltalkOOP associationOOP, string keyName)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        InstallAssociation(systemDictOOP, associationOOP, keyName);
    }

    /// <summary>
    /// Removes an Association from a dictionary.
    /// Implements Dictionary>>removeKey: with fixCollisionsFrom: for chain repair.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to remove from.</param>
    /// <param name="keySymbolOOP">The key symbol of the Association to remove.</param>
    /// <param name="keyName">The key name for diagnostics.</param>
    public void RemoveAssociation(SmalltalkOOP dictionaryOOP, SmalltalkOOP keySymbolOOP, string keyName)
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);

        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

        int pass = 1;
        int startLocation = location;

        while (true)
        {
            var probe = _allocator.GetIndexedOOP(dictionaryOOP, location);

            if (probe == nilOOP)
            {
                // Not found
                return;
            }

            var probeKeyOOP = _allocator.GetInstanceVariable(probe, 0);
            if (probeKeyOOP == keySymbolOOP)
            {
                // Found - remove
                _allocator.SetIndexedOOP(dictionaryOOP, location, nilOOP);

                // Decrement tally
                DecrementDictionaryTally(dictionaryOOP);

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
    /// Gets the identity hash of a symbol using Primitive 75 semantics.
    /// Symbol>>hash returns half the object pointer - an IDENTITY hash based on OOP.
    /// </summary>
    /// <param name="symbolOOP">The symbol OOP to hash.</param>
    /// <returns>The identity hash value.</returns>
    public int GetSymbolHash(SmalltalkOOP symbolOOP)
    {
        // Symbol>>hash uses Primitive 75: returns half the object pointer
        // Blue Book: "Answer with a SmallInteger whose value is half of the receiver's object pointer"
        return IdentityHashUtility.ComputeIdentityHash(symbolOOP);
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
        var keySymbol = _symbolService.InternSymbol(keyName);
        var associationClassOOP = GlobalRegistry.LookupClassOOP("Association");

        // Search through all allocated objects
        foreach (var kvp in _localIdToObjectId)
        {
            var objectOOP = kvp.Value;

            try
            {
                // Check if this is an Association object
                var classOOP = _allocator.GetSlot(objectOOP, 0);
                if (classOOP == associationClassOOP)
                {
                    // Get the key (instance variable 0)
                    var assocKeyOOP = _allocator.GetInstanceVariable(objectOOP, 0);

                    // Check if key matches
                    if (assocKeyOOP == keySymbol)
                    {
                        // Update the value (instance variable 1)
                        _allocator.SetInstanceVariable(objectOOP, 1, newValueOOP);
                        _initializedFields.Add((objectOOP, 1));
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

    /// <summary>
    /// Repairs the linear probing chain after removing an entry.
    /// Implements Smalltalk-80 Dictionary>>fixCollisionsFrom: semantics.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to repair.</param>
    /// <param name="startIndex">The index where the entry was removed.</param>
    private void FixCollisionsFrom(SmalltalkOOP dictionaryOOP, int startIndex)
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);
        int oldIndex = startIndex;

        while (true)
        {
            oldIndex = (oldIndex + 1) % dictSize;
            var nextObject = _allocator.GetIndexedOOP(dictionaryOOP, oldIndex);

            if (nextObject == nilOOP)
                break;

            // Find where this Association SHOULD be
            var keyOOP = _allocator.GetInstanceVariable(nextObject, 0);
            int hash = GetSymbolHash(keyOOP);
            int nextIndex = (hash % dictSize + dictSize) % dictSize;

            // Linear probe to find correct slot
            int probeCount = 0;
            while (true)
            {
                var probe = _allocator.GetIndexedOOP(dictionaryOOP, nextIndex);

                if (probe == nilOOP || probe == nextObject)
                    break;

                var probeKeyOOP = _allocator.GetInstanceVariable(probe, 0);
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
                _allocator.SetIndexedOOP(dictionaryOOP, nextIndex, nextObject);
                _allocator.SetIndexedOOP(dictionaryOOP, oldIndex, nilOOP);
            }
        }
    }

    /// <summary>
    /// Increments the tally field of a dictionary.
    /// </summary>
    private void IncrementDictionaryTally(SmalltalkOOP dictionaryOOP, string keyName)
    {
        // Try to find tally index for Dictionary class
        int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", _bundle);
        if (tallyIndex < 0)
        {
            // For SystemDictionary and other dictionaries, tally is usually at index 0
            tallyIndex = 0;
        }

        var currentTallyOOP = _allocator.GetInstanceVariable(dictionaryOOP, tallyIndex);

        if (!currentTallyOOP.IsSmallInteger)
        {
            // Some dictionaries store tally differently, check if it's nil (uninitialized)
            if (currentTallyOOP == SpecialObjectsArray.Instance.Nil)
            {
                _allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(1));
                return;
            }

            throw new InvalidOperationException(
                $"Dictionary tally is not a SmallInteger when installing '{keyName}'. " +
                $"Tally OOP: {currentTallyOOP}");
        }

        int newTally = currentTallyOOP.AsSmallInteger() + 1;
        _allocator.SetInstanceVariable(dictionaryOOP, tallyIndex, SmalltalkOOP.FromSmallInteger(newTally));
    }

    /// <summary>
    /// Decrements the tally field of a dictionary.
    /// </summary>
    private void DecrementDictionaryTally(SmalltalkOOP dictionaryOOP)
    {
        // For SystemDictionary and other dictionaries, tally is usually at index 0
        var tallyOOP = _allocator.GetInstanceVariable(dictionaryOOP, 0);
        if (tallyOOP.IsSmallInteger)
        {
            int tally = tallyOOP.AsSmallInteger();
            _allocator.SetInstanceVariable(dictionaryOOP, 0, SmalltalkOOP.FromSmallInteger(tally - 1));
        }
    }

    /// <summary>
    /// Emits a diagnostic event for global installation.
    /// </summary>
    private void EmitGlobalInstalledEvent(SmalltalkOOP associationOOP, string keyName)
    {
        if (_diagnostics == null)
            return;

        var valueOOP = _allocator.GetInstanceVariable(associationOOP, 1);
        _diagnostics.Emit(new GlobalInstalledEvent
        {
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0,
            GlobalName = keyName,
            AssociationOOP = associationOOP,
            ValueOOP = valueOOP
        });
    }
}
