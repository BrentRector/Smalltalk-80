using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Graphics primitives for BitBlt, display, and time (96-104) using new IPrimitive interface.
/// </summary>
public static class GraphicsOperations
{
    /// <summary>Primitive 96: copyBits - Core BitBlt raster operation</summary>
    public class CopyBits : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Prefer hardware-accelerated BitBlt if available
            if (context.Platform != null)
            {
                bool success = context.Platform.PerformBitBlt(context.Receiver);
                if (!success) return PrimitiveResult.Fail(context.Metadata!);
                return PrimitiveResult.Ok(context.Receiver);
            }

            // Software BitBlt implementation
            var receiverOOP = context.Receiver;

            // Get BitBlt fields (14 instance variables)
            SmalltalkOOP destForm = context.HeapManager.GetInstanceVariable(receiverOOP, 0);
            SmalltalkOOP sourceForm = context.HeapManager.GetInstanceVariable(receiverOOP, 1);
            SmalltalkOOP halftoneForm = context.HeapManager.GetInstanceVariable(receiverOOP, 2);
            SmalltalkOOP combinationRuleOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 3);
            SmalltalkOOP destXOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 4);
            SmalltalkOOP destYOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 5);
            SmalltalkOOP widthOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 6);
            SmalltalkOOP heightOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 7);
            SmalltalkOOP sourceXOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 8);
            SmalltalkOOP sourceYOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 9);
            SmalltalkOOP clipXOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 10);
            SmalltalkOOP clipYOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 11);
            SmalltalkOOP clipWidthOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 12);
            SmalltalkOOP clipHeightOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 13);

            // Validate all numeric parameters
            if (!combinationRuleOOP.IsSmallInteger || !destXOOP.IsSmallInteger || !destYOOP.IsSmallInteger ||
                !widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger || !sourceXOOP.IsSmallInteger ||
                !sourceYOOP.IsSmallInteger || !clipXOOP.IsSmallInteger || !clipYOOP.IsSmallInteger ||
                !clipWidthOOP.IsSmallInteger || !clipHeightOOP.IsSmallInteger)
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // Extract numeric parameters
            int combinationRule = combinationRuleOOP.AsSmallInteger();
            int destX = destXOOP.AsSmallInteger();
            int destY = destYOOP.AsSmallInteger();
            int width = widthOOP.AsSmallInteger();
            int height = heightOOP.AsSmallInteger();
            int sourceX = sourceXOOP.AsSmallInteger();
            int sourceY = sourceYOOP.AsSmallInteger();
            int clipX = clipXOOP.AsSmallInteger();
            int clipY = clipYOOP.AsSmallInteger();
            int clipWidth = clipWidthOOP.AsSmallInteger();
            int clipHeight = clipHeightOOP.AsSmallInteger();

            // Extract bitmap data from Form objects
            var (destBits, destWidth, destHeight) = ExtractFormData(context, destForm);
            var (sourceBits, sourceWidth, sourceHeight) = ExtractFormData(context, sourceForm);
            var (halftoneBits, halftoneWidth, halftoneHeight) = ExtractFormData(context, halftoneForm);

            // Perform BitBlt
            BitBltEngine.CopyBits(
                destBits.Span, destWidth, destHeight, destX, destY,
                sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
                width, height,
                clipX, clipY, clipWidth, clipHeight,
                halftoneBits.Span, halftoneWidth, halftoneHeight,
                (CombinationRule)(combinationRule & 0x0F));

            return PrimitiveResult.Ok(context.Receiver);
        }

        private static (Memory<byte> bits, int width, int height) ExtractFormData(
            PrimitiveExecutionContext context, SmalltalkOOP formOOP)
        {
            if (formOOP == context.HeapManager.GetNil() || formOOP.IsSmallInteger)
                return (Memory<byte>.Empty, 0, 0);

            // Get Form instance variables: bits (0), width (1), height (2)
            SmalltalkOOP bitsOOP = context.HeapManager.GetInstanceVariable(formOOP, 0);
            SmalltalkOOP widthOOP = context.HeapManager.GetInstanceVariable(formOOP, 1);
            SmalltalkOOP heightOOP = context.HeapManager.GetInstanceVariable(formOOP, 2);

            if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
                return (Memory<byte>.Empty, 0, 0);

            int width = widthOOP.AsSmallInteger();
            int height = heightOOP.AsSmallInteger();

            // Get bitmap data
            if (bitsOOP == context.HeapManager.GetNil() || bitsOOP.IsSmallInteger)
                return (Memory<byte>.Empty, 0, 0);

            // Check object format to determine how to extract bits
            var format = context.HeapManager.GetObjectFormat(bitsOOP);
            int size = context.HeapManager.GetIndexedSize(bitsOOP);
            byte[] bits;

            if (format == ObjectFormat.WordIndexed)
            {
                // WordArray: each element is a 16-bit word, convert to bytes
                bits = new byte[size * 2];
                for (int i = 0; i < size; i++)
                {
                    ushort word = context.HeapManager.GetIndexedWord(bitsOOP, i);
                    // Big-endian byte order (Smalltalk convention)
                    bits[i * 2] = (byte)(word >> 8);
                    bits[i * 2 + 1] = (byte)(word & 0xFF);
                }
            }
            else
            {
                // ByteArray or other: read as bytes directly
                bits = new byte[size];
                for (int i = 0; i < size; i++)
                {
                    bits[i] = context.HeapManager.GetIndexedByte(bitsOOP, i);
                }
            }

            return (bits, width, height);
        }
    }

    /// <summary>Primitive 97: snapshot - Save image to disk</summary>
    public class Snapshot : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Platform == null) return PrimitiveResult.Fail(context.Metadata!);

            bool success = context.Platform.PerformSnapshot(context.Receiver);
            if (!success) return PrimitiveResult.Fail(context.Metadata!);

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 98: oopsLeft - Available object pointers</summary>
    public class OopsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Return estimate of available OOP slots
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(100000));
        }
    }

    /// <summary>Primitive 99: coreLeft - Available memory in bytes</summary>
    public class CoreLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Query .NET GC for available memory estimate
            long availableMemory = GC.GetTotalMemory(false);
            int coreLeft = availableMemory > int.MaxValue ? int.MaxValue : (int)availableMemory;
            coreLeft = coreLeft & 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(coreLeft));
        }
    }

    /// <summary>Primitive 100: timeWords - Current time in seconds</summary>
    public class TimeWords : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            int seconds;
            if (context.Platform != null)
            {
                seconds = context.Platform.GetTimeWords();
            }
            else
            {
                seconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(seconds));
        }
    }

    /// <summary>Primitive 101: beDisplay - Make Form the active display</summary>
    public class BeDisplay : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Invalidate display if platform bridge available
            if (context.Platform != null)
            {
                try
                {
                    int width = context.HeapManager.GetInstanceVariable(context.Receiver, 1).AsSmallInteger();
                    int height = context.HeapManager.GetInstanceVariable(context.Receiver, 2).AsSmallInteger();
                    context.Platform.InvalidateDisplayRegion(0, 0, width, height);
                }
                catch
                {
                    // Ignore invalidation errors
                }
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 102: beCursor - Set hardware cursor</summary>
    public class BeCursor : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Stub implementation
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 103: beep - System beep sound</summary>
    public class Beep : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Platform != null)
            {
                context.Platform.Beep();
            }
            else
            {
                try { Console.Beep(); } catch { }
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 104: clipboardText - Clipboard operations</summary>
    public class ClipboardText : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Platform == null) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Arguments.Length == 0)
            {
                // Read clipboard
                SmalltalkOOP stringOOP = context.Platform.ReadClipboardToStringOOP();
                return PrimitiveResult.Ok(stringOOP);
            }
            else if (context.Arguments.Length == 1)
            {
                // Write clipboard
                SmalltalkOOP stringOOP = context.Arguments[0];
                if (stringOOP.IsSmallInteger || stringOOP == context.HeapManager.GetNil())
                    return PrimitiveResult.Fail(context.Metadata!);

                bool success = context.Platform.WriteClipboardFromStringOOP(stringOOP);
                if (!success) return PrimitiveResult.Fail(context.Metadata!);

                return PrimitiveResult.Ok(context.Receiver);
            }

            return PrimitiveResult.Fail(context.Metadata!);
        }
    }
}
