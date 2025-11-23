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
        Console.WriteLine($"[Display] OnPaint called - clip bounds: {e.ClipRectangle}");
        lock (_bufferLock)
        {
            // Copy Display bitmap from heap to local buffer
            CopyDisplayFromHeap();

            // Copy display buffer to bitmap
            CopyBufferToBitmap();

            // Draw bitmap to screen
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }
        Console.WriteLine($"[Display] OnPaint completed");
    }

    /// <summary>
    /// Copies the Display bitmap from the Smalltalk heap to the local buffer.
    /// This syncs the heap changes (from BitBlt, etc.) to the renderable buffer.
    /// </summary>
    private void CopyDisplayFromHeap()
    {
        if (_heap == null || _vmMetadata == null)
        {
            Console.WriteLine($"[Display] CopyDisplayFromHeap - no heap or metadata");
            return;
        }

        try
        {
            // Look up Display global
            var smalltalkOOP = _vmMetadata.KnownObjects.Smalltalk;
            var displayOOP = LookupGlobal(smalltalkOOP, "Display");
            if (displayOOP == _heap.GetNil())
            {
                Console.WriteLine($"[Display] CopyDisplayFromHeap - Display is nil");
                return;
            }

            // Get Display bitmap (Form instance variable 0)
            var bitsOOP = _heap.GetInstanceVariable(displayOOP, 0);
            if (bitsOOP == _heap.GetNil())
            {
                Console.WriteLine($"[Display] CopyDisplayFromHeap - Display bitmap is nil");
                return;
            }

            // Read bitmap data from heap
            var format = _heap.GetObjectFormat(bitsOOP);
            int size = _heap.GetIndexedSize(bitsOOP);
            Console.WriteLine($"[Display] CopyDisplayFromHeap - format={format}, size={size}, bufferLength={_displayBuffer.Length}");

            if (format == ObjectFormat.WordIndexed)
            {
                // WordArray: convert words to bytes
                int wordsCopied = 0;
                for (int i = 0; i < size && (i * 2 + 1) < _displayBuffer.Length; i++)
                {
                    ushort word = _heap.GetIndexedWord(bitsOOP, i);
                    _displayBuffer[i * 2] = (byte)(word >> 8);
                    _displayBuffer[i * 2 + 1] = (byte)(word & 0xFF);
                    wordsCopied++;
                }
                Console.WriteLine($"[Display] CopyDisplayFromHeap - copied {wordsCopied} words");

                // Diagnostic: Sample center area of display
                // Display is 800x600 with 4x4 BitBlt test grid (after DisplayScreen initialize)
                // Each tile is 200x150 pixels
                int rowStride = (Width + 7) / 8; // Should be 100 for 800px width
                int sampleRow = 120;
                int sampleStartByte = sampleRow * rowStride + 20;
                Console.Write($"[Display] Sample row {sampleRow} bytes 20-30: ");
                for (int i = 0; i < 11 && (sampleStartByte + i) < _displayBuffer.Length; i++)
                {
                    Console.Write($"{_displayBuffer[sampleStartByte + i]:X2} ");
                }
                Console.WriteLine();
            }
            else
            {
                // ByteArray: copy bytes directly
                int bytesCopied = 0;
                for (int i = 0; i < size && i < _displayBuffer.Length; i++)
                {
                    _displayBuffer[i] = _heap.GetIndexedByte(bitsOOP, i);
                    bytesCopied++;
                }
                Console.WriteLine($"[Display] CopyDisplayFromHeap - copied {bytesCopied} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Display] CopyDisplayFromHeap error: {ex.Message}");
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

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        // Get byte and bit position
                        int byteIndex = y * rowStride + (x / 8);
                        int bitIndex = 7 - (x % 8); // MSB first

                        // Extract bit: 1 = black, 0 = white
                        int bit = (_displayBuffer[byteIndex] >> bitIndex) & 1;

                        // Set pixel: 1 = black (0xFF000000), 0 = white (0xFFFFFFFF)
                        ptr[y * (bmpData.Stride / 4) + x] = bit == 1 ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF);
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
        EnqueueMouseEvent(e);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        EnqueueMouseEvent(e);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        EnqueueMouseEvent(e);
    }

    private void EnqueueMouseEvent(MouseEventArgs e)
    {
        _eventQueue.Enqueue(new SmalltalkEvent
        {
            Type = SmalltalkEventType.Mouse,
            X = e.X,
            Y = e.Y,
            RedButton = e.Button.HasFlag(MouseButtons.Left),
            YellowButton = e.Button.HasFlag(MouseButtons.Middle),
            BlueButton = e.Button.HasFlag(MouseButtons.Right),
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
            Milliseconds = Environment.TickCount64
        });
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Smalltalk typically only needs key down events
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
        Console.WriteLine($"[Display] InvalidateDisplayRegion called: ({x},{y}) {w}x{h}");
        // Signal WinForms thread to repaint
        // Note: Display buffer is updated by Smalltalk thread via direct memory access
        if (InvokeRequired)
        {
            Console.WriteLine($"[Display] BeginInvoke to UI thread");
            BeginInvoke(new Action(() => Invalidate(new Rectangle(x, y, w, h))));
        }
        else
        {
            Console.WriteLine($"[Display] Already on UI thread, invalidating directly");
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
        // Dequeue event from queue and pack into Smalltalk input word format
        if (_eventQueue.TryDequeue(out var evt))
        {
            if (evt == null)
                return 0;

            // Smalltalk input word format:
            // Bits 0-7: Character code (or 0 for mouse events)
            // Bits 8-10: Mouse button state (red=bit 0, yellow=bit 1, blue=bit 2)
            // Bits 11-31: Reserved

            int buttonBits = 0;
            if (evt.RedButton) buttonBits |= 0x1;
            if (evt.YellowButton) buttonBits |= 0x2;
            if (evt.BlueButton) buttonBits |= 0x4;

            if (evt.Type == SmalltalkEventType.Keyboard)
            {
                return (evt.Character & 0xFF) | (buttonBits << 8);
            }
            else
            {
                // Mouse event - no character
                return buttonBits << 8;
            }
        }

        return 0;
    }

    public void SetSampleInterval(int intervalMs)
    {
        // Not implemented yet - would configure event polling rate
    }

    public void SetInputSemaphore(SmalltalkOOP semaphoreOOP)
    {
        // Not implemented yet - would signal semaphore when events available
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
