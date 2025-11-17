using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (primitives 112-139).
/// Provides heap queries, garbage collection, object enumeration, and debugging support.
/// </summary>
/// <remarks>
/// <para><b>Blue Book Specification:</b></para>
/// <para>System primitives provide access to VM internals, memory management, and debugging facilities.
/// Chapter 29 specifies primitives 110-116, while 128-139 are extensions for advanced system operations.</para>
///
/// <para><b>Implementation Architecture:</b></para>
/// <list type="bullet">
/// <item>Memory management primitives (112, 115) query heap statistics</item>
/// <item>System control primitives (113, 114) manage VM lifecycle</item>
/// <item>Object enumeration primitives (128, 129, 139) walk the heap</item>
/// <item>Identity mutation primitive (130) performs become: operation</item>
/// <item>Debugging support (114) integrates with .NET debugger</item>
/// </list>
///
/// <para><b>Primitives Implemented:</b></para>
/// <list type="bullet">
/// <item>112: coreLeft - Available memory in bytes</item>
/// <item>113: quit - Exit the VM</item>
/// <item>114: exitToDebugger - Break to debugger</item>
/// <item>115: oopsLeft - Available object table slots</item>
/// <item>116: signalAtOopsLeftWordsLeft - Low memory threshold (stub)</item>
/// <item>128: someInstance - Get arbitrary instance of class</item>
/// <item>129: nextInstance - Get next instance of same class</item>
/// <item>130: becomeForward: - One-way become operation</item>
/// <item>131: instanceCount - Count instances of class</item>
/// <item>133: forceGC - Force garbage collection</item>
/// <item>139: nextObject - Enumerate all objects</item>
/// </list>
///
/// <para><b>Conformance:</b> Good conformance with Blue Book, primitive 116 is stub implementation</para>
/// </remarks>
public static class SystemPrimitives
{
    // <summary>
    // Primitive 112: coreLeft - Free memory available.
    // Returns estimate of available heap memory in bytes.
    // </summary>
    public static SmalltalkOOP CoreLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveCoreLeft
        // Returns available heap memory estimate in bytes

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Query garbage collector for memory statistics
        var stats = ctx.HeapManager.GetGCStatistics();

        // Calculate available slots in object table
        int totalSlots = ctx.HeapManager.GetObjectTableSize();
        int usedSlots = stats.TotalObjects; // Reachable objects after last GC
        int freeSlots = totalSlots - usedSlots;

        // Estimate memory: assume ~1KB average object size
        // This is a rough estimate for Smalltalk's memory reporting
        int estimatedFreeBytes = freeSlots * 1024;

        // Clamp to positive SmallInteger range (30 bits)
        if (estimatedFreeBytes < 0)
            estimatedFreeBytes = 0;
        if (estimatedFreeBytes > 0x3FFFFFFF)
            estimatedFreeBytes = 0x3FFFFFFF;

        // Return memory estimate as SmallInteger
        return SmalltalkOOP.FromSmallInteger(estimatedFreeBytes);
    }

    // <summary>
    // Primitive 113: quit - Exit the VM.
    // Uses IPlatformBridge if available for graceful shutdown, falls back to Environment.Exit.
    // </summary>
    public static SmalltalkOOP Quit(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveQuit
        // Terminates the VM and returns to operating system

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Platform-specific: Use bridge for clean shutdown
        // Bridge can save image, close files, release resources
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.Quit(0); // Exit code 0 = success
        }
        else
        {
            // Fallback: Immediate termination in headless mode
            Environment.Exit(0);
        }

        // Never reached (VM terminates)
        return receiver;
    }

    // <summary>
    // Primitive 114: exitToDebugger - Break to debugger.
    // Triggers a breakpoint for attached debuggers, or fails silently if no debugger attached.
    // </summary>
    public static SmalltalkOOP ExitToDebugger(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveExitToDebugger
        // Breaks into native debugger if attached

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Check if .NET debugger is attached to process
        if (System.Diagnostics.Debugger.IsAttached)
        {
            // Trigger breakpoint for debugging VM internals
            System.Diagnostics.Debugger.Break();
            return receiver;
        }
        else
        {
            // No debugger - fail primitive
            // Smalltalk can open its own debugger window instead
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 115: oopsLeft - Available object pointer slots.
    // Returns number of available OOP slots in the object table.
    // </summary>
    public static SmalltalkOOP OopsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveOopsLeft
        // Returns count of free object table entries

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Query object table statistics
        int totalSlots = ctx.HeapManager.GetObjectTableSize();
        int activeObjects = ctx.HeapManager.GetActiveObjectCount();
        int freeSlots = totalSlots - activeObjects;

        // Ensure positive SmallInteger range
        if (freeSlots < 0)
            freeSlots = 0;
        if (freeSlots > 0x3FFFFFFF)
            freeSlots = 0x3FFFFFFF;

        // Return available OOP count
        return SmalltalkOOP.FromSmallInteger(freeSlots);
    }

    // <summary>
    // Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold
    // Blue Book: Page 587, Chapter 29
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <para>Sets up monitoring for low-memory conditions. When available memory drops below
    // the specified thresholds, the receiver Semaphore is signaled to allow graceful handling
    // of low-memory situations.</para>
    //
    // <para><b>Arguments:</b></para>
    // <list type="bullet">
    // <item>Receiver: Semaphore to signal (or nil to disable monitoring)</item>
    // <item>args[0]: Minimum OOPs threshold (SmallInteger)</item>
    // <item>args[1]: Minimum words threshold (SmallInteger)</item>
    // </list>
    //
    // <para><b>Implementation Status:</b></para>
    // <para>⚠️ STUB IMPLEMENTATION - Accepts arguments but does not implement monitoring.
    // Full implementation would require GC integration to check thresholds during allocation
    // and collection cycles, signaling the semaphore when either threshold is crossed.</para>
    //
    // <para><b>Conformance:</b> STUB - Interface correct, monitoring not implemented</para>
    // </remarks>
    public static SmalltalkOOP SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveSignalAtOopsLeftWordsLeft
        // Sets up low-memory monitoring with thresholds

        // Expect 2 arguments: oopsThreshold, wordsThreshold
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be Semaphore object (or nil to disable)
        if (!receiver.IsSmallInteger && receiver != SpecialObjectsArray.Instance.Nil)
        {
            // Both thresholds must be SmallIntegers
            if (!args[0].IsSmallInteger || !args[1].IsSmallInteger)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // Extract threshold values
            int oopsThreshold = args[0].AsSmallInteger();
            int wordsThreshold = args[1].AsSmallInteger();

            // Thresholds must be non-negative
            if (oopsThreshold < 0 || wordsThreshold < 0)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            // STUB IMPLEMENTATION
            // Full implementation would:
            // 1. Store receiver semaphore in VM state
            // 2. Store oopsThreshold and wordsThreshold
            // 3. GC would check during collection and signal if below threshold
            // For now, accept parameters but don't implement monitoring
        }

        // Return receiver (the semaphore)
        return receiver;
    }

    // <summary>
    // Primitive 128: someInstance - Get an instance of a class.
    // Blue Book: Returns an arbitrary instance of the receiver class, or nil if none exists.
    // </summary>
    public static SmalltalkOOP SomeInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveSomeInstance
        // Returns first instance of receiver class found in heap

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class object
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Walk entire heap searching for instances
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            // Check if object's class matches receiver
            SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
            if (objClassOOP == receiver)
            {
                // Found an instance - return it
                return oop;
            }
        }

        // No instances of this class exist
        return SpecialObjectsArray.Instance.Nil;
    }

    // <summary>
    // Primitive 129: nextInstance - Get next instance of same class.
    // Blue Book: Returns the next instance of the same class after receiver, or nil if none.
    // </summary>
    public static SmalltalkOOP NextInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveNextInstance
        // Returns next instance of same class after receiver

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be an object instance
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Get receiver's class
        SmalltalkOOP receiverClassOOP = ctx.HeapManager.GetClassOOP(receiver);

        // Walk heap looking for next instance of same class
        bool foundReceiver = false;
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            // After finding receiver, look for next instance
            if (foundReceiver)
            {
                SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
                if (objClassOOP == receiverClassOOP)
                {
                    // Found next instance of same class
                    return oop;
                }
            }

            // Mark when we've found the receiver
            if (oop == receiver)
                foundReceiver = true;
        }

        // No more instances of this class
        return SpecialObjectsArray.Instance.Nil;
    }

    // <summary>
    // Primitive 130: becomeForward: - One-way become operation.
    // Blue Book: All references to receiver become references to argument.
    // </summary>
    public static SmalltalkOOP BecomeForward(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveBecomeForward
        // One-way identity mutation - receiver becomes argument

        // Expect single argument (target object)
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Cannot mutate SmallIntegers (immediate values)
        if (receiver.IsSmallInteger || args[0].IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            // Perform one-way become operation
            // All references to receiver now point to argument
            ctx.BecomeOneWay(receiver, args[0]);

            // Return the target object (new identity)
            return args[0];
        }
        catch
        {
            // Become failed (possibly critical objects)
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // <summary>
    // Primitive 131: instanceCount - Count instances of a class.
    // Blue Book: Returns the number of instances of the receiver class.
    // </summary>
    public static SmalltalkOOP InstanceCount(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveInstanceCount
        // Counts all instances of receiver class in heap

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be a Class object
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int count = 0;

        // Walk entire heap counting instances
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            // Check if object's class matches receiver
            SmalltalkOOP objClassOOP = ctx.HeapManager.GetClassOOP(oop);
            if (objClassOOP == receiver)
            {
                count++;
            }
        }

        // Return count as SmallInteger
        return SmalltalkOOP.FromSmallInteger(count);
    }

    // <summary>
    // Primitive 133: forceGC - Force garbage collection.
    // </summary>
    public static SmalltalkOOP ForceGC(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveForceGC
        // Forces immediate garbage collection

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Trigger .NET garbage collection
        // In full VM, would trigger Smalltalk GC as well
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Return receiver
        return receiver;
    }

    // <summary>
    // Primitive 139: nextObject - Enumerate all objects.
    // Blue Book: Returns the next object in memory after receiver, or nil if none.
    // </summary>
    public static SmalltalkOOP NextObject(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book extension: primitiveNextObject
        // Returns next object in heap enumeration

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Receiver must be an object
        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Find next object in heap enumeration
        bool foundReceiver = false;
        foreach (SmalltalkOOP oop in ctx.GetAllObjects())
        {
            // Return first object after receiver
            if (foundReceiver)
            {
                return oop;
            }

            // Mark when we've found receiver
            if (oop == receiver)
                foundReceiver = true;
        }

        // No more objects - end of heap
        return SpecialObjectsArray.Instance.Nil;
    }
}
