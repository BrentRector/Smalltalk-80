namespace Smalltalk80.Interpreter.Memory;

using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Config;

/// <summary>
/// Mark-and-sweep garbage collector for the Smalltalk-80 interpreter.
/// </summary>
public sealed class GarbageCollector
{
    private readonly HashSet<SObject> reachableObjects = new();
    private readonly Queue<SObject> workQueue = new();
    private readonly List<WeakReference> allObjects = new();
    private readonly object gcLock = new();
    
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
    /// Registers an object with the garbage collector.
    /// </summary>
    /// <param name="obj">The object to register.</param>
    public void RegisterObject(SObject obj)
    {
        if (obj == null) return;
        
        lock (gcLock)
        {
            allObjects.Add(new WeakReference(obj));
        }
    }
    
    /// <summary>
    /// Performs a garbage collection cycle.
    /// </summary>
    /// <param name="roots">The root objects to start marking from.</param>
    /// <returns>Statistics about the collection.</returns>
    public GarbageCollectionStatistics Collect(IEnumerable<SObject> roots)
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
            
            // Add roots to work queue
            foreach (var root in roots)
            {
                if (root != null && !root.IsImmediate)
                {
                    workQueue.Enqueue(root);
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
            
            // Phase 2: Sweep - remove unreachable objects
            var newObjectList = new List<WeakReference>();
            
            foreach (var weakRef in allObjects)
            {
                if (weakRef.IsAlive)
                {
                    var obj = weakRef.Target as SObject;
                    if (obj != null)
                    {
                        if (reachableObjects.Contains(obj))
                        {
                            // Object is still reachable, keep it
                            newObjectList.Add(weakRef);
                        }
                        else
                        {
                            // Object is unreachable, it will be collected
                            objectsCollected++;
                            bytesFreed += EstimateObjectSize(obj);
                            
                            if (Program.RuntimeOptions.GCTraceLevel == GCTraceLevel.Deep)
                            {
                                Console.WriteLine($"[GC] Collecting: {obj.GetType().Name}");
                            }
                        }
                    }
                }
            }
            
            // Replace the object list with survivors
            allObjects.Clear();
            allObjects.AddRange(newObjectList);
            
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
        // Get roots from the system
        var roots = GetSystemRoots();
        return Collect(roots);
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
            var value = instance.GetInstanceVariable(i);
            if (value != null && !value.IsImmediate)
            {
                EnqueueForTracing(value);
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
    /// Gets the system root objects.
    /// </summary>
    private List<SObject> GetSystemRoots()
    {
        var roots = new List<SObject>();
        
        // Add system dictionary entries
        var globals = SystemDictionary.GetAllGlobals();
        foreach (var global in globals)
        {
            if (global != null)
            {
                roots.Add(global);
            }
        }
        
        // Add special objects (true, false, nil)
        roots.Add(STrue.Instance);
        roots.Add(SFalse.Instance);
        roots.Add(SNil.Instance);
        
        // Add all classes
        var allClasses = SystemDictionary.GetAllClasses();
        foreach (var cls in allClasses)
        {
            if (cls != null)
            {
                roots.Add(cls);
            }
        }
        
        return roots;
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
                
            case SInstance instance:
                size += instance.InstanceVariableCount * 8; // Reference per instance var
                break;
                
            case SCompiledMethod method:
                size += method.Bytecodes?.Length ?? 0;
                size += 32; // Method metadata
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
        allObjects.Count(wr => wr.IsAlive));
    
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