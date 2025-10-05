namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Creates pool dictionaries, Association objects, and other global objects from JSON.
///
/// Why this is separate from classes?
/// These are runtime objects (not classes) that need to exist before init expressions run.
/// Examples:
/// - Pool dictionaries (shared class variables)
/// - Association objects (key-value pairs for dictionaries)
/// - Singleton objects (nil, true, false)
/// - SystemDictionary (Smalltalk global)
/// </summary>
public class ObjectAllocator(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    /// <summary>
    /// Allocates an object from JSON definition.
    ///
    /// LocalId formats handled:
    /// - "object:nil", "object:true", "object:false" - singletons
    /// - "object:Smalltalk" - SystemDictionary
    /// - "pool:PoolName" - pool dictionaries
    /// - "assoc:VariableName" - Association objects
    /// </summary>
    public void AllocateObject(string localId)
    {
        // Find object definition in JSON
        var jsonObject = FindObjectDefinition(localId);
        if (jsonObject == null)
            throw new InvalidOperationException($"Object definition not found: {localId}");

        // Determine object type from localId prefix
        if (localId.StartsWith("object:"))
        {
            AllocateSingletonOrGlobal(localId, jsonObject);
        }
        else if (localId.StartsWith("pool:"))
        {
            AllocatePoolDictionary(localId, jsonObject);
        }
        else if (localId.StartsWith("assoc:"))
        {
            AllocateAssociation(localId, jsonObject);
        }
        else
        {
            throw new InvalidOperationException($"Unknown object localId format: {localId}");
        }
    }

    /// <summary>
    /// Allocates singleton objects (nil, true, false) and global objects (Smalltalk, ProcessorScheduler).
    ///
    /// Blue Book Singletons:
    /// - nil: instance of UndefinedObject
    /// - true: instance of True
    /// - false: instance of False
    ///
    /// These are the 5 GC roots in Special Objects Array.
    /// </summary>
    private void AllocateSingletonOrGlobal(string localId, JsonObject jsonObject)
    {
        // Resolve class from JSON
        var className = _loader.Bundle.Symbols[jsonObject.ClassNameIndex];
        var classLocalId = $"class:{className}";
        var classRef = _loader.ResolveLocalId(classLocalId);

        // Determine instance variable count from JSON
        var instVarCount = jsonObject.Fields?.Length ?? 0;

        // Allocate object
        var oop = ObjectMemory.AllocateObject(
            classRef,
            ObjectFormat.FixedPointer,      // Singletons are fixed pointer objects
            instanceVarCount: instVarCount,
            indexedSize: 0                   // No indexed slots
        );

        // Register localId
        _loader.RegisterLocalId(localId, SObjectRef.FromObjectPointer(oop));

        // Populate fields if present
        if (jsonObject.Fields != null)
        {
            PopulateObjectFields(oop, jsonObject.Fields);
        }
    }

    /// <summary>
    /// Allocates a pool dictionary.
    ///
    /// Pool dictionaries are shared class variable pools (e.g., TextConstants).
    /// They're Dictionary objects containing Associations.
    /// </summary>
    private void AllocatePoolDictionary(string localId, JsonObject jsonObject)
    {
        // Pool dictionaries are Dictionary objects
        var dictionaryClass = ObjectMemory.GetDictionaryClass();
        if (dictionaryClass.IsNil)
            throw new InvalidOperationException("Dictionary class not found");

        // Calculate capacity based on field count
        var elementCount = jsonObject.Fields?.Length ?? 0;
        var capacity = CalculateDictionaryCapacity(elementCount);

        // Allocate Dictionary: 2 instance vars (tally, array) + indexed slots
        var dictOop = ObjectMemory.AllocateObject(
            dictionaryClass,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 2,                // tally, array
            indexedSize: capacity           // Slots for keys
        );

        // Initialize tally to 0
        ObjectMemory.SetInstanceVariable(dictOop, 0, SObjectRef.FromSmallInteger(0));

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
            ObjectMemory.SetIndexedPointer(dictOop, i, SObjectRef.Nil);
            ObjectMemory.SetIndexedPointer(valueArrayOop, i, SObjectRef.Nil);
        }

        // Set array field
        ObjectMemory.SetInstanceVariable(dictOop, 1, SObjectRef.FromObjectPointer(valueArrayOop));

        // Register localId
        _loader.RegisterLocalId(localId, SObjectRef.FromObjectPointer(dictOop));

        // Populate dictionary entries if present
        if (jsonObject.Fields != null)
        {
            PopulateDictionaryEntries(dictOop, jsonObject.Fields);
        }
    }

    /// <summary>
    /// Allocates an Association object (key-value pair).
    ///
    /// Blue Book Association:
    /// - Has 2 instance variables: key, value
    /// - Key must be a Symbol (for SystemDictionary and class pools)
    /// - Value can be any object
    ///
    /// Used in:
    /// - SystemDictionary entries (key=class name Symbol, value=Class)
    /// - Pool dictionaries
    /// - Class variable dictionaries
    /// </summary>
    private void AllocateAssociation(string localId, JsonObject jsonObject)
    {
        var associationClass = ObjectMemory.GetAssociationClass();
        if (associationClass.IsNil)
            throw new InvalidOperationException("Association class not found");

        // Allocate Association: 2 instance vars (key, value)
        var assocOop = ObjectMemory.AllocateObject(
            associationClass,
            ObjectFormat.FixedPointer,
            instanceVarCount: 2,
            indexedSize: 0
        );

        // Register localId
        _loader.RegisterLocalId(localId, SObjectRef.FromObjectPointer(assocOop));

        // Populate fields (key, value)
        if (jsonObject.Fields != null && jsonObject.Fields.Length >= 2)
        {
            PopulateObjectFields(assocOop, jsonObject.Fields);
        }
        else
        {
            throw new InvalidOperationException($"Association must have 2 fields (key, value): {localId}");
        }
    }

    /// <summary>
    /// Populates object instance variable fields from JSON field values.
    /// </summary>
    private void PopulateObjectFields(nint oop, FieldValue[] fields)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            var fieldValue = ResolveFieldValue(fields[i]);
            ObjectMemory.SetInstanceVariable(oop, i, fieldValue);
        }
    }

    /// <summary>
    /// Populates dictionary entries from JSON field values.
    /// Each field represents a key-value pair to add to the dictionary.
    /// </summary>
    private void PopulateDictionaryEntries(nint dictOop, FieldValue[] entries)
    {
        foreach (var entry in entries)
        {
            // Entry should be an Association object reference
            var assocRef = ResolveFieldValue(entry);

            if (assocRef.IsNil)
                continue; // Skip nil entries

            // Get Association's key and value
            var assocOop = assocRef.AsObjectPointer();
            var keyRef = ObjectMemory.GetInstanceVariable(assocOop, 0);
            var valueRef = ObjectMemory.GetInstanceVariable(assocOop, 1);

            // Add to dictionary
            // Note: This is simplified - real implementation would use hashing
            AddToDictionary(dictOop, keyRef, valueRef);
        }
    }

    /// <summary>
    /// Adds a key-value pair to a dictionary.
    /// Simplified linear probing - real implementation would use proper hashing.
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
    /// Resolves a FieldValue from JSON to SObjectRef.
    ///
    /// FieldValue is a discriminated union with these types:
    /// - nil
    /// - smallInt
    /// - objectRef (localId reference)
    /// - symbolRef (symbol index)
    /// - stringRef (string index)
    /// </summary>
    private SObjectRef ResolveFieldValue(FieldValue fieldValue)
    {
        return fieldValue switch
        {
            NilValue => SObjectRef.Nil,
            TrueValue => _loader.ResolveLocalId("object:true"),
            FalseValue => _loader.ResolveLocalId("object:false"),
            SmallIntValue si => SObjectRef.FromSmallInteger(si.Value),
            ObjectRefValue or => _loader.ResolveLocalId(or.LocalId),
            SymbolRefValue sr => _loader.ResolveLocalId($"symbol:{sr.Index}"),
            StringRefValue str => _loader.ResolveLocalId($"string:{str.Index}"),
            CharacterValue ch => _loader.ResolveLocalId($"char:{ch.CodePoint}"),
            _ => throw new InvalidOperationException($"Unknown field value type: {fieldValue.GetType().Name}")
        };
    }

    /// <summary>
    /// Finds object definition in JSON objects array.
    /// </summary>
    private JsonObject? FindObjectDefinition(string localId)
    {
        // For now, return null - object definitions might not be in JSON yet
        // This will be implemented when compiler generates object definitions
        return null;
    }

    /// <summary>
    /// Calculates dictionary capacity for given element count.
    /// Uses power of 2 sizing with load factor ~0.75
    /// </summary>
    private int CalculateDictionaryCapacity(int elementCount)
    {
        if (elementCount == 0)
            return 4; // Minimum size

        // Target 75% load factor
        var targetCapacity = (int)(elementCount / 0.75);

        // Round up to next power of 2
        var capacity = 4;
        while (capacity < targetCapacity)
            capacity *= 2;

        return capacity;
    }
}
