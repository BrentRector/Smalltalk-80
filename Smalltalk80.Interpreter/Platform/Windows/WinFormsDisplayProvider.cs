using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Platform.Windows;

/// <summary>
/// Windows Forms implementation of display provider and platform bridge.
/// Provides a monochrome bitmap display with mouse and keyboard input.
/// Thread-safe: Display updates can occur from Smalltalk thread, rendering occurs on UI thread.
///
/// Input Architecture:
/// - UI thread (WinForms): Enqueues input words to ring buffer, updates current state
/// - Smalltalk thread: Reads input words via primitive 95, reads state via primitives 90/91
/// - InputWordRingBuffer: Thread-safe Blue Book format input word queue with shadow counter
/// </summary>
public class WinFormsDisplayProvider : Form, IPlatformBridge, IDisposable
{
    private byte[] _displayBuffer;
    private Bitmap _backBuffer;
    private readonly object _bufferLock = new();
    private readonly ISmalltalkHeap? _heap;
    private readonly VMMetadata? _vmMetadata;
    private bool _disposed = false;

    // Input semaphore OOP for event notification (signaled on raw input)
    private SmalltalkOOP _inputSemaphore = default;
    // Controller semaphore OOP for button transitions (signaled on button down/up)
    private SmalltalkOOP _controllerSemaphore = default;

    // Thread-safe ring buffer for Blue Book input words
    // UI thread writes, Smalltalk thread reads via primitive 95
    private readonly InputWordRingBuffer _inputWordBuffer = new();

    // Atomic mouse state packed into a single long for true atomic read/write.
    // Emulates hardware registers that the original Blue Book system had.
    // UI thread writes via Interlocked.Exchange, Smalltalk thread reads via Interlocked.Read.
    // Format:
    //   Bits 0-15:   X coordinate (16 bits, supports 0-65535)
    //   Bits 16-31:  Y coordinate (16 bits, supports 0-65535)
    //   Bits 32-34:  Button state (3 bits: blue=bit2, yellow=bit1, red=bit0)
    //   Bits 35-63:  Reserved
    private long _mouseState;

    // Previous button state for detecting transitions in mouse event handlers
    private bool _prevRedButton;
    private bool _prevYellowButton;
    private bool _prevBlueButton;

    // Previous mouse position for throttling - only enqueue when position changes
    private int _prevMouseX = -1;
    private int _prevMouseY = -1;

    // ============================================================================
    // Atomic Mouse State Helpers (Simulated Hardware Registers)
    // ============================================================================

    /// <summary>
    /// Packs mouse X, Y, and button state into a single long for atomic write.
    /// Called by UI thread when mouse state changes.
    /// </summary>
    private static long PackMouseState(int x, int y, int buttons)
    {
        return (long)(x & 0xFFFF) | ((long)(y & 0xFFFF) << 16) | ((long)(buttons & 0x7) << 32);
    }

    /// <summary>
    /// Unpacks mouse state from atomic long read.
    /// Called by Smalltalk thread to get coherent mouse state.
    /// </summary>
    private static (int x, int y, int buttons) UnpackMouseState(long state)
    {
        return (
            (int)(state & 0xFFFF),
            (int)((state >> 16) & 0xFFFF),
            (int)((state >> 32) & 0x7)
        );
    }

    // Idle wake signaling for efficient waiting
    private readonly ManualResetEventSlim _idleWake = new(false);
    private Func<bool>? _isIdleCallback;

    public new int Width { get; private set; }
    public new int Height { get; private set; }

    /// <summary>
    /// Gets direct access to the display buffer (1-bit monochrome bitmap).
    /// Format: Each byte contains 8 pixels, MSB first (bit 7 = leftmost pixel).
    /// Row stride = (Width + 7) / 8 bytes.
    /// Total size = ((Width + 7) / 8) * Height bytes.
    /// </summary>
    public Memory<byte> DisplayBuffer => new Memory<byte>(_displayBuffer);

    public WinFormsDisplayProvider(int width, int height, string title = "Smalltalk-80", ISmalltalkHeap? heap = null, VMMetadata? vmMetadata = null)
    {
        Width = width;
        Height = height;
        _heap = heap;
        _vmMetadata = vmMetadata;

        Console.WriteLine($"[WinForms] Creating window: {width}x{height}");

        // Calculate buffer size: each row needs (width + 7) / 8 bytes
        int rowStride = (width + 7) / 8;
        int bufferSize = rowStride * height;
        _displayBuffer = new byte[bufferSize];

        // Create back buffer for double-buffered rendering
        // Use 32bpp ARGB format (Graphics.FromImage doesn't support indexed formats)
        _backBuffer = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        // Configure form
        Text = title;
        ClientSize = new Size(width, height);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        // Event handlers
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    /// <summary>
    /// Marks entire display as dirty and triggers repaint.
    /// </summary>
    public void InvalidateAll()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(InvalidateAll));
            return;
        }

        Invalidate();
    }

    /// <summary>
    /// Marks a rectangular region as dirty and needing screen refresh.
    /// </summary>
    public void Invalidate(int x, int y, int w, int h)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Invalidate(x, y, w, h)));
            return;
        }

        Invalidate(new Rectangle(x, y, w, h));
    }

    /// <summary>
    /// Paint event handler - copies display buffer to screen.
    /// </summary>
    private void OnPaint(object? sender, PaintEventArgs e)
    {
        lock (_bufferLock)
        {
            // Copy Display bitmap from heap to local buffer
            CopyDisplayFromHeap();

            // Copy display buffer to bitmap
            CopyBufferToBitmap();

            // Draw bitmap to screen
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }
    }

    /// <summary>
    /// Copies the Display bitmap from the Smalltalk heap to the local buffer.
    /// This syncs the heap changes (from BitBlt, etc.) to the renderable buffer.
    /// </summary>
    private void CopyDisplayFromHeap()
    {
        if (_heap == null || _vmMetadata == null)
            return;

        try
        {
            // Look up Display global
            var smalltalkOOP = _vmMetadata.KnownObjects.Smalltalk;
            var displayOOP = LookupGlobal(smalltalkOOP, "Display");
            if (displayOOP == _heap.GetNil())
                return;

            // Get Display bitmap (Form instance variable 0)
            var bitsOOP = _heap.GetInstanceVariable(displayOOP, 0);
            if (bitsOOP == _heap.GetNil())
                return;

            // Read bitmap data from heap
            var format = _heap.GetObjectFormat(bitsOOP);
            int size = _heap.GetIndexedSize(bitsOOP);

            if (format == ObjectFormat.WordIndexed)
            {
                // WordArray: convert words to bytes
                for (int i = 0; i < size && (i * 2 + 1) < _displayBuffer.Length; i++)
                {
                    ushort word = _heap.GetIndexedWord(bitsOOP, i);
                    _displayBuffer[i * 2] = (byte)(word >> 8);
                    _displayBuffer[i * 2 + 1] = (byte)(word & 0xFF);
                }
            }
            else
            {
                // ByteArray: copy bytes directly
                for (int i = 0; i < size && i < _displayBuffer.Length; i++)
                {
                    _displayBuffer[i] = _heap.GetIndexedByte(bitsOOP, i);
                }
            }
        }
        catch
        {
            // Silently ignore errors
        }
    }

    private SmalltalkOOP LookupGlobal(SmalltalkOOP smalltalkDictOOP, string globalName)
    {
        if (_heap == null || _vmMetadata == null)
            return _heap?.GetNil() ?? default;

        int dictSize = _heap.GetIndexedSize(smalltalkDictOOP);
        for (int i = 0; i < dictSize; i++)
        {
            var assocOOP = _heap.GetIndexedOOP(smalltalkDictOOP, i);
            if (assocOOP == _heap.GetNil())
                continue;

            // Get key (Symbol)
            var keyOOP = _heap.GetInstanceVariable(assocOOP, 0); // Association key
            if (keyOOP == _heap.GetNil())
                continue;

            // Extract symbol string
            int symbolSize = _heap.GetIndexedSize(keyOOP);
            var symbolBytes = new byte[symbolSize];
            for (int j = 0; j < symbolSize; j++)
            {
                symbolBytes[j] = _heap.GetIndexedByte(keyOOP, j);
            }
            string symbolText = System.Text.Encoding.UTF8.GetString(symbolBytes);

            if (symbolText == globalName)
            {
                // Return value
                return _heap.GetInstanceVariable(assocOOP, 1); // Association value
            }
        }

        return _heap.GetNil();
    }

    /// <summary>
    /// Copies the Smalltalk display buffer to the Windows bitmap.
    /// Smalltalk format: MSB first (bit 7 = leftmost pixel), 1 = black, 0 = white.
    /// Converts from 1bpp to 32bpp ARGB format.
    /// Optimized to process 8 pixels per byte for 8x speedup.
    /// </summary>
    private void CopyBufferToBitmap()
    {
        BitmapData bmpData = _backBuffer.LockBits(
            new Rectangle(0, 0, Width, Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            unsafe
            {
                int* ptr = (int*)bmpData.Scan0;
                int rowStride = (Width + 7) / 8;
                int stride32 = bmpData.Stride / 4;

                // Precompute color values
                const int black = unchecked((int)0xFF000000);
                const int white = unchecked((int)0xFFFFFFFF);

                for (int y = 0; y < Height; y++)
                {
                    int* rowPtr = ptr + (y * stride32);
                    int bufferOffset = y * rowStride;
                    int x = 0;

                    // Process complete bytes (8 pixels at a time)
                    int completeBytes = Width / 8;
                    for (int b = 0; b < completeBytes; b++)
                    {
                        byte sourceByte = _displayBuffer[bufferOffset + b];

                        // Expand 8 bits to 8 pixels (MSB first)
                        // Unrolled loop for maximum performance
                        rowPtr[x++] = (sourceByte & 0x80) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x40) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x20) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x10) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x08) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x04) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x02) != 0 ? black : white;
                        rowPtr[x++] = (sourceByte & 0x01) != 0 ? black : white;
                    }

                    // Process remaining pixels (if width not multiple of 8)
                    int remainingPixels = Width % 8;
                    if (remainingPixels > 0)
                    {
                        byte sourceByte = _displayBuffer[bufferOffset + completeBytes];
                        for (int bit = 0; bit < remainingPixels; bit++)
                        {
                            int bitIndex = 7 - bit; // MSB first
                            rowPtr[x++] = (sourceByte & (1 << bitIndex)) != 0 ? black : white;
                        }
                    }
                }
            }
        }
        finally
        {
            _backBuffer.UnlockBits(bmpData);
        }
    }

    // ============================================================================
    // Mouse Input
    // ============================================================================

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        Console.WriteLine($"[WinForms] OnMouseDown: button={e.Button} x={e.X} y={e.Y}");

        // For MouseDown, the button that triggered the event IS pressed.
        // Combine current button state with the button being pressed.
        var currentButtons = Control.MouseButtons;

        bool red = currentButtons.HasFlag(MouseButtons.Left) || e.Button == MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(MouseButtons.Middle) || e.Button == MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(MouseButtons.Right) || e.Button == MouseButtons.Right;

        EnqueueMouseEvent(e.X, e.Y, red, yellow, blue);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        Console.WriteLine($"[WinForms] OnMouseUp: button={e.Button} x={e.X} y={e.Y}");

        // For MouseUp, the button that triggered the event is NOT pressed.
        var currentButtons = Control.MouseButtons;

        bool red = currentButtons.HasFlag(MouseButtons.Left) && e.Button != MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(MouseButtons.Middle) && e.Button != MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(MouseButtons.Right) && e.Button != MouseButtons.Right;

        EnqueueMouseEvent(e.X, e.Y, red, yellow, blue);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        // For MouseMove, use current button state as-is
        var currentButtons = Control.MouseButtons;

        EnqueueMouseEvent(e.X, e.Y,
            currentButtons.HasFlag(MouseButtons.Left),
            currentButtons.HasFlag(MouseButtons.Middle),
            currentButtons.HasFlag(MouseButtons.Right));
    }

    /// <summary>
    /// Enqueues mouse event to the ring buffer (for Blue Book primitive 95).
    /// Atomically updates simulated hardware register for primitives 90/202.
    /// Detects button transitions and generates appropriate key up/down words.
    /// </summary>
    private void EnqueueMouseEvent(int x, int y, bool red, bool yellow, bool blue)
    {
        // Pack button state: red=bit0, yellow=bit1, blue=bit2
        // This matches Blue Book encoding where red=4, yellow=2, blue=1 but
        // the bits are shifted for our internal format
        int buttons = 0;
        if (red) buttons |= 4;      // Red = bit 2 (value 4)
        if (yellow) buttons |= 2;   // Yellow = bit 1 (value 2)
        if (blue) buttons |= 1;     // Blue = bit 0 (value 1)

        // Atomically update simulated hardware register (for primitives 90/202)
        Interlocked.Exchange(ref _mouseState, PackMouseState(x, y, buttons));

        // Throttle position events - only enqueue if position actually changed
        // This prevents mouse movement from flooding the buffer and burying button events
        bool positionChanged = (x != _prevMouseX || y != _prevMouseY);
        if (positionChanged)
        {
            _inputWordBuffer.EnqueueMousePosition(x, y);
            _prevMouseX = x;
            _prevMouseY = y;
        }

        // Detect and enqueue button transitions
        // Red button (left mouse = key code 130)
        if (red && !_prevRedButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeRed);
        else if (!red && _prevRedButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeRed);

        // Yellow button (middle mouse = key code 129)
        if (yellow && !_prevYellowButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeYellow);
        else if (!yellow && _prevYellowButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeYellow);

        // Blue button (right mouse = key code 128)
        if (blue && !_prevBlueButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeBlue);
        else if (!blue && _prevBlueButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeBlue);

        // Update previous state
        _prevRedButton = red;
        _prevYellowButton = yellow;
        _prevBlueButton = blue;

        // Signal idle wake if VM is idle
        if (_isIdleCallback?.Invoke() == true)
        {
            _idleWake.Set();
        }
    }

    // ============================================================================
    // Keyboard Input
    // ============================================================================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Enqueue to ring buffer (Blue Book format)
        _inputWordBuffer.EnqueueKeyDown(e.KeyValue);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Enqueue to ring buffer (Blue Book format)
        _inputWordBuffer.EnqueueKeyUp(e.KeyValue);
    }

    // ============================================================================
    // IPlatformBridge Implementation
    // ============================================================================

    public bool PerformBitBlt(SmalltalkOOP bitBltOOP)
    {
        // Return false to use software BitBlt implementation
        // Software BitBlt modifies Display bitmap in heap
        // InvalidateDisplayRegion will be called afterward to trigger redraw
        return false;
    }

    public bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP)
    {
        // Snapshot not yet implemented
        return false;
    }

    public void InvalidateDisplayRegion(int x, int y, int w, int h)
    {
        // Signal WinForms thread to repaint
        // Note: Display buffer is updated by Smalltalk thread via direct memory access
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Invalidate(new Rectangle(x, y, w, h))));
        }
        else
        {
            Invalidate(new Rectangle(x, y, w, h));
        }
    }

    public void DisplayResized(int newWidth, int newHeight)
    {
        // Called from Smalltalk thread when Display dimensions may have changed
        // Must marshal to UI thread for window operations
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => DisplayResized(newWidth, newHeight)));
            return;
        }

        // On UI thread now - check if dimensions actually changed
        if (newWidth == Width && newHeight == Height)
        {
            // Dimensions unchanged - just invalidate the display
            Invalidate();
            return;
        }

        // Dimensions changed - resize window
        Console.WriteLine($"[Display] Resizing from {Width}x{Height} to {newWidth}x{newHeight}");

        // Reallocate display buffers to match new dimensions
        lock (_bufferLock)
        {
            // Update dimensions
            Width = newWidth;
            Height = newHeight;

            // Reallocate display buffer (1 bit per pixel = width × height / 8 bytes)
            int newBufferSize = (newWidth * newHeight) / 8;
            _displayBuffer = new byte[newBufferSize];
            Console.WriteLine($"[Display] Reallocated _displayBuffer: {newBufferSize} bytes");

            // Dispose old back buffer and create new one
            _backBuffer?.Dispose();
            _backBuffer = new Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Console.WriteLine($"[Display] Reallocated _backBuffer: {newWidth}×{newHeight}");
        }

        // Resize window to match new Display dimensions
        this.ClientSize = new Size(newWidth, newHeight);
        Invalidate(); // Repaint entire window
    }

    public (int x, int y) GetMousePosition()
    {
        // Atomically read from simulated hardware register
        long state = Interlocked.Read(ref _mouseState);
        var (x, y, _) = UnpackMouseState(state);
        return (x, y);
    }

    public (bool red, bool yellow, bool blue) GetMouseButtons()
    {
        // Atomically read from simulated hardware register
        long state = Interlocked.Read(ref _mouseState);
        var (_, _, buttons) = UnpackMouseState(state);
        // Blue Book encoding: red=4, yellow=2, blue=1
        return ((buttons & 4) != 0, (buttons & 2) != 0, (buttons & 1) != 0);
    }

    public int GetButtonState()
    {
        // Atomically read just the button state from simulated hardware register.
        // Returns integer with Blue Book encoding: red=4, yellow=2, blue=1.
        // Used by primitive 202 (bitState).
        long state = Interlocked.Read(ref _mouseState);
        return (int)((state >> 32) & 0x7);
    }

    public void SetCursorPosition(int x, int y)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Cursor.Position = PointToScreen(new Point(x, y))));
        }
        else
        {
            Cursor.Position = PointToScreen(new Point(x, y));
        }
    }

    public int GetInputWord()
    {
        // Dequeue from the thread-safe ring buffer (Blue Book format)
        if (_inputWordBuffer.TryDequeue(out int word))
            return word;
        return 0;  // No events available
    }

    public bool TryConsumeInputShadowSignal() => _inputWordBuffer.TryConsumeSignal();

    public bool HasInputWords => _inputWordBuffer.HasWords;

    public (int eventType, int x, int y, int buttonMask, bool hasButtonTransition, int keyCode, bool isKeyDown)? DequeueCompositeEvent()
    {
        // Not used - primitive 95 reads from ring buffer directly
        // This method exists only for interface compatibility
        return null;
    }

    public void SetSampleInterval(int intervalMs)
    {
        // Not implemented yet - would configure event polling rate
    }

    public void SetInputSemaphore(SmalltalkOOP semaphoreOOP)
    {
        _inputSemaphore = semaphoreOOP;
    }

    public SmalltalkOOP GetInputSemaphore()
    {
        return _inputSemaphore;
    }

    public void SetControllerSemaphore(SmalltalkOOP semaphoreOOP)
    {
        _controllerSemaphore = semaphoreOOP;
    }

    public SmalltalkOOP GetControllerSemaphore()
    {
        return _controllerSemaphore;
    }

    public bool HasPendingEvents()
    {
        return _inputWordBuffer.HasWords;
    }

    public bool ConsumeButtonTransition()
    {
        // Button transitions are now handled via ring buffer key events
        // This method is kept for interface compatibility but not used
        return false;
    }

    public bool WaitForEvents(int timeoutMs)
    {
        // Wait on idle wake handle
        return _idleWake.Wait(timeoutMs);
    }

    public WaitHandle? EventAvailableHandle => _idleWake.WaitHandle;

    public void ResetEventSignalIfEmpty()
    {
        if (!_inputWordBuffer.HasWords)
        {
            _idleWake.Reset();
            // Double-check: producer might have added after check but before reset
            if (_inputWordBuffer.HasWords)
                _idleWake.Set();
        }
    }

    public void ForceResetEventSignal()
    {
        _idleWake.Reset();
    }

    public void SetEventCallback(Action callback)
    {
        // Wire callback to ring buffer - this is the canonical event source
        _inputWordBuffer.SetEventCallback(callback);
    }

    public void SetIsIdleCallback(Func<bool> callback)
    {
        _isIdleCallback = callback;
    }

    public WaitHandle? IdleWakeHandle => _idleWake.WaitHandle;

    public ManualResetEventSlim? IdleWake => _idleWake;

    public SmalltalkOOP ReadClipboardToStringOOP()
    {
        // Not implemented yet - return value should be provided by Smalltalk thread
        return default(SmalltalkOOP);
    }

    public bool WriteClipboardFromStringOOP(SmalltalkOOP stringOOP)
    {
        // Not implemented yet
        return false;
    }

    public void Beep()
    {
        System.Media.SystemSounds.Beep.Play();
    }

    public void Quit(int exitCode)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => {
                Close();
                Environment.Exit(exitCode);
            }));
        }
        else
        {
            Close();
            Environment.Exit(exitCode);
        }
    }

    public int GetTimeWords()
    {
        // Return seconds since Unix epoch, masked to 31 bits
        long secondsSinceEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (int)(secondsSinceEpoch & 0x7FFFFFFF);
    }

    // ============================================================================
    // IDisposable
    // ============================================================================

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _backBuffer?.Dispose();
                _idleWake?.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Shows the display window and processes Windows messages.
    /// This is a non-blocking call - use ShowDialog() for blocking.
    /// </summary>
    public new void Show()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(Show));
            return;
        }

        base.Show();
        Application.DoEvents();
    }

    /// <summary>
    /// Processes pending Windows messages without blocking.
    /// Call this periodically from the VM execution loop.
    /// </summary>
    public void ProcessEvents()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(ProcessEvents));
            return;
        }

        Application.DoEvents();
    }
}
