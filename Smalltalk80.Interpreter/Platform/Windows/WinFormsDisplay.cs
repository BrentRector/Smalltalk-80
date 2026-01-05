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
/// Provides 32bpp bitmap display with mouse and keyboard input.
/// Thread-safe: Display updates can occur from Smalltalk thread, rendering occurs on UI thread.
///
/// Architecture (32bpp-everywhere):
/// - Internal display buffer is 32bpp ARGB for Windows rendering
/// - Smalltalk Forms store RGB-only (24 bits, alpha implicit)
/// - Alpha (0xFF) is added when copying from Smalltalk heap to display buffer
/// - Monochrome: Black=0x000000 (RGB), White=0xFFFFFF (RGB)
///
/// Input Architecture:
/// - UI thread (WinForms): Enqueues input words to ring buffer, updates current state
/// - Smalltalk thread: Reads input words via primitive 95, reads state via primitives 90/91
/// - InputWordRingBuffer: Thread-safe Blue Book format input word queue with shadow counter
/// </summary>
public class WinFormsDisplay : Form, IDisplay
{
    private uint[]? _displayBuffer;  // 32bpp: 1 pixel per uint (matches Smalltalk Form format) - lazy init
    private Bitmap? _backBuffer;     // 32bpp back buffer for screen rendering - lazy init
    private bool _buffersInitialized = false;  // Track if buffers have been allocated
    private bool _windowShown = false;  // Track if window has been shown (deferred until BeDisplay)
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
    public PixelDepth BitsPerPixel => PixelDepth.ThirtyTwoBit;  // Always 32bpp

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
        _heap = heap;
        _vmMetadata = vmMetadata;
        _runtimeServices = (heap != null && vmMetadata != null)
            ? new RuntimeServices(heap, vmMetadata)
            : null;

        // Configure form (always needed for WinForms)
        Text = title;
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
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        KeyPress += OnKeyPress;

        // Initialize buffers if dimensions provided, otherwise defer to DisplayResized
        if (width > 0 && height > 0)
        {
            InitializeBuffers(width, height);
            _windowShown = true;  // Eager init means window will be shown normally
        }
        else
        {
            // Lazy init mode: use minimal window size, buffers allocated on first DisplayResized
            // Window stays hidden until BeDisplay is called with real dimensions
            Width = 1;
            Height = 1;
            ClientSize = new Size(1, 1);
            Visible = false;  // Start hidden - will be shown by DisplayResized
            Console.WriteLine($"[WinForms] Creating window with deferred display (hidden until BeDisplay)");
        }
    }

    /// <summary>
    /// Allocates display buffers and sets window size.
    /// Called from constructor (eager init) or DisplayResized (lazy init).
    /// </summary>
    private void InitializeBuffers(int width, int height)
    {
        Console.WriteLine($"[WinForms] Initializing buffers: {width}x{height} (32bpp)");

        Width = width;
        Height = height;

        // 32bpp buffer: one uint per pixel
        int bufferSize = width * height;
        _displayBuffer = new uint[bufferSize];
        Array.Fill(_displayBuffer, 0xFFFFFFFF);  // Initialize to white

        // Create back buffer for double-buffered rendering
        _backBuffer = new Bitmap(width, height, PixelFormat.Format32bppRgb);

        // Set window size
        ClientSize = new Size(width, height);

        _buffersInitialized = true;
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
        Focus();  // Ensure keyboard focus for key events
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
        Span<uint> destPixels, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<uint> sourcePixels, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<uint> halftonePixels, int halftoneWidth, int halftoneHeight,
        int combinationRule)
    {
        // Delegate to the 32bpp BitBlt engine
        BitBlt32Engine.CopyBits(
            destPixels,
            destWidth, destHeight,
            destX, destY,
            sourcePixels,
            sourceWidth, sourceHeight,
            sourceX, sourceY,
            width, height,
            clipX, clipY, clipWidth, clipHeight,
            halftonePixels,
            halftoneWidth, halftoneHeight,
            null,  // no color for direct BitBlt calls
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

        // Skip if already at this size (unless lazy init pending)
        if (_buffersInitialized && newWidth == Width && newHeight == Height)
        {
            Invalidate();
            return;
        }

        Console.WriteLine($"[Display] Resizing from {Width}x{Height} to {newWidth}x{newHeight}");

        lock (_bufferLock)
        {
            Width = newWidth;
            Height = newHeight;

            // 32bpp buffer: one uint per pixel
            int newBufferSize = newWidth * newHeight;
            _displayBuffer = new uint[newBufferSize];
            Array.Fill(_displayBuffer, 0xFFFFFFFF);  // Initialize to white
            Console.WriteLine($"[Display] Reallocated _displayBuffer: {newBufferSize} pixels (32bpp)");

            _backBuffer?.Dispose();
            _backBuffer = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
            Console.WriteLine($"[Display] Reallocated _backBuffer: {newWidth}x{newHeight}");

            _buffersInitialized = true;
        }

        ClientSize = new Size(newWidth, newHeight);

        // Show window if this is the first real resize (deferred display mode)
        if (!_windowShown)
        {
            Console.WriteLine($"[Display] Showing window for first time at {newWidth}x{newHeight}");
            Visible = true;
            _windowShown = true;
        }

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
            // Skip painting if buffers not initialized (lazy init pending)
            if (!_buffersInitialized || _displayBuffer == null || _backBuffer == null)
            {
                e.Graphics.Clear(Color.White);
                return;
            }

            // Sync display buffer from Smalltalk heap, render to bitmap
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

            // Copy 32bpp data from Smalltalk heap to display buffer
            CopyDisplayFromHeap32bpp(bitsOOP, format, size);
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cross-thread") || ex.Message.Contains("disposed")) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WinForms] CopyDisplayFromHeap failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CopyDisplayFromHeap32bpp(SmalltalkOOP bitsOOP, ObjectFormat format, int size)
    {
        if (_heap == null) return;

        if (format == ObjectFormat.WordIndexed)
        {
            // OPTIMIZATION: Use direct span access for bulk copy instead of element-by-element
            // 32bpp: each word is one pixel (RGB only)
            // Using Format32bppRgb so high byte is ignored as padding
            // Standard RGB: 0x000000=black, 0xFFFFFF=white
            var heapSpan = _heap.GetWordSpan(bitsOOP);
            if (heapSpan.Length > 0 && _displayBuffer != null)
            {
                int copySize = Math.Min(heapSpan.Length, _displayBuffer.Length);
                heapSpan.Slice(0, copySize).CopyTo(_displayBuffer.AsSpan(0, copySize));
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
        // Guard: caller (OnPaint) checks these, but compiler needs explicit check
        if (_backBuffer == null || _displayBuffer == null) return;

        // Direct 32bpp copy from display buffer to bitmap
        BitmapData bmpData = _backBuffer.LockBits(
            new Rectangle(0, 0, Width, Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppRgb);

        try
        {
            CopyBufferToBitmap32bpp(bmpData);
        }
        finally
        {
            _backBuffer.UnlockBits(bmpData);
        }
    }

    private void CopyBufferToBitmap32bpp(BitmapData bmpData)
    {
        // Guard: caller checks _displayBuffer != null, but compiler needs explicit check
        if (_displayBuffer == null) return;

        unsafe
        {
            int* ptr = (int*)bmpData.Scan0;
            int stride32 = bmpData.Stride / 4;

            for (int y = 0; y < Height; y++)
            {
                int* rowPtr = ptr + (y * stride32);
                int bufferOffset = y * Width;

                // Direct copy - each display buffer entry is already a 32bpp pixel
                for (int x = 0; x < Width; x++)
                {
                    if (bufferOffset + x < _displayBuffer.Length)
                    {
                        rowPtr[x] = (int)_displayBuffer[bufferOffset + x];
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

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        // e.Delta is +/- 120 per notch (WHEEL_DELTA constant in Windows)
        // Convert to scroll lines (typically 3 lines per notch, but we use 1:1 for notches)
        int notches = e.Delta / 120;
        if (notches != 0)
        {
            _inputWordBuffer.EnqueueMouseWheel(notches);

            // Wake if idle
            if (_isIdleCallback?.Invoke() == true)
            {
                _idleWake.Set();
            }
        }
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

    // Override to treat arrow keys as input keys (not navigation keys)
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData switch
        {
            Keys.Left or Keys.Right or Keys.Up or Keys.Down => true,
            _ => base.IsInputKey(keyData)
        };
    }

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
            // Arrow keys for cursor movement
            Keys.Left => 188,
            Keys.Right => 189,
            Keys.Up => 190,
            Keys.Down => 191,
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
