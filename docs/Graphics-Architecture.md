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

## Phase 6: 32-bit Display Migration ✅ IMPLEMENTED

### Goal

Migrate from 1bpp monochrome display surface to 32bpp ARGB while maintaining the Smalltalk-80 BitBlt semantics internally. This enables:

1. **Smooth halftone rendering** - Halftone patterns become actual gray values
2. **Smoother font rendering** - TrueType antialiasing can work properly
3. **Color support** - Future color extensions without architectural changes
4. **Modern graphics backends** - SkiaSharp, OpenGL, Vulkan via pluggable interface

### Implementation Status

**Step 1: IDisplay Interface Extended** ✅
- Added `BitsPerPixel` property (PixelDepth enum: OneBit, EightBit, ThirtyTwoBit)
- Added `GetDisplayBuffer32()` for direct 32bpp buffer access from primitives

**Step 2: WinFormsDisplay 32bpp Mode** ✅
- Constructor accepts `PixelDepth` parameter
- `_displayBuffer` sized appropriately for selected depth
- `CopyDisplayFromHeap32bpp()` converts 1bpp heap → 32bpp display buffer
- `CopyBufferToBitmap32bpp()` copies 32bpp buffer directly to screen

**Step 3: BitBltEngine32 Created** ✅
- New `BitBltEngine32.cs` with 32bpp rendering support
- All 16 combination rules implemented for 32bpp pixels
- Halftone-to-gray conversion: pattern density → solid ARGB gray color
- Color operations: AND, OR, XOR work on RGB components with alpha preserved

**Step 4: Primitives Updated** ✅
- Primitive 96 (copyBits): When dest=Display + 32bpp mode, also calls BitBltEngine32
- Primitive 104 (drawLoopX:Y:): Line drawing to 32bpp buffer with halftone-to-gray

**Step 5: Program.cs Configuration** ✅
- Experimental toggle: `PixelDepth.ThirtyTwoBit` for 32bpp mode
- Falls back to `PixelDepth.OneBit` for traditional monochrome

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│         Smalltalk-80 Code (Form, BitBlt, Halftones)         │
│                      (All 1bpp internally)                   │
├──────────────────────────────────────────────────────────────┤
│  Primitive 96/104 → BitBltEngine (1bpp to heap)             │
│                   ↓ (if dest=Display && 32bpp mode)         │
│                   → BitBltEngine32 (halftone→gray to buffer) │
├──────────────────────────────────────────────────────────────┤
│     WinFormsDisplay (32bpp _displayBuffer)                   │
├──────────────────────────────────────────────────────────────┤
│        32bpp back buffer → Window rendering                  │
└──────────────────────────────────────────────────────────────┘
```

### Halftone-to-Gray Conversion

When a halftone pattern is used in 32bpp mode, instead of rendering as a dithered pattern, it is converted to an equivalent gray color:

| Halftone Pattern | Density | ARGB Gray Color |
|------------------|---------|-----------------|
| Form white       | 0%      | 0xFFFFFFFF      |
| Form veryLightGray| ~12%   | 0xFFE0E0E0      |
| Form lightGray   | ~25%    | 0xFFC0C0C0      |
| Form gray        | 50%     | 0xFF808080      |
| Form darkGray    | ~75%    | 0xFF404040      |
| Form veryDarkGray| ~87%   | 0xFF202020      |
| Form black       | 100%    | 0xFF000000      |

### Files Modified

- `Core/VM/Platform/IDisplay.cs` - Added BitsPerPixel, GetDisplayBuffer32()
- `Core/VM/Graphics/BitBltEngine32.cs` - **NEW** - 32bpp BitBlt engine
- `Platform/Windows/WinFormsDisplay.cs` - 32bpp buffer support
- `Platform/DisplaySystemFactory.cs` - NullDisplay 32bpp support
- `Core/VM/Primitives/GraphicsPrimitives.cs` - 32bpp path in primitives 96/104
- `Program.cs` - Experimental 32bpp toggle

### Future Work

**Step 6: Silk.NET GPU Backend (Optional)**
- Create `SilkNetDisplay : IDisplay` implementation
- Use Silk.NET for OpenGL/Vulkan rendering
- Enables cross-platform with GPU acceleration

---

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book) - Chapter on BitBlt
2. Ingalls, D. (1981). *Design Principles Behind Smalltalk* - Graphics system design
3. [Silk.NET](https://github.com/dotnet/Silk.NET) - Official .NET Foundation graphics library
4. [Avalonia Documentation](https://docs.avaloniaui.net/)
5. [SkiaSharp Documentation](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/)

---

---

## Future Work: Native Form Depth Support

The current implementation intercepts primitives at the C#/display boundary to convert 1bpp Smalltalk Forms to 32bpp rendering. The next phase would add native multi-depth Form support in Smalltalk itself.

### Why Native Depth Support?

**Current Limitation**: Intermediate Forms (window backing stores, cached images) remain 1bpp internally. Only the final Display composite gets halftone-to-gray conversion. True smooth rendering requires Forms to natively support 32bpp.

### Smalltalk Form Class Changes

Add `depth` instance variable to Form:
```smalltalk
Form instanceVariableNames: 'bits width height offset depth'
```

**Critical Methods Requiring Changes** (47+ locations audited):

| Class | Methods | Severity | Summary |
|-------|---------|----------|---------|
| Form | 17 methods + 6 constants | CRITICAL | Bitmap allocation uses `(width + 15) // 16` formula |
| BitBlt | 3 methods | CRITICAL | Primitive 96 must dispatch by depth |
| DisplayScreen | 2 methods | CRITICAL | Display creation/initialization |
| Cursor | 2 methods | HIGH | Keep 1bpp, convert in primitive 101 |
| DisplayMedium | 6 methods | MEDIUM | Convenience fill methods use halftone masks |
| StrikeFont | 3 methods | MEDIUM | Keep fonts 1bpp, convert when rendering |
| CharacterScanner | 3 methods | MEDIUM | Text color application |
| Pen | 4 methods | LOW | Color property for pen drawing |

**Key Formula Change**:
```smalltalk
"Current 1bpp allocation"
bits _ WordArray new: (width + 15) // 16 * height.

"Multi-depth allocation"
Form>>wordsPerLine
    depth = 32 ifTrue: [^width * 2].  "2 words per 32-bit pixel"
    ^(width + 15) // 16  "1bpp default"
```

### Color Class Design

```smalltalk
Object subclass: #Color
    instanceVariableNames: 'red green blue alpha'
    classVariableNames: 'Black White Gray'

Color>>asARGB
    "Answer 32-bit ARGB integer"
    ^(alpha << 24) + (red << 16) + (green << 8) + blue

Color>>asForm32
    "Answer 1x1 32bpp Form with this color"
    | form |
    form _ Form extent: 1@1 depth: 32.
    form pixelAt: 0@0 put: self asARGB.
    ^form

Color>>asHalftone
    "Answer halftone approximating this color for 1bpp compatibility"
    | brightness |
    brightness _ (red + green + blue) / 3.0 / 255.0.
    brightness < 0.5 ifTrue: [^Form gray].
    ^Form white
```

### Primitive 96 Depth-Aware Dispatch

```csharp
// Dispatch based on Form depth combination
(sourceDepth, destDepth) switch
{
    (1, 1)   => BitBltEngine.CopyBits(...),       // Current 1bpp engine
    (32, 32) => BitBltEngine32.CopyBits32(...),   // 32bpp pixel ops
    (1, 32)  => BitBltEngine32.CopyBits1To32(...),// Expand bits to pixels
    (32, 1)  => BitBltEngine32.CopyBits32To1(...),// Threshold/dither
};
```

### Implementation Phases

| Phase | Scope | Complexity |
|-------|-------|------------|
| 1. Form depth ivar | Add depth to Form, update allocation | Medium |
| 2. DisplayScreen 32bpp | 32bpp Display creation | Low |
| 3. Color class | New Color class with ARGB | Low |
| 4. Primitive depth dispatch | Update primitives 96, 102, 103, 104 | Medium |
| 5. Widget updates | DisplayMedium, Pen color support | Medium |
| 6. GPU Backend | Silk.NET OpenGL/Vulkan implementation | High |

---

## References

1. Goldberg, A., & Robson, D. (1983). *Smalltalk-80: The Language and its Implementation* (Blue Book) - Chapter on BitBlt
2. Ingalls, D. (1981). *Design Principles Behind Smalltalk* - Graphics system design
3. [Silk.NET](https://github.com/dotnet/Silk.NET) - Official .NET Foundation graphics library

---

**Document Status**: ✅ **Phases 1-6 Complete** - 32bpp display with halftone-to-gray implemented
**Next Steps**: Test 32bpp mode, implement native Form depth support, GPU backends via IDisplay abstraction
