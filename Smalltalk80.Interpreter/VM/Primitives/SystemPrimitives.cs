using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (primitives 112-139).
/// Stubs for now - require GC, object table enumeration, and system infrastructure.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Primitive 112: coreLeft - Free memory available.
    /// </summary>
    public static SObjectRef CoreLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Query actual heap free space
        // For now, return a large number
        return SObjectRef.FromSmallInteger(1000000);
    }

    /// <summary>
    /// Primitive 113: quit - Exit the VM.
    /// </summary>
    public static SObjectRef Quit(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef ExitToDebugger(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef OopsLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Query object table for free slots
        return SObjectRef.FromSmallInteger(100000);
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold.
    /// </summary>
    public static SObjectRef SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef SomeInstance(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int classOop = (int)receiver.AsObjectPointer();

        // Search heap for first instance of this class
        foreach (int oop in ctx.GetAllObjects())
        {
            int objClassOop = ctx.Heap.GetClass(oop);
            if (objClassOop == classOop)
            {
                return SObjectRef.FromObjectPointer(oop);
            }
        }

        // No instances found
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 129: nextInstance - Get next instance of same class.
    /// Blue Book: Returns the next instance of the same class after receiver, or nil if none.
    /// </summary>
    public static SObjectRef NextInstance(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int receiverOop = (int)receiver.AsObjectPointer();
        int receiverClassOop = ctx.Heap.GetClass(receiverOop);

        // Search for next instance of same class after receiver
        bool foundReceiver = false;
        foreach (int oop in ctx.GetAllObjects())
        {
            if (foundReceiver)
            {
                int objClassOop = ctx.Heap.GetClass(oop);
                if (objClassOop == receiverClassOop)
                {
                    return SObjectRef.FromObjectPointer(oop);
                }
            }

            if (oop == receiverOop)
                foundReceiver = true;
        }

        // No more instances found
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Primitive 130: becomeForward: - One-way become operation.
    /// Blue Book: All references to receiver become references to argument.
    /// </summary>
    public static SObjectRef BecomeForward(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int fromOop = (int)receiver.AsObjectPointer();
        int toOop = (int)args[0].AsObjectPointer();

        try
        {
            // Forward all references from receiver to argument
            ctx.BecomeOneWay(fromOop, toOop);
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
    public static SObjectRef InstanceCount(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int classOop = (int)receiver.AsObjectPointer();
        int count = 0;

        // Count instances of this class
        foreach (int oop in ctx.GetAllObjects())
        {
            int objClassOop = ctx.Heap.GetClass(oop);
            if (objClassOop == classOop)
            {
                count++;
            }
        }

        return SObjectRef.FromSmallInteger(count);
    }

    /// <summary>
    /// Primitive 133: forceGC - Force garbage collection.
    /// </summary>
    public static SObjectRef ForceGC(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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
    public static SObjectRef NextObject(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
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

        int receiverOop = (int)receiver.AsObjectPointer();

        // Find next object after receiver
        bool foundReceiver = false;
        foreach (int oop in ctx.GetAllObjects())
        {
            if (foundReceiver)
            {
                return SObjectRef.FromObjectPointer(oop);
            }

            if (oop == receiverOop)
                foundReceiver = true;
        }

        // No more objects
        return SpecialObjectsArray.Instance.Nil;
    }
}
