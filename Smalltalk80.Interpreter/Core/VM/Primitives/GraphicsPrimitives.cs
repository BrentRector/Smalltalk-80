using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Graphics primitives for BitBlt, display, and time (96-104).
/// Uses BitBlt32Engine for 32bpp raster operations.
/// 32bpp architecture: each pixel is one uint (0xAARRGGBB format).
/// </summary>
public static class GraphicsOperations
{
    /// <summary>Primitive 96: copyBits - Core BitBlt raster operation</summary>
    public class CopyBits : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0)
                return PrimitiveResult.Fail(context.Metadata!);

            if (context.Receiver.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            var receiverOOP = context.Receiver;
            var heap = context.HeapManager;
            var nil = heap.GetNil();

            // Get BitBlt fields (15 instance variables - 'color' added at index 14)
            SmalltalkOOP destForm = heap.GetInstanceVariable(receiverOOP, 0);
            SmalltalkOOP sourceForm = heap.GetInstanceVariable(receiverOOP, 1);
            SmalltalkOOP halftoneForm = heap.GetInstanceVariable(receiverOOP, 2);
            SmalltalkOOP combinationRuleOOP = heap.GetInstanceVariable(receiverOOP, 3);
            SmalltalkOOP destXOOP = heap.GetInstanceVariable(receiverOOP, 4);
            SmalltalkOOP destYOOP = heap.GetInstanceVariable(receiverOOP, 5);
            SmalltalkOOP widthOOP = heap.GetInstanceVariable(receiverOOP, 6);
            SmalltalkOOP heightOOP = heap.GetInstanceVariable(receiverOOP, 7);
            SmalltalkOOP sourceXOOP = heap.GetInstanceVariable(receiverOOP, 8);
            SmalltalkOOP sourceYOOP = heap.GetInstanceVariable(receiverOOP, 9);
            SmalltalkOOP clipXOOP = heap.GetInstanceVariable(receiverOOP, 10);
            SmalltalkOOP clipYOOP = heap.GetInstanceVariable(receiverOOP, 11);
            SmalltalkOOP clipWidthOOP = heap.GetInstanceVariable(receiverOOP, 12);
            SmalltalkOOP clipHeightOOP = heap.GetInstanceVariable(receiverOOP, 13);
            SmalltalkOOP colorOOP = heap.GetInstanceVariable(receiverOOP, 14);

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

            // Rules 0-15 are standard BitBlt rules
            // Rule 16 is "paint" mode - for 32bpp we implement grayscale alpha blending
            // Rule 17+ require Smalltalk handling
            if (combinationRule < 0 || combinationRule > 16)
                return PrimitiveResult.Fail(context.Metadata!);

            // OPTIMIZATION: Use direct heap span access - no extract/writeback copies
            if (!GetFormSpanDirect32(heap, destForm, out var destPixels, out var destWidth, out var destHeight))
                return PrimitiveResult.Fail(context.Metadata!);

            Span<uint> sourcePixelsSpan = default;
            int sourceWidth = 0, sourceHeight = 0;

            if (sourceForm != nil && !sourceForm.IsSmallInteger)
            {
                GetFormSpanDirect32(heap, sourceForm, out sourcePixelsSpan, out sourceWidth, out sourceHeight);
            }
            ReadOnlySpan<uint> sourcePixels = sourcePixelsSpan;

            Span<uint> halftonePixelsSpan = default;
            int halftoneWidth = 0, halftoneHeight = 0;

            if (halftoneForm != nil && !halftoneForm.IsSmallInteger)
            {
                GetFormSpanDirect32(heap, halftoneForm, out halftonePixelsSpan, out halftoneWidth, out halftoneHeight);
            }
            ReadOnlySpan<uint> halftonePixels = halftonePixelsSpan;

            // Extract color if present (first-class color support for 32bpp)
            // Color object stores 10-bit RGB in instance variable 0 (rgb field)
            uint? colorPixel = null;
            if (colorOOP != nil && !colorOOP.IsSmallInteger)
            {
                SmalltalkOOP rgbOOP = heap.GetInstanceVariable(colorOOP, 0);
                if (rgbOOP.IsSmallInteger)
                {
                    int rgb = rgbOOP.AsSmallInteger();
                    // Convert from 10-bit RGB (internal Color format) to 8-bit RGB (pixel)
                    int r = ((rgb >> 20) & 0x3FF) * 255 / 1023;
                    int g = ((rgb >> 10) & 0x3FF) * 255 / 1023;
                    int b = (rgb & 0x3FF) * 255 / 1023;
                    colorPixel = ((uint)r << 16) | ((uint)g << 8) | (uint)b;
                }
            }

            // Perform 32bpp BitBlt directly on heap memory - no copy needed!
            BitBlt32Engine.CopyBits(
                destPixels,
                destWidth, destHeight,
                destX, destY,
                sourcePixels,
                sourceWidth, sourceHeight,
                sourceX, sourceY,
                width, height,
                clipX, clipY, clipWidth, clipHeight,
                halftonePixels,
                halftoneWidth, halftoneHeight,
                colorPixel,
                (CombinationRule)combinationRule);

            // No writeback needed - we modified heap directly!

            // Invalidate Display region if destination is Display
            InvalidateDisplayIfNeeded(context, destForm, destX, destY, width, height,
                clipX, clipY, clipWidth, clipHeight);

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 97: snapshot - Save image to disk</summary>
    public class Snapshot : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Display == null) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Metadata == null) return PrimitiveResult.Fail(context.Metadata!);
            if (context.StackManager == null) return PrimitiveResult.Fail(context.Metadata!);

            var heap = context.HeapManager;

            // Get active process from ProcessorScheduler
            var schedulerOOP = context.Metadata.ProcessorScheduler;
            var activeProcessOOP = heap.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

            // Save current active context to Process.suspendedContext
            var currentContextOOP = context.GetActiveContext();
            heap.SetInstanceVariable(activeProcessOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, currentContextOOP);

            // Push receiver onto stack (becomes return value on restore)
            context.StackManager.Push(context.Receiver);

            // Write the image
            bool success = context.Display.PerformSnapshot(context.Receiver);

            // Pop receiver back
            context.StackManager.Pop();

            if (!success) return PrimitiveResult.Fail(context.Metadata!);

            // Return nil to indicate "snapshot was just created"
            return PrimitiveResult.Ok(heap.GetNil());
        }
    }

    /// <summary>Primitive 98: oopsLeft - Available object pointers</summary>
    public class OopsLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(100000));
        }
    }

    /// <summary>Primitive 99: coreLeft - Available memory in bytes</summary>
    public class CoreLeft : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

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

            int seconds = context.Display?.GetTimeWords()
                ?? (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(seconds));
        }
    }

    /// <summary>Primitive 101: beCursor - Set hardware cursor</summary>
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

    /// <summary>Primitive 102: beDisplay - Make Form the active display</summary>
    public class BeDisplay : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Read Display dimensions from Form instance variables
            int width = context.HeapManager.GetInstanceVariable(context.Receiver, 1).AsSmallInteger();
            int height = context.HeapManager.GetInstanceVariable(context.Receiver, 2).AsSmallInteger();

            // Get or create display with these dimensions
            // This may trigger lazy display creation if no display exists yet
            context.GetOrCreateDisplay(width, height);

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 103: beep - System beep sound</summary>
    public class Beep : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Display != null)
                context.Display.Beep();
            else
                try { Console.Beep(); } catch { }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 104: drawLoopX:Y: - Bresenham line drawing for BitBlt</summary>
    public class DrawLoop : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Receiver.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            if (context.Arguments.Length != 2)
                return PrimitiveResult.Fail(context.Metadata!);

            var xDeltaOOP = context.Arguments[0];
            var yDeltaOOP = context.Arguments[1];

            if (!xDeltaOOP.IsSmallInteger || !yDeltaOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int xDelta = xDeltaOOP.AsSmallInteger();
            int yDelta = yDeltaOOP.AsSmallInteger();

            var heap = context.HeapManager;
            var nil = heap.GetNil();

            // Extract BitBlt instance variables
            var destFormOOP = heap.GetInstanceVariable(context.Receiver, 0);
            var sourceFormOOP = heap.GetInstanceVariable(context.Receiver, 1);
            var halftoneFormOOP = heap.GetInstanceVariable(context.Receiver, 2);
            var combinationRuleOOP = heap.GetInstanceVariable(context.Receiver, 3);
            var destXOOP = heap.GetInstanceVariable(context.Receiver, 4);
            var destYOOP = heap.GetInstanceVariable(context.Receiver, 5);
            var widthOOP = heap.GetInstanceVariable(context.Receiver, 6);
            var heightOOP = heap.GetInstanceVariable(context.Receiver, 7);
            var sourceXOOP = heap.GetInstanceVariable(context.Receiver, 8);
            var sourceYOOP = heap.GetInstanceVariable(context.Receiver, 9);
            var clipXOOP = heap.GetInstanceVariable(context.Receiver, 10);
            var clipYOOP = heap.GetInstanceVariable(context.Receiver, 11);
            var clipWidthOOP = heap.GetInstanceVariable(context.Receiver, 12);
            var clipHeightOOP = heap.GetInstanceVariable(context.Receiver, 13);
            var colorOOP = heap.GetInstanceVariable(context.Receiver, 14);

            // Validate numeric fields
            if (!combinationRuleOOP.IsSmallInteger || !destXOOP.IsSmallInteger || !destYOOP.IsSmallInteger ||
                !widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger || !sourceXOOP.IsSmallInteger ||
                !sourceYOOP.IsSmallInteger || !clipXOOP.IsSmallInteger || !clipYOOP.IsSmallInteger ||
                !clipWidthOOP.IsSmallInteger || !clipHeightOOP.IsSmallInteger)
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }

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

            // OPTIMIZATION: Use direct heap span access - no extract/writeback copies
            if (!GetFormSpanDirect32(heap, destFormOOP, out var destPixels, out var destWidth, out var destHeight))
                return PrimitiveResult.Fail(context.Metadata!);

            Span<uint> sourcePixelsSpan = default;
            int sourceWidth = 0, sourceHeight = 0;

            if (sourceFormOOP != nil && !sourceFormOOP.IsSmallInteger)
            {
                GetFormSpanDirect32(heap, sourceFormOOP, out sourcePixelsSpan, out sourceWidth, out sourceHeight);
            }
            ReadOnlySpan<uint> sourcePixels = sourcePixelsSpan;

            Span<uint> halftonePixelsSpan = default;
            int halftoneWidth = 0, halftoneHeight = 0;

            if (halftoneFormOOP != nil && !halftoneFormOOP.IsSmallInteger)
            {
                GetFormSpanDirect32(heap, halftoneFormOOP, out halftonePixelsSpan, out halftoneWidth, out halftoneHeight);
            }
            ReadOnlySpan<uint> halftonePixels = halftonePixelsSpan;

            // Extract color if present (first-class color support for 32bpp)
            // Color object stores 10-bit RGB in instance variable 0 (rgb field)
            uint? colorPixel = null;
            if (colorOOP != nil && !colorOOP.IsSmallInteger)
            {
                SmalltalkOOP rgbOOP = heap.GetInstanceVariable(colorOOP, 0);
                if (rgbOOP.IsSmallInteger)
                {
                    int rgb = rgbOOP.AsSmallInteger();
                    // Convert from 10-bit RGB (internal Color format) to 8-bit RGB (pixel)
                    int r = ((rgb >> 20) & 0x3FF) * 255 / 1023;
                    int g = ((rgb >> 10) & 0x3FF) * 255 / 1023;
                    int b = (rgb & 0x3FF) * 255 / 1023;
                    colorPixel = ((uint)r << 16) | ((uint)g << 8) | (uint)b;
                }
            }

            // Map combination rule 16 (paint) to Or
            var rule = combinationRule == 16 ? CombinationRule.Or : (CombinationRule)combinationRule;

            // Bresenham line drawing algorithm
            int dx = Math.Sign(xDelta);
            int dy = Math.Sign(yDelta);
            int px = Math.Abs(yDelta);
            int py = Math.Abs(xDelta);

            // Draw starting point
            DrawPixel32(destPixels, destWidth, destHeight, destX, destY,
                sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY,
                width, height, clipX, clipY, clipWidth, clipHeight,
                halftonePixels, halftoneWidth, halftoneHeight, colorPixel, rule);

            if (py > px)
            {
                // More horizontal
                int P = py / 2;
                for (int i = 0; i < py; i++)
                {
                    destX += dx;
                    P -= px;
                    if (P < 0)
                    {
                        destY += dy;
                        P += py;
                    }
                    DrawPixel32(destPixels, destWidth, destHeight, destX, destY,
                        sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY,
                        width, height, clipX, clipY, clipWidth, clipHeight,
                        halftonePixels, halftoneWidth, halftoneHeight, colorPixel, rule);
                }
            }
            else
            {
                // More vertical
                int P = px / 2;
                for (int i = 0; i < px; i++)
                {
                    destY += dy;
                    P -= py;
                    if (P < 0)
                    {
                        destX += dx;
                        P += px;
                    }
                    DrawPixel32(destPixels, destWidth, destHeight, destX, destY,
                        sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY,
                        width, height, clipX, clipY, clipWidth, clipHeight,
                        halftonePixels, halftoneWidth, halftoneHeight, colorPixel, rule);
                }
            }

            // No writeback needed - we modified heap directly!

            // Invalidate Display region
            int startX = destXOOP.AsSmallInteger();
            int startY = destYOOP.AsSmallInteger();
            InvalidateDisplayIfNeeded(context, destFormOOP,
                Math.Min(startX, startX + xDelta),
                Math.Min(startY, startY + yDelta),
                Math.Abs(xDelta) + width,
                Math.Abs(yDelta) + height,
                clipX, clipY, clipWidth, clipHeight);

            return PrimitiveResult.Ok(context.Receiver);
        }

        private static void DrawPixel32(
            Span<uint> destPixels, int destWidth, int destHeight, int destX, int destY,
            ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
            int width, int height,
            int clipX, int clipY, int clipWidth, int clipHeight,
            ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
            uint? color,
            CombinationRule rule)
        {
            BitBlt32Engine.CopyBits(
                destPixels, destWidth, destHeight, destX, destY,
                sourcePixels, sourceWidth, sourceHeight, sourceX, sourceY,
                width, height,
                clipX, clipY, clipWidth, clipHeight,
                halftonePixels, halftoneWidth, halftoneHeight,
                color,  // first-class color support for all drawing
                rule);
        }
    }

    // ============================================================================
    // Helper methods for 32bpp Forms
    // ============================================================================

    /// <summary>
    /// Get direct span access to 32bpp Form pixel data in heap memory.
    /// OPTIMIZATION: Zero-copy access - operates directly on heap backing store.
    /// Eliminates the extract/writeback overhead for every BitBlt operation.
    /// Uses out parameters since Span is a ref struct and can't be in tuples.
    /// </summary>
    private static bool GetFormSpanDirect32(
        ISmalltalkHeap heap, SmalltalkOOP formOOP,
        out Span<uint> pixels, out int width, out int height)
    {
        pixels = Span<uint>.Empty;
        width = 0;
        height = 0;

        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger)
            return false;

        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        SmalltalkOOP widthOOP = heap.GetInstanceVariable(formOOP, 1);
        SmalltalkOOP heightOOP = heap.GetInstanceVariable(formOOP, 2);

        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return false;

        width = widthOOP.AsSmallInteger();
        height = heightOOP.AsSmallInteger();

        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return false;

        // Get direct span into heap memory - no copy!
        pixels = heap.GetWordSpan(bitsOOP);
        return pixels.Length > 0;
    }

    /// <summary>
    /// Extract 32bpp pixel data from a Form object (creates a copy).
    /// LEGACY: Use GetFormSpanDirect32 for better performance.
    /// 32bpp architecture: each pixel is one uint in the WordArray.
    /// </summary>
    private static (Memory<uint> pixels, int width, int height) ExtractFormData32(
        ISmalltalkHeap heap, SmalltalkOOP formOOP)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger)
            return (Memory<uint>.Empty, 0, 0);

        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        SmalltalkOOP widthOOP = heap.GetInstanceVariable(formOOP, 1);
        SmalltalkOOP heightOOP = heap.GetInstanceVariable(formOOP, 2);

        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return (Memory<uint>.Empty, 0, 0);

        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();

        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return (Memory<uint>.Empty, 0, 0);

        var format = heap.GetObjectFormat(bitsOOP);
        if (format != ObjectFormat.WordIndexed)
            return (Memory<uint>.Empty, 0, 0);

        int size = heap.GetIndexedSize(bitsOOP);
        uint[] pixels = new uint[size];

        // Copy pixels directly from heap (each word is one 32bpp pixel)
        for (int i = 0; i < size; i++)
        {
            pixels[i] = heap.GetIndexedWord(bitsOOP, i);
        }

        return (pixels, width, height);
    }

    /// <summary>
    /// Write 32bpp pixel data back to a Form object.
    /// </summary>
    private static void WriteFormData32(ISmalltalkHeap heap, SmalltalkOOP formOOP, ReadOnlySpan<uint> pixels)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger || pixels.Length == 0)
            return;

        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return;

        var format = heap.GetObjectFormat(bitsOOP);
        if (format != ObjectFormat.WordIndexed)
            return;

        // Write pixels directly to heap (each word is one 32bpp pixel)
        for (int i = 0; i < pixels.Length; i++)
        {
            heap.SetIndexedWord(bitsOOP, i, pixels[i]);
        }
    }

    // Legacy 1bpp helper methods (kept for compatibility during transition)
    private static (Memory<byte> bits, int width, int height) ExtractFormData(
        ISmalltalkHeap heap, SmalltalkOOP formOOP)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        SmalltalkOOP widthOOP = heap.GetInstanceVariable(formOOP, 1);
        SmalltalkOOP heightOOP = heap.GetInstanceVariable(formOOP, 2);

        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();

        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        var format = heap.GetObjectFormat(bitsOOP);
        int size = heap.GetIndexedSize(bitsOOP);
        byte[] bits;

        if (format == ObjectFormat.WordIndexed)
        {
            // 32-bit words: 4 bytes per word in big-endian format (MSB-first pixels)
            bits = new byte[size * 4];
            heap.CopyIndexedWords(bitsOOP, bits.AsSpan());

            // For narrow Forms (width <= 16), the 16-bit pattern is stored in lower 16 bits
            // of 32-bit words. After big-endian extraction, the pattern is in bytes 2-3.
            // BitBlt reads pixels 0-15 from bytes 0-1, so swap bytes 0↔2 and 1↔3.
            if (width > 0 && width <= 16)
            {
                for (int i = 0; i < bits.Length; i += 4)
                {
                    (bits[i], bits[i + 2]) = (bits[i + 2], bits[i]);
                    (bits[i + 1], bits[i + 3]) = (bits[i + 3], bits[i + 1]);
                }
            }
        }
        else
        {
            bits = new byte[size];
            heap.CopyIndexedBytes(bitsOOP, bits.AsSpan());
        }

        return (bits, width, height);
    }

    private static void WriteFormData(ISmalltalkHeap heap, SmalltalkOOP formOOP, ReadOnlySpan<byte> bits)
    {
        var nil = heap.GetNil();
        if (formOOP == nil || formOOP.IsSmallInteger || bits.Length == 0)
            return;

        SmalltalkOOP bitsOOP = heap.GetInstanceVariable(formOOP, 0);
        SmalltalkOOP widthOOP = heap.GetInstanceVariable(formOOP, 1);
        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return;

        var format = heap.GetObjectFormat(bitsOOP);

        if (format == ObjectFormat.WordIndexed)
        {
            // For narrow Forms (width <= 16), reverse the byte swap done during read
            int width = widthOOP.IsSmallInteger ? widthOOP.AsSmallInteger() : 0;
            if (width > 0 && width <= 16)
            {
                byte[] swapped = bits.ToArray();
                for (int i = 0; i < swapped.Length; i += 4)
                {
                    (swapped[i], swapped[i + 2]) = (swapped[i + 2], swapped[i]);
                    (swapped[i + 1], swapped[i + 3]) = (swapped[i + 3], swapped[i + 1]);
                }
                heap.SetIndexedWords(bitsOOP, swapped);
            }
            else
            {
                heap.SetIndexedWords(bitsOOP, bits);
            }
        }
        else
            heap.SetIndexedBytes(bitsOOP, bits);
    }

    private static void InvalidateDisplayIfNeeded(
        PrimitiveExecutionContext context,
        SmalltalkOOP destForm,
        int destX, int destY, int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight)
    {
        if (context.Display == null || context.Metadata == null)
            return;

        try
        {
            var displayOOP = context.Metadata.KnownObjects.Display;
            var nil = context.HeapManager.GetNil();

            if (destForm == displayOOP && displayOOP != nil)
            {
                int invalidX = Math.Max(destX, clipX);
                int invalidY = Math.Max(destY, clipY);
                int invalidRight = Math.Min(destX + width, clipX + clipWidth);
                int invalidBottom = Math.Min(destY + height, clipY + clipHeight);
                int invalidWidth = Math.Max(0, invalidRight - invalidX);
                int invalidHeight = Math.Max(0, invalidBottom - invalidY);

                if (invalidWidth > 0 && invalidHeight > 0)
                {
                    context.Display.InvalidateRegion(
                        invalidX, invalidY, invalidWidth, invalidHeight);
                }
            }
        }
        catch
        {
            // Silently ignore display invalidation errors
        }
    }
}
