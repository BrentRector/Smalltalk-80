namespace Smalltalk80.Interpreter.Memory;

using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Integration layer that modifies ObjectMemoryNew to use ObjectTable instead of ConcurrentDictionary.
/// This file shows how to integrate the ObjectTable without breaking existing code.
///
/// INTEGRATION STEPS:
/// 1. Replace the ConcurrentDictionary with ObjectTable in ObjectMemory.cs
/// 2. Update AllocateObject to use objectTable.Allocate()
/// 3. Update GetObjectData to use objectTable.Get()
/// 4. Update CollectGarbage to use the new GarbageCollectorNew
/// 5. Update Become to swap entries in the ObjectTable
/// </summary>
public static class ObjectMemoryTableIntegration
{
    /// <summary>
    /// Example of how to modify the AllocateObject method in ObjectMemory.cs
    /// Replace lines 85-122 with this implementation.
    /// </summary>
    public static nint AllocateObjectExample(
        ObjectTable objectTable,
        SObjectRef classRef,
        ObjectFormat format,
        int instanceVarCount,
        int indexedSize)
    {
        // Create object data
        var data = new ObjectMemoryNew.ObjectData(classRef, format, instanceVarCount);

        // Initialize indexed data based on format
        if (indexedSize > 0)
        {
            data.IndexedData = format switch
            {
                ObjectFormat.PointerIndexed => InitializePointerIndexedExample(indexedSize),
                ObjectFormat.ByteIndexed => new byte[indexedSize],
                ObjectFormat.WordIndexed => new ushort[indexedSize],
                ObjectFormat.CompiledMethod => new byte[indexedSize],
                _ => null
            };
        }

        // Allocate in object table (replaces the old dictionary approach)
        int index = objectTable.Allocate(data);

        // Convert index to OOP
        return ObjectTable.OopFromIndex(index);
    }

    /// <summary>
    /// Example of how to modify the GetObjectData method in ObjectMemory.cs
    /// Replace lines 141-153 with this implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectMemoryNew.ObjectData? GetObjectDataExample(ObjectTable objectTable, nint oop)
    {
        if (oop == 0)
            throw new InvalidOperationException("Cannot access nil object data");

        int index = ObjectTable.IndexFromOop(oop);
        var data = objectTable.Get(index);

        if (data == null)
            throw new InvalidOperationException($"Invalid OOP: 0x{oop:X}");

        return data;
    }

    /// <summary>
    /// Example of how to modify the IsValidOop method in ObjectMemory.cs
    /// Replace lines 157-162 with this implementation.
    /// </summary>
    public static bool IsValidOopExample(ObjectTable objectTable, nint oop)
    {
        if (oop == 0)
            return true; // nil is valid

        int index = ObjectTable.IndexFromOop(oop);
        return objectTable.Get(index) != null;
    }

    /// <summary>
    /// Example of how to modify the Become method in ObjectMemory.cs
    /// Replace lines 388-405 with this implementation.
    /// </summary>
    public static void BecomeExample(ObjectTable objectTable, nint oop1, nint oop2)
    {
        if (oop1 == 0 || oop2 == 0)
            throw new ArgumentException("Cannot become: with nil");

        int index1 = ObjectTable.IndexFromOop(oop1);
        int index2 = ObjectTable.IndexFromOop(oop2);

        var data1 = objectTable.Get(index1);
        var data2 = objectTable.Get(index2);

        if (data1 == null || data2 == null)
            throw new InvalidOperationException("Invalid OOP in become:");

        // Swap the object data in the table
        objectTable.Set(index1, data2);
        objectTable.Set(index2, data1);

        // Note: All existing SObjectRef instances pointing to oop1 now resolve to data2
        // and vice versa. This is the magic of the indirection!
    }

    /// <summary>
    /// Example of how to modify the CollectGarbage method in ObjectMemory.cs
    /// Replace lines 410-446 with this implementation.
    /// </summary>
    public static void CollectGarbageExample(
        ObjectTable objectTable,
        GarbageCollectorNew garbageCollector,
        SObjectRef[] roots)
    {
        // Convert roots to list for the new GC
        var soaList = new List<SObjectRef>(roots);
        garbageCollector.SetSpecialObjectsArray(soaList);

        // Perform collection using the new GC
        var stats = garbageCollector.Collect();

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] GC completed: {stats}");
        }
    }

    /// <summary>
    /// Helper to initialize pointer-indexed arrays.
    /// </summary>
    private static SObjectRef[] InitializePointerIndexedExample(int size)
    {
        var array = new SObjectRef[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = SObjectRef.Nil;
        }
        return array;
    }

    /// <summary>
    /// Shows the changes needed in ObjectMemory.cs at the field declaration level.
    /// </summary>
    public static class FieldReplacementExample
    {
        // OLD (line 24):
        // private static readonly ConcurrentDictionary<nint, ObjectData> objectTable = new();

        // NEW:
        private static readonly ObjectTable objectTable = new();
        private static readonly GarbageCollectorNew garbageCollector = new(objectTable);

        // OLD (lines 26-30):
        // private static nint nextOop = 0x1000;
        // private static readonly object allocationLock = new();

        // NEW: Remove these - ObjectTable handles allocation internally
    }

    /// <summary>
    /// Migration guide for updating ObjectMemory.cs
    /// </summary>
    public static class MigrationGuide
    {
        /*
         * STEP-BY-STEP MIGRATION:
         *
         * 1. Add using directive at top of ObjectMemory.cs:
         *    using Smalltalk80.Interpreter.Memory;
         *
         * 2. Replace field declarations (lines 24-30):
         *    - Remove: ConcurrentDictionary<nint, ObjectData> objectTable
         *    - Remove: nint nextOop
         *    - Remove: object allocationLock
         *    - Add: ObjectTable objectTable = new(65536)
         *    - Add: GarbageCollectorNew garbageCollector = new(objectTable)
         *
         * 3. Update AllocateObject method (lines 85-122):
         *    - Remove lock statement
         *    - Remove nextOop manipulation
         *    - Replace with: int index = objectTable.Allocate(data);
         *    - Return: ObjectTable.OopFromIndex(index)
         *
         * 4. Update GetObjectData method (lines 141-153):
         *    - Replace dictionary lookup with:
         *      int index = ObjectTable.IndexFromOop(oop);
         *      return objectTable.Get(index);
         *
         * 5. Update IsValidOop method (lines 157-162):
         *    - Replace ContainsKey with:
         *      int index = ObjectTable.IndexFromOop(oop);
         *      return objectTable.Get(index) != null;
         *
         * 6. Update Become method (lines 388-405):
         *    - Remove lock statement
         *    - Use ObjectTable.IndexFromOop to get indices
         *    - Use objectTable.Get/Set to swap data
         *
         * 7. Update CollectGarbage method (lines 410-446):
         *    - Replace entire implementation with new GC calls
         *    - Use garbageCollector.SetSpecialObjectsArray()
         *    - Use garbageCollector.Collect()
         *
         * 8. Update Clear method (lines 670-677):
         *    - Replace objectTable.Clear() with objectTable.Clear()
         *    - Remove nextOop reset
         *    - Add garbageCollector.ResetStatistics()
         *
         * 9. Update GetStatistics method (lines 660-665):
         *    - Use objectTable.GetStatistics()
         *    - Return LiveObjects and Capacity from table stats
         */
    }
}