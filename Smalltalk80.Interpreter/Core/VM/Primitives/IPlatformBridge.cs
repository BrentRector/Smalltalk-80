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
/// - All OOPs passed in/out reference heap allocations created via ISmalltalkHeap
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

    /// <summary>
    /// Notifies the platform that the Display dimensions have changed.
    /// Called by Primitive 102 (beDisplay) when a new DisplayScreen becomes active
    /// with different dimensions than the previous Display.
    /// The platform should resize its window and reallocate buffers to match.
    /// </summary>
    /// <param name="newWidth">New width of Display in pixels.</param>
    /// <param name="newHeight">New height of Display in pixels.</param>
    void DisplayResized(int newWidth, int newHeight);

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
    /// Gets the current mouse button state from host platform.
    /// Returns a tuple of (red, yellow, blue) button states.
    /// Red = left button, Yellow = middle button, Blue = right button.
    /// </summary>
    /// <returns>Tuple of (red, yellow, blue) button states.</returns>
    (bool red, bool yellow, bool blue) GetMouseButtons();

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
    /// Registers an input semaphore to be signaled when events are available.
    /// Host platform stores this OOP and the VM polls it periodically.
    /// </summary>
    /// <param name="semaphoreOOP">Semaphore OOP to signal (heap-resident object).</param>
    void SetInputSemaphore(SmalltalkOOP semaphoreOOP);

    /// <summary>
    /// Gets the registered input semaphore OOP, or default if none registered.
    /// Used by the VM to signal the semaphore when events arrive.
    /// </summary>
    /// <returns>The input semaphore OOP, or default if none.</returns>
    SmalltalkOOP GetInputSemaphore();

    /// <summary>
    /// Checks if there are pending input events to process.
    /// Used by the VM for idle detection and event signaling.
    /// </summary>
    /// <returns>True if events are pending.</returns>
    bool HasPendingEvents();

    /// <summary>
    /// Blocks the calling thread until an input event arrives or timeout expires.
    /// Used to reduce CPU usage when all Smalltalk processes are idle.
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds. -1 for infinite.</param>
    /// <returns>True if an event arrived, false if timeout expired.</returns>
    bool WaitForEvents(int timeoutMs);

    /// <summary>
    /// Gets a WaitHandle that is signaled when input events are available.
    /// Used with WaitHandle.WaitAny for efficient multi-source waiting.
    /// </summary>
    /// <returns>WaitHandle for event notification, or null if not supported.</returns>
    WaitHandle? EventAvailableHandle { get; }

    /// <summary>
    /// Resets the event signal if the queue is empty.
    /// Must be called from the consumer thread after WaitAny returns for the event handle.
    /// Uses double-check pattern to handle race conditions with producers.
    /// </summary>
    void ResetEventSignalIfEmpty();

    /// <summary>
    /// Forces reset of the event signal regardless of queue state.
    /// Used after signaling the input semaphore to prevent busy-loop when events
    /// are pending but no Smalltalk process is waiting on the semaphore.
    /// New events arriving will re-signal.
    /// </summary>
    void ForceResetEventSignal();

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
