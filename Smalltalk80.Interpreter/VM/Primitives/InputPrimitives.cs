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
    public static PrimitiveResult MousePoint(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement when IDisplayProvider is added
        // Should return Point object with x and y coordinates
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 91: cursorLocPut: - Set cursor position.
    /// </summary>
    public static PrimitiveResult CursorLocPut(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        // TODO: Implement cursor positioning
        // Requires: IDisplayProvider, Point extraction
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 92: cursorLink: - Link cursor to Form.
    /// </summary>
    public static PrimitiveResult CursorLink(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        // TODO: Implement cursor linking
        // Requires: IDisplayProvider, Form support
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 93: inputSemaphore: - Set input event semaphore.
    /// </summary>
    public static PrimitiveResult InputSemaphore(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        // TODO: Implement input semaphore registration
        // Requires: Event system, Semaphore support
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 94: sampleInterval: - Set input sampling interval.
    /// </summary>
    public static PrimitiveResult SampleInterval(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 1)
            return PrimitiveResult.Failed();

        // TODO: Implement sampling interval setting
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 95: inputWord - Read raw input event word.
    /// </summary>
    public static PrimitiveResult InputWord(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement input word reading
        // Requires: IDisplayProvider.GetInputWord()
        return PrimitiveResult.Failed();
    }
}
