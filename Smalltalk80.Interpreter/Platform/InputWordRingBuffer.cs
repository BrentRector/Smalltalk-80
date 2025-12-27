namespace Smalltalk80.Interpreter.Platform;

/// <summary>
/// Thread-safe ring buffer for Blue Book format input words.
///
/// Blue Book Input Word Format:
/// - Bits 0-11: parameter value (12 bits)
/// - Bits 12-15: event type (4 bits)
///   - Type 1: Mouse X (param = X coordinate)
///   - Type 2: Mouse Y (param = Y coordinate)
///   - Type 3: Key down (param = key code)
///   - Type 4: Key up (param = key code)
///
/// Mouse buttons are encoded as key events with codes:
/// - Blue/Right = 128 (8r200)
/// - Yellow/Middle = 129 (8r201)
/// - Red/Left = 130 (8r202)
///
/// Threading Model:
/// - UI thread (producer): calls Enqueue() to add input words
/// - Smalltalk thread (consumer): calls TryDequeue() via primitive 95
/// - Shadow counter: atomically tracks available signals for primitive 86
///
/// The shadow counter mirrors what excessSignals WOULD be in the Blue Book:
/// - Enqueue increments shadow counter (like hardware interrupt incrementing excessSignals)
/// - Primitive 86 (wait) checks shadow counter before normal semaphore wait
/// - If shadow counter > 0, decrements it and returns immediately (no actual wait)
/// - This avoids cross-thread Smalltalk heap access
/// </summary>
public class InputWordRingBuffer
{
    // Input word type constants (shifted to bits 12-15)
    public const int TypeMouseX = 1 << 12;    // 0x1000
    public const int TypeMouseY = 2 << 12;    // 0x2000
    public const int TypeKeyDown = 3 << 12;   // 0x3000
    public const int TypeKeyUp = 4 << 12;     // 0x4000

    // Mouse button key codes
    public const int ButtonCodeBlue = 128;    // Right button
    public const int ButtonCodeYellow = 129;  // Middle button
    public const int ButtonCodeRed = 130;     // Left button

    // Ring buffer - fixed size, power of 2 for efficient modulo
    private readonly int[] _buffer;
    private readonly int _mask;  // For fast modulo: index & _mask

    // Head and tail indices (use Interlocked for thread safety)
    // Head: next position to write (producer)
    // Tail: next position to read (consumer)
    private volatile int _head;
    private volatile int _tail;

    // Shadow counter: mirrors excessSignals behavior
    // Incremented by UI thread on Enqueue
    // Decremented by Smalltalk thread when primitive 86 consumes a signal
    private volatile int _shadowSignalCount;

    // Diagnostic counter for dropped events (buffer overflow)
    // Atomically incremented when Enqueue fails due to full buffer
    private long _droppedEventCount;

    // Callback invoked when words are enqueued (to signal VM's interrupt flag)
    private Action? _eventCallback;

    /// <summary>
    /// Sets the callback to invoke when input words are enqueued.
    /// This callback sets the VM's atomic interrupt flag so the Smalltalk thread
    /// can safely signal the InputSemaphore.
    /// </summary>
    public void SetEventCallback(Action callback)
    {
        _eventCallback = callback;
    }

    /// <summary>
    /// Creates a ring buffer with the specified capacity (rounded up to power of 2).
    /// </summary>
    /// <param name="minCapacity">Minimum capacity (default 1024 words)</param>
    public InputWordRingBuffer(int minCapacity = 1024)
    {
        // Round up to next power of 2
        int capacity = 1;
        while (capacity < minCapacity)
            capacity <<= 1;

        _buffer = new int[capacity];
        _mask = capacity - 1;
        _head = 0;
        _tail = 0;
        _shadowSignalCount = 0;
    }

    /// <summary>
    /// Gets the current shadow signal count.
    /// Used by primitive 86 to check if signals are available without touching Smalltalk heap.
    /// </summary>
    public int ShadowSignalCount => _shadowSignalCount;

    /// <summary>
    /// Gets the total count of dropped events due to buffer overflow.
    /// Useful for diagnostics to detect if input events are being lost.
    /// </summary>
    public long DroppedEventCount => Interlocked.Read(ref _droppedEventCount);

    /// <summary>
    /// Gets whether there are input words available to dequeue.
    /// </summary>
    public bool HasWords => _head != _tail;

    /// <summary>
    /// Gets the number of words currently in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            int h = _head;
            int t = _tail;
            return (h - t) & _mask;
        }
    }

    /// <summary>
    /// Enqueue an input word from the UI thread.
    /// Thread-safe. Increments shadow signal count atomically.
    /// </summary>
    /// <param name="word">Blue Book format input word (type shifted left 12 bits OR param)</param>
    /// <returns>True if enqueued, false if buffer full</returns>
    public bool Enqueue(int word)
    {
        int head = _head;
        int nextHead = (head + 1) & _mask;

        // Check if buffer is full
        if (nextHead == _tail)
        {
            // Buffer full - drop the word (same as original hardware behavior)
            long totalDropped = Interlocked.Increment(ref _droppedEventCount);
            Console.WriteLine($"[InputWordRingBuffer] WARNING: Buffer full, dropping input word (total dropped: {totalDropped})");
            return false;
        }

        // Write word to buffer
        _buffer[head] = word;

        // Memory barrier to ensure word is written before head advances
        Thread.MemoryBarrier();

        // Advance head
        _head = nextHead;

        // Increment shadow signal count atomically
        Interlocked.Increment(ref _shadowSignalCount);

        // Notify VM that events are available (sets atomic interrupt flag)
        _eventCallback?.Invoke();

        return true;
    }

    /// <summary>
    /// Try to dequeue an input word for the Smalltalk thread (primitive 95).
    /// Thread-safe.
    /// NOTE: Does NOT decrement shadow signal count - that's done by primitive 86 (wait).
    /// </summary>
    /// <param name="word">The dequeued word if successful</param>
    /// <returns>True if a word was dequeued, false if buffer empty</returns>
    public bool TryDequeue(out int word)
    {
        int tail = _tail;

        // Check if buffer is empty
        if (tail == _head)
        {
            word = 0;
            return false;
        }

        // Read word from buffer
        word = _buffer[tail];

        // Debug logging disabled - uncomment to trace input events
        // int typeDeq = (word >> 12) & 0xF;
        // if (typeDeq == 3 || typeDeq == 4) // KeyDown or KeyUp
        // {
        //     int param = word & 0xFFF;
        //     string typeStr = typeDeq == 3 ? "KeyDown" : "KeyUp";
        //     Console.WriteLine($"[RingBuffer] Dequeue: {typeStr} param={param} (word=0x{word:X4}) remaining={Count}");
        // }

        // Memory barrier to ensure word is read before tail advances
        Thread.MemoryBarrier();

        // Advance tail
        _tail = (tail + 1) & _mask;

        return true;
    }

    /// <summary>
    /// Try to consume a shadow signal (decrement count if > 0).
    /// Called by primitive 86 (wait) when waiting on InputSemaphore.
    /// Thread-safe via Interlocked.
    /// </summary>
    /// <returns>True if signal was consumed, false if no signals available</returns>
    public bool TryConsumeSignal()
    {
        // Spin until we either consume a signal or see count is 0
        while (true)
        {
            int current = _shadowSignalCount;
            if (current <= 0)
                return false;

            // Try to decrement atomically
            if (Interlocked.CompareExchange(ref _shadowSignalCount, current - 1, current) == current)
                return true;

            // CAS failed - another thread modified count, retry
        }
    }

    /// <summary>
    /// Helper to enqueue a mouse position event (X and Y words).
    /// </summary>
    public void EnqueueMousePosition(int x, int y)
    {
        // Clamp to 12-bit range
        x = Math.Clamp(x, 0, 4095);
        y = Math.Clamp(y, 0, 4095);

        Enqueue(TypeMouseX | x);
        Enqueue(TypeMouseY | y);
    }

    /// <summary>
    /// Helper to enqueue a button down event.
    /// </summary>
    public void EnqueueButtonDown(int buttonCode)
    {
        Enqueue(TypeKeyDown | (buttonCode & 0xFFF));
    }

    /// <summary>
    /// Helper to enqueue a button up event.
    /// </summary>
    public void EnqueueButtonUp(int buttonCode)
    {
        Enqueue(TypeKeyUp | (buttonCode & 0xFFF));
    }

    /// <summary>
    /// Helper to enqueue a key down event.
    /// </summary>
    public void EnqueueKeyDown(int keyCode)
    {
        Enqueue(TypeKeyDown | (keyCode & 0xFFF));
    }

    /// <summary>
    /// Helper to enqueue a key up event.
    /// </summary>
    public void EnqueueKeyUp(int keyCode)
    {
        Enqueue(TypeKeyUp | (keyCode & 0xFFF));
    }
}
