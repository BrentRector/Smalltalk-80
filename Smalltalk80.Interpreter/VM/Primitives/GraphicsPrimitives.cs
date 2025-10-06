namespace Smalltalk80.Interpreter.VM.Primitives;

/// <summary>
/// Graphics primitives for BitBlt, display, and time (primitives 96-104).
/// Stubs for now - require BitBlt engine and IDisplayProvider.
/// </summary>
public static class GraphicsPrimitives
{
    /// <summary>
    /// Primitive 96: copyBits - Core BitBlt operation.
    /// </summary>
    public static PrimitiveResult CopyBits(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement BitBlt
        // Requires: BitBlt engine, Form extraction, bitmap manipulation
        // Receiver is BitBlt object with destForm, sourceForm, halftoneForm, etc.
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 97: snapshot - Save image to disk.
    /// </summary>
    public static PrimitiveResult Snapshot(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement image saving
        // Requires: Image serialization, file I/O
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 98: oopsLeft - Available object pointers.
    /// </summary>
    public static PrimitiveResult OopsLeft(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement object table query
        // For now, return a large number
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(100000));
    }

    /// <summary>
    /// Primitive 99: coreLeft - Available memory.
    /// </summary>
    public static PrimitiveResult CoreLeft(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement heap memory query
        // For now, return a large number
        return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger(1000000));
    }

    /// <summary>
    /// Primitive 100: timeWords - Current time in seconds.
    /// </summary>
    public static PrimitiveResult TimeWords(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // Return current time as seconds since epoch
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // TODO: Should return as LargeInteger or array of words
        // For now, try SmallInteger if it fits
        if (seconds < (1 << 30))
        {
            return PrimitiveResult.Succeeded(ctx.EncodeSmallInteger((int)seconds));
        }

        // TODO: Create LargeInteger or word array
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 101: beDisplay - Make Form the active display.
    /// </summary>
    public static PrimitiveResult BeDisplay(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement display setting
        // Requires: IDisplayProvider, Form support
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 102: beCursor - Set hardware cursor.
    /// </summary>
    public static PrimitiveResult BeCursor(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement cursor setting
        // Requires: IDisplayProvider, Form support
        return PrimitiveResult.Failed();
    }

    /// <summary>
    /// Primitive 103: beep - System beep sound.
    /// </summary>
    public static PrimitiveResult Beep(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement system beep
        // Could use Console.Beep() or system sound
        try
        {
            Console.Beep();
            return PrimitiveResult.Succeeded(receiver);
        }
        catch
        {
            // Beep not supported on all platforms
            return PrimitiveResult.Succeeded(receiver);
        }
    }

    /// <summary>
    /// Primitive 104: clipboardText - Clipboard operations.
    /// </summary>
    public static PrimitiveResult ClipboardText(IPrimitiveContext ctx, int receiver, int[] args)
    {
        if (args.Length != 0)
            return PrimitiveResult.Failed();

        // TODO: Implement clipboard access
        // Requires: String creation, clipboard API
        return PrimitiveResult.Failed();
    }
}
