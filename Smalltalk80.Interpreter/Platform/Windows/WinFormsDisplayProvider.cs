using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Primitives;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Platform;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Platform.Windows;

/// <summary>
/// Windows Forms implementation of display provider and platform bridge.
/// Provides a monochrome bitmap display with mouse and keyboard input.
/// Thread-safe: Display updates can occur from Smalltalk thread, rendering occurs on UI thread.
/// </summary>
public class WinFormsDisplayProvider : Form, IPlatformBridge, IDisposable
{
    private byte[] _displayBuffer;
    private Bitmap _backBuffer;
    private readonly object _bufferLock = new();
    private readonly SmalltalkEventQueue _eventQueue;
    private readonly ISmalltalkHeap? _heap;
    private readonly VMMetadata? _vmMetadata;
    private bool _disposed = false;

    // Input semaphore OOP for event notification (signaled on raw input)
    private SmalltalkOOP _inputSemaphore = default;
    // Controller semaphore OOP for button transitions (signaled on button down/up)
    private SmalltalkOOP _controllerSemaphore = default;

    public new int Width { get; private set; }
    public new int Height { get; private set; }

    /// <summary>
    /// Gets direct access to the display buffer (1-bit monochrome bitmap).
    /// Format: Each byte contains 8 pixels, MSB first (bit 7 = leftmost pixel).
    /// Row stride = (Width + 7) / 8 bytes.
    /// Total size = ((Width + 7) / 8) * Height bytes.
    /// </summary>
    public Memory<byte> DisplayBuffer => new Memory<byte>(_displayBuffer);

    public WinFormsDisplayProvider(int width, int height, SmalltalkEventQueue eventQueue, string title = "Smalltalk-80", ISmalltalkHeap? heap = null, VMMetadata? vmMetadata = null)
    {
        Width = width;
        Height = height;
        _eventQueue = eventQueue;
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
        // For MouseDown, the button that triggered the event IS pressed.
        // Combine current button state with the button being pressed.
        // This handles fast clicks where polling Control.MouseButtons might miss the press.
        var currentButtons = Control.MouseButtons;

        bool red = currentButtons.HasFlag(MouseButtons.Left) || e.Button == MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(MouseButtons.Middle) || e.Button == MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(MouseButtons.Right) || e.Button == MouseButtons.Right;

        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = red,
            YellowButton = yellow,
            BlueButton = blue,
            Milliseconds = Environment.TickCount64
        });
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        // For MouseUp, the button that triggered the event is NOT pressed.
        // Use current button state but ensure the released button is false.
        // This handles fast clicks where polling Control.MouseButtons might miss the release.
        var currentButtons = Control.MouseButtons;

        bool red = currentButtons.HasFlag(MouseButtons.Left) && e.Button != MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(MouseButtons.Middle) && e.Button != MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(MouseButtons.Right) && e.Button != MouseButtons.Right;

        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = red,
            YellowButton = yellow,
            BlueButton = blue,
            Milliseconds = Environment.TickCount64
        });
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        // For MouseMove, use current button state as-is (which buttons are held during move)
        var currentButtons = Control.MouseButtons;

        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = currentButtons.HasFlag(MouseButtons.Left),
            YellowButton = currentButtons.HasFlag(MouseButtons.Middle),
            BlueButton = currentButtons.HasFlag(MouseButtons.Right),
            Milliseconds = Environment.TickCount64
        });
    }

    // ============================================================================
    // Keyboard Input
    // ============================================================================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Keyboard,
            Character = (char)e.KeyValue,
            KeyCode = e.KeyValue,
            IsKeyDown = true,
            Milliseconds = Environment.TickCount64
        });
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Keyboard,
            Character = (char)e.KeyValue,
            KeyCode = e.KeyValue,
            IsKeyDown = false,
            Milliseconds = Environment.TickCount64
        });
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
        return _eventQueue.GetMousePosition();
    }

    public (bool red, bool yellow, bool blue) GetMouseButtons()
    {
        return _eventQueue.GetMouseButtons();
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
        // Delegate to the event queue which properly formats input words
        // Smalltalk-80 input word format:
        // - Bits 0-11: parameter value
        // - Bits 12-15: event type (1=mouseX, 2=mouseY, 3=keyDown, 4=keyUp)
        return _eventQueue.GetNextInputWord();
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
        return _eventQueue.HasPendingEvents;
    }

    public bool ConsumeButtonTransition()
    {
        return _eventQueue.ConsumeButtonTransition();
    }

    public bool WaitForEvents(int timeoutMs)
    {
        return _eventQueue.WaitOne(timeoutMs);
    }

    public WaitHandle? EventAvailableHandle => _eventQueue.EventAvailableHandle;

    public void ResetEventSignalIfEmpty()
    {
        _eventQueue.ResetSignalIfEmpty();
    }

    public void ForceResetEventSignal()
    {
        _eventQueue.ForceResetEventSignal();
    }

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
