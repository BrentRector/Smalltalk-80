namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Creates and populates the SystemDictionary (Smalltalk global) with all global associations.
///
/// WHY THIS IS PHASE 6:
/// Must happen AFTER all classes exist so we can create associations mapping class names to classes.
/// Must happen BEFORE symbol migration so associations can reference symbol placeholders.
///
/// SystemDictionary contains Association objects for:
/// - All class names: Object, Array, String, etc.
/// - Pool dictionaries: TextConstants, etc.
/// - System globals: Processor (→ ProcessorScheduler), Transcript, etc.
/// - Singleton objects: nil, true, false
///
/// Blue Book Semantics:
/// - SystemDictionary is a Dictionary (or IdentityDictionary)
/// - Keys are Symbols (class names, global variable names)
/// - Values are the actual objects (Class objects, pool Dictionaries, etc.)
/// - Associations store key-value pairs
/// </summary>
public class SystemDictionaryCreator(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Creates SystemDictionary and populates it with all global associations.
    ///
    /// Steps:
    /// 1. Create SystemDictionary object (instance of SystemDictionary class)
    /// 2. For each class: create Association(className Symbol → Class) and add to dict
    /// 3. For each pool: create Association(poolName Symbol → Dictionary) and add to dict
    /// 4. For system globals: create associations (Processor → ProcessorScheduler, etc.)
    /// 5. Register "object:Smalltalk" localId pointing to SystemDictionary
    ///
    /// This makes all classes and globals reachable from Smalltalk, which is one of the 5 GC roots.
    /// </summary>
    public void CreateAndPopulate()
    {
        Console.WriteLine("[SystemDictionaryCreator] Creating SystemDictionary and global associations...");

        // Get SystemDictionary class
        var systemDictClass = _loader.ResolveLocalId("class:SystemDictionary");
        if (systemDictClass.IsNil)
        {
            // Fallback: use Dictionary class if SystemDictionary doesn't exist
            systemDictClass = _loader.ResolveLocalId("class:Dictionary");
            if (systemDictClass.IsNil)
                throw new InvalidOperationException("Neither SystemDictionary nor Dictionary class found");
        }

        // Calculate capacity: all classes + pools + system globals
        var classCount = _loader.Bundle.Classes.Length / 2; // Divide by 2 (classes + metaclasses)
        var estimatedGlobalCount = classCount + 50; // Classes + pools + system globals
        var capacity = CalculateDictionaryCapacity(estimatedGlobalCount);

        // Create SystemDictionary object
        var systemDictOop = ObjectMemory.AllocateObject(
            systemDictClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 2,          // tally, array (IdentityDictionary structure)
            indexedSize: capacity      // Indexed slots for keys
        );

        // Initialize tally to 0
        ObjectMemory.SetInstanceVariable(systemDictOop, 0, SObjectRef.FromSmallInteger(0));

        // Create values array
        var arrayClass = ObjectMemory.GetArrayClass();
        if (arrayClass.IsNil)
            throw new InvalidOperationException("Array class not found");

        var valueArrayOop = ObjectMemory.AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: capacity
        );

        // Initialize arrays to nil
        for (int i = 0; i < capacity; i++)
        {
            ObjectMemory.SetIndexedPointer(systemDictOop, i, SObjectRef.Nil);
            ObjectMemory.SetIndexedPointer(valueArrayOop, i, SObjectRef.Nil);
        }

        // Set values array field
        ObjectMemory.SetInstanceVariable(systemDictOop, 1, SObjectRef.FromObjectPointer(valueArrayOop));

        // Register "object:Smalltalk" localId
        var systemDictRef = SObjectRef.FromObjectPointer(systemDictOop);
        _loader.RegisterLocalId("object:Smalltalk", systemDictRef);

        // Add all classes to SystemDictionary
        AddAllClasses(systemDictOop);

        // Add pool dictionaries
        AddPoolDictionaries(systemDictOop);

        // Add system globals
        AddSystemGlobals(systemDictOop);

        Console.WriteLine("[SystemDictionaryCreator] SystemDictionary created and populated.");
    }

    /// <summary>
    /// Adds all classes to SystemDictionary.
    ///
    /// For each class (not metaclass):
    /// - Create Association(className Symbol → Class object)
    /// - Add association to SystemDictionary
    /// </summary>
    private void AddAllClasses(nint systemDictOop)
    {
        Console.WriteLine("[SystemDictionaryCreator] Adding all classes to SystemDictionary...");

        int addedCount = 0;
        foreach (var jsonClass in _loader.Bundle.Classes)
        {
            // Only add instance-side classes (not metaclasses)
            if (jsonClass.IsMetaclass)
                continue;

            var className = _loader.Bundle.Symbols[jsonClass.NameIndex];

            // Get class name symbol (placeholder for now, will be real Symbol after migration)
            var classNameSymbol = _loader.ResolveLocalId($"symbol:{jsonClass.NameIndex}");

            // Get class object
            var classRef = _loader.ResolveLocalId($"class:{className}");

            // Add to SystemDictionary
            AddToDictionary(systemDictOop, classNameSymbol, classRef);
            addedCount++;
        }

        Console.WriteLine($"[SystemDictionaryCreator] Added {addedCount} classes to SystemDictionary.");
    }

    /// <summary>
    /// Adds pool dictionaries to SystemDictionary.
    ///
    /// Pool dictionaries are shared class variable pools (e.g., TextConstants).
    /// </summary>
    private void AddPoolDictionaries(nint systemDictOop)
    {
        Console.WriteLine("[SystemDictionaryCreator] Adding pool dictionaries to SystemDictionary...");

        // TODO: Iterate through JSON pool dictionaries
        // For now, skip - pools are optional

        Console.WriteLine("[SystemDictionaryCreator] Pool dictionaries not yet implemented.");
    }

    /// <summary>
    /// Adds system globals to SystemDictionary.
    ///
    /// System globals include:
    /// - Processor → ProcessorScheduler
    /// - Transcript → TranscriptStream (if exists)
    /// - SourceFiles → Array of file streams (if exists)
    /// - nil, true, false (singleton objects)
    /// </summary>
    private void AddSystemGlobals(nint systemDictOop)
    {
        Console.WriteLine("[SystemDictionaryCreator] Adding system globals to SystemDictionary...");

        // Add Processor → ProcessorScheduler
        AddGlobalIfExists(systemDictOop, "Processor", "object:ProcessorScheduler");

        // Add nil, true, false
        AddGlobalIfExists(systemDictOop, "nil", "object:nil");
        AddGlobalIfExists(systemDictOop, "true", "object:true");
        AddGlobalIfExists(systemDictOop, "false", "object:false");

        Console.WriteLine("[SystemDictionaryCreator] System globals added.");
    }

    /// <summary>
    /// Adds a global variable to SystemDictionary if it exists.
    /// </summary>
    private void AddGlobalIfExists(nint systemDictOop, string globalName, string objectLocalId)
    {
        try
        {
            // Find symbol index for global name
            var symbolIndex = FindSymbolIndex(globalName);
            if (symbolIndex < 0)
                return; // Symbol not found

            var keySymbol = _loader.ResolveLocalId($"symbol:{symbolIndex}");
            var valueRef = _loader.ResolveLocalId(objectLocalId);

            if (!valueRef.IsNil)
            {
                AddToDictionary(systemDictOop, keySymbol, valueRef);
                Console.WriteLine($"[SystemDictionaryCreator]   Added global: {globalName}");
            }
        }
        catch
        {
            // Global doesn't exist - skip
        }
    }

    /// <summary>
    /// Finds symbol index in JSON symbols array.
    /// Returns -1 if not found.
    /// </summary>
    private int FindSymbolIndex(string symbolText)
    {
        for (int i = 0; i < _loader.Bundle.Symbols.Length; i++)
        {
            if (_loader.Bundle.Symbols[i] == symbolText)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Adds a key-value pair to a dictionary.
    ///
    /// Uses linear probing to find empty slot.
    /// Updates tally after successful insertion.
    /// </summary>
    private void AddToDictionary(nint dictOop, SObjectRef keyRef, SObjectRef valueRef)
    {
        var dictSize = ObjectMemory.GetIndexedSize(dictOop);
        var valueArrayRef = ObjectMemory.GetInstanceVariable(dictOop, 1);
        var valueArrayOop = valueArrayRef.AsObjectPointer();

        // Find empty slot (linear search - simplified)
        for (int i = 0; i < dictSize; i++)
        {
            var slotKey = ObjectMemory.GetIndexedPointer(dictOop, i);
            if (slotKey.IsNil)
            {
                // Found empty slot - add entry
                ObjectMemory.SetIndexedPointer(dictOop, i, keyRef);
                ObjectMemory.SetIndexedPointer(valueArrayOop, i, valueRef);

                // Update tally
                var tallyRef = ObjectMemory.GetInstanceVariable(dictOop, 0);
                if (tallyRef.IsSmallInteger)
                {
                    var newTally = tallyRef.AsSmallInteger() + 1;
                    ObjectMemory.SetInstanceVariable(dictOop, 0, SObjectRef.FromSmallInteger(newTally));
                }

                return;
            }
        }

        throw new InvalidOperationException("Dictionary full - cannot add entry");
    }

    /// <summary>
    /// Calculates dictionary capacity for given element count.
    /// Uses power of 2 sizing with load factor ~0.75
    /// </summary>
    private int CalculateDictionaryCapacity(int elementCount)
    {
        if (elementCount == 0)
            return 64; // Minimum size for SystemDictionary

        // Target 75% load factor
        var targetCapacity = (int)(elementCount / 0.75);

        // Round up to next power of 2
        var capacity = 64;
        while (capacity < targetCapacity)
            capacity *= 2;

        return capacity;
    }
}
