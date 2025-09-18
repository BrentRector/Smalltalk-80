using System;
using Smalltalk80.Interpreter.ObjectModel;

namespace Smalltalk80.Interpreter.Primitives
{
    /// <summary>
    /// Core object manipulation primitives (0-19).
    /// These are the most fundamental primitives in the system.
    /// </summary>
    public static class ObjectPrimitives
    {
        /// <summary>
        /// Primitive 0: Fail - always fails (used for testing).
        /// </summary>
        public static PrimitiveResult PrimitiveFail(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }

        /// <summary>
        /// Primitive 1: SmallInteger addition.
        /// </summary>
        public static PrimitiveResult PrimitiveSmallIntegerAdd(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            
            // Check for overflow
            long result = (long)receiver.SmallIntegerValue + arguments[0].SmallIntegerValue;
            if (result < int.MinValue || result > int.MaxValue)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            return OOP.FromSmallInteger((int)result);
        }

        /// <summary>
        /// Primitive 2: SmallInteger subtraction.
        /// </summary>
        public static PrimitiveResult PrimitiveSmallIntegerSubtract(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            
            // Check for overflow
            long result = (long)receiver.SmallIntegerValue - arguments[0].SmallIntegerValue;
            if (result < int.MinValue || result > int.MaxValue)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            return OOP.FromSmallInteger((int)result);
        }

        /// <summary>
        /// Primitive 60: Object at: (indexed access).
        /// </summary>
        public static PrimitiveResult PrimitiveAt(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            
            int index = arguments[0].SmallIntegerValue - 1; // Smalltalk uses 1-based indexing
            
            var obj = context.Memory.GetObject(receiver);
            if (obj == null)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            if (!obj.IsIndexable)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            if (index < 0 || index >= obj.Fields.Length)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
            
            return obj.Fields[index];
        }

        /// <summary>
        /// Primitive 61: Object at:put: (indexed store).
        /// </summary>
        public static PrimitiveResult PrimitiveAtPut(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 2)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            
            int index = arguments[0].SmallIntegerValue - 1; // Smalltalk uses 1-based indexing
            OOP value = arguments[1];
            
            var obj = context.Memory.GetObject(receiver);
            if (obj == null)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            if (!obj.IsIndexable)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            if (index < 0 || index >= obj.Fields.Length)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
            
            obj.Fields[index] = value;
            return value; // at:put: returns the value stored
        }

        /// <summary>
        /// Primitive 62: Object size (number of indexed fields).
        /// </summary>
        public static PrimitiveResult PrimitiveSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            var obj = context.Memory.GetObject(receiver);
            if (obj == null)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            if (!obj.IsIndexable)
                return OOP.FromSmallInteger(0);
            
            return OOP.FromSmallInteger(obj.Fields.Length);
        }

        /// <summary>
        /// Primitive 70: Object new (basic allocation).
        /// </summary>
        public static PrimitiveResult PrimitiveNew(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            // Receiver should be a class
            var classObj = context.Memory.GetObject(receiver);
            if (classObj == null)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
            
            // Get instance size from class format field
            // For now, use a default size
            int size = 0; // TODO: Get from class format
            
            return context.NewInstance(receiver, size);
        }

        /// <summary>
        /// Primitive 71: Object new: (allocation with indexed size).
        /// </summary>
        public static PrimitiveResult PrimitiveNewWithSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            
            int size = arguments[0].SmallIntegerValue;
            if (size < 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadSize);
            
            return context.NewInstance(receiver, size);
        }

        /// <summary>
        /// Primitive 75: Object identityHash.
        /// </summary>
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
        /// Primitive 110: Object == (identity comparison).
        /// </summary>
        public static PrimitiveResult PrimitiveIdentical(OOP receiver, OOP[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
            
            bool identical = receiver == arguments[0];
            return context.GetSpecialObject(identical ? SpecialObjects.TrueObject : SpecialObjects.FalseObject);
        }

        /// <summary>
        /// Primitive 111: Object class.
        /// </summary>
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
        /// Registers all object primitives in the primitive table.
        /// </summary>
        public static void RegisterAll(PrimitiveTable table)
        {
            table.Register(0, "primitiveFail", PrimitiveFail);
            table.Register(1, "primitiveAdd", PrimitiveSmallIntegerAdd);
            table.Register(2, "primitiveSubtract", PrimitiveSmallIntegerSubtract);
            table.Register(60, "primitiveAt", PrimitiveAt);
            table.Register(61, "primitiveAtPut", PrimitiveAtPut);
            table.Register(62, "primitiveSize", PrimitiveSize);
            table.Register(70, "primitiveNew", PrimitiveNew);
            table.Register(71, "primitiveNewWithArg", PrimitiveNewWithSize);
            table.Register(75, "primitiveIdentityHash", PrimitiveIdentityHash);
            table.Register(110, "primitiveIdentical", PrimitiveIdentical);
            table.Register(111, "primitiveClass", PrimitiveClass);
        }
    }
}