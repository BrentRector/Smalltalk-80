// Display System Factory
// Creates and manages the current display system instance.

using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Platform;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Platform;

/// <summary>
/// Factory for creating display system instances.
/// Manages the singleton current display system.
/// </summary>
public static class DisplaySystemFactory
{
    private static readonly object _lock = new();
    private static IDisplaySystem? _current;

    /// <summary>
    /// Gets the current display system instance.
    /// Returns null if not initialized.
    /// </summary>
    public static IDisplaySystem? Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Creates and sets the current display system.
    /// </summary>
    /// <param name="type">Type of display system to create</param>
    /// <param name="heap">Heap manager for Smalltalk object access</param>
    /// <param name="vmMetadata">VM metadata for known objects</param>
    /// <returns>The created display system</returns>
    public static IDisplaySystem CreateAndSetCurrent(
        DisplaySystemType type,
        ISmalltalkHeap? heap = null,
        VMMetadata? vmMetadata = null)
    {
        lock (_lock)
        {
            // Dispose existing if any
            _current?.Dispose();

            _current = type switch
            {
                DisplaySystemType.Auto => CreateBestAvailable(heap, vmMetadata),
                DisplaySystemType.WinForms => CreateWinForms(heap, vmMetadata),
                DisplaySystemType.Null => new NullDisplaySystem(),
                _ => throw new ArgumentException($"Display system type {type} is not yet implemented")
            };

            return _current;
        }
    }

    /// <summary>
    /// Clears the current display system and disposes it.
    /// </summary>
    public static void ClearCurrent()
    {
        lock (_lock)
        {
            _current?.Dispose();
            _current = null;
        }
    }

    private static IDisplaySystem CreateBestAvailable(ISmalltalkHeap? heap, VMMetadata? vmMetadata)
    {
        // For now, WinForms is the only implementation
        // Future: Try Silk.NET first if available, then SDL2, then WinForms
        return CreateWinForms(heap, vmMetadata);
    }

    private static IDisplaySystem CreateWinForms(ISmalltalkHeap? heap, VMMetadata? vmMetadata)
    {
        return new Windows.WinFormsDisplaySystem(heap, vmMetadata);
    }
}

/// <summary>
/// Null display system for headless testing.
/// </summary>
public class NullDisplaySystem : IDisplaySystem
{
    public string Name => "Null (Headless)";

    public DisplayCapabilities Capabilities =>
        DisplayCapabilities.OneBitDepth |
        DisplayCapabilities.ThirtyTwoBitDepth;

    public IDisplay CreateDisplay(DisplayOptions options) => new NullDisplay(options);

    public void Dispose() { }
}

/// <summary>
/// Null display for headless testing.
/// </summary>
public class NullDisplay : IDisplay
{
    public NullDisplay(DisplayOptions options)
    {
        Width = options.Width;
        Height = options.Height;
        Title = options.Title;
        BitsPerPixel = options.PreferredDepth;
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public PixelDepth BitsPerPixel { get; }
    public string Title { get; set; }
    public bool IsVisible => false;

    public void Show() { }
    public void Hide() { }
    public void Close() { }
    public void Resize(int width, int height) { Width = width; Height = height; }

    public void BitBlt(
        Span<byte> destBits, int destWidth, int destHeight, int destX, int destY,
        ReadOnlySpan<byte> sourceBits, int sourceWidth, int sourceHeight, int sourceX, int sourceY,
        int width, int height,
        int clipX, int clipY, int clipWidth, int clipHeight,
        ReadOnlySpan<byte> halftoneBits, int halftoneWidth, int halftoneHeight,
        int combinationRule)
    {
        // No-op for null display
    }

    public void InvalidateRegion(int x, int y, int width, int height) { }
    public void InvalidateAll() { }
    public void DisplayResized(int newWidth, int newHeight) { Width = newWidth; Height = newHeight; }

    public (int X, int Y) MousePosition => (0, 0);
    public (bool Red, bool Yellow, bool Blue) MouseButtons => (false, false, false);
    public int ButtonState => 0;
    public void SetCursorPosition(int x, int y) { }

    public int GetInputWord() => 0;
    public bool HasInputWords => false;
    public bool TryConsumeInputShadowSignal() => false;

    private Shared.Types.SmalltalkOOP _inputSemaphore;
    private Shared.Types.SmalltalkOOP _controllerSemaphore;

    public void SetInputSemaphore(Shared.Types.SmalltalkOOP semaphoreOOP) => _inputSemaphore = semaphoreOOP;
    public Shared.Types.SmalltalkOOP GetInputSemaphore() => _inputSemaphore;
    public void SetControllerSemaphore(Shared.Types.SmalltalkOOP semaphoreOOP) => _controllerSemaphore = semaphoreOOP;
    public Shared.Types.SmalltalkOOP GetControllerSemaphore() => _controllerSemaphore;
    public void SetSampleInterval(int intervalMs) { /* No-op for null display */ }

    private readonly ManualResetEventSlim _idleWake = new(false);
    public WaitHandle? EventAvailableHandle => _idleWake.WaitHandle;
    public ManualResetEventSlim? IdleWake => _idleWake;
    public void ResetEventSignalIfEmpty() { _idleWake.Reset(); }
    public void ForceResetEventSignal() { _idleWake.Reset(); }
    public void SetEventCallback(Action callback) { }
    public void SetIsIdleCallback(Func<bool> callback) { }
    public void ProcessEvents() { }
    public void RunEventLoop() { }

    public void Beep() { }
    public void Quit(int exitCode) { }
    public int GetTimeWords() => (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() & 0x7FFFFFFF);
    public bool PerformSnapshot(Shared.Types.SmalltalkOOP snapshotRequestOOP) => false;

    public void Dispose() { _idleWake.Dispose(); }
}
