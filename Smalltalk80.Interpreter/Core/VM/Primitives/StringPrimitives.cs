using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

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
            if (context.Arguments.Length != 4) return PrimitiveResult.Fail(context.Metadata!);

            if (!context.Arguments[0].IsSmallInteger || !context.Arguments[1].IsSmallInteger || !context.Arguments[3].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int startIndex = context.Arguments[0].AsSmallInteger() - 1;
            int stopIndex = context.Arguments[1].AsSmallInteger() - 1;
            SmalltalkOOP sourceOop = context.Arguments[2];
            int sourceStart = context.Arguments[3].AsSmallInteger() - 1;

            if (startIndex < 0 || stopIndex < startIndex || sourceStart < 0)
                return PrimitiveResult.Fail(context.Metadata!);

            if (context.Receiver.IsSmallInteger || sourceOop.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int copyCount = stopIndex - startIndex + 1;
            int receiverSize = context.HeapManager.GetIndexedSize(context.Receiver);
            int sourceSize = context.HeapManager.GetIndexedSize(sourceOop);

            if (stopIndex >= receiverSize || sourceStart + copyCount > sourceSize)
                return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Get string bytes
            int size = context.HeapManager.GetIndexedSize(context.Receiver);
            if (size == 0) return PrimitiveResult.Fail(context.Metadata!);

            byte[] bytes = new byte[size];
            for (int i = 0; i < size; i++)
            {
                bytes[i] = context.HeapManager.GetIndexedByte(context.Receiver, i);
            }

            string stringValue = System.Text.Encoding.Latin1.GetString(bytes);

            // Look up in USTable
            if (context.RuntimeServices == null) return PrimitiveResult.Fail(context.Metadata!);
            var symbolOOP = context.RuntimeServices.InternSymbol(stringValue);

            if (symbolOOP == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(symbolOOP);
        }
    }

    /// <summary>
    /// Primitive 107: String storeOn: aStream
    /// Fast bulk write of string with quote doubling to a WriteStream.
    /// WriteStream layout: collection(0), position(1), readLimit(2), writeLimit(3)
    /// </summary>
    public class StoreOn : IPrimitive
    {
        private const int STREAM_COLLECTION_INDEX = 0;
        private const int STREAM_POSITION_INDEX = 1;
        private const int STREAM_WRITELIMIT_INDEX = 3;
        private const byte QUOTE = (byte)'\'';

        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            var streamOop = context.Arguments[0];
            if (streamOop.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Get string contents
            int stringSize = context.HeapManager.GetIndexedSize(context.Receiver);

            // Count embedded quotes to calculate required space
            int quoteCount = 0;
            for (int i = 0; i < stringSize; i++)
            {
                if (context.HeapManager.GetIndexedByte(context.Receiver, i) == QUOTE)
                    quoteCount++;
            }

            // Required space: opening quote + string + doubled quotes + closing quote
            int requiredSpace = 1 + stringSize + quoteCount + 1;

            // Get stream fields
            var collectionOop = context.HeapManager.GetInstanceVariable(streamOop, STREAM_COLLECTION_INDEX);
            var positionField = context.HeapManager.GetInstanceVariable(streamOop, STREAM_POSITION_INDEX);
            var writeLimitField = context.HeapManager.GetInstanceVariable(streamOop, STREAM_WRITELIMIT_INDEX);

            if (!positionField.IsSmallInteger || !writeLimitField.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            if (collectionOop.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int position = positionField.AsSmallInteger();
            int writeLimit = writeLimitField.AsSmallInteger();
            int collectionSize = context.HeapManager.GetIndexedSize(collectionOop);

            // Check if we have enough space (fail to let Smalltalk grow the collection)
            if (position + requiredSpace > writeLimit || position + requiredSpace > collectionSize)
                return PrimitiveResult.Fail(context.Metadata!);

            // Write opening quote
            context.HeapManager.SetIndexedByte(collectionOop, position++, QUOTE);

            // Write string contents, doubling embedded quotes
            for (int i = 0; i < stringSize; i++)
            {
                byte ch = context.HeapManager.GetIndexedByte(context.Receiver, i);
                context.HeapManager.SetIndexedByte(collectionOop, position++, ch);
                if (ch == QUOTE)
                {
                    context.HeapManager.SetIndexedByte(collectionOop, position++, QUOTE);
                }
            }

            // Write closing quote
            context.HeapManager.SetIndexedByte(collectionOop, position++, QUOTE);

            // Update stream position (Smalltalk position is 1-based index of last written)
            context.HeapManager.SetInstanceVariable(streamOop, STREAM_POSITION_INDEX, SmalltalkOOP.FromSmallInteger(position));

            // Return the receiver (String)
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>
    /// Primitive 108: String indexOf:startingAt:
    /// Fast character search in string. Returns 1-based index or 0 if not found.
    /// </summary>
    public class IndexOf : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // First arg is the character to find
            var charArg = context.Arguments[0];
            if (charArg.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Check if it's a Character object (class index for Character)
            var charClass = context.HeapManager.GetClassOOP(charArg);
            var characterClassOop = context.RuntimeServices?.GetClass("Character") ?? default;
            if (charClass != characterClassOop)
                return PrimitiveResult.Fail(context.Metadata!);

            // Character stores its value in first instance variable
            var charValueOop = context.HeapManager.GetInstanceVariable(charArg, 0);
            if (!charValueOop.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            byte searchByte = (byte)charValueOop.AsSmallInteger();

            // Second arg is the start index (1-based)
            if (!context.Arguments[1].IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            int startIndex = context.Arguments[1].AsSmallInteger();

            // Get string size
            int size = context.HeapManager.GetIndexedSize(context.Receiver);

            // Convert to 0-based and validate
            int start0 = startIndex - 1;
            if (start0 < 0) start0 = 0;
            if (start0 >= size)
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0));

            // Fast search
            for (int i = start0; i < size; i++)
            {
                if (context.HeapManager.GetIndexedByte(context.Receiver, i) == searchByte)
                {
                    return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(i + 1)); // 1-based
                }
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0)); // Not found
        }
    }
}
