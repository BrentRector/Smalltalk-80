using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and events (primitives 90-95).
/// Based on Blue Book specification for user input and event sampling.
/// </summary>
public static class InputPrimitives
{
    /// <summary>
    /// Primitive 90: mousePoint - Get current mouse position as Point.
    /// Returns a Point object with x and y coordinates.
    /// Per Blue Book: Returns Point with current mouse coordinates in display space.
    /// </summary>
    public static SObjectRef MousePoint(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: When IDisplayProvider is integrated, get actual mouse position:
        // var (x, y) = displayProvider.GetMousePosition();
        // For now, return Point at (0, 0)

        // Allocate Point object (class:Point has fields: x, y)
        int pointId = ctx.AllocateObject("class:Point", 0);

        // Set x and y to 0 (encoded as SmallIntegers)
        ctx.SetFieldValue(pointId, "class:Point", "x", EncodeSmallInteger(0));
        ctx.SetFieldValue(pointId, "class:Point", "y", EncodeSmallInteger(0));

        return SObjectRef.FromObjectPointer(pointId);
    }

    /// <summary>
    /// Primitive 91: cursorLocPut: - Set cursor position.
    /// Per Blue Book: Argument is a Point specifying new cursor location.
    /// </summary>
    public static SObjectRef CursorLocPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var point = args[0];
        if (point.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract x and y from Point object
        int pointId = point.AsObjectPointer();

        try
        {
            int x = ctx.GetFieldValue(pointId, "class:Point", "x");
            int y = ctx.GetFieldValue(pointId, "class:Point", "y");

            // TODO: When IDisplayProvider is integrated, set cursor position:
            // displayProvider.SetCursorPosition(DecodeSmallInteger(x), DecodeSmallInteger(y));
            // For now, just succeed

            return receiver;
        }
        catch
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }
    }

    /// <summary>
    /// Primitive 92: cursorLink: - Link cursor to Form.
    /// Per Blue Book: Associates cursor display with a Cursor object.
    /// </summary>
    public static SObjectRef CursorLink(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: When IDisplayProvider is integrated:
        // Extract cursor bitmap from Form (16x16 monochrome)
        // Call displayProvider.SetCursor(cursorBits, hotspotX, hotspotY)
        // For now, just succeed

        return receiver;
    }

    /// <summary>
    /// Primitive 93: inputSemaphore: - Set input event semaphore.
    /// Per Blue Book: Designates a semaphore to be signaled on input events.
    /// </summary>
    public static SObjectRef InputSemaphore(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var semaphore = args[0];

        // Verify argument is a heap object or nil (should be a Semaphore)
        if (!semaphore.IsSmallInteger && semaphore != SpecialObjectsArray.Instance.Nil)
        {
            // It's a valid heap object
            // TODO: Store semaphore reference and signal it when input events occur
            // Requires: IDisplayProvider callback support
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 94: sampleInterval: - Set input sampling interval.
    /// Per Blue Book: Sets interval in milliseconds for polling input devices.
    /// </summary>
    public static SObjectRef SampleInterval(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var interval = args[0];
        if (!interval.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Modern event-driven input doesn't need explicit sampling interval
        // Just succeed
        return receiver;
    }

    /// <summary>
    /// Primitive 95: inputWord - Read raw input event word.
    /// Per Blue Book: Returns SmallInteger with encoded input event data.
    /// Format: bits 0-7 = character, bits 8-10 = mouse buttons, bits 11-13 = modifiers.
    /// </summary>
    public static SObjectRef InputWord(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // TODO: When IDisplayProvider is integrated, return:
        // int inputWord = displayProvider.GetInputWord();
        // return SObjectRef.FromSmallInteger(inputWord);

        // For now, return 0 (no input events)
        return SObjectRef.FromSmallInteger(0);
    }

    /// <summary>
    /// Helper to encode a C# int as a Smalltalk SmallInteger.
    /// SmallIntegers have LSB set to 1 for tagging.
    /// </summary>
    private static int EncodeSmallInteger(int value) => (value << 1) | 1;

    /// <summary>
    /// Helper to decode a Smalltalk SmallInteger to a C# int.
    /// </summary>
    private static int DecodeSmallInteger(int encoded) => encoded >> 1;
}
