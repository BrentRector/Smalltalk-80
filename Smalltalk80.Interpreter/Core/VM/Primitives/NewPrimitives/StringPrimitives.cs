using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives.NewPrimitives;

/// <summary>
/// String primitives using new IPrimitive interface.
/// </summary>
public static class StringPrimitives
{
    /// <summary>Primitive 105: replaceFrom:to:with:startingAt:</summary>
    public class StringReplace : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 4) return PrimitiveResult.Fail();

            if (!context.Arguments[0].IsSmallInteger || !context.Arguments[1].IsSmallInteger || !context.Arguments[3].IsSmallInteger)
                return PrimitiveResult.Fail();

            int startIndex = context.Arguments[0].AsSmallInteger() - 1;
            int stopIndex = context.Arguments[1].AsSmallInteger() - 1;
            SmalltalkOOP sourceOop = context.Arguments[2];
            int sourceStart = context.Arguments[3].AsSmallInteger() - 1;

            if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
                return PrimitiveResult.Fail();

            if (context.Receiver.IsSmallInteger || sourceOop.IsSmallInteger)
                return PrimitiveResult.Fail();

            int copyCount = stopIndex - startIndex + 1;
            int receiverSize = context.HeapManager.GetIndexedSize(context.Receiver);
            int sourceSize = context.HeapManager.GetIndexedSize(sourceOop);

            if (stopIndex >= receiverSize || sourceStart + copyCount > sourceSize)
                return PrimitiveResult.Fail();

            // Handle overlapping regions
            if (context.Receiver == sourceOop && startIndex > sourceStart && startIndex < sourceStart + copyCount)
            {
                for (int i = copyCount - 1; i >= 0; i--)
                {
                    byte value = context.HeapManager.GetIndexedByte(sourceOop, sourceStart + i);
                    context.HeapManager.SetIndexedByte(context.Receiver, startIndex + i, value);
                }
            }
            else
            {
                for (int i = 0; i < copyCount; i++)
                {
                    byte value = context.HeapManager.GetIndexedByte(sourceOop, sourceStart + i);
                    context.HeapManager.SetIndexedByte(context.Receiver, startIndex + i, value);
                }
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 106: String asSymbol</summary>
    public class AsSymbol : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

            // Get string bytes
            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (size == 0) return PrimitiveResult.Fail();

            byte[] bytes = new byte[size];
            for (int i = 0; i < size; i++)
            {
                bytes[i] = context.HeapManager.GetIndexedByte(context.Receiver, i);
            }

            string stringValue = System.Text.Encoding.Latin1.GetString(bytes);

            // Look up in USTable
            if (context.RuntimeHeapAccess == null) return PrimitiveResult.Fail();
            var symbolOOP = context.RuntimeHeapAccess.InternSymbol(stringValue);

            if (symbolOOP == SpecialObjectsArray.Instance.Nil)
                return PrimitiveResult.Fail();

            return PrimitiveResult.Ok(symbolOOP);
        }
    }
}
