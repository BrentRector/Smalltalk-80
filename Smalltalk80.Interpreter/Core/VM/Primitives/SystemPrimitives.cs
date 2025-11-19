using Smalltalk80.Core;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Query garbage collector for memory statistics
            var stats = context.HeapManager.GetGCStatistics();
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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Break();
                return PrimitiveResult.Ok(context.Receiver);
            }
            else
            {
                return PrimitiveResult.Fail();
            }
        }
    }

    /// <summary>Primitive 115: oopsLeft - Available object pointer slots</summary>
    public class OopsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            int totalSlots = context.HeapManager.GetObjectTableSize();
            int activeObjects = context.HeapManager.GetActiveObjectCount();
            int freeSlots = totalSlots - activeObjects;

            if (freeSlots < 0) freeSlots = 0;
            if (freeSlots > 0x3FFFFFFF) freeSlots = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(freeSlots));
        }
    }

    /// <summary>Primitive 116: signalAtOopsLeftWordsLeft - Set low memory threshold</summary>
    public class SignalAtOopsLeftWordsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();

            // Receiver must be Semaphore object (or nil to disable)
            if (!context.Receiver.IsSmallInteger && context.Receiver != SpecialObjectsArray.Instance.Nil)
            {
                if (!context.Arguments[0].IsSmallInteger || !context.Arguments[1].IsSmallInteger)
                    return PrimitiveResult.Fail();

                int oopsThreshold = context.Arguments[0].AsSmallInteger();
                int wordsThreshold = context.Arguments[1].AsSmallInteger();

                if (oopsThreshold < 0 || wordsThreshold < 0)
                    return PrimitiveResult.Fail();

                // Stub implementation - accept parameters but don't implement monitoring
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 117: Behavior instSize - Get number of named instance variables</summary>
    public class InstSize : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Receiver is a class (Behavior) - get its format field
            // Behavior instance variable layout: [0]=superclass, [1]=methodDict, [2]=format
            var formatOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);
            if (!formatOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Find first instance of receiver class
            var instance = context.HeapManager.FindInstanceOf(context.Receiver);

            if (instance == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(instance);
        }
    }

    /// <summary>Primitive 129: nextInstance - Get next instance of same class</summary>
    public class NextInstance : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Get receiver's class
            SmalltalkOOP receiverClassOOP = context.HeapManager.GetClassOOP(context.Receiver);

            // Find next instance after receiver
            var nextInstance = context.HeapManager.FindNextInstanceOf(receiverClassOOP, context.Receiver);

            if (nextInstance == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(nextInstance);
        }
    }

    /// <summary>Primitive 130: becomeForward: - One-way become operation</summary>
    public class BecomeForward : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger || context.Arguments[0].IsSmallInteger)
                return PrimitiveResult.Fail();

            try
            {
                context.HeapManager.BecomeTwoWay(context.Receiver, context.Arguments[0]);
                return PrimitiveResult.Ok(context.Arguments[0]);
            }
            catch
            {
                return PrimitiveResult.Fail();
            }
        }
    }

    /// <summary>Primitive 131: instanceCount - Count instances of a class</summary>
    public class InstanceCount : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

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
            return PrimitiveResult.Ok(SpecialObjectsArray.Instance.Nil);
        }
    }
}
