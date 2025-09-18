using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Memory;
/// <summary>
/// Maps OOP indices to object layouts.
/// Provides fast lookup and enumeration of all objects.
/// </summary>
/// <param name="initialCapacity">Initial capacity of the object table.</param>
public sealed class ObjectTable(int initialCapacity)
{
    private ObjectLayout?[] table = initialCapacity > 0 
        ? new ObjectLayout?[initialCapacity]
        : throw new ArgumentException("Capacity must be positive", nameof(initialCapacity));
    private int count = 0;

    /// <summary>
    /// Stores an object at the given index.
    /// </summary>
    public void StoreObject(uint index, ObjectLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        
        EnsureCapacity(index);
        
        if (table[index] == null)
            count++;
        
        table[index] = layout;
    }

    /// <summary>
    /// Gets an object at the given index.
    /// Returns null if no object at that index.
    /// </summary>
    public ObjectLayout? GetObject(uint index) =>
        index >= table.Length ? null : table[index];

    /// <summary>
    /// Removes an object at the given index.
    /// Used by GC to free objects.
    /// </summary>
    public void RemoveObject(uint index)
    {
        if (index < table.Length && table[index] != null)
        {
            table[index] = null;
            count--;
        }
    }

    /// <summary>
    /// Enumerates all non-null objects in the table.
    /// </summary>
    public IEnumerable<(OOP oop, ObjectLayout layout)> EnumerateObjects()
    {
        for (uint i = 0; i < table.Length; i++)
        {
            if (table[i] is { } layout)
            {
                yield return (OOP.FromObjectIndex(i), layout);
            }
        }
    }

    /// <summary>
    /// Gets the number of objects in the table.
    /// </summary>
    public int Count => count;

    /// <summary>
    /// Gets the current capacity of the table.
    /// </summary>
    public int Capacity => table.Length;

    /// <summary>
    /// Clears all objects from the table.
    /// </summary>
    public void Clear()
    {
        Array.Clear(table);
        count = 0;
    }

    private void EnsureCapacity(uint index)
    {
        if (index >= table.Length)
        {
            // Grow by doubling up to the required size
            int newSize = table.Length;
            while (newSize <= index)
            {
                newSize *= 2;
            }
            
            Array.Resize(ref table, newSize);
        }
    }
}