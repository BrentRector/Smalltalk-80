using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (112-139) using new IPrimitive interface.
/// </summary>
public static class SystemOperations
{
    /// <summary>Primitive 112: coreLeft - Free memory available</summary>
    public class CoreLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Query garbage collector for memory statistics
            var stats = context.HeapManager.GC.GetStatistics();
            int totalSlots = context.HeapManager.GetObjectTableSize();
            int usedSlots = stats.TotalObjects;
            int freeSlots = totalSlots - usedSlots;

            // Estimate memory: assume ~1KB average object size
            int estimatedFreeBytes = freeSlots * 1024;
            if (estimatedFreeBytes < 0) estimatedFreeBytes = 0;
            if (estimatedFreeBytes > 0x3FFFFFFF) estimatedFreeBytes = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(estimatedFreeBytes));
        }
    }

    /// <summary>Primitive 113: quit - Exit the VM</summary>
    public class Quit : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Platform != null)
            {
                context.Platform.Quit(0);
            }
            else
            {
                Environment.Exit(0);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 114: exitToDebugger - Break to debugger</summary>
    public class ExitToDebugger : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
                return PrimitiveResult.Ok(context.Receiver);
            }
            else
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>Primitive 115: oopsLeft - Available object pointer slots</summary>
    public class OopsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            int totalSlots = context.HeapManager.GetObjectTableSize();
            int activeObjects = context.HeapManager.GetActiveObjectCount();
            int freeSlots = totalSlots - activeObjects;

            if (freeSlots < 0) freeSlots = 0;
            if (freeSlots > 0x3FFFFFFF) freeSlots = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(freeSlots));
        }
    }

    /// <summary>Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold</summary>
    /// <remarks>
    /// Blue Book: numOops is a 16-bit Integer, numWords is a 32-bit LargePositiveInteger.
    /// We accept SmallInteger or LargePositiveInteger for both to handle .NET's large memory.
    /// This is a stub - we accept the thresholds but don't implement actual monitoring.
    /// </remarks>
    public class SignalAtOopsLeftWordsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be Semaphore object or nil (to disable monitoring)
            // For stub implementation, just accept any valid receiver

            // Arguments can be SmallInteger or LargePositiveInteger
            // We don't validate strictly since this is a stub implementation
            // Just verify they are valid OOPs (not corrupt)
            var arg0 = context.Arguments[0];
            var arg1 = context.Arguments[1];

            // Accept SmallIntegers directly
            // For non-SmallInteger, just accept it (could be LargePositiveInteger)
            // Stub: don't actually set up monitoring, just succeed

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 117: Behavior instSize - Get number of named instance variables</summary>
    public class InstSize : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver is a class (Behavior) - get its format field
            // Behavior instance variable layout: [0]=superclass, [1]=methodDict, [2]=format
            var formatOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);
            if (!formatOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Use FormatWord to decode the instance variable count (bits 0-7)
            var formatWord = new FormatWord(formatOOP.AsSmallInteger());
            int instSize = formatWord.FixedFields;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(instSize));
        }
    }

    /// <summary>Primitive 128: someInstance - Get an instance of a class</summary>
    public class SomeInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Find first instance of receiver class
            var instance = context.HeapManager.FindInstanceOf(context.Receiver);

            if (instance == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(instance);
        }
    }

    /// <summary>Primitive 129: nextInstance - Get next instance of same class</summary>
    public class NextInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Get receiver's class
            SmalltalkOOP receiverClassOOP = context.HeapManager.GetClassOOP(context.Receiver);

            // Find next instance after receiver
            var nextInstance = context.HeapManager.FindNextInstanceOf(receiverClassOOP, context.Receiver);

            if (nextInstance == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(nextInstance);
        }
    }

    /// <summary>Primitive 130: becomeForward: - One-way become operation</summary>
    public class BecomeForward : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger || context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                context.HeapManager.Become.BecomeTwoWay(context.Receiver, context.Arguments[0]);
                return PrimitiveResult.Ok(context.Arguments[0]);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>Primitive 131: instanceCount - Count instances of a class</summary>
    public class InstanceCount : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int count = 0;

            // Walk heap counting instances
            int tableSize = context.HeapManager.GetObjectTableSize();
            for (int i = 0; i < tableSize; i++)
            {
                var oop = SmalltalkOOP.FromObjectTableIndex(i);
                if (!context.HeapManager.IsValidObjectOOP(oop)) continue;

                SmalltalkOOP objClassOOP = context.HeapManager.GetClassOOP(oop);
                if (objClassOOP == context.Receiver)
                {
                    count++;
                }
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(count));
        }
    }

    /// <summary>Primitive 133: forceGC - Force garbage collection</summary>
    public class ForceGC : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 139: nextObject - Enumerate all objects</summary>
    public class NextObject : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Find next object in heap enumeration
            int receiverIndex = context.Receiver.ToObjectTableIndex();
            int tableSize = context.HeapManager.GetObjectTableSize();

            for (int i = receiverIndex + 1; i < tableSize; i++)
            {
                var oop = SmalltalkOOP.FromObjectTableIndex(i);
                if (context.HeapManager.IsValidObjectOOP(oop))
                {
                    return PrimitiveResult.Ok(oop);
                }
            }

            // No more objects
            return PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }
}
