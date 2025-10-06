using Smalltalk80.Interpreter.Core.Values;

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
    public static SObjectRef? CoreLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Query actual heap free space
        // For now, return a large number
        return SObjectRef.FromSmallInteger(1000000);
    }

    /// <summary>
    /// Primitive 113: quit - Exit the VM.
    /// </summary>
    public static SObjectRef? Quit(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement graceful VM shutdown
        // For now, just exit
        Environment.Exit(0);
        return receiver;
    }

    /// <summary>
    /// Primitive 114: exitToDebugger - Break to debugger.
    /// </summary>
    public static SObjectRef? ExitToDebugger(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement debugger entry point
        return null;
    }

    /// <summary>
    /// Primitive 115: oopsLeft - Available object pointer slots.
    /// </summary>
    public static SObjectRef? OopsLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Query object table for free slots
        return SObjectRef.FromSmallInteger(100000);
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold.
    /// </summary>
    public static SObjectRef? SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 2)
            return null;

        // TODO: Implement low memory signaling
        // Args: oops threshold, words threshold
        return null;
    }

    /// <summary>
    /// Primitive 128: someInstance - Get an instance of a class.
    /// </summary>
    public static SObjectRef? SomeInstance(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement object enumeration by class
        // Receiver is the class
        // Return first instance found
        return null;
    }

    /// <summary>
    /// Primitive 129: nextInstance - Get next instance of same class.
    /// </summary>
    public static SObjectRef? NextInstance(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement object enumeration
        // Receiver is an instance, return next instance of same class
        return null;
    }

    /// <summary>
    /// Primitive 130: becomeForward: - One-way become operation.
    /// </summary>
    public static SObjectRef? BecomeForward(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
            return null;

        // TODO: Implement one-way object swapping
        // All references to receiver become references to args[0]
        return null;
    }

    /// <summary>
    /// Primitive 131: instanceCount - Count instances of a class.
    /// </summary>
    public static SObjectRef? InstanceCount(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement instance counting
        // Receiver is the class
        return null;
    }

    /// <summary>
    /// Primitive 133: forceGC - Force garbage collection.
    /// </summary>
    public static SObjectRef? ForceGC(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Trigger GC
        // For now, just call .NET GC
        GC.Collect();
        GC.WaitForPendingFinalizers();

        return receiver;
    }

    /// <summary>
    /// Primitive 139: nextObject - Enumerate all objects.
    /// </summary>
    public static SObjectRef? NextObject(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement full object table enumeration
        return null;
    }
}
