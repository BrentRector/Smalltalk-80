using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Memory;

/// <summary>
/// Raw object memory management - allocation and access only.
/// No semantic knowledge of Smalltalk classes or methods.
/// This is the lowest level of memory management.
/// </summary>
public sealed class ObjectMemory
{
    private readonly ObjectTable objectTable;
    private OOP _specialObjectsArray = OOP.Nil;
    private uint nextObjectIndex = 1; // Start at 1, 0 is reserved for nil
    private long totalAllocations;
    private long totalAllocatedBytes;

    /// <summary>
    /// Creates a new object memory with the given initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for the object table.</param>
    public ObjectMemory(int initialCapacity = 65536)
    {
        objectTable = new(initialCapacity);
        
        // Pre-allocate nil object at index 0
        var nilHeader = new ObjectHeader(OOP.Nil, 0, format: 0)
        {
            IsPermanent = true // nil is never collected
        };
        var nilObject = new ObjectLayout(nilHeader, 0);
        objectTable.StoreObject(0, nilObject);
    }
    
    /// <summary>
    /// Gets the total number of allocations made.
    /// </summary>
    public long TotalAllocations => totalAllocations;
    
    /// <summary>
    /// Gets the total bytes allocated.
    /// </summary>
    public long TotalAllocatedBytes => totalAllocatedBytes;
    
    /// <summary>
    /// Gets the Special Objects Array, which is the sole GC root.
    /// </summary>
    public OOP SpecialObjectsArray => _specialObjectsArray;
    
    /// <summary>
    /// Sets the Special Objects Array as the sole GC root.
    /// </summary>
    /// <param name="specialObjectsArray">The Special Objects Array to use as the GC root.</param>
    public void SetSpecialObjectsArray(OOP specialObjectsArray)
    {
        _specialObjectsArray = specialObjectsArray;
    }

    /// <summary>
    /// Allocates a raw object with the given number of fields.
    /// Class OOP can be a placeholder during bootstrap Phase 0-1.
    /// </summary>
    public OOP AllocateObject(OOP classOOP, int fieldCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldCount);

        var header = new ObjectHeader(classOOP, fieldCount, format: 0x10); // Has pointers
        var layout = new ObjectLayout(header, fieldCount);
        
        uint index = AllocateIndex();
        objectTable.StoreObject(index, layout);
        
        // Track allocation statistics
        totalAllocations++;
        totalAllocatedBytes += CalculateObjectSize(layout);
        
        return OOP.FromObjectIndex(index);
    }

    /// <summary>
    /// Allocates a byte array object.
    /// </summary>
    public OOP AllocateByteArray(OOP byteArrayClassOOP, int byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);

        var layout = ObjectLayout.ForByteArray(byteArrayClassOOP, byteCount);
        
        uint index = AllocateIndex();
        objectTable.StoreObject(index, layout);
        
        // Track allocation statistics
        totalAllocations++;
        totalAllocatedBytes += CalculateObjectSize(layout);
        
        return OOP.FromObjectIndex(index);
    }

    /// <summary>
    /// Allocates a word array object.
    /// </summary>
    public OOP AllocateWordArray(OOP arrayClassOOP, int elementCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementCount);

        var layout = ObjectLayout.ForWordArray(arrayClassOOP, elementCount);
        
        uint index = AllocateIndex();
        objectTable.StoreObject(index, layout);
        
        // Track allocation statistics
        totalAllocations++;
        totalAllocatedBytes += CalculateObjectSize(layout);
        
        return OOP.FromObjectIndex(index);
    }

    /// <summary>
    /// Gets the object layout for the given OOP.
    /// Returns null if OOP is not a valid object pointer.
    /// </summary>
    public ObjectLayout? GetObject(OOP oop) =>
        oop.IsSmallInteger ? null : objectTable.GetObject(oop.ObjectIndex);

    /// <summary>
    /// Updates the class OOP for an object.
    /// Used during bootstrap Phase 2 to fix temporary class pointers.
    /// </summary>
    public void UpdateClassPointer(OOP objectOOP, OOP newClassOOP)
    {
        var layout = GetObject(objectOOP)
            ?? throw new ArgumentException("Invalid object OOP", nameof(objectOOP));
        
        layout.Header.ClassOOP = newClassOOP;
    }

    /// <summary>
    /// Gets a field value from an object.
    /// </summary>
    public OOP GetField(OOP objectOOP, int fieldIndex)
    {
        var layout = GetObject(objectOOP)
            ?? throw new ArgumentException("Invalid object OOP", nameof(objectOOP));
        
        return layout.GetField(fieldIndex);
    }

    /// <summary>
    /// Sets a field value in an object.
    /// </summary>
    public void SetField(OOP objectOOP, int fieldIndex, OOP value)
    {
        var layout = GetObject(objectOOP)
            ?? throw new ArgumentException("Invalid object OOP", nameof(objectOOP));
        
        layout.SetField(fieldIndex, value);
    }

    /// <summary>
    /// Gets the class OOP for an object.
    /// </summary>
    public OOP GetClass(OOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
        {
            // SmallIntegers don't have a class pointer stored
            // The VM must handle this specially
            throw new InvalidOperationException("SmallInteger class must be handled by VM");
        }
        
        var layout = GetObject(objectOOP)
            ?? throw new ArgumentException("Invalid object OOP", nameof(objectOOP));
        
        return layout.Header.ClassOOP;
    }

    /// <summary>
    /// Gets the size (field count) of an object.
    /// </summary>
    public int GetSize(OOP objectOOP)
    {
        if (objectOOP.IsSmallInteger)
            return 0; // SmallIntegers have no fields
        
        var layout = GetObject(objectOOP)
            ?? throw new ArgumentException("Invalid object OOP", nameof(objectOOP));
        
        return layout.Fields.Length;
    }

    /// <summary>
    /// Enumerates all allocated objects for GC or debugging.
    /// </summary>
    public IEnumerable<(OOP oop, ObjectLayout layout)> EnumerateObjects() =>
        objectTable.EnumerateObjects();

    
    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    public MemoryStatistics GetStatistics() => new()
    {
        TotalObjects = objectTable.Count,
        TotalCapacity = objectTable.Capacity,
        NextIndex = nextObjectIndex,
        TotalAllocations = totalAllocations,
        TotalAllocatedBytes = totalAllocatedBytes,
        GCRootsCount = _specialObjectsArray.IsNil ? 0 : 1
    };

    /// <summary>
    /// Swaps two objects in memory (for become: primitive).
    /// All references to first will point to second and vice versa.
    /// </summary>
    /// <param name="first">First object to swap.</param>
    /// <param name="second">Second object to swap.</param>
    public void SwapObjects(OOP first, OOP second)
    {
        if (first.IsSmallInteger || second.IsSmallInteger)
            throw new ArgumentException("Cannot swap SmallIntegers");
        
        var firstLayout = GetObject(first);
        var secondLayout = GetObject(second);
        
        if (firstLayout == null || secondLayout == null)
            throw new ArgumentException("Invalid object OOP for swap");
        
        // Swap the objects in the table
        objectTable.StoreObject(first.ObjectIndex, secondLayout);
        objectTable.StoreObject(second.ObjectIndex, firstLayout);
    }

    /// <summary>
    /// Forwards all references from one object to another (for becomeForward:).
    /// After this, all references to 'from' will point to 'to'.
    /// </summary>
    /// <param name="from">Object to forward from.</param>
    /// <param name="to">Object to forward to.</param>
    public void ForwardReferences(OOP from, OOP to)
    {
        if (from.IsSmallInteger || to.IsSmallInteger)
            throw new ArgumentException("Cannot forward SmallIntegers");
        
        // This would require scanning all objects and updating references
        // For now, we can swap the object contents
        var fromLayout = GetObject(from);
        var toLayout = GetObject(to);
        
        if (fromLayout == null || toLayout == null)
            throw new ArgumentException("Invalid object OOP for forward");
        
        // Copy 'to' object into 'from' slot
        objectTable.StoreObject(from.ObjectIndex, toLayout);
    }

    /// <summary>
    /// Gets an enumerator for all allocated object OOPs.
    /// Used for instance enumeration primitives.
    /// </summary>
    /// <returns>Enumerator of all object OOPs.</returns>
    public IEnumerator<OOP> GetAllObjects()
    {
        foreach (var (oop, _) in EnumerateObjects())
        {
            yield return oop;
        }
    }
    
    /// <summary>
    /// Removes an object from memory.
    /// Used by the garbage collector to free unreachable objects.
    /// </summary>
    /// <param name="index">The object index to remove.</param>
    public void RemoveObject(uint index)
    {
        objectTable.RemoveObject(index);
    }

    /// <summary>
    /// Marks an object as permanent (never collected).
    /// </summary>
    /// <param name="oop">The object to mark as permanent.</param>
    public void MarkPermanent(OOP oop)
    {
        if (oop.IsSmallInteger || oop.IsNil)
            return;
        
        var layout = GetObject(oop);
        if (layout != null)
        {
            layout.Header.IsPermanent = true;
        }
    }
    
    /// <summary>
    /// Compacts the object table after GC.
    /// </summary>
    internal void Compact()
    {
        // Compaction would involve:
        // 1. Creating a new contiguous block of memory
        // 2. Copying all live objects to the new block
        // 3. Updating all references to point to new locations
        // 4. Freeing the old memory block
        
        // For now, we rely on the object table's internal management
        // Full compaction requires a more sophisticated approach
        // that updates all OOP references throughout the system
    }
    
    /// <summary>
    /// Calculates the memory size of an object layout.
    /// </summary>
    private static long CalculateObjectSize(ObjectLayout layout)
    {
        const int headerSize = 16; // Estimated header overhead
        var fieldsSize = layout.Fields.Length * sizeof(uint); // Each OOP is 4 bytes
        return headerSize + fieldsSize;
    }

    private uint AllocateIndex()
    {
        // Simple linear allocation for now
        // In a production system, we would maintain a free list
        // of indices that have been freed by the GC
        return nextObjectIndex++;
    }
    
    /// <summary>
    /// Performs validation checks on the object memory.
    /// </summary>
    public bool Validate()
    {
        try
        {
            // Check that nil exists at index 0
            var nilObject = GetObject(OOP.Nil);
            if (nilObject == null || !nilObject.Header.IsPermanent)
                return false;
            
            // Check that the Special Objects Array is valid if set
            if (!_specialObjectsArray.IsNil && !_specialObjectsArray.IsSmallInteger)
            {
                var specialObjects = GetObject(_specialObjectsArray);
                if (specialObjects == null)
                    return false;
            }
            
            // Check that all objects have valid class pointers
            foreach (var (oop, layout) in EnumerateObjects())
            {
                if (!layout.Header.ClassOOP.IsNil && !layout.Header.ClassOOP.IsSmallInteger)
                {
                    var classObj = GetObject(layout.Header.ClassOOP);
                    if (classObj == null)
                        return false;
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}