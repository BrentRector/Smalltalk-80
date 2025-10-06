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
    public static PrimitiveResult CoreLeft(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Query actual heap free space
        // For now, return a large number
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(1000000));
    }

    /// <summary>
    /// Primitive 113: quit - Exit the VM.
    /// </summary>
    public static PrimitiveResult Quit(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement graceful VM shutdown
        // For now, just exit
        Environment.Exit(0);
        return PrimitiveResult.Succeeded(receiver);
    }

    /// <summary>
    /// Primitive 114: exitToDebugger - Break to debugger.
    /// </summary>
    public static PrimitiveResult ExitToDebugger(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement debugger entry point
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 115: oopsLeft - Available object pointer slots.
    /// </summary>
    public static PrimitiveResult OopsLeft(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Query object table for free slots
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(100000));
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold.
    /// </summary>
    public static PrimitiveResult SignalAtOopsLeftWordsLeft(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 2)
            return PrimitiveResult.Failed();

        // TODO: Implement low memory signaling
        // Args: oops threshold, words threshold
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 128: someInstance - Get an instance of a class.
    /// </summary>
    public static PrimitiveResult SomeInstance(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement object enumeration by class
        // Receiver is the class
        // Return first instance found
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 129: nextInstance - Get next instance of same class.
    /// </summary>
    public static PrimitiveResult NextInstance(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement object enumeration
        // Receiver is an instance, return next instance of same class
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 130: becomeForward: - One-way become operation.
    /// </summary>
    public static PrimitiveResult BecomeForward(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        // TODO: Implement one-way object swapping
        // All references to receiver become references to args[0]
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 131: instanceCount - Count instances of a class.
    /// </summary>
    public static PrimitiveResult InstanceCount(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement instance counting
        // Receiver is the class
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 133: forceGC - Force garbage collection.
    /// </summary>
    public static PrimitiveResult ForceGC(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Trigger GC
        // For now, just call .NET GC
        GC.Collect();
        GC.WaitForPendingFinalizers();

        return PrimitiveResult.Succeeded(receiver);
    }

    /// <summary>
    /// Primitive 139: nextObject - Enumerate all objects.
    /// </summary>
    public static PrimitiveResult NextObject(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement full object table enumeration
        return PrimitiveResult.Failed();
    }
}
