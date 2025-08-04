using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Implements a real object memory system following the Smalltalk-80 specification.
/// Reference: Virtual Machine - Object Memory.md, lines 1-202
/// </summary>
public class RealObjectMemory
{
    /// <summary>
    /// The heap where all objects are stored.
    /// </summary>
    private readonly List<HeapObject> heap = new();

    /// <summary>
    /// Free object table for garbage collection.
    /// </summary>
    private readonly Stack<int> freeObjects = new();

    /// <summary>
    /// Reference counting table for automatic memory management.
    /// </summary>
    private readonly Dictionary<int, int> referenceCounts = new();

    /// <summary>
    /// Creates a new object with the specified class and number of fields.
    /// </summary>
    /// <param name="classPointer">The class of the new object.</param>
    /// <param name="fieldCount">Number of instance variables.</param>
    /// <returns>Object pointer to the new object.</returns>
    public int CreateObject(SmalltalkClass classPointer, int fieldCount)
    {
        int objectPointer;

        if (freeObjects.Count > 0)
        {
            objectPointer = freeObjects.Pop();
            heap[objectPointer] = new HeapObject
            {
                ClassPointer = classPointer,
                Fields = new object?[fieldCount],
                Size = fieldCount + 2 // Header size
            };
        }
        else
        {
            objectPointer = heap.Count;
            heap.Add(new HeapObject
            {
                ClassPointer = classPointer,
                Fields = new object?[fieldCount],
                Size = fieldCount + 2
            });
        }

        referenceCounts[objectPointer] = 1;
        return objectPointer;
    }

    /// <summary>
    /// Retrieves the object at the specified pointer.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    /// <returns>The heap object.</returns>
    public HeapObject GetObject(int objectPointer)
    {
        if (objectPointer < 0 || objectPointer >= heap.Count)
            throw new ArgumentOutOfRangeException(nameof(objectPointer));

        return heap[objectPointer];
    }

    /// <summary>
    /// Fetches a field from an object.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    /// <param name="fieldIndex">The field index.</param>
    /// <returns>The field value.</returns>
    public object? FetchField(int objectPointer, int fieldIndex)
    {
        var obj = GetObject(objectPointer);
        if (fieldIndex < 0 || fieldIndex >= obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex));

        return obj.Fields[fieldIndex];
    }

    /// <summary>
    /// Stores a value in an object field.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    /// <param name="fieldIndex">The field index.</param>
    /// <param name="value">The value to store.</param>
    public void StoreField(int objectPointer, int fieldIndex, object? value)
    {
        var obj = GetObject(objectPointer);
        if (fieldIndex < 0 || fieldIndex >= obj.Fields.Length)
            throw new ArgumentOutOfRangeException(nameof(fieldIndex));

        // Decrement reference count of old value
        if (obj.Fields[fieldIndex] is int oldPointer)
        {
            DecrementReferenceCount(oldPointer);
        }

        obj.Fields[fieldIndex] = value;

        // Increment reference count of new value
        if (value is int newPointer)
        {
            IncrementReferenceCount(newPointer);
        }
    }

    /// <summary>
    /// Increments the reference count for an object.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    public void IncrementReferenceCount(int objectPointer)
    {
        if (referenceCounts.ContainsKey(objectPointer))
        {
            referenceCounts[objectPointer]++;
        }
        else
        {
            referenceCounts[objectPointer] = 1;
        }
    }

    /// <summary>
    /// Decrements the reference count for an object and deallocates if count reaches zero.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    public void DecrementReferenceCount(int objectPointer)
    {
        if (!referenceCounts.ContainsKey(objectPointer))
            return;

        referenceCounts[objectPointer]--;

        if (referenceCounts[objectPointer] <= 0)
        {
            DeallocateObject(objectPointer);
        }
    }

    /// <summary>
    /// Deallocates an object and adds it to the free list.
    /// </summary>
    /// <param name="objectPointer">The object pointer.</param>
    private void DeallocateObject(int objectPointer)
    {
        var obj = GetObject(objectPointer);

        // Decrement reference counts for all field values
        foreach (var field in obj.Fields)
        {
            if (field is int fieldPointer)
            {
                DecrementReferenceCount(fieldPointer);
            }
        }

        // Clear the object
        heap[objectPointer] = new HeapObject();
        freeObjects.Push(objectPointer);
        referenceCounts.Remove(objectPointer);
    }

    /// <summary>
    /// Performs garbage collection using mark and sweep algorithm.
    /// </summary>
    public void GarbageCollect()
    {
        // Mark phase: mark all reachable objects
        var marked = new HashSet<int>();
        var roots = GetRootObjects();

        foreach (var root in roots)
        {
            MarkObject(root, marked);
        }

        // Sweep phase: deallocate unmarked objects
        for (int i = 0; i < heap.Count; i++)
        {
            if (!marked.Contains(i) && referenceCounts.ContainsKey(i))
            {
                DeallocateObject(i);
            }
        }
    }

    /// <summary>
    /// Marks an object and all objects it references.
    /// </summary>
    /// <param name="objectPointer">The object to mark.</param>
    /// <param name="marked">Set of already marked objects.</param>
    private void MarkObject(int objectPointer, HashSet<int> marked)
    {
        if (marked.Contains(objectPointer))
            return;

        marked.Add(objectPointer);
        var obj = GetObject(objectPointer);

        // Mark all referenced objects
        foreach (var field in obj.Fields)
        {
            if (field is int fieldPointer)
            {
                MarkObject(fieldPointer, marked);
            }
        }
    }

    /// <summary>
    /// Gets all root objects that should not be garbage collected.
    /// </summary>
    /// <returns>Collection of root object pointers.</returns>
    private IEnumerable<int> GetRootObjects()
    {
        // Implementation would return:
        // - Objects referenced by the interpreter stack
        // - Global variables
        // - Active contexts
        // - System classes
        yield break; // Placeholder
    }

    /// <summary>
    /// Checks if a value represents a SmallInteger (immediate value).
    /// SmallIntegers are tagged pointers, not actual object pointers.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is a SmallInteger.</returns>
    public static bool IsSmallInteger(object? value)
    {
        // In Smalltalk-80, SmallIntegers are stored as immediate values
        // with the low bit set to 1 to distinguish from object pointers
        return value is int intValue && (intValue & 1) == 1;
    }

    /// <summary>
    /// Converts a .NET integer to a SmallInteger object pointer.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>SmallInteger object pointer.</returns>
    public static int IntegerToSmallInteger(int value)
    {
        // Shift left by 1 and set low bit to mark as SmallInteger
        return (value << 1) | 1;
    }

    /// <summary>
    /// Converts a SmallInteger object pointer to a .NET integer.
    /// </summary>
    /// <param name="smallInteger">The SmallInteger object pointer.</param>
    /// <returns>The integer value.</returns>
    public static int SmallIntegerToInteger(int smallInteger)
    {
        // Shift right by 1 to remove the tag bit
        return smallInteger >> 1;
    }
}

/// <summary>
/// Represents an object in the heap.
/// </summary>
public class HeapObject
{
    /// <summary>
    /// The class of this object.
    /// </summary>
    public SmalltalkClass? ClassPointer { get; set; }

    /// <summary>
    /// The instance variables/fields of this object.
    /// </summary>
    public object?[] Fields { get; set; } = Array.Empty<object?>();

    /// <summary>
    /// Size in words (including header).
    /// </summary>
    public int Size { get; set; }
}
