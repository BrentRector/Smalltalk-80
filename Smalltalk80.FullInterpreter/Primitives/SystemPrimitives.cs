using System;
using System.Collections.Generic;
using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// System primitives (primitives 110-131).
/// These primitives handle system-level operations like object identity,
/// memory management, and system queries.
/// Based on Blue Book specification for system primitives.
/// </summary>
public static class SystemPrimitives
{
    /// <summary>
    /// Primitive 110: Object == (identity comparison).
    /// Tests if two objects are identical (same OOP).
    /// </summary>
    /// <param name="receiver">The first object.</param>
    /// <param name="arguments">Single argument containing the second object.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>True if objects are identical, false otherwise.</returns>
    public static PrimitiveResult PrimitiveIdentical(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        bool identical = SObjectRef.AreIdentical(receiver, arguments[0]);
        return context.GetSpecialObject(identical ? 2 : 1);
    }

    /// <summary>
    /// Primitive 111: Object class.
    /// Returns the class of an object.
    /// </summary>
    /// <param name="receiver">The object to query.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The class OOP of the receiver.</returns>
    public static PrimitiveResult PrimitiveClass(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (receiver.IsSmallInteger)
        {
            // Return SmallInteger class
            return context.GetClass("SmallInteger");
        }

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        var oop = receiver.AsObjectPointer();
        return context.GetObjectClass(oop);
    }

    /// <summary>
    /// Primitive 112: Object coreLeft.
    /// Returns the amount of free memory available.
    /// </summary>
    /// <param name="receiver">Ignored (typically the system dictionary).</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Free memory in words as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveCoreLeft(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Return available memory in words (approximate)
        // For now, return a large number as we have dynamic memory
        int freeWords = 1000000; // Placeholder value

        return SObjectRef.FromSmallInteger(Math.Min(freeWords, int.MaxValue));
    }

    /// <summary>
    /// Primitive 113: Object quit.
    /// Terminates the Smalltalk system.
    /// </summary>
    /// <param name="receiver">Ignored.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Never returns (system exits).</returns>
    public static PrimitiveResult PrimitiveQuit(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        Console.WriteLine("[VM] System quit requested via primitive 113");
        Environment.Exit(0);

        // Never reached
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Primitive 114: Object exitToDebugger.
    /// Breaks into the system debugger if available.
    /// </summary>
    /// <param name="receiver">Ignored.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Failure (debugger not implemented).</returns>
    public static PrimitiveResult PrimitiveExitToDebugger(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        Console.WriteLine("[VM] Exit to debugger requested - not implemented");
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 115: Object oopChanged.
    /// Notifies that an object's OOP has changed (used after become:).
    /// </summary>
    /// <param name="receiver">The object that changed.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveOopChanged(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // This is typically a no-op in modern VMs
        // It was used to update cached OOPs after become:
        return receiver;
    }

    /// <summary>
    /// Primitive 116: Object flushCache.
    /// Flushes the method cache for a selector.
    /// </summary>
    /// <param name="receiver">The selector to flush (or nil for all).</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveFlushCache(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // TODO: Implement method cache flushing when cache is implemented
        Console.WriteLine("[VM] Method cache flush requested");
        return receiver;
    }

    /// <summary>
    /// Primitive 117-127: Reserved for system use.
    /// These primitives are implementation-specific.
    /// </summary>
    public static PrimitiveResult PrimitiveReserved(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 128: Object someInstance.
    /// Returns an instance of a class.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>An instance of the class, or nil if none exists.</returns>
    public static PrimitiveResult PrimitiveSomeInstance(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // This primitive would need to enumerate all objects in memory
        // For now, return nil as we don't have object enumeration
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Primitive 129: Object nextInstance.
    /// Returns the next instance of the same class after the receiver.
    /// </summary>
    /// <param name="receiver">An instance object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Next instance of the same class, or nil if none.</returns>
    public static PrimitiveResult PrimitiveNextInstance(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // This primitive would need to enumerate all objects in memory
        // For now, return nil as we don't have object enumeration
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Primitive 130: Object becomeFowardTo:.
    /// One-way become operation (receiver becomes argument).
    /// </summary>
    /// <param name="receiver">Object to replace.</param>
    /// <param name="arguments">Single argument containing replacement object.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The argument (new identity).</returns>
    public static PrimitiveResult PrimitiveBecomeForward(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        SObjectRef other = arguments[0];

        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || other.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);

        // Forward all references from receiver to other
        // This would need deep VM integration to update all references
        // For now, return failure as the operation is not supported
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 131: Object instanceCount.
    /// Returns the number of instances of a class.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Count of instances as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveInstanceCount(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // This would need to enumerate all objects and count instances
        // For now, return 0 as we don't have object enumeration
        return SObjectRef.FromSmallInteger(0);
    }

    /// <summary>
    /// Primitive 75: Object identityHash.
    /// Returns a hash value based on object identity.
    /// </summary>
    /// <param name="receiver">The object to hash.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Hash value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveIdentityHash(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (receiver.IsSmallInteger)
        {
            // SmallIntegers use their value as hash
            return receiver;
        }

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // Use the OOP as the hash value
        var oop = receiver.AsObjectPointer();
        int hash = (int)(oop & 0x3FFFFFFF); // Ensure positive and within SmallInteger range

        return SObjectRef.FromSmallInteger(hash);
    }

    /// <summary>
    /// Primitive 76: Object identityHashImmediate.
    /// Like identityHash but works on immediate values.
    /// </summary>
    /// <param name="receiver">The object to hash.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Hash value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveIdentityHashImmediate(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // Same as identityHash in our implementation
        return PrimitiveIdentityHash(receiver, arguments, context);
    }

    /// <summary>
    /// Registers all system primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        // Object identity and class
        table.Register(75, "primitiveIdentityHash", PrimitiveIdentityHash);
        table.Register(76, "primitiveIdentityHashImmediate", PrimitiveIdentityHashImmediate);
        table.Register(110, "primitiveIdentical", PrimitiveIdentical);
        table.Register(111, "primitiveClass", PrimitiveClass);

        // System operations
        table.Register(112, "primitiveCoreLeft", PrimitiveCoreLeft);
        table.Register(113, "primitiveQuit", PrimitiveQuit);
        table.Register(114, "primitiveExitToDebugger", PrimitiveExitToDebugger);
        table.Register(115, "primitiveOopChanged", PrimitiveOopChanged);
        table.Register(116, "primitiveFlushCache", PrimitiveFlushCache);

        // Reserved
        for (int i = 117; i <= 127; i++)
        {
            table.Register(i, $"primitiveReserved{i}", PrimitiveReserved);
        }

        // Instance enumeration
        table.Register(128, "primitiveSomeInstance", PrimitiveSomeInstance);
        table.Register(129, "primitiveNextInstance", PrimitiveNextInstance);
        table.Register(130, "primitiveBecomeForward", PrimitiveBecomeForward);
        table.Register(131, "primitiveInstanceCount", PrimitiveInstanceCount);
    }

    /// <summary>
    /// Primitive 115: oopsLeft (number of available object table entries).
    /// </summary>
    public static PrimitiveResult PrimitiveOopsLeft(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Return a large number - we have a dynamic object table
        return SObjectRef.FromSmallInteger(1000000);
    }

    /// <summary>
    /// Primitive 116: signalAtOopsLeftWordsLeft (low memory warning).
    /// </summary>
    public static PrimitiveResult PrimitiveSignalAtOopsLeftWordsLeft(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Not implemented - we don't have memory pressure signaling
        return receiver;
    }

    /// <summary>
    /// Primitive 133: forceGC (force garbage collection).
    /// </summary>
    public static PrimitiveResult PrimitiveForceGC(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // GC is handled specially by the interpreter
        return receiver;
    }

    /// <summary>
    /// Primitive 139: nextObject (enumerate all objects).
    /// </summary>
    public static PrimitiveResult PrimitiveNextObject(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Not implemented
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }
}