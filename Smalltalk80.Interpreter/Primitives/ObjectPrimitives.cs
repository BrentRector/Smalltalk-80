using System;
using Smalltalk80.Interpreter.Core.Values;

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
        public static PrimitiveResult PrimitiveFail(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }

        /// <summary>
        /// Primitive 60: Object at: (indexed access).
        /// </summary>
        public static PrimitiveResult PrimitiveAt(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();
            var indexedSize = context.GetIndexedSize(oop);

            if (indexedSize == 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            if (index < 0 || index >= indexedSize)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            return context.GetIndexedPointer(oop, index);
        }

        /// <summary>
        /// Primitive 61: Object at:put: (indexed store).
        /// </summary>
        public static PrimitiveResult PrimitiveAtPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 2)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger() - 1; // Smalltalk uses 1-based indexing
            SObjectRef value = arguments[1];

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();
            var indexedSize = context.GetIndexedSize(oop);

            if (indexedSize == 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            if (index < 0 || index >= indexedSize)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            context.SetIndexedPointer(oop, index, value);
            return value; // at:put: returns the value stored
        }

        /// <summary>
        /// Primitive 62: Object size (number of indexed fields).
        /// </summary>
        public static PrimitiveResult PrimitiveSize(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!context.IsHeapObject(receiver))
                return SObjectRef.FromSmallInteger(0);

            var oop = receiver.AsObjectPointer();
            var indexedSize = context.GetIndexedSize(oop);

            return SObjectRef.FromSmallInteger(indexedSize);
        }

        /// <summary>
        /// Primitive 70: Object new (basic allocation).
        /// </summary>
        public static PrimitiveResult PrimitiveNew(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            // Receiver should be a class
            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            // Get instance size from class format field
            // For now, use a default size
            int size = 0; // TODO: Get from class format

            return context.NewInstance(receiver, size);
        }

        /// <summary>
        /// Primitive 71: Object new: (allocation with indexed size).
        /// </summary>
        public static PrimitiveResult PrimitiveNewWithSize(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int size = arguments[0].AsSmallInteger();
            if (size < 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadSize);

            return context.NewInstance(receiver, size);
        }

        /// <summary>
        /// Primitive 75: Object identityHash.
        /// </summary>
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

            // Get the hash from the object header
            // For now, use the OOP value as hash
            var oop = receiver.AsObjectPointer();
            int hash = (int)(oop & 0x3FFFFFFF); // Ensure positive and within SmallInteger range

            return SObjectRef.FromSmallInteger(hash);
        }

        /// <summary>
        /// Primitive 110: Object == (identity comparison).
        /// </summary>
        public static PrimitiveResult PrimitiveIdentical(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            bool identical = SObjectRef.AreIdentical(receiver, arguments[0]);
            return context.GetSpecialObject(identical ? 2 : 1);
        }

        /// <summary>
        /// Primitive 111: Object class.
        /// </summary>
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
        /// Registers all object primitives in the primitive table.
        /// </summary>
        public static void RegisterAll(PrimitiveTable table)
        {
            table.Register(0, "primitiveFail", PrimitiveFail);
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