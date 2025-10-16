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

        try
        {
            // Extract BitBlt instance variables (Blue Book page 346):
            // 0: destForm, 1: sourceForm, 2: halftoneForm, 3: combinationRule
            // 4: destX, 5: destY, 6: width, 7: height
            // 8: sourceX, 9: sourceY
            // 10: clipX, 11: clipY, 12: clipWidth, 13: clipHeight

            SmalltalkOOP destForm = ctx.GetFieldValue(receiver, "class:BitBlt", "destForm");
            SmalltalkOOP sourceForm = ctx.GetFieldValue(receiver, "class:BitBlt", "sourceForm");
            SmalltalkOOP halftoneForm = ctx.GetFieldValue(receiver, "class:BitBlt", "halftoneForm");
            int combinationRule = ctx.GetFieldValue(receiver, "class:BitBlt", "combinationRule").AsSmallInteger();
            int destX = ctx.GetFieldValue(receiver, "class:BitBlt", "destX").AsSmallInteger();
            int destY = ctx.GetFieldValue(receiver, "class:BitBlt", "destY").AsSmallInteger();
            int width = ctx.GetFieldValue(receiver, "class:BitBlt", "width").AsSmallInteger();
            int height = ctx.GetFieldValue(receiver, "class:BitBlt", "height").AsSmallInteger();
            int sourceX = ctx.GetFieldValue(receiver, "class:BitBlt", "sourceX").AsSmallInteger();
            int sourceY = ctx.GetFieldValue(receiver, "class:BitBlt", "sourceY").AsSmallInteger();
            int clipX = ctx.GetFieldValue(receiver, "class:BitBlt", "clipX").AsSmallInteger();
            int clipY = ctx.GetFieldValue(receiver, "class:BitBlt", "clipY").AsSmallInteger();
            int clipWidth = ctx.GetFieldValue(receiver, "class:BitBlt", "clipWidth").AsSmallInteger();
            int clipHeight = ctx.GetFieldValue(receiver, "class:BitBlt", "clipHeight").AsSmallInteger();

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

            // TODO: When IDisplayProvider is integrated, invalidate display region:
            // if (IsDisplayForm(destForm))
            //     displayProvider.Invalidate(destX, destY, width, height);

            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
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

        try
        {
            // Get Form instance variables
            SmalltalkOOP bitsOOP = ctx.GetFieldValue(formOOP, "class:Form", "bits");
            int width = ctx.GetFieldValue(formOOP, "class:Form", "width").AsSmallInteger();
            int height = ctx.GetFieldValue(formOOP, "class:Form", "height").AsSmallInteger();

            // Get bitmap data
            Memory<byte> bits = ctx.GetByteArray(bitsOOP);

            return (bits, width, height);
        }
        catch
        {
            return (Memory<byte>.Empty, 0, 0);
        }
    }

    /// <summary>
    /// Primitive 97: snapshot - Save image to disk.
    /// </summary>
    public static SmalltalkOOP Snapshot(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Implement image snapshot/save
        // For now, just succeed
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
    /// </summary>
    public static SmalltalkOOP TimeWords(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Return seconds since Unix epoch, masked to fit in SmallInteger
        var seconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
        return SmalltalkOOP.FromSmallInteger(seconds);
    }

    /// <summary>
    /// Primitive 101: beDisplay - Make Form the active display.
    /// </summary>
    public static SmalltalkOOP BeDisplay(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: When IDisplayProvider is integrated, set receiver as Display object
        // For now, just succeed
        return receiver;
    }

    /// <summary>
    /// Primitive 102: beCursor - Set hardware cursor.
    /// </summary>
    public static SmalltalkOOP BeCursor(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: When IDisplayProvider is integrated, extract cursor bitmap and set it
        // For now, just succeed
        return receiver;
    }

    /// <summary>
    /// Primitive 103: beep - System beep sound.
    /// Per Blue Book: Makes a beep sound.
    /// </summary>
    public static SmalltalkOOP Beep(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        try
        {
            Console.Beep();
            return receiver;
        }
        catch
        {
            // Beep not supported on all platforms - just succeed anyway
            return receiver;
        }
    }

    /// <summary>
    /// Primitive 104: clipboardText - Clipboard operations.
    /// </summary>
    public static SmalltalkOOP ClipboardText(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: Implement clipboard access (requires String creation)
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    // Helper methods are no longer needed - we use SmalltalkOOP's built-in methods
}
