# Graphics Abstraction Layer - Unified Display System

**Last Updated**: 2025-12-31
**Status**: PHASE 1 COMPLETE

## Problem Statement

The initial graphics abstraction separated BitBlt operations from display/input handling:

```
FLAWED Design:
┌─────────────────────────────────────┐
│     WinFormsDisplayProvider         │ ← Owns window, input, display rendering
└─────────────────────────────────────┘
              ╳ Separate (WRONG)
┌─────────────────────────────────────┐
│     IGraphicsBackend                │ ← Only handles BitBlt operations
└─────────────────────────────────────┘
```

**This doesn't work for GPU backends** because GPU graphics libraries want to own:
1. **Window/surface creation** - they create the GPU context tied to the window
2. **Rendering operations** - they manage GPU resources and command buffers
3. **Input events** - they receive events from their window

You cannot use WinForms for the window and OpenGL for rendering without awkward bridging that defeats GPU acceleration.

## GPU Graphics Library Landscape

| Library | Window | Input | 2D Rendering | GPU | Cross-Platform | C# Support |
|---------|--------|-------|--------------|-----|----------------|------------|
| **Silk.NET** | GLFW/SDL | Yes | Via OpenGL/Vulkan | Yes | Yes | Official .NET Foundation |
| **SDL2-CS** | Yes | Yes | SDL_Renderer | Yes | Yes | Community bindings |
| **OpenTK** | Yes | Yes | Via OpenGL | Yes | Yes | Mature, v5 in development |
| **SkiaSharp + GLFW** | Via GLFW | Via GLFW | SkiaSharp canvas | Yes | Yes | Integration issues reported |
| **WinForms + GDI** | Yes | Yes | GDI/BitBlt | No | Windows only | Built-in |

**Recommended**: **Silk.NET** - official .NET Foundation project with unified windowing, input, and OpenGL/Vulkan/DirectX bindings.

## Proposed Architecture: Unified IDisplay

The abstraction must encompass the **entire display system**:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        IDisplaySystem                                │
│  - Factory for creating displays                                     │
│  - Backend enumeration and capability query                          │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                           IDisplay                                   │
│  ┌──────────────┬────────────────┬─────────────────────────────┐    │
│  │   Window     │    Renderer    │         Input               │    │
│  │  Management  │    Context     │        Events               │    │
│  ├──────────────┼────────────────┼─────────────────────────────┤    │
│  │ Width/Height │ CreateTexture  │ MousePosition (atomic)      │    │
│  │ Title        │ BitBlt         │ MouseButtons (atomic)       │    │
│  │ Show/Hide    │ FillRect       │ KeyboardState               │    │
│  │ Resize       │ Clear          │ PollEvents/WaitEvents       │    │
│  │ Close        │ Present        │ EventAvailableHandle        │    │
│  └──────────────┴────────────────┴─────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        ▼                           ▼                           ▼
┌───────────────────┐   ┌───────────────────┐   ┌───────────────────┐
│  WinFormsDisplay  │   │  SilkNetDisplay   │   │    SDL2Display    │
│  (Software/GDI)   │   │  (OpenGL/Vulkan)  │   │  (SDL_Renderer)   │
└───────────────────┘   └───────────────────┘   └───────────────────┘
```

## Interface Definitions

### IDisplaySystem

```csharp
/// <summary>
/// Factory for creating display systems. Entry point for the graphics subsystem.
/// </summary>
public interface IDisplaySystem : IDisposable
{
    /// <summary>Display system name (e.g., "WinForms/GDI", "Silk.NET/OpenGL")</summary>
    string Name { get; }

    /// <summary>Capabilities supported by this display system</summary>
    DisplayCapabilities Capabilities { get; }

    /// <summary>Create a new display with the specified options</summary>
    IDisplay CreateDisplay(DisplayOptions options);

    /// <summary>Enumerate available monitors</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();
}

public record DisplayOptions
{
    public int Width { get; init; } = 800;
    public int Height { get; init; } = 600;
    public string Title { get; init; } = "Smalltalk-80";
    public bool Fullscreen { get; init; } = false;
    public bool VSync { get; init; } = true;
    public PixelDepth PreferredDepth { get; init; } = PixelDepth.OneBit;
}

[Flags]
public enum DisplayCapabilities
{
    None = 0,
    OneBitDepth = 1 << 0,
    EightBitDepth = 1 << 1,
    ThirtyTwoBitDepth = 1 << 2,
    AlphaBlending = 1 << 3,
    HardwareAccelerated = 1 << 4,
    Fullscreen = 1 << 5,
    Resizable = 1 << 6,
    MultipleMonitors = 1 << 7,
}
```

### IDisplay

```csharp
/// <summary>
/// Unified display surface with integrated rendering and input.
/// Window, graphics, and input are inseparable in GPU-accelerated systems.
/// </summary>
public interface IDisplay : IDisposable
{
    // ═══════════════════════════════════════════════════════════════
    // WINDOW MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    int Width { get; }
    int Height { get; }
    string Title { get; set; }
    bool IsVisible { get; }
    bool IsFocused { get; }
    bool IsFullscreen { get; }

    void Show();
    void Hide();
    void Close();
    void Resize(int width, int height);
    void SetFullscreen(bool fullscreen);

    // ═══════════════════════════════════════════════════════════════
    // RENDERING CONTEXT
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Create an off-screen texture for rendering</summary>
    ITexture CreateTexture(int width, int height, PixelDepth depth);

    /// <summary>Create a texture initialized with pixel data</summary>
    ITexture CreateTextureFromData(int width, int height, PixelDepth depth, ReadOnlySpan<byte> data);

    /// <summary>Perform BitBlt raster operation</summary>
    void BitBlt(in BitBltParams p);

    /// <summary>Fill rectangle with solid color</summary>
    void FillRect(int x, int y, int width, int height, Color32 color, BlendMode mode = BlendMode.Copy);

    /// <summary>Clear entire display to color</summary>
    void Clear(Color32 color);

    /// <summary>Begin a new frame (for GPU backends that batch commands)</summary>
    void BeginFrame();

    /// <summary>End frame and present to screen (swap buffers)</summary>
    void EndFrame();

    /// <summary>
    /// The display's backing texture (Smalltalk's Display Form).
    /// BitBlt operations targeting this texture appear on screen after EndFrame().
    /// </summary>
    ITexture DisplayTexture { get; }

    /// <summary>Mark a region as needing redraw (for partial updates)</summary>
    void InvalidateRegion(int x, int y, int width, int height);

    // ═══════════════════════════════════════════════════════════════
    // INPUT HANDLING (Unified with display - NOT separable)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Current mouse position relative to display (atomic read).
    /// Thread-safe for polling from Smalltalk thread.
    /// </summary>
    (int X, int Y) MousePosition { get; }

    /// <summary>
    /// Current mouse button state (atomic read).
    /// Thread-safe for polling from Smalltalk thread.
    /// </summary>
    MouseButtons MouseButtons { get; }

    /// <summary>
    /// Check if a key is currently pressed.
    /// </summary>
    bool IsKeyDown(KeyCode key);

    /// <summary>
    /// Poll for next pending event. Returns false if queue is empty.
    /// </summary>
    bool PollEvent(out DisplayEvent evt);

    /// <summary>
    /// WaitHandle that signals when events are available.
    /// Use with WaitHandle.WaitAny() for efficient multi-source waiting.
    /// </summary>
    WaitHandle EventAvailableHandle { get; }

    // ═══════════════════════════════════════════════════════════════
    // EVENT LOOP INTEGRATION
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Process pending window system events.
    /// Must be called on the display's owning thread (usually UI thread).
    /// </summary>
    void ProcessEvents();

    /// <summary>
    /// Run the display's event loop until Close() is called.
    /// Blocks the calling thread.
    /// </summary>
    void RunEventLoop();

    /// <summary>Event raised when display is resized</summary>
    event Action<int, int>? OnResize;

    /// <summary>Event raised when close is requested</summary>
    event Action? OnCloseRequested;
}
```

### DisplayEvent

```csharp
/// <summary>
/// Unified event type for all display input events.
/// Follows Blue Book input word format for compatibility.
/// </summary>
public readonly struct DisplayEvent
{
    public DisplayEventType Type { get; init; }

    // Mouse events: position and button
    public int MouseX { get; init; }
    public int MouseY { get; init; }
    public MouseButtons Button { get; init; }

    // Keyboard events: key code and character
    public KeyCode Key { get; init; }
    public char Character { get; init; }
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Convert to Blue Book input word format (type << 12 | param)</summary>
    public int ToBlueBookWord() => Type switch
    {
        DisplayEventType.MouseMove => (1 << 12) | MouseX,  // X in first word
        DisplayEventType.MouseButtonDown => (5 << 12) | (int)Button,
        DisplayEventType.MouseButtonUp => (6 << 12) | (int)Button,
        DisplayEventType.KeyDown => (3 << 12) | (int)Key,
        DisplayEventType.KeyUp => (4 << 12) | (int)Key,
        _ => 0
    };
}

public enum DisplayEventType
{
    None = 0,
    MouseMove,
    MouseButtonDown,
    MouseButtonUp,
    KeyDown,
    KeyUp,
    TextInput,      // Character typed (after key translation)
    Resize,
    Focus,
    Blur,
    CloseRequested,
}

[Flags]
public enum MouseButtons
{
    None = 0,
    Red = 1,        // Left button (Blue Book: red)
    Yellow = 2,     // Middle button (Blue Book: yellow)
    Blue = 4,       // Right button (Blue Book: blue)
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
}
```

## Threading Model Integration

Our two-thread architecture integrates as follows:

```
┌────────────────────────────────────────────────────────────────────┐
│                    UI/Display Thread                                │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                  IDisplay Implementation                      │   │
│  │                                                               │   │
│  │  ProcessEvents() / RunEventLoop()                            │   │
│  │    - Pumps window system messages                            │   │
│  │    - Enqueues DisplayEvents to thread-safe queue             │   │
│  │    - Updates atomic MousePosition/MouseButtons               │   │
│  │    - Signals EventAvailableHandle                            │   │
│  │                                                               │   │
│  │  EndFrame() / Present()                                      │   │
│  │    - Swaps GPU buffers or blits to screen                    │   │
│  │                                                               │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                      │
│                              │ EventAvailableHandle (WaitHandle)    │
│                              │ + Atomic state accessors             │
│                              ▼                                      │
└────────────────────────────────────────────────────────────────────┘
                               │
                               │  WaitHandle.WaitAny([EventHandle, TimerHandle])
                               ▼
┌────────────────────────────────────────────────────────────────────┐
│                       Smalltalk Thread                              │
│                                                                     │
│  InterpreterLoop:                                                   │
│    - WaitAny([display.EventAvailableHandle, timer.Handle])         │
│    - Read events via display.PollEvent()                           │
│    - Poll state via display.MousePosition, display.MouseButtons    │
│    - Execute BitBlt primitives:                                    │
│        - For software backend: direct execution                    │
│        - For GPU backend: may queue commands for UI thread         │
│                                                                     │
└────────────────────────────────────────────────────────────────────┘
```

### Thread Safety Requirements

1. **Atomic state reads**: `MousePosition` and `MouseButtons` must be atomic for polling
2. **Event queue**: Thread-safe queue with `EventAvailableHandle` signaling
3. **BitBlt operations**:
   - Software backend: Can execute on Smalltalk thread directly
   - GPU backend: May need to marshal to UI thread (implementation detail)

## Implementation Plan

### Phase 1: Refactor WinForms to IDisplay ✅ COMPLETE

Restructured WinForms code to implement unified IDisplay interface:

```csharp
public class WinFormsDisplay : Form, IDisplay, IPlatformBridge
{
    // Window management: Form functionality
    // Rendering: BitBltEngine directly for 1-bit operations
    // Input: Ring buffer with atomic mouse state
    // IPlatformBridge: Backward compatibility for primitives
}
```

Files created:
- `Platform/IDisplay.cs` - Unified display interface
- `Platform/IDisplaySystem.cs` - Factory interface
- `Platform/DisplaySystemFactory.cs` - Factory implementation
- `Platform/Windows/WinFormsDisplay.cs` - WinForms implementation
- `Platform/Windows/WinFormsDisplaySystem.cs` - WinForms factory

Files removed:
- `Core/VM/Graphics/Abstraction/*` - Old separated abstraction
- `Platform/Windows/WinFormsDisplayProvider.cs` - Replaced by WinFormsDisplay

### Phase 2: Add Silk.NET Backend

Implement `SilkNetDisplay` using Silk.NET:

```csharp
public class SilkNetDisplay : IDisplay
{
    private IWindow _window;      // Silk.NET window
    private GL _gl;               // OpenGL context
    private IInputContext _input; // Silk.NET input

    // Use OpenGL textures for ITexture
    // Implement BitBlt as textured quad draw with appropriate blend mode
    // Handle 1-bit format via shader or CPU conversion
}
```

### Phase 3: 32-bit Native Support

1. Extend Smalltalk Form class to support 32-bit depth
2. Modify DisplayScreen to prefer 32-bit when GPU backend available
3. Remove 1-bit → 32-bit conversion overhead
4. Keep 1-bit compatibility for legacy code

## Migration from Current Implementation

The current `IGraphicsBackend` and related code should be:

1. **Deprecated**: Mark as obsolete
2. **Wrapped**: WinFormsDisplay can internally use SoftwareGraphicsBackend
3. **Eventually removed**: Once all backends implement IDisplay

Current files affected:
- `GraphicsPrimitives.cs` → Use `DisplaySystem.Current.Display` instead of `GraphicsBackendFactory.Current`
- `WinFormsDisplayProvider.cs` → Refactor into `WinFormsDisplay : IDisplay`
- `Program.cs` → Initialize via `DisplaySystemFactory.CreateAndSetCurrent()`

## File Structure (Current)

```
Smalltalk80.Interpreter/
├── Core/VM/Graphics/
│   ├── BitBltEngine.cs           # Core 1-bit BitBlt engine
│   └── CombinationRule.cs        # BitBlt combination rules enum
│
├── Platform/
│   ├── IDisplay.cs               # Unified display interface
│   ├── IDisplaySystem.cs         # Factory interface
│   ├── DisplaySystemFactory.cs   # Factory implementation + NullDisplay
│   │
│   └── Windows/
│       ├── WinFormsDisplay.cs    # WinForms IDisplay implementation
│       └── WinFormsDisplaySystem.cs  # WinForms factory
│
│   # FUTURE:
│   ├── SilkNet/                  # GPU backend (Silk.NET + OpenGL)
│   │   ├── SilkNetDisplaySystem.cs
│   │   └── SilkNetDisplay.cs
│   │
│   └── SDL2/                     # Alternative GPU backend
│       └── ...
```

## References

- [Silk.NET](https://github.com/dotnet/Silk.NET) - Official .NET Foundation graphics library
- [SDL2-CS](https://github.com/flibitijibibo/SDL2-CS) - SDL2 C# bindings
- [OpenTK](https://github.com/opentk/opentk) - OpenGL/OpenAL C# library
- [SkiaSharp + GLFW integration](https://github.com/mono/SkiaSharp/issues/749) - Known issues
