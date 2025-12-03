using System.Collections.Concurrent;

namespace Smalltalk80.Interpreter.Platform;

/// <summary>
/// Types of events that can be sent from WinForms to Smalltalk.
/// </summary>
public enum SmalltalkEventType
{
    Mouse,
    Keyboard,
    System
}

/// <summary>
/// Interface for event queue idle waiting support.
/// </summary>
public interface IEventWaitHandle
{
    /// <summary>
    /// Waits for an event to arrive or timeout.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds. -1 for infinite.</param>
    /// <returns>True if event arrived, false if timeout.</returns>
    bool WaitOne(int timeoutMs);
}

/// <summary>
/// Represents a UI event from Windows that will be consumed by Smalltalk code.
/// </summary>
public class SmalltalkEvent
{
    public SmalltalkEventType Type { get; set; }

    // Mouse data
    public int X { get; set; }
    public int Y { get; set; }
    public bool RedButton { get; set; }      // Left mouse button
    public bool YellowButton { get; set; }   // Middle mouse button
    public bool BlueButton { get; set; }     // Right mouse button

    // Keyboard data
    public char Character { get; set; }
    public int KeyCode { get; set; }
    public bool IsKeyDown { get; set; }      // true for key down, false for key up

    // Timestamp (milliseconds since system start)
    public long Milliseconds { get; set; }
}

/// <summary>
/// Thread-safe event queue for Windows UI thread -> Smalltalk interpreter thread communication.
/// WinForms thread enqueues high-level events, Smalltalk thread dequeues via primitives 90 and 95.
///
/// Uses ManualResetEventSlim for WaitHandle.WaitAny compatibility:
/// - Signal is SET when events are enqueued
/// - Signal is RESET when queue is confirmed empty
/// - Double-check pattern prevents race conditions between producer and consumer
///
/// Smalltalk-80 input word format:
/// - Bits 0-11: parameter value
/// - Bits 12-15: event type
///   - Type 1: Mouse X (param = X coordinate)
///   - Type 2: Mouse Y (param = Y coordinate)
///   - Type 3: Key down (param = key code)
///   - Type 4: Key up (param = key code)
///
/// Mouse buttons are encoded as key events with codes:
/// - Red/Left = 128 (8r200)
/// - Yellow/Middle = 129 (8r201)
/// - Blue/Right = 130 (8r202)
/// </summary>
public class SmalltalkEventQueue : IEventWaitHandle, IDisposable
{
    // Input word type constants (shifted to bits 12-15)
    private const int TypeMouseX = 1 << 12;    // 0x1000
    private const int TypeMouseY = 2 << 12;    // 0x2000
    private const int TypeKeyDown = 3 << 12;   // 0x3000
    private const int TypeKeyUp = 4 << 12;     // 0x4000

    // Mouse button key codes (matching Smalltalk BitMin..BitMax)
    private const int ButtonCodeRed = 128;     // 8r200 - Left button
    private const int ButtonCodeYellow = 129;  // 8r201 - Middle button
    private const int ButtonCodeBlue = 130;    // 8r202 - Right button

    private readonly ConcurrentQueue<SmalltalkEvent> _events;
    private readonly ConcurrentQueue<int> _inputWords;  // Processed input words ready for Smalltalk
    private readonly object _mouseLock = new object();

    // Event wait handle for idle blocking - ManualResetEventSlim for WaitAny compatibility
    private readonly ManualResetEventSlim _eventSignal = new(false);
    private bool _disposed = false;

    // Current state (for polling via primitive 90)
    private int _currentX;
    private int _currentY;
    private bool _currentRedButton;
    private bool _currentYellowButton;
    private bool _currentBlueButton;

    // Previous state (for detecting transitions)
    private bool _prevRedButton;
    private bool _prevYellowButton;
    private bool _prevBlueButton;

    public SmalltalkEventQueue()
    {
        _events = new ConcurrentQueue<SmalltalkEvent>();
        _inputWords = new ConcurrentQueue<int>();
    }

    /// <summary>
    /// Enqueue an event from the WinForms thread.
    /// Thread-safe (can be called from any thread).
    /// Signals waiting threads that events are available.
    /// </summary>
    public void Enqueue(SmalltalkEvent evt)
    {
        _events.Enqueue(evt);

        // Update current state for polling
        if (evt.Type == SmalltalkEventType.Mouse)
        {
            lock (_mouseLock)
            {
                _currentX = evt.X;
                _currentY = evt.Y;
                _currentRedButton = evt.RedButton;
                _currentYellowButton = evt.YellowButton;
                _currentBlueButton = evt.BlueButton;
            }
        }

        // Signal any waiting threads that events are available
        // ManualResetEventSlim stays set until explicitly reset
        _eventSignal.Set();
    }

    /// <summary>
    /// Process all pending events and convert them to Smalltalk input words.
    /// Called internally when getting next input word.
    /// </summary>
    private void ProcessEvents()
    {
        while (_events.TryDequeue(out var evt))
        {
            if (evt == null) continue;

            if (evt.Type == SmalltalkEventType.Mouse)
            {
                // Generate mouse X event
                int mouseX = Math.Clamp(evt.X, 0, 4095);  // 12-bit max
                _inputWords.Enqueue(TypeMouseX | mouseX);

                // Generate mouse Y event
                int mouseY = Math.Clamp(evt.Y, 0, 4095);  // 12-bit max
                _inputWords.Enqueue(TypeMouseY | mouseY);

                // Generate button transition events
                // Red button
                if (evt.RedButton && !_prevRedButton)
                    _inputWords.Enqueue(TypeKeyDown | ButtonCodeRed);
                else if (!evt.RedButton && _prevRedButton)
                    _inputWords.Enqueue(TypeKeyUp | ButtonCodeRed);

                // Yellow button
                if (evt.YellowButton && !_prevYellowButton)
                    _inputWords.Enqueue(TypeKeyDown | ButtonCodeYellow);
                else if (!evt.YellowButton && _prevYellowButton)
                    _inputWords.Enqueue(TypeKeyUp | ButtonCodeYellow);

                // Blue button
                if (evt.BlueButton && !_prevBlueButton)
                    _inputWords.Enqueue(TypeKeyDown | ButtonCodeBlue);
                else if (!evt.BlueButton && _prevBlueButton)
                    _inputWords.Enqueue(TypeKeyUp | ButtonCodeBlue);

                // Update previous state
                _prevRedButton = evt.RedButton;
                _prevYellowButton = evt.YellowButton;
                _prevBlueButton = evt.BlueButton;
            }
            else if (evt.Type == SmalltalkEventType.Keyboard)
            {
                int keyCode = evt.KeyCode & 0xFFF;  // 12-bit max
                if (evt.IsKeyDown)
                    _inputWords.Enqueue(TypeKeyDown | keyCode);
                else
                    _inputWords.Enqueue(TypeKeyUp | keyCode);
            }
        }
    }

    /// <summary>
    /// Get the next input word for Smalltalk primitive 95.
    /// Returns 0 if no events are available.
    /// </summary>
    public int GetNextInputWord()
    {
        // First, try to get a pending input word
        if (_inputWords.TryDequeue(out int word))
            return word;

        // Process any pending events to generate more input words
        ProcessEvents();

        // Try again after processing
        if (_inputWords.TryDequeue(out word))
            return word;

        return 0;  // No events
    }

    /// <summary>
    /// Try to dequeue an event from the Smalltalk thread.
    /// Called by Primitive 95 (inputWord).
    /// Thread-safe (can be called from any thread).
    /// </summary>
    public bool TryDequeue(out SmalltalkEvent? evt)
    {
        return _events.TryDequeue(out evt);
    }

    /// <summary>
    /// Get the current mouse position.
    /// Called by Primitive 90 (mousePoint).
    /// Thread-safe (can be called from any thread).
    /// </summary>
    public (int x, int y) GetMousePosition()
    {
        lock (_mouseLock)
        {
            return (_currentX, _currentY);
        }
    }

    /// <summary>
    /// Get the current mouse button state.
    /// Thread-safe (can be called from any thread).
    /// </summary>
    public (bool red, bool yellow, bool blue) GetMouseButtons()
    {
        lock (_mouseLock)
        {
            return (_currentRedButton, _currentYellowButton, _currentBlueButton);
        }
    }

    /// <summary>
    /// Check if there are any pending events or input words.
    /// </summary>
    public bool HasPendingEvents => !_events.IsEmpty || !_inputWords.IsEmpty;

    /// <summary>
    /// Get the approximate count of pending events.
    /// Note: This is a snapshot and may change immediately after calling.
    /// </summary>
    public int Count => _events.Count + _inputWords.Count;

    /// <summary>
    /// Gets a WaitHandle that is signaled when events arrive.
    /// Used with WaitHandle.WaitAny for efficient multi-source waiting.
    ///
    /// IMPORTANT: After WaitAny returns for this handle, the caller should:
    /// 1. Process any available events
    /// 2. Call ResetSignalIfEmpty() to properly reset the signal state
    /// </summary>
    public WaitHandle EventAvailableHandle => _eventSignal.WaitHandle;

    /// <summary>
    /// Waits for an event to arrive or timeout.
    /// Used to reduce CPU usage when the VM is idle.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds. -1 for infinite.</param>
    /// <returns>True if event arrived, false if timeout.</returns>
    public bool WaitOne(int timeoutMs)
    {
        // If events already pending, return immediately
        if (HasPendingEvents)
            return true;

        // Reset signal before waiting (in case it's stale)
        // Use the safe pattern to handle race conditions
        ResetSignalIfEmpty();

        // Check again after reset
        if (HasPendingEvents)
            return true;

        // Wait for signal or timeout
        return _eventSignal.Wait(timeoutMs);
    }

    /// <summary>
    /// Safely resets the event signal when the queue is empty.
    /// Uses double-check pattern to handle race conditions with producers.
    ///
    /// Must be called from the single consumer thread (VM thread) after
    /// processing events or after WaitAny returns for this handle.
    ///
    /// Race condition handling:
    /// 1. We reset the signal
    /// 2. If producer added event between our check and reset, the signal
    ///    was Set() after our Reset(), so it's still set - no problem
    /// 3. If producer adds after our reset but before our double-check,
    ///    we detect it and re-signal
    /// 4. If producer adds after our double-check, their Set() is effective
    /// </summary>
    public void ResetSignalIfEmpty()
    {
        // Only reset if queue appears empty
        if (!HasPendingEvents)
        {
            _eventSignal.Reset();

            // Double-check: producer might have added after our check but before reset
            // If so, we need to re-signal
            if (HasPendingEvents)
            {
                _eventSignal.Set();
            }
        }
    }

    /// <summary>
    /// Forces reset of the event signal regardless of queue state.
    /// Used after signaling the input semaphore to prevent busy-loop:
    /// - We've signaled the semaphore once for pending events
    /// - If no process switch occurred (no one waiting), we must block
    /// - Force reset prevents WaitAny from returning immediately
    /// - New events arriving will call Set() again
    /// </summary>
    public void ForceResetEventSignal()
    {
        _eventSignal.Reset();
    }

    /// <summary>
    /// Disposes of the event wait handle.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _eventSignal.Dispose();
            _disposed = true;
        }
    }
}
