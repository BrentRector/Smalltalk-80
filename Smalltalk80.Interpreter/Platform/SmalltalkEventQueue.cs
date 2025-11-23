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

    // Timestamp (milliseconds since system start)
    public long Milliseconds { get; set; }
}

/// <summary>
/// Thread-safe event queue for Windows UI thread -> Smalltalk interpreter thread communication.
/// WinForms thread enqueues events, Smalltalk thread dequeues via primitives 90 and 95.
/// </summary>
public class SmalltalkEventQueue
{
    private readonly ConcurrentQueue<SmalltalkEvent> _events;
    private SmalltalkEvent? _lastMouseState;
    private readonly object _mouseLock = new object();

    public SmalltalkEventQueue()
    {
        _events = new ConcurrentQueue<SmalltalkEvent>();
    }

    /// <summary>
    /// Enqueue an event from the WinForms thread.
    /// Thread-safe (can be called from any thread).
    /// </summary>
    public void Enqueue(SmalltalkEvent evt)
    {
        // Track latest mouse state for Primitive 90 (mousePoint)
        if (evt.Type == SmalltalkEventType.Mouse)
        {
            lock (_mouseLock)
            {
                _lastMouseState = evt;
            }
        }

        _events.Enqueue(evt);
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
            if (_lastMouseState != null)
                return (_lastMouseState.X, _lastMouseState.Y);
            return (0, 0);
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
            if (_lastMouseState != null)
                return (_lastMouseState.RedButton, _lastMouseState.YellowButton, _lastMouseState.BlueButton);
            return (false, false, false);
        }
    }

    /// <summary>
    /// Check if there are any pending events.
    /// </summary>
    public bool HasPendingEvents => !_events.IsEmpty;

    /// <summary>
    /// Get the approximate count of pending events.
    /// Note: This is a snapshot and may change immediately after calling.
    /// </summary>
    public int Count => _events.Count;
}
