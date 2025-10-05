namespace Smalltalk80.Interpreter.Memory;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Object Memory manages the mapping between Object-Oriented Pointers (OOPs)
/// and actual Smalltalk objects. This indirection layer is essential for
/// implementing the become: primitive (#72), which swaps object identities.
///
/// Per Blue Book specification, object pointers are 4-byte aligned addresses
/// with LSB = 0 (distinguishing them from SmallInteger immediate values).
/// </summary>
public static class ObjectMemory
{
    // Object table: maps OOP → SObject
    // Using concurrent dictionary for thread-safety during GC
    private static readonly ConcurrentDictionary<nint, WeakReference<SObject>> objectTable = new();

    // Reverse mapping: SObject → OOP (for quick lookups)
    private static readonly ConditionalWeakTable<SObject, OopHolder> reverseTable = new();

    // Next available OOP (always even, 4-byte aligned)
    private static nint nextOop = 0x1000;  // Start at reasonable offset

    // Lock for OOP allocation
    private static readonly object allocationLock = new();

    /// <summary>
    /// Helper class to hold OOP values in ConditionalWeakTable.
    /// </summary>
    private class OopHolder
    {
        public nint Oop { get; set; }
    }

    /// <summary>
    /// Gets or creates an OOP for the given Smalltalk object.
    /// </summary>
    /// <param name="obj">The Smalltalk object</param>
    /// <returns>4-byte aligned OOP (LSB = 0)</returns>
    public static nint GetOop(SObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Check if object already has an OOP
        if (reverseTable.TryGetValue(obj, out var holder))
        {
            return holder.Oop;
        }

        // Allocate new OOP
        lock (allocationLock)
        {
            // Double-check after acquiring lock
            if (reverseTable.TryGetValue(obj, out holder))
            {
                return holder.Oop;
            }

            // Allocate next available OOP (ensure 4-byte alignment)
            nint oop = nextOop;
            nextOop += 4;  // Next OOP (4-byte aligned)

            // Verify alignment (LSB must be 0)
            if ((oop & 1) != 0)
                throw new InvalidOperationException("OOP not properly aligned");

            // Register in both tables
            objectTable[oop] = new WeakReference<SObject>(obj);
            reverseTable.Add(obj, new OopHolder { Oop = oop });

            return oop;
        }
    }

    /// <summary>
    /// Retrieves the Smalltalk object for the given OOP.
    /// </summary>
    /// <param name="oop">The Object-Oriented Pointer</param>
    /// <returns>The Smalltalk object, or null if not found/collected</returns>
    public static SObject? GetObject(nint oop)
    {
        if (oop == 0)
            return null;

        if (objectTable.TryGetValue(oop, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var obj))
            {
                return obj;
            }
            else
            {
                // Object was garbage collected, remove from table
                objectTable.TryRemove(oop, out _);
                return null;
            }
        }

        throw new InvalidOperationException($"Invalid OOP: 0x{oop:X}");
    }

    /// <summary>
    /// Implements the become: primitive (#72).
    /// Swaps the identities of two objects by swapping their OOPs.
    /// After this operation, all references to obj1 now point to obj2 and vice versa.
    /// </summary>
    /// <param name="obj1">First object</param>
    /// <param name="obj2">Second object</param>
    public static void Become(SObject obj1, SObject obj2)
    {
        if (obj1 == null || obj2 == null)
            throw new ArgumentNullException("Cannot become: with nil");

        lock (allocationLock)
        {
            // Get OOPs for both objects
            if (!reverseTable.TryGetValue(obj1, out var holder1))
                throw new InvalidOperationException("obj1 has no OOP");
            if (!reverseTable.TryGetValue(obj2, out var holder2))
                throw new InvalidOperationException("obj2 has no OOP");

            nint oop1 = holder1.Oop;
            nint oop2 = holder2.Oop;

            // Swap the mappings in object table
            objectTable[oop1] = new WeakReference<SObject>(obj2);
            objectTable[oop2] = new WeakReference<SObject>(obj1);

            // Swap the reverse mappings
            holder1.Oop = oop2;
            holder2.Oop = oop1;

            // Note: All existing SObjectRef instances pointing to oop1 now resolve to obj2
            // and vice versa. This is the magic of the indirection!
        }
    }

    /// <summary>
    /// Performs garbage collection, removing dead weak references from the object table.
    /// </summary>
    public static void CollectGarbage()
    {
        var deadOops = new List<nint>();

        foreach (var kvp in objectTable)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                deadOops.Add(kvp.Key);
            }
        }

        foreach (var oop in deadOops)
        {
            objectTable.TryRemove(oop, out _);
        }

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] GC: Removed {deadOops.Count} dead OOPs, {objectTable.Count} live objects");
        }
    }

    /// <summary>
    /// Gets statistics about the object memory.
    /// </summary>
    public static (int LiveObjects, int AllocatedOops) GetStatistics()
    {
        int liveCount = 0;
        foreach (var kvp in objectTable)
        {
            if (kvp.Value.TryGetTarget(out _))
                liveCount++;
        }

        int allocatedOops = (int)((nextOop - 0x1000) / 4);
        return (liveCount, allocatedOops);
    }

    /// <summary>
    /// Clears all object mappings (for testing/reset only).
    /// </summary>
    internal static void Clear()
    {
        objectTable.Clear();
        // Can't clear ConditionalWeakTable, but it will clean itself up
        nextOop = 0x1000;
    }
}