using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and events (primitives 90-95).
/// Based on Blue Book specification for user input and event sampling.
/// Uses IPlatformBridge for platform-specific input operations (heap-only architecture).
/// </summary>
/// <remarks>
/// <para><b>Blue Book Specification:</b></para>
/// <para>"The input/output primitive routines provide Smalltalk with access to the state of the
/// hardware devices. Since the implementation of these routines will be dependent on the structure
/// of the implementing machine, no routines will be given, just a specification of the behavior"
/// (Blue Book page 587, Chapter 29)</para>
///
/// <para><b>Implementation Architecture:</b></para>
/// <list type="bullet">
/// <item>Platform-agnostic design using IPlatformBridge interface</item>
/// <item>Headless mode support - returns sensible defaults when no platform bridge available</item>
/// <item>All heap operations use heap-only access pattern (no cold start data)</item>
/// <item>Mouse, keyboard, and event handling through abstracted platform layer</item>
/// </list>
///
/// <para><b>Primitives Implemented:</b></para>
/// <list type="bullet">
/// <item>90: mousePoint - Get current mouse position</item>
/// <item>91: cursorLocPut: - Set cursor position</item>
/// <item>92: cursorLink: - Link cursor to Form</item>
/// <item>93: inputSemaphore: - Set input event semaphore</item>
/// <item>94: sampleInterval: - Set input sampling interval</item>
/// <item>95: inputWord - Read raw input event word</item>
/// </list>
///
/// <para><b>Conformance:</b> Platform-specific implementation following Blue Book behavioral specification</para>
/// </remarks>
public static class InputPrimitives
{
    // <summary>
    // Primitive 90: mousePoint - Get current mouse position as Point
    // Blue Book: Page 587, Chapter 29
    // </summary>
    // <remarks>
    // <para><b>Blue Book Behavior:</b></para>
    // <para>Returns a Point object containing the current mouse position in display coordinates.</para>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Queries platform bridge for current mouse position</item>
    // <item>Creates Point object with x and y coordinates as SmallIntegers</item>
    // <item>Returns (0, 0) in headless mode (no platform bridge)</item>
    // </list>
    //
    // <para><b>Conformance:</b> Behavioral specification implemented via platform abstraction</para>
    // </remarks>
    public static SmalltalkOOP MousePoint(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveMousePoint
        // Returns current mouse position as a Point object

        // No arguments expected for mousePoint primitive
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Default to origin (0,0) for headless mode
        int x = 0, y = 0;

        // Platform-specific: Query native cursor position via platform bridge
        // Bridge abstracts OS-specific mouse APIs (Windows GetCursorPos, X11, etc.)
        if (ctx.PlatformBridge != null)
        {
            (x, y) = ctx.PlatformBridge.GetMousePosition();
        }

        // Allocate Point instance with 2 instance variables (x, y)
        // Uses heap-only allocation - no cold start data access
        SmalltalkOOP pointOOP = ctx.AllocateObject("class:Point", 0);

        // Set x and y coordinates as SmallInteger values
        // Display coordinates typically range 0-1024 or 0-1920, well within SmallInteger
        ctx.SetFieldValue(pointOOP, "class:Point", "x", SmalltalkOOP.FromSmallInteger(x));
        ctx.SetFieldValue(pointOOP, "class:Point", "y", SmalltalkOOP.FromSmallInteger(y));

        // Return the Point object containing current mouse position
        return pointOOP;
    }

    // <summary>
    // Primitive 91: cursorLocPut: - Set cursor position.
    // Per Blue Book: Argument is a Point specifying new cursor location.
    // Uses IPlatformBridge if available to move platform cursor.
    // </summary>
    public static SmalltalkOOP CursorLocPut(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveCursorLocPut
        // Moves cursor to specified Point location

        // Expect single Point argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Argument must be a Point object, not SmallInteger
        var point = args[0];
        if (point.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract x and y coordinates from Point instance variables
        SmalltalkOOP xOOP = ctx.GetFieldValue(point, "class:Point", "x");
        SmalltalkOOP yOOP = ctx.GetFieldValue(point, "class:Point", "y");

        // Both coordinates must be SmallIntegers
        if (!xOOP.IsSmallInteger || !yOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Convert to platform integers
        int x = xOOP.AsSmallInteger();
        int y = yOOP.AsSmallInteger();

        // Platform-specific: Move native cursor via platform bridge
        // Bridge handles OS-specific cursor positioning (SetCursorPos on Windows, etc.)
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetCursorPosition(x, y);
        }

        // Return receiver (standard for setter primitives)
        return receiver;
    }

    // <summary>
    // Primitive 92: cursorLink: - Link cursor to Form.
    // Per Blue Book: Associates cursor display with a Cursor object.
    // Stores cursor reference in heap (heap-only operation).
    // </summary>
    public static SmalltalkOOP CursorLink(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveCursorLink
        // Links custom cursor bitmap to display cursor

        // Expect single Cursor argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var cursorOOP = args[0];

        // Cursor must be a heap object (Form or Cursor instance)
        if (cursorOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Platform-specific: Would extract bitmap from Cursor object
        // and set as platform cursor via bridge
        // For now, accept the cursor and succeed (stub implementation)

        // Return receiver
        return receiver;
    }

    // <summary>
    // Primitive 93: inputSemaphore: - Set input event semaphore.
    // Per Blue Book: Designates a semaphore to be signaled on input events.
    // Uses IPlatformBridge to register semaphore with platform event system.
    // </summary>
    public static SmalltalkOOP InputSemaphore(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveInputSemaphore
        // Registers semaphore to be signaled when input events arrive

        // Expect single Semaphore argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var semaphoreOOP = args[0];

        // Argument must be Semaphore object or nil (to clear)
        // SmallIntegers not allowed (except nil which isn't SmallInteger)
        if (semaphoreOOP.IsSmallInteger && semaphoreOOP != SpecialObjectsArray.Instance.Nil)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Platform-specific: Register semaphore with event system
        // Platform will signal this semaphore when mouse/keyboard events occur
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetInputSemaphore(semaphoreOOP);
        }

        // Return receiver
        return receiver;
    }

    // <summary>
    // Primitive 94: sampleInterval: - Set input sampling interval.
    // Per Blue Book: Sets interval in milliseconds for polling input devices.
    // Uses IPlatformBridge to configure polling interval.
    // </summary>
    public static SmalltalkOOP SampleInterval(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveSampleInterval
        // Sets millisecond interval for input device polling

        // Expect single SmallInteger argument
        if (args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        var intervalOOP = args[0];

        // Interval must be SmallInteger (milliseconds)
        if (!intervalOOP.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract interval in milliseconds
        int intervalMs = intervalOOP.AsSmallInteger();

        // Platform-specific: Configure polling interval
        // Affects how often platform checks for mouse/keyboard events
        if (ctx.PlatformBridge != null)
        {
            ctx.PlatformBridge.SetSampleInterval(intervalMs);
        }

        // Return receiver
        return receiver;
    }

    // <summary>
    // Primitive 95: inputWord - Read raw input event word.
    // Per Blue Book: Returns SmallInteger with encoded input event data.
    // Format: bits 0-7 = character, bits 8-10 = mouse buttons, bits 11-13 = modifiers.
    // Uses IPlatformBridge to get next input event from platform queue.
    // </summary>
    public static SmalltalkOOP InputWord(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveInputWord
        // Returns encoded input event as SmallInteger

        // No arguments expected
        if (args.Length != 0)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Default to 0 (no events) for headless mode
        int inputWord = 0;

        // Platform-specific: Get next input event from queue
        // Encoding: bits 0-7 = ASCII character
        //          bits 8-10 = mouse buttons (left, middle, right)
        //          bits 11-13 = keyboard modifiers (shift, ctrl, alt)
        if (ctx.PlatformBridge != null)
        {
            inputWord = ctx.PlatformBridge.GetInputWord();
        }

        // Return encoded event word as SmallInteger
        // Returns 0 if no events pending
        return SmalltalkOOP.FromSmallInteger(inputWord);
    }
}
