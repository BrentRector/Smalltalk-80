// Display System Factory Interface
// Entry point for creating display surfaces with different backends.

namespace Smalltalk80.Interpreter.Platform;

/// <summary>
/// Factory for creating display systems.
/// Entry point for the graphics/windowing subsystem.
/// </summary>
public interface IDisplaySystem : IDisposable
{
    /// <summary>Display system name (e.g., "WinForms/GDI", "Silk.NET/OpenGL")</summary>
    string Name { get; }

    /// <summary>Capabilities supported by this display system</summary>
    DisplayCapabilities Capabilities { get; }

    /// <summary>Create a new display with the specified options</summary>
    IDisplay CreateDisplay(DisplayOptions options);
}

/// <summary>
/// Options for creating a display.
/// </summary>
public record DisplayOptions
{
    /// <summary>Display width in pixels</summary>
    public int Width { get; init; } = 800;

    /// <summary>Display height in pixels</summary>
    public int Height { get; init; } = 600;

    /// <summary>Window title</summary>
    public string Title { get; init; } = "Smalltalk-80";

    /// <summary>Preferred pixel depth</summary>
    public PixelDepth PreferredDepth { get; init; } = PixelDepth.OneBit;
}

/// <summary>
/// Capabilities supported by a display system.
/// </summary>
[Flags]
public enum DisplayCapabilities
{
    None = 0,

    /// <summary>Supports 1-bit monochrome</summary>
    OneBitDepth = 1 << 0,

    /// <summary>Supports 8-bit indexed/grayscale</summary>
    EightBitDepth = 1 << 1,

    /// <summary>Supports 32-bit ARGB</summary>
    ThirtyTwoBitDepth = 1 << 2,

    /// <summary>Supports alpha blending</summary>
    AlphaBlending = 1 << 3,

    /// <summary>Uses GPU hardware acceleration</summary>
    HardwareAccelerated = 1 << 4,

    /// <summary>Supports fullscreen mode</summary>
    Fullscreen = 1 << 5,

    /// <summary>Window can be resized by user</summary>
    Resizable = 1 << 6,
}

/// <summary>
/// Available display system backends.
/// </summary>
public enum DisplaySystemType
{
    /// <summary>Automatically select best available backend</summary>
    Auto,

    /// <summary>WinForms with GDI/software rendering</summary>
    WinForms,

    /// <summary>Silk.NET with OpenGL (future)</summary>
    SilkNetOpenGL,

    /// <summary>SDL2 with SDL_Renderer (future)</summary>
    SDL2,

    /// <summary>Null backend for headless testing</summary>
    Null
}
