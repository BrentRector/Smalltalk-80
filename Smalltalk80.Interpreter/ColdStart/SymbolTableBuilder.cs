namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Builds the Symbol USTable (Unique String Table) during cold start.
/// Per Blue Book: All Symbol instances are stored in a hash table for uniqueness.
///
/// COLD START KNOWLEDGE (isolated to this file):
/// - Symbol class has classVariable 'USTable' which is the symbol hash table
/// - USTable is an Array of buckets (each bucket is an Array of Symbol instances)
/// - Symbol lookup uses string hashing to find bucket, then linear search
/// - Symbols are unique (only one instance per string)
/// </summary>
public static class SymbolTableBuilder
{
    /// <summary>
    /// Creates the USTable (Unique String Table) for Symbol class.
    /// This is called during cold start after the Symbol class has been created.
    /// </summary>
    /// <returns>An Array containing symbol hash table buckets</returns>
    public static SObjectRef CreateUSTable()
    {
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (symbolClassRef.IsNil)
            throw new InvalidOperationException("Symbol class must be created before USTable");

        var arrayClassRef = SystemDictionary.ArrayClassRef;
        if (arrayClassRef.IsNil)
            throw new InvalidOperationException("Array class must be created before USTable");

        // Create hash table with reasonable initial size (must be power of 2 for efficient modulo)
        const int initialBucketCount = 256;

        nint tableOop = ObjectMemoryNew.AllocateObject(
            arrayClassRef,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: initialBucketCount
        );
        var usTable = SObjectRef.FromObjectPointer(tableOop);

        // Initialize each bucket to empty array
        var emptyArrayOop = ObjectMemoryNew.AllocateObject(
            arrayClassRef,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: 0
        );
        var emptyArray = SObjectRef.FromObjectPointer(emptyArrayOop);

        for (int i = 0; i < initialBucketCount; i++)
        {
            usTable.SetIndexedPointer(i, emptyArray);
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[SymbolTableBuilder] Created USTable with {initialBucketCount} buckets");
        }

        return usTable;
    }

    /// <summary>
    /// Installs the USTable as a class variable in the Symbol class's classPool.
    /// Symbol class inherits from String -> ByteArray -> SequenceableCollection -> Collection -> Object -> ProtoObject.
    /// Class structure: Symbol class has classPool (Dictionary) which should contain #USTable -> the hash table.
    /// </summary>
    public static void InstallUSTable(SObjectRef usTable)
    {
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (symbolClassRef.IsNil)
            throw new InvalidOperationException("Symbol class must exist");

        // Get the Symbol class object
        var symbolClassOop = symbolClassRef.AsObjectPointer();

        // Symbol class inherits from Class, which has:
        // Slot 0-5: inherited from Behavior/ClassDescription (superclass, methodDict, format, instanceVariables, organization, name)
        // Slot 6: name (actually at index 6 in ClassLayout)
        // Slot 7: classPool (Dictionary) - per BlueBook ClassLayout.ClassPool = 7
        // Slot 8: sharedPools

        // Get or create the classPool Dictionary
        var classPoolRef = ObjectMemoryNew.GetInstanceVariable(symbolClassOop, Core.BlueBook.ClassLayout.ClassPool);

        if (classPoolRef.IsNil)
        {
            // Create new Dictionary for classPool
            var dictionaryClassRef = SystemDictionary.DictionaryClassRef;
            if (dictionaryClassRef.IsNil)
                throw new InvalidOperationException("Dictionary class must exist");

            var newClassPoolOop = ObjectMemoryNew.AllocateObject(
                dictionaryClassRef,
                ObjectFormat.PointerIndexed,
                instanceVarCount: 1, // tally
                indexedSize: 16 // initial capacity for associations
            );
            classPoolRef = SObjectRef.FromObjectPointer(newClassPoolOop);

            // Initialize tally to 0
            classPoolRef.SetInstanceVariable(0, SObjectRef.FromSmallInteger(0));

            // Set classPool in Symbol class
            ObjectMemoryNew.SetInstanceVariable(symbolClassOop, Core.BlueBook.ClassLayout.ClassPool, classPoolRef);
        }

        // Add Association #USTable -> usTable to classPool
        var associationClassRef = SystemDictionary.AssociationClassRef;
        if (associationClassRef.IsNil)
            throw new InvalidOperationException("Association class must exist");

        // Create #USTable symbol
        var usTableSymbol = ObjectMemoryNew.InternSymbol("USTable");

        // Create Association: key=#USTable, value=usTable
        var associationOop = ObjectMemoryNew.AllocateObject(
            associationClassRef,
            ObjectFormat.FixedPointer,
            instanceVarCount: 2, // key, value
            indexedSize: 0
        );
        var association = SObjectRef.FromObjectPointer(associationOop);
        association.SetInstanceVariable(0, usTableSymbol); // key
        association.SetInstanceVariable(1, usTable);       // value

        // Add association to classPool Dictionary
        // Simple implementation: find nil slot and add
        var indexedSize = ObjectMemoryNew.GetIndexedSize(classPoolRef.AsObjectPointer());

        for (int i = 0; i < indexedSize; i++)
        {
            var slot = classPoolRef.GetIndexedPointer(i);
            if (slot.IsNil)
            {
                classPoolRef.SetIndexedPointer(i, association);

                // Increment tally
                var tally = classPoolRef.GetInstanceVariable(0);
                if (tally.IsSmallInteger)
                {
                    classPoolRef.SetInstanceVariable(0, SObjectRef.FromSmallInteger(tally.AsSmallInteger() + 1));
                }

                break;
            }
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[SymbolTableBuilder] USTable installed in Symbol class classPool");
        }
    }

    /// <summary>
    /// Gets the USTable from Symbol class's classPool.
    /// NOTE: This method MUST NOT call InternSymbol to avoid infinite recursion.
    /// </summary>
    public static SObjectRef? GetUSTable()
    {
        var symbolClassRef = SystemDictionary.SymbolClassRef;
        if (symbolClassRef.IsNil)
            return null;

        var symbolClassOop = symbolClassRef.AsObjectPointer();
        var classPoolRef = ObjectMemoryNew.GetInstanceVariable(symbolClassOop, Core.BlueBook.ClassLayout.ClassPool);

        if (classPoolRef.IsNil)
            return null;

        // Search classPool for #USTable association
        // IMPORTANT: Compare by string value, NOT by interning (which would cause infinite recursion)
        var indexedSize = ObjectMemoryNew.GetIndexedSize(classPoolRef.AsObjectPointer());

        for (int i = 0; i < indexedSize; i++)
        {
            var association = classPoolRef.GetIndexedPointer(i);
            if (association.IsNil)
                continue;

            var key = association.GetInstanceVariable(0);
            if (key.IsHeapObject)
            {
                // Compare symbol string value
                try
                {
                    var keyString = ObjectMemoryNew.SymbolToString(key);
                    if (keyString == "USTable")
                    {
                        return association.GetInstanceVariable(1); // value
                    }
                }
                catch
                {
                    // Not a symbol, skip
                    continue;
                }
            }
        }

        return null;
    }
}
