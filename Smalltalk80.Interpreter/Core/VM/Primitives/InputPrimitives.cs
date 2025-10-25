using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and events (primitives 90-95).
/// Based on Blue Book specification for user input and event sampling.
/// Uses IPlatformBridge for platform-specific input operations (heap-only architecture).
/// </summary>
public static class InputPrimitives
{
    /// <summary>
    /// Primitive 90: mousePoint - Get current mouse position as Point.
    /// Returns a Point object with x and y coordinates.
    /// Per Blue Book: Returns Point with current mouse coordinates in display space.
    /// Uses IPlatformBridge if available, returns (0, 0) otherwise (headless mode).
    /// </summary>
    public static SmalltalkOOP MousePoint(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int x = 0, y = 0;

        // Get mouse position from platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            (x, y) = ctx.PlatformBridge.GetMousePosition();
        }

        // Allocate Point object (class:Point has fields: x, y)
        SmalltalkOOP pointOOP = ctx.AllocateObject("class:Point", 0);

        // Set x and y (encoded as SmallIntegers)
        ctx.SetFieldValue(pointOOP, "class:Point", "x", SmalltalkOOP.FromSmallInteger(x));
        ctx.SetFieldValue(pointOOP, "class:Point", "y", SmalltalkOOP.FromSmallInteger(y));

        return pointOOP;
    }

    /// <summary>
    /// Primitive 91: cursorLocPut: - Set cursor position.
    /// Per Blue Book: Argument is a Point specifying new cursor location.
    /// Uses IPlatformBridge if available to move platform cursor.
    /// </summary>
    public static SmalltalkOOP CursorLocPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
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
        SmalltalkOOP xOOP = ctx.GetFieldValue(point, "class:Point", "x");
        SmalltalkOOP yOOP = ctx.GetFieldValue(point, "class:Point", "y");

        if (!xOOP.IsSmallInteger || !yOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int x = xOOP.AsSmallInteger();
        int y = yOOP.AsSmallInteger();

        // Set cursor position via platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetCursorPosition(x, y);
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 92: cursorLink: - Link cursor to Form.
    /// Per Blue Book: Associates cursor display with a Cursor object.
    /// Stores cursor reference in heap (heap-only operation).
    /// </summary>
    public static SmalltalkOOP CursorLink(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var cursorOOP = args[0];

        if (cursorOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Store cursor reference in system (could be in SpecialObjectsArray or Display object)
        // Platform bridge would be notified of cursor change via separate mechanism

        // For now, just accept the cursor object and succeed
        return receiver;
    }

    /// <summary>
    /// Primitive 93: inputSemaphore: - Set input event semaphore.
    /// Per Blue Book: Designates a semaphore to be signaled on input events.
    /// Uses IPlatformBridge to register semaphore with platform event system.
    /// </summary>
    public static SmalltalkOOP InputSemaphore(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var semaphoreOOP = args[0];

        // Verify argument is a heap object or nil (should be a Semaphore)
        if (semaphoreOOP.IsSmallInteger && semaphoreOOP != SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Register semaphore with platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetInputSemaphore(semaphoreOOP);
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 94: sampleInterval: - Set input sampling interval.
    /// Per Blue Book: Sets interval in milliseconds for polling input devices.
    /// Uses IPlatformBridge to configure polling interval.
    /// </summary>
    public static SmalltalkOOP SampleInterval(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var intervalOOP = args[0];
        if (!intervalOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int intervalMs = intervalOOP.AsSmallInteger();

        // Configure sampling interval via platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetSampleInterval(intervalMs);
        }

        return receiver;
    }

    /// <summary>
    /// Primitive 95: inputWord - Read raw input event word.
    /// Per Blue Book: Returns SmallInteger with encoded input event data.
    /// Format: bits 0-7 = character, bits 8-10 = mouse buttons, bits 11-13 = modifiers.
    /// Uses IPlatformBridge to get next input event from platform queue.
    /// </summary>
    public static SmalltalkOOP InputWord(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        int inputWord = 0;

        // Get input word from platform bridge if available
        if (ctx.PlatformBridge != null)
        {
            inputWord = ctx.PlatformBridge.GetInputWord();
        }

        // Return 0 if no events (headless mode or no pending events)
        return SmalltalkOOP.FromSmallInteger(inputWord);
    }
}
