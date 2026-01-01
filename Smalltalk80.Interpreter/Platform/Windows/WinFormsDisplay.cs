// WinForms Display Implementation
// Unified display with window management, software rendering, and input handling.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Graphics;
using Smalltalk80.Interpreter.Core.VM.Platform;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Platform.Windows;

/// <summary>
/// WinForms implementation of IDisplay.
/// Provides monochrome (1bpp) bitmap display with mouse and keyboard input.
/// Thread-safe: Display updates can occur from Smalltalk thread, rendering occurs on UI thread.
///
/// Architecture:
/// - Internal display buffer is 1bpp (matches Smalltalk Form format)
/// - Final rendering converts 1bpp to 32bpp for screen display
/// - No runtime depth conversion between Smalltalk Forms
///
/// Input Architecture:
/// - UI thread (WinForms): Enqueues input words to ring buffer, updates current state
/// - Smalltalk thread: Reads input words via primitive 95, reads state via primitives 90/91
/// - InputWordRingBuffer: Thread-safe Blue Book format input word queue with shadow counter
/// </summary>
public class WinFormsDisplay : Form, IDisplay
{
    private byte[] _displayBuffer;  // 1bpp: 1 bit per pixel packed (matches Smalltalk Form format)
    private Bitmap _backBuffer;     // 32bpp back buffer for screen rendering
    private readonly object _bufferLock = new();
    private readonly ISmalltalkHeap? _heap;
    private readonly VMMetadata? _vmMetadata;
    private readonly IRuntimeServices? _runtimeServices;
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

    // Idle wake signaling for efficient waiting
    private readonly ManualResetEventSlim _idleWake = new(false);
    private Func<bool>? _isIdleCallback;

    // ============================================================================
    // IDisplay Properties
    // ============================================================================

    public new int Width { get; private set; }
    public new int Height { get; private set; }
    public PixelDepth BitsPerPixel => PixelDepth.OneBit;  // Always 1bpp

    string IDisplay.Title
    {
        get => Text;
        set => Text = value;
    }

    public bool IsVisible => Visible;

    // ============================================================================
    // Constructor
    // ============================================================================

    public WinFormsDisplay(int width, int height, string title = "Smalltalk-80",
        ISmalltalkHeap? heap = null, VMMetadata? vmMetadata = null,
        PixelDepth pixelDepth = PixelDepth.OneBit)  // pixelDepth parameter kept for API compatibility but ignored
    {
        Width = width;
        Height = height;
        _heap = heap;
        _vmMetadata = vmMetadata;
        _runtimeServices = (heap != null && vmMetadata != null)
            ? new RuntimeServices(heap, vmMetadata)
            : null;

        Console.WriteLine($"[WinForms] Creating window: {width}x{height} (1bpp)");

        // 1bpp buffer: each row needs (width + 7) / 8 bytes
        int rowStride = (width + 7) / 8;
        int bufferSize = rowStride * height;
        _displayBuffer = new byte[bufferSize];

        // Create back buffer for double-buffered rendering (32bpp for screen display)
        _backBuffer = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        // Configure form
        Text = title;
        ClientSize = new Size(width, height);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true; // Receive keyboard events before child controls

        // Event handlers
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        KeyPress += OnKeyPress;
    }

    // ============================================================================
    // Atomic Mouse State Helpers (Simulated Hardware Registers)
    // ============================================================================

    private static long PackMouseState(int x, int y, int buttons)
    {
        return (long)(x & 0xFFFF) | ((long)(y & 0xFFFF) << 16) | ((long)(buttons & 0x7) << 32);
    }

    private static (int x, int y, int buttons) UnpackMouseState(long state)
    {
        return (
            (int)(state & 0xFFFF),
            (int)((state >> 16) & 0xFFFF),
            (int)((state >> 32) & 0x7)
        );
    }

    // ============================================================================
    // IDisplay - Window Management
    // ============================================================================

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

    public new void Hide()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(Hide));
            return;
        }
        base.Hide();
    }

    public new void Close()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => base.Close()));
            return;
        }
        base.Close();
    }

    public new void Resize(int width, int height)
    {
        DisplayResized(width, height);
    }

    // ============================================================================
    // IDisplay - Rendering
    // ============================================================================

    public void BitBlt(
        Span<byte> destBits, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<byte> sourceBits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftoneBits, int halftoneWidth, int halftoneHeight,
        int combinationRule)
    {
        // Delegate to the BitBlt engine
        BitBltEngine.CopyBits(
            destBits,
            destWidth, destHeight,
            destX, destY,
            sourceBits,
            sourceWidth, sourceHeight,
            sourceX, sourceY,
            width, height,
            clipX, clipY, clipWidth, clipHeight,
            halftoneBits,
            halftoneWidth, halftoneHeight,
            (CombinationRule)combinationRule);
    }

    public void InvalidateRegion(int x, int y, int w, int h)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Invalidate(new Rectangle(x, y, w, h))));
        }
        else
        {
            Invalidate(new Rectangle(x, y, w, h));
        }
    }

    public void InvalidateAll()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(InvalidateAll));
            return;
        }
        Invalidate();
    }

    public void DisplayResized(int newWidth, int newHeight)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => DisplayResized(newWidth, newHeight)));
            return;
        }

        if (newWidth == Width && newHeight == Height)
        {
            Invalidate();
            return;
        }

        Console.WriteLine($"[Display] Resizing from {Width}x{Height} to {newWidth}x{newHeight}");

        lock (_bufferLock)
        {
            Width = newWidth;
            Height = newHeight;

            // 1bpp buffer: each row needs (width + 7) / 8 bytes
            int newBufferSize = ((newWidth + 7) / 8) * newHeight;
            _displayBuffer = new byte[newBufferSize];
            Console.WriteLine($"[Display] Reallocated _displayBuffer: {newBufferSize} bytes (1bpp)");

            _backBuffer?.Dispose();
            _backBuffer = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            Console.WriteLine($"[Display] Reallocated _backBuffer: {newWidth}x{newHeight}");
        }

        ClientSize = new Size(newWidth, newHeight);
        Invalidate();
    }

    // ============================================================================
    // IDisplay - Input State (Atomic)
    // ============================================================================

    public new (int X, int Y) MousePosition
    {
        get
        {
            long state = Interlocked.Read(ref _mouseState);
            var (x, y, _) = UnpackMouseState(state);
            return (x, y);
        }
    }

    public new (bool Red, bool Yellow, bool Blue) MouseButtons
    {
        get
        {
            long state = Interlocked.Read(ref _mouseState);
            var (_, _, buttons) = UnpackMouseState(state);
            return ((buttons & 4) != 0, (buttons & 2) != 0, (buttons & 1) != 0);
        }
    }

    public int ButtonState
    {
        get
        {
            long state = Interlocked.Read(ref _mouseState);
            return (int)((state >> 32) & 0x7);
        }
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

    // ============================================================================
    // IDisplay - Input Event Queue
    // ============================================================================

    public int GetInputWord()
    {
        if (_inputWordBuffer.TryDequeue(out int word))
            return word;
        return 0;
    }

    public bool HasInputWords => _inputWordBuffer.HasWords;

    public bool TryConsumeInputShadowSignal() => _inputWordBuffer.TryConsumeSignal();

    // ============================================================================
    // IDisplay - Semaphores
    // ============================================================================

    public void SetInputSemaphore(SmalltalkOOP semaphoreOOP) => _inputSemaphore = semaphoreOOP;
    public SmalltalkOOP GetInputSemaphore() => _inputSemaphore;
    public void SetControllerSemaphore(SmalltalkOOP semaphoreOOP) => _controllerSemaphore = semaphoreOOP;
    public SmalltalkOOP GetControllerSemaphore() => _controllerSemaphore;

    // ============================================================================
    // IDisplay - Event Loop Integration
    // ============================================================================

    public WaitHandle? EventAvailableHandle => _idleWake.WaitHandle;
    public ManualResetEventSlim? IdleWake => _idleWake;

    public void ResetEventSignalIfEmpty()
    {
        if (!_inputWordBuffer.HasWords)
        {
            _idleWake.Reset();
            if (_inputWordBuffer.HasWords)
                _idleWake.Set();
        }
    }

    public void ForceResetEventSignal() => _idleWake.Reset();

    public void SetEventCallback(Action callback)
    {
        _inputWordBuffer.SetEventCallback(callback);
    }

    public void SetIsIdleCallback(Func<bool> callback)
    {
        _isIdleCallback = callback;
    }

    public void ProcessEvents()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(ProcessEvents));
            return;
        }
        Application.DoEvents();
    }

    public void RunEventLoop()
    {
        Application.Run(this);
    }

    // ============================================================================
    // IDisplay - System Operations
    // ============================================================================

    public void Beep()
    {
        System.Media.SystemSounds.Beep.Play();
    }

    public void Quit(int exitCode)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => base.Close()));
        }
        else
        {
            base.Close();
        }
    }

    public int GetTimeWords()
    {
        return (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
    }

    public bool PerformSnapshot(SmalltalkOOP snapshotRequestOOP)
    {
        string? filePath = ImageWriter.SnapshotFilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            Console.WriteLine("[Snapshot] Error: No snapshot file path set. Call beSnapshotFile first.");
            return false;
        }

        if (_heap == null || _vmMetadata == null)
        {
            Console.WriteLine("[Snapshot] Error: Heap or VMMetadata not available.");
            return false;
        }

        Console.WriteLine($"[Snapshot] Saving image to: {filePath}");

        var smalltalkOOP = _vmMetadata.KnownObjects.Smalltalk;

        Console.WriteLine("[Snapshot] Running garbage collection...");
        var gcStats = _heap.GC.GarbageCollect();
        Console.WriteLine($"[Snapshot] GC complete: collected {gcStats.CollectedObjects} objects");

        var writer = new ImageWriter(_heap, smalltalkOOP);
        bool success = writer.WriteImage(filePath);

        if (success)
        {
            Console.WriteLine("[Snapshot] Image saved successfully.");
            ImageWriter.SnapshotFilePath = null;
        }
        else
        {
            Console.WriteLine("[Snapshot] Failed to save image.");
        }

        return success;
    }

    public void SetSampleInterval(int intervalMs)
    {
        // No-op - sampling interval not used in this implementation
    }

    // ============================================================================
    // Paint Handler - Copies Display bitmap to screen
    // ============================================================================

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        lock (_bufferLock)
        {
            // Sync 1bpp display buffer from Smalltalk heap, convert to 32bpp bitmap, render
            CopyDisplayFromHeap();
            CopyBufferToBitmap();
            e.Graphics.DrawImageUnscaled(_backBuffer, 0, 0);
        }
    }

    private void CopyDisplayFromHeap()
    {
        if (_heap == null || _vmMetadata == null || _runtimeServices == null)
            return;

        try
        {
            var smalltalkOOP = _vmMetadata.KnownObjects.Smalltalk;
            var displaySymbolOOP = _runtimeServices.InternSymbol("Display");
            if (displaySymbolOOP == _heap.GetNil())
                return;
            var displayOOP = HashBasedDictionaryLookup(smalltalkOOP, displaySymbolOOP);
            if (displayOOP == _heap.GetNil())
                return;

            var bitsOOP = _heap.GetInstanceVariable(displayOOP, 0);
            if (bitsOOP == _heap.GetNil())
                return;

            var format = _heap.GetObjectFormat(bitsOOP);
            int size = _heap.GetIndexedSize(bitsOOP);

            // Copy 1bpp data from Smalltalk heap to display buffer
            CopyDisplayFromHeap1bpp(bitsOOP, format, size);
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cross-thread") || ex.Message.Contains("disposed")) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinForms] CopyDisplayFromHeap failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CopyDisplayFromHeap1bpp(SmalltalkOOP bitsOOP, ObjectFormat format, int size)
    {
        if (_heap == null) return;

        if (format == ObjectFormat.WordIndexed)
        {
            for (int i = 0; i < size && (i * 2 + 1) < _displayBuffer.Length; i++)
            {
                ushort word = _heap.GetIndexedWord(bitsOOP, i);
                _displayBuffer[i * 2] = (byte)(word >> 8);
                _displayBuffer[i * 2 + 1] = (byte)(word & 0xFF);
            }
        }
        else
        {
            for (int i = 0; i < size && i < _displayBuffer.Length; i++)
            {
                _displayBuffer[i] = _heap.GetIndexedByte(bitsOOP, i);
            }
        }
    }

    private SmalltalkOOP HashBasedDictionaryLookup(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
    {
        if (_heap == null)
            return default;

        int size = _heap.GetIndexedSize(dictOOP);
        if (size == 0) return _heap.GetNil();

        var nilOOP = _heap.GetNil();
        int startIndex = IdentityHashUtility.ComputeDictionaryHashIndex(keyOOP, size);
        int index = startIndex;

        do
        {
            var associationOOP = _heap.GetIndexedOOP(dictOOP, index);
            if (associationOOP == nilOOP)
                return nilOOP;

            if (!associationOOP.IsSmallInteger)
            {
                var assocKeyOOP = _heap.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                if (assocKeyOOP == keyOOP)
                {
                    return _heap.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
                }
            }
            index = (index + 1) % size;
        } while (index != startIndex);

        return nilOOP;
    }

    private void CopyBufferToBitmap()
    {
        // Convert 1bpp display buffer to 32bpp bitmap for screen rendering
        BitmapData bmpData = _backBuffer.LockBits(
            new Rectangle(0, 0, Width, Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            CopyBufferToBitmap1bpp(bmpData);
        }
        finally
        {
            _backBuffer.UnlockBits(bmpData);
        }
    }

    private void CopyBufferToBitmap1bpp(BitmapData bmpData)
    {
        unsafe
        {
            int* ptr = (int*)bmpData.Scan0;
            int rowStride = (Width + 7) / 8;
            int stride32 = bmpData.Stride / 4;

            const int black = unchecked((int)0xFF000000);
            const int white = unchecked((int)0xFFFFFFFF);

            for (int y = 0; y < Height; y++)
            {
                int* rowPtr = ptr + (y * stride32);
                int bufferOffset = y * rowStride;
                int x = 0;

                int completeBytes = Width / 8;
                for (int b = 0; b < completeBytes; b++)
                {
                    byte sourceByte = _displayBuffer[bufferOffset + b];
                    rowPtr[x++] = (sourceByte & 0x80) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x40) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x20) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x10) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x08) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x04) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x02) != 0 ? black : white;
                    rowPtr[x++] = (sourceByte & 0x01) != 0 ? black : white;
                }

                int remainingPixels = Width % 8;
                if (remainingPixels > 0)
                {
                    byte sourceByte = _displayBuffer[bufferOffset + completeBytes];
                    for (int bit = 0; bit < remainingPixels; bit++)
                    {
                        int bitIndex = 7 - bit;
                        rowPtr[x++] = (sourceByte & (1 << bitIndex)) != 0 ? black : white;
                    }
                }
            }
        }
    }

    // ============================================================================
    // Mouse Input Handlers
    // ============================================================================

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        var currentButtons = Control.MouseButtons;
        bool red = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Left) || e.Button == System.Windows.Forms.MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Middle) || e.Button == System.Windows.Forms.MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Right) || e.Button == System.Windows.Forms.MouseButtons.Right;
        EnqueueMouseEvent(e.X, e.Y, red, yellow, blue);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        var currentButtons = Control.MouseButtons;
        bool red = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Left) && e.Button != System.Windows.Forms.MouseButtons.Left;
        bool yellow = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Middle) && e.Button != System.Windows.Forms.MouseButtons.Middle;
        bool blue = currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Right) && e.Button != System.Windows.Forms.MouseButtons.Right;
        EnqueueMouseEvent(e.X, e.Y, red, yellow, blue);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        var currentButtons = Control.MouseButtons;
        EnqueueMouseEvent(e.X, e.Y,
            currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Left),
            currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Middle),
            currentButtons.HasFlag(System.Windows.Forms.MouseButtons.Right));
    }

    private void EnqueueMouseEvent(int x, int y, bool red, bool yellow, bool blue)
    {
        int buttons = 0;
        if (red) buttons |= 4;
        if (yellow) buttons |= 2;
        if (blue) buttons |= 1;

        Interlocked.Exchange(ref _mouseState, PackMouseState(x, y, buttons));

        bool positionChanged = (x != _prevMouseX || y != _prevMouseY);
        if (positionChanged)
        {
            _inputWordBuffer.EnqueueMousePosition(x, y);
            _prevMouseX = x;
            _prevMouseY = y;
        }

        if (red && !_prevRedButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeRed);
        else if (!red && _prevRedButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeRed);

        if (yellow && !_prevYellowButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeYellow);
        else if (!yellow && _prevYellowButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeYellow);

        if (blue && !_prevBlueButton)
            _inputWordBuffer.EnqueueButtonDown(InputWordRingBuffer.ButtonCodeBlue);
        else if (!blue && _prevBlueButton)
            _inputWordBuffer.EnqueueButtonUp(InputWordRingBuffer.ButtonCodeBlue);

        _prevRedButton = red;
        _prevYellowButton = yellow;
        _prevBlueButton = blue;

        if (_isIdleCallback?.Invoke() == true)
        {
            _idleWake.Set();
        }
    }

    // ============================================================================
    // Keyboard Input Handlers
    // ============================================================================

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        int smalltalkCode = MapToSmalltalkCode(e.KeyCode);
        if (smalltalkCode > 0)
        {
            _inputWordBuffer.EnqueueKeyDown(smalltalkCode);
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        int smalltalkCode = MapToSmalltalkCode(e.KeyCode);
        if (smalltalkCode > 0)
        {
            _inputWordBuffer.EnqueueKeyUp(smalltalkCode);
        }
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        char c = e.KeyChar;
        if (c > 127) return;

        if (c >= 1 && c <= 26 && Control.ModifierKeys.HasFlag(Keys.Control))
        {
            c = (char)(c + 96);
        }

        _inputWordBuffer.EnqueueKeyDown((int)c);
        _inputWordBuffer.EnqueueKeyUp((int)c);
    }

    private static int MapToSmalltalkCode(Keys key)
    {
        return key switch
        {
            Keys.LShiftKey => Control.ModifierKeys.HasFlag(Keys.Control) ? 136 : 0,
            Keys.RShiftKey => Control.ModifierKeys.HasFlag(Keys.Control) ? 137 : 0,
            Keys.ShiftKey => Control.ModifierKeys.HasFlag(Keys.Control) ? 136 : 0,
            Keys.LControlKey => 138,
            Keys.RControlKey => 138,
            Keys.ControlKey => 138,
            Keys.Capital => 139,
            _ => 0
        };
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
}
