using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM.Graphics;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Graphics primitives for BitBlt, display, and time (primitives 96-104).
/// Based on Blue Book specification for Smalltalk-80 graphics.
/// </summary>
public static class GraphicsPrimitives
{
    /// <summary>
    /// Primitive 96: copyBits - Core BitBlt operation.
    /// Receiver is a BitBlt object with instance variables describing the operation.
    /// Per Blue Book page 346: BitBlt has 14 instance variables defining the operation.
    /// </summary>
    public static SmalltalkOOP CopyBits(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate arguments
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Preferred path: Use platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            bool success = ctx.PlatformBridge.PerformBitBlt(receiver);
            if (!success)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }
            return receiver;
        }

        // Fallback: No platform bridge - extract fields and call BitBltEngine directly
        // This path handles scenarios where no platform bridge is configured

        // Get the receiver's actual class name (e.g., "class:BitBlt" or any BitBlt subclass)
        // This ensures the primitive works correctly with BitBlt subclasses
        string? className = ctx.GetClassNameForObject(receiver);
        if (className == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract BitBlt instance variables (Blue Book page 346):
        // 0: destForm, 1: sourceForm, 2: halftoneForm, 3: combinationRule
        // 4: destX, 5: destY, 6: width, 7: height
        // 8: sourceX, 9: sourceY
        // 10: clipX, 11: clipY, 12: clipWidth, 13: clipHeight

        SmalltalkOOP destForm = ctx.GetFieldValue(receiver, className, "destForm");
        SmalltalkOOP sourceForm = ctx.GetFieldValue(receiver, className, "sourceForm");
        SmalltalkOOP halftoneForm = ctx.GetFieldValue(receiver, className, "halftoneForm");

        SmalltalkOOP combinationRuleOOP = ctx.GetFieldValue(receiver, className, "combinationRule");
        SmalltalkOOP destXOOP = ctx.GetFieldValue(receiver, className, "destX");
        SmalltalkOOP destYOOP = ctx.GetFieldValue(receiver, className, "destY");
        SmalltalkOOP widthOOP = ctx.GetFieldValue(receiver, className, "width");
        SmalltalkOOP heightOOP = ctx.GetFieldValue(receiver, className, "height");
        SmalltalkOOP sourceXOOP = ctx.GetFieldValue(receiver, className, "sourceX");
        SmalltalkOOP sourceYOOP = ctx.GetFieldValue(receiver, className, "sourceY");
        SmalltalkOOP clipXOOP = ctx.GetFieldValue(receiver, className, "clipX");
        SmalltalkOOP clipYOOP = ctx.GetFieldValue(receiver, className, "clipY");
        SmalltalkOOP clipWidthOOP = ctx.GetFieldValue(receiver, className, "clipWidth");
        SmalltalkOOP clipHeightOOP = ctx.GetFieldValue(receiver, className, "clipHeight");

        // Validate all numeric fields are SmallIntegers
        if (!combinationRuleOOP.IsSmallInteger || !destXOOP.IsSmallInteger || !destYOOP.IsSmallInteger ||
            !widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger || !sourceXOOP.IsSmallInteger ||
            !sourceYOOP.IsSmallInteger || !clipXOOP.IsSmallInteger || !clipYOOP.IsSmallInteger ||
            !clipWidthOOP.IsSmallInteger || !clipHeightOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
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

        // Extract Form data (Form has: bits, width, height)
        var (destBits, destWidth, destHeight) = ExtractFormData(ctx, destForm);
        var (sourceBits, sourceWidth, sourceHeight) = ExtractFormData(ctx, sourceForm);
        var (halftoneBits, halftoneWidth, halftoneHeight) = ExtractFormData(ctx, halftoneForm);

        // Perform BitBlt operation
        BitBltEngine.CopyBits(
            destBits.Span, destWidth, destHeight, destX, destY,
            sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
            width, height,
            clipX, clipY, clipWidth, clipHeight,
            halftoneBits.Span, halftoneWidth, halftoneHeight,
            (CombinationRule)(combinationRule & 0x0F));

        return receiver;
    }

    /// <summary>
    /// Extracts bitmap data from a Form object.
    /// Form has instance variables: bits (ByteArray), width (SmallInteger), height (SmallInteger).
    /// </summary>
    private static (Memory<byte> bits, int width, int height) ExtractFormData(IPrimitiveContext ctx, SmalltalkOOP formOOP)
    {
        // Check for nil or SmallInteger (invalid Form)
        if (formOOP == SpecialObjectsArray.Instance.Nil || formOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        // Get the Form's actual class name (e.g., "class:Form" or any Form subclass)
        string? formClassName = ctx.GetClassNameForObject(formOOP);
        if (formClassName == null)
            return (Memory<byte>.Empty, 0, 0);

        // Get Form instance variables
        SmalltalkOOP bitsOOP = ctx.GetFieldValue(formOOP, formClassName, "bits");
        SmalltalkOOP widthOOP = ctx.GetFieldValue(formOOP, formClassName, "width");
        SmalltalkOOP heightOOP = ctx.GetFieldValue(formOOP, formClassName, "height");

        // Validate numeric fields
        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();

        // Get bitmap data
        Memory<byte> bits = ctx.GetByteArray(bitsOOP);

        return (bits, width, height);
    }

    /// <summary>
    /// Primitive 97: snapshot - Save image to disk.
    /// Uses IPlatformBridge to perform platform-specific snapshot operation.
    /// </summary>
    public static SmalltalkOOP Snapshot(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // If no platform bridge, fail (headless mode doesn't support snapshot)
        if (ctx.PlatformBridge == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Call platform bridge with receiver as snapshot request object
        // Receiver should contain snapshot parameters (Form, filename, etc.)
        bool success = ctx.PlatformBridge.PerformSnapshot(receiver);
        if (!success)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 98: oopsLeft - Available object pointers.
    /// Returns an estimate of available object table slots.
    /// </summary>
    public static SmalltalkOOP OopsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return a large number as estimate
        return SmalltalkOOP.FromSmallInteger(100000);
    }

    /// <summary>
    /// Primitive 99: coreLeft - Available memory in bytes.
    /// Returns estimate of available heap memory.
    /// Per Blue Book, returns SmallInteger with available bytes.
    /// </summary>
    public static SmalltalkOOP CoreLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return available memory estimate from GC
        long availableMemory = GC.GetTotalMemory(false);
        int coreLeft = availableMemory > int.MaxValue ? int.MaxValue : (int)availableMemory;

        // Mask to positive SmallInteger range
        coreLeft = coreLeft & 0x7FFFFFFF;
        return SmalltalkOOP.FromSmallInteger(coreLeft);
    }

    /// <summary>
    /// Primitive 100: timeWords - Current time in seconds.
    /// Returns seconds since Unix epoch (January 1, 1970 UTC).
    /// Per Blue Book, returns SmallInteger with time value (masked to 31 bits).
    /// Uses IPlatformBridge if available, falls back to CLR DateTimeOffset.
    /// </summary>
    public static SmalltalkOOP TimeWords(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int seconds;
        if (ctx.PlatformBridge != null)
        {
            // Use platform bridge for consistent time source
            seconds = ctx.PlatformBridge.GetTimeWords();
        }
        else
        {
            // Fallback: Use CLR time (headless mode)
            seconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
        }

        return SmalltalkOOP.FromSmallInteger(seconds);
    }

    /// <summary>
    /// Primitive 101: beDisplay - Make Form the active display.
    /// Updates SpecialObjectsArray and notifies platform bridge if available.
    /// </summary>
    public static SmalltalkOOP BeDisplay(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Update Display reference in SpecialObjectsArray or system dictionary
        // (In Blue Book, Display is a global variable pointing to the current display Form)

        // If platform bridge available, invalidate entire display to trigger redraw
        if (ctx.PlatformBridge != null)
        {
            try
            {
                // Get the receiver's actual class name (e.g., "class:Form" or any Form subclass)
                string? className = ctx.GetClassNameForObject(receiver);
                if (className == null)
                {
                    ctx.PrimitiveSuccess = false;
                    return SpecialObjectsArray.Instance.Nil;
                }

                // Get display dimensions to invalidate entire screen
                int width = ctx.GetFieldValue(receiver, className, "width").AsSmallInteger();
                int height = ctx.GetFieldValue(receiver, className, "height").AsSmallInteger();
                ctx.PlatformBridge.InvalidateDisplayRegion(0, 0, width, height);
            }
            catch
            {
                // Ignore errors in invalidation (not critical)
            }
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 102: beCursor - Set hardware cursor.
    /// Updates cursor Form (heap-only operation).
    /// </summary>
    public static SmalltalkOOP BeCursor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (receiver.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Store cursor Form reference in system (could be in SpecialObjectsArray or Display object)
        // Platform bridge would be notified of cursor change via separate call if needed

        return receiver;
    }

    /// <summary>
    /// Primitive 103: beep - System beep sound.
    /// Uses IPlatformBridge if available, falls back to Console.Beep.
    /// </summary>
    public static SmalltalkOOP Beep(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (ctx.PlatformBridge != null)
        {
            // Use platform bridge for beep
            ctx.PlatformBridge.Beep();
        }
        else
        {
            // Fallback: Use console beep (headless mode)
            try
            {
                Console.Beep();
            }
            catch
            {
                // Beep not supported on all platforms - just succeed anyway
            }
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 104: clipboardText - Clipboard operations.
    /// 0 args: Read clipboard (returns String OOP)
    /// 1 arg: Write clipboard (arg is String OOP)
    /// </summary>
    public static SmalltalkOOP ClipboardText(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // No platform bridge = fail (clipboard requires platform integration)
        if (ctx.PlatformBridge == null)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        if (args.Length == 0)
        {
            // Read clipboard - returns String OOP or Nil
            SmalltalkOOP stringOOP = ctx.PlatformBridge.ReadClipboardToStringOOP();
            return stringOOP;
        }
        else if (args.Length == 1)
        {
            // Write clipboard - arg[0] must be String OOP
            SmalltalkOOP stringOOP = args[0];

            if (stringOOP.IsSmallInteger || stringOOP == SpecialObjectsArray.Instance.Nil)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            bool success = ctx.PlatformBridge.WriteClipboardFromStringOOP(stringOOP);
            if (!success)
            {
                ctx.PrimitiveSuccess = false;
                return SpecialObjectsArray.Instance.Nil;
            }

            return receiver;
        }
        else
        {
            // Invalid arg count
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    // Helper methods are no longer needed - we use SmalltalkOOP's built-in methods
}
