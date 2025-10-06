using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and events (primitives 90-95).
/// Stubs for now - require IDisplayProvider interface.
/// </summary>
public static class InputPrimitives
{
    /// <summary>
    /// Primitive 90: mousePoint - Get current mouse position as Point.
    /// </summary>
    public static SObjectRef? MousePoint(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement when IDisplayProvider is added
        // Should return Point object with x and y coordinates
        return null;
    }

    /// <summary>
    /// Primitive 91: cursorLocPut: - Set cursor position.
    /// </summary>
    public static SObjectRef? CursorLocPut(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
            return null;

        // TODO: Implement cursor positioning
        // Requires: IDisplayProvider, Point extraction
        return null;
    }

    /// <summary>
    /// Primitive 92: cursorLink: - Link cursor to Form.
    /// </summary>
    public static SObjectRef? CursorLink(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
            return null;

        // TODO: Implement cursor linking
        // Requires: IDisplayProvider, Form support
        return null;
    }

    /// <summary>
    /// Primitive 93: inputSemaphore: - Set input event semaphore.
    /// </summary>
    public static SObjectRef? InputSemaphore(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
            return null;

        // TODO: Implement input semaphore registration
        // Requires: Event system, Semaphore support
        return null;
    }

    /// <summary>
    /// Primitive 94: sampleInterval: - Set input sampling interval.
    /// </summary>
    public static SObjectRef? SampleInterval(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 1)
            return null;

        // TODO: Implement sampling interval setting
        return null;
    }

    /// <summary>
    /// Primitive 95: inputWord - Read raw input event word.
    /// </summary>
    public static SObjectRef? InputWord(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement input word reading
        // Requires: IDisplayProvider.GetInputWord()
        return null;
    }
}
