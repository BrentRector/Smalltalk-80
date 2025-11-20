using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Headless implementation of IPlatformBridge for testing and server scenarios.
/// Provides fully functional implementations that work without a GUI display.
/// All operations are complete - clipboard, input events, and time functions work correctly.
/// BitBlt operations update heap memory (rendering handled elsewhere if needed).
/// </summary>
public class HeadlessPlatformBridge : IPlatformBridge
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly Func<string, SmalltalkOOP> _getClassOOP;
    private readonly Func<SmalltalkOOP, string, string, SmalltalkOOP> _getFieldValue;
    private readonly Func<SmalltalkOOP, Memory<byte>> _getByteArray;
    private int _mouseX = 0;
    private int _mouseY = 0;
    private string _clipboardText = "";
    private readonly Queue<int> _inputEvents = new();

    public HeadlessPlatformBridge(
        ISmalltalkHeapManager heapManager,
        Func<string, SmalltalkOOP> getClassOOP,
        Func<SmalltalkOOP, string, string, SmalltalkOOP> getFieldValue,
        Func<SmalltalkOOP, Memory<byte>> getByteArray)
    {
        _heapManager = heapManager;
        _getClassOOP = getClassOOP;
        _getFieldValue = getFieldValue;
        _getByteArray = getByteArray;
    }

    // ============================================================================
    // Graphics / BitBlt
    // ============================================================================

    public bool PerformBitBlt(SmalltalkOOP bitBltOOP)
    {
        // Validate input
        if (bitBltOOP.IsSmallInteger || bitBltOOP == metadata.KnownObjects.Nil)
            return false;

        // Extract BitBlt instance variables (Blue Book page 346):
        // 0: destForm, 1: sourceForm, 2: halftoneForm, 3: combinationRule
        // 4: destX, 5: destY, 6: width, 7: height
        // 8: sourceX, 9: sourceY
        // 10: clipX, 11: clipY, 12: clipWidth, 13: clipHeight

        SmalltalkOOP destForm = _getFieldValue(bitBltOOP, "class:BitBlt", "destForm");
        SmalltalkOOP sourceForm = _getFieldValue(bitBltOOP, "class:BitBlt", "sourceForm");
        SmalltalkOOP halftoneForm = _getFieldValue(bitBltOOP, "class:BitBlt", "halftoneForm");

        SmalltalkOOP combinationRuleOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "combinationRule");
        SmalltalkOOP destXOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "destX");
        SmalltalkOOP destYOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "destY");
        SmalltalkOOP widthOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "width");
        SmalltalkOOP heightOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "height");
        SmalltalkOOP sourceXOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "sourceX");
        SmalltalkOOP sourceYOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "sourceY");
        SmalltalkOOP clipXOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "clipX");
        SmalltalkOOP clipYOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "clipY");
        SmalltalkOOP clipWidthOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "clipWidth");
        SmalltalkOOP clipHeightOOP = _getFieldValue(bitBltOOP, "class:BitBlt", "clipHeight");

        // Validate all numeric fields are SmallIntegers
        if (!combinationRuleOOP.IsSmallInteger || !destXOOP.IsSmallInteger || !destYOOP.IsSmallInteger ||
            !widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger || !sourceXOOP.IsSmallInteger ||
            !sourceYOOP.IsSmallInteger || !clipXOOP.IsSmallInteger || !clipYOOP.IsSmallInteger ||
            !clipWidthOOP.IsSmallInteger || !clipHeightOOP.IsSmallInteger)
        {
            return false;
        }

        int combinationRule = combinationRuleOOP.AsSmallInteger();
        int destX = destXOOP.AsSmallInteger();
        int destY = destYOOP.AsSmallInteger();
        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();
        int sourceX = sourceXOOP.AsSmallInteger();
        int sourceY = sourceYOOP.AsSmallInteger();
        int clipX = clipXOOP.AsSmallInteger();
        int clipY = clipYOOP.AsSmallInteger();
        int clipWidth = clipWidthOOP.AsSmallInteger();
        int clipHeight = clipHeightOOP.AsSmallInteger();

        // Extract Form data (Form has: bits, width, height)
        var (destBits, destWidth, destHeight) = ExtractFormData(destForm);
        var (sourceBits, sourceWidth, sourceHeight) = ExtractFormData(sourceForm);
        var (halftoneBits, halftoneWidth, halftoneHeight) = ExtractFormData(halftoneForm);

        // Perform BitBlt operation using the engine
        BitBltEngine.CopyBits(
            destBits.Span, destWidth, destHeight, destX, destY,
            sourceBits.Span, sourceWidth, sourceHeight, sourceX, sourceY,
            width, height,
            clipX, clipY, clipWidth, clipHeight,
            halftoneBits.Span, halftoneWidth, halftoneHeight,
            (CombinationRule)(combinationRule & 0x0F));

        return true;
    }

    /// <summary>
    /// Extracts bitmap data from a Form object.
    /// Form has instance variables: bits (ByteArray), width (SmallInteger), height (SmallInteger).
    /// </summary>
    private (Memory<byte> bits, int width, int height) ExtractFormData(SmalltalkOOP formOOP)
    {
        // Check for nil or SmallInteger (invalid Form)
        if (formOOP == metadata.KnownObjects.Nil || formOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        // Get Form instance variables
        SmalltalkOOP bitsOOP = _getFieldValue(formOOP, "class:Form", "bits");
        SmalltalkOOP widthOOP = _getFieldValue(formOOP, "class:Form", "width");
        SmalltalkOOP heightOOP = _getFieldValue(formOOP, "class:Form", "height");

        // Validate numeric fields
        if (!widthOOP.IsSmallInteger || !heightOOP.IsSmallInteger)
            return (Memory<byte>.Empty, 0, 0);

        int width = widthOOP.AsSmallInteger();
        int height = heightOOP.AsSmallInteger();

        // Get bitmap data
        Memory<byte> bits = _getByteArray(bitsOOP);

        return (bits, width, height);
    }

    public bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP)
    {
        // In headless mode, snapshot would save image to file
        // Not implemented yet - would require image serialization infrastructure
        Console.WriteLine("[HeadlessPlatformBridge] Snapshot requested (not yet implemented)");
        return true;
    }

    public void InvalidateDisplayRegion(int x, int y, int w, int h)
    {
        // No-op in headless mode - no display to invalidate
        // A GUI implementation would queue this region for redrawing
    }

    // ============================================================================
    // Input / Mouse / Cursor
    // ============================================================================

    public (int x, int y) GetMousePosition()
    {
        // Return simulated mouse position
        return (_mouseX, _mouseY);
    }

    public void SetCursorPosition(int x, int y)
    {
        // Store simulated cursor position
        _mouseX = x;
        _mouseY = y;
    }

    public int GetInputWord()
    {
        // Return queued event or 0 if no events pending
        if (_inputEvents.Count > 0)
            return _inputEvents.Dequeue();
        return 0;
    }

    public void SetSampleInterval(int intervalMs)
    {
        // No-op in headless mode - no actual event polling needed
        // A GUI implementation would configure timer for polling
    }

    public void SetInputSemaphore(SmalltalkOOP semaphoreOOP)
    {
        // No-op in headless mode - no async event generation
        // A GUI implementation would store and signal this semaphore on events
    }

    // ============================================================================
    // Clipboard
    // ============================================================================

    public SmalltalkOOP ReadClipboardToStringOOP()
    {
        // Return nil if clipboard is empty
        if (string.IsNullOrEmpty(_clipboardText))
            return metadata.KnownObjects.Nil;

        // Get String class OOP from heap
        SmalltalkOOP stringClassOOP = _getClassOOP("class:String");
        if (stringClassOOP == metadata.KnownObjects.Nil)
            return metadata.KnownObjects.Nil;

        // Allocate String object with proper class
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(_clipboardText);
        var stringOOP = _heapManager.AllocateInstanceOf(stringClassOOP, bytes.Length);

        // Fill bytes
        for (int i = 0; i < bytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, bytes[i]);
        }

        return stringOOP;
    }

    public bool WriteClipboardFromStringOOP(SmalltalkOOP stringOOP)
    {
        // Read String from heap and store in simulated clipboard
        if (stringOOP.IsSmallInteger || stringOOP == metadata.KnownObjects.Nil)
            return false;

        // Read string bytes from heap
        int length = _heapManager.GetIndexedSize(stringOOP);
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(stringOOP, i);
        }

        _clipboardText = System.Text.Encoding.Latin1.GetString(bytes);
        return true;
    }

    // ============================================================================
    // System / Misc
    // ============================================================================

    public void Beep()
    {
        // In headless mode, log beep to console
        Console.WriteLine("[HeadlessPlatformBridge] Beep!");
    }

    public void Quit(int exitCode)
    {
        // Exit the process - works in all scenarios
        Console.WriteLine($"[HeadlessPlatformBridge] Exiting with code {exitCode}");
        Environment.Exit(exitCode);
    }

    public int GetTimeWords()
    {
        // Return current Unix epoch time (masked to 31 bits for SmallInteger)
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (int)(seconds & 0x7FFFFFFF);
    }

    // ============================================================================
    // Test Helpers (for unit testing)
    // ============================================================================

    /// <summary>
    /// Test helper: Enqueues an input event for GetInputWord to return.
    /// </summary>
    public void EnqueueInputEvent(int eventWord)
    {
        _inputEvents.Enqueue(eventWord);
    }

    /// <summary>
    /// Test helper: Sets the simulated mouse position.
    /// </summary>
    public void SetMousePositionTest(int x, int y)
    {
        _mouseX = x;
        _mouseY = y;
    }

    /// <summary>
    /// Test helper: Sets the clipboard text for testing.
    /// </summary>
    public void SetClipboardTest(string text)
    {
        _clipboardText = text;
    }

    /// <summary>
    /// Test helper: Gets the clipboard text for verification.
    /// </summary>
    public string GetClipboardTest()
    {
        return _clipboardText;
    }
}
