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
            if (context.Arguments.Length != 0)
            {
                Console.WriteLine($"[BitBlt P96] Failed: unexpected arguments (count={context.Arguments.Length})");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (context.Receiver.IsSmallInteger)
            {
                Console.WriteLine($"[BitBlt P96] Failed: receiver is SmallInteger ({context.Receiver})");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // Try hardware-accelerated BitBlt if available
            if (context.Platform != null)
            {
                bool success = context.Platform.PerformBitBlt(context.Receiver);
                if (success)
                {
                    // Hardware BitBlt succeeded - return immediately
                    return PrimitiveResult.Ok(context.Receiver);
                }
                // Hardware BitBlt declined (returned false) - fall through to software implementation
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
                // DEBUG: Log all instance variable values
                Console.WriteLine("[BitBlt P96] Validation failed - All BitBlt ivars:");
                Console.WriteLine($"  [0] destForm: {destForm}");
                Console.WriteLine($"  [1] sourceForm: {sourceForm}");
                Console.WriteLine($"  [2] halftoneForm: {halftoneForm}");
                Console.WriteLine($"  [3] combinationRule: {combinationRuleOOP}{(combinationRuleOOP.IsSmallInteger ? $" = {combinationRuleOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [4] destX: {destXOOP}{(destXOOP.IsSmallInteger ? $" = {destXOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [5] destY: {destYOOP}{(destYOOP.IsSmallInteger ? $" = {destYOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [6] width: {widthOOP}{(widthOOP.IsSmallInteger ? $" = {widthOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [7] height: {heightOOP}{(heightOOP.IsSmallInteger ? $" = {heightOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [8] sourceX: {sourceXOOP}{(sourceXOOP.IsSmallInteger ? $" = {sourceXOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [9] sourceY: {sourceYOOP}{(sourceYOOP.IsSmallInteger ? $" = {sourceYOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [10] clipX: {clipXOOP}{(clipXOOP.IsSmallInteger ? $" = {clipXOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [11] clipY: {clipYOOP}{(clipYOOP.IsSmallInteger ? $" = {clipYOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [12] clipWidth: {clipWidthOOP}{(clipWidthOOP.IsSmallInteger ? $" = {clipWidthOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine($"  [13] clipHeight: {clipHeightOOP}{(clipHeightOOP.IsSmallInteger ? $" = {clipHeightOOP.AsSmallInteger()}" : " (NOT SmallInt)")}");
                Console.WriteLine("Failed fields:");
                if (!combinationRuleOOP.IsSmallInteger) Console.WriteLine($"  combinationRule is not SmallInteger: {combinationRuleOOP}");
                if (!destXOOP.IsSmallInteger) Console.WriteLine($"  destX is not SmallInteger: {destXOOP}");
                if (!destYOOP.IsSmallInteger) Console.WriteLine($"  destY is not SmallInteger: {destYOOP}");
                if (!widthOOP.IsSmallInteger) Console.WriteLine($"  width is not SmallInteger: {widthOOP}");
                if (!heightOOP.IsSmallInteger) Console.WriteLine($"  height is not SmallInteger: {heightOOP}");
                if (!sourceXOOP.IsSmallInteger) Console.WriteLine($"  sourceX is not SmallInteger: {sourceXOOP}");
                if (!sourceYOOP.IsSmallInteger) Console.WriteLine($"  sourceY is not SmallInteger: {sourceYOOP}");
                if (!clipXOOP.IsSmallInteger) Console.WriteLine($"  clipX is not SmallInteger: {clipXOOP}");
                if (!clipYOOP.IsSmallInteger) Console.WriteLine($"  clipY is not SmallInteger: {clipYOOP}");
                if (!clipWidthOOP.IsSmallInteger) Console.WriteLine($"  clipWidth is not SmallInteger: {clipWidthOOP}");
                if (!clipHeightOOP.IsSmallInteger) Console.WriteLine($"  clipHeight is not SmallInteger: {clipHeightOOP}");
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

            // Fail primitive if combination rule is outside 0-15
            // Rules 16+ (like Form paint) require special Smalltalk handling (paintBits)
            if (combinationRule < 0 || combinationRule > 15)
            {
                Console.WriteLine($"[BitBlt P96] Failed: combinationRule {combinationRule} out of range [0-15]");
                return PrimitiveResult.Fail(context.Metadata!);
            }

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
                (CombinationRule)combinationRule);

            // Write modified dest bits back to heap Form bitmap
            WriteFormData(context, destForm, destBits);

            // Invalidate Display region if destination is Display
            if (context.Platform != null && context.Metadata != null)
            {
                try
                {
                    // Get cached Display global
                    var displayOOP = context.Metadata.KnownObjects.Display;

                    // Check if destination Form is the Display
                    if (destForm == displayOOP && displayOOP != context.HeapManager.GetNil())
                    {
                        // Calculate affected rectangle (intersection of dest rect and clip rect)
                        int invalidX = Math.Max(destX, clipX);
                        int invalidY = Math.Max(destY, clipY);
                        int invalidRight = Math.Min(destX + width, clipX + clipWidth);
                        int invalidBottom = Math.Min(destY + height, clipY + clipHeight);
                        int invalidWidth = Math.Max(0, invalidRight - invalidX);
                        int invalidHeight = Math.Max(0, invalidBottom - invalidY);

                        // Invalidate the affected region
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

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    // Shared helper methods for Form data extraction/writing (used by CopyBits and DrawLoop)
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
            // WordArray: bulk copy words as big-endian bytes (10-100x faster)
            bits = new byte[size * 2];
            context.HeapManager.CopyIndexedWords(bitsOOP, bits.AsSpan());
        }
        else
        {
            // ByteArray: bulk copy bytes directly (10-100x faster)
            bits = new byte[size];
            context.HeapManager.CopyIndexedBytes(bitsOOP, bits.AsSpan());
        }

        return (bits, width, height);
    }

    private static void WriteFormData(
        PrimitiveExecutionContext context,
        SmalltalkOOP formOOP,
        Memory<byte> bits)
    {
        if (formOOP == context.HeapManager.GetNil() || formOOP.IsSmallInteger || bits.Length == 0)
            return;

        // Get Form bitmap OOP
        SmalltalkOOP bitsOOP = context.HeapManager.GetInstanceVariable(formOOP, 0);
        if (bitsOOP == context.HeapManager.GetNil() || bitsOOP.IsSmallInteger)
            return;

        // Write bytes back to heap using bulk operations (10-100x faster)
        var format = context.HeapManager.GetObjectFormat(bitsOOP);

        if (format == ObjectFormat.WordIndexed)
        {
            // WordArray: bulk copy big-endian bytes to words
            context.HeapManager.SetIndexedWords(bitsOOP, bits.Span);
        }
        else
        {
            // ByteArray: bulk copy bytes directly
            context.HeapManager.SetIndexedBytes(bitsOOP, bits.Span);
        }
    }

    /// <summary>Primitive 97: snapshot - Save image to disk</summary>
    /// <remarks>
    /// Returns nil when the snapshot was just created (so Smalltalk can quit if requested).
    /// When the image is later restored, execution continues from here but returns
    /// the receiver (non-nil), so the restore path runs instead of quit.
    ///
    /// Smalltalk code:
    ///   justSnapped := self snapshotPrimitive isNil.
    ///   justSnapped &amp; quitIfTrue ifTrue: [self quitPrimitive]
    ///
    /// CRITICAL: During primitive execution, Process.suspendedContext is stale/nil.
    /// The actual active context is only in C# (ContextManager.ActiveContext).
    /// Before writing the image, we must:
    /// 1. Save current active context to Process.suspendedContext
    /// 2. Push receiver (Smalltalk) onto stack - becomes return value on restore
    /// 3. Write the image
    /// 4. Pop receiver back (for continued execution)
    /// 5. Return nil
    /// </remarks>
    public class Snapshot : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            if (context.Platform == null) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Metadata == null) return PrimitiveResult.Fail(context.Metadata!);
            if (context.StackManager == null) return PrimitiveResult.Fail(context.Metadata!);

            var heap = context.HeapManager;

            // Step 1: Get active process from ProcessorScheduler
            var schedulerOOP = context.Metadata.ProcessorScheduler;
            var activeProcessOOP = heap.GetInstanceVariable(schedulerOOP, VMConstants.PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX);

            // Step 2: Save current active context to Process.suspendedContext
            // During primitive execution, this is nil/stale - the real context is in C#
            var currentContextOOP = context.GetActiveContext();
            heap.SetInstanceVariable(activeProcessOOP, VMConstants.PROCESS_SUSPENDEDCONTEXT_INDEX, currentContextOOP);

            // Step 3: Push receiver (Smalltalk) onto stack
            // This becomes the return value when the image is restored
            context.StackManager.Push(context.Receiver);

            // Step 4: Write the image
            bool success = context.Platform.PerformSnapshot(context.Receiver);

            // Step 5: Pop receiver back (for continued execution after save)
            context.StackManager.Pop();

            if (!success) return PrimitiveResult.Fail(context.Metadata!);

            // Step 6: Return nil to indicate "snapshot was just created"
            // This allows Smalltalk code to call quitPrimitive if quitIfTrue is set
            return PrimitiveResult.Ok(heap.GetNil());
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

    /// <summary>Primitive 102: beDisplay - Make Form the active display (Blue Book Chapter 29)</summary>
    public class BeDisplay : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Read Display dimensions from Form instance variables
            // Form has: bits (0), width (1), height (2), offset (3)
            int width = context.HeapManager.GetInstanceVariable(context.Receiver, 1).AsSmallInteger();
            int height = context.HeapManager.GetInstanceVariable(context.Receiver, 2).AsSmallInteger();

            // Notify platform bridge of new Display dimensions
            // Platform will check if dimensions changed and resize window if needed
            if (context.Platform != null)
            {
                try
                {
                    context.Platform.DisplayResized(width, height);
                }
                catch
                {
                    // Ignore platform errors - display operations are optional
                }
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 101: beCursor - Set hardware cursor (Blue Book Chapter 29)</summary>
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

    /// <summary>Primitive 104: drawLoopX:Y: - Bresenham line drawing for BitBlt</summary>
    public class DrawLoop : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Receiver must be a BitBlt object
            if (context.Receiver.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Arguments: xDelta and yDelta (SmallIntegers)
            if (context.Arguments.Length != 2)
                return PrimitiveResult.Fail(context.Metadata!);

            var xDeltaOOP = context.Arguments[0];
            var yDeltaOOP = context.Arguments[1];

            if (!xDeltaOOP.IsSmallInteger || !yDeltaOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int xDelta = xDeltaOOP.AsSmallInteger();
            int yDelta = yDeltaOOP.AsSmallInteger();

            // Extract BitBlt instance variables (14 fields)
            var destFormOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
            var sourceFormOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            var halftoneFormOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 2);
            var combinationRuleOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 3);
            var destXOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 4);
            var destYOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 5);
            var widthOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 6);
            var heightOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 7);
            var sourceXOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 8);
            var sourceYOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 9);
            var clipXOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 10);
            var clipYOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 11);
            var clipWidthOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 12);
            var clipHeightOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 13);

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

            // Extract Form bitmap data
            var (destBits, destWidth, destHeight) = ExtractFormData(context, destFormOOP);
            var (sourceBits, sourceWidth, sourceHeight) = ExtractFormData(context, sourceFormOOP);
            var (halftoneBits, halftoneWidth, halftoneHeight) = ExtractFormData(context, halftoneFormOOP);

            // Implement Bresenham line drawing algorithm
            int dx = Math.Sign(xDelta);
            int dy = Math.Sign(yDelta);
            int px = Math.Abs(yDelta);
            int py = Math.Abs(xDelta);

            // Draw starting point
            DrawPixel(destBits.Span, destWidth, destHeight, destX, destY,
                     sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
                     width, height, clipX, clipY, clipWidth, clipHeight,
                     halftoneBits.Span, halftoneWidth, halftoneHeight, combinationRule);

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
                    DrawPixel(destBits.Span, destWidth, destHeight, destX, destY,
                             sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
                             width, height, clipX, clipY, clipWidth, clipHeight,
                             halftoneBits.Span, halftoneWidth, halftoneHeight, combinationRule);
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
                    DrawPixel(destBits.Span, destWidth, destHeight, destX, destY,
                             sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
                             width, height, clipX, clipY, clipWidth, clipHeight,
                             halftoneBits.Span, halftoneWidth, halftoneHeight, combinationRule);
                }
            }

            // Write modified dest bits back
            WriteFormData(context, destFormOOP, destBits);

            // Invalidate Display region if destination is Display
            if (context.Platform != null && context.Metadata != null)
            {
                try
                {
                    // Get cached Display global
                    var displayOOP = context.Metadata.KnownObjects.Display;

                    // Check if destination Form is the Display
                    if (destFormOOP == displayOOP && displayOOP != context.HeapManager.GetNil())
                    {
                        // Calculate affected rectangle for the entire line
                        // Bresenham draws from (destX, destY) to (destX+xDelta, destY+yDelta)
                        int startX = destX;
                        int startY = destY;
                        int endX = destX + xDelta;
                        int endY = destY + yDelta;

                        int invalidX = Math.Min(startX, endX);
                        int invalidY = Math.Min(startY, endY);
                        int invalidWidth = Math.Abs(xDelta) + width;
                        int invalidHeight = Math.Abs(yDelta) + height;

                        // Clip to clipping rectangle
                        int clipRight = clipX + clipWidth;
                        int clipBottom = clipY + clipHeight;
                        invalidX = Math.Max(invalidX, clipX);
                        invalidY = Math.Max(invalidY, clipY);
                        int invalidRight = Math.Min(invalidX + invalidWidth, clipRight);
                        int invalidBottom = Math.Min(invalidY + invalidHeight, clipBottom);
                        invalidWidth = Math.Max(0, invalidRight - invalidX);
                        invalidHeight = Math.Max(0, invalidBottom - invalidY);

                        // Invalidate the affected region
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

            return PrimitiveResult.Ok(context.Receiver);
        }

        private static void DrawPixel(Span<byte> destBits, int destWidth, int destHeight, int destX, int destY,
                                      Span<byte> sourceBits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
                                      int width, int height, int clipX, int clipY, int clipWidth, int clipHeight,
                                      Span<byte> halftoneBits, int halftoneWidth, int halftoneHeight, int combinationRule)
        {
            // Handle paint mode (rule 16) - convert to Or (rule 7)
            // Pen uses Form black halftone, which skips erase pass and only does under pass
            if (combinationRule == 16)
            {
                BitBltEngine.CopyBits(
                    destBits, destWidth, destHeight, destX, destY,
                    sourceBits, sourceWidth, sourceHeight, sourceX, sourceY,
                    width, height,
                    clipX, clipY, clipWidth, clipHeight,
                    halftoneBits, halftoneWidth, halftoneHeight,
                    CombinationRule.Or);  // Paint mode → Or (under)
            }
            else
            {
                // Simple rules 0-15: single BitBlt operation
                BitBltEngine.CopyBits(
                    destBits, destWidth, destHeight, destX, destY,
                    sourceBits, sourceWidth, sourceHeight, sourceX, sourceY,
                    width, height,
                    clipX, clipY, clipWidth, clipHeight,
                    halftoneBits, halftoneWidth, halftoneHeight,
                    (CombinationRule)combinationRule);
            }
        }
    }
}
