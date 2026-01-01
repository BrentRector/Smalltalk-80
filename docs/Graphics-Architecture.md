# Smalltalk-80 Graphics System Architecture

**Last Updated**: 2026-01-01
**Status**: ✅ **PHASES 1-5 COMPLETE** - Full graphics system operational with font support

## Overview

This document describes the architecture for implementing the Smalltalk-80 graphics system in our C# interpreter, including the unified display abstraction for pluggable graphics backends.

## Table of Contents

1. [Smalltalk-80 Graphics Model](#smalltalk-80-graphics-model)
2. [Current Architecture](#current-architecture)
3. [Unified Display Abstraction](#unified-display-abstraction)
4. [Implementation Status](#implementation-status)
5. [Future: 32-bit Display Migration](#future-32-bit-display-migration)

---

## Smalltalk-80 Graphics Model

The Smalltalk-80 system has an elegant graphics architecture built entirely on a single primitive operation: **BitBlt** (Bit Block Transfer).

### Key Classes

- **Form** - Bitmap/raster image (width, height, bits array, offset)
- **DisplayScreen** - The actual display device (subclass of Form)
- **BitBlt** - The bitmap block transfer engine
- **Cursor** - Mouse cursor (16x16 bitmap with hotspot)
- **Pen** - Drawing tool built on BitBlt
- **DisplayText** - Text rendering with strike fonts

### Critical Primitive: 96 (copyBits)

Primitive 96 is the **sole graphics primitive** - all other graphics operations are built on top of it in Smalltalk code.

**BitBlt Parameters:**
```smalltalk
destForm       "Destination bitmap"
sourceForm     "Source bitmap"
halftoneForm   "Pattern/mask for operations"
combinationRule "Operation mode (0-15)"
destX, destY   "Destination coordinates"
width, height  "Operation dimensions"
sourceX, sourceY "Source coordinates"
clipX, clipY, clipWidth, clipHeight "Clipping rectangle"
```

**16 Combination Rules:**

| Rule | Name | Operation | Use Case |
|------|------|-----------|----------|
| 0 | Clear | 0 | Clear destination |
| 1 | And | src AND dst | Masking |
| 2 | AndReverse | src AND (NOT dst) | Erase pattern |
| 3 | Copy | src | Normal copy |
| 4 | AndInverted | (NOT src) AND dst | Reverse erase |
| 5 | Noop | dst | No operation |
| 6 | Xor | src XOR dst | Invert/toggle |
| 7 | Or | src OR dst | Paint over |
| 8 | Nor | NOT (src OR dst) | |
| 9 | Equiv | NOT (src XOR dst) | |
| 10 | Invert | NOT dst | |
| 11 | OrReverse | src OR (NOT dst) | |
| 12 | CopyInverted | NOT src | |
| 13 | OrInverted | (NOT src) OR dst | |
| 14 | Nand | NOT (src AND dst) | |
| 15 | Set | 1 | Fill with white |

### Display Requirements

- **Bit Depth**: Originally 1-bit monochrome, flexible architecture
- **Resolution**: Dynamic (no fixed resolution)
- **Coordinate System**: Origin at top-left, Y increases downward
- **Color Model**: 0 = white, 1 = black (traditional monochrome)

### Input System

**Mouse:**
- Position tracking (X, Y coordinates)
- 3-button support (red, yellow, blue buttons)
- **Primitive 90**: mousePoint (returns Point)
- **Primitive 91**: cursorLocPut: (set cursor position)

**Cursor:**
- 16x16 monochrome bitmap
- Hotspot offset point
- **Primitive 101**: beCursor (set hardware cursor)

**Keyboard:**
- **Primitive 95**: inputWord (read encoded input event)
- **Primitive 93**: inputSemaphore: (set event notification)

---

## Current Architecture

### Layered Stack

```
┌─────────────────────────────────────────────────────────────────┐
│              Smalltalk-80 Code (Form, BitBlt, DisplayText)      │
├─────────────────────────────────────────────────────────────────┤
│  Primitives 90-102 (Graphics/Input) - GraphicsPrimitives.cs     │
├─────────────────────────────────────────────────────────────────┤
│      BitBltEngine (Pure C# - All 16 rules, 1bpp operations)     │
├─────────────────────────────────────────────────────────────────┤
│    IDisplay (Unified display/input/cursor abstraction)          │
├─────────────────────────────────────────────────────────────────┤
│  WinFormsDisplay (Windows Forms + GDI software rendering)       │
├─────────────────────────────────────────────────────────────────┤
│         32bpp back buffer → Window rendering                    │
└─────────────────────────────────────────────────────────────────┘
```

### Technology Stack

- **BitBlt Engine**: Pure C# with unsafe code for performance
- **Display**: Windows Forms with System.Drawing.Bitmap
- **Bitmap Format**: 1bpp monochrome internal, 32bpp rendering
- **Font Support**: IFontBridge abstraction with GDI/GDI+ backends
- **Platform**: Windows (with abstraction for future cross-platform)
- **Dependencies**: Zero external - uses only built-in .NET libraries

### File Structure

```
Smalltalk80.Interpreter/
├── Core/VM/Graphics/
│   ├── BitBltEngine.cs           # Core 1-bit BitBlt engine
│   ├── IFontBridge.cs            # Font abstraction interface
│   ├── FontBridgeFactory.cs      # Font factory with toggle
│   ├── WinFormsFontBridge.cs     # TrueType/GDI+ fonts
│   └── GdiBitmapFontBridge.cs    # Bitmap fonts (Fixedsys)
│
├── Core/VM/Platform/
│   └── IDisplay.cs               # Unified display interface
│
├── Platform/
│   ├── IDisplaySystem.cs         # Factory interface
│   ├── DisplaySystemFactory.cs   # Factory implementation
│   │
│   └── Windows/
│       ├── WinFormsDisplay.cs    # WinForms IDisplay implementation
│       └── WinFormsDisplaySystem.cs  # WinForms factory
│
├── Core/VM/Primitives/
│   ├── GraphicsPrimitives.cs     # BitBlt, cursor, display primitives
│   └── InputPrimitives.cs        # Mouse, keyboard primitives
```

---

## Unified Display Abstraction

### Problem: Why Unified Interface?

The initial approach separated BitBlt operations from display/input handling:

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

### GPU Graphics Library Landscape

| Library | Window | Input | 2D Rendering | GPU | Cross-Platform | C# Support |
|---------|--------|-------|--------------|-----|----------------|------------|
| **Silk.NET** | GLFW/SDL | Yes | Via OpenGL/Vulkan | Yes | Yes | Official .NET Foundation |
| **SDL2-CS** | Yes | Yes | SDL_Renderer | Yes | Yes | Community bindings |
| **OpenTK** | Yes | Yes | Via OpenGL | Yes | Yes | Mature, v5 in development |
| **SkiaSharp + GLFW** | Via GLFW | Via GLFW | SkiaSharp canvas | Yes | Yes | Integration issues reported |
| **WinForms + GDI** | Yes | Yes | GDI/BitBlt | No | Windows only | Built-in |

**Recommended for GPU**: **Silk.NET** - official .NET Foundation project with unified windowing, input, and OpenGL/Vulkan/DirectX bindings.

### IDisplaySystem Interface

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

### IDisplay Interface

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

    /// <summary>Check if a key is currently pressed.</summary>
    bool IsKeyDown(KeyCode key);

    /// <summary>Poll for next pending event. Returns false if queue is empty.</summary>
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

### DisplayEvent Structure

```csharp
/// <summary>
/// Unified event type for all display input events.
/// Follows Blue Book input word format for compatibility.
/// </summary>
public readonly struct DisplayEvent
{
    public DisplayEventType Type { get; init; }

    // Mouse events
    public int MouseX { get; init; }
    public int MouseY { get; init; }
    public MouseButtons Button { get; init; }

    // Keyboard events
    public KeyCode Key { get; init; }
    public char Character { get; init; }
    public KeyModifiers Modifiers { get; init; }

    /// <summary>Convert to Blue Book input word format (type << 12 | param)</summary>
    public int ToBlueBookWord() => Type switch
    {
        DisplayEventType.MouseMove => (1 << 12) | MouseX,
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
    TextInput,
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
```

### Threading Model

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

**Thread Safety Requirements:**
1. **Atomic state reads**: `MousePosition` and `MouseButtons` must be atomic for polling
2. **Event queue**: Thread-safe queue with `EventAvailableHandle` signaling
3. **BitBlt operations**: Software backend executes directly; GPU backend may marshal to UI thread

---

## Implementation Status

### Phase 1: Core BitBlt Engine ✅ COMPLETE

- ✅ `BitBltEngine.cs` - Pure C# BitBlt implementation with unsafe code
- ✅ All 16 combination rules implemented (Clear through Set)
- ✅ Clipping support with boundary validation
- ✅ Halftone pattern support (16×16 patterns)
- ✅ Nil source handling for fill operations
- ✅ MSB-first bit packing (Smalltalk convention)

### Phase 2: Display Integration ✅ COMPLETE

- ✅ `WinFormsDisplay.cs` - Windows Forms display backend
- ✅ Thread-safe Display synchronization (Smalltalk thread → UI thread)
- ✅ `CopyDisplayFromHeap()` - Syncs Display bitmap from heap to local buffer
- ✅ `InvalidateDisplayRegion()` - Dirty rectangle optimization
- ✅ `DisplayResized()` - Dynamic window resizing support
- ✅ Format conversion: 1bpp monochrome → 32bpp ARGB rendering
- ✅ Double buffering for flicker-free updates

### Phase 3: Primitive 96 Implementation ✅ COMPLETE

- ✅ `GraphicsPrimitives.cs` - Primitive 96 (copyBits) implementation
- ✅ Form parameter extraction from heap objects
- ✅ Bitmap buffer access via ISmalltalkHeap interface
- ✅ Display invalidation for BitBlt operations to Display
- ✅ Complete flow: `Form>>fill:mask:` → Primitive 96 → BitBltEngine → Display invalidation → UI repaint

### Phase 4: Additional Graphics Primitives ✅ COMPLETE

- ✅ Primitive 90 - mousePoint (returns Point with mouse coordinates)
- ✅ Primitive 91 - cursorLocPut: (set cursor position)
- ✅ Primitive 93 - inputSemaphore: (set event notification semaphore)
- ✅ Primitive 95 - inputWord (read encoded input event from ring buffer)
- ✅ Primitive 96 - copyBits (BitBlt)
- ✅ Primitive 97 - snapshot (image save/restore)
- ✅ Primitive 101 - beCursor (cursor management)
- ✅ Primitive 102 - beDisplay (Display management with resize support)
- ✅ Primitive 104 - drawLoopX:Y: (Bresenham line drawing - 3.6x faster than Smalltalk)
- ✅ Primitive 202 - bitState (atomic mouse button polling for tight loops)

### Phase 5: Font Support ✅ COMPLETE

**Font Bridge Architecture:**
- ✅ `IFontBridge` interface - Platform-agnostic font abstraction
- ✅ `FontDescriptor` - Font specification (family, size, bold, italic)
- ✅ `FontMetrics` - Ascent, descent, leading, widths
- ✅ `GlyphData` - Individual character bitmap data
- ✅ `StrikeFontData` - Complete Smalltalk-80 strike font structure

**Implementations:**
- ✅ `WinFormsFontBridge` - GDI+/System.Drawing TrueType font rendering
- ✅ `GdiBitmapFontBridge` - Native GDI bitmap font support (Fixedsys)
- ✅ `NullFontBridge` - Stub implementation for headless/testing
- ✅ `FontBridgeFactory` - Factory with runtime toggle

**Fixedsys Bitmap Font Experiment (2026-01-01):**
- ✅ Native GDI P/Invoke for bitmap font loading (GDI+ cannot load .FON files)
- ✅ Direct 1bpp DIB rendering - no antialiasing artifacts
- ✅ Underscore → left-arrow conversion for Smalltalk-80 assignment operator
- ✅ Runtime toggle: `FontBridgeFactory.UseGdiBitmapFonts = true/false`
- ✅ **Currently enabled** - Provides cleaner character rendering on 1bpp display

### BitBlt Combination Rules ✅ ALL VERIFIED

| Rule | Name | Status |
|------|------|--------|
| 0 | Clear | ✅ VERIFIED |
| 1 | And | ✅ VERIFIED |
| 2 | AndReverse | ✅ VERIFIED |
| 3 | Copy | ✅ VERIFIED |
| 4 | AndInverted | ✅ VERIFIED |
| 5 | Noop | ✅ VERIFIED |
| 6 | Xor | ✅ VERIFIED |
| 7 | Or | ✅ VERIFIED |
| 8 | Nor | ✅ VERIFIED |
| 9 | Equiv | ✅ VERIFIED |
| 10 | Invert | ✅ VERIFIED |
| 11 | OrReverse | ✅ VERIFIED |
| 12 | CopyInverted | ✅ VERIFIED |
| 13 | OrInverted | ✅ VERIFIED |
| 14 | Nand | ✅ VERIFIED |
| 15 | Set | ✅ VERIFIED |

**Visual Test:** `DisplayScreen initialize` draws 4×4 grid showing all 16 rules with checkerboard source pattern over gray halftone background.

---

## Future: 32-bit Display Migration

### Goal

Migrate from 1bpp monochrome display surface to 32bpp ARGB while maintaining the Smalltalk-80 BitBlt semantics internally. This enables:

1. **Smoother font rendering** - TrueType antialiasing can work properly
2. **Color support** - Future color extensions without architectural changes
3. **Modern graphics backends** - SkiaSharp, OpenGL, Vulkan via pluggable interface
4. **Cross-platform** - Avalonia UI instead of Windows Forms

### Implementation Plan

**Step 1: Complete IDisplay Interface Integration**
- Ensure all primitives use IDisplay abstraction
- Remove any remaining IPlatformBridge references
- Clean up dead code from old architecture

**Step 2: Add 32bpp Support to WinFormsDisplay**
- Add `BitsPerPixel` property (1 or 32)
- Update `GetDisplayBuffer()` to return appropriate format
- Modify `CopyDisplayFromHeap()` for 32bpp → screen rendering

**Step 3: Update BitBltEngine for 32bpp**
- Add 32bpp pixel format support to `BitBltEngine`
- Implement all 16 combination rules for 32bpp
- Maintain 1bpp support for Smalltalk Form objects

**Step 4: Bridge 1bpp Smalltalk to 32bpp Display**
- Smalltalk Forms remain 1bpp (internal representation)
- Display surface is 32bpp
- BitBlt to Display converts 1bpp → 32bpp on the fly
- Black pixels (1) → 0xFF000000 (opaque black)
- White pixels (0) → 0xFFFFFFFF (opaque white)

**Step 5: Silk.NET GPU Backend (Optional)**
- Create `SilkNetDisplay : IDisplay` implementation
- Use Silk.NET for OpenGL/Vulkan rendering
- Enables cross-platform with GPU acceleration

### Key Design Decisions

1. **No backward compatibility shims** - Clean break from old interface
2. **Single IDisplay interface** - Replaces IPlatformBridge fragmentation
3. **1bpp Forms preserved** - Smalltalk code unchanged
4. **32bpp display surface** - Modern rendering target
5. **Format conversion at BitBlt boundary** - When dest is Display

### Files to Modify

- `Core/VM/Platform/IDisplay.cs` - Ensure complete interface
- `Platform/Windows/WinFormsDisplay.cs` - Add 32bpp mode
- `Core/VM/Graphics/BitBltEngine.cs` - Add 32bpp support
- `Core/VM/Primitives/GraphicsPrimitives.cs` - Verify IDisplay usage
- `Program.cs` - Use IDisplay consistently

---

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book) - Chapter on BitBlt
2. Ingalls, D. (1981). *Design Principles Behind Smalltalk* - Graphics system design
3. [Silk.NET](https://github.com/dotnet/Silk.NET) - Official .NET Foundation graphics library
4. [Avalonia Documentation](https://docs.avaloniaui.net/)
5. [SkiaSharp Documentation](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/)

---

**Document Status**: ✅ **Phases 1-5 Complete** - Planning Phase 6 (32bpp Migration)
**Next Steps**: Complete IDisplay integration and implement 32bpp display surface
