using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Graphics;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and event handling (primitives 90-95).
/// Based on Blue Book specification for user input and event sampling.
/// These primitives delegate to the IDisplayProvider for actual input state.
/// </summary>
public static class InputPrimitives
{
    private static IDisplayProvider? _displayProvider;

    /// <summary>
    /// Sets the display provider for input operations.
    /// Should be the same provider used by GraphicsPrimitives.
    /// </summary>
    public static void SetDisplayProvider(IDisplayProvider provider)
    {
        _displayProvider = provider;
    }

    /// <summary>
    /// Primitive 90: mousePoint - Get current mouse position.
    /// Returns a Point object representing the current mouse coordinates.
    /// Per Blue Book specification:
    /// - Returns Point with x and y coordinates
    /// - Coordinates are in display coordinate system
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Should be empty.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Point object with mouse coordinates.</returns>
    public static PrimitiveResult PrimitiveMousePoint(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (_displayProvider == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

        // Get mouse position from display provider
        var (mouseX, mouseY) = _displayProvider.GetMousePosition();

        // Look up Point class
        var pointClass = context.GetSpecialObject(13); // Point class is at special object index 13
        if (pointClass.IsNil)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

        // Create a new Point instance (has 2 instance variables: x and y)
        var pointRef = context.NewInstance(pointClass, 0);
        var oop = pointRef.AsObjectPointer();

        // Set x and y instance variables
        context.SetInstanceVariable(oop, 0, SObjectRef.FromSmallInteger(mouseX));
        context.SetInstanceVariable(oop, 1, SObjectRef.FromSmallInteger(mouseY));

        return PrimitiveResult.CreateSuccess(pointRef);
    }

    /// <summary>
    /// Primitive 91: cursorLocPut: - Set cursor position.
    /// Sets the cursor to a specific position on the display.
    /// Per Blue Book specification:
    /// - Argument is a Point with new cursor coordinates
    /// - Updates cursor position (Windows Forms doesn't support setting cursor position easily)
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Single argument: Point with cursor position.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveCursorLocPut(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        var point = arguments[0];
        if (!context.IsHeapObject(point))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Get x and y from Point (instance variables 0 and 1)
        var x = context.GetInstanceVariable(point.AsObjectPointer(), 0);
        var y = context.GetInstanceVariable(point.AsObjectPointer(), 1);

        if (!x.IsSmallInteger || !y.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Note: Windows Forms doesn't easily support setting cursor position
        // This would require Cursor.Position which sets absolute screen coordinates
        // For now, just succeed without actually moving the cursor

        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 92: cursorLink: - Link cursor to an object.
    /// Associates the cursor display with a Form or Cursor object.
    /// Per Blue Book specification:
    /// - Argument is the cursor object (typically a Cursor instance)
    /// - Stores reference for cursor rendering
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Single argument: cursor object.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveCursorLink(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (_displayProvider == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

        var cursorObj = arguments[0];

        // Extract cursor bitmap if it's a Cursor object
        // Cursor has: form, offset instance variables
        // For now, just succeed - full cursor support would extract the 16x16 bitmap

        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 93: inputSemaphore: - Set input event semaphore.
    /// Designates a semaphore to be signaled when input events occur.
    /// Per Blue Book specification:
    /// - Argument is a Semaphore instance
    /// - Semaphore is signaled on mouse/keyboard events
    /// - Used for event-driven input processing
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Single argument: semaphore.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveInputSemaphore(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        var semaphore = arguments[0];

        // Verify argument is a heap object (should be a Semaphore)
        if (!semaphore.IsNil && !context.IsHeapObject(semaphore))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // TODO: Store semaphore and signal it when input events occur
        // This would require the display provider to call back to signal the semaphore

        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 94: sampleInterval: - Set input sampling interval.
    /// Sets the interval (in milliseconds) for polling input devices.
    /// Per Blue Book specification:
    /// - Argument is SmallInteger representing milliseconds
    /// - Controls frequency of input device sampling
    /// - 0 means continuous sampling
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Single argument: interval in milliseconds.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveSampleInterval(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        var interval = arguments[0];
        if (!interval.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);

        // Windows Forms event-driven input doesn't need explicit sampling
        // Just succeed

        return PrimitiveResult.CreateSuccess(receiver);
    }

    /// <summary>
    /// Primitive 95: inputWord - Read raw input event word.
    /// Returns a raw word containing encoded input event data.
    /// Per Blue Book specification:
    /// - Returns SmallInteger with encoded event information
    /// - Bits encode mouse buttons, keyboard state, etc.
    /// - Format: [unused][buttons][modifiers][character code]
    /// - This is a low-level primitive for event processing
    /// </summary>
    /// <param name="receiver">The receiver (typically Sensor).</param>
    /// <param name="arguments">Should be empty.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>SmallInteger with encoded input event.</returns>
    public static PrimitiveResult PrimitiveInputWord(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);

        if (_displayProvider == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);

        // Get next input event from display provider
        int inputWord = _displayProvider.GetInputWord();

        return PrimitiveResult.CreateSuccess(SObjectRef.FromSmallInteger(inputWord));
    }

    /// <summary>
    /// Registers all input primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        table.Register(90, "primitiveMousePoint", PrimitiveMousePoint);
        table.Register(91, "primitiveCursorLocPut", PrimitiveCursorLocPut);
        table.Register(92, "primitiveCursorLink", PrimitiveCursorLink);
        table.Register(93, "primitiveInputSemaphore", PrimitiveInputSemaphore);
        table.Register(94, "primitiveSampleInterval", PrimitiveSampleInterval);
        table.Register(95, "primitiveInputWord", PrimitiveInputWord);
    }
}
