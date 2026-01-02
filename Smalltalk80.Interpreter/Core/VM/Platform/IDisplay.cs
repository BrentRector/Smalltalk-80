// Unified Display Interface
// Combines window management, rendering, and input handling.
// GPU graphics libraries require these to be inseparable.

using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Platform;

/// <summary>
/// Unified display surface with integrated rendering and input.
/// Window, graphics, and input are inseparable in GPU-accelerated systems.
/// </summary>
public interface IDisplay : IDisposable
{
    // ═══════════════════════════════════════════════════════════════════════════
    // WINDOW MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Display width in pixels</summary>
    int Width { get; }

    /// <summary>Display height in pixels</summary>
    int Height { get; }

    /// <summary>Display bits per pixel (1, 8, or 32)</summary>
    PixelDepth BitsPerPixel { get; }

    /// <summary>Window title</summary>
    string Title { get; set; }

    /// <summary>True if window is visible</summary>
    bool IsVisible { get; }

    /// <summary>Show the display window</summary>
    void Show();

    /// <summary>Hide the display window</summary>
    void Hide();

    /// <summary>Close the display window</summary>
    void Close();

    /// <summary>Resize the display to new dimensions</summary>
    void Resize(int width, int height);

    // ═══════════════════════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Perform 32bpp BitBlt raster operation.
    /// Each pixel is a uint in 0xAARRGGBB format.
    /// </summary>
    void BitBlt(
        Span<uint> destPixels, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
        int combinationRule);

    /// <summary>
    /// Mark a rectangular region as needing screen refresh.
    /// </summary>
    void InvalidateRegion(int x, int y, int width, int height);

    /// <summary>
    /// Mark entire display as needing refresh.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Notify display that dimensions may have changed (from Smalltalk beDisplay).
    /// </summary>
    void DisplayResized(int newWidth, int newHeight);

    // ═══════════════════════════════════════════════════════════════════════════
    // INPUT - Atomic State (for polling from Smalltalk thread)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Current mouse position (atomic read, thread-safe).
    /// </summary>
    (int X, int Y) MousePosition { get; }

    /// <summary>
    /// Current mouse button state (atomic read, thread-safe).
    /// Returns (red, yellow, blue) where red=left, yellow=middle, blue=right.
    /// </summary>
    (bool Red, bool Yellow, bool Blue) MouseButtons { get; }

    /// <summary>
    /// Button state as packed integer: red=4, yellow=2, blue=1.
    /// Used by primitive 202 (bitState) for tight polling loops.
    /// </summary>
    int ButtonState { get; }

    /// <summary>
    /// Set cursor position on screen.
    /// </summary>
    void SetCursorPosition(int x, int y);

    // ═══════════════════════════════════════════════════════════════════════════
    // INPUT - Event Queue (Blue Book format)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get next input word from queue (Blue Book format: type &lt;&lt; 12 | param).
    /// Returns 0 if no events available.
    /// </summary>
    int GetInputWord();

    /// <summary>
    /// True if input words are available in the queue.
    /// </summary>
    bool HasInputWords { get; }

    /// <summary>
    /// Try to consume a shadow signal from input buffer.
    /// Used by primitive 86 (wait) for InputSemaphore.
    /// </summary>
    bool TryConsumeInputShadowSignal();

    // ═══════════════════════════════════════════════════════════════════════════
    // INPUT - Semaphores
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Set input semaphore to signal when events arrive.
    /// </summary>
    void SetInputSemaphore(SmalltalkOOP semaphoreOOP);

    /// <summary>
    /// Get the registered input semaphore.
    /// </summary>
    SmalltalkOOP GetInputSemaphore();

    /// <summary>
    /// Set controller semaphore to signal on button transitions.
    /// </summary>
    void SetControllerSemaphore(SmalltalkOOP semaphoreOOP);

    /// <summary>
    /// Get the registered controller semaphore.
    /// </summary>
    SmalltalkOOP GetControllerSemaphore();

    /// <summary>
    /// Set input sampling interval in milliseconds.
    /// </summary>
    void SetSampleInterval(int intervalMs);

    // ═══════════════════════════════════════════════════════════════════════════
    // EVENT LOOP INTEGRATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WaitHandle signaled when input events are available.
    /// Use with WaitHandle.WaitAny for efficient multi-source waiting.
    /// </summary>
    WaitHandle? EventAvailableHandle { get; }

    /// <summary>
    /// ManualResetEventSlim for explicit reset after WaitAny.
    /// </summary>
    ManualResetEventSlim? IdleWake { get; }

    /// <summary>
    /// Reset event signal if queue is empty (double-check pattern).
    /// </summary>
    void ResetEventSignalIfEmpty();

    /// <summary>
    /// Force reset event signal regardless of queue state.
    /// </summary>
    void ForceResetEventSignal();

    /// <summary>
    /// Set callback invoked when events are enqueued (for interrupt flag).
    /// </summary>
    void SetEventCallback(Action callback);

    /// <summary>
    /// Set callback to check if VM is idle (for conditional wake signaling).
    /// </summary>
    void SetIsIdleCallback(Func<bool> callback);

    /// <summary>
    /// Process pending window system events.
    /// Must be called on display's owning thread.
    /// </summary>
    void ProcessEvents();

    /// <summary>
    /// Run display's event loop until Close() is called.
    /// Blocks calling thread. Used by UI thread.
    /// </summary>
    void RunEventLoop();

    // ═══════════════════════════════════════════════════════════════════════════
    // SYSTEM OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Produce audible beep.
    /// </summary>
    void Beep();

    /// <summary>
    /// Quit the application with exit code.
    /// </summary>
    void Quit(int exitCode);

    /// <summary>
    /// Get current time as seconds since epoch (masked to 31 bits).
    /// </summary>
    int GetTimeWords();

    /// <summary>
    /// Perform snapshot (save image) operation.
    /// </summary>
    bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP);
}

/// <summary>
/// Pixel depth for textures and display surfaces.
/// </summary>
public enum PixelDepth
{
    /// <summary>1 bit per pixel, monochrome (original Smalltalk-80)</summary>
    OneBit = 1,

    /// <summary>8 bits per pixel, grayscale or indexed</summary>
    EightBit = 8,

    /// <summary>32 bits per pixel, ARGB format</summary>
    ThirtyTwoBit = 32
}
