using Smalltalk80.Interpreter.Core.Values;

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
    public static SObjectRef? CopyBits(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement BitBlt
        // Requires: BitBlt engine, Form extraction, bitmap manipulation
        // Receiver is BitBlt object with destForm, sourceForm, halftoneForm, etc.
        return null;
    }

    /// <summary>
    /// Primitive 97: snapshot - Save image to disk.
    /// </summary>
    public static SObjectRef? Snapshot(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement image saving
        // Requires: Image serialization, file I/O
        return null;
    }

    /// <summary>
    /// Primitive 98: oopsLeft - Available object pointers.
    /// </summary>
    public static SObjectRef? OopsLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement object table query
        // For now, return a large number
        return SObjectRef.FromSmallInteger(100000);
    }

    /// <summary>
    /// Primitive 99: coreLeft - Available memory.
    /// </summary>
    public static SObjectRef? CoreLeft(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement heap memory query
        // For now, return a large number
        return SObjectRef.FromSmallInteger(1000000);
    }

    /// <summary>
    /// Primitive 100: timeWords - Current time in seconds.
    /// </summary>
    public static SObjectRef? TimeWords(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // Return current time as seconds since epoch
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // TODO: Should return as LargeInteger or array of words
        // For now, try SmallInteger if it fits
        if (seconds < (1 << 30))
        {
            return SObjectRef.FromSmallInteger((int)seconds);
        }

        // TODO: Create LargeInteger or word array
        return null;
    }

    /// <summary>
    /// Primitive 101: beDisplay - Make Form the active display.
    /// </summary>
    public static SObjectRef? BeDisplay(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement display setting
        // Requires: IDisplayProvider, Form support
        return null;
    }

    /// <summary>
    /// Primitive 102: beCursor - Set hardware cursor.
    /// </summary>
    public static SObjectRef? BeCursor(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement cursor setting
        // Requires: IDisplayProvider, Form support
        return null;
    }

    /// <summary>
    /// Primitive 103: beep - System beep sound.
    /// </summary>
    public static SObjectRef? Beep(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement system beep
        // Could use Console.Beep() or system sound
        try
        {
            Console.Beep();
            return receiver;
        }
        catch
        {
            // Beep not supported on all platforms
            return receiver;
        }
    }

    /// <summary>
    /// Primitive 104: clipboardText - Clipboard operations.
    /// </summary>
    public static SObjectRef? ClipboardText(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
    {
        if (args.Length != 0)
            return null;

        // TODO: Implement clipboard access
        // Requires: String creation, clipboard API
        return null;
    }
}
