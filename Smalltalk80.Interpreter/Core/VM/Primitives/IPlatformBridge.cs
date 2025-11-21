using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Platform bridge interface for primitives that require host platform interaction.
/// This interface allows platform-exempt primitives (graphics, input, clipboard, etc.)
/// to interact with the host environment while maintaining heap-only architecture.
///
/// CRITICAL RULES:
/// - All methods accept/return only SmalltalkOOPs and primitive scalars (int, byte, etc.)
/// - Never return CLR proxy objects to primitives
/// - Request-object pattern: Multi-field operations use heap-resident request objects
/// - All OOPs passed in/out reference heap allocations created via ISmalltalkHeapManager
/// </summary>
public interface IPlatformBridge
{
    // ============================================================================
    // Graphics / BitBlt (Primitives 96, 97, 101, 102)
    // ============================================================================

    /// <summary>
    /// Performs BitBlt (bit block transfer) operation.
    /// Reads BitBlt fields from the heap-resident BitBlt object, performs raster operation,
    /// and writes results back to destination Form in the heap.
    /// </summary>
    /// <param name="bitBltOOP">BitBlt object OOP containing all operation parameters.</param>
    /// <returns>True on success, false on failure.</returns>
    bool PerformBitBlt(SmalltalkOOP bitBltOOP);

    /// <summary>
    /// Performs snapshot (save image) operation.
    /// Reads snapshot request fields from heap object and saves system state to file.
    /// </summary>
    /// <param name="snapshotRequestOOP">Snapshot request object OOP with Form and filename.</param>
    /// <returns>True on success, false on failure.</returns>
    bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP);

    /// <summary>
    /// Marks a display region as needing redraw (invalidation).
    /// Called when display Form is updated to trigger host rendering.
    /// </summary>
    /// <param name="x">X coordinate of region.</param>
    /// <param name="y">Y coordinate of region.</param>
    /// <param name="w">Width of region.</param>
    /// <param name="h">Height of region.</param>
    void InvalidateDisplayRegion(int x, int y, int w, int h);

    // ============================================================================
    // Input / Mouse / Cursor (Primitives 90-95)
    // ============================================================================

    /// <summary>
    /// Gets the current mouse position from host platform.
    /// Returns coordinates as a tuple (x, y).
    /// </summary>
    /// <returns>Tuple of (x, y) mouse coordinates.</returns>
    (int x, int y) GetMousePosition();

    /// <summary>
    /// Sets the cursor position on host platform.
    /// </summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    void SetCursorPosition(int x, int y);

    /// <summary>
    /// Gets the next input event word from host platform.
    /// Returns a packed integer containing event data (type, timestamp, etc.)
    /// or 0 if no event is available.
    /// </summary>
    /// <returns>Input event word (packed integer) or 0 if no events.</returns>
    int GetInputWord();

    /// <summary>
    /// Sets the input sampling interval for event polling.
    /// </summary>
    /// <param name="intervalMs">Sampling interval in milliseconds.</param>
    void SetSampleInterval(int intervalMs);

    /// <summary>
    /// Signals an input semaphore when events are available.
    /// Host platform should call this to wake up the Smalltalk input handler.
    /// </summary>
    /// <param name="semaphoreOOP">Semaphore OOP to signal (heap-resident object).</param>
    void SetInputSemaphore(SmalltalkOOP semaphoreOOP);

    // ============================================================================
    // Clipboard (Primitive 104)
    // ============================================================================

    /// <summary>
    /// Reads text from the host clipboard and returns a String OOP.
    /// Creates a heap-resident String object containing clipboard text.
    /// </summary>
    /// <returns>String OOP with clipboard contents, or Nil if clipboard empty/unavailable.</returns>
    SmalltalkOOP ReadClipboardToStringOOP();

    /// <summary>
    /// Writes text from a heap-resident String object to the host clipboard.
    /// </summary>
    /// <param name="stringOOP">String OOP containing text to write to clipboard.</param>
    /// <returns>True on success, false on failure.</returns>
    bool WriteClipboardFromStringOOP(SmalltalkOOP stringOOP);

    // ============================================================================
    // System / Misc (Primitives 100, 103, 113)
    // ============================================================================

    /// <summary>
    /// Produces an audible beep on the host platform.
    /// </summary>
    void Beep();

    /// <summary>
    /// Quits the Smalltalk system with the given exit code.
    /// </summary>
    /// <param name="exitCode">Exit code for the process (0 = success).</param>
    void Quit(int exitCode);

    /// <summary>
    /// Gets the current time as a word (seconds since epoch, masked to 31 bits).
    /// Used for Smalltalk time primitives.
    /// </summary>
    /// <returns>Time word (seconds masked to 31 bits).</returns>
    int GetTimeWords();
}
