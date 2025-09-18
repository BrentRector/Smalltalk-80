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
    
    // GC configuration - adaptive thresholds
    private const int GROWTH_FACTOR = 2;        // How much to grow object table when full
    
    // VM reference for accessing Special Objects Array
    private static VirtualMachine? vm;
    
    // Flag to indicate if GC can run (special objects are set up)
    private static bool isGCEnabled = false;
    
    // Adaptive GC thresholds
    private static int adaptiveHighWaterMark = 50000;    // Dynamic threshold for triggering GC
    private static int adaptiveLowFreeCount = 1000;     // Dynamic threshold for free list size
    private static double gcEfficiencyTarget = 0.15;    // Target: GC should free at least 15% of objects
    private static double gcTimeTarget = 50.0;          // Target: GC should complete within 50ms
    
    // GC adaptation state
    private static readonly Queue<double> recentGcEfficiencies = new();
    private static readonly Queue<double> recentGcTimes = new();
    private static int gcAdaptationCycle = 0;
    private const int ADAPTATION_WINDOW = 5;             // Number of GCs to consider for adaptation
    
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
    /// Determines if garbage collection should be triggered using adaptive thresholds.
    /// </summary>
    private static bool ShouldTriggerGC()
    {
        // CRITICAL: During bootstrap, never auto-trigger GC
        // GC will only run when explicitly enabled
        if (!isGCEnabled)
            return false;
            
        // For now, disable automatic GC entirely during bootstrap
        // Future enhancement: enable automatic GC after bootstrap with proper Process/Context roots
        return false;
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
        return ManualGCInternal(isValidationGC: false);
    }
    
    /// <summary>
    /// Internal GC implementation with diagnostic flag.
    /// </summary>
    private static int ManualGCInternal(bool isValidationGC)
    {
        var stopwatch = Stopwatch.StartNew();
        var reachable = new HashSet<int>();
        
        // Phase 1: Mark all reachable objects from Smalltalk-80 root set
        
        // 1. Permanent roots (includes Special Objects Array)
        foreach (var root in permanentRoots)
        {
            if (root != null)
                MarkReachable(root, reachable);
        }
        
        // 2. Transient roots (active interpreter state, contexts)
        foreach (var root in transientRoots)
        {
            if (root != null)
                MarkReachable(root, reachable);
        }
        
        // 3. VM's Special Objects Array contents - each slot is a GC root
        if (vm != null)
        {
            var specialObjects = vm.GetSpecialObjectsArray();
            if (specialObjects != null)
            {
                int nonNullCount = specialObjects.Count(o => o != null);
                Console.WriteLine($"[GC] Marking VM Special Objects Array as roots ({nonNullCount} non-null objects)");
                for (int i = 0; i < specialObjects.Length; i++)
                {
                    var obj = specialObjects[i];
                    if (obj != null)
                    {
                        MarkReachable(obj, reachable);
                        Console.WriteLine($"[GC]   Slot {i}: marked object OOP {obj.Oop} as root");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("[GC] WARNING: No VM reference for Special Objects Array");
        }
        
        // 4. Legacy fallback: individual special objects if Special Objects Array isn't available
        // This ensures backward compatibility and handles the transition period
        if (NilObject != null) MarkReachable(NilObject, reachable);
        if (TrueObject != null) MarkReachable(TrueObject, reachable);
        if (FalseObject != null) MarkReachable(FalseObject, reachable);
        
        // 5. Legacy fallback: SystemDictionary lookup if not in Special Objects Array
        var smalltalkDict = FindSystemDictionary();
        if (smalltalkDict != null)
            MarkReachable(smalltalkDict, reachable);
        
        // Debug: Log how many objects are marked as reachable
        Console.WriteLine($"[GC] Marked {reachable.Count} objects as reachable");
        
        // Phase 2: Sweep unreachable objects
        int freedCount = 0;
        var freedByType = new Dictionary<string, int>();
        var freedExamples = new Dictionary<string, List<string>>();
        
        for (int i = 0; i < nextFreeIndex; i++)
        {
            if (objects[i] != null && !reachable.Contains(i))
            {
                var objToFree = objects[i]!;
                
                // Collect diagnostic information about what's being freed
                string typeName = "Unknown";
                string details = "";
                
                if (objToFree.ClassPointer != null)
                {
                    // Try to get class name - for Class objects, the name is in field 5
                    // But first check if this is a class or metaclass
                    var classPtr = objToFree.ClassPointer;
                    
                    // Get the name from the class object
                    if (classPtr.Fields.Length > 5)
                    {
                        // Class name is in field 5 for Class objects
                        if (classPtr.Fields[5] is string className)
                        {
                            typeName = className;
                        }
                        else if (classPtr.Fields[5] is SmalltalkObject nameSymbol && 
                                 nameSymbol.Fields.Length > 0 && 
                                 nameSymbol.Fields[0] is string symName)
                        {
                            // Sometimes the name is a Symbol object
                            typeName = symName;
                        }
                    }
                    
                    // Collect details for specific types
                    if (typeName == "String" && objToFree.Fields.Length > 0 && objToFree.Fields[0] is string strValue)
                    {
                        details = $"'{strValue}'";
                    }
                    else if (typeName == "Symbol" && objToFree.Fields.Length > 0 && objToFree.Fields[0] is string symValue)
                    {
                        details = $"#{symValue}";
                    }
                    else if (typeName == "CompiledMethod" && objToFree.Fields.Length >= 2)
                    {
                        var selectorField = objToFree.Fields[0];
                        if (selectorField is SmalltalkObject selObj && selObj.Fields.Length > 0 && selObj.Fields[0] is string selector)
                        {
                            details = $"selector: {selector}";
                        }
                    }
                    else if (typeName == "Array")
                    {
                        details = $"size: {objToFree.Fields.Length}";
                    }
                }
                
                // Track freed objects by type
                if (!freedByType.ContainsKey(typeName))
                {
                    freedByType[typeName] = 0;
                    freedExamples[typeName] = new List<string>();
                }
                freedByType[typeName]++;
                
                // Collect up to 3 examples per type
                if (freedExamples[typeName].Count < 3 && !string.IsNullOrEmpty(details))
                {
                    freedExamples[typeName].Add($"OOP {objToFree.Oop} {details}");
                }
                
                UpdateTypeIndex(objToFree, i, isRegistration: false);
                objects[i] = null;
                freeList.Enqueue(i);
                freedCount++;
            }
        }
        
        // Log what was freed if validation GC
        if (isValidationGC && freedCount > 0)
        {
            Console.WriteLine($"[GC-DIAGNOSTIC] Objects freed by type:");
            foreach (var kvp in freedByType.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value} objects");
                if (freedExamples.ContainsKey(kvp.Key) && freedExamples[kvp.Key].Count > 0)
                {
                    foreach (var example in freedExamples[kvp.Key])
                    {
                        Console.WriteLine($"    Example: {example}");
                    }
                }
            }
        }
        
        // Update statistics
        totalCollections++;
        totalObjectsFreed += freedCount;
        lastGCTime = DateTime.Now;
        
        stopwatch.Stop();
        
        // Calculate GC efficiency and adapt thresholds
        double efficiency = AllocatedObjectCount > 0 ? (double)freedCount / (AllocatedObjectCount + freedCount) : 0.0;
        double gcTime = stopwatch.ElapsedMilliseconds;
        
        AdaptGCThresholds(efficiency, gcTime, freedCount);
        
        // Log GC results if significant
        if (freedCount > 0 || stopwatch.ElapsedMilliseconds > 100)
        {
            Console.WriteLine($"[GC] Collection #{totalCollections}: Freed {freedCount} objects in {stopwatch.ElapsedMilliseconds}ms. " +
                            $"Live objects: {AllocatedObjectCount}, Total freed: {totalObjectsFreed}, Efficiency: {efficiency:P1}");
        }
        
        return freedCount;
    }
    
    /// <summary>
    /// Marks an object and all objects reachable from it.
    /// Special handling for MethodDictionary, Association, and CompiledMethod objects.
    /// </summary>
    private static void MarkReachable(SmalltalkObject obj, HashSet<int> reachable, int depth = 0)
    {
        if (obj == null) return;
        
        int index = obj.Oop >> 1;
        if (reachable.Contains(index))
            return; // Already marked
            
        reachable.Add(index);
        
        // Optional deep tracing for debugging (controlled by environment variable or flag)
        bool deepTrace = Environment.GetEnvironmentVariable("SMALLTALK_GC_TRACE") == "deep";
        string? className = null;
        if (deepTrace && depth < 3)
        {
            className = obj.ClassPointer != null ? TryGetClassName(obj.ClassPointer) : null;
            className = className ?? obj.ClassPointer?.ToString() ?? "Unknown";
            Console.WriteLine($"[GC]   {"  ".PadLeft(depth * 2)}Marking OOP {obj.Oop} (class: {className}, {obj.Fields.Length} fields)");
        }
        
        // Mark the class pointer
        if (obj.ClassPointer != null)
            MarkReachable(obj.ClassPointer, reachable, depth + 1);
        
        // Simple GC: Just mark ALL fields that could be object pointers
        // The format word in the class tells us how many fields to scan, but
        // for simplicity, we'll scan all fields and check if they're valid OOPs
        
        // Debug for SystemDictionary
        if (obj.Oop == 412)
        {
            int objectFields = 0;
            int oopFields = 0;
            int nilFields = 0;
            
            foreach (var field in obj.Fields)
            {
                if (field == null) nilFields++;
                else if (field is SmalltalkObject so) 
                {
                    objectFields++;
                    // Check if it's the nil object
                    if (so.Oop == 42) nilFields++; // nil is OOP 42
                }
                else if (field is int oop && oop > 0 && (oop & 1) == 0) oopFields++;
                
                MarkFieldAsReachable(field, reachable, depth + 1);
            }
            
            Console.WriteLine($"[GC] SystemDictionary fields: {objectFields} objects, {oopFields} OOPs, {nilFields} nils");
        }
        else
        {
            foreach (var field in obj.Fields)
            {
                MarkFieldAsReachable(field, reachable, depth + 1);
            }
        }
    }
    
    /// <summary>
    /// Helper method to mark a field as reachable, handling both object references and OOP integers.
    /// </summary>
    private static void MarkFieldAsReachable(object? field, HashSet<int> reachable, int depth)
    {
        if (field is SmalltalkObject fieldObj)
        {
            MarkReachable(fieldObj, reachable, depth);
        }
        else if (field is int oop && oop >= 0 && (oop & 1) == 0 && HasObject(oop))
        {
            // Handle OOP references stored as integers
            // OOPs are even numbers (including 0), SmallIntegers have low bit set
            var referencedObj = FromOop(oop);
            if (referencedObj != null)
                MarkReachable(referencedObj, reachable, depth);
        }
    }
    
    /// <summary>
    /// Attempts to find the Smalltalk system dictionary.
    /// </summary>
    private static SmalltalkObject? FindSystemDictionary()
    {
        // The SystemDictionary is now marked directly through VM's Special Objects Array
        // This method is kept for legacy compatibility but may not be needed
        Console.WriteLine("[GC] SystemDictionary now marked through VM Special Objects Array");
        return null;
    }
    
    /// <summary>
    /// Try to get a class name for debugging/logging.
    /// </summary>
    private static string? TryGetClassName(SmalltalkObject cls)
    {
        // Class name is in field 5 as a Symbol object
        if (cls.Fields.Length > 5)
        {
            if (cls.Fields[5] is string name)
                return name;
            else if (cls.Fields[5] is SmalltalkObject nameSymbol && 
                     nameSymbol.Fields.Length > 0 && 
                     nameSymbol.Fields[0] is string symName)
                return symName;
            else if (cls.Fields[5] is int nameOop && nameOop > 0 && (nameOop & 1) == 0)
            {
                // Sometimes the name is stored as an OOP
                var nameObj = FromOop(nameOop);
                if (nameObj != null && nameObj.Fields.Length > 0 && nameObj.Fields[0] is string symStr)
                    return symStr;
            }
        }
        // Try field 6 as fallback (some inconsistency in the codebase)
        if (cls.Fields.Length > 6)
        {
            if (cls.Fields[6] is string name)
                return name;
            else if (cls.Fields[6] is SmalltalkObject nameSymbol && 
                     nameSymbol.Fields.Length > 0 && 
                     nameSymbol.Fields[0] is string symName)
                return symName;
        }
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
        
        // Reset adaptive GC state
        adaptiveHighWaterMark = 50000;
        adaptiveLowFreeCount = 1000;
        recentGcEfficiencies.Clear();
        recentGcTimes.Clear();
        gcAdaptationCycle = 0;
        
        // Reset GC and VM state
        isGCEnabled = false;
        vm = null;
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
    
    /// <summary>
    /// Adapts GC thresholds based on recent performance metrics.
    /// </summary>
    /// <param name="efficiency">The efficiency of the current GC (0.0 to 1.0).</param>
    /// <param name="gcTime">The time taken for the current GC in milliseconds.</param>
    /// <param name="freedCount">The number of objects freed in the current GC.</param>
    private static void AdaptGCThresholds(double efficiency, double gcTime, int freedCount)
    {
        // Track recent performance
        recentGcEfficiencies.Enqueue(efficiency);
        recentGcTimes.Enqueue(gcTime);
        
        // Keep only the most recent measurements
        while (recentGcEfficiencies.Count > ADAPTATION_WINDOW)
        {
            recentGcEfficiencies.Dequeue();
        }
        while (recentGcTimes.Count > ADAPTATION_WINDOW)
        {
            recentGcTimes.Dequeue();
        }
        
        // Only adapt after we have enough data
        if (recentGcEfficiencies.Count < ADAPTATION_WINDOW)
            return;
            
        gcAdaptationCycle++;
        
        // Calculate average performance over the window
        double avgEfficiency = recentGcEfficiencies.Average();
        double avgGcTime = recentGcTimes.Average();
        
        // Adaptation logic
        int oldHighWaterMark = adaptiveHighWaterMark;
        int oldLowFreeCount = adaptiveLowFreeCount;
        
        // If GC is too inefficient (freeing too few objects), increase high water mark
        // to delay GC and allow more garbage to accumulate
        if (avgEfficiency < gcEfficiencyTarget)
        {
            adaptiveHighWaterMark = Math.Min(adaptiveHighWaterMark * 5 / 4, 200000); // Increase by 25%, cap at 200k
            adaptiveLowFreeCount = Math.Min(adaptiveLowFreeCount * 5 / 4, 5000);     // Increase by 25%, cap at 5k
        }
        // If GC is too efficient (freeing many objects), we might be doing GC too late
        // Decrease thresholds to run GC more frequently with smaller collections
        else if (avgEfficiency > gcEfficiencyTarget * 2.0)
        {
            adaptiveHighWaterMark = Math.Max(adaptiveHighWaterMark * 4 / 5, 10000);  // Decrease by 20%, floor at 10k
            adaptiveLowFreeCount = Math.Max(adaptiveLowFreeCount * 4 / 5, 500);      // Decrease by 20%, floor at 500
        }
        
        // If GC is taking too long, increase thresholds to reduce frequency
        if (avgGcTime > gcTimeTarget)
        {
            adaptiveHighWaterMark = Math.Min(adaptiveHighWaterMark * 6 / 5, 200000); // Increase by 20%
            adaptiveLowFreeCount = Math.Min(adaptiveLowFreeCount * 6 / 5, 5000);     // Increase by 20%
        }
        // If GC is very fast, we can afford to run it more frequently
        else if (avgGcTime < gcTimeTarget / 2.0)
        {
            adaptiveHighWaterMark = Math.Max(adaptiveHighWaterMark * 9 / 10, 10000); // Decrease by 10%
            adaptiveLowFreeCount = Math.Max(adaptiveLowFreeCount * 9 / 10, 500);     // Decrease by 10%
        }
        
        // Log adaptation if thresholds changed significantly
        if (Math.Abs(adaptiveHighWaterMark - oldHighWaterMark) > oldHighWaterMark * 0.1 ||
            Math.Abs(adaptiveLowFreeCount - oldLowFreeCount) > oldLowFreeCount * 0.1)
        {
            Console.WriteLine($"[GC Adapt] Cycle {gcAdaptationCycle}: " +
                            $"High water mark: {oldHighWaterMark} -> {adaptiveHighWaterMark}, " +
                            $"Low free count: {oldLowFreeCount} -> {adaptiveLowFreeCount}, " +
                            $"Avg efficiency: {avgEfficiency:P1}, Avg time: {avgGcTime:F1}ms");
        }
    }
    
    /// <summary>
    /// Gets the current adaptive GC thresholds and performance metrics.
    /// </summary>
    /// <returns>Current GC configuration and recent performance data.</returns>
    public static AdaptiveGCStats GetAdaptiveGCStats()
    {
        return new AdaptiveGCStats
        {
            HighWaterMark = adaptiveHighWaterMark,
            LowFreeCount = adaptiveLowFreeCount,
            EfficiencyTarget = gcEfficiencyTarget,
            TimeTarget = gcTimeTarget,
            AdaptationCycle = gcAdaptationCycle,
            RecentEfficiencies = recentGcEfficiencies.ToArray(),
            RecentTimes = recentGcTimes.ToArray(),
            AverageEfficiency = recentGcEfficiencies.Count > 0 ? recentGcEfficiencies.Average() : 0.0,
            AverageTime = recentGcTimes.Count > 0 ? recentGcTimes.Average() : 0.0
        };
    }
    
    /// <summary>
    /// Allows manual tuning of adaptive GC targets for different workloads.
    /// </summary>
    /// <param name="efficiencyTarget">Target GC efficiency (0.0 to 1.0).</param>
    /// <param name="timeTarget">Target GC time in milliseconds.</param>
    public static void SetAdaptiveGCTargets(double efficiencyTarget, double timeTarget)
    {
        if (efficiencyTarget < 0.05 || efficiencyTarget > 0.5)
            throw new ArgumentOutOfRangeException(nameof(efficiencyTarget), "Efficiency target must be between 5% and 50%");
        if (timeTarget < 10.0 || timeTarget > 1000.0)
            throw new ArgumentOutOfRangeException(nameof(timeTarget), "Time target must be between 10ms and 1000ms");
            
        gcEfficiencyTarget = efficiencyTarget;
        gcTimeTarget = timeTarget;
        
        Console.WriteLine($"[GC Adapt] Updated targets: Efficiency={efficiencyTarget:P1}, Time={timeTarget:F1}ms");
    }
    
    /// <summary>
    /// Manual GC for bootstrap validation - should collect 0 objects when system is stable.
    /// </summary>
    public static int ManualGCForBootstrapValidation()
    {
        Console.WriteLine("[GC] Running explicit GC for bootstrap validation...");
        return ManualGCInternal(isValidationGC: true);
    }
    
    /// <summary>
    /// Registers the VM reference for accessing Special Objects Array.
    /// Must be called before GC can run.
    /// </summary>
    public static void RegisterVM(VirtualMachine virtualMachine)
    {
        vm = virtualMachine;
        Console.WriteLine("[GC] VM registered for Special Objects Array access");
    }
    
    /// <summary>
    /// Enables garbage collection once the Special Objects Array is populated.
    /// </summary>
    public static void EnableGC()
    {
        if (!isGCEnabled)
        {
            isGCEnabled = true;
            Console.WriteLine("[GC] Garbage collection enabled");
        }
    }
}

/// <summary>
/// Statistics and configuration for adaptive garbage collection.
/// </summary>
public record AdaptiveGCStats
{
    /// <summary>Current high water mark threshold.</summary>
    public int HighWaterMark { get; init; }
    
    /// <summary>Current low free count threshold.</summary>
    public int LowFreeCount { get; init; }
    
    /// <summary>Target GC efficiency.</summary>
    public double EfficiencyTarget { get; init; }
    
    /// <summary>Target GC time in milliseconds.</summary>
    public double TimeTarget { get; init; }
    
    /// <summary>Number of adaptation cycles completed.</summary>
    public int AdaptationCycle { get; init; }
    
    /// <summary>Recent GC efficiency measurements.</summary>
    public double[] RecentEfficiencies { get; init; } = Array.Empty<double>();
    
    /// <summary>Recent GC time measurements in milliseconds.</summary>
    public double[] RecentTimes { get; init; } = Array.Empty<double>();
    
    /// <summary>Average efficiency over recent collections.</summary>
    public double AverageEfficiency { get; init; }
    
    /// <summary>Average time over recent collections in milliseconds.</summary>
    public double AverageTime { get; init; }
    
    /// <summary>
    /// Gets a summary string of the adaptive GC performance.
    /// </summary>
    public string Summary => 
        $"Thresholds: {HighWaterMark}/{LowFreeCount}, " +
        $"Performance: {AverageEfficiency:P1} efficiency, {AverageTime:F1}ms, " +
        $"Cycles: {AdaptationCycle}";
}