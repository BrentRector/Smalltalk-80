using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Manages the Smalltalk object memory with manual garbage collection support.
/// Uses Option D: Explicit memory management with manual GC while maintaining
/// full support for become: and instance enumeration.
/// </summary>
public static class ObjectMemory
{
    // Object table - uses array for O(1) access by OOP
    private static SmalltalkObject?[] objects = new SmalltalkObject?[65536]; // Initial capacity
    private static int nextFreeIndex = 0;
    private static readonly Queue<int> freeList = new();
    
    // GC roots - objects that should never be collected
    private static readonly HashSet<SmalltalkObject> permanentRoots = new();
    private static readonly HashSet<SmalltalkObject> transientRoots = new();
    
    // Object type indexing for faster instance enumeration
    private static readonly Dictionary<SmalltalkObject, HashSet<int>> typeIndex = new();
    private static readonly object typeIndexLock = new();
    
    // GC configuration
    private const int HIGH_WATER_MARK = 50000;  // Trigger GC when this many objects allocated
    private const int LOW_FREE_COUNT = 1000;    // Trigger GC when free list below this
    private const int GROWTH_FACTOR = 2;        // How much to grow object table when full
    
    // GC statistics
    private static int totalAllocations = 0;
    private static int totalCollections = 0;
    private static int totalObjectsFreed = 0;
    private static DateTime lastGCTime = DateTime.MinValue;
    
    /// <summary>Gets a read-only view of all registered objects.</summary>
    public static IReadOnlyList<SmalltalkObject?> Objects => objects;
    
    /// <summary>The singleton nil object (instance of UndefinedObject).</summary>
    public static SmalltalkObject? NilObject { get; set; }
    
    /// <summary>The singleton true object (instance of True).</summary>
    public static SmalltalkObject? TrueObject { get; set; }
    
    /// <summary>The singleton false object (instance of False).</summary>
    public static SmalltalkObject? FalseObject { get; set; }
    
    /// <summary>Gets the current number of allocated objects.</summary>
    public static int AllocatedObjectCount => nextFreeIndex - freeList.Count;
    
    /// <summary>Gets the total number of GC collections performed.</summary>
    public static int GCCollectionCount => totalCollections;
    
    /// <summary>Gets the total number of objects freed by GC.</summary>
    public static int TotalObjectsFreed => totalObjectsFreed;

    /// <summary>
    /// Registers a new object with the object table assigning it an OOP value.
    /// May trigger garbage collection if memory pressure is high.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    internal static void Register(SmalltalkObject obj)
    {
        int index;
        
        // Try to reuse a freed slot first
        if (freeList.Count > 0)
        {
            index = freeList.Dequeue();
            objects[index] = obj;
        }
        else
        {
            // Need a new slot
            if (nextFreeIndex >= objects.Length)
            {
                // Grow the object table
                GrowObjectTable();
            }
            
            index = nextFreeIndex++;
            objects[index] = obj;
            
        }
        
        obj.Oop = index << 1;
        totalAllocations++;
        
        // Update type index for faster instance enumeration
        UpdateTypeIndex(obj, index, isRegistration: true);
        
        // Check if we should trigger GC
        if (ShouldTriggerGC())
        {
            ManualGC();
        }
    }
    
    /// <summary>
    /// Grows the object table when it's full.
    /// </summary>
    private static void GrowObjectTable()
    {
        int newSize = objects.Length * GROWTH_FACTOR;
        var newArray = new SmalltalkObject?[newSize];
        Array.Copy(objects, newArray, objects.Length);
        objects = newArray;
        
        Console.WriteLine($"[ObjectMemory] Grew object table from {objects.Length / GROWTH_FACTOR} to {newSize}");
    }
    
    /// <summary>
    /// Determines if garbage collection should be triggered.
    /// </summary>
    private static bool ShouldTriggerGC()
    {
        // Don't GC during very early bootstrap (first 100 objects)
        // These are critical system objects that shouldn't be collected
        if (totalAllocations < 100)
            return false;
            
        // Don't GC too frequently
        if ((DateTime.Now - lastGCTime).TotalSeconds < 1.0)
            return false;
            
        return AllocatedObjectCount > HIGH_WATER_MARK || 
               freeList.Count < LOW_FREE_COUNT;
    }

    /// <summary>
    /// Adds a permanent root object that will never be garbage collected.
    /// Use for system objects like classes, Smalltalk dictionary, etc.
    /// </summary>
    public static void AddPermanentRoot(SmalltalkObject obj)
    {
        permanentRoots.Add(obj);
    }
    
    /// <summary>
    /// Removes a permanent root, allowing it to be collected if unreachable.
    /// </summary>
    public static void RemovePermanentRoot(SmalltalkObject obj)
    {
        permanentRoots.Remove(obj);
    }
    
    /// <summary>
    /// Adds a transient root object (e.g., current execution context).
    /// These are cleared after execution completes.
    /// </summary>
    public static void AddTransientRoot(SmalltalkObject obj)
    {
        transientRoots.Add(obj);
    }
    
    /// <summary>
    /// Clears all transient roots. Call after method execution completes.
    /// </summary>
    public static void ClearTransientRoots()
    {
        transientRoots.Clear();
    }
    
    /// <summary>
    /// Performs manual garbage collection using mark-and-sweep algorithm.
    /// Returns the number of objects freed.
    /// </summary>
    public static int ManualGC()
    {
        var stopwatch = Stopwatch.StartNew();
        var reachable = new HashSet<int>();
        
        // Phase 1: Mark all reachable objects from roots
        foreach (var root in permanentRoots)
        {
            if (root != null)
                MarkReachable(root, reachable);
        }
        
        foreach (var root in transientRoots)
        {
            if (root != null)
                MarkReachable(root, reachable);
        }
        
        // Always mark the special objects as roots
        if (NilObject != null) MarkReachable(NilObject, reachable);
        if (TrueObject != null) MarkReachable(TrueObject, reachable);
        if (FalseObject != null) MarkReachable(FalseObject, reachable);
        
        // Mark the Smalltalk system dictionary if it exists
        var smalltalkDict = FindSystemDictionary();
        if (smalltalkDict != null)
            MarkReachable(smalltalkDict, reachable);
        
        // Phase 2: Sweep unreachable objects
        int freedCount = 0;
        for (int i = 0; i < nextFreeIndex; i++)
        {
            if (objects[i] != null && !reachable.Contains(i))
            {
                var objToFree = objects[i]!;
                UpdateTypeIndex(objToFree, i, isRegistration: false);
                objects[i] = null;
                freeList.Enqueue(i);
                freedCount++;
            }
        }
        
        // Update statistics
        totalCollections++;
        totalObjectsFreed += freedCount;
        lastGCTime = DateTime.Now;
        
        stopwatch.Stop();
        
        // Log GC results if significant
        if (freedCount > 0 || stopwatch.ElapsedMilliseconds > 100)
        {
            Console.WriteLine($"[GC] Collection #{totalCollections}: Freed {freedCount} objects in {stopwatch.ElapsedMilliseconds}ms. " +
                            $"Live objects: {AllocatedObjectCount}, Total freed: {totalObjectsFreed}");
        }
        
        return freedCount;
    }
    
    /// <summary>
    /// Marks an object and all objects reachable from it.
    /// </summary>
    private static void MarkReachable(SmalltalkObject obj, HashSet<int> reachable)
    {
        if (obj == null) return;
        
        int index = obj.Oop >> 1;
        if (reachable.Contains(index))
            return; // Already marked
            
        reachable.Add(index);
        
        // Mark the class pointer
        if (obj.ClassPointer != null)
            MarkReachable(obj.ClassPointer, reachable);
        
        // Mark all fields
        foreach (var field in obj.Fields)
        {
            if (field is SmalltalkObject fieldObj)
                MarkReachable(fieldObj, reachable);
        }
    }
    
    /// <summary>
    /// Attempts to find the Smalltalk system dictionary.
    /// </summary>
    private static SmalltalkObject? FindSystemDictionary()
    {
        // Look for an object that might be the Smalltalk dictionary
        // This is typically a SystemDictionary instance
        for (int i = 0; i < nextFreeIndex; i++)
        {
            var obj = objects[i];
            if (obj?.ClassPointer != null)
            {
                // Check if this might be SystemDictionary
                var className = TryGetClassName(obj.ClassPointer);
                if (className == "SystemDictionary")
                    return obj;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Try to get a class name for debugging/logging.
    /// </summary>
    private static string? TryGetClassName(SmalltalkObject cls)
    {
        // This is a simplified version - you might have a better way
        // to get class names in your implementation
        if (cls.Fields.Length > 6 && cls.Fields[6] is string name)
            return name;
        return null;
    }
    
    /// <summary>
    /// Forces a full garbage collection regardless of thresholds.
    /// Use sparingly as this can be expensive.
    /// </summary>
    public static void ForceGC()
    {
        Console.WriteLine("[GC] Forced collection requested");
        ManualGC();
    }
    
    /// <summary>
    /// Gets memory statistics for monitoring.
    /// </summary>
    public static (int allocated, int free, int total, int collections) GetMemoryStats()
    {
        return (
            allocated: AllocatedObjectCount,
            free: freeList.Count,
            total: nextFreeIndex,
            collections: totalCollections
        );
    }

    /// <summary>Clears the object table. Primarily used for tests.</summary>
    public static void Reset()
    {
        Array.Clear(objects, 0, nextFreeIndex);
        nextFreeIndex = 0;
        freeList.Clear();
        permanentRoots.Clear();
        transientRoots.Clear();
        lock (typeIndexLock)
        {
            typeIndex.Clear();
        }
        totalAllocations = 0;
        totalCollections = 0;
        totalObjectsFreed = 0;
        lastGCTime = DateTime.MinValue;
    }

    /// <summary>Determines whether an object exists for the specified pointer.</summary>
    /// <param name="oop">The encoded object pointer.</param>
    public static bool HasObject(int oop)
    {
        int index = oop >> 1;
        return index >= 0 && index < objects.Length && objects[index] != null;
    }

    /// <summary>Resolves an encoded pointer to the corresponding object.</summary>
    /// <param name="oop">The encoded object pointer.</param>
    /// <returns>The referenced object or <c>null</c>.</returns>
    public static SmalltalkObject? FromOop(int oop)
    {
        int index = oop >> 1;
        return (index >= 0 && index < objects.Length) ? objects[index] : null;
    }

    /// <summary>Obtains the encoded pointer for the specified object.</summary>
    /// <param name="obj">The object to encode.</param>
    /// <returns>An integer encoding the object pointer.</returns>
    public static int ToOop(SmalltalkObject obj) => obj.Oop;

    /// <summary>
    /// Finds an arbitrary instance of the given class if one exists.
    /// Note: This skips over freed slots for efficiency.
    /// </summary>
    /// <param name="cls">The class to search for.</param>
    /// <returns>The first instance found or <c>null</c>.</returns>
    public static SmalltalkObject? SomeInstance(SmalltalkObject cls)
    {
        for (int i = 0; i < nextFreeIndex; i++)
        {
            var obj = objects[i];
            if (obj != null && ReferenceEquals(obj.ClassPointer, cls))
                return obj;
        }
        return null;
    }

    /// <summary>
    /// Returns the instance following the specified object in enumeration order.
    /// </summary>
    /// <param name="obj">An existing instance.</param>
    /// <returns>The next instance of the same class or <c>null</c>.</returns>
    public static SmalltalkObject? NextInstance(SmalltalkObject obj)
    {
        int start = obj.Oop >> 1;
        var cls = obj.ClassPointer;
        
        for (int i = start + 1; i < nextFreeIndex; i++)
        {
            var candidate = objects[i];
            if (candidate != null && ReferenceEquals(candidate.ClassPointer, cls))
                return candidate;
        }
        
        return null;
    }

    /// <summary>
    /// Swaps the identity of two objects and updates all references.
    /// This operation is expensive as it must scan all live objects.
    /// </summary>
    /// <param name="a">The first object.</param>
    /// <param name="b">The second object.</param>
    public static void SwapPointers(SmalltalkObject a, SmalltalkObject b)
    {
        int indexA = a.Oop >> 1;
        int indexB = b.Oop >> 1;

        // Swap in object table
        (objects[indexA], objects[indexB]) = (objects[indexB], objects[indexA]);
        (a.Oop, b.Oop) = (b.Oop, a.Oop);

        // Update all references in all live objects
        for (int i = 0; i < nextFreeIndex; i++)
        {
            var obj = objects[i];
            if (obj != null)
            {
                // Update class pointer if needed
                if (ReferenceEquals(obj.ClassPointer, a)) obj.ClassPointer = b;
                else if (ReferenceEquals(obj.ClassPointer, b)) obj.ClassPointer = a;
                
                // Update fields
                for (int j = 0; j < obj.Fields.Length; j++)
                {
                    if (ReferenceEquals(obj.Fields[j], a)) obj.Fields[j] = b;
                    else if (ReferenceEquals(obj.Fields[j], b)) obj.Fields[j] = a;
                }
            }
        }
    }
    
    /// <summary>
    /// Replace an object at a specific OOP with a new object.
    /// Used to fix objects that were created incorrectly.
    /// </summary>
    public static void ReplaceObject(int oop, SmalltalkObject newObject)
    {
        int index = oop >> 1;
        if (index >= 0 && index < objects.Length)
        {
            var oldObject = objects[index];
            objects[index] = newObject;
            newObject.Oop = oop; // Ensure the new object has the correct OOP
        }
    }
    
    /// <summary>
    /// Enumerates all live objects. Used for debugging and statistics.
    /// </summary>
    public static IEnumerable<SmalltalkObject> EnumerateLiveObjects()
    {
        for (int i = 0; i < nextFreeIndex; i++)
        {
            var obj = objects[i];
            if (obj != null)
                yield return obj;
        }
    }
    
    /// <summary>
    /// Gets the number of objects of a specific class.
    /// Uses the type index for improved performance.
    /// </summary>
    public static int CountInstancesOf(SmalltalkObject cls)
    {
        lock (typeIndexLock)
        {
            return typeIndex.TryGetValue(cls, out var instances) ? instances.Count : 0;
        }
    }
    
    /// <summary>
    /// Updates the type index when objects are registered or freed.
    /// </summary>
    /// <param name="obj">The object being registered or freed.</param>
    /// <param name="index">The object table index.</param>
    /// <param name="isRegistration">True if registering, false if freeing.</param>
    private static void UpdateTypeIndex(SmalltalkObject obj, int index, bool isRegistration)
    {
        var classPointer = obj.ClassPointer;
        if (classPointer == null) return;
        
        lock (typeIndexLock)
        {
            if (isRegistration)
            {
                // Add object to type index
                if (!typeIndex.TryGetValue(classPointer, out var instances))
                {
                    instances = new HashSet<int>();
                    typeIndex[classPointer] = instances;
                }
                instances.Add(index);
            }
            else
            {
                // Remove object from type index
                if (typeIndex.TryGetValue(classPointer, out var instances))
                {
                    instances.Remove(index);
                    // Clean up empty sets to prevent memory leaks
                    if (instances.Count == 0)
                    {
                        typeIndex.Remove(classPointer);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Finds an arbitrary instance of the given class if one exists.
    /// Uses the type index for O(1) performance instead of O(n) linear search.
    /// </summary>
    /// <param name="cls">The class to search for.</param>
    /// <returns>The first instance found or <c>null</c>.</returns>
    public static SmalltalkObject? SomeInstanceOptimized(SmalltalkObject cls)
    {
        lock (typeIndexLock)
        {
            if (typeIndex.TryGetValue(cls, out var instances) && instances.Count > 0)
            {
                // Get any instance from the set
                int index = instances.First();
                return objects[index];
            }
        }
        return null;
    }
    
    /// <summary>
    /// Enumerates all instances of a specific class.
    /// Uses the type index for O(k) performance where k is the number of instances,
    /// instead of O(n) where n is the total number of objects.
    /// </summary>
    /// <param name="cls">The class to enumerate instances of.</param>
    /// <returns>All live instances of the specified class.</returns>
    public static IEnumerable<SmalltalkObject> EnumerateInstancesOf(SmalltalkObject cls)
    {
        HashSet<int> instanceIndices;
        lock (typeIndexLock)
        {
            if (!typeIndex.TryGetValue(cls, out var foundInstances) || foundInstances == null)
            {
                yield break; // No instances of this class
            }
            // Create a copy to avoid holding the lock during enumeration
            instanceIndices = new HashSet<int>(foundInstances);
        }
        
        foreach (int index in instanceIndices)
        {
            var obj = objects[index];
            if (obj != null) // Double-check in case GC ran between lock release and now
            {
                yield return obj;
            }
        }
    }
    
    /// <summary>
    /// Gets statistics about the type index for monitoring and debugging.
    /// </summary>
    /// <returns>Number of classes indexed and total instances tracked.</returns>
    public static (int classesIndexed, int totalInstances) GetTypeIndexStats()
    {
        lock (typeIndexLock)
        {
            int totalInstances = 0;
            foreach (var instances in typeIndex.Values)
            {
                totalInstances += instances.Count;
            }
            return (typeIndex.Count, totalInstances);
        }
    }
    
    /// <summary>
    /// Gets all classes that have at least one live instance.
    /// Useful for debugging and introspection.
    /// </summary>
    /// <returns>All classes with live instances.</returns>
    public static IEnumerable<SmalltalkObject> GetClassesWithInstances()
    {
        lock (typeIndexLock)
        {
            return typeIndex.Keys.ToList(); // Return a copy to avoid holding the lock
        }
    }
}