namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// Abstraction for display surface and input events.
/// Allows different implementations (WinForms, headless testing, etc.).
/// </summary>
public interface IDisplayProvider
{
    /// <summary>
    /// Gets direct access to the display buffer (1-bit monochrome bitmap).
    /// This is the same buffer that the Smalltalk Display object references.
    /// Format: Each byte contains 8 pixels, MSB first (bit 7 = leftmost pixel).
    /// Row stride = (Width + 7) / 8 bytes.
    /// </summary>
    Memory<byte> DisplayBuffer { get; }

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Marks a rectangular region as dirty and needing screen refresh.
    /// </summary>
    /// <param name="x">X coordinate of top-left corner.</param>
    /// <param name="y">Y coordinate of top-left corner.</param>
    /// <param name="width">Width of dirty region in pixels.</param>
    /// <param name="height">Height of dirty region in pixels.</param>
    void Invalidate(int x, int y, int width, int height);

    /// <summary>
    /// Gets current mouse position in display coordinates.
    /// </summary>
    /// <returns>Tuple of (X, Y) coordinates.</returns>
    (int X, int Y) GetMousePosition();

    /// <summary>
    /// Gets the next input event word (keyboard/mouse), or 0 if none pending.
    /// Per Blue Book specification, the word encodes:
    /// - Bits 0-7: Character code (or 0 for mouse events)
    /// - Bits 8-10: Mouse button state (bit 0=red/left, 1=yellow/middle, 2=blue/right)
    /// - Bits 11-13: Modifier keys (shift, control, etc.)
    /// - Remaining bits: Timestamp or unused
    /// </summary>
    /// <returns>Encoded input event word, or 0 if no events.</returns>
    int GetInputWord();
}
