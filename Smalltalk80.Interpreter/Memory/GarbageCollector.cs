namespace Smalltalk80.Interpreter.Memory;

using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Mark-and-sweep garbage collector for the Smalltalk-80 interpreter.
/// 
/// CRITICAL DESIGN: The GC has exactly ONE root - the Special Objects Array.
/// NO individual objects are registered. All live objects are found by tracing
/// references from the SOA. Objects become collectible when no longer reachable.
/// </summary>
public sealed class GarbageCollector
{
    private readonly HashSet<SObject> reachableObjects = new();
    private readonly Queue<SObject> workQueue = new();
    private readonly object gcLock = new();
    
    // The ONLY root for GC - can be C# List or Smalltalk Array
    private List<SObject?> specialObjectsArray = new();
    
    private long totalCollections;
    private long totalObjectsCollected;
    private long totalBytesFreed;
    private long lastCollectionDuration;
    
    /// <summary>
    /// Gets the total number of garbage collections performed.
    /// </summary>
    public long TotalCollections => totalCollections;
    
    /// <summary>
    /// Gets the total number of objects collected.
    /// </summary>
    public long TotalObjectsCollected => totalObjectsCollected;
    
    /// <summary>
    /// Gets the total amount of memory freed in bytes.
    /// </summary>
    public long TotalBytesFreed => totalBytesFreed;
    
    /// <summary>
    /// Gets the duration of the last collection in milliseconds.
    /// </summary>
    public long LastCollectionDurationMs => lastCollectionDuration;
    
    /// <summary>
    /// Sets the Special Objects Array - the ONLY root for garbage collection.
    /// </summary>
    /// <param name="soa">The special objects array.</param>
    public void SetSpecialObjectsArray(List<SObject?> soa)
    {
        lock (gcLock)
        {
            specialObjectsArray = soa ?? new List<SObject?>();
        }
    }
    
    /// <summary>
    /// Gets or sets a specific special object.
    /// </summary>
    public SObject? GetSpecialObject(int index)
    {
        lock (gcLock)
        {
            if (index >= 0 && index < specialObjectsArray.Count)
                return specialObjectsArray[index];
            return null;
        }
    }
    
    /// <summary>
    /// Sets a specific special object in the Special Objects Array.
    /// </summary>
    /// <param name="index">The index in the Special Objects Array.</param>
    /// <param name="obj">The object to set at the specified index.</param>
    public void SetSpecialObject(int index, SObject? obj)
    {
        lock (gcLock)
        {
            while (specialObjectsArray.Count <= index)
                specialObjectsArray.Add(null);
            specialObjectsArray[index] = obj;
        }
    }
    
    /// <summary>
    /// Performs a garbage collection cycle.
    /// Uses ONLY the Special Objects Array as roots.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics Collect()
    {
        var stopwatch = Stopwatch.StartNew();
        var objectsCollected = 0;
        var bytesFreed = 0L;
        
        lock (gcLock)
        {
            if (Program.RuntimeOptions.EnableGCLogging)
            {
                Console.WriteLine($"[GC] Starting collection #{totalCollections + 1}...");
            }
            
            // Phase 1: Mark all reachable objects
            reachableObjects.Clear();
            workQueue.Clear();
            
            // Add ONLY Special Objects Array elements as roots
            foreach (var specialObj in specialObjectsArray)
            {
                if (specialObj != null && !specialObj.IsImmediate)
                {
                    workQueue.Enqueue(specialObj);
                }
            }
            
            // Mark phase - trace all reachable objects
            while (workQueue.Count > 0)
            {
                var obj = workQueue.Dequeue();
                
                if (reachableObjects.Add(obj))
                {
                    // First time seeing this object, trace its references
                    TraceObject(obj);
                }
            }
            
            if (Program.RuntimeOptions.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Marked {reachableObjects.Count} reachable objects");
            }
            
            // Phase 2: Sweep
            // In a real implementation, we would sweep through memory
            // For now, just count reachable objects
            objectsCollected = 0; // We don't track all objects anymore
            
            if (Program.RuntimeOptions.GCTraceLevel == GCTraceLevel.Deep)
            {
                Console.WriteLine($"[GC] Found {reachableObjects.Count} reachable objects from SOA");
            }
            
            totalCollections++;
            totalObjectsCollected += objectsCollected;
            totalBytesFreed += bytesFreed;
        }
        
        stopwatch.Stop();
        lastCollectionDuration = stopwatch.ElapsedMilliseconds;
        
        if (Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine($"[GC] Collection #{totalCollections} complete: " +
                            $"{objectsCollected} objects collected, " +
                            $"{bytesFreed:N0} bytes freed, " +
                            $"{lastCollectionDuration}ms");
        }
        
        return new GarbageCollectionStatistics(
            objectsCollected,
            bytesFreed,
            lastCollectionDuration,
            totalCollections);
    }
    
    /// <summary>
    /// Forces a garbage collection.
    /// </summary>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics ForceCollection()
    {
        return Collect();
    }
    
    /// <summary>
    /// Traces all objects referenced by the given object.
    /// </summary>
    private void TraceObject(SObject obj)
    {
        switch (obj)
        {
            case SClass classObj:
                TraceClass(classObj);
                break;
                
            case SDictionary dict:
                TraceDictionary(dict);
                break;
                
            case SCompiledMethod method:
                TraceMethod(method);
                break;
                
            case SArray array:
                TraceArray(array);
                break;
                
            case SInstance instance:
                TraceInstance(instance);
                break;
                
            case SString _:
            case SSymbol _:
            case SNil _:
            case SBoolean _:
                // These have no references to trace
                break;
                
            default:
                // Unknown object type, trace conservatively if needed
                if (Program.RuntimeOptions.GCTraceLevel == GCTraceLevel.Deep)
                {
                    Console.WriteLine($"[GC] Unknown object type for tracing: {obj.GetType()}");
                }
                break;
        }
    }
    
    /// <summary>
    /// Traces references in a class object.
    /// </summary>
    private void TraceClass(SClass classObj)
    {
        if (classObj.Superclass != null && !classObj.Superclass.IsImmediate)
        {
            EnqueueForTracing(classObj.Superclass);
        }
        
        if (classObj.MethodDictionary != null)
        {
            foreach (var method in classObj.MethodDictionary.Values)
            {
                if (method != null && !method.IsImmediate)
                {
                    EnqueueForTracing(method);
                }
            }
        }
    }
    
    /// <summary>
    /// Traces references in a dictionary object.
    /// </summary>
    private void TraceDictionary(SDictionary dict)
    {
        // First trace as an instance (for instance variables and class)
        TraceInstance(dict);
        
        // Then trace all keys and values
        foreach (var key in dict.Keys)
        {
            if (key != null && !key.IsImmediate)
            {
                EnqueueForTracing(key);
            }
        }
        
        foreach (var value in dict.Values)
        {
            if (value != null && !value.IsImmediate)
            {
                EnqueueForTracing(value);
            }
        }
    }
    
    /// <summary>
    /// Traces references in a compiled method.
    /// </summary>
    private void TraceMethod(SCompiledMethod method)
    {
        if (method.Literals != null && !method.Literals.IsImmediate)
        {
            EnqueueForTracing(method.Literals);
        }
    }
    
    /// <summary>
    /// Traces references in an array.
    /// </summary>
    private void TraceArray(SArray array)
    {
        for (var i = 1; i <= array.Size; i++)
        {
            var element = array.At(i);
            if (element != null && !element.IsImmediate)
            {
                EnqueueForTracing(element);
            }
        }
    }
    
    /// <summary>
    /// Traces references in an instance object.
    /// </summary>
    private void TraceInstance(SInstance instance)
    {
        // Trace the class
        if (instance.Class != null && !instance.Class.IsImmediate)
        {
            EnqueueForTracing(instance.Class);
        }

        // Trace instance variables
        for (var i = 0; i < instance.InstanceVariableCount; i++)
        {
            var valueRef = instance.GetInstanceVariable(i);
            if (!valueRef.IsNil && valueRef.IsHeapObject)
            {
                var valueObj = valueRef.AsObject();
                if (valueObj != null && !valueObj.IsImmediate)
                {
                    EnqueueForTracing(valueObj);
                }
            }
        }
    }
    
    /// <summary>
    /// Enqueues an object for tracing if not already marked.
    /// </summary>
    private void EnqueueForTracing(SObject obj)
    {
        if (!reachableObjects.Contains(obj))
        {
            workQueue.Enqueue(obj);
        }
    }
    
    /// <summary>
    /// Validates that all objects are reachable from the SOA.
    /// This should be run after cold start to ensure proper setup.
    /// </summary>
    /// <returns>True if all expected objects are reachable.</returns>
    public bool ValidateReachability()
    {
        if (Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine("[GC] Validating all objects are reachable from SOA...");
        }
        
        // Perform a collection to mark all reachable objects
        Collect();
        
        // Check that critical objects are reachable
        // With the minimal SOA, these should all be reachable through SystemDictionary
        var criticalObjects = new List<(string name, SObject? obj)>
        {
            ("nil", SObjectRef.Nil.AsObject()),
            ("true", STrue.Instance),
            ("false", SFalse.Instance),
            ("Behavior class", SystemDictionary.BehaviorClass),
            ("Metaclass class", SystemDictionary.MetaclassClass),
            // Classes should be reachable through SystemDictionary
            ("Object class (via Smalltalk)", SystemDictionary.ObjectClass),
            ("SmallInteger class (via Smalltalk)", SystemDictionary.SmallIntegerClass),
            ("Array class (via Smalltalk)", SystemDictionary.ArrayClass),
            ("String class (via Smalltalk)", SystemDictionary.StringClass)
        };
        
        var allReachable = true;
        foreach (var (name, obj) in criticalObjects)
        {
            if (obj != null && !obj.IsImmediate && !reachableObjects.Contains(obj))
            {
                Console.WriteLine($"[GC] WARNING: {name} is not reachable from SOA!");
                allReachable = false;
            }
        }
        
        if (allReachable && Program.RuntimeOptions.EnableGCLogging)
        {
            Console.WriteLine($"[GC] Validation passed: {reachableObjects.Count} objects reachable from SOA");
        }
        
        return allReachable;
    }
    
    /// <summary>
    /// Estimates the size of an object in bytes.
    /// </summary>
    private static long EstimateObjectSize(SObject obj)
    {
        // Base object overhead (estimated)
        var size = 24L; // Object header + reference
        
        switch (obj)
        {
            case SArray array:
                size += array.Size * 8; // Reference per element
                break;
                
            case SString str:
                size += str.Length * 2; // Unicode chars
                break;
                
            case SCompiledMethod method:
                // Handle CompiledMethod specifically (before SInstance)
                size += method.Bytecodes?.Length ?? 0;
                size += 32; // Method metadata
                size += method.InstanceVariableCount * 8; // Instance vars
                break;
                
            case SInstance instance:
                size += instance.InstanceVariableCount * 8; // Reference per instance var
                break;
                
            case SClass _:
                size += 128; // Class metadata and dictionary
                break;
        }
        
        return size;
    }
    
    /// <summary>
    /// Gets statistics about the garbage collector.
    /// </summary>
    public GarbageCollectorStatistics GetStatistics() => new(
        TotalCollections,
        TotalObjectsCollected,
        TotalBytesFreed,
        LastCollectionDurationMs,
        reachableObjects.Count);  // Live objects from last collection
    
    /// <summary>
    /// Resets all statistics.
    /// </summary>
    public void ResetStatistics()
    {
        totalCollections = 0;
        totalObjectsCollected = 0;
        totalBytesFreed = 0;
        lastCollectionDuration = 0;
    }
}

/// <summary>
/// Statistics about a garbage collection cycle.
/// </summary>
public readonly record struct GarbageCollectionStatistics(
    int ObjectsCollected,
    long BytesFreed,
    long DurationMs,
    long TotalCollections)
{
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"GC[Collection #{TotalCollections}: {ObjectsCollected} objects, {BytesFreed:N0} bytes, {DurationMs}ms]";
}

/// <summary>
/// Extended garbage collector statistics.
/// </summary>
public readonly record struct GarbageCollectorStatistics(
    long TotalCollections,
    long TotalObjectsCollected,
    long TotalBytesFreed,
    long LastCollectionDurationMs,
    int LiveObjectCount)
{
    /// <summary>
    /// Returns a string representation of the statistics.
    /// </summary>
    public override string ToString() =>
        $"""
        Garbage Collector Statistics:
          Total Collections:     {TotalCollections:N0}
          Objects Collected:     {TotalObjectsCollected:N0}
          Memory Freed:          {TotalBytesFreed:N0} bytes
          Last Collection Time:  {LastCollectionDurationMs}ms
          Live Objects:          {LiveObjectCount:N0}
        """;
}