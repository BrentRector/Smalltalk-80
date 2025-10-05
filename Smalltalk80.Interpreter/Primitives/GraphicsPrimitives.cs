using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Graphics;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Graphics primitives for display and BitBlt operations (primitives 96-104).
/// Based on Blue Book specification for Smalltalk-80 graphics.
/// </summary>
public static class GraphicsPrimitives
{
    private static IDisplayProvider? _displayProvider;

    /// <summary>
    /// Sets the display provider for graphics operations.
    /// Must be called before using graphics primitives.
    /// </summary>
    public static void SetDisplayProvider(IDisplayProvider provider)
    {
        _displayProvider = provider;
    }

    /// <summary>
    /// Primitive 96: copyBits - The core BitBlt operation.
    /// Receiver is a BitBlt object with instance variables describing the operation.
    /// </summary>
    public static PrimitiveResult PrimitiveCopyBits(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        try
        {
            var bitbltOop = receiver.AsObjectPointer();

            // Extract BitBlt instance variables (see Blue Book page 346)
            // Instance variable indices (0-based):
            // 0: destForm
            // 1: sourceForm
            // 2: halftoneForm
            // 3: combinationRule
            // 4: destX
            // 5: destY
            // 6: width
            // 7: height
            // 8: sourceX
            // 9: sourceY
            // 10: clipX
            // 11: clipY
            // 12: clipWidth
            // 13: clipHeight

            var destForm = context.GetInstanceVariable(bitbltOop, 0);
            var sourceForm = context.GetInstanceVariable(bitbltOop, 1);
            var halftoneForm = context.GetInstanceVariable(bitbltOop, 2);
            var combinationRule = context.GetInstanceVariable(bitbltOop, 3);
            var destX = context.GetInstanceVariable(bitbltOop, 4);
            var destY = context.GetInstanceVariable(bitbltOop, 5);
            var width = context.GetInstanceVariable(bitbltOop, 6);
            var height = context.GetInstanceVariable(bitbltOop, 7);
            var sourceX = context.GetInstanceVariable(bitbltOop, 8);
            var sourceY = context.GetInstanceVariable(bitbltOop, 9);
            var clipX = context.GetInstanceVariable(bitbltOop, 10);
            var clipY = context.GetInstanceVariable(bitbltOop, 11);
            var clipWidth = context.GetInstanceVariable(bitbltOop, 12);
            var clipHeight = context.GetInstanceVariable(bitbltOop, 13);

            // Validate all are SmallIntegers
            if (!combinationRule.IsSmallInteger || !destX.IsSmallInteger || !destY.IsSmallInteger ||
                !width.IsSmallInteger || !height.IsSmallInteger ||
                !sourceX.IsSmallInteger || !sourceY.IsSmallInteger ||
                !clipX.IsSmallInteger || !clipY.IsSmallInteger ||
                !clipWidth.IsSmallInteger || !clipHeight.IsSmallInteger)
            {
                return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
            }

            // Extract Form data (Form has: bits, width, height, offset instance vars)
            var (destBits, destWidth, destHeight) = ExtractFormData(destForm, context);
            var (sourceBits, sourceWidth, sourceHeight) = ExtractFormData(sourceForm, context);

            Memory<byte>? halftoneBits = null;
            int halftoneWidth = 16;
            int halftoneHeight = 16;
            if (!halftoneForm.IsNil && context.IsHeapObject(halftoneForm))
            {
                var (htBits, htWidth, htHeight) = ExtractFormData(halftoneForm, context);
                halftoneBits = htBits;
                halftoneWidth = htWidth;
                halftoneHeight = htHeight;
            }

            // Build BitBlt operation
            var operation = new BitBltOperation
            {
                DestBits = destBits,
                SourceBits = sourceBits,
                HalftoneBits = halftoneBits,
                DestWidth = destWidth,
                DestHeight = destHeight,
                SourceWidth = sourceWidth,
                SourceHeight = sourceHeight,
                HalftoneWidth = halftoneWidth,
                HalftoneHeight = halftoneHeight,
                DestX = destX.AsSmallInteger(),
                DestY = destY.AsSmallInteger(),
                SourceX = sourceX.AsSmallInteger(),
                SourceY = sourceY.AsSmallInteger(),
                Width = width.AsSmallInteger(),
                Height = height.AsSmallInteger(),
                ClipX = clipX.AsSmallInteger(),
                ClipY = clipY.AsSmallInteger(),
                ClipWidth = clipWidth.AsSmallInteger(),
                ClipHeight = clipHeight.AsSmallInteger(),
                Rule = (CombinationRule)(combinationRule.AsSmallInteger() & 0x0F)
            };

            // Execute BitBlt
            BitBltEngine.CopyBits(operation);

            // If destination is Display, invalidate screen region
            if (IsDisplayForm(destForm, context))
            {
                _displayProvider?.InvalidateRect(
                    operation.DestX,
                    operation.DestY,
                    operation.Width,
                    operation.Height);
            }

            return PrimitiveResult.CreateSuccess(receiver);
        }
        catch
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 97: snapshot - Save image to disk.
    /// For now, this is a stub that returns success.
    /// </summary>
    public static PrimitiveResult PrimitiveSnapshot(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement image snapshot/save
        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 98: oopsLeft - Return number of available object pointers.
    /// </summary>
    public static PrimitiveResult PrimitiveOopsLeft(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Return a reasonable estimate
        int oopsLeft = 100000; // Arbitrary large number
        return PrimitiveResult.CreateSuccess(SObjectRef.FromSmallInteger(oopsLeft));
    }

    /// <summary>
    /// Primitive 99: coreLeft - Return number of available bytes.
    /// </summary>
    public static PrimitiveResult PrimitiveCoreLeft(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Return available memory
        long availableMemory = GC.GetTotalMemory(false);
        int coreLeft = availableMemory > int.MaxValue ? int.MaxValue : (int)availableMemory;
        return PrimitiveResult.CreateSuccess(SObjectRef.FromSmallInteger(coreLeft));
    }

    /// <summary>
    /// Primitive 100: timeWords - Return current time as word array.
    /// Returns SmallInteger with seconds since epoch.
    /// </summary>
    public static PrimitiveResult PrimitiveTimeWords(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // Return seconds since Unix epoch
        var seconds = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
        return PrimitiveResult.CreateSuccess(SObjectRef.FromSmallInteger(seconds));
    }

    /// <summary>
    /// Primitive 101: beDisplay - Make a Form the active display.
    /// </summary>
    public static PrimitiveResult PrimitiveBeDisplay(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // TODO: Set the receiver as the Display object
        // For now, just succeed
        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 102: beCursor - Set the hardware cursor.
    /// </summary>
    public static PrimitiveResult PrimitiveBeCursor(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (!context.IsHeapObject(receiver))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);

        // TODO: Extract cursor bitmap and set it
        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 103: beep - Make a beep sound.
    /// </summary>
    public static PrimitiveResult PrimitiveBeep(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        // System beep
        Console.Beep();
        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 104: clipboardText - Get/set clipboard text.
    /// For now, this is a stub.
    /// </summary>
    public static PrimitiveResult PrimitiveClipboardText(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        // TODO: Implement clipboard operations
        return PrimitiveResult.CreateSuccess(receiver);
    }

    // Helper methods

    private static (Memory<byte> bits, int width, int height) ExtractFormData(SObjectRef formRef, IPrimitiveContext context)
    {
        if (formRef.IsNil || !formRef.IsHeapObject)
        {
            // Return empty form
            return (Memory<byte>.Empty, 0, 0);
        }

        var formOop = formRef.AsObjectPointer();

        // Form instance variables (0-based):
        // 0: bits (Bitmap - array of SmallIntegers or byte array)
        // 1: width (SmallInteger)
        // 2: height (SmallInteger)
        // 3: offset (Point - usually ignored)

        var bitsRef = context.GetInstanceVariable(formOop, 0);
        var widthRef = context.GetInstanceVariable(formOop, 1);
        var heightRef = context.GetInstanceVariable(formOop, 2);

        if (!widthRef.IsSmallInteger || !heightRef.IsSmallInteger)
        {
            return (Memory<byte>.Empty, 0, 0);
        }

        int width = widthRef.AsSmallInteger();
        int height = heightRef.AsSmallInteger();

        // Get bitmap data
        Memory<byte> bits = ExtractBitmapData(bitsRef, width, height, context);

        return (bits, width, height);
    }

    private static Memory<byte> ExtractBitmapData(SObjectRef bitsRef, int width, int height, IPrimitiveContext context)
    {
        if (bitsRef.IsNil)
            return Memory<byte>.Empty;

        if (!bitsRef.IsHeapObject)
            return Memory<byte>.Empty;

        var bitsOop = bitsRef.AsObjectPointer();

        // Check if it's a byte array (most common for display)
        var indexedSize = context.GetIndexedSize(bitsOop);
        if (indexedSize > 0)
        {
            // It's an indexed object - try to get byte data
            var data = context.GetIndexedBytes(bitsOop);
            if (data != null)
                return data.Value;
        }

        // Fallback: allocate empty buffer
        int bytesNeeded = (width * height + 7) / 8;
        return new byte[bytesNeeded];
    }

    private static bool IsDisplayForm(SObjectRef formRef, IPrimitiveContext context)
    {
        // Check if this Form is the Display object
        var displayRef = context.LookupGlobal("Display");
        if (!displayRef.HasValue)
            return false;

        return SObjectRef.AreIdentical(formRef, displayRef.Value);
    }

    /// <summary>
    /// Registers all graphics primitives in the primitive table.
    /// </summary>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(96, "primitiveCopyBits", PrimitiveCopyBits);
        table.Register(97, "primitiveSnapshot", PrimitiveSnapshot);
        table.Register(98, "primitiveOopsLeft", PrimitiveOopsLeft);
        table.Register(99, "primitiveCoreLeft", PrimitiveCoreLeft);
        table.Register(100, "primitiveTimeWords", PrimitiveTimeWords);
        table.Register(101, "primitiveBeDisplay", PrimitiveBeDisplay);
        table.Register(102, "primitiveBeCursor", PrimitiveBeCursor);
        table.Register(103, "primitiveBeep", PrimitiveBeep);
        table.Register(104, "primitiveClipboardText", PrimitiveClipboardText);
    }
}
