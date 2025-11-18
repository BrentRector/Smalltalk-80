using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

/// <summary>
/// Simple Object primitives using new IPrimitive interface.
/// Complex primitives (at:, at:put:, new:, etc.) require more dependencies and remain as legacy.
/// </summary>
public static class SimpleObjectPrimitives
{
    /// <summary>Primitive 62: Object size - returns number of indexed elements</summary>
    public class Size : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(size));
        }
    }

    /// <summary>Primitive 75: Object hash - returns identity hash (OOP/2)</summary>
    public class Hash : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Blue Book: hash returns half the object pointer
            // SmallIntegers hash to their value
            if (context.Receiver.IsSmallInteger)
            {
                int value = context.Receiver.AsSmallInteger();
                // For SmallIntegers, hash is typically the value itself (or abs value)
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(value & 0x3FFFFFFF));
            }

            // For objects, hash is half the OOP (Primitive 75 semantics)
            int hash = (int)(context.Receiver.RawValue >> 1);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(hash));
        }
    }

    /// <summary>Primitive 110: Object == (identity equals)</summary>
    public class IdentityEquals : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            bool identical = context.Receiver == context.Arguments[0];
            return PrimitiveResult.FromBool(identical);
        }
    }

    /// <summary>Primitive 111: Object class</summary>
    public class Class : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            SmalltalkOOP classOOP;
            if (context.Receiver.IsSmallInteger)
            {
                // SmallInteger class - need to look it up
                classOOP = context.RuntimeHeapAccess?.GetClass("SmallInteger")
                    ?? SpecialObjectsArray.Instance.Nil;
            }
            else
            {
                classOOP = context.HeapManager.GetClassOOP(context.Receiver);
            }

            return PrimitiveResult.Ok(classOOP);
        }
    }

    /// <summary>Primitive 60: Array at: - get indexed element</summary>
    public class At : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail();

            var format = context.HeapManager.GetObjectFormat(context.Receiver);
            SmalltalkOOP value;

            if (format == ObjectFormat.ByteIndexed)
            {
                byte b = context.HeapManager.GetIndexedByte(context.Receiver, index - 1);
                value = SmalltalkOOP.FromSmallInteger(b);
            }
            else if (format == ObjectFormat.WordIndexed)
            {
                ushort w = context.HeapManager.GetIndexedWord(context.Receiver, index - 1);
                value = SmalltalkOOP.FromSmallInteger(w);
            }
            else
            {
                value = context.HeapManager.GetIndexedOOP(context.Receiver, index - 1);
            }

            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 61: Array at:put: - set indexed element</summary>
    public class AtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > size) return PrimitiveResult.Fail();

            var format = context.HeapManager.GetObjectFormat(context.Receiver);

            if (format == ObjectFormat.ByteIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail();
                int byteValue = value.AsSmallInteger();
                if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail();
                context.HeapManager.SetIndexedByte(context.Receiver, index - 1, (byte)byteValue);
            }
            else if (format == ObjectFormat.WordIndexed)
            {
                if (!value.IsSmallInteger) return PrimitiveResult.Fail();
                int wordValue = value.AsSmallInteger();
                if (wordValue < 0 || wordValue > 65535) return PrimitiveResult.Fail();
                context.HeapManager.SetIndexedWord(context.Receiver, index - 1, (ushort)wordValue);
            }
            else
            {
                context.HeapManager.SetIndexedOOP(context.Receiver, index - 1, value);
            }

            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 73: Object instVarAt: - get instance variable</summary>
    public class InstVarAt : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            int count = context.HeapManager.GetInstanceVariableCount(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail();

            var value = context.HeapManager.GetInstanceVariable(context.Receiver, index - 1);
            return PrimitiveResult.Ok(value);
        }
    }

    /// <summary>Primitive 74: Object instVarAt:put: - set instance variable</summary>
    public class InstVarAtPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (!context.Arguments[0].IsSmallInteger) return PrimitiveResult.Fail();

            int index = context.Arguments[0].AsSmallInteger();
            var value = context.Arguments[1];
            int count = context.HeapManager.GetInstanceVariableCount(context.Receiver);

            // Smalltalk indices are 1-based
            if (index < 1 || index > count) return PrimitiveResult.Fail();

            context.HeapManager.SetInstanceVariable(context.Receiver, index - 1, value);
            return PrimitiveResult.Ok(value);
        }
    }
}
