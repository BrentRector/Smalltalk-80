using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Engine.Services;

/// <summary>
/// Provides dictionary operations for cold start phases.
/// Implements hash-based installation using the same algorithm as Smalltalk-80 Dictionary>>findKeyOrNil:.
/// </summary>
/// <remarks>
/// This service does NOT allocate new objects - it only installs existing Associations
/// into dictionaries. Object allocation remains in the phase-specific code to ensure
/// proper class reference tracking.
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
    /// Installs an Association in SystemDictionary using hash-based slot finding.
    /// CRITICAL: Uses same algorithm as Dictionary>>findKeyOrNil: so runtime lookups work!
    /// </summary>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInSystemDictionary(SmalltalkOOP associationOOP, string name)
    {
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        int systemDictSize = _allocator.GetIndexedSize(systemDictOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup (same algorithm as Dictionary>>findKeyOrNil:)
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % systemDictSize + systemDictSize) % systemDictSize;

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
    /// Installs an Association in any Dictionary object.
    /// Uses hash-based lookup to match Dictionary>>findKeyOrNil: algorithm.
    /// </summary>
    /// <param name="dictionaryOOP">The dictionary to install into.</param>
    /// <param name="associationOOP">The Association to install.</param>
    /// <param name="name">The key name for diagnostics.</param>
    public void InstallInDictionary(SmalltalkOOP dictionaryOOP, SmalltalkOOP associationOOP, string name)
    {
        int dictSize = _allocator.GetIndexedSize(dictionaryOOP);
        SmalltalkOOP nilOOP = SpecialObjectsArray.Instance.Nil;

        // Get the key symbol from the Association (slot 0)
        var keySymbolOOP = _allocator.GetInstanceVariable(associationOOP, 0);

        // Use hash-based lookup
        int hash = GetSymbolHash(keySymbolOOP);
        int location = (hash % dictSize + dictSize) % dictSize;

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
