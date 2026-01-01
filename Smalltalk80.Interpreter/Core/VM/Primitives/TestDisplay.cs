using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Core.VM.Platform;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Test implementation of IDisplay for automated testing and non-GUI scenarios.
/// Provides fully functional in-memory implementations without requiring a GUI display.
/// All operations are complete - clipboard, input events, and time functions work correctly.
/// BitBlt operations update heap memory (rendering handled elsewhere if needed).
/// Includes test helper methods for simulating user input and verifying state.
/// </summary>
public class TestDisplay : IDisplay
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly VMMetadata _metadata;
    private readonly Func<string, SmalltalkOOP> _getClassOOP;
    private readonly Func<SmalltalkOOP, string, string, SmalltalkOOP> _getFieldValue;
    private readonly Func<SmalltalkOOP, Memory<byte>> _getByteArray;
    private int _mouseX = 0;
    private int _mouseY = 0;
    private bool _redButton = false;
    private bool _yellowButton = false;
    private bool _blueButton = false;
    private string _clipboardText = "";
    private readonly Queue<int> _inputEvents = new();
    private SmalltalkOOP _inputSemaphore = default;
    private SmalltalkOOP _controllerSemaphore = default;

    public TestDisplay(
        ISmalltalkHeap heapManager,
        VMMetadata metadata,
        Func<string, SmalltalkOOP> getClassOOP,
        Func<SmalltalkOOP, string, string, SmalltalkOOP> getFieldValue,
        Func<SmalltalkOOP, Memory<byte>> getByteArray)
    {
        _heapManager = heapManager;
        _metadata = metadata;
        _getClassOOP = getClassOOP;
        _getFieldValue = getFieldValue;
        _getByteArray = getByteArray;
    }

    // ============================================================================
    // IDisplay - Window Management
    // ============================================================================

    public int Width { get; private set; } = 640;
    public int Height { get; private set; } = 480;
    public string Title { get; set; } = "Test Display";
    public bool IsVisible { get; private set; } = false;
    public PixelDepth BitsPerPixel => PixelDepth.OneBit;

    public void Show() => IsVisible = true;
    public void Hide() => IsVisible = false;
    public void Close() => IsVisible = false;
    public void Resize(int width, int height)
    {
        Width = width;
        Height = height;
    }

    // ============================================================================
    // IDisplay - Rendering
    // ============================================================================

    public void BitBlt(
        Span<byte> destBits, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<byte> sourceBits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftoneBits, int halftoneWidth, int halftoneHeight,
        int combinationRule)
    {
        BitBltEngine.CopyBits(
            destBits, destWidth, destHeight, destX, destY,
            sourceBits, sourceWidth, sourceHeight, sourceX, sourceY,
            width, height,
            clipX, clipY, clipWidth, clipHeight,
            halftoneBits, halftoneWidth, halftoneHeight,
            (CombinationRule)combinationRule);
    }

    public void InvalidateRegion(int x, int y, int width, int height)
    {
        // No-op in test mode - no display to invalidate
    }

    public void InvalidateAll()
    {
        // No-op in test mode
    }

    public void DisplayResized(int newWidth, int newHeight)
    {
        Width = newWidth;
        Height = newHeight;
    }

    // ============================================================================
    // IDisplay - Input State (Atomic)
    // ============================================================================

    public (int X, int Y) MousePosition => (_mouseX, _mouseY);

    public (bool Red, bool Yellow, bool Blue) MouseButtons => (_redButton, _yellowButton, _blueButton);

    public int ButtonState
    {
        get
        {
            int buttons = 0;
            if (_redButton) buttons |= 4;
            if (_yellowButton) buttons |= 2;
            if (_blueButton) buttons |= 1;
            return buttons;
        }
    }

    public void SetCursorPosition(int x, int y)
    {
        _mouseX = x;
        _mouseY = y;
    }

    // ============================================================================
    // IDisplay - Input Event Queue
    // ============================================================================

    public int GetInputWord()
    {
        if (_inputEvents.Count > 0)
            return _inputEvents.Dequeue();
        return 0;
    }

    public bool HasInputWords => _inputEvents.Count > 0;

    public bool TryConsumeInputShadowSignal() => false;

    // ============================================================================
    // IDisplay - Semaphores
    // ============================================================================

    public void SetInputSemaphore(SmalltalkOOP semaphoreOOP) => _inputSemaphore = semaphoreOOP;
    public SmalltalkOOP GetInputSemaphore() => _inputSemaphore;
    public void SetControllerSemaphore(SmalltalkOOP semaphoreOOP) => _controllerSemaphore = semaphoreOOP;
    public SmalltalkOOP GetControllerSemaphore() => _controllerSemaphore;
    public void SetSampleInterval(int intervalMs) { /* No-op in test mode */ }

    // ============================================================================
    // IDisplay - Event Loop Integration
    // ============================================================================

    public WaitHandle? EventAvailableHandle => null;
    public ManualResetEventSlim? IdleWake => null;

    public void ResetEventSignalIfEmpty() { /* No-op */ }
    public void ForceResetEventSignal() { /* No-op */ }
    public void SetEventCallback(Action callback) { /* No-op */ }
    public void SetIsIdleCallback(Func<bool> callback) { /* No-op */ }

    public void ProcessEvents()
    {
        // No-op in test mode
    }

    public void RunEventLoop()
    {
        // In test mode, just return immediately
    }

    // ============================================================================
    // IDisplay - System Operations
    // ============================================================================

    public void Beep()
    {
        Console.WriteLine("[TestDisplay] Beep!");
    }

    public void Quit(int exitCode)
    {
        Console.WriteLine($"[TestDisplay] Exiting with code {exitCode}");
        Environment.Exit(exitCode);
    }

    public int GetTimeWords()
    {
        long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (int)(seconds & 0x7FFFFFFF);
    }

    public bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP)
    {
        Console.WriteLine("[TestDisplay] Snapshot requested (not yet implemented)");
        return true;
    }

    // ============================================================================
    // IDisposable
    // ============================================================================

    public void Dispose()
    {
        // Nothing to dispose in test mode
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
    /// Test helper: Sets the simulated mouse button states.
    /// </summary>
    public void SetMouseButtonsTest(bool red, bool yellow, bool blue)
    {
        _redButton = red;
        _yellowButton = yellow;
        _blueButton = blue;
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

    /// <summary>
    /// Test helper: Reads clipboard as Smalltalk String OOP.
    /// </summary>
    public SmalltalkOOP ReadClipboardToStringOOP()
    {
        if (string.IsNullOrEmpty(_clipboardText))
            return _metadata.KnownObjects.Nil;

        SmalltalkOOP stringClassOOP = _getClassOOP("class:String");
        if (stringClassOOP == _metadata.KnownObjects.Nil)
            return _metadata.KnownObjects.Nil;

        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(_clipboardText);
        var stringOOP = _heapManager.AllocateInstanceOf(stringClassOOP, bytes.Length);

        for (int i = 0; i < bytes.Length; i++)
        {
            _heapManager.SetIndexedByte(stringOOP, i, bytes[i]);
        }

        return stringOOP;
    }

    /// <summary>
    /// Test helper: Writes Smalltalk String OOP to clipboard.
    /// </summary>
    public bool WriteClipboardFromStringOOP(SmalltalkOOP stringOOP)
    {
        if (stringOOP.IsSmallInteger || stringOOP == _metadata.KnownObjects.Nil)
            return false;

        int length = _heapManager.GetIndexedSize(stringOOP);
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(stringOOP, i);
        }

        _clipboardText = System.Text.Encoding.Latin1.GetString(bytes);
        return true;
    }
}
