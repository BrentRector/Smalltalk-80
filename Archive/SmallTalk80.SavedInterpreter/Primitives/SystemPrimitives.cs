using System;
using Smalltalk80.Interpreter.ObjectModel;

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
    public static PrimitiveResult PrimitiveIdentical(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        bool identical = receiver.RawValue == arguments[0].RawValue;
        return context.GetSpecialObject(identical ? SpecialObjects.TrueObject : SpecialObjects.FalseObject);
    }

    /// <summary>
    /// Primitive 111: Object class.
    /// Returns the class of an object.
    /// </summary>
    /// <param name="receiver">The object to query.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The class OOP of the receiver.</returns>
    public static PrimitiveResult PrimitiveClass(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (receiver.IsSmallInteger)
        {
            // Return SmallInteger class
            return context.GetClass("SmallInteger");
        }
        
        var obj = context.Memory.GetObject(receiver);
        if (obj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        return obj.Header.ClassOOP;
    }

    /// <summary>
    /// Primitive 112: Object coreLeft.
    /// Returns the amount of free memory available.
    /// </summary>
    /// <param name="receiver">Ignored (typically the system dictionary).</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Free memory in words as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveCoreLeft(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Return available memory in words (approximate)
        var stats = context.Memory.GetStatistics();
        int freeWords = stats.FreeObjects * 4; // Rough estimate
        
        return OOP.FromSmallInteger(Math.Min(freeWords, int.MaxValue));
    }

    /// <summary>
    /// Primitive 113: Object quit.
    /// Terminates the Smalltalk system.
    /// </summary>
    /// <param name="receiver">Ignored.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Never returns (system exits).</returns>
    public static PrimitiveResult PrimitiveQuit(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        Console.WriteLine("[VM] System quit requested via primitive 113");
        Environment.Exit(0);
        
        // Never reached
        return OOP.Nil;
    }

    /// <summary>
    /// Primitive 114: Object exitToDebugger.
    /// Breaks into the system debugger if available.
    /// </summary>
    /// <param name="receiver">Ignored.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Failure (debugger not implemented).</returns>
    public static PrimitiveResult PrimitiveExitToDebugger(OOP receiver, OOP[] arguments, IPrimitiveContext context)
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
    public static PrimitiveResult PrimitiveOopChanged(OOP receiver, OOP[] arguments, IPrimitiveContext context)
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
    public static PrimitiveResult PrimitiveFlushCache(OOP receiver, OOP[] arguments, IPrimitiveContext context)
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
    public static PrimitiveResult PrimitiveReserved(OOP receiver, OOP[] arguments, IPrimitiveContext context)
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
    public static PrimitiveResult PrimitiveSomeInstance(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Search memory for an instance of this class
        var memory = context.Memory;
        var enumerator = memory.GetAllObjects();
        
        while (enumerator.MoveNext())
        {
            var oop = enumerator.Current;
            if (!oop.IsSmallInteger)
            {
                var obj = memory.GetObject(oop);
                if (obj != null && obj.Header.ClassOOP.RawValue == receiver.RawValue)
                {
                    return oop;
                }
            }
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Primitive 129: Object nextInstance.
    /// Returns the next instance of the same class after the receiver.
    /// </summary>
    /// <param name="receiver">An instance object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Next instance of the same class, or nil if none.</returns>
    public static PrimitiveResult PrimitiveNextInstance(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        var receiverObj = context.Memory.GetObject(receiver);
        if (receiverObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        var classOOP = receiverObj.Header.ClassOOP;
        
        // Search for the next instance of the same class
        var memory = context.Memory;
        var enumerator = memory.GetAllObjects();
        bool foundReceiver = false;
        
        while (enumerator.MoveNext())
        {
            var oop = enumerator.Current;
            
            if (foundReceiver && !oop.IsSmallInteger)
            {
                var obj = memory.GetObject(oop);
                if (obj != null && obj.Header.ClassOOP.RawValue == classOOP.RawValue)
                {
                    return oop;
                }
            }
            
            if (oop.RawValue == receiver.RawValue)
            {
                foundReceiver = true;
            }
        }
        
        return OOP.Nil;
    }

    /// <summary>
    /// Primitive 130: Object becomeFowardTo:.
    /// One-way become operation (receiver becomes argument).
    /// </summary>
    /// <param name="receiver">Object to replace.</param>
    /// <param name="arguments">Single argument containing replacement object.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The argument (new identity).</returns>
    public static PrimitiveResult PrimitiveBecomeForward(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        OOP other = arguments[0];
        
        // Cannot become with SmallIntegers
        if (receiver.IsSmallInteger || other.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        // Forward all references from receiver to other
        context.Memory.ForwardReferences(receiver, other);
        
        return other;
    }

    /// <summary>
    /// Primitive 131: Object instanceCount.
    /// Returns the number of instances of a class.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Count of instances as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveInstanceCount(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Count instances of this class
        var memory = context.Memory;
        var enumerator = memory.GetAllObjects();
        int count = 0;
        
        while (enumerator.MoveNext())
        {
            var oop = enumerator.Current;
            if (!oop.IsSmallInteger)
            {
                var obj = memory.GetObject(oop);
                if (obj != null && obj.Header.ClassOOP.RawValue == receiver.RawValue)
                {
                    count++;
                }
            }
        }
        
        return OOP.FromSmallInteger(count);
    }

    /// <summary>
    /// Primitive 75: Object identityHash.
    /// Returns a hash value based on object identity.
    /// </summary>
    /// <param name="receiver">The object to hash.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Hash value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveIdentityHash(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (receiver.IsSmallInteger)
        {
            // SmallIntegers use their value as hash
            return receiver;
        }
        
        var obj = context.Memory.GetObject(receiver);
        if (obj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        return OOP.FromSmallInteger(obj.Header.Hash);
    }

    /// <summary>
    /// Primitive 76: Object identityHashImmediate.
    /// Like identityHash but works on immediate values.
    /// </summary>
    /// <param name="receiver">The object to hash.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Hash value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveIdentityHashImmediate(OOP receiver, OOP[] arguments, IPrimitiveContext context)
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
}