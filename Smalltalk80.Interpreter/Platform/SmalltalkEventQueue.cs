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
/// Uses a COUNTING SEMAPHORE for 1:1 event correspondence:
/// - Each Enqueue() increments the semaphore count
/// - Each WaitAny return decrements the count
/// - VM signals InputSemaphore once per event
/// - InputProcess processes one event's words, then waits
///
/// Smalltalk-80 input word format:
/// - Bits 0-11: parameter value
/// - Bits 12-15: event type
///   - Type 1: Mouse X (param = X coordinate)
///   - Type 2: Mouse Y (param = Y coordinate)
///   - Type 3: Key down (param = key code)
///   - Type 4: Key up (param = key code)
///   - Type 6: End of event (sentinel - signals end of one event's words)
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
    private const int TypeEndOfEvent = 6 << 12; // 0x6000 - Sentinel marking end of one event

    // Mouse button key codes (matching Smalltalk BitMin..BitMax)
    private const int ButtonCodeRed = 128;     // 8r200 - Left button
    private const int ButtonCodeYellow = 129;  // 8r201 - Middle button
    private const int ButtonCodeBlue = 130;    // 8r202 - Right button

    private readonly ConcurrentQueue<SmalltalkEvent> _events;
    private readonly ConcurrentQueue<int> _inputWords;  // Processed input words ready for Smalltalk
    private readonly object _mouseLock = new object();

    // Counting semaphore for 1:1 event signaling
    // Each Enqueue increments, each WaitAny return decrements
    private readonly Semaphore _eventSemaphore;
    private bool _disposed = false;

    // Current state (for polling via primitive 90)
    private int _currentX;
    private int _currentY;
    private bool _currentRedButton;
    private bool _currentYellowButton;
    private bool _currentBlueButton;

    // Previous state (for detecting transitions at enqueue time)
    private bool _lastRedButton;
    private bool _lastYellowButton;
    private bool _lastBlueButton;

    // Previous state (for detecting transitions during ProcessOneEvent)
    private bool _prevRedButton;
    private bool _prevYellowButton;
    private bool _prevBlueButton;

    // Flag set when button transition detected at enqueue time
    // VM checks and clears this to signal ControllerSemaphore
    private volatile bool _buttonTransitionPending;

    public SmalltalkEventQueue()
    {
        _events = new ConcurrentQueue<SmalltalkEvent>();
        _inputWords = new ConcurrentQueue<int>();
        // Counting semaphore: initial count 0, max count allows many events
        _eventSemaphore = new Semaphore(0, int.MaxValue);
    }

    /// <summary>
    /// Returns true if a button transition occurred since last check, then clears the flag.
    /// Thread-safe (uses volatile read and Interlocked.Exchange).
    /// Called by VM to decide whether to signal ControllerSemaphore.
    /// </summary>
    public bool ConsumeButtonTransition()
    {
        if (!_buttonTransitionPending)
            return false;
        _buttonTransitionPending = false;
        return true;
    }

    /// <summary>
    /// Enqueue an event from the WinForms thread.
    /// Thread-safe (can be called from any thread).
    /// Increments counting semaphore for 1:1 event correspondence.
    /// Detects button transitions and sets flag for ControllerSemaphore signaling.
    /// </summary>
    public void Enqueue(SmalltalkEvent evt)
    {
        _events.Enqueue(evt);

        // Update current state for polling and detect button transitions
        if (evt.Type == SmalltalkEventType.Mouse)
        {
            lock (_mouseLock)
            {
                // Detect button transitions before updating state
                bool buttonChanged =
                    (evt.RedButton != _lastRedButton) ||
                    (evt.YellowButton != _lastYellowButton) ||
                    (evt.BlueButton != _lastBlueButton);

                if (buttonChanged)
                {
                    _buttonTransitionPending = true;
                }

                // Update state
                _currentX = evt.X;
                _currentY = evt.Y;
                _currentRedButton = evt.RedButton;
                _currentYellowButton = evt.YellowButton;
                _currentBlueButton = evt.BlueButton;

                // Track last button state for next transition detection
                _lastRedButton = evt.RedButton;
                _lastYellowButton = evt.YellowButton;
                _lastBlueButton = evt.BlueButton;
            }
        }

        // Increment counting semaphore - one signal per event
        _eventSemaphore.Release();
    }

    /// <summary>
    /// Process exactly ONE event from the queue and convert to Smalltalk input words.
    /// Adds an end-of-event sentinel (type 6) after each event's words.
    /// Returns true if an event was processed, false if queue was empty.
    /// </summary>
    private bool ProcessOneEvent()
    {
        if (!_events.TryDequeue(out var evt) || evt == null)
            return false;

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

        // Add end-of-event sentinel so InputProcess knows when to stop
        _inputWords.Enqueue(TypeEndOfEvent);

        return true;
    }

    /// <summary>
    /// Get the next input word for Smalltalk primitive 95.
    /// Returns 0 if no events are available.
    /// Processes ONE event at a time (not all pending events).
    /// </summary>
    public int GetNextInputWord()
    {
        // First, try to get a pending input word
        if (_inputWords.TryDequeue(out int word))
            return word;

        // Process ONE pending event to generate more input words
        if (ProcessOneEvent())
        {
            if (_inputWords.TryDequeue(out word))
                return word;
        }

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
    /// Dequeue the next composite event, returning all data in one call.
    /// Returns null if no events available.
    /// For mouse events: updates button transition state tracking.
    /// Thread-safe.
    /// </summary>
    /// <returns>Tuple of (eventType, x, y, buttonMask, hasButtonTransition, keyCode, isKeyDown) or null if no events.</returns>
    public (int eventType, int x, int y, int buttonMask, bool hasButtonTransition, int keyCode, bool isKeyDown)? DequeueCompositeEvent()
    {
        if (!_events.TryDequeue(out var evt) || evt == null)
            return null;

        if (evt.Type == SmalltalkEventType.Mouse)
        {
            // Compute button mask
            int buttonMask = 0;
            if (evt.RedButton) buttonMask |= 1;      // Bit 0 = red/left
            if (evt.YellowButton) buttonMask |= 2;   // Bit 1 = yellow/middle
            if (evt.BlueButton) buttonMask |= 4;     // Bit 2 = blue/right

            // Detect button transitions
            bool hasTransition =
                (evt.RedButton != _prevRedButton) ||
                (evt.YellowButton != _prevYellowButton) ||
                (evt.BlueButton != _prevBlueButton);

            // Update previous state for next transition detection
            _prevRedButton = evt.RedButton;
            _prevYellowButton = evt.YellowButton;
            _prevBlueButton = evt.BlueButton;

            return (1, evt.X, evt.Y, buttonMask, hasTransition, 0, false);  // eventType=1 for mouse
        }
        else if (evt.Type == SmalltalkEventType.Keyboard)
        {
            return (2, 0, 0, 0, false, evt.KeyCode, evt.IsKeyDown);  // eventType=2 for keyboard
        }

        // System events - just skip
        return (0, 0, 0, 0, false, 0, false);  // eventType=0 for system/unknown
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
    /// Gets the counting semaphore's WaitHandle for use with WaitHandle.WaitAny.
    /// Each Enqueue increments the count; each successful wait decrements it.
    /// This provides 1:1 correspondence between events and signals.
    /// </summary>
    public WaitHandle EventAvailableHandle => _eventSemaphore;

    /// <summary>
    /// Waits for an event to arrive or timeout.
    /// Used to reduce CPU usage when the VM is idle.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds. -1 for infinite.</param>
    /// <returns>True if event arrived, false if timeout.</returns>
    public bool WaitOne(int timeoutMs)
    {
        // If events already pending in word queue, return immediately
        if (!_inputWords.IsEmpty)
            return true;

        // Wait on counting semaphore
        return _eventSemaphore.WaitOne(timeoutMs);
    }

    /// <summary>
    /// No-op for counting semaphore - kept for interface compatibility.
    /// With counting semaphore, signal state is managed automatically.
    /// </summary>
    public void ResetSignalIfEmpty()
    {
        // No-op - counting semaphore manages its own state
    }

    /// <summary>
    /// No-op for counting semaphore - kept for interface compatibility.
    /// With counting semaphore, signal state is managed automatically.
    /// </summary>
    public void ForceResetEventSignal()
    {
        // No-op - counting semaphore manages its own state
    }

    /// <summary>
    /// Disposes of the event semaphore.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _eventSemaphore.Dispose();
            _disposed = true;
        }
    }
}
