namespace Smalltalk80.Interpreter.Graphics;

/// <summary>
/// Abstraction for display surface and input events.
/// Allows different implementations (WinForms, headless, etc.).
/// </summary>
public interface IDisplayProvider
{
    /// <summary>
    /// Gets direct access to the display buffer.
    /// This is the same buffer that Smalltalk Display object references.
    /// </summary>
    Memory<byte> GetDisplayBuffer();

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Bits per pixel (1 for monochrome).
    /// </summary>
    int BitsPerPixel { get; }

    /// <summary>
    /// Marks a rectangle as dirty and needing refresh.
    /// </summary>
    /// <param name="x">X coordinate of top-left corner.</param>
    /// <param name="y">Y coordinate of top-left corner.</param>
    /// <param name="width">Width of dirty region.</param>
    /// <param name="height">Height of dirty region.</param>
    void InvalidateRect(int x, int y, int width, int height);

    /// <summary>
    /// Marks entire display as dirty and needing refresh.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Shows the display window.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the display window.
    /// </summary>
    void Hide();

    /// <summary>
    /// Processes pending events (message pump).
    /// Returns false when window is closed.
    /// </summary>
    bool ProcessEvents();

    // Input event support (called by primitives)

    /// <summary>
    /// Gets current mouse position in display coordinates.
    /// </summary>
    (int X, int Y) GetMousePosition();

    /// <summary>
    /// Gets current mouse button states.
    /// Bit 0 = left (red), bit 1 = middle (yellow), bit 2 = right (blue).
    /// </summary>
    int GetMouseButtons();

    /// <summary>
    /// Gets the next keyboard input event, or 0 if none pending.
    /// Returns encoded event word with character and modifiers.
    /// </summary>
    int GetInputWord();

    /// <summary>
    /// Sets the cursor bitmap (16x16 monochrome).
    /// </summary>
    /// <param name="cursorBits">16x16 bitmap (32 bytes).</param>
    /// <param name="hotspotX">Hotspot X offset.</param>
    /// <param name="hotspotY">Hotspot Y offset.</param>
    void SetCursor(ReadOnlySpan<byte> cursorBits, int hotspotX, int hotspotY);
}
