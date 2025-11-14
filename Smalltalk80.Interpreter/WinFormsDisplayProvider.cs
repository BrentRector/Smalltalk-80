using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Windows Forms implementation of IDisplayProvider.
/// Provides a 640x480 monochrome bitmap display with mouse and keyboard input.
/// Thread-safe: All display updates must occur on the UI thread.
/// </summary>
public class WinFormsDisplayProvider : Form, IDisposable
{
    private readonly byte[] _displayBuffer;
    private readonly Bitmap _backBuffer;
    private readonly Graphics _backBufferGraphics;
    private readonly object _bufferLock = new();
    private readonly Queue<int> _inputQueue = new();
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

    public WinFormsDisplayProvider(int width = 640, int height = 480, string title = "Smalltalk-80")
    {
        Width = width;
        Height = height;

        // Calculate buffer size: each row needs (width + 7) / 8 bytes
        int rowStride = (width + 7) / 8;
        int bufferSize = rowStride * height;
        _displayBuffer = new byte[bufferSize];

        // Create back buffer for double-buffered rendering
        _backBuffer = new Bitmap(width, height, PixelFormat.Format1bppIndexed);
        _backBufferGraphics = Graphics.FromImage(_backBuffer);

        // Configure form
        Text = title;
        ClientSize = new Size(width, height);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        // Set up palette (0 = white, 1 = black for Smalltalk convention)
        SetupMonochromePalette();

        // Event handlers
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    /// <summary>
    /// Sets up the monochrome palette: 0 = white (background), 1 = black (foreground).
    /// This matches Smalltalk-80 convention where 1 bits are black pixels.
    /// </summary>
    private void SetupMonochromePalette()
    {
        ColorPalette palette = _backBuffer.Palette;
        palette.Entries[0] = Color.White;  // 0 = white (background)
        palette.Entries[1] = Color.Black;  // 1 = black (foreground)
        _backBuffer.Palette = palette;
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
    /// Smalltalk format: MSB first (bit 7 = leftmost pixel).
    /// </summary>
    private void CopyBufferToBitmap()
    {
        BitmapData bmpData = _backBuffer.LockBits(
            new Rectangle(0, 0, Width, Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format1bppIndexed);

        try
        {
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int rowStride = (Width + 7) / 8;

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < rowStride; x++)
                    {
                        // Copy byte directly - Smalltalk and Windows both use MSB first for 1bpp
                        ptr[y * bmpData.Stride + x] = _displayBuffer[y * rowStride + x];
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

    private int _mouseButtons = 0;
    private int _mouseX = 0;
    private int _mouseY = 0;

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        UpdateMouseState(e);
        EnqueueMouseEvent();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        UpdateMouseState(e);
        EnqueueMouseEvent();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        UpdateMouseState(e);
    }

    private void UpdateMouseState(MouseEventArgs e)
    {
        _mouseX = e.X;
        _mouseY = e.Y;

        // Map Windows buttons to Smalltalk convention
        // Smalltalk: bit 0 = red (left), bit 1 = yellow (middle), bit 2 = blue (right)
        _mouseButtons = 0;
        if (e.Button.HasFlag(MouseButtons.Left))   _mouseButtons |= 0x1; // Red button
        if (e.Button.HasFlag(MouseButtons.Middle)) _mouseButtons |= 0x2; // Yellow button
        if (e.Button.HasFlag(MouseButtons.Right))  _mouseButtons |= 0x4; // Blue button
    }

    private void EnqueueMouseEvent()
    {
        // Smalltalk input word format (simplified):
        // Bits 0-7: 0 (no character for mouse events)
        // Bits 8-10: Mouse button state
        int eventWord = (_mouseButtons << 8);

        lock (_inputQueue)
        {
            _inputQueue.Enqueue(eventWord);
        }
    }

    public (int X, int Y) GetMousePosition()
    {
        return (_mouseX, _mouseY);
    }

    // ============================================================================
    // Keyboard Input
    // ============================================================================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        EnqueueKeyEvent((char)e.KeyValue, true);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Smalltalk typically only needs key down events
    }

    private void EnqueueKeyEvent(char ch, bool isKeyDown)
    {
        if (!isKeyDown) return;

        // Smalltalk input word format:
        // Bits 0-7: Character code
        // Bits 8-10: Mouse button state (preserved)
        int eventWord = (ch & 0xFF) | (_mouseButtons << 8);

        lock (_inputQueue)
        {
            _inputQueue.Enqueue(eventWord);
        }
    }

    public int GetInputWord()
    {
        lock (_inputQueue)
        {
            if (_inputQueue.Count > 0)
            {
                return _inputQueue.Dequeue();
            }
            return 0;
        }
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
                _backBufferGraphics?.Dispose();
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
