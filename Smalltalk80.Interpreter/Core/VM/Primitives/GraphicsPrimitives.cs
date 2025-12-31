using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;
using Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Graphics primitives for BitBlt, display, and time (96-104).
/// Uses IGraphicsBackend abstraction for pluggable rendering implementations.
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

            // Get BitBlt fields (14 instance variables)
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
            // Rule 16+ require Smalltalk handling (paintBits)
            if (combinationRule < 0 || combinationRule > 15)
                return PrimitiveResult.Fail(context.Metadata!);

            // Get graphics backend
            var backend = GraphicsBackendFactory.Current;
            if (backend == null)
                return PrimitiveResult.Fail(context.Metadata!);

            // Extract Form data and create textures
            var (destBits, destWidth, destHeight) = ExtractFormData(heap, destForm);
            if (destBits.Length == 0)
                return PrimitiveResult.Fail(context.Metadata!);

            using var destTexture = backend.CreateTextureFromData(
                destWidth, destHeight, PixelDepth.OneBit, destBits.Span);

            ITexture? sourceTexture = null;
            ITexture? halftoneTexture = null;

            try
            {
                if (sourceForm != nil && !sourceForm.IsSmallInteger)
                {
                    var (srcBits, srcWidth, srcHeight) = ExtractFormData(heap, sourceForm);
                    if (srcBits.Length > 0)
                    {
                        sourceTexture = backend.CreateTextureFromData(
                            srcWidth, srcHeight, PixelDepth.OneBit, srcBits.Span);
                    }
                }

                if (halftoneForm != nil && !halftoneForm.IsSmallInteger)
                {
                    var (htBits, htWidth, htHeight) = ExtractFormData(heap, halftoneForm);
                    if (htBits.Length > 0)
                    {
                        halftoneTexture = backend.CreateTextureFromData(
                            htWidth, htHeight, PixelDepth.OneBit, htBits.Span);
                    }
                }

                // Perform BitBlt via abstraction layer
                backend.BitBlt(new BitBltParams
                {
                    Destination = destTexture,
                    DestX = destX,
                    DestY = destY,
                    Source = sourceTexture,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    Width = width,
                    Height = height,
                    ClipRect = new GraphicsRect(clipX, clipY, clipWidth, clipHeight),
                    Halftone = halftoneTexture,
                    BlendMode = (BlendMode)combinationRule
                });

                // Write modified dest bits back to heap
                using var destLock = destTexture.Lock(TextureAccess.Read);
                WriteFormData(heap, destForm, destLock.Data.ToArray());

                // Invalidate Display region if destination is Display
                InvalidateDisplayIfNeeded(context, destForm, destX, destY, width, height,
                    clipX, clipY, clipWidth, clipHeight);
            }
            finally
            {
                sourceTexture?.Dispose();
                halftoneTexture?.Dispose();
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 97: snapshot - Save image to disk</summary>
    public class Snapshot : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Platform == null) return PrimitiveResult.Fail(context.Metadata!);
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
            bool success = context.Platform.PerformSnapshot(context.Receiver);

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

            int seconds = context.Platform?.GetTimeWords()
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

            // Notify platform bridge of new Display dimensions
            context.Platform?.DisplayResized(width, height);

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
                context.Platform.Beep();
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

            // Get graphics backend
            var backend = GraphicsBackendFactory.Current;
            if (backend == null)
                return PrimitiveResult.Fail(context.Metadata!);

            // Extract Form data
            var (destBits, destWidth, destHeight) = ExtractFormData(heap, destFormOOP);
            if (destBits.Length == 0)
                return PrimitiveResult.Fail(context.Metadata!);

            using var destTexture = backend.CreateTextureFromData(
                destWidth, destHeight, PixelDepth.OneBit, destBits.Span);

            ITexture? sourceTexture = null;
            ITexture? halftoneTexture = null;

            try
            {
                if (sourceFormOOP != nil && !sourceFormOOP.IsSmallInteger)
                {
                    var (srcBits, srcWidth, srcHeight) = ExtractFormData(heap, sourceFormOOP);
                    if (srcBits.Length > 0)
                    {
                        sourceTexture = backend.CreateTextureFromData(
                            srcWidth, srcHeight, PixelDepth.OneBit, srcBits.Span);
                    }
                }

                if (halftoneFormOOP != nil && !halftoneFormOOP.IsSmallInteger)
                {
                    var (htBits, htWidth, htHeight) = ExtractFormData(heap, halftoneFormOOP);
                    if (htBits.Length > 0)
                    {
                        halftoneTexture = backend.CreateTextureFromData(
                            htWidth, htHeight, PixelDepth.OneBit, htBits.Span);
                    }
                }

                // Map combination rule 16 (paint) to Or
                var blendMode = combinationRule == 16 ? BlendMode.Or : (BlendMode)combinationRule;
                var clipRect = new GraphicsRect(clipX, clipY, clipWidth, clipHeight);

                // Bresenham line drawing algorithm
                int dx = Math.Sign(xDelta);
                int dy = Math.Sign(yDelta);
                int px = Math.Abs(yDelta);
                int py = Math.Abs(xDelta);

                // Draw starting point
                DrawPixel(backend, destTexture, sourceTexture, halftoneTexture,
                    destX, destY, sourceX, sourceY, width, height, clipRect, blendMode);

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
                        DrawPixel(backend, destTexture, sourceTexture, halftoneTexture,
                            destX, destY, sourceX, sourceY, width, height, clipRect, blendMode);
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
                        DrawPixel(backend, destTexture, sourceTexture, halftoneTexture,
                            destX, destY, sourceX, sourceY, width, height, clipRect, blendMode);
                    }
                }

                // Write modified dest bits back
                using var destLock = destTexture.Lock(TextureAccess.Read);
                WriteFormData(heap, destFormOOP, destLock.Data.ToArray());

                // Invalidate Display region
                int startX = destXOOP.AsSmallInteger();
                int startY = destYOOP.AsSmallInteger();
                InvalidateDisplayIfNeeded(context, destFormOOP,
                    Math.Min(startX, startX + xDelta),
                    Math.Min(startY, startY + yDelta),
                    Math.Abs(xDelta) + width,
                    Math.Abs(yDelta) + height,
                    clipX, clipY, clipWidth, clipHeight);
            }
            finally
            {
                sourceTexture?.Dispose();
                halftoneTexture?.Dispose();
            }

            return PrimitiveResult.Ok(context.Receiver);
        }

        private static void DrawPixel(IGraphicsBackend backend,
            ITexture dest, ITexture? source, ITexture? halftone,
            int destX, int destY, int sourceX, int sourceY,
            int width, int height, GraphicsRect clipRect, BlendMode blendMode)
        {
            backend.BitBlt(new BitBltParams
            {
                Destination = dest,
                DestX = destX,
                DestY = destY,
                Source = source,
                SourceX = sourceX,
                SourceY = sourceY,
                Width = width,
                Height = height,
                ClipRect = clipRect,
                Halftone = halftone,
                BlendMode = blendMode
            });
        }
    }

    // Helper methods

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
            bits = new byte[size * 2];
            heap.CopyIndexedWords(bitsOOP, bits.AsSpan());
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
        if (bitsOOP == nil || bitsOOP.IsSmallInteger)
            return;

        var format = heap.GetObjectFormat(bitsOOP);

        if (format == ObjectFormat.WordIndexed)
            heap.SetIndexedWords(bitsOOP, bits);
        else
            heap.SetIndexedBytes(bitsOOP, bits);
    }

    private static void InvalidateDisplayIfNeeded(
        PrimitiveExecutionContext context,
        SmalltalkOOP destForm,
        int destX, int destY, int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight)
    {
        if (context.Platform == null || context.Metadata == null)
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
                    context.Platform.InvalidateDisplayRegion(
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
