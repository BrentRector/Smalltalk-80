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
        /// Primitive 68: objectAt: (CompiledMethod literal access).
        /// Used on CompiledMethod to access method header (index 1) or literals (index > 1).
        /// 1-based indexing.
        /// </summary>
        public static PrimitiveResult PrimitiveObjectAt(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger();

            if (index < 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();

            // Index 1 is the method header
            if (index == 1)
            {
                // Get the method header
                var header = Memory.ObjectMemory.GetCompiledMethodHeader(oop);
                return SObjectRef.FromSmallInteger(header);
            }

            // Index > 1 accesses literals (stored as instance variables)
            // Adjust for 1-based indexing and skip header
            int literalIndex = index - 2;

            // Get the literals from instance variables
            var literals = Memory.ObjectMemory.GetCompiledMethodLiterals(oop);

            if (literalIndex < 0 || literalIndex >= literals.Length)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            return literals[literalIndex];
        }

        /// <summary>
        /// Primitive 69: objectAt:put: (CompiledMethod literal store).
        /// Store into CompiledMethod literals.
        /// Returns the value stored.
        /// </summary>
        public static PrimitiveResult PrimitiveObjectAtPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 2)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger();
            SObjectRef value = arguments[1];

            if (index < 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();

            // Index 1 would be the method header - typically not modified
            if (index == 1)
            {
                // For now, fail on attempt to modify header
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
            }

            // Index > 1 accesses literals (stored as instance variables)
            // Adjust for 1-based indexing and skip header
            int literalIndex = index - 2;

            // Get the literal count to validate index
            var literals = Memory.ObjectMemory.GetCompiledMethodLiterals(oop);

            if (literalIndex < 0 || literalIndex >= literals.Length)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            // Store the literal as an instance variable
            context.SetInstanceVariable(oop, literalIndex, value);

            return value; // Return the value stored
        }

        /// <summary>
        /// Primitive 72: become: (swap object identities).
        /// Swaps instance pointers of receiver and argument.
        /// All references to receiver now point to argument and vice versa.
        /// Fails if either is SmallInteger.
        /// </summary>
        public static PrimitiveResult PrimitiveBecome(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            // Cannot become SmallIntegers
            if (receiver.IsSmallInteger || arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            if (!context.IsHeapObject(receiver) || !context.IsHeapObject(arguments[0]))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop1 = receiver.AsObjectPointer();
            var oop2 = arguments[0].AsObjectPointer();

            // Use ObjectMemory's Become method to swap the objects
            Memory.ObjectMemory.Become(oop1, oop2);

            return receiver; // Return the receiver (which now has the other object's data)
        }

        /// <summary>
        /// Primitive 73: instVarAt: (instance variable access by index).
        /// Access fixed (named) instance variables by number.
        /// 1-based indexing (1 = first inst var).
        /// </summary>
        public static PrimitiveResult PrimitiveInstVarAt(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger();

            if (index < 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();

            // Get instance variable count
            var instVarCount = Memory.ObjectMemory.GetInstanceVariableCount(oop);

            // Convert to 0-based index
            int zeroBasedIndex = index - 1;

            if (zeroBasedIndex < 0 || zeroBasedIndex >= instVarCount)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            return context.GetInstanceVariable(oop, zeroBasedIndex);
        }

        /// <summary>
        /// Primitive 74: instVarAt:put: (instance variable store by index).
        /// Store into fixed instance variable.
        /// Returns value stored.
        /// </summary>
        public static PrimitiveResult PrimitiveInstVarAtPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 2)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int index = arguments[0].AsSmallInteger();
            SObjectRef value = arguments[1];

            if (index < 1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var oop = receiver.AsObjectPointer();

            // Get instance variable count
            var instVarCount = Memory.ObjectMemory.GetInstanceVariableCount(oop);

            // Convert to 0-based index
            int zeroBasedIndex = index - 1;

            if (zeroBasedIndex < 0 || zeroBasedIndex >= instVarCount)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);

            context.SetInstanceVariable(oop, zeroBasedIndex, value);

            return value; // Return the value stored
        }

        /// <summary>
        /// Primitive 77: someInstance (find instance of class).
        /// Receiver is a class.
        /// Returns an instance of that class, or nil if none exists.
        /// </summary>
        public static PrimitiveResult PrimitiveSomeInstance(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            // Enumerate all objects to find an instance of this class
            foreach (var (index, data) in Memory.ObjectMemory.ObjectTable.EnumerateLiveObjects())
            {
                if (SObjectRef.AreIdentical(data.ClassRef, receiver))
                {
                    // Found an instance of this class
                    return SObjectRef.FromObjectPointer(Memory.ObjectTable.OopFromIndex(index));
                }
            }

            // No instances found
            return SObjectRef.Nil;
        }

        /// <summary>
        /// Primitive 78: nextInstance (next instance of same class).
        /// Receiver is an instance.
        /// Returns next instance of same class, or nil.
        /// </summary>
        public static PrimitiveResult PrimitiveNextInstance(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!context.IsHeapObject(receiver))
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

            var receiverOop = receiver.AsObjectPointer();
            var receiverIndex = Memory.ObjectTable.IndexFromOop(receiverOop);

            // Get the class of the receiver
            var classRef = context.GetObjectClass(receiverOop);

            // Find the next instance after the receiver
            bool foundReceiver = false;
            foreach (var (index, data) in Memory.ObjectMemory.ObjectTable.EnumerateLiveObjects())
            {
                if (!foundReceiver)
                {
                    // Still looking for the receiver
                    if (index == receiverIndex)
                        foundReceiver = true;
                }
                else
                {
                    // Found receiver, now looking for next instance of same class
                    if (SObjectRef.AreIdentical(data.ClassRef, classRef))
                    {
                        return SObjectRef.FromObjectPointer(Memory.ObjectTable.OopFromIndex(index));
                    }
                }
            }

            // No next instance found
            return SObjectRef.Nil;
        }

        /// <summary>
        /// Primitive 79: newMethod:header: (create CompiledMethod).
        /// Creates a new CompiledMethod.
        /// First arg: number of literals
        /// Second arg: method header
        /// Returns new CompiledMethod.
        /// </summary>
        public static PrimitiveResult PrimitiveNewMethod(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
        {
            if (arguments.Length != 2)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

            if (!arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

            int literalCount = arguments[0].AsSmallInteger();
            int header = arguments[1].AsSmallInteger();

            if (literalCount < 0)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadSize);

            // Create an empty literals array
            var literals = new SObjectRef[literalCount];
            for (int i = 0; i < literalCount; i++)
            {
                literals[i] = SObjectRef.Nil;
            }

            // Extract header fields
            int numArgs = header & 0x07;                    // Bits 0-2
            int numTemps = (header >> 3) & 0x1F;            // Bits 3-7
            bool hasPrimitive = (header & 0x8000) != 0;     // Bit 15

            // For now, create with empty bytecodes
            // The actual bytecodes will be filled in later
            var bytecodes = new byte[0];

            // Create the CompiledMethod using the helper
            var methodRef = Memory.ObjectMemory.CreateCompiledMethod(
                bytecodes,
                literals,
                numTemps,
                numArgs,
                0  // No primitive index for now
            );

            // Now we need to set the actual header value in the bytecode area
            if (context.IsHeapObject(methodRef))
            {
                var methodOop = methodRef.AsObjectPointer();
                var data = Memory.ObjectMemory.GetObjectData(methodOop);

                if (data.Format == Core.Objects.ObjectFormat.CompiledMethod)
                {
                    // Set the header directly in the bytecode area
                    var fullData = (byte[])data.IndexedData!;
                    if (fullData.Length >= 2)
                    {
                        fullData[0] = (byte)(header & 0xFF);
                        fullData[1] = (byte)((header >> 8) & 0xFF);
                    }
                }
            }

            return methodRef;
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
            table.Register(68, "primitiveObjectAt", PrimitiveObjectAt);
            table.Register(69, "primitiveObjectAtPut", PrimitiveObjectAtPut);
            table.Register(70, "primitiveNew", PrimitiveNew);
            table.Register(71, "primitiveNewWithArg", PrimitiveNewWithSize);
            table.Register(72, "primitiveBecome", PrimitiveBecome);
            table.Register(73, "primitiveInstVarAt", PrimitiveInstVarAt);
            table.Register(74, "primitiveInstVarAtPut", PrimitiveInstVarAtPut);
            table.Register(75, "primitiveIdentityHash", PrimitiveIdentityHash);
            table.Register(77, "primitiveSomeInstance", PrimitiveSomeInstance);
            table.Register(78, "primitiveNextInstance", PrimitiveNextInstance);
            table.Register(79, "primitiveNewMethod", PrimitiveNewMethod);
            table.Register(110, "primitiveIdentical", PrimitiveIdentical);
            table.Register(111, "primitiveClass", PrimitiveClass);
        }
    }
}