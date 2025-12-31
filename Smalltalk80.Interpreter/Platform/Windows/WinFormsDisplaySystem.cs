// WinForms Display System
// Factory for creating WinForms-based displays with software rendering.

using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core.VM.Platform;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Platform.Windows;

/// <summary>
/// WinForms-based display system using GDI for software rendering.
/// </summary>
public class WinFormsDisplaySystem : IDisplaySystem
{
    private readonly ISmalltalkHeap? _heap;
    private readonly VMMetadata? _vmMetadata;

    public WinFormsDisplaySystem(ISmalltalkHeap? heap = null, VMMetadata? vmMetadata = null)
    {
        _heap = heap;
        _vmMetadata = vmMetadata;
    }

    public string Name => "WinForms/GDI (Software)";

    public DisplayCapabilities Capabilities =>
        DisplayCapabilities.OneBitDepth |
        DisplayCapabilities.ThirtyTwoBitDepth;

    public IDisplay CreateDisplay(DisplayOptions options)
    {
        return new WinFormsDisplay(
            options.Width,
            options.Height,
            options.Title,
            _heap,
            _vmMetadata);
    }

    public void Dispose()
    {
        // Nothing to dispose at the system level
    }
}
