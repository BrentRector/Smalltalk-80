using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Smalltalk80.Interpreter.Core;
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
    private readonly byte[] _displayBuffer;
    private readonly Bitmap _backBuffer;
    private readonly object _bufferLock = new();
    private readonly SmalltalkEventQueue _eventQueue;
    private bool _disposed = false;

    public new int Width { get; }
    public new int Height { get; }

    /// <summary>
    /// Gets direct access to the display buffer (1-bit monochrome bitmap).
    /// Format: Each byte contains 8 pixels, MSB first (bit 7 = leftmost pixel).
    /// Row stride = (Width + 7) / 8 bytes.
    /// Total size = ((Width + 7) / 8) * Height bytes.
    /// </summary>
    public Memory<byte> DisplayBuffer => new Memory<byte>(_displayBuffer);

    public WinFormsDisplayProvider(int width, int height, SmalltalkEventQueue eventQueue, string title = "Smalltalk-80")
    {
        Width = width;
        Height = height;
        _eventQueue = eventQueue;

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
            // Copy display buffer to bitmap
            CopyBufferToBitmap();

            // Draw bitmap to screen
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }
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
        // BitBlt is handled by the VM primitives
        // This method is called AFTER the VM performs the BitBlt
        // We just need to detect if it affected Display and invalidate
        return true;
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
        // Called from Smalltalk thread when Display dimensions change
        // Must marshal to UI thread for window resize
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => DisplayResized(newWidth, newHeight)));
            return;
        }

        // On UI thread now - safe to resize window
        Console.WriteLine($"[Display] Resizing from {Width}x{Height} to {newWidth}x{newHeight}");

        // NOTE: Cannot reallocate buffers here because:
        // 1. Width and Height are readonly properties (set in constructor)
        // 2. _displayBuffer and _backBuffer are readonly fields
        // 3. Window resizing currently not supported at runtime
        //
        // Future enhancement: Make Width/Height mutable and reallocate buffers
        // For now, just resize the window - buffer will be clipped/padded

        // Resize window to match new dimensions
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
