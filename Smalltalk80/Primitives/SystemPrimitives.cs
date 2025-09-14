using System;

namespace Smalltalk80;

/// <summary>
/// Implements system-level primitives for the interpreter.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Returns an approximation of free object slots in the object memory.
    /// Updated to reflect actual available memory with GC.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool CoreLeft(Context ctx)
    {
        _ = ctx.Stack.Pop();
        // Return approximate free slots available
        var stats = ObjectMemory.GetMemoryStats();
        int free = 65536 - stats.allocated + stats.free; // Max slots - allocated + free list
        ctx.Stack.Push(free);
        return true;
    }

    /// <summary>
    /// Requests termination of the process. This implementation does not exit during testing.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool Quit(Context ctx)
    {
        _ = ctx.Stack.Pop();
        ctx.Stack.Push(null);
        return true;
    }

    /// <summary>
    /// Breaks into the debugger if attached.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool ExitToDebugger(Context ctx)
    {
        _ = ctx.Stack.Pop();
        System.Diagnostics.Debugger.Launch();
        ctx.Stack.Push(null);
        return true;
    }

    /// <summary>
    /// Returns a placeholder value representing remaining oops.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool OopsLeft(Context ctx)
    {
        _ = ctx.Stack.Pop();
        ctx.Stack.Push(int.MaxValue - ObjectMemory.Objects.Count);
        return true;
    }

    /// <summary>
    /// Signals a semaphore when the number of allocated objects reaches a threshold.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool SignalAtOopsLeftWordsLeft(Context ctx)
    {
        if (ctx.Stack.Pop() is not int threshold || ctx.Stack.Pop() is not Semaphore sem)
            return false;
        _ = ctx.Stack.Pop();
        if (ObjectMemory.AllocatedObjectCount >= threshold)
        {
            ProcessorScheduler.Instance.Resume(new Process(ctx));
        }
        ctx.Stack.Push(sem);
        return true;
    }
    
    /// <summary>
    /// Primitive 130: Performs manual garbage collection.
    /// Returns the number of objects freed.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool GarbageCollect(Context ctx)
    {
        _ = ctx.Stack.Pop(); // Pop receiver
        int freed = ObjectMemory.ManualGC();
        ctx.Stack.Push(freed);
        return true;
    }
    
    /// <summary>
    /// Primitive 131: Returns garbage collection statistics.
    /// Returns an Array with: [allocated, free, total, collections, totalFreed]
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool GCStats(Context ctx)
    {
        _ = ctx.Stack.Pop(); // Pop receiver
        
        var stats = ObjectMemory.GetMemoryStats();
        
        // Find the Array class - look for a class named "Array" in the object memory
        SmalltalkObject? arrayClass = null;
        foreach (var obj in ObjectMemory.Objects)
        {
            if (obj != null)
            {
                // Check if this is the Array class by looking at its name
                // The class name is typically stored in a specific field (field 6)
                if (obj.Fields != null && obj.Fields.Length > 6 && obj.Fields[6] is Symbol sym && sym.Value == "Array")
                {
                    arrayClass = obj;
                    break;
                }
            }
        }
        
        if (arrayClass == null)
        {
            // If we can't find Array class, just return the numbers as integers
            ctx.Stack.Push(stats.allocated);
            return true;
        }
        
        var statsArray = new SmalltalkObject(arrayClass, 5);
        statsArray.SetField(0, stats.allocated);
        statsArray.SetField(1, stats.free);
        statsArray.SetField(2, stats.total);
        statsArray.SetField(3, stats.collections);
        statsArray.SetField(4, ObjectMemory.TotalObjectsFreed);
        
        ctx.Stack.Push(statsArray);
        return true;
    }
    
    /// <summary>
    /// Primitive 132: Forces immediate garbage collection.
    /// Ignores thresholds and runs GC immediately.
    /// </summary>
    /// <param name="ctx">The active context.</param>
    public static bool ForceGC(Context ctx)
    {
        _ = ctx.Stack.Pop(); // Pop receiver
        ObjectMemory.ForceGC();
        ctx.Stack.Push(ObjectMemory.NilObject);
        return true;
    }
}
