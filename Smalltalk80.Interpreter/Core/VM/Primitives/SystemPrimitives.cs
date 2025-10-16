using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (primitives 112-139).
/// Stubs for now - require GC, object table enumeration, and system infrastructure.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Primitive 112: coreLeft - Free memory available.
    /// </summary>
    public static SmalltalkOOP CoreLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Query actual heap free space
        // For now, return a large number
        return SmalltalkOOP.FromSmallInteger(1000000);
    }

    /// <summary>
    /// Primitive 113: quit - Exit the VM.
    /// </summary>
    public static SmalltalkOOP Quit(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Implement graceful VM shutdown
        // For now, just exit
        Environment.Exit(0);
        return receiver;
    }

    /// <summary>
    /// Primitive 114: exitToDebugger - Break to debugger.
    /// </summary>
    public static SmalltalkOOP ExitToDebugger(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Implement debugger entry point
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 115: oopsLeft - Available object pointer slots.
    /// </summary>
    public static SmalltalkOOP OopsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Query object table for free slots
        return SmalltalkOOP.FromSmallInteger(100000);
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold.
    /// </summary>
    public static SmalltalkOOP SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 2)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Implement low memory signaling
        // Args: oops threshold, words threshold
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
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
