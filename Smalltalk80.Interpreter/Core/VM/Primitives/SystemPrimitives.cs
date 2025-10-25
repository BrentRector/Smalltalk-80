using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (primitives 112-139).
/// Provides heap queries, garbage collection, object enumeration, and debugging support.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Primitive 112: coreLeft - Free memory available.
    /// Returns estimate of available heap memory in bytes.
    /// </summary>
    public static SmalltalkOOP CoreLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get GC statistics for memory usage
        var stats = ctx.HeapManager.GetGCStatistics();

        // Calculate available memory: Total allocated minus objects in use
        // Each object table entry is approximately 24 bytes (managed storage overhead)
        int totalSlots = ctx.HeapManager.GetObjectTableSize();
        int usedSlots = stats.TotalObjects; // After GC, this is reachable objects
        int freeSlots = totalSlots - usedSlots;

        // Estimate: each free slot can hold ~1KB on average
        int estimatedFreeBytes = freeSlots * 1024;

        // Clamp to SmallInteger range (positive 31-bit)
        if (estimatedFreeBytes < 0)
            estimatedFreeBytes = 0;
        if (estimatedFreeBytes > 0x3FFFFFFF) // Max positive SmallInteger
            estimatedFreeBytes = 0x3FFFFFFF;

        return SmalltalkOOP.FromSmallInteger(estimatedFreeBytes);
    }

    /// <summary>
    /// Primitive 113: quit - Exit the VM.
    /// Uses IPlatformBridge if available for graceful shutdown, falls back to Environment.Exit.
    /// </summary>
    public static SmalltalkOOP Quit(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Use platform bridge for graceful shutdown if available
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.Quit(0);
        }
        else
        {
            // Fallback: direct exit (headless mode)
            Environment.Exit(0);
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 114: exitToDebugger - Break to debugger.
    /// Triggers a breakpoint for attached debuggers, or fails silently if no debugger attached.
    /// </summary>
    public static SmalltalkOOP ExitToDebugger(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Trigger debugger breakpoint if one is attached
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
            return receiver;
        }
        else
        {
            // No debugger attached - fail primitive
            // Smalltalk code can handle this by opening a debugger window
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 115: oopsLeft - Available object pointer slots.
    /// Returns number of available OOP slots in the object table.
    /// </summary>
    public static SmalltalkOOP OopsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Query object table for actual free slots
        int totalSlots = ctx.HeapManager.GetObjectTableSize();
        int activeObjects = ctx.HeapManager.GetActiveObjectCount();
        int freeSlots = totalSlots - activeObjects;

        // Clamp to positive SmallInteger range
        if (freeSlots < 0)
            freeSlots = 0;
        if (freeSlots > 0x3FFFFFFF) // Max positive SmallInteger
            freeSlots = 0x3FFFFFFF;

        return SmalltalkOOP.FromSmallInteger(freeSlots);
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold.
    /// Receiver is a Semaphore to signal when memory drops below threshold.
    /// Args: minimum OOPs remaining, minimum words remaining.
    /// NOTE: This is a stub implementation - full implementation would require GC integration
    /// to check thresholds and signal the semaphore during allocation/collection.
    /// </summary>
    public static SmalltalkOOP SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Semaphore (or nil to disable)
        if (!receiver.IsSmallInteger && receiver != SpecialObjectsArray.Instance.Nil)
        {
            // Validate args are SmallIntegers
            if (!args[0].IsSmallInteger || !args[1].IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            int oopsThreshold = args[0].AsSmallInteger();
            int wordsThreshold = args[1].AsSmallInteger();

            // Validate thresholds are positive
            if (oopsThreshold < 0 || wordsThreshold < 0)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Store the semaphore and thresholds for GC to monitor
            // In a full implementation, this would:
            // 1. Store receiver semaphore in VM state
            // 2. Store oopsThreshold and wordsThreshold
            // 3. GC would check during collection and signal if below threshold
            // For now, we just succeed without actually implementing the monitoring
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 128: someInstance - Get an instance of a class.
    /// Blue Book: Returns an arbitrary instance of the receiver class, or nil if none exists.
    /// </summary>
    public static SmalltalkOOP SomeInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Search heap for first instance of this class
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
            if (objClassOOP == receiver)
            {
                return oop;
            }
        }

        // No instances found
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 129: nextInstance - Get next instance of same class.
    /// Blue Book: Returns the next instance of the same class after receiver, or nil if none.
    /// </summary>
    public static SmalltalkOOP NextInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be an instance (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        SmalltalkOOP receiverClassOOP = ctx.HeapManager.GetClassOOP(receiver);

        // Search for next instance of same class after receiver
        bool foundReceiver = false;
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            if (foundReceiver)
            {
                SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
                if (objClassOOP == receiverClassOOP)
                {
                    return oop;
                }
            }

            if (oop == receiver)
                foundReceiver = true;
        }

        // No more instances found
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 130: becomeForward: - One-way become operation.
    /// Blue Book: All references to receiver become references to argument.
    /// </summary>
    public static SmalltalkOOP BecomeForward(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Neither receiver nor argument can be SmallIntegers
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Forward all references from receiver to argument
            ctx.BecomeOneWay(receiver, args[0]);
            return args[0]; // Return the argument (new identity)
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 131: instanceCount - Count instances of a class.
    /// Blue Book: Returns the number of instances of the receiver class.
    /// </summary>
    public static SmalltalkOOP InstanceCount(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int count = 0;

        // Count instances of this class
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
            if (objClassOOP == receiver)
            {
                count++;
            }
        }

        return SmalltalkOOP.FromSmallInteger(count);
    }

    /// <summary>
    /// Primitive 133: forceGC - Force garbage collection.
    /// </summary>
    public static SmalltalkOOP ForceGC(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Trigger .NET GC (Smalltalk GC would be triggered here in full implementation)
        GC.Collect();
        GC.WaitForPendingFinalizers();

        return receiver;
    }

    /// <summary>
    /// Primitive 139: nextObject - Enumerate all objects.
    /// Blue Book: Returns the next object in memory after receiver, or nil if none.
    /// </summary>
    public static SmalltalkOOP NextObject(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be an object (not SmallInteger)
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Find next object after receiver
        bool foundReceiver = false;
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            if (foundReceiver)
            {
                return oop;
            }

            if (oop == receiver)
                foundReceiver = true;
        }

        // No more objects
        return SpecialObjectsArray.Instance.Nil;
    }
}
