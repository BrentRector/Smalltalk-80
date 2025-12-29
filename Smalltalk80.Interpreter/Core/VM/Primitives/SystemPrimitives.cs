using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// System primitives for memory, GC, debugging, and object enumeration (112-139) using new IPrimitive interface.
/// </summary>
public static class SystemOperations
{
    /// <summary>
    /// Maximum theoretical OOP count based on our SmalltalkOOP representation.
    /// SmalltalkOOP uses a 32-bit tagged pointer where LSB=0 means object pointer.
    /// Maximum object table index = Int32.MaxValue >> 1 = 1,073,741,823.
    /// </summary>
    private const int MaxTheoreticalOops = int.MaxValue >> 1;

    /// <summary>Primitive 112: coreLeft - Free memory available</summary>
    /// <remarks>
    /// On modern systems with virtual memory, we have essentially unlimited memory.
    /// The original Blue Book ran on systems with limited physical memory.
    /// We report a large value based on theoretical maximum to ensure Smalltalk's
    /// low memory monitoring never triggers spuriously.
    /// </remarks>
    public class CoreLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Use theoretical maximum OOPs minus active objects, times estimated bytes per slot.
            // This ensures we always report a very large amount of free memory.
            int activeObjects = context.HeapManager.GetActiveObjectCount();
            int freeOops = MaxTheoreticalOops - activeObjects;
            if (freeOops < 0) freeOops = int.MaxValue;

            // Estimate memory: assume ~1KB average object size
            // Use long arithmetic to avoid overflow, then cap at SmallInteger max
            long estimatedFreeBytes = (long)freeOops * 1024;
            if (estimatedFreeBytes > 0x3FFFFFFF) estimatedFreeBytes = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)estimatedFreeBytes));
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
    /// <remarks>
    /// On modern systems, the object table can grow as needed up to the theoretical maximum.
    /// We report free OOPs relative to what we CAN allocate, not what we HAVE allocated.
    /// This ensures Smalltalk's low memory monitoring never triggers spuriously.
    /// </remarks>
    public class OopsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Use theoretical maximum minus active objects for free OOPs.
            // This ensures we always report a very large number of available OOPs.
            int activeObjects = context.HeapManager.GetActiveObjectCount();
            int freeOops = MaxTheoreticalOops - activeObjects;

            if (freeOops < 0) freeOops = int.MaxValue;
            if (freeOops > 0x3FFFFFFF) freeOops = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(freeOops));
        }
    }

    /// <summary>Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold</summary>
    /// <remarks>
    /// Smalltalk: signal: aSemaphore atOopsLeft: numOops wordsLeft: numWords
    /// Blue Book: numOops is a 16-bit Integer, numWords is a 32-bit LargePositiveInteger.
    /// We accept SmallInteger or LargePositiveInteger for both to handle .NET's large memory.
    ///
    /// When available oops or words drop below the specified thresholds, the VM
    /// signals the semaphore, allowing the system to respond to low memory.
    /// </remarks>
    public class SignalAtOopsLeftWordsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Method signature: signal: aSemaphore atOopsLeft: numOops wordsLeft: numWords
            // So we have 3 arguments: semaphore, oopsLimit, wordsLimit
            if (context.Arguments.Length != 3) return PrimitiveResult.Fail(context.Metadata!);

            // Arguments:
            // [0] = aSemaphore (Semaphore or nil)
            // [1] = numOops (SmallInteger)
            // [2] = numWords (SmallInteger or LargePositiveInteger)
            var semaphoreOOP = context.Arguments[0];
            var oopsOOP = context.Arguments[1];
            var wordsOOP = context.Arguments[2];

            // LowMemoryManager is required
            if (context.LowMemoryManager == null)
                return PrimitiveResult.Fail(context.Metadata!);

            // If semaphore is nil, disable monitoring
            if (semaphoreOOP == context.HeapManager.GetNil())
            {
                context.LowMemoryManager.Disable();
                return PrimitiveResult.Ok(context.Receiver);
            }

            // Extract oops threshold (must be SmallInteger)
            if (!oopsOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            int oopsThreshold = oopsOOP.AsSmallInteger();

            // Extract words threshold (SmallInteger or LargePositiveInteger)
            int wordsThreshold;
            if (wordsOOP.IsSmallInteger)
            {
                wordsThreshold = wordsOOP.AsSmallInteger();
            }
            else
            {
                // LargePositiveInteger - extract value from bytes
                // For practical purposes, cap at int.MaxValue
                int size = context.HeapManager.GetIndexedSize(wordsOOP);
                if (size == 0)
                {
                    wordsThreshold = 0;
                }
                else if (size <= 4)
                {
                    // Read as little-endian 32-bit integer
                    int value = 0;
                    for (int i = 0; i < size; i++)
                    {
                        value |= context.HeapManager.GetIndexedByte(wordsOOP, i) << (i * 8);
                    }
                    wordsThreshold = value;
                }
                else
                {
                    // Larger than 32-bit - cap at max
                    wordsThreshold = int.MaxValue;
                }
            }

            // Set the thresholds in the low memory manager
            context.LowMemoryManager.SetThresholds(semaphoreOOP, oopsThreshold, wordsThreshold);

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
