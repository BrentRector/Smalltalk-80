using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Class and behavior primitives (primarily primitive 70).
/// These primitives handle class creation and manipulation.
/// Based on Blue Book specification for class-related primitives.
/// </summary>
public static class ClassPrimitives
{
    /// <summary>
    /// Primitive 70: Behavior new (basic instance allocation).
    /// Creates a new instance of a class with no indexed fields.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>New instance of the class.</returns>
    public static PrimitiveResult PrimitiveNew(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Receiver should be a class
        var classObj = context.Memory.GetObject(receiver);
        if (classObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Get instance specification from class
        // The class format field contains instance size and format information
        // For now, we'll use the basic object format
        int instanceSize = GetInstanceSize(classObj, context);
        
        return context.NewInstance(receiver, instanceSize);
    }

    /// <summary>
    /// Primitive 71: Behavior new: (instance allocation with indexed size).
    /// Creates a new instance of a class with the specified number of indexed fields.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">Single argument containing indexed size.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>New instance with indexed fields.</returns>
    public static PrimitiveResult PrimitiveNewWithSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int indexedSize = arguments[0].SmallIntegerValue;
        if (indexedSize < 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadSize);
        
        // Receiver should be a class
        var classObj = context.Memory.GetObject(receiver);
        if (classObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        // Get base instance size and add indexed size
        int instanceSize = GetInstanceSize(classObj, context);
        int totalSize = instanceSize + indexedSize;
        
        return context.NewInstance(receiver, totalSize);
    }

    /// <summary>
    /// Primitive 72: Behavior basicNew (like new but bypasses initialization).
    /// Creates a new instance without sending initialize.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>New uninitialized instance.</returns>
    public static PrimitiveResult PrimitiveBasicNew(OOP receiver, OOP[] arguments, IPrimitiveContext context) =>
        PrimitiveNew(receiver, arguments, context); // Same as new at primitive level

    /// <summary>
    /// Primitive 73: Behavior basicNew: (like new: but bypasses initialization).
    /// Creates a new indexed instance without sending initialize.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">Single argument containing indexed size.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>New uninitialized instance with indexed fields.</returns>
    public static PrimitiveResult PrimitiveBasicNewWithSize(OOP receiver, OOP[] arguments, IPrimitiveContext context) =>
        PrimitiveNewWithSize(receiver, arguments, context); // Same as new: at primitive level

    /// <summary>
    /// Primitive 74: Behavior instanceSize.
    /// Returns the number of named instance variables in instances of the class.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Instance size as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveInstanceSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var classObj = context.Memory.GetObject(receiver);
        if (classObj == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        int instanceSize = GetInstanceSize(classObj, context);
        return OOP.FromSmallInteger(instanceSize);
    }

    /// <summary>
    /// Primitive 77: Behavior someInstance.
    /// Returns an instance of the class, or nil if none exists.
    /// </summary>
    /// <param name="receiver">The class object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>An instance of the class or nil.</returns>
    public static PrimitiveResult PrimitiveSomeInstance(OOP receiver, OOP[] arguments, IPrimitiveContext context) =>
        SystemPrimitives.PrimitiveSomeInstance(receiver, arguments, context);

    /// <summary>
    /// Primitive 78: Object nextInstance.
    /// Returns the next instance of the receiver's class.
    /// </summary>
    /// <param name="receiver">An instance object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Next instance or nil.</returns>
    public static PrimitiveResult PrimitiveNextInstance(OOP receiver, OOP[] arguments, IPrimitiveContext context) =>
        SystemPrimitives.PrimitiveNextInstance(receiver, arguments, context);

    /// <summary>
    /// Primitive 79: Class new (creates a new class).
    /// This is a complex primitive that creates a new class object.
    /// </summary>
    /// <param name="receiver">The metaclass or Class class.</param>
    /// <param name="arguments">Arguments for class creation.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The new class object.</returns>
    public static PrimitiveResult PrimitiveNewClass(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // Class creation is complex and typically handled by Smalltalk code
        // This primitive is rarely implemented at VM level
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Helper method to extract instance size from a class object.
    /// </summary>
    /// <param name="classObj">The class object layout.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>Number of instance variables.</returns>
    private static int GetInstanceSize(ObjectLayout classObj, IPrimitiveContext context)
    {
        // In Smalltalk-80, the class format is stored in a specific field
        // For now, we'll check if there's an instanceVariables field
        // Field layout for Class:
        // 0: superclass
        // 1: methodDictionary
        // 2: format (contains instance size and indexability info)
        // 3: instanceVariables
        // 4: organization
        
        if (classObj.Fields.Length > 2)
        {
            var formatField = classObj.Fields[2];
            if (formatField.IsSmallInteger)
            {
                // Extract instance size from format
                // Format encoding: bits 0-5 = fixed fields, bit 6 = has pointers, bit 7 = indexable
                int format = formatField.SmallIntegerValue;
                return format & 0x3F; // Lower 6 bits
            }
        }
        
        // Default to 0 if we can't determine
        return 0;
    }

    /// <summary>
    /// Registers all class primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(70, "primitiveNew", PrimitiveNew);
        table.Register(71, "primitiveNewWithArg", PrimitiveNewWithSize);
        table.Register(72, "primitiveBasicNew", PrimitiveBasicNew);
        table.Register(73, "primitiveBasicNewWithArg", PrimitiveBasicNewWithSize);
        table.Register(74, "primitiveInstanceSize", PrimitiveInstanceSize);
        table.Register(77, "primitiveSomeInstance", PrimitiveSomeInstance);
        table.Register(78, "primitiveNextInstance", PrimitiveNextInstance);
        table.Register(79, "primitiveNewClass", PrimitiveNewClass);
    }
}