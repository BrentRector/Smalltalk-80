using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Smalltalk80.Interpreter.Graphics;

/// <summary>
/// Windows Forms implementation of IDisplayProvider.
/// Displays Smalltalk bitmap in a Form window with PictureBox.
/// </summary>
public class WinFormsDisplayProvider : IDisplayProvider, IDisposable
{
    private readonly byte[] _displayBits;
    private readonly Bitmap _displayBitmap;
    private readonly Form _window;
    private readonly PictureBox _pictureBox;
    private readonly int _width;
    private readonly int _height;
    private readonly Queue<int> _inputQueue = new();
    private int _mouseX;
    private int _mouseY;
    private int _mouseButtons;
    private bool _disposed;

    public WinFormsDisplayProvider(int width, int height, string title = "Smalltalk-80")
    {
        _width = width;
        _height = height;

        // Allocate display buffer (1-bit monochrome: width * height / 8 bytes)
        _displayBits = new byte[(_width * _height + 7) / 8];

        // Create 1-bit monochrome bitmap
        _displayBitmap = new Bitmap(_width, _height, PixelFormat.Format1bppIndexed);

        // Set palette for monochrome: 0=white, 1=black (Smalltalk convention)
        var palette = _displayBitmap.Palette;
        palette.Entries[0] = Color.White;  // Bit 0 = white
        palette.Entries[1] = Color.Black;  // Bit 1 = black
        _displayBitmap.Palette = palette;

        // Create window
        _window = new Form
        {
            Text = title,
            ClientSize = new Size(_width, _height),
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };

        // Create PictureBox to display bitmap
        _pictureBox = new PictureBox
        {
            Image = _displayBitmap,
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Normal
        };

        _window.Controls.Add(_pictureBox);

        // Hook up input events
        _pictureBox.MouseMove += OnMouseMove;
        _pictureBox.MouseDown += OnMouseDown;
        _pictureBox.MouseUp += OnMouseUp;
        _window.KeyDown += OnKeyDown;
        _window.KeyPress += OnKeyPress;
        _window.KeyUp += OnKeyUp;
    }

    public Memory<byte> GetDisplayBuffer() => _displayBits;

    public int Width => _width;

    public int Height => _height;

    public int BitsPerPixel => 1;

    public void InvalidateRect(int x, int y, int width, int height)
    {
        // Copy display bits to bitmap
        UpdateBitmapFromBuffer();

        // Invalidate the region on the PictureBox
        _pictureBox.Invalidate(new Rectangle(x, y, width, height));
        _pictureBox.Update();
    }

    public void InvalidateAll()
    {
        UpdateBitmapFromBuffer();
        _pictureBox.Invalidate();
        _pictureBox.Update();
    }

    public void Show()
    {
        _window.Show();
    }

    public void Hide()
    {
        _window.Hide();
    }

    public bool ProcessEvents()
    {
        if (_window.IsDisposed || !_window.Visible)
            return false;

        Application.DoEvents();
        return true;
    }

    public (int X, int Y) GetMousePosition() => (_mouseX, _mouseY);

    public int GetMouseButtons() => _mouseButtons;

    public int GetInputWord()
    {
        lock (_inputQueue)
        {
            if (_inputQueue.Count > 0)
                return _inputQueue.Dequeue();
            return 0;
        }
    }

    public void SetCursor(ReadOnlySpan<byte> cursorBits, int hotspotX, int hotspotY)
    {
        // TODO: Implement custom cursor from bitmap
        // For now, just use system cursor
    }

    private void UpdateBitmapFromBuffer()
    {
        // Lock bitmap for writing
        var bitmapData = _displayBitmap.LockBits(
            new Rectangle(0, 0, _width, _height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format1bppIndexed);

        try
        {
            // Calculate stride (may have padding)
            int bitmapStride = bitmapData.Stride;
            int bufferStride = (_width + 7) / 8;

            // Copy row by row to handle potential stride difference
            for (int y = 0; y < _height; y++)
            {
                int srcOffset = y * bufferStride;
                IntPtr dstPtr = bitmapData.Scan0 + (y * bitmapStride);
                Marshal.Copy(_displayBits, srcOffset, dstPtr, bufferStride);
            }
        }
        finally
        {
            _displayBitmap.UnlockBits(bitmapData);
        }
    }

    // Input event handlers

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        _mouseX = e.X;
        _mouseY = e.Y;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        UpdateMouseButtons(e.Button, true);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        UpdateMouseButtons(e.Button, false);
    }

    private void UpdateMouseButtons(MouseButtons button, bool pressed)
    {
        // Map Windows mouse buttons to Smalltalk (red=left, yellow=middle, blue=right)
        int mask = button switch
        {
            MouseButtons.Left => 1,    // Red button (bit 0)
            MouseButtons.Middle => 2,  // Yellow button (bit 1)
            MouseButtons.Right => 4,   // Blue button (bit 2)
            _ => 0
        };

        if (pressed)
            _mouseButtons |= mask;
        else
            _mouseButtons &= ~mask;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Encode key down event
        int inputWord = EncodeKeyEvent(e.KeyCode, e.Modifiers, true);
        if (inputWord != 0)
        {
            lock (_inputQueue)
            {
                _inputQueue.Enqueue(inputWord);
            }
        }
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        // Encode character
        int inputWord = (int)e.KeyChar;
        lock (_inputQueue)
        {
            _inputQueue.Enqueue(inputWord);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        // Encode key up event
        int inputWord = EncodeKeyEvent(e.KeyCode, e.Modifiers, false);
        if (inputWord != 0)
        {
            lock (_inputQueue)
            {
                _inputQueue.Enqueue(inputWord);
            }
        }
    }

    private static int EncodeKeyEvent(Keys keyCode, Keys modifiers, bool down)
    {
        // Smalltalk input word encoding:
        // Bits 0-7: Character code
        // Bit 8: Meta/Alt
        // Bit 9: Control
        // Bit 10: Shift
        // Bit 11: Key down (1) vs up (0)

        int word = (int)keyCode & 0xFF;

        if ((modifiers & Keys.Alt) != 0)
            word |= 1 << 8;
        if ((modifiers & Keys.Control) != 0)
            word |= 1 << 9;
        if ((modifiers & Keys.Shift) != 0)
            word |= 1 << 10;
        if (down)
            word |= 1 << 11;

        return word;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _pictureBox?.Dispose();
        _displayBitmap?.Dispose();
        _window?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
